using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

internal sealed class WorkflowExecutorInvocationLeaseHeartbeatSession : IAsyncDisposable
{
    private readonly Lock gate = new();
    private readonly IWorkflowExecutorInvocationDeduplicationStore store;
    private readonly TimeProvider timeProvider;
    private readonly TimeSpan leaseDuration;
    private readonly CancellationTokenSource stopSource;
    private readonly CancellationTokenSource leaseLostSource = new();
    private readonly Task loop;
    private WorkflowExecutorInvocationClaim claim;
    private Exception? failure;
    private bool disposed;

    public WorkflowExecutorInvocationLeaseHeartbeatSession(
        IWorkflowExecutorInvocationDeduplicationStore store,
        TimeProvider timeProvider,
        TimeSpan leaseDuration,
        TimeSpan renewalInterval,
        WorkflowExecutorInvocationClaim claim)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(claim);
        if (leaseDuration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(leaseDuration));
        }

        if (renewalInterval <= TimeSpan.Zero || renewalInterval >= leaseDuration)
        {
            throw new ArgumentOutOfRangeException(
                nameof(renewalInterval),
                "The invocation renewal interval must be positive and shorter than its lease duration.");
        }

        this.store = store;
        this.timeProvider = timeProvider;
        this.leaseDuration = leaseDuration;
        this.claim = claim;
        stopSource = new CancellationTokenSource();
        loop = RenewAsync(renewalInterval);
    }

    public CancellationToken LeaseLostToken => leaseLostSource.Token;

    public WorkflowExecutorInvocationClaim CurrentClaim
    {
        get
        {
            lock (gate)
            {
                return claim;
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
                WorkflowExecutorInvocationClaim current;
                lock (gate)
                {
                    current = claim;
                }

                var now = timeProvider.GetUtcNow();
                var renewal = await store.TryRenewLeaseAsync(
                    new WorkflowExecutorInvocationLeaseRenewalRequest(
                        current.Identity.Key,
                        current.ConcurrencyVersion,
                        current.Lease.OwnerId,
                        current.Lease.Epoch,
                        now,
                        now + leaseDuration),
                    CancellationToken.None);
                if (!renewal.Succeeded || renewal.Record is not { } record)
                {
                    throw new InvalidOperationException(
                        $"Workflow executor invocation '{current.Identity.Key}' lease renewal failed with outcome '{renewal.Outcome}'.");
                }

                var renewedLease = record.Lease;
                if (record.State != WorkflowExecutorInvocationState.Claimed ||
                    record.Identity != current.Identity ||
                    renewedLease is null ||
                    renewedLease.OwnerId != current.Lease.OwnerId ||
                    renewedLease.Epoch != current.Lease.Epoch)
                {
                    throw new InvalidOperationException(
                        $"Workflow executor invocation '{current.Identity.Key}' lease renewal returned an inconsistent claim.");
                }

                lock (gate)
                {
                    claim = new WorkflowExecutorInvocationClaim(
                        record.Identity,
                        renewedLease,
                        record.Attempt,
                        record.ConcurrencyVersion);
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
