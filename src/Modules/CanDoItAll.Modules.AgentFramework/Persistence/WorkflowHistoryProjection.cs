using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.ProviderHistory;
using CanDoItAll.AgentFramework.ProviderHistory.Persistence;
using CanDoItAll.Infrastructure.Persistence;

namespace CanDoItAll.Modules.AgentFramework;

public sealed class WorkflowHistoryProjection(HistoryOutboxWriter outbox) {
    public async Task StageAsync(AppDbContext ownerContext, IEnumerable<WorkflowUsageObservation> observations,
        CancellationToken cancellationToken) {
        var partition = await HistoryPartitionStore.GetForWriteAsync(ownerContext, cancellationToken);
        foreach (var observation in observations) {
            if (Create(observation, partition) is { } mutation) {
                outbox.Stage(ownerContext, mutation);
            }
        }
    }

    internal static HistorySourceMutation? Create(WorkflowUsageObservation observation, HistoryPartition partition) {
        if (observation.HistoryEvidence is { IsPrimary: false }) {
            return null;
        }
        var source = new CanonicalEvidenceReference(partition, HistorySourceKind.Workflow,
            new(observation.RunId!.Value.Value.ToString("N")), new(observation.Id.Value.ToString("N")));
        if (observation.HistoryEvidence is { } history) {
            return new(source, new(1), HistorySourceMutationKind.Upsert, null, []) {
                Role = HistoryOwnerRole.PrimaryEvidence,
                Attempts = history.Attempts
            };
        }
        var knownUsage = observation.UsageStatus is WorkflowUsageStatus.Observed or WorkflowUsageStatus.Estimated;
        var priceState = observation.PricingStatus != WorkflowPricingStatus.Known ? HistoryPriceState.Unpriced
            : observation.PricingProvenance switch {
                WorkflowUsagePricingProvenance.ProviderReported => HistoryPriceState.ProviderReported,
                WorkflowUsagePricingProvenance.PricingProfileSnapshot => HistoryPriceState.CalculatedAtExecution,
                _ => HistoryPriceState.PartialEstimate
            };
        var entry = new HistoryEntry(new(observation.Id.Value), partition, null, null,
            HistoryGranularity.LegacyAggregate, HistoryStorageTimestamp.Normalize(observation.RecordedAtUtc),
            HistoryTimeBasis.CanonicalRecorded, null, HistoryStorageTimestamp.Normalize(observation.CompletedAtUtc),
            new(observation.ProviderProfileId is { } provider ? new ProviderIdentity(provider) : null,
                observation.ProviderName, observation.ProviderKind?.ToString() ?? "Unknown",
                string.IsNullOrWhiteSpace(observation.Model) ? null : new ProviderModelIdentity(observation.Model),
                string.IsNullOrWhiteSpace(observation.Model) ? null : new ProviderModelIdentity(observation.Model)),
            HistoryOperation.CompleteChat, observation.Origin is WorkflowLaunchOrigin.ProcessAssignment
                ? HistoryWorkload.Process : HistoryWorkload.Workflow,
            HistoryOutcome.Unknown,
            new(HistoryAuthenticationKind.Unknown),
            new(knownUsage ? HistoryUsageState.Partial : HistoryUsageState.Unavailable,
                knownUsage ? observation.InputTokens : null, knownUsage ? observation.OutputTokens : null,
                knownUsage ? observation.CachedInputTokens : null, ReasoningTokens: knownUsage ? observation.ReasoningTokens : null),
            new(priceState, observation.CostUsd, observation.CostUsd.HasValue ? "USD" : null,
                observation.PricingProfileHash, observation.PricingVersion),
            HistoryMetadataAuthority.CanonicalProjection, HistoryRetentionAuthority.CanonicalOwner, HistoryDetailState.Unavailable) {
                Version = 1
            };
        return new(source, new(1), HistorySourceMutationKind.Upsert, entry, []) { Role = HistoryOwnerRole.PrimaryEvidence };
    }
}
