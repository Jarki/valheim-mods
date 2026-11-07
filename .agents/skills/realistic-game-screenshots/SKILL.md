---
name: realistic-game-screenshots
description: Use when capturing player-facing mod screenshots, composing believable native gameplay scenes, or replacing diagnostic fixture images with showcase captures. Covers equipped characters, real biomes, camera composition, genuine feature states, capture readiness and original-image review.
---

# Realistic game screenshots

## Scope and boundaries

Capture a believable scene rendered by the actual game and unchanged production mod, not a mockup. A staged native-game capture is acceptable; fabricated UI, composited enemies/inventory and painted feature labels are not. Describe staging honestly in development evidence. A screenshot proves the inspected scene, not combat balance, normal AI behavior, clean-profile installation or multiplayer compatibility.

Read the project's mod-development and engine guidance, plus `game-automation`, before runtime work. Use `host-native-integration` for camera/rendering problems and `constraint-first-diagnosis` for capture/tooling failures. In this repository run toolkit commands as `uv run um`.

Writing this skill, editing a page or packaging a mod does not authorize launching/driving a game, installing loaders, changing working profiles, server access or uploads. Obtain explicit runtime authorization first. Screenshot hosting and package publication each require separate approval. Follow project game-access coordination; never take over another session.

## 1. Choose a small shot list

- Inspect existing captures first. Keep useful approved shots; replace diagnostic images that do not communicate the feature.
- Give each shot one player-visible purpose: ordinary use, a useful context, or an important alternate state. Do not repeat the same view without a reason.
- Plan a recognizable environment, character loadout, subject placement and visible feature. An empty starter character in a dark test clearing is not automatically a useful showcase.
- For an inventory feature, show a plausible populated native inventory. For an enemy overlay, show recognizable enemies and readable labels. Preserve the HUD needed to understand the feature.
- When asked only for an additional shot, retain the approved selection rather than rebuilding the whole set.

## 2. Isolate the scene and preserve the real feature

- Use a separate development loader profile and disposable local characters/worlds. Isolate save locations and relevant configuration/data/cache roots using the project's verified launcher; do not assume a server save flag isolates native client saves or Steam Cloud.
- Check for an existing game session before launching. Track the exact launched PID and its window; do not use wildcard process kills or resize unrelated windows.
- Load the intended production DLL unchanged and record its version/hash. Keep any scene harness separate from the shipping plugin and release payload.
- Staging may grant native equipment/food, position real enemies, hold AI and select native daylight/weather for composition. Record these interventions. Do not alter production calculations, force its displayed captions/colors, or imply that held AI is a live combat test.
- Change the real gameplay input for alternate states, then let the production feature update. If the desired result does not occur, inspect the state; never paint it into the image.

## 3. Make the player look played

- Equip a coherent progression-appropriate set of armor, held weapons/shield and tools; consume native food where appropriate.
- Populate the inventory with plausible quantities of supplies, ammunition and consumables. Avoid repeated filler stacks merely to occupy slots.
- Check actual equipped flags, held weapon, current/max health, armor, food and carried weight after native updates settle. Do not trust item creation alone to equip gear.
- Avoid accidental encumbrance unless that is the subject. Reduce granted quantities rather than hiding the weight indicator.
- Let grant-generated discovery/unlock messages finish before capture, or clear only the disposable fixture's transient grant notifications. Preserve ordinary feature UI and relevant status indicators.

## 4. Use a real, fully loaded location

- Find actual native terrain in the intended biome. Weather alone does not turn another biome into a Mountain or Swamp scene.
- Search for usable ground and confirm the player's actual biome after relocation. Apply constraints to the subject positions, not an unnecessarily perfect surrounding area: nearby Swamp puddles are normal even when the player and enemy need dry footing.
- Wait for teleport completion, native scene/zone readiness and loaded terrain, then for vegetation, environment, equipment and HUD to settle. Prefer observed readiness over a fixed sleep alone.
- In Valheim, relevant observations include `ZNetScene.IsAreaReady(destination)`, `ZoneSystem.IsZoneLoaded(destination)`, a loaded `Heightmap`, and the current player biome/environment. Resolve signatures against the installed game before implementing a harness.
- Use a bounded readiness deadline and retain logs. Continued native location generation is a streaming constraint, not automatically a mod defect. The historical Mountain fixture needed up to 300 seconds rather than its initial 120-second deadline; these are examples, not universal timeouts.

## 5. Compose through the real camera

- Search nearby clearings and camera headings rather than forcing the first spawn location to work. Prefer readable native light, clear silhouettes and enough environment to identify the encounter.
- Reject terrain-clipped cameras, blocked camera-to-player paths and foliage hiding the subject. Check sightlines at the enemy's lower body, torso and head; a clear label above an obscured enemy is insufficient.
- Keep relevant enemies and labels separated. If a reticle hover-name overlaps the mod overlay, move the subject off-reticle or reframe; do not hide vanilla HUD or modify the mod to conceal the overlap.
- Use host-native camera controls/state and the correct update phase. Writing only a transform can be undone by native yaw/pitch or follow-camera updates.
- Preview at the intended page size as well as original resolution. Leave enough space around labels and inventory panels; do not sacrifice the actual feature for a scenic background.

## 6. Confirm state, capture, inspect

1. Observe fixture readiness and the actual production feature before capture: visible subject count, rendered captions/colors and relevant input state. Object existence alone does not prove visibility.
2. For alternate health/configuration states, wait for at least the feature's refresh interval and native UI updates; assert the actual result rather than predicting it from the requested input.
3. Confirm effective window and render dimensions. Launch flags may be overridden by a tiling window manager. Resize only the launched game window if authorized, and verify both runtime dimensions and the saved image dimensions.
4. Capture a real rendered frame with a GPU-compatible window backend or native end-of-frame capture. In Unity, a harness can capture after `WaitForEndOfFrame`; do not assume the request means the asynchronous PNG write has finished.
5. Wait for the completed file and decode it before changing scenes or quitting. File existence or a nonzero size alone is not proof of a complete image. Allow the required additional rendered frames for the capture backend.
6. Inspect every selected original visually: correct scene, readable feature, recognizable enemies, plausible equipment/inventory, no clipping, popups, unintended overlays, truncation or wrong colors. Check logs separately for runtime/shutdown errors.
7. Retain original pixels. Do not upscale a small capture and describe it as native resolution. Derivative crops/resizes must not remove context that changes the claim; keep a clickable original for small HUD text. Fix lighting/color issues in the native scene or capture path, not with invented UI or undisclosed recoloring.

## 7. Review delivery and restore isolation

- Render the exact proposed page and selected assets; inspect desktop/mobile layout, image loading, full-resolution links and horizontal overflow. A good original can still be unreadable in a narrow page.
- Follow `publish-mod` for changed-page previews and approval. Serve previews from an isolated asset directory, not the repository, profiles or logs. Do not upload merely to make a preview work.
- Record image paths, production DLL identity, game/loader versions, actual resolution, relevant player/scene/feature state, staging interventions and any observed errors in private development evidence. Do not commit saves, profiles, proprietary binaries or operational state.
- Exit only the session you launched. Remove temporary capture plugins from the disposable profile, restore temporary settings and leave working profiles/saves untouched. Keep useful originals and evidence under ignored development storage; do not ship capture tooling.

## Completion checklist

- Each selected shot serves a distinct requested purpose.
- Genuine environment and native character/enemies/UI are loaded and settled.
- Loadout, inventory, food and weight make sense for the scene.
- Subjects and real feature states are unobscured; no camera clipping or accidental HUD overlap.
- Actual dimensions and completed image decoding are confirmed; originals inspected.
- Staging and runtime errors are recorded without overstating the evidence.
- Final page layout is checked when applicable; approvals remain separate from capture.
- Temporary runtime changes are removed/restored without touching unrelated sessions.