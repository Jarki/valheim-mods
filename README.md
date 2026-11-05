# Valheim mods

Source repository for the Northern Hearth Valheim plugins, their C# tests, mod
development records, and the project-local Universal Modder toolkit. It does not
contain client profile archives, monitoring deployment assets, server state, or
server operations.

## Repository ownership

- [`mods/PistolMod/`](mods/PistolMod/README.md) — client firearm mod and original
  assets.
- [`mods/StartupHelper/`](mods/StartupHelper/README.md) — optional client startup
  helper.
- [`mods/ServerMetrics/`](mods/ServerMetrics/README.md) — server-only metrics
  plugin source and metric contract.
- `tests/` — C# regression and in-game test sources for these plugins.
- `MODLOG.md`, `MODDING_PLAN.md`, and `NEXT-STEPS.md` — development history and
  current mod plans.

The sibling [`valheim-server`](../valheim-server/) repository owns modpack
tooling, client archives, monitoring assets, deployment state, and all operational
procedures. Use its
[`server-management` skill](../valheim-server/.agents/skills/server-management/SKILL.md)
for server work and connection troubleshooting. The external MobThreat codebase
remains owned by its remote source and is not vendored here.

## Server metrics exporter

[`mods/ServerMetrics/`](mods/ServerMetrics/README.md) contains the original,
server-only Prometheus exporter. It exposes concrete connection observations,
save/native-backup inventory, loaded mod versions, and character-name
playtime/death leaderboards without Steam identity lookups. Monitoring
configuration and production evidence live only in the sibling server repository.

## Mod development toolkit

Universal Modder CLI **0.2.0** and **11 mod-development skills** are installed from
[`rehan-remade/universal-modder`](https://github.com/rehan-remade/universal-modder)
at revision `c1dff21663665dd17af289744a301b66e369605e`.
The CLI and its Python dependencies live only in this project's ignored `.venv/`,
managed by `pyproject.toml` and `uv.lock`. The former user-wide `uv tool`
installation has been removed. Skills are vendored in `.agents/skills/` with
their bundled references/scripts and MIT license (`.agents/skills/LICENSE`).
`AGENTS.md` routes assistants to the skills and project-local CLI.

Reproduce the CLI installation from the project root:

```sh
uv sync --locked
```

Run through `uv` without activating the environment or changing global PATH:

```sh
uv run um --help
uv run um kb search "valheim"
uv run um scan "$HOME/.local/share/Steam/steamapps/common/Valheim"
```

Knowledge-base commands automatically fetch/cache upstream notes when no local
knowledge base exists; those notes track upstream `main`, independently of the
pinned CLI and skills. The knowledge-base cache remains user-level; the executable
and Python dependencies are project-local. References to upstream-root `knowledge/`,
`examples/`, `bin/um`, or `um/` inside skills are not paths in this project.
Invoke upstream skills' bare `um` commands as `uv run um` here.

To update, choose and review a new upstream revision, change the Git revision in
`pyproject.toml`, run `uv lock` and `uv sync --locked`, replace the 11 vendored
mod-development skill directories from that revision's `skills/`, retain the upstream license, and update
this revision record together. Do not overlay an update that would leave obsolete
upstream files behind.

Python dependencies are managed by `uv`. FFmpeg is available locally. This
workstation currently has only .NET SDK 6.0.428 at
`$HOME/.dotnet/dotnet`; the `net10.0` PistolMod standalone tests require
a .NET 10 SDK before they can run. Blender is required only to regenerate the
committed firearm models and icons.
No fal/MCP or other paid asset service is configured by this setup. CLI/skills
installation does not install BepInEx into a game or authorize server changes.

Project-local setup verification: `uv run --locked um --version` reports
`universal-modder 0.2.0`; both the executable and imported `um` module resolve
inside this project's `.venv/`. A fresh shell outside the project finds no
global `um` executable. `uv run --locked um kb search valheim` returns the Mjolnir
and trap-taming notes. Earlier setup checks verified the read-only local game scan
(Unity Mono `6000.0.75f1`) and all skill entry points then vendored, including
the server-management skill now owned by the sibling server repository. No game
was launched and no live-server check was performed.

## 9mm Pistol mod

### Separate startup helper

[`mods/StartupHelper/`](mods/StartupHelper/README.md) contains an independent,
client-only BepInEx plugin for selecting an exact existing character and
automatically starting a private local world or joining a configured server.
It is disabled by default and attempts startup once per process. Local startup
has been exercised with the disposable `firearmlab` / `firearmlab2026` pair;
server joining/password submission remain unverified. The helper has no firearm
or Jotunn dependency. See its README for configuration and F8 cancellation.

### Firearm expansion

**Expansion in development (2026-10-27):** The working tree now targets
**Firearm Arsenal 1.1**, adding an AKM-style rifle, RPG, recorded audio and new
visuals. See [the current development README](mods/PistolMod/README.md) and
[MODLOG.md](MODLOG.md) for status. The 1.0 description and verification record
below describe the previous implementation, not verification of the expansion.

`mods/PistolMod/` implements a Glock-like, polymer-framed semi-automatic pistol.
The mod is named **9mm Pistol**, independently of the server name. Its low-poly
mesh, inventory icons, gunshot, and reload sounds are generated locally from
original code; no paid services, downloaded firearm assets, or publishing are
involved. It reuses Valheim's crossbow character stance and skill, with its own
slide, magazine, recoil, and muzzle-flash visuals.

### Gameplay

- **Attack** (default left mouse): one shot per press, minimum 0.16 seconds between
  shots. Holding attack does not repeat.
- **Secondary attack** (default middle mouse): reload over 1.8 seconds.
- 17-round magazine; new pistols are empty. A HUD shows loaded and reserve rounds.
  Interrupted reloads leave ammunition untouched; swapping weapons, dodging,
  staggering, and other incompatible actions cancel the reload.
- 35 base piercing damage, modified by the Crossbows skill and normal game damage
  rules. Bullets use swept collision, travel at 180 game units/second, and expire
  after 1.2 seconds. Walls block them.
- 300 durability, one point per shot at the default durability rate; repair at a
  forge. The vanilla crossbow stance occupies both hands, so no shield pairing.
- Magazine contents belong to each item and persist through inventory saves.

| Item | Crafting station | Ingredients | Output |
| --- | --- | --- | --- |
| 9mm pistol (`Pistol9mm`) | Forge level 2 | 20 Iron, 5 Copper, 5 Fine wood | 1 empty pistol |
| 9mm cartridges (`Ammo9mm`) | Forge level 1 | 1 Copper, 2 Coal, 1 Resin | 20 cartridges |

### Build and isolated local play

Requires the owned native Linux game, a .NET SDK supporting `netstandard2.1`,
and a local BepInEx/Jotunn reference profile. The build reads game and loader
assemblies in place; it does not copy them into the distributable.
Verified here with Valheim `l-1.0.20` / Unity `6000.0.75f1`,
BepInEx `5.4.2351`, Jotunn `2.30.2`, and .NET SDK `6.0.428`.

```sh
$HOME/.dotnet/dotnet build mods/PistolMod/PistolMod.csproj -c Release
$HOME/.dotnet/dotnet run --project tests/PistolMod.Tests/PistolMod.Tests.csproj -c Release
sh mods/PistolMod/launch-dev.sh -force-glcore
```

The plugin output is
`mods/PistolMod/bin/Release/netstandard2.1/PistolMod.dll`.
For nondefault locations, pass MSBuild properties `-p:GameDir=...` and
`-p:ReferenceProfile=...`; the launcher accepts `VALHEIM_GAME_DIR` and
`VALHEIM_REFERENCE_PROFILE`. Defaults use the Steam native game installation
and the existing `Valheim2` profile as read-only references.

The launcher copies loader files, Jotunn, and this plugin into
`.dev/pistol/profile`, using Linux Doorstop rather than Proton. It leaves the
Steam game directory and existing mod profiles unchanged. Local development
saves are isolated under
`.dev/pistol/config/unity3d/IronGate/Valheim` via `XDG_CONFIG_HOME`;
the client does **not** use the dedicated-server `-savedir` argument.
Steam Cloud characters/worlds can still appear: select only a **new local**
character and world. Do not join a live server from this development profile.
Remove custom pistols/cartridges from disposable saves before uninstalling the mod.

### Verification and limits

An isolated native client smoke run exercised actual forge crafting and material
consumption, equipping, empty-fire rejection, reload/partial reload, cooldown,
held-trigger behavior, durability, weapon-swap cancellation, inventory Save/Load,
dropping/picking up a loaded pistol, and reserve exhaustion. A real projectile
damaged a spawned boar; an intervening wall prevented damage. The final run also
verified barrel alignment, muzzle-flash activation, and synthesized-audio playback.
Five standalone magazine regression groups pass, including malformed persisted values and independent
items. Local runtime logs and screenshots are retained under
`.dev/pistol/evidence/run8/` and are not committed. The temporary automated
scenario has been removed; the development profile now contains the normal plugin.

The build succeeds with two `MSB3277` dependency-version warnings from the local
game/reference assemblies (`System.Net.Http` and `System.IO.Compression`).
The verified native OpenGL smoke run has no pistol exceptions. Multiplayer is
**not verified**; the plugin declares Jotunn's everyone-must-have-mod network
requirement and needs matching installations before a separately authorized
multiplayer test. No live server was changed, restarted, or tested.

## Repository split — 2026-11-05

This repository starts with fresh local Git history. The combined checkout,
including its original Git history and ignored development lab, is retained at
`../valheim-archive-20261105/`; it is not an active dependency of either new repo.
No GitHub repository was created, removed, or published by the split.

Verification from this repository: the pinned toolkit reports version 0.2.0;
asset validation passes; PistolMod, ServerMetrics, StartupHelper, and the in-game
test plugin build with SDK 6.0.428. StartupHelper and the in-game test build used
`-p:ReferenceProfile="$HOME/.config/r2modmanPlus-local/Valheim/profiles/Valheim2"`
because their default `.dev/reference-profile` is intentionally not copied.
Builds retain assembly-version conflict warnings; ServerMetrics also reports
unassigned JSON DTO field warnings. All 15 ServerMetrics regression groups pass.
The standalone PistolMod tests were not run: their declared `net10.0` target
requires an SDK unavailable on this workstation. No game was launched.

Public-source privacy cleanup removes local account paths and real deployment IPs.
Networking defaults/tests retain standard loopback, wildcard, multicast, and
documentation-only addresses. Secret files, live profile configuration, player
ledgers, and diagnostic logs are ignored; only sanitized examples belong in Git.
