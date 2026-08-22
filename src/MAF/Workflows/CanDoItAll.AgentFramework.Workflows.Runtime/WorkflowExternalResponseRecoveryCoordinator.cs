using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;

namespace CanDoItAll.AgentFramework.Core;

public sealed class WorkflowExternalResponseRecoveryCoordinator
{
    public const int DefaultMaximumCount = 32;
    public const int AbsoluteMaximumCount = 128;

    private readonly IWorkflowExternalResponseOperationStore operationStore;
    private readonly IWorkflowExternalResponseContinuation continuation;
    private readonly TimeProvider timeProvider;
    private readonly WorkflowExternalResponseLeaseOwnerId leaseOwnerId = new(
        $"{Environment.MachineName}:{Environment.ProcessId}:recovery:{Guid.NewGuid():N}");

    public WorkflowExternalResponseRecoveryCoordinator(
        IWorkflowExternalResponseOperationStore operationStore,
        IWorkflowExternalResponseContinuation continuation,
        TimeProvider timeProvider)
    {
        this.operationStore = operationStore ?? throw new ArgumentNullException(nameof(operationStore));
        this.continuation = continuation ?? throw new ArgumentNullException(nameof(continuation));
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task<IReadOnlyList<WorkflowExternalResponseContinuationResult>> RecoverAsync(
        int maximumCount = DefaultMaximumCount,
        CancellationToken cancellationToken = default)
    {
        if (maximumCount is < 1 or > AbsoluteMaximumCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumCount),
                maximumCount,
                $"Workflow response recovery batch size must be between 1 and {AbsoluteMaximumCount}.");
        }

        var operations = await operationStore.ListRecoverableAsync(
            timeProvider.GetUtcNow(),
            maximumCount,
            cancellationToken);
        var results = new List<WorkflowExternalResponseContinuationResult>(
            Math.Min(operations.Count, maximumCount));
        foreach (var operation in operations.Take(maximumCount))
        {
            cancellationToken.ThrowIfCancellationRequested();
            results.Add(await continuation.ContinueAsync(
                new WorkflowExternalResponseContinuationRequest(
                    operation.Id,
                    leaseOwnerId),
                cancellationToken));
        }

        return results;
    }
}
