# Fitness

Train your Run skill on a deer-hide treadmill, but pace yourself: overworked running muscles cost more stamina.

[![Watch the six-biome Fitness showcase](https://jarki.github.io/valheim-mods/fitness-showcase.jpg)](https://jarki.github.io/valheim-mods/)

[Watch the 42-second gameplay showcase](https://jarki.github.io/valheim-mods/): Meadows, Black Forest, Swamp, Mountains, Plains and Mistlands.

## Core features

- **Buildable wooden treadmill** with a moving deer-hide belt. Train Run in place at **1.5× normal XP** by default.
- **Three muscle-strain levels** from sustained sprinting: mild **+15%**, moderate **+30%**, severe **+50%** running stamina drain. Other stamina costs are unchanged.
- **Skill-based endurance:** higher Run skill slows strain buildup; at Run **75**, no new strain accumulates by default.
- **Real stamina, real rest:** treadmill exercise consumes native running stamina and stops on exhaustion. Walk or rest to recover; remaining strain persists across saves.
- Separate treadmill **XP, stamina-cost and strain-buildup multipliers**.

## Build and use

Use **Hammer → Furniture → Treadmill**, near a workbench, with:

![Native Valheim treadmill recipe: 20 Wood, 10 Core wood, 10 Bronze nails, 6 Deer hide, 10 Leather scraps; requires a workbench.](https://jarki.github.io/valheim-mods/fitness-recipe.png)

Interact to start or stop training. Move or jump to dismount. **You can open inventory while training**; stamina consumption, Run XP and muscle strain continue. Closing inventory does not interrupt the workout. Other blocking interfaces, damage, exhaustion or destruction of the treadmill end the session. After recovering stamina, interact again to resume; there is no automatic restart or unlimited AFK training.

Only sprinting builds muscle strain, including active treadmill training. At a fixed Run skill of 0, the default is **90 cumulative sprint seconds per strain level**; higher skill increases that time. Walking or resting while alive heals one level per **180 seconds**. Short breaks retain unrecovered fatigue; stopping does not immediately clear an injury. Death, teleporting and time offline do not heal strain.

## Configuration

Launch once, then close Valheim and edit **`BepInEx/config/jarki.fitness.cfg`**.

| Section / setting | Default | Effect |
| --- | --- | --- |
| Treadmill / `RunExperienceMultiplier` | `1.5` | Treadmill-only Run XP; range 0.1–10 |
| Treadmill / `StaminaConsumptionMultiplier` | `1` | Treadmill-only running stamina cost; range 0.1–5 |
| Treadmill / `StrainBuildupMultiplier` | `1` | Treadmill-only strain buildup; range 0.1–5 |
| Muscle strain / `SecondsPerLevel` | `90` | Cumulative sprint seconds per strain level at Run 0; range 10–3600 |
| Muscle strain / `RecoverySecondsPerLevel` | `180` | Alive, non-sprinting seconds to heal one level; range 10–3600 |
| Muscle strain / `StrainFreeRunSkill` | `75` | Skill at which new strain stops; range 1–100 |

Treadmill multipliers do not change ordinary running. Severity penalties are fixed. Configurations are **not synchronized**; use matching settings across clients.

## Requirements and installation

Requires **denikson-BepInExPack_Valheim 5.4.2351** and **ValheimModding-Jotunn 2.30.2**. Single-player behavior was tested on **Valheim l-1.0.21, native Linux**.

**Mod manager:** install `Jarki-Fitness` and accept its dependencies. Use a separate profile and new disposable local character/world for your first session. Profiles isolate mods, not saves; back up valued saves.

**Manual:** close Valheim, install the dependencies, and copy the archive's `plugins/Jarki-Fitness/` directory into `BepInEx/plugins/`. The DLL belongs at `BepInEx/plugins/Jarki-Fitness/Fitness.dll`. Remove any older manually installed `Fitness.dll`, including a copy under `plugins/Fitness/`; never keep two copies. Native Linux uses Linux Doorstop, not Proton DLL overrides.

Because the treadmill is a networked custom piece, **the server and every client must have matching Fitness installations**. This requirement does not establish multiplayer compatibility.

## Uninstall

Back up your saves and **remove all treadmill pieces before uninstalling**. Loading a world without its custom-piece definition can lose those pieces. Close Valheim, then uninstall through the mod manager or remove `BepInEx/plugins/Jarki-Fitness/`. Uninstall/reinstall has not been runtime-tested.

## Compatibility and troubleshooting

**Multiplayer and dedicated servers are unverified.** Windows, controller use, long sessions and unrelated-mod combinations are also unverified. Use a disposable local world before trying a shared or valued save. The packaged ZIP has not had a fresh mod-manager import test.

If the treadmill is missing, check both dependencies loaded, learn its ingredients, and use the Hammer near a workbench. If training stops, check stamina and nearby threats. For other issues, report game/mod/dependency versions, reproduction steps and redacted `BepInEx/LogOutput.log` errors. Never post passwords, tokens or private saves.

[Report an issue](https://github.com/Jarki/valheim-mods/issues) · [Source repository](https://github.com/Jarki/valheim-mods)
