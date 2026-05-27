# SB14: 14-recovery-and-operator-approval-correctness

## Goal

Prove recovery manager and operator approval cannot fake required artifacts.

## Required work

- Operator decision artifacts must remain decision evidence unless explicitly mapped to a decision expectation.
- Manager recovery must create/rebind the original required artifact with current-run lineage and content.
- Pending approval must not leave active/failed mixed state without clear next recovery action.

## Required proof

- Failing-first or adversarial proof.
- Passing production-path proof.
- Source assertions with exact repo paths.
- Anti-stub audit.
- Changed-file hashes.
- Classification: MAF package-level / MAF adapter-level / process runtime-level / template/UI-level.
- Note whether this subbundle changes behavior or only improves proof/documentation.

## Closure criteria

This subbundle is complete only when proof files under `proof/SB14` are updated and downstream subbundles can rely on it.

## Status

- Completed

## Objective

Prove recovery and operator approval cannot mask missing required artifact content.

## Covered Inputs

- RQ09 recovery and operator approval semantics.

## Prerequisites

- SB11 and SB13 close content validation and read-model parity.

## Exact Source References

- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs`

## Deliverables

- Recovery remains bound to current-run artifact evidence and diagnostics.

## Dependency Impact

- SB18 relies on this as a release-readiness boundary.

## Validation Depth

- Existing finalizer/recovery tests plus SB11/SB13 focused regressions.

## Implementation Steps

- Preserve manager recovery checks.
- Ensure missing content remains visible to operators.

## Do Not Do

- Do not allow approval to synthesize required evidence without lineage.

## Acceptance Checklist

- Required artifact content failures remain operator-visible.

## Proof Required

- SB11 and SB13 proof manifests.

## Browser Validation Logging

- No browser route is affected.

## Progression Gate

- Recovery remains honest before final release gate.

## Suggested Agent Prompt

Verify recovery and approvals preserve current-run evidence rather than hiding content failures.
