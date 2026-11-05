# NearbyStore development log

## 2026-11-05 — inventory storage and favorites

- User requested a separate mod: inventory-open button deposits into nearby chests; favorite shortcut protects items. This authorizes this new project, not resuming paused projects.
- Route: client-side BepInEx 5 + Harmony, native Linux Valheim Mono (Unity 6000.0.75f1, owned installation scanned read-only). No Jotunn dependency, loader installation, game launch, deployment or server changes.
- Read owned `Container`, `Inventory`, `InventoryGui`, `InventoryGrid`, `InventoryElement`, and `ItemDrop` APIs from private temporary decompilation outside the repository. Never redistribute these sources or assemblies.
- Container storage uses the vanilla owner-authorized RPC handshake, not ClaimOwnership. RequestStack checks privacy and in-use state, queues the inventory/ownership ZDO and separately sends a grant response. The response can arrive first: wait for observed ownership, then refresh Container.Load before targeted transfers. Already-owned chests need no request. Late replies after cancellation must not invoke vanilla StackAll, which ignores our favorites.
- Check ward access locally before request and again before transfer, like vanilla interactions. Restrict discovery to placed Piece containers, excluding tombstones and auto-destroy loot. Radius defaults to 10m. Matching chests first, then free space, nearer first in each pass.
- Favorite marker: per-stack `m_customData["jarki.nearbystore.favorite"]`, cloned and serialized by vanilla item data. Alt+F toggles hovered player-inventory stack; F overlay identifies it. Equipped, queued-for-equip and quest items remain carried.
- Universal Modder KB search timed out after 60 seconds; owned-source inspection used instead. No upstream field-note submission.
- Verification results are recorded in README after build/smoke; in-game layout, save/reload and multiplayer must not be claimed without authorized runtime verification.

### Verification

- Initial build exposed missing `assembly_guiutils.dll` reference for Localization and UITooltip; added read-only reference. Replaced obsolete FindObjectsOfType with FindObjectsByType(...None); distance ordering is applied explicitly.
- Final Release build: zero errors, two existing MSB3277 reference conflicts (System.Net.Http and System.IO.Compression). No game/loader binaries copied into output.
- 13/13 production-core behavioral cases pass via `dotnet run --project tests/NearbyStore.Tests/NearbyStore.Tests.csproj -c Release`.
- Disposable smoke: first two-pass deposit stored 32 wood/stone units, retained 8 favorited food units and filled matching wood stack to 50. Unfavorite plus next deposit stored 8 food units and left no carried stacks. API doubles only, no Unity/native execution.
- No authorized runtime was available: actual UI layout, binary save/reload, remote-owner concurrency and ward behavior remain unverified in game. No game launch, loader installation, deployment, server operation or publication occurred.

### Publication request

- Owner requested publication. Prepared Jarki-NearbyStore 1.0.0 metadata, original icon, public README, changelog and explicit DLL/license allowlist using the existing pinned tcli wrapper.
- Candidate ZIP SHA-256: `5745ce078366098c73f4436051a2d073964a39123250d0259bcfe492ee42ddf9`.
- Metadata checks, fresh Release build, tcli packaging and final archive verification pass. Standalone suite remains 13/13. Extracted six-file payload passes publishing lint with zero failures/warnings. No upload yet; exact-candidate approval pending.
- Reviewed current Thunderstore rules; public docs distinguish tests from missing runtime evidence, warn about real inventory movement, disclose AI assistance and include MIT terms. No approval inferred for game/profile/server operations or source push.

### Authorized isolated runtime checks

- Owner selected in-game testing before publication, explicitly authorizing a disposable profile/loader setup/game launch/input automation. Three non-overlapping agents reviewed chest protocol, reviewed UI/favorite lifecycle, and added `tests/NearbyStore.EdgeCases`; only parent touched game input, test profile or production fixes.
- Edge suite passes 3/3 groups: 320 deterministic seeds, 8 rounds, 20,480 checked deposits plus fixed-point repeats. Original 13/13 cases also pass.
- Created fresh `.dev/nearbystore-lab/` loader and isolated XDG config, imported the candidate DLL from the tcli ZIP, and created only local `NearbyStoreLab1105` world / `nearbystorelab1105` character. No live profile or server changed. Sibling operational repository is absent from this workstation; no remote or existing-profile operations attempted.
- First two runs exposed a fixture timing issue: programmatic Show during spawn arrival was closed by native game flow. Switched to actual Tab input after arrival rather than changing production readiness.
- Run3 actual Alt+F/button input passed storing, favorite protection, matching-first/overflow, private/busy/range exclusions and on-disk character favorite persistence. Its injected granted-before-ZDO scenario reproduced the protocol review finding and failed.
- Fixed ownership waiting; already-owned quarantined chests recover without overlapping network requests or removing late-response suppression. Vanilla replies lack request IDs; an unanswered still-remote chest remains quarantined until ownership arrives/container reloads. Public docs describe normal-open recovery and the potential hover-stack response interception during quarantine.
- Also initialize TMP components while inactive so the real native font is assigned before OnEnable, avoiding attempts to load missing default LiberationSans assets.
- Disposable lab backup before run4: `nearbystore-lab-before-run4/20261105-184727.zip` in the local Universal Modder backup store.
- Run4 passed 38 checks, including real input, default/70%/130% UI screenshots, actual character-file favorite save/reload and the corrected ownership/late-response/quarantine transitions on real containers. Returned to main menu and exited cleanly. No plugin exception in BepInEx log.
- Runtime inspection confirmed the optional freshly created UITooltip had no prefab and therefore provided no tooltip. Removed this unused component rather than carrying nonfunctional extra UI; no tooltip is part of the requested feature.
- Final candidate ZIP SHA-256: `e0088a5c45cddc8eb53c1b74698a04220a08478a5199d9cd2b2174cc80d6d492`; six-file extracted payload lint has zero failures/warnings. Final exact-candidate runtime run follows the tooltip cleanup.
- Backup before exact-candidate run: `nearbystore-lab-before-final/20261105-185344.zip`. Multiplayer, denied wards, mod-manager import UI and uninstall/reinstall remain outside verified scope.
- Run5 failed the second-storage destination assertion before quantities were logged. No production change followed. Added diagnostic source/destination/moved/favorite observations and a capture; run6 with unique disposable world/character and run7 with the original names both passed 38 checks on the identical final DLL. Both logged `bag=0 target=8 moved=8 favorite=False`. The earlier assertion's cause is unconfirmed; do not claim a production fix or proven world-isolation cause. Public README discloses it.
- Final runtime plugin bytes were compared directly to the candidate archive's DLL and match. Two exact-DLL verdicts, failure history and archive/lint evidence are recorded privately in `.dev/release/nearbystore-final-verification.json`; all existing gameplay profiles/servers remain untouched.
- CLI auth environment is unset; no publication credential file found in narrowly scoped Thunderstore/token filename checks in home, config and temp locations. Browser Thunderstore session is signed out and the temporary tab was closed. No upload attempted; final approval and an owner-provided private token-file path (or manual upload) are required.
- Final documentation-only rebuild retains the byte-identical runtime-tested DLL. Final ZIP SHA-256: `543600cdd70cf27c76567ee29af493747dc9e6a958513dc87f66ef90f35afef3`; wrapper archive verification and extracted-payload lint pass (six files, zero failures/warnings). Full build output retains the two known MSB3277 warnings privately.
- Archived original test harness/driver sources with evidence, removed their disposable source/build directory and test DLL from the isolated profile, and shut down the Steam instance started for these tests. Production candidate, disposable saves and evidence are retained; no Valheim process remains.
- Owner separately approved uploading final ZIP `543600cdd70cf27c76567ee29af493747dc9e6a958513dc87f66ef90f35afef3` and indicated an existing publication token should be available. Consulting only the prior release authentication record for its private file location; credential contents must never enter logs/chat/repository.
- Historical release consult located the existing private token file. Its ownership and restrictive permissions were validated without printing contents; pinned tcli 0.2.4 published the approved NearbyStore 1.0.0 ZIP successfully using child-process-only `TCLI_AUTH_TOKEN`. Token file remains outside the repository and unchanged.
- Public download passes `tools/release.py verify` and SHA-256 exactly matches `543600cdd70cf27c76567ee29af493747dc9e6a958513dc87f66ef90f35afef3`. Public metadata reports active 1.0.0 in Valheim, expected four categories, BepInEx dependency, NSFW off and review status unreviewed.
- Live Valheim page, original icon, version and public README disclosures were inspected in Chromium. An initial canonical static read returned 404 immediately after upload; the cache-busted browser page rendered correctly. Temporary verification tab closed. Release docs and active-target guidance now include NearbyStore; all three paused projects remain paused.
- Published page: <https://thunderstore.io/c/valheim/p/Jarki/NearbyStore/>. No other package upload, live profile/server change or GitHub source push was performed.
