using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public interface IAgentFrameworkWorkspaceActivityExecutionService
{
    Task<ExecutionRunResult> ExecuteRunWithinOperationAsync(
        IAgentExecutionActivityOperationLease operation,
        ExecutionRunRequest request,
        CancellationToken cancellationToken = default);

    Task<ExecutionRunSourceExecutionResult> ExecuteSameSourceRunWithinOperationAsync(
        IAgentExecutionActivityOperationLease operation,
        ExecutionRunSourceKey source,
        ExecutionRunRequest request,
        CancellationToken cancellationToken = default);

    Task<ExecutionRunResult> ContinueExecutionRunWithinOperationAsync(
        IAgentExecutionActivityOperationLease operation,
        Guid executionRunId,
        IReadOnlyList<PendingToolApprovalDecision> decisions,
        bool autoApprovePendingToolCalls = false,
        CancellationToken cancellationToken = default);

    Task<AgentToolRunCancellationReconciliation> ReconcileCancelledExecutionRunWithinOperationAsync(
        IAgentExecutionActivityOperationLease operation, Guid executionRunId, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("This workspace does not support durable cancelled-run reconciliation.");

    Task<ExecutionRunResult> RecoverExecutionRunWithinOperationAsync(
        IAgentExecutionActivityOperationLease operation,
        Guid executionRunId,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException("This workspace does not support activity-bound admitted-run recovery.");

    Task<AgentChatRunResult> SendMessageWithinOperationAsync(
        IAgentExecutionActivityOperationLease operation,
        Guid agentId,
        Guid? chatSessionId,
        string prompt,
        AgentChatRunOptions options,
        CancellationToken cancellationToken = default,
        IReadOnlyList<string>? attachmentPaths = null);

    Task<AgentChatRunResult> RespondToPendingApprovalsWithinOperationAsync(
        IAgentExecutionActivityOperationLease operation,
        Guid agentId,
        Guid chatSessionId,
        IReadOnlyList<PendingToolApprovalDecision> decisions,
        bool autoApprovePendingToolCalls = false,
        CancellationToken cancellationToken = default);
}
