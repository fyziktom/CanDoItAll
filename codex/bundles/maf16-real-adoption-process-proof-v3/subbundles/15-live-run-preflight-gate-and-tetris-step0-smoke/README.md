# SB15: 15-live-run-preflight-gate-and-tetris-step0-smoke

## Goal

Prepare a safe real-run gate before full live test.

## Required work

- Run only step 0 through live profile or deterministic real-ish harness.
- Verify current-run delivery contract artifact validates through finalizer and read model.
- Do not proceed to implementation until this gate passes.
- Capture API evidence bundle for the step0 smoke.

## Required proof

- Failing-first or adversarial proof.
- Passing production-path proof.
- Source assertions with exact repo paths.
- Anti-stub audit.
- Changed-file hashes.
- Classification: MAF package-level / MAF adapter-level / process runtime-level / template/UI-level.
- Note whether this subbundle changes behavior or only improves proof/documentation.

## Closure criteria

This subbundle is complete only when proof files under `proof/SB15` are updated and downstream subbundles can rely on it.

## Status

- Completed

## Objective

Prepare the next live run gate using deterministic first-step artifact validation proof.

## Covered Inputs

- RQ10 safe preflight gate.

## Prerequisites

- SB11 and SB13 must pass before a full live run.

## Exact Source References

- `repo://Templates/Processes/README.md`
- `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`

## Deliverables

- Deterministic preflight proof and runbook direction for the next live run.

## Dependency Impact

- SB18 relies on this to avoid another ambiguous full live test.

## Validation Depth

- First-step delivery contract simulated by focused integration tests.

## Implementation Steps

- Validate required brief content failure and read-model projection.
- Record that no full live run was attempted in this bundle.

## Do Not Do

- Do not run the full live user process until the gate is green.

## Acceptance Checklist

- Step0-equivalent required brief proof passes in tests.

## Proof Required

- SB11, SB13, and SB18 proof artifacts.

## Browser Validation Logging

- No browser route is changed by this bundle.

## Progression Gate

- Deterministic preflight must pass before the user performs the next full live test.

## Suggested Agent Prompt

Use the focused artifact validation tests as the preflight gate before attempting a full Blazor/Tetris live run.
