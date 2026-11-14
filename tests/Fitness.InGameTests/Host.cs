using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace Fitness.InGameTests
{
    // Never install this assembly in a player's profile. The launcher creates the entire disposable sandbox.
    [BepInPlugin("local.fitness.ingame-tests", "Fitness isolated native checks", "1.0.0")]
    [BepInDependency("jarki.fitness", BepInDependency.DependencyFlags.SoftDependency)]
    [DefaultExecutionOrder(20000)]
    public sealed partial class Host : BaseUnityPlugin
    {
        const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        const string LoadKey = "jarki.fitness.runStrain", SeverityKey = "jarki.fitness.runStrainLevel";
        static Host active;
        string dir, profileName, worldName, stage = "bootstrap";
        Player player;
        BaseUnityPlugin fitness;
        Type treadmillType;
        Behaviour treadmill;
        ItemDrop.ItemData hammer;
        Harmony observer;
        int passes, nativeErrors;
        float started, fixedSkill = -1f;
        float nativeBaseStamina, nativeStaminaRegen, recoveryResumeStamina = -1f;
        bool observeRecoveryResume;
        bool drive, sprint, recordVideo, biomeShowcase;
        Vector3 arena, placedAt;
        readonly List<float> xp = new List<float>();
        readonly List<GameObject> fixturePieces = new List<GameObject>();

        static FieldInfo Member(object obj, string name)
        {
            Type type = obj as Type ?? obj.GetType();
            for (; type != null; type = type.BaseType)
            {
                var field = type.GetField(name, Flags);
                if (field != null) return field;
            }
            throw new MissingFieldException((obj as Type ?? obj.GetType()).FullName, name);
        }
        static object Get(object obj, string name) => Member(obj, name).GetValue(obj is Type ? null : obj);
        static void Set(object obj, string name, object value) => Member(obj, name).SetValue(obj is Type ? null : obj, value);
        static object Call(object obj, string name, params object[] args)
        {
            Type type = obj as Type ?? obj.GetType();
            for (; type != null; type = type.BaseType)
                foreach (var method in type.GetMethods(Flags | BindingFlags.DeclaredOnly))
                    if (method.Name == name && method.GetParameters().Length == args.Length)
                        return method.Invoke(obj is Type ? null : obj, args);
            throw new MissingMethodException((obj as Type ?? obj.GetType()).FullName, name);
        }
        void Log(string text)
        {
            string line = "t=" + Time.realtimeSinceStartup.ToString("F3", CultureInfo.InvariantCulture) + " stage=" + stage + " " + text;
            File.AppendAllText(Path.Combine(dir, "runtime.log"), line + "\n");
            Logger.LogInfo(line);
        }
        void Stage(string name) { stage = name; Log("STAGE " + name); }
        void Check(bool condition, string label)
        {
            if (!condition) throw new InvalidOperationException("FAIL: " + label);
            ++passes; Log("PASS: " + label);
        }
        static bool Inside(string root, string path)
        {
            return !string.IsNullOrEmpty(path) && Path.IsPathRooted(path) &&
                Path.GetFullPath(path).StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal);
        }
        void Awake()
        {
            if (Environment.GetEnvironmentVariable("FITNESS_RUN") != "disposable-local-only") { enabled = false; return; }
            string run = Environment.GetEnvironmentVariable("FITNESS_RUN_ID");
            dir = Environment.GetEnvironmentVariable("FITNESS_EVIDENCE");
            string saves = Environment.GetEnvironmentVariable("FITNESS_SAVES");
            string marker = "/.dev/fitness-lab/runs/";
            int index = string.IsNullOrEmpty(dir) ? -1 : Path.GetFullPath(dir).IndexOf(marker, StringComparison.Ordinal);
            string root = index < 0 || string.IsNullOrEmpty(run) ? "" : Path.GetFullPath(dir).Substring(0, index) + marker + run;
            var args = Environment.GetCommandLineArgs();
            bool savedir = false;
            for (int i = 0; i + 1 < args.Length; ++i) if (args[i] == "-savedir" && args[i + 1] == saves) savedir = true;
            if (root.Length == 0 || run.IndexOfAny(new[] { '/', '\\', '.' }) >= 0 || !Inside(root, dir) || !Inside(root, saves) ||
                !Inside(root, Environment.GetEnvironmentVariable("XDG_CONFIG_HOME")) ||
                !Inside(root, Environment.GetEnvironmentVariable("XDG_DATA_HOME")) ||
                !Inside(root, Environment.GetEnvironmentVariable("XDG_CACHE_HOME")) || !savedir || Array.IndexOf(args, "-server") >= 0)
            { Logger.LogError("Fitness harness refuses missing isolation, unsafe paths, or server flags"); enabled = false; return; }
            Directory.CreateDirectory(dir);
            profileName = "fitness" + run; worldName = "Fitness" + run;
            recordVideo = Environment.GetEnvironmentVariable("FITNESS_RECORD_VIDEO") == "1";
            biomeShowcase = Environment.GetEnvironmentVariable("FITNESS_SHOWCASE") == "1";
            started = Time.realtimeSinceStartup;
            Application.runInBackground = true;
            active = this;
            Application.logMessageReceived += ObserveLog;
            Log("BOOTSTRAP diagnostics enabled; launcher additionally preserves loader errors preceding harness Awake");
            StartCoroutine(Execute(Run()));
        }
        void ObserveLog(string condition, string trace, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            ++nativeErrors;
            File.AppendAllText(Path.Combine(dir, "native-errors.log"), "stage=" + stage + " " + condition + "\n" + trace + "\n");
        }
        void Update()
        {
            if (active != this) return;
            if (Time.realtimeSinceStartup - started > (biomeShowcase ? 5400f : 810f))
            {
                StopAllCoroutines();
                Finish(true, "harness overall watchdog");
                Application.Quit();
                return;
            }
            if (!player) return;
            if (fixedSkill >= 0f) Skill().m_level = fixedSkill;
            if (drive)
            {
                Vector3 target = arena + new Vector3(Mathf.Cos(Time.time) * 4f, .15f, Mathf.Sin(Time.time) * 4f);
                Vector3 direction = target - player.transform.position; direction.y = 0f;
                player.SetLookDir(direction.normalized);
                Controls(Vector3.forward, sprint);
            }
        }
        void Controls(Vector3 move, bool run = false, bool jump = false)
        {
            player.SetControls(move, false, false, false, false, false, false, jump, false, run, false);
        }
        IEnumerator Execute(IEnumerator routine)
        {
            var stack = new Stack<IEnumerator>(); stack.Push(routine);
            bool failed = false;
            while (stack.Count > 0)
            {
                IEnumerator top = stack.Peek(); bool more = false;
                try { more = top.MoveNext(); }
                catch (Exception error) { Log("FAIL: " + error); failed = true; }
                if (failed) break;
                if (!more) { stack.Pop(); continue; }
                if (top.Current is IEnumerator nested) stack.Push(nested); else yield return top.Current;
            }
            drive = false;
            if (player) Controls(Vector3.zero);
            if (failed)
            {
                Log("BLOCKED: subsequent stages require the failed stage's independently asserted preconditions");
                // Failure evidence is best-effort: a broken renderer must not prevent clean shutdown.
                try { ScreenCapture.CaptureScreenshot(Path.Combine(dir, "failure-" + stage + ".png")); }
                catch (Exception error) { Log("Failure screenshot unavailable: " + error.Message); }
                yield return new WaitForSecondsRealtime(1f);
            }
            Finish(failed, "scenario end");
            yield return new WaitForSecondsRealtime(2f);
            Application.Quit();
        }
        void Finish(bool failed, string reason)
        {
            observer?.UnpatchSelf();
            Application.logMessageReceived -= ObserveLog;
            active = null;
            string result = (failed ? "FAIL" : "PASS") + " " + passes;
            File.WriteAllText(Path.Combine(dir, "complete.txt"), result + "\n");
            Log("FITNESS_RUNTIME_COMPLETE " + result + " nativeErrors=" + nativeErrors + " reason=" + reason);
        }
        IEnumerator Wait(Func<bool> ready, string label, float timeout = 20f)
        {
            float deadline = Time.realtimeSinceStartup + timeout;
            while (!ready())
            {
                if (CinematicsManager.s_instance && CinematicsManager.IsStartedPlaying()) CinematicsManager.Stop();
                if (Time.realtimeSinceStartup > deadline) throw new TimeoutException(label);
                yield return null;
            }
        }
        IEnumerator Capture(string name, float hold = .35f)
        {
            yield return new WaitForSecondsRealtime(hold);
            yield return new WaitForEndOfFrame();
            string path = Path.Combine(dir, name + ".png");
            ScreenCapture.CaptureScreenshot(path);
            yield return Wait(() => File.Exists(path) && new FileInfo(path).Length > 100, "PNG written " + name, 15f);
            Log("SCREENSHOT " + name + ".png");
        }
        IEnumerator FramedCapture(string name)
        {
            var rig = GameCamera.instance;
            Check(rig && Hud.instance && Hud.instance.IsVisible(), "real GameCamera and native HUD visible for " + name);
            bool enabledBefore = rig.enabled;
            Vector3 pos = rig.transform.position; Quaternion rot = rig.transform.rotation;
            rig.enabled = false;
            Vector3 target = treadmill.transform.position + Vector3.up;
            rig.transform.SetPositionAndRotation(target + new Vector3(-4f, 2.5f, -4f), Quaternion.LookRotation(new Vector3(4f, -2.5f, 4f)));
            yield return Capture(name);
            rig.transform.SetPositionAndRotation(pos, rot); rig.enabled = enabledBefore;
        }
        GameObject Prefab(string name)
        {
            var prefab = ZNetScene.instance.GetPrefab(name);
            if (!prefab) throw new InvalidOperationException("Missing registered native prefab " + name);
            return prefab;
        }
        ItemDrop.ItemData Add(string name, int amount)
        {
            var prefab = Prefab(name);
            var item = prefab.GetComponent<ItemDrop>().m_itemData.Clone(); item.m_dropPrefab = prefab; item.m_stack = amount;
            Check(player.GetInventory().AddItem(item), "fixture native inventory accepts " + amount + " " + name);
            return item;
        }
        int InventoryCount(string name) => player.GetInventory().CountItems(Prefab(name).GetComponent<ItemDrop>().m_itemData.m_shared.m_name);
        GameObject Fixture(string name, Vector3 at)
        {
            var obj = Instantiate(Prefab(name), at, Quaternion.identity);
            var wear = obj.GetComponent<WearNTear>(); if (wear) wear.enabled = false;
            fixturePieces.Add(obj); return obj;
        }
        Skills.Skill Skill() => (Skills.Skill)Call(player.GetSkills(), "GetSkill", Skills.SkillType.Run);
        void SetSkill(float level)
        {
            fixedSkill = -1f;
            Skill().m_level = level; Skill().m_accumulator = 0f;
            Check(Mathf.Abs(player.GetSkills().GetSkillLevel(Skills.SkillType.Run) - level) < .01f, "fixture effective Run skill " + level);
        }
        void Config(string section, string key, float value)
        {
            ConfigEntryBase entry = fitness.Config[new ConfigDefinition(section, key)];
            entry.BoxedValue = value;
            Check(Mathf.Abs((float)entry.BoxedValue - value) < .001f, "fixture effective config " + key + "=" + value);
        }
        void FillStamina()
        {
            Set(player, "m_stamina", player.GetMaxStamina());
            Log("FIXTURE stamina refilled once to " + player.GetStamina() + "; regeneration remains disabled, no refill during measurement");
        }
        int Level
        {
            get
            {
                int result = 0, count = 0;
                for (int i = 1; i <= 3; ++i)
                    if (player.GetSEMan().HaveStatusEffect(("FitnessRunStrain" + i).GetStableHashCode())) { result = i; ++count; }
                if (count > 1) throw new InvalidOperationException("Multiple strain severity status effects active");
                return result;
            }
        }
        float PersistedLoad()
        {
            var pkg = new ZPackage(); player.Save(pkg);
            Check(player.m_customData.ContainsKey(LoadKey) && player.m_customData.ContainsKey(SeverityKey), "native Player.Save emitted both persistent strain keys");
            return float.Parse(player.m_customData[LoadKey], CultureInfo.InvariantCulture);
        }
        bool Reserved(Behaviour target)
        {
            var view = target.GetComponent<ZNetView>();
            return view && view.IsValid() && view.GetZDO().GetZDOID(ZDO.GetHashZDOID("FitnessTreadmillUser")) != ZDOID.None;
        }
        IEnumerator Begin()
        {
            drive = false; Controls(Vector3.zero);
            Vector3 interactionAt = treadmill.transform.position + new Vector3(-1.8f, .1f, 0f);
            var body = player.GetComponent<Rigidbody>();
            body.position = interactionAt;
            body.linearVelocity = Vector3.zero;
            player.transform.position = interactionAt;
            Physics.SyncTransforms();
            yield return new WaitForSeconds(.25f);
            yield return Wait(() => !player.InAttack() && !player.InDodge() && player.CanMove(),
                "native placement/equip/dismount action animation permits movement", 4f);
            var anchor = (Transform)Get(treadmill, "attachPoint");
            Log("INTERACTION enabled=" + treadmill.isActiveAndEnabled + " valid=" + treadmill.GetComponent<ZNetView>().IsValid() +
                " crouching=" + player.IsCrouching() + " swimming=" + player.IsSwimming() + " flying=" + player.IsFlying() +
                " debugFlying=" + player.IsDebugFlying() + " distance=" + Vector3.Distance(player.transform.position, anchor.position) +
                " player=" + player.transform.position + " anchor=" + anchor.position + " canStart=" + Call(treadmill, "CanStart", player));
            Check(!player.IsAttached() && !player.InPlaceMode() && !player.IsEncumbered() &&
                (bool)Call(player, "TakeInput") && player.HaveStamina() &&
                Vector3.Distance(player.transform.position, anchor.position) < 3f,
                "interaction fixture unattached, input-ready, stamina available and observed within three metres");
            Check(((Interactable)treadmill).Interact(player, false, false), "ordinary Interactable use accepted");
            yield return Wait(() => player.IsAttached() && player.GetAttachPoint() && player.GetAttachPoint().IsChildOf(treadmill.transform), "native attach after owner reservation");
            Check(Reserved(treadmill) && !player.GetComponent<Rigidbody>().useGravity, "owner reservation and native gravity disable observed");
            var colliders = (Collider[])Get(player, "m_attachColliders");
            Check(colliders != null && colliders.Length > 0, "native attachment owns real collision exclusions");
            var playerCollider = (Collider)Get(player, "m_collider");
            foreach (var collider in colliders) Check(Physics.GetIgnoreCollision(playerCollider, collider), "attached collider pair ignored " + collider.name);
        }
        IEnumerator Detached(string cause, Behaviour previous, Collider[] previousColliders)
        {
            yield return Wait(() => !player.IsAttached(), cause + " detaches");
            Controls(Vector3.zero);
            yield return new WaitForSecondsRealtime(.15f);
            Check(player.GetAttachPoint() == null && Get(player, "m_attachColliders") == null && player.GetComponent<Rigidbody>().useGravity,
                cause + " clears native attachment, collider ownership and restores gravity");
            var playerCollider = (Collider)Get(player, "m_collider");
            foreach (var collider in previousColliders)
                if (collider) Check(!Physics.GetIgnoreCollision(playerCollider, collider), cause + " restores collision pair " + collider.name);
            if (previous) Check(!Reserved(previous), cause + " releases owner reservation");
            Check(Get(treadmillType, "active") == null && Get(treadmillType, "pending") == null, cause + " clears active and pending identities");
            yield return Wait(() => player.GetComponentInChildren<Animator>().GetFloat(ZSyncAnimation.GetHash("forward_speed")) < .1f,
                cause + " running animation settles to zero", 3f);
            Check(!player.IsRunning(), cause + " clears running state");
        }
        Collider[] AttachedColliders() => (Collider[])((Collider[])Get(player, "m_attachColliders")).Clone();
        IEnumerator Stop(string label)
        {
            var colliders = AttachedColliders();
            Check(((Interactable)treadmill).Interact(player, false, false), label + " ordinary interact stop accepted");
            yield return Detached(label, treadmill, colliders);
        }
        // Read-only native gameplay observers; never alter factors, inputs or results.
        static void ObserveRecoveryRun(Player __instance)
        {
            if (!active || !active.observeRecoveryResume || __instance != active.player
                || !(bool)Call(active.treadmillType, "IsTrainingRun", __instance)) return;
            active.recoveryResumeStamina = __instance.GetStamina();
            active.observeRecoveryResume = false;
            active.Log("RECOVERY_NATIVE_RESUME staminaBeforeRun=" + active.recoveryResumeStamina.ToString("R", CultureInfo.InvariantCulture)
                + " max=" + __instance.GetMaxStamina().ToString("R", CultureInfo.InvariantCulture));
        }

        static void XpBefore(Skills __instance, Skills.SkillType skillType, out float __state)
        {
            __state = float.NaN;
            if (active && active.player && __instance == active.player.GetSkills() && skillType == Skills.SkillType.Run)
                __state = active.Skill().m_accumulator;
        }
        static void XpAfter(Skills __instance, Skills.SkillType skillType, float __state)
        {
            if (!active || float.IsNaN(__state)) return;
            float delta = active.Skill().m_accumulator - __state;
            active.xp.Add(delta);
            active.Log("NATIVE_XP pulse=" + active.xp.Count + " accumulatorBefore=" + __state.ToString("R", CultureInfo.InvariantCulture) +
                " accumulatorAfter=" + active.Skill().m_accumulator.ToString("R", CultureInfo.InvariantCulture) +
                " delta=" + delta.ToString("R", CultureInfo.InvariantCulture) + " skill=" + active.Skill().m_level);
        }
        IEnumerator XpPulse(bool training)
        {
            SetSkill(40f); xp.Clear(); Set(player, "m_runSkillImproveTimer", .999f);
            float stamina = player.GetStamina();
            if (training)
                yield return Wait(() => xp.Count > 0, "treadmill actual native >1 second XP pulse", 4f);
            else
            {
                Controls(Vector3.forward, true);
                Check((bool)Call(player, "CheckRun", Vector3.forward, .02f), "ordinary native CheckRun control succeeds");
                Controls(Vector3.zero);
            }
            Check(xp.Count == 1 && xp[0] > 0f, "one positive native Skills accumulator increment (not prefix arguments)");
            Check(player.GetStamina() < stamina, "same native XP pulse consumes real stamina");
        }
        IEnumerator PlaceTreadmill(string label)
        {
            Stage(label);
            Vector3 buildAt = arena + new Vector3(0f, .15f, -3.5f);
            var body = player.GetComponent<Rigidbody>();
            body.position = buildAt;
            body.linearVelocity = Vector3.zero;
            player.transform.position = buildAt;
            Check(player.EquipItem(hammer, false), "native Hammer equipped");
            var piece = Prefab("FitnessTreadmill").GetComponent<Piece>();
            Call(player, "AddKnownPiece", piece); Call(player, "UpdateAvailablePiecesList");
            Check(player.SetSelectedPiece(piece), "treadmill selected from real Hammer piece table");
            Call(player, "SetupPlacementGhost"); Set(player, "m_placeRotation", 0);
            var rig = GameCamera.instance; bool oldEnabled = rig.enabled;
            Vector3 oldPos = rig.transform.position; Quaternion oldRot = rig.transform.rotation;
            rig.enabled = false;
            Vector3 target = arena + new Vector3(0f, .01f, 1f);
            rig.transform.SetPositionAndRotation(arena + new Vector3(0f, 3f, -2.8f), Quaternion.LookRotation(target - (arena + new Vector3(0f, 3f, -2.8f))));
            Physics.SyncTransforms();
            Call(player, "UpdatePlacementGhost", false);
            var ghost = (GameObject)Get(player, "m_placementGhost");
            Check(ghost && ghost.activeInHierarchy, "native placement ghost exists and is visible");
            Log("PLACEMENT status=" + Get(player, "m_placementStatus") + " ghost=" + ghost.transform.position);
            Check(Get(player, "m_placementStatus").ToString() == "Valid", "real raycast produced valid native placement (status never overwritten)");
            Check(!player.NoCostCheat() && player.HaveRequirements(piece, Player.RequirementMode.CanBuild), "real workbench and inventory satisfy native Hammer requirements");
            placedAt = ghost.transform.position;
            var recipeIcon = (UnityEngine.UI.Image)Get(Hud.instance, "m_buildIcon");
            yield return Wait(() => recipeIcon && recipeIcon.isActiveAndEnabled && recipeIcon.sprite == piece.m_icon,
                "native recipe panel becomes visible with the current selected piece icon", 5f);
            Check(piece.m_icon && piece.m_icon != Prefab("wood_floor").GetComponent<Piece>().m_icon
                && recipeIcon, "native recipe UI renders the custom treadmill icon, not the cloned wood-floor icon");
            Log("RECIPE_ICON sprite=" + recipeIcon.sprite.name + " size="
                + recipeIcon.sprite.texture.width + "x" + recipeIcon.sprite.texture.height);
            for (Transform parent = recipeIcon.transform; parent && parent != Hud.instance.transform; parent = parent.parent)
            {
                if (!(parent is RectTransform rect)) continue;
                var corners = new Vector3[4];
                rect.GetWorldCorners(corners);
                var canvas = rect.GetComponentInParent<Canvas>();
                var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
                Vector2 lower = RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
                Vector2 upper = RectTransformUtility.WorldToScreenPoint(camera, corners[2]);
                Log("RECIPE_UI_BOUNDS name=" + parent.name + " left=" + lower.x.ToString("R", CultureInfo.InvariantCulture)
                    + " bottom=" + lower.y.ToString("R", CultureInfo.InvariantCulture)
                    + " right=" + upper.x.ToString("R", CultureInfo.InvariantCulture)
                    + " top=" + upper.y.ToString("R", CultureInfo.InvariantCulture)
                    + " screen=" + Screen.width + "x" + Screen.height);
            }
            yield return Capture(label + "-ghost");
            int hideBefore = InventoryCount("DeerHide"), scrapsBefore = InventoryCount("LeatherScraps");
            Check(player.TryPlacePiece(piece), "native TryPlacePiece calls PlacePiece with raycast ghost, creator and placement effects");
            // Same native resource operation as UpdatePlacement; only the click dispatch is bypassed.
            player.ConsumeResources(piece.m_resources, 0);
            Check(hideBefore - InventoryCount("DeerHide") == 6, "native construction consumes exactly six DeerHide");
            Check(scrapsBefore - InventoryCount("LeatherScraps") == 10, "native construction consumes exactly ten LeatherScraps");
            rig.transform.SetPositionAndRotation(oldPos, oldRot); rig.enabled = oldEnabled;
            player.UnequipItem(hammer, false);
            yield return Wait(() => !player.InPlaceMode(), "Hammer unequipped leaves placement mode");
            yield return null;
            treadmill = null;
            foreach (var candidate in UnityEngine.Object.FindObjectsOfType(treadmillType))
            {
                var behaviour = candidate as Behaviour;
                if (behaviour && behaviour.GetComponent<ZNetView>() && behaviour.GetComponent<ZNetView>().IsValid() &&
                    Vector3.Distance(behaviour.transform.position, placedAt) < 1f) treadmill = behaviour;
            }
            Check(treadmill && treadmill.GetComponent<Piece>().GetCreator() == player.GetPlayerID(), "native placement created networked player-authored treadmill");
            var wear = treadmill.GetComponent<WearNTear>(); wear.enabled = false;
            Log("FIXTURE support/weather decay disabled on placed treadmill, never on gameplay/attachment components");
            yield return Wait(() => (bool)Get(treadmill, "initialized"), "placed treadmill RPC lifecycle initialized");
        }
        void DrainComparators(int severity)
        {
            float run = 100f, jump = 100f, attack = 100f;
            player.GetSEMan().ModifyRunStaminaDrain(100f, ref run, Vector3.forward);
            player.GetSEMan().ModifyJumpStaminaUsage(100f, ref jump);
            player.GetSEMan().ModifyAttackStaminaUsage(100f, ref attack);
            float[] expected = { 100f, 115f, 130f, 150f };
            Log("NATIVE_MODIFIERS severity=" + severity + " run=" + run + " jump=" + jump + " attack=" + attack);
            Check(Mathf.Abs(run - expected[severity]) < .02f, "native SEMan running drain severity " + severity + " is " + expected[severity] + "%");
            Check(Mathf.Abs(jump - 100f) < .02f && Mathf.Abs(attack - 100f) < .02f, "jump and attack native stamina costs unchanged at severity " + severity);
        }
        bool HudShowsStrain(int severity)
        {
            var effect = player.GetSEMan().GetStatusEffect(("FitnessRunStrain" + severity).GetStableHashCode());
            if (!effect || !Hud.instance || !Hud.instance.IsVisible()) return false;
            string label = Localization.instance.Localize(effect.m_name);
            foreach (var text in Hud.instance.m_statusEffectListRoot.GetComponentsInChildren<TMPro.TMP_Text>())
                if (text.isActiveAndEnabled && text.text == label) return true;
            return false;
        }
        IEnumerator RecoverFully(string label)
        {
            Stage(label); drive = false; fixedSkill = -1f; Controls(Vector3.zero);
            float deadline = Time.realtimeSinceStartup + 45f;
            while (true)
            {
                // Level zero may still contain sub-onset fatigue. Observe native Save, not the pure model.
                player.Save(new ZPackage());
                float load = player.m_customData.TryGetValue(LoadKey, out var text) ? float.Parse(text, CultureInfo.InvariantCulture) : 0f;
                if (Level == 0 && load < .002f) break;
                if (Time.realtimeSinceStartup > deadline) throw new TimeoutException("native idle recovery clears severity and partial load");
                yield return new WaitForSeconds(.25f);
            }
            Check(PersistedLoad() < .02f, "native idle recovery removes persistent load");
        }
        bool NativeMessagesSettled()
        {
            var hud = MessageHud.instance;
            if (!hud) return false;
            if (((ICollection)Get(hud, "m_unlockMsgQueue")).Count != 0 ||
                ((ICollection)Get(hud, "m_msgQeue")).Count != 0 ||
                ((ICollection)Get(hud, "m_biomeFoundQueue")).Count != 0 ||
                ((ICollection)Get(hud, "_crossFadeTextBuffer")).Count != 0 ||
                (GameObject)Get(hud, "m_biomeMsgInstance")) return false;
            foreach (GameObject message in (IList)Get(hud, "m_unlockMessages"))
                if (message) return false;
            return hud.m_messageText.canvasRenderer.GetAlpha() < .01f &&
                hud.m_messageCenterText.canvasRenderer.GetAlpha() < .01f;
        }
        IEnumerator VideoTraining()
        {
            Stage("video-native-food-preparation");
            float baseHealth = player.m_baseHP, baseStamina = player.m_baseStamina, regen = player.m_staminaRegen;
            Check(!(bool)Get(player, "m_godMode") && regen > 0f && fixedSkill < 0f, "video uses ordinary player resources, native regeneration and unfrozen skills");
            Check(player.GetFoods().Count == 0, "fresh disposable video character has no pre-existing foods");
            foreach (string name in new[] { "Salad", "Bread", "OnionSoup" })
            {
                var food = Add(name, 1);
                Check(food.m_shared.m_food > 0f && food.m_shared.m_foodStamina > 0f && player.CanEat(food, false),
                    "registered native " + name + " has ordinary food health/stamina and is edible");
                int before = InventoryCount(name);
                Check(player.ConsumeItem(player.GetInventory(), food), "native ConsumeItem eats " + name);
                Check(InventoryCount(name) == before - 1, "native food consumption removes one " + name);
                Log("VIDEO_NATIVE_FOOD prefab=" + name + " health=" + food.m_shared.m_food + " stamina=" + food.m_shared.m_foodStamina +
                    " duration=" + food.m_shared.m_foodBurnTime);
            }
            Check(player.GetFoods().Count == 3, "three actual native foods active");
            SetSkill(0f);
            xp.Clear();
            Controls(Vector3.zero);
            // Public native API clears queued discovery notices; active notices finish their real animations.
            yield return new WaitForSecondsRealtime(1f);
            Check(MessageHud.instance, "native MessageHud available for discovery notification cleanup");
            MessageHud.instance.ClearUnlockQueue();
            yield return Wait(NativeMessagesSettled, "native discovery, food and biome notifications finish before recording", 45f);
            yield return Wait(() => player.GetStamina() >= player.GetMaxStamina() - .25f,
                "ordinary native stamina regeneration fills the food-expanded pool before mounting", 50f);
            float foodHealth = baseHealth, foodStamina = baseStamina;
            foreach (var food in player.GetFoods()) { foodHealth += food.m_health; foodStamina += food.m_stamina; }
            Check(Mathf.Abs(player.GetMaxHealth() - foodHealth) < .1f && Mathf.Abs(player.GetMaxStamina() - foodStamina) < .1f,
                "video maximum health and stamina are exactly native base values plus active native foods");
            Check(player.m_baseHP == baseHealth && player.m_baseStamina == baseStamina && player.m_staminaRegen == regen,
                "food preparation never alters base health, base stamina or native regeneration");
            var rig = GameCamera.instance;
            Check(rig && Hud.instance && Hud.instance.IsVisible(), "real GameCamera and native HUD visible throughout video");
            bool oldEnabled = rig.enabled;
            Vector3 oldPosition = rig.transform.position; Quaternion oldRotation = rig.transform.rotation;
            rig.enabled = false;
            Vector3 target = treadmill.transform.position + Vector3.up;
            rig.transform.SetPositionAndRotation(target + new Vector3(-4f, 2.5f, -4f), Quaternion.LookRotation(new Vector3(4f, -2.5f, 4f)));
            Check(!(bool)Get(player, "m_run"), "video starts without held Run input");
            float movieStarted = Time.realtimeSinceStartup;
            Stage("video-training");
            // Allow the external window recorder to attach before native interaction starts the treadmill.
            yield return new WaitForSecondsRealtime(1f);
            yield return Begin();
            var collisions = AttachedColliders();
            float staminaBefore = player.GetStamina(), skillBefore = Skill().m_level, accumulatorBefore = Skill().m_accumulator;
            float trainingStarted = Time.realtimeSinceStartup, nextLog = trainingStarted;
            bool noRunInput = true, sawRunning = false, sawStaminaHud = false, sawStrainHud = false;
            int maximumSeverity = 0;
            while (player.IsAttached() && !(bool)Get(treadmill, "recovering"))
            {
                noRunInput &= !(bool)Get(player, "m_run");
                sawRunning |= player.IsRunning() && player.GetComponentInChildren<Animator>().GetFloat(ZSyncAnimation.GetHash("forward_speed")) > 1f;
                sawStaminaHud |= Hud.instance.IsVisible() && Hud.instance.m_staminaBar2Root.gameObject.activeInHierarchy &&
                    Hud.instance.m_staminaAnimator.GetBool("Visible") && !string.IsNullOrEmpty(Hud.instance.m_staminaText.text);
                maximumSeverity = Mathf.Max(maximumSeverity, Level);
                sawStrainHud |= Level > 0 && HudShowsStrain(Level);
                if (Time.realtimeSinceStartup >= nextLog)
                {
                    Log("VIDEO_NATIVE_PROGRESS elapsed=" + (Time.realtimeSinceStartup - trainingStarted).ToString("F2", CultureInfo.InvariantCulture) +
                        " health=" + player.GetHealth() + "/" + player.GetMaxHealth() + " stamina=" + player.GetStamina() + "/" + player.GetMaxStamina() +
                        " Run=" + Skill().m_level + " accumulator=" + Skill().m_accumulator + " severity=" + Level + " runInput=" + Get(player, "m_run"));
                    nextLog = Time.realtimeSinceStartup + 1f;
                }
                if (Time.realtimeSinceStartup - trainingStarted > 35f) throw new TimeoutException("native food stamina must naturally exhaust within the continuous 25-40 second movie");
                yield return null;
            }
            float exhaustedStamina = player.GetStamina(), elapsed = Time.realtimeSinceStartup - trainingStarted;
            Check(player.IsAttached() && (bool)Get(treadmill, "recovering") && !player.IsRunning()
                && exhaustedStamina < 1f && exhaustedStamina < staminaBefore - 100f,
                "natural food stamina exhaustion transitions to attached recovery walking without forced drain or refill");
            Check(noRunInput && !(bool)Get(player, "m_run") && sawRunning,
                "native treadmill running animation and stamina drain require no held Run input throughout");
            Check(xp.Count > 0 && (Skill().m_level > skillBefore || Skill().m_accumulator > accumulatorBefore),
                "unfrozen native Run skill level or XP accumulator genuinely progresses during the movie");
            Check(sawStaminaHud && sawStrainHud && maximumSeverity >= 2,
                "native visible stamina bar and actual strain status HUD accompany continuous training");
            Check(player.m_baseHP == baseHealth && player.m_baseStamina == baseStamina && player.m_staminaRegen == regen &&
                !(bool)Get(player, "m_godMode") && !player.IsDead(), "video finishes alive with ordinary unchanged base resources and regeneration");
            Log("VIDEO_NATIVE_EXHAUSTION trainingSeconds=" + elapsed.ToString("F2", CultureInfo.InvariantCulture) +
                " staminaBefore=" + staminaBefore + " staminaAtExhaustion=" + exhaustedStamina + " RunBefore=" + skillBefore +
                " RunAfter=" + Skill().m_level + " accumulatorAfter=" + Skill().m_accumulator + " xpPulses=" + xp.Count);
            yield return new WaitForSecondsRealtime(Mathf.Max(4f, 26f - (Time.realtimeSinceStartup - movieStarted)));
            yield return Stop("video recovery-walk interact stop");
            Check(Time.realtimeSinceStartup - movieStarted >= 25f && Time.realtimeSinceStartup - movieStarted <= 40f,
                "continuous actual-game movie lasts 25-40 seconds, including readable native exhaustion hold");
            Stage("video-finished");
            rig.transform.SetPositionAndRotation(oldPosition, oldRotation); rig.enabled = oldEnabled;
            Check(nativeErrors == 0, "no Unity native error/exception/assert logs during video scenario");
            Log("BOUNDARY: actual native single-player treadmill gameplay and real food/resources/Run progression; fixed showcase camera, accelerated 10-second strain onset, " +
                "600-second recovery and 1.5x treadmill XP explicitly disclosed. Native Hammer placement/ConsumeResources are the fixture, not OS input.");
        }
        IEnumerator Run()
        {
            Stage("loader-registration");
            yield return Wait(() => FejdStartup.instance && FejdStartup.instance.m_mainMenu.activeInHierarchy, "startup main menu", 180f);
            Check(Chainloader.PluginInfos.TryGetValue("jarki.fitness", out var info) && info.Instance, "Fitness loader registered a real plugin instance");
            fitness = (BaseUnityPlugin)info.Instance;
            treadmillType = fitness.GetType().Assembly.GetType("Fitness.Treadmill", true);
            Check(Get(fitness.GetType(), "trainingMultiplier") != null, "Fitness Awake initialized configuration (loader errors are not hidden)");
            observer = new Harmony("local.fitness.ingame-tests.observer");
            observer.Patch(typeof(Skills).GetMethod("RaiseSkill", new[] { typeof(Skills.SkillType), typeof(float) }),
                prefix: new HarmonyMethod(typeof(Host), nameof(XpBefore)), postfix: new HarmonyMethod(typeof(Host), nameof(XpAfter)));
            observer.Patch(AccessTools.DeclaredMethod(typeof(Player), "CheckRun"),
                prefix: new HarmonyMethod(typeof(Host), nameof(ObserveRecoveryRun)));
            Config("Treadmill", "RunExperienceMultiplier", 1.5f);
            // Existing diagnostic onset/ratio scenarios use a controlled unit multiplier.
            Config("Treadmill", "StrainBuildupMultiplier", biomeShowcase ? 2f : 1f);
            Config("Muscle strain", "SecondsPerLevel", biomeShowcase ? 60f : 10f);
            Config("Muscle strain", "RecoverySecondsPerLevel", recordVideo || biomeShowcase ? 600f : 10f);
            Config("Muscle strain", "StrainFreeRunSkill", 75f);
            Stage("disposable-world");
            var menu = FejdStartup.instance;
            var profile = new PlayerProfile(profileName, FileHelpers.FileSource.Local);
            profile.SetName("Fitness Lab"); profile.m_firstSpawn = false;
            Check(profile.Save(), "new disposable local character saved");
            Set(menu, "m_profiles", new List<PlayerProfile> { profile }); Set(menu, "m_profileIndex", 0);
            Game.SetProfile(profileName, FileHelpers.FileSource.Local);
            var world = new World(worldName, "fitness") { m_fileSource = FileHelpers.FileSource.Local, m_needsDB = false };
            world.SaveWorldFWLData(DateTime.Now); Set(menu, "m_world", world);
            menu.m_openServerToggle.SetIsOnWithoutNotify(false); menu.m_publicServerToggle.SetIsOnWithoutNotify(false);
            menu.m_crossplayServerToggle.SetIsOnWithoutNotify(false); menu.m_serverPassword.text = "";
            menu.OnWorldStart();
            yield return Wait(() => !FejdStartup.instance && Game.instance && !Game.instance.WaitingForRespawn() && Player.m_localPlayer &&
                !Player.m_localPlayer.IsTeleporting() && !Player.m_localPlayer.InCutscene() && !Player.m_localPlayer.InIntro(), "disposable world spawn", 300f);
            Check(ZNet.IsSinglePlayer && ZNet.instance.GetWorld().m_name == worldName && ZNet.instance.GetWorld().m_fileSource == FileHelpers.FileSource.Local,
                "local single-player disposable world guard; no multiplayer claim");
            player = Player.m_localPlayer; player.SetGodMode(false); player.GetInventory().RemoveAll();
            nativeBaseStamina = player.m_baseStamina;
            nativeStaminaRegen = player.m_staminaRegen;
            if (TextViewer.instance) TextViewer.instance.Hide();
            yield return new WaitForSeconds(3f);
            if (biomeShowcase)
            {
                yield return BiomeShowcase();
                yield break;
            }
            Stage("arena-fixtures");
            foreach (var character in new List<Character>(Character.GetAllCharacters()))
                if (character != player && character.GetComponent<BaseAI>()) character.GetComponent<BaseAI>().enabled = false;
            player.GetComponent<PlayerController>().enabled = false;
            if (!recordVideo)
            {
                player.m_baseHP = 1000f; player.SetMaxHealth(1000f, false); player.SetHealth(1000f);
                player.m_baseStamina = 10000f; player.SetMaxStamina(10000f, false); player.m_staminaRegen = 0f;
                FillStamina();
                Log("FIXTURE: local PlayerController disabled; all movement uses native SetControls. HP/baseHP=1000, base/max stamina=10000, stamina regen=0. " +
                    "No god mode, no free crafting, no attachment-field writes, no answer patches, no world time acceleration. AI disabled only on existing non-player creatures.");
            }
            else
                Log("VIDEO FIXTURE: local PlayerController disabled; native SetControls sends no Run input. Ordinary unmodified player health/stamina and regeneration; " +
                    "native Salad/Bread/OnionSoup consumption, unfrozen real Run progression. No resource setters/refills, god mode, free crafting, attachment-field writes or answer patches. " +
                    "DEMO CONFIG DISCLOSURE: accelerated strain onset 10 seconds per level, recovery 180 seconds per level, native Run XP multiplier 1.5; no world time acceleration.");
            EnvMan.instance.m_debugTimeOfDay = true; EnvMan.instance.m_debugTime = .5f;
            arena = player.transform.position + new Vector3(45f, 0f, 0f);
            player.transform.position = arena + Vector3.up * 5f;
            yield return new WaitForSeconds(3f);
            arena.y = ZoneSystem.instance.GetGroundHeight(arena) + 4f;
            for (int x = -5; x <= 5; ++x)
                for (int z = -5; z <= 5; ++z) Fixture("wood_floor", arena + new Vector3(x * 2f, 0f, z * 2f));
            var bench = Fixture("piece_workbench", arena + new Vector3(6f, .05f, 2f)).GetComponent<CraftingStation>();
            player.AddKnownStation(bench);
            Vector3 standingAt = arena + new Vector3(0f, .2f, -3.5f);
            var standingBody = player.GetComponent<Rigidbody>();
            standingBody.position = standingAt; standingBody.linearVelocity = Vector3.zero;
            player.transform.position = standingAt;
            Physics.SyncTransforms();
            yield return Wait(() => CraftingStation.HaveBuildStationInRange(bench.m_name, player.transform.position),
                "native workbench Start registers a real in-range build station before ingredient rejection checks", 5f);
            hammer = Add("Hammer", 1);
            Add("Wood", 40); Add("RoundLog", 20); Add("BronzeNails", 20);
            var recipe = Prefab("FitnessTreadmill").GetComponent<Piece>();
            Add("LeatherScraps", 20);
            Check(InventoryCount("DeerHide") == 0 && !player.HaveRequirements(recipe, Player.RequirementMode.CanBuild),
                "native HaveRequirements rejects treadmill when DeerHide is missing despite all four other materials");
            Add("DeerHide", 12);
            Check(player.HaveRequirements(recipe, Player.RequirementMode.CanBuild), "native HaveRequirements accepts all five materials at the real workbench");
            player.GetInventory().RemoveItem(Prefab("LeatherScraps").GetComponent<ItemDrop>().m_itemData.m_shared.m_name, 20);
            Check(InventoryCount("LeatherScraps") == 0 && !player.HaveRequirements(recipe, Player.RequirementMode.CanBuild),
                "native HaveRequirements rejects treadmill when LeatherScraps are missing despite all four other materials");
            Add("LeatherScraps", 20);
            Log("FIXTURE: 22x22m native wood-floor platform at terrain+4m, native workbench, two treadmill ingredient sets and Hammer granted; fixture WearNTear disabled, midday lighting fixed.");
            yield return new WaitForSeconds(1f);
            Check(player.IsOnGround() && !player.IsSwimming() && !player.IsEncumbered(), "arena fixture grounded, dry, unencumbered");
            var prefab = Prefab("FitnessTreadmill");
            Check(prefab.GetComponent(treadmillType) && hammer.m_shared.m_buildPieces.m_pieces.Contains(prefab), "Fitness treadmill registered in native Hammer table");
            for (int level = 1; level <= 3; ++level)
            {
                var effect = ObjectDB.instance.GetStatusEffect(("FitnessRunStrain" + level).GetStableHashCode());
                Check(effect && effect.m_icon && effect is SE_Stats, "strain " + level + " registered with native Run icon and SE_Stats");
            }
            yield return PlaceTreadmill("01-native-hammer-placement");
            if (recordVideo)
            {
                yield return VideoTraining();
                yield break;
            }
            yield return CheckDistance();
            yield return CheckTreadmillConfiguration();
            Stage("02-thin-native-training"); SetSkill(40f); FillStamina();
            yield return Begin();
            float before = player.GetStamina();
            yield return new WaitForSeconds(.5f);
            Check(player.GetStamina() < before && player.IsRunning(), "real attached training consumes stamina and reports running");
            Check(player.GetComponentInChildren<Animator>().GetFloat(ZSyncAnimation.GetHash("forward_speed")) > 1f, "native running animation forward blend nonzero");
            yield return FramedCapture("02-training-native-animation-and-hud");
            yield return Stop("interact-stop");
            Stage("03-native-xp-control");
            yield return XpPulse(false); float control = xp[0];
            yield return Begin();
            yield return XpPulse(true); float boosted = xp[0];
            Log("XP_RATIO control=" + control.ToString("R", CultureInfo.InvariantCulture) + " treadmill=" + boosted.ToString("R", CultureInfo.InvariantCulture) + " ratio=" + boosted / control);
            Check(Mathf.Abs(boosted / control - 1.5f) < .002f, "native CheckRun XP accumulator pulse is default 1.5x ordinary-run control at identical Run 40");
            Config("Treadmill", "RunExperienceMultiplier", 2.25f);
            yield return XpPulse(true); float configured = xp[0];
            Check(Mathf.Abs(configured / control - 2.25f) < .002f, "configurable native treadmill XP accumulator pulse is 2.25x ordinary control");
            yield return Stop("xp-stage-stop");
            yield return XpPulse(false);
            Check(Mathf.Abs(xp[0] / control - 1f) < .002f, "ordinary running unaffected by configured treadmill multiplier");
            Config("Treadmill", "RunExperienceMultiplier", 1.5f);
            yield return RecoverFully("04-pre-strain-native-recovery");
            Stage("05-strain-native-training"); SetSkill(0f); fixedSkill = 0f; FillStamina();
            Log("FIXTURE Run level held at 0 during strain timing; XP accumulator still native and observed, no direct StrainState manipulation");
            DrainComparators(0);
            yield return Begin();
            for (int severity = 1; severity <= 3; ++severity)
            {
                int expected = severity;
                yield return Wait(() => Level == expected, "native treadmill CheckRun produces strain level " + severity, 16f);
                Check(player.IsAttached() && player.GetStamina() > 0f, "strain " + severity + " arose during real live training");
                DrainComparators(severity);
                yield return Wait(() => HudShowsStrain(expected), "native HUD renders the active strain severity label " + severity, 3f);
                Check(HudShowsStrain(severity), "native status HUD contains actual severity " + severity + " label");
                yield return FramedCapture("05-strain-" + severity + "-native-hud");
            }
            fixedSkill = -1f;
            yield return Stop("strain-stage-stop");
            Stage("06-walking-cost-control");
            // Start the outdoor comparator clear of the treadmill's solid rails.
            // Its circular target depends on Time.time; an arbitrary post-dismount
            // position can otherwise send the avatar into the piece instead of walking.
            var walkBody = player.GetComponent<Rigidbody>();
            Vector3 clearWalkAt = arena + new Vector3(0f, .2f, 4f);
            walkBody.position = clearWalkAt; walkBody.linearVelocity = Vector3.zero;
            player.transform.position = clearWalkAt; Physics.SyncTransforms();
            yield return new WaitForSeconds(.3f);
            Check(player.IsOnGround() && !player.IsAttached() && player.CanMove()
                && (bool)Call(player, "TakeInput"), "walking comparator starts grounded and input-ready on clear floor");
            float walkBefore = player.GetStamina();
            // Use a straight path: a moving circular target can return near its
            // starting point despite real walking, invalidating endpoint distance.
            Vector3 walkAt = player.transform.position;
            float walkUntil = Time.time + 1.5f;
            while (Time.time < walkUntil)
            {
                player.SetLookDir(Vector3.right);
                Controls(Vector3.forward);
                yield return null;
            }
            Controls(Vector3.zero);
            Log("WALK_NATIVE from=" + walkAt + " to=" + player.transform.position
                + " distance=" + Vector3.Distance(walkAt, player.transform.position));
            Check(Vector3.Distance(walkAt, player.transform.position) > .3f && Level > 0, "walking comparator really moves with native strain active");
            Check(Mathf.Abs(walkBefore - player.GetStamina()) < .1f, "ordinary walking consumes no additional stamina while strained (regeneration disabled)");
            Stage("07-persistence-and-recovery");
            float initialLoad = PersistedLoad(); int initialSeverity = Level;
            var saved = new ZPackage(); player.Save(saved); byte[] bytes = saved.GetArray();
            File.WriteAllBytes(Path.Combine(dir, "disposable-player-strain.pkg"), bytes);
            Log("PERSIST saved native Player.Save bytes=" + bytes.Length + " load=" + initialLoad + " severity=" + initialSeverity);
            Check(initialSeverity == 3 && initialLoad > 2f && initialLoad < 3.2f, "save fixture preserves partial load with latched severity III");
            float recoveryAt = Time.time;
            yield return Wait(() => Level == 2, "native configured ten-second severity recovery", 12f);
            float elapsed = Time.time - recoveryAt;
            Log("RECOVERY remainingLoadAtStart=" + initialLoad + " elapsed=" + elapsed + " expectedRemainingSeconds=" + (initialLoad - 2f) * 10f);
            Check(Mathf.Abs(elapsed - (initialLoad - 2f) * 10f) < .75f, "native recovery follows configured 10 seconds per full load level");
            yield return RecoverFully("08-complete-native-recovery");
            Stage("09-player-load-restores-load-and-severity");
            // Vanilla Load expects a newly constructed player's empty station dictionary.
            // This fixture reuses its live avatar; clear only that vanilla constructor precondition.
            ((IDictionary)Get(player, "m_knownStations")).Clear();
            Log("FIXTURE known stations cleared before same-avatar native Load; strain cache/data untouched");
            player.Load(new ZPackage(bytes));
            hammer = null;
            foreach (var item in player.GetInventory().GetAllItems())
                if (item.m_dropPrefab && item.m_dropPrefab.name == "Hammer") { hammer = item; break; }
            Check(hammer != null, "native inventory load restored Hammer identity for later replacement placement");
            yield return null;
            float restored = PersistedLoad();
            Check(Mathf.Abs(restored - initialLoad) < .15f && int.Parse(player.m_customData[SeverityKey], CultureInfo.InvariantCulture) == initialSeverity,
                "native Player.Load restores saved partial load and latched severity after live recovery cleared cached state");
            yield return Wait(() => Level == initialSeverity, "native recovery tick reapplies loaded status effect", 2f);
            yield return FramedCapture("09-restored-strain-hud");
            // Player.Load restores the fixture stats too; reassert effective values before later comparators.
            player.m_baseStamina = 10000f; player.SetMaxStamina(10000f, false); player.m_staminaRegen = 0f; FillStamina();
            yield return RecoverFully("10-prepare-ordinary-sprint");
            Stage("11-ordinary-sprint-strain"); SetSkill(0f); fixedSkill = 0f;
            player.transform.position = arena + new Vector3(-4f, .15f, -4f);
            Vector3 sprintAt = player.transform.position;
            drive = true; sprint = true;
            yield return Wait(() => Level == 1, "ordinary native SetControls sprint causes new strain", 17f);
            Check(!player.IsAttached() && player.IsRunning() && Vector3.Distance(sprintAt, player.transform.position) > 1f, "ordinary sprint comparator actually runs and moves without treadmill");
            drive = false; Controls(Vector3.zero); fixedSkill = -1f;
            yield return RecoverFully("12-prepare-skill75-immunity");
            Stage("13-skill75-native-immunity"); SetSkill(75f); fixedSkill = 75f; FillStamina();
            float immuneBefore = player.GetStamina();
            drive = true; sprint = true;
            yield return new WaitForSeconds(12f);
            Check(player.IsRunning() && !player.IsAttached() && player.GetStamina() < immuneBefore - 10f, "skill75 fixture still performs stamina-consuming native ordinary sprint");
            drive = false; Controls(Vector3.zero); fixedSkill = -1f;
            Check(Level == 0 && PersistedLoad() < .02f, "Run75 immunity prevents new strain beyond the ten-second onset interval");
            Stage("13b-skill75-treadmill-immunity"); SetSkill(75f); fixedSkill = 75f;
            yield return Begin();
            immuneBefore = player.GetStamina();
            yield return new WaitForSeconds(11f);
            Check(player.IsAttached() && player.IsRunning() && player.GetStamina() < immuneBefore - 10f,
                "Run75 treadmill immunity fixture still performs stamina-consuming native training");
            Check(Level == 0 && PersistedLoad() < .02f, "Run75 treadmill training creates no new strain beyond onset interval");
            fixedSkill = -1f;
            yield return Stop("immune-training-stop");
            Stage("14-native-move-dismount");
            yield return Begin(); var collisions = AttachedColliders();
            Controls(Vector3.forward);
            yield return Detached("movement SetControls", treadmill, collisions);
            Stage("15-native-jump-dismount");
            yield return Begin(); collisions = AttachedColliders();
            Controls(Vector3.zero, false, true);
            yield return Detached("jump SetControls", treadmill, collisions);
            Stage("16-exhaustion-recovery");
            SetSkill(0f);
            yield return Begin(); collisions = AttachedColliders();
            yield return new WaitForSeconds(.5f);
            player.UseStamina(player.GetStamina() + 1f);
            yield return Wait(() => (bool)Get(treadmill, "recovering") && !player.IsRunning(),
                "exhaustion switches to walking instead of dismounting");
            Check(player.IsAttached() && Reserved(treadmill), "recovery retains attachment and reservation");
            float recoveryXp = Skill().m_accumulator, recoveryLoad = PersistedLoad();
            int recoveryPulses = xp.Count;
            Set(player, "m_stamina", player.GetMaxStamina() - 1f);
            yield return new WaitForSeconds(1.2f);
            Check(player.IsAttached() && !player.IsRunning() && (bool)Get(treadmill, "recovering"),
                "nearly full stamina does not resume running");
            Check(player.GetStamina() == player.GetMaxStamina() - 1f,
                "recovery walking neither consumes nor refunds stamina with fixture regeneration disabled");
            Check(Skill().m_accumulator == recoveryXp && xp.Count == recoveryPulses,
                "recovery walking grants no Run XP");
            Check(PersistedLoad() < recoveryLoad, "recovery walking heals strain rather than building sprint effort");
            Check(Mathf.Abs(player.GetComponentInChildren<Animator>().GetFloat(ZSyncAnimation.GetHash("forward_speed"))
                - player.m_walkSpeed) < .1f, "native animation uses walking speed during recovery");
            FillStamina();
            yield return Wait(() => player.IsRunning() && !(bool)Get(treadmill, "recovering"),
                "full stamina automatically resumes native running without interaction");
            Check(player.IsAttached() && Reserved(treadmill) && player.GetStamina() < player.GetMaxStamina(),
                "resumed running retains occupancy and consumes native stamina");
            player.UseStamina(player.GetStamina() + 1f);
            yield return Wait(() => (bool)Get(treadmill, "recovering"), "second exhaustion returns to recovery");
            Controls(Vector3.forward);
            yield return Detached("movement while recovering", treadmill, collisions);
            FillStamina();
            yield return Begin();
            yield return Wait(() => player.IsRunning(), "new session after recovery cancellation starts running");
            yield return Stop("recovery-reuse-stop");
            Stage("16a-natural-recovery-cycles");
            Check(nativeBaseStamina > 0f && nativeStaminaRegen > 0f && player.GetFoods().Count == 0,
                "natural recovery fixture preserves original unfed base stamina and native regeneration");
            float fixtureBase = player.m_baseStamina, fixtureRegen = player.m_staminaRegen;
            float fixtureOnset = (float)fitness.Config[new ConfigDefinition("Muscle strain", "SecondsPerLevel")].BoxedValue;
            float fixtureRecovery = (float)fitness.Config[new ConfigDefinition("Muscle strain", "RecoverySecondsPerLevel")].BoxedValue;
            Config("Treadmill", "StrainBuildupMultiplier", 2f);
            Config("Muscle strain", "SecondsPerLevel", 60f);
            Config("Muscle strain", "RecoverySecondsPerLevel", 600f);
            player.m_baseStamina = nativeBaseStamina;
            player.SetMaxStamina(nativeBaseStamina, false);
            player.m_staminaRegen = nativeStaminaRegen;
            Set(player, "m_stamina", nativeBaseStamina);
            SetSkill(0f);
            yield return Begin();
            for (int cycle = 1; cycle <= 2; ++cycle)
            {
                yield return Wait(() => player.IsRunning(), "natural cycle starts running");
                yield return Wait(() => (bool)Get(treadmill, "recovering") && !player.IsRunning(),
                    "native consumption naturally exhausts stamina and starts walking", 60f);
                float startStamina = player.GetStamina(), walkingXp = Skill().m_accumulator;
                float walkingLoad = PersistedLoad();
                int walkingPulses = xp.Count;
                Check(player.IsAttached() && Reserved(treadmill) && startStamina < 1f,
                    "natural exhaustion remains attached and reserved with nearly empty stamina");
                recoveryResumeStamina = -1f;
                observeRecoveryResume = true;
                yield return FramedCapture("16a-natural-walking-" + cycle);
                yield return new WaitForSeconds(1f);
                Check((bool)Get(treadmill, "recovering") && !player.IsRunning(),
                    "natural walking continues while stamina is only partly recovered");
                Check(Skill().m_accumulator == walkingXp && xp.Count == walkingPulses,
                    "natural recovery walking pauses native Run XP pulses");
                Check(PersistedLoad() < walkingLoad, "natural recovery walking heals strain at production recovery rate");
                yield return Wait(() => player.GetStamina() > startStamina + 1f,
                    "native regeneration restores stamina without fixture writes", 15f);
                Log("RECOVERY_NATIVE_WALK cycle=" + cycle + " staminaFrom=" + startStamina
                    + " staminaNow=" + player.GetStamina() + " max=" + player.GetMaxStamina()
                    + " xpPulses=" + xp.Count + " loadFrom=" + walkingLoad + " loadNow=" + PersistedLoad());
                yield return Wait(() => recoveryResumeStamina >= 0f && player.IsRunning(),
                    "native regeneration automatically resumes running without interaction or refill", 60f);
                Check(recoveryResumeStamina == player.GetMaxStamina(),
                    "first resumed native CheckRun observes exactly full current maximum stamina");
                Check(player.IsAttached() && Reserved(treadmill), "natural automatic resume retains occupancy");
                yield return FramedCapture("16a-natural-running-" + cycle);
                Check(player.m_baseStamina == nativeBaseStamina && player.m_staminaRegen == nativeStaminaRegen,
                    "natural cycles never alter original stamina pool or regeneration");
            }
            yield return Stop("natural-recovery-interact-stop");
            player.m_baseStamina = fixtureBase;
            player.SetMaxStamina(fixtureBase, false);
            player.m_staminaRegen = fixtureRegen;
            Config("Treadmill", "StrainBuildupMultiplier", 1f);
            Config("Muscle strain", "SecondsPerLevel", fixtureOnset);
            Config("Muscle strain", "RecoverySecondsPerLevel", fixtureRecovery);
            FillStamina();
            Stage("16b-inventory-training");
            SetSkill(30f); FillStamina();
            yield return Begin(); collisions = AttachedColliders();
            InventoryGui.instance.Show(null);
            yield return Wait(() => InventoryGui.IsVisible() && !(bool)Call(player, "TakeInput"),
                "native inventory is visible and blocks ordinary movement input", 3f);
            float inventoryStamina = player.GetStamina();
            float inventoryXp = Skill().m_accumulator;
            float inventoryLoad = PersistedLoad();
            yield return new WaitForSeconds(2.2f);
            Check(player.IsAttached() && player.IsRunning() && Reserved(treadmill),
                "inventory leaves treadmill attached, running and reserved");
            Check(player.GetStamina() < inventoryStamina - 1f,
                "native stamina consumption continues with inventory open");
            Check(Skill().m_accumulator > inventoryXp,
                "native Run XP continues with inventory open");
            Check(PersistedLoad() > inventoryLoad + .05f,
                "native strain buildup continues with inventory open");
            yield return Capture("16b-native-inventory-training");
            InventoryGui.instance.Hide();
            yield return Wait(() => !InventoryGui.IsVisible() && (bool)Call(player, "TakeInput"),
                "closing inventory restores ordinary input", 3f);
            Check(player.IsAttached() && player.IsRunning(),
                "closing inventory does not restart or interrupt the workout");
            yield return Stop("inventory-close-interact-stop");
            yield return Begin(); collisions = AttachedColliders();
            InventoryGui.instance.Show(null);
            yield return Wait(() => InventoryGui.IsVisible(), "inventory visible before blocking menu", 3f);
            Menu.instance.Show();
            yield return Wait(() => Menu.IsVisible(), "blocking menu visible over inventory", 3f);
            yield return Wait(() => !player.IsAttached(),
                "inventory allowance cannot override a simultaneously blocking menu");
            Menu.instance.Hide(); InventoryGui.instance.Hide();
            yield return Detached("menu over inventory", treadmill, collisions);
            yield return Wait(() => (bool)Call(player, "TakeInput"), "both UI layers closed restore input");
            Stage("17-menu-cancellation");
            yield return Begin(); collisions = AttachedColliders();
            Menu.instance.Show();
            // Menu.IsVisible uses native frame counters, not the root's immediate active state.
            yield return Wait(() => Menu.IsVisible() && !(bool)Call(player, "TakeInput"),
                "native menu visibility/input frame transition", 3f);
            Check(Menu.IsVisible() && !(bool)Call(player, "TakeInput"), "real native menu blocks gameplay input");
            yield return Wait(() => !player.IsAttached(), "native menu cancels training while gameplay is paused");
            Menu.instance.Hide();
            yield return Detached("native menu", treadmill, collisions);
            yield return Wait(() => (bool)Call(player, "TakeInput"), "menu closed restores native input");
            Stage("18-native-damage-stop");
            yield return Begin(); collisions = AttachedColliders();
            float health = player.GetHealth();
            var hit = new HitData(); hit.m_damage.m_blunt = 5f; hit.m_point = player.GetCenterPoint();
            player.Damage(hit);
            yield return Detached("native Damage", treadmill, collisions);
            Check(player.GetHealth() < health && !player.IsDead(), "real damage consequence observed independently of detach callback");
            Stage("19-disable-reenable-reuse");
            yield return Begin(); collisions = AttachedColliders(); treadmill.enabled = false;
            yield return Detached("component disable", treadmill, collisions);
            treadmill.enabled = true;
            yield return Begin();
            Check(player.IsAttached() && Reserved(treadmill), "same treadmill reusable after disable and re-enable");
            yield return Stop("reenable-reuse-stop");
            Stage("20-destruction");
            yield return Begin(); collisions = AttachedColliders();
            var destroyed = treadmill; var destroyedId = treadmill.GetComponent<ZNetView>().GetZDO().m_uid;
            ZNetScene.instance.Destroy(treadmill.gameObject);
            yield return Detached("networked destruction", destroyed, collisions);
            yield return Wait(() => !destroyed, "native networked piece destroyed");
            var instances = (IList)Get(treadmillType, "instances");
            foreach (var instance in instances) Check(!ReferenceEquals(instance, destroyed), "destroyed instance removed from production registry");
            Log("DESTROYED zdo=" + destroyedId + " native attachment ownership cleared; destroyed collider pairs no longer exist");
            yield return PlaceTreadmill("21-native-replacement-placement");
            yield return Begin();
            yield return FramedCapture("21-replacement-reuse");
            yield return Stop("replacement-reuse-stop");
            Stage("22-final-native-errors");
            Check(nativeErrors == 0, "no Unity native error/exception/assert logs during entire host run (launcher separately checks early BepInEx log)");
            Log("BOUNDARY: actual installed host, local single-player only; no multiplayer/network-peer claims. Native TryPlacePiece/PlacePiece and ConsumeResources used, not an OS Hammer click. " +
                "Native Player.Save/Load round-trip uses real package bytes on same live player after recovery; not a process restart. Rendering established only by captured GameCamera framebuffer for parent inspection.");
        }
    }
}
