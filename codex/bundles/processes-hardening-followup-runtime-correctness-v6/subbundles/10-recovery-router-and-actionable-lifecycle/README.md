# SB10: Make recovery options executable and deterministic.

## Objective

Make recovery options executable and deterministic.

## Why This Matters

This subbundle closes a concrete runtime correctness gap observed after phase5. The process runtime must avoid both false completion and unnecessary blocking while staying generic.

## Implementation Tasks

- Add `ProcessRecoveryRouter`.
- Given block code, failure ownership, and diagnostics, select next recovery action.
- Persist recovery routing events and next-action state.
- Prevent repeated no-progress recovery attempts without new evidence.
- Add tests for wait-for-materialization, recover-artifacts-only, fresh-agent-session, human-escalation, repair-implementation.

## Required Tests

- Add failing-first or red-team tests before the production fix where practical.
- Add positive tests proving the fixed behavior.
- Include at least one generic/non-software case if this subbundle changes generic process semantics.

## Closure Criteria

- Production code implements the behavior; no prompt-only fix.
- Proof manifest is updated.
- Focused tests pass.
- No SQLite runtime/migration dependency is introduced.

## Status

- Completed

## Covered Inputs

- RN06 infer wrong block/recovery classification from broad reason text.
- RQ10 executable recovery router.
- RQ04 typed block cause propagation.

## Prerequisites

- SB05 typed block cause proof is trusted.
- SB09 mapping proof is trusted for projection-related recovery.

## Exact Source References

- repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessStepRunBlockState.cs
- repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.RuntimeReadQuery.Support.cs
- repo://src/CanDoItAll.Modules.Processes/Automation/Recovery/ProcessRunRecoveryWorker.cs
- repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.Runtime.Rerun.cs
- repo://tests/CanDoItAll.Tests.Integration/ProcessesServiceIntegrationTests.cs

## Deliverables

- `ProcessRecoveryRouter` or equivalent cohesive service.
- Deterministic next recovery action from block code, failure ownership, diagnostics, and recent attempts.
- Persisted recovery routing events and next-action state where supported by the runtime model.
- No-progress guard that prevents repeated recovery without new evidence.

## Dependency Impact

- SB11 extracts and stabilizes recovery/health services after router behavior lands.
- SB13 diagnostics display actionable recovery state.
- SB14 final scenarios validate the router across process types.

## Validation Depth

- Tests for wait-for-materialization, recover-artifacts-only, fresh-agent-session, human-escalation, and repair-implementation.
- Negative test for repeated no-progress recovery.
- Source assertion for lifecycle event persistence.

## Implementation Steps

- Add router decision model and map typed block causes to next actions.
- Persist or expose next-action state consistently in read models.
- Record recovery lifecycle events.
- Add no-progress repeat guard.
- Record proof under `bundle://proof/SB10/`.

## Do Not Do

- Do not infer router behavior from broad reason text when typed state is available.
- Do not add a display-only router with no lifecycle effect.
- Do not retry indefinitely without new evidence.

## Acceptance Checklist

- [x] Router produces deterministic next actions for each required recovery class.
- [x] Runtime persists or exposes actionable recovery lifecycle state.
- [x] Repeated no-progress attempts are blocked or escalated.
- [x] Focused tests pass.

## Closure Notes

- Added `ProcessRecoveryRouter` and persisted `NextRecoveryAction` on `ProcessStepRun`.
- Added PostgreSQL migration `20260526015652_ProcessRecoveryNextAction`.
- Added lifecycle event `recovery-routing-decision-recorded`.
- Focused SB10 tests, SB05/SB09 regression slice, and migrations build passed.

## Proof Required

- `bundle://proof/SB10/manifest.md`
- `bundle://proof/SB10/semantic-invariants.md`
- Failing-first or red-team transcript.
- Passing focused test transcript.
- Changed-file SHA-256 transcript.
- Anti-stub audit transcript.

## Browser Validation Logging

- N/A: SB10 changes runtime recovery behavior only.

## Progression Gate

- SB11 may start only after router behavior is deterministic and recovery lifecycle proof exists.

## Suggested Agent Prompt

- Implement SB10 recovery routing with typed causes, update `proof/SB10`, run focused recovery tests, and record gate closure.
