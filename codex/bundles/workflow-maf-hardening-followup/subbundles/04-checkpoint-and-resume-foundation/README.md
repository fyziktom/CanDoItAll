# 04-checkpoint-and-resume-foundation

## Status

- Status: `Completed`

## Objective

Add a checkpoint abstraction and initial trusted storage path so long-running workflows, HITL, and future durable backends can resume safely.

## Covered Inputs

- R5: Add checkpoint abstraction and initial trusted storage implementation.
- R4: Use superstep/request events as checkpoint lifecycle evidence.

## Prerequisites

- SB03 event normalization is completed or blocked with explicit checkpoint impact.
- Checkpoint storage trust boundary is accepted as private infrastructure.

## Exact Source References

- `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Workflows/MafInProcessWorkflowExecutionBackend.cs`
- `repo://src/CanDoItAll.AgentFramework.Core/Workflows/WorkflowRuntimeManager.cs`
- `repo://src/CanDoItAll.AgentFramework.Models/Workflows/WorkflowModels.cs`
- `repo://src/CanDoItAll.AgentFramework.Persistence/CanDoItAll.AgentFramework.Persistence.csproj`
- `repo://src/CanDoItAll.Modules.AgentFramework/Persistence/PersistentWorkflowStores.cs`
- `repo://docs/workflow-maf-hardening.md`

## Scope

- Define checkpoint models and store/manager abstractions.
- Implement trusted in-memory or file-backed checkpoint capture for tests.
- Persist checkpoint metadata on superstep or equivalent runtime boundary.
- Implement minimal resume for a simple workflow or explicitly block resume with clear API/UI state.

## Dependency Impact

- SB05 artifact policy and SB07 backend honesty must avoid claiming production durability from preview checkpoint storage.

## Validation Depth

- Unit tests for checkpoint metadata plus runtime capture test.
- Critical proof requires trust-boundary source assertions and downstream smoke.

## Implementation Steps

1. Define checkpoint metadata models.
2. Define `IWorkflowCheckpointStore` and manager/factory abstractions.
3. Implement trusted test storage.
4. Wire checkpoint capture into in-process streaming execution where API-compatible.
5. Persist checkpoint metadata on superstep completion or equivalent lifecycle.
6. Implement minimal resume or explicitly disable resume with clear state.
7. Update documentation with security/trust-boundary rules.

## Do Not Do

- Do not load checkpoint blobs from user-uploaded or untrusted sources.
- Do not expose raw checkpoint blobs in normal UI.
- Do not claim production durability from in-memory checkpointing.

## Acceptance Checklist

- Checkpoint metadata is persisted for a test workflow.
- Pending request state is compatible with checkpoint capture.
- Resume behavior is implemented and tested or explicitly blocked with user-facing messaging.
- Documentation states checkpoint storage is a trust boundary.

## Proof Required

- Unit tests for checkpoint metadata store.
- Runtime test with checkpoint capture.
- Security/trust-boundary documentation update.
- `bundle://proof/SB04/manifest.md` and `bundle://proof/SB04/semantic-invariants.md`.

## Browser Validation Logging

- Browser proof is required only if resume/checkpoint UI state changes.

## Progression Gate

- Continue to SB05 only after checkpoint capture semantics and resume availability are explicit and cannot be mistaken for production durability.
- Result: `Passed`. Proof is captured in `bundle://proof/SB04/manifest.md` and `bundle://proof/SB04/semantic-invariants.md`.
- Runtime note: the in-process MAF backend captures metadata-only checkpoints at completed/failed/waiting boundaries. Resume is explicitly `NotSupported` until a durable backend can write trusted native runtime state.

## Suggested Agent Prompt

Add checkpoint metadata abstractions, trusted test storage, runtime capture proof, and clear resume availability state without exposing raw checkpoint blobs.
