using NearbyStore;

var tests = new (string Name, Action Run)[]
{
    ("favorite toggling preserves other custom data", FavoriteMarker),
    ("favorite equipped and caller-protected stacks stay put", ProtectedStacks),
    ("matching-only skips types absent from the destination", MatchingOnly),
    ("matching-only can open a new stack of an existing type", MatchingNewStack),
    ("partial capacity counts units and preserves source grid", PartialCapacity),
    ("empty and full inventories do not move anything", NoCapacity),
    ("merge before empty cells preserves destination favorites", MergeFavoriteDestination),
    ("new destination stacks do not alias source metadata", NewStackMetadata),
    ("quality and world-level incompatibility prevent merging", CompatibilityBoundaries),
    ("multiple source stacks are not skipped after removal", MultipleSourceStacks),
    ("matching pass across chests precedes fallback pass", MultipleChestPasses),
    ("self-deposit is a no-op", SelfDeposit),
    ("two-pass deposits conserve quantities across capacity boundaries", CapacityMatrix),
};

int failures = 0;
foreach ((string name, Action run) in tests)
{
    try
    {
        run();
        Console.WriteLine($"PASS: {name}");
    }
    catch (Exception exception)
    {
        failures++;
        Console.Error.WriteLine($"FAIL: {name} - {exception.Message}");
    }
}

Console.WriteLine($"{tests.Length - failures}/{tests.Length} tests passed.");
return failures == 0 ? 0 : 1;

static void FavoriteMarker()
{
    var item = Item("wood", 10);
    item.m_customData["another.mod"] = "untouched";
    False(StorageTransfer.IsFavorite(item));
    True(StorageTransfer.ToggleFavorite(item));
    True(StorageTransfer.IsFavorite(item));
    False(StorageTransfer.ToggleFavorite(item));
    False(StorageTransfer.IsFavorite(item));
    Equal("untouched", item.m_customData["another.mod"]);
    item.m_customData[StorageTransfer.FavoriteKey] = "";
    True(StorageTransfer.IsFavorite(item));
    False(StorageTransfer.ToggleFavorite(item));
}

static void ProtectedStacks()
{
    var source = new Inventory(4, 2);
    var favorite = Item("wood", 8);
    var equipped = Item("sword", 1, 1);
    var queued = Item("shield", 1, 1);
    var ordinary = Item("stone", 9);
    StorageTransfer.ToggleFavorite(favorite);
    equipped.m_equipped = true;
    source.Put(favorite, 3, 0);
    source.Put(equipped, 1, 1);
    source.Put(queued, 2, 0);
    source.Put(ordinary, 0, 1);
    var destination = new Inventory(4, 2);

    Equal(9, StorageTransfer.Deposit(source, destination, false, item => ReferenceEquals(item, queued)));
    Equal(3, source.GetAllItems().Count);
    Same(favorite, source.GetItemAt(3, 0));
    Same(equipped, source.GetItemAt(1, 1));
    Same(queued, source.GetItemAt(2, 0));
    Equal(8, favorite.m_stack);
    Equal(1, equipped.m_stack);
    Equal(1, queued.m_stack);
    Equal(9, Units(destination, "stone"));
    ValidGrid(source);
    ValidGrid(destination);
}

static void MatchingOnly()
{
    var source = new Inventory(2, 1);
    var wood = Item("wood", 4);
    var stone = Item("stone", 6);
    source.Put(wood, 1, 0);
    source.Put(stone, 0, 0);
    var destination = new Inventory(3, 1);
    destination.Put(Item("wood", 3), 0, 0);
    Equal(4, StorageTransfer.Deposit(source, destination, true));
    Equal(7, Units(destination, "wood"));
    Equal(0, Units(destination, "stone"));
    Same(stone, source.GetItemAt(0, 0));
    Equal(6, stone.m_stack);
    Equal(6, StorageTransfer.Deposit(source, destination, false));
    Equal(0, source.GetAllItems().Count);
}

static void MatchingNewStack()
{
    var source = new Inventory(2, 1);
    var wood = Item("wood", 7);
    source.Put(wood, 1, 0);
    var destination = new Inventory(2, 1);
    destination.Put(Item("wood", 10, 10), 0, 0);
    Equal(7, StorageTransfer.Deposit(source, destination, true));
    Equal(17, Units(destination, "wood"));
    Equal(7, destination.GetItemAt(1, 0).m_stack);
    Equal(0, source.GetAllItems().Count);
}

static void PartialCapacity()
{
    var source = new Inventory(4, 3);
    var wood = Item("wood", 8, 10);
    source.Put(wood, 3, 2);
    var destination = new Inventory(1, 1);
    destination.Put(Item("wood", 7, 10), 0, 0);
    Equal(3, StorageTransfer.Deposit(source, destination, false));
    Equal(5, wood.m_stack);
    Same(wood, source.GetItemAt(3, 2));
    Equal(3, wood.m_gridPos.x);
    Equal(2, wood.m_gridPos.y);
    Equal(10, Units(destination, "wood"));
    Equal(15, Units(source, "wood") + Units(destination, "wood"));
    True(source.ChangeCount > 0);
    True(destination.ChangeCount > 0);
    Equal(0, StorageTransfer.Deposit(source, destination, false));
    ValidGrid(source);
    ValidGrid(destination);
}

static void NoCapacity()
{
    var source = new Inventory(1, 1);
    var destination = new Inventory(1, 1);
    Equal(0, StorageTransfer.Deposit(source, destination, false));
    source.Put(Item("wood", 9), 0, 0);
    Equal(0, StorageTransfer.Deposit(source, destination, true));
    destination.Put(Item("stone", 10, 10), 0, 0);
    Equal(0, StorageTransfer.Deposit(source, destination, false));
    Equal(9, Units(source, "wood"));
    Equal(0, source.ChangeCount);
    Equal(0, destination.ChangeCount);
}

static void MergeFavoriteDestination()
{
    var source = new Inventory(1, 1);
    var item = Item("wood", 7, 10);
    item.m_customData["source-only"] = "new stacks only";
    source.Put(item, 0, 0);
    var destination = new Inventory(2, 1);
    var favorite = Item("wood", 8, 10);
    favorite.m_customData["other.mod"] = "keep";
    StorageTransfer.ToggleFavorite(favorite);
    destination.Put(favorite, 1, 0);

    Equal(7, StorageTransfer.Deposit(source, destination, false));
    Same(favorite, destination.GetItemAt(1, 0));
    Equal(10, favorite.m_stack);
    True(StorageTransfer.IsFavorite(favorite));
    Equal("keep", favorite.m_customData["other.mod"]);
    False(favorite.m_customData.ContainsKey("source-only"));
    Equal(5, destination.GetItemAt(0, 0).m_stack);
    False(StorageTransfer.IsFavorite(destination.GetItemAt(0, 0)));
    Equal("new stacks only", destination.GetItemAt(0, 0).m_customData["source-only"]);
}

static void NewStackMetadata()
{
    var source = new Inventory(2, 2);
    var original = Item("ore", 25, 10);
    original.m_quality = 3;
    original.m_worldLevel = 2;
    original.m_variant = 4;
    original.m_customData["payload"] = "preserved";
    source.Put(original, 1, 1);
    var destination = new Inventory(2, 1);

    Equal(20, StorageTransfer.Deposit(source, destination, false));
    Equal(5, original.m_stack);
    Same(original, source.GetItemAt(1, 1));
    var first = destination.GetItemAt(0, 0);
    var second = destination.GetItemAt(1, 0);
    False(ReferenceEquals(first, original));
    False(ReferenceEquals(first, second));
    Equal(10, first.m_stack);
    Equal(10, second.m_stack);
    Equal(3, first.m_quality);
    Equal((byte)2, first.m_worldLevel);
    Equal(4, first.m_variant);
    Equal("preserved", first.m_customData["payload"]);
    first.m_customData["payload"] = "changed";
    Equal("preserved", original.m_customData["payload"]);
    Equal("preserved", second.m_customData["payload"]);
    ValidGrid(source);
    ValidGrid(destination);
}

static void CompatibilityBoundaries()
{
    foreach (bool differentWorld in new[] { false, true })
    {
        var source = new Inventory(1, 1);
        var incoming = Item("arrow", 6, 10);
        incoming.m_shared.m_maxQuality = 4;
        incoming.m_quality = differentWorld ? 1 : 2;
        incoming.m_worldLevel = differentWorld ? (byte)1 : (byte)0;
        source.Put(incoming, 0, 0);
        var destination = new Inventory(2, 1);
        var existing = Item("arrow", 4, 10);
        existing.m_shared.m_maxQuality = 4;
        destination.Put(existing, 0, 0);
        Equal(6, StorageTransfer.Deposit(source, destination, true));
        Equal(4, existing.m_stack);
        Equal(6, destination.GetItemAt(1, 0).m_stack);
    }

    // Vanilla IsSameType ignores visual variant and ignores quality for maxQuality == 1.
    var compatibleSource = new Inventory(1, 1);
    var compatible = Item("wood", 3, 10);
    compatible.m_variant = 9;
    compatible.m_quality = 8;
    compatibleSource.Put(compatible, 0, 0);
    var compatibleDestination = new Inventory(1, 1);
    compatibleDestination.Put(Item("wood", 2, 10), 0, 0);
    Equal(3, StorageTransfer.Deposit(compatibleSource, compatibleDestination, false));
    Equal(5, Units(compatibleDestination, "wood"));

    var blockedSource = new Inventory(1, 1);
    var blocked = Item("wood", 3, 10);
    blocked.m_worldLevel = 1;
    blockedSource.Put(blocked, 0, 0);
    Equal(0, StorageTransfer.Deposit(blockedSource, compatibleDestination, true));
    Same(blocked, blockedSource.GetItemAt(0, 0));
}

static void MultipleSourceStacks()
{
    var source = new Inventory(3, 1);
    source.Put(Item("wood", 3, 10), 2, 0);
    source.Put(Item("wood", 4, 10), 0, 0);
    source.Put(Item("stone", 5, 10), 1, 0);
    var destination = new Inventory(3, 1);
    Equal(12, StorageTransfer.Deposit(source, destination, false));
    Equal(0, source.GetAllItems().Count);
    Equal(7, Units(destination, "wood"));
    Equal(5, Units(destination, "stone"));
    ValidGrid(destination);
}

static void MultipleChestPasses()
{
    var source = new Inventory(3, 1);
    source.Put(Item("wood", 8, 10), 0, 0);
    source.Put(Item("stone", 4, 10), 1, 0);
    source.Put(Item("resin", 2, 10), 2, 0);
    var spare = new Inventory(1, 1);
    var woodChest = new Inventory(1, 1);
    var stoneChest = new Inventory(1, 1);
    woodChest.Put(Item("wood", 7, 10), 0, 0);
    stoneChest.Put(Item("stone", 8, 10), 0, 0);
    var chests = new[] { spare, woodChest, stoneChest };

    int moved = 0;
    foreach (var chest in chests)
    {
        moved += StorageTransfer.Deposit(source, chest, true);
    }
    Equal(5, moved);
    Equal(0, spare.GetAllItems().Count);
    Equal(5, Units(source, "wood"));
    Equal(2, Units(source, "stone"));
    Equal(2, Units(source, "resin"));
    foreach (var chest in chests)
    {
        moved += StorageTransfer.Deposit(source, chest, false);
    }
    Equal(7, moved);
    Equal(2, Units(spare, "resin"));
    Equal(10, Units(woodChest, "wood"));
    Equal(10, Units(stoneChest, "stone"));
    Equal(7, source.GetAllItems().Sum(item => item.m_stack));
    ValidGrid(source);
    foreach (var chest in chests)
    {
        ValidGrid(chest);
    }
}

static void SelfDeposit()
{
    var inventory = new Inventory(2, 1);
    var item = Item("wood", 4);
    inventory.Put(item, 1, 0);
    Equal(0, StorageTransfer.Deposit(inventory, inventory, false));
    Equal(0, StorageTransfer.Deposit(inventory, inventory, true));
    Same(item, inventory.GetItemAt(1, 0));
    Equal(4, item.m_stack);
    Equal(0, inventory.ChangeCount);
}

static void CapacityMatrix()
{
    foreach (int maxStack in new[] { 1, 10, 50 })
    {
        foreach (int sourceAmount in new[] { 1, maxStack, maxStack + 1, 2 * maxStack + 3 })
        {
            foreach (int usedCapacity in new[] { 0, maxStack - 1, maxStack })
            {
                var source = new Inventory(4, 2);
                var eligible = Item("wood", sourceAmount, maxStack);
                var favorite = Item("wood", 1, maxStack);
                var equipped = Item("tool", 1, 1);
                var queued = Item("queued", 1, 1);
                StorageTransfer.ToggleFavorite(favorite);
                equipped.m_equipped = true;
                source.Put(eligible, 3, 1);
                source.Put(favorite, 0, 1);
                source.Put(equipped, 2, 0);
                source.Put(queued, 1, 0);
                var first = new Inventory(1, 1);
                if (usedCapacity > 0)
                {
                    first.Put(Item("wood", usedCapacity, maxStack), 0, 0);
                }
                var second = new Inventory(1, 1);
                var inventories = new[] { source, first, second };
                int before = inventories.Sum(inventory => Units(inventory, "wood"));
                int moved = 0;
                foreach (bool matching in new[] { true, false })
                {
                    foreach (var chest in new[] { first, second })
                    {
                        moved += StorageTransfer.Deposit(source, chest, matching,
                            item => ReferenceEquals(item, queued));
                    }
                }

                int expected = Math.Min(sourceAmount, 2 * maxStack - usedCapacity);
                Equal(expected, moved);
                Equal(sourceAmount - moved + 1, Units(source, "wood"));
                Equal(before, inventories.Sum(inventory => Units(inventory, "wood")));
                Same(favorite, source.GetItemAt(0, 1));
                Same(equipped, source.GetItemAt(2, 0));
                Same(queued, source.GetItemAt(1, 0));
                if (sourceAmount > moved)
                {
                    Same(eligible, source.GetItemAt(3, 1));
                }
                foreach (var inventory in inventories)
                {
                    ValidGrid(inventory, allowOversizedStack: ReferenceEquals(inventory, source));
                }
            }
        }
    }
}

static ItemDrop.ItemData Item(string name, int amount, int maxStack = 50)
{
    return new ItemDrop.ItemData
    {
        m_shared = new ItemDrop.ItemData.SharedData { m_name = name, m_maxStackSize = maxStack },
        m_stack = amount,
    };
}

static int Units(Inventory inventory, string name)
{
    return inventory.GetAllItems().Where(item => item.m_shared.m_name == name).Sum(item => item.m_stack);
}

static void ValidGrid(Inventory inventory, bool allowOversizedStack = false)
{
    var cells = new HashSet<(int, int)>();
    foreach (var item in inventory.GetAllItems())
    {
        True(item.m_gridPos.x >= 0 && item.m_gridPos.x < inventory.GetWidth());
        True(item.m_gridPos.y >= 0 && item.m_gridPos.y < inventory.GetHeight());
        True(cells.Add((item.m_gridPos.x, item.m_gridPos.y)));
        True(item.m_stack > 0);
        True(allowOversizedStack || item.m_stack <= item.m_shared.m_maxStackSize);
    }
}

static void Equal<T>(T expected, T actual)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"Expected {expected}, got {actual}.");
    }
}

static void Same(object expected, object actual)
{
    True(ReferenceEquals(expected, actual));
}

static void True(bool value)
{
    if (!value)
    {
        throw new InvalidOperationException("Expected true, got false.");
    }
}

static void False(bool value)
{
    if (value)
    {
        throw new InvalidOperationException("Expected false, got true.");
    }
}
