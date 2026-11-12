using Fitness;

// These assertions cover the linked production state model, not Unity, stamina
// consumption, persistence parsing, animations, or multiplayer behavior.
var tests = new (string Name, Action Run)[]
{
    ("new state and level boundaries", LevelBoundaries),
    ("Run 0 onset and recovery scenario", OnsetAndRecoveryScenario),
    ("partial strain survives short rests", ShortRestRetainsPartialLoad),
    ("skill scales growth and immunity does not clear strain", SkillRatesAndImmunity),
    ("recovery crosses levels and stops at zero", RecoveryTransitions),
    ("earned strain requires a full recovery level", HystereticRecovery),
    ("extended exercise caps strain without hidden overflow", CappedStrain),
    ("restore clamps finite values and rejects nonfinite values", RestoreValues),
    ("restore preserves bounded saved recovery levels", RestoreRecoveryLevel),
    ("idle and running split steps preserve elapsed-time behavior", SplitStepEquivalence),
    ("20ms ticks preserve onset and full recovery boundaries", FixedTimeSteps),
    ("configured onset, recovery, and immunity affect behavior", ConfiguredRates),
    ("stamina penalties are bounded by level", PenaltyLevels),
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

Console.WriteLine($"{tests.Length - failures}/{tests.Length} pure strain tests passed.");
return failures == 0 ? 0 : 1;

static void LevelBoundaries()
{
    var state = new StrainState();
    State(state, 0f, 0, "new state");

    foreach ((float load, int level) in new (float, int)[]
    {
        (0.001f, 0), (0.999f, 0), (1f, 1), (1.001f, 1),
        (1.999f, 1), (2f, 2), (2.001f, 2), (2.999f, 2),
        (3f, 3), (3.001f, 3), (3.999f, 3),
    })
    {
        state.Restore(load);
        State(state, load, level, $"restored load {load}");
    }
}

static void OnsetAndRecoveryScenario()
{
    var state = new StrainState();
    Advance(state, 89f, true);
    State(state, 89f / 90f, 0, "before first onset");

    // Independent elapsed-time checkpoints avoid deriving the oracle from Level.
    foreach ((float seconds, float load, int level) in new (float, float, int)[]
    {
        (90f, 1f, 1), (180f, 2f, 2), (270f, 3f, 3),
    })
    {
        state = new StrainState();
        Advance(state, seconds, true);
        State(state, load, level, $"Run 0 after {seconds}s");
        Console.WriteLine($"SCENARIO Run 0: {seconds:0}s running -> load {state.Load:0.###}, level {state.Level}");
    }

    foreach ((float load, int level) in new (float, int)[] { (2f, 2), (1f, 1), (0f, 0) })
    {
        Advance(state, 180f, false);
        State(state, load, level, "180s recovery step");
        Console.WriteLine($"SCENARIO recovery: +180s resting -> load {state.Load:0.###}, level {state.Level}");
    }
}

static void ShortRestRetainsPartialLoad()
{
    var state = new StrainState();
    Advance(state, 67.5f, true);
    State(state, 0.75f, 0, "partial first level");
    Advance(state, 9f, false);
    State(state, 0.70f, 0, "short rest does not reset partial effort");
    Advance(state, 36f, true);
    State(state, 1.10f, 1, "resumed exercise includes retained effort");
    Advance(state, 36f, false);
    State(state, 0.90f, 1, "strain persists while the first level heals");
    Advance(state, 18f, true);
    State(state, 1.10f, 1, "crossing the same boundary again");

    var repeated = new StrainState();
    for (int cycle = 0; cycle < 10; cycle++)
    {
        Advance(repeated, 18f, true);
        Advance(repeated, 9f, false);
    }
    State(repeated, 1.5f, 1, "repeated sub-onset exercise still accumulates strain");
}

static void SkillRatesAndImmunity()
{
    foreach ((float skill, float expectedLoad) in new (float, float)[]
    {
        (0f, 1f), (25f, 2f / 3f), (37.5f, 0.5f),
        (74f, 1f / 75f), (75f, 0f), (100f, 0f),
    })
    {
        var state = new StrainState();
        Advance(state, 90f, true, skill);
        State(state, expectedLoad, skill == 0f ? 1 : 0, $"90s at Run {skill}");
    }

    foreach (float immuneSkill in new[] { 75f, 100f })
    {
        var state = new StrainState();
        state.Restore(2.5f);
        Advance(state, 900f, true, immuneSkill);
        State(state, 2.5f, 2, $"Run {immuneSkill} sprinting pauses recovery");
        Advance(state, 90f, false, immuneSkill);
        State(state, 2f, 2, $"Run {immuneSkill} idle permits recovery");
    }
}

static void RecoveryTransitions()
{
    var state = new StrainState();
    state.Restore(3.25f);
    Advance(state, 45f, false);
    State(state, 3f, 3, "exact level 3 recovery boundary");
    Advance(state, 9f, false);
    State(state, 2.95f, 3, "severe strain persists below its onset boundary");
    Advance(state, 180f, false);
    State(state, 1.95f, 2, "a full load unit heals severe to moderate");
    Advance(state, 180f, false);
    State(state, 0.95f, 1, "mild strain persists until fully recovered");
    Advance(state, 360f, false);
    State(state, 0f, 0, "long rest clamps at zero");
    Advance(state, 90f, true);
    State(state, 1f, 1, "rest creates no negative recovery debt");
}

static void HystereticRecovery()
{
    foreach ((float initialLoad, int initialLevel, float recoveredLoad, int recoveredLevel) in new (float, int, float, int)[]
    {
        (1f, 1, 0f, 0), (2f, 2, 1f, 1), (3f, 3, 2f, 2),
    })
    {
        var state = new StrainState();
        Advance(state, initialLoad * 90f, true);
        State(state, initialLoad, initialLevel, "earned onset level");
        Advance(state, 1f, false);
        State(state, initialLoad - 1f / 180f, initialLevel, "one resting tick cannot erase earned strain");
        Advance(state, 178f, false);
        State(state, recoveredLoad + 1f / 180f, initialLevel, "179s rest retains earned level");
        // Use a fresh state at the exact total-time boundary to avoid making
        // an accumulated float rounding artifact the recovery oracle.
        state.Restore(initialLoad);
        Advance(state, 180f, false);
        State(state, recoveredLoad, recoveredLevel, "180s rest recovers exactly one earned level");
    }

    var resumed = new StrainState();
    resumed.Restore(2f);
    Advance(resumed, 90f, false);
    State(resumed, 1.5f, 2, "partial moderate recovery");
    Advance(resumed, 9f, true);
    State(resumed, 1.6f, 2, "resuming a sprint cannot downgrade retained moderate strain");
    Advance(resumed, 360f, false);
    State(resumed, 0f, 0, "one long rest may recover multiple levels");
}

static void CappedStrain()
{
    var state = new StrainState();
    Advance(state, 360f, true);
    State(state, 3.999f, 3, "first cap");
    Advance(state, 100000f, true);
    State(state, 3.999f, 3, "extended capped exercise");
    Advance(state, 180f, false);
    State(state, 2.999f, 3, "capped severe strain retains its recovery level");
    Advance(state, 180f, false);
    State(state, 1.999f, 2, "cap has no hidden exertion overflow");
    Advance(state, 360f, true);
    State(state, 3.999f, 3, "strain can reach cap again");
}

static void RestoreValues()
{
    var state = new StrainState();
    foreach ((float input, float expected, int level) in new (float, float, int)[]
    {
        (-float.MaxValue, 0f, 0), (-1f, 0f, 0), (0f, 0f, 0),
        (0.375f, 0.375f, 0), (2.75f, 2.75f, 2),
        (3.999f, 3.999f, 3), (4f, 3.999f, 3), (float.MaxValue, 3.999f, 3),
        (float.NaN, 0f, 0), (float.PositiveInfinity, 0f, 0), (float.NegativeInfinity, 0f, 0),
    })
    {
        state.Restore(2.5f);
        state.Restore(input);
        State(state, expected, level, $"restore {input} replaces existing load");
    }

    state.Restore(1.25f);
    Advance(state, 45f, true);
    State(state, 1.75f, 1, "restored fractional progress contributes to exercise");
    Advance(state, 45f, false);
    State(state, 1.5f, 1, "restored fractional progress recovers normally");
}

static void RestoreRecoveryLevel()
{
    var state = new StrainState();
    foreach ((float load, int savedLevel, float expectedLoad, int expectedLevel) in new (float, int, float, int)[]
    {
        (0.5f, 1, 0.5f, 1), (1.5f, 2, 1.5f, 2), (2.5f, 3, 2.5f, 3),
        (0.5f, 3, 0.5f, 1), (1.5f, int.MaxValue, 1.5f, 2),
        (2.5f, int.MinValue, 2.5f, 2), (1f, 2, 1f, 1),
        (0f, 3, 0f, 0), (-1f, 3, 0f, 0), (4f, 4, 3.999f, 3),
        (float.NaN, 3, 0f, 0), (float.PositiveInfinity, 3, 0f, 0),
        (float.NegativeInfinity, 3, 0f, 0),
    })
    {
        state.Restore(load, savedLevel);
        State(state, expectedLoad, expectedLevel, $"restore load {load}, saved level {savedLevel}");
    }

    state.Restore(1.5f, 2);
    Advance(state, 900f, true, 75f);
    State(state, 1.5f, 2, "saved moderate level survives immune sprinting");
    Advance(state, 90f, false);
    State(state, 1f, 1, "saved moderate level recovers at load 1");
    state.Restore(1.5f, 2);
    state.Restore(1.5f);
    State(state, 1.5f, 1, "load-only restore establishes the floor baseline");
}

static void SplitStepEquivalence()
{
    foreach ((float skill, float runLoad, float finalLoad, int level) in new (float, float, float, int)[]
    {
        (0f, 2.375f, 2.125f, 2), (37.5f, 1.625f, 1.375f, 1), (75f, 0.875f, 0.625f, 0),
    })
    {
        var whole = new StrainState();
        var split = new StrainState();
        whole.Restore(0.875f);
        split.Restore(0.875f);
        Advance(whole, 135f, true, skill);
        for (int step = 0; step < 27; step++)
            Advance(split, 5f, true, skill);
        Near(runLoad, whole.Load, $"single run step at skill {skill}");
        Near(runLoad, split.Load, $"split run steps at skill {skill}");

        Advance(whole, 45f, false, skill);
        for (int step = 0; step < 9; step++)
            Advance(split, 5f, false, skill);
        Near(finalLoad, whole.Load, $"single idle step at skill {skill}");
        Near(finalLoad, split.Load, $"split idle steps at skill {skill}");
        Equal(level, whole.Level, $"single-step final level at skill {skill}");
        Equal(level, split.Level, $"split-step final level at skill {skill}");
    }

    var unchanged = new StrainState();
    unchanged.Restore(1.25f);
    Advance(unchanged, 0f, true);
    Advance(unchanged, 0f, false);
    State(unchanged, 1.25f, 1, "zero elapsed time changes nothing");
}

static void FixedTimeSteps()
{
    foreach ((float skill, int onsetTicks) in new (float, int)[] { (0f, 4500), (37.5f, 9000) })
    {
        var state = new StrainState();
        for (int tick = 0; tick < onsetTicks - 1; tick++)
            Advance(state, 0.02f, true, skill);
        Equal(0, state.Level, $"Run {skill}: one tick before first onset");
        Advance(state, 0.02f, true, skill);
        State(state, 1f, 1, $"Run {skill}: first onset after {onsetTicks} ticks");

        Advance(state, 0.02f, false, skill);
        State(state, 1f - 0.02f / 180f, 1, $"Run {skill}: one idle tick retains first strain");
        for (int tick = 1; tick < 8999; tick++)
            Advance(state, 0.02f, false, skill);
        Equal(1, state.Level, $"Run {skill}: 8999 recovery ticks still retain mild strain");
        Advance(state, 0.02f, false, skill);
        State(state, 0f, 0, $"Run {skill}: 9000 recovery ticks fully heal first strain");
    }

    var levels = new StrainState();
    for (int expected = 1; expected <= 3; expected++)
    {
        for (int tick = 0; tick < 4500; tick++)
            Advance(levels, 0.02f, true);
        State(levels, expected, expected, $"20ms running reaches level {expected}");
    }
    for (int expected = 2; expected >= 0; expected--)
    {
        for (int tick = 0; tick < 9000; tick++)
            Advance(levels, 0.02f, false);
        State(levels, expected, expected, $"20ms recovery reaches level {expected}");
    }
}

static void ConfiguredRates()
{
    var state = new StrainState();
    state.Advance(45f, true, 0f, 45f, 60f, 50f);
    State(state, 1f, 1, "45s configured onset");
    state.Advance(45f, true, 25f, 45f, 60f, 50f);
    State(state, 1.5f, 1, "half configured immunity slows growth by half");
    state.Advance(450f, true, 50f, 45f, 60f, 50f);
    State(state, 1.5f, 1, "configured immunity pauses growth without recovery");
    state.Advance(30f, false, 0f, 45f, 60f, 50f);
    State(state, 1f, 1, "60s configured recovery removes half a level in 30s");
    state = new StrainState();
    Advance(state, 90f, true);
    Advance(state, 1f, false);
    State(state, 179f / 180f, 1, "first strain must not vanish on the next rest tick");
    var restored = new StrainState();
    restored.Restore(state.Load, state.Level);
    State(restored, state.Load, 1, "saved healing severity survives reloading");
    Advance(restored, 179f, false);
    State(restored, 0f, 0, "first strain heals after a full recovery interval");
}

static void PenaltyLevels()
{
    foreach ((int level, float penalty) in new (int, float)[]
    {
        (int.MinValue, 0f), (-1, 0f), (0, 0f), (1, 0.15f),
        (2, 0.30f), (3, 0.50f), (4, 0.50f), (int.MaxValue, 0.50f),
    })
        Near(penalty, StrainState.StaminaPenalty(level), $"penalty at level {level}");
}

static void Advance(StrainState state, float seconds, bool sprinting, float runSkill = 0f)
    => state.Advance(seconds, sprinting, runSkill, 90f, 180f, 75f);

static void State(StrainState state, float load, int level, string context)
{
    Near(load, state.Load, context + " load");
    Equal(level, state.Level, context + " level");
    if (!float.IsFinite(state.Load) || state.Load < 0f || state.Load > 3.999f)
        throw new InvalidOperationException($"{context}: load {state.Load} is outside the finite [0, 3.999] range");
}

static void Near(float expected, float actual, string context)
{
    if (!float.IsFinite(actual) || MathF.Abs(expected - actual) > 0.0001f)
        throw new InvalidOperationException($"{context}: expected {expected:R}, got {actual:R}");
}

static void Equal(int expected, int actual, string context)
{
    if (expected != actual)
        throw new InvalidOperationException($"{context}: expected {expected}, got {actual}");
}
