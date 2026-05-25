# Target Runtime Architecture

```mermaid
flowchart TD
    Definition[Process Definition]
    Contract[Persisted Step Operation Contract]
    Lint[Definition Lint + Publish/Start Gate]
    Dispatch[Process Dispatch]
    Metadata[Execution Metadata]
    ToolPolicy[Operation-Aware Tool Policy]
    Executor[Agent / Workflow / Subprocess]
    Projection[Typed Artifact Projection Adapter]
    Finalizer[Process-Owned Finalizer]
    Audit[Runtime Invariant Audit]
    Recovery[Typed Recovery / Escalation]
    Next[Next Step Activation]

    Definition --> Contract
    Contract --> Lint
    Lint --> Dispatch
    Dispatch --> Metadata
    Metadata --> ToolPolicy
    ToolPolicy --> Executor
    Executor --> Projection
    Projection --> Finalizer
    Finalizer --> Audit
    Audit --> Recovery
    Recovery --> Next
```

## Key Shift

The process runtime should not depend on keyword guessing for core governance. The next version should use persisted typed contracts and only use heuristics as fallback/migration helpers.

## Required Runtime Concepts

- `ProcessStepOperationContract`
- `ProcessStepOperation`
- `ProcessStepTargetScope`
- `GroundedTargetAliasLedger`
- `ArtifactProjectionIdentity`
- `ProcessArtifactOutputMapping`
- `ProcessRuntimeInvariantViolation`
- `ProcessBlockReasonCode`
- `ProcessRecoveryOption`
