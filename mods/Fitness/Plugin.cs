using System;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using Jotunn.Entities;
using Jotunn.Managers;
using Jotunn.Utils;
using UnityEngine;

namespace Fitness
{
    [BepInPlugin(Guid, "Fitness", "0.1.4")]
    [BepInDependency(Jotunn.Main.ModGuid)]
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Guid = "jarki.fitness";
        internal static readonly int[] StrainHashes = { 0,
            "FitnessRunStrain1".GetStableHashCode(), "FitnessRunStrain2".GetStableHashCode(),
            "FitnessRunStrain3".GetStableHashCode() };
        private static ConfigEntry<float> trainingMultiplier, staminaMultiplier, strainMultiplier, onset, recovery, immuneSkill;
        internal static float TrainingMultiplier => trainingMultiplier.Value;
        internal static float StaminaMultiplier => staminaMultiplier.Value;
        internal static float StrainMultiplier => strainMultiplier.Value;
        internal static float OnsetSeconds => onset.Value;
        internal static float RecoverySeconds => recovery.Value;
        internal static float ImmuneSkill => immuneSkill.Value;
        private Harmony patches;

        private void Awake()
        {
            trainingMultiplier = Config.Bind("Treadmill", "RunExperienceMultiplier", 1.5f,
                new ConfigDescription("Run XP multiplier while actively training; ordinary running is unchanged.",
                    new AcceptableValueRange<float>(.1f, 10f)));
            staminaMultiplier = Config.Bind("Treadmill", "StaminaConsumptionMultiplier", 1f,
                new ConfigDescription("Final native running stamina cost multiplier while actively training; ordinary running is unchanged.",
                    new AcceptableValueRange<float>(.1f, 5f)));
            strainMultiplier = Config.Bind("Treadmill", "StrainBuildupMultiplier", 2f,
                new ConfigDescription("Sprint effort multiplier for treadmill strain buildup only; ordinary running and recovery are unchanged.",
                    new AcceptableValueRange<float>(.1f, 5f)));
            onset = Config.Bind("Muscle strain", "SecondsPerLevel", 60f,
                new ConfigDescription("Cumulative sprint seconds per strain level at Run 0. Higher Run skill slows buildup.",
                    new AcceptableValueRange<float>(10f, 3600f)));
            recovery = Config.Bind("Muscle strain", "RecoverySecondsPerLevel", 600f,
                new ConfigDescription("Non-sprinting, alive seconds to recover one full strain level. No offline recovery.",
                    new AcceptableValueRange<float>(10f, 3600f)));
            immuneSkill = Config.Bind("Muscle strain", "StrainFreeRunSkill", 75f,
                new ConfigDescription("Run skill at which new strain stops building; susceptibility decreases linearly up to this skill.",
                    new AcceptableValueRange<float>(1f, 100f)));
            patches = new Harmony(Guid);
            patches.PatchAll();
            PrefabManager.OnVanillaPrefabsAvailable += Register;
        }

        private void Register()
        {
            var skills = PrefabManager.Instance.GetPrefab("Player").GetComponent<Skills>();
            Sprite icon = null;
            foreach (var skill in skills.m_skills)
                if (skill.m_skill == Skills.SkillType.Run) { icon = skill.m_icon; break; }
            if (!icon) throw new InvalidOperationException("Fitness requires the native Run skill icon");
            string[] severity = { "", "I — mild", "II — moderate", "III — severe" };
            for (int level = 1; level <= 3; ++level)
            {
                var effect = ScriptableObject.CreateInstance<SE_Stats>();
                effect.name = "FitnessRunStrain" + level;
                effect.m_name = "Muscle strain " + severity[level];
                effect.m_icon = icon;
                effect.m_tooltip = "Overworked running muscles. Stop sprinting to recover.\n"
                    + "Partial fatigue persists between runs and across saves.";
                effect.m_runStaminaDrainModifier = StrainState.StaminaPenalty(level);
                if (!ItemManager.Instance.AddStatusEffect(new CustomStatusEffect(effect, false)))
                    throw new InvalidOperationException("Could not register " + effect.name);
            }
            Treadmill.Register();
            PrefabManager.OnVanillaPrefabsAvailable -= Register;
            Logger.LogInfo("Fitness registered: treadmill training and three running muscle-strain levels.");
        }

        private void OnDestroy()
        {
            PrefabManager.OnVanillaPrefabsAvailable -= Register;
            Treadmill.Shutdown();
            StrainController.Shutdown();
            patches?.UnpatchSelf();
        }
    }
}
