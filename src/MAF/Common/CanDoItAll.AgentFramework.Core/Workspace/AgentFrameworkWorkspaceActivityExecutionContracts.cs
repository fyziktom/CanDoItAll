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
        bool approved,
        bool autoApprovePendingToolCalls = false,
        CancellationToken cancellationToken = default);

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
        bool approved,
        bool autoApprovePendingToolCalls = false,
        CancellationToken cancellationToken = default);
}
