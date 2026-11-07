# Releasing to Thunderstore

Current release procedure for the **Jarki** team and **Valheim** community.
This guide does not authorize publication, game launches, profile changes or server operations.
See [development](DEVELOPMENT.md) for prerequisites and local references, and the
[publishing skill](../.agents/skills/publish-mod/SKILL.md) for the public-page template.

## 1. Select the package and prepare metadata

Release-wrapper targets are **MobThreat, NearbyStore and Rotheart**. Firearm Arsenal
(`PistolMod`), Startup Helper and Server Metrics remain owner-paused; do not build,
test or publish them without explicit resumption. Do not use build-all commands.

- Use the selected mod's `thunderstore.toml` and public `package/` files.
- Synchronize the project version, `BepInPlugin` version and package version.
  Never reuse a published version for changed contents.
- Put release numbers and changes in `package/CHANGELOG.md`, not the public README
  title or introduction. Update version-dependent development docs and the root package table.
- Preserve plugin IDs, DLL names, prefab identities and save keys.
- Check dependency versions, install sides, website, Valheim categories and NSFW flag.
  Retain the required `mods` and `ai-generated` categories; keep development provenance
  outside the player-facing README.

## 2. Check and build with pinned tools

Run from the repository root with the prerequisites in the development guide:

```sh
uv sync --locked
dotnet tool restore
MOD=MobThreat  # Select MobThreat, NearbyStore or Rotheart.
uv run python tools/release.py check "$MOD"
uv run python tools/release.py build "$MOD" \
  --game-dir /path/to/Valheim --reference-profile /path/to/reference-profile
```

The check applies to the selected mod, not a mandatory MobThreat baseline.
Use `tools/release.py` and pinned local **official tcli 0.2.4**, never a global CLI
or ad-hoc directory ZIP. `uv run um` is the separate lint/research toolkit.

Build against read-only owned game/loader references; do not install a loader or
modify a live profile to obtain them. `VALHEIM_GAME_DIR` and
`VALHEIM_REFERENCE_PROFILE` can supply the reference paths instead of flags.
`build` performs a fresh Release build, packages, verifies the archive against its
inputs, normalizes ZIP metadata and prints the candidate path and SHA-256.

## 3. Verify the candidate payload

Set `ZIP` to the exact candidate path printed by the build:

```sh
ZIP=/path/to/candidate.zip
uv run python tools/release.py verify "$ZIP"
```

Verify every candidate intended for distribution. The standalone verifier checks
current metadata and document inputs; the build workflow additionally compares the
packaged DLL with the fresh build. Do not overwrite retained or published artifacts
just to make them match newer source inputs; prepare a new candidate/version.

Active packages contain only:

- Generated `manifest.json`, `icon.png` and `README.md`.
- `plugins/<namespace>-<package-name>/<DLL-name>`, for example
  `plugins/Jarki-MobThreat/MobThreat.dll`.
- `CHANGELOG.md` and the root MIT `LICENSE`.

Keep source folders and DLL identities unchanged. When changing installed folders,
remove the previous DLL to avoid duplicate plugins. Preserve third-party terms and
required attribution files; firearm audio has separate [CC BY/CC0 terms](../mods/PistolMod/Assets/Audio/ATTRIBUTION.md).
Never bundle dependencies, game assemblies, sources, PDBs, test plugins, profiles,
saves, credentials or diagnostic state. Dependencies belong in package metadata.

Extract the candidate into disposable staging and lint the extracted payload:

```sh
uv run um publish check /path/to/extracted-candidate --game /path/to/Valheim
```

Resolve all failures and deliberately review warnings. Metadata checks, builds,
archive verification and payload lint are not proof of gameplay compatibility.

## 4. Verify behavior and preview changed pages

Runtime work requires separate approval. Before calling a candidate runtime-verified:

- Use a new isolated profile and disposable saves, never a live gameplay profile or server.
- Install declared dependencies and import the **built ZIP** through the mod manager;
  a hand-copied development DLL is not a clean-profile ZIP-import test.
- Check the installed path, one-copy-only loading, ID/version, dependency errors,
  ordinary gameplay, configuration, startup/logout and uninstall/save-safety behavior.
- Test multiplayer only in a separately authorized disposable setup. Local success
  does not establish multiplayer correctness. State unverified limitations honestly.

If the public page changes, follow the publishing skill: demonstrate a rendered
preview of the proposed text/assets and obtain approval before updating it.
Check desktop/mobile layout, images and links. Serve only isolated page assets in
LAN previews. Material changes after approval require another preview.
If the page is unchanged, skip the preview. Preview approval is not upload approval.

Keep detailed hashes, logs, screenshots, failures and verification results privately
under ignored `.dev/release/`, without secrets or player data. Public feature history
belongs in each package's changelog.

## 5. Publish only with separate explicit approval

Before uploading, confirm the exact team, package/version, candidate ZIP/hash,
reviewed page, metadata, verification results and remaining limitations with the owner.
Check current Thunderstore rules. A successful build does not establish support for
a new game version, and no wrapper command uploads automatically.

Use an account permitted to publish under **Jarki**, either through the Valheim
package-upload UI or the pinned CLI after approval:

```sh
dotnet tcli publish --config-path "mods/$MOD/thunderstore.toml" --file "$ZIP"
```

This is a publication action, not an authentication test. Authentication setup is
owner-controlled. Keep credentials outside the repository; pass a token only through
a child-process `TCLI_AUTH_TOKEN`, never arguments, logs, screenshots or committed config.

After upload, download the public ZIP, compare its SHA-256 with the approved candidate,
check package metadata/dependencies, and inspect the live page, icon and hosted images.
Distinguish upload success, artifact identity and moderation status. Record the URL,
version, hash and outcome privately; publication does not add runtime evidence.

## Official references

- [Creating a package](https://wiki.thunderstore.io/mods/creating-a-package.md)
- [Packaging mods](https://wiki.thunderstore.io/mods/packaging-your-mods.md)
- [Global moderation rules](https://wiki.thunderstore.io/moderation/global-rules.md)
- [Official Thunderstore CLI and project schema](https://github.com/thunderstore-io/thunderstore-cli)
