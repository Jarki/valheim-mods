using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NearbyStore
{
    [BepInPlugin("jarki.nearbystore", "NearbyStore", "1.1.1")]
    public sealed class Plugin : BaseUnityPlugin
    {
        private static Plugin instance;
        private static readonly AccessTools.FieldRef<Container, ZNetView> View =
            AccessTools.FieldRefAccess<Container, ZNetView>("m_nview");
        private static readonly Func<Container, bool> LoadContainer =
            AccessTools.MethodDelegate<Func<Container, bool>>(AccessTools.Method(typeof(Container), "Load"));
        private static readonly Func<Container, long, bool> HasAccess =
            AccessTools.MethodDelegate<Func<Container, long, bool>>(AccessTools.Method(typeof(Container), "CheckAccess"));
        private static readonly AccessTools.FieldRef<InventoryGui, ItemDrop.ItemData> DragItem =
            AccessTools.FieldRefAccess<InventoryGui, ItemDrop.ItemData>("m_dragItem");
        private static readonly AccessTools.FieldRef<InventoryGrid, List<InventoryElement>> Elements =
            AccessTools.FieldRefAccess<InventoryGrid, List<InventoryElement>>("m_elements");

        private readonly List<Container> chests = new List<Container>();
        private readonly HashSet<Container> outstanding = new HashSet<Container>();
        private readonly Dictionary<InventoryElement, TMP_Text> markers = new Dictionary<InventoryElement, TMP_Text>();
        private ConfigEntry<float> radius;
        private ConfigEntry<KeyboardShortcut> favoriteKey;
        private Harmony harmony;
        private GameObject panel;
        private Button storeButton;
        private TMP_Text caption;
        private TMP_Text hint;
        private Player actor;
        private Container pending;
        private bool running;
        private bool answered;
        private bool granted;
        private int index;
        private int moved;
        private float deadline;
        private Func<ItemDrop.ItemData, bool> protectedItem;
        private string shortcutLabel;

        private void Awake()
        {
            instance = this;
            radius = Config.Bind("Storage", "Radius", 10f,
                new ConfigDescription("Nearby chest radius in metres.", new AcceptableValueRange<float>(1f, 50f)));
            favoriteKey = Config.Bind("Controls", "FavoriteShortcut", new KeyboardShortcut(KeyCode.F, KeyCode.LeftAlt),
                "Toggle favorite on the hovered player-inventory stack while inventory is open.");
            UpdateShortcutLabel(null, EventArgs.Empty);
            favoriteKey.SettingChanged += UpdateShortcutLabel;
            protectedItem = IsProtected;
            harmony = new Harmony("jarki.nearbystore");
            harmony.PatchAll(typeof(Plugin));
        }

        private bool Ready(bool storage = true)
        {
            var player = Player.m_localPlayer;
            var gui = InventoryGui.instance;
            return player && !player.IsDead() && !player.IsTeleporting() && !player.InCutscene() &&
                gui && InventoryGui.IsVisible() && (!storage || !gui.IsContainerOpen()) && DragItem(gui) == null &&
                !gui.m_splitDialog.IsActive && !Menu.IsVisible() && !Console.IsVisible() &&
                (!Chat.instance || !Chat.instance.HasFocus()) && !Minimap.IsOpen() &&
                !TextInput.IsVisible();
        }

        private bool IsProtected(ItemDrop.ItemData item)
        {
            return item.m_shared.m_questItem || (actor && actor.IsEquipActionQueued(item));
        }

        private void LateUpdate()
        {
            var gui = InventoryGui.instance;
            if (!gui || !InventoryGui.IsVisible())
            {
                if (running) Finish();
                return;
            }
            EnsurePanel(gui);
            storeButton.interactable = Ready() && !running;
            if (running && (!Ready() || actor != Player.m_localPlayer)) Finish();
            if (running) Advance();
            if (Ready(false) && favoriteKey.Value.IsDown())
            {
                var cursor = ZInput.pointerPosition;
                var item = gui.m_playerGrid.GetItem(new Vector2i((int)cursor.x, (int)cursor.y));
                if (item != null)
                {
                    bool favorite = StorageTransfer.ToggleFavorite(item);
                    Player.m_localPlayer.GetInventory().m_onChanged?.Invoke();
                    Player.m_localPlayer.Message(MessageHud.MessageType.TopLeft,
                        (favorite ? "Favorited: " : "Unfavorited: ") + Localization.instance.Localize(item.m_shared.m_name));
                }
            }
            if (hint.text != shortcutLabel) hint.text = shortcutLabel;
            UpdateMarkers(gui.m_playerGrid);
        }

        private void UpdateShortcutLabel(object sender, EventArgs args)
        {
            shortcutLabel = favoriteKey.Value.ToString() + " : favorite hovered item";
        }

        private void EnsurePanel(InventoryGui gui)
        {
            if (panel) return;
            markers.Clear();
            panel = new GameObject("NearbyStore", typeof(RectTransform));
            var root = (RectTransform)panel.transform;
            root.SetParent(gui.m_player, false);
            root.anchorMin = root.anchorMax = new Vector2(0f, 0f);
            root.pivot = new Vector2(0f, 1f);
            root.anchoredPosition = new Vector2(0f, -8f);
            root.sizeDelta = new Vector2(260f, 68f);
            var buttonObject = new GameObject("StoreButton", typeof(RectTransform), typeof(Image), typeof(Button));
            var rect = (RectTransform)buttonObject.transform;
            rect.SetParent(root, false);
            rect.anchorMin = new Vector2(0f, .45f);
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var image = buttonObject.GetComponent<Image>();
            image.color = new Color(.12f, .1f, .07f, .96f);
            storeButton = buttonObject.GetComponent<Button>();
            storeButton.targetGraphic = image;
            storeButton.onClick.AddListener(Begin);
            caption = MakeText(rect, gui.m_playerName, "Stack nearby", 18f);
            var note = MakeText(root, gui.m_playerName, "", 13f);
            var noteRect = note.rectTransform;
            noteRect.anchorMax = new Vector2(1f, .4f);
            hint = note;
        }

        private static TMP_Text MakeText(Transform parent, TMP_Text template, string text, float size)
        {
            var go = new GameObject("NearbyStoreText", typeof(RectTransform));
            go.SetActive(false);
            var label = go.AddComponent<TextMeshProUGUI>();
            label.transform.SetParent(parent, false);
            label.font = template.font;
            label.fontSharedMaterial = template.fontSharedMaterial;
            label.fontSize = size;
            label.color = new Color(1f, .84f, .45f);
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            label.text = text;
            var rect = label.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            go.SetActive(true);
            return label;
        }

        private void UpdateMarkers(InventoryGrid grid)
        {
            var inventory = grid.GetInventory();
            if (inventory == null) return;
            foreach (var element in Elements(grid))
            {
                var item = inventory.GetItemAt(element.Position.x, element.Position.y);
                bool favorite = item != null && StorageTransfer.IsFavorite(item);
                if (!markers.TryGetValue(element, out var label) || !label)
                {
                    if (!favorite) continue;
                    label = MakeText(element.transform, element.m_amount, "F", 16f);
                    label.alignment = TextAlignmentOptions.TopLeft;
                    label.rectTransform.offsetMin = new Vector2(4f, 0f);
                    label.rectTransform.offsetMax = new Vector2(0f, -3f);
                    markers[element] = label;
                }
                label.gameObject.SetActive(favorite);
            }
        }

        private bool Eligible(Container chest)
        {
            if (!chest || !actor || chest.m_autoDestroyEmpty || !chest.GetComponent<Piece>() ||
                chest.GetComponent<TombStone>() || chest.GetInventory() == null || chest.IsInUse() ||
                (chest.m_wagon && chest.m_wagon.InUse()) ||
                (chest.transform.position - actor.transform.position).sqrMagnitude > radius.Value * radius.Value)
                return false;
            var view = View(chest);
            return view && view.IsValid() && HasAccess(chest, actor.GetPlayerID()) &&
                (!chest.m_checkGuardStone || PrivateArea.CheckAccess(chest.transform.position, 0f, false));
        }

        private void Begin()
        {
            if (running || !Ready()) return;
            actor = Player.m_localPlayer;
            chests.Clear();
            foreach (var chest in FindObjectsByType<Container>(FindObjectsSortMode.None))
                if (Eligible(chest) && (!outstanding.Contains(chest) || chest.IsOwner())) chests.Add(chest);
            chests.Sort((a, b) => (a.transform.position - actor.transform.position).sqrMagnitude.CompareTo(
                (b.transform.position - actor.transform.position).sqrMagnitude));
            index = moved = 0;
            running = true;
            caption.text = "Stacking...";
            Advance();
        }

        private void Advance()
        {
            if (pending)
            {
                // A granted RPC can precede its queued ownership/inventory ZDO.
                if (!answered || (granted && Eligible(pending) && !pending.IsOwner()))
                {
                    if (Time.unscaledTime > deadline) Finish(true);
                    return;
                }
                var chest = pending;
                pending = null;
                if (granted && Eligible(chest) && chest.IsOwner())
                {
                    // Refresh the synchronized inventory only after ownership has arrived.
                    LoadContainer(chest);
                    moved += StorageTransfer.Deposit(actor.GetInventory(), chest.GetInventory(), protectedItem);
                }
            }
            while (index < chests.Count)
            {
                var chest = chests[index++];
                if (!Eligible(chest)) continue;
                if (chest.IsOwner())
                {
                    LoadContainer(chest);
                    moved += StorageTransfer.Deposit(actor.GetInventory(), chest.GetInventory(), protectedItem);
                    continue;
                }
                // Do not overlap uncorrelatable vanilla requests after a timeout.
                if (outstanding.Contains(chest)) continue;
                pending = chest;
                answered = granted = false;
                deadline = Time.unscaledTime + 5f;
                outstanding.Add(chest);
                View(chest).InvokeRPC("RPC_RequestStack", actor.GetPlayerID());
                return;
            }
            Finish();
        }

        private void Finish(bool timedOut = false)
        {
            running = false;
            pending = null;
            if (caption) caption.text = "Stack nearby";
            if (actor) actor.Message(MessageHud.MessageType.TopLeft,
                timedOut ? "Stacking timed out. Open the chest normally, close it, then retry." :
                moved > 0 ? "Stacked " + moved + " items nearby." : "Nothing stacked: no room in matching chests or eligible items.");
            actor = null;
            chests.Clear();
        }

        [HarmonyPatch(typeof(Container), "RPC_StackResponse"), HarmonyPrefix]
        private static bool StackResponse(Container __instance, bool granted)
        {
            if (!instance || !instance.outstanding.Remove(__instance)) return true;
            if (instance.running && instance.pending == __instance)
            {
                instance.answered = true;
                instance.granted = granted;
            }
            // Also consume late responses after cancellation, never run vanilla StackAll on favorites.
            return false;
        }

        private void OnDestroy()
        {
            if (running) Finish();
            favoriteKey.SettingChanged -= UpdateShortcutLabel;
            harmony?.UnpatchSelf();
            if (panel) Destroy(panel);
            foreach (var marker in markers.Values)
                if (marker) Destroy(marker.gameObject);
            if (instance == this) instance = null;
        }
    }
}
