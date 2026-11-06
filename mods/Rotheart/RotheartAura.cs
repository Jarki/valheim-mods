using System.Collections.Generic;
using UnityEngine;

namespace Rotheart
{
    public sealed class RotheartAura : StatusEffect
    {
        public const float Duration = 10f;
        private const float Interval = 2f;
        private const float Radius = 6f;
        private List<Character> targets;
        private int pulses;

        public override void Setup(Character character)
        {
            // StatusEffect.Clone uses MemberwiseClone: never share mutable target buffers.
            targets = new List<Character>(32);
            pulses = 0;
            base.Setup(character);
        }

        public override void ResetTime()
        {
            base.ResetTime();
            pulses = 0;
        }

        public override bool IsDone()
        {
            return !(m_character is Player player) || player.IsDead() || !Plugin.IsEquipped(player) || base.IsDone();
        }

        public override void UpdateStatusEffect(float dt)
        {
            base.UpdateStatusEffect(dt);
            if (!(m_character is Player player) || player != Player.m_localPlayer || player.IsDead()
                || !Plugin.IsEquipped(player) || player.IsTeleporting() || player.InCutscene()) return;
            var view = player.GetComponent<ZNetView>();
            if (!view || !view.IsValid() || !view.IsOwner()) return;
            // Bounded catch-up includes the fifth tick when native expiry crosses ten seconds.
            int due = Mathf.Min(5, Mathf.FloorToInt((m_time + .0001f) / Interval));
            while (pulses < due)
            {
                ++pulses;
                Pulse(player);
            }
        }

        private void Pulse(Player player)
        {
            float health = player.GetHealth();
            // A health payment, not a poison hit: armor/mead must not erase the tradeoff.
            if (health > 1f) player.SetHealth(Mathf.Max(1f, health - 1f));
            targets.Clear();
            Character.GetCharactersInRange(player.transform.position, Radius, targets);
            foreach (var target in targets)
            {
                if (!target || target.IsPlayer() || target.IsTamed() || target.IsDead()
                    || !BaseAI.IsEnemy(player, target)) continue;
                var hit = new HitData
                {
                    m_damage = new HitData.DamageTypes { m_blunt = 12f },
                    m_point = target.GetCenterPoint(),
                    m_dir = (target.transform.position - player.transform.position).normalized,
                    m_pushForce = 0f,
                    m_dodgeable = false,
                    m_blockable = false,
                    m_staggerMultiplier = 0f,
                    m_skill = Skills.SkillType.None
                };
                hit.SetAttacker(player);
                target.Damage(hit);
            }
            targets.Clear();
        }
    }
}
