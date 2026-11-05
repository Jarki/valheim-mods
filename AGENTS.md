# Valheim mod development

Read `README.md` for development context. For server management, deployment, client-profile operations, or connection troubleshooting, switch to the sibling `../valheim-server` repository and read `../valheim-server/.agents/skills/server-management/SKILL.md` plus its state reference before acting.

## Universal Modder

The 11 upstream mod-development skills, including their bundled references and scripts, are vendored under `.agents/skills/`. For mod development, start with `.agents/skills/mod-any-game/SKILL.md`, then read the task-specific skills and the Unity engine reference. Run the project-local CLI with `uv run um` from this repository (or `.venv/bin/um`); do not install it as a user-wide `uv tool`. Bare `um` and `bin/um` commands in upstream skills must be invoked through `uv run um` here.

Search prior work with `uv run um kb search "valheim"`. Skill references to upstream-root `knowledge/`, `examples/`, or `um/` paths refer to https://github.com/rehan-remade/universal-modder, not local project files. Use `uv run um kb show` for knowledge-base notes; retrieve other upstream references at the revision recorded in `README.md`.

## Boundaries

- Toolkit setup does not authorize game launches, loader installation, deployment, restarts, remote access, profile publication, or any server change.
- Preserve working client profiles, worlds, characters, modded furniture, and server saves. Use disposable local saves and a separate development profile for future testing; server backups and restore decisions belong to the sibling server repository.
- Native Linux Valheim uses the Linux Doorstop launch route, not Proton's Windows DLL overrides.
- Obtain explicit approval before driving the game, installing a loader, publishing packages/profiles, changing a server, or submitting upstream field notes. Never commit credentials, saves, backups, game assemblies, extracted assets, decompiled game source, or operational state.
