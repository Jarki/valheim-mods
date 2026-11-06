using System;
using BepInEx;
using HarmonyLib;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using Jotunn.Utils;
using UnityEngine;

namespace Rotheart
{
    [BepInPlugin(Guid, "Rotheart", "1.0.0")]
    [BepInDependency(Jotunn.Main.ModGuid)]
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Guid = "jarki.rotheart";
        public const string ItemName = "Rotheart";
        internal static readonly int AuraHash = "RotheartAura".GetStableHashCode();
        private static readonly AccessTools.FieldRef<Humanoid, ItemDrop.ItemData> Trinket =
            AccessTools.FieldRefAccess<Humanoid, ItemDrop.ItemData>("m_trinketItem");
        private Harmony patches;

        private void Awake()
        {
            patches = new Harmony(Guid);
            patches.PatchAll();
            PrefabManager.OnVanillaPrefabsAvailable += Register;
        }

        private void OnDestroy()
        {
            PrefabManager.OnVanillaPrefabsAvailable -= Register;
            patches?.UnpatchSelf();
        }

        internal static bool IsEquipped(Humanoid wearer)
        {
            var item = Trinket(wearer);
            return item != null && item.m_dropPrefab && item.m_dropPrefab.name == ItemName;
        }

        private void Register()
        {
            var icon = RotheartAssets.Icon();
            var resistance = ScriptableObject.CreateInstance<SE_Stats>();
            resistance.name = "RotheartResistance";
            resistance.m_name = "Rotheart ward";
            resistance.m_icon = icon;
            resistance.m_tooltip = "50% less poison damage while equipped.";
            resistance.m_mods.Add(new HitData.DamageModPair
                { m_type = HitData.DamageType.Poison, m_modifier = HitData.DamageModifier.Resistant });
            var visual = PrefabManager.Instance.CreateEmptyPrefab("RotheartMiasma", true);
            visual.GetComponent<ZNetView>().m_persistent = false;
            DestroyImmediate(visual.GetComponent<Collider>());
            DestroyImmediate(visual.GetComponent<MeshRenderer>());
            DestroyImmediate(visual.GetComponent<MeshFilter>());
            var particles = RotheartAssets.CreateAuraPrefab();
            particles.transform.SetParent(visual.transform, false);
            particles.SetActive(true);
            PrefabManager.Instance.AddPrefab(new CustomPrefab(visual, false));
            var aura = ScriptableObject.CreateInstance<RotheartAura>();
            aura.name = "RotheartAura";
            aura.m_name = "Rotheart miasma";
            aura.m_icon = icon;
            aura.m_ttl = RotheartAura.Duration;
            aura.m_tooltip = "Every 2s: lose 1 health (minimum 1 HP) and deal 12 blunt damage to hostile creatures within 6m.\n"
                + "Spare other players, tamed creatures and structures. Ends when unequipped.";
            aura.m_startEffects = new EffectList { m_effectPrefabs = new[] {
                new EffectList.EffectData { m_prefab = visual, m_enabled = true, m_attach = true }
            }};
            if (!ItemManager.Instance.AddStatusEffect(new CustomStatusEffect(resistance, false)) ||
                !ItemManager.Instance.AddStatusEffect(new CustomStatusEffect(aura, false)))
                throw new InvalidOperationException("Could not register Rotheart status effects");

            var item = new CustomItem(ItemName, "BeltStrength", new ItemConfig
            {
                Name = "Rotheart",
                Description = "A swamp trinket of living ooze, caged in iron and bound with rancid entrails.\n"
                    + "At 10 adrenaline, automatically unleash a 10-second miasma.",
                CraftingStation = "forge", MinStationLevel = 1,
                Requirements = new[] { new RequirementConfig("Ooze", 10), new RequirementConfig("Guck", 5),
                    new RequirementConfig("Entrails", 5), new RequirementConfig("Iron", 5) }
            });
            var shared = item.ItemDrop.m_itemData.m_shared;
            shared.m_itemType = ItemDrop.ItemData.ItemType.Trinket;
            shared.m_attachOverride = ItemDrop.ItemData.ItemType.None;
            shared.m_weight = 2f;
            shared.m_maxStackSize = 1;
            shared.m_maxQuality = 1;
            shared.m_useDurability = false;
            shared.m_movementModifier = 0f;
            shared.m_maxAdrenaline = 0f;
            shared.m_equipStatusEffect = resistance;
            shared.m_fullAdrenalineSE = aura;
            shared.m_setStatusEffect = null;
            shared.m_setName = "";
            shared.m_damageModifiers.Clear();
            shared.m_icons = new[] { icon };
            ReplaceVisual(item.ItemPrefab);
            if (!ItemManager.Instance.AddItem(item)) throw new InvalidOperationException("Could not register Rotheart");
            PrefabManager.OnVanillaPrefabsAvailable -= Register;
            Logger.LogInfo("Rotheart registered: swamp trinket, poison ward, 10-adrenaline miasma and forge recipe.");
        }

        private static void ReplaceVisual(GameObject prefab)
        {
            var renderer = prefab.GetComponentInChildren<MeshRenderer>(true);
            var material = renderer ? renderer.sharedMaterial : null;
            foreach (var old in prefab.GetComponentsInChildren<Renderer>(true)) DestroyImmediate(old);
            foreach (var old in prefab.GetComponentsInChildren<MeshFilter>(true)) DestroyImmediate(old);
            for (int i = prefab.transform.childCount - 1; i >= 0; --i)
            {
                var child = prefab.transform.GetChild(i);
                if (child.name == "attach" || child.name.StartsWith("attach_", StringComparison.Ordinal))
                    DestroyImmediate(child.gameObject);
            }
            var dropped = new GameObject("attach").transform;
            dropped.SetParent(prefab.transform, false);
            RotheartAssets.Create(dropped, material);
            var equipped = new GameObject("attach_Hips");
            equipped.transform.SetParent(prefab.transform, false);
            var charm = RotheartAssets.Create(equipped.transform, material);
            charm.transform.localPosition = new Vector3(.19f, -.13f, .12f);
            equipped.SetActive(false);
        }

        [HarmonyPatch(typeof(Player), nameof(Player.GetMaxAdrenaline))]
        private static class AdrenalineCapacity
        {
            private static void Postfix(Player __instance, ref float __result)
            {
                if (IsEquipped(__instance)) __result = 10f;
            }
        }

        [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.UnequipItem))]
        private static class EndMiasmaOnUnequip
        {
            private static void Postfix(Humanoid __instance)
            {
                if (__instance is Player && !IsEquipped(__instance))
                    __instance.GetSEMan().RemoveStatusEffect(AuraHash);
            }
        }
    }
}
