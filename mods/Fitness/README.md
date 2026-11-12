# Fitness

Independent Valheim BepInEx/Jotunn mod. **0.1.0 is [published on Thunderstore](https://thunderstore.io/c/valheim/p/Jarki/Fitness/); development version 0.1.1 is unreleased.** Adds a buildable wood-frame treadmill with a deer-hide belt and running muscle strain. Original mechanics were native single-player verified on Valheim l-1.0.21; multiplayer remains unverified. The 0.1.1 inventory-continuation patch has separate verification recorded below. Not deployed to a gameplay profile or server.

[Watch the public six-biome showcase](https://jarki.github.io/valheim-mods/).
The Thunderstore page uses its thumbnail and the captured native recipe UI;
media is hosted separately and is not bundled in the mod ZIP. A fresh
mod-manager ZIP-import runtime test remains unverified.

The original package icon is reproducible with Blender 3.6:
`blender -b -t 2 -P mods/Fitness/Tools/build_icon.py`. Materials are procedural;
no native icon textures or game models are extracted or packaged.

## Gameplay

### Muscle strain

Only sprinting uses this mechanic. Successful native running checks accumulate exertion; ordinary walking, jumping, swimming, attacks and other skills receive no new strain penalties.

| Strain level | Severity | Extra running stamina drain |
| --- | --- | --- |
| I | Mild | +15% |
| II | Moderate | +30% |
| III | Severe | +50% |

Penalties use vanilla status effects: percentages are added to the skill/equipment-adjusted base running drain, alongside other native run-drain modifiers. They do not increase every stamina cost. The HUD uses the native Run skill icon and a named severity effect.

Default buildup is one load unit per **90 cumulative sprint seconds at Run 0**, slowing linearly with skill: Run 25 takes 135 seconds, Run 30 takes 150 seconds and Run 50 takes 270 seconds per unit. At Run 75+, no new strain accumulates. Those timings hold skill constant and exclude recovery; actual training raises the skill while exercising. Unsuccessful running checks do not accumulate strain. `Treadmill / StrainBuildupMultiplier` scales only active treadmill sprint effort: `2` doubles its buildup and `0.5` halves it, without changing ordinary sprinting, recovery or native Run XP timing.

Alive, non-sprinting time recovers one load unit per **180 seconds**. Walking is sufficient; treadmill use is not rest. Sprinting pauses recovery even at an immune skill level. Teleporting, death and offline time do not heal strain. A newly triggered injury must heal a full unit before losing its severity: mild strain does not vanish immediately on stopping. Uninjured fractional fatigue also recovers gradually. Short rests retain unrecovered fatigue. Maximum load is 3.999 units, preventing unlimited recovery debt. Both load and earned healing severity persist in character custom data under `jarki.fitness.runStrain` and `jarki.fitness.runStrainLevel`.

### Wooden treadmill

- Build with the **Hammer → Furniture**, near a workbench: **20 Wood, 10 Core wood, 10 Bronze nails, 6 Deer hide, 10 Leather scraps**. Materials are recoverable through normal building removal.
- Interact to train Run in place. Default **1.5× Run XP** applies only to active treadmill training; ordinary running XP is unchanged.
- Uses native running stamina, including equipment, status effects and world modifiers; builds the same muscle strain as sprinting at default settings. `StaminaConsumptionMultiplier` scales the final native treadmill running cost before consumption; `StrainBuildupMultiplier` independently scales treadmill strain buildup. Both default to **1×** and accept **0.1–5×**; neither changes ordinary running, recovery or the XP multiplier. **No free stamina, stamina refunds or AFK endless training.** Native exhaustion stops training, which must be restarted after recovery.
- Move or jump to dismount; interact again to stop. Blocking/attack inputs use native attachment cancellation too. **Inventory may remain open during active training**: stamina, XP and strain keep progressing; closing inventory does not restart the session. Other native input blockers (escape menu, chat/console focus, text/store/map/barber interfaces, text viewer, free camera and focused build search) still stop it, even with inventory open. Damage, death, teleporting, treadmill destruction and plugin shutdown terminate training. Starting a new session still requires ordinary gameplay input.
- Original procedural frame, handrails and rollers borrow native wood; the moving belt borrows the native `rug_deer` hide material. Belt UVs use its opaque body rather than the texture's transparent outline. No game assets are redistributed. The placed model, running pose and native status HUD were inspected in actual game screenshots/video.
- Network-owner reservations prevent simultaneous occupancy, with short renewable leases to release disconnected users. This network path has not been tested in multiplayer.

## Local gameplay test

1. Close Valheim. Create a separate **Fitness Test** profile in r2modman/Thunderstore Mod Manager; keep the normal profile untouched. Install the dependency versions below.
2. Open that profile's folder and copy only the built/downloaded `Fitness.dll` to `BepInEx/plugins/Fitness/Fitness.dll` (create the last directory). Never install `Fitness.InGameTests.dll`, lab profiles or development saves. Launch through the manager's **Start modded** route; native Linux needs Linux Doorstop, not Proton DLL overrides.
3. Create a **new disposable local character and world**. A mod-manager profile isolates mods/configuration, **not Valheim characters or worlds**. Do not join a live server for this first test; multiplayer has not been verified.
4. Play with ordinary health/stamina and real food, without god/debug resource cheats. Build a workbench, then use **Hammer → Furniture → Treadmill**, supplying the five ingredients listed above. Interact, observe native stamina draining and Run gaining experience; move/jump to dismount, or continue until exhaustion. Try ordinary sprinting too, and check that only running gets the strain penalty.
5. For a short strain test, first launch once to generate `BepInEx/config/jarki.fitness.cfg`, exit the game, and set `SecondsPerLevel = 10` under `[Muscle strain]`. Optionally set `RecoverySecondsPerLevel = 30`. Leave `RunExperienceMultiplier = 1.5` under `[Treadmill]`. Relaunch; low-Run training should show the named strain levels, then recover while resting/walking. These are configuration changes, not health/stamina overrides. Restore the production defaults **90 / 180** afterward.
6. Remove treadmill pieces before removing Fitness from any world you intend to keep. Keep this experimental profile away from valued saves and multiplayer until those paths are verified.

## Configuration

BepInEx creates `BepInEx/config/jarki.fitness.cfg` on a modded launch.

| Section / key | Default | Meaning |
| --- | --- | --- |
| `Treadmill / RunExperienceMultiplier` | `1.5` | Treadmill-only Run XP multiplier; range 0.1–10 |
| `Treadmill / StaminaConsumptionMultiplier` | `1.0` | Active treadmill-only native running stamina cost multiplier, applied once before consumption; range 0.1–5 |
| `Treadmill / StrainBuildupMultiplier` | `1.0` | Active treadmill-only sprint effort multiplier for strain buildup; ordinary running and recovery unchanged; range 0.1–5 |
| `Muscle strain / SecondsPerLevel` | `90` | Sprint seconds per exertion unit at Run 0; range 10–3600 |
| `Muscle strain / RecoverySecondsPerLevel` | `180` | Non-sprinting alive seconds to heal one exertion unit; range 10–3600 |
| `Muscle strain / StrainFreeRunSkill` | `75` | Run skill where new strain stops; range 1–100 |

Values are read during simulation; the mod does not synchronize configurations. Use matching settings on all clients. Fixed severity penalties are not configurable.

## Dependencies and build

Requires `denikson-BepInExPack_Valheim` **5.4.2351** and `ValheimModding-Jotunn` **2.30.2**. Install on every client and the server when using a shared world with treadmill pieces. Plugin ID: `jarki.fitness`; assembly: `Fitness.dll`; prefab: `FitnessTreadmill`. Do not remove the mod from a valued world while it contains custom pieces.

From the repository root:

```sh
dotnet build mods/Fitness/Fitness.csproj -c Release \
  -p:GameDir=/path/to/Valheim -p:ReferenceProfile=/path/to/read-only/profile
dotnet run --project tests/Fitness.Tests/Fitness.Tests.csproj -c Release
```

The build produces `mods/Fitness/bin/Release/netstandard2.1/Fitness.dll`. No dependency/game assemblies are copied into the output for redistribution. Fitness is also supported by `uv run python tools/release.py check Fitness`, `build Fitness` and `verify dist/Jarki-Fitness-0.1.1.zip`; follow [the release gates](../../docs/RELEASING.md). These commands neither install nor publish it. Existing paused projects are unchanged.

## Verification — 2026-11-12 inventory continuation

- Unreleased **0.1.1** Release build and native harness compile with zero errors; existing framework-reference warnings remain unsuppressed. The 13 standalone strain scenarios pass, including Run-0 onset at90/180/270 seconds.
- With the owner's explicit isolated-test approval, the full native run passed **619 assertions**, nativeErrors=0, game exit0. Native inventory visibility blocked ordinary `TakeInput` while the treadmill stayed attached/running/reserved for2.2 seconds; stamina fell, Run XP accumulated in native0.3 pulses, and persisted strain load increased. Closing inventory preserved the session. The inventory-open screenshot was inspected.
- A blocking menu still detached and released the treadmill after inventory had been opened. Existing interact/move/jump, exhaustion, damage, disable/re-enable, destruction/replacement, configuration, XP, strain and persistence checks also passed.
- Tested and built DLL SHA-256: **29fe7dc03bd5bb0fe5ce8b65c9a1cd2ed7d63ddddcfc6f250651760aee06b4ef**. Evidence: ignored `.dev/fitness-lab/runs/20261112122501404452-18966be0/evidence/`. This is a disposable, directly installed development DLL test, not a mod-manager ZIP-import or multiplayer test.
- Fixture limits: scripted native APIs rather than keyboard movement; enlarged HP/stamina, regeneration disabled, ingredients/platform granted, accelerated10-second onset/recovery. Inventory UI and the production training path were real; no input-result/attachment-answer patches were used. This is diagnostic evidence, not ordinary-resource showcase footage. No publication or gameplay-profile/server deployment.

## Verification — 2026-11-07

- Release build passed against the owned native-game references: **0 errors**, two unsuppressed existing-reference conflict warnings (`System.Net.Http`, `System.IO.Compression`).
- **13/13 standalone behavioral scenarios passed**, covering skill scaling/immunity, three severity levels, fractional effort, healing hysteresis, save-state restoration, caps and 20ms simulation boundaries.
- A throwaway executable exercised the **compiled Fitness.dll** model: 180 seconds at Run 0 produced moderate strain/+30%; rest and reload preserved load 1.50 and moderate severity; immune sprinting did not heal it; subsequent rest fully cleared strain. This proves the exercised model, not Unity integration.
- **414 native assertions passed** for the gameplay mechanics before the leather recipe extension, in an isolated Valheim **l-1.0.21** client, Steam build **26249040**, Unity **6000.0.75f1**, BepInExPack **5.4.2351**, Jotunn **2.30.2**. Count includes fixture readiness and per-collider lifecycle invariants, not 414 distinct gameplay features. The game exited with code 0.
- Verified Hammer selection, valid placement ghost/raycast, native `TryPlacePiece`, real interaction, attachment, running animation and stamina consumption. Actual native Run XP accumulator deltas were **0.2 ordinary / 0.3 treadmill** (1.5×); setting the multiplier to 2.25 produced **0.450000018**. Ordinary running remained unchanged.
- Real treadmill exercise produced all three muscle-strain levels, visible native HUD labels and **115% / 130% / 150%** native run-drain modifiers. Jump/attack modifiers and walking cost were unchanged. Timed recovery, fractional-load/severity `Player.Save`/`Load`, ordinary-sprint strain and Run-75 immunity on both paths passed.
- Interact-stop, movement/jump dismount, exhaustion, menu and damage cancellation, disable/re-enable reuse, destruction while training and replacement construction/reuse passed. Collision exclusions, gravity, running state and occupancy cleanup were checked. Fixed a real shutdown exception caused by unregistering after Jotunn had destroyed the prefab; the final loader log contains no error/exception, including shutdown.
- The original SSH graphics blocker was a diagnosis error, not a missing screen. After the owner warned another agent was using the game, subsequent tests used a separate **Xvnc display `:90`**, **UDP 24876/24877**, fresh save/XDG/profile trees, and PID-scoped control. A completed run recorded another Valheim PID concurrently, without driving its display. The private desktop was stopped afterward.
- Changing the Steam service wrapper's lifetime mode unexpectedly restarted shared Steam and aborted one attempt with a disconnected IPC pipe. This may have affected the other agent; it was disclosed immediately. The later full run passed with Steam left alone. No live profile/save, server or publication changes were made.
- Mechanics evidence: `.dev/fitness-lab/runs/20261107124228348516-01202817/evidence/` (`runtime.log`, `BepInEx.log`, `Player.log`, `result.json`, placement/training/strain/replacement PNGs). Screenshots were inspected, not merely generated. That run's tested DLL matched its contemporaneous Release DLL; the later leather recipe extension produces a new artifact.
- Leather recipe extension: native `HaveRequirements` independently rejected missing Deer hide and missing Leather scraps with all other ingredients present; native construction consumed exactly **6 Deer hide and 10 Leather scraps**. Placement screenshot and measurements: `.dev/fitness-lab/runs/20261107130105975414-355dc881/evidence/`. That take was later interrupted by an odd-height video-encoder issue, unrelated to construction.
- Normal-resource video run `.dev/fitness-lab/runs/20261107130359258033-8d0ed5c1` passed **84 assertions** with the updated recipe and no health/stamina overrides. Native Salad, Bread and Onion soup supplied food buffs; stamina fell from **258.8213 to 0** through **28.63 seconds** of genuine training, with unfrozen Run skill progressing from 0 to 3. Normal exhaustion detached the player and restored gravity/collisions. The silent **34-second H.264 MP4** was inspected and browser playback/seeking verified on the LAN gallery. Demo onset is explicitly accelerated to 10s (default 90s), recovery remains 180s. Production base resources are never modified by Fitness.
- Deer-hide belt build: `.dev/fitness-lab/runs/20261107132930034765-ea723301` passed **84 native assertions**, nativeErrors=0, game/recorder exit 0. Visually confirmed the opaque hide belt with wood frame/rollers in actual placement and running footage. Normal food-derived stamina exhausted **258.8213 → 0 in 28.64s**, Run **0 → 3**. Refreshed the LAN DLL/video/screenshots; HTTP-downloaded DLL SHA-256 matches the tested artifact: `14ca77452176f9142da94d14f2fb32b3bf921f2798fe83c1ee3e4fa189b53b61`. Chromium played the 34.082s video and sought to 15s without a media error.
- Configurable treadmill stamina/strain build: `.dev/fitness-lab/runs/20261107135821045734-c82461cf` passed **562 native assertions**, nativeErrors=0, game exit 0. Actual native stamina/load ratios matched independent settings **2.5×/3×**, **0.5×/0.25×**, and both range endpoints **0.1×/5×** and **5×/0.1×**. Ordinary sprinting stayed **1×/1×**, non-running stamina costs were unchanged, native recovery matched its control and the XP timer retained its original simulation interval. Existing placement, strain, persistence and lifecycle scenarios passed in the same run.
- **Full-suite fixture limits (not the normal-resource video):** native gameplay APIs were driven rather than OS Hammer clicks; onset/recovery were configured to 10 seconds, stamina/HP enlarged, regeneration disabled, ingredients/platform granted and fixture support decay disabled. Save/load used real package bytes on the same live avatar, not a process restart. Private-display audio fell back to emulated output before plugin startup; audio was not tested. Multiplayer, death/teleport transitions, long sessions and compatibility with other gameplay mods remain unverified.

### Repeatable isolated native run

Requires explicit game-test authorization and the repository's safety procedure. Build the separate harness; never ship its DLL:

```sh
dotnet build tests/Fitness.InGameTests/Fitness.InGameTests.csproj -c Release
agent-desktop start fitness-verify
uv run python tests/Fitness.InGameTests/run.py setup
FITNESS_RUN=disposable-local-only uv run python tests/Fitness.InGameTests/run.py run \
  --run-dir <path-printed-by-setup> --desktop-session fitness-verify --port 24876
agent-desktop stop fitness-verify
```

The launcher discovers existing native Doorstop files from earlier local labs, or accepts `VALHEIM_LOADER_PROFILE`. It never installs into the game directory. Without an explicit private desktop session it refuses to launch while another Valheim process exists. Never stop/restart shared Steam to manage the test's lifetime.

Append `--record-video` to select the separate normal-resource demo instead of the boosted-resource verification suite. It consumes real food, preserves ordinary health/stamina/regeneration, trains until natural exhaustion, holds a fixed camera and records only the owned game window to `evidence/training.mkv`. FFmpeg pads an odd window dimension by one pixel for H.264/yuv420p compatibility. This mode is silent and explicitly discloses its accelerated 10-second strain onset; it does not change production defaults.

## License and credits

Original code and procedural geometry: MIT, copyright 2026 Jarki. Development used OpenAI Codex (`gpt-6.1-sol`) assistance. Game materials/icons are borrowed only at runtime and are not redistributed.
