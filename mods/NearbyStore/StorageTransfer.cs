using System;

namespace NearbyStore
{
    internal static class StorageTransfer
    {
        internal const string FavoriteKey = "jarki.nearbystore.favorite";

        internal static bool IsFavorite(ItemDrop.ItemData item)
        {
            return item.m_customData.ContainsKey(FavoriteKey);
        }

        internal static bool ToggleFavorite(ItemDrop.ItemData item)
        {
            if (item.m_customData.Remove(FavoriteKey))
            {
                return false;
            }

            item.m_customData[FavoriteKey] = "1";
            return true;
        }

        internal static bool CanTransfer(ItemDrop.ItemData item,
            Func<ItemDrop.ItemData, bool> protectedItem)
        {
            return !item.m_equipped && !IsFavorite(item) &&
                (protectedItem == null || !protectedItem(item));
        }

        internal static bool HasTransferableItems(Inventory source,
            Func<ItemDrop.ItemData, bool> protectedItem = null)
        {
            foreach (var item in source.GetAllItems())
                if (CanTransfer(item, protectedItem)) return true;
            return false;
        }

        internal static int Deposit(Inventory source, Inventory destination,
            ItemDrop.ItemData item, Func<ItemDrop.ItemData, bool> protectedItem = null)
        {
            if (ReferenceEquals(source, destination) ||
                !source.GetAllItems().Contains(item) || !CanTransfer(item, protectedItem) ||
                !HasCapacity(destination, item))
                return 0;

            int before = item.m_stack;
            // Native insertion owns merging, metadata and inventory notifications.
            destination.MoveItemToThis(source, item);
            return source.GetAllItems().Contains(item) ? before - item.m_stack : before;
        }

        private static bool HasCapacity(Inventory destination, ItemDrop.ItemData item)
        {
            var items = destination.GetAllItems();
            bool spareSlot = items.Count < destination.GetWidth() * destination.GetHeight();
            foreach (var existing in items)
            {
                if (existing.m_shared.m_name != item.m_shared.m_name) continue;
                // StackAll requires a matching name even when an empty cell exists.
                if (spareSlot) return true;
                // Mirror native FindFreeStackItem, not CanAddItem (which ignores quality).
                if (item.m_shared.m_maxStackSize > 1 &&
                    existing.m_quality == item.m_quality &&
                    existing.m_worldLevel == item.m_worldLevel &&
                    existing.m_stack < existing.m_shared.m_maxStackSize)
                    return true;
            }
            return false;
        }

    }
}
