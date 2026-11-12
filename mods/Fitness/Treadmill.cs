using System;
using System.Collections.Generic;
using HarmonyLib;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Fitness
{
    internal sealed class Treadmill : MonoBehaviour, Interactable, Hoverable
    {
        private const string PrefabName = "FitnessTreadmill";
        private const string RequestRpc = "FitnessRequestTreadmill";
        private const string HeartbeatRpc = "FitnessTreadmillHeartbeat";
        private const string LeaseRpc = "FitnessTreadmillLease";
        private const string ReleaseRpc = "FitnessReleaseTreadmill";
        private const float UseDistance = 3f;
        private const long LeaseTicks = 5 * TimeSpan.TicksPerSecond;
        private static readonly KeyValuePair<int, int> UserKey = ZDO.GetHashZDOID("FitnessTreadmillUser");
        private static readonly int PeerKey = "FitnessTreadmillPeer".GetStableHashCode();
        private static readonly int TokenKey = "FitnessTreadmillToken".GetStableHashCode();
        private static readonly int LeaseKey = "FitnessTreadmillLease".GetStableHashCode();
        private static readonly int ForwardSpeed = ZSyncAnimation.GetHash("forward_speed");
        private static readonly int SideSpeed = ZSyncAnimation.GetHash("sideway_speed");
        private static readonly int TurnSpeed = ZSyncAnimation.GetHash("turn_speed");
        private static readonly int OnGround = ZSyncAnimation.GetHash("onGround");
        private static readonly int InWater = ZSyncAnimation.GetHash("inWater");
        private static readonly int Flying = ZSyncAnimation.GetHash("flying");
        private static readonly int Encumbered = ZSyncAnimation.GetHash("encumbered");
        private static readonly List<Treadmill> instances = new List<Treadmill>();
        private static readonly AccessTools.FieldRef<Character, bool> RunInput =
            AccessTools.FieldRefAccess<Character, bool>("m_run");
        private static readonly AccessTools.FieldRef<Character, bool> Running =
            AccessTools.FieldRefAccess<Character, bool>("m_running");
        private delegate bool RunCheck(Player player, Vector3 direction, float dt);
        private static readonly RunCheck CheckRun = AccessTools.MethodDelegate<RunCheck>(
            AccessTools.DeclaredMethod(typeof(Player), "CheckRun"));
        private static readonly Func<Player, bool> TakeInput = AccessTools.MethodDelegate<Func<Player, bool>>(
            AccessTools.DeclaredMethod(typeof(Player), "TakeInput"));
        private static readonly Action<Player> UpdateAttach = AccessTools.MethodDelegate<Action<Player>>(
            AccessTools.DeclaredMethod(typeof(Player), "UpdateAttach"));
        private static readonly Action<Player> StopEmote = AccessTools.MethodDelegate<Action<Player>>(
            AccessTools.DeclaredMethod(typeof(Player), "StopEmote"));
        private static readonly Func<Player, float> RunSpeedFactor = AccessTools.MethodDelegate<Func<Player, float>>(
            AccessTools.DeclaredMethod(typeof(Player), "GetRunSpeedFactor"));
        private static CustomPiece registeredPiece;
        private static Treadmill active;
        private static Treadmill pending;
        private static int nextToken;
        private static bool checkingRun;

        [SerializeField] private Transform attachPoint;
        private ZNetView view;
        private TreadmillVisual visual;
        private Player runner;
        private Player requestingPlayer;
        private ZDOID userId;
        private int token;
        private long confirmedLease;
        private float requestTimeout;
        private float nextHeartbeat;
        private object[] userArguments;
        private bool initialized;

        internal static void Register()
        {
            if (registeredPiece != null) return;
            var source = PrefabManager.Instance.GetPrefab("wood_floor");
            if (!source) throw new InvalidOperationException("Fitness requires the vanilla wood_floor prefab.");
            var sourceWear = source.GetComponent<WearNTear>();
            if (!sourceWear || !sourceWear.m_new)
                throw new InvalidOperationException("Fitness requires wood_floor's intact wood visual.");
            Material wood = null;
            foreach (var renderer in sourceWear.m_new.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (renderer != sourceWear.m_snow && renderer != sourceWear.m_snowWorn
                    && renderer != sourceWear.m_snowBroken && renderer.sharedMaterial)
                {
                    wood = renderer.sharedMaterial;
                    break;
                }
            }
            if (!wood) throw new InvalidOperationException("Fitness could not find the vanilla wood floor material.");
            var rug = PrefabManager.Instance.GetPrefab("rug_deer");
            if (!rug) throw new InvalidOperationException("Fitness requires the vanilla deer-hide rug prefab.");
            Material hide = null;
            foreach (var renderer in rug.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (renderer.sharedMaterial && renderer.sharedMaterial.name == "deerhide_rug")
                {
                    hide = renderer.sharedMaterial;
                    break;
                }
            }
            if (!hide) throw new InvalidOperationException("Fitness could not find the native deer-hide rug material.");
            var piece = new CustomPiece(PrefabName, "wood_floor", new PieceConfig
            {
                Name = "Wooden treadmill",
                Description = "Run in place to train Run. Uses normal running stamina and causes muscle strain. Move or jump to stop.",
                PieceTable = "Hammer",
                Category = "Furniture",
                CraftingStation = "piece_workbench",
                Requirements = new[]
                {
                    new RequirementConfig("Wood", 20, 0, true),
                    new RequirementConfig("RoundLog", 10, 0, true),
                    new RequirementConfig("BronzeNails", 10, 0, true),
                    new RequirementConfig("DeerHide", 6, 0, true),
                    new RequirementConfig("LeatherScraps", 10, 0, true)
                }
            });
            var prefab = piece.PiecePrefab;
            if (!prefab) throw new InvalidOperationException("Fitness could not clone wood_floor.");
            // Keep the construction/network owners, not a hidden floor under the model.
            // This also deliberately removes old colliders, snap points and interactions.
            for (int i = prefab.transform.childCount - 1; i >= 0; --i)
                Object.DestroyImmediate(prefab.transform.GetChild(i).gameObject);
            foreach (var component in prefab.GetComponents<Component>())
                if (!(component is Transform) && !(component is Piece) && !(component is WearNTear)
                    && !(component is ZNetView) && !(component is ZSyncTransform))
                    Object.DestroyImmediate(component);
            prefab.transform.localScale = Vector3.one;
            var model = TreadmillVisual.Build(prefab, wood, hide);
            var wear = prefab.GetComponent<WearNTear>();
            if (!wear || !prefab.GetComponent<ZNetView>())
            {
                Object.DestroyImmediate(prefab);
                TreadmillVisual.Shutdown();
                throw new InvalidOperationException("Fitness wood_floor clone lacks construction networking.");
            }
            // WearNTear assumes all three state references exist whenever one exists.
            wear.m_new = wear.m_worn = wear.m_broken = model;
            wear.m_wet = null;
            wear.m_snow = wear.m_snowWorn = wear.m_snowBroken = null;
            wear.m_nonSolidRenderers.Clear();
            wear.m_fragmentRoots = new[] { model };
            wear.m_comOffset = new Vector3(0f, 0.65f, 0f);
            piece.Piece.m_usage = Piece.UsageTagFlags.Furniture;
            piece.Piece.m_clipEverything = false;
            piece.Piece.m_clipGround = false;
            piece.Piece.m_noClipping = true;
            piece.Piece.m_notOnTiltingSurface = true;
            piece.Piece.m_noInWater = true;
            var anchor = new GameObject("FitnessRunPoint");
            anchor.transform.SetParent(prefab.transform, false);
            anchor.transform.localPosition = new Vector3(0f, 0.5f, -0.25f);
            prefab.AddComponent<Treadmill>().attachPoint = anchor.transform;
            if (!PieceManager.Instance.AddPiece(piece))
            {
                Object.DestroyImmediate(prefab);
                TreadmillVisual.Shutdown();
                throw new InvalidOperationException("Fitness treadmill registration failed.");
            }
            registeredPiece = piece;
        }

        private void Start()
        {
            view = GetComponent<ZNetView>();
            // Placement previews intentionally have no ZDO and must never reserve a user.
            if (!view || !view.IsValid())
            {
                enabled = false;
                return;
            }
            visual = GetComponentInChildren<TreadmillVisual>(true);
            view.Register<ZDOID, int>(RequestRpc, RequestUse);
            view.Register<ZDOID, int>(HeartbeatRpc, RenewUse);
            view.Register<ZDOID, int, long>(LeaseRpc, ReceiveLease);
            view.Register<ZDOID, int>(ReleaseRpc, ReleaseUse);
            initialized = true;
            instances.Add(this);
        }

        internal static bool IsTraining(Player player)
        {
            return active && active.isActiveAndEnabled && active.runner == player && player
                && player.isActiveAndEnabled && !player.IsDead() && !player.IsTeleporting()
                && player.GetAttachPoint() == active.attachPoint;
        }

        internal static bool IsTrainingRun(Player player) => checkingRun && IsTraining(player);

        public string GetHoverName() => "Wooden treadmill";
        public float GetHoverOffset() => 0.7f;

        public string GetHoverText()
        {
            if (runner && IsTraining(runner))
                return Localization.instance.Localize("Wooden treadmill\n[<color=yellow><b>$KEY_Use</b></color>] Stop training\nMove or jump to stop");
            if (HasOccupant()) return "Wooden treadmill\nIn use";
            if (requestingPlayer) return "Wooden treadmill\nWaiting for treadmill owner…";
            return Localization.instance.Localize("Wooden treadmill\n[<color=yellow><b>$KEY_Use</b></color>] Train Run\nUses stamina; causes muscle strain");
        }

        public bool Interact(Humanoid user, bool hold, bool alt)
        {
            if (hold || !initialized || !view.IsValid()) return false;
            var player = user as Player;
            if (!player || player != Player.m_localPlayer) return false;
            if (runner == player)
            {
                StopTraining();
                return true;
            }
            if (!CanStart(player)) return false;
            if (HasOccupant())
            {
                player.Message(MessageHud.MessageType.Center, "This treadmill is in use.");
                return false;
            }
            if (active) active.StopTraining();
            if (pending) pending.CancelRequest();
            requestingPlayer = player;
            pending = this;
            userId = player.GetZDOID();
            token = unchecked(++nextToken);
            if (token == 0) token = unchecked(++nextToken);
            userArguments = new object[] { userId, token };
            requestTimeout = Time.time + 3f;
            view.InvokeRPC(RequestRpc, userArguments);
            return true;
        }

        public bool UseItem(Humanoid user, ItemDrop.ItemData item) => false;

        private bool CanStart(Player player)
        {
            return isActiveAndEnabled && player && player.isActiveAndEnabled
                && player == Player.m_localPlayer && !player.IsDead() && !player.IsTeleporting()
                && !player.IsAttached() && !player.IsEncumbered() && !player.IsCrouching()
                && !player.InDodge() && !player.InAttack() && !player.InPlaceMode()
                && !player.IsSwimming() && !player.IsFlying() && !player.IsDebugFlying()
                && player.CanMove() && player.HaveStamina() && TakeInput(player)
                && (player.transform.position - attachPoint.position).sqrMagnitude < UseDistance * UseDistance;
        }

        private static long NetworkTicks => ZNet.instance ? ZNet.instance.GetTime().Ticks : 0L;

        private bool HasOccupant()
        {
            return initialized && view && view.IsValid()
                && view.GetZDO().GetZDOID(UserKey) != ZDOID.None
                && view.GetZDO().GetLong(LeaseKey) > NetworkTicks;
        }

        private bool VerifyUser(long sender, ZDOID id)
        {
            var playerZdo = ZDOMan.instance.GetZDO(id);
            if (playerZdo == null || playerZdo.GetOwner() != sender) return false;
            var players = Player.GetAllPlayers();
            for (int i = 0; i < players.Count; ++i)
            {
                var player = players[i];
                if (player.GetZDOID() != id) continue;
                return !player.IsDead() && !player.IsTeleporting()
                    && (player.transform.position - attachPoint.position).sqrMagnitude < UseDistance * UseDistance;
            }
            return false;
        }

        private bool MatchesReservation(long sender, ZDOID id, int requestToken)
        {
            var zdo = view.GetZDO();
            return zdo.GetZDOID(UserKey) == id && zdo.GetLong(PeerKey) == sender
                && zdo.GetInt(TokenKey) == requestToken;
        }

        private void RequestUse(long sender, ZDOID id, int requestToken)
        {
            if (!initialized || !view.IsOwner()) return;
            long lease = 0L;
            if (isActiveAndEnabled && requestToken != 0 && VerifyUser(sender, id)
                && (!HasOccupant() || MatchesReservation(sender, id, requestToken)))
            {
                var zdo = view.GetZDO();
                zdo.Set(UserKey, id);
                zdo.Set(PeerKey, sender);
                zdo.Set(TokenKey, requestToken);
                lease = NetworkTicks + LeaseTicks;
                zdo.Set(LeaseKey, lease);
            }
            view.InvokeRPC(sender, LeaseRpc, id, requestToken, lease);
        }

        private void RenewUse(long sender, ZDOID id, int requestToken)
        {
            if (!initialized || !view.IsOwner()) return;
            long lease = 0L;
            if (isActiveAndEnabled && HasOccupant() && MatchesReservation(sender, id, requestToken)
                && VerifyUser(sender, id))
            {
                lease = NetworkTicks + LeaseTicks;
                view.GetZDO().Set(LeaseKey, lease);
            }
            view.InvokeRPC(sender, LeaseRpc, id, requestToken, lease);
        }

        private void ReceiveLease(long sender, ZDOID id, int requestToken, long lease)
        {
            if (!initialized || !view.IsValid() || sender != view.GetZDO().GetOwner()) return;
            if (id != userId || requestToken != token)
            {
                if (lease > 0L) view.InvokeRPC(ReleaseRpc, id, requestToken);
                return;
            }
            if (runner)
            {
                if (lease <= NetworkTicks) StopTraining();
                else confirmedLease = lease;
                return;
            }
            var player = requestingPlayer;
            if (pending != this || !player || Time.time > requestTimeout || !CanStart(player)
                || lease <= NetworkTicks)
            {
                if (player && lease <= NetworkTicks)
                    player.Message(MessageHud.MessageType.Center, "The treadmill is unavailable.");
                if (lease > 0L && pending != this)
                    view.InvokeRPC(ReleaseRpc, id, requestToken);
                CancelRequest();
                return;
            }
            confirmedLease = lease;
            requestingPlayer = null;
            pending = null;
            if (active) active.StopTraining();
            active = this;
            runner = player;
            runner.m_onDamaged += OnRunnerDamaged;
            runner.m_onDeath += OnRunnerDeath;
            runner.m_autoRun = false;
            runner.SetMoveDir(Vector3.zero);
            runner.SetRun(false);
            StopEmote(runner);
            // onGround is the real locomotion bool, not a chair/sit animation. Native
            // attachment owns position, gravity, ignored colliders and network parent.
            runner.AttachStart(attachPoint, gameObject, true, false, false, "onGround",
                new Vector3(-1.45f, -0.45f, 0f));
            nextHeartbeat = Time.time + 1f;
            if (runner.GetAttachPoint() != attachPoint) StopTraining();
        }

        private void ReleaseUse(long sender, ZDOID id, int requestToken)
        {
            if (initialized && view.IsOwner() && MatchesReservation(sender, id, requestToken))
                ClearReservation();
        }

        private void ClearReservation()
        {
            var zdo = view.GetZDO();
            zdo.Set(UserKey, ZDOID.None);
            zdo.Set(PeerKey, 0L);
            zdo.Set(TokenKey, 0);
            zdo.Set(LeaseKey, 0L);
        }

        private void Update()
        {
            if (!initialized || !view || !view.IsValid())
            {
                StopTraining();
                CancelRequest();
                return;
            }
            if (view.IsOwner() && view.GetZDO().GetZDOID(UserKey) != ZDOID.None && !HasOccupant())
                ClearReservation();
            if (requestingPlayer && (Time.time > requestTimeout || !CanStart(requestingPlayer)))
                CancelRequest();
            if (runner && (!IsTraining(runner) || !CanContinueTraining(runner) || !runner.HaveStamina()
                || confirmedLease <= NetworkTicks))
                StopTraining();
            if (runner && Time.time >= nextHeartbeat)
            {
                nextHeartbeat = Time.time + 1f;
                view.InvokeRPC(HeartbeatRpc, userArguments);
            }
            if (visual && HasOccupant())
                visual.Advance(runner ? runner.m_runSpeed * RunSpeedFactor(runner) : 6f, Time.deltaTime);
        }

        private static bool CanContinueTraining(Player player)
        {
            if (TakeInput(player)) return true;
            // Inventory alone blocks vanilla movement input, not an existing workout.
            // Retain every other native TakeInput gate; never enable normal controls.
            return InventoryGui.IsVisible() && !player.IsDead() && !player.InCutscene()
                && !player.IsTeleporting() && (!Chat.instance || !Chat.instance.HasFocus())
                && !Console.IsVisible() && !TextInput.IsVisible() && !StoreGui.IsVisible()
                && !Menu.IsVisible() && (!TextViewer.instance || !TextViewer.instance.IsVisible())
                && !Minimap.IsOpen() && !GameCamera.InFreeFly()
                && !PlayerCustomizaton.IsBarberGuiVisible()
                && Hud.instance && !Hud.instance.m_buildUi.SearchFieldFocused;
        }

        private void Train(Player player, float dt)
        {
            if (!IsTraining(player) || !view || !view.IsValid() || confirmedLease <= NetworkTicks
                || !CanContinueTraining(player) || !player.HaveStamina() || player.IsEncumbered()
                || player.IsCrouching() || player.InDodge() || !player.CanMove()
                || player.IsSwimming() || player.IsFlying() || player.IsDebugFlying())
            {
                StopTraining();
                return;
            }
            bool wasRun = RunInput(player);
            bool success;
            try
            {
                checkingRun = true;
                RunInput(player) = true;
                // Enter the patched native virtual method: equipment, SE_Stats, world
                // drain modifiers and the native >1-second Run XP timer remain intact.
                success = CheckRun(player, attachPoint.forward, dt);
            }
            finally
            {
                RunInput(player) = wasRun;
                checkingRun = false;
            }
            if (!success)
            {
                StopTraining();
                return;
            }
            Running(player) = true;
            // Character.UpdateWalking writes zero-speed animation/gravity for a fixed
            // attachment. Restore native attachment after that pass, then its run blend.
            UpdateAttach(player);
            var animation = player.GetZAnim();
            animation.SetFloat(ForwardSpeed, player.m_runSpeed * RunSpeedFactor(player));
            animation.SetFloat(SideSpeed, 0f);
            animation.SetFloat(TurnSpeed, 0f);
            animation.SetBool(OnGround, true);
            animation.SetBool(InWater, false);
            animation.SetBool(Flying, false);
            animation.SetBool(Encumbered, false);
        }

        private void OnRunnerDamaged(float damage, Character attacker)
        {
            if (damage > 0f) StopTraining();
        }

        private void OnRunnerDeath() => StopTraining();

        private void CancelRequest()
        {
            if (pending != this && ReferenceEquals(requestingPlayer, null)) return;
            if (pending == this) pending = null;
            requestingPlayer = null;
            if (view && view.IsValid() && userArguments != null)
                view.InvokeRPC(ReleaseRpc, userArguments);
            if (!runner) userArguments = null;
        }

        private void StopTraining()
        {
            var player = runner;
            // Clear identity before AttachStop's postfix re-enters this method.
            runner = null;
            if (active == this) active = null;
            if (!ReferenceEquals(player, null))
            {
                player.m_onDamaged -= OnRunnerDamaged;
                player.m_onDeath -= OnRunnerDeath;
                if (player)
                {
                    Running(player) = false;
                    if (player.GetAttachPoint() == attachPoint) player.AttachStop();
                    var animation = player.GetZAnim();
                    if (animation)
                    {
                        animation.SetFloat(ForwardSpeed, 0f);
                        animation.SetFloat(SideSpeed, 0f);
                        animation.SetFloat(TurnSpeed, 0f);
                    }
                }
                if (view && view.IsValid() && userArguments != null)
                    view.InvokeRPC(ReleaseRpc, userArguments);
                userArguments = null;
            }
            confirmedLease = 0L;
        }

        private void OnDisable()
        {
            StopTraining();
            CancelRequest();
            if (initialized && view && view.IsValid() && view.IsOwner()) ClearReservation();
        }

        private void OnDestroy()
        {
            StopTraining();
            CancelRequest();
            instances.Remove(this);
        }

        internal static void Shutdown()
        {
            if (pending) pending.CancelRequest();
            if (active) active.StopTraining();
            for (int i = instances.Count - 1; i >= 0; --i)
                if (instances[i]) instances[i].enabled = false;
            if (registeredPiece != null)
            {
                var prefab = registeredPiece.PiecePrefab;
                // Jotunn may have already destroyed its prefab container during game shutdown.
                // Only unregister while that owner and its Unity prefab are still alive.
                if (prefab)
                {
                    var table = PieceManager.Instance.GetPieceTable("Hammer");
                    if (table) table.m_pieces.Remove(prefab);
                    PieceManager.Instance.RemovePiece(registeredPiece);
                    Object.Destroy(prefab);
                }
                registeredPiece = null;
            }
            TreadmillVisual.Shutdown();
        }

        [HarmonyPatch(typeof(Character), nameof(Character.CustomFixedUpdate))]
        private static class TrainingTick
        {
            [HarmonyPostfix]
            private static void Postfix(Character __instance, float dt)
            {
                if (active && __instance == active.runner) active.Train(active.runner, dt);
            }
        }

        [HarmonyPatch(typeof(Player), nameof(Player.UseStamina))]
        private static class TrainingStamina
        {
            [HarmonyPrefix]
            private static void Prefix(Player __instance, ref float v)
            {
                // CheckRun has already applied skill, equipment, status and movement-world
                // modifiers. Scale its cost once, before native consumption/exhaustion.
                if (IsTrainingRun(__instance)) v *= Plugin.StaminaMultiplier;
            }
        }

        [HarmonyPatch(typeof(Player), nameof(Player.RaiseSkill))]
        private static class TrainingExperience
        {
            [HarmonyPrefix]
            private static void Prefix(Player __instance, Skills.SkillType skill, ref float value)
            {
                if (skill == Skills.SkillType.Run && IsTrainingRun(__instance))
                    value *= Plugin.TrainingMultiplier;
            }
        }

        [HarmonyPatch(typeof(Player), nameof(Player.AttachStop))]
        private static class NativeDetach
        {
            [HarmonyPostfix]
            private static void Postfix(Player __instance)
            {
                if (active && active.runner == __instance && __instance.GetAttachPoint() != active.attachPoint)
                    active.StopTraining();
            }
        }

        [HarmonyPatch(typeof(Player), "OnDestroy")]
        private static class PlayerDestroyed
        {
            [HarmonyPrefix]
            private static void Prefix(Player __instance)
            {
                if (active && active.runner == __instance) active.StopTraining();
                if (pending && pending.requestingPlayer == __instance) pending.CancelRequest();
            }
        }
    }
}
