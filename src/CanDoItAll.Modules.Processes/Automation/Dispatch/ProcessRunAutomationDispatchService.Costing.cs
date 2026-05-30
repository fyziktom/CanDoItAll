using CanDoItAll.AgentFramework.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.Processes;

internal sealed partial class ProcessRunAutomationDispatchService
{
    private const int ProcessRunCostExecutionRunLimit = 1_000;

    private async Task TrySyncProcessRunActualCostAsync(
        Guid processRunId,
        CancellationToken cancellationToken)
    {
        try
        {
            await SyncProcessRunActualCostAsync(processRunId, cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Failed to synchronize actual process run cost for process run {ProcessRunId}.",
                processRunId);
        }
    }

    private async Task SyncProcessRunActualCostAsync(
        Guid processRunId,
        CancellationToken cancellationToken)
    {
        var providers = await workspaceService.ListProvidersAsync(cancellationToken);
        var executionRuns = await workspaceService.ListExecutionRunsAsync(
            new ExecutionRunQuery(
                Take: ProcessRunCostExecutionRunLimit,
                ProcessRunId: processRunId.ToString("D")),
            cancellationToken);
        if (executionRuns.Count == 0)
        {
            return;
        }

        var metricCosts = new Dictionary<Guid, decimal>();
        foreach (var executionRun in executionRuns)
        {
            var detail = await workspaceService.GetExecutionRunDetailAsync(executionRun.Id, cancellationToken);
            foreach (var metric in detail.Metrics)
            {
                if (metricCosts.ContainsKey(metric.Id))
                {
                    continue;
                }

                if (ProviderPricingCalculator.TryResolveMetricCost(metric, providers, out var costUsd))
                {
                    metricCosts[metric.Id] = costUsd;
                }
            }
        }

        if (metricCosts.Count == 0)
        {
            return;
        }

        var actualCost = decimal.Round(metricCosts.Values.Sum(), 6, MidpointRounding.AwayFromZero);
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var processRun = await dbContext.Set<ProcessRun>()
            .FirstOrDefaultAsync(item => item.Id == processRunId, cancellationToken);
        if (processRun is null || processRun.ActualCost == actualCost)
        {
            return;
        }

        processRun.ActualCost = actualCost;
        processRun.UpdatedAtUtc = clock.GetUtcNow();
        processRun.ConcurrencyToken = Guid.NewGuid();
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
