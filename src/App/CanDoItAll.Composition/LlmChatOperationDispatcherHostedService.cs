using CanDoItAll.Modules.LlmChats.Application;
using CanDoItAll.Modules.LlmChats.Common;
using CanDoItAll.Modules.LlmChats.Ports;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Composition;

internal sealed class LlmChatOperationDispatcherHostedService(
    IServiceScopeFactory scopeFactory,
    ILlmChatOperationDispatchSignal dispatchSignal,
    LlmChatExecutionLeaseOptions options,
    ILogger<LlmChatOperationDispatcherHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        options.Validate();
        logger.LogInformation(
            "Starting {WorkerCount} LLM Chat dispatcher worker(s). CandidateBatchSize={CandidateBatchSize} MaximumQueuedAge={MaximumQueuedAge} MaximumOperationDuration={MaximumOperationDuration}.",
            options.WorkerCount,
            options.CandidateBatchSize,
            options.MaximumQueuedAge,
            options.MaximumOperationDuration);
        var workers = Enumerable.Range(1, options.WorkerCount)
            .Select(workerId => RunWorkerAsync(workerId, stoppingToken))
            .ToArray();
        await Task.WhenAll(workers).ConfigureAwait(false);
    }

    private async Task RunWorkerAsync(int workerId, CancellationToken stoppingToken)
    {
        var ownerId = LlmChatExecutionOwnerId.New();
        using var registration = dispatchSignal.RegisterExecutor();
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var dispatcher = scope.ServiceProvider.GetRequiredService<LlmChatOperationDispatcher>();
                if (await dispatcher.DispatchNextAsync(ownerId, stoppingToken).ConfigureAwait(false))
                {
                    continue;
                }

                await dispatchSignal.WaitAsync(options.PollInterval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                var availability = dispatchSignal.Availability;
                logger.LogError(
                    "LLM Chat dispatcher worker {WorkerId} pass failed. FailureType={FailureType} RegisteredWorkers={RegisteredWorkers} ProgressingWorkers={ProgressingWorkers}.",
                    workerId,
                    exception.GetType().FullName,
                    availability.RegisteredWorkers,
                    availability.ProgressingWorkers);
                try
                {
                    await Task.Delay(options.PollInterval, stoppingToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }
    }
}
