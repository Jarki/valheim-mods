using System;
using System.Collections;
using System.Globalization;
using BepInEx.Configuration;
using UnityEngine;

namespace Fitness.InGameTests
{
    public sealed partial class Host
    {
        struct ConfigurationRunSample
        {
            internal float Stamina, Load;
        }

        ConfigurationRunSample ConfigurationRun(bool training, string label)
        {
            const float seconds = .25f;
            Check(Level == 0 && player.HaveStamina(100f), label + " fixture has no strain penalty and ample native stamina");
            Check(training == (bool)Call(treadmillType, "IsTraining", player), label + " actual treadmill attachment matches sample path");
            Set(player, "m_runSkillImproveTimer", 0f);
            float load = PersistedLoad(), stamina = player.GetStamina();
            if (training)
            {
                Call(treadmill, "Train", player, seconds);
                Check(player.IsAttached() && player.IsRunning(), label + " real treadmill Train/CheckRun succeeds");
            }
            else
            {
                Controls(Vector3.forward, true);
                try { Check((bool)Call(player, "CheckRun", Vector3.forward, seconds), label + " ordinary native CheckRun succeeds"); }
                finally { Controls(Vector3.zero); }
            }
            var result = new ConfigurationRunSample { Stamina = stamina - player.GetStamina(), Load = PersistedLoad() - load };
            Check(result.Stamina > 0f && result.Load > 0f && Level == 0, label + " native stamina and persisted strain increase without crossing severity");
            Check(Mathf.Abs((float)Get(player, "m_runSkillImproveTimer") - seconds) < .00001f,
                label + " native Run XP timer advances by unchanged dt, independently of both multipliers");
            Check(!(bool)Get(treadmillType, "checkingRun"), label + " scoped native running context cleared after call");
            Log("CONFIG_NATIVE_SAMPLE label=" + label + " stamina=" + result.Stamina.ToString("R", CultureInfo.InvariantCulture) +
                " load=" + result.Load.ToString("R", CultureInfo.InvariantCulture));
            return result;
        }

        float ConfigurationDirectCost()
        {
            float before = player.GetStamina();
            player.UseStamina(4f);
            float cost = before - player.GetStamina();
            Check(cost > 0f, "direct native non-running stamina control consumes stamina");
            return cost;
        }

        void ConfigurationRatio(ConfigurationRunSample sample, ConfigurationRunSample control,
            float staminaRatio, float strainRatio, string label)
        {
            Log("CONFIG_NATIVE_RATIO label=" + label + " stamina=" + sample.Stamina / control.Stamina + " strain=" + sample.Load / control.Load);
            Check(Mathf.Abs(sample.Stamina / control.Stamina - staminaRatio) < .015f,
                label + " observed native stamina consumption ratio " + staminaRatio);
            Check(Mathf.Abs(sample.Load / control.Load - strainRatio) < .002f,
                label + " observed persisted strain buildup ratio " + strainRatio);
        }

        float ConfigurationRecovery(string label)
        {
            Check(!player.IsAttached() && !(bool)Call(treadmillType, "IsTraining", player), label + " recovery fixture is not training");
            // Seed through native sprinting, not StrainState writes, so recovery cannot hit zero.
            Controls(Vector3.forward, true);
            try { Check((bool)Call(player, "CheckRun", Vector3.forward, .5f), label + " native sprint provides fractional recovery debt"); }
            finally { Controls(Vector3.zero); }
            float before = PersistedLoad();
            player.CustomFixedUpdate(.25f);
            Check(!player.IsRunning(), label + " native idle locomotion reports not running");
            float recovered = before - PersistedLoad();
            Check(recovered > 0f && PersistedLoad() > 0f, label + " actual native fixed-update recovery is positive and uncapped");
            Log("CONFIG_NATIVE_RECOVERY label=" + label + " loadRecovered=" + recovered.ToString("R", CultureInfo.InvariantCulture));
            return recovered;
        }

        IEnumerator CheckTreadmillConfiguration()
        {
            Stage("configuration-native-ratios");
            Check(player && player == Player.m_localPlayer && fitness && treadmill && treadmill.isActiveAndEnabled && treadmillType != null,
                "configuration fixture has initialized local player, production plugin and real placed treadmill");
            Check(player.GetComponent<ZNetView>().IsOwner() && !player.IsDead() && !player.IsTeleporting() &&
                !player.IsAttached() && player.IsOnGround() && !player.IsEncumbered() && player.GetMaxStamina() >= 1000f && player.m_staminaRegen == 0f,
                "configuration fixture is locally owned, alive, grounded, unattached, unencumbered with boosted stamina and disabled regeneration");
            float oldOnset = (float)fitness.Config[new ConfigDefinition("Muscle strain", "SecondsPerLevel")].BoxedValue;
            float oldRecovery = (float)fitness.Config[new ConfigDefinition("Muscle strain", "RecoverySecondsPerLevel")].BoxedValue;
            float oldImmune = (float)fitness.Config[new ConfigDefinition("Muscle strain", "StrainFreeRunSkill")].BoxedValue;
            float oldSkill = Skill().m_level, oldAccumulator = Skill().m_accumulator, oldFixedSkill = fixedSkill;
            float oldTimer = (float)Get(player, "m_runSkillImproveTimer"), oldStamina = player.GetStamina();
            try
            {
                drive = false; Controls(Vector3.zero);
                Config("Muscle strain", "SecondsPerLevel", 10f);
                Config("Muscle strain", "RecoverySecondsPerLevel", 10f);
                Config("Muscle strain", "StrainFreeRunSkill", 75f);
                yield return RecoverFully("configuration-initial-native-recovery");
                Stage("configuration-native-ratios");
                SetSkill(0f); fixedSkill = 0f; FillStamina();
                var ordinary = ConfigurationRun(false, "ordinary-default");
                float direct = ConfigurationDirectCost();
                float recovery = ConfigurationRecovery("recovery-default");
                yield return Begin();
                var baseline = ConfigurationRun(true, "treadmill-default");
                ConfigurationRatio(baseline, ordinary, 1f, 1f, "defaults preserve native running cost and effort");

                // Independent settings make accidental coupling, double scaling and dt scaling observable.
                Config("Treadmill", "StaminaConsumptionMultiplier", 2.5f);
                Config("Treadmill", "StrainBuildupMultiplier", 3f);
                ConfigurationRatio(ConfigurationRun(true, "treadmill-independent-high"), baseline, 2.5f, 3f, "independent high settings");
                Check(Mathf.Abs(ConfigurationDirectCost() / direct - 1f) < .002f,
                    "attached non-CheckRun stamina use is unchanged despite configured treadmill multiplier");
                Config("Treadmill", "StaminaConsumptionMultiplier", .5f);
                Config("Treadmill", "StrainBuildupMultiplier", .25f);
                ConfigurationRatio(ConfigurationRun(true, "treadmill-independent-low"), baseline, .5f, .25f, "independent low settings");
                Config("Treadmill", "StaminaConsumptionMultiplier", .1f);
                Config("Treadmill", "StrainBuildupMultiplier", 5f);
                ConfigurationRatio(ConfigurationRun(true, "treadmill-range-ends"), baseline, .1f, 5f, "minimum stamina and maximum strain settings");
                Config("Treadmill", "StaminaConsumptionMultiplier", 5f);
                Config("Treadmill", "StrainBuildupMultiplier", .1f);
                ConfigurationRatio(ConfigurationRun(true, "treadmill-reversed-range-ends"), baseline, 5f, .1f, "maximum stamina and minimum strain settings");
                yield return Stop("configuration-stop");
                ConfigurationRatio(ConfigurationRun(false, "ordinary-configured"), ordinary, 1f, 1f,
                    "ordinary sprint unaffected by both nondefault treadmill settings");
                Check(Mathf.Abs(ConfigurationDirectCost() / direct - 1f) < .002f,
                    "unattached non-running stamina cost remains unchanged");
                Check(Mathf.Abs(ConfigurationRecovery("recovery-configured") / recovery - 1f) < .002f,
                    "native idle recovery unaffected by treadmill strain buildup multiplier");
                yield return RecoverFully("configuration-final-native-recovery");
                Check(Level == 0 && PersistedLoad() < .002f && !player.IsAttached() &&
                    Get(treadmillType, "active") == null && Get(treadmillType, "pending") == null && !(bool)Get(treadmillType, "checkingRun"),
                    "configuration checks leave no strain, attachment, reservation context or scoped running state");
            }
            finally
            {
                // The caller's 10-second full-suite settings are not production defaults.
                // Restore those separately; both new treadmill knobs return to their production defaults.
                Config("Treadmill", "StaminaConsumptionMultiplier", 1f);
                Config("Treadmill", "StrainBuildupMultiplier", 1f);
                Config("Muscle strain", "SecondsPerLevel", oldOnset);
                Config("Muscle strain", "RecoverySecondsPerLevel", oldRecovery);
                Config("Muscle strain", "StrainFreeRunSkill", oldImmune);
                if (player.IsAttached()) player.AttachStop();
                Controls(Vector3.zero); drive = false;
                Skill().m_level = oldSkill; Skill().m_accumulator = oldAccumulator; fixedSkill = oldFixedSkill;
                Set(player, "m_runSkillImproveTimer", oldTimer);
                Set(player, "m_stamina", oldStamina);
            }
        }
    }
}
