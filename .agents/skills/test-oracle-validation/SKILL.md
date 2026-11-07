---
name: test-oracle-validation
description: Use when designing a test harness, diagnosing inconsistent test results, or deciding what a passing check actually proves. Validate fixture preconditions and independent observations before attributing failure to production code.
---

# Test oracle validation

## Scope

Make the observer trustworthy before trusting its verdict. Applies to UI automation, integration tests, simulations, and runtime probes. This skill does not authorize launches, input capture, deployment, or profile changes; obtain the approvals required by the project.

## Procedure

1. State the consumer-visible claim and its expected result before running the check.
2. Identify the observation that can establish that claim: arithmetic output, real host state, rendered pixels, persisted data, or downloaded artifact bytes. A stubbed host cannot establish native rendering or integration correctness.
3. Assert fixture readiness independently of the behavior under test. Confirm the actual target identity, ownership, input state, interaction range, granted resources, and active UI mode as relevant. Read effective values, not assumed defaults.
4. Exercise one thin end-to-end path early. For a UI feature, load it, render one real element, and perform one real interaction before expanding coverage.
5. Observe the consequence through a separate surface where practical: resource consumption after crafting, persistent state after reload, pixels alongside widget state. Do not use the same calculation to produce both expected and actual results.
6. Separate fixture failure from product failure. If a prerequisite fails, mark dependent assertions blocked rather than counting their downstream failures as independent defects. Do not loosen production rules to make a fixture pass.
7. Repair a faulty fixture and rerun the affected scenario. For a product defect, preserve a failing-before/passing-after regression when practical. Use permanent tests for consumer behavior, not copied implementation details.

## Evidence and completion

Report the claim, fixture preconditions, action, observed result, and evidence location. Name the evidence boundary explicitly:

- Pure or simulated checks establish only the exercised logic/state model.
- Real-host execution establishes only the exercised host behavior and versions.
- Screenshots establish the inspected view, not all resolutions or lifecycle states.
- Archive checks establish package contents; public hash equality establishes artifact identity, not gameplay correctness.
- Single-player success does not establish multiplayer authority or synchronization.

Complete when the intended path ran with valid preconditions and its consequence was observed, or state the exact missing prerequisite and leave the claim unverified. Preserve failure evidence before replacing fixtures.

## Failure patterns

- Many failures share one setup dependency: check that dependency before changing multiple production paths.
- A scripted camera shows missing geometry: verify native visibility, LOD, and camera timing before blaming the asset.
- A UI object exists but appears blank: inspect actual pixels; object existence is not rendering proof.

## Origin

Extracted from [MODLOG.md](../../../MODLOG.md): 2026-10-27 fixture corrections and run2–run4; 2026-11-05 native MobThreat verification and release records. These are historical examples, not fresh checks or universal host guarantees.
