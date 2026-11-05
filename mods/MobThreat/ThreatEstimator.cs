using UnityEngine;

namespace MobThreat
{
    internal sealed class ThreatEstimator
    {
        private readonly HitData hit = new HitData();

        // Strongest available weapon attack, not a prediction of AI selection.
        internal float Estimate(Humanoid mob, HitData.DamageModifiers resistance, float armor, float difficulty)
        {
            float strongest = 0f;
            var items = mob.GetInventory().GetAllItems();
            for (int i = 0; i < items.Count; ++i)
                strongest = Mathf.Max(strongest, EstimateWeapon(mob, items[i], resistance, armor, difficulty));
            return Mathf.Max(strongest, EstimateWeapon(mob, mob.GetCurrentWeapon(), resistance, armor, difficulty));
        }

        private float EstimateWeapon(Humanoid mob, ItemDrop.ItemData weapon,
            HitData.DamageModifiers resistance, float armor, float difficulty)
        {
            if (weapon == null || !weapon.IsWeapon() || weapon.m_shared.m_aiTargetType != ItemDrop.ItemData.AiTarget.Enemy)
                return 0f;
            float damage = EstimateAttack(mob, weapon, weapon.m_shared.m_attack, resistance, armor, difficulty);
            if (weapon.HaveSecondaryAttack())
                damage = Mathf.Max(damage, EstimateAttack(mob, weapon, weapon.m_shared.m_secondaryAttack, resistance, armor, difficulty));
            return damage;
        }

        private float EstimateAttack(Humanoid mob, ItemDrop.ItemData weapon, Attack attack,
            HitData.DamageModifiers resistance, float armor, float difficulty)
        {
            if (attack == null) return 0f;
            hit.m_damage = weapon.GetDamage();
            float factor = attack.m_damageMultiplier * (1f + Mathf.Max(0, mob.GetLevel() - 1) * .5f);
            // Midpoint of the vanilla sqrt(0.75..1) skill roll, without consuming RNG.
            factor *= .935f;
            if (attack.m_damageMultiplierPerMissingHP > 0f)
                factor *= 1f + (mob.GetMaxHealth() - mob.GetHealth()) * attack.m_damageMultiplierPerMissingHP;
            if (attack.m_damageMultiplierByTotalHealthMissing > 0f)
                factor *= 1f + (1f - mob.GetHealthPercentage()) * attack.m_damageMultiplierByTotalHealthMissing;
            hit.m_damage.Modify(factor);
            // These channels damage tools/structures, not the player.
            hit.m_damage.m_chop = hit.m_damage.m_pickaxe = 0f;
            float extraDamage = Game.m_worldLevel > 0
                ? Game.m_worldLevel * Game.instance.m_worldLevelEnemyBaseDamage : 0f;
            return Mitigate(hit, resistance, armor, difficulty * Game.m_enemyDamageRate,
                Game.m_localDamgeTakenRate, extraDamage);
        }

        internal static float Mitigate(HitData incoming, HitData.DamageModifiers resistance,
            float armor, float difficultyAndEnemyRate, float localDamageRate, float extraDamage)
        {
            incoming.ApplyModifier(difficultyAndEnemyRate);
            incoming.ApplyResistance(resistance, out _);
            incoming.ApplyArmor(armor);
            // Include the full resisted DOT payload as an approximation; overlapping
            // poison/burning applications do not necessarily add linearly in game.
            incoming.ApplyModifier(localDamageRate);
            float damage = incoming.m_damage.GetTotalDamage() + extraDamage;
            return damage > .1f ? damage : 0f;
        }
    }
}
