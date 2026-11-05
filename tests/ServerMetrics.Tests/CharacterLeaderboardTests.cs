namespace ValheimServerMetrics
{
    internal static class CharacterLeaderboardTests
    {
        internal static Task Run()
        {
            string root = Path.Combine(Path.GetTempPath(), "character-leaderboard-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                TracksIdentitySessionsAndExactWindow(root);
                ClockChangesDoNotFabricatePlaytime(root);
                ContinuousPersistenceCoalescesSegments(root);
                RestoresOfflineWithoutDowntime(root);
                RejectsCorruptionAndReportsWriteFailure(root);
                TracksAndRestoresDeaths(root);
                MigratesOldLedgerAndRejectsInvalidDeaths(root);
            }
            finally
            {
                try { Directory.Delete(root, true); } catch { }
            }
            return Task.CompletedTask;
        }

        private static void TracksIdentitySessionsAndExactWindow(string root)
        {
            var clock = new Clock(1_700_000_000);
            string path = Path.Combine(root, "identity.json");
            using (var tracker = new CharacterLeaderboard(path, () => clock.Mono, () => clock.Utc))
            {
                tracker.Observe(new[] { new CharacterPresence("one", "Same"), new CharacterPresence("one", "Ignored duplicate"), new CharacterPresence("two", "Same") });
                clock.Advance(10);
                tracker.Observe(new[] { new CharacterPresence("one", "Renamed"), new CharacterPresence("two", "Same") });
                CharacterLeaderboardEntry[] entries = tracker.Snapshot();
                Equal(2, entries.Length, "Duplicate display names must remain distinct identities.");
                Entry one = Find(entries, "one");
                Equal("Renamed", one.Value.CharacterName, "Rename must update display name without replacing identity.");
                Equal(10d, one.Value.AllTimeSeconds, "Duplicate presence must not double-count time.");
                Equal(1L, one.Value.Sessions, "Continuous presence is one session.");
                clock.Advance(604795);
                tracker.Observe(Array.Empty<CharacterPresence>());
                clock.Advance(10);
                Equal(604790d, Find(tracker.Snapshot(), "one").Value.LastSevenDaysSeconds, "Seven-day cutoff must retain only the exact overlap.");
                tracker.Observe(new[] { new CharacterPresence("one", "Renamed") });
                Equal(2L, Find(tracker.Snapshot(), "one").Value.Sessions, "Reconnect must increment durable sessions.");
                tracker.Flush();
            }
        }

        private static void RestoresOfflineWithoutDowntime(string root)
        {
            var clock = new Clock(1_800_000_000);
            string path = Path.Combine(root, "restart.json");
            var first = new CharacterLeaderboard(path, () => clock.Mono, () => clock.Utc);
            first.Observe(new[] { new CharacterPresence("stable", "Before") });
            clock.Advance(30);
            first.Dispose();
            clock.Advance(3600);
            using var second = new CharacterLeaderboard(path, () => clock.Mono, () => clock.Utc);
            CharacterLeaderboardEntry restored = Find(second.Snapshot(), "stable").Value;
            Equal(false, restored.Online, "Persisted records must restore offline.");
            Equal(30d, restored.AllTimeSeconds, "Restart downtime must not count.");
            second.Observe(new[] { new CharacterPresence("stable", "After") });
            Equal(2L, Find(second.Snapshot(), "stable").Value.Sessions, "Presence after restart is a new session.");
        }

        private static void ContinuousPersistenceCoalescesSegments(string root)
        {
            var clock = new Clock(1_900_000_000);
            string path = Path.Combine(root, "coalesced.json");
            using (var tracker = new CharacterLeaderboard(path, () => clock.Mono, () => clock.Utc))
            {
                tracker.Observe(new[] { new CharacterPresence("steady", "Steady") });
                for (int index = 0; index < 20; index++)
                {
                    clock.Advance(1);
                    tracker.Observe(new[] { new CharacterPresence("steady", "Steady") });
                    tracker.Flush();
                }
            }
            Equal(1, Count(File.ReadAllText(path), "\"start\""), "A continuous session must remain one retained segment.");
        }

        private static void ClockChangesDoNotFabricatePlaytime(string root)
        {
            var clock = new Clock(2_000_000_000);
            using var tracker = new CharacterLeaderboard(Path.Combine(root, "clock.json"), () => clock.Mono, () => clock.Utc);
            tracker.Observe(new[] { new CharacterPresence("clocked", "Clocked") });
            clock.AdvanceMonotonic(10);
            clock.SetUtc(clock.Utc + 86400);
            Equal(10d, Find(tracker.Snapshot(), "clocked").Value.AllTimeSeconds, "UTC jumps must not fabricate elapsed hours.");
            clock.SetMono(5);
            clock.SetUtc(clock.Utc - 172800);
            Equal(10d, Find(tracker.Snapshot(), "clocked").Value.AllTimeSeconds, "Monotonic rollback must not subtract or fabricate time.");
            clock.SetMono(15);
            Equal(20d, Find(tracker.Snapshot(), "clocked").Value.AllTimeSeconds, "Recovery after rollback must resume from the new baseline.");
        }

        private static void RejectsCorruptionAndReportsWriteFailure(string root)
        {
            string corrupt = Path.Combine(root, "corrupt.json");
            File.WriteAllText(corrupt, "not json");
            Throws<Exception>(() => new CharacterLeaderboard(corrupt, () => 0, () => 0), "Corrupt persistence must not become empty valid state.");
            Equal("not json", File.ReadAllText(corrupt), "Corrupt input must remain untouched.");
            string overlapping = Path.Combine(root, "overlapping.json");
            File.WriteAllText(overlapping, "{\"characters\":[{\"all_time_seconds\":20,\"id\":\"x\",\"name\":\"X\",\"segments\":[{\"end\":20,\"start\":10},{\"end\":25,\"start\":15}],\"sessions\":1}],\"version\":1}");
            Throws<InvalidDataException>(() => new CharacterLeaderboard(overlapping, () => 0, () => 0), "Overlapping retained segments must be rejected as corrupt.");
            string symlinkTarget = Path.Combine(root, "safe-target");
            string symlink = Path.Combine(root, "linked-parent");
            Directory.CreateDirectory(symlinkTarget);
            try
            {
                Directory.CreateSymbolicLink(symlink, symlinkTarget);
                string nested = Path.Combine(symlink, "not-created", "playtime.json");
                Throws<IOException>(() => new CharacterLeaderboard(nested, () => 0, () => 0), "A symlink ancestor above a missing directory must be rejected.");
            }
            catch (Exception exception) when (exception is UnauthorizedAccessException || exception is IOException || exception is PlatformNotSupportedException)
            {
                // Symlink creation is unavailable on this test host.
            }



            string directoryAsFile = Path.Combine(root, "write-failure");
            Directory.CreateDirectory(directoryAsFile);
            var tracker = new CharacterLeaderboard(directoryAsFile, () => 0, () => 0);
            tracker.Observe(new[] { new CharacterPresence("x", "X") });
            Throws<IOException>(() => tracker.Flush(), "Flush must expose persistence failures.");
            if (tracker.PersistenceError == null) throw new InvalidOperationException("Background persistence error must remain observable.");
            Throws<IOException>(() => tracker.Dispose(), "Shutdown must expose a failed final write.");
        }

        private static void TracksAndRestoresDeaths(string root)
        {
            string path = Path.Combine(root, "deaths.json");
            using (var tracker = new CharacterLeaderboard(path, () => 0, () => 1))
            {
                tracker.Observe(new[] { new CharacterPresence("fallen", "Fallen"), new CharacterPresence("other", "Other") });
                Equal(false, tracker.RecordDeath("unknown", "death:unknown"), "Unknown characters must not create records.");
                Equal(true, tracker.RecordDeath("fallen", "death:first"), "A new death must be accepted.");
                Equal(false, tracker.RecordDeath("fallen", "death:first"), "A duplicate death must be rejected.");
                Equal(1L, Find(tracker.Snapshot(), "fallen").Value.Deaths, "Death total must be per character.");
                tracker.Flush();
            }
            using (var restored = new CharacterLeaderboard(path, () => 0, () => 1))
            {
                Equal(1L, Find(restored.Snapshot(), "fallen").Value.Deaths, "Death count must survive restart.");
                Equal(false, restored.RecordDeath("other", "death:first"), "Death deduplication must survive restart globally.");
                Equal(0L, Find(restored.Snapshot(), "other").Value.Deaths, "A duplicate after reload must not mutate another character.");
            }
        }

        private static void MigratesOldLedgerAndRejectsInvalidDeaths(string root)
        {
            string old = Path.Combine(root, "old-schema.json");
            File.WriteAllText(old, "{\"characters\":[{\"all_time_seconds\":12,\"id\":\"old\",\"name\":\"Old\",\"segments\":[{\"end\":12,\"start\":0}],\"sessions\":2}],\"version\":1}");
            using (var tracker = new CharacterLeaderboard(old, () => 0, () => 20))
            {
                CharacterLeaderboardEntry migrated = Find(tracker.Snapshot(), "old").Value;
                Equal(12d, migrated.AllTimeSeconds, "Original playtime must survive additive schema migration.");
                Equal(2L, migrated.Sessions, "Original sessions must survive additive schema migration.");
                Equal(0L, migrated.Deaths, "Original ledgers must migrate with zero deaths.");
            }

            string negative = Path.Combine(root, "negative-deaths.json");
            File.WriteAllText(negative, "{\"characters\":[{\"all_time_seconds\":0,\"deaths\":-1,\"id\":\"x\",\"name\":\"X\",\"segments\":[],\"sessions\":0}],\"version\":1}");
            Throws<InvalidDataException>(() => new CharacterLeaderboard(negative, () => 0, () => 0), "Negative death counters must be rejected.");

            string overflow = Path.Combine(root, "overflow-deaths.json");
            File.WriteAllText(overflow, "{\"characters\":[{\"all_time_seconds\":0,\"deaths\":9223372036854775807,\"id\":\"x\",\"name\":\"X\",\"segments\":[],\"sessions\":0}],\"version\":1}");
            using (var tracker = new CharacterLeaderboard(overflow, () => 0, () => 0))
            {
                Throws<OverflowException>(() => tracker.RecordDeath("x", "death:overflow"), "Death counter overflow must be rejected.");
                Equal(long.MaxValue, Find(tracker.Snapshot(), "x").Value.Deaths, "Overflow rejection must not mutate the death total.");
                tracker.Flush();
            }
            using (var restored = new CharacterLeaderboard(overflow, () => 0, () => 0))
            {
                Throws<OverflowException>(() => restored.RecordDeath("x", "death:overflow"), "Overflow rejection must not retain the event id.");
                Equal(long.MaxValue, Find(restored.Snapshot(), "x").Value.Deaths, "Overflow rejection must leave durable state unchanged.");
            }

            string retained = Path.Combine(root, "retained-deaths.json");
            using (var tracker = new CharacterLeaderboard(retained, () => 0, () => 0))
            {
                tracker.Observe(new[] { new CharacterPresence("x", "X") });
                for (int index = 0; index <= 8192; index++)
                    Equal(true, tracker.RecordDeath("x", "death:" + index), "Distinct deaths must be accepted.");
                Equal(true, tracker.RecordDeath("x", "death:0"), "An event older than the newest 8192 must expire.");
                Equal(false, tracker.RecordDeath("x", "death:8192"), "A retained recent event must remain deduplicated.");
            }
        }

        private static Entry Find(CharacterLeaderboardEntry[] entries, string id)
        {
            foreach (CharacterLeaderboardEntry entry in entries) if (entry.Id == id) return new Entry(entry);
            throw new InvalidOperationException("Missing character " + id);
        }

        private static int Count(string value, string needle)
        {
            int count = 0;
            int offset = 0;
            while ((offset = value.IndexOf(needle, offset, StringComparison.Ordinal)) >= 0)
            {
                count++;
                offset += needle.Length;
            }
            return count;
        }

        private readonly struct Entry { internal Entry(CharacterLeaderboardEntry value) { Value = value; } internal CharacterLeaderboardEntry Value { get; } }
        private sealed class Clock
        {
            internal Clock(long utc) { Utc = utc; }
            internal double Mono { get; private set; }
            internal long Utc { get; private set; }
            internal void Advance(long seconds) { Mono += seconds; Utc += seconds; }
            internal void AdvanceMonotonic(long seconds) { Mono += seconds; }
            internal void SetUtc(long utc) { Utc = utc; }
            internal void SetMono(double mono) { Mono = mono; }
        }
        private static void Equal<T>(T expected, T actual, string message)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new InvalidOperationException(message + " Expected " + expected + ", got " + actual + ".");
        }
        private static void Throws<T>(Action action, string message) where T : Exception
        {
            try { action(); } catch (T) { return; }
            throw new InvalidOperationException(message);
        }
    }
}
