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
        var providers = await executionClient.ListProvidersAsync(cancellationToken);
        var executionRuns = await executionClient.ListExecutionRunsAsync(
            new ExecutionRunQuery(
                Take: ProcessRunCostExecutionRunLimit,
                ProcessRunId: processRunId.ToString("D")),
            cancellationToken);
        if (executionRuns.Count == 0)
        {
            return;
        }

        var executionRunDetails = new List<ExecutionRunDetail>(executionRuns.Count);
        foreach (var executionRun in executionRuns)
        {
            executionRunDetails.Add(await executionClient.GetExecutionRunDetailAsync(executionRun.Id, cancellationToken));
        }

        var actualCost = ResolveProcessRunActualCost(executionRunDetails, providers);
        if (!actualCost.HasValue)
        {
            return;
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var processRun = await dbContext.Set<ProcessRun>()
            .FirstOrDefaultAsync(item => item.Id == processRunId, cancellationToken);
        if (processRun is null || processRun.ActualCost == actualCost.Value)
        {
            return;
        }

        processRun.ActualCost = actualCost.Value;
        processRun.UpdatedAtUtc = clock.GetUtcNow();
        processRun.ConcurrencyToken = Guid.NewGuid();
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    internal static decimal? ResolveProcessRunActualCost(
        IReadOnlyList<ExecutionRunDetail> executionRunDetails,
        IReadOnlyList<ProviderProfile> providers)
    {
        var usageCosts = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        var legacyMetricCosts = new Dictionary<Guid, decimal>();
        foreach (var detail in executionRunDetails)
        {
            if (detail.UsageObservations.Count > 0)
            {
                foreach (var observation in detail.UsageObservations)
                {
                    var usageKey = ResolveUsageCostKey(observation);
                    if (usageCosts.ContainsKey(usageKey))
                    {
                        continue;
                    }

                    if (ProviderPricingCalculator.TryResolveObservationCost(observation, providers, out var costUsd))
                    {
                        usageCosts[usageKey] = costUsd;
                    }
                }

                continue;
            }

            foreach (var metric in detail.Metrics)
            {
                if (legacyMetricCosts.ContainsKey(metric.Id))
                {
                    continue;
                }

                if (ProviderPricingCalculator.TryResolveMetricCost(metric, providers, out var costUsd))
                {
                    legacyMetricCosts[metric.Id] = costUsd;
                }
            }
        }

        if (usageCosts.Count == 0 && legacyMetricCosts.Count == 0)
        {
            return null;
        }

        return decimal.Round(usageCosts.Values.Sum() + legacyMetricCosts.Values.Sum(), 6, MidpointRounding.AwayFromZero);
    }

    private static string ResolveUsageCostKey(ProviderUsageObservation observation)
    {
        if (!string.IsNullOrWhiteSpace(observation.ProviderResponseId))
        {
            return $"{observation.ProviderName}|{observation.Model}|{observation.ProviderResponseId}|{observation.SourcePhase}";
        }

        return observation.Id.ToString("N");
    }
}
