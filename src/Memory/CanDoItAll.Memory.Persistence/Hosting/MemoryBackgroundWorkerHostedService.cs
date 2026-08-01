using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Memory.Persistence.Hosting;

public sealed class MemoryBackgroundWorkerHostedService(
    IServiceScopeFactory scopeFactory,
    MemoryWorkerHostingOptions options,
    TimeProvider timeProvider,
    ILoggerFactory loggerFactory) : BackgroundService
{
    private readonly ILogger<MemoryBackgroundWorkerHostedService> logger =
        loggerFactory.CreateLogger<MemoryBackgroundWorkerHostedService>();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        options.Validate();
        if (!options.Enabled)
        {
            throw new InvalidOperationException(
                "The durable memory background service cannot run while worker hosting is disabled.");
        }

        logger.LogInformation(
            "Durable memory background workers started with cycle interval {CycleInterval}, lease duration {LeaseDuration}, and renewal interval {LeaseRenewalInterval}. Source-capture delivery remains queued-only and is not processed by this hosted service.",
            options.CycleInterval,
            options.LeaseDuration,
            options.LeaseRenewalInterval);

        using var timer = new PeriodicTimer(options.CycleInterval, timeProvider);
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await RunCycleAsync(stoppingToken);
                if (!await timer.WaitForNextTickAsync(stoppingToken))
                {
                    break;
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            logger.LogInformation("Durable memory background workers stopped by host cancellation.");
        }
    }

    private async Task RunCycleAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var cycle = scope.ServiceProvider.GetRequiredService<IMemoryBackgroundWorkerCycle>();
            await cycle.RunAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Durable memory background cycle failed before all scoped phases could be dispatched. The next cycle will retry.");
        }
    }
}
