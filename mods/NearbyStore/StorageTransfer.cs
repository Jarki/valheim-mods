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

        internal static int Deposit(Inventory source, Inventory destination,
            Func<ItemDrop.ItemData, bool> protectedItem = null)
        {
            if (ReferenceEquals(source, destination))
            {
                return 0;
            }

            int transferred = 0;
            var items = source.GetAllItems();
            // Keep vanilla source order without allocating its snapshot list. A successful
            // native move removes this entry; a partial move leaves it in its original cell.
            for (int index = 0; index < items.Count;)
            {
                var item = items[index];
                if (item.m_equipped || IsFavorite(item) || (protectedItem != null && protectedItem(item)) ||
                    !destination.ContainsItemByName(item.m_shared.m_name))
                {
                    index++;
                    continue;
                }

                int before = item.m_stack;
                int count = items.Count;
                // Same AddItem path as vanilla StackAll: merge first, then use an empty slot.
                destination.MoveItemToThis(source, item);
                if (items.Count < count)
                {
                    transferred += before;
                }
                else
                {
                    transferred += before - item.m_stack;
                    index++;
                }
            }

            return transferred;
        }

    }
}
