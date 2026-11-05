# Firearm mod — requested next changes

Status: **implemented and locally verified (2026-10-27)**. The user approved
installing needed tools and free-asset research. They selected an RPG-style launcher
and ward-respecting structure damage with a five-metre blast and distance falloff.
The native-client suite passed 142 checks. See `MODLOG.md` and
`mods/PistolMod/README.md` for evidence and remaining multiplayer verification.
This development build has not been published.

Implemented choices: AKM-style rifle (30 rounds, 0.1-second automatic cadence,
2.5-second reload); RPG with five-metre ward-respecting blast/falloff; original
Blender meshes, procedural skeletal poses, and attributed recorded audio.
The requests below preserve the original planning context.

## Requested changes

1. **Realistic pistol audio.** Replace the current synthesized gunshot and reload sounds with convincing, appropriately licensed recordings. Use free sources, verify each asset's license, and retain required attribution. Do not rip sounds from other games or assume a free download permits reuse.
2. **Better presentation.** Explore a more detailed custom pistol model and better holding, aiming, firing, and reloading animations instead of the current crossbow character stance. A new model and moving weapon parts are separate tasks from custom hand/body animation; the latter needs Valheim skeleton and animation-system integration. The exact visual approach remains open.
3. **AK-style rifle.** Add an AK-style gun, with automatic fire, its own model, and appropriate sounds. Exact variant, ammunition, magazine size, crafting costs, and balance remain undecided.
4. **Explosive launcher.** Add a grenade launcher or RPG-style rocket launcher; whether to choose one or both remains undecided. Include explosive projectiles and blast damage.
5. **Structure damage.** The user would like launcher explosions to damage structures as well as creatures. Decide blast radius, damage, player-built structure behavior, and protection/ward interactions before implementation. Verify destruction only in disposable worlds, not existing builds.

## Tooling plan

- Blender is recommended for better meshes, UVs, materials, rigs, and authored animation. A Blender MCP bridge is optional, not a prerequisite: Blender can be automated directly with its Python API and background command-line mode.
- For conventional Unity prefabs, imported animation clips, and AssetBundles, plan to use a Unity Editor version compatible with the installed game. The current game was verified as Unity 6000.0.75f1. Confirm the appropriate editor/build pipeline before installing; free Personal licensing is subject to Unity's eligibility terms.
- Existing C#/.NET, BepInEx, and Jotunn tooling covers gun behavior. More guns do not themselves require MCP.
- FFmpeg is already documented as available for audio processing; Audacity is an optional free waveform editor. Realistic sound requires suitable recordings, not a Blender integration.
- Start with ordinary Blender scripting rather than adding an MCP service. Consider MCP later only if live interactive Blender control provides a concrete benefit. No paid generation services are authorized.

## How the current pistol was made

`mods/PistolMod/PistolAssets.cs` constructs the model directly in C# inside Unity. Shared box and cylinder meshes are positioned, scaled, and rotated into the slide, grip, barrel, trigger guard, sights, and other details. Materials are created at runtime. There is no authored Blender/FBX model behind this version.

The slide, magazine, and recoil effects move transforms in code; the player's body uses the vanilla crossbow stance. The current sounds are generated from noise and decaying sine waves, not firearm recordings.

## Boundaries

- The original planning-only restriction was superseded by the user's implementation request.
- No paid asset services, purchases, publishing, or upstream submissions.
- Keep the mod's name separate from the server name.
- Preserve existing profiles, worlds, characters, and modded furniture.
- Use the isolated development profile and disposable local saves for future testing.
- For production/server operations and approval boundaries, use the [server-management skill](../valheim-server/.agents/skills/server-management/SKILL.md) in the sibling server repository.
