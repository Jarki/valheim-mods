using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;

namespace ValheimServerMetrics
{
    internal sealed class BackupInventoryResult
    {
        internal BackupInventoryResult(BackupEntry[] backups, double? liveSaveUnixSeconds)
        {
            Backups = backups ?? throw new ArgumentNullException(nameof(backups));
            LiveSaveUnixSeconds = liveSaveUnixSeconds;
        }

        internal BackupEntry[] Backups { get; }
        internal double? LiveSaveUnixSeconds { get; }
    }

    internal static class BackupInventoryReader
    {
        private const int MaximumBackups = 128;
        private const string MarkerPrefix = "_main.";
        private const string MarkerSuffix = ".ok";

        internal static BackupInventoryResult Read(string worldDirectory)
        {
            if (string.IsNullOrWhiteSpace(worldDirectory))
                throw new ArgumentException("A world directory is required.", nameof(worldDirectory));

            string fullWorldDirectory = Path.GetFullPath(worldDirectory);
            var world = new DirectoryInfo(fullWorldDirectory);
            EnsurePathIsSafe(world);
            DirectoryInfo parent = world.Parent ?? throw new IOException("The world directory has no parent directory.");

            string worldName = world.Name;
            string backupPrefix = worldName + "_backup_auto-";
            var observedNames = new HashSet<string>(StringComparer.Ordinal);
            var backups = new List<BackupEntry>();
            foreach (DirectoryInfo candidate in parent.EnumerateDirectories())
            {
                string name = candidate.Name;
                if (!name.StartsWith(backupPrefix, StringComparison.Ordinal))
                    continue;

                string timestampText = name.Substring(backupPrefix.Length);
                if (!TryParseBackupTimestamp(timestampText, out double? createdUnixSeconds))
                    continue;

                observedNames.Add(name);
                EnsureDirectoryIsSafe(candidate);
                double? dataUnixSeconds = ReadLatestMarkerTimestamp(candidate);
                backups.Add(new BackupEntry(name, createdUnixSeconds, dataUnixSeconds));
                if (backups.Count > MaximumBackups)
                    throw new IOException("The native backup inventory exceeds the supported limit of 128 entries.");
            }

            var revalidatedNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (DirectoryInfo candidate in parent.EnumerateDirectories())
            {
                string name = candidate.Name;
                if (!name.StartsWith(backupPrefix, StringComparison.Ordinal) ||
                    !TryParseBackupTimestamp(name.Substring(backupPrefix.Length), out _))
                    continue;
                EnsureDirectoryIsSafe(candidate);
                revalidatedNames.Add(name);
            }
            if (!observedNames.SetEquals(revalidatedNames))
                throw new IOException("The native backup inventory changed during scanning.");

            backups.Sort((left, right) => StringComparer.Ordinal.Compare(left.Name, right.Name));
            double? liveSaveUnixSeconds = ReadLatestMarkerTimestamp(world);
            return new BackupInventoryResult(backups.ToArray(), liveSaveUnixSeconds);
        }

        private static bool TryParseBackupTimestamp(string value, out double? unixSeconds)
        {
            unixSeconds = null;
            if (!DateTime.TryParseExact(
                value,
                "yyyyMMdd-HHmmss",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out DateTime localTime))
                return false;

            localTime = DateTime.SpecifyKind(localTime, DateTimeKind.Unspecified);
            TimeZoneInfo localZone = TimeZoneInfo.Local;
            if (localZone.IsAmbiguousTime(localTime) || localZone.IsInvalidTime(localTime))
                return true;

            DateTime utc = TimeZoneInfo.ConvertTimeToUtc(localTime, localZone);
            unixSeconds = new DateTimeOffset(utc).ToUnixTimeSeconds();
            return true;
        }

        private static double? ReadLatestMarkerTimestamp(DirectoryInfo directory)
        {
            bool found = false;
            uint highestGeneration = 0;
            DateTime latestWriteTime = default;
            foreach (FileInfo file in directory.EnumerateFiles())
            {
                if (!TryParseMarkerGeneration(file.Name, out uint generation))
                    continue;
                file.Refresh();
                if (!file.Exists)
                    throw new IOException("A world commit marker disappeared during inventory scanning.");
                EnsureNotReparsePoint(file);
                DateTime writeTime = file.LastWriteTimeUtc;
                file.Refresh();
                if (!file.Exists)
                    throw new IOException("A world commit marker disappeared during inventory scanning.");
                EnsureNotReparsePoint(file);
                if (!found || generation > highestGeneration ||
                    (generation == highestGeneration && writeTime > latestWriteTime))
                {
                    found = true;
                    highestGeneration = generation;
                    latestWriteTime = writeTime;
                }
            }

            directory.Refresh();
            if (!directory.Exists)
                throw new IOException("A world directory disappeared during inventory scanning.");
            return found ? new DateTimeOffset(DateTime.SpecifyKind(latestWriteTime, DateTimeKind.Utc)).ToUnixTimeSeconds() : null;
        }

        private static bool TryParseMarkerGeneration(string name, out uint generation)
        {
            generation = 0;
            if (!name.StartsWith(MarkerPrefix, StringComparison.Ordinal) ||
                !name.EndsWith(MarkerSuffix, StringComparison.Ordinal))
                return false;
            int digitsLength = name.Length - MarkerPrefix.Length - MarkerSuffix.Length;
            if (digitsLength <= 0)
                return false;
            return uint.TryParse(
                name.Substring(MarkerPrefix.Length, digitsLength),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out generation);
        }

        private static void EnsurePathIsSafe(DirectoryInfo directory)
        {
            for (DirectoryInfo current = directory; current != null; current = current.Parent)
                EnsureDirectoryIsSafe(current);
        }

        private static void EnsureDirectoryIsSafe(DirectoryInfo directory)
        {
            directory.Refresh();
            if (!directory.Exists)
                throw new DirectoryNotFoundException("World inventory directory was not found: " + directory.FullName);
            EnsureNotReparsePoint(directory);
        }

        private static void EnsureNotReparsePoint(FileSystemInfo entry)
        {
            if ((entry.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Symbolic links and reparse points are not allowed in world inventory paths: " + entry.FullName);
        }
    }

    internal sealed class BackupInventoryMonitor : IDisposable
    {
        private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(60);
        private static readonly TimeSpan ShutdownTimeout = TimeSpan.FromSeconds(5);

        private readonly MetricsState state;
        private readonly Action<string> logWarning;
        private readonly object sync = new object();
        private readonly AutoResetEvent wake = new AutoResetEvent(false);
        private readonly Thread worker;

        private string worldDirectory;
        private long generation;
        private bool stopping;
        private bool errorLogged;

        internal BackupInventoryMonitor(MetricsState state, Action<string> logWarning)
        {
            this.state = state ?? throw new ArgumentNullException(nameof(state));
            this.logWarning = logWarning ?? throw new ArgumentNullException(nameof(logWarning));
            worker = new Thread(WorkerLoop)
            {
                IsBackground = true,
                Name = "Valheim backup inventory"
            };
            worker.Start();
        }

        internal void SetWorldDirectory(string worldDirectory)
        {
            lock (sync)
            {
                if (stopping)
                    return;
                if (string.Equals(this.worldDirectory, worldDirectory, StringComparison.Ordinal))
                    return;
                this.worldDirectory = worldDirectory;
                generation++;
                state.MarkBackupInventoryUnavailable();
                wake.Set();
            }
        }

        internal void RequestRefresh()
        {
            lock (sync)
            {
                if (stopping)
                    return;
                wake.Set();
            }
        }

        public void Dispose()
        {
            lock (sync)
            {
                if (stopping)
                    return;
                stopping = true;
                generation++;
                wake.Set();
            }
            if (Thread.CurrentThread != worker)
                worker.Join(ShutdownTimeout);
        }

        private void WorkerLoop()
        {
            try
            {
                while (true)
                {
                    string directory;
                    long sourceGeneration;
                    lock (sync)
                    {
                        if (stopping)
                            return;
                        directory = worldDirectory;
                        sourceGeneration = generation;
                    }

                    try
                    {
                        if (directory == null)
                            PublishUnavailable(sourceGeneration, null);
                        else
                        {
                            BackupInventoryResult result = BackupInventoryReader.Read(directory);
                            lock (sync)
                            {
                                if (!stopping && sourceGeneration == generation)
                                {
                                    state.SetBackupInventory(result.Backups, result.LiveSaveUnixSeconds);
                                    errorLogged = false;
                                }
                            }
                        }
                    }
                    catch (Exception exception)
                    {
                        PublishUnavailable(sourceGeneration, exception);
                    }

                    if (!wake.WaitOne(RefreshInterval))
                        continue;
                }
            }
            catch (Exception exception)
            {
                long currentGeneration;
                lock (sync)
                    currentGeneration = generation;
                PublishUnavailable(currentGeneration, exception);
            }
            finally
            {
                lock (sync)
                    stopping = true;
                wake.Dispose();
            }
        }

        private void PublishUnavailable(long sourceGeneration, Exception exception)
        {
            lock (sync)
            {
                if (stopping || sourceGeneration != generation)
                    return;
                try
                {
                    state.MarkBackupInventoryUnavailable();
                }
                catch (Exception stateException)
                {
                    exception = exception ?? stateException;
                }
                if (exception != null && !errorLogged)
                {
                    errorLogged = true;
                    try
                    {
                        logWarning("Unable to read the native world backup inventory: " + exception.Message);
                    }
                    catch (Exception)
                    {
                        // Logging must never terminate the background inventory worker.
                    }
                }
            }
        }
    }
}
