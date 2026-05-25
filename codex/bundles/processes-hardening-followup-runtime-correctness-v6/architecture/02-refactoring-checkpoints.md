# Refactoring Checkpoints

Codex must not continue adding logic into already large partial classes without extracting cohesive services.

## Checkpoint A after SB03

Extract and stabilize:

- `ProcessTargetGroundingLedgerBuilder`
- `ProcessStepOperationContractResolver`
- `ProcessInvocationMetadataBuilder`

Exit criteria:
- `ProcessRunAutomationDispatchService.ExecutionMetadata.cs` stops growing as a monolithic classifier.
- Unit tests cover the extracted services without reflection where possible.

## Checkpoint B after SB06

Extract and stabilize:

- `ProcessToolOperationAuthorizer`
- `ProcessScriptSideEffectAnalyzer`
- `ProcessCompletionArtifactValidator`
- `ProcessArtifactIdentityService`

Exit criteria:
- `AgentToolInvocationPolicy.cs` does not continue accumulating all process-specific logic.
- Policy logic has unit tests for each operation family.

## Checkpoint C after SB10

Extract and stabilize:

- `ProcessRecoveryRouter`
- `ProcessBlockStateClassifier`
- `ProcessHealthInvariantAuditor`
- `WorkflowSubprocessArtifactMapper`

Exit criteria:
- Runtime recovery is testable without running the full process dispatch service.
- Manual/API transitions and automated finalizer use shared validation services.
