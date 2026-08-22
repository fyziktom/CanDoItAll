using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;

namespace CanDoItAll.AgentFramework.Core;

public sealed class WorkflowExternalResponseLeaseHeartbeat
{
    private readonly IWorkflowExternalResponseOperationStore operationStore;
    private readonly TimeProvider timeProvider;
    private readonly TimeSpan leaseDuration;
    private readonly TimeSpan renewalInterval;

    public WorkflowExternalResponseLeaseHeartbeat(
        IWorkflowExternalResponseOperationStore operationStore,
        TimeProvider timeProvider,
        TimeSpan leaseDuration,
        TimeSpan renewalInterval)
    {
        ArgumentNullException.ThrowIfNull(operationStore);
        ArgumentNullException.ThrowIfNull(timeProvider);
        if (leaseDuration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(leaseDuration));
        }

        if (renewalInterval <= TimeSpan.Zero || renewalInterval >= leaseDuration)
        {
            throw new ArgumentOutOfRangeException(
                nameof(renewalInterval),
                "The response-operation renewal interval must be positive and shorter than its lease duration.");
        }

        this.operationStore = operationStore;
        this.timeProvider = timeProvider;
        this.leaseDuration = leaseDuration;
        this.renewalInterval = renewalInterval;
    }

    public WorkflowExternalResponseLeaseHeartbeatSession Start(
        WorkflowExternalResponseOperationRecord operation,
        WorkflowExternalResponseLeaseOwnerId ownerId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (operation.Lease is not { } lease || lease.OwnerId != ownerId)
        {
            throw new InvalidOperationException(
                $"Workflow external response operation '{operation.Id}' does not hold the requested lease.");
        }

        return new WorkflowExternalResponseLeaseHeartbeatSession(
            operationStore,
            timeProvider,
            leaseDuration,
            renewalInterval,
            operation,
            ownerId,
            cancellationToken);
    }
}
