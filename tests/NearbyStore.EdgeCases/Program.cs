using NearbyStore;

var cases = new (string Name, Action Run)[]
{
    ("favorite changes between multi-chest deposits take effect immediately", FavoriteSequence),
    ("mixed compatibility and metadata survive constrained multi-chest passes", MixedCompatibilitySequence),
    ("320 seeded valid inventory sequences conserve units and source positions", GeneratedSequences),
};

int failures = 0;
foreach (var test in cases)
{
    try
    {
        test.Run();
        Console.WriteLine($"PASS: {test.Name}");
    }
    catch (Exception exception)
    {
        failures++;
        Console.Error.WriteLine($"FAIL: {test.Name}: {exception}");
    }
}
Console.WriteLine($"{cases.Length - failures}/{cases.Length} edge-case groups passed.");
return failures == 0 ? 0 : 1;

static void FavoriteSequence()
{
    var source = new Inventory(4, 2);
    var wood = Item("wood", 10, 10, "carried wood");
    var stone = Item("stone", 6, 10, "carried stone");
    source.Put(wood, 3, 1);
    source.Put(stone, 1, 0);
    StorageTransfer.ToggleFavorite(wood);
    StorageTransfer.ToggleFavorite(stone);
    var first = new Inventory(1, 1);
    first.Put(Item("wood", 8, 10, "first chest"), 0, 0);
    var second = new Inventory(2, 1);
    second.Put(Item("wood", 9, 10, "second chest wood"), 0, 0);
    second.Put(Item("stone", 9, 10, "second chest stone"), 1, 0);
    var spare = new Inventory(1, 1);
    var chests = new[] { first, second, spare };
    var protectedItems = new HashSet<ItemDrop.ItemData>();

    Equal(0, Pass(source, chests, protectedItems), "initial favorites must stay carried");
    StorageTransfer.ToggleFavorite(wood);
    Equal(3, Pass(source, chests, protectedItems, matchingOnly: true), "only wood merge space is available");
    Equal(7, wood.m_stack, "partially deposited wood");
    Same(wood, source.GetItemAt(3, 1), "partial wood keeps its original cell");
    StorageTransfer.ToggleFavorite(wood);
    StorageTransfer.ToggleFavorite(stone);
    Equal(1, Pass(source, chests, protectedItems, matchingOnly: true), "newly unfavorited stone can merge");
    Equal(5, Pass(source, chests, protectedItems), "stone uses spare chest while wood is reprotected");
    Same(wood, source.GetItemAt(3, 1), "refavorited wood cannot move to the spare chest");
    Equal(7, wood.m_stack, "refavoriting preserves its remaining quantity");
    Equal(5, spare.GetItemAt(0, 0).m_stack, "spare contains only eligible stone");
    Equal("stone", spare.GetItemAt(0, 0).m_shared.m_name, "spare item type");
    StorageTransfer.ToggleFavorite(wood);
    Equal(0, Pass(source, chests, protectedItems), "unfavoriting does not bypass incompatible occupied cells");
    var discovered = new Inventory(1, 1);
    var expanded = chests.Append(discovered).ToArray();
    Equal(7, Pass(source, expanded, protectedItems), "unfavorited remainder reaches newly available chest");
    Equal(0, source.GetAllItems().Count, "both stacks eventually leave the source");
    Equal("carried wood", discovered.GetItemAt(0, 0).m_customData["payload"], "metadata follows moved remainder");
    True(!StorageTransfer.IsFavorite(discovered.GetItemAt(0, 0)), "unfavorited marker must not reappear");
    AssertIdempotent(source, expanded, protectedItems);
}

static void MixedCompatibilitySequence()
{
    var source = new Inventory(4, 2);
    var qualityTwo = Item("arrow", 7, 10, "quality-two payload", quality: 2, maxQuality: 4, variant: 3);
    var otherWorld = Item("arrow", 8, 10, "world-one payload", world: 1, maxQuality: 4, variant: 2);
    var wood = Item("wood", 5, 10, "wood payload", quality: 8, variant: 9);
    source.Put(qualityTwo, 3, 1);
    source.Put(otherWorld, 1, 0);
    source.Put(wood, 0, 1);
    var first = new Inventory(3, 1);
    var wrongQuality = Item("arrow", 4, 10, "wrong quality", maxQuality: 4);
    var favorite = Item("arrow", 8, 10, "destination favorite", quality: 2, maxQuality: 4);
    StorageTransfer.ToggleFavorite(favorite);
    first.Put(wrongQuality, 0, 0);
    first.Put(favorite, 1, 0);
    first.Put(Item("wood", 10, 10, "full wood"), 2, 0);
    var second = new Inventory(2, 1);
    var wrongWorldQuality = Item("arrow", 6, 10, "world-one wrong quality", quality: 2, world: 1, maxQuality: 4);
    second.Put(wrongWorldQuality, 0, 0);
    second.Put(Item("arrow", 9, 10, "world-one target", world: 1, maxQuality: 4), 1, 0);
    var third = new Inventory(2, 1);
    var storedWood = Item("wood", 8, 10, "destination wood");
    third.Put(storedWood, 0, 0);
    var chests = new[] { first, second, third };
    var protectedItems = new HashSet<ItemDrop.ItemData>();

    Equal(8, Pass(source, chests, protectedItems, matchingOnly: true), "matching merges plus new wood stack");
    Equal(5, qualityTwo.m_stack, "quality-two remainder");
    Equal(7, otherWorld.m_stack, "world-one remainder");
    Equal(4, wrongQuality.m_stack, "wrong quality cannot absorb arrow units");
    Equal(6, wrongWorldQuality.m_stack, "same world but wrong quality cannot absorb arrow units");
    Equal(10, favorite.m_stack, "compatible favorite destination accepts a merge");
    True(StorageTransfer.IsFavorite(favorite), "destination favorite survives merging");
    Equal("destination favorite", favorite.m_customData["payload"], "merged stack keeps destination metadata");
    Equal("destination wood", storedWood.m_customData["payload"], "wood merge keeps destination payload");
    var newWood = third.GetItemAt(1, 0);
    Equal(3, newWood.m_stack, "only overflow occupies the last free cell");
    Equal("wood payload", newWood.m_customData["payload"], "overflow clones incoming metadata");
    Equal(8, newWood.m_quality, "new stack retains ignored-for-merging quality");
    Equal(9, newWood.m_variant, "new stack retains visual variant");
    Equal(0, Pass(source, chests, protectedItems), "fallback cannot merge incompatible remainders");
    AssertIdempotent(source, chests, protectedItems);

    var discovered = new Inventory(2, 1);
    var expanded = chests.Append(discovered).ToArray();
    Equal(12, Pass(source, expanded, protectedItems), "both incompatible remainder classes can use new cells");
    Equal(0, source.GetAllItems().Count, "all source quantities transferred");
    var depositedQualityTwo = discovered.GetAllItems().Single(item => item.m_worldLevel == 0);
    var depositedOtherWorld = discovered.GetAllItems().Single(item => item.m_worldLevel == 1);
    Equal("quality-two payload", depositedQualityTwo.m_customData["payload"], "quality-two clone payload");
    Equal(2, depositedQualityTwo.m_quality, "quality-two clone quality");
    Equal(3, depositedQualityTwo.m_variant, "quality-two clone variant");
    Equal("world-one payload", depositedOtherWorld.m_customData["payload"], "world-one clone payload");
    Equal(1, depositedOtherWorld.m_quality, "world-one clone quality");
    AssertIdempotent(source, expanded, protectedItems);
}

static void GeneratedSequences()
{
    long totalMoved = 0;
    int deposits = 0;
    for (int seed = 0; seed < 320; seed++)
    {
        try
        {
            var random = new Random(seed);
            var source = new Inventory(4, 3);
            Populate(source, random, $"seed-{seed}-source", sourceItems: true);
            var chests = Enumerable.Range(0, 4)
                .Select(index => new Inventory(random.Next(1, 5), random.Next(1, 3))).ToArray();
            for (int index = 0; index < chests.Length; index++)
            {
                Populate(chests[index], random, $"seed-{seed}-chest-{index}", sourceItems: false);
            }
            var world = new[] { source }.Concat(chests).ToArray();
            var initialTotals = Totals(world);
            var initialPositions = source.GetAllItems()
                .ToDictionary(item => item, item => (item.m_gridPos.x, item.m_gridPos.y));
            var protectedItems = new HashSet<ItemDrop.ItemData>();
            for (int round = 0; round < 8; round++)
            {
                protectedItems.Clear();
                foreach (var item in source.GetAllItems())
                {
                    if (round == 7)
                    {
                        if (StorageTransfer.IsFavorite(item)) StorageTransfer.ToggleFavorite(item);
                    }
                    else
                    {
                        if (random.Next(3) == 0) StorageTransfer.ToggleFavorite(item);
                        if (random.Next(5) == 0) protectedItems.Add(item);
                    }
                }
                // Shuffle discovery order without changing any fixture quantities or positions.
                for (int index = chests.Length - 1; index > 0; index--)
                {
                    int other = random.Next(index + 1);
                    (chests[index], chests[other]) = (chests[other], chests[index]);
                }
                foreach (bool matchingOnly in new[] { true, false })
                {
                    foreach (var chest in chests)
                    {
                        totalMoved += CheckedDeposit(source, chest, world, matchingOnly, protectedItems);
                        deposits++;
                    }
                }
                AssertIdempotent(source, chests, protectedItems);
                EqualTotals(initialTotals, Totals(world), $"seed {seed}, round {round}: global conservation");
                foreach (var item in source.GetAllItems())
                {
                    Equal(initialPositions[item], (item.m_gridPos.x, item.m_gridPos.y),
                        $"seed {seed}, round {round}: surviving source item must never shift");
                }
            }
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException($"Deterministic seed {seed} failed.", exception);
        }
    }
    True(totalMoved > 0, "generated sequences must actually move eligible quantities");
    Console.WriteLine($"  Generated: 320 seeds, 8 rounds each, {deposits} checked deposits plus fixed-point repeats; {totalMoved} moved units.");
}

static void Populate(Inventory inventory, Random random, string prefix, bool sourceItems)
{
    var cells = Enumerable.Range(0, inventory.GetWidth() * inventory.GetHeight()).ToArray();
    for (int index = cells.Length - 1; index > 0; index--)
    {
        int other = random.Next(index + 1);
        (cells[index], cells[other]) = (cells[other], cells[index]);
    }
    int count = sourceItems ? random.Next(5, cells.Length + 1) : random.Next(cells.Length + 1);
    for (int index = 0; index < count; index++)
    {
        int type = random.Next(4);
        string name = new[] { "wood", "ore", "tool", "resin" }[type];
        int maximum = new[] { 10, 7, 1, 20 }[type];
        var item = Item(name, random.Next(1, maximum + 1), maximum, $"{prefix}-{index}",
            quality: type == 1 ? random.Next(1, 5) : 1,
            world: (byte)random.Next(3), maxQuality: type == 1 ? 4 : 1, variant: random.Next(4));
        item.m_customData["another.mod"] = $"unchanged-{random.Next(100)}";
        if (random.Next(5) == 0) StorageTransfer.ToggleFavorite(item);
        item.m_equipped = sourceItems && random.Next(9) == 0;
        int cell = cells[index];
        inventory.Put(item, cell % inventory.GetWidth(), cell / inventory.GetWidth());
    }
}

static int Pass(Inventory source, Inventory[] chests, HashSet<ItemDrop.ItemData> protectedItems,
    bool? matchingOnly = null)
{
    var world = new[] { source }.Concat(chests).ToArray();
    int moved = 0;
    foreach (bool matching in matchingOnly.HasValue ? new[] { matchingOnly.Value } : new[] { true, false })
    {
        foreach (var chest in chests)
        {
            moved += CheckedDeposit(source, chest, world, matching, protectedItems);
        }
    }
    return moved;
}

static int CheckedDeposit(Inventory source, Inventory destination, Inventory[] world, bool matchingOnly,
    HashSet<ItemDrop.ItemData> protectedItems)
{
    var before = world.SelectMany(inventory => inventory.GetAllItems()).ToDictionary(item => item,
        item => new ItemSnapshot(item));
    var totals = Totals(world);
    var sourceTotals = Totals(new[] { source });
    var destinationTotals = Totals(new[] { destination });
    var sourceItems = source.GetAllItems().ToArray();
    var destinationNames = destination.GetAllItems().Select(item => item.m_shared.m_name).ToHashSet();
    int sourceBefore = sourceItems.Sum(item => item.m_stack);
    int destinationBefore = destination.GetAllItems().Sum(item => item.m_stack);
    int moved = StorageTransfer.Deposit(source, destination, matchingOnly, protectedItems.Contains);

    Equal(sourceBefore - source.GetAllItems().Sum(item => item.m_stack), moved, "returned count equals source loss");
    Equal(destination.GetAllItems().Sum(item => item.m_stack) - destinationBefore, moved, "returned count equals chest gain");
    True(moved >= 0, "moved count cannot be negative");
    EqualTotals(totals, Totals(world), "conservation by name, world, and merge-relevant quality");
    var sourceAfter = Totals(new[] { source });
    var destinationAfter = Totals(new[] { destination });
    foreach (var key in sourceTotals.Keys.Concat(destinationTotals.Keys).Distinct())
    {
        Equal(sourceTotals.GetValueOrDefault(key) - sourceAfter.GetValueOrDefault(key),
            destinationAfter.GetValueOrDefault(key) - destinationTotals.GetValueOrDefault(key),
            $"source loss equals target gain for {key}");
    }
    var remaining = source.GetAllItems();
    True(remaining.SequenceEqual(sourceItems.Where(item => remaining.Contains(item))), "source survivor order is stable");
    foreach (var item in sourceItems)
    {
        bool blocked = item.m_equipped || StorageTransfer.IsFavorite(item) || protectedItems.Contains(item) ||
            (matchingOnly && !destinationNames.Contains(item.m_shared.m_name));
        if (blocked)
        {
            Same(item, source.GetItemAt(before[item].X, before[item].Y), "protected or unmatched item stays in source cell");
            Equal(before[item].Stack, item.m_stack, "protected or unmatched stack quantity is untouched");
        }
    }
    // A consumer must not be left holding any eligible units while this chest can accept them.
    foreach (var item in remaining)
    {
        if (item.m_equipped || StorageTransfer.IsFavorite(item) || protectedItems.Contains(item) ||
            (matchingOnly && !destinationNames.Contains(item.m_shared.m_name))) continue;
        True(destination.GetAllItems().Count == destination.GetWidth() * destination.GetHeight(),
            "eligible remainder requires a completely occupied destination");
        True(!destination.GetAllItems().Any(stored => Compatible(stored, item) && stored.m_stack < stored.m_shared.m_maxStackSize),
            "eligible remainder cannot leave a compatible stack partly empty");
    }
    if (matchingOnly)
    {
        True(destination.GetAllItems().All(item => destinationNames.Contains(item.m_shared.m_name)),
            "matching-only deposit must not introduce an absent name");
    }
    var seen = new HashSet<ItemDrop.ItemData>();
    foreach (var inventory in world)
    {
        var cells = new HashSet<(int, int)>();
        foreach (var item in inventory.GetAllItems())
        {
            True(seen.Add(item), "item references must not alias between inventories");
            True(item.m_stack > 0 && item.m_stack <= item.m_shared.m_maxStackSize, "all generated stacks remain valid");
            True(item.m_gridPos.x >= 0 && item.m_gridPos.x < inventory.GetWidth() &&
                item.m_gridPos.y >= 0 && item.m_gridPos.y < inventory.GetHeight(), "grid bounds");
            True(cells.Add((item.m_gridPos.x, item.m_gridPos.y)), "grid cells must be unique");
            if (before.TryGetValue(item, out var snapshot))
            {
                Equal((snapshot.X, snapshot.Y), (item.m_gridPos.x, item.m_gridPos.y), "existing items retain positions");
                True(snapshot.SameMetadata(item), "existing items retain all metadata");
                if (!ReferenceEquals(inventory, source) && !ReferenceEquals(inventory, destination))
                    Equal(snapshot.Stack, item.m_stack, "unvisited chests are untouched");
            }
            else
            {
                True(ReferenceEquals(inventory, destination), "new items belong only to the visited chest");
                True(sourceItems.Any(original => before[original].SameMetadata(item)), "new stack inherits an incoming item's metadata");
                True(before.Keys.All(original => !ReferenceEquals(original.m_customData, item.m_customData)), "cloned metadata cannot alias any original");
            }
        }
    }
    var metadataReferences = new HashSet<Dictionary<string, string>>();
    True(world.SelectMany(inventory => inventory.GetAllItems()).All(item => metadataReferences.Add(item.m_customData)),
        "all surviving stacks have independent custom-data dictionaries");
    return moved;
}

static void AssertIdempotent(Inventory source, Inventory[] chests, HashSet<ItemDrop.ItemData> protectedItems)
{
    var world = new[] { source }.Concat(chests).ToArray();
    var state = world.Select(inventory => inventory.GetAllItems().Select(item => (Item: item, State: new ItemSnapshot(item))).ToArray()).ToArray();
    var changes = world.Select(inventory => inventory.ChangeCount).ToArray();
    for (int repeat = 0; repeat < 3; repeat++)
    {
        Equal(0, Pass(source, chests, protectedItems), "repeating a completed two-pass deposit moves no units");
        for (int index = 0; index < world.Length; index++)
        {
            var actual = world[index].GetAllItems();
            Equal(state[index].Length, actual.Count, "idempotent deposit keeps stack count");
            Equal(changes[index], world[index].ChangeCount, "idempotent deposit does not dirty inventories");
            for (int itemIndex = 0; itemIndex < actual.Count; itemIndex++)
            {
                var expected = state[index][itemIndex];
                Same(expected.Item, actual[itemIndex], "idempotent deposit retains item identity and order");
                Equal(expected.State.Stack, actual[itemIndex].m_stack, "idempotent stack quantity");
                Equal((expected.State.X, expected.State.Y), (actual[itemIndex].m_gridPos.x, actual[itemIndex].m_gridPos.y), "idempotent grid position");
                True(expected.State.SameMetadata(actual[itemIndex]), "idempotent metadata");
            }
        }
    }
}

static ItemDrop.ItemData Item(string name, int amount, int maxStack, string payload,
    int quality = 1, byte world = 0, int maxQuality = 1, int variant = 0)
{
    var item = new ItemDrop.ItemData
    {
        m_shared = new ItemDrop.ItemData.SharedData { m_name = name, m_maxStackSize = maxStack, m_maxQuality = maxQuality },
        m_stack = amount,
        m_quality = quality,
        m_worldLevel = world,
        m_variant = variant,
    };
    item.m_customData["payload"] = payload;
    return item;
}

static bool Compatible(ItemDrop.ItemData left, ItemDrop.ItemData right) =>
    left.m_shared.m_name == right.m_shared.m_name && left.m_worldLevel == right.m_worldLevel &&
    (left.m_shared.m_maxQuality <= 1 || left.m_quality == right.m_quality);

static Dictionary<(string Name, byte World, int Quality), int> Totals(IEnumerable<Inventory> inventories) =>
    inventories.SelectMany(inventory => inventory.GetAllItems())
        .GroupBy(item => (item.m_shared.m_name, item.m_worldLevel, item.m_shared.m_maxQuality > 1 ? item.m_quality : 0))
        .ToDictionary(group => group.Key, group => group.Sum(item => item.m_stack));

static void EqualTotals(Dictionary<(string Name, byte World, int Quality), int> expected,
    Dictionary<(string Name, byte World, int Quality), int> actual, string message)
{
    foreach (var key in expected.Keys.Concat(actual.Keys).Distinct())
        Equal(expected.GetValueOrDefault(key), actual.GetValueOrDefault(key), $"{message}: {key}");
}

static void Equal<T>(T expected, T actual, string message)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"{message}: expected {expected}, got {actual}.");
}

static void Same(object expected, object actual, string message) => True(ReferenceEquals(expected, actual), message);

static void True(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

sealed class ItemSnapshot
{
    public readonly int Stack;
    public readonly int X;
    public readonly int Y;
    private readonly string name;
    private readonly int maxStack;
    private readonly int maxQuality;
    private readonly int quality;
    private readonly byte world;
    private readonly int variant;
    private readonly bool equipped;
    private readonly Dictionary<string, string> metadata;

    public ItemSnapshot(ItemDrop.ItemData item)
    {
        Stack = item.m_stack;
        X = item.m_gridPos.x;
        Y = item.m_gridPos.y;
        name = item.m_shared.m_name;
        maxStack = item.m_shared.m_maxStackSize;
        maxQuality = item.m_shared.m_maxQuality;
        quality = item.m_quality;
        world = item.m_worldLevel;
        variant = item.m_variant;
        equipped = item.m_equipped;
        metadata = new Dictionary<string, string>(item.m_customData);
    }

    public bool SameMetadata(ItemDrop.ItemData item) =>
        name == item.m_shared.m_name && maxStack == item.m_shared.m_maxStackSize &&
        maxQuality == item.m_shared.m_maxQuality && quality == item.m_quality && world == item.m_worldLevel &&
        variant == item.m_variant && equipped == item.m_equipped && metadata.Count == item.m_customData.Count &&
        metadata.All(pair => item.m_customData.TryGetValue(pair.Key, out string value) && pair.Value == value);
}
