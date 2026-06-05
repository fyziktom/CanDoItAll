# Target Solution

## Target Boundary

Create a module-local observation, declared-outcome, and completion-decision boundary inside `CanDoItAll.Modules.Processes` without introducing Process Core or production driver APIs.

## Target Helpers

- `ProcessAutomationSessionObservation` for session-state observations.
- `ProcessAutomationExecutionLogObservation` for execution-log observations.
- `ProcessAutomationObservationSnapshot` for combined dispatch evidence.
- `ProcessDeclaredStepOutcomeRules` for declared outcome parsing and branch facts.
- `ProcessCompletionDecisionSnapshot` for completion inputs.
- `ProcessCompletionStatusDecisionRules` for status decisions.
- `ProcessCompletionReasonBuilder` for completion reason text.

## Non-Goals

- No `CanDoItAll.Processes.Core` project.
- No `IProcessDriverPack`, `IProcessDriverRegistry`, driver package, or driver DI surface.
- No UI, Razor, CSS, JavaScript, or TypeScript changes.
