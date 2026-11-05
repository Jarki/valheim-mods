using System.Globalization;
using System.Text;

namespace ValheimServerMetrics
{
    internal static class InventoryTests
    {
        internal static async Task Run()
        {
            string root = Path.Combine(Path.GetTempPath(), "valheim-inventory-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                ReaderUsesExactNativeNamesAndCommitGenerations(root);
                ReaderDistinguishesEmptyFromUnavailable(root);
                ReaderRejectsSymlinkTraversal(root);
                await MonitorRefreshesAndStopsWithoutPublishingOldWorld(root);
            }
            finally
            {
                try
                {
                    Directory.Delete(root, true);
                }
                catch
                {
                    // A failed cleanup must not conceal the regression result.
                }
            }
        }

        private static void ReaderUsesExactNativeNamesAndCommitGenerations(string root)
        {
            string parent = Path.Combine(root, "matching");
            string world = Path.Combine(parent, "NorthernHearth");
            Directory.CreateDirectory(world);
            DateTime liveOld = Utc(2025, 1, 1, 1, 0, 0);
            DateTime liveHigh = Utc(2025, 1, 1, 2, 0, 0);
            Marker(world, 2, liveOld);
            Marker(world, 10, liveHigh);
            Marker(world, 3, Utc(2026, 1, 1, 0, 0, 0));
            File.WriteAllText(Path.Combine(world, "_main.11.db"), "not a marker");

            string firstName = "NorthernHearth_backup_auto-20250102-030405";
            string first = Path.Combine(parent, firstName);
            Directory.CreateDirectory(first);
            DateTime firstLow = Utc(2025, 1, 2, 5, 0, 0);
            DateTime firstHigh = Utc(2025, 1, 2, 6, 0, 0);
            Marker(first, 4, firstLow);
            Marker(first, 12, firstHigh);
            Marker(first, 5, Utc(2026, 1, 2, 0, 0, 0));

            string noMarkerName = "NorthernHearth_backup_auto-20250103-040506";
            Directory.CreateDirectory(Path.Combine(parent, noMarkerName));
            Directory.CreateDirectory(Path.Combine(parent, "OtherWorld_backup_auto-20250102-030405"));
            Directory.CreateDirectory(Path.Combine(parent, "NorthernHearth_backup_20250102-030405"));
            Directory.CreateDirectory(Path.Combine(parent, "NorthernHearth_backup_auto-20250230-030405"));
            File.WriteAllText(Path.Combine(parent, "NorthernHearth_backup_auto-20250104-010203"), "file, not folder");

            BackupInventoryResult result = BackupInventoryReader.Read(world + Path.DirectorySeparatorChar);
            Equal(2, result.Backups.Length, "Only exact, valid native backup directories should match.");
            Equal(firstName, result.Backups[0].Name, "Backups should be deterministically name-sorted.");
            Equal(noMarkerName, result.Backups[1].Name, "Backups should be deterministically name-sorted.");
            Equal(ToUnix(firstHigh), result.Backups[0].DataUnixSeconds, "Highest numeric marker generation must win over arbitrary newest mtime.");
            Equal(null, result.Backups[1].DataUnixSeconds, "A backup without a commit marker has unknown data time.");
            Equal(ToUnix(liveHigh), result.LiveSaveUnixSeconds, "Live save time must use the highest numeric marker generation.");

            DateTime local = DateTime.SpecifyKind(
                DateTime.ParseExact("20250102-030405", "yyyyMMdd-HHmmss", CultureInfo.InvariantCulture),
                DateTimeKind.Unspecified);
            double? expectedCreated = TimeZoneInfo.Local.IsAmbiguousTime(local) || TimeZoneInfo.Local.IsInvalidTime(local)
                ? null
                : new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, TimeZoneInfo.Local)).ToUnixTimeSeconds();
            Equal(expectedCreated, result.Backups[0].CreatedUnixSeconds, "Folder timestamps must be interpreted strictly in server local time.");
        }

        private static void ReaderDistinguishesEmptyFromUnavailable(string root)
        {
            string world = Path.Combine(root, "empty", "QuietWorld");
            Directory.CreateDirectory(world);
            BackupInventoryResult empty = BackupInventoryReader.Read(world);
            Equal(0, empty.Backups.Length, "A successful empty scan is a concrete zero.");
            Equal(null, empty.LiveSaveUnixSeconds, "A missing live marker is unknown.");

            DateTime firstBackup = new DateTime(2025, 1, 1, 0, 0, 0);
            for (int index = 0; index < 129; index++)
            {
                string timestamp = firstBackup.AddMinutes(index).ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
                Directory.CreateDirectory(Path.Combine(Path.GetDirectoryName(world)!, "QuietWorld_backup_auto-" + timestamp));
            }
            Throws<IOException>(() => BackupInventoryReader.Read(world), "Oversized inventories must fail instead of being silently truncated.");

            Directory.Delete(world);
            Throws<DirectoryNotFoundException>(() => BackupInventoryReader.Read(world), "A failed scan must not masquerade as zero backups.");
        }

        private static void ReaderRejectsSymlinkTraversal(string root)
        {
            string parent = Path.Combine(root, "links");
            string world = Path.Combine(parent, "LinkedWorld");
            string target = Path.Combine(root, "link-target");
            Directory.CreateDirectory(world);
            Directory.CreateDirectory(target);
            string linkedBackup = Path.Combine(parent, "LinkedWorld_backup_auto-20250102-030405");
            try
            {
                Directory.CreateSymbolicLink(linkedBackup, target);
            }
            catch (Exception exception) when (exception is UnauthorizedAccessException || exception is IOException || exception is PlatformNotSupportedException)
            {
                return;
            }

            Throws<IOException>(() => BackupInventoryReader.Read(world), "Backup symlinks must never be traversed.");
        }

        private static async Task MonitorRefreshesAndStopsWithoutPublishingOldWorld(string root)
        {
            string parent = Path.Combine(root, "monitor");
            string oldWorld = Path.Combine(parent, "OldWorld");
            string newWorld = Path.Combine(parent, "NewWorld");
            Directory.CreateDirectory(oldWorld);
            Directory.CreateDirectory(newWorld);
            for (int index = 0; index < 128; index++)
                Directory.CreateDirectory(Path.Combine(parent, "OldWorld_backup_auto-20250101-" + index.ToString("000000", CultureInfo.InvariantCulture)));
            string newBackup = "NewWorld_backup_auto-20250102-030405";
            Directory.CreateDirectory(Path.Combine(parent, newBackup));

            var warnings = new List<string>();
            var state = new MetricsState("test", () => 0, () => 1_700_000_000);
            var monitor = new BackupInventoryMonitor(state, message => warnings.Add(message));
            monitor.SetWorldDirectory(oldWorld);
            monitor.SetWorldDirectory(newWorld);
            monitor.RequestRefresh();

            string snapshot = await WaitForSnapshot(state, text => text.Contains("backup=\"" + newBackup + "\"", StringComparison.Ordinal));
            Contains(snapshot, "valheim_world_backups 1\n", "The current world inventory should be published.");
            False(snapshot.Contains("OldWorld_backup_auto", StringComparison.Ordinal), "A superseded world scan must not publish.");
            Equal(0, warnings.Count, "A valid refresh should not warn.");

            monitor.Dispose();
            monitor.RequestRefresh();
            monitor.SetWorldDirectory(oldWorld);
        }

        private static async Task<string> WaitForSnapshot(MetricsState state, Func<string, bool> predicate)
        {
            DateTime deadline = DateTime.UtcNow.AddSeconds(5);
            while (DateTime.UtcNow < deadline)
            {
                string snapshot = Encoding.UTF8.GetString(state.GetSnapshot());
                if (predicate(snapshot))
                    return snapshot;
                await Task.Delay(20);
            }
            throw new InvalidOperationException("Timed out waiting for the background inventory refresh.");
        }

        private static void Marker(string directory, uint generation, DateTime writeTimeUtc)
        {
            string path = Path.Combine(directory, "_main." + generation.ToString(CultureInfo.InvariantCulture) + ".ok");
            File.WriteAllText(path, string.Empty);
            File.SetLastWriteTimeUtc(path, writeTimeUtc);
        }

        private static DateTime Utc(int year, int month, int day, int hour, int minute, int second)
        {
            return new DateTime(year, month, day, hour, minute, second, DateTimeKind.Utc);
        }

        private static double ToUnix(DateTime utc)
        {
            return new DateTimeOffset(utc).ToUnixTimeSeconds();
        }

        private static void Equal<T>(T expected, T actual, string message)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
                throw new InvalidOperationException(message + " Expected " + expected + ", got " + actual + ".");
        }

        private static void Contains(string value, string expected, string message)
        {
            if (!value.Contains(expected, StringComparison.Ordinal))
                throw new InvalidOperationException(message);
        }

        private static void False(bool value, string message)
        {
            if (value)
                throw new InvalidOperationException(message);
        }

        private static void Throws<TException>(Action action, string message) where TException : Exception
        {
            try
            {
                action();
            }
            catch (TException)
            {
                return;
            }
            throw new InvalidOperationException(message);
        }
    }
}
