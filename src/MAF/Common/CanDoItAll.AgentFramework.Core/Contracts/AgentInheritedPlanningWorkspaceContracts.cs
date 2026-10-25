using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public interface IAgentInheritedPlanningWorkspaceService {
    Task<AgentInheritedPlanningWorkspaceResult> ExecuteInheritedPlanningAsync(AgentInheritedPlanningWorkspaceRequest request,
        CancellationToken cancellationToken = default);
    Task<AgentInheritedPlanningWorkspaceResult> ExecuteInheritedPlanningWithinOperationAsync(IAgentExecutionActivityOperationLease operation,
        AgentInheritedPlanningWorkspaceRequest request, CancellationToken cancellationToken = default);
    Task<T> ReviewInheritedPlanningAsync<T>(AgentInheritedPlanningReviewRequest request,
        AgentInheritedSavedEffectContinuation authority, AgentToolSemanticDigest ownerIntent, AgentExecutionOperationId activityOperationId,
        Func<AgentInheritedPlanningReviewResult, CancellationToken, Task<T>> settleOriginal, CancellationToken cancellationToken = default);
    Task<T> ReviewInheritedPlanningWithinOperationAsync<T>(IAgentExecutionActivityOperationLease operation,
        AgentInheritedPlanningReviewRequest request, AgentInheritedSavedEffectContinuation authority, AgentToolSemanticDigest ownerIntent,
        Func<AgentInheritedPlanningReviewResult, CancellationToken, Task<T>> settleOriginal, CancellationToken cancellationToken = default);
}

public sealed record AgentInheritedPlanningWorkspaceRequest(Guid ExecutionRunId, AgentInheritedPlanningStart Start, Guid AgentId, string OriginalInput,
    AgentExecutionOperationId ActivityOperationId, ExecutionInvocationContext Context, AgentPlanningOwner Owner, WorkspaceScopeDescriptor ContextScope,
    AgentToolProtocolEnvelope ParentBinding,
    Func<ExecutionRunRecord, CancellationToken, Task> RequireOriginalParent,
    Func<ExecutionRunRecord, AgentToolProtocolEnvelope> CaptureSourceEvidence);

public sealed record AgentInheritedPlanningWorkspaceResult(ExecutionRunRecord Run, ExecutionRunResult? Execution);
