using CanDoItAll.Infrastructure.BackgroundJobs;
using CanDoItAll.Modules.Workspace;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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
    IOptions<AutomationRuntimeOptions> options) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var dispatcher = scope.ServiceProvider.GetRequiredService<IAutomationMessageDispatcher>();
            var processedCount = await dispatcher.DispatchPendingAsync(
                options.Value.MessageDispatchBatchSize,
                stoppingToken);

            if (processedCount == 0)
            {
                await Task.Delay(options.Value.MessagePollInterval, stoppingToken);
            }
        }
    }
}

public sealed class ConnectorOutboxDrainWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<AutomationRuntimeOptions> options) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var outbox = scope.ServiceProvider.GetRequiredService<ConnectorOutboxService>();
            var processedCount = await outbox.ProcessPendingAsync(
                options.Value.ConnectorOutboxBatchSize,
                stoppingToken);

            if (processedCount == 0)
            {
                await Task.Delay(options.Value.ConnectorOutboxPollInterval, stoppingToken);
            }
        }
    }
}

public sealed class LegacyBackgroundJobQueueBridgeWorker(
    IBackgroundJobQueue backgroundJobQueue,
    IServiceScopeFactory scopeFactory,
    IOptions<AutomationRuntimeOptions> options) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var request = await backgroundJobQueue.DequeueAsync(stoppingToken);

            await using var scope = scopeFactory.CreateAsyncScope();
            var telemetryPublisher = scope.ServiceProvider.GetRequiredService<IAutomationTelemetryPublisher>();
            await telemetryPublisher.PublishAsync(new AutomationTelemetryEvent(
                AutomationExecutionLogKind.BackgroundJobQueued,
                "legacy-background-job-queue",
                request.CorrelationId.ToString("N"),
                request.CorrelationId,
                null,
                $"Observed legacy background job queue item '{request.JobType}'.",
                $$"""
                  {
                    "jobType":"{{EscapeJson(request.JobType)}}",
                    "description":"{{EscapeJson(request.Description)}}"
                  }
                  """), stoppingToken);

            await Task.Delay(options.Value.LegacyBackgroundQueuePollInterval, stoppingToken);
        }
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
