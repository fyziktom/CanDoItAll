using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Modules.AgentFramework;

public sealed class HrAgentUsageAnalyticsService(
    ISandboxWorkspaceExecutionStore executionStore,
    IAgentReferenceDataProvider referenceDataProvider)
{
    public async Task<HrAgentUsageResult> GetAsync(
        HrAgentUsageInput input,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (!Enum.IsDefined(input.Scope))
        {
            throw new ArgumentOutOfRangeException(nameof(input), input.Scope, "Usage scope is not defined.");
        }

        if (input.AgentId == Guid.Empty)
        {
            throw new ArgumentException("Agent id cannot be empty.", nameof(input));
        }

        if (input.FromUtc.HasValue && input.ToUtc.HasValue && input.FromUtc > input.ToUtc)
        {
            throw new InvalidOperationException("FromUtc cannot be later than ToUtc.");
        }

        var referenceData = await referenceDataProvider.GetAsync(
            AgentReferenceDataRequest.AgentsAndProviders(includeAgentTemplates: true),
            cancellationToken);
        if (input.AgentId.HasValue &&
            referenceData.Agents.All(agent => agent.Id != input.AgentId.Value))
        {
            throw new InvalidOperationException($"Agent '{input.AgentId.Value:D}' was not found.");
        }

        var state = await executionStore.LoadExecutionAsync(cancellationToken);
        var providers = referenceData.Providers;
        var runsById = state.ExecutionRuns.ToDictionary(run => run.Id);
        var candidateObservations = state.ProviderUsageObservations
            .DistinctBy(observation => observation.Id)
            .Where(observation => IsWithinWindow(observation.CreatedAtUtc, input.FromUtc, input.ToUtc))
            .Where(observation => MatchesAgent(observation, runsById, input.AgentId))
            .ToArray();
        var workflowExecutionRunIds = candidateObservations
            .Where(observation => !string.IsNullOrWhiteSpace(observation.WorkflowRunId))
            .Select(observation => observation.ExecutionRunId)
            .OfType<Guid>()
            .ToHashSet();
        var observations = candidateObservations
            .Where(observation =>
                input.Scope == HrAgentUsageScope.All ||
                Classify(observation, runsById) == input.Scope)
            .ToArray();
        var runs = state.ExecutionRuns
            .Where(run => IsWithinWindow(run.CreatedAtUtc, input.FromUtc, input.ToUtc))
            .Where(run => !input.AgentId.HasValue || run.AgentId == input.AgentId.Value)
            .Where(run => MatchesRunScope(run, workflowExecutionRunIds, input.Scope))
            .ToArray();

        var knownUsageCount = observations.Count(observation =>
            ProviderPricingCalculator.IsKnownUsageStatus(observation.UsageStatus));
        var estimatedUsageCount = observations.Count(observation =>
            observation.UsageStatus == ProviderUsageObservationStatus.EstimatedFromMetric);
        var unknownUsageCount = observations.Length - knownUsageCount - estimatedUsageCount;
        var usageSummary = ProviderPricingCalculator.SummarizeUsage(observations, providers);
        var knownCostCount = observations.Count(observation =>
            ProviderPricingCalculator.TryResolveObservationCost(observation, providers, out _));
        var unknownCostCount = observations.Length - knownCostCount;
        var isComplete = estimatedUsageCount == 0 && unknownUsageCount == 0 && unknownCostCount == 0;

        return new HrAgentUsageResult(
            input.AgentId,
            input.Scope,
            input.FromUtc,
            input.ToUtc,
            runs.Length,
            runs.Count(run => run.Outcome == RunOutcome.Failed || run.State == ExecutionState.Failed),
            observations.Length,
            knownUsageCount,
            estimatedUsageCount,
            unknownUsageCount,
            knownCostCount,
            unknownCostCount,
            usageSummary.InputTokens,
            usageSummary.CachedInputTokens,
            usageSummary.OutputTokens,
            usageSummary.ReasoningTokens,
            usageSummary.TotalTokens,
            usageSummary.KnownCostUsd,
            isComplete,
            BuildCostQualification(
                knownCostCount,
                unknownCostCount,
                estimatedUsageCount,
                unknownUsageCount));
    }

    private static HrAgentUsageScope Classify(
        ProviderUsageObservation observation,
        IReadOnlyDictionary<Guid, ExecutionRunRecord> runsById)
    {
        var linkedRun = observation.ExecutionRunId.HasValue &&
                        runsById.TryGetValue(observation.ExecutionRunId.Value, out var run)
            ? run
            : null;
        if (linkedRun is not null && HrAgentExecutionLineage.IsManagerReview(linkedRun))
        {
            return HrAgentUsageScope.Other;
        }

        if (HasProcessLineage(observation, linkedRun))
        {
            return HrAgentUsageScope.Process;
        }

        if (!string.IsNullOrWhiteSpace(observation.WorkflowRunId))
        {
            return HrAgentUsageScope.Workflow;
        }

        if (observation.ChatSessionId.HasValue)
        {
            return HrAgentUsageScope.BasicChat;
        }

        return HrAgentUsageScope.Other;
    }

    private static bool HasProcessLineage(
        ProviderUsageObservation observation,
        ExecutionRunRecord? linkedRun)
    {
        if (string.IsNullOrWhiteSpace(observation.ProcessRunId) ||
            string.IsNullOrWhiteSpace(observation.ProcessStepId))
        {
            return false;
        }

        return linkedRun is null ||
               (HrAgentExecutionLineage.IsProcessStep(linkedRun) &&
                string.Equals(
                    linkedRun.ProcessRunId,
                    observation.ProcessRunId,
                    StringComparison.OrdinalIgnoreCase) &&
                string.Equals(
                    linkedRun.ProcessStepId,
                    observation.ProcessStepId,
                    StringComparison.OrdinalIgnoreCase));
    }

    private static bool MatchesRunScope(
        ExecutionRunRecord run,
        IReadOnlySet<Guid> workflowExecutionRunIds,
        HrAgentUsageScope scope)
    {
        if (scope == HrAgentUsageScope.All)
        {
            return true;
        }

        if (HrAgentExecutionLineage.IsManagerReview(run))
        {
            return scope == HrAgentUsageScope.Other;
        }

        if (HrAgentExecutionLineage.IsProcessStep(run))
        {
            return scope == HrAgentUsageScope.Process;
        }

        if (workflowExecutionRunIds.Contains(run.Id))
        {
            return scope == HrAgentUsageScope.Workflow;
        }

        if (run.ChatSessionId.HasValue)
        {
            return scope == HrAgentUsageScope.BasicChat;
        }

        return scope == HrAgentUsageScope.Other;
    }

    private static bool MatchesAgent(
        ProviderUsageObservation observation,
        IReadOnlyDictionary<Guid, ExecutionRunRecord> runsById,
        Guid? agentId)
    {
        if (!agentId.HasValue)
        {
            return true;
        }

        if (observation.AgentId.HasValue)
        {
            return observation.AgentId.Value == agentId.Value;
        }

        return observation.ExecutionRunId.HasValue &&
               runsById.TryGetValue(observation.ExecutionRunId.Value, out var run) &&
               run.AgentId == agentId.Value;
    }

    private static bool IsWithinWindow(
        DateTimeOffset value,
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc)
    {
        return (!fromUtc.HasValue || value >= fromUtc.Value) &&
               (!toUtc.HasValue || value <= toUtc.Value);
    }

    private static string BuildCostQualification(
        int knownCostObservationCount,
        int unknownCostObservationCount,
        int estimatedUsageObservationCount,
        int unknownUsageObservationCount)
    {
        if (unknownCostObservationCount == 0 &&
            estimatedUsageObservationCount == 0 &&
            unknownUsageObservationCount == 0)
        {
            return $"Known cost covers all {knownCostObservationCount} usage observations in the selected scope.";
        }

        return $"Known token totals and KnownCostUsd include observed usage only. {unknownCostObservationCount} observations lack resolved canonical pricing, {estimatedUsageObservationCount} contain estimates, and {unknownUsageObservationCount} have unavailable usage.";
    }
}
