# Firearm Arsenal

**Unpublished release candidate for the Jarki team.** Mod-manager instructions apply after an approved publication; until then, import a locally built ZIP or use the manual instructions.

Craft a 9mm pistol, an AKM-style automatic rifle and an RPG-style launcher at the forge. Includes matching ammunition, persistent per-item magazines, moving weapon parts, procedural hand/reload animation and recorded audio. All use the Crossbows skill and two hands.

## Requirements and installation

Requires Valheim, **BepInExPack Valheim 5.4.2351** and **Jotunn 2.30.2**. Install matching Firearm Arsenal versions on every client and the server; do not mix with the old 1.0 pistol plugin. Multiplayer deployment is not yet verified.

- **Mod manager:** install `Jarki-Firearm_Arsenal` into a separate Valheim profile using a Thunderstore-compatible manager such as r2modman. Let it install dependencies, then start modded.
- **Manual:** close the game/server and install the dependencies for your operating system first. Copy the archive's `plugins/PistolMod/` directory into `BepInEx/plugins/`, producing `BepInEx/plugins/PistolMod/PistolMod.dll`. Keep `LICENSE` and `ATTRIBUTION.md` with your copy. Remove duplicate/older `PistolMod.dll` copies. Linux needs an appropriate existing Linux Doorstop launch route.

## Playing and configuration

Attack fires; secondary attack reloads. Hold attack for automatic rifle fire. New weapons start empty; reserve ammunition is consumed only when reload completes. Interrupted reloads preserve reserve ammunition. Magazines hold 17 pistol rounds, 30 rifle rounds or one rocket; reloads take 1.8, 2.5 and 3 seconds. All repair at a forge. No user-facing configuration settings are currently exposed; balance is fixed in the plugin.

| Recipe | Forge level | Materials | Output |
| --- | ---: | --- | ---: |
| Pistol | 2 | 20 Iron, 5 Copper, 5 FineWood | 1 |
| 9mm cartridges | 1 | 1 Copper, 2 Coal, 1 Resin | 20 |
| AKM rifle | 2 | 35 Iron, 10 Copper, 15 FineWood | 1 |
| 7.62mm cartridges | 1 | 2 Copper, 3 Coal, 1 Resin | 20 |
| RPG launcher | 3 | 40 Iron, 10 Silver, 10 FineWood | 1 |
| Rockets | 3 | 2 Iron, 5 Coal, 1 SurtlingCore | 1 |

Rockets have a five-metre blast with falloff, solid-geometry occlusion and ward checks. They damage creatures, trees, props and buildings, but do not deform terrain. Native PVP, dodge, damage resistance and tool-tier rules still apply. Test destructive weapons only in a disposable world first.

## Uninstall and save safety

Back up characters and worlds before installation or removal. This mod adds persistent items and magazine data. Remove its weapons and ammunition from all characters, containers and worlds while it is still installed, then close the game/server and remove `PistolMod.dll` from every peer. Do not assume modded items survive loading a save without the mod. Restore a backup if needed; removing the DLL does not undo damage to buildings or creatures.

## Testing and limitations

The development record reports 142 native-client integration checks and a subsequent 59-check pose/audio rerun on **Valheim l-1.0.20, Steam build 25909357, Unity 6000.0.75f1**. These exercised weapons, recipes, projectiles, ward/blast behavior, inventory persistence and visible animation. Multiplayer and server deployment remain unverified. Technical playback checks do not establish subjective audio quality. Compatibility with later game versions or arbitrary modpacks is not guaranteed. Packaging readiness is not a clean-profile archive installation test or publication; this first Thunderstore package is unpublished.

## License, credits and AI disclosure

Original code, meshes and package icon: **MIT, copyright 2026 Jarki**; see `LICENSE`. Valheim belongs to its respective owners; game, loader and Jotunn assemblies are not bundled. Runtime game effects are taken from the user's installed game, not distributed assets.

Third-party recordings are **not relicensed under MIT**. See the included `ATTRIBUTION.md` for full source/license links and adaptation notices. Credits include Ben Jaszczak, Brian Nelson, Kevin Heras and Matthew Nanney (CC0 firearm reports); Sophia O. Caldwell, “Fast Pistol Reload [Bersa BP9CC 9x19]” (CC BY 4.0); Mittelmorder, “AKManipulation.wav” (CC BY 3.0); klangfabrik (CC0 RPG launch); and sidohzen / qubodup (CC0 explosion). The pistol report is a .45 recording used for the 9mm item; RPG reload uses adapted AK handling foley. Preserve the attribution file when redistributing.

Developed with AI assistance, including OpenAI Codex. This packaging work used **gpt-6.1-sol**; historical work is not claimed to use that same model. The package icon is original programmatic artwork, not stock Thunderstore art.

## Source and support

[Source and development documentation](https://github.com/Jarki/valheim-mods/tree/main/mods/PistolMod) · [Report issues](https://github.com/Jarki/valheim-mods/issues). Include game/mod versions, reproduction steps and relevant redacted BepInEx logs; never post passwords, tokens or private saves.
