---
name: constraint-first-diagnosis
description: Use when startup, tooling, graphics, sandbox, or download failures tempt broad environment changes. Treat error text as a symptom and distinguish effective capabilities, CPU instruction support, renderer selection, and dependency scheduling with narrow probes.
---

# Constraint-first diagnosis

## Scope

Diagnose the effective execution environment before changing it. This skill does not authorize software installation, driver changes, security-policy changes, game launches, remote access, or server operations.

## Procedure

1. Preserve the reported error, relevant log context, exact executable/version, launch context, and last known working state. Treat the user's observed failure as evidence; do not rerun solely to confirm it.
2. Separate observation from hypothesis. For each plausible cause, choose a small discriminating probe and state its expected outcomes. Prefer existing logs and read-only capability/version inspection.
3. Inspect effective rather than nominal properties. Use the probes below only where relevant; do not run an indiscriminate environment audit.
4. Select the narrowest supported correction. Prefer a compatible tool build, a process-local launch adjustment, or a correct dependency interpretation over global security changes or driver replacement. Preserve the user's working environment.
5. Change one causal variable where practical, then exercise the original failing operation after authorization. A different command succeeding is insufficient if it bypasses the user's required path.
6. For finite background work, wait for fresh completion/error events from the current run. Distinguish current markers from old log entries; do not repeatedly restart or poll a healthy dependency operation.

## Discriminating probes

| Symptom | Inspect before changing anything |
| --- | --- |
| Illegal instruction or tool exits immediately | CPU features exposed to this process/VM and that tool release's documented ISA requirements |
| Sandbox or namespace initialization failure | Exact sandbox diagnostic and inherited/effective capabilities, not just whether namespaces exist |
| Unexpected graphics behavior | Active process/display renderer and API support, not merely the list of installed GPUs |
| Download stalled or suspended | Dependency queue, scheduler reason, current transfer/errors, then actual network measurements if still implicated |
| Host initialization fails | Required companion process/service and its readiness, separate from plugin initialization |

## Evidence and completion

Report symptom → discriminating evidence → cause or remaining hypothesis → narrow correction → result on the original path. Label an unproven cause as a hypothesis. Record what was intentionally not changed and restore temporary diagnostic state.

Complete when the original operation works under the identified constraints, or all reachable diagnosis is done and the precise missing prerequisite is documented. Never suppress an exception or disable security globally as a substitute for finding the cause.

## Failure patterns

- Replacing graphics drivers because a different GPU appears in hardware enumeration.
- Disabling system security because a process inherited incompatible capabilities.
- Retrying a CPU-incompatible binary without changing its compatibility.
- Treating normal dependency scheduling as a network failure.

## Origin

Extracted from [MODLOG.md](../../../MODLOG.md): 2026-10-27 capability-related Steam startup, CPU-incompatible Blender/ILSpy, actual renderer inspection, and download dependency scheduling; 2026-11-05 missing Steam-client prerequisite. These are historical diagnoses, not presumed causes for new failures.
