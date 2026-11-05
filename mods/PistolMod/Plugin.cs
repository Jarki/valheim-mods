using System;
using BepInEx;
using HarmonyLib;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using Jotunn.Utils;
using UnityEngine;

namespace PistolMod
{
    [BepInPlugin(Guid, "Firearm Arsenal", "1.1.0")]
    [BepInDependency(Jotunn.Main.ModGuid)]
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Guid = "local.pistol9mm";
        internal const string PistolName = "Pistol9mm";
        internal const string AmmoName = "Ammo9mm";
        internal static GameObject BulletPrefab, RocketPrefab;
        private Harmony patches;

        private void Awake()
        {
            patches = new Harmony(Guid);
            patches.PatchAll();
            PrefabManager.OnVanillaPrefabsAvailable += RegisterItems;
        }

        private void OnDestroy()
        {
            PrefabManager.OnVanillaPrefabsAvailable -= RegisterItems;
            patches?.UnpatchSelf();
        }

        private void RegisterItems()
        {
            var bolt = PrefabManager.Instance.GetPrefab("BoltBone").GetComponent<ItemDrop>();
            var baseProjectile = bolt.m_itemData.m_shared.m_attack.m_attackProjectile;
            if (!baseProjectile) throw new InvalidOperationException("BoltBone projectile unavailable");
            BulletPrefab = MakeProjectile("Projectile9mm", baseProjectile, false);
            RocketPrefab = MakeProjectile("ProjectileRPG", baseProjectile, true);
            foreach (var spec in WeaponSpec.All) RegisterWeapon(spec);
            PrefabManager.OnVanillaPrefabsAvailable -= RegisterItems;
            Logger.LogInfo("Firearm Arsenal registered: 9mm pistol, AKM, RPG, ammunition and forge recipes.");
        }

        private static GameObject MakeProjectile(string name, GameObject basis, bool rocket)
        {
            var prefab = PrefabManager.Instance.CreateClonedPrefab(name, basis);
            var projectile = prefab.GetComponent<Projectile>();
            projectile.m_gravity = rocket ? .5f : 0;
            projectile.m_drag = 0; projectile.m_rayRadius = rocket ? .08f : .015f;
            projectile.m_ttl = rocket ? 8f : 1.2f;
            projectile.m_respawnItemOnHit = false; projectile.m_spawnOnHit = null;
            projectile.m_spawnOnTtl = false; projectile.m_randomSpawnOnHit.Clear();
            projectile.m_stayAfterHitStatic = false; projectile.m_stayAfterHitDynamic = false;
            projectile.m_attachToRigidBody = false; projectile.m_attachToClosestBone = false;
            projectile.m_hitNoise = rocket ? 100 : 40;
            projectile.m_aoe = rocket ? BlastRules.Radius : 0;
            projectile.m_hitMidFlight = false;
            projectile.m_bounce = false;
            foreach (var renderer in prefab.GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;
            if (rocket)
            {
                var sourceRenderer = basis.GetComponentInChildren<MeshRenderer>(true);
                var visual = PistolAssets.Create(prefab.transform, "rocket", sourceRenderer ? sourceRenderer.sharedMaterial : null);
                projectile.m_hideOnHit = visual;
                projectile.m_stayAfterHitStatic = projectile.m_stayAfterHitDynamic = true;
                projectile.m_stayTTL = .3f;
                var effect = PrefabManager.Instance.CreateClonedPrefab("FirearmRocketExplosion",
                    PrefabManager.Instance.GetPrefab("fx_siegebomb_explosion"));
                // Use the installed game's particles, with our own recorded audio. Damage
                // belongs exclusively to RocketBlast; never inherit terrain destruction.
                foreach (var aoe in effect.GetComponentsInChildren<Aoe>(true)) DestroyImmediate(aoe);
                foreach (var terrain in effect.GetComponentsInChildren<TerrainModifier>(true)) DestroyImmediate(terrain);
                foreach (var terrain in effect.GetComponentsInChildren<TerrainOp>(true)) DestroyImmediate(terrain);
                foreach (var sfx in effect.GetComponentsInChildren<ZSFX>(true)) DestroyImmediate(sfx);
                foreach (var audio in effect.GetComponentsInChildren<AudioSource>(true)) DestroyImmediate(audio);
                effect.AddComponent<RocketExplosionAudio>();
                PrefabManager.Instance.AddPrefab(new CustomPrefab(effect, false));
                projectile.m_hitEffects = new EffectList { m_effectPrefabs = new[]
                    { new EffectList.EffectData { m_prefab = effect, m_enabled = true } } };
                projectile.m_hitWaterEffects = projectile.m_hitEffects;
                projectile.m_canHitWater = true;
                prefab.AddComponent<RocketBlast>();
            }
            PrefabManager.Instance.AddPrefab(new CustomPrefab(prefab, false));
            return prefab;
        }

        private static void RegisterWeapon(WeaponSpec spec)
        {
            PistolAssets.Clip(spec.ShotSound);
            PistolAssets.Clip(spec.ReloadSound);
            if (spec.Explosive) PistolAssets.Clip("explosion");
            var requirements = spec == WeaponSpec.Pistol
                ? new[] { new RequirementConfig("Iron", 20), new RequirementConfig("Copper", 5), new RequirementConfig("FineWood", 5) }
                : spec == WeaponSpec.Rifle
                    ? new[] { new RequirementConfig("Iron", 35), new RequirementConfig("Copper", 10), new RequirementConfig("FineWood", 15) }
                    : new[] { new RequirementConfig("Iron", 40), new RequirementConfig("Silver", 10), new RequirementConfig("FineWood", 10) };
            var item = new CustomItem(spec.Prefab, "CrossbowArbalest", new ItemConfig
            {
                Name = spec.Label,
                Description = (spec.Automatic ? "Hold attack for automatic fire. " : "Attack fires one shot. ")
                    + "Secondary attack reloads (" + spec.Reload.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)
                    + "s). Capacity: " + spec.Capacity + ". Uses " + spec.AmmoLabel + "."
                    + (spec.Explosive ? " 5m blast damages creatures and structures; respects wards. Walls block blast." : ""),
                CraftingStation = "forge", RepairStation = "forge", MinStationLevel = spec.Explosive ? 3 : 2,
                Requirements = requirements
            });
            var data = item.ItemDrop.m_itemData;
            var shared = data.m_shared;
            shared.m_itemType = ItemDrop.ItemData.ItemType.TwoHandedWeapon;
            shared.m_attachOverride = ItemDrop.ItemData.ItemType.None;
            // Neutral locomotion underneath our skeletal firearm layer, not the crossbow pose.
            shared.m_animationState = ItemDrop.ItemData.AnimationState.Unarmed;
            shared.m_skillType = Skills.SkillType.Crossbows;
            shared.m_maxQuality = 1; shared.m_weight = spec.Weight;
            shared.m_movementModifier = spec.Explosive ? -.1f : 0;
            shared.m_maxDurability = 300; shared.m_durabilityPerLevel = 0;
            shared.m_useDurability = true; shared.m_useDurabilityDrain = 1;
            shared.m_destroyBroken = false; shared.m_canBeReparied = true;
            shared.m_damages = spec.Explosive
                ? new HitData.DamageTypes { m_blunt = spec.Damage, m_fire = 40 }
                : new HitData.DamageTypes { m_pierce = spec.Damage };
            shared.m_damagesPerLevel = new HitData.DamageTypes();
            shared.m_attackForce = spec.Explosive ? 80 : 12;
            shared.m_backstabBonus = spec.Explosive ? 1 : 2;
            shared.m_blockPower = 5; shared.m_blockPowerPerLevel = 0;
            shared.m_ammoType = "";
            shared.m_attack = new Attack { m_attackType = Attack.AttackType.None };
            shared.m_secondaryAttack = new Attack { m_attackType = Attack.AttackType.None };
            shared.m_icons = new[] { PistolAssets.Icon(spec.Asset) };
            data.m_durability = shared.m_maxDurability;
            Magazine.Write(data.m_customData, 0, spec.Capacity);
            ReplaceVisual(item.ItemPrefab, spec.Asset, spec);

            var ammo = new CustomItem(spec.Ammo, "BoltBone", new ItemConfig
            {
                Name = spec.AmmoLabel, Description = "Ammunition for the " + spec.Label + ". Load with secondary attack.",
                CraftingStation = "forge", MinStationLevel = spec.Explosive ? 3 : 1,
                Amount = spec.Explosive ? 1 : 20,
                Requirements = spec.Explosive
                    ? new[] { new RequirementConfig("Iron", 2), new RequirementConfig("Coal", 5), new RequirementConfig("SurtlingCore", 1) }
                    : new[] { new RequirementConfig("Copper", spec.Automatic ? 2 : 1), new RequirementConfig("Coal", spec.Automatic ? 3 : 2), new RequirementConfig("Resin", 1) }
            });
            var ammoShared = ammo.ItemDrop.m_itemData.m_shared;
            string asset = spec.Explosive ? "rocket" : spec.Automatic ? "ammo762" : "ammo9mm";
            ammoShared.m_itemType = ItemDrop.ItemData.ItemType.Material;
            ammoShared.m_ammoType = spec.Prefab; ammoShared.m_maxStackSize = spec.Explosive ? 20 : 100;
            ammoShared.m_weight = spec.Explosive ? 1 : .015f; ammoShared.m_maxQuality = 1;
            ammoShared.m_icons = new[] { PistolAssets.Icon(asset) };
            ammoShared.m_attack = new Attack { m_attackType = Attack.AttackType.None };
            ReplaceVisual(ammo.ItemPrefab, asset, null);
            if (!ItemManager.Instance.AddItem(ammo) || !ItemManager.Instance.AddItem(item))
                throw new InvalidOperationException("Could not register " + spec.Prefab);
        }

        private static void ReplaceVisual(GameObject prefab, string asset, WeaponSpec spec)
        {
            var oldRenderer = prefab.GetComponentInChildren<MeshRenderer>(true);
            var material = oldRenderer ? oldRenderer.sharedMaterial : null;
            var attach = prefab.transform.Find("attach");
            if (!attach) { attach = new GameObject("attach").transform; attach.SetParent(prefab.transform, false); }
            foreach (var state in prefab.GetComponentsInChildren<WeaponLoadState>(true)) DestroyImmediate(state);
            foreach (var renderer in prefab.GetComponentsInChildren<Renderer>(true)) DestroyImmediate(renderer);
            foreach (var filter in prefab.GetComponentsInChildren<MeshFilter>(true)) DestroyImmediate(filter);
            for (int i = attach.childCount - 1; i >= 0; --i) DestroyImmediate(attach.GetChild(i).gameObject);
            var back = prefab.transform.Find("attach_back"); if (back) DestroyImmediate(back.gameObject);
            var offset = prefab.transform.Find("equipoffset"); if (offset) DestroyImmediate(offset.gameObject);
            attach.localPosition = Vector3.zero; attach.localRotation = Quaternion.identity; attach.localScale = Vector3.one;
            PistolAssets.Create(attach, asset, material, spec);
        }

        internal static WeaponSpec Spec(ItemDrop.ItemData item) => item != null && item.m_dropPrefab ? WeaponSpec.Find(item.m_dropPrefab.name) : null;

        [HarmonyPatch(typeof(Player), "Awake")]
        private static class AttachController
        {
            private static void Postfix(Player __instance)
            {
                __instance.gameObject.AddComponent<PistolController>();
                __instance.gameObject.AddComponent<FirearmPose>();
            }
        }

        [HarmonyPatch(typeof(Player), nameof(Player.SetControls))]
        private static class Controls
        {
            private static void Prefix(Player __instance, ref bool attack, ref bool attackHold, ref bool secondaryAttack, ref bool secondaryAttackHold)
            {
                var spec = Spec(__instance.GetCurrentWeapon());
                if (__instance != Player.m_localPlayer || spec == null) return;
                var controller = __instance.GetComponent<PistolController>();
                if (secondaryAttack) controller.BeginReload();
                else if (attack || (spec.Automatic && attackHold)) controller.Fire();
                attack = attackHold = secondaryAttack = secondaryAttackHold = false;
            }
        }

        [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.StartAttack))]
        private static class PreventVanillaAttack
        {
            private static bool Prefix(Humanoid __instance, ref bool __result)
            {
                if (Spec(__instance.GetCurrentWeapon()) == null) return true;
                __result = false;
                return false;
            }
        }
    }
}
