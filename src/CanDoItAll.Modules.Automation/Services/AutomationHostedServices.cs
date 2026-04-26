using CanDoItAll.Infrastructure.BackgroundJobs;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Workspace;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Modules.Automation;

public sealed class AutomationSchedulerProjectionHostedService(
    IServiceScopeFactory scopeFactory) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var bridge = scope.ServiceProvider.GetRequiredService<QuartzAutomationSchedulerBridge>();
        await bridge.SynchronizeAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}

public sealed class AutomationMessagePumpWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<AutomationRuntimeOptions> options,
    ILogger<AutomationMessagePumpWorker> logger) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return HostedWorkerLoop.RunAsync(
            nameof(AutomationMessagePumpWorker),
            options.Value.MessagePollInterval,
            options.Value.WorkerFailureBackoff,
            scopeFactory,
            logger,
            async (provider, cancellationToken) =>
            {
                var dispatcher = provider.GetRequiredService<IAutomationMessageDispatcher>();
                return await dispatcher.DispatchPendingAsync(
                    options.Value.MessageDispatchBatchSize,
                    cancellationToken);
            },
            stoppingToken);
    }
}

public sealed class ConnectorOutboxDrainWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<AutomationRuntimeOptions> options,
    ILogger<ConnectorOutboxDrainWorker> logger) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return HostedWorkerLoop.RunAsync(
            nameof(ConnectorOutboxDrainWorker),
            options.Value.ConnectorOutboxPollInterval,
            options.Value.WorkerFailureBackoff,
            scopeFactory,
            logger,
            async (provider, cancellationToken) =>
            {
                var outbox = provider.GetRequiredService<ConnectorOutboxService>();
                return await outbox.ProcessPendingAsync(
                    options.Value.ConnectorOutboxBatchSize,
                    options.Value.ConnectorCommandLeaseDuration,
                    cancellationToken);
            },
            stoppingToken);
    }
}

public sealed class LegacyBackgroundJobQueueBridgeWorker(
    IBackgroundJobQueue backgroundJobQueue,
    IServiceScopeFactory scopeFactory,
    IOptions<AutomationRuntimeOptions> options,
    ILogger<LegacyBackgroundJobQueueBridgeWorker> logger) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return HostedWorkerLoop.RunAsync(
            nameof(LegacyBackgroundJobQueueBridgeWorker),
            TimeSpan.Zero,
            options.Value.WorkerFailureBackoff,
            scopeFactory,
            logger,
            async (provider, cancellationToken) =>
            {
                var request = await backgroundJobQueue.DequeueAsync(cancellationToken);
                var scheduler = provider.GetRequiredService<IAutomationBackgroundJobScheduler>();
                var telemetryPublisher = provider.GetRequiredService<IAutomationTelemetryPublisher>();
                var backgroundJobId = await scheduler.ScheduleAsync(
                    request.JobType,
                    request.Description,
                    request.Metadata,
                    request.CorrelationId,
                    cancellationToken);

                await telemetryPublisher.PublishAsync(new AutomationTelemetryEvent(
                    AutomationExecutionLogKind.BackgroundJobQueued,
                    "legacy-background-job-queue",
                    request.CorrelationId.ToString("N"),
                    request.CorrelationId,
                    null,
                    $"Forwarded legacy background job queue item '{request.JobType}' into the durable runtime plane.",
                    $$"""
                      {
                        "jobType":"{{EscapeJson(request.JobType)}}",
                        "description":"{{EscapeJson(request.Description)}}",
                        "backgroundJobId":"{{backgroundJobId:N}}"
                      }
                      """),
                    cancellationToken);

                if (options.Value.LegacyBackgroundQueuePollInterval > TimeSpan.Zero)
                {
                    await Task.Delay(options.Value.LegacyBackgroundQueuePollInterval, cancellationToken);
                }

                return 1;
            },
            stoppingToken);
    }

    private static string EscapeJson(string? value)
    {
        return (value ?? string.Empty)
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal);
    }
}

internal static class HostedWorkerLoop
{
    public static async Task RunAsync(
        string workerName,
        TimeSpan idleDelay,
        TimeSpan failureBackoff,
        IServiceScopeFactory scopeFactory,
        ILogger logger,
        Func<IServiceProvider, CancellationToken, Task<int>> processAsync,
        CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var processedCount = await processAsync(scope.ServiceProvider, stoppingToken);
                if (processedCount == 0 && idleDelay > TimeSpan.Zero)
                {
                    await Task.Delay(idleDelay, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                if (SqliteWriteCoordination.IsBusy(ex))
                {
                    logger.LogWarning(
                        ex,
                        "{WorkerName} hit transient SQLite contention. The worker will retry after {FailureBackoff}.",
                        workerName,
                        failureBackoff);

                    if (failureBackoff > TimeSpan.Zero)
                    {
                        await Task.Delay(failureBackoff, stoppingToken);
                    }

                    continue;
                }

                logger.LogError(
                    ex,
                    "{WorkerName} iteration failed. The worker will retry after {FailureBackoff}.",
                    workerName,
                    failureBackoff);

                if (failureBackoff > TimeSpan.Zero)
                {
                    await Task.Delay(failureBackoff, stoppingToken);
                }
            }
        }
    }
}
