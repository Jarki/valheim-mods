---
name: lifecycle-transition-checks
description: Use when implementing or reviewing pooled UI, caches, scene-bound objects, subscriptions, or other stateful resources. Check shrinking, empty, invalid, disabled, destroyed, and recovery transitions rather than only steady-state output.
---

# Lifecycle transition checks

## Scope

A bookkeeping reset is not a resource-state reset. Use this skill for existing stateful features; do not add pooling, caching, or a lifecycle abstraction merely to follow it.

## Procedure

1. Identify logical state and external effects separately: active count versus rendered children; cached key versus live host reference; subscription flag versus registered callback.
2. Define observable invariants. For pooled labels, live rendered labels must correspond exactly to eligible current targets; inactive entries must not render or retain invalid scene references.
3. Map ownership: who creates, activates, reassigns, deactivates, clears references, and destroys each resource? Follow existing host lifecycle hooks and conventions.
4. Exercise the applicable transitions below using the real changed path. Select relevant cases rather than generating a Cartesian product.
5. Fix the state transition at its owner. Deactivate externally visible objects before discarding the bookkeeping needed to find them. Clear references when their lifetime ends. Do not hide leaks with a second overlay or repeated allocation.
6. Verify recovery as well as cleanup. Reuse after clearing must restore correct identity, content, position, and visibility without stale state.

## Transition checklist

- Many eligible objects → fewer → zero → nonzero again.
- Valid input → invalid/unknown input → valid input.
- Same count, different object identities or order.
- Visible → hidden → visible; enabled → disabled → enabled.
- Target destroyed or replaced; owner/scene exits and re-enters.
- Repeated clear/disable calls where those calls are supported.

## Evidence and completion

Observe external effects, not just counters: rendered children, active callbacks, held host references, or persisted resources as appropriate. Pair screenshots with runtime state for visual defects. Include at least the regression transition and recovery. A simulated binding check must be labeled simulated and cannot establish native visual correctness.

Complete when the applicable transitions preserve the stated invariants with no orphaned effects or invalid references. Record deliberately unsupported transitions rather than silently assuming them safe.

## Failure patterns

- Setting an active count to zero while children remain active.
- Updating a pool only when its size changes, leaving stale identities at equal size.
- Treating partial initialization as a successful cache hit.
- Cleanup that works only on normal exit, not on disable or target destruction.

## Origin

Extracted from [MODLOG.md](../../../MODLOG.md): 2026-11-05 MobThreat ghost-label, unknown-estimate, disable/re-enable, and world-exit checks; 2026-10-27 partial-rig cache correction. Historical evidence, not fresh verification.
