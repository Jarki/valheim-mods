using System;
using System.Collections.Generic;
using System.Diagnostics;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NearbyStore
{
    [BepInPlugin("jarki.nearbystore", "NearbyStore", "1.1.4")]
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
        private static readonly AccessTools.FieldRef<InventoryGui, float> CraftTimer =
            AccessTools.FieldRefAccess<InventoryGui, float>("m_craftTimer");

        private const int MaxRequests = 4;
        private const int MaxStepsPerFrame = 32;
        private const float RequestTimeout = 5f;
        private const float OperationTimeout = 15f;
        private static readonly long FrameBudgetTicks = Math.Max(1, Stopwatch.Frequency / 500);
        private static readonly Predicate<Container> DestroyedContainer = chest => !chest;

        private struct TransferRequest
        {
            public Container Chest;
            public bool Answered;
            public bool Granted;
            public bool Loaded;
            public float Deadline;
            public int ItemIndex;
        }

        private readonly List<Container> chests = new List<Container>();
        private readonly HashSet<Container> outstanding = new HashSet<Container>();
        private readonly HashSet<Container> vanillaOutstanding = new HashSet<Container>();
        private readonly TransferRequest[] requests = new TransferRequest[MaxRequests];
        private readonly List<ItemDrop.ItemData> transferItems = new List<ItemDrop.ItemData>();
        private readonly Dictionary<InventoryElement, TMP_Text> markers = new Dictionary<InventoryElement, TMP_Text>();
        private ConfigEntry<float> radius;
        private ConfigEntry<KeyboardShortcut> favoriteKey;
        private Harmony harmony;
        private GameObject panel;
        private Button storeButton;
        private Player actor;
        private bool running;
        private bool skippedTimeout;
        private int index;
        private int moved;
        private int nextRequest;
        private float deadline;
        private float nextPrune;
        private InventoryGrid markerGrid;
        private InventoryElement firstMarkerCell;
        private int markerCellCount;
        private Func<ItemDrop.ItemData, bool> protectedItem;

        private void Awake()
        {
            instance = this;
            radius = Config.Bind("Storage", "Radius", 10f,
                new ConfigDescription("Nearby chest radius in metres.", new AcceptableValueRange<float>(1f, 50f)));
            favoriteKey = Config.Bind("Controls", "FavoriteShortcut", new KeyboardShortcut(KeyCode.F, KeyCode.LeftAlt),
                "Toggle favorite on the hovered player-inventory stack while inventory is open.");
            protectedItem = IsProtected;
            harmony = new Harmony("jarki.nearbystore");
            harmony.PatchAll(typeof(Plugin));
        }

        private bool Ready(bool storage = true)
        {
            var player = Player.m_localPlayer;
            var gui = InventoryGui.instance;
            return player && !player.IsDead() && !player.IsTeleporting() && !player.InCutscene() &&
                gui && InventoryGui.IsVisible() && DragItem(gui) == null &&
                !gui.m_splitDialog.IsActive && !Menu.IsVisible() && !Console.IsVisible() &&
                (!Chat.instance || !Chat.instance.HasFocus()) && !Minimap.IsOpen() &&
                !TextInput.IsVisible() &&
                (!storage || (!gui.IsContainerOpen() && !player.GetCurrentCraftingStation() &&
                    CraftTimer(gui) < 0f && !gui.IsSkillsPanelOpen && !gui.IsTextPanelOpen &&
                    !gui.IsTrophisPanelOpen && !gui.IsAchievementsPanelOpen &&
                    !gui.m_variantDialog.gameObject.activeInHierarchy &&
                    !StoreGui.IsVisible() && (!TextViewer.instance || !TextViewer.instance.IsVisible()) &&
                    !PlayerCustomizaton.IsBarberGuiVisible() && !Hud.IsPieceSelectionVisible() &&
                    !Hud.InRadial() && !GameCamera.InFreeFly()));
        }

        private bool IsProtected(ItemDrop.ItemData item)
        {
            return item.m_shared.m_questItem || (actor && actor.IsEquipActionQueued(item));
        }

        private void LateUpdate()
        {
            if (Time.unscaledTime >= nextPrune)
            {
                outstanding.RemoveWhere(DestroyedContainer);
                vanillaOutstanding.RemoveWhere(DestroyedContainer);
                nextPrune = Time.unscaledTime + 1f;
            }
            var gui = InventoryGui.instance;
            if (!gui || !InventoryGui.IsVisible())
            {
                if (panel && panel.activeSelf) panel.SetActive(false);
                if (running) Finish();
                return;
            }
            EnsurePanel(gui);
            bool storageReady = Ready();
            if (panel.activeSelf != storageReady) panel.SetActive(storageReady);
            storeButton.interactable = storageReady && !running;
            if (running && (!storageReady || actor != Player.m_localPlayer)) Finish();
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
            if (running) Advance();
            UpdateMarkers(gui.m_playerGrid);
        }


        private void EnsurePanel(InventoryGui gui)
        {
            if (panel) return;
            ClearMarkers();
            panel = new GameObject("NearbyStore", typeof(RectTransform));
            var root = (RectTransform)panel.transform;
            root.SetParent(gui.m_player, false);
            root.anchorMin = root.anchorMax = new Vector2(1f, .5f);
            root.pivot = new Vector2(0f, .5f);
            root.anchoredPosition = new Vector2(8f, 0f);
            root.sizeDelta = new Vector2(56f, 56f);
            var nativeButton = gui.m_craftButton;
            var nativeImage = (Image)nativeButton.targetGraphic;
            var buttonObject = new GameObject("StoreButton", typeof(RectTransform), typeof(Image), typeof(Button));
            var rect = (RectTransform)buttonObject.transform;
            rect.SetParent(root, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var image = buttonObject.GetComponent<Image>();
            image.sprite = nativeImage.sprite;
            image.type = nativeImage.type;
            image.pixelsPerUnitMultiplier = nativeImage.pixelsPerUnitMultiplier;
            image.material = nativeImage.material;
            image.color = nativeImage.color;
            storeButton = buttonObject.GetComponent<Button>();
            storeButton.targetGraphic = image;
            storeButton.spriteState = nativeButton.spriteState;
            storeButton.colors = nativeButton.colors;
            storeButton.transition = nativeButton.transition;
            storeButton.onClick.AddListener(Begin);
            var gold = new Color(1f, .73f, .3f);
            var ivory = new Color(1f, .94f, .72f);
            MakeWandPart(rect, "Wand", new Vector2(-3f, -3f), new Vector2(5f, 30f), -45f, gold);
            MakeWandPart(rect, "Tip", new Vector2(8f, 8f), new Vector2(5f, 8f), -45f, ivory);
            MakeWandPart(rect, "SparkVertical", new Vector2(13f, 14f), new Vector2(2f, 11f), 0f, ivory);
            MakeWandPart(rect, "SparkHorizontal", new Vector2(13f, 14f), new Vector2(11f, 2f), 0f, ivory);
            MakeWandPart(rect, "SparkLeft", new Vector2(-13f, 9f), new Vector2(4f, 4f), 45f, gold);
            MakeWandPart(rect, "SparkRight", new Vector2(14f, -10f), new Vector2(3f, 3f), 45f, gold);
        }

        private static void MakeWandPart(RectTransform parent, string name, Vector2 position,
            Vector2 size, float rotation, Color color)
        {
            var part = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)part.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            rect.localRotation = Quaternion.Euler(0f, 0f, rotation);
            var image = part.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
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

        private void ClearMarkers()
        {
            foreach (var marker in markers.Values)
                if (marker) Destroy(marker.gameObject);
            markers.Clear();
        }

        private void UpdateMarkers(InventoryGrid grid)
        {
            var inventory = grid.GetInventory();
            if (inventory == null) return;
            var cells = Elements(grid);
            var first = cells.Count == 0 ? null : cells[0];
            // Native grid rebuilds replace every element, including same-size rebuilds.
            if (grid != markerGrid || cells.Count != markerCellCount || first != firstMarkerCell)
            {
                ClearMarkers();
                markerGrid = grid;
                firstMarkerCell = first;
                markerCellCount = cells.Count;
            }
            foreach (var element in cells)
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

        private bool HasWork()
        {
            var items = actor.GetInventory().GetAllItems();
            foreach (var item in transferItems)
                if (items.Contains(item) && StorageTransfer.CanTransfer(item, protectedItem)) return true;
            return false;
        }

        private void Begin()
        {
            if (running || !Ready()) return;
            actor = Player.m_localPlayer;
            index = moved = nextRequest = 0;
            skippedTimeout = false;
            transferItems.Clear();
            if (!StorageTransfer.HasTransferableItems(actor.GetInventory(), protectedItem))
            {
                Finish();
                return;
            }
            // Snapshot identities, not permissions: input and access remain live at each step.
            transferItems.AddRange(actor.GetInventory().GetAllItems());
            chests.Clear();
            outstanding.RemoveWhere(DestroyedContainer);
            vanillaOutstanding.RemoveWhere(DestroyedContainer);
            foreach (var chest in FindObjectsByType<Container>(FindObjectsSortMode.None))
                if (Eligible(chest) && !vanillaOutstanding.Contains(chest) &&
                    (!outstanding.Contains(chest) || chest.IsOwner())) chests.Add(chest);
            chests.Sort((a, b) => (a.transform.position - actor.transform.position).sqrMagnitude.CompareTo(
                (b.transform.position - actor.transform.position).sqrMagnitude));
            deadline = Time.unscaledTime + OperationTimeout;
            running = true;
            // Defer mutations until LateUpdate, after this frame's favorite input.
        }

        private void Advance()
        {
            if (Time.unscaledTime >= deadline) { Finish(true); return; }
            if (!HasWork()) { Finish(); return; }
            long stop = Stopwatch.GetTimestamp() + FrameBudgetTicks;
            int idleSlots = 0;
            // Cheap no-op checks use the clock budget, not an artificial frame delay.
            for (int step = 0; step < MaxStepsPerFrame && Stopwatch.GetTimestamp() < stop;)
            {
                int slot = nextRequest;
                nextRequest = (nextRequest + 1) % requests.Length;
                ref var request = ref requests[slot];
                bool progressed = false;
                if (!request.Chest)
                {
                    request = default;
                    if (index < chests.Count)
                    {
                        var chest = chests[index++];
                        step++;
                        progressed = true;
                        if (Eligible(chest) && !vanillaOutstanding.Contains(chest))
                        {
                            if (chest.IsOwner())
                            {
                                request.Chest = chest;
                                request.Answered = request.Granted = true;
                                request.Deadline = Time.unscaledTime + RequestTimeout;
                            }
                            else if (!outstanding.Contains(chest))
                            {
                                request.Chest = chest;
                                request.Deadline = Time.unscaledTime + RequestTimeout;
                                outstanding.Add(chest);
                                View(chest).InvokeRPC("RPC_RequestStack", actor.GetPlayerID());
                            }
                        }
                    }
                }
                else if (!request.Loaded && Time.unscaledTime >= request.Deadline)
                {
                    // Keep an unanswered guard, but free the slot for another destination.
                    skippedTimeout = true;
                    request = default;
                    progressed = true;
                }
                else if (request.Answered)
                {
                    var chest = request.Chest;
                    if (!request.Granted || !Eligible(chest) || vanillaOutstanding.Contains(chest))
                    {
                        request = default;
                        progressed = true;
                    }
                    else if (chest.IsOwner())
                    {
                        if (!request.Loaded)
                        {
                            LoadContainer(chest);
                            request.Loaded = true;
                        }
                        if (request.ItemIndex < transferItems.Count)
                        {
                            var item = transferItems[request.ItemIndex++];
                            int transferred = StorageTransfer.Deposit(actor.GetInventory(), chest.GetInventory(), item, protectedItem);
                            moved += transferred;
                            if (transferred > 0) step++;
                            if (transferred > 0 && !HasWork()) { Finish(); return; }
                        }
                        if (request.ItemIndex >= transferItems.Count) request = default;
                        progressed = true;
                    }
                    else if (request.Loaded)
                    {
                        // Authority lost after a partial transfer: never reuse that inventory.
                        request = default;
                        progressed = true;
                    }
                }
                idleSlots = progressed ? 0 : idleSlots + 1;
                if (idleSlots >= requests.Length) break;
            }
            bool active = false;
            foreach (var request in requests)
                if (request.Chest) { active = true; break; }
            if ((!active && index >= chests.Count) || !HasWork()) Finish();
        }

        private void Finish(bool timedOut = false)
        {
            running = false;
            Array.Clear(requests, 0, requests.Length);
            bool incomplete = timedOut || skippedTimeout;
            if (actor) actor.Message(MessageHud.MessageType.TopLeft,
                incomplete ? "Stacked " + moved + " items nearby; some chests timed out. Retry to continue." :
                moved > 0 ? "Stacked " + moved + " items nearby." : "Nothing stacked: no room in matching chests or eligible items.");
            actor = null;
            chests.Clear();
            transferItems.Clear();
        }

        private bool HasRequest(Container chest)
        {
            foreach (var request in requests)
                if (request.Chest == chest) return true;
            return false;
        }

        [HarmonyPatch(typeof(Container), nameof(Container.StackAll)), HarmonyPrefix]
        private static bool VanillaStack(Container __instance)
        {
            if (!instance) return true;
            // Native replies have no request ID. Never let two callers share one container.
            if (instance.outstanding.Contains(__instance) || instance.HasRequest(__instance) ||
                instance.vanillaOutstanding.Contains(__instance))
            {
                if (Player.m_localPlayer) Player.m_localPlayer.Message(MessageHud.MessageType.TopLeft,
                    "Waiting for this chest's previous stack response.");
                return false;
            }
            instance.vanillaOutstanding.Add(__instance);
            return true;
        }

        [HarmonyPatch(typeof(Container), "RPC_StackResponse"), HarmonyPrefix]
        private static bool StackResponse(Container __instance, bool granted)
        {
            if (!instance) return true;
            if (instance.vanillaOutstanding.Remove(__instance)) return true;
            if (!instance.outstanding.Remove(__instance)) return true;
            if (instance.running)
            {
                for (int i = 0; i < instance.requests.Length; i++)
                {
                    ref var request = ref instance.requests[i];
                    // An owned retry may coexist with an older unanswered guard.
                    if (request.Chest != __instance || request.Answered) continue;
                    request.Answered = true;
                    request.Granted = granted;
                    break;
                }
            }
            // Consume the original response even after cancellation or timeout.
            return false;
        }

        private void OnDestroy()
        {
            if (running) Finish();
            harmony?.UnpatchSelf();
            if (panel) Destroy(panel);
            ClearMarkers();
            if (instance == this) instance = null;
        }
    }
}
