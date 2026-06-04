# Execution Boundary Staging

## Stage 1: Facade Inside Processes

Introduce a process-owned execution client/facade that wraps the existing `IAgentFrameworkWorkspaceService` calls.

Required initial shape:

```csharp
internal interface IProcessAutomationExecutionClient
{
    Task<ExecutionRunResult> ExecuteRunAsync(
        ExecutionRunRequest request,
        CancellationToken cancellationToken = default);

    Task<ExecutionRunDetail> GetExecutionRunDetailAsync(
        Guid executionRunId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ExecutionRunRecord>> ListExecutionRunsAsync(
        ExecutionRunQuery query,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AgentDefinition>> ListAgentsAsync(
        bool includeTemplates,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProviderProfile>> ListProvidersAsync(
        CancellationToken cancellationToken = default);

    Task<ProviderHealthResult> TestProviderAsync(
        Guid providerId,
        CancellationToken cancellationToken = default);

    Task<AgentEditorModel> GetAgentEditorAsync(
        Guid agentId,
        CancellationToken cancellationToken = default);

    Task<Guid> SaveAgentAsync(
        AgentEditorModel model,
        CancellationToken cancellationToken = default);
}
```

The first stage may still return selected AgentFramework types if a full DTO conversion would create too much risk. The key goal is to reduce scattered direct calls and centralize error/adoption/recovery behavior.

### SB06 Movement Cutline

Move dispatcher calls behind `IProcessAutomationExecutionClient` where the current dispatcher directly:

- starts automation execution with `ExecuteRunAsync`;
- reads execution details with `GetExecutionRunDetailAsync`;
- lists execution runs with `ListExecutionRunsAsync` for adoption, recovery, carry-forward proof, cost sync, grounding, and competing-run checks;
- reads or updates AgentFramework agent/provider records during process-owned provider recovery.

The facade implementation may remain in `CanDoItAll.Modules.Processes` for this bundle and may delegate directly to `IAgentFrameworkWorkspaceService`. That delegation is the boundary; it is not a final `Processes.Core` contract.

### Explicit Out Of Scope For Stage 1

The following usages remain in the dispatcher or module after SB06 unless a later gate reopens them:

- `ExecutionRunDetail`, `ToolExecutionReceiptRecord`, and artifact/receipt interpretation in validation and projection code;
- finalizer parsing, structured-output contract construction, and process-specific completion logic;
- manager chat, observation services, recovery worker, UI run-detail loaders, and process runtime tool providers outside the dispatcher execution path;
- EF entities, Razor models, process driver packs, or public process tool names.

### Registration Rule

Register the facade in `ProcessesModuleServiceCollectionExtensions` beside `IProcessRunAutomationDispatchService`. The dispatcher should depend on `IProcessAutomationExecutionClient`; the facade implementation is the only process-dispatch source that should directly depend on `IAgentFrameworkWorkspaceService` for the moved calls after SB06.

## Stage 2: Minimal Process Contracts

Add a small `CanDoItAll.Processes.Contracts` or `CanDoItAll.Processes.Abstractions` project only for stable identity/metadata/request objects that are not EF/UI-bound.

Allowed examples:

- `ProcessExecutionIdentity`
- `ProcessAutomationSourceContext`
- `ProcessAutomationExecutionPolicySnapshot`
- `ProcessAutomationReceiptSummary`

Do not move large view models or EF entities.

## Stage 3: Later Bundle

A later bundle can convert the facade from AgentFramework DTO pass-through to process-facing DTOs and then move pure policies into `Processes.Core`.
