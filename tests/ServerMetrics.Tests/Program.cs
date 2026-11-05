using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using ValheimServerMetrics;

var tests = new (string Name, Func<Task> Run)[]
{
    ("session transitions and injected clocks", SessionTransitionsAndClocks),
    ("save and backup concrete outcomes", SaveAndBackupOutcomes),
    ("unknown values and counter reset", UnknownValuesAndReset),
    ("bounded labels escaping and culture", LabelsEscapingAndCulture),
    ("backup inventory replacement and availability", BackupInventoryState),
    ("mod inventory replacement validation and escaping", ModInventoryState),
    ("filesystem backup inventory", InventoryTests.Run),
    ("filesystem modpack manifest", ModpackManifestTests.Run),
    ("persistent character playtime and rolling window", CharacterLeaderboardTests.Run),
    ("character leaderboard metric identity and privacy", CharacterMetrics),
    ("HTTP binding validation", HttpBindingValidation),
    ("real HTTP routing and methods", HttpRoutingAndMethods),
    ("HTTP bind backoff and recovery preserves telemetry", HttpBindRetryRecovery),
    ("source-IP access control", HttpSourceAccessControl),
    ("parallel scrapes and disposal rebind", ParallelScrapesAndDisposal),
};

int failures = 0;
foreach ((string name, Func<Task> run) in tests)
{
    try
    {
        await run();
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

static Task SessionTransitionsAndClocks()
{
    double monotonic = 100;
    double unix = 1_700_000_000;
    var state = new MetricsState("test", () => monotonic, () => unix);

    state.ObservePlayers(2, 2);
    Equal(2d, Sample(state, "valheim_connected_players"));
    Equal(2d, Sample(state, "valheim_sessions_total"));
    Equal(0d, Sample(state, "valheim_player_connected_seconds_total"));
    Equal(unix, Sample(state, "valheim_exporter_last_game_update_timestamp_seconds"));

    monotonic += 5;
    Equal(10d, Sample(state, "valheim_player_connected_seconds_total"));
    Equal(5d, Sample(state, "valheim_exporter_uptime_seconds"));

    unix += 5;
    state.ObservePlayers(2, 0); // A duplicate refresh integrates time but not sessions.
    Equal(10d, Sample(state, "valheim_player_connected_seconds_total"));
    Equal(2d, Sample(state, "valheim_sessions_total"));

    monotonic += 3;
    unix += 3;
    state.ObservePlayers(1, 0);
    Equal(16d, Sample(state, "valheim_player_connected_seconds_total"));
    monotonic += 4;
    state.ObservePlayers(0, 0);
    Equal(20d, Sample(state, "valheim_player_connected_seconds_total"));
    monotonic += 20;
    Equal(20d, Sample(state, "valheim_player_connected_seconds_total"));
    Equal(0d, Sample(state, "valheim_connected_players"));
    return Task.CompletedTask;
}

static Task SaveAndBackupOutcomes()
{
    double monotonic = 0;
    double unix = 2_000;
    var state = new MetricsState("test", () => monotonic, () => unix);

    state.BeginWorldSave();
    Equal(1d, Sample(state, "valheim_world_save_attempts_total"));
    Equal(1d, Sample(state, "valheim_world_save_in_progress"));
    state.CompleteWorldSave(false, 1.25);
    Equal(1d, Sample(state, "valheim_world_saves_total", "result=\"failure\""));
    Equal(0d, Sample(state, "valheim_world_saves_total", "result=\"success\""));
    Equal(1.25d, Sample(state, "valheim_world_save_worker_duration_seconds"));
    Throws<ArgumentOutOfRangeException>(() => state.CompleteWorldSave(false, double.NaN));
    Throws<ArgumentOutOfRangeException>(() => state.CompleteWorldSave(false, double.PositiveInfinity));
    Throws<ArgumentOutOfRangeException>(() => state.CompleteWorldSave(false, -0.01));
    Missing(state, "valheim_world_last_save_timestamp_seconds");

    unix = 2_100;
    state.BeginWorldSave();
    state.CompleteWorldSave(true, 2.5);
    Equal(2d, Sample(state, "valheim_world_save_attempts_total"));
    Equal(1d, Sample(state, "valheim_world_saves_total", "result=\"success\""));
    Equal(2.5d, Sample(state, "valheim_world_save_worker_duration_seconds"));
    Equal(2_100d, Sample(state, "valheim_world_last_save_timestamp_seconds"));
    Equal(0d, Sample(state, "valheim_world_save_in_progress"));

    unix = 2_200;
    state.RecordAutoBackup();
    Equal(1d, Sample(state, "valheim_world_auto_backups_total"));
    Equal(2_200d, Sample(state, "valheim_world_last_auto_backup_timestamp_seconds"));
    return Task.CompletedTask;
}

static Task UnknownValuesAndReset()
{
    var first = new MetricsState("test", () => 0, () => 500);
    Missing(first, "valheim_server_info");
    Missing(first, "valheim_connected_players");
    Missing(first, "valheim_exporter_last_game_update_timestamp_seconds");
    Missing(first, "valheim_world_save_worker_duration_seconds");
    Missing(first, "valheim_world_last_save_timestamp_seconds");
    Missing(first, "valheim_world_last_auto_backup_timestamp_seconds");
    first.ObservePlayers(1, 1);
    first.BeginWorldSave();
    first.CompleteWorldSave(true, 3);
    first.RecordAutoBackup();

    var restarted = new MetricsState("test", () => 0, () => 600);
    Equal(0d, Sample(restarted, "valheim_sessions_total"));
    Equal(0d, Sample(restarted, "valheim_world_save_attempts_total"));
    Equal(0d, Sample(restarted, "valheim_world_auto_backups_total"));
    Missing(restarted, "valheim_world_last_save_timestamp_seconds");
    Missing(restarted, "valheim_world_last_auto_backup_timestamp_seconds");
    return Task.CompletedTask;
}

static Task LabelsEscapingAndCulture()
{
    CultureInfo original = CultureInfo.CurrentCulture;
    try
    {
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
        double monotonic = 0.5;
        var state = new MetricsState("v\\\"\n", () => monotonic, () => 10.25);
        state.SetWorld("fjord\\\"\n", "game\\\"\n", "server\\\"\n");
        monotonic = 1.0;
        string text = Snapshot(state);
        Contains(text, "world=\"fjord\\\\\\\"\\n\"");
        Contains(text, "game_version=\"game\\\\\\\"\\n\"");
        Contains(text, "server=\"server\\\\\\\"\\n\"");
        Contains(text, "exporter_version=\"v\\\\\\\"\\n\"");
        Contains(text, "valheim_exporter_uptime_seconds 0.5");
        state.SetWorld(new string('w', 256), new string('g', 256), new string('s', 256));
        string replacement = Snapshot(state);
        Contains(replacement, new string('w', 256));
        Equal(1, replacement.Split('\n').Count(line => line.StartsWith("valheim_server_info{", StringComparison.Ordinal)));
        False(replacement.Contains("fjord", StringComparison.Ordinal));
        Throws<ArgumentException>(() => state.SetWorld(new string('w', 257), "game", "server"));
        Throws<ArgumentException>(() => state.SetWorld("world", new string('g', 257), "server"));
        Throws<ArgumentException>(() => state.SetWorld("world", "game", new string('s', 257)));
        state.SetWorld(string.Empty, "ignored", "ignored");
        Missing(state, "valheim_server_info");
    }
    finally
    {
        CultureInfo.CurrentCulture = original;
    }
    return Task.CompletedTask;
}

static Task BackupInventoryState()
{
    double unix = 1_700_000_100.25;
    var state = new MetricsState("test", () => 0, () => unix);
    Missing(state, "valheim_world_backup_inventory_success");
    Missing(state, "valheim_world_backups");
    Missing(state, "valheim_world_backup_inventory_timestamp_seconds");

    var input = new[]
    {
        new BackupEntry("world_backup_auto-20261029-120000", 10.5, null),
        new BackupEntry("a\\\"\n", null, 20.25),
        new BackupEntry("unknown-dates", null, null),
    };
    state.SetBackupInventory(input, 30.75);
    input[0] = new BackupEntry("mutated", 99, 99);
    Equal(1d, Sample(state, "valheim_world_backup_inventory_success"));
    Equal(3d, Sample(state, "valheim_world_backups"));
    Equal(unix, Sample(state, "valheim_world_backup_inventory_timestamp_seconds"));
    Equal(30.75d, Sample(state, "valheim_world_save_file_timestamp_seconds"));
    Equal(10.5d, Sample(state, "valheim_world_backup_created_timestamp_seconds", "backup=\"world_backup_auto-20261029-120000\""));
    Equal(20.25d, Sample(state, "valheim_world_backup_data_timestamp_seconds", "backup=\"a\\\\\\\"\\n\""));
    Equal(1d, Sample(state, "valheim_world_backup_info", "backup=\"unknown-dates\""));
    False(Snapshot(state).Contains("mutated", StringComparison.Ordinal));

    unix++;
    state.SetBackupInventory(Array.Empty<BackupEntry>(), null);
    Equal(0d, Sample(state, "valheim_world_backups"));
    Equal(unix, Sample(state, "valheim_world_backup_inventory_timestamp_seconds"));
    Missing(state, "valheim_world_backup_created_timestamp_seconds");
    Missing(state, "valheim_world_backup_data_timestamp_seconds");
    Missing(state, "valheim_world_save_file_timestamp_seconds");
    Missing(state, "valheim_world_backup_info");

    state.SetBackupInventory(new[] { new BackupEntry("current", null, null) }, 41);
    Equal(1d, Sample(state, "valheim_world_backup_info", "backup=\"current\""));
    double lastSuccess = Sample(state, "valheim_world_backup_inventory_timestamp_seconds");
    state.MarkBackupInventoryUnavailable();
    Equal(0d, Sample(state, "valheim_world_backup_inventory_success"));
    Equal(lastSuccess, Sample(state, "valheim_world_backup_inventory_timestamp_seconds"));
    Missing(state, "valheim_world_backups");
    Missing(state, "valheim_world_backup_info");
    Missing(state, "valheim_world_backup_data_timestamp_seconds");
    Missing(state, "valheim_world_save_file_timestamp_seconds");

    Throws<ArgumentException>(() => state.SetBackupInventory(new[]
    {
        new BackupEntry("same", null, null),
        new BackupEntry("same", null, null),
    }, null));
    Throws<ArgumentOutOfRangeException>(() => state.SetBackupInventory(
        Enumerable.Range(0, 129).Select(index => new BackupEntry(index.ToString(CultureInfo.InvariantCulture), null, null)).ToArray(), null));
    Throws<ArgumentOutOfRangeException>(() => state.SetBackupInventory(new[] { new BackupEntry("bad", -1, null) }, null));
    Throws<ArgumentOutOfRangeException>(() => state.SetBackupInventory(new[] { new BackupEntry("bad", null, double.NaN) }, null));
    Throws<ArgumentOutOfRangeException>(() => state.SetBackupInventory(Array.Empty<BackupEntry>(), double.PositiveInfinity));
    Throws<ArgumentOutOfRangeException>(() => state.SetBackupInventory(new[] { new BackupEntry(new string('b', 257), null, null) }, null));
    return Task.CompletedTask;
}

static Task ModInventoryState()
{
    var state = new MetricsState("test", () => 0, () => 0);
    var input = new[]
    {
        new ModEntry("z", "Zulu", "2"),
        new ModEntry("a\\\"\n", "Alpha\\\"\n", "1.2\\\"\n"),
    };
    state.SetMods(input);
    input[0] = new ModEntry("mutated", "mutated", "mutated");
    string first = Snapshot(state);
    Contains(first, "valheim_server_mod_info{guid=\"a\\\\\\\"\\n\",name=\"Alpha\\\\\\\"\\n\",version=\"1.2\\\\\\\"\\n\"} 1");
    False(first.Contains("mutated", StringComparison.Ordinal));
    True(first.IndexOf("guid=\"a", StringComparison.Ordinal) < first.IndexOf("guid=\"z", StringComparison.Ordinal));

    state.SetMods(new[] { new ModEntry("current", "Current", "3") });
    string replacement = Snapshot(state);
    Contains(replacement, "guid=\"current\"");
    False(replacement.Contains("guid=\"z\"", StringComparison.Ordinal));
    state.SetMods(Array.Empty<ModEntry>());
    Missing(state, "valheim_server_mod_info");

    Throws<ArgumentException>(() => state.SetMods(new[]
    {
        new ModEntry("same", "one", "1"),
        new ModEntry("same", "two", "2"),
    }));
    Throws<ArgumentOutOfRangeException>(() => state.SetMods(
        Enumerable.Range(0, 129).Select(index => new ModEntry(index.ToString(CultureInfo.InvariantCulture), "name", "version")).ToArray()));
    Throws<ArgumentOutOfRangeException>(() => state.SetMods(new[] { new ModEntry(new string('g', 257), "name", "version") }));
    Throws<ArgumentException>(() => state.SetMods(new[] { new ModEntry("guid", "", "version") }));
    return Task.CompletedTask;
}

static Task CharacterMetrics()
{
    var state = new MetricsState("test");
    Missing(state, "valheim_character_storage_success");
    state.SetCharacters(new[]
    {
        new CharacterLeaderboardEntry("character-a", "Same \"Name\"", 7200, 1800, true, 3, 1700000000, 2),
        new CharacterLeaderboardEntry("character-b", "Same \"Name\"", 3600, 300, false, 1, null),
    }, true);
    string text = Snapshot(state);
    Contains(text, "valheim_character_playtime_seconds{character_id=\"character-a\",character=\"Same \\\"Name\\\"\"} 7200\n");
    Contains(text, "valheim_character_playtime_seconds{character_id=\"character-b\",character=\"Same \\\"Name\\\"\"} 3600\n");
    Contains(text, "valheim_character_playtime_7d_seconds{character_id=\"character-a\",character=\"Same \\\"Name\\\"\"} 1800\n");
    Contains(text, "valheim_character_online{character_id=\"character-b\",character=\"Same \\\"Name\\\"\"} 0\n");
    Contains(text, "valheim_character_deaths_total{character_id=\"character-a\",character=\"Same \\\"Name\\\"\"} 2\n");
    False(text.Split('\n').Any(line => line.StartsWith("valheim_character_last_seen_timestamp_seconds{character_id=\"character-b\"")));
    False(text.Contains("steam", StringComparison.OrdinalIgnoreCase));
    state.SetCharacters(new[] { new CharacterLeaderboardEntry("character-a", "Renamed", 7300, 1900, false, 3, 1700000100) }, false);
    text = Snapshot(state);
    Contains(text, "valheim_character_storage_success 0\n");
    Contains(text, "valheim_character_playtime_seconds{character_id=\"character-a\",character=\"Renamed\"} 7300\n");
    False(text.Contains("Same", StringComparison.Ordinal));
    Throws<ArgumentException>(() => state.SetCharacters(new[]
    {
        new CharacterLeaderboardEntry("duplicate", "A", 1, 1, false, 0, null),
        new CharacterLeaderboardEntry("duplicate", "B", 1, 1, false, 0, null),
    }, true));
    return Task.CompletedTask;
}

static async Task HttpBindRetryRecovery()
{
    using var blocker = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
    blocker.Bind(new IPEndPoint(IPAddress.Loopback, 0));
    blocker.Listen(1);
    int port = ((IPEndPoint)blocker.LocalEndPoint).Port;
    var state = new MetricsState("test");
    state.ObservePlayers(1, 1);
    using var server = new MetricsHttpServer(state, "127.0.0.1", port);

    foreach (var attempt in new[] { (Time: 0d, Delay: 60d), (Time: 60d, Delay: 300d),
        (Time: 360d, Delay: 1800d), (Time: 2160d, Delay: 1800d) })
    {
        False(server.StartWhenDue(attempt.Time, out var failure, out var delay));
        True(failure is SocketException || failure is HttpListenerException);
        Equal(attempt.Delay, delay);
        False(server.StartWhenDue(attempt.Time + attempt.Delay - 0.01, out failure, out delay));
        True(failure == null);
        Equal(0d, delay);
    }

    // Observation continues while the endpoint is unavailable.
    state.ObservePlayers(2, 1);
    state.BeginWorldSave();
    state.CompleteWorldSave(true, 0.25);
    blocker.Dispose();
    True(server.StartWhenDue(3960, out var recoveredError, out var recoveredDelay));
    True(recoveredError == null);
    Equal(0d, recoveredDelay);
    using var client = Client();
    string body = await client.GetStringAsync($"http://127.0.0.1:{port}/metrics");
    Contains(body, "valheim_connected_players 2\n");
    Contains(body, "valheim_sessions_total 2\n");
    Contains(body, "valheim_world_saves_total{result=\"success\"} 1\n");
    False(server.StartWhenDue(5760, out var laterError, out _));
    True(laterError == null);
    server.Dispose();
    Throws<ObjectDisposedException>(() => server.StartWhenDue(7560, out _, out _));

    using var pendingBlocker = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
    pendingBlocker.Bind(new IPEndPoint(IPAddress.Loopback, port));
    pendingBlocker.Listen(1);
    using var pending = new MetricsHttpServer(state, "127.0.0.1", port);
    False(pending.StartWhenDue(0, out _, out _));
    pending.Dispose();
    pendingBlocker.Dispose();
    Throws<ObjectDisposedException>(() => pending.StartWhenDue(60, out _, out _));
}

static async Task HttpRoutingAndMethods()
{
    int port = ReservePort();
    var state = new MetricsState("test");
    state.ObservePlayers(0, 0);
    using var server = new MetricsHttpServer(state, "127.0.0.1", port);
    server.Start();
    using var client = Client();
    string root = $"http://127.0.0.1:{port}";

    using HttpResponseMessage get = await client.GetAsync(root + "/metrics");
    Equal(HttpStatusCode.OK, get.StatusCode);
    Equal("text/plain", get.Content.Headers.ContentType.MediaType);
    Contains(get.Content.Headers.ContentType.ToString(), "version=0.0.4");
    byte[] body = await get.Content.ReadAsByteArrayAsync();
    Equal(get.Content.Headers.ContentLength.Value, (long)body.Length);
    Contains(Encoding.UTF8.GetString(body), "valheim_connected_players 0\n");

    using var headRequest = new HttpRequestMessage(HttpMethod.Head, root + "/metrics/");
    using HttpResponseMessage head = await client.SendAsync(headRequest);
    Equal(HttpStatusCode.OK, head.StatusCode);
    True(head.Content.Headers.ContentLength > 0);
    Equal(0, (await head.Content.ReadAsByteArrayAsync()).Length);

    using HttpResponseMessage missing = await client.GetAsync(root + "/");
    Equal(HttpStatusCode.NotFound, missing.StatusCode);
    using HttpResponseMessage nested = await client.GetAsync(root + "/metrics/child");
    Equal(HttpStatusCode.NotFound, nested.StatusCode);
    using HttpResponseMessage traversal = await client.GetAsync(root + "/secret?path=../../etc/passwd");
    Equal(HttpStatusCode.NotFound, traversal.StatusCode);
    using HttpResponseMessage query = await client.GetAsync(root + "/metrics?path=../../etc/passwd");
    Equal(HttpStatusCode.OK, query.StatusCode);
    Contains(await query.Content.ReadAsStringAsync(), "valheim_connected_players");
    using HttpResponseMessage post = await client.PostAsync(root + "/metrics", new StringContent("ignored"));
    Equal(HttpStatusCode.MethodNotAllowed, post.StatusCode);
    True(HasHeaderValue(post, "Allow", "GET"));
    True(HasHeaderValue(post, "Allow", "HEAD"));
}

static Task HttpBindingValidation()
{
    var state = new MetricsState("test");
    Throws<ArgumentException>(() => new MetricsHttpServer(state, "localhost", 9101));
    Throws<ArgumentException>(() => new MetricsHttpServer(state, "0.0.0.0", 9101));
    Throws<ArgumentException>(() => new MetricsHttpServer(state, "::", 9101));
    Throws<ArgumentException>(() => new MetricsHttpServer(state, "224.0.0.1", 9101));
    Throws<ArgumentException>(() => new MetricsHttpServer(state, "ff02::1", 9101));
    Throws<ArgumentException>(() => new MetricsHttpServer(state, "::ffff:224.0.0.1", 9101));
    Throws<ArgumentException>(() => new MetricsHttpServer(state, "192.0.2.10", 9101));
    Throws<ArgumentException>(() => new MetricsHttpServer(state, "127.0.0.1", 9101, "localhost"));
    Throws<ArgumentException>(() => new MetricsHttpServer(state, "127.0.0.1", 9101, "0.0.0.0"));
    Throws<ArgumentException>(() => new MetricsHttpServer(state, "127.0.0.1", 9101, "::ffff:224.0.0.1"));
    Throws<ArgumentOutOfRangeException>(() => new MetricsHttpServer(state, "127.0.0.1", 0));
    Throws<ArgumentOutOfRangeException>(() => new MetricsHttpServer(state, "127.0.0.1", 65536));
    return Task.CompletedTask;
}

static async Task HttpSourceAccessControl()
{
    int port = ReservePort();
    var state = new MetricsState("test");
    state.ObservePlayers(2, 2);
    using var server = new MetricsHttpServer(state, "127.0.0.1", port, "::ffff:127.0.0.1");
    server.Start();
    string url = $"http://127.0.0.1:{port}/metrics";
    using var allowed = Client();
    Contains(await allowed.GetStringAsync(url), "valheim_connected_players 2\n");
    using var rejected = new HttpClient(new SocketsHttpHandler
    {
        UseProxy = false,
        ConnectCallback = async (context, cancellationToken) =>
        {
            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            try
            {
                socket.Bind(new IPEndPoint(IPAddress.Parse("127.0.0.2"), 0));
                await socket.ConnectAsync(context.DnsEndPoint, cancellationToken);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch { socket.Dispose(); throw; }
        }
    }) { Timeout = TimeSpan.FromSeconds(5) };
    using var request = new HttpRequestMessage(HttpMethod.Get, url);
    request.Headers.Add("X-Forwarded-For", "127.0.0.1");
    using var response = await rejected.SendAsync(request);
    Equal(HttpStatusCode.Forbidden, response.StatusCode);
    Equal(0, (await response.Content.ReadAsByteArrayAsync()).Length);
}

static async Task ParallelScrapesAndDisposal()
{
    int port = ReservePort();
    var state = new MetricsState("test");
    state.ObservePlayers(3, 4);
    var server = new MetricsHttpServer(state, "127.0.0.1", port);
    server.Start();
    using var client = Client();
    string url = $"http://127.0.0.1:{port}/metrics";
    string[] bodies = await Task.WhenAll(Enumerable.Range(0, 24).Select(_ => client.GetStringAsync(url)));
    True(bodies.All(body =>
        body.Split('\n').Contains("valheim_connected_players 3") &&
        body.Split('\n').Contains("valheim_sessions_total 4")));
    server.Dispose();
    server.Dispose();

    using var rebound = new MetricsHttpServer(new MetricsState("second"), "127.0.0.1", port);
    rebound.Start();
    Contains(await client.GetStringAsync(url), "valheim_sessions_total 0\n");
}

static HttpClient Client() => new(new SocketsHttpHandler { UseProxy = false }) { Timeout = TimeSpan.FromSeconds(5) };

static bool HasHeaderValue(HttpResponseMessage response, string name, string expected)
{
    IEnumerable<string> values;
    if (!response.Headers.TryGetValues(name, out values) &&
        !response.Content.Headers.TryGetValues(name, out values))
        return false;
    return values.SelectMany(value => value.Split(',')).Any(value =>
        string.Equals(value.Trim(), expected, StringComparison.OrdinalIgnoreCase));
}

static int ReservePort()
{
    var listener = new TcpListener(IPAddress.Loopback, 0);
    listener.Start();
    int port = ((IPEndPoint)listener.LocalEndpoint).Port;
    listener.Stop();
    return port;
}

static double Sample(MetricsState state, string metric, string label = null)
{
    string prefix = label == null ? metric + " " : metric + "{";
    string line = Snapshot(state).Split('\n').SingleOrDefault(value =>
        value.StartsWith(prefix, StringComparison.Ordinal) && (label == null || value.Contains(label, StringComparison.Ordinal)));
    if (line == null)
        throw new InvalidOperationException($"Missing sample {metric}{(label == null ? "" : " " + label)}.");
    return double.Parse(line[(line.LastIndexOf(' ') + 1)..], CultureInfo.InvariantCulture);
}

static void Missing(MetricsState state, string metric)
{
    if (Snapshot(state).Split('\n').Any(line => line.StartsWith(metric + " ", StringComparison.Ordinal) || line.StartsWith(metric + "{", StringComparison.Ordinal)))
        throw new InvalidOperationException($"Expected {metric} to be absent.");
}

static string Snapshot(MetricsState state) => Encoding.UTF8.GetString(state.GetSnapshot());
static void Contains(string actual, string expected)
{
    if (!actual.Contains(expected, StringComparison.Ordinal))
        throw new InvalidOperationException($"Expected text to contain '{expected}'.");
}
static void Equal<T>(T expected, T actual)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"Expected {expected}, got {actual}.");
}
static void True(bool value)
{
    if (!value) throw new InvalidOperationException("Expected true, got false.");
}
static void False(bool value)
{
    if (value) throw new InvalidOperationException("Expected false, got true.");
}
static void Throws<T>(Action action) where T : Exception
{
    try { action(); }
    catch (T) { return; }
    throw new InvalidOperationException($"Expected {typeof(T).Name}.");
}
