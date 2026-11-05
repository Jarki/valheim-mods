using System;

namespace MobThreat
{
    internal static class ThreatMath
    {
        // Zero means no finite estimate. Never show a reassuring count for unknown damage.
        internal static int HitsToDie(float health, float damage)
        {
            if (float.IsNaN(health) || float.IsInfinity(health) || health <= 0f ||
                float.IsNaN(damage) || float.IsInfinity(damage) || damage <= 0f)
                return 0;
            double hits = Math.Ceiling((double)health / damage);
            return hits > int.MaxValue ? int.MaxValue : (int)hits;
        }
    }
}
