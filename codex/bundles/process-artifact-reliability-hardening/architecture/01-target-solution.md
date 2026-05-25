# Target Solution

## Core Concept

Introduce a process-owned finalization pipeline that every process step completion path must use before a step transition is committed.

Suggested service names:

- `IProcessStepCompletionFinalizer`
- `ProcessStepCompletionFinalizer`
- `ProcessArtifactContractValidator`
- `ProcessArtifactProjectionDiagnosticsService`
- `ProcessArtifactRecoveryCoordinator`

The exact names may change, but the ownership boundary must not: finalization belongs to `CanDoItAll.Modules.Processes`.

## Finalizer Responsibilities

```text
FinalizeAsync(ProcessStepCompletionContext context)
  1. Verify dispatch claim / concurrency token when called by dispatcher.
  2. Project executor artifacts if the executor produced projectable artifacts.
  3. Reload current artifact ledger from PostgreSQL.
  4. Validate required artifact expectations by mode and contract.
  5. Persist projection/validation diagnostics for required artifact failures.
  6. If completed outcome has recoverable missing/invalid artifacts, invoke manager recovery once with evidence gap packet.
  7. Reload ledger after recovery.
  8. Revalidate artifacts.
  9. Return final transition request: Completed, Blocked, Failed, WaitingApproval, etc.
```

## Executor-Neutral Completion Context

The finalizer should not care whether work came from a direct agent or workflow-backed role.

Example fields:

```text
ProcessRunId
StepRunId
ExecutorKind = DirectAgent | WorkflowBackedRole | Subprocess | Manual | RecoveryManager
ExecutionRunId?
WorkflowRunId?
ResponseText?
CompletionStatusRequested
CompletionReasonRequested
SelectedBranchOutcomeId?
Artifacts
ToolReceipts
SerializedSessionStateJson
DispatchClaim?
RecoveryContext?
```

## Artifact Completion State

Replace boolean “missing/not missing” with a richer state:

```text
ArtifactExpectationValidationResult
  ExpectationId
  Status = Satisfied | Missing | InvalidFormat | InsufficientEvidence | StaleOrWrongRun | WrongProducerMode | PlaceholderOnly | DuplicateAmbiguous | RecoveryRequired
  SatisfyingArtifactRecordId?
  EvidenceReferences[]
  Diagnostics[]
  SuggestedAction = Complete | Recover | Block | RetryWithChangedPrompt | ManualReview
```

## Recovery Contract

Manager recovery should receive a typed recovery packet, not just a text directive:

```text
MissingArtifactRecoveryPacket
  ProcessRunId
  StepRunId
  FailedExecutionRunId
  MissingOrInvalidExpectations[]
  ProjectionDiagnostics[]
  UpstreamArtifacts[]
  ToolReceiptRefs[]
  CurrentRunArtifactRoot
  AllowedRecoveryOutputs[]
  DisallowedActions[]
```

Recovery output must be revalidated by the same `ProcessArtifactContractValidator`.

## Transition Rule

A process step may transition to `Completed` only when:

- requested outcome is compatible with completion
- every required expectation is `Satisfied`
- no required expectation is invalid, stale, placeholder-only, or wrong producer mode
- branch outcome requirements are satisfied when applicable
- recovery, if used, produced evidence-bound artifacts or explicit blocked diagnostics

Otherwise the finalizer returns `Blocked` or another explicit state with durable diagnostics.
