# Source Cutline

## Allowed production source changes

Allowed under:

- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/`

Expected files may include:

- `ProcessAutomationSessionObservation.cs`
- `ProcessAutomationExecutionLogObservation.cs`
- `ProcessAutomationObservationSnapshot.cs`
- `ProcessDeclaredStepOutcomeRules.cs`
- `ProcessDeclaredStepOutcomeBranchRules.cs`
- `ProcessCompletionDecisionSnapshot.cs`
- `ProcessCompletionStatusDecisionRules.cs`
- `ProcessCompletionReasonBuilder.cs`
- `ProcessCompletionBlockerSnapshot.cs`
- `ProcessCompletionDecisionDiagnostics.cs`

## Existing files allowed to be touched

- `ProcessRunAutomationDispatchService.ToolValidation.cs`
- `ProcessRunAutomationDispatchService.Concurrency.cs`
- `ProcessRunAutomationDispatchService.ArtifactValidation.cs`
- `ProcessRunAutomationDispatchService.Execution.cs`
- `ProcessAutomationReceiptObservationHelper.cs`
- `ProcessToolReceiptFacts.cs`
- focused tests under `tests/`

## Forbidden

- New project `CanDoItAll.Processes.Core`
- New production driver API/registry/package
- UI files
- EF/storage/service-scope/execution-client calls inside pure observation/rule helpers
