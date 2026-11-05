# Valheim mod development

Read `README.md` for the package overview and `docs/DEVELOPMENT.md` for development context. For server management, deployment, client-profile operations, or connection troubleshooting, switch to the sibling `../valheim-server` repository and read `../valheim-server/.agents/skills/server-management/SKILL.md` plus its state reference before acting.

## Universal Modder

The upstream mod-development skills, including their bundled references and scripts, are vendored under `.agents/skills/`. For mod development, start with `.agents/skills/mod-any-game/SKILL.md`, then read the task-specific skills and the Unity engine reference. Run the project-local CLI with `uv run um` from this repository (or `.venv/bin/um`); do not install it as a user-wide `uv tool`. Bare `um` and `bin/um` commands in upstream skills must be invoked through `uv run um` here.

Search prior work with `uv run um kb search "valheim"`. Skill references to upstream-root `knowledge/`, `examples/`, or `um/` paths refer to https://github.com/rehan-remade/universal-modder, not local project files. Use `uv run um kb show` for knowledge-base notes; retrieve other upstream references at the revision recorded in `docs/DEVELOPMENT.md`.

## Thunderstore release work

Read `docs/RELEASING.md` and the publishing skill before release preparation. Each mod has its own `thunderstore.toml` and public `package/` files; retain existing plugin IDs, DLL names and save keys. Use the pinned local `dotnet tcli` via `tools/release.py`, not a global CLI or ad-hoc directory zips.

Run `uv run python tools/release.py check MobThreat` before packaging. Build only with read-only owned game/loader references, and verify each candidate ZIP. Do not bundle dependencies, test plugins, development state, saves or credentials. Original code/assets are MIT; preserve separate third-party audio terms and attribution. CI validates MobThreat metadata and standalone behavior only, never downloads game assemblies or uploads packages. The confirmed team is `Jarki`; every actual upload still needs separate explicit approval.

MobThreat and NearbyStore are the active development/publication targets. The owner paused Firearm Arsenal (`PistolMod`), Startup Helper (including server joining), and Server Metrics. Preserve their source, tests, assets and historical package definitions; do not develop, build, test, deploy or publish them unless the owner explicitly resumes the relevant project. Historical plans and old build-all instructions do not override this pause.


## Boundaries

- Toolkit setup does not authorize game launches, loader installation, deployment, restarts, remote access, profile publication, or any server change.
- Preserve working client profiles, worlds, characters, modded furniture, and server saves. Use disposable local saves and a separate development profile for future testing; server backups and restore decisions belong to the sibling server repository.
- Native Linux Valheim uses the Linux Doorstop launch route, not Proton's Windows DLL overrides.
- Obtain explicit approval before driving the game, installing a loader, publishing packages/profiles, changing a server, or submitting upstream field notes. Never commit credentials, saves, backups, game assemblies, extracted assets, decompiled game source, or operational state.
