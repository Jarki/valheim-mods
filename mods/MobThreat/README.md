# MobThreat

New, independent client-only BepInEx 5 plugin. Displays `~N hits` above nearby
hostile mobs: the approximate number of unblocked attacks needed to kill the local
player at their **current** health. One or two hits are red. Unknown or zero
estimated weapon damage shows no label.

Published as **[Jarki-MobThreat 1.0.1](https://thunderstore.io/c/valheim/p/Jarki/MobThreat/)**
on 2026-11-05. The public ZIP matches the verified local artifact. The owner
accepted the missing fresh clean-profile archive-import test; publication does not
add runtime compatibility evidence. See [release records](../../docs/RELEASING.md).

For 1.0.1, standalone arithmetic tests and a headless simulation of the production
display code passed, including known-to-unknown cleanup and label-pool reuse.
Release build/archive checks passed. This update has not been visually retested
in game; the real-client records below describe earlier versions.


## Estimate

- Samples every 0.25 seconds, projects labels above the collider top every frame.
- Uses the strongest primary/secondary attack among the mob's initialized inventory
  weapons and current/unarmed weapon. This is not a prediction of AI attack selection.
- Includes weapon quality/world-level damage, creature level, an approximate midpoint
  vanilla damage roll, and missing-health attack multipliers.
- Applies nearby-player difficulty and enemy damage rate, current player equipment
  and status-effect resistance, body armor (including status modifiers), local
  damage-taken multiplier, and world-level enemy base damage.
- Uses the installed game's `HitData` resistance and armor implementation. Does not
  inflict damage, simulate hits on the player, consume attack RNG, invoke potentially
  stateful status-effect attack/damage hooks, or change equipment.
- Excludes players, non-hostile creatures, dead creatures, and hidden-HUD creatures.
  Labels hide off-screen, behind terrain/buildings, when HUD is hidden, and when
  no living local player is available. Registry entries are refreshed and released
  on world exit/disable; no scene callbacks, Harmony patches, tasks, or blocking waits.

This is an approximation, not a survival guarantee. Assumes no blocking, parrying,
dodging, healing, armor degradation or changing defenses. Full fire/poison/spirit
payloads are included, but DOT admission thresholds/timing/stacking, wetness,
attack chains, custom status hooks, synchronized random-skill overrides,
projectile ammo, spawned AOE damage, and mod-defined attacks can differ.
Non-humanoid or damage-less attacks without a finite estimate have no label.
Invulnerability/debug modes are not modeled. Remote equipment/AI selection can differ
from locally available weapons. The strongest known attack is not a guaranteed bound
on unknown secondary effects.

## Build

Requires .NET SDK, an owned native Valheim installation, and BepInEx 5 references.
No Jotunn or Harmony dependency. Game and loader assemblies are read in place and
are not bundled.

```sh
dotnet build mods/MobThreat/MobThreat.csproj -c Release \
  -p:GameDir=/path/to/Valheim \
  -p:ReferenceProfile=/path/to/profile
dotnet run --project tests/MobThreat.Tests/MobThreat.Tests.csproj -c Release
```

Output: `bin/Release/netstandard2.1/MobThreat.dll`.
For an approved isolated development-profile test, put only this DLL in that
profile's `BepInEx/plugins/MobThreat/`. Do not load the old MobThreat DLL alongside
this one. No server-side installation is needed. No deployment is performed by
the build.

Config: `BepInEx/config/local.mobthreat.cfg`, generated when loaded:

```ini
[Display]
Enabled = true
MaxDistance = 35
```

Distance is in metres, constrained to 5–100. Uses pooled screen-space TextMeshPro
labels with the native enemy-name SDF font. Unity 6's missing IMGUI
`LegacyRuntime` font makes default IMGUI text unsuitable here. Does not modify
vanilla enemy health bars; the plugin owns and hides its overlay on world exit.

## Verification (2026-11-05)

Release build succeeded against installed Valheim assemblies and BepInEx 5.4.2351,
using .NET SDK 10.0.113. Two existing game-reference `MSB3277` version-conflict
warnings (`System.Net.Http`, `System.IO.Compression`); no compiler errors.
Standalone lethal-hit regressions pass, including exact lethal boundaries,
fractional damage, invalid/unknown values and overflow. A throwaway console smoke
harness executed the production mitigation method against the real installed
`HitData` implementation: unarmored, both armor branches, resistance-before-armor,
difficulty-before-armor, local multiplier after armor, world-level addition, and
immunity all passed with expected damage and hit counts.

Approved isolated native-client run completed with only MobThreat and a temporary
scenario plugin. **16 runtime checks passed:** main menu and fresh private-world
startup, visible overhead label/head projection, current-health update, real armor
update, HUD hiding/restoration, plugin disable/re-enable, mob destruction/reference
release, and zero active labels after world exit. Screenshots were inspected:
Greydwarf `~9 hits` at 100 HP / 1 armor, red `~2 hits` at 20 HP / 1 armor,
and `~33 hits` at 100 HP / 14 armor after equipping Iron Scale Mail.

The first render attempt exposed missing IMGUI `LegacyRuntime` font data; the
final implementation uses native enemy-name TextMeshPro font assets and runs
after the normal camera update. Final runtime logs contain no exceptions or
font-loading errors. Earlier helper failures were fixture errors: calling
`Game.SkipIntro()` requests respawn, invalidating the test player; writing a
camera transform without updating free-camera state also misaligns test captures.

Local evidence: `.dev/mobthreat/evidence/` (`game-run6.log`, `final-bepinex.log`,
`smoke.log`, `complete.txt`, and the three health/armor screenshots). The temporary
scenario source/DLL was removed after verification. Only the normal MobThreat DLL
remains in the separate development profile. Existing profiles and saves were not
selected or modified; no server changes or production deployment.

**Not yet verified:** multiplayer equipment, broad creature/special-attack coverage,
occlusion across all environments, and interactions with the full Northern Hearth
mod set. This new implementation's isolated startup success does not identify or
prove a fix for the removed external plugin's freeze.

### Adversarial UI fixes

- `Clear()` now deactivates each pooled text object before resetting the active
  count. The original two-mob hide/remove/restore and disable/remove/re-enable
  probes now leave one rendered label for the one surviving mob, zero orphans.
- Uses the game's `WorldToScreenPointScaled()` for window-space overlay placement.
  At a 1280×720 window / native 640×360 render target, measured position error
  fell from 368.5 pixels to 0.000; full-resolution error also remained 0.000.
  Native graphics settings were restored after the scenario.
- Both fixes were exercised sequentially in a fresh disposable native client.
  Screenshots inspected; no exceptions or font-loading errors in the fixed run.
  Release build and standalone lethal-hit regressions passed. Existing reference
  version warnings remain.
- Before-fix evidence: `.dev/mobthreat-live-review/evidence/`.
  Fixed-run logs, screenshots and archived probe sources:
  `.dev/mobthreat-ui-fix/evidence/`.
- Damage estimation, including the tiny-DOT approximation, is intentionally
  unchanged at the user's request. No Northern Hearth deployment performed.

