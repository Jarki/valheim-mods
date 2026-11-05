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

        internal static int Deposit(Inventory source, Inventory destination, bool matchingOnly,
            Func<ItemDrop.ItemData, bool> protectedItem = null)
        {
            if (ReferenceEquals(source, destination))
            {
                return 0;
            }

            int transferred = 0;
            var items = source.GetAllItems();
            var storedItems = destination.GetAllItems();
            int width = destination.GetWidth();
            int height = destination.GetHeight();
            // Vanilla removes exhausted source stacks. Reverse traversal keeps the live list safe
            // without copying it and leaves every remaining source stack in its original grid cell.
            for (int index = items.Count - 1; index >= 0; index--)
            {
                var item = items[index];
                if (item.m_equipped || IsFavorite(item) || (protectedItem != null && protectedItem(item)) ||
                    (matchingOnly && !destination.ContainsItemByName(item.m_shared.m_name)))
                {
                    continue;
                }

                for (int storedIndex = 0; storedIndex < storedItems.Count && item.m_stack > 0; storedIndex++)
                {
                    var stored = storedItems[storedIndex];
                    int space = stored.m_shared.m_maxStackSize - stored.m_stack;
                    if (space > 0 && stored.IsSameType(item))
                    {
                        // Targeted movement preserves destination metadata, including favorites.
                        transferred += Move(source, destination, item, Math.Min(space, item.m_stack),
                            stored.m_gridPos.x, stored.m_gridPos.y);
                    }
                }

                for (int y = 0; y < height && item.m_stack > 0; y++)
                {
                    for (int x = 0; x < width && item.m_stack > 0; x++)
                    {
                        if (destination.GetItemAt(x, y) == null)
                        {
                            transferred += Move(source, destination, item,
                                Math.Min(item.m_shared.m_maxStackSize, item.m_stack), x, y);
                        }
                    }
                }
            }

            return transferred;
        }

        private static int Move(Inventory source, Inventory destination, ItemDrop.ItemData item,
            int amount, int x, int y)
        {
            int before = item.m_stack;
            // The amount/coordinates overload clones into empty cells and decrements the original
            // stack even on a partial move. Its bool is not a transferred-unit count.
            destination.MoveItemToThis(source, item, amount, x, y);
            return before - item.m_stack;
        }
    }
}
