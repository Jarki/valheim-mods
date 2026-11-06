# NearbyStore 1.1.2

An inventory **Stack nearby** button applies vanilla-style **Stack items** across nearby placed chests.
**Left Alt + F** favorites the hovered inventory stack so it stays with you.
Client-side; no Jotunn dependency or server installation.

**1.1.2 moves the button to the right of inventory and fixes menu overlap.**
The button and shortcut hint hide while a container, crafting station, active crafting
operation or blocking overlay is open, then return beside the standalone player inventory.
Stacking still matches vanilla: fill compatible stacks, then use free slots only
in chests already holding that item. Unmatched items stay carried.
Native-client checks include **OdinsFoodBarrels 1.4.0**; multiplayer remains
unverified. Try disposable saves first and keep backups: this moves real items.

## Use

1. Open your inventory. Hover a stack and press **Left Alt + F** to toggle its
   favorite marker. A gold **F** identifies favorites.
2. Close any container, crafting station or overlay, then click **Stack nearby**
   to the right of the standalone player inventory. Its normal basic crafting list is allowed.
3. Chests within **10 metres** are visited nearest first. Like vanilla **Stack items**,
   each chest accepts only item names already present in that chest.
4. Compatible stacks are filled first; remaining units use free slots in that
   matching chest. A full stone stack does not prevent another stone stack.
5. Items absent from all eligible nearby chests stay carried. If matching chests
   run out of room, the remainder stays in its original inventory slot.

Favorites, equipped items, queued-for-equip items and quest items stay carried.
Unequipped hotbar items are eligible unless favorited. Empty chests receive nothing.
Vanilla stack limits, quality/world-level merge rules and slot placement apply.
Different quality/world-level items can occupy separate slots in a matching chest.
Private ownership, wards, busy containers and the vanilla ownership handshake are
checked; tombstones, dropped loot and non-Piece containers are excluded.
Search is spherical, including through walls and between floors.

Favorites belong to stacks, not slots, using vanilla item custom data. Moving or
splitting a stack retains its marker. **Manual merging keeps the destination
stack's marker**: merging a favorite into an unmarked stack can remove protection.
Re-favorite the merged stack if needed. NearbyStore never deposits a favorite
source stack. Favorite persistence was verified through an actual disposable
character-file save and reload in the running game.

Containers, crafting stations, active crafting, inventory dialogs, dragging,
splitting, chat and blocking menus hide the entire button/hint panel. Death and
teleportation also block storage. Closing inventory or opening a blocking UI
cancels remaining transfers without undoing completed transfers. A missing network
response stops the operation after five seconds; late responses from this operation
are intercepted rather than running vanilla stack-all on favorites.

After a timeout, **open the affected chest normally, close it, then retry**.
Already-owned chests can be used without another network request. A still-remote
chest with an unanswered request is deliberately skipped until ownership arrives
or the container reloads: vanilla replies have no request ID, so overlapping
retries cannot safely be distinguished from late replies. A hover-stack request
on that same quarantined chest can also be consumed by the late-response guard;
opening the chest normally avoids that RPC path. No automatic retry is attempted.

## Requirements and installation

Requires Valheim and **denikson-BepInExPack_Valheim 5.4.2351**. Native-client
checks used Valheim **l-1.0.20**, Unity **6000.0.75f1**.

- **Mod manager:** install `Jarki-NearbyStore` in a separate Valheim profile and
  accept its BepInEx dependency. Launch modded using your normal loader route.
- **Manual:** with Valheim closed and a working OS-appropriate BepInEx loader,
  copy `plugins/NearbyStore/` into `BepInEx/plugins/`, resulting in
  `BepInEx/plugins/NearbyStore/NearbyStore.dll`. Keep `LICENSE`; remove duplicate
  NearbyStore DLLs. Native Linux uses Linux Doorstop, not Proton DLL overrides.

## Configuration

First load creates `BepInEx/config/jarki.nearbystore.cfg`. Edit while the game is
closed:

```ini
[Storage]
Radius = 10

[Controls]
FavoriteShortcut = F + LeftAlt
```

Radius is in metres, constrained to 1–50. The current favorite shortcut appears
below the button. Favorites can be toggled in the player inventory even while a
chest is open; storing requires the chest to be closed.

## Uninstall, compatibility and troubleshooting

Close Valheim and remove `BepInEx/plugins/NearbyStore/NearbyStore.dll`; remove its
config separately if desired. No custom item prefabs or world objects are added.
Favorite custom-data markers are inert without the plugin. Already deposited items
remain where they were stored. Uninstall/reinstall and cross-version behavior
have not been runtime-tested.

**OdinsFoodBarrels 1.4.0:** tested with the exact released DLL in an isolated
native client. All **36** restricted container types, including the seed bag,
meat freezer and spice shelf, passed nearby eligibility, matching overflow,
partial merging, favorites and prohibited-item rejection checks. A real button
click also deposited raspberry overflow into a full-stack raspberry barrel.
The mod's own inventory restrictions remain active; no hard dependency or
special-case bypass is added. **Empty barrels still need an initial item**, just
like vanilla Stack items. Custom barrel settings and other versions are unverified.

Other inventory/UI/controller mods, multiplayer, real remote-owner concurrency,
denied wards and uninstall/reinstall remain unverified. NearbyStore is client-only;
install OdinsFoodBarrels on the sides required by its author. Local checks are
not a claim of live-server compatibility.

For missing UI or errors, include game/mod versions, reproduction steps and relevant
redacted `BepInEx/LogOutput.log` entries in a report. Never post private saves,
passwords or tokens.

## Verification, license and credits

The right-side **1.1.2 DLL** passed **95 native-client checks including fixtures**:
first-open chest, Odin raspberry barrel, workbench, active handcrafting, skills,
compendium, trophies, achievements, variants, split/drag, pause and text overlays
hide the whole panel; closing them restores it. Pending-transfer cancellation and
late-response suppression pass. Actual Tab and mouse input then deposit 12 wood
into a chest already holding a full 50 stack, leaving unmatched stone/resin carried.
Screenshots were inspected; clean logout/exit, no errors in the successful run.
The panel stays to the right, fully on-screen and clear of the crafting menu at
70%, 100% and 130% UI scale on 1280×720. The initial panel width clipped crafting
at 130%; reducing it to 240 layout units fixed the overlap.
**12/12 standalone transfer cases** pass; the Release build retains two existing
MSB3277 warnings. A first launch failed Steam API initialization before the menu;
retrying with Steam already running passed without production-code changes.
This is not verification of every additional overlay guard or third-party UI mod.

Historical **1.1.1** transfer verification: **12/12 standalone cases** and
**3/3 adversarial groups** passed:
320 seeds, 8 rounds and **10,240 deposits** plus fixed-point repeats. Tests cover
matching overflow, absent types, full capacity, quality/world-level separation,
favorites, metadata, quantity conservation and carried-slot stability.

The exact **1.1.1 gameplay DLL** completed **673 native-client checks**, including
fixture assertions, on Valheim **l-1.0.20**, Unity **6000.0.75f1**, BepInExPack
**5.4.2351**, with **OdinsFoodBarrels 1.4.0** loaded:

- Six scenarios compared real vanilla `Inventory.StackAll` against production
  transfer, with identical inventory contents and positions.
- Actual Tab and button clicks verified full-stack overflow, partial merges across
  chests, favorites, absent types, repeated-click no-op and private/busy/range exclusions.
- All 36 barrel types passed native inventory checks; their item restrictions
  rejected disallowed transfers even with an invalid matching fixture item present.
- Inventory/result screenshots were inspected; the client logged out and exited
  cleanly, with no logged plugin errors or exceptions.

Verification used a separate profile and disposable local saves. The test did not
change live profiles or servers. Loader startup initially needed `sh` because the
copied launch script was not executable; a Steam sign-in dialog was moved off the
test surface without signing in. No production-code workaround was needed.
The release ZIP's installation UI and multiplayer have not been tested.
Earlier releases' runtime evidence and limitations remain in the source development log.

Original code and programmatic package icon: **MIT, copyright 2026 Jarki**, see
`LICENSE`. Valheim, BepInEx and Harmony belong to their respective owners; no game,
loader, library binaries or extracted assets are included. Developed with
**OpenAI Codex** assistance: earlier release work used **gpt-6.1-sol**; this
stacking correction, compatibility verification and UI fix used **gpt-6-astra**.
Categorized **AI Generated**.

[Project repository](https://github.com/Jarki/valheim-mods) ·
[Report issues](https://github.com/Jarki/valheim-mods/issues).
[NearbyStore source](https://github.com/Jarki/valheim-mods/tree/main/mods/NearbyStore).
