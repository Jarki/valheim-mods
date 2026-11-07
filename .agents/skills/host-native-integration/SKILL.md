---
name: host-native-integration
description: Use when a plugin or extension compiles but fails in its actual host, especially fonts, materials, coordinates, camera timing, interaction APIs, or runtime assets. Prefer verified host conventions over generic engine assumptions.
---

# Host-native integration

## Scope

Engine support does not imply availability in a shipped application. Follow the project's engine, reverse-engineering, and automation skills when applicable. Inspect only authorized local references; do not redistribute proprietary assets or decompiled code.

## Procedure

1. Record the actual host build, engine/runtime, loader, and relevant dependency versions. Separate reference-assembly compatibility from runtime behavior.
2. Find an existing host feature with the same responsibility. Inspect its public APIs, initialization order, resource ownership, and effective runtime values before designing a replacement.
3. Choose the narrowest native integration that meets the requirement: the host font and UI hierarchy, scaled projection, existing material, interaction handler, or documented update phase. Do not introduce a broad fallback chain to conceal a missing prerequisite.
4. Respect resource ownership. Borrowed fonts/materials must outlive consumers; do not destroy shared resources or mutate them globally when a local change is required. Rebind after host replacement where necessary.
5. Run a thin native slice early: instantiate/register one element, inspect its rendering, and exercise its ordinary interaction path. A successful build is not this check.
6. For spatial or visual features, check the relevant coordinate spaces, actual render scale, camera update order, visibility, and LOD. Test both ordinary settings and the specific alternate setting implicated by the change.
7. Restore temporary graphics, camera, capture, and profile changes. Keep diagnostic hooks separate from the shipped feature.

## Evidence and completion

Record the native symbol/resource used, why it matches the responsibility, observed behavior, and tested host versions/settings. For UI, inspect actual screenshots and runtime errors; a non-null text/material object is insufficient. If native execution is unavailable or unauthorized, report the integration as unverified rather than claiming a simulated result is equivalent.

Complete when the ordinary host path demonstrates the feature and relevant changed settings do not break it. Do not generalize one host build's workaround to all engine versions.

## Failure patterns

- Assuming generic shaders or legacy font data are shipped because an engine API exposes them.
- Using window dimensions where internal render dimensions differ.
- Moving a camera transform while the host subsequently overwrites it from yaw/pitch state.
- Duplicating native crafting or collision rules instead of entering through their normal handler.

## Origin

Extracted from [MODLOG.md](../../../MODLOG.md): 2026-11-05 TMP/native font and scaled projection fixes; 2026-10-27 shader, camera-timing, and ordinary forge-handler verification. Historical examples, not new runtime proof.
