# Isolated in-game regression scenario

This is a separate BepInEx test plugin. It is not part of the main DLL or copied
by `launch-dev.sh`. **142 checks passed, zero failed** in the native client on
2026-10-27. Results and captures are under `.dev/pistol/evidence/arsenal-run4/`;
earlier fixture failures and fixes are recorded in `MODLOG.md`.

The scenario exercises native inventory Save/Load, ammo/reload/trigger behavior,
durability, real structure damage RPCs, denied and permitted ward access, blast
falloff, radius boundaries, and blast occlusion by a real building piece.
The expanded scenario also checks equipped visuals/rigs/materials, captures
hold/reload views, exercises projectile travel/impact, and crafts all six items
through the normal forge UI handler with resource-consumption checks. Its temporary
forge uses real extensions; fixture roof/fire prerequisites are waived and
fixture colliders are disabled to prevent displacing the player. Station-range,
recipe discovery, crafting, and resource checks use the native game rules.
It rejects shared sessions and worlds whose names do not start with `FirearmLab`.

Build using the same game/reference paths as the main plugin:

```sh
dotnet build tests/PistolMod.InGameTests/PistolMod.InGameTests.csproj -c Release
```

For a deliberately started disposable client test only:

1. Copy the test DLL from `bin/Release/netstandard2.1/` to
   `.dev/pistol/profile/BepInEx/plugins/`.
2. Launch with `FIREARM_TEST_DIR="$PWD/.dev/pistol/evidence/arsenal"` in the environment.
3. Create a new **local** character and an unshared **local** world named `FirearmLab...`.
4. Write an empty `run.request` file inside `FIREARM_TEST_DIR`; it waits for a
   grounded local player outside the intro/teleport sequence before starting.
5. Inspect `results.log`, `complete.txt`, BepInEx errors and in-game screenshots.
6. Remove the test DLL after testing. Keep only the normal plugin in the development profile.

For a targeted weapon/pose/audio rerun, use `pose.request` instead of `run.request`.
This stops after the weapon checks and screenshots, omitting structure/crafting
fixtures. The inspection camera is applied after the native game camera and
restores automatic LOD when finished.

The scenario grants test weapons/ammunition and creates/damages temporary pieces.
Use disposable saves only. It does not launch or test a dedicated server.
Inspect the generated screenshots/video for visible rig quality. Playback assertions
cannot establish subjective audio quality. Multiplayer needs separate verification.
The independent [Startup Helper](../../mods/StartupHelper/README.md) can select
the existing disposable local character/world on repeated development launches.
