using CanDoItAll.Memory.Application;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Memory.Persistence.Hosting;

public sealed class MemoryWorkerLeaseRunner(
    IMemoryWorkerLeaseStore leaseStore,
    MemoryWorkerLeaseOwnerId ownerId,
    MemoryWorkerHostingOptions options,
    TimeProvider timeProvider,
    ILoggerFactory loggerFactory) : IMemoryWorkerLeaseRunner
{
    private readonly ILogger<MemoryWorkerLeaseRunner> logger =
        loggerFactory.CreateLogger<MemoryWorkerLeaseRunner>();

    public async Task<MemoryWorkerLeaseExecution> RunAsync(
        MemoryBackgroundWorkerPhase phase,
        Func<CancellationToken, Task<MemoryAsyncWorkerRunResult>> execute,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(execute);
        var lease = await leaseStore.TryAcquireAsync(
            phase,
            ownerId,
            timeProvider.GetUtcNow(),
            options.LeaseDuration,
            cancellationToken);
        if (lease is null)
        {
            return MemoryWorkerLeaseExecution.NotAcquired;
        }

        using var executionCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var leaseLost = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var renewal = RenewUntilStoppedAsync(lease, executionCancellation, leaseLost);
        var completed = false;
        try
        {
            var execution = execute(executionCancellation.Token);
            var winner = await Task.WhenAny(execution, leaseLost.Task);
            if (winner == leaseLost.Task)
            {
                executionCancellation.Cancel();
                await ObserveCancelledExecutionAsync(execution, executionCancellation.Token);
                await leaseLost.Task;
            }

            var result = await execution;
            executionCancellation.Cancel();
            await renewal;
            if (leaseLost.Task.IsCompleted)
            {
                await leaseLost.Task;
            }

            completed = await leaseStore.CompleteAsync(
                lease,
                timeProvider.GetUtcNow(),
                cancellationToken);
            if (!completed)
            {
                throw new MemoryWorkerLeaseLostException(phase);
            }

            return MemoryWorkerLeaseExecution.Completed(result);
        }
        finally
        {
            executionCancellation.Cancel();
            await renewal;
            if (!completed)
            {
                await ReleaseAfterFailureAsync(lease);
            }
        }
    }

    private async Task RenewUntilStoppedAsync(
        MemoryWorkerLease lease,
        CancellationTokenSource executionCancellation,
        TaskCompletionSource leaseLost)
    {
        using var timer = new PeriodicTimer(options.LeaseRenewalInterval, timeProvider);
        try
        {
            while (await timer.WaitForNextTickAsync(executionCancellation.Token))
            {
                var renewed = await leaseStore.RenewAsync(
                    lease,
                    timeProvider.GetUtcNow(),
                    options.LeaseDuration,
                    executionCancellation.Token);
                if (renewed)
                {
                    continue;
                }

                leaseLost.TrySetException(new MemoryWorkerLeaseLostException(lease.Phase));
                executionCancellation.Cancel();
                return;
            }
        }
        catch (OperationCanceledException) when (executionCancellation.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            leaseLost.TrySetException(exception);
            executionCancellation.Cancel();
        }
    }

    private static async Task ObserveCancelledExecutionAsync(
        Task execution,
        CancellationToken cancellationToken)
    {
        try
        {
            await execution;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private async Task ReleaseAfterFailureAsync(MemoryWorkerLease lease)
    {
        try
        {
            await leaseStore.ReleaseAsync(
                lease,
                timeProvider.GetUtcNow(),
                CancellationToken.None);
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Memory worker phase {Phase} failed and its lease could not be released. Lease expiry will recover ownership.",
                lease.Phase);
        }
    }
}
