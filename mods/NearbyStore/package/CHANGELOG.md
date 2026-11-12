# Changelog

## 1.1.4

- Skip native insertion and inventory notifications for guaranteed no-capacity destinations, while retaining matching overflow, useful partial transfers and native restriction hooks.
- Keep inventory mutations on the main thread, spread work across frames with a cooperative 2 ms budget and at most 32 candidates/productive transfers per frame, and allow up to four active ownership/transfer slots. Cheap rejected items do not force extra frames.
- Continue past individual five-second ownership timeouts and bound each operation to 15 seconds. Stop admitting work once the original carried-stack snapshot has no transferable items.
- Process favorite input before advancing transfers. Recheck source membership, protection, access and ownership before each step; newly added stacks wait for the next click.
- Prevent overlapping vanilla and mod stack requests to the same loaded chest. Retain unresolved response guards after cancellation/timeouts; vanilla stacking waits until the original response arrives.
- Prune destroyed-container references and clear stale favorite markers on native grid rebuild.
- Verify 16 standalone cases, three adversarial groups, 36 workload oracles, and a native smoke plus 11 scenario groups. Native controlled-delay/full-chest improvements and limitations are recorded in the development README; real multiplayer remains unverified.
- Preserve the plugin ID, DLL name, configuration and favorite save keys.

## 1.1.3

- Stop before chest discovery when no carried stacks are transferable; stop visiting further chests after the last transferable stack is stored.
- Stop safely if all remaining stacks become protected while ownership is pending. Preserve late-response suppression, favorites/equipment/quest protection and vanilla ownership handshakes.
- Native controlled-ownership experiments: first-chest-completes storage across 50 chests drops from 50 requests to one (median 5.78 s to 132 ms at injected 100 ms arrival). Empty/all-favorite inventories issue zero requests.
- Verify 24 scenarios with three reset samples each, plus smoke and five safety transitions; conservation, cancellation, timeout/quarantine, late replies and recovery pass. These are native ownership injections, not real multiplayer measurements. Full-chest insertion cost is unchanged.

## 1.1.2

- Install under `plugins/Jarki-NearbyStore/`; remove the previous NearbyStore DLL when upgrading to avoid duplicate copies. Plugin ID, DLL name and config path stay unchanged.
- Refresh the player-facing page with concise core features and before/after stacking screenshots.
- Place a square 56×56 magic-wand icon button to the right of inventory. Remove all button captions and shortcut hints; Alt+F favoriting remains available.
- Reuse Valheim's native border and hover/pressed states around original gold wand artwork; no extracted game assets bundled.
- Hide the wand while a container, crafting station, active crafting operation, inventory dialog or blocking overlay is open; restore it in standalone inventory. The ordinary basic crafting list remains allowed.
- Cancel pending transfers when a blocking UI opens, retaining late-response protection. Favorite shortcuts in chest inventory and vanilla-style stacking remain unchanged.
- Square-wand DLL passed 102 native-client checks with OdinsFoodBarrels 1.4.0 loaded: text-free square layout, nonblocking icon graphics, actual Alt+F, hover/click, three UI scales, prior visibility transitions and overflow stacking. Multiplayer and other UI mods remain unverified.

## 1.1.1

- Correct 1.1.0's overly restrictive stacking to match vanilla **Stack items**. Chests containing an item accept it through native inventory insertion: fill compatible stacks, then create another stack in a free slot. No fallback into unrelated or empty chests.
- Reuse the native movement path, including quality/world-level merge rules and slot placement, rather than maintaining custom merge logic. Favorites, equipment/quest protection, nearest-first discovery and access/ownership guards remain intact.
- Verify **OdinsFoodBarrels 1.4.0** with all 36 restricted container types: eligibility, overflow, partial merging, favorites and prohibited-item rejection. No new dependency or restriction bypass.
- 12 standalone cases and 3 adversarial groups pass, including 10,240 generated deposits. The exact 1.1.1 DLL passed 673 native-client checks including fixtures, six comparisons against vanilla StackAll, and actual Tab/button transfers. Build retains two existing MSB3277 warnings. Multiplayer/custom barrel settings remain unverified.

## 1.1.0

- Replace automatic storage with stacking into compatible existing chest stacks only, nearest chest first.
- Never create new stacks or fill empty slots. Absent item types, full/incompatible stacks and overflow stay carried.
- Rename the button to **Stack nearby** and update result messages. Favorites, equipment/quest protection and chest-access checks remain unchanged.
- 12 standalone cases, 3 adversarial groups (10,240 generated deposits) and a disposable transfer smoke pass with inventory API doubles. Release build passes with two existing MSB3277 warnings.
- Stack-only gameplay passed 74 native-client checks before the release-version bump, including fixture assertions: real Tab/Alt+F/button input, matching-only transfers, overflow/unmatched/full-stack retention, empty-slot preservation, repeated-click no-op, favorite save/reload, access exclusions and injected ownership transitions. Default/70%/130% screenshots inspected. Initial focus/setup failures and verification limits are recorded in the README. No live deployment; multiplayer remains unverified.

## 1.0.0

- Initial NearbyStore: inventory Store nearby button, 10m configurable search, matching-chest priority and free-space fallback.
- Left Alt + F favorites hovered player-inventory stacks; gold F markers; favorites, equipped, queued-for-equip and quest items stay carried.
- Uses vanilla chest ownership requests with privacy, ward and busy checks; skips tombstones and dropped loot.
- Waits for ownership synchronization after access grants; already-owned chests need no request. Timed-out remote requests remain safely quarantined; normal-open recovery is documented.
- Native-font UI initialization avoids loading missing default font assets. Removed an unused tooltip component without its required prefab.
- 13 standalone cases, 3 adversarial groups (20,480 generated deposits) and 38 native-client checks pass. Real keyboard/mouse workflow, character-file favorite persistence and 70%/130% UI scale were exercised.
- Multiplayer, denied wards, uninstall/reinstall and mod-manager ZIP-import UI remain unverified.
