# SB10: Process Artifact Validation Live-Run Regression

## Status

- Completed

## Objective

Prove the failed live-run artifact binding failure is fixed by production code.

## Covered Inputs

- RQ05: prove process artifact validation with live-run/integration regression.

## Prerequisites

- SB09 adapter boundary checkpoint must be complete.

## Exact Source References

- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.CompletionArtifactValidator.cs
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs
- repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs

## Deliverables

- Regression for current-run org-scoped artifact acceptance.
- Distinct rejection statuses for stale run, wrong step, wrong expectation, wrong execution run, and unreadable content.

## Dependency Impact

- SB11, SB12, SB13, SB14, and SB18 depend on this artifact-validation foundation.

## Validation Depth

- Critical semantic proof must include integration-path tests, not only unit mocks.

## Implementation Steps

- Reconstruct the failed run artifact class from captured evidence.
- Add failing-first/adversarial tests for wrong lineage and unreadable content.
- Fix production validation if needed.
- Update `proof/SB10`.

## Do Not Do

- Do not hardcode the failed run id or Blazor/Tetris template.
- Do not collapse content/hash failures into `StaleOrWrongRun`.

## Acceptance Checklist

- Current-run valid artifact is accepted.
- Wrong lineage/content cases report distinct statuses.
- Integration-path tests pass.

## Proof Required

- Failing-first transcript for wrong lineage/content.
- Passing integration transcript.
- Source assertions, anti-stub audit, and hashes.

## Browser Validation Logging

- N/A - no browser-visible behavior in this subbundle.

## Progression Gate

- SB11 may start only after current-run validation semantics are proven.

## Suggested Agent Prompt

Prove and, if needed, fix production process artifact validation for current-run org-scoped artifacts and distinct invalid states.

## Closure Proof

- bundle://proof/SB10/manifest.md
- bundle://proof/SB10/semantic-invariants.md

