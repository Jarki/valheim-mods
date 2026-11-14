# Fitness

Independent Valheim BepInEx/Jotunn mod. **0.1.3 is [published on Thunderstore](https://thunderstore.io/c/valheim/p/Jarki/Fitness/).** Adds a buildable wood-frame treadmill with a deer-hide belt and running muscle strain, automatic recovery walking and revised strain defaults. The Valheim-style package/build icons and border-corrected recipe image are published. Gameplay passed 713 isolated native assertions on Valheim l-1.0.21; all 92 method bodies and the embedded icon in the version-bumped release match the tested build. Multiplayer remains unverified. Public ZIP SHA-256: **c6550969c4d3a065c94394ceada839e7f1831a904d0cc0fca0b7a376cc9fa291**, matching the approved candidate. Not deployed to a gameplay profile or server.

**0.1.4 candidate prepared:** the minimal treadmill display shows saved
per-character/world kilometres and approximate calories. Gameplay passed
**775 native assertions**, with zero native errors; all **97 release method bodies**
and embedded resources match that native-tested build after the version bump.
Candidate SHA-256: `4666834310d88ae8f52fa9e941c40c36016927fdb60c64c3d348932b7b7a406e`.
Selected packaging verification and extracted-payload lint pass. The marked page
preview and exact-candidate upload await owner approval; 0.1.3 remains public.

[Watch the public six-biome showcase](https://jarki.github.io/valheim-mods/).
The Thunderstore page uses its thumbnail and the captured native recipe UI;
media is hosted separately and is not bundled in the mod ZIP. A fresh
mod-manager ZIP-import runtime test remains unverified.

The 0.1.3 public ZIP, dependencies, package icon and live page were verified after
upload. The corrected native recipe image is deployed on GitHub Pages and matches
the approved capture byte-for-byte. The README uses `?v=0.1.3` to refresh image caches.

Regenerate both original icon assets with Blender **3.6**:
`blender -b -t 2 -P mods/Fitness/Tools/build_icon.py`.
It writes the **256×256** package icon to `package/icon.png` and the **128×128**
transparent build icon to `Assets/treadmill.png`. Both use original procedural
geometry and quantized weathered wood/hide shading, not extracted game textures.
The build icon is embedded in `Fitness.dll`, assigned to the treadmill's native
`Piece.m_icon`, and its owned sprite/texture are released during plugin shutdown.
The in-game artwork has a transparent inset below the native title strip
(alpha bounds `(30, 38, 98, 110)` within 128×128), while the main package icon keeps
its original framing. `SpriteMeshType.FullRect` preserves that padding in UI geometry.

`-- --piece-variants` still renders the two camera options to ignored
`.dev/fitness-icon-options/`; `--output-dir PATH` overrides the output root.

### 0.1.3 visual verification and publication — 2026-11-12

- Release DLL and harness compile; **713 native assertions pass**, `nativeErrors=0`,
  game exit 0, on Valheim l-1.0.21 / Unity 6000.0.75f1, BepInExPack 5.4.2351 and
  Jotunn 2.30.2. Native `Hud.m_buildIcon` shows the custom treadmill sprite during
  initial and replacement construction, not the inherited wood-floor icon.
  The full walking/recovery, configuration, strain and lifecycle suite also passes.
- Corrected the owner-reported recipe-border overlap by framing the embedded
  artwork more loosely and moving it below the native title strip. The native
  icon RectTransform was not resized, and no game HUD layout was modified.
  The corrected full frame and recipe crop were inspected after recapture;
  the main package icon remains byte-identical. The previous oversized capture
  is retained privately as `.dev/fitness-visual-update/recipe-before-border-fix.png`.
- Native-tested gameplay DLL SHA-256 before the release-version bump:
  `9a1b30b43bbb680d7e0ac59054fa7a1286d566d3c49d026d91c4cc2ab8e353ad`.
  Evidence: `.dev/fitness-lab/runs/20261112221150082754-52993b49/evidence/`.
  Used a fresh disposable profile/world, private display `:92`, UDP 24888/24889.
  The owned desktop was stopped after clean game exit; shared Steam was untouched.
  The 0.1.3 package rebuild has **92 identical method bodies** and an identical
  embedded icon resource compared with that tested DLL. This metadata-only
  comparison is not a fresh mod-manager ZIP-import runtime test.
- `package/screenshots/fitness-recipe.png` is an unmodified **343×139 crop** of the
  actual native recipe panel, from a **1276×771** original frame. Crop coordinates
  `(467, 585, 810, 724)` follow the native `SelectedInfo` RectTransform, retaining
  the icon, description, all five ingredient amounts and workbench requirement.
  Both original and crop were inspected. This screenshot is hosted-page source,
  not an extra archive payload; it contains no painted or composited UI.
- Staging: granted materials/platform, native Hammer selection/placement, fixed
  midday/camera and diagnostic HP/stamina. The cropped panel does not establish
  ordinary-resource gameplay or multiplayer correctness. The first fixture run
  checked before the recipe panel was visible; waiting for the real `m_buildIcon`
  to bind its selected sprite fixed the harness without changing production code.
  Failed evidence remains under `.dev/fitness-lab/runs/20261112215246466496-2e5503e6/`.
  That native capture did not publish or deploy to a gameplay profile/server.
  After separate owner approvals, the recipe image and 0.1.3 ZIP were uploaded.
  Public ZIP, packaged README/DLL/icon and hosted recipe bytes match the reviewed
  inputs. Payload lint passed with 6 files, 0 failures and 0 warnings. The listing
  is active and unreviewed; dependency/category/NSFW settings are unchanged.
  Detailed publication records: `.dev/release/fitness-0.1.3/`. Media-only commit:
  `777dd536056788d6ec9916ef6c6587d59002e1cb`. No gameplay-profile/server deployment.

## Gameplay

### Muscle strain

Only sprinting uses this mechanic. Successful native running checks accumulate exertion; ordinary walking, jumping, swimming, attacks and other skills receive no new strain penalties.

| Strain level | Severity | Extra running stamina drain |
| --- | --- | --- |
| I | Mild | +15% |
| II | Moderate | +30% |
| III | Severe | +50% |

Penalties use vanilla status effects: percentages are added to the skill/equipment-adjusted base running drain, alongside other native run-drain modifiers. They do not increase every stamina cost. The HUD uses the native Run skill icon and a named severity effect.

Default ordinary-sprint buildup is one load unit per **60 cumulative sprint seconds at Run 0**, slowing linearly with skill: Run 25 takes 90 seconds, Run 30 takes 100 seconds and Run 50 takes 180 seconds per unit. At Run 75+, no new strain accumulates. Those timings hold skill constant and exclude recovery; actual training raises the skill while exercising. Unsuccessful running checks do not accumulate strain. `Treadmill / StrainBuildupMultiplier` defaults to **2×**, halving those active running times on the treadmill: **30 seconds at Run 0, 50 seconds at Run 30 and 90 seconds at Run 50**. It changes neither ordinary sprinting, recovery nor native Run XP timing.

Alive, non-sprinting time recovers one load unit per **600 seconds**. Walking is sufficient, including treadmill recovery walking. Active treadmill sprinting pauses recovery, even at an immune skill level. Teleporting, death and offline time do not heal strain. A newly triggered injury must heal a full unit before losing its severity: mild strain does not vanish immediately on stopping. Uninjured fractional fatigue also recovers gradually. Short rests retain unrecovered fatigue. Maximum load is 3.999 units, preventing unlimited recovery debt. Both load and earned healing severity persist in character custom data under `jarki.fitness.runStrain` and `jarki.fitness.runStrainLevel`.

### Wooden treadmill

- Build with the **Hammer → Furniture**, near a workbench: **20 Wood, 10 Core wood, 10 Bronze nails, 6 Deer hide, 10 Leather scraps**. Materials are recoverable through normal building removal.
- Interact to train Run in place. Default **1.5× Run XP** applies only to active treadmill training; ordinary running XP is unchanged.
- Uses native running stamina, including equipment, status effects and world modifiers; builds muscle strain **2× faster** than ordinary sprinting by default. `StaminaConsumptionMultiplier` defaults to **1×** and scales the final native treadmill running cost before consumption; `StrainBuildupMultiplier` defaults to **2×** and independently scales treadmill strain buildup. Both accept **0.1–5×**; neither changes ordinary running, recovery or the XP multiplier. **Exhaustion switches to walking in place while native stamina regenerates. Running resumes automatically only at full current maximum stamina.** Recovery walking grants no Run XP, incurs no running stamina cost and heals strain normally. No free stamina or stamina refunds; without native regeneration the player stays walking.
- Move or jump to dismount; interact again to stop, during either running or recovery walking. Blocking/attack inputs use native attachment cancellation too. **Inventory may remain open during the workout**: running still consumes stamina and earns XP/strain, while recovery walking still regenerates stamina and heals strain. Closing inventory does not restart the session. Other native input blockers (escape menu, chat/console focus, text/store/map/barber interfaces, text viewer, free camera and focused build search) still stop it, even with inventory open. Damage, death, teleporting, treadmill destruction and plugin shutdown terminate training. Starting a new session still requires ordinary gameplay input.
- Original procedural frame, handrails and rollers borrow native wood; the moving belt borrows the native `rug_deer` hide material. Belt UVs use its opaque body rather than the texture's transparent outline. No game assets are redistributed. The placed model, running pose and native status HUD were inspected in actual game screenshots/video.
- Network-owner reservations prevent simultaneous occupancy, with short renewable leases to release disconnected users. This network path has not been tested in multiplayer.

### Per-player kilometre counter (0.1.4)

The small screen above the treadmill's front rail shows only two lines:
cumulative treadmill **km** (three decimals) and estimated **~kcal** (whole calories).
An idle treadmill shows the viewing player's own totals.
Every treadmill uses the same character total; replacing a piece does not reset it.

Calories are a rough running-energy estimate: **70 kcal per kilometre**, assuming
a fixed 70 kg runner at approximately 1 kcal/kg/km. They derive from the same
saved distance, not Valheim food or stamina, and are not personalized measurements.

Distance integrates native running speed over successful treadmill running ticks.
Recovery walking, dismounted time and ordinary outdoor movement do not count.
Counting begins with this update; earlier workouts cannot be reconstructed.
Totals persist with the character under
`jarki.fitness.treadmillMetres.<world UID>`, separately for each world. Reconnecting
with the same character retains its saved total; another character starts its own.
This is character-save persistence, not a server-authoritative anti-cheat ledger.
Other clients read the occupant's synchronized distance; real multiplayer remains
unverified.


### Counter verification — development build

**Final km/calorie readout run passed 775 native assertions, zero native errors, exit 0.**
Evidence: `.dev/fitness-lab/runs/20261114101406469955-33012765/evidence/`.
Inspected the actual screen showing only **0.018 km / ~1 kcal**, backed by
**18.059992868453264 metres**. Existing strain, recovery, inventory, save/load,
cancellation, disable/re-enable, destruction and replacement scenarios passed.
The walking comparator now uses a straight native-control path on clear floor;
observed displacement was 5.514411m, with no added stamina cost.

- Native single-player counter checks passed on Valheim l-1.0.21:
  successful running accumulates virtual metres, dismounting and recovery walking
  add nothing, native character Save/Load retains the exact total, and automatic
  running resume continues that total. Earlier distance-only screen evidence:
  `Fitness Lab / 0.017 km / THIS WORLD`, backed by **17.77999297901988 metres**.
- Evidence: `.dev/fitness-lab/runs/20261114095531345955-3ef376ab/evidence/`.
  That run reached **348 passing assertions, zero native errors**, then stopped
  at the broader walking comparator: its time-dependent target sent the avatar
  against the treadmill rails. The comparator now starts on clear platform floor;
  no production collision/cancellation rules were changed.
- Nine simulated production-source checks also passed, including independent
  character/world totals and persistence when switching world IDs. Actual
  multiplayer synchronization and reconnecting to a dedicated server remain
  unverified. Native Save/Load used the same live character, not a process restart.

## Local gameplay test

1. Close Valheim. Create a separate **Fitness Test** profile in r2modman/Thunderstore Mod Manager; keep the normal profile untouched. Install the dependency versions below.
2. Open that profile's folder and copy only the built/downloaded `Fitness.dll` to `BepInEx/plugins/Fitness/Fitness.dll` (create the last directory). Never install `Fitness.InGameTests.dll`, lab profiles or development saves. Launch through the manager's **Start modded** route; native Linux needs Linux Doorstop, not Proton DLL overrides.
3. Create a **new disposable local character and world**. A mod-manager profile isolates mods/configuration, **not Valheim characters or worlds**. Do not join a live server for this first test; multiplayer has not been verified.
4. Play with ordinary health/stamina and real food, without god/debug resource cheats. Build a workbench, then use **Hammer → Furniture → Treadmill**, supplying the five ingredients listed above. Interact, observe native stamina draining and Run gaining experience; continue until exhaustion switches to walking without dismounting. Verify stamina recovers, Run XP pauses, then running resumes only at full stamina. Move/jump or interact to stop during either phase. Try ordinary sprinting too, and check that only running gets the strain penalty.
5. For a short strain test, first launch once to generate `BepInEx/config/jarki.fitness.cfg`, exit the game, and set `SecondsPerLevel = 10` under `[Muscle strain]`. Optionally set `RecoverySecondsPerLevel = 30`. Leave `RunExperienceMultiplier = 1.5` under `[Treadmill]`. Relaunch; low-Run training should show the named strain levels, then recover while resting/walking. These are configuration changes, not health/stamina overrides. Restore the production defaults **60 / 600** afterward.
6. Remove treadmill pieces before removing Fitness from any world you intend to keep. Keep this experimental profile away from valued saves and multiplayer until those paths are verified.

## Configuration

BepInEx creates `BepInEx/config/jarki.fitness.cfg` on a modded launch.

| Section / key | Default | Meaning |
| --- | --- | --- |
| `Treadmill / RunExperienceMultiplier` | `1.5` | Treadmill-only Run XP multiplier; range 0.1–10 |
| `Treadmill / StaminaConsumptionMultiplier` | `1.0` | Active treadmill-only native running stamina cost multiplier, applied once before consumption; range 0.1–5 |
| `Treadmill / StrainBuildupMultiplier` | `2.0` | Active treadmill-only sprint effort multiplier for strain buildup; ordinary running and recovery unchanged; range 0.1–5 |
| `Muscle strain / SecondsPerLevel` | `60` | Sprint seconds per exertion unit at Run 0; range 10–3600 |
| `Muscle strain / RecoverySecondsPerLevel` | `600` | Non-sprinting alive seconds to heal one exertion unit; range 10–3600 |
| `Muscle strain / StrainFreeRunSkill` | `75` | Run skill where new strain stops; range 1–100 |

Values are read during simulation; the mod does not synchronize configurations. Use matching settings on all clients. Fixed severity penalties are not configurable.

Existing configuration files retain their saved values. To adopt the new balance,
close the game and set `SecondsPerLevel = 60` and `RecoverySecondsPerLevel = 600`
under `[Muscle strain]`, plus `StrainBuildupMultiplier = 2` under `[Treadmill]`;
other custom settings are not overwritten.

### Balance rationale

The previous 90/180 defaults erased a 20-second sprint during the following
40-second walk even at Run 0. At Run 30, net fatigue required over 45% sprint
time; at Run 50, over 60%. The new 60/600 defaults lower those thresholds to
14.3% and 23.1%, retaining meaningful skill protection and Run-75 immunity.

Fixed-skill, 20ms production-model simulation; repeated **20s sprint / 40s walk**:

| Run skill | Previous first mild strain | Revised first mild strain |
| --- | --- | --- |
| 0 | Never in this repeating cycle | 3m12s |
| 25 | Never in this repeating cycle | 5m20s |
| 30 | Never in this repeating cycle | 6m20s |
| 50 | Never in this repeating cycle | 20m20s |

These are assumed travel cycles, not measured player habits or native stamina
simulations. They hold skill and duty cycle constant; food, terrain, increasing
Run skill and strain penalties change real sprint/rest timing. At Run 50,
10s sprint / 50s walking remains injury-free. Camping or crafting still heals;
elapsed play time alone is not an injury trigger.

## Dependencies and build

Requires `denikson-BepInExPack_Valheim` **5.4.2351** and `ValheimModding-Jotunn` **2.30.2**. Install on every client and the server when using a shared world with treadmill pieces. Plugin ID: `jarki.fitness`; assembly: `Fitness.dll`; prefab: `FitnessTreadmill`. Do not remove the mod from a valued world while it contains custom pieces.

From the repository root:

```sh
dotnet build mods/Fitness/Fitness.csproj -c Release \
  -p:GameDir=/path/to/Valheim -p:ReferenceProfile=/path/to/read-only/profile
dotnet run --project tests/Fitness.Tests/Fitness.Tests.csproj -c Release
```

The build produces `mods/Fitness/bin/Release/netstandard2.1/Fitness.dll`. No dependency/game assemblies are copied into the output for redistribution. Fitness is also supported by `uv run python tools/release.py check Fitness`, `build Fitness` and `verify dist/Jarki-Fitness-0.1.3.zip`; follow [the release gates](../../docs/RELEASING.md). These commands neither install nor publish it. Existing paused projects are unchanged.

## Verification — 0.1.2 recovery walking and balance

- **2026-11-12 native run passed 711 assertions**, `nativeErrors=0`, game exit 0,
  on Valheim **l-1.0.21**, Unity **6000.0.75f1**, BepInExPack **5.4.2351** and
  Jotunn **2.30.2**. Waited for the other agent's BigBatch game to exit, then
  used a new private desktop **`:92`**, UDP **24886/24887**, fresh saves/XDG/profile
  trees and PID-scoped control. Only this run's desktop was stopped afterward;
  shared Steam and other desktops were left untouched.
- Two consecutive natural exhaustion/recovery cycles used the avatar's original
  unfed **50-stamina pool and native regeneration**, with production **60/600**
  strain rates and **2× treadmill buildup**. No stamina writes or interactions
  occurred between exhaustion and automatic resume. Native stamina rose from
  **0 to 6.506732** during each early walking sample; a read-only `CheckRun`
  observer measured **50/50 before the first resumed running cost** in both cycles.
  Walking paused native Run XP pulses and healed persisted strain; attachment and
  reservation remained intact. Walking and resumed-running screenshots were inspected.
- Controlled boundary checks also passed: max-minus-one stamina stays walking,
  no walking drain/refund with regeneration disabled, native walking animation
  speed, movement cancellation during recovery, repeated exhaustion and clean reuse.
  Existing configuration, strain, persistence, inventory, damage, menu, disable,
  destruction and replacement scenarios passed in the same run. No gameplay fix
  was needed after native verification.
- Tested gameplay DLL SHA-256:
  `5ca6930f3bdd7642b92326bfe7fad3c3f58e7e0212c5693353b4ac8c2ff53e1b`,
  from the pre-version-bump native-tested build. Evidence:
  `.dev/fitness-lab/runs/20261112211557517384-3c5f4749/evidence/`
  (`result.json`, `runtime.log`, loader/player logs and `16a-natural-*.png`).
  The 0.1.2 package rebuild changes version metadata; a Mono.Cecil comparison of
  all **91 method bodies**, local types and exception-handler layouts against
  that native-tested DLL found **no differences**. This is not a fresh ZIP-import
  runtime test.
- Limits: native APIs, not keyboard input; granted materials/platform, disabled
  local controller and boosted HP. Other diagnostic stages use enlarged stamina,
  disabled regeneration and accelerated strain rates; only the natural-cycle
  stage restores original stamina/regen and production balance. Initial stamina
  was set once before mounting, not during either recovery interval. Audio fell
  back to emulated output before plugin startup; world-generation placement
  warnings remain. Multiplayer, long-session balance and mod-manager ZIP import
  are unverified. That native run did not package, publish or deploy to a gameplay profile/server.
- **0.1.2 publication:** package checks and archive verification passed; extracted
  payload lint passed with **6 files, 0 failures, 0 warnings**. The public ZIP
  downloaded after the approved upload matches the candidate SHA-256 above;
  packaged README/icon bytes match, and the live icon/showcase/recipe images load.
  Dependencies remain BepInExPack 5.4.2351 and Jotunn 2.30.2; categories remain
  Mods and AI Generated, NSFW false. The listing is active and **unreviewed**.
  The release-page preview was approved before upload. Detailed publication
  records are ignored under `.dev/release/fitness-0.1.2/`. No live deployment.

- Release gameplay DLL and native harness compile with **zero errors**. The plugin
  retains two existing framework-reference warnings; the harness retains those
  plus its existing hidden-member and obsolete-API warnings.

- Production-method smoke: **13 simulated checks** passed for last-stamina walking,
  partial/full recovery, XP pause, strain recovery, walk animation speed selection,
  changed maximum stamina, cancellation, reuse, failed running and input-flag cleanup.
  This compiles the actual `Train` method against a stubbed host; it does not prove
  native regeneration, rendered animation, attachment or network behavior.
- **14/14 standalone strain scenarios pass**, including fixed-skill travel onset,
  sustained recovery, light-travel protection and Run-75 immunity. Historical
  90/180 model scenarios remain explicit rate tests, not assertions of new defaults.
- Native harness now covers recovery walking, partial/full stamina boundaries,
  paused XP, healing, automatic resume, repeated exhaustion and recovery dismount.
  The movie fixture stops explicitly after showing exhaustion recovery walking.
- Native results above supersede the earlier simulation-only limit for the
  exercised single-player paths; older evidence below applies only to its builds.


## Verification — 2026-11-12 inventory continuation

- **0.1.1** Release build and native harness compile with zero errors; existing framework-reference warnings remain unsuppressed. The 13 standalone strain scenarios pass, including Run-0 onset at90/180/270 seconds.
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
