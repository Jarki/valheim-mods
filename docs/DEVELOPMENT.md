# Development

See the [root package overview](../README.md) and [release procedure](RELEASING.md).
The source READMEs remain authoritative for gameplay, configuration and historical
verification: [PistolMod](../mods/PistolMod/README.md),
[StartupHelper](../mods/StartupHelper/README.md),
[ServerMetrics](../mods/ServerMetrics/README.md), and
[MobThreat](../mods/MobThreat/README.md). Do not interpret historical build or
runtime records as fresh verification of a release archive.

**Active development: MobThreat only.** Firearm Arsenal, Startup Helper
(including server joining), and Server Metrics are paused by the owner. Their
instructions and verification records are retained as history, not authorization
to resume work, testing, deployment or publication.


## Prerequisites and local references

Use Python 3.11+, `uv`, and .NET SDK 10. The current development SDK is
**10.0.113**; earlier records mentioning SDK 6.0.428 describe historical work, not
the current prerequisite. All standalone test projects target `net10.0` and need
SDK/runtime 10. Root `global.json` requests SDK 10.0.100 with `latestFeature`
roll-forward, permitting the current SDK 10.0.113.
Builds use an owned game installation and an existing BepInEx 5.4.2351 reference
profile; PistolMod additionally uses Jotunn 2.30.2. Proprietary game assemblies and
loader/dependency binaries must stay outside release archives.

```sh
uv sync --locked
dotnet tool restore
dotnet build mods/MobThreat/MobThreat.csproj -c Release \
  -p:GameDir=/path/to/Valheim -p:ReferenceProfile=/path/to/profile
dotnet run --project tests/MobThreat.Tests/MobThreat.Tests.csproj -c Release
```

The example targets the only active plugin. Source build properties are
not release-wrapper flags: see the release guide for `--game-dir`,
`--reference-profile`, and optional `--managed-dir`. Builds may report existing
framework reference-version conflicts; consult the per-mod records rather than
hiding warnings. Do not copy local references into a package to resolve them.

## Project-local Universal Modder

Universal Modder CLI **0.2.0** and **11 mod-development skills** are pinned from
[rehan-remade/universal-modder](https://github.com/rehan-remade/universal-modder)
at revision `c1dff21663665dd17af289744a301b66e369605e`.
`pyproject.toml` and `uv.lock` manage the executable and Python dependencies in the
ignored project `.venv/`; no global `um` installation is required. The skills and
their bundled references/scripts are vendored under `.agents/skills/`, with the
upstream MIT license in `.agents/skills/LICENSE`. `AGENTS.md` routes assistants to
them.

```sh
uv sync --locked
uv run um --help
uv run um kb search "valheim"
uv run um scan /path/to/Valheim
```

Run upstream examples' bare `um` commands as `uv run um`. Upstream-root paths such
as `knowledge/`, `examples/`, `bin/um`, and `um/` are not paths in this repository.
Knowledge-base commands fetch/cache upstream notes when no local knowledge base
exists. That user-level cache follows upstream `main`, independently of the
pinned CLI/skills; the executable and dependencies remain project-local.

To update the toolkit, review a new upstream revision, change the Git revision in
`pyproject.toml`, run `uv lock` and `uv sync --locked`, and replace the 11 vendored
mod-development skill directories with those from that revision's `skills/`.
Retain the upstream license and update this revision record together. Do not
merely overlay files and leave obsolete upstream files behind.

Blender is needed only to regenerate committed firearm models/icons; follow the
PistolMod README's actual tool constraints. FFmpeg is used for local evidence
media. No fal/MCP or other paid asset service is configured by this setup.
Installing CLI tools or skills does not install BepInEx or authorize game launches,
profile changes, server operations or uploads.

## Isolated runtime development and evidence

Use the per-mod development instructions for launchers and disposable profiles.
The firearm launcher uses `.dev/pistol/profile` and isolates local saves through
`XDG_CONFIG_HOME`, not the dedicated-server `-savedir` option. Steam Cloud saves
can still appear: use only disposable local characters/worlds and do not join a
live server from a development profile. Remove custom firearm items from
throwaway saves before uninstalling. Runtime work requires separate authorization;
the packaging commands do not launch a game.

Keep evidence, local tools, diagnostic logs, profiles and saves ignored under
`.dev/` or outside the repository. Do not commit credentials, live configuration,
player ledgers, personal account paths or deployment addresses. Sanitized examples
may use standard loopback, wildcard, multicast and documentation-only addresses.

The sibling [`valheim-server`](../../valheim-server/) repository owns operational
procedures and monitoring/deployment state; consult its
[server-management skill](../../valheim-server/.agents/skills/server-management/SKILL.md)
for authorized server work. It is not required to build these mods. The original
combined checkout/history is retained at `../../valheim-archive-20261105/` and is
not an active dependency. Historical docs remain in place; the repository split
and packaging preparation do not imply any server change or publication.
