# MobThreat

A standalone **client-only** overlay showing `~N hits` above nearby hostile mobs: the approximate number of unblocked attacks needed to defeat you at your **current health**. One or two hits are red; unknown or zero estimated damage shows no label. No server installation or Jotunn dependency.

## Requirements and installation

Requires Valheim and **BepInExPack Valheim 5.4.2351**.

- **Mod manager:** install `Jarki-MobThreat` into a Valheim profile with a Thunderstore-compatible manager such as r2modman, accept its BepInEx dependency, then launch modded.
- **Manual:** close Valheim and install the appropriate BepInEx loader for your operating system. Copy the archive's `plugins/MobThreat/` directory into `BepInEx/plugins/`, producing `BepInEx/plugins/MobThreat/MobThreat.dll`. Keep `LICENSE` and remove older/duplicate MobThreat DLLs. Use your existing loader launch route; native Linux requires Linux Doorstop.

## Configuration and behavior

Load once to generate `BepInEx/config/local.mobthreat.cfg`; edit while Valheim is closed:

```ini
[Display]
Enabled = true
MaxDistance = 35
```

Distance is in metres, constrained to 5–100. Estimates sample every 0.25 seconds; labels follow the collider top each frame. Labels hide when off-screen, occluded by terrain/buildings, the HUD is hidden, or no living local player exists. Players, friendly/dead creatures and hidden-HUD creatures are excluded. The plugin owns its pooled TextMeshPro overlay and does not replace vanilla health bars.

## What the estimate means

Uses the strongest known primary/secondary attack among initialized weapons, not predicted AI attack choice. Includes creature/weapon/world levels, nearby-player difficulty, enemy damage settings, approximate midpoint damage rolls, current player armor/resistance and local damage-taken settings. It uses native resistance/armor calculations without inflicting hits, consuming attack RNG or invoking potentially stateful attack/damage status hooks.

**Not a survival guarantee.** Assumes no blocking, parrying, dodging, healing, armor degradation or changing defenses. Full fire/poison/spirit payloads are included, but DOT admission thresholds/timing/stacking are approximated, intentionally unchanged. Wetness, attack chains, custom status hooks, remote equipment, ammo, spawned AOE, unknown special attacks and modded behavior can differ. Damage-less/non-humanoid attacks may be unknown. Debug invulnerability is not modeled; the strongest known attack is not a guaranteed bound on unknown effects.

## Uninstall and save safety

Set `Display.Enabled = false` or close Valheim and remove `BepInEx/plugins/MobThreat/MobThreat.dll`. The config can be removed separately. The overlay adds no items or save schema and does not modify equipment or apply damage. Normal gameplay still saves normally; keep backups and try a separate profile/disposable world before adding any mod to valued saves.

## Testing and limitations

Real isolated native-client UI tests exercised health/armor updates, visibility, world exit, mob destruction and disable/re-enable. Follow-up ghost-label fixes and window/render-resolution scaling fixes were exercised in fresh disposable clients with inspected screenshots; the fixed runs reported no exceptions or font-loading errors. Standalone lethal-hit regressions and real installed-game mitigation smoke checks are recorded in development documentation. Existing framework-reference warnings remain.

Multiplayer equipment, broad creature/special-attack coverage, all-environment occlusion and arbitrary full-modpack interaction remain unverified. Isolated startup does not prove a fix for any removed third-party plugin's freeze. No game was launched for packaging; a fresh clean-profile archive installation test has not been run for this release.

## License, credits and AI disclosure

Original code and package icon: **MIT, copyright 2026 Jarki**; see `LICENSE`. Valheim and BepInEx belong to their respective owners; no game/loader assemblies or native font assets are bundled. Developed with AI assistance, including OpenAI Codex. This packaging work used **gpt-6.1-sol**; historical work is not claimed to use that same model. The icon is original programmatic artwork.

## Project and support

[Project repository](https://github.com/Jarki/valheim-mods) · [Report issues](https://github.com/Jarki/valheim-mods/issues). Include game/mod versions, reproduction steps and relevant redacted BepInEx logs; never post passwords, tokens or private saves.

This new plugin's source has not yet been pushed to the linked public repository.
