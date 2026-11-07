---
name: publish-mod
description: Package and release a finished game mod with payload lint, platform packaging, a concise player-facing README, changelog, and owner-approved publication. Use when preparing a package, rewriting its public description, or publishing a release.
---

# Publish a mod

## 1. Lint
```bash
um publish check ./MyMod --game "<game install folder>"
```
- **FAIL:** files byte-identical to game files, leaked keys (fal/Anthropic/OpenAI/GitHub/AWS), `.env` files.
- **WARN:** decompiler fingerprints in source (`FUN_`/`DAT_`/`sub_` names, "Decompiled with" headers), large
  engine archives, absolute user paths, a missing README, fal assets without credit.

Fix every FAIL. Resolve each WARN deliberately. For example, AoE2 data mods do ship a modified `.dat`, which
is the platform's norm.

## 2. Package the way the platform expects

| Platform / loader | Package |
|---|---|
| tModLoader | Build → `.tmod`; publish from the in-game Mod Sources menu (Steam Workshop) |
| BepInEx / Thunderstore | zip with `manifest.json`, `icon.png` (256×256), `README.md`, `plugins/<Mod>.dll`; dependency strings like `BepInEx-BepInExPack-5.4.2100` |
| Nexus Mods | zip laid out as it installs (`Data/...` for Bethesda, `BepInEx/plugins/...`, `ue4ss/Mods/...`, `~mods/*.pak`) |
| AoE2 DE | the official mod site (ageofempires.com/mods) or the in-game uploader; data mods include `resources/_common/dat/...` |
| Steam Workshop | the game's own uploader or SDK tool |
| Minecraft | Modrinth / CurseForge jar with `fabric.mod.json` / `neoforge.mods.toml` |
| ROM hacks / GameMaker | patches only (BPS/IPS/xdelta), never the modified game file |

A Nexus mod with options gets a FOMOD installer: see the
[FOMOD tutorial](https://fomod-docs.readthedocs.io/en/latest/tutorial.html) and
[STEP's FOMOD guide](https://stepmodifications.org/wiki/Guide:FOMOD). Nexus Mods' own
[modding wiki](https://modding.wiki/en/home) covers the rest of their site's conventions.

## 3. README (in the zip and on the page)

Write for players, not as a development report. Use this order:

1. **Mod name:** no release number in the title.
2. **Short description:** one sentence explaining what the mod does. No release
   history, version announcements, test counts or implementation narrative.
3. **Core features:** a short bullet list of the main player-visible behavior.
4. **Use** (only if needed): essential controls and non-obvious behavior.
5. **Configuration** (if applicable): config location, settings and their meaning.
   Preserve a correct existing configuration section unless settings changed.
6. **Requirements and installation:** near the end; dependencies, install sides,
   mod-manager/manual steps and duplicate-plugin avoidance.
7. **Uninstall:** near the end; removal steps and any real save/item-loss risk.
8. **Compatibility and troubleshooting:** last; actionable conflicts, multiplayer
   limits, recovery steps and a support link. Keep this brief and factual.

```markdown
# Mod name

One sentence describing the mod's purpose.

## Core features

- Main player-visible feature.
- Another core feature.

## Configuration

Config location, settings and their meaning, when applicable.

## Requirements and installation

Dependencies, install sides and installation steps.

## Uninstall

Removal steps and any save-related precautions.

## Compatibility and troubleshooting

Known limitations, recovery steps and support link.
```

Put release numbers and changes in `CHANGELOG.md`, not the README title or
introduction. Dependency versions are appropriate in requirements; an exact
compatibility version belongs only where it changes practical advice.

Do not add verification reports, test/build statistics, license sections, credits,
AI/model bylines or development provenance to the README. Keep evidence in
development records. This is a presentation rule: still ship legally required
license/attribution files and retain required platform disclosures in metadata.
Do not turn unknown compatibility into a positive claim when shortening the text.

### Preview changed pages before publication

- Compare the proposed public page with its current content and assets. If the
  page is unchanged, skip the preview; a new DLL or release alone does not require it.
- If the description, README, screenshots, icon or layout changes, prepare a
  draft and demonstrate a rendered preview before updating the public page or
  publishing a package carrying the changed page. Local draft edits are allowed.
- Use the actual proposed text and assets. For screenshots, find and inspect
  existing captures first; prefer a small, relevant selection after Core features.
  New game captures still require the project's separate runtime authorization.
- Provide a clickable preview URL. When using a LAN preview, serve only an
  isolated directory of page assets, never the repository, logs or profiles.
  Identify approximate styling as a preview, not an exact platform rendering.
- Open the preview in a browser, inspect desktop/mobile layout, and check images
  and links before showing it. Ask the owner to approve the proposed page.
- Apply the approved content and assets. If they materially change after approval,
  demonstrate the revised preview again. Preview approval does not authorize upload;
  publication still requires its own explicit approval.

## 4. Version and changelog
Use semver in the manifest/build file and put version-specific changes in the
changelog. Keep supported game/dependency requirements accurate. When the game
updates, re-run the authorized in-game test scene before claiming compatibility.

## 5. The post
- Lead with the video: the showcase-video skill; 20-45 s, gameplay within 2-3 s.
- Post text: the hook, core behavior and a link; no repeated development boilerplate.
- If the video uses anyone else's footage, credit them by handle and ask first.
- Publishing is always the user's call. Draft it, show them, and let them press the button.
