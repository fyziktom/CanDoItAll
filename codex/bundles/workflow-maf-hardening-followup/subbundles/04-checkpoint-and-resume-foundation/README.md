# 04-checkpoint-and-resume-foundation

## Objective

Add a checkpoint abstraction and initial trusted storage path so long-running workflows, HITL, and future durable backends can resume safely.

## Exact source references

- `src/CanDoItAll.AgentFramework.Maf/Runtime/Workflows/MafInProcessWorkflowExecutionBackend.cs`
- `src/CanDoItAll.AgentFramework.Core/Workflows/WorkflowRuntimeManager.cs`
- `src/CanDoItAll.AgentFramework.Models/Workflows/*`
- `src/CanDoItAll.AgentFramework.Persistence/*`
- `src/CanDoItAll.Modules.AgentFramework/Persistence/PersistentWorkflowStores.cs`
- `docs/workflow-maf-hardening.md`

## Implementation steps

1. Define checkpoint models:
   - checkpoint id,
   - run id,
   - workflow id/version,
   - backend,
   - superstep/index,
   - storage provider,
   - storage path/key,
   - schema version,
   - created timestamp.
2. Define `IWorkflowCheckpointStore` and `IWorkflowCheckpointManagerFactory`.
3. Implement a trusted in-memory or file-backed checkpoint path for tests.
4. Wire checkpoint manager/storage into MAF in-process streaming execution.
5. Persist checkpoint metadata on superstep completion.
6. Implement one of:
   - minimal resume from latest checkpoint for a simple workflow, or
   - explicit API/UI state that checkpoint capture exists but resume is disabled until SB follow-up.
7. Add trust-boundary documentation and security checks.

## Do not do

- Do not load checkpoint blobs from user-uploaded or untrusted sources.
- Do not expose raw checkpoint blobs in normal UI.
- Do not claim production durability from in-memory checkpointing.

## Acceptance checklist

- Checkpoint metadata is persisted for a test workflow.
- Pending request state is compatible with checkpoint capture.
- Resume behavior is either implemented and tested or explicitly blocked with clear user-facing messaging.
- Documentation states checkpoint storage is a trust boundary.

## Proof required

- Unit tests for checkpoint metadata store.
- Runtime test with checkpoint capture.
- Security/trust-boundary documentation update.
