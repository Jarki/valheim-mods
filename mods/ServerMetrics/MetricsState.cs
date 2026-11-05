using System;
using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace ValheimServerMetrics
{
    internal readonly struct ModEntry
    {
        internal ModEntry(string guid, string name, string version)
        {
            Guid = guid;
            Name = name;
            Version = version;
        }

        internal string Guid { get; }
        internal string Name { get; }
        internal string Version { get; }
    }

    internal readonly struct BackupEntry
    {
        internal BackupEntry(string name, double? createdUnixSeconds, double? dataUnixSeconds)
        {
            Name = name;
            CreatedUnixSeconds = createdUnixSeconds;
            DataUnixSeconds = dataUnixSeconds;
        }

        internal string Name { get; }
        internal double? CreatedUnixSeconds { get; }
        internal double? DataUnixSeconds { get; }
    }

    internal sealed class MetricsState
    {
        private const int MaximumLabelLength = 256;
        private const int MaximumBackups = 128;
        private const int MaximumMods = 128;
        private const int MaximumSnapshotLength = 1024 * 1024;
        private readonly object sync = new object();
        private readonly string exporterVersion;
        private readonly Func<double> monotonicSeconds;
        private readonly Func<double> unixSeconds;
        private readonly double startedAt;

        private string worldName;
        private string gameVersion;
        private string serverName;
        private ModEntry[] mods = Array.Empty<ModEntry>();
        private BackupEntry[] backups = Array.Empty<BackupEntry>();
        private ModpackPackageEntry[] modpackPackages = Array.Empty<ModpackPackageEntry>();
        private string modpackProfileName;
        private string modpackProfileCode;
        private bool hasModpackManifestStatus;
        private bool modpackManifestSuccess;
        private bool hasBackupInventoryStatus;
        private bool backupInventorySuccess;
        private bool hasBackupInventoryTimestamp;
        private double backupInventoryTimestamp;
        private bool hasLiveSaveTimestamp;
        private double liveSaveTimestamp;
        private int connectedPlayers;
        private long sessions;
        private double connectedSeconds;
        private double lastPlayerIntegration;
        private bool hasGameUpdate;
        private double lastGameUpdate;
        private long saveAttempts;
        private long successfulSaves;
        private long failedSaves;
        private int savesInProgress;
        private bool hasWorkerDuration;
        private double workerDuration;
        private bool hasLastSave;
        private double lastSave;
        private long autoBackups;
        private bool hasLastAutoBackup;
        private double lastAutoBackup;
        private CharacterLeaderboardEntry[] characters = Array.Empty<CharacterLeaderboardEntry>();
        private bool hasCharacterLeaderboard;
        private bool characterStorageSuccess;
        private byte[] cachedSnapshot;
        private double cachedAt = double.NegativeInfinity;

        internal MetricsState(
            string exporterVersion,
            Func<double> monotonicSeconds = null,
            Func<double> unixSeconds = null)
        {
            this.exporterVersion = ValidateLabel(exporterVersion, nameof(exporterVersion), false);
            this.monotonicSeconds = monotonicSeconds ?? DefaultMonotonicSeconds;
            this.unixSeconds = unixSeconds ?? DefaultUnixSeconds;
            startedAt = ReadFiniteClock(this.monotonicSeconds, nameof(monotonicSeconds));
            lastPlayerIntegration = startedAt;
        }

        internal void SetWorld(string worldName, string gameVersion, string serverName)
        {
            string validatedWorld = ValidateLabel(worldName, nameof(worldName), true);
            string validatedGameVersion = validatedWorld == null
                ? null
                : ValidateLabel(gameVersion, nameof(gameVersion), false);
            string validatedServer = validatedWorld == null
                ? null
                : ValidateLabel(serverName, nameof(serverName), false);

            lock (sync)
            {
                if (string.Equals(this.worldName, validatedWorld, StringComparison.Ordinal) &&
                    string.Equals(this.gameVersion, validatedGameVersion, StringComparison.Ordinal) &&
                    string.Equals(this.serverName, validatedServer, StringComparison.Ordinal))
                    return;
                this.worldName = validatedWorld;
                this.gameVersion = validatedGameVersion;
                this.serverName = validatedServer;
                InvalidateSnapshot();
            }
        }

        internal void SetMods(ModEntry[] mods)
        {
            if (mods == null)
                throw new ArgumentNullException(nameof(mods));
            if (mods.Length > MaximumMods)
                throw new ArgumentOutOfRangeException(nameof(mods), "At most 128 mods can be reported.");

            var replacement = new ModEntry[mods.Length];
            var identities = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < mods.Length; index++)
            {
                ModEntry mod = mods[index];
                string guid = ValidateLabel(mod.Guid, nameof(mods), false);
                string name = ValidateLabel(mod.Name, nameof(mods), false);
                string version = ValidateLabel(mod.Version, nameof(mods), false);
                if (!identities.Add(guid))
                    throw new ArgumentException("Mod GUIDs must be unique.", nameof(mods));
                replacement[index] = new ModEntry(guid, name, version);
            }
            Array.Sort(replacement, (left, right) => StringComparer.Ordinal.Compare(left.Guid, right.Guid));

            lock (sync)
            {
                this.mods = replacement;
                InvalidateSnapshot();
            }
        }

        // Snapshot ownership transfers to this state; the tracker never mutates it.
        internal void SetCharacters(CharacterLeaderboardEntry[] snapshot, bool storageSuccess)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (snapshot.Length > 256)
                throw new ArgumentOutOfRangeException(nameof(snapshot), "At most 256 characters can be reported.");
            var identities = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in snapshot)
            {
                ValidateLabel(entry.Id, nameof(snapshot), false);
                ValidateLabel(entry.CharacterName, nameof(snapshot), false);
                if (!identities.Add(entry.Id) || !IsNonnegativeFinite(entry.AllTimeSeconds) ||
                    !IsNonnegativeFinite(entry.LastSevenDaysSeconds) || entry.LastSevenDaysSeconds > entry.AllTimeSeconds ||
                    entry.Sessions < 0 || entry.Deaths < 0 || entry.LastSeenUnixSeconds < 0)
                    throw new ArgumentException("Invalid character leaderboard entry.", nameof(snapshot));
            }
            lock (sync)
            {
                characters = snapshot;
                characterStorageSuccess = storageSuccess;
                hasCharacterLeaderboard = true;
                InvalidateSnapshot();
            }
        }


        private static bool IsNonnegativeFinite(double value)
            => value >= 0 && !double.IsNaN(value) && !double.IsInfinity(value);

        internal void SetBackupInventory(BackupEntry[] backups, double? liveSaveUnixSeconds)
        {
            if (backups == null)
                throw new ArgumentNullException(nameof(backups));
            if (backups.Length > MaximumBackups)
                throw new ArgumentOutOfRangeException(nameof(backups), "At most 128 backups can be reported.");
            ValidateOptionalTimestamp(liveSaveUnixSeconds, nameof(liveSaveUnixSeconds));

            var replacement = new BackupEntry[backups.Length];
            var identities = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < backups.Length; index++)
            {
                BackupEntry backup = backups[index];
                string name = ValidateLabel(backup.Name, nameof(backups), false);
                ValidateOptionalTimestamp(backup.CreatedUnixSeconds, nameof(backups));
                ValidateOptionalTimestamp(backup.DataUnixSeconds, nameof(backups));
                if (!identities.Add(name))
                    throw new ArgumentException("Backup names must be unique.", nameof(backups));
                replacement[index] = new BackupEntry(name, backup.CreatedUnixSeconds, backup.DataUnixSeconds);
            }
            Array.Sort(replacement, (left, right) => StringComparer.Ordinal.Compare(left.Name, right.Name));
            double timestamp = ReadFiniteClock(unixSeconds, nameof(unixSeconds));
            if (timestamp < 0.0)
                throw new InvalidOperationException("unixSeconds returned a negative value.");

            lock (sync)
            {
                this.backups = replacement;
                hasLiveSaveTimestamp = liveSaveUnixSeconds.HasValue;
                liveSaveTimestamp = liveSaveUnixSeconds.GetValueOrDefault();
                hasBackupInventoryStatus = true;
                backupInventorySuccess = true;
                hasBackupInventoryTimestamp = true;
                backupInventoryTimestamp = timestamp;
                InvalidateSnapshot();
            }
        }

        internal void MarkBackupInventoryUnavailable()
        {
            lock (sync)
            {
                backups = Array.Empty<BackupEntry>();
                hasLiveSaveTimestamp = false;
                hasBackupInventoryStatus = true;
                backupInventorySuccess = false;
                InvalidateSnapshot();
            }
        }

        internal void SetModpackManifest(string profileName, string profileCode, ModpackPackageEntry[] packages)
        {
            string validatedProfileName = ValidateLabel(profileName, nameof(profileName), false);
            string validatedProfileCode = ValidateLabel(profileCode, nameof(profileCode), true);
            if (packages == null)
                throw new ArgumentNullException(nameof(packages));
            if (packages.Length == 0)
                throw new ArgumentOutOfRangeException(nameof(packages), "At least one package must be reported.");
            if (packages.Length > MaximumMods)
                throw new ArgumentOutOfRangeException(nameof(packages), "At most 128 packages can be reported.");

            var replacement = new ModpackPackageEntry[packages.Length];
            var identities = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < packages.Length; index++)
            {
                ModpackPackageEntry package = packages[index];
                string packageNamespace = ValidatePackageIdentity(package.Namespace, nameof(packages));
                string name = ValidatePackageIdentity(package.Name, nameof(packages));
                string version = ValidatePackageVersion(package.Version, nameof(packages));
                string source = ValidateLabel(package.Source, nameof(packages), false);
                if (!string.Equals(source, "thunderstore", StringComparison.Ordinal) &&
                    !string.Equals(source, "local", StringComparison.Ordinal))
                    throw new ArgumentException("Package sources must be thunderstore or local.", nameof(packages));
                if (string.Equals(source, "local", StringComparison.Ordinal) && package.Client)
                    throw new ArgumentException("Local packages cannot be client packages.", nameof(packages));
                if (!package.Client && !package.Server)
                    throw new ArgumentException("Each package must have a client or server placement.", nameof(packages));
                if (!identities.Add(packageNamespace + "\0" + name))
                    throw new ArgumentException("Package identities must be unique.", nameof(packages));
                replacement[index] = new ModpackPackageEntry(packageNamespace, name, version, source, package.Client, package.Server);
            }
            Array.Sort(replacement, (left, right) =>
            {
                int order = StringComparer.Ordinal.Compare(left.Namespace, right.Namespace);
                return order != 0 ? order : StringComparer.Ordinal.Compare(left.Name, right.Name);
            });

            lock (sync)
            {
                modpackProfileName = validatedProfileName;
                modpackProfileCode = validatedProfileCode;
                modpackPackages = replacement;
                hasModpackManifestStatus = true;
                modpackManifestSuccess = true;
                InvalidateSnapshot();
            }
        }

        internal void MarkModpackManifestUnavailable()
        {
            lock (sync)
            {
                modpackProfileName = null;
                modpackProfileCode = null;
                modpackPackages = Array.Empty<ModpackPackageEntry>();
                hasModpackManifestStatus = true;
                modpackManifestSuccess = false;
                InvalidateSnapshot();
            }
        }

        internal void ObservePlayers(int readyPlayers, int newSessions)
        {
            if (readyPlayers < 0)
                throw new ArgumentOutOfRangeException(nameof(readyPlayers));
            if (newSessions < 0)
                throw new ArgumentOutOfRangeException(nameof(newSessions));

            double now = ReadFiniteClock(monotonicSeconds, nameof(monotonicSeconds));
            double timestamp = ReadFiniteClock(unixSeconds, nameof(unixSeconds));
            lock (sync)
            {
                IntegratePlayers(now);
                connectedPlayers = readyPlayers;
                sessions = checked(sessions + newSessions);
                hasGameUpdate = true;
                lastGameUpdate = timestamp;
                InvalidateSnapshot();
            }
        }

        internal void BeginWorldSave()
        {
            lock (sync)
            {
                saveAttempts = checked(saveAttempts + 1);
                savesInProgress = checked(savesInProgress + 1);
                InvalidateSnapshot();
            }
        }

        internal void CompleteWorldSave(bool success, double workerDurationSeconds)
        {
            if (!IsFiniteNonnegative(workerDurationSeconds))
                throw new ArgumentOutOfRangeException(nameof(workerDurationSeconds));

            double completionTimestamp = success
                ? ReadFiniteClock(unixSeconds, nameof(unixSeconds))
                : 0.0;

            lock (sync)
            {
                if (success)
                {
                    successfulSaves = checked(successfulSaves + 1);
                    hasLastSave = true;
                    lastSave = completionTimestamp;
                }
                else
                {
                    failedSaves = checked(failedSaves + 1);
                }

                if (savesInProgress > 0)
                    savesInProgress--;
                hasWorkerDuration = true;
                workerDuration = workerDurationSeconds;
                InvalidateSnapshot();
            }
        }

        internal void RecordAutoBackup()
        {
            double timestamp = ReadFiniteClock(unixSeconds, nameof(unixSeconds));
            lock (sync)
            {
                autoBackups = checked(autoBackups + 1);
                hasLastAutoBackup = true;
                lastAutoBackup = timestamp;
                InvalidateSnapshot();
            }
        }

        internal byte[] GetSnapshot()
        {
            double now = ReadFiniteClock(monotonicSeconds, nameof(monotonicSeconds));
            lock (sync)
            {
                if (cachedSnapshot != null && now >= cachedAt && now - cachedAt < 1.0)
                    return cachedSnapshot;

                double currentConnectedSeconds = connectedSeconds +
                    Math.Max(0.0, now - lastPlayerIntegration) * connectedPlayers;
                double uptime = Math.Max(0.0, now - startedAt);
                cachedSnapshot = RenderSnapshot(currentConnectedSeconds, uptime);
                cachedAt = now;
                return cachedSnapshot;
            }
        }

        private byte[] RenderSnapshot(double currentConnectedSeconds, double uptime)
        {
            var text = new StringBuilder(2048);
            if (worldName != null)
            {
                AppendType(text, "valheim_server_info", "gauge");
                text.Append("valheim_server_info{world=\"");
                AppendEscapedLabel(text, worldName);
                text.Append("\",game_version=\"");
                AppendEscapedLabel(text, gameVersion);
                text.Append("\",server=\"");
                AppendEscapedLabel(text, serverName);
                text.Append("\",exporter_version=\"");
                AppendEscapedLabel(text, exporterVersion);
                text.Append("\"} 1\n");
            }

            if (hasGameUpdate)
            {
                AppendType(text, "valheim_connected_players", "gauge");
                AppendMetric(text, "valheim_connected_players", connectedPlayers);
            }
            AppendType(text, "valheim_sessions_total", "counter");
            AppendMetric(text, "valheim_sessions_total", sessions);
            AppendType(text, "valheim_player_connected_seconds_total", "counter");
            AppendMetric(text, "valheim_player_connected_seconds_total", currentConnectedSeconds);
            AppendType(text, "valheim_exporter_uptime_seconds", "gauge");
            AppendMetric(text, "valheim_exporter_uptime_seconds", uptime);
            if (hasGameUpdate)
            {
                AppendType(text, "valheim_exporter_last_game_update_timestamp_seconds", "gauge");
                AppendMetric(text, "valheim_exporter_last_game_update_timestamp_seconds", lastGameUpdate);
            }
            AppendType(text, "valheim_world_save_attempts_total", "counter");
            AppendMetric(text, "valheim_world_save_attempts_total", saveAttempts);
            AppendType(text, "valheim_world_saves_total", "counter");
            text.Append("valheim_world_saves_total{result=\"success\"} ").Append(successfulSaves.ToString(CultureInfo.InvariantCulture)).Append('\n');
            text.Append("valheim_world_saves_total{result=\"failure\"} ").Append(failedSaves.ToString(CultureInfo.InvariantCulture)).Append('\n');
            AppendType(text, "valheim_world_save_in_progress", "gauge");
            AppendMetric(text, "valheim_world_save_in_progress", savesInProgress);
            if (hasWorkerDuration)
            {
                AppendType(text, "valheim_world_save_worker_duration_seconds", "gauge");
                AppendMetric(text, "valheim_world_save_worker_duration_seconds", workerDuration);
            }
            if (hasLastSave)
            {
                AppendType(text, "valheim_world_last_save_timestamp_seconds", "gauge");
                AppendMetric(text, "valheim_world_last_save_timestamp_seconds", lastSave);
            }
            AppendType(text, "valheim_world_auto_backups_total", "counter");
            AppendMetric(text, "valheim_world_auto_backups_total", autoBackups);
            if (hasLastAutoBackup)
            {
                AppendType(text, "valheim_world_last_auto_backup_timestamp_seconds", "gauge");
                AppendMetric(text, "valheim_world_last_auto_backup_timestamp_seconds", lastAutoBackup);
            }


            if (hasCharacterLeaderboard)
            {
                AppendType(text, "valheim_character_storage_success", "gauge");
                AppendMetric(text, "valheim_character_storage_success", characterStorageSuccess ? 1 : 0);
                AppendType(text, "valheim_character_playtime_seconds", "gauge");
                AppendType(text, "valheim_character_playtime_7d_seconds", "gauge");
                AppendType(text, "valheim_character_online", "gauge");
                AppendType(text, "valheim_character_sessions_total", "counter");
                AppendType(text, "valheim_character_last_seen_timestamp_seconds", "gauge");
                AppendType(text, "valheim_character_deaths_total", "counter");
                foreach (var entry in characters)
                {
                    AppendCharacterMetric(text, "valheim_character_playtime_seconds", entry, entry.AllTimeSeconds);
                    AppendCharacterMetric(text, "valheim_character_playtime_7d_seconds", entry, entry.LastSevenDaysSeconds);
                    AppendCharacterMetric(text, "valheim_character_online", entry, entry.Online ? 1 : 0);
                    AppendCharacterMetric(text, "valheim_character_sessions_total", entry, entry.Sessions);
                    AppendCharacterMetric(text, "valheim_character_deaths_total", entry, entry.Deaths);
                    if (entry.LastSeenUnixSeconds.HasValue)
                        AppendCharacterMetric(text, "valheim_character_last_seen_timestamp_seconds", entry, entry.LastSeenUnixSeconds.Value);
                }
            }
            if (hasBackupInventoryStatus)
            {
                AppendType(text, "valheim_world_backup_inventory_success", "gauge");
                AppendMetric(text, "valheim_world_backup_inventory_success", backupInventorySuccess ? 1 : 0);
                if (backupInventorySuccess)
                {
                    AppendType(text, "valheim_world_backups", "gauge");
                    AppendMetric(text, "valheim_world_backups", backups.Length);
                    if (backups.Length > 0)
                    {
                        AppendType(text, "valheim_world_backup_info", "gauge");
                        AppendType(text, "valheim_world_backup_created_timestamp_seconds", "gauge");
                        AppendType(text, "valheim_world_backup_data_timestamp_seconds", "gauge");
                        foreach (BackupEntry backup in backups)
                        {
                            AppendLabeledMetric(text, "valheim_world_backup_info", "backup", backup.Name, 1.0);
                            if (backup.CreatedUnixSeconds.HasValue)
                                AppendLabeledMetric(text, "valheim_world_backup_created_timestamp_seconds", "backup", backup.Name, backup.CreatedUnixSeconds.Value);
                            if (backup.DataUnixSeconds.HasValue)
                                AppendLabeledMetric(text, "valheim_world_backup_data_timestamp_seconds", "backup", backup.Name, backup.DataUnixSeconds.Value);
                        }
                    }
                    if (hasLiveSaveTimestamp)
                    {
                        AppendType(text, "valheim_world_save_file_timestamp_seconds", "gauge");
                        AppendMetric(text, "valheim_world_save_file_timestamp_seconds", liveSaveTimestamp);
                    }
                }
            }
            if (hasBackupInventoryTimestamp)
            {
                AppendType(text, "valheim_world_backup_inventory_timestamp_seconds", "gauge");
                AppendMetric(text, "valheim_world_backup_inventory_timestamp_seconds", backupInventoryTimestamp);
            }

            if (mods.Length > 0)
            {
                AppendType(text, "valheim_server_mod_info", "gauge");
                foreach (ModEntry mod in mods)
                {
                    text.Append("valheim_server_mod_info{guid=\"");
                    AppendEscapedLabel(text, mod.Guid);
                    text.Append("\",name=\"");
                    AppendEscapedLabel(text, mod.Name);
                    text.Append("\",version=\"");
                    AppendEscapedLabel(text, mod.Version);
                    text.Append("\"} 1\n");
                }
            }

            if (hasModpackManifestStatus)
            {
                AppendType(text, "valheim_modpack_manifest_success", "gauge");
                AppendMetric(text, "valheim_modpack_manifest_success", modpackManifestSuccess ? 1 : 0);
                if (modpackManifestSuccess)
                {
                    if (modpackPackages.Length > 0)
                    {
                        AppendType(text, "valheim_modpack_package_info", "gauge");
                        foreach (ModpackPackageEntry package in modpackPackages)
                        {
                            text.Append("valheim_modpack_package_info{namespace=\"");
                            AppendEscapedLabel(text, package.Namespace);
                            text.Append("\",name=\"");
                            AppendEscapedLabel(text, package.Name);
                            text.Append("\",version=\"");
                            AppendEscapedLabel(text, package.Version);
                            text.Append("\",source=\"");
                            AppendEscapedLabel(text, package.Source);
                            text.Append("\",client=\"").Append(package.Client ? "true" : "false");
                            text.Append("\",server=\"").Append(package.Server ? "true" : "false");
                            text.Append("\"} 1\n");
                        }
                    }
                    if (modpackProfileCode != null)
                    {
                        AppendType(text, "valheim_modpack_info", "gauge");
                        text.Append("valheim_modpack_info{profile_name=\"");
                        AppendEscapedLabel(text, modpackProfileName);
                        text.Append("\",profile_code=\"");
                        AppendEscapedLabel(text, modpackProfileCode);
                        text.Append("\"} 1\n");
                    }
                }
            }

            byte[] snapshot = Encoding.UTF8.GetBytes(text.ToString());
            if (snapshot.Length > MaximumSnapshotLength)
                throw new InvalidOperationException("Metrics snapshot exceeded its fixed size limit.");
            return snapshot;
        }

        private void IntegratePlayers(double now)
        {
            if (now > lastPlayerIntegration)
            {
                connectedSeconds += (now - lastPlayerIntegration) * connectedPlayers;
                lastPlayerIntegration = now;
            }
        }

        private void InvalidateSnapshot()
        {
            cachedSnapshot = null;
            cachedAt = double.NegativeInfinity;
        }

        private static void AppendType(StringBuilder text, string name, string metricType)
        {
            text.Append("# TYPE ").Append(name).Append(' ').Append(metricType).Append('\n');
        }

        private static void AppendMetric(StringBuilder text, string name, long value)
        {
            text.Append(name).Append(' ').Append(value.ToString(CultureInfo.InvariantCulture)).Append('\n');
        }

        private static void AppendMetric(StringBuilder text, string name, int value)
        {
            text.Append(name).Append(' ').Append(value.ToString(CultureInfo.InvariantCulture)).Append('\n');
        }

        private static void AppendMetric(StringBuilder text, string name, double value)
        {
            text.Append(name).Append(' ').Append(value.ToString("R", CultureInfo.InvariantCulture)).Append('\n');
        }

        private static void AppendLabeledMetric(
            StringBuilder text,
            string metric,
            string label,
            string labelValue,
            double value)
        {
            text.Append(metric).Append('{').Append(label).Append("=\"");
            AppendEscapedLabel(text, labelValue);
            text.Append("\"} ").Append(value.ToString("R", CultureInfo.InvariantCulture)).Append('\n');
        }

        private static void AppendCharacterMetric(StringBuilder text, string metric,
            CharacterLeaderboardEntry entry, double value)
        {
            text.Append(metric).Append("{character_id=\"");
            AppendEscapedLabel(text, entry.Id);
            text.Append("\",character=\"");
            AppendEscapedLabel(text, entry.CharacterName);
            text.Append("\"} ").Append(value.ToString("R", CultureInfo.InvariantCulture)).Append('\n');
        }

        private static void AppendEscapedLabel(StringBuilder text, string value)
        {
            foreach (char character in value)
            {
                switch (character)
                {
                    case '\\': text.Append("\\\\"); break;
                    case '"': text.Append("\\\""); break;
                    case '\n': text.Append("\\n"); break;
                    case '\r': text.Append("\\n"); break;
                    default: text.Append(character); break;
                }
            }
        }

        private static string ValidateLabel(string value, string parameterName, bool allowEmpty)
        {
            if (string.IsNullOrEmpty(value))
            {
                if (allowEmpty)
                    return null;
                throw new ArgumentException("A non-empty label value is required.", parameterName);
            }
            if (value.Length > MaximumLabelLength)
                throw new ArgumentOutOfRangeException(parameterName, "Label values cannot exceed 256 characters.");
            return value;
        }

        private static string ValidatePackageIdentity(string value, string parameterName)
        {
            value = ValidateLabel(value, parameterName, false);
            foreach (char character in value)
                if (!(character >= 'a' && character <= 'z') && !(character >= 'A' && character <= 'Z') &&
                    !(character >= '0' && character <= '9') && character != '_')
                    throw new ArgumentException("Package identities may contain only letters, digits, and underscores.", parameterName);
            return value;
        }

        private static string ValidatePackageVersion(string value, string parameterName)
        {
            value = ValidateLabel(value, parameterName, false);
            string[] parts = value.Split('.');
            if (parts.Length != 3)
                throw new ArgumentException("Package versions require three numeric segments.", parameterName);
            foreach (string part in parts)
            {
                if (part.Length == 0 || (part.Length > 1 && part[0] == '0'))
                    throw new ArgumentException("Package versions require three numeric segments.", parameterName);
                foreach (char character in part)
                    if (character < '0' || character > '9')
                        throw new ArgumentException("Package versions require three numeric segments.", parameterName);
            }
            return value;
        }

        private static void ValidateOptionalTimestamp(double? value, string parameterName)
        {
            if (value.HasValue && !IsFiniteNonnegative(value.Value))
                throw new ArgumentOutOfRangeException(parameterName, "Timestamps must be finite and nonnegative.");
        }

        private static bool IsFiniteNonnegative(double value)
        {
            return value >= 0.0 && !double.IsInfinity(value) && !double.IsNaN(value);
        }

        private static double ReadFiniteClock(Func<double> clock, string clockName)
        {
            double value = clock();
            if (double.IsNaN(value) || double.IsInfinity(value))
                throw new InvalidOperationException(clockName + " returned a non-finite value.");
            return value;
        }

        private static double DefaultMonotonicSeconds()
        {
            return (double)Stopwatch.GetTimestamp() / Stopwatch.Frequency;
        }

        private static double DefaultUnixSeconds()
        {
            return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0;
        }
    }
}
