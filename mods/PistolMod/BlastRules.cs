using System;

namespace PistolMod
{
    internal static class BlastRules
    {
        internal const float Radius = 5f;

        // Distance is measured to collider bounds (Valheim's native convention), not the object pivot.
        internal static float Falloff(float distance)
        {
            if (float.IsNaN(distance) || float.IsInfinity(distance) || distance >= Radius) return 0;
            return distance <= 0 ? 1 : 1 - distance / Radius;
        }
    }
}
