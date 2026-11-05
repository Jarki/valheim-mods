using MobThreat;

static void Check(float health, float damage, int expected)
{
    int actual = ThreatMath.HitsToDie(health, damage);
    if (actual != expected)
        throw new Exception($"HP={health}, damage={damage}: expected {expected}, got {actual}");
}

Check(100, 25, 4);
Check(100.01f, 25, 5);
Check(99.99f, 25, 4);
Check(10, 50, 1);
Check(1, .125f, 8);
Check(50, 25, 2); // Current health, not maximum health.
Check(100, 0, 0);
Check(100, -1, 0);
Check(0, 25, 0);
Check(float.NaN, 25, 0);
Check(100, float.PositiveInfinity, 0);
Check(100, float.NaN, 0);
Check(float.PositiveInfinity, 25, 0);
Check(float.MaxValue, float.Epsilon, int.MaxValue);
Console.WriteLine("PASS: lethal boundaries, fractional damage, current health, unknown/invalid damage, overflow.");
