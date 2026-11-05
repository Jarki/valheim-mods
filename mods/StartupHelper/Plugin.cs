using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace StartupHelper
{
    public enum LaunchMode { LocalWorld, Server }
    public enum SaveSource { Local, Cloud, Legacy }

    [BepInPlugin(PluginId, "Startup Helper", "1.0.0")]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string PluginId = "local.valheim.startuphelper";
        private enum Stage { Idle, WaitingForMenu, Countdown, AwaitingEula, Done }

        // Static even if a loader recreates the component: returning to the menu never rearms it.
        private static bool startupSeen;
        private static Plugin instance;
        private Harmony harmony;
        private Stage stage;
        private FejdStartup menu;
        private float deadline;
        private string notice;
        private float noticeUntil;
        private string passwordEnvironmentForAttempt;
        private float passwordDeadline;
        private GameObject overlay;
        private TextMeshProUGUI overlayText;
        private bool overlayFailed;
        private bool introWaitBudgeted;
        private bool introSkipAttempted;

        private ConfigEntry<bool> enabledOnLaunch;
        private ConfigEntry<string> character;
        private ConfigEntry<SaveSource> characterSource;
        private ConfigEntry<LaunchMode> mode;
        private ConfigEntry<string> worldName;
        private ConfigEntry<SaveSource> worldSource;
        private ConfigEntry<string> address;
        private ConfigEntry<int> port;
        private ConfigEntry<string> passwordEnvironment;
        private ConfigEntry<float> delay;
        private ConfigEntry<KeyCode> cancelKey;
        private ConfigEntry<bool> skipStartupCinematic;

        private void Awake()
        {
            instance = this;
            enabledOnLaunch = Config.Bind("Startup", "Enabled", false, "Automatically launch once per process. Restart the game after changing configuration.");
            character = Config.Bind("Startup", "Character", "", "Exact, case-sensitive existing character filename without .fch; not the display name. No fallback.");
            characterSource = Config.Bind("Startup", "CharacterSource", SaveSource.Local, "Exact character save source; Auto is intentionally unsupported.");
            mode = Config.Bind("Startup", "Mode", LaunchMode.LocalWorld, "Start an existing private local world or join a server.");
            delay = Config.Bind("Startup", "LaunchDelaySeconds", 5f, new ConfigDescription("Countdown after the main menu is ready.", new AcceptableValueRange<float>(0f, 120f)));
            cancelKey = Config.Bind("Startup", "CancelKey", KeyCode.F8, "Press or hold during startup/countdown to cancel for this process. None disables the shortcut.");
            skipStartupCinematic = Config.Bind("Startup", "SkipStartupCinematic", true, "Use the game's normal intro skip action when automatic startup is enabled and a character/target is configured. Does not dismiss other UI.");
            worldName = Config.Bind("LocalWorld", "Name", "", "Exact, case-sensitive existing world name (m_name in the game's world list).");
            worldSource = Config.Bind("LocalWorld", "Source", SaveSource.Local, "Exact world save source. LocalWorld always starts private, with server/public/crossplay off.");
            address = Config.Bind("Server", "Address", "", "Server IP or DNS hostname only; no scheme, path, or port. IPv6 is accepted without brackets.");
            port = Config.Bind("Server", "Port", 2456, new ConfigDescription("Gameplay port, not Steam query port.", new AcceptableValueRange<int>(1, 65535)));
            passwordEnvironment = Config.Bind("Server", "PasswordEnvironmentVariable", "", "Optional name of an environment variable inherited by the game. Empty/unset uses the normal password prompt. Never put a password here.");
            try
            {
                Api.Validate();
                harmony = new Harmony(PluginId);
                harmony.PatchAll(typeof(Plugin).Assembly);
                Logger.LogInfo("Startup Helper loaded; automatic startup is " + (enabledOnLaunch.Value ? "enabled." : "disabled."));
            }
            catch (Exception ex)
            {
                harmony?.UnpatchSelf();
                Fail("Unsupported game API; helper disabled (" + ex.GetType().Name + ").");
            }
        }

        private void OnDestroy()
        {
            DestroyOverlay();
            passwordEnvironmentForAttempt = null;
            harmony?.UnpatchSelf();
            if (instance == this) instance = null;
        }

        private void MenuStarted(FejdStartup startup)
        {
            passwordEnvironmentForAttempt = null;
            if (startupSeen) return;
            startupSeen = true;
            if (!enabledOnLaunch.Value || Application.isBatchMode) return;
            menu = startup;
            // Let Steam invites/CLI joins own startup rather than racing them.
            string[] args = Environment.GetCommandLineArgs();
            if (args.Any(a => a == "+connect" || a == "+connect_lobby" || a == "-joinserverwithcharacter" || a == "-joincode"))
            {
                Fail("A game command-line join is active; automatic startup cancelled.");
                return;
            }
            stage = Stage.WaitingForMenu;
            deadline = Time.realtimeSinceStartup + 180f;
            Logger.LogInfo("Waiting for the main menu. Cancel key: " + cancelKey.Value);
        }

        private void Update()
        {
            try
            {
                if (passwordEnvironmentForAttempt != null &&
                    (Time.realtimeSinceStartup > passwordDeadline || (menu && Active(menu.m_connectionFailedPanel))))
                    passwordEnvironmentForAttempt = null;

                if (stage == Stage.Idle || stage == Stage.Done) return;
                if (!enabledOnLaunch.Value || (cancelKey.Value != KeyCode.None && Input.GetKey(cancelKey.Value)))
                {
                    Fail("Automatic startup cancelled for this process.");
                    return;
                }
                if (!menu || FejdStartup.instance != menu)
                {
                    Fail("Startup menu changed; automatic startup cancelled.");
                    return;
                }
                if (stage == Stage.AwaitingEula) return;
                if (menu.HasServerToJoin() || Api.QueuedJoin(menu).IsValid || MatchmakingManager.HasPendingInvite())
                {
                    Fail("Another join request is active; automatic startup cancelled.");
                    return;
                }
                if (Active(menu.m_characterSelectScreen) || Active(menu.m_startGamePanel) || Active(menu.m_loading))
                {
                    Fail("Menu navigation detected; automatic startup cancelled.");
                    return;
                }
                if (stage == Stage.WaitingForMenu) HandleStartupCinematic();
                bool ready = MainMenuReady();
                if (stage == Stage.WaitingForMenu)
                {
                    if (Time.realtimeSinceStartup > deadline) Fail("Main menu wait expired (180 seconds plus any startup intro duration); automatic startup cancelled.");
                    else if (ready)
                    {
                        stage = Stage.Countdown;
                        deadline = Time.realtimeSinceStartup + delay.Value;
                    }
                    return;
                }
                if (!ready)
                {
                    Fail("Main menu is busy; automatic startup cancelled.");
                    return;
                }
                if (Time.realtimeSinceStartup >= deadline) BeginAttempt();
            }
            catch (Exception ex)
            {
                // Avoid exception messages/arguments: another mod or network API could include secrets.
                Fail("Automatic startup stopped (" + ex.GetType().Name + "); use the normal menu.");
            }
        }

        private bool MainMenuReady()
        {
            return Active(menu.m_mainMenu) && Active(menu.m_menuList) &&
                !CinematicsManager.IsStartedPlaying() && !UnifiedPopup.IsVisible() &&
                !menu.m_eulaWindow.IsWindowActive() && !Active(menu.m_ndaPanel) &&
                !Active(menu.m_connectionFailedPanel) && !Active(menu.m_pleaseWait) &&
                !Active(menu.m_newGameVersionPanel) && !Active(menu.m_playerVersionPanel) &&
                !Active(menu.m_worldVersionPanel) && !Active(menu.m_changeLog) &&
                !menu.m_manageSavesMenu.IsVisible();
        }

        private void HandleStartupCinematic()
        {
            CinematicsManager cinematics = CinematicsManager.s_instance;
            if (!cinematics || !CinematicsManager.IsStartedPlaying()) return;
            var video = (CinematicsManager.VideoEntry)Api.CurrentVideo.GetValue(cinematics);
            if (video == null || !video.m_settings.HasFlag(CinematicsManager.Settings.Intro)) return;
            var player = cinematics.m_videoPlayer;
            if (!player || !player.clip) return;
            if (!introWaitBudgeted)
            {
                // Budget the entire clip plus normal readiness time, even if playback just began.
                // Realtime still advances while CinematicsManager has paused the game.
                deadline = Mathf.Max(deadline, Time.realtimeSinceStartup + (float)player.clip.length + 180f);
                introWaitBudgeted = true;
            }
            bool configured = !string.IsNullOrWhiteSpace(character.Value) &&
                ((mode.Value == LaunchMode.LocalWorld && !string.IsNullOrWhiteSpace(worldName.Value)) ||
                 (mode.Value == LaunchMode.Server && Uri.CheckHostName(address.Value) != UriHostNameType.Unknown));
            if (!skipStartupCinematic.Value || !configured || introSkipAttempted) return;
            // Same readiness gate used by the game's Escape/Jump/Controller-B skip handler.
            if (player.frame <= 5 || !player.isPlaying) return;
            introSkipAttempted = true;
            CinematicsManager.Stop();
            Logger.LogInfo("Skipped startup intro through the game's normal cinematic stop action.");
        }

        private void BeginAttempt()
        {
            stage = Stage.Done; // Terminal before any UI, validation, or connection can fail.
            if (mode.Value == LaunchMode.Server && !PlatformPrefs.GetBool("EulaAccepted"))
            {
                stage = Stage.AwaitingEula;
                menu.m_eulaWindow.Open(accepted =>
                {
                    if (stage != Stage.AwaitingEula) return;
                    stage = Stage.Done;
                    if (!accepted || !enabledOnLaunch.Value || !menu || FejdStartup.instance != menu)
                    {
                        Fail("Online startup cancelled.");
                        return;
                    }
                    TryLaunch();
                });
                return;
            }
            TryLaunch();
        }

        private void TryLaunch()
        {
            try { Launch(); }
            catch (Exception ex) { Fail("Automatic startup stopped (" + ex.GetType().Name + "); use the normal menu."); }
        }

        private void Launch()
        {
            if (!Active(menu.m_mainMenu) || menu.HasServerToJoin() || Api.QueuedJoin(menu).IsValid || MatchmakingManager.HasPendingInvite())
                throw new InvalidOperationException();
            if (string.IsNullOrWhiteSpace(character.Value))
            {
                Fail("Set Startup.Character to an exact existing filename without .fch.");
                return;
            }
            List<PlayerProfile> profiles = Api.Profiles(menu);
            var matches = profiles.Where(p => p.GetFilename() == character.Value && p.m_fileSource == Source(characterSource.Value)).ToList();
            if (matches.Count != 1)
            {
                Fail("Expected exactly one loadable character matching Startup.Character and CharacterSource; found " + matches.Count + ".");
                return;
            }
            // SaveSystem.GetAllPlayerProfiles already rejected failed/incompatible loads while
            // holding the game's cloud-storage read mount. Do not reread files outside that mount.
            PlayerProfile profile = matches[0];
            World world = null;
            ServerJoinData join = ServerJoinData.None;
            if (mode.Value == LaunchMode.LocalWorld)
            {
                var worlds = SaveSystem.GetWorldList().Where(w => w.m_name == worldName.Value && w.m_fileSource == Source(worldSource.Value)).ToList();
                if (string.IsNullOrWhiteSpace(worldName.Value) || worlds.Count != 1)
                {
                    Fail("Expected exactly one existing world matching LocalWorld.Name and Source; found " + worlds.Count + ".");
                    return;
                }
                world = worlds[0];
                if (world.m_dataError != World.SaveDataError.None)
                {
                    Fail("World has a save error (" + world.m_dataError + "); use the game's world menu.");
                    return;
                }
            }
            else if (mode.Value == LaunchMode.Server)
            {
                if (Uri.CheckHostName(address.Value) == UriHostNameType.Unknown || port.Value < 1 || port.Value > 65535)
                {
                    Fail("Set Server.Address to an IP/hostname only and Port to a gameplay port (1-65535).");
                    return;
                }
                join = new ServerJoinData(new ServerJoinDataDedicated(address.Value, (ushort)port.Value));
                if (!join.IsValid) throw new InvalidOperationException();
            }
            else throw new InvalidOperationException();

            // Select by exact index, not SetSelectedProfile (which silently falls back to index zero).
            Api.ProfileIndex.SetValue(menu, profiles.IndexOf(profile));
            Api.UpdateCharacterList.Invoke(menu, null);
            menu.OnStartGame();
            if (mode.Value == LaunchMode.Server) Api.QueuedJoinField.SetValue(menu, join);
            menu.OnCharacterStart();
            if (mode.Value == LaunchMode.Server)
            {
                // Only arm password submission if the vanilla JoinServer actually started a transition.
                if (menu.IsInvoking("LoadMainSceneIfBackendSelected"))
                {
                    passwordEnvironmentForAttempt = string.IsNullOrWhiteSpace(passwordEnvironment.Value) ? null : passwordEnvironment.Value;
                    passwordDeadline = Time.realtimeSinceStartup + 120f;
                    Logger.LogInfo("Server join handed to the game; no automatic reconnect will be attempted.");
                }
                else Logger.LogInfo("Server join requires game UI/validation; no automatic retry will be attempted.");
                return;
            }

            // ShowStartGame refreshes/falls back to a world: replace that selection with the exact object
            // after it returns. Do not call UpdateWorldList here: it coalesces worlds by filename.
            Api.WorldField.SetValue(menu, world);
            menu.m_openServerToggle.SetIsOnWithoutNotify(false);
            menu.m_publicServerToggle.SetIsOnWithoutNotify(false);
            menu.m_crossplayServerToggle.SetIsOnWithoutNotify(false);
            menu.m_serverPassword.text = "";
            if (!(bool)Api.CanStartServer.Invoke(menu, null))
            {
                Fail("Game validation refused world startup; use the normal world menu.");
                return;
            }
            Logger.LogInfo("Starting exact existing world '" + world.m_name + "' (" + world.m_fileSource + ") with character '" + profile.GetFilename() + "'.");
            menu.OnWorldStart();
        }

        private void SubmitPassword(ZNet net, bool needPassword)
        {
            string variable = passwordEnvironmentForAttempt;
            passwordEnvironmentForAttempt = null; // At most one handshake, even if the password is wrong.
            if (variable == null || Time.realtimeSinceStartup > passwordDeadline || !needPassword || !net.InPasswordDialog()) return;
            try
            {
                string password = Environment.GetEnvironmentVariable(variable);
                if (string.IsNullOrEmpty(password))
                {
                    Logger.LogInfo("Password environment variable is unset/empty; use the normal password prompt.");
                    return;
                }
                Api.OnPasswordEntered.Invoke(net, new object[] { password });
                Logger.LogInfo("Submitted environment password through the game's password handler.");
            }
            catch (Exception ex)
            {
                Logger.LogWarning("Could not submit environment password (" + ex.GetType().Name + "); use the game UI.");
            }
        }

        private static FileHelpers.FileSource Source(SaveSource source)
        {
            switch (source)
            {
                case SaveSource.Local: return FileHelpers.FileSource.Local;
                case SaveSource.Cloud: return FileHelpers.FileSource.Cloud;
                case SaveSource.Legacy: return FileHelpers.FileSource.Legacy;
                default: throw new ArgumentOutOfRangeException(nameof(source));
            }
        }

        private static bool Active(GameObject obj) => obj && obj.activeInHierarchy;

        private void Fail(string message)
        {
            stage = Stage.Done;
            passwordEnvironmentForAttempt = null;
            notice = message;
            noticeUntil = Time.realtimeSinceStartup + 12f;
            Logger.LogWarning(message);
        }

        private void OnDisable() => DestroyOverlay();

        private void LateUpdate()
        {
            if (overlayFailed || Application.isBatchMode) return;
            string text = null;
            if (stage == Stage.Countdown)
                text = "Startup Helper: " + mode.Value + " in " + Mathf.CeilToInt(Mathf.Max(0f, deadline - Time.realtimeSinceStartup)) + "s — cancel: " + cancelKey.Value;
            else if (notice != null && Time.realtimeSinceStartup < noticeUntil) text = "Startup Helper: " + notice;
            if (text == null)
            {
                DestroyOverlay();
                return;
            }
            try
            {
                if (!overlay)
                {
                    FejdStartup startup = FejdStartup.instance;
                    TMP_FontAsset font = startup && startup.m_versionLabel ? startup.m_versionLabel.font : null;
                    if (!font) font = Resources.FindObjectsOfTypeAll<TMP_FontAsset>().FirstOrDefault(f => f && f.material);
                    if (!font) return; // Wait for a game font to load rather than using IMGUI's missing font.
                    CreateOverlay(font);
                }
                if (overlayText.text != text) overlayText.text = text;
            }
            catch (Exception ex)
            {
                DestroyOverlay();
                overlayFailed = true;
                Logger.LogWarning("Startup display unavailable (" + ex.GetType().Name + "); countdown/cancel remain active. See log messages.");
            }
        }

        private void CreateOverlay(TMP_FontAsset font)
        {
            overlay = new GameObject("StartupHelperOverlay", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            overlay.transform.SetParent(transform, false);
            Canvas canvas = overlay.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30000;
            CanvasScaler scaler = overlay.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

            GameObject panel = new GameObject("Notice", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(overlay.transform, false);
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = panelRect.anchorMax = panelRect.pivot = new Vector2(0, 1);
            panelRect.anchoredPosition = new Vector2(16, -16);
            panelRect.sizeDelta = new Vector2(880, 88);
            Image background = panel.GetComponent<Image>();
            background.color = new Color(0.035f, 0.035f, 0.035f, 0.94f);
            background.raycastTarget = false;

            GameObject label = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            label.transform.SetParent(panel.transform, false);
            RectTransform labelRect = label.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(14, 10);
            labelRect.offsetMax = new Vector2(-14, -10);
            overlayText = label.GetComponent<TextMeshProUGUI>();
            overlayText.font = font;
            overlayText.fontSharedMaterial = font.material;
            overlayText.color = Color.white;
            overlayText.fontSize = 24;
            overlayText.enableAutoSizing = true;
            overlayText.fontSizeMin = 18;
            overlayText.fontSizeMax = 24;
            overlayText.alignment = TextAlignmentOptions.MidlineLeft;
            overlayText.richText = false;
            overlayText.raycastTarget = false;
        }

        private void DestroyOverlay()
        {
            if (overlay) Destroy(overlay);
            overlay = null;
            overlayText = null;
        }

        // All private members checked against native Valheim build 25909357 (l-1.0.20).
        private static class Api
        {
            internal static readonly FieldInfo ProfileIndex = AccessTools.Field(typeof(FejdStartup), "m_profileIndex");
            internal static readonly FieldInfo ProfilesField = AccessTools.Field(typeof(FejdStartup), "m_profiles");
            internal static readonly FieldInfo WorldField = AccessTools.Field(typeof(FejdStartup), "m_world");
            internal static readonly FieldInfo QueuedJoinField = AccessTools.Field(typeof(FejdStartup), "m_queuedJoinServer");
            internal static readonly FieldInfo CurrentVideo = AccessTools.Field(typeof(CinematicsManager), "m_currentVideo");
            internal static readonly MethodInfo UpdateCharacterList = AccessTools.Method(typeof(FejdStartup), "UpdateCharacterList", Type.EmptyTypes);
            internal static readonly MethodInfo CanStartServer = AccessTools.Method(typeof(FejdStartup), "CanStartServer", Type.EmptyTypes);
            internal static readonly MethodInfo OnPasswordEntered = AccessTools.Method(typeof(ZNet), "OnPasswordEntered", new[] { typeof(string) });
            internal static List<PlayerProfile> Profiles(FejdStartup startup) => (List<PlayerProfile>)ProfilesField.GetValue(startup);
            internal static ServerJoinData QueuedJoin(FejdStartup startup) => (ServerJoinData)QueuedJoinField.GetValue(startup);
            internal static void Validate()
            {
                if (ProfileIndex == null || ProfilesField == null || WorldField == null || QueuedJoinField == null || CurrentVideo == null ||
                    UpdateCharacterList == null || CanStartServer == null || OnPasswordEntered == null)
                    throw new MissingMemberException();
            }
        }

        [HarmonyPatch(typeof(FejdStartup), "Start")]
        private static class MenuStartedPatch
        {
            private static void Postfix(FejdStartup __instance)
            {
                if (!instance) return;
                try { instance.MenuStarted(__instance); }
                catch (Exception ex) { instance.Fail("Startup initialization stopped (" + ex.GetType().Name + ")."); }
            }
        }

        [HarmonyPatch(typeof(ZNet), "RPC_ClientHandshake")]
        private static class PasswordPatch
        {
            private static void Postfix(ZNet __instance, bool needPassword)
            {
                if (instance) instance.SubmitPassword(__instance, needPassword);
            }
        }

        // A manual launch in the same menu scene must never inherit our environment password.
        // Our own launch arms it only after these vanilla handlers have returned successfully.
        [HarmonyPatch(typeof(FejdStartup), nameof(FejdStartup.JoinServer))]
        private static class JoinStartedPatch
        {
            private static void Prefix()
            {
                if (instance) instance.passwordEnvironmentForAttempt = null;
            }
        }

        [HarmonyPatch(typeof(FejdStartup), nameof(FejdStartup.OnWorldStart))]
        private static class WorldStartedPatch
        {
            private static void Prefix()
            {
                if (instance) instance.passwordEnvironmentForAttempt = null;
            }
        }
    }
}
