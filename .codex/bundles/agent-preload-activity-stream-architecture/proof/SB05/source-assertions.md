# SB05 Source Assertions

## Activity and startup

- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Execution/AgentExecutionActivityCoordinator.cs`
  creates and publishes typed `Accepted` during `AdmitOperation`.
- `repo://src/Modules/CanDoItAll.Modules.AgentFramework/Workspace/CurrentProfileAgentFrameworkWorkspaceService.cs`
  admits the operation before cold workspace resolution and owns failure
  terminalization/disposal.
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.ExecutionRuns.cs`
  consumes one catalog snapshot, provider acquisition, and atomic chat-backed start.

## Provider

- `repo://src/Modules/CanDoItAll.Modules.AgentFramework/Providers/ProviderRuntimeProfileSnapshotService.cs`
  owns factory-created no-tracking loaders, scalar revision probes, immutable
  publication, generation fencing, and profile-switch invalidation.
- synthetic fallback resolution returns before context creation.
- reusable provider state contains configuration and revision identity, not a secret
  payload, client, `DbContext`, session, agent, tool, approval, or authorization result.

## File persistence

- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Persistence/Storage/FileSandboxWorkspaceStore.cs`
  owns the in-process gate, cross-process lock, catalog/session/run validation,
  journal-before-commit, and atomic start result.
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Persistence/Storage/FileSandboxWorkspaceExecutionSliceStore.cs`
  owns split execution indexes, latest-header/index reads, usage delta publication,
  and idempotent commit stages.
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Persistence/Storage/FileSandboxWorkspaceJsonStore.cs`
  records physical JSON opens and atomically replaces temporary files; it does not
  prove `flushToDisk` durability.

## Process queries

- `repo://src/Processes/CanDoItAll.Processes.Application/ProcessRuntimeProjectionQueryService.cs`
  selects bounded live runs and calls state/assignment batch APIs.
- `repo://src/Processes/CanDoItAll.Processes.Persistence/EfProcessRuntimeUnitOfWork.cs`
  uses no-tracking split queries and enforces a maximum batch count.
- `repo://src/Processes/CanDoItAll.Processes.Persistence/EfProcessRuntimeStepAssignmentStore.cs`
  uses a no-tracking run-ID batch query with deterministic ordering.

## Explicit residuals

- `repo://src/Foundation/CanDoItAll.Infrastructure/Persistence/DatabaseRuntimeSwitching.cs`
  invokes switch subscribers synchronously.
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Persistence/Storage/FileSandboxWorkspaceJsonStore.cs`
  writes/replaces without a physical flush contract.
- `repo://src/Modules/CanDoItAll.Modules.AgentFramework/Providers/ProviderRuntimeProfileSnapshotService.cs`
  cannot atomically span a remote database commit and later external provider use.

Current SHA-256 values tying these assertions to the reviewed working-tree state are
recorded in `bundle://proof/SB05/transcripts/source-hashes.md`.
