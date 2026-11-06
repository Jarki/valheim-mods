# Releasing to Thunderstore

This guide documents local packaging and the release workflow; it does **not**
authorize future publication or runtime operations. **MobThreat 1.0.1 and
NearbyStore 1.0.0 were published on 2026-11-05; Rotheart 1.0.0 on 2026-11-06**
under the confirmed **Jarki** team.

**Active release scope: MobThreat, NearbyStore and Rotheart.** The owner separately
approved each published artifact after disclosure of its verification limits.
Firearm Arsenal, Startup Helper (including server joining), and Server Metrics
remain paused; older multi-package verification is not a current release plan.

## Published Rotheart 1.0.0 — 2026-11-06

- ZIP: `dist/Jarki-Rotheart-1.0.0.zip`; **Jarki**, **Valheim**.
- SHA-256: `e0c940623679ac88ffc4ba6e48827295cf297625d92ae5495c0cdb87a46d3222`.
- Categories: `mods`, `ai-generated`, `gear`; NSFW off.
- Dependencies: `denikson-BepInExPack_Valheim-5.4.2351`, `ValheimModding-Jotunn-2.30.2`.
- Pinned tcli build and six-file archive verification pass. Extracted-payload lint
  reports zero failures/warnings. No dependencies, game assemblies, tests, saves,
  development state or credentials are bundled.
- ZIP DLL is byte-identical to the final native-game-tested DLL:
  `93ce43500cfc24e3b2c737778d8a7f46029e580f7e8ab81f4cd4bc5f4c3293be`.
- 65 isolated native-client assertions passed with zero Unity errors/exceptions;
  actual crafting, activation, combat, persistence and visuals were verified.
- Remote multiplayer, dedicated servers, controllers, Windows, mod-manager ZIP
  import and uninstall/reinstall remain unverified. The public README discloses
  these limits, the two existing build reference warnings, AI assistance and
  the source-not-pushed state.
- Original Blender model/icon and package-icon composition are MIT licensed.
  The package icon can be regenerated with `mods/Rotheart/Tools/build_package_icon.py`.
- Current global rules and live category slugs checked. Icon and exact candidate
  README were visually inspected using Thunderstore's Markdown Preview.
- Owner approved this exact ZIP and its disclosed limits. Pinned tcli 0.2.4
  published it successfully using the existing private external token only in
  child-process `TCLI_AUTH_TOKEN`; nothing was printed or saved containing the token.
- Page: <https://thunderstore.io/c/valheim/p/Jarki/Rotheart/>.
- Download: <https://thunderstore.io/package/download/Jarki/Rotheart/1.0.0/>.
- Downloaded public ZIP passes archive verification and matches the approved hash
  above. Public API reports active 1.0.0, expected dependencies/categories and
  NSFW off. Review is **unreviewed**, not moderation approval.
- Live listing, icon, version, README and testing disclosures visually inspected
  in Chromium; screenshots and sanitized publication log remain in ignored
  `.dev/release/rotheart-1.0.0/`.
- No game/profile/server operation or public-source push occurred during publication.
- The owner subsequently authorized the GitHub source push. Rotheart source is
  linked from its development README; the immutable 1.0.0 package keeps its
  source-publication disclosure as recorded at packaging time.


## Published NearbyStore 1.0.0 — 2026-11-05

- Release ZIP: `dist/Jarki-NearbyStore-1.0.0.zip`; team **Jarki**, community **Valheim**.
- SHA-256: `543600cdd70cf27c76567ee29af493747dc9e6a958513dc87f66ef90f35afef3`.
- Categories: `mods`, `ai-generated`, `client-side`, `utility`; NSFW off.
- Only dependency: `denikson-BepInExPack_Valheim-5.4.2351`.
- Pinned tcli build and archive verification pass; six-file extracted payload
  passes publishing lint with zero failures/warnings. 13/13 transfer cases and
  3/3 adversarial groups pass (20,480 generated deposits plus fixed-point repeats).
- Original programmatic chest/arrow/favorite icon, MIT license and AI disclosure
  included. No game/dependency binaries, tests, configs or saves bundled.
- Authorized isolated native-client checks passed 38 assertions, including fixture
  setup. Actual keyboard/mouse workflow, character-file save/reload, matching/
  overflow and private/busy/range exclusions were exercised; UI screenshots at
  default/70%/130% scale were inspected.
- Delayed ownership failed before the fix and passed after injecting ownership
  transitions on real local game containers. Late-response suppression and
  already-owned quarantine recovery passed; no two-client test was performed.
- Candidate DLL was extracted from the tcli ZIP into a new isolated development
  profile. Mod-manager local-import UI, multiplayer, denied wards and uninstall/
  reinstall remain unverified. Public README discloses these and remote-request
  quarantine/manual-merge limitations.
- Current global rules were read; MIT terms and AI disclosure are included.
- One earlier second-store assertion failed without quantity diagnostics. Two
  instrumented runs of the identical final DLL passed in fresh/reused local
  worlds. Original cause unconfirmed; public README and private evidence retain
  this distinction rather than claiming a production fix.
- Owner approved the exact artifact and confirmed an existing token. The private
  external token file was validated and used only through child-process
  `TCLI_AUTH_TOKEN` with pinned tcli 0.2.4; publishing returned success. No credential
  was printed, copied into the repository or persisted in CLI configuration.
- Page: <https://thunderstore.io/c/valheim/p/Jarki/NearbyStore/>.
- Download: <https://thunderstore.io/package/download/Jarki/NearbyStore/1.0.0/>.
- Downloaded public ZIP passes archive verification and exactly matches the hash
  above. Public metadata reports active 1.0.0, expected categories/dependency and
  NSFW off. Review is **unreviewed**, not moderation approval.
- The live Valheim page, original icon, version and README/testing disclosures
  were visually inspected in Chromium. An initial canonical-page read returned
  404 immediately after upload; the cache-busted live browser page rendered correctly.
- Only a disposable local profile/game was used; no live profile/server change,
  paused-mod upload or public-source push.

## Published MobThreat 1.0.1 — 2026-11-05

The owner explicitly approved publication of 1.0.1 after the display change and
its verification limits were disclosed. Unknown estimates now have no label.
The published 1.0.0 artifact and its historical record below remain unchanged.

Published ZIP `dist/Jarki-MobThreat-1.0.1.zip` passed metadata, Release build and archive
verification; extracted payload lint reports six files, zero failures/warnings.
A headless simulation compiling the production display code failed before the
change and passed after it, including known-to-unknown cleanup and numeric recovery.
Standalone lethal-hit tests pass. No new in-game visual test was run.

- Page: <https://thunderstore.io/c/valheim/p/Jarki/MobThreat/>.
- Download: <https://thunderstore.io/package/download/Jarki/MobThreat/1.0.1/>.
- Pinned tcli 0.2.4 returned success using the private external token file through
  a child-process environment variable; no credential was printed or committed.
- Downloaded public ZIP passed archive verification and exactly matches SHA-256
  `5ede28c042a98e4512394d08455b1886d8ac3888d157273489e5acf767d736c7`.
- Fresh public metadata reports active 1.0.1, the expected Valheim categories,
  BepInEx dependency and NSFW off. Review remains **unreviewed**, not approved.
- Live page showed latest version 1.0.1 while its README initially used stale
  cached 1.0.0 wording. Cache-busted public README/changelog endpoints independently
  served the correct 1.0.1 content with no manual edits. Do not reupload or replace
  valid package markdown merely because a cached page still shows an old copy.
- No paused-mod upload, GitHub push, game launch or profile/server change.




## Published MobThreat 1.0.0 — 2026-11-05

The owner selected **MobThreat 1.0.0 only** for publication and accepted the missing
fresh clean-profile ZIP-import test. This is not permission to launch a game or
touch any live profile/server. The three paused packages must not be uploaded.

- File: `dist/Jarki-MobThreat-1.0.0.zip`.
- Team: **Jarki**; community: **Valheim**.
- Categories: **Mods**, **AI Generated**, **Client-side**, **Utility**.
- NSFW: **off**; dependency: `denikson-BepInExPack_Valheim-5.4.2351`.
- The final ZIP was rebuilt/verified, its standalone tests passed, and extracted
  payload lint passed with six files, zero failures and zero warnings. Its README
  was rendered and visually checked using Thunderstore's Markdown Preview.
- The public MobThreat source path currently returns HTTP 404. Its manifest and
  README link the working project repository instead and disclose that this
  plugin's source is not yet pushed. No GitHub push is authorized or performed.

**Published:** <https://thunderstore.io/c/valheim/p/Jarki/MobThreat/>.
The owner supplied a private local token file and approved this MobThreat-only
upload. Pinned tcli 0.2.4 used a child-process `TCLI_AUTH_TOKEN` environment variable,
not a command-line token, and returned success. No credential was printed,
committed, copied into this repository or saved in a CLI config.

The live Valheim page renders the expected icon and README. Public package metadata
reports version 1.0.0 active, the exact dependency/categories above, NSFW off, and
review status **unreviewed** (not moderation approval). The public downloadable ZIP
matches the approved local artifact's SHA-256:
`fc4a09d3ab5c5cc372d6953fd7278dfe72ced6991b303e67581ddec90fcf519d`.

Use the community-specific page above: the generic `/package/Jarki/MobThreat/`
link redirects anonymous browsers to the site's default Risk of Rain 2 community.
The current experimental per-version metadata endpoint returned 404 despite the
working package endpoint, live Valheim listing and public download.

Private evidence is retained under `.dev/release/`. The token file remains outside
the repository; revoke a one-off service-account token through Thunderstore after
release. No game/server/profile change, GitHub push or other mod upload occurred.

## 1. Use the confirmed publisher with local authentication

Sign in with an account permitted to publish to the **Jarki** team. Team identity
is part of each package ID and cannot be renamed later. Never share credentials
or an API token in chat. If a publisher must change before the first upload,
update all four TOML configs, package identities, documentation and expected ZIP
names together, then rebuild and verify. These commands target `Jarki`.

No tokens belong in Git, config examples, logs, screenshots or chat. Authentication
setup is a separate owner-controlled step, not part of `tools/release.py`.

## 2. Synchronize versions and public metadata

For each changed plugin, choose a semantic `major.minor.patch` version and keep
these sources synchronized in the same change:

- Version in `mods/<project>/<project>.csproj`.
- Runtime `BepInPlugin` version in the plugin source.
- Package version in `mods/<project>/thunderstore.toml`.
- New entry in `mods/<project>/package/CHANGELOG.md`, public package README and
  development README where version-dependent statements change; update the root
  package table and any affected examples.

The checker rejects config/project/plugin version mismatches. Do not rename source
directories, DLLs or existing plugin IDs to match public package names. Current
mapping: `PistolMod` → `Firearm_Arsenal` 1.1.0, `StartupHelper` → `Startup_Helper`
1.0.0, `ServerMetrics` → `Server_Metrics` 1.4.0, `MobThreat` → `MobThreat` 1.0.1,
`NearbyStore` → `NearbyStore` 1.0.0, `Rotheart` → `Rotheart` 1.0.0.
Do not silently reuse a published version for changed contents.

Each config uses schema `0.0.1`, the confirmed `Jarki` namespace, community
`valheim`, `containsNsfwContent = false`, and the project's GitHub source URL.
All require `denikson-BepInExPack_Valheim` 5.4.2351; Firearm Arsenal and Rotheart also
require `ValheimModding-Jotunn` 2.30.2. Keep verified community category slugs:
`mods`, **`ai-generated`**, and the appropriate `client-side`, `server-side`,
`utility` or `gear` categories. A client-only optional helper is not a server
requirement; Firearm Arsenal and Rotheart require matching server/client installations.

Preserve the public disclosure: developed with AI assistance including OpenAI
Codex. Earlier release preparation used `gpt-6.1-sol`; Rotheart used `gpt-6-astra`.
Do not attribute every historical change to one model. State known runtime
limitations honestly and distinguish source tests, historical client evidence,
fresh packaging checks and clean-profile ZIP tests.

## 3. Restore pinned tools and check metadata

Run from repository root with Python 3.11+, `uv` and .NET SDK 10 (development SDK
10.0.113):

```sh
uv sync --locked
dotnet tool restore
uv run python tools/release.py check MobThreat
```

The repository-local .NET tool manifest pins **official Thunderstore CLI 0.2.4**;
use `dotnet tcli`, not an unpinned global executable. The pinned Universal Modder
CLI is a different tool, not the archive builder. Metadata checking requires no
game launch or proprietary game references. Metadata/test-only CI does not prove
a game build or runtime test passed.

## 4. Build and verify archives

Use an owned game installation and an existing reference profile; do not install
loaders or alter a live profile to prepare packages.

```sh
uv run python tools/release.py build MobThreat \
  --game-dir /path/to/Valheim --reference-profile /path/to/profile
uv run python tools/release.py verify dist/Jarki-MobThreat-1.0.1.zip
```

`VALHEIM_GAME_DIR` and `VALHEIM_REFERENCE_PROFILE` supply paths when flags are
absent. `--managed-dir` optionally supplies explicit server managed-assembly
references. `build` performs a fresh Release build, invokes tcli, and verifies the
archive; `verify` can independently inspect an existing ZIP. Run it on every ZIP
you intend to distribute, not only the example above.

Configs use `package/icon.png`, `package/README.md` and `../../dist` as build
icon/readme/output paths. tcli generates `manifest.json`, `icon.png` and `README.md`
in the ZIP: do not add a duplicate hand-maintained manifest. Explicit copies add
only:

- `bin/Release/netstandard2.1/<project>.dll` at
  `plugins/<project>/<project>.dll`;
- `package/CHANGELOG.md` at `CHANGELOG.md`;
- root `LICENSE` at `LICENSE`;
- for Firearm Arsenal only, `Assets/Audio/ATTRIBUTION.md` at `ATTRIBUTION.md`.

Never add directory globs or bundle sources, PDBs, test DLLs, game/BepInEx/Jotunn
assemblies, live configs, logs or saves. Loader/dependency packages are declared
requirements, not copied payloads. Public package files are distinct from the
retained development README beside the source.

As an additional preflight, extract a candidate ZIP into a disposable staging
directory and run the pinned toolkit's read-only publishing check on **that
extracted payload**, not the source tree or ZIP bytes:

```sh
uv run um publish check /path/to/extracted-candidate --game /path/to/Valheim
```

Resolve every failure and deliberately review warnings. This supplements archive
verification and credits/privacy review; it does not replace platform validation
or prove runtime compatibility.

## 5. Review licensing and test a clean-profile ZIP import

Original code/assets are MIT, copyright 2026 Jarki. Include the root license.
Firearm third-party recordings remain CC BY/CC0 as individually documented in
[the audio attribution](../mods/PistolMod/Assets/Audio/ATTRIBUTION.md); include
that document and preserve required credits/license links. Do not relicense those
recordings as MIT. Vendored toolkit licensing is separate and the toolkit is not
part of the mod ZIP. Do not distribute proprietary game or dependency binaries.

Before calling a candidate runtime-verified, obtain separate authorization for
local profile creation/game launches, then record the exact ZIP, game version,
loader/dependency versions and results:

- Create a new disposable profile, not a live or existing gameplay profile.
- Install declared dependencies at the documented versions, then import the
  **built ZIP** with the mod manager's local import workflow. Do not substitute a
  hand-copied development DLL.
- Confirm the archive installs to the intended plugin path with one plugin copy;
  inspect BepInEx logs for its correct ID/version and missing-dependency/errors.
- Use disposable saves; check documented functionality, configuration defaults,
  clean startup/logout and uninstall behavior. For firearms remove custom items
  from disposable saves before removal.
- Test MobThreat labels/scaling and disclose its unchanged DOT approximation;
  test Startup Helper's disabled default, exact selection and cancellation.
- Firearm multiplayer needs a separately authorized disposable server and matching
  clients. Local client evidence alone does not verify multiplayer. Startup
  Helper server/password joining likewise needs its own explicit test.
- Test Server Metrics on an authorized disposable server with safe endpoint
  binding/access settings; do not expose a live server to demonstrate packaging.
- Check public README/icon/changelog/credits as displayed after local import.
  Record failures, logs and evidence privately without secrets/player data.

Current evidence is historical/per-mod: Firearm 142 native checks and 59 targeted
pose/audio checks on `l-1.0.20` (multiplayer unverified), Startup Helper local world
startup (server join unverified), real MobThreat client UI fixes, and historical
Server Metrics tests/source. This preparation does not claim a fresh server
launch or a new clean-profile ZIP-import test. Do not erase those distinctions.

### Local packaging verification — 2026-11-05

- All four plugins built from source with .NET SDK 10.0.113 against read-only
  owned Valheim and pinned BepInEx/Jotunn references. All four tcli candidates
  passed allowlist, current metadata, document and built-DLL payload checks.
- Every extracted candidate passed `um publish check` against the installed
  game: zero failures and zero warnings. Firearm contains seven files including
  its audio credits; each other package contains six. No dependency/test/game
  assemblies are bundled.
- MobThreat, Firearm (7/7), and Server Metrics (15/15) standalone suites passed.
  All standalone projects now target .NET 10. Existing framework-reference and
  reflection-populated DTO warnings remain; they were not suppressed.
- Actual malformed ZIP probes rejected an extra BepInEx DLL and a mismatched
  manifest version. An identical-input MobThreat tcli rebuild plus ZIP
  normalization preserved its SHA-256 hash.
- CI YAML parses locally; GitHub-hosted CI has not been executed by this session.
  No game/server launched for packaging, no clean-profile ZIP-import test, and
  no upload performed. These remain distinct release gates.


## 6. Publish only after a separate explicit approval

The owner must first confirm the exact team, target package/version, candidate ZIP,
reviewed metadata/credits, verification results and any remaining limitations.
Changing supported game versions requires appropriate fresh runtime evidence; a
successful build is not that evidence. Confirm current Thunderstore rules before
submitting. No wrapper command uploads automatically.

After **separate explicit approval**, either manually upload the verified ZIP
through Thunderstore's package upload UI for the owning team and Valheim community,
or use the locally pinned official CLI. Exact MobThreat example:

```sh
dotnet tcli publish --config-path mods/MobThreat/thunderstore.toml --file dist/Jarki-MobThreat-1.0.1.zip
```

Use the matching config and verified file for other packages. This command is a
publication action, **not** part of the build/check workflow and not authorized by
this guide. Never run it merely to test authentication. After an approved upload,
record the resulting package URL/version and actual evidence, without committing
authentication material.

## Official references

- [Creating a package](https://wiki.thunderstore.io/mods/creating-a-package.md)
- [Packaging mods](https://wiki.thunderstore.io/mods/packaging-your-mods.md)
- [Global moderation rules](https://wiki.thunderstore.io/moderation/global-rules.md)
- [Official Thunderstore CLI and project schema](https://github.com/thunderstore-io/thunderstore-cli)
