using System;
using System.Globalization;
using System.Runtime.CompilerServices;
using HarmonyLib;

namespace Fitness
{
    internal static class RunDistance
    {
        private sealed class State
        {
            internal string Key;
            internal long World;
            internal double Metres;
            internal int PublishedMetres = -1;
            internal bool Dirty;
        }

        private static readonly ConditionalWeakTable<Player, State> States = new ConditionalWeakTable<Player, State>();
        private static readonly int DistanceKey = "FitnessTreadmillMetres".GetStableHashCode();

        private static State Current(Player player)
        {
            long world = ZNet.instance.GetWorldUID();
            var state = States.GetOrCreateValue(player);
            if (state.Key != null && state.World == world) return state;
            Persist(player);
            state.World = world;
            state.Key = "jarki.fitness.treadmillMetres." + world.ToString("x16", CultureInfo.InvariantCulture);
            state.Metres = player.m_customData.TryGetValue(state.Key, out var text)
                && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value)
                && !double.IsNaN(value) && !double.IsInfinity(value) && value >= 0d ? value : 0d;
            state.PublishedMetres = -1;
            return state;
        }

        internal static double Metres(Player player)
        {
            if (!player || !ZNet.instance) return 0d;
            if (player == Player.m_localPlayer) return Current(player).Metres;
            var view = player.GetComponent<ZNetView>();
            return view && view.IsValid() ? view.GetZDO().GetFloat(DistanceKey) : 0d;
        }

        internal static void Add(Player player, float speed, float dt)
        {
            if (player != Player.m_localPlayer || !ZNet.instance || speed <= 0f || dt <= 0f) return;
            var state = Current(player);
            state.Metres += (double)speed * dt;
            state.Dirty = true;
            int wholeMetres = (int)Math.Min(int.MaxValue, state.Metres);
            if (wholeMetres == state.PublishedMetres) return;
            var view = player.GetComponent<ZNetView>();
            if (view && view.IsValid() && view.IsOwner())
            {
                view.GetZDO().Set(DistanceKey, (float)state.Metres);
                state.PublishedMetres = wholeMetres;
            }
        }

        internal static void Persist(Player player)
        {
            if (States.TryGetValue(player, out var state) && state.Dirty)
            {
                player.m_customData[state.Key] = state.Metres.ToString("R", CultureInfo.InvariantCulture);
                state.Dirty = false;
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
            private static void Prefix(Player __instance) => States.Remove(__instance);
        }
    }
}
