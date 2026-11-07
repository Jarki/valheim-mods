# Firearm Arsenal — development build

**Paused by the owner (2026-11-05): no further development or publication for
now.** Source/assets/tests and the historical notes below are preserved. Resume
only on an explicit owner decision; MobThreat is the sole active release target.

Expands the original 9mm Pistol plugin with an AKM-style rifle and RPG-style
launcher. **Release build, asset validation, seven standalone regression groups,
and 142 native-client integration checks pass.** Verification details are below.

The original plugin GUID (`local.pistol9mm`), DLL name, pistol/ammo prefab names,
and item magazine save key are retained. Version 1.1 requires matching mods on
all peers. Do not mix it with the old 1.0 plugin.

## Controls and balance

| Weapon | Ammunition | Capacity | Fire interval | Reload | Base damage |
| --- | --- | ---: | ---: | ---: | --- |
| 9mm pistol | 9mm cartridges | 17 | 0.16s, press per shot | 1.8s | 35 pierce |
| AKM rifle | 7.62mm cartridges | 30 | 0.10s, hold for automatic | 2.5s | 42 pierce |
| RPG launcher | RPG rockets | 1 | 0.8s, press per shot | 3s | 160 blunt + 40 fire |

Attack fires; secondary attack reloads. All three use the Crossbows skill and
two hands. New weapons are empty. Ammunition is consumed only when a reload
finishes; interruptions preserve reserve ammunition. Loaded rounds persist per
item. Durability is 300, with the normal game durability rate applied per shot.
All repair at a forge. RPG movement penalty is 10%.

Bullets travel at 180 (pistol) or 240 (rifle) game units/second, lasting 1.2s.
Rockets travel at 45 units/second, with slight gravity and an 8s lifetime. Native
swept projectile collision prevents shooting through nearby walls.

### RPG blast rules

- Five-metre radius, linear falloff to zero at the edge, measured to collider bounds.
- Damage creatures, trees, destructible props and building pieces through native damage APIs.
- Each target is hit once, regardless of how many colliders it has.
- Solid geometry blocks blast. Occlusion is evaluated before any target is damaged.
- All enabled wards covering a building's pivot or hit point must permit the shooter.
  Access is evaluated before any wards are damaged. Ward owners/permitted users can
  damage their own protected structures.
- Native PVP, friendly-target and dodge rules apply; the shooter has native projectile immunity.
- No terrain deformation. Tool tier 2 permits ordinary stone structures; native
  damage resistance and tool-tier requirements still apply.
- Damage is evaluated on the owning shooter's client. If ownership migrates without
  that local shooter, splash damage is suppressed rather than using another player's
  ward permissions. Multiplayer behavior is not verified.

### Forge recipes

| Item / prefab | Forge level | Materials | Output |
| --- | ---: | --- | ---: |
| `Pistol9mm` | 2 | 20 Iron, 5 Copper, 5 FineWood | 1 |
| `Ammo9mm` | 1 | 1 Copper, 2 Coal, 1 Resin | 20 |
| `RifleAKM` | 2 | 35 Iron, 10 Copper, 15 FineWood | 1 |
| `Ammo762` | 1 | 2 Copper, 3 Coal, 1 Resin | 20 |
| `LauncherRPG` | 3 | 40 Iron, 10 Silver, 10 FineWood | 1 |
| `AmmoRocket` | 3 | 2 Iron, 5 Coal, 1 SurtlingCore | 1 |

## Art and animation

Original bevelled Blender meshes preserve moving slides/bolts and magazines.
`Tools/build_models.py` is the source; it exports the embedded mesh payload and
256px transparent icons. Unity Editor and paid asset services are not required.

The character's arms use a weapon-specific two-bone IK animation layer on
Valheim's skeleton, including aiming, recoil, finger grip and reload hand paths.
Neutral vanilla locomotion continues underneath rather than the crossbow stance.
This is procedural skeletal animation, not an
imported set of motion-capture clips. Rig names have been verified in the game
prefab; equipped hold/reload poses and runtime materials were inspected in-game.

Recorded audio is embedded with the plugin. See
[`Assets/Audio/ATTRIBUTION.md`](Assets/Audio/ATTRIBUTION.md) for mandatory CC-BY
credits, CC0 sources and adaptations. The pistol shot is a .45 1911 recording
used for the 9mm item; RPG reload uses AK mechanical handling as foley. The RPG
impact particles are cloned at runtime from the user's installed game's siege-bomb
effect, with inherited damage/terrain/audio components removed. No game assets
are distributed.

## Development

Building the plugins requires a .NET SDK supporting `netstandard2.1`, the owned
native Linux game, and BepInEx 5.4.2351/Jotunn 2.30.2 references. The standalone
tests target `net10.0` and require .NET SDK 10. Blender is needed only when
regenerating art; Blender 3.6.23 is known to work on the original SSE4.2/no-AVX
development VM, while the tested Blender 5.2.3 package crashed there with
`SIGILL`. Install local tools outside version control; no `.dev/tools` contents
are part of this repository.

```sh
blender -b --python mods/PistolMod/Tools/build_models.py
python mods/PistolMod/Tools/check_assets.py
dotnet build mods/PistolMod/PistolMod.csproj -c Release \
  -p:GameDir=/path/to/Valheim -p:ReferenceProfile=/path/to/reference-profile
dotnet run --project tests/PistolMod.Tests/PistolMod.Tests.csproj -c Release
```

For the ignored local assembly-only reference distribution, pass
`-p:ManagedDir="$PWD/.dev/reference-game/valheim_server_Data/Managed"` and
`-p:ReferenceProfile="$PWD/.dev/reference-profile"`. It is not a playable client.

`launch-dev.sh` uses Linux Doorstop and isolated `.dev/pistol` configuration.
Use only disposable **local** saves. The client still requires a signed-in owned
Steam account. Test destruction in a fresh world; see the
[server-management skill](../../../valheim-server/.agents/skills/server-management/SKILL.md)
for production/server constraints.

### Verification (2026-10-27)

Native Valheim `l-1.0.20`, Steam build `25909357`, Unity `6000.0.75f1`:

- All three weapons: equip, reload, trigger behavior, rifle sustained automatic
  fire, durability, interrupted reloads and native inventory save/load.
- Loaded recording playback, supported runtime materials, skeleton discovery,
  visible hold/reload poses and TMP magazine HUD.
- Native bullet/rocket travel and impacts, wall shielding, blast falloff/radius,
  unprotected structures, and denied/permitted ward access.
- All six forge recipes discovered and crafted through the normal crafting UI
  handler, with exact ingredient-consumption checks. The disposable fixture
  supplies station extensions and waives roof/fire/collision setup.

Results/screenshots: `.dev/pistol/evidence/arsenal-run4/` (**142 passed, 0 failed**).
After tightening the RPG reload hand/rocket alignment, the targeted weapon,
pose and audio rerun passed **59 checks** (`.dev/pistol/evidence/pose-final/`).
Local evidence clip: `.dev/pistol/evidence/firearm-demo.mp4`; its companion
`firearm-demo-credits.md` retains audio attribution.
Builds have two known MSB3277 framework-reference warnings and no errors.
Multiplayer and server deployment are unverified. Recording/playback checks do
not establish subjective sound quality. The separate
[Startup Helper](../StartupHelper/README.md) automates the disposable local world
for development; it is not a firearm dependency.

### Build and run on another Linux machine

Prerequisites: signed-in Steam with native Valheim installed, .NET SDK 10,
and an existing profile containing BepInEx 5.4.2351 and Jotunn 2.30.2. Match the
tested game version above. Models, icons and recordings are committed source
assets; Blender, `uv`, the VM's `.dev/` directory, and its saves are not needed.

Close Valheim first. From the repository root, set the two paths below to the
actual game and BepInEx/Jotunn profile on this machine (these are examples):

```sh
export VALHEIM_GAME_DIR="$HOME/.local/share/Steam/steamapps/common/Valheim"
export VALHEIM_REFERENCE_PROFILE="$HOME/.config/r2modmanPlus-local/Valheim/profiles/Valheim2"

dotnet build mods/PistolMod/PistolMod.csproj -c Release \
  -p:GameDir="$VALHEIM_GAME_DIR" -p:ReferenceProfile="$VALHEIM_REFERENCE_PROFILE"
dotnet build mods/StartupHelper/StartupHelper.csproj -c Release \
  -p:GameDir="$VALHEIM_GAME_DIR" -p:ReferenceProfile="$VALHEIM_REFERENCE_PROFILE"
dotnet run --project tests/PistolMod.Tests/PistolMod.Tests.csproj -c Release

install -Dm644 mods/StartupHelper/bin/Release/netstandard2.1/StartupHelper.dll \
  .dev/pistol/profile/BepInEx/plugins/StartupHelper/StartupHelper.dll
sh mods/PistolMod/launch-dev.sh -force-glcore
```

The launcher creates an isolated development profile and copies the loader,
Jotunn and firearm DLL into it. It uses native Linux Doorstop. Steam Cloud saves
can still appear: select/create disposable **local** saves for the first run.
Do not install the in-game test DLL for normal play. `-force-glcore` matches the
verified VM route; it can be omitted to use Valheim's default renderer.

The startup helper is initially disabled. After creating your local character
and world, quit and edit:

```sh
${EDITOR:-nano} .dev/pistol/profile/BepInEx/config/local.valheim.startuphelper.cfg
```

Set `[Startup] Enabled = true`, `Character` to the exact local `.fch` filename
without its extension, `CharacterSource = Local`, and `Mode = LocalWorld`.
Under `[LocalWorld]`, set `Name` to your exact world name and `Source = Local`.
See the [helper README](../StartupHelper/README.md) for a complete example.
The VM's `firearmlab` / `firearmlab2026` saves are not included in Git.
Launch again with the same shell command above; F8 cancels automatic startup.

For subsequent terminals, re-export the two paths if you use nondefault locations.
This command is for local play. Firearm multiplayer requires matching firearm
versions on the server and every client; installing only this client DLL is not
a verified way to join the existing dedicated server.

All current changes were developed with AI assistance. Keep audio attribution
with any future distribution. This repository has not published this expansion.
