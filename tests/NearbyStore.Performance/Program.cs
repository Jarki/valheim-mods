using System.Diagnostics;
using System.Globalization;
using NearbyStore;

// Real production transfer core, handwritten inventory doubles. No Plugin scheduler,
// Unity, ownership, serialization or transport runs here. Timings are diagnostic only.
CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
const int warmups = 3;
const int samples = 40;
var cases = new List<Workload>();
foreach (int chests in new[] { 1, 10, 50, 100 })
    foreach (string content in new[] { "unmatched", "full", "partial", "spare", "mixed-protection", "all-protected", "empty" })
        cases.Add(new Workload(content, chests, 32));
foreach (int stacks in new[] { 8, 128 })
    foreach (string content in new[] { "unmatched", "full", "partial", "spare" })
        cases.Add(new Workload(content, 1, stacks));

Console.Error.WriteLine("TRANSFER-CORE DOUBLES ONLY: not native frame times or multiplayer latency.");
Console.Error.WriteLine($"{cases.Count} workloads; {warmups} warmups and {samples} fresh-fixture samples each. Forced sweeps, not Plugin scheduling.");
Console.WriteLine("case,chests,source_stacks,chest_slots,samples,moved_units,double_change_notifications,median_ms,p95_ms,max_ms,median_alloc_bytes");
int failures = 0;
foreach (var workload in cases)
{
    try
    {
        var timings = new double[samples];
        var allocations = new long[samples];
        int moved = 0, changes = 0;
        for (int sample = -warmups; sample < samples; sample++)
        {
            // Build, validate and snapshot OUTSIDE the measured region. Never reuse a
            // depleted inventory: otherwise successful cases become empty-bag benchmarks.
            var fixture = new Fixture(workload);
            var before = fixture.Totals();
            long allocated = GC.GetAllocatedBytesForCurrentThread();
            long start = Stopwatch.GetTimestamp();
            moved = fixture.Sweep();
            double elapsed = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            long bytes = GC.GetAllocatedBytesForCurrentThread() - allocated;
            fixture.Verify(before, moved);
            changes = fixture.Source.ChangeCount + fixture.Chests.Sum(chest => chest.ChangeCount);
            if (sample >= 0)
            {
                timings[sample] = elapsed;
                allocations[sample] = bytes;
            }
        }
        Array.Sort(timings);
        Array.Sort(allocations);
        int middle = samples / 2;
        Console.WriteLine($"{workload.Content},{workload.Chests},{(workload.Content == "empty" ? 0 : workload.Stacks)},{workload.Slots},{samples},{moved},{changes},{(timings[middle - 1] + timings[middle]) / 2:F6},{timings[(int)Math.Ceiling(.95 * samples) - 1]:F6},{timings[^1]:F6},{(allocations[middle - 1] + allocations[middle]) / 2}");
    }
    catch (Exception exception)
    {
        failures++;
        Console.Error.WriteLine($"FAIL {workload}: {exception}");
    }
}
Console.Error.WriteLine($"{cases.Count - failures}/{cases.Count} workload oracles passed. Timing has no machine-dependent pass threshold.");
return failures == 0 ? 0 : 1;

sealed record Workload(string Content, int Chests, int Stacks)
{
    public int Slots => Content == "spare" ? Stacks * 2 : Stacks;
}

sealed class Fixture
{
    private readonly Workload workload;
    private readonly ItemDrop.ItemData[] carried;
    private readonly Func<ItemDrop.ItemData, bool> protectedItem;
    public Inventory Source { get; }
    public Inventory[] Chests { get; }

    public Fixture(Workload workload)
    {
        this.workload = workload;
        Source = new Inventory(workload.Stacks, 1);
        carried = new ItemDrop.ItemData[workload.Content == "empty" ? 0 : workload.Stacks];
        var queued = new HashSet<ItemDrop.ItemData>();
        protectedItem = queued.Contains;
        for (int i = 0; i < carried.Length; i++)
        {
            var item = Item("type" + i, 10, i);
            item.m_customData["fixture.identity"] = "source" + i;
            if (workload.Content == "all-protected" || (workload.Content == "mixed-protection" && i % 4 == 0))
                item.m_customData[StorageTransfer.FavoriteKey] = "1";
            if (workload.Content == "mixed-protection" && i % 4 == 1) item.m_equipped = true;
            if (workload.Content == "mixed-protection" && i % 4 == 2) queued.Add(item);
            carried[i] = item;
            Source.Put(item, i, 0);
        }
        Chests = new Inventory[workload.Chests];
        for (int c = 0; c < Chests.Length; c++)
        {
            var chest = new Inventory(workload.Slots, 1);
            for (int i = 0; i < workload.Stacks; i++)
            {
                string name = (workload.Content == "unmatched" ? "other" : "type") + i;
                int amount = workload.Content == "partial" ? 49 : workload.Content == "mixed-protection" ? 1 : 50;
                chest.Put(Item(name, amount, i), i, 0);
            }
            Chests[c] = chest;
            Check(chest.GetAllItems().Count == workload.Stacks, "destination fixture stack count");
            Check(chest.ChangeCount == 0, "fixture insertion must not simulate notifications");
            ValidateGrid(chest);
        }
        Check(Source.GetAllItems().Count == carried.Length, "source fixture stack count");
        Check(Source.GetAllItems().Sum(item => item.m_stack) == carried.Length * 10, "source fixture units");
        ValidateGrid(Source);
    }

    // This deliberately isolates a fixed amount of transfer-core work. It does NOT
    // copy Begin/Advance or pretend to test their discovery/early-stop/ownership logic.
    public int Sweep()
    {
        int moved = 0;
        foreach (var chest in Chests)
            foreach (var item in carried)
                moved += StorageTransfer.Deposit(Source, chest, item, protectedItem);
        return moved;
    }

    public void Verify(Dictionary<(string, int, byte), int> before, int moved)
    {
        var after = Totals();
        Check(before.Count == after.Count && before.All(pair => after.TryGetValue(pair.Key, out int amount) && pair.Value == amount),
            "per-name/quality/world-level conservation");
        int expectedMoved = 0;
        for (int i = 0; i < carried.Length; i++)
        {
            int remaining = workload.Content switch
            {
                "partial" => 10 - Math.Min(workload.Chests, 10),
                "spare" => 0,
                "mixed-protection" when i % 4 == 3 => 0,
                _ => 10
            };
            expectedMoved += 10 - remaining;
            var item = carried[i];
            Check(item.m_customData["fixture.identity"] == "source" + i, "source metadata retained");
            if (remaining == 0)
                Check(!Source.GetAllItems().Contains(item), "fully moved stack removed from source");
            else
            {
                Check(ReferenceEquals(Source.GetItemAt(i, 0), item), "protected/remainder stack stays in original slot");
                Check(item.m_stack == remaining, "exact remainder/protected quantity");
            }
            if (workload.Content == "all-protected" || (workload.Content == "mixed-protection" && i % 4 == 0))
                Check(item.m_customData.ContainsKey(StorageTransfer.FavoriteKey), "favorite marker retained");
        }
        Check(moved == expectedMoved, $"moved {moved}, expected {expectedMoved}");
        Check(Source.GetAllItems().Sum(item => item.m_stack) == carried.Length * 10 - expectedMoved, "exact source total");
        ValidateGrid(Source);
        foreach (var chest in Chests) ValidateGrid(chest);
    }

    public Dictionary<(string, int, byte), int> Totals()
    {
        var totals = new Dictionary<(string, int, byte), int>();
        Add(Source);
        foreach (var chest in Chests) Add(chest);
        return totals;

        void Add(Inventory inventory)
        {
            foreach (var item in inventory.GetAllItems())
            {
                var key = (item.m_shared.m_name, item.m_quality, item.m_worldLevel);
                totals.TryGetValue(key, out int amount);
                totals[key] = amount + item.m_stack;
            }
        }
    }

    private static ItemDrop.ItemData Item(string name, int amount, int index) => new()
    {
        m_shared = new ItemDrop.ItemData.SharedData { m_name = name, m_maxStackSize = 50 },
        m_stack = amount,
        m_quality = index % 3 + 1,
        m_worldLevel = (byte)(index % 2)
    };

    private static void ValidateGrid(Inventory inventory)
    {
        var cells = new HashSet<(int, int)>();
        foreach (var item in inventory.GetAllItems())
        {
            Check(item.m_stack > 0 && item.m_stack <= item.m_shared.m_maxStackSize, "valid stack quantity");
            Check(item.m_gridPos.x >= 0 && item.m_gridPos.x < inventory.GetWidth() && item.m_gridPos.y >= 0 && item.m_gridPos.y < inventory.GetHeight(), "valid cell");
            Check(cells.Add((item.m_gridPos.x, item.m_gridPos.y)), "no overlapping stacks");
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
