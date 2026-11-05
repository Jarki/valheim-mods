using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using BepInEx;
using HarmonyLib;

namespace ValheimServerMetrics;

[BepInPlugin(Guid, "Valheim Server Metrics", ExporterVersion)]
[BepInProcess("valheim_server.exe")]
[BepInProcess("valheim_server.x86_64")]
public sealed class Plugin : BaseUnityPlugin
{
    internal const string Guid = "local.valheim.servermetrics";
    internal const string ExporterVersion = "1.4.0";
    private static Plugin _instance;
    private static readonly System.Reflection.FieldInfo ServerNameField =
        AccessTools.Field(typeof(ZNet), "m_ServerName")
        ?? throw new MissingFieldException(typeof(ZNet).FullName, "m_ServerName");
    private readonly HashSet<ZNetPeer> _readyPeers = new();
    private readonly List<ZNetPeer> _departedPeers = new();
    private readonly List<CharacterPresence> _characterPresence = new();
    private readonly Dictionary<long, string> _characterKeys = new();
    private CharacterLeaderboard _leaderboard;
    private DeathObserver _deathObserver;
    private Exception _reportedStorageError;
    private float _nextLeaderboardRefresh;
    private MetricsState _metrics;
    private MetricsHttpServer _http;
    private BackupInventoryMonitor _backupInventory;
    private ModpackManifestMonitor _modpackManifest;
    private Harmony _harmony;
    private string _gameVersion;
    private string _endpoint;
    private float _nextRefresh;
    private float _nextMetadataRefresh;
    private int _acceptEvents;
    private int _faulted;

    private void Awake()
    {
        var bind = Config.Bind("Exporter", "BindAddress", "127.0.0.1",
            "Explicit local IP address. No wildcard addresses. Non-loopback binding requires AllowedScraperAddress. The endpoint has no authentication or TLS.");
        var port = Config.Bind("Exporter", "Port", 9101,
            new BepInEx.Configuration.ConfigDescription("Prometheus HTTP port; GET /metrics. Restart required after changing this setting.",
                new BepInEx.Configuration.AcceptableValueRange<int>(1, 65535)));
        var scraper = Config.Bind("Exporter", "AllowedScraperAddress", "",
            "Only this source IP may scrape metrics. Empty permits loopback clients only and requires a loopback bind. Uses the actual peer IP, not proxy headers. Restart required.");
        var modpackManifestPath = Config.Bind("Exporter", "ModpackManifestPath", "",
            "Optional absolute path to the authoritative modpack manifest. Empty disables manifest metrics and the background reader.");
        var characterPlaytimePath = Config.Bind("Exporter", "CharacterPlaytimePath",
            Path.Combine(Paths.BepInExRootPath, "data", "ServerMetrics", "character-playtime.json"),
            "Persistent character-name connected-time ledger. No Steam identity is collected. Keep outside world saves; restart required after changing this path.");
        try
        {
            _gameVersion = global::Version.CurrentVersion.ToString();
            _metrics = new MetricsState(ExporterVersion);
            _leaderboard = new CharacterLeaderboard(characterPlaytimePath.Value);
            _deathObserver = new DeathObserver(_leaderboard, OnDeathObserved, DisableTelemetry);
            _metrics.SetCharacters(_leaderboard.Snapshot(), _leaderboard.PersistenceError == null);
            _http = new MetricsHttpServer(_metrics, bind.Value, port.Value, scraper.Value);
            _endpoint = $"{bind.Value}:{port.Value}/metrics";
            _instance = this;
            _harmony = new Harmony(Guid);
            _harmony.PatchAll(typeof(Plugin).Assembly);
            _backupInventory = new BackupInventoryMonitor(_metrics, message => Logger.LogWarning(message));
            if (!string.IsNullOrWhiteSpace(modpackManifestPath.Value))
                _modpackManifest = new ModpackManifestMonitor(_metrics, modpackManifestPath.Value,
                    message => Logger.LogWarning(message));
            Volatile.Write(ref _acceptEvents, 1);
            StartHttpWhenDue(UnityEngine.Time.realtimeSinceStartup);
        }
        catch (Exception error)
        {
            DisableTelemetry(error);
        }
    }

    private void Update()
    {
        if (Volatile.Read(ref _acceptEvents) == 0) return;
        var now = UnityEngine.Time.realtimeSinceStartup;
        if (now < _nextRefresh) return;
        _nextRefresh = now + 1f;
        try
        {
            RefreshPlayers(ZNet.instance);
            StartHttpWhenDue(now);
        }
        catch (Exception error) { DisableTelemetry(error); }
    }

    private void StartHttpWhenDue(double now)
    {
        if (_http.StartWhenDue(now, out var failure, out var retryDelay))
            Logger.LogInfo($"Server Metrics listening on {_endpoint}; aggregate counters reset only on plugin restart.");
        else if (failure != null)
            Logger.LogWarning($"Server Metrics could not bind {_endpoint}; retrying in {retryDelay / 60:g} minute(s). Telemetry collection continues. {failure}");
    }

    private void RefreshPlayers(ZNet znet)
    {
        if (znet == null || !znet.IsServer() || !znet.IsDedicated()) return;
        var added = 0;
        var count = 0;
        var peers = znet.GetPeers();
        _characterPresence.Clear();
        var changed = false;
        _departedPeers.Clear();
        foreach (var previous in _readyPeers)
        {
            if (peers == null || !previous.IsReady() || !peers.Contains(previous))
                _departedPeers.Add(previous);
        }
        for (var i = 0; i < _departedPeers.Count; i++)
        {
            _readyPeers.Remove(_departedPeers[i]);
            changed = true;
        }
        if (peers != null)
        {
            for (var i = 0; i < peers.Count; i++)
            {
                var peer = peers[i];
                if (peer == null || !peer.IsReady()) continue;
                count++;
                if (_readyPeers.Add(peer)) { added++; changed = true; }
                // PlayerID is the saved character profile ID, not the network session
                // UID, Steam account, or transient character-object ZDOID.
                if (peer.m_playerID == 0 || string.IsNullOrEmpty(peer.m_playerName)) continue;
                if (!_characterKeys.TryGetValue(peer.m_playerID, out var key))
                {
                    key = peer.m_playerID.ToString("x16", CultureInfo.InvariantCulture);
                    _characterKeys.Add(peer.m_playerID, key);
                    changed = true;
                }
                _characterPresence.Add(new CharacterPresence(key, peer.m_playerName));
            }
            RefreshMetadata(znet);
        }
        _metrics.ObservePlayers(count, added);
        _leaderboard.Observe(_characterPresence);
        PublishCharacters(changed);
    }

    private void PublishCharacters(bool force)
    {
        var error = _leaderboard.PersistenceError;
        if (!ReferenceEquals(error, _reportedStorageError))
        {
            if (error != null) Logger.LogError($"Character playtime persistence failed; current totals are in memory only: {error}");
            else if (_reportedStorageError != null) Logger.LogInfo("Character playtime persistence recovered.");
            _reportedStorageError = error;
            force = true;
        }
        var now = UnityEngine.Time.realtimeSinceStartup;
        if (!force && now < _nextLeaderboardRefresh) return;
        _nextLeaderboardRefresh = now + 15f;
        _metrics.SetCharacters(_leaderboard.Snapshot(), error == null);
    }

    private void OnDeathObserved() => PublishCharacters(true);

    private void StopLeaderboard()
    {
        _deathObserver?.Stop();
        _deathObserver = null;
        var leaderboard = Interlocked.Exchange(ref _leaderboard, null);
        try { leaderboard?.Dispose(); }
        catch (Exception error) { Logger.LogError($"Character playtime could not be saved during shutdown: {error}"); }
    }

    private void RefreshMetadata(ZNet znet)
    {
        var now = UnityEngine.Time.realtimeSinceStartup;
        if (now < _nextMetadataRefresh) return;
        var world = znet.GetWorld();
        if (world == null) return;
        _nextMetadataRefresh = now + 60f;
        _metrics.SetWorld(znet.GetWorldName(), _gameVersion, (string)ServerNameField.GetValue(null));

        var registry = BepInEx.Bootstrap.Chainloader.PluginInfos;
        var mods = new List<ModEntry>(registry.Count);
        foreach (var entry in registry)
        {
            var info = entry.Value;
            if (info.Instance == null) continue;
            mods.Add(new ModEntry(info.Metadata.GUID, info.Metadata.Name, info.Metadata.Version.ToString()));
        }
        _metrics.SetMods(mods.ToArray());
        _backupInventory.SetWorldDirectory(
            world.m_fileSource == FileHelpers.FileSource.Local && world.IsChunkedSave()
                ? world.GetSaveDirectory(world.m_fileSource)
                : null);
    }

    private static void OnPeerChange(ZNet znet)
    {
        var instance = Volatile.Read(ref _instance);
        if (instance == null || Volatile.Read(ref instance._acceptEvents) == 0) return;
        try { instance.RefreshPlayers(znet); }
        catch (Exception error) { instance.DisableTelemetry(error); }
    }

    // A telemetry failure must neither interrupt the game nor leave apparently healthy,
    // stale metrics available. Stop exporting and log once; never suppress game exceptions.
    private void DisableTelemetry(Exception error)
    {
        Volatile.Write(ref _acceptEvents, 0);
        StopLeaderboard();
        try { _backupInventory?.Dispose(); } catch { }
        try { _modpackManifest?.Dispose(); } catch { }
        try { _http?.Dispose(); } catch { }
        if (Interlocked.Exchange(ref _faulted, 1) == 0)
            Logger.LogError($"Server Metrics disabled: {error}");
    }

    private void OnDestroy()
    {
        Volatile.Write(ref _acceptEvents, 0);
        if (ReferenceEquals(Volatile.Read(ref _instance), this)) Volatile.Write(ref _instance, null);
        StopLeaderboard();
        try { _backupInventory?.Dispose(); } catch { }
        try { _modpackManifest?.Dispose(); } catch { }
        try { _http?.Dispose(); }
        finally { _harmony?.UnpatchSelf(); }
        _readyPeers.Clear();
        _departedPeers.Clear();
    }

    [HarmonyPatch(typeof(ZNet), "RPC_PeerInfo")]
    private static class PeerAcceptedPatch
    {
        [HarmonyPostfix]
        private static void Postfix(ZNet __instance) => OnPeerChange(__instance);
    }

    [HarmonyPatch(typeof(ZNet), "RPC_PlayerID")]
    private static class CharacterIdentifiedPatch
    {
        [HarmonyPostfix]
        private static void Postfix(ZNet __instance) => OnPeerChange(__instance);
    }

    [HarmonyPatch(typeof(ZRoutedRpc), "RPC_RoutedRPC")]
    private static class PlayerDeathObservedPatch
    {
        [HarmonyPrefix]
        private static void Prefix(ZRpc rpc, ZPackage pkg)
        {
            var instance = Volatile.Read(ref _instance);
            if (instance == null || Volatile.Read(ref instance._acceptEvents) == 0) return;
            try { instance._deathObserver?.Observe(rpc, pkg); }
            catch (Exception error) { instance.DisableTelemetry(error); }
        }
    }

    [HarmonyPatch(typeof(ZNet), nameof(ZNet.Disconnect))]
    private static class PeerDisconnectedPatch
    {
        // The original removes the peer; sampling in a prefix would leave a stale +1.
        [HarmonyPostfix]
        private static void Postfix(ZNet __instance) => OnPeerChange(__instance);
    }

    [HarmonyPatch(typeof(ZNet), "StopAll")]
    private static class NetworkStoppedPatch
    {
        // Shutdown clears the peer list directly, without calling Disconnect.
        [HarmonyPostfix]
        private static void Postfix(ZNet __instance) => OnPeerChange(__instance);
    }

    [HarmonyPatch(typeof(ZNet), "OnDestroy")]
    private static class NetworkDestroyedPatch
    {
        [HarmonyPostfix]
        private static void Postfix()
        {
            var instance = Volatile.Read(ref _instance);
            if (instance == null || Volatile.Read(ref instance._acceptEvents) == 0) return;
            try
            {
                instance._readyPeers.Clear();
                instance._metrics.ObservePlayers(0, 0);
                instance._characterPresence.Clear();
                instance._leaderboard.Observe(instance._characterPresence);
                instance.PublishCharacters(true);
                instance._backupInventory?.SetWorldDirectory(null);
                instance._metrics.SetWorld(null, null, null);
                instance._nextMetadataRefresh = 0f;
            }
            catch (Exception error) { instance.DisableTelemetry(error); }
        }
    }

    [ThreadStatic] private static MetricsState _saveMetrics;
    [ThreadStatic] private static bool _saveCommitted;

    private struct SaveAttempt
    {
        internal MetricsState Metrics;
        internal long Started;
    }

    [HarmonyPatch(typeof(ZNet), "SaveWorldThread")]
    private static class WorldSavePatch
    {
        [HarmonyPrefix]
        private static void Prefix(out SaveAttempt __state)
        {
            __state = default;
            var instance = Volatile.Read(ref _instance);
            if (instance == null || Volatile.Read(ref instance._acceptEvents) == 0) return;
            try
            {
                _saveCommitted = false;
                _saveMetrics = instance._metrics;
                __state = new SaveAttempt { Metrics = _saveMetrics, Started = Stopwatch.GetTimestamp() };
                _saveMetrics.BeginWorldSave();
            }
            catch (Exception error)
            {
                _saveMetrics = null;
                __state = default;
                instance.DisableTelemetry(error);
            }
        }

        [HarmonyFinalizer]
        private static Exception Finalizer(Exception __exception, SaveAttempt __state)
        {
            var success = _saveCommitted && __exception == null;
            _saveMetrics = null;
            _saveCommitted = false;
            if (__state.Metrics != null)
            {
                try
                {
                    var elapsed = (Stopwatch.GetTimestamp() - __state.Started) / (double)Stopwatch.Frequency;
                    __state.Metrics.CompleteWorldSave(success, elapsed);
                    Volatile.Read(ref _instance)?._backupInventory?.RequestRefresh();
                }
                catch (Exception error) { Volatile.Read(ref _instance)?.DisableTelemetry(error); }
            }
            return __exception;
        }
    }

    [HarmonyPatch(typeof(SaveSystem), nameof(SaveSystem.EndSave))]
    private static class WorldCommitPatch
    {
        [HarmonyPostfix]
        private static void Postfix(bool writeOK)
        {
            // EndSave is shared by game code. Only an active world-save worker belongs
            // to our observation; never call Unity APIs from this background thread.
            if (_saveMetrics != null) _saveCommitted = writeOK;
        }
    }

    [HarmonyPatch(typeof(ZNet), nameof(ZNet.ConsiderAutoBackup))]
    private static class AutoBackupPatch
    {
        [HarmonyPostfix]
        private static void Postfix(SaveDataType dataType, bool __result)
        {
            if (!__result || dataType != SaveDataType.World) return;
            var instance = Volatile.Read(ref _instance);
            if (instance == null || Volatile.Read(ref instance._acceptEvents) == 0) return;
            try
            {
                instance._metrics.RecordAutoBackup();
                instance._backupInventory?.RequestRefresh();
            }
            catch (Exception error) { instance.DisableTelemetry(error); }
        }
    }
}
