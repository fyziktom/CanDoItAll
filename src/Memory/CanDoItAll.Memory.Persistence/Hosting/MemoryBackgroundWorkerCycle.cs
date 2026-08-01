using CanDoItAll.Memory.Application;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Memory.Persistence.Hosting;

public interface IMemoryBackgroundWorkerCycle
{
    Task RunAsync(CancellationToken cancellationToken = default);
}

public sealed class MemoryBackgroundWorkerCycle(
    IMemoryAsyncOperationWorker operationWorker,
    IMemoryFeedbackWorker feedbackWorker,
    IMemoryProviderEventWorker eventWorker,
    IMemoryRetentionWorker retentionWorker,
    IMemoryWorkerLeaseRunner leaseRunner,
    ILoggerFactory loggerFactory) : IMemoryBackgroundWorkerCycle
{
    private readonly ILogger<MemoryBackgroundWorkerCycle> logger =
        loggerFactory.CreateLogger<MemoryBackgroundWorkerCycle>();

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        await RunPhaseAsync(
            MemoryBackgroundWorkerPhase.OperationPolling,
            operationWorker.PollOperationsAsync,
            cancellationToken);
        await RunPhaseAsync(
            MemoryBackgroundWorkerPhase.FeedbackDelivery,
            feedbackWorker.DeliverPendingFeedbackAsync,
            cancellationToken);
        await RunPhaseAsync(
            MemoryBackgroundWorkerPhase.ProviderEventPolling,
            eventWorker.PollProviderEventsAsync,
            cancellationToken);
        await RunPhaseAsync(
            MemoryBackgroundWorkerPhase.ProviderEventInbox,
            eventWorker.DrainInboxAsync,
            cancellationToken);
        await RunPhaseAsync(
            MemoryBackgroundWorkerPhase.ProviderEventOutbox,
            eventWorker.DrainOutboxAsync,
            cancellationToken);
        await RunPhaseAsync(
            MemoryBackgroundWorkerPhase.Retention,
            retentionWorker.ApplyDueRetentionAsync,
            cancellationToken);
    }

    private async Task RunPhaseAsync(
        MemoryBackgroundWorkerPhase phase,
        Func<CancellationToken, Task<MemoryAsyncWorkerRunResult>> execute,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var execution = await leaseRunner.RunAsync(phase, execute, cancellationToken);
            if (!execution.Acquired)
            {
                logger.LogDebug(
                    "Memory background phase {Phase} was skipped because another host owns its distributed lease.",
                    phase);
                return;
            }

            LogResult(
                phase,
                execution.WorkerResult ?? throw new InvalidOperationException(
                    $"Memory background phase '{phase}' completed without a worker result."));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            logger.LogDebug(
                "Memory background phase {Phase} stopped because host cancellation was requested.",
                phase);
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Memory background phase {Phase} failed. Remaining phases in the cycle will continue.",
                phase);
        }
    }

    private void LogResult(
        MemoryBackgroundWorkerPhase phase,
        MemoryAsyncWorkerRunResult result)
    {
        var logLevel = result.Retried > 0 ||
                       result.DeadLettered > 0 ||
                       result.TimedOut > 0 ||
                       result.LoopRejected > 0
            ? LogLevel.Warning
            : LogLevel.Debug;

        logger.Log(
            logLevel,
            "Memory background phase {Phase} completed. Scanned={Scanned}, Completed={Completed}, Retried={Retried}, DeadLettered={DeadLettered}, TimedOut={TimedOut}, Cancelled={Cancelled}, Enqueued={Enqueued}, Duplicates={Duplicates}, LoopRejected={LoopRejected}, IpfsUnpinRequests={IpfsUnpinRequests}, DiagnosticCount={DiagnosticCount}.",
            phase,
            result.Scanned,
            result.Completed,
            result.Retried,
            result.DeadLettered,
            result.TimedOut,
            result.Cancelled,
            result.Enqueued,
            result.Duplicates,
            result.LoopRejected,
            result.IpfsUnpinRequests,
            result.Diagnostics.Count);
    }
}
