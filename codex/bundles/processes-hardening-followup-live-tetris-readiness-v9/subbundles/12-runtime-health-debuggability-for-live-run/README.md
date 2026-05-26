# SB12: Runtime Health Debuggability For Live Run

## Status

- Status: `Completed`

## Objective

Expose actionable runtime health for generic Blazor WASM PWA live runs: step status, block reason code, block cause, recovery options, next action, missing artifacts, invariant diagnostics, execution attempts, and artifact satisfaction.

## Covered Inputs

- RQ07 health diagnostics and blockers.

## Prerequisites

- SB11 writeback proof is complete.

## Exact Source References

- `repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspaceRunDetailsLoader.cs`
- `repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspaceRunStepsDialog.razor`
- `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessRuntimeViewModels.cs`
- `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`

## Deliverables

- Runtime health/API/UI fields that explain why a live run is blocked and what to do next.
- Tests proving generic recovery classifications and recommended actions.

## Dependency Impact

- SB13 and SB14 depend on clear runtime diagnostics for regression and live-run readiness.

## Validation Depth

- Integration/component tests for health projection, plus browser proof if UI layout changes.

## Implementation Steps

1. Audit run detail health and step dialog projections.
2. Add missing diagnostic fields or display state.
3. Validate blocked and healthy live-run states.

## Do Not Do

- Do not introduce topic-specific health messages.
- Do not hide missing proof as residual risk.

## Acceptance Checklist

- Block reason code and recovery options are visible.
- Missing artifact count and invariant diagnostics are visible.
- Next action is actionable for operators and agents.

## Proof Required

- `proof/SB12/manifest.md`
- `proof/SB12/semantic-invariants.md`
- `proof/SB12/transcripts/passing.txt`
- `proof/SB12/transcripts/source-assertions.txt`

## Browser Validation Logging

- If UI changes, record route, viewport, open dialog state, assertions, screenshots, and result in `bundle://reviews/01-execution-report.md`.

## Progression Gate

- SB13 may start after runtime health diagnostics are proven in tests and browser proof when UI changes.

## Suggested Agent Prompt

Harden generic live-run health diagnostics so blocked Blazor WASM PWA runs explain the missing proof, cause, recovery options, and next action.
