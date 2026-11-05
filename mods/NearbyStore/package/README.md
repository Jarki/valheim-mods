# NearbyStore

An inventory **Store nearby** button sends carried items to nearby placed chests.
**Left Alt + F** favorites the hovered inventory stack so it stays with you.
Client-side; no Jotunn dependency or server installation.

**Tested locally, multiplayer unverified.** The inventory workflow and character
favorite save/reload were exercised in an isolated native Valheim client. This
does not establish live-server or modpack compatibility. Try a separate profile
and disposable saves first; inventory automation moves real items, so keep backups.

## Use

1. Open your inventory. Hover a stack and press **Left Alt + F** to toggle its
   favorite marker. A gold **F** identifies favorites.
2. Close any open chest, then click **Store nearby** below the player inventory.
3. Chests within **10 metres** are visited nearest first, matching item types
   first, then any free space. Full chests leave overflow carried.

Favorites, equipped items, queued-for-equip items and quest items stay carried.
Unequipped hotbar items are eligible unless favorited. Empty placed chests can
receive items. Private ownership, wards, busy containers and the vanilla ownership
handshake are checked; tombstones, dropped loot and non-Piece containers are excluded.
Search is spherical, including through walls and between floors.

Favorites belong to stacks, not slots, using vanilla item custom data. Moving or
splitting a stack retains its marker. **Manual merging keeps the destination
stack's marker**: merging a favorite into an unmarked stack can remove protection.
Re-favorite the merged stack if needed. NearbyStore never deposits a favorite
source stack. Favorite persistence was verified through an actual disposable
character-file save and reload in the running game.

Dragging, splitting, chat, menus, death and teleportation disable storage. Closing
inventory cancels remaining transfers without undoing completed transfers. A missing
network response stops the operation after five seconds; late responses from this
operation are intercepted rather than running vanilla stack-all on favorites.

After a timeout, **open the affected chest normally, close it, then retry**.
Already-owned chests can be used without another network request. A still-remote
chest with an unanswered request is deliberately skipped until ownership arrives
or the container reloads: vanilla replies have no request ID, so overlapping
retries cannot safely be distinguished from late replies. A hover-stack request
on that same quarantined chest can also be consumed by the late-response guard;
opening the chest normally avoids that RPC path. No automatic retry is attempted.

## Requirements and installation

Requires Valheim and **denikson-BepInExPack_Valheim 5.4.2351**. Built and locally
tested on native Valheim **l-1.0.20**, Unity **6000.0.75f1**.

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

Other inventory, chest, UI, controller and sorting mods have not been compatibility
tested. Multiplayer, real remote-owner concurrency and denied wards remain
unverified. Private/busy/out-of-range exclusion was exercised locally. This is
not a claim that the plugin is safe on a live server.

For missing UI or errors, include game/mod versions, reproduction steps and relevant
redacted `BepInEx/LogOutput.log` entries in a report. Never post private saves,
passwords or tokens.

## Verification, license and credits

Release build succeeds with two existing MSB3277 framework-reference conflicts.
**13/13 standalone cases** and **3/3 adversarial groups** pass. The latter include
320 deterministic seeds, 8 rounds and **20,480 checked deposits**, plus fixed-point
repeats, testing protection, compatibility, conservation and source-position stability
with handwritten inventory API doubles.

An isolated native client completed **38 checks**, including fixture assertions:
real Tab/Alt+F/button input, favorites retained while 32 wood/stone units were stored,
unfavorite then storing 8 food units, matching-first/overflow behavior,
private/busy/range exclusions, actual character-file favorite save/reload, and clean
logout. Screenshots were inspected at default, 70% and 130% UI scale.

A granted-before-ownership-update race was reproduced before the fix and passed
afterward using injected ownership transitions on real local game containers.
Late canceled responses and already-owned quarantine recovery were also exercised.
These are not two-client network tests. The candidate DLL was installed from the
release ZIP into a fresh isolated profile; mod-manager local-import UI was not tested.

One earlier automation run failed the second-storage destination assertion before
its quantities were logged. Two instrumented runs of the identical DLL, in fresh
and reused disposable worlds, passed with zero food left carried and eight units
in the intended chest. The earlier assertion's cause is unconfirmed; its evidence
is retained rather than treated as proof of a fixed production defect.

Original code and programmatic package icon: **MIT, copyright 2026 Jarki**, see
`LICENSE`. Valheim, BepInEx and Harmony belong to their respective owners; no game,
loader, library binaries or extracted assets are included. Developed and packaged
with **OpenAI Codex (gpt-6.1-sol)** assistance; categorized **AI Generated**.

[Project repository](https://github.com/Jarki/valheim-mods) ·
[Report issues](https://github.com/Jarki/valheim-mods/issues).
NearbyStore source has not been pushed to the public repository as part of this
release preparation.
