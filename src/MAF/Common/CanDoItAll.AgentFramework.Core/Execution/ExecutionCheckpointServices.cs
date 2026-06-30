using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public sealed class NullAgentExecutionCheckpointBridge : IAgentExecutionCheckpointBridge
{
    public Task<ExecutionWorkflowCheckpointRecord?> CapturePendingApprovalCheckpointAsync(
        ExecutionRunRecord run,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<ExecutionWorkflowCheckpointRecord?>(null);
    }

    public Task<ExecutionWorkflowCheckpointRecord?> ValidatePendingApprovalResumeAsync(
        ExecutionRunRecord run,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<ExecutionWorkflowCheckpointRecord?>(null);
    }

    public Task MarkCheckpointResumedAsync(
        Guid executionRunId,
        DateTimeOffset resumedAtUtc,
        CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
