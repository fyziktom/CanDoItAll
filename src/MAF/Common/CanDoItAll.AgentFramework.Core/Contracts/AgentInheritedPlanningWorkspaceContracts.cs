using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public interface IAgentInheritedPlanningWorkspaceService {
    Task<AgentInheritedPlanningWorkspaceResult> ExecuteInheritedPlanningAsync(AgentInheritedPlanningWorkspaceRequest request,
        CancellationToken cancellationToken = default);
    Task<AgentInheritedPlanningWorkspaceResult> ExecuteInheritedPlanningWithinOperationAsync(IAgentExecutionActivityOperationLease operation,
        AgentInheritedPlanningWorkspaceRequest request, CancellationToken cancellationToken = default);
}

public sealed record AgentInheritedPlanningWorkspaceRequest(Guid ExecutionRunId, Guid AgentId, string OriginalInput,
    AgentExecutionOperationId ActivityOperationId, ExecutionInvocationContext Context, AgentPlanningOwner Owner, WorkspaceScopeDescriptor ContextScope,
    AgentToolProtocolEnvelope ParentBinding,
    Func<ExecutionRunRecord, CancellationToken, Task> RequireOriginalParent,
    Func<AgentInheritedPlanningBindingContext, CancellationToken, Task<AgentInheritedPlanningBinding>> BindAsync);

public sealed record AgentInheritedPlanningWorkspaceResult(ExecutionRunRecord Run, ExecutionRunResult? Execution);
