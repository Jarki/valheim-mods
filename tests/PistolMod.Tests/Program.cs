using PistolMod;

var tests = new (string Name, Action Run)[]
{
    ("save roundtrip and write boundaries", SaveRoundtripAndWriteBoundaries),
    ("empty magazine and seventeen shots", EmptyMagazineAndSeventeenShots),
    ("malformed and extreme persisted data", MalformedAndExtremePersistedData),
    ("reload arithmetic", ReloadArithmetic),
    ("independent dictionary magazines", IndependentDictionaryMagazines),
    ("rifle and launcher capacities and persistence", ExpandedMagazines),
    ("blast radius and falloff boundaries", BlastFalloff),
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

static void SaveRoundtripAndWriteBoundaries()
{
    var data = new Dictionary<string, string>();

    Magazine.Write(data, 9);
    Equal(9, Magazine.Read(data));
    Equal("9", data[Magazine.DataKey]);

    Magazine.Write(data, -1);
    Equal(0, Magazine.Read(data));
    Equal("0", data[Magazine.DataKey]);

    Magazine.Write(data, 18);
    Equal(17, Magazine.Read(data));
    Equal("17", data[Magazine.DataKey]);
}

static void EmptyMagazineAndSeventeenShots()
{
    var data = new Dictionary<string, string>();
    False(Magazine.TryFire(data));

    Magazine.Write(data, 17);
    for (int expected = 16; expected >= 0; expected--)
    {
        True(Magazine.TryFire(data));
        Equal(expected, Magazine.Read(data));
    }

    False(Magazine.TryFire(data));
    Equal(0, Magazine.Read(data));
}

static void MalformedAndExtremePersistedData()
{
    var data = new Dictionary<string, string>();
    Equal(0, Magazine.Read(data));

    data[Magazine.DataKey] = "not-a-number";
    Equal(0, Magazine.Read(data));

    data[Magazine.DataKey] = int.MinValue.ToString(System.Globalization.CultureInfo.InvariantCulture);
    Equal(0, Magazine.Read(data));

    data[Magazine.DataKey] = int.MaxValue.ToString(System.Globalization.CultureInfo.InvariantCulture);
    Equal(17, Magazine.Read(data));

    data[Magazine.DataKey] = "999999999999999999999999";
    Equal(0, Magazine.Read(data));
}

static void ReloadArithmetic()
{
    Equal(5, Magazine.ReloadAmount(12, 9));
    Equal(3, Magazine.ReloadAmount(0, 3));
    Equal(17, Magazine.ReloadAmount(0, 30));
    Equal(0, Magazine.ReloadAmount(17, 30));
    Equal(0, Magazine.ReloadAmount(6, 0));
    Equal(0, Magazine.ReloadAmount(6, -4));
}

static void IndependentDictionaryMagazines()
{
    var first = new Dictionary<string, string>();
    var second = new Dictionary<string, string>();
    Magazine.Write(first, 2);
    Magazine.Write(second, 7);

    True(Magazine.TryFire(first));
    Equal(1, Magazine.Read(first));
    Equal(7, Magazine.Read(second));
}

static void ExpandedMagazines()
{
    foreach (int capacity in new[] { 1, 17, 30 })
    {
        var data = new Dictionary<string, string>();
        Magazine.Write(data, int.MaxValue, capacity);
        Equal(capacity, Magazine.Read(data, capacity));
        var saved = new Dictionary<string, string>(data);
        for (int remaining = capacity - 1; remaining >= 0; remaining--)
        {
            True(Magazine.TryFire(saved, capacity));
            Equal(remaining, Magazine.Read(saved, capacity));
        }
        False(Magazine.TryFire(saved, capacity));
        Equal(capacity, Magazine.Read(data, capacity));
        Equal(capacity, Magazine.ReloadAmount(0, int.MaxValue, capacity));
        Equal(1, Magazine.ReloadAmount(capacity - 1, 80, capacity));
        Equal(0, Magazine.ReloadAmount(capacity, 80, capacity));
        Equal(0, Magazine.ReloadAmount(0, 0, capacity));
    }
    // The original pistol save key and default capacity remain backward compatible.
    var oldPistol = new Dictionary<string, string> { ["PistolMod.Rounds"] = "12" };
    Equal(12, Magazine.Read(oldPistol));
}

static void BlastFalloff()
{
    Equal(1f, BlastRules.Falloff(0));
    Equal(1f, BlastRules.Falloff(-1));
    Equal(.5f, BlastRules.Falloff(2.5f));
    True(BlastRules.Falloff(4.99f) > 0);
    Equal(0f, BlastRules.Falloff(5));
    Equal(0f, BlastRules.Falloff(6));
    Equal(0f, BlastRules.Falloff(float.NaN));
    Equal(0f, BlastRules.Falloff(float.PositiveInfinity));
}

static void Equal<T>(T expected, T actual)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"Expected {expected}, got {actual}.");
    }
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
