# Stable Core Roadmap Update

## Completed In This Bundle
- Execution evidence descriptors are in Core with module-owned AgentFramework execution and retry orchestration.
- Finalizer intent/result descriptors are in Core with module-owned finalizer invocation and transition application.
- Retry/no-progress/provider diagnostics are in Core with module-owned provider repair, recovery packet creation, and retry persistence.
- Projection/validation descriptors are in Core with module-owned storage, filesystem, browser output probing, lineage persistence, and projection orchestration.
- Adapter ownership and public API surface are guarded by architecture tests and proof transcripts.

## Remaining Non-Core Areas
- EF contexts, queries, storage drivers, workspace path resolution, filesystem operations, provider repair, retry scheduling, claims, transitions, finalizer application, AgentFramework execution, runtime service orchestration, Blazor/UI, and external service calls remain outside Core.

## Future Core Candidate Filter
Future Core additions must:
- Accept immutable snapshots or primitive value facts.
- Produce deterministic values.
- Avoid module, infrastructure, AgentFramework, storage, filesystem, workspace, UI, and external-service dependencies.
- Have adapter-owned module boundaries.
- Update API owner classification and architecture proof.

## Decision
- Stable Core is ready for driver-contract prerequisite work.
- No broad runtime extraction is approved.
