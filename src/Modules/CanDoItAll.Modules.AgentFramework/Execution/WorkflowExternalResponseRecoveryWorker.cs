using CanDoItAll.AgentFramework.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.AgentFramework;

internal sealed class WorkflowExternalResponseRecoveryWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<WorkflowExternalResponseRecoveryWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var coordinator = scope.ServiceProvider
                .GetRequiredService<WorkflowExternalResponseRecoveryCoordinator>();
            var results = await coordinator.RecoverAsync(
                WorkflowExternalResponseRecoveryCoordinator.DefaultMaximumCount,
                stoppingToken);

            logger.LogInformation(
                "Workflow external response startup recovery processed {RecoveryCount} operation(s).",
                results.Count);
            foreach (var outcomeGroup in results.GroupBy(result => result.Outcome))
            {
                logger.LogInformation(
                    "Workflow external response startup recovery outcome {RecoveryOutcome}: {OutcomeCount} operation(s).",
                    outcomeGroup.Key,
                    outcomeGroup.Count());
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            logger.LogError(
                "Workflow external response startup recovery failed with {FailureType}.",
                exception.GetType().Name);
        }
    }
}
