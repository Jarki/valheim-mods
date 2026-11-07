# Jarki's Valheim mods

A source collection of independent Valheim BepInEx plugins under the confirmed
**Jarki** Thunderstore team.
**Published: [MobThreat 1.0.1](https://thunderstore.io/c/valheim/p/Jarki/MobThreat/) · [NearbyStore 1.1.1](https://thunderstore.io/c/valheim/p/Jarki/NearbyStore/) · [Rotheart 1.0.0](https://thunderstore.io/c/valheim/p/Jarki/Rotheart/).**
Publication and packaging checks are not fresh clean-profile runtime verification.

## Packages

**MobThreat, NearbyStore and Rotheart are active release targets.**
[NearbyStore](mods/NearbyStore/README.md) **1.1.1 is published** and its source is
pushed to GitHub. The correction matches vanilla Stack items:
fill matching stacks, then use spare slots only in chests already holding that item.
Native-client checks passed with OdinsFoodBarrels 1.4.0; multiplayer remains unverified.
**1.1.2 is an unpublished local UI fix:** place the button to the right of inventory
and hide it while containers, crafting stations or blocking overlays are open.
Its square, text-free magic-wand button passed 102 native-client checks;
see the development notes.

[Rotheart](mods/Rotheart/README.md) 1.0.0 adds a swamp trinket:
passive poison resistance and a damaging miasma triggered at 10 adrenaline.
Its first release is published, and the public ZIP matches the approved artifact.
The gameplay DLL passed 65 isolated native-client assertions; multiplayer remains unverified.
Firearm Arsenal, Startup Helper (including server joining), and Server Metrics
remain paused; their source, tests, assets and historical package definitions are
preserved.


All packages require `denikson-BepInExPack_Valheim` **5.4.2351**.

| Thunderstore package | Version | Install side | Additional dependency | Source / development notes |
| --- | --- | --- | --- | --- |
| `Jarki-MobThreat` | 1.0.1 | Client only | None | **Active** — [MobThreat](mods/MobThreat/README.md) |
| `Jarki-NearbyStore` | 1.1.2 local / 1.1.1 published | Client only | None | **UI fix locally tested; unpublished** — [NearbyStore](mods/NearbyStore/README.md) |
| `Jarki-Rotheart` | 1.0.0 | Server and every client; multiplayer unverified | `ValheimModding-Jotunn` 2.30.2 | **Published; locally tested** — [Rotheart](mods/Rotheart/README.md) |
| `Jarki-Firearm_Arsenal` | 1.1.0 | Server and every client; matching versions | `ValheimModding-Jotunn` 2.30.2 | **Paused** — [PistolMod](mods/PistolMod/README.md) |
| `Jarki-Startup_Helper` | 1.0.0 | Client only; optional, disabled by default | None | **Paused** — [StartupHelper](mods/StartupHelper/README.md) |
| `Jarki-Server_Metrics` | 1.4.0 | Dedicated server only | None | **Paused** — [ServerMetrics](mods/ServerMetrics/README.md) |

Firearm Arsenal adds a pistol, rifle and RPG. Startup Helper selects an existing
character and local world or configured server. Server Metrics exposes observed
server metrics for Prometheus. MobThreat shows approximate incoming hits to death.
Existing source directory names, plugin IDs and DLL names remain unchanged.

## Build and package

Use Python **3.11+**, `uv`, **.NET SDK 10** (current development SDK: **10.0.113**),
an owned Valheim installation, and an existing BepInEx reference profile.
From the repository root:

```sh
uv sync --locked
dotnet tool restore
uv run python tools/release.py check MobThreat
uv run python tools/release.py build MobThreat \
  --game-dir /path/to/Valheim --reference-profile /path/to/profile
uv run python tools/release.py verify dist/Jarki-MobThreat-1.0.1.zip
```

`check` validates metadata without launching a game. `build` performs a fresh
Release build, packages with locally pinned official Thunderstore CLI **0.2.4**,
and verifies the archive. Active release-wrapper targets are MobThreat, NearbyStore and
Rotheart. Paused projects remain excluded from current work. The wrapper reads
`VALHEIM_GAME_DIR` and `VALHEIM_REFERENCE_PROFILE` when flags are absent;
`--managed-dir` supports explicit server managed-assembly references. No command
above uploads anything. See [releasing](docs/RELEASING.md) for the full gates and
[development](docs/DEVELOPMENT.md) for toolkit and local-build guidance.

## Repository layout and boundaries

- `mods/<project>/README.md`: source/development documentation linked above.
- `mods/<project>/package/`: public README, changelog and icon;
  `thunderstore.toml` beside it defines each package.
- `tests/`: standalone regressions and in-game test sources.
- [MODDING_PLAN.md](MODDING_PLAN.md) and [NEXT-STEPS.md](NEXT-STEPS.md):
  retained development plans.
- `.agents/skills/`: vendored Universal Modder development skills.

The sibling [`valheim-server`](../valheim-server/) repository owns deployment,
modpack tooling, client archives, monitoring configuration and operational state.
It is **not a build or package dependency**. This repository does not contain live
profiles, server state or credentials. The historical combined checkout at
`../valheim-archive-20261105/` is not a dependency either.

## License, credits and verification limits

Original code and original assets are [MIT licensed](LICENSE), copyright 2026
Jarki. Third-party firearm audio retains its separate CC BY/CC0 terms and credits
in [ATTRIBUTION.md](mods/PistolMod/Assets/Audio/ATTRIBUTION.md), also included in
that package. Game, BepInEx and Jotunn binaries are not redistributed. The vendored
toolkit has its own upstream [MIT license](.agents/skills/LICENSE).

Development used AI assistance, including OpenAI Codex; this release preparation
uses `gpt-6.1-sol`. This does not identify the model used for every historical
change. Packages disclose AI assistance and use Thunderstore's `ai-generated`
category.

Existing development evidence includes Firearm Arsenal's 142 native-client checks
and 59 targeted pose/audio checks on Valheim `l-1.0.20`, Startup Helper's local-world
startup test, and MobThreat's real client UI tests including ghost-label/scaling
fixes. Firearm multiplayer and Startup Helper server joining remain unverified;
MobThreat's DOT approximation is intentionally unchanged. Server Metrics evidence
is historical tests and source, not a newly launched server. See each development
README for exact scope. These records do not establish that the release ZIPs
have been imported and tested in clean profiles. MobThreat's actual publication
and public-download verification are recorded in [releasing](docs/RELEASING.md).
