---
name: snapshot-before-mutation
description: Use when a batch operation changes permissions, visibility, topology, or other predicates used to decide later actions. Decide whether to snapshot eligibility before mutation, then preserve that policy without confusing a snapshot with a transaction.
---

# Snapshot before mutation

## Scope

Use for operations such as area damage, bulk deletion, or access-controlled graph updates where early mutations can alter later eligibility. Do not apply snapshots indiscriminately: some workflows intentionally require live, sequential decisions.

## Procedure

1. List every decision predicate and every mutation. Identify which mutations can change predicates for later targets: removing a wall, disabling an access boundary, deleting a parent, or changing ownership.
2. State the policy explicitly: eligibility at operation start, or eligibility after each preceding action? Derive it from the feature contract. If unspecified and materially consequential, resolve that policy before implementation.
3. For operation-start semantics, collect candidate identities and the required eligibility results before applying any operation effects. Evaluate access and visibility on the authoritative actor/context, not an unrelated local user.
4. Separate decision from mutation. Apply effects only to candidates approved by the initial decision phase. Preserve existing target filters and damage/action APIs rather than rebuilding their unrelated rules.
5. Define handling for missing authority, vanished targets, and external state changes. A snapshot is not atomicity or continuing authorization. Use host transactions/version checks if required; skip invalid targets or fail closed when the contract requires it. Do not silently switch authority.
6. Keep only the data needed for the operation and reuse existing buffers where appropriate. Do not introduce avoidable per-frame snapshots, whole-world copies, or a generic transaction framework.
7. Exercise the destructive dependency case and vary candidate order. Under operation-start semantics, removing an early blocker must not newly authorize later targets.

## Evidence and completion

Compare the approved target identities and resulting effects for at least two relevant candidate orders, plus a denied target and the missing-authority path where applicable. Check that normal authorized targets still work. Record whether concurrent changes are supported and whether partial application is possible.

Complete when observed effects follow the chosen temporal policy and authority rules. A single-peer check cannot establish multiplayer ownership migration correctness.

## Failure patterns

- Checking visibility after an earlier effect destroyed the obstruction.
- Checking access after the operation destroyed the access controller.
- Treating a local-player permission API as authoritative on every peer.
- Calling a snapshot a transaction despite partial effects or concurrent mutations.

## Origin

Extracted from [MODLOG.md](../../../MODLOG.md): 2026-10-27 RPG visibility/ward snapshot design and authoritative-shooter constraints. The log records local blast checks, while multiplayer remained unverified. Application to other batch workflows is a derived engineering technique, not recorded testing of those systems.
