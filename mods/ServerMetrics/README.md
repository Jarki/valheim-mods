# Valheim Server Metrics

**Paused by the owner (2026-11-05): no further development or publication for
now.** Source/tests and the historical notes below are preserved. This pause
does not authorize changing an existing server deployment or its monitoring.

Server-only BepInEx 5 plugin exposing Prometheus metrics at `/metrics`.
Original implementation; no Valheim Stats code or dependency. Playtime/deaths are server-only.
**No inferred gameplay statistics:** connected playtime and vanilla death
notifications are observed explicitly; kills and damage attribution are not tracked.
The character-name leaderboard records observed connected time in its own ledger,
outside world saves. No Steam usernames/account IDs, chat commands, or external
identity lookups. No client mod or client-profile change is required.

## Build

Uses the repository's existing native game and BepInEx profile as read-only assembly
references. Requires a .NET SDK supporting C# 10 and `netstandard2.1` (built here
with SDK 6.0.428). No game, Unity, BepInEx, or Harmony DLLs are bundled.

```sh
$HOME/.dotnet/dotnet build mods/ServerMetrics/ServerMetrics.csproj -c Release
$HOME/.dotnet/dotnet run --project tests/ServerMetrics.Tests/ServerMetrics.Tests.csproj -c Release
```

Output: `bin/Release/netstandard2.1/ServerMetrics.dll`. Override `GameDir`,
`ManagedDir`, or `ReferenceProfile` with MSBuild properties as needed. For dedicated
server references, set `ManagedDir` to its `valheim_server_Data/Managed` directory.
The build never copies the plugin into a game installation.

The plugin is filtered to `valheim_server.exe` / `valheim_server.x86_64` and uses
BepInEx/Harmony supplied by the host. Production installation, restarts, and test
approval boundaries belong to the [server-management skill](../../../valheim-server/.agents/skills/server-management/SKILL.md).
No production deployment or server test is implied by this build.

## Endpoint and configuration

BepInEx generates `BepInEx/config/local.valheim.servermetrics.cfg`:

```ini
[Exporter]
BindAddress = 127.0.0.1
Port = 9101
AllowedScraperAddress =
ModpackManifestPath =
```

`CharacterPlaytimePath` defaults to
`<BepInEx>/data/ServerMetrics/character-playtime.json`, with the actual absolute
installation path generated in the config. Set a persistent, writable path outside
world saves; the managed deployment uses `/var/lib/valheim/servermetrics/character-playtime.json`.
Changing the path requires a restart; move the existing ledger deliberately to
retain totals.

Settings take effect on plugin startup. Only explicit IP literals are accepted;
wildcard, unspecified, and multicast addresses are rejected. Empty
`AllowedScraperAddress` permits loopback clients only and requires a loopback bind.
A non-loopback bind requires one explicit allowed scraper IP; other actual peer
addresses receive HTTP 403 before any metrics are rendered. IPv4-mapped IPv6 peers
are normalized. Proxy headers cannot override the source check. Use a private
Tailscale binding for direct remote scraping; no SSH tunnel is required.
The endpoint provides **plain HTTP without user authentication or TLS**; do not
expose it publicly. Source filtering is not a substitute for network security.

`GET /metrics` or `/metrics/` returns Prometheus text; `HEAD` returns headers only.
Other paths return 404; other methods return 405. Two fixed HTTP workers consume
cached, bounded snapshots without calling Unity APIs. No request-controlled labels,
files, commands, or writes. Repeated scrapes do not create retained time series.
Prometheus adds target labels such as `job` and `instance` itself.

The game thread refreshes ready-peer state once per second and server/world/mod
metadata once per minute. Connection events update player state immediately.
Loaded mods come from BepInEx's runtime registry, not package manifests; bundled
plugin versions can differ from package versions. Network destruction clears the
current world inventory. HTTP snapshots cache for up to one second and invalidate
on telemetry updates. HTTP bind failures retry after 1 minute, then
5 minutes, then every 30 minutes until the listener starts. Each failed attempt
logs the next delay; skipped checks do not log. Telemetry and inventory collection
continue while binding is unavailable, preserving counters when HTTP recovers.
No wildcard binding or source-IP fallback is used. Invalid configuration and
game-observation failures still disable the endpoint rather than serving
apparently healthy stale game observations. Plugin destruction cancels pending
retries. Retry timing uses the game's monotonic real-time clock and is checked
once per observation tick (approximately one second), without a sleeping thread.

A separate worker reads native chunked-world backup metadata every 60 seconds and
after observed saves/backups. It never reads world contents, copies saves, or calls
Unity APIs. It rejects symlinks, oversized inventories, and observed rotation races.
An unreadable inventory becomes unavailable, not zero; its last-success scan time
remains available. There is no atomic filesystem snapshot or restore validation.
Monitor Prometheus `up`, game-observation freshness, and inventory scan status.

## Concrete metrics

Aggregate connection/save counters cover observations **since this exporter
instance started** and reset on restart; use Prometheus `increase`/`rate` for
interval totals. Character leaderboard totals and session counts are separately
persisted across restarts. All playtime means connected time, including AFK—not
active gameplay. Character names and opaque character-profile keys are exported;
no Steam usernames/account IDs are read or exported.

| Metric | Type | Direct source / meaning |
| --- | --- | --- |
| `valheim_server_info` | Gauge | Value 1 with current `server`, `world`, `game_version`, and `exporter_version` labels; omitted until a world name is available. Server name is the game's configured display name. |
| `valheim_connected_players` | Gauge | Current dedicated-server peers for which `ZNetPeer.IsReady()` is true. Excludes handshake-only peers; omitted until a server observation exists. |
| `valheim_sessions_total` | Counter | Ready connections first observed by this exporter, deduplicated by peer object for each connection; reconnects count again. Not unique people. |
| `valheim_player_connected_seconds_total` | Counter | Monotonic elapsed time multiplied by the observed ready-player count, integrated at connection transitions and scrapes. Two players connected for 60 seconds contribute 120. |
| `valheim_exporter_uptime_seconds` | Gauge | Monotonic elapsed time since this exporter initialized, not server network availability. |
| `valheim_exporter_last_game_update_timestamp_seconds` | Gauge | UTC time of the last completed ready-player observation. Omitted before the first observation. |
| `valheim_world_save_attempts_total` | Counter | Actual entries into `ZNet.SaveWorldThread`, not requested or skipped saves. |
| `valheim_world_saves_total` | Counter | Fixed `result="success"` / `"failure"` samples. Success requires `SaveSystem.EndSave(writeOK: true)` returning inside that worker and the worker exiting without an unhandled exception. Other completed attempts are failures. |
| `valheim_world_save_in_progress` | Gauge | Observed save workers entered but not yet completed. |
| `valheim_world_save_worker_duration_seconds` | Gauge | Measured duration of the last completed save worker, including its backup checks; excludes main-thread save preparation. Omitted until a worker completes. |
| `valheim_world_last_save_timestamp_seconds` | Gauge | UTC completion time of the last observed successful worker. Omitted until success is observed. |
| `valheim_world_auto_backups_total` | Counter | World-only `ZNet.ConsiderAutoBackup` calls returning true; the inspected game code returns true after a successful copy. Not retained-backup inventory. |
| `valheim_world_last_auto_backup_timestamp_seconds` | Gauge | UTC time when the latest successful world auto-backup call returned. Not the backed-up world's save time. Omitted until observed. |
| `valheim_world_backups` | Gauge | Number of retained native automatic backup directories for the current local chunked world. Includes existing backups after exporter restart; absent if inventory is unavailable. |
| `valheim_world_backup_info` | Gauge | Value 1 per retained backup, labeled `backup` with its directory name, including backups whose timestamps are unknown. |
| `valheim_world_backup_created_timestamp_seconds` | Gauge | Backup creation time parsed from the native directory suffix `yyyyMMdd-HHmmss` in the server's local timezone; absent for invalid/ambiguous local times. |
| `valheim_world_backup_data_timestamp_seconds` | Gauge | UTC modification time of the backup's highest numeric-generation `_main.<uint>.ok` marker. This describes the saved data, not when it was copied. |
| `valheim_world_save_file_timestamp_seconds` | Gauge | UTC modification time of the live world's highest numeric-generation save marker. Available across exporter restarts; not a historical worker-completion observation. |
| `valheim_world_backup_inventory_success` | Gauge | 1 if the latest inventory scan succeeded, 0 if unavailable. |
| `valheim_world_backup_inventory_timestamp_seconds` | Gauge | UTC time of the last successful filesystem scan; retained after a failed scan, absent before any success. |
| `valheim_server_mod_info` | Gauge | Value 1 per loaded BepInEx plugin, labeled `guid`, `name`, and runtime `version`. Replaced on metadata refresh; includes this server-only plugin. |
| `valheim_modpack_manifest_success` | Gauge | Whether the configured manifest was successfully read and validated. Invalid/unreadable manifests clear current package/code samples. |
| `valheim_modpack_package_info` | Gauge | Value 1 per declared package with `namespace`, `name`, package `version`, `source`, `client`, and `server` labels. This is the intended package inventory, not the runtime-loaded plugin registry. |
| `valheim_modpack_info` | Gauge | Value 1 with `profile_name` and `profile_code`, only for a matching sidecar. Missing/stale codes remain absent while package inventory stays visible. |
| `valheim_character_playtime_seconds` | Gauge | Durable all-time connected seconds per `character_id` and `character`; starts with installation, not historical game playtime. |
| `valheim_character_playtime_7d_seconds` | Gauge | Connected seconds intersecting the rolling previous 604800 seconds, from retained session intervals rather than Prometheus scrape coverage. |
| `valheim_character_online` | Gauge | 1 when the saved character profile is currently observed on a ready peer, otherwise 0. Duplicate simultaneous appearances count once. |
| `valheim_character_sessions_total` | Counter | Durable observed offline-to-online transitions per character. Reconnects count; profile renames do not create new identities. |
| `valheim_character_last_seen_timestamp_seconds` | Gauge | UTC time of the latest actual presence heartbeat/disconnect observation; absent if unknown. |
| `valheim_character_storage_success` | Gauge | 1 unless the latest background persistence attempt failed; 0 means live totals may exist only in memory. |
| `valheim_character_deaths_total` | Counter | Durable server-observed vanilla player `OnDeath` broadcasts, matched to the actual sending peer and its current character life. No client companion needed. |

Save/backup observations are the game's reported outcomes, **not independent disk
integrity or restore validation**. Filesystem inventory covers native sibling
`<world>_backup_auto-*` directories only, not administrative tar archives or legacy
unchunked/cloud saves. Unknown timestamps are absent, never invented as zero.
Inventories are bounded to 128 backups and 128 mods, labels to 256 characters,
and rendered snapshots to 1 MiB.
Invalid/oversized inventories are not silently truncated.
File sizes, CPU/RAM/disk metrics, and unobserved gameplay counters are not exposed.

`ModpackManifestPath` is optional and takes effect on restart. When configured, a
fixed background worker reads at startup and every 60 seconds, with 256 KiB/4 KiB
manifest/sidecar limits and symlink rejection. It never uploads or changes files.
The [profile tool](../../../valheim-server/tools/modpack.py) validates config hashes and dependencies
and exports a deterministic public-client archive. Publishing an import code
requires separate approval. Death tracking does not change client-profile contents
and requires no upload.
The dashboard reads package metadata and the current code from these metrics.

### Character leaderboard and durability

- Identity comes from `ZNetPeer.m_playerID`, the ID saved in `PlayerProfile`, not
  `m_uid` (network session), `m_characterID` (transient ZDO), or a Steam account.
  A hexadecimal profile key is hidden in the dashboard but present as a metric
  label and in the private ledger. Different same-name characters stay distinct;
  renames retain totals. A copied profile with the same ID is the same character.
  These client-reported identities are not an anti-cheat/account-verification system.
- The dashboard shows character name, all-time and seven-day hours, online
  status, session count, and last seen. Names are visible to dashboard viewers.
  There is no backfill of playtime before installation.
- A background worker atomically replaces a versioned JSON ledger approximately
  every 60 seconds. Game observations/snapshots do not perform disk I/O.
  Graceful shutdown drains the final write; a crash/power loss can lose time since
  the last successful write. Back up this ledger separately from world saves.
  Storage failures log and set `valheim_character_storage_success=0`; corrupt
  existing files are rejected, never silently reset or overwritten.
- Accounting uses monotonic elapsed time, excluding restart/offline gaps. Recent
  intervals use UTC for the rolling window; clock jumps cannot add playtime.
  Records restore offline until actually observed again. Continuous intervals
  coalesce; expired recent intervals are pruned without deleting lifetime totals.
- Tracking is bounded to 256 characters, 256-character labels and a 16 MiB file;
  invalid/oversized data fails explicitly rather than truncating identities.
  Snapshot metrics refresh at most every 15 seconds and immediately on observed
  connection transitions. Bind retries do not interrupt collection/persistence.

### Server-observed deaths

- The server observes the vanilla player `OnDeath` routed RPC before scene
  dispatch, so a remote player need not have a local headless scene instance.
  Sender/session and target life ZDO must match the actual ready connection.
  Malformed or mismatched notifications are ignored without altering game dispatch.
- Accepted deaths increment the existing character ledger. Schema-1 playtime-only
  ledgers load with zero deaths, preserving connected time and sessions.
  There is no historical death backfill.
- Duplicate notifications for the same world/life count once. The newest 8192
  accepted death event IDs survive restarts; older IDs are evicted.
  The sender's current life must still match, independently rejecting old lives.
- Counts are trust-based observations, not anti-cheat: a modified client could
  suppress or forge its own notification. No Steam identity or client plugin is used.
- Both saved dashboard sources contain a death leaderboard, sorted descending
  by `valheim_character_deaths_total`. Live publishing requires authenticated Grafana access.

## Prometheus and Grafana

- [Prometheus example](../../../valheim-server/monitoring/prometheus.yml): same-host, non-container scraper.
  A scraper in another machine/container needs a deliberately reachable target;
  its own `127.0.0.1` is not the game host.
- [Grafana dashboard](../../../valheim-server/monitoring/grafana-dashboard.json): import and select the
  Prometheus data source and server instance. Queries filter by that instance.
  Sections show server/game/world identity, players, character playtime leaderboard,
  saves/backups, and loaded mods.
  Backup rows distinguish creation age from saved-data age; the inventory persists
  across exporter restarts. Unknown values remain “No data,” not healthy zeroes.
- No Grafana/Prometheus services, credentials, public listeners, or paid services
  are configured by this project.

## Development record and limits

### 1.4.0 — server-only deaths, 2026-11-01

- Adds durable death counts from vanilla routed notifications, with actual-peer
  and current-life matching, duplicate suppression, and unchanged packet position.
- Existing character playtime and sessions are preserved; deaths start at zero.
  No kills, creature reports, companion, client installation, or external upload.
- Local verification and production activation are recorded in the
  [server-state reference](../../../valheim-server/.agents/skills/server-management/references/server-state.md).
  Constructed wire-level smoke tests are not a real multiplayer death test.

### 1.3.0 — persistent character-name leaderboard, 2026-11-01

- Verified character IDs against the owned game's managed assemblies: the saved
  profile ID is transmitted by `ZNet.RPC_PlayerID` and stored in `ZNetPeer.m_playerID`.
  Read-only decompiler output remained outside the repository.
- Release build succeeded with the existing framework/DTO warnings; all 15
  regression groups passed. Added coverage for stable identities, duplicate names,
  renames/reconnects, restart/offline gaps, exact rolling-window expiry, clock
  rollback recovery, safe paths, corrupt state, write failures, and metric privacy.
- A throwaway host loaded the production DLL, persisted two distinct same-name
  characters for 120 seconds, restarted accounting across an offline hour, and
  served HTTP 200 with 120 seconds retained for each and no Steam fields.
  Advancing UTC beyond seven days expired recent time without losing lifetime time.
- Live deployment and rendered dashboard verification are recorded separately in
  the server state reference; local smoke is not proof of in-game character tracking.


### 1.2.1 — HTTP bind recovery, 2026-11-01

- A production boot failed to bind the configured Tailscale address with
  `SocketException: The requested address is not valid in this context`.
  The 1.2.0 plugin then stayed disabled, with no listener on port 9101.
- Keep the existing BepInEx/Harmony observation route; retry only HTTP startup
  socket/listener failures at 60, 300, then repeated 1800-second delays. Each
  attempt uses a fresh listener and closes failed resources. Configuration and
  observation errors remain fatal; no retry recreates counters or game hooks.
- Release build succeeded with framework-reference and reflection-populated DTO
  field warnings. All 13 regression groups passed, including actual occupied-port
  failures, backoff boundaries, recovery with retained telemetry, and disposal.
- A throwaway host loaded the built production DLL, forced four actual bind
  failures, advanced the retry clock, released the port, and received HTTP 200
  with both player and session observations retained. Delays were exercised with
  an injected clock, not a real 30-minute wait. This is not an in-game Mono test.
- Production activation completed on 2026-11-01 after explicit restart approval.
  Unity/Mono loaded 1.2.1 and bound the private endpoint; the final startup loaded
  all 15 plugins, connected Steam, and loaded NorthernHearth. Unauthorized HTTP
  access received 403. Actual production bind-failure recovery and an authorized
  Prometheus scrape/dashboard refresh were not exercised during activation.


2026-10-29: read the modding/recon/reverse-engineering skills and Unity reference.
`uv run um kb search "valheim"` timed out; the upstream index was retrieved directly
as a fallback and listed the Frostner/Mjolnir and trap-taming notes, not an exporter.
Read-only `uv run um scan` identified the owned native Unity Mono 6000.0.75f1 game
without a loader in its installation. Existing BepInEx profile is referenced only.
Chose BepInEx 5 plus narrow Harmony observations of existing connection/save/backup
methods; no game behavior is replaced. Decompiled API inspection remained in `/tmp`,
not in this repository. No upstream implementation was copied.

Historical 1.1.0 verification: the plugin built against the owned game/profile, with
the two existing `MSB3277` framework-reference warnings (`System.Net.Http` and
`System.IO.Compression`), no errors. Eleven regression groups pass, including
filesystem fixtures, inventory replacement/unavailability, loaded-mod labels,
actual loopback HTTP routing, method rejection, parallel scrapes,
disposal/rebinding, and source-IP authorization (including spoofed-header denial).
A throwaway host loaded the **built 1.1.0 production DLL**, scanned real temporary
world/backup metadata, and served all 21 metric families over HTTP 200. It verified
exact server/mod/backup fields, marker timestamps, POST 405, and immediate inventory
invalidation when the world is removed. Controlled inputs exercised save/backup
event observations; no production events were fabricated. The seven existing
Harmony method signatures were previously resolved against the owned game assembly.
This smoke does not establish runtime behavior inside Unity/Mono.

Independent source reviews found no material security defect in the reviewed paths.
Correctness review caught bulk-shutdown accounting and stale Grafana range-query
reductions; both were corrected and the fixes re-reviewed. HTTP admission/timeout
behavior under hostile traffic remains unverified.
Dashboard JSON and instance selectors were checked; current-state stat panels use
instant queries so old non-null history does not replace missing observations.

Version 1.0.1 adds fail-closed source-IP authorization for explicit private-network
binding. It changes transport access only; the concrete metric definitions and
counter reset semantics are unchanged.

Version 1.1.0 adds configured server identity, loaded-plugin inventory, and retained
native backup/save-file metadata with a bounded background scanner. It keeps the
existing observed-event metrics and transport access rules unchanged.

Version 1.2.0 adds the optional manifest/verified-code reader and three concrete
modpack metric families. Integrated verification passed 12 C# regression groups,
13 Python tool tests, and actual CLI manifest validation/export. Build succeeds
with the existing framework-reference warnings and reflection-assigned DTO field
warnings (`CS0649`); production/runtime evidence remains in the state reference.

Production deployment evidence and remaining runtime checks are maintained only
in the [server-state reference](../../../valheim-server/.agents/skills/server-management/references/server-state.md).
