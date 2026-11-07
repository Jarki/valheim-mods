# Valheim mod development

Read `README.md` and `docs/DEVELOPMENT.md` for project context. For server management, deployment, client-profile operations or connection troubleshooting, use sibling `../valheim-server` and read its `.agents/skills/server-management/SKILL.md` plus state reference before acting.

## Universal Modder

- For mod development, start with `.agents/skills/mod-any-game/SKILL.md`, then the relevant skills and Unity engine reference. Project-owned skills are indexed in `docs/DEVELOPMENT.md`.
- Run `uv run um` from this repository, including where upstream skills say `um` or `bin/um`; never install it user-wide.
- Search prior work with `uv run um kb search "valheim"`; read notes with `uv run um kb show`. Upstream `knowledge/`, `examples/` and `um/` paths are not local; see `docs/DEVELOPMENT.md` for the upstream revision.

## Thunderstore release work

- Read `docs/RELEASING.md` and `.agents/skills/publish-mod/SKILL.md` before release preparation.
- Before packaging, run `uv run python tools/release.py check <ModName>` for the selected active mod;
- Use `tools/release.py` with the pinned local tcli, read-only owned game/loader references, and verify every candidate ZIP. No global CLI or ad-hoc ZIPs.
- Preserve plugin IDs, DLL names, save keys and third-party attribution. Never bundle dependencies, test plugins or development state.

## Active scope

MobThreat, NearbyStore and Rotheart are active. Firearm Arsenal (`PistolMod`), Startup Helper (including server joining) and Server Metrics are owner-paused: preserve their files; do not develop, build, test, deploy or publish them without explicit resumption. Historical plans and build-all instructions do not override this pause.

## Boundaries

- Setup/build/package work does not authorize game launches, loader installation, deployment, restarts, remote access or server changes.
- Obtain explicit approval before driving the game, installing loaders, publishing packages/profiles, changing servers or submitting upstream field notes. Each upload needs separate approval.
- Preserve working profiles and saves. Use disposable local saves and a separate development profile; server backup/restore belongs to the sibling repository.
- Native Linux Valheim uses Linux Doorstop, not Proton DLL overrides.
- Never commit credentials, saves, backups, game assemblies, extracted assets, decompiled game source or operational state.
