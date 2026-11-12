using NearbyStore;

var tests = new (string Name, Action Run)[]
{
    ("favorite toggling preserves other custom data", FavoriteMarker),
    ("favorite equipped and caller-protected stacks stay put", ProtectedStacks),
    ("transfer eligibility follows protection changes and deposited remainders", TransferableItems),
    ("stacking skips types absent from the destination", MatchingOnly),
    ("full matching stacks allow another stack with source metadata", MatchingFullStack),
    ("partial capacity counts units and preserves source grid", PartialCapacity),
    ("empty and full inventories do not move anything", NoCapacity),
    ("merging preserves destination favorites and carried remainder metadata", MergeFavoriteDestination),
    ("quality and world-level mismatches use separate stacks", CompatibilityBoundaries),
    ("multiple source stacks are not skipped after removal", MultipleSourceStacks),
    ("multiple chests accept matching overflow but reject absent types", MultipleChests),
    ("self-deposit is a no-op", SelfDeposit),
    ("stacking conserves quantities across capacity boundaries", CapacityMatrix),
    ("guaranteed no-capacity skips all native change notifications", ImpossibleCapacity),
    ("snapshot steps recheck membership and live protection", LiveSnapshotGuards),
    ("single-item steps do not process other source stacks", SingleItemStep),
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

// Full-inventory exercises belong to the harness; production handles one snapshot item.
static int DepositAll(Inventory source, Inventory destination,
    Func<ItemDrop.ItemData, bool> protectedItem = null)
{
    int moved = 0;
    foreach (var item in source.GetAllItems().ToArray())
        moved += StorageTransfer.Deposit(source, destination, item, protectedItem);
    return moved;
}

static void ImpossibleCapacity()
{
    foreach (string reason in new[] { "full", "quality", "world", "nonstackable" })
    {
        var source = new Inventory(1, 1);
        var incoming = Item("wood", reason == "nonstackable" ? 1 : 6,
            reason == "nonstackable" ? 1 : 10);
        incoming.m_customData["source"] = "unchanged";
        var destination = new Inventory(1, 1);
        var existing = Item("wood", reason == "full" ? 10 : 2, 10);
        if (reason == "quality") incoming.m_quality = 2;
        if (reason == "world") incoming.m_worldLevel = 1;
        source.Put(incoming, 0, 0);
        destination.Put(existing, 0, 0);
        int before = incoming.m_stack;
        Equal(0, StorageTransfer.Deposit(source, destination, incoming));
        Equal(0, source.ChangeCount);
        Equal(0, destination.ChangeCount);
        Same(incoming, source.GetItemAt(0, 0));
        Same(existing, destination.GetItemAt(0, 0));
        Equal(before, incoming.m_stack);
        Equal(reason == "full" ? 10 : 2, existing.m_stack);
        Equal("unchanged", incoming.m_customData["source"]);
    }
}

static void LiveSnapshotGuards()
{
    foreach (string change in new[] { "removed", "favorite", "equipped", "protected" })
    {
        var source = new Inventory(1, 1);
        var incoming = Item("wood", 6, 10);
        incoming.m_customData["source"] = "retained";
        source.Put(incoming, 0, 0);
        var snapshot = source.GetAllItems().ToArray();
        var destination = new Inventory(1, 1);
        destination.Put(Item("wood", 2, 10), 0, 0);
        bool protectedNow = false;
        Func<ItemDrop.ItemData, bool> protect = item => protectedNow;
        True(StorageTransfer.HasTransferableItems(source, protect));
        switch (change)
        {
            case "removed": source.GetAllItems().Remove(incoming); break;
            case "favorite": StorageTransfer.ToggleFavorite(incoming); break;
            case "equipped": incoming.m_equipped = true; break;
            case "protected": protectedNow = true; break;
        }
        Equal(0, StorageTransfer.Deposit(source, destination, snapshot[0], protect));
        Equal(0, source.ChangeCount);
        Equal(0, destination.ChangeCount);
        Equal(6, incoming.m_stack);
        Equal(2, Units(destination, "wood"));
        Equal("retained", incoming.m_customData["source"]);
        if (change == "removed") source.Put(incoming, 0, 0);
        if (change == "favorite") StorageTransfer.ToggleFavorite(incoming);
        incoming.m_equipped = false;
        protectedNow = false;
        Equal(6, StorageTransfer.Deposit(source, destination, snapshot[0], protect));
        Equal(0, source.GetAllItems().Count);
        Equal(8, Units(destination, "wood"));
    }
}

static void SingleItemStep()
{
    var source = new Inventory(2, 1);
    var first = Item("wood", 3, 10);
    var second = Item("wood", 4, 10);
    source.Put(first, 0, 0);
    source.Put(second, 1, 0);
    var destination = new Inventory(1, 1);
    destination.Put(Item("wood", 1, 10), 0, 0);
    Equal(3, StorageTransfer.Deposit(source, destination, first));
    Same(second, source.GetItemAt(1, 0));
    Equal(4, second.m_stack);
    Equal(4, Units(destination, "wood"));
    Equal(0, StorageTransfer.Deposit(source, destination, first));
    Equal(4, StorageTransfer.Deposit(source, destination, second));
    Equal(8, Units(destination, "wood"));
}

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
    destination.Put(Item("wood", 1), 0, 0);
    destination.Put(Item("sword", 1, 10), 1, 0);
    destination.Put(Item("shield", 1, 10), 2, 0);
    destination.Put(Item("stone", 1), 3, 0);

    Equal(9, DepositAll(source, destination, item => ReferenceEquals(item, queued)));
    Equal(3, source.GetAllItems().Count);
    Same(favorite, source.GetItemAt(3, 0));
    Same(equipped, source.GetItemAt(1, 1));
    Same(queued, source.GetItemAt(2, 0));
    Equal(8, favorite.m_stack);
    Equal(1, equipped.m_stack);
    Equal(1, queued.m_stack);
    Equal(10, Units(destination, "stone"));
    ValidGrid(source);
    ValidGrid(destination);
}

static void TransferableItems()
{
    var source = new Inventory(4, 1);
    var favorite = Item("wood", 8);
    var equipped = Item("sword", 1, 1);
    var guarded = Item("shield", 1, 1);
    StorageTransfer.ToggleFavorite(favorite);
    equipped.m_equipped = true;
    Func<ItemDrop.ItemData, bool> protect = item => ReferenceEquals(item, guarded);
    False(StorageTransfer.HasTransferableItems(source, protect));
    source.Put(favorite, 0, 0);
    source.Put(equipped, 1, 0);
    source.Put(guarded, 2, 0);
    False(StorageTransfer.HasTransferableItems(source, protect));
    True(StorageTransfer.HasTransferableItems(source));
    StorageTransfer.ToggleFavorite(favorite);
    True(StorageTransfer.HasTransferableItems(source, protect));
    var chest = new Inventory(1, 1);
    chest.Put(Item("wood", 6, 10), 0, 0);
    Equal(4, DepositAll(source, chest, protect));
    True(StorageTransfer.HasTransferableItems(source, protect));
    Same(favorite, source.GetItemAt(0, 0));
    var next = new Inventory(1, 1);
    next.Put(Item("wood", 1, 10), 0, 0);
    Equal(4, DepositAll(source, next, protect));
    False(StorageTransfer.HasTransferableItems(source, protect));
    equipped.m_equipped = false;
    True(StorageTransfer.HasTransferableItems(source, protect));
    equipped.m_equipped = true;
    False(StorageTransfer.HasTransferableItems(source, protect));
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
    Equal(4, DepositAll(source, destination));
    Equal(7, Units(destination, "wood"));
    Equal(0, Units(destination, "stone"));
    Same(stone, source.GetItemAt(0, 0));
    Equal(6, stone.m_stack);
    Equal(0, DepositAll(source, destination));
    Equal(1, source.GetAllItems().Count);
}

static void MatchingFullStack()
{
    var source = new Inventory(2, 1);
    var wood = Item("wood", 7, 10);
    wood.m_customData["another.mod"] = "preserved";
    source.Put(wood, 1, 0);
    var destination = new Inventory(2, 1);
    destination.Put(Item("wood", 10, 10), 0, 0);
    Equal(7, DepositAll(source, destination));
    Equal(17, Units(destination, "wood"));
    Same(wood, destination.GetItemAt(1, 0));
    Equal("preserved", wood.m_customData["another.mod"]);
    True(source.GetItemAt(1, 0) == null);
    Equal(0, DepositAll(source, destination));
}

static void PartialCapacity()
{
    var source = new Inventory(4, 3);
    var wood = Item("wood", 8, 10);
    source.Put(wood, 3, 2);
    var destination = new Inventory(1, 1);
    destination.Put(Item("wood", 7, 10), 0, 0);
    Equal(3, DepositAll(source, destination));
    Equal(5, wood.m_stack);
    Same(wood, source.GetItemAt(3, 2));
    Equal(3, wood.m_gridPos.x);
    Equal(2, wood.m_gridPos.y);
    Equal(10, Units(destination, "wood"));
    Equal(15, Units(source, "wood") + Units(destination, "wood"));
    True(source.ChangeCount > 0);
    True(destination.ChangeCount > 0);
    Equal(0, DepositAll(source, destination));
    ValidGrid(source);
    ValidGrid(destination);
}

static void NoCapacity()
{
    var source = new Inventory(1, 1);
    var destination = new Inventory(1, 1);
    Equal(0, DepositAll(source, destination));
    source.Put(Item("wood", 9), 0, 0);
    Equal(0, DepositAll(source, destination));
    destination.Put(Item("stone", 10, 10), 0, 0);
    Equal(0, DepositAll(source, destination));
    Equal(9, Units(source, "wood"));
    Equal(0, source.ChangeCount);
    Equal(0, destination.ChangeCount);
}

static void MergeFavoriteDestination()
{
    var source = new Inventory(1, 1);
    var item = Item("wood", 7, 10);
    item.m_customData["source-only"] = "carried remainder";
    source.Put(item, 0, 0);
    var destination = new Inventory(1, 1);
    var favorite = Item("wood", 8, 10);
    favorite.m_customData["other.mod"] = "keep";
    StorageTransfer.ToggleFavorite(favorite);
    destination.Put(favorite, 0, 0);

    Equal(2, DepositAll(source, destination));
    Same(favorite, destination.GetItemAt(0, 0));
    Equal(10, favorite.m_stack);
    True(StorageTransfer.IsFavorite(favorite));
    Equal("keep", favorite.m_customData["other.mod"]);
    False(favorite.m_customData.ContainsKey("source-only"));
    Same(item, source.GetItemAt(0, 0));
    Equal(5, item.m_stack);
    Equal("carried remainder", item.m_customData["source-only"]);
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
        Equal(6, DepositAll(source, destination));
        Equal(4, existing.m_stack);
        Same(incoming, destination.GetItemAt(1, 0));
        True(source.GetItemAt(0, 0) == null);
        Equal(6, incoming.m_stack);
    }

    // Native insertion ignores visual variants, but quality must match even for maxQuality == 1.
    var compatibleSource = new Inventory(1, 1);
    var compatible = Item("wood", 3, 10);
    compatible.m_variant = 9;
    compatibleSource.Put(compatible, 0, 0);
    var compatibleDestination = new Inventory(1, 1);
    compatibleDestination.Put(Item("wood", 2, 10), 0, 0);
    Equal(3, DepositAll(compatibleSource, compatibleDestination));
    Equal(5, Units(compatibleDestination, "wood"));

    var blockedSource = new Inventory(1, 1);
    var blocked = Item("wood", 3, 10);
    blocked.m_worldLevel = 1;
    blockedSource.Put(blocked, 0, 0);
    Equal(0, DepositAll(blockedSource, compatibleDestination));
    Same(blocked, blockedSource.GetItemAt(0, 0));

    blocked.m_worldLevel = 0;
    blocked.m_quality = 8;
    Equal(0, DepositAll(blockedSource, compatibleDestination));
    Same(blocked, blockedSource.GetItemAt(0, 0));
}

static void MultipleSourceStacks()
{
    var source = new Inventory(3, 1);
    source.Put(Item("wood", 3, 10), 2, 0);
    source.Put(Item("wood", 4, 10), 0, 0);
    source.Put(Item("stone", 5, 10), 1, 0);
    var destination = new Inventory(3, 1);
    destination.Put(Item("wood", 1, 10), 0, 0);
    destination.Put(Item("stone", 1, 10), 1, 0);
    Equal(12, DepositAll(source, destination));
    Equal(0, source.GetAllItems().Count);
    Equal(8, Units(destination, "wood"));
    Equal(6, Units(destination, "stone"));
    ValidGrid(destination);
}

static void MultipleChests()
{
    var source = new Inventory(3, 1);
    source.Put(Item("wood", 8, 10), 0, 0);
    source.Put(Item("stone", 4, 10), 1, 0);
    source.Put(Item("resin", 2, 10), 2, 0);
    var spare = new Inventory(1, 1);
    var woodChest = new Inventory(3, 1);
    var stoneChest = new Inventory(1, 1);
    woodChest.Put(Item("wood", 7, 10), 0, 0);
    stoneChest.Put(Item("stone", 8, 10), 0, 0);
    woodChest.Put(Item("wood", 9, 10), 2, 0);
    var chests = new[] { spare, woodChest, stoneChest };

    int moved = 0;
    foreach (var chest in chests)
    {
        moved += DepositAll(source, chest);
    }
    Equal(10, moved);
    Equal(0, spare.GetAllItems().Count);
    Equal(0, Units(source, "wood"));
    Equal(2, Units(source, "stone"));
    Equal(2, Units(source, "resin"));
    foreach (var chest in chests)
    {
        moved += DepositAll(source, chest);
    }
    Equal(10, moved);
    Equal(0, spare.GetAllItems().Count);
    Equal(4, woodChest.GetItemAt(1, 0).m_stack);
    Equal(24, Units(woodChest, "wood"));
    Equal(10, Units(stoneChest, "stone"));
    Equal(4, source.GetAllItems().Sum(item => item.m_stack));
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
    Equal(0, DepositAll(inventory, inventory));
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
                int remaining = sourceAmount;
                for (int cell = 0; remaining > 0; cell++)
                {
                    int amount = Math.Min(remaining, maxStack);
                    source.Put(Item("wood", amount, maxStack), cell % 4, cell / 4);
                    remaining -= amount;
                }
                var eligible = source.GetAllItems().ToArray();
                var positions = eligible.Select(item => item.m_gridPos).ToArray();
                var favorite = Item("wood", 1, maxStack);
                var equipped = Item("tool", 1, 1);
                var queued = Item("queued", 1, 1);
                StorageTransfer.ToggleFavorite(favorite);
                equipped.m_equipped = true;
                source.Put(favorite, 3, 1);
                source.Put(equipped, 2, 1);
                source.Put(queued, 1, 1);
                var first = new Inventory(2, 1);
                if (usedCapacity > 0)
                {
                    first.Put(Item("wood", usedCapacity, maxStack), 0, 0);
                }
                var second = new Inventory(1, 1);
                var inventories = new[] { source, first, second };
                int before = inventories.Sum(inventory => Units(inventory, "wood"));
                int moved = 0;
                foreach (var chest in new[] { first, second })
                {
                    moved += DepositAll(source, chest,
                        item => ReferenceEquals(item, queued));
                }

                int expected = Math.Min(sourceAmount, usedCapacity > 0 ? 2 * maxStack - usedCapacity : 0);
                Equal(expected, moved);
                Equal(sourceAmount - moved + 1, Units(source, "wood"));
                Equal(before, inventories.Sum(inventory => Units(inventory, "wood")));
                Same(favorite, source.GetItemAt(3, 1));
                Same(equipped, source.GetItemAt(2, 1));
                Same(queued, source.GetItemAt(1, 1));
                for (int index = 0; index < eligible.Length; index++)
                {
                    if (source.GetAllItems().Contains(eligible[index]))
                        Same(eligible[index], source.GetItemAt(positions[index].x, positions[index].y));
                }
                foreach (var inventory in inventories)
                {
                    ValidGrid(inventory);
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

static void ValidGrid(Inventory inventory)
{
    var cells = new HashSet<(int, int)>();
    foreach (var item in inventory.GetAllItems())
    {
        True(item.m_gridPos.x >= 0 && item.m_gridPos.x < inventory.GetWidth());
        True(item.m_gridPos.y >= 0 && item.m_gridPos.y < inventory.GetHeight());
        True(cells.Add((item.m_gridPos.x, item.m_gridPos.y)));
        True(item.m_stack > 0);
        True(item.m_stack <= item.m_shared.m_maxStackSize);
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
