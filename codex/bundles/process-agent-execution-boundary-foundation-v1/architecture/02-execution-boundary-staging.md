# Execution Boundary Staging

## Stage 1: Facade Inside Processes

Introduce a process-owned execution client/facade that wraps the existing `IAgentFrameworkWorkspaceService` calls.

Allowed initial shape:

```csharp
internal interface IProcessAutomationExecutionClient
{
    Task<ProcessAutomationExecutionStartResult> StartAsync(...);
    Task<ExecutionRunDetail> GetDetailAsync(...);
    Task<ConcurrentAutomationExecution?> TryAdoptConcurrentAsync(...);
}
```

The first stage may still return selected AgentFramework types if a full DTO conversion would create too much risk. The key goal is to reduce scattered direct calls and centralize error/adoption/recovery behavior.

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
