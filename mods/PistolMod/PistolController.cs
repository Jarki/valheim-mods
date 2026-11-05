using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace PistolMod
{
    public sealed class PistolController : MonoBehaviour
    {
        private const string EffectsRpc = "Firearm_Effects_v2";
        private Player player;
        private ZNetView view;
        private VisEquipment equipment;
        private ItemDrop.ItemData reloadingItem;
        private float reloadEnds, nextShot, nextMessage;
        private TMP_Text hudText;
        private GameObject hudRoot;

        private void Awake()
        {
            player = GetComponent<Player>(); view = GetComponent<ZNetView>(); equipment = GetComponent<VisEquipment>();
            if (view && view.IsValid()) view.Register<int, int>(EffectsRpc, ReceiveEffects);
        }

        internal bool CanPose => !player.IsDead() && !player.InDodge() && !player.IsStaggering()
            && !player.IsKnockedBack() && !player.IsTeleporting() && !player.InCutscene() && !player.IsAttached()
            && !(player.IsSwimming() && !player.IsOnGround());

        private bool CanUse() => player == Player.m_localPlayer && view && view.IsValid() && view.IsOwner()
            && CanPose && !player.InAttack() && !player.InMinorAction();

        internal bool BeginReload()
        {
            var item = player.GetCurrentWeapon(); var spec = Plugin.Spec(item);
            if (!CanUse() || spec == null || reloadingItem != null || item.m_durability <= 0) return false;
            int loaded = Magazine.Read(item.m_customData, spec.Capacity);
            if (Magazine.ReloadAmount(loaded, Reserve(spec), spec.Capacity) == 0)
            {
                if (loaded < spec.Capacity) Message("No " + spec.AmmoLabel);
                return false;
            }
            reloadingItem = item; reloadEnds = Time.time + spec.Reload;
            SendEffect(spec, 1);
            return true;
        }

        internal bool Fire()
        {
            var item = player.GetCurrentWeapon(); var spec = Plugin.Spec(item);
            if (!CanUse() || spec == null || reloadingItem != null || Time.time < nextShot) return false;
            if (item.m_durability <= 0) { Message("Weapon needs repair at a forge"); return false; }
            if (Magazine.Read(item.m_customData, spec.Capacity) == 0)
            {
                Message("Magazine empty — secondary attack to reload"); return false;
            }
            var prefab = spec.Explosive ? Plugin.RocketPrefab : Plugin.BulletPrefab;
            if (!prefab || !ZNetScene.instance) return false;
            // Start at the eye, never beyond a close wall. Native projectile uses swept collision.
            Vector3 origin = player.GetEyePoint();
            Vector3 direction = player.GetAimDir(origin).normalized;
            player.FaceLookDirection();
            var projectile = Instantiate(prefab, origin, Quaternion.LookRotation(direction)).GetComponent<Projectile>();
            var hit = new HitData
            {
                m_damage = item.GetDamage(), m_pushForce = item.m_shared.m_attackForce,
                m_backstabBonus = item.m_shared.m_backstabBonus, m_staggerMultiplier = 1,
                m_blockable = !spec.Explosive, m_dodgeable = true,
                m_skill = Skills.SkillType.Crossbows, m_skillLevel = player.GetSkillLevel(Skills.SkillType.Crossbows),
                m_skillRaiseAmount = .25f, m_itemLevel = (short)item.m_quality,
                m_itemWorldLevel = (byte)item.m_worldLevel, m_hitType = HitData.HitType.PlayerHit
            };
            hit.SetAttacker(player);
            hit.m_damage.Modify(player.GetRandomSkillFactor(Skills.SkillType.Crossbows));
            player.GetSEMan().ModifyAttack(Skills.SkillType.Crossbows, ref hit);
            projectile.Setup(player, direction * spec.Speed, spec.Explosive ? 100 : 40, hit, item, null);
            Magazine.TryFire(item.m_customData, spec.Capacity);
            item.m_durability = Mathf.Max(0, item.m_durability - Game.m_durabilityRate);
            nextShot = Time.time + spec.Interval;
            player.AddNoise(spec.Explosive ? 100 : 40);
            SendEffect(spec, 0);
            return true;
        }

        private void Update()
        {
            UpdateHud();
            if (reloadingItem == null) return;
            var spec = Plugin.Spec(reloadingItem);
            if (!CanUse() || player.GetCurrentWeapon() != reloadingItem || !player.GetInventory().ContainsItem(reloadingItem))
            {
                if (spec != null && view && view.IsValid() && view.IsOwner()) SendEffect(spec, 2);
                reloadingItem = null;
                return;
            }
            if (Time.time < reloadEnds) return;
            // Transaction at completion: no ammo lost on interruptions or reserve changes.
            int loaded = Magazine.Read(reloadingItem.m_customData, spec.Capacity);
            int remaining = Magazine.ReloadAmount(loaded, Reserve(spec), spec.Capacity), consumed = 0;
            var inventory = player.GetInventory(); var items = inventory.GetAllItems();
            for (int i = items.Count - 1; i >= 0 && remaining > 0; --i)
            {
                var ammo = items[i];
                if (!IsAmmo(ammo, spec)) continue;
                int count = Mathf.Min(remaining, ammo.m_stack);
                if (inventory.RemoveItem(ammo, count)) { consumed += count; remaining -= count; }
            }
            Magazine.Write(reloadingItem.m_customData, loaded + consumed, spec.Capacity);
            reloadingItem = null;
        }

        private static bool IsAmmo(ItemDrop.ItemData item, WeaponSpec spec) => item.m_dropPrefab && item.m_dropPrefab.name == spec.Ammo;

        private int Reserve(WeaponSpec spec)
        {
            int count = 0;
            foreach (var item in player.GetInventory().GetAllItems()) if (IsAmmo(item, spec)) count += item.m_stack;
            return count;
        }

        internal WeaponSpec EquippedSpec()
        {
            if (!view || !view.IsValid()) return null;
            int hash = view.GetZDO().GetInt(ZDOVars.s_rightItem);
            foreach (var spec in WeaponSpec.All) if (hash == spec.Prefab.GetStableHashCode()) return spec;
            return null;
        }

        internal FirearmVisual FindVisual()
        {
            return equipment && equipment.m_rightHand ? equipment.m_rightHand.GetComponentInChildren<FirearmVisual>() : null;
        }

        private void SendEffect(WeaponSpec spec, int action) => view.InvokeRPC(ZNetView.Everybody, EffectsRpc, spec.Prefab.GetStableHashCode(), action);

        private void ReceiveEffects(long sender, int hash, int action)
        {
            if (!view || !view.IsValid() || sender != view.GetZDO().GetOwner()) return;
            var spec = EquippedSpec();
            if (spec == null || spec.Prefab.GetStableHashCode() != hash) return;
            var visual = FindVisual();
            if (!visual) return;
            // Runtime clones don't serialize ordinary C# fields on MonoBehaviours.
            visual.Spec = spec;
            if (action == 1) visual.Reload();
            else if (action == 2) visual.CancelReload();
            else if (action == 0) visual.Shot();
        }

        private void Message(string message)
        {
            if (Time.time < nextMessage) return;
            nextMessage = Time.time + 1;
            player.Message(MessageHud.MessageType.Center, message);
        }

        private void OnDestroy()
        {
            if (hudRoot) Destroy(hudRoot);
        }

        private void UpdateHud()
        {
            if (player != Player.m_localPlayer) return;
            var item = player.GetCurrentWeapon(); var spec = Plugin.Spec(item);
            bool visible = spec != null && !player.IsDead() && !InventoryGui.IsVisible() && !Menu.IsVisible() && !Console.IsVisible();
            if (hudRoot) hudRoot.SetActive(visible);
            if (!visible || !Hud.instance) return;
            if (!hudRoot)
            {
                // Use Valheim's loaded TMP font: the stripped client has no working default IMGUI font.
                hudRoot = new GameObject("FirearmMagazineHUD", typeof(RectTransform), typeof(Image));
                hudRoot.transform.SetParent(Hud.instance.m_rootObject.transform, false);
                var rect = (RectTransform)hudRoot.transform;
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
                rect.anchoredPosition = new Vector2(0, -87);
                rect.sizeDelta = new Vector2(440, 38);
                var background = hudRoot.GetComponent<Image>();
                background.color = new Color(0, 0, 0, .65f); background.raycastTarget = false;
                var label = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
                label.transform.SetParent(rect, false);
                var labelRect = (RectTransform)label.transform;
                labelRect.anchorMin = Vector2.zero; labelRect.anchorMax = Vector2.one;
                labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
                hudText = label.GetComponent<TextMeshProUGUI>();
                hudText.font = Hud.instance.m_healthText.font;
                hudText.fontSize = 20; hudText.color = Color.white;
                hudText.alignment = TextAlignmentOptions.Center; hudText.raycastTarget = false;
            }
            hudText.text = reloadingItem != null ? spec.Label + "  |  RELOADING"
                : spec.Label + "  " + Magazine.Read(item.m_customData, spec.Capacity) + " / " + spec.Capacity + "  |  " + Reserve(spec) + " reserve";
        }
    }
}
