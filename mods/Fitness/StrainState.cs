using System;

namespace Fitness
{
    public sealed class StrainState
    {
        private double load;
        public float Load => (float)load;
        public int Level { get; private set; }

        public void Advance(float seconds, bool sprinting, float runSkill, float onsetSeconds,
            float recoverySeconds, float immuneSkill)
        {
            if (seconds <= 0f || float.IsNaN(seconds) || float.IsInfinity(seconds)) return;
            if (sprinting)
            {
                float susceptibility = Math.Max(0f, 1f - Math.Max(0f, runSkill) / immuneSkill);
                load = Math.Min(3.999, load + (double)seconds * susceptibility / onsetSeconds);
                Level = Math.Max(Level, Math.Min(3, (int)(load + 0.000001)));
            }
            else
            {
                load = Math.Max(0, load - (double)seconds / recoverySeconds);
                // A newly triggered injury needs a full level of recovery, not one idle tick.
                while (Level > 0 && load <= Level - 1 + 0.000001) --Level;
            }
        }

        public void Restore(float load)
        {
            this.load = float.IsNaN(load) || float.IsInfinity(load) ? 0 : Math.Max(0, Math.Min(3.999, load));
            Level = Math.Min(3, (int)this.load);
        }

        public void Restore(float load, int level)
        {
            Restore(load);
            Level = Math.Max(Level, Math.Min(Math.Min(3, (int)Math.Ceiling(this.load)), level));
        }

        public static float StaminaPenalty(int level)
        {
            if (level <= 0) return 0f;
            if (level == 1) return .15f;
            return level == 2 ? .30f : .50f;
        }
    }
}
