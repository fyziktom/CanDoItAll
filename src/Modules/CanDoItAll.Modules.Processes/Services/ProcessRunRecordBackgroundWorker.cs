using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Modules.Processes;

internal sealed class ProcessRunRecordBackgroundWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<ProcessRunRecordProcessingOptions> options,
    ILogger<ProcessRunRecordBackgroundWorker> logger) : BackgroundService
{
    private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan DrainDelay = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan FailureDelay = TimeSpan.FromSeconds(15);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled ||
            !await DelayAsync(StartupDelay, stoppingToken).ConfigureAwait(false))
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            var nextDelay = await ProcessNextBatchAsync(stoppingToken).ConfigureAwait(false);
            if (!await DelayAsync(nextDelay, stoppingToken).ConfigureAwait(false))
            {
                break;
            }
        }
    }

    private async Task<TimeSpan> ProcessNextBatchAsync(CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var processor = scope.ServiceProvider.GetRequiredService<ProcessRunRecordBatchProcessor>();
            var result = await processor.ProcessNextBatchAsync(stoppingToken).ConfigureAwait(false);
            if (result.ProcessedCount > 0)
            {
                logger.LogInformation(
                    "Processed process run records. Backfilled={BackfilledCount} FactsCompleted={FactsCompletedCount} NarrativesCompleted={NarrativesCompletedCount}.",
                    result.BackfilledCount,
                    result.FactsCompletedCount,
                    result.NarrativesCompletedCount);
                return DrainDelay;
            }

            return options.Value.PollInterval;
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return TimeSpan.Zero;
        }
        catch (Exception exception)
        {
            logger.LogError(
                "Process run record background processing failed. ErrorClass={ErrorClass}.",
                exception.GetType().Name);
            return FailureDelay;
        }
    }

    private static async Task<bool> DelayAsync(
        TimeSpan delay,
        CancellationToken cancellationToken)
    {
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
