using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Usage;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.AgentFramework.Core;

public sealed class AgentProviderUsageProjectionSource(
    IAgentProviderUsageEvidenceStore evidenceStore,
    ISandboxWorkspaceCatalogStore catalogStore,
    ILogger<AgentProviderUsageProjectionSource> logger) : IBoundedProviderUsageProjectionSource
{
    public const string SourceIdentity = "agent-workspace";

    public string SourceName => SourceIdentity;

    public ProviderUsageWorkloadKind WorkloadKind => ProviderUsageWorkloadKind.Agent;

    public ValueTask<ProviderUsageSourceResult> ReadAsync(
        CancellationToken cancellationToken = default)
        => ReadCoreAsync(null, cancellationToken);

    public ValueTask<ProviderUsageSourceResult> ReadWindowAsync(ProviderUsageWindow window, CancellationToken cancellationToken = default)
        => ReadCoreAsync(window ?? throw new ArgumentNullException(nameof(window)), cancellationToken);

    private async ValueTask<ProviderUsageSourceResult> ReadCoreAsync(ProviderUsageWindow? window, CancellationToken cancellationToken)
    {
        try
        {
            AgentProviderUsageWindowEvidence? bounded = null;
            if (window is not null) {
                if (evidenceStore is not IBoundedAgentProviderUsageEvidenceStore rangeStore) {
                    throw new InvalidOperationException("The workspace store does not support bounded usage reads.");
                }
                bounded = await rangeStore.LoadProviderUsageWindowAsync(window, cancellationToken).ConfigureAwait(false);
                if (bounded.Evidence.ProviderUsageObservations.Count == 0) {
                    return new(SourceIdentity, WorkloadKind, bounded.State, [], DateTimeOffset.UnixEpoch, bounded.Error) {
                        Window = window, CoverageVerifiedAtUtc = bounded.CoverageVerifiedAtUtc
                    };
                }
            }
            var evidenceTask = bounded is null ? evidenceStore.LoadProviderUsageEvidenceAsync(cancellationToken) : Task.FromResult(bounded.Evidence);
            var catalogTask = catalogStore.LoadCatalogAsync(cancellationToken);
            await Task.WhenAll(evidenceTask, catalogTask).ConfigureAwait(false);
            var evidence = await evidenceTask.ConfigureAwait(false);
            var catalog = await catalogTask.ConfigureAwait(false);
            var runs = evidence.ExecutionRuns.ToDictionary(run => run.Id);
            var agents = catalog.Agents.ToDictionary(agent => agent.Id);
            var contributions = evidence.ProviderUsageObservations
                .Select((observation, index) => Map(observation, bounded?.ObservationIdentities[index]
                    ?? (observation.Id == Guid.Empty ? $"legacy:{index:D8}:{observation.CreatedAtUtc.UtcTicks}" : observation.Id.ToString("D")), runs, agents))
                .ToList();
            var updatedAtUtc = contributions.Count == 0
                ? DateTimeOffset.UnixEpoch
                : contributions.Max(contribution => contribution.OccurredAtUtc);
            return new(
                SourceIdentity,
                ProviderUsageWorkloadKind.Agent,
                bounded?.State ?? ProviderUsageSourceState.Complete,
                contributions,
                updatedAtUtc, bounded?.Error) { Window = window, CoverageVerifiedAtUtc = bounded?.CoverageVerifiedAtUtc };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to read the Agent provider usage projection.");
            return ProviderUsageSourceResult.Failed(
                SourceIdentity,
                ProviderUsageWorkloadKind.Agent,
                "agent_usage_read_failed",
                "Agent usage could not be read from the workspace store.",
                DateTimeOffset.UnixEpoch) with { Window = window };
        }
    }

    private static ProviderUsageContribution Map(
        ProviderUsageObservation observation,
        string contributionId,
        IReadOnlyDictionary<Guid, ExecutionRunRecord> runs,
        IReadOnlyDictionary<Guid, AgentDefinition> agents)
    {
        runs.TryGetValue(observation.ExecutionRunId ?? Guid.Empty, out var run);
        var agentId = ResolveAgentId(observation, run);
        var workload = agentId.HasValue || run is not null
            ? ProviderUsageWorkloadKind.Agent
            : ProviderUsageWorkloadKind.Unknown;
        agents.TryGetValue(agentId ?? Guid.Empty, out var agent);
        var consumerKind = agentId.HasValue
            ? ProviderUsageConsumerKind.Agent
            : ProviderUsageConsumerKind.Unattributed;
        return new(
            contributionId,
            workload,
            consumerKind,
            agentId?.ToString("D") ?? string.Empty,
            agent?.Name ?? (agentId.HasValue ? "Unknown agent" : "Unattributed Agent usage"),
            observation.ProviderProfileId,
            observation.ProviderName,
            observation.ProviderKind,
            observation.Model,
            (run?.Id ?? observation.ExecutionRunId)?.ToString("D") ?? string.Empty,
            MapOutcome(run?.Outcome),
            MapUsage(observation.UsageStatus),
            MapPricing(observation),
            new(
                observation.InputTokens,
                observation.CachedInputTokens,
                observation.CacheWriteTokens,
                observation.OutputTokens,
                observation.ReasoningTokens,
                observation.TotalTokens),
            observation.ProviderCostUsd ?? observation.CalculatedCostUsd,
            observation.CreatedAtUtc);
    }

    private static Guid? ResolveAgentId(
        ProviderUsageObservation observation,
        ExecutionRunRecord? run)
    {
        if (observation.AgentId.HasValue && run is not null && observation.AgentId.Value != run.AgentId)
        {
            throw new InvalidDataException(
                $"Usage observation '{observation.Id}' conflicts with execution run '{run.Id}'.");
        }

        return observation.AgentId ?? run?.AgentId;
    }

    private static ProviderUsageExecutionOutcome MapOutcome(RunOutcome? outcome)
    {
        return outcome switch
        {
            RunOutcome.Succeeded => ProviderUsageExecutionOutcome.Succeeded,
            RunOutcome.Failed => ProviderUsageExecutionOutcome.Failed,
            RunOutcome.Cancelled => ProviderUsageExecutionOutcome.Cancelled,
            null => ProviderUsageExecutionOutcome.Unknown,
            _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "Unknown Agent run outcome.")
        };
    }

    private static ProviderUsageCompleteness MapUsage(ProviderUsageObservationStatus status)
    {
        return status switch
        {
            ProviderUsageObservationStatus.Observed or ProviderUsageObservationStatus.ObservedFromMetric =>
                ProviderUsageCompleteness.Observed,
            ProviderUsageObservationStatus.MissingAfterProviderActivity =>
                ProviderUsageCompleteness.MissingAfterProviderActivity,
            ProviderUsageObservationStatus.UsageUnavailable or ProviderUsageObservationStatus.EstimatedFromMetric =>
                ProviderUsageCompleteness.UsageUnavailable,
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown Agent usage status.")
        };
    }

    private static ProviderUsagePricingCompleteness MapPricing(ProviderUsageObservation observation)
    {
        if (observation.ProviderCostUsd is >= 0m)
        {
            return ProviderUsagePricingCompleteness.ProviderReported;
        }

        return observation.CalculatedCostUsd is >= 0m
            ? ProviderUsagePricingCompleteness.CalculatedAtExecution
            : ProviderUsagePricingCompleteness.Unpriced;
    }
}
