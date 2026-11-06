# Changelog

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
