# Refactoring Checkpoints

Codex must not continue adding logic into already large partial classes without extracting cohesive services.

## Checkpoint A after SB03

Extract and stabilize:

- `ProcessTargetGroundingLedgerBuilder`
- `ProcessStepOperationContractResolver`
- `ProcessInvocationMetadataBuilder`

Status: completed in SB04.

Implemented boundary:
- `ProcessInvocationMetadataBuilder` owns metadata assembly and is the production path behind `BuildProcessInvocationMetadataJson`.
- `ProcessStepOperationContractResolver` exposes operation-contract resolution for direct tests without private-method reflection.
- `ProcessTargetGroundingLedgerBuilder` exposes grounding resolution, mutable/read-only alias selection, pruning, and ledger construction for production metadata assembly and direct tests.

Exit criteria:
- `ProcessRunAutomationDispatchService.ExecutionMetadata.cs` stops growing as a monolithic classifier.
- Unit tests cover the extracted services without reflection where possible.

## Checkpoint B after SB06

Extract and stabilize:

- `ProcessToolOperationAuthorizer`
- `ProcessScriptSideEffectAnalyzer`
- `ProcessCompletionArtifactValidator`
- `ProcessArtifactIdentityService`

Status: completed in SB07.

Implemented boundary:
- `ProcessToolOperationAuthorizer` owns governed-step operation enforcement after `AgentToolInvocationPolicy` resolves the required operation family.
- `ProcessScriptSideEffectAnalyzer` owns typed script findings for write, encoded command, shell delegation, and child-script signals.
- `ProcessCompletionArtifactValidator` owns the production artifact-validation entrypoint used by the dispatch service compatibility wrapper.
- `ProcessArtifactIdentityService` owns projection lineage normalization, stable identity hashing, and normalized serialization before artifact dedupe/persistence.

Exit criteria:
- `AgentToolInvocationPolicy.cs` does not continue accumulating all process-specific logic.
- Policy logic has unit tests for each operation family.
- Artifact validator and identity service have direct integration tests, including a generic non-software deliverable case.

## Checkpoint C after SB10

Extract and stabilize:

- `ProcessRecoveryRouter`
- `ProcessBlockStateClassifier`
- `ProcessHealthInvariantAuditor`
- `WorkflowSubprocessArtifactMapper`

Status: completed in SB11.

Implemented boundary:
- `ProcessRecoveryRouter` remains the isolated executable recovery decision service introduced in SB10.
- `ProcessBlockStateClassifier` owns typed block cause classification, reason-code inference, and recovery option selection.
- `ProcessHealthInvariantAuditor` owns runtime step health classification, actionable reason construction, manual-rerun eligibility, and recovery-state exposure.
- `WorkflowSubprocessArtifactMapper` owns workflow output mapping, subprocess child expectation mapping, legacy fallback warnings, and ambiguity blocking.

Exit criteria:
- Runtime recovery is testable without running the full process dispatch service.
- Manual/API transitions and automated finalizer use shared classifier/router services.
- Workflow/subprocess projection mapping is directly testable outside the dispatch partials.
