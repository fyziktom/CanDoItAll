using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;

namespace CanDoItAll.AgentFramework.Core;

public sealed class WorkflowExternalResponseLeaseHeartbeatSession : IAsyncDisposable
{
    private readonly Lock gate = new();
    private readonly IWorkflowExternalResponseOperationStore operationStore;
    private readonly TimeProvider timeProvider;
    private readonly TimeSpan leaseDuration;
    private readonly WorkflowExternalResponseLeaseOwnerId ownerId;
    private readonly CancellationTokenSource stopSource;
    private readonly CancellationTokenSource leaseLostSource = new();
    private readonly Task loop;
    private WorkflowExternalResponseOperationRecord operation;
    private Exception? failure;
    private bool disposed;

    internal WorkflowExternalResponseLeaseHeartbeatSession(
        IWorkflowExternalResponseOperationStore operationStore,
        TimeProvider timeProvider,
        TimeSpan leaseDuration,
        TimeSpan renewalInterval,
        WorkflowExternalResponseOperationRecord operation,
        WorkflowExternalResponseLeaseOwnerId ownerId,
        CancellationToken cancellationToken)
    {
        this.operationStore = operationStore;
        this.timeProvider = timeProvider;
        this.leaseDuration = leaseDuration;
        this.operation = operation;
        this.ownerId = ownerId;
        stopSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        loop = RenewAsync(renewalInterval);
    }

    public CancellationToken LeaseLostToken => leaseLostSource.Token;

    public WorkflowExternalResponseOperationRecord CurrentOperation
    {
        get
        {
            lock (gate)
            {
                return operation;
            }
        }
    }

    public Exception? Failure
    {
        get
        {
            lock (gate)
            {
                return failure;
            }
        }
    }

    public async ValueTask StopAsync()
    {
        if (!stopSource.IsCancellationRequested)
        {
            await stopSource.CancelAsync();
        }

        try
        {
            await loop;
        }
        catch (OperationCanceledException) when (stopSource.IsCancellationRequested)
        {
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        await StopAsync();
        stopSource.Dispose();
        leaseLostSource.Dispose();
    }

    private async Task RenewAsync(TimeSpan renewalInterval)
    {
        using var timer = new PeriodicTimer(renewalInterval, timeProvider);
        try
        {
            while (await timer.WaitForNextTickAsync(stopSource.Token))
            {
                WorkflowExternalResponseOperationRecord current;
                lock (gate)
                {
                    current = operation;
                }

                var lease = current.Lease
                    ?? throw new InvalidOperationException(
                        $"Workflow external response operation '{current.Id}' lost its persisted lease.");
                var now = timeProvider.GetUtcNow();
                var renewal = await operationStore.TryRenewLeaseAsync(
                    new WorkflowExternalResponseOperationLeaseRenewalRequest(
                        current.Id,
                        current.ConcurrencyVersion,
                        ownerId,
                        lease.Epoch,
                        now,
                        now.Add(leaseDuration)),
                    stopSource.Token);
                if (!renewal.Succeeded || renewal.Operation is null)
                {
                    throw new InvalidOperationException(
                        $"Workflow external response operation '{current.Id}' lease renewal failed with outcome '{renewal.Outcome}'.");
                }

                lock (gate)
                {
                    operation = renewal.Operation;
                }
            }
        }
        catch (OperationCanceledException) when (stopSource.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            lock (gate)
            {
                failure = exception;
            }

            await leaseLostSource.CancelAsync();
        }
    }
}
