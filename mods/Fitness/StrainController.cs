using System.Globalization;
using System.Runtime.CompilerServices;
using HarmonyLib;

namespace Fitness
{
    internal static class StrainController
    {
        private const string SaveKey = "jarki.fitness.runStrain";
        private static readonly ConditionalWeakTable<Player, StrainState> States = new ConditionalWeakTable<Player, StrainState>();
        private const string LevelSaveKey = "jarki.fitness.runStrainLevel";

        private static StrainState State(Player player) => States.GetValue(player, CreateState);

        private static StrainState CreateState(Player player)
        {
            var state = new StrainState();
            if (player.m_customData.TryGetValue(SaveKey, out string text)
                && float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float load))
            {
                int level = 0;
                if (player.m_customData.TryGetValue(LevelSaveKey, out string severity))
                    int.TryParse(severity, NumberStyles.Integer, CultureInfo.InvariantCulture, out level);
                state.Restore(load, level);
            }
            return state;
        }

        private static void Advance(Player player, float seconds, bool sprinting)
        {
            var state = State(player);
            state.Advance(seconds, sprinting, player.GetSkills().GetSkillLevel(Skills.SkillType.Run),
                Plugin.OnsetSeconds, Plugin.RecoverySeconds, Plugin.ImmuneSkill);
            Apply(player, state.Level);
        }

        internal static void RecoverTraining(Player player, float dt) => Advance(player, dt, false);

        private static void Apply(Player player, int level)
        {
            var effects = player.GetSEMan();
            for (int i = 1; i <= 3; ++i)
            {
                int hash = Plugin.StrainHashes[i];
                if (i != level)
                {
                    if (effects.HaveStatusEffect(hash)) effects.RemoveStatusEffect(hash, true);
                }
                else if (!effects.HaveStatusEffect(hash)) effects.AddStatusEffect(hash);
            }
        }

        internal static void Shutdown()
        {
            var player = Player.m_localPlayer;
            if (!player) return;
            Persist(player);
            Apply(player, 0);
        }

        private static void Persist(Player player)
        {
            if (States.TryGetValue(player, out var state))
            {
                player.m_customData[SaveKey] = state.Load.ToString("R", CultureInfo.InvariantCulture);
                player.m_customData[LevelSaveKey] = state.Level.ToString(CultureInfo.InvariantCulture);
            }
        }

        [HarmonyPatch(typeof(Player), "CheckRun")]
        private static class SprintEffort
        {
            private static void Postfix(Player __instance, float dt, bool __result)
            {
                if (__instance == Player.m_localPlayer && __result && !__instance.IsDead())
                {
                    float effort = Treadmill.IsTrainingRun(__instance) ? dt * Plugin.StrainMultiplier : dt;
                    Advance(__instance, effort, true);
                }
            }
        }

        [HarmonyPatch(typeof(Character), nameof(Character.CustomFixedUpdate))]
        private static class Recover
        {
            private static void Postfix(Character __instance, float dt)
            {
                var player = Player.m_localPlayer;
                if (!player || __instance != player || player.IsDead() || player.IsTeleporting()) return;
                // The treadmill owns both sprint effort and walking recovery.
                if (!player.IsRunning() && !Treadmill.IsTraining(player)) Advance(player, dt, false);
                else Apply(player, State(player).Level);
            }
        }

        [HarmonyPatch(typeof(Player), nameof(Player.Save))]
        private static class Save
        {
            private static void Prefix(Player __instance) => Persist(__instance);
        }

        [HarmonyPatch(typeof(Player), nameof(Player.Load))]
        private static class Load
        {
            private static void Postfix(Player __instance)
            {
                States.Remove(__instance);
                State(__instance);
            }
        }
    }
}
