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
                logger.LogError(exception, "The LLM Chat dispatcher pass failed.");
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
