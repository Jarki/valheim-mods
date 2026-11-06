// Handwritten behavioral doubles, not game code. Normal inventory movement fills stacks
// with matching name, quality and world level, then places the same item in a free cell.
// A full destination can accept a partial amount while retaining the source item.
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

    public void MoveItemToThis(Inventory source, ItemDrop.ItemData item)
    {
        int remaining = item.m_stack;
        if (item.m_shared.m_maxStackSize > 1)
        {
            foreach (var existing in items)
            {
                if (existing.m_shared.m_name != item.m_shared.m_name ||
                    existing.m_quality != item.m_quality || existing.m_worldLevel != item.m_worldLevel)
                    continue;
                int moved = Math.Min(remaining, existing.m_shared.m_maxStackSize - existing.m_stack);
                existing.m_stack += moved;
                remaining -= moved;
                if (remaining == 0) break;
            }
        }
        if (remaining == 0)
        {
            source.items.Remove(item);
        }
        else
        {
            item.m_stack = remaining;
            // These fixtures are resources; vanilla searches their rows bottom first.
            for (int y = height - 1; y >= 0; y--)
            {
                for (int x = 0; x < width; x++)
                {
                    if (GetItemAt(x, y) != null) continue;
                    source.items.Remove(item);
                    Put(item, x, y);
                    source.ChangeCount++;
                    ChangeCount++;
                    return;
                }
            }
        }
        source.ChangeCount++;
        ChangeCount++;
    }
}
