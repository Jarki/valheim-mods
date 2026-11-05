using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Threading;

namespace ValheimServerMetrics
{
    internal readonly struct CharacterPresence
    {
        internal CharacterPresence(string id, string characterName)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            CharacterName = characterName ?? throw new ArgumentNullException(nameof(characterName));
        }

        internal string Id { get; }
        internal string CharacterName { get; }
    }

    internal readonly struct CharacterLeaderboardEntry
    {
        internal CharacterLeaderboardEntry(string id, string characterName, double allTimeSeconds,
            double lastSevenDaysSeconds, bool online, long sessions, long? lastSeenUnixSeconds,
            long deaths = 0)
        {
            Id = id;
            CharacterName = characterName;
            AllTimeSeconds = allTimeSeconds;
            LastSevenDaysSeconds = lastSevenDaysSeconds;
            Online = online;
            Sessions = sessions;
            LastSeenUnixSeconds = lastSeenUnixSeconds;
            Deaths = deaths;
        }

        internal string Id { get; }
        internal string CharacterName { get; }
        internal double AllTimeSeconds { get; }
        internal double LastSevenDaysSeconds { get; }
        internal bool Online { get; }
        internal long Sessions { get; }
        internal long? LastSeenUnixSeconds { get; }
        internal long Deaths { get; }
    }

    // Public operations only take the in-memory lock. A single worker owns all file I/O,
    // writes approximately once a minute, and is synchronously drained by Flush/Dispose.
    internal sealed class CharacterLeaderboard : IDisposable
    {
        private const double SevenDaysSeconds = 604800;
        private const int MaximumFileBytes = 16 * 1024 * 1024;
        private const int MaximumCharacters = 256;
        private const int MaximumSegments = 131072;
        private const int MaximumIdLength = 256;
        private const int MaximumNameLength = 256;
        // Deduplication deliberately retains only the newest 8192 accepted death events.
        private const int MaximumRecentDeaths = 8192;
        private static readonly TimeSpan WriteInterval = TimeSpan.FromSeconds(60);
        private readonly string path;
        private readonly Func<double> monotonicSeconds;
        private readonly Func<long> utcUnixSeconds;
        private readonly object sync = new object();
        private readonly AutoResetEvent wake = new AutoResetEvent(false);
        private readonly Thread worker;
        private readonly Dictionary<string, Record> records;
        private readonly HashSet<string> presentIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly Queue<string> recentDeaths;
        private readonly HashSet<string> recentDeathIds;
        private bool dirty;
        private bool stopping;
        private long requestedFlush;
        private long completedFlush;
        private long generation;
        private Exception persistenceError;

        internal CharacterLeaderboard(string persistentPath)
            : this(persistentPath, DefaultMonotonicSeconds, DefaultUtcUnixSeconds)
        {
        }

        internal CharacterLeaderboard(string persistentPath, Func<double> monotonicClock, Func<long> utcClock)
        {
            path = persistentPath ?? throw new ArgumentNullException(nameof(persistentPath));
            monotonicSeconds = monotonicClock ?? throw new ArgumentNullException(nameof(monotonicClock));
            utcUnixSeconds = utcClock ?? throw new ArgumentNullException(nameof(utcClock));
            LoadedState loaded = Load(path);
            records = loaded.Records;
            recentDeaths = loaded.RecentDeaths;
            recentDeathIds = new HashSet<string>(recentDeaths, StringComparer.Ordinal);
            worker = new Thread(WorkerLoop) { IsBackground = true, Name = "Valheim character playtime" };
            worker.Start();
        }

        internal Exception PersistenceError { get { lock (sync) return persistenceError; } }

        internal void Observe(IReadOnlyList<CharacterPresence> presences)
        {
            if (presences == null) throw new ArgumentNullException(nameof(presences));
            double mono = monotonicSeconds();
            long utc = utcUnixSeconds();
            lock (sync)
            {
                ThrowIfStopped();
                presentIds.Clear();
                for (int i = 0; i < presences.Count; i++)
                {
                    CharacterPresence presence = presences[i];
                    if (string.IsNullOrEmpty(presence.Id) || string.IsNullOrEmpty(presence.CharacterName))
                        throw new ArgumentException("Character ids and names must not be empty.", nameof(presences));
                    if (presence.Id.Length > MaximumIdLength || presence.CharacterName.Length > MaximumNameLength)
                        throw new ArgumentException("Character identity or name is too long.", nameof(presences));
                    if (!presentIds.Add(presence.Id)) continue;
                    if (!records.TryGetValue(presence.Id, out Record record))
                    {
                        if (records.Count >= MaximumCharacters)
                            throw new InvalidOperationException("Character leaderboard exceeds the 256-character export limit.");
                        record = new Record { Id = presence.Id, Name = presence.CharacterName ?? string.Empty };
                        records.Add(record.Id, record);
                    }
                    if (!record.Online)
                    {
                        record.Online = true;
                        record.Sessions++;
                        record.LastMono = mono;
                        record.LastCheckedMono = mono;
                        record.LastWall = record.Segments.Count == 0
                            ? utc
                            : Math.Max(utc, record.Segments[record.Segments.Count - 1].End);
                    }
                    record.Name = presence.CharacterName ?? string.Empty;
                    record.LastSeen = utc;
                }
                foreach (Record record in records.Values)
                {
                    if (record.Online && !presentIds.Contains(record.Id))
                    {
                        Settle(record, mono, utc);
                        record.Online = false;
                        record.LastSeen = utc;
                    }
                }
                dirty = true;
                generation++;
            }
        }

        internal CharacterLeaderboardEntry[] Snapshot()
        {
            double mono = monotonicSeconds();
            long utc = utcUnixSeconds();
            lock (sync)
            {
                var result = new CharacterLeaderboardEntry[records.Count];
                int index = 0;
                foreach (Record record in records.Values)
                {
                    double pending = record.Online ? Elapsed(record, mono) : 0;
                    double recent = Recent(record, utc);
                    if (pending > 0)
                    {
                        double cutoff = utc - SevenDaysSeconds;
                        double pendingStart = record.LastWall;
                        double pendingEnd = pendingStart + pending;
                        recent += Math.Max(0, Math.Min(pendingEnd, utc) - Math.Max(pendingStart, cutoff));
                    }
                    result[index++] = new CharacterLeaderboardEntry(record.Id, record.Name, record.AllTime + pending,
                        recent, record.Online, record.Sessions, record.LastSeen, record.Deaths);
                }
                Array.Sort(result, (left, right) => string.CompareOrdinal(left.Id, right.Id));
                dirty = true;
                generation++;
                return result;
            }
        }


        internal bool RecordDeath(string characterId, string eventId)
        {
            if (string.IsNullOrEmpty(characterId) || characterId.Length > MaximumIdLength)
                throw new ArgumentException("Character id is invalid.", nameof(characterId));
            if (string.IsNullOrEmpty(eventId) || eventId.Length > MaximumIdLength)
                throw new ArgumentException("Event id is invalid.", nameof(eventId));
            lock (sync)
            {
                ThrowIfStopped();
                if (!records.TryGetValue(characterId, out Record record) || recentDeathIds.Contains(eventId)) return false;
                long deaths = checked(record.Deaths + 1);
                record.Deaths = deaths;
                recentDeaths.Enqueue(eventId);
                recentDeathIds.Add(eventId);
                if (recentDeaths.Count > MaximumRecentDeaths) recentDeathIds.Remove(recentDeaths.Dequeue());
                dirty = true;
                generation++;
                return true;
            }
        }

        internal void Flush()
        {
            long request;
            lock (sync)
            {
                ThrowIfStopped();
                request = ++requestedFlush;
                wake.Set();
                while (completedFlush < request && !stopping) Monitor.Wait(sync);
                if (persistenceError != null) throw new IOException("Unable to persist character playtime.", persistenceError);
            }
        }

        public void Dispose()
        {
            Exception error;
            lock (sync)
            {
                if (stopping) return;
                requestedFlush++;
                stopping = true;
                wake.Set();
            }
            if (Thread.CurrentThread != worker) worker.Join();
            lock (sync) error = persistenceError;
            wake.Dispose();
            if (error != null) throw new IOException("Unable to persist character playtime during shutdown.", error);
        }

        private void WorkerLoop()
        {
            while (true)
            {
                wake.WaitOne(WriteInterval);
                long flush = 0;
                bool stop = false;
                long capturedGeneration = 0;
                Exception error = null;
                try
                {
                    PersistedState state;
                    lock (sync)
                    {
                        stop = stopping;
                        flush = requestedFlush;
                        if (!dirty && flush == completedFlush && !stop) continue;
                        double mono = monotonicSeconds();
                        long utc = utcUnixSeconds();
                        foreach (Record record in records.Values)
                            if (record.Online) Settle(record, mono, utc);
                        generation++;
                        capturedGeneration = generation;
                        state = Capture();
                    }
                    Write(path, state);
                }
                catch (Exception exception) { error = exception; }
                lock (sync)
                {
                    persistenceError = error;
                    if (error == null && capturedGeneration == generation) dirty = false;
                    completedFlush = flush;
                    Monitor.PulseAll(sync);
                    if (stop) return;
                }
            }
        }

        private PersistedState Capture()
        {
            var state = new PersistedState { Version = 1, Characters = new List<PersistedCharacter>(records.Count),
                RecentDeathIds = new List<string>(recentDeaths) };
            foreach (Record record in records.Values)
            {
                if (record.Segments.Count > MaximumSegments)
                    throw new InvalidDataException("Character playtime history exceeds the persistence limit.");
                var item = new PersistedCharacter { Id = record.Id, Name = record.Name, AllTime = record.AllTime,
                    Sessions = record.Sessions, LastSeen = record.LastSeen, Deaths = record.Deaths,
                    Segments = new List<PersistedSegment>(record.Segments.Count) };
                foreach (Segment segment in record.Segments)
                    item.Segments.Add(new PersistedSegment { Start = segment.Start, End = segment.End });
                state.Characters.Add(item);
            }
            return state;
        }

        private static LoadedState Load(string file)
        {
            var result = new LoadedState();
            EnsureSafePath(file);
            if (!File.Exists(file)) return result;
            PersistedState state;
            using (FileStream stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                if (stream.Length > MaximumFileBytes) throw new InvalidDataException("Character playtime file is too large.");
                state = (PersistedState)new DataContractJsonSerializer(typeof(PersistedState)).ReadObject(stream);
            }
            if (state == null || state.Version != 1 || state.Characters == null) throw new InvalidDataException("Invalid character playtime file.");
            if (state.Characters.Count > MaximumCharacters) throw new InvalidDataException("Too many character playtime records.");
            foreach (PersistedCharacter item in state.Characters)
            {
                if (item == null || string.IsNullOrEmpty(item.Id) || string.IsNullOrEmpty(item.Name) ||
                    item.Id.Length > MaximumIdLength || item.Name.Length > MaximumNameLength ||
                    item.LastSeen < 0 || item.AllTime < 0 || double.IsNaN(item.AllTime) ||
                    double.IsInfinity(item.AllTime) || item.Sessions < 0 || item.Deaths < 0 ||
                    result.Records.ContainsKey(item.Id))
                    throw new InvalidDataException("Invalid character playtime record.");
                var record = new Record { Id = item.Id, Name = item.Name ?? string.Empty, AllTime = item.AllTime,
                    Sessions = item.Sessions, LastSeen = item.LastSeen, Deaths = item.Deaths };
                double priorEnd = double.NegativeInfinity;
                double segmentSeconds = 0;
                if (item.Segments != null)
                {
                    if (item.Segments.Count > MaximumSegments) throw new InvalidDataException("Too many playtime segments.");
                    foreach (PersistedSegment segment in item.Segments)
                    {
                        if (segment == null || segment.End < segment.Start || segment.Start < priorEnd ||
                            double.IsNaN(segment.Start) || double.IsNaN(segment.End) ||
                            double.IsInfinity(segment.Start) || double.IsInfinity(segment.End))
                            throw new InvalidDataException("Invalid or overlapping playtime segment.");
                        record.Segments.Add(new Segment(segment.Start, segment.End));
                        priorEnd = segment.End;
                        segmentSeconds += segment.End - segment.Start;
                    }
                }
                if (segmentSeconds > item.AllTime + Math.Max(1, item.AllTime) * 1e-9)
                    throw new InvalidDataException("Retained playtime exceeds the all-time total.");
                result.Records.Add(record.Id, record);
            }
            if (state.RecentDeathIds != null)
            {
                if (state.RecentDeathIds.Count > MaximumRecentDeaths) throw new InvalidDataException("Too many retained death event ids.");
                var unique = new HashSet<string>(StringComparer.Ordinal);
                foreach (string eventId in state.RecentDeathIds)
                {
                    if (string.IsNullOrEmpty(eventId) || eventId.Length > MaximumIdLength || !unique.Add(eventId))
                        throw new InvalidDataException("Invalid retained death event id.");
                    result.RecentDeaths.Enqueue(eventId);
                }
            }
            return result;
        }

        private static void Write(string file, PersistedState state)
        {
            EnsureSafePath(file);
            string directory = Path.GetDirectoryName(Path.GetFullPath(file));
            Directory.CreateDirectory(directory);
            string temporary = file + ".tmp." + Guid.NewGuid().ToString("N");
            try
            {
                using (FileStream stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    new DataContractJsonSerializer(typeof(PersistedState)).WriteObject(stream, state);
                    if (stream.Length > MaximumFileBytes)
                        throw new InvalidDataException("Character playtime output exceeds the persistence limit.");
                    stream.Flush(true);
                }
                if (File.Exists(file)) File.Replace(temporary, file, null);
                else File.Move(temporary, file);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }

        private static void EnsureSafePath(string file)
        {
            string fullPath = Path.GetFullPath(file);
            string candidate = fullPath;
            while (!string.IsNullOrEmpty(candidate))
            {
                try
                {
                    if ((File.GetAttributes(candidate) & FileAttributes.ReparsePoint) != 0)
                        throw new IOException("Character playtime path must not contain symbolic links.");
                }
                catch (FileNotFoundException) { }
                catch (DirectoryNotFoundException) { }
                string parent = Path.GetDirectoryName(candidate);
                if (string.IsNullOrEmpty(parent) || string.Equals(parent, candidate, StringComparison.Ordinal)) break;
                candidate = parent;
            }
        }

        private static double DefaultMonotonicSeconds()
        {
            return (double)Stopwatch.GetTimestamp() / Stopwatch.Frequency;
        }

        private static long DefaultUtcUnixSeconds()
        {
            return DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }

        private static double Elapsed(Record record, double mono)
        {
            if (double.IsNaN(mono) || double.IsInfinity(mono)) return 0;
            if (mono < record.LastCheckedMono)
            {
                Commit(record, record.LastCheckedMono - record.LastMono);
                record.LastMono = mono;
                record.LastCheckedMono = mono;
                return 0;
            }
            record.LastCheckedMono = mono;
            double elapsed = mono - record.LastMono;
            return elapsed > 0 ? elapsed : 0;
        }

        private static void Settle(Record record, double mono, long utc)
        {
            if (double.IsNaN(mono) || double.IsInfinity(mono)) return;
            if (mono < record.LastCheckedMono)
            {
                Commit(record, record.LastCheckedMono - record.LastMono);
                record.LastMono = mono;
                record.LastCheckedMono = mono;
                return;
            }
            double elapsed = mono - record.LastMono;
            record.LastMono = mono;
            record.LastCheckedMono = mono;
            Commit(record, elapsed);
        }

        private static void Commit(Record record, double elapsed)
        {
            if (elapsed <= 0 || double.IsNaN(elapsed) || double.IsInfinity(elapsed)) return;
            double start = record.LastWall;
            double end = start + elapsed;
            record.AllTime += elapsed;
            int last = record.Segments.Count - 1;
            if (last >= 0 && record.Segments[last].End == start)
                record.Segments[last] = new Segment(record.Segments[last].Start, end);
            else
                record.Segments.Add(new Segment(start, end));
            record.LastWall = end;
        }

        private static double Recent(Record record, long utc)
        {
            double cutoff = utc - SevenDaysSeconds;
            double total = 0;
            for (int index = record.Segments.Count - 1; index >= 0; index--)
            {
                Segment segment = record.Segments[index];
                if (segment.End <= cutoff)
                {
                    record.Segments.RemoveAt(index);
                    continue;
                }
                double start = Math.Max(segment.Start, cutoff);
                double end = Math.Min(segment.End, utc);
                if (end > start) total += end - start;
            }
            return total;
        }

        private void ThrowIfStopped() { if (stopping) throw new ObjectDisposedException(nameof(CharacterLeaderboard)); }

        private sealed class Record
        {
            internal string Id; internal string Name; internal double AllTime; internal long Sessions; internal long? LastSeen;
            internal long Deaths;
            internal bool Online; internal double LastMono; internal double LastCheckedMono; internal double LastWall;
            internal readonly List<Segment> Segments = new List<Segment>();
        }
        private readonly struct Segment { internal Segment(double start, double end) { Start = start; End = end; } internal double Start { get; } internal double End { get; } }
        private sealed class LoadedState
        {
            internal readonly Dictionary<string, Record> Records = new Dictionary<string, Record>(StringComparer.Ordinal);
            internal readonly Queue<string> RecentDeaths = new Queue<string>();
        }

        [DataContract] private sealed class PersistedState
        {
            [DataMember(Name = "version")] internal int Version;
            [DataMember(Name = "characters")] internal List<PersistedCharacter> Characters;
            [DataMember(Name = "recent_death_event_ids", EmitDefaultValue = false)] internal List<string> RecentDeathIds;
        }
        [DataContract] private sealed class PersistedCharacter
        {
            [DataMember(Name = "id")] internal string Id; [DataMember(Name = "name")] internal string Name;
            [DataMember(Name = "all_time_seconds")] internal double AllTime; [DataMember(Name = "sessions")] internal long Sessions;
            [DataMember(Name = "last_seen_unix_seconds", EmitDefaultValue = false)] internal long? LastSeen;
            [DataMember(Name = "deaths", EmitDefaultValue = false)] internal long Deaths;
            [DataMember(Name = "segments")] internal List<PersistedSegment> Segments;
        }
        [DataContract] private sealed class PersistedSegment { [DataMember(Name = "start")] internal double Start; [DataMember(Name = "end")] internal double End; }
    }
}
