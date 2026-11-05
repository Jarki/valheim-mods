// Handwritten behavioral doubles, not game code. Only the APIs used by StorageTransfer are
// represented. In particular, targeted movement mutates the original source stack, clones into
// empty cells, preserves metadata on merges, and can return false after transferring some units.
public struct Vector2i
{
    public int x;
    public int y;

    public Vector2i(int x, int y)
    {
        this.x = x;
        this.y = y;
    }
}

public sealed class ItemDrop
{
    public sealed class ItemData
    {
        public sealed class SharedData
        {
            public string m_name;
            public int m_maxStackSize;
            public int m_maxQuality = 1;
        }

        public SharedData m_shared;
        public int m_stack;
        public int m_quality = 1;
        public byte m_worldLevel;
        public int m_variant;
        public bool m_equipped;
        public Vector2i m_gridPos;
        public Dictionary<string, string> m_customData = new();

        public ItemData Clone()
        {
            var result = (ItemData)MemberwiseClone();
            result.m_customData = new Dictionary<string, string>(m_customData);
            return result;
        }

        public bool IsSameType(ItemData other)
        {
            return other != null && m_shared.m_name == other.m_shared.m_name &&
                m_worldLevel == other.m_worldLevel &&
                (m_shared.m_maxQuality <= 1 || m_quality == other.m_quality);
        }
    }
}

public sealed class Inventory
{
    private readonly List<ItemDrop.ItemData> items = new();
    private readonly int width;
    private readonly int height;

    public int ChangeCount { get; private set; }

    public Inventory(int width, int height)
    {
        this.width = width;
        this.height = height;
    }

    public int GetWidth() => width;
    public int GetHeight() => height;
    public List<ItemDrop.ItemData> GetAllItems() => items;

    public ItemDrop.ItemData GetItemAt(int x, int y)
    {
        return items.Find(item => item.m_gridPos.x == x && item.m_gridPos.y == y);
    }

    public bool ContainsItemByName(string name)
    {
        return items.Exists(item => item.m_shared.m_name == name);
    }

    // Fixture-only insertion avoids involving the implementation under test in arranging data.
    public void Put(ItemDrop.ItemData item, int x, int y)
    {
        if (x < 0 || x >= width || y < 0 || y >= height || GetItemAt(x, y) != null)
        {
            throw new InvalidOperationException("Invalid fixture grid position.");
        }
        item.m_gridPos = new Vector2i(x, y);
        items.Add(item);
    }

    public bool MoveItemToThis(Inventory source, ItemDrop.ItemData item, int amount, int x, int y)
    {
        amount = Math.Min(amount, item.m_stack);
        bool complete = false;
        if (x >= 0 && x < width && y >= 0 && y < height)
        {
            var existing = GetItemAt(x, y);
            if (existing == null)
            {
                var placed = item.Clone();
                placed.m_stack = amount;
                placed.m_gridPos = new Vector2i(x, y);
                items.Add(placed);
                item.m_stack -= amount;
                complete = true;
                ChangeCount++;
            }
            else if (existing.IsSameType(item) && existing.m_stack < existing.m_shared.m_maxStackSize)
            {
                int moved = Math.Min(amount, existing.m_shared.m_maxStackSize - existing.m_stack);
                existing.m_stack += moved;
                item.m_stack -= moved;
                complete = moved == amount;
                ChangeCount++;
            }
        }

        if (item.m_stack == 0)
        {
            if (source.items.Remove(item))
            {
                source.ChangeCount++;
            }
        }
        else
        {
            source.ChangeCount++;
        }
        return complete;
    }
}
