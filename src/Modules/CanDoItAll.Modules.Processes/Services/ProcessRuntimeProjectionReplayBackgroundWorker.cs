using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Projections;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.Processes;

internal sealed class ProcessRuntimeProjectionReplayBackgroundWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<ProcessRuntimeProjectionReplayBackgroundWorker> logger) : BackgroundService
{
    private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan DrainDelay = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan IdleDelay = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan FailureDelay = TimeSpan.FromSeconds(15);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!await DelayAsync(StartupDelay, stoppingToken).ConfigureAwait(false))
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            var nextDelay = await ReplayNextBatchAsync(stoppingToken).ConfigureAwait(false);
            if (!await DelayAsync(nextDelay, stoppingToken).ConfigureAwait(false))
            {
                break;
            }
        }
    }

    private async Task<TimeSpan> ReplayNextBatchAsync(CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var catchupService = scope.ServiceProvider.GetRequiredService<ProcessRuntimeProjectionCatchupService>();
            var result = await catchupService.CatchUpAsync(stoppingToken).ConfigureAwait(false);
            if (result.Status == ProcessProjectionReplayStatus.DeadLettered)
            {
                logger.LogError(
                    "Process projection replay stopped at global sequence {GlobalSequence} after processing {ProcessedCount} event(s). Backlog={BacklogEventCount}. Inspect the projection dead-letter store before replay can advance.",
                    result.LastProcessedGlobalSequence,
                    result.ProcessedCount,
                    result.BacklogEventCount);
                return FailureDelay;
            }

            if (result.ProcessedCount == 0)
            {
                return IdleDelay;
            }

            logger.LogDebug(
                "Process projection replay processed {ProcessedCount} event(s) through global sequence {GlobalSequence}. Backlog={BacklogEventCount}.",
                result.ProcessedCount,
                result.LastProcessedGlobalSequence,
                result.BacklogEventCount);
            return DrainDelay;
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return TimeSpan.Zero;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Process projection background replay failed.");
            return FailureDelay;
        }
    }

    private static async Task<bool> DelayAsync(
        TimeSpan delay,
        CancellationToken cancellationToken)
    {
        if (delay <= TimeSpan.Zero)
        {
            return !cancellationToken.IsCancellationRequested;
        }

        try
        {
            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return false;
        }
    }
}
