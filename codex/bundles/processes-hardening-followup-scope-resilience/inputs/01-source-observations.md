# Source Observations

## Reviewed Branch

- `processes-hardening`
- Head commit: `a3ce7b2659bfeeaf9a7400bfbb99274b1f2171b6`
- Compare base: `development`

## Important Reviewed Sources

- `repo://src/CanDoItAll.Modules.Processes/README.md`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Execution.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ExecutionPrompt.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactProjection.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.CompletionArtifactRecovery.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs`
- `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessRuntimeEventTypes.cs`
- `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`

## Key Implemented Changes Observed

- A new `ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs` file adds:
  - `ProcessStepCompletionExecutorKind`
  - `ProcessArtifactExpectationMode`
  - `ProcessArtifactValidationStatus`
  - `ProcessArtifactProducerKind`
  - `FinalizeStepCompletionAsync`
  - `ValidateRequiredCompletionArtifactsAsync`
  - artifact validation diagnostic journal entries.
- Direct AgentFramework completion now calls `FinalizeStepCompletionAsync`.
- Workflow-backed role completion now calls `FinalizeStepCompletionAsync`.
- Stranded manager recovery completion now calls `FinalizeStepCompletionAsync`.
- Manager recovery fallback was improved: the old generic `lead` fallback was removed and explicit artifact-recovery capability signals were added.
- `ProcessRuntimeEventTypes.ArtifactValidationDiagnostic` was added.
- Tests were added around finalizer source routing and artifact validation.

## Observed Remaining Weak Spots

1. Workflow-backed process steps are still created with empty expected artifact lists and empty artifact inputs in `LoadDispatchCandidateAsync`. This makes the finalizer structurally unable to validate workflow-backed process step contracts.
2. Subprocess parent completion still transitions directly after `ProjectCompletedSubprocessArtifactsAsync` and does not use `FinalizeStepCompletionAsync`.
3. Subprocess parent projection still creates a parent `ProcessArtifactRecord` with the required `ArtifactExpectationId` even when no matching child artifact exists.
4. The finalizer artifact mode resolver is heuristic and string-based. It can misclassify generic process artifacts when words like `log`, `screenshot`, `json`, or `markdown` appear in ordinary artifact descriptions.
5. Placeholder detection is broad and may reject legitimate planning or diagnostic artifacts containing words like `todo`, `not available`, or `missing artifact`.
6. Current-run artifact lineage validation is incomplete. Most producer kinds pass current-run checks as long as `ProcessRunId` and `StepRunId` match; only `existing-managed-artifact` receives extra execution-run scrutiny.
7. Missing upstream artifact materialization blocks the downstream step before rerunning the source step, but the visible dispatch query only loads `Ready`, `WaitingApproval`, and `InProgress`; a blocked downstream step may not automatically resume after upstream materialization unless another service unblocks it.
8. The prompt has many strong scope instructions, but there is still no explicit step operation policy enforced by tools. Prompt-only guardrails cannot reliably prevent an architecture or planning agent from mutating product files.
9. Branchable review/QA decisions can still become hard `Blocked` states when artifact validation fails, even if the process model has a repair/rework/no-go branch that should be selected instead.
10. Retry behavior still risks repeated attempts for the same no-progress condition before the finalizer sees the outcome, especially for missing tools, failed validation, unavailable proof, or scope/tool-policy mismatch.
