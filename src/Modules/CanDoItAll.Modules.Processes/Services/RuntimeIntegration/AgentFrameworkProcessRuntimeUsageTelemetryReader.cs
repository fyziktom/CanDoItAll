using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.AgentFramework.Hosting;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Builder;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Drivers.Standard;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Projections;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.Processes.Templates;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Modules.Processes;


internal sealed class AgentFrameworkProcessRuntimeUsageTelemetryReader(
    IAgentReferenceDataProvider agentReferenceDataProvider,
    IAgentFrameworkWorkspaceService workspaceService) : IProcessRuntimeUsageTelemetryReader
{
    private const int ContextEstimatedInputTokenWarningThreshold = 128_000;
    private const int ContextToolSchemaTokenWarningThreshold = 32_000;
    private const int ContextToolCountWarningThreshold = 64;
    private const int UsageExecutionRunBatchTake = 5_000;

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

        var runIdSet = query.RunIds.ToHashSet();
        var referenceData = await agentReferenceDataProvider
            .GetAsync(new AgentReferenceDataRequest(AgentReferenceDataSections.Providers), cancellationToken)
            .ConfigureAwait(false);
        var providers = referenceData.Providers;
        var executionRunRead = await ListExecutionRunsAsync(query, cancellationToken).ConfigureAwait(false);
        var effectiveTake = Math.Min(
            query.TakePerRun,
            UsageExecutionRunBatchTake);
        var observationsByRun =
            new Dictionary<ProcessRunId, List<ProcessRuntimeUsageObservation>>();
        var detectionTake = effectiveTake + 1;
        var isComplete = executionRunRead.IsComplete;

        foreach (var executionRun in executionRunRead.Items
                     .OrderByDescending(run => run.UpdatedAtUtc))
        {
            if (!TryCreateProcessRunId(executionRun.ProcessRunId, out var executionProcessRunId) ||
                !runIdSet.Contains(executionProcessRunId))
            {
                continue;
            }

            ExecutionRunDetail detail;
            try
            {
                detail = await workspaceService.GetExecutionRunDetailAsync(executionRun.Id, cancellationToken).ConfigureAwait(false);
            }
            catch (InvalidOperationException)
            {
                isComplete = false;
                continue;
            }

            foreach (var usageObservation in detail.UsageObservations)
            {
                if (usageObservation.CreatedAtUtc < query.FromUtc ||
                    usageObservation.CreatedAtUtc > query.ToUtc ||
                    !TryResolveProcessRunId(usageObservation, detail.Run, out var processRunId) ||
                    !runIdSet.Contains(processRunId))
                {
                    continue;
                }

                if (!observationsByRun.TryGetValue(processRunId, out var runObservations))
                {
                    runObservations = new List<ProcessRuntimeUsageObservation>(
                        Math.Min(detectionTake, 32));
                    observationsByRun.Add(processRunId, runObservations);
                }

                if (runObservations.Count >= detectionTake)
                {
                    isComplete = false;
                    continue;
                }

                runObservations.Add(MapUsageObservation(
                    usageObservation,
                    detail.Run,
                    processRunId,
                    providers));
            }
        }

        var boundedObservationsByRun = observationsByRun.Values
            .Select(observations => observations
                .OrderBy(observation => observation.CreatedAtUtc)
                .ThenBy(observation => observation.UsageObservationId)
                .ToArray())
            .ToArray();
        isComplete &= boundedObservationsByRun.All(group =>
            group.Length <= effectiveTake);
        return new ProcessRuntimeUsageTelemetryReadResult(
            boundedObservationsByRun
                .SelectMany(group => group.Take(effectiveTake))
                .OrderBy(observation => observation.CreatedAtUtc)
                .ThenBy(observation => observation.UsageObservationId)
                .ToArray(),
            isComplete);
    }

    private async Task<ExecutionRunRead> ListExecutionRunsAsync(
        ProcessRuntimeUsageTelemetryQuery query,
        CancellationToken cancellationToken)
    {
        var runIds = query.RunIds
            .Select(runId => runId.ToString())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (runIds.Count == 0)
        {
            return new ExecutionRunRead([], IsComplete: true);
        }

        var requestedTake = Math.Max(
            (long)query.TakePerRun * runIds.Count + 1,
            runIds.Count);
        var take = (int)Math.Clamp(
            requestedTake,
            1,
            UsageExecutionRunBatchTake + 1L);
        var executionRuns = await workspaceService.ListExecutionRunsAsync(
            new ExecutionRunQuery(
                Take: take,
                UpdatedFromUtc: query.FromUtc,
                UpdatedToUtc: query.ToUtc)
            {
                ProcessRunIds = runIds
                    .OrderBy(runId => runId, StringComparer.OrdinalIgnoreCase)
                    .ToArray()
            },
            cancellationToken).ConfigureAwait(false);
        return new ExecutionRunRead(
            executionRuns
                .Where(run => runIds.Contains(run.ProcessRunId))
                .Take(UsageExecutionRunBatchTake)
                .ToArray(),
            executionRuns.Count < take);
    }

    private static ProcessRuntimeUsageObservation MapUsageObservation(
        ProviderUsageObservation usageObservation,
        ExecutionRunRecord? executionRun,
        ProcessRunId processRunId,
        IReadOnlyList<ProviderProfile> providers)
    {
        var isKnownUsage = ProviderPricingCalculator.IsKnownUsageStatus(usageObservation.UsageStatus);
        var actualCostUsd = isKnownUsage &&
            ProviderPricingCalculator.TryResolveObservationCost(usageObservation, providers, out var knownCostUsd)
                ? knownCostUsd
                : 0m;
        var estimatedCostUsd = usageObservation.UsageStatus == ProviderUsageObservationStatus.EstimatedFromMetric
            ? ResolveEstimatedCost(usageObservation, providers)
            : 0m;
        var contextSummary = ResolveRuntimeContextSummary(usageObservation.DiagnosticsJson);

        return new ProcessRuntimeUsageObservation(
            usageObservation.Id,
            usageObservation.ExecutionRunId ?? executionRun?.Id ?? Guid.Empty,
            processRunId,
            TryResolveStepInstanceId(usageObservation, executionRun),
            usageObservation.CreatedAtUtc,
            usageObservation.ProviderName,
            usageObservation.Model,
            usageObservation.SourcePhase,
            usageObservation.UsageStatus.ToString(),
            isKnownUsage,
            Math.Max(0, usageObservation.InputTokens),
            Math.Clamp(usageObservation.CachedInputTokens, 0, Math.Max(0, usageObservation.InputTokens)),
            Math.Max(0, usageObservation.OutputTokens),
            Math.Max(0, usageObservation.ReasoningTokens),
            Math.Max(0, usageObservation.TotalTokens),
            decimal.Round(estimatedCostUsd, 6, MidpointRounding.AwayFromZero),
            decimal.Round(actualCostUsd, 6, MidpointRounding.AwayFromZero))
        {
            ToolCallCount = usageObservation.ToolCallCount,
            ContextEstimatedInputTokens = contextSummary.EstimatedInputTokens,
            ContextInputMessageCount = contextSummary.InputMessageCount,
            ContextToolCount = contextSummary.ToolCount,
            ContextToolSchemaEstimatedTokens = contextSummary.ToolSchemaEstimatedTokens,
            ContextSourceCount = contextSummary.SourceCount,
            ContextBudgetExceeded = HasRuntimeContextBudgetWarning(contextSummary),
            ContextBudgetWarning = ResolveRuntimeContextBudgetWarning(contextSummary),
            ContextDiagnosticsJson = contextSummary.DiagnosticsJson
        };
    }

    private static RuntimeContextUsageSummary ResolveRuntimeContextSummary(string diagnosticsJson)
    {
        if (string.IsNullOrWhiteSpace(diagnosticsJson))
        {
            return RuntimeContextUsageSummary.Empty;
        }

        try
        {
            using var document = JsonDocument.Parse(diagnosticsJson);
            if (!document.RootElement.TryGetProperty("contextAssemblyManifest", out var manifest) ||
                manifest.ValueKind != JsonValueKind.Object)
            {
                return RuntimeContextUsageSummary.Empty;
            }

            if (!manifest.TryGetProperty("totals", out var totals) ||
                totals.ValueKind != JsonValueKind.Object)
            {
                return RuntimeContextUsageSummary.Empty;
            }

            var sourceCount = manifest.TryGetProperty("sources", out var sources) && sources.ValueKind == JsonValueKind.Array
                ? sources.GetArrayLength()
                : 0;
            return new RuntimeContextUsageSummary(
                ReadInt32(totals, "estimatedInputTokens"),
                ReadInt32(totals, "inputMessageCount"),
                ReadInt32(totals, "toolCount"),
                ReadInt32(totals, "toolSchemaEstimatedTokens"),
                sourceCount,
                manifest.GetRawText());
        }
        catch (JsonException)
        {
            return RuntimeContextUsageSummary.Empty;
        }
    }

    private static int ReadInt32(JsonElement element, string propertyName)
        => element.TryGetProperty(propertyName, out var property) && property.TryGetInt32(out var value)
            ? Math.Max(0, value)
            : 0;

    private static bool HasRuntimeContextBudgetWarning(RuntimeContextUsageSummary contextSummary)
        => contextSummary.EstimatedInputTokens >= ContextEstimatedInputTokenWarningThreshold ||
           contextSummary.ToolSchemaEstimatedTokens >= ContextToolSchemaTokenWarningThreshold ||
           contextSummary.ToolCount >= ContextToolCountWarningThreshold;

    private static string ResolveRuntimeContextBudgetWarning(RuntimeContextUsageSummary contextSummary)
    {
        if (!HasRuntimeContextBudgetWarning(contextSummary))
        {
            return string.Empty;
        }

        return string.Join(
            " ",
            "Agent context request shape is above the diagnostic warning threshold.",
            $"EstimatedInputTokens={contextSummary.EstimatedInputTokens}.",
            $"ToolCount={contextSummary.ToolCount}.",
            $"ToolSchemaEstimatedTokens={contextSummary.ToolSchemaEstimatedTokens}.",
            $"SourceCount={contextSummary.SourceCount}.");
    }

    private static decimal ResolveEstimatedCost(
        ProviderUsageObservation usageObservation,
        IReadOnlyList<ProviderProfile> providers)
    {
        if (usageObservation.ProviderCostUsd is > 0m)
        {
            return usageObservation.ProviderCostUsd.Value;
        }

        if (usageObservation.CalculatedCostUsd is > 0m)
        {
            return usageObservation.CalculatedCostUsd.Value;
        }

        var provider = providers.FirstOrDefault(candidate =>
            string.Equals(candidate.Name, usageObservation.ProviderName, StringComparison.OrdinalIgnoreCase));
        if (provider is not null &&
            ProviderPricingCalculator.TryCalculate(
                provider.Name,
                usageObservation.Model,
                usageObservation.InputTokens,
                usageObservation.CachedInputTokens,
                ProviderPricingCalculator.ResolveBillableOutputTokens(
                    usageObservation.InputTokens,
                    usageObservation.OutputTokens,
                    usageObservation.TotalTokens),
                provider.ModelPrices,
                out var cost))
        {
            return cost.TotalUsd;
        }

        return 0m;
    }

    private static bool TryResolveProcessRunId(
        ProviderUsageObservation usageObservation,
        ExecutionRunRecord? executionRun,
        out ProcessRunId processRunId)
    {
        if (TryCreateProcessRunId(usageObservation.ProcessRunId, out processRunId))
        {
            return true;
        }

        return executionRun is not null &&
               TryCreateProcessRunId(executionRun.ProcessRunId, out processRunId);
    }

    private static ProcessStepInstanceId? TryResolveStepInstanceId(
        ProviderUsageObservation usageObservation,
        ExecutionRunRecord? executionRun)
    {
        if (TryCreateStepInstanceId(usageObservation.ProcessStepId, out var observationStepId))
        {
            return observationStepId;
        }

        return executionRun is not null &&
               TryCreateStepInstanceId(executionRun.ProcessStepId, out var executionStepId)
            ? executionStepId
            : null;
    }

    private static bool TryCreateProcessRunId(string value, out ProcessRunId processRunId)
    {
        processRunId = default;
        if (!Guid.TryParse(value, out var parsed) || parsed == Guid.Empty)
        {
            return false;
        }

        try
        {
            processRunId = new ProcessRunId(parsed);
            return true;
        }
        catch (ArgumentException)
        {
            processRunId = default;
            return false;
        }
    }

    private static bool TryCreateStepInstanceId(string value, out ProcessStepInstanceId stepInstanceId)
    {
        stepInstanceId = default;
        if (!Guid.TryParse(value, out var parsed) || parsed == Guid.Empty)
        {
            return false;
        }

        try
        {
            stepInstanceId = new ProcessStepInstanceId(parsed);
            return true;
        }
        catch (ArgumentException)
        {
            stepInstanceId = default;
            return false;
        }
    }

    private sealed record RuntimeContextUsageSummary(
        int EstimatedInputTokens,
        int InputMessageCount,
        int ToolCount,
        int ToolSchemaEstimatedTokens,
        int SourceCount,
        string DiagnosticsJson)
    {
        public static RuntimeContextUsageSummary Empty { get; } = new(
            EstimatedInputTokens: 0,
            InputMessageCount: 0,
            ToolCount: 0,
            ToolSchemaEstimatedTokens: 0,
            SourceCount: 0,
            DiagnosticsJson: string.Empty);
    }

    private sealed record ExecutionRunRead(
        IReadOnlyList<ExecutionRunRecord> Items,
        bool IsComplete);
}

