# NearbyStore

Client-side Valheim BepInEx plugin. Click the square **magic-wand icon** to the
right of your inventory to apply vanilla-style **Stack items** to placed chests
within **10 metres**. No server plugin or Jotunn dependency.
**1.1.2 is published:** the icon hides while containers,
crafting stations or blocking overlays are open. It uses Valheim's native border
and interaction sprites with an original gold wand graphic. No caption or shortcut hint.
[Download NearbyStore 1.1.2](https://thunderstore.io/c/valheim/p/Jarki/NearbyStore/).
No live profile or server deployment was performed.

## 1.1.3 candidate: stop when nothing is transferable

The source now skips chest discovery when the carried inventory is empty or all
stacks are protected, and finishes the chest loop as soon as no transferable
stacks remain. If the remaining stacks become protected while waiting for
ownership, it stops while retaining the outstanding late-response guard.
Favorites, equipped/queued/quest protections and the vanilla ownership handshake
are unchanged. This change is not in the published 1.1.2 ZIP. Source and release
metadata now identify the unpublished 1.1.3 candidate.

Native verification on Valheim l-1.0.21 / BepInExPack 5.4.2351 used a disposable
world, default 10-metre radius, 32 real carried types and 24 controlled-ownership
scenarios with three reset samples each. With 50 remote-owned test chests and
permission/ownership arrival injected at 100 ms after each request:

- First chest stores everything: median 5,776.4 ms before versus 132.3 ms after;
  ownership requests drop from 50 to one.
- Empty inventory: median 5,682.8 ms versus 0.036 ms callback time, with zero requests.
- Only favorites: median 5,729.4 ms versus 0.035 ms callback time, with zero requests.
- Per-type/quality/world-level conservation, remainder positions, favorites,
  timeout/quarantine, partial-transfer cancellation, late replies, owned recovery
  and favoriting every remaining stack during a pending request pass.
- All 24 scenarios plus one smoke and five safety scenarios pass: 78 timed
  operations. The standalone suite passes 13 cases and three adversarial groups
  (10,240 checked deposits plus repeats). Builds retain two existing MSB3277 warnings.

These are native-game ownership injections, not real two-client networking.
The historical baseline and fresh candidate are not randomized paired samples;
timings exclude next-frame HUD redraw/rendering. Full-chest insertion cost and
serial ownership waits while transferable items remain are not optimized.
Actual multiplayer, disconnects and disk persistence remain unverified.
The gameplay measurements used the development DLL with 1.1.2 version metadata.
The release-version rebuild and candidate ZIP have not been separately launched
or imported through a mod manager; release preparation adds no runtime evidence.
Static comparison of the packaged 1.1.3 DLL with that native-tested development
DLL passes for all 24 method definitions/IL bodies, 411 referenced metadata
records and the runtime string heap. Plugin ID and DLL identity are preserved.
Private measurements and reproducible harnesses are under
`.dev/nearbystore-ownership-lab/`; the sanitized side-by-side HTML/data are under
`.dev/nearbystore-earlystop-preview/public/`. No live profile/server deployment.


## Controls and behavior

- **Left Alt + F** while hovering a stack in your player inventory toggles its
  favorite status. A gold **F** marks favorites; they are never auto-deposited.
- Equipped items, queued-for-equip items and quest items also stay carried.
  Unequipped hotbar items are eligible unless favorited.
- Chests are visited nearest first. Like vanilla `Inventory.StackAll`, an item is
  eligible when its name already exists in that chest. Native insertion fills
  compatible stacks, then uses free slots for another stack, even if the original
  stack was full. Different quality/world-level items use separate slots.
  Empty chests and absent item types accept nothing; capacity-limited remainders
  stay in their original player-inventory slots.
- Favorite markers are stored on individual stacks in vanilla item custom data,
  not on inventory slots, so moving a stack does not unprotect it. Splitting
  copies its marker. Vanilla manual merging keeps the destination stack's marker;
  merging a favorite into a non-favorite stack can therefore remove protection.
  Re-favorite the merged stack if needed.
- Respects private-chest ownership, wards, busy chests and the vanilla network
  ownership handshake. No forced ownership or writes to remotely owned inventories.
  Tombstones, dropped-loot containers and containers without a placed Piece are excluded.
- The wand button appears only in the standalone player inventory.
  Containers, crafting stations, active crafting, inventory dialogs and blocking
  menus hide the whole panel. The basic crafting recipe list beside the ordinary
  inventory does not hide it. Dragging/splitting also hide it.
  Opening a blocking UI or closing inventory cancels further transfers; already
  stored items stay in the chests. Chat, teleportation and death block storage.
  A missing network response stops the operation after five seconds; late responses cannot trigger vanilla
  stack-all and accidentally deposit favorites.
  If it times out, open the affected chest normally, close it, and retry.
  Still-remote containers with an unanswered request remain quarantined until
  ownership arrives or they reload; vanilla replies have no request ID. Hover
  stack-all on that same chest may be consumed during quarantine; opening the
  chest normally avoids that RPC path.
- Search radius is spherical and does not require line of sight: chests behind
  walls or on another floor can be eligible. No automatic sorting on pickup.
- **OdinsFoodBarrels 1.4.0**: all 36 restricted container types were exercised in
  the real native client, including seed bag, meat freezer and spice shelf.
  Native `MoveItemToThis`/`AddItem` calls retain its Harmony restriction patches.
  Matching overflow, partial merging, favorites and prohibited-item rejection pass.
  Empty barrels require seeding, as with vanilla Stack items. Custom barrel settings,
  other versions and multiplayer remain unverified; no dependency is added.

Configuration: `BepInEx/config/jarki.nearbystore.cfg` after first plugin load:
`Storage.Radius` (1–50 metres, default 10) and
`Controls.FavoriteShortcut` (default `F + LeftAlt`). Favoriting remains available,
but no shortcut hint is displayed.

## Build and standalone verification

Requires the owned native Valheim managed assemblies and an existing BepInEx
**5.4.2351** reference profile; dependencies are read-only and not copied to output.

```sh
dotnet build mods/NearbyStore/NearbyStore.csproj -c Release \
  -p:GameDir=/path/to/Valheim -p:ReferenceProfile=/path/to/reference-profile
dotnet run --project tests/NearbyStore.Tests/NearbyStore.Tests.csproj -c Release
uv run python tools/release.py check NearbyStore
uv run python tools/release.py build NearbyStore \
  --game-dir /path/to/Valheim --reference-profile /path/to/reference-profile
uv run python tools/release.py verify dist/Jarki-NearbyStore-1.1.3.zip
```

Build output: `bin/Release/netstandard2.1/NearbyStore.dll` beneath this directory.
For an authorized isolated development profile, the DLL belongs under
`BepInEx/plugins/Jarki-NearbyStore/`. Do not copy game assemblies or test binaries.

Prepared 1.1.3 candidate: `dist/Jarki-NearbyStore-1.1.3.zip`.
ZIP SHA-256: `9f4bc4e3eb768d92eee5c1785c1f61006709324b447c757ad4fbd1f5ed41b38c`.
Pinned tcli 0.2.4 build, archive verification and extracted-payload lint pass:
six files, no lint failures or warnings. The public README is byte-identical to
the current live Thunderstore page; its prior edited wording differs from the
retained 1.1.2 ZIP. Icon, dependencies and install layout are unchanged.
No additional game launch, source push or package upload was performed.
Private release identity and packaging records: `.dev/release/nearbystore-1.1.3/`.

Published 1.1.2 archive: `dist/Jarki-NearbyStore-1.1.2.zip`.
ZIP SHA-256: `c42e3319c7fea2f20fee3291f2ed020bdd6c0cb20ca0508c208c8c533801252c`.
The public ZIP matches the upload and includes the approved page with before/after
screenshots. Rebuilt gameplay IL/signatures/runtime strings match the native-tested
wand DLL; this final rebuild was not separately launched.
Published archive: `dist/Jarki-NearbyStore-1.1.1.zip`; public SHA-256
`64b4158a6bee93df94e3f07123d0ee691239f6bd49c196c56dc31c3f120b8abd`.
Historical archive: `dist/Jarki-NearbyStore-1.1.0.zip`; public download matches SHA-256
`50164e0a0ef14e4a0e14076c89bf4c40cd670218aba0f78c4ab45356d2b792b8`.
Historical published archive: `dist/Jarki-NearbyStore-1.0.0.zip`; public SHA-256
`543600cdd70cf27c76567ee29af493747dc9e6a958513dc87f66ef90f35afef3`.
The public [README](package/README.md), [changelog](package/CHANGELOG.md) and
`thunderstore.toml` define the package. Only an authorized disposable development
profile was created; no live profile or server deployment was performed.

Verification of unpublished 1.1.2:

- **102 native-client checks including fixtures** pass with the square-wand 1.1.2 DLL,
  Valheim l-1.0.20 and OdinsFoodBarrels 1.4.0. First-open chest, raspberry barrel,
  workbench, active handcrafting, skills, compendium, trophies, achievements,
  variants, split/drag, pause, text viewer and text input hide the whole panel.
- Closing each tested overlay restores the button. Pending transfer cancellation
  retains late-response protection. Actual Tab and mouse input restore inventory
  and deposit 12 wood into a chest already holding a full 50 stack; unmatched
  stone/resin remain carried. The basic crafting list does not block the button.
- Right-side panel placement, on-screen bounds and separation from crafting pass
  at 70%, 100% and 130% UI scale on a 1280×720 surface. Screenshots inspected.
- The square 56×56-layout-unit button contains only original wand artwork, with no
  caption or hint. Native hover/pressed screenshots inspected; actual Alt+F still
  favorites the hovered item. Wand graphics do not intercept button clicks.
  Native border/material and SpriteSwap states are reused; no extracted assets bundled.
- **12/12 standalone transfer cases** pass. Build retains two existing MSB3277
  warnings. Screenshots inspected; successful run has no logged errors/exceptions,
  clean logout and game exit 0.
- First launch stopped before the menu because Steam API initialization failed;
  retry with Steam already running passed without production-code changes.
- Current private evidence: `.dev/nearbystore-wand-lab/evidence/`; prior style,
  placement and visibility evidence remains in the earlier isolated labs. Fresh disposable
  character/world in a copied development profile; no live profiles or servers
  touched. ZIP-install UI, every additional overlay guard, controller, other UI
  mods and multiplayer remain unverified. Earlier visibility/placement changes were
  pushed in `f59b755`; native styling remains local. The owner deferred Thunderstore publication.

Verification of corrected 1.1.1:

- **12/12 standalone cases** and **3/3 adversarial groups** pass: 10,240 deposits
  plus fixed-point repeats, 6,617 transferred units. Full matching-stack overflow,
  partial capacity, unmatched types, protection, metadata and conservation covered.
- The exact candidate DLL passed **673 native-client checks including fixtures**
  on Valheim l-1.0.20 / Unity 6000.0.75f1 / BepInExPack 5.4.2351 with
  OdinsFoodBarrels 1.4.0. Six scenarios matched real `Inventory.StackAll` contents
  and positions; actual Tab/button input exercised nearby stacking and barrel overflow.
- All 36 barrel prefabs passed eligibility, merge, overflow, favorite and
  prohibited-item checks. Default sizes/settings only; no special adapter needed.
- Result screenshots inspected; clean logout/game exit 0; no plugin errors or
  exceptions. Build passes with the two existing MSB3277 warnings.
- Private evidence: `.dev/nearbystore-vanilla-lab/evidence/`. Fresh disposable
  character/world in a copied development profile; no live profile/server changes.
  Test-started Steam was shut down afterward. Initial launch permission issue was
  resolved by invoking the loader with `sh`; a sign-in dialog was hidden without login.
- Multiplayer, denied wards, custom barrel configurations, controller/modpack
  compatibility, release-ZIP installation UI and uninstall/reinstall remain unverified.
- Published after the owner's separate **push and publish** instruction. Source
  commit `5ac8f66` is public. Public 1.1.1 ZIP passes archive verification and is
  byte-identical to the approved candidate; live listing/version/README inspected.
  The package's source-push sentence records the earlier preparation stage;
  the approved ZIP was intentionally kept unchanged for publication.

Historical verification of stack-only 1.1.0 (not the corrected behavior):

- **12/12 standalone cases** and **3/3 adversarial groups** pass, including
  320 seeds × 8 rounds, **10,240 deposits** plus fixed-point repeats. Coverage
  includes empty cells staying empty, full/missing/incompatible stacks, partial
  merges, multiple chests, protection, metadata and quantity conservation.
- Disposable production-core smoke with handwritten inventory doubles: 20 carried
  stone fills a 45/50 chest stack with 5 units, leaving 15 stone carried. Unmatched
  wood and favorite food stay carried; an empty chest stays empty. Seeding a wood
  stack and unfavoriting food permits only the available matching merges.
- Release build succeeds against owned game and existing isolated-loader references,
  with the same two MSB3277 warnings. The default reference profile was absent;
  verification used `.dev/nearbystore-lab/profile` as a read-only reference.
- **74 native-client checks pass**, including fixture assertions, on Valheim
  `l-1.0.20`, Unity 6000.0.75f1 and BepInExPack 5.4.2351. Actual Tab/Alt+F/button
  input filled wood/stone stacks across chests, retained exact overflow, unmatched
  resin, full-stack flint and favorite food, and left empty chest slots untouched.
  Repeated clicking moved nothing; seeding matching stacks and unfavoriting food
  enabled the remaining eligible transfers.
- The actual **Stack nearby** button, favorite marker and default/70%/130% scale
  screenshots were inspected. Favorite character-file save/reload, private/busy/
  range exclusion, injected ownership-delay/late-response/quarantine checks and
  clean logout passed. No plugin exception was found in the successful run's logs.
- Tested DLL SHA-256:
  `88344660e6b93e588b055cfc67d76454adec29e4f1cb9d214222b3d2f2abe598`.
  Evidence and archived harness sources are under `.dev/nearbystore-stack-lab/`.
  Two initial runs stopped at input-focus setup; the next run found an unexpectedly
  empty second chest. After disabling structural/weather wear on floating test
  chests and adding pre-transfer fixture checks, the identical production DLL
  passed. The earlier empty-chest cause was not independently proven.
- Testing used disposable local saves and a separate development profile, not
  multiplayer or a packaged ZIP. No live profile/server deployment was performed.
- The runtime-tested DLL retained version 1.0.0 during development. Release
  preparation changes only version metadata to 1.1.0, not gameplay code.
  The version-bumped DLL/ZIP has not been separately launched in-game.
- After exact-candidate owner approval, pinned tcli published 1.1.0 successfully.
  The public ZIP passed archive verification and matches the approved hash.
  All 23 method bodies, signatures, member/type references and bytecode strings
  match the native-game-tested build; only release version metadata changed.
  Live listing/README/icon verified. Moderation status is unreviewed.

Historical verification of published 1.0.0 (not stack-only runtime evidence):

- Release build succeeds with zero errors and two existing MSB3277 framework
  conflicts (`System.Net.Http`, `System.IO.Compression`).
- **13/13 standalone cases** and **3/3 adversarial groups** pass; the latter
  exercise 320 deterministic seeds × 8 rounds, 20,480 deposits plus fixed-point
  repeats, using handwritten inventory API doubles.
- **38 native-client checks** pass (including fixture assertions) on Valheim
  `l-1.0.20`, Unity 6000.0.75f1 and BepInExPack 5.4.2351. Real Tab/Alt+F/button input
  stored wood/stone while retaining favorite food; unfavoriting enabled its deposit.
  Character-file save/reload preserved favorites. Matching-first/overflow,
  private/busy/range exclusion and clean logout passed. Default/70%/130% UI-scale
  screenshots were inspected.
- A delayed-ownership race failed before the fix and passed afterward using
  injected ownership state on real game containers; late-response suppression and
  already-owned quarantine recovery passed. This is not two-client verification.
- The DLL was extracted from the candidate ZIP into a fresh isolated profile,
  not hand-copied from build output. Mod-manager import UI was not tested.
- Multiplayer, denied wards, full modpack/controller compatibility and uninstall/
  reinstall remain unverified. Keep these limits in public release notes.

Original code: MIT, copyright 2026 Jarki. Developed with OpenAI Codex assistance.
