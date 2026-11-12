# NearbyStore slowdown tests

Targets: current unreleased NearbyStore source, with published 1.1.3 as the historical
baseline, especially multiplayer. This is a runnable transfer-core workload probe
plus a **real-client test protocol**, not a claim that multiplayer has passed.
Release metadata is unchanged.

## What to investigate first

| Priority | Source path | Risk and discriminating experiment |
| --- | --- | --- |
| P0 | `Plugin.Advance`: four active slots, separate grant/ownership arrival, per-request and total deadlines | Verify overlapping waits, healthy-tail progress and bounded completion. Keep an unmatched transferable stack so early-stop cannot hide waiting; separate elapsed waiting from frame stalls. |
| P0 | `StorageTransfer.Deposit`: single-item capacity preflight before native `MoveItemToThis` | Full matching inventories must perform no native moves/saves. Compare full matching versus full unmatched controls; useful partial transfers must preserve native semantics and may still emit native occupied-slot warnings. |
| P0 | `Plugin.Advance`: cooperative 2 ms budget and 32 candidate/productive-transfer steps per frame | Verify work spans natural frames with large workloads. Cheap rejections use the remaining time budget, not extra frames. Measure worst callback and total operation latency separately; an individual native call cannot be preempted. |
| P1 | `Plugin.Begin`: `FindObjectsByType<Container>`, eligibility, sort | Discovery considers loaded containers before filtering by radius. Hold nearby candidates constant and increase loaded distant containers and wards. Measure discovery separately from transfer. |
| P1 | `Plugin.LateUpdate` / `UpdateMarkers` | Open-inventory cost occurs every frame even without depositing. Compare inventory closed/open, no favorites/all favorites, ordinary/enlarged inventories, and repeated GUI recreation. |
| P1 | `outstanding` quarantine and late responses | Repeated timeout, cancellation, unload and retry may affect responsiveness or retain references. Exercise recovery without bypassing request correlation safeguards. |

Historical evidence (not rerun here): `.dev/nearbystore-ownership-lab/findings.json`
records a 1.1.2 native controlled-ownership experiment with 32 carried types:
50 remote unmatched chests with injected 100 ms response/ownership delay took
about 5.93 seconds; 50 locally owned full matching chests took about 330 ms,
with 1,600 insertion calls and 3,200 native `Container.Save` calls but zero units
moved. These are three-sample, instrumented single-player results, not real RTT
or multiplayer measurements. Published 1.1.3 fixed empty/protected-bag and post-success
continuation but did not change the insertion path or serial ownership waits.
The current unreleased source changes both; fresh measurements must identify its DLL.

## Runnable local workload probe

From the repository root:

```sh
dotnet run --project tests/NearbyStore.Performance/NearbyStore.Performance.csproj -c Release
dotnet run --project tests/NearbyStore.Tests/NearbyStore.Tests.csproj -c Release
dotnet run --project tests/NearbyStore.EdgeCases/NearbyStore.EdgeCases.csproj -c Release
```

The probe links the **actual `StorageTransfer.cs`** and existing handwritten
`GameStubs.cs`; no copied transfer algorithm, game installation or loader needed.
It runs 36 workloads, each with three warmups and 40 measured fresh fixtures:

- 32 carried types against 1, 10, 50 or 100 destination inventories.
- Full unmatched, full matching, partial capacity, matching spare slots, mixed
  favorite/equipped/caller-protected stacks, all favorites, and empty inventory.
- Additional 8- and 128-stack single-destination scaling probes. These are synthetic
  inventory sizes, not an assertion about vanilla player capacity.

Each carried type starts with ten units. Partial-capacity chests have one unit of
room per type. Spare-slot chests hold full matching stacks plus one free slot per
type. Every fixture is rebuilt and checked outside the timed region. Every sample
checks exact movement/remainders, name/quality/world-level conservation, protected
stack identity/position, metadata, stack bounds and grid validity. A failed oracle
exits nonzero; elapsed time alone does not fail a machine-dependent CI run.

CSV stdout reports median, nearest-rank p95, maximum, median current-thread allocated
bytes, moved units and **double notification counts**, not native save counts.
Diagnostics go to stderr. Save raw stdout under ignored `.dev/` when comparing runs.
The measured region is a fixed sweep of single-item `Deposit` calls over a prebuilt
carried-stack snapshot; setup, snapshots, assertions and output are excluded.
Allocation includes the inventory double's implementation.
GC caused by fixture setup may still perturb timings. No forced GC is used.
Tiered compilation is disabled in this diagnostic executable to avoid changing JIT
tiers mid-matrix. This does not make .NET match Unity/Mono. Run without concurrent
build/test workloads when comparing timings; the CSV is not a controlled native
performance baseline.

**Limits:** this does not execute `Plugin.Begin`, `Advance`, early-stop, Unity,
Harmony patches, native inventory serialization, UI, or any networking. It keeps
calling the core for the specified sweep even after the bag empties. Its timings
must not be used as Valheim frame times or extrapolated to multiplayer latency.
Use it to validate reproducible load shapes and catch changed transfer behavior;
use the native protocol below to determine actual slowdown. Notification counts
are diagnostic, not a contract requiring the current redundant work to continue.

### Current fix verification

The current transfer core passes **16/16 standalone cases**, **3/3 adversarial
groups** (10,240 checked deposits plus repeats), and **36/36 workload oracles**
(1,440 measured fixtures plus 108 warmups). These checks use handwritten doubles.

The final development DLL also passed a native smoke and **11 scenario groups**
in disposable Valheim, including full/unmatched controls, useful partial moves,
early-stop with unused grants, source snapshot changes, four concurrent requests,
timeout/denial/grant-before-owner, between-slice ownership/access/range loss,
overall deadline, both vanilla/mod request orders, stale denial during an owned
retry, same-frame favoriting and destroyed-request/grid-rebuild recovery.
Native screenshots were inspected.

| Native measurement | Published 1.1.3 investigation | Current development build |
| --- | --- | --- |
| Full matching: 50 chests × 32 carried types, default 10 m | 288.32 ms median synchronous button callback | 13.05 ms median button callback, at most 2.09 ms per transfer callback; 49.63 ms median total completion |
| Same full-chest workload: native insertions / serializations / warnings per click | 1,600 / 3,200 / 1,600; zero units moved | 0 / 0 / 0; zero units moved and no data-revision increments |
| Three handoffs each delayed 4.8 s | 14.43–14.48 s serial waiting | 4.83 s with those three handoffs overlapping, plus a fourth 100 ms handoff |
| Unanswered nearest chest | Healthy tail not visited before operation stops | All four healthy successors transfer while the nearest is pending; its remaining wait expires after about 5 s |
| 100 unresponsive candidates | No overall operation deadline | Stops at about 15 s, after 12 requests; unresolved guards retained |

Full-chest figures use seven fresh samples after two warmups; the baseline and
candidate are separate instrumented runs, not randomized paired measurements.
The overlapping-handoff candidate is one controlled schedule, not an RTT benchmark.
Useful partial transfers remain native: the 50 one-unit-hole fixture produced
49 native occupied-slot warnings while successfully transferring 50 units.
The 2 ms budget is cooperative; one owned-recovery callback reached 2.25 ms.
Initial discovery still traverses loaded containers and is outside that budget.

Verification used Unity 6000.0.75f1, Vulkan / GTX 1050 and the existing BepInEx
reference loader. Builds retain the two known MSB3277 warnings. No native exception
or oracle failure was found in the final logs. The initial fixed scheduler's
overly conservative no-op step accounting took about 1.2 s for the full sweep;
the final scheduler uses its time budget for cheap rejections instead.

Private final evidence and source/DLL hashes:
`.dev/nearbystore-deep-lab/runs/c707f9155b8240c09a4b38746ac4d3c4/`.
Reproducible local fixture: `.dev/nearbystore-deep-lab/run-native.py fixes`.
It requires the isolated private desktop and existing owned game/loader references.
These are **native single-player fault-injection results**, not real multiplayer.
No live profiles or servers were changed; third-party container mods, scene
unload/reload with delayed replies, disconnects and disk persistence were not
reverified by this run. The real-peer protocol below remains necessary.

### Historical local verification (2026-11-12, before capacity/scheduling fixes)

The Release probe passed all 36 workload oracles: 1,440 measured fresh-fixture
sweeps plus 108 warmups. The existing standalone suite passed 13/13 cases;
the adversarial runner passed 3/3 groups, including 10,240 checked deposits plus
fixed-point repeats. The final probe run used tiered compilation disabled and
did not overlap the other test runners.

These results validate the workload fixtures and exercised transfer-core behavior
only. No real-client multiplayer scenario below was executed, and no production
DLL, profile or server was changed.

## Real multiplayer fixture and controls

Execution requires separate owner approval for game driving and the isolated
client/server setup. Follow the sibling server-management skill and state reference
before any profile/server operation. Do not use a live world, change a shared
network interface, deploy a test plugin publicly, or copy saves into this repository.

1. Use disposable characters/worlds and independent clients A and B, initially
   on an owner-controlled dedicated server. Also repeat the core cases with a
   player-hosted session: host advantage can otherwise hide remote ownership cost.
   Record game/network/Unity/BepInEx versions, DLL hash, mod list, radius, hardware,
   frame cap, transport/crossplay mode and client roles. NearbyStore belongs only
   on clients; include A modded/B vanilla and then both clients modded.
2. Start with one actual remote-owned chest and a known partial stack. A deposits;
   B observes the same final quantity, closes/reopens the chest, and the fixture
   passes the conservation oracle before expanding. No synthetic RPC interception
   or fake ZDO owners in a test labeled real multiplayer.
3. Arrange 1/10/50/100 **eligible** chests within the effective radius (default
   10 m), with distinct distances to avoid equal-distance order ambiguity. Verify
   placed Piece, valid network identity, access, no busy user, actual owner,
   contents, capacity, inventory readiness and carried protections before every run.
   Stabilize loading, chest wear and background activity; label any fixture mods.
4. Reset both inventories **and actual ownership** before each sample. A previous
   deposit can make later samples locally owned and falsely remove network cost.
   Have the intended peer acquire ownership normally and independently confirm it
   on both clients. If the intended ownership/state cannot be established, mark
   that cell BLOCKED, not a fast PASS. Measure first-use and warmed paths separately.
5. Controls: mod disabled with no action; mod enabled with inventory closed/open
   but no action; one ordinary vanilla stack action into the same chest; and one
   local-owned NearbyStore deposit. A vanilla one-chest action is a cost/safety
   reference, not an equivalent 100-chest interaction.
6. Run a small smoke matrix first, then at least 30 reset samples per cost cell in
   randomized/interleaved order. Report median, p95 and maximum with sample count;
   do not infer p99 from 30 operations. Capture thousands of frame samples for
   frame-tail distributions. Repeat promising regressions in an independent run.
   Retain timeout/cancellation outcomes instead of dropping them from percentiles.

## Measurements: distinguish waiting, freezing and convergence

Use low-overhead, test-only observation of the real production DLL. The existing
ignored native harness documents useful profiling points, but is **not a real-MP
harness**: it hardcodes a single-player world, a historical version assertion, fake
owners and intercepted requests. Do not simply run it and relabel the results.

Record a run/case/client/operation ID and chest network ID. IDs in the observer
are for logs only; vanilla replies have no request ID. Use monotonic timestamps
locally; never subtract raw clocks from different machines. On the requesting
client record request, response (including granted/denied), actual owner observed,
load, deposit, completion/cancel/timeout, and the next rendered inventory frame.
On peers record arrival/convergence separately or use a measured clock-offset bound.

| Measurement | Collection and interpretation |
| --- | --- |
| User completion | Click to finished message and next rendered inventory frame; measure replica convergence separately. A fast callback with stale peer inventory is not completion. |
| Frame hitch | Per-frame main-thread/frame duration on A, B and an uninvolved observer; p50/p95/p99, maximum, and counts over 16.7/33.3/50/100 ms. Keep frame cap and camera fixed. |
| Production work | Outermost `Begin`/`Advance` duration and maximum per invocation; isolate discovery/eligibility/sort, `Load`, `Deposit`, native `MoveItemToThis`, `Container.Save` and GUI refresh. Nested inclusive times must not be added. |
| Ownership waiting | Per chest: request to reply, request to observed local ownership, and wait release; distinguish denied, unanswered and granted-but-not-owned cases. Sum measured serial waits; do not assume this equals chest count times ping. |
| Amplification | Requests/replies, ownership changes, loads, moves, saves, bytes sent/received and units moved. Record total work even for zero-unit cases; ratios per moved unit are undefined when zero. Separate unrelated scene traffic using baseline windows. |
| Allocation/GC | Supported native Unity/Mono profiler allocation and collection data, peak per frame and total per operation. Do not treat the standalone .NET runtime's allocation figures as Mono evidence. |
| Remote impact | CPU/frame or server-update duration, traffic, save spikes and inventory convergence on the chest owner/server and other clients. Client-only installation can still create remote RPC/save work. |
| Recovery/retention | Pending/quarantined request count, retained/destroyed chest references and memory after repeated cancellation, unload/reload and retry, alongside externally observed chest usability. |

Buffer observer events in memory and flush outside timed frames; no disk logging,
reflection-heavy assertions or per-item allocations inside the measured hot path.
Run an observer-disabled control and a lightweight-observer control to quantify
measurement overhead. Use more detailed nested profiling only after a hotspot is
reproduced. Keep raw samples, fixture snapshots and outcome labels under `.dev/`.

## Prioritized multiplayer matrix

Vary one dimension at a time around 32 carried types and 50 chests; use boundary
sizes after the first discriminating case. Do not start with a giant Cartesian product.

| ID | Scenario and variation | Slowdown signal / required outcome |
| --- | --- | --- |
| M1 | Full matching vs full unmatched; 1/10/50/100 chests, A owns all vs B owns all | No units move and no native insertion/save occurs for guaranteed no-capacity transfers. Compare worst callback/frame and completion; distinguish saved work from added frame scheduling latency. |
| M2 | Unmatched bag; 0/25/50/100% remote ownership, ownership split across B/host/other peers | Plot completion against **observed handshake duration and count**; verify at most four active slots and measure whether each client/owner stalls. Keep a transferable unmatched stack throughout. |
| M3 | Capacity only in nearest vs last chest; one partial unit per chest; one unmatched leftover | First-capacity case stops admitting work immediately once the snapshot has no transferable items; already-issued overlapping requests may remain guarded. Empty/all-protected bag makes no discovery/request. Late grants must not trigger native stacking. |
| M4 | Two clients click simultaneously into the same nearly-full chests, then disjoint chests; expand to 4/8 clients where available | Measure per-client p95, denied responses, ownership churn and aggregate traffic. No duplicated/lost units, no writes before ownership, no stuck button. Do not prescribe which client wins a contested slot. |
| M5 | B opens or manually moves items during A's deposit; B vanilla and B modded | Busy/in-use containers are skipped or denied safely. Observe stale reads, retries on later explicit clicks and convergence; no silent overwrite of B's change. Include reverse chest order between players. |
| M6 | Controlled isolated network: baseline, then measured RTT targets 50/150/300 ms; jitter ±50 ms; loss 1/3%; bandwidth limitation separately | Record actual RTT/throughput and both reply and ownership arrival. Determine whether latency is in transport, permission, ZDO arrival or main-thread work. Do not equate configured one-way delay with RTT or inject fake ownership. |
| M7 | Reply precedes ownership; ownership precedes reply; reply denied; reply or ownership exceeds 5 s | Never deposit before both grant and local ownership. Skip a pending chest after five seconds plus scheduling delay, allow healthy successors, and enforce the 15-second operation deadline. Observe quarantine and successful normal-open/close wand retry. |
| M8 | Owner leaves/disconnects or requester disconnects during pending and after a partial transfer; ownership migrates; reconnect | Measure stop/recovery time and settled contents. No late auto-deposit/favorite loss, duplication or missing partial transfer after reconnect. Preserve failure evidence; do not clear quarantine just to speed the test. |
| M9 | Close inventory, open blocking UI, teleport, die, favorite all remaining stacks while pending; click repeatedly then retry | Cancellation stops further moves; stored items stay stored. Late reply cannot execute vanilla StackAll on favorites. Rapid clicks cannot start overlapping operations or duplicate an unanswered request to the same chest. |
| M10 | Constant 10 nearby chests plus 0/100/500 loaded distant chests; denied wards/private chests, busy chests; radius 10 vs 50 m | Attribute click hitch to discovery/eligibility rather than network. Assert actual loaded/eligible counts, not world totals. Excluded chests remain unchanged. |
| M11 | Two minutes inventory closed/open; 0/8/32 favorites; repeat 100 open/close and scene unload/reload cycles | Detect idle CPU/GC, first-marker spikes, stale labels and retained references. Unchanged closed-inventory baseline should not scale with nearby chest count. Confirm new scene/GUI still works. |
| M12 | Vanilla chest vs supported restricted container (e.g. OdinsFoodBarrels 1.4.0); mixed qualities/world levels, capacity and restriction rejection | Preserve native restriction patches, metadata and exact remainders while measuring futile insertion/save work. Record dependency version/configuration; zero moved is not automatically a performance pass. |

For M7, naturally occurring network order may not cover both paths; label missing
orders unobserved. Controlled injection can supplement real-peer results as a
separate **native fault-injection** category, never as real transport evidence.
For M8, isolate voluntary leave, transport loss and process crash as distinct cases;
only execute approved destructive fault cases on the disposable fixture.

## Oracles and triage

**Correctness gates apply to every timing sample.** Snapshot total units by item
identity (prefab/shared name plus quality/world level as appropriate) across the
actual player inventories and authoritative chest inventories. Count each chest
once, not every peer replica. Independently check replica agreement after settling,
protected-stack custom data/equipment state, remainder positions and restricted
container rules. For disputed ownership, wait for a bounded stable state or record
FAIL/BLOCKED; do not pick the replica that makes totals pass. After reconnect and a
normal save/reload, recheck conservation and favorites. Also sample chest contents
through ordinary open/close UI to avoid relying solely on the same observer hooks.

For concurrency with limited room, assert the combined total and capacity, not a
particular winner. Validate before/after totals independently of `Plugin.moved`;
that field is an additional cross-check, not the oracle. Classify fixture failures
separately from production failures. Never discard failed/timed-out runs as outliers.

**Initial investigation triggers, not universal shipping budgets:** a reproducible
additional main-thread callback over 16.7 ms, a new frame over 50 ms, or paired p95
regression exceeding both 20% and 2 ms merits investigation. Agree a target-hardware
budget before making these CI gates. Any safety violation is a failure regardless
of speed. For network-dominated operations, report total duration and serial wait
contribution instead of calling seconds of yielded waiting a freeze.

The five-second deadline is **per pending chest**; the current scheduler also has
a **15-second operation deadline**. A frame or atomic native call can postpone
observing a deadline. Use an external watchdog with scheduling margin, cancel
through the normal UI on overrun, and retain incomplete traces. Do not patch the
product timeout or force ownership to make a slow result pass. The old serial
scheduler's many-minute theoretical sweep is historical, not the current contract.

Exercise actual vanilla `Container.StackAll` in both orders around a mod request.
The second caller must not issue an overlapping request to the same loaded chest.
After a canceled/timed-out mod request, its original reply is swallowed; after a
tracked vanilla request, its reply still runs native stacking. Verify recovery
once that reply arrives. Live unanswered guards must not expire merely to improve
responsiveness, because vanilla replies contain no request IDs.

Capacity preflight must not bypass native compatibility or third-party insertion
restrictions. Re-run contention, late-response, cancellation, unload/reload and
persistence cases after any optimization; local fault injection does not prove
real-peer behavior.
