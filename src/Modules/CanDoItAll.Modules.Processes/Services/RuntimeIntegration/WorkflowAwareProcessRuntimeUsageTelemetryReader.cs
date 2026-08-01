using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Projections;

namespace CanDoItAll.Modules.Processes;

internal sealed class WorkflowAwareProcessRuntimeUsageTelemetryReader(
    AgentFrameworkProcessRuntimeUsageTelemetryReader agentTelemetryReader,
    IWorkflowUsageObservationStore workflowUsageStore) : IProcessRuntimeUsageTelemetryReader
{
    private const int UsageObservationBatchTake = 5_000;

    public async ValueTask<IReadOnlyList<ProcessRuntimeUsageObservation>> ListAsync(
        ProcessRuntimeUsageTelemetryQuery query,
        CancellationToken cancellationToken = default)
        => (await ReadAsync(query, cancellationToken).ConfigureAwait(false)).Items;

    public async ValueTask<ProcessRuntimeUsageTelemetryReadResult> ReadAsync(
        ProcessRuntimeUsageTelemetryQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (query.TakePerRun <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(query),
                query.TakePerRun,
                "Process runtime usage take per run must be greater than zero.");
        }

        if (query.ToUtc < query.FromUtc)
        {
            throw new ArgumentException(
                "Process runtime usage time range must end at or after it starts.",
                nameof(query));
        }

        if (query.RunIds.Count == 0)
        {
            return new ProcessRuntimeUsageTelemetryReadResult([], IsComplete: true);
        }

        var agentRead = await agentTelemetryReader
            .ReadAsync(query, cancellationToken)
            .ConfigureAwait(false);
        var effectiveTake = Math.Min(
            query.TakePerRun,
            UsageObservationBatchTake);
        var distinctRunCount = query.RunIds.Distinct().Count();
        var requestedTake = Math.Min(
            (long)effectiveTake * distinctRunCount + 1,
            UsageObservationBatchTake + 1L);
        var workflowPage = await workflowUsageStore
            .ListPageAsync(
                new WorkflowUsageObservationPageRequest(
                    new WorkflowUsageObservationQuery
                    {
                        OriginProcessRunIds = query.RunIds
                            .Select(runId => new WorkflowProcessRunId(runId.Value))
                            .ToArray(),
                        RecordedFromUtc = query.FromUtc,
                        RecordedToUtc = query.ToUtc
                    },
                    PageSize: (int)requestedTake),
                cancellationToken)
            .ConfigureAwait(false);
        var merged = new Dictionary<Guid, ProcessRuntimeUsageObservation>();
        foreach (var observation in agentRead.Items)
        {
            AddOrValidate(merged, observation);
        }

        foreach (var observation in workflowPage.Items)
        {
            if (observation.Origin is not WorkflowLaunchOrigin.ProcessAssignment processOrigin)
            {
                throw new InvalidOperationException(
                    $"Workflow usage observation '{observation.Id}' was returned for a process-origin query without typed process-assignment origin.");
            }

            AddOrValidate(merged, Map(observation, processOrigin));
        }

        var observationsByRun = merged.Values
            .OrderBy(observation => observation.CreatedAtUtc)
            .ThenBy(observation => observation.UsageObservationId)
            .GroupBy(observation => observation.RunId)
            .Select(group => group.Take(effectiveTake + 1).ToArray())
            .ToArray();
        var isComplete = agentRead.IsComplete &&
            workflowPage.TotalCount == workflowPage.Items.Count &&
            observationsByRun.All(group => group.Length <= effectiveTake);
        return new ProcessRuntimeUsageTelemetryReadResult(
            observationsByRun
                .SelectMany(group => group.Take(effectiveTake))
                .OrderBy(observation => observation.CreatedAtUtc)
                .ThenBy(observation => observation.UsageObservationId)
                .ToArray(),
            isComplete);
    }

    private static ProcessRuntimeUsageObservation Map(
        WorkflowUsageObservation observation,
        WorkflowLaunchOrigin.ProcessAssignment processOrigin)
    {
        WorkflowUsageObservationValidator.ThrowIfInvalid(observation);
        var executionRunId = observation.RunId?.Value ?? throw new InvalidOperationException(
            $"Workflow usage observation '{observation.Id}' is not correlated to a workflow run.");
        var costUsd = observation.PricingStatus == WorkflowPricingStatus.Known
            ? observation.CostUsd ?? throw new InvalidOperationException(
                $"Workflow usage observation '{observation.Id}' has known pricing without cost.")
            : 0m;
        var roundedCostUsd = decimal.Round(costUsd, 6, MidpointRounding.AwayFromZero);
        return new ProcessRuntimeUsageObservation(
            observation.Id.Value,
            executionRunId,
            new ProcessRunId(processOrigin.ProcessRun.Value),
            new ProcessStepInstanceId(processOrigin.Assignment.Value),
            observation.RecordedAtUtc,
            observation.ProviderName,
            observation.Model,
            observation.SourcePhase,
            observation.UsageStatus.ToString(),
            WorkflowUsageCompatibilityProjection.IsUsageKnown(observation),
            observation.InputTokens,
            observation.CachedInputTokens,
            observation.OutputTokens,
            observation.ReasoningTokens,
            observation.TotalTokens,
            observation.UsageStatus == WorkflowUsageStatus.Estimated ? roundedCostUsd : 0m,
            roundedCostUsd)
        {
            ToolCallCount = observation.ToolCallCount
        };
    }

    private static void AddOrValidate(
        IDictionary<Guid, ProcessRuntimeUsageObservation> observations,
        ProcessRuntimeUsageObservation candidate)
    {
        if (!observations.TryGetValue(candidate.UsageObservationId, out var stored))
        {
            observations.Add(candidate.UsageObservationId, candidate);
            return;
        }

        if (stored != candidate)
        {
            throw new InvalidOperationException(
                $"Process usage observation '{candidate.UsageObservationId:D}' has conflicting immutable dimensions across agent and workflow telemetry.");
        }
    }
}
