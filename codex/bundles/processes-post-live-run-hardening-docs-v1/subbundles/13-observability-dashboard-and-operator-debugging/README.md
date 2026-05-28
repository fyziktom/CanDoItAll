# SB13: Observability Dashboard And Operator Debugging

## Status

- Status: Completed

## Objective

- Improve operator debugging of process runs.

## Covered Inputs

- RN13 maps to RQ13.
- Preserve the original bundle scope for this subbundle.

## Prerequisites

- SB03, SB06, and SB07 completed.

## Exact Source References

- repo://src/CanDoItAll.Modules.Processes/Components/LiveProcessesDashboard.razor
- repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspaceRunsOperatorConsoleSection.razor
- repo://src/CanDoItAll.Modules.Processes/Runtime/Observation/ProcessObservationDashboardState.cs
- repo://tests/CanDoItAll.Tests.Components/ProcessWorkspaceTests.cs

## Deliverables

- Operator-visible run health, artifact matrix, diagnostics, roots, manager reason, receipts, approvals, and recovery advice.

## Dependency Impact

- SB18 relies on operator visibility for final red-team.

## Validation Depth

- UI/API proof plus browser validation if rendered surfaces change.
- Include failing-first or adversarial proof when behavior changes, passing proof, source assertions, anti-stub audit, changed-file hashes, classification, and proof-debt closure status.

## Implementation Steps

- Inspect the referenced source and nearby tests.
- Implement the smallest correct change set for this subbundle only.
- Update proof artifacts under bundle://proof/SB13/.

## Do Not Do

- Do not hard-code Blazor, Tetris, project ids, run ids, local paths, or user-specific paths.
- Do not use docs-only changes to satisfy runtime proof requirements.

## Acceptance Checklist

- Deliverables are complete.
- Required tests or source assertions are recorded.
- Execution report gate rows are updated.

## Proof Required

- bundle://proof/SB13/manifest.md
- bundle://proof/SB13/semantic-invariants.md when the subbundle is critical or behavior-changing.
- Command transcripts under bundle://proof/SB13/transcripts/.

## Browser Validation Logging

- Required for any rendered dashboard/operator-console change.
- Update bundle://reviews/01-execution-report.md if browser proof is applicable.

## Progression Gate

- SB18 may rely on observability only after completed, blocked, failed, and recovered states are proved.

## Suggested Agent Prompt

- Execute SB13 literally, preserve runtime genericity, and close owned proof before moving downstream.
