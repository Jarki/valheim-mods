# NearbyStore

Client-side Valheim BepInEx plugin. Open your inventory and click **Store nearby**
below the player inventory to deposit carried items into placed chests within
**10 metres**. No server plugin or Jotunn dependency.
[NearbyStore 1.0.0 is published](https://thunderstore.io/c/valheim/p/Jarki/NearbyStore/);
no live profile or server deployment was performed.

## Controls and behavior

- **Left Alt + F** while hovering a stack in your player inventory toggles its
  favorite status. A gold **F** marks favorites; they are never auto-deposited.
- Equipped items, queued-for-equip items and quest items also stay carried.
  Unequipped hotbar items are eligible unless favorited.
- Storage first visits chests already containing that item type, nearest first.
  Remaining items use any available nearby chest space, nearest first. Empty
  chests can receive items. Full chests leave overflow in your inventory.
- Favorite markers are stored on individual stacks in vanilla item custom data,
  not on inventory slots, so moving a stack does not unprotect it. Splitting
  copies its marker. Vanilla manual merging keeps the destination stack's marker;
  merging a favorite into a non-favorite stack can therefore remove protection.
  Re-favorite the merged stack if needed.
- Respects private-chest ownership, wards, busy chests and the vanilla network
  ownership handshake. No forced ownership or writes to remotely owned inventories.
  Tombstones, dropped-loot containers and containers without a placed Piece are excluded.
- Close any chest before using the button. Dragging, splitting, menus, chat,
  teleportation and death disable storage. Closing inventory cancels further
  transfers; already stored items stay in the chests. A missing network response
  stops the operation after five seconds; late responses cannot trigger vanilla
  stack-all and accidentally deposit favorites.
  If it times out, open the affected chest normally, close it, and retry.
  Still-remote containers with an unanswered request remain quarantined until
  ownership arrives or they reload; vanilla replies have no request ID. Hover
  stack-all on that same chest may be consumed during quarantine; opening the
  chest normally avoids that RPC path.
- Search radius is spherical and does not require line of sight: chests behind
  walls or on another floor can be eligible. No automatic sorting on pickup.

Configuration: `BepInEx/config/jarki.nearbystore.cfg` after first plugin load:
`Storage.Radius` (1–50 metres, default 10) and
`Controls.FavoriteShortcut` (default `F + LeftAlt`). The current shortcut is shown
below the button.

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
uv run python tools/release.py verify dist/Jarki-NearbyStore-1.0.0.zip
```

Build output: `bin/Release/netstandard2.1/NearbyStore.dll` beneath this directory.
For an authorized isolated development profile, the DLL belongs under
`BepInEx/plugins/NearbyStore/`. Do not copy game assemblies or test binaries.
Published archive: `dist/Jarki-NearbyStore-1.0.0.zip`; its public download matches
SHA-256 `543600cdd70cf27c76567ee29af493747dc9e6a958513dc87f66ef90f35afef3`.
The public [README](package/README.md), [changelog](package/CHANGELOG.md) and
`thunderstore.toml` define the package. Only an authorized disposable development
profile was created; no live profile or server deployment was performed.

Verification:

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
