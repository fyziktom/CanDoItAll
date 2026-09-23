using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.ProviderHistory;

namespace CanDoItAll.AgentFramework.Persistence;

internal sealed record FileHistoryFact(
    HistoryOwnerIdentity Owner, HistoryPartition? Partition, HistoryEntry? Aggregate,
    IReadOnlyList<HistoryEntry> Attempts, bool HasContentOwner) {
    internal static FileHistoryFact? From(ProviderUsageObservation observation) {
        if (observation.HistoryEvidence is { IsPrimary: false }) {
            return null;
        }
        var evidenceId = observation.HistoryEvidence?.RequestId.Value ?? observation.Id;
        var owner = new HistoryOwnerIdentity(HistorySourceKind.AgentConversation,
            new((observation.ExecutionRunId ?? observation.Id).ToString("N")), new(evidenceId.ToString("N")));
        if (observation.HistoryEvidence is { } captured) {
            var partition = captured.Attempts[0].Partition;
            if (captured.Attempts.Any(attempt => attempt.Partition != partition)) {
                throw new ProviderHistoryException(HistoryFailure.Conflict, "File evidence spans multiple history partitions.");
            }
            return new(owner, partition, null, captured.Attempts, observation.ExecutionRunId.HasValue);
        }
        var known = observation.UsageStatus is ProviderUsageObservationStatus.Observed
            or ProviderUsageObservationStatus.ObservedFromMetric or ProviderUsageObservationStatus.EstimatedFromMetric;
        var amount = observation.ProviderCostUsd ?? observation.CalculatedCostUsd;
        var price = new HistoryPrice(observation.ProviderCostUsd.HasValue ? HistoryPriceState.ProviderReported
            : amount.HasValue ? HistoryPriceState.CalculatedAtExecution : HistoryPriceState.Unpriced,
            amount, amount.HasValue ? "USD" : null, observation.PricingProfileHash, observation.PricingVersion);
        var recorded = new DateTimeOffset(observation.CreatedAtUtc.UtcTicks - observation.CreatedAtUtc.UtcTicks % 10, TimeSpan.Zero);
        var entry = new HistoryEntry(new(observation.Id), default, null, null, HistoryGranularity.LegacyAggregate,
            recorded, HistoryTimeBasis.CanonicalRecorded, null, null,
            new(observation.ProviderProfileId is { } id ? new ProviderIdentity(id) : null,
                observation.ProviderName, observation.ProviderKind.ToString(),
                string.IsNullOrWhiteSpace(observation.Model) ? null : new ProviderModelIdentity(observation.Model),
                string.IsNullOrWhiteSpace(observation.Model) ? null : new ProviderModelIdentity(observation.Model)),
            HistoryOperation.CompleteChat, HistoryWorkload.Agent, HistoryOutcome.Unknown,
            new(HistoryAuthenticationKind.Unknown),
            new(known ? HistoryUsageState.Partial : HistoryUsageState.Unavailable,
                known ? observation.InputTokens : null, known ? observation.OutputTokens : null,
                known ? observation.CachedInputTokens : null, known ? observation.CacheWriteTokens : null,
                known ? observation.ReasoningTokens : null),
            price, HistoryMetadataAuthority.CanonicalProjection, HistoryRetentionAuthority.CanonicalOwner,
            observation.ExecutionRunId.HasValue ? HistoryDetailState.Canonical : HistoryDetailState.Unavailable);
        return new(owner, null, entry, [], observation.ExecutionRunId.HasValue);
    }

    internal HistorySourceMutation Project(HistoryPartition partition, long version, bool deleted) {
        if (Partition is { } bound && bound != partition) {
            throw new ProviderHistoryException(HistoryFailure.StaleContext, "File evidence belongs to a different history partition.");
        }
        var source = new CanonicalEvidenceReference(partition, Owner.Kind, Owner.OwnerId, Owner.EvidenceId);
        return new(source, new(version), deleted ? HistorySourceMutationKind.Delete : HistorySourceMutationKind.Upsert,
            deleted || Aggregate is null ? null : Aggregate with { Partition = partition, Version = version }, []) {
            Attempts = deleted ? [] : Attempts,
            Role = HasContentOwner ? HistoryOwnerRole.ContentOwner : HistoryOwnerRole.PrimaryEvidence
        };
    }
}

internal enum FileHistoryCommitStage { Prepared, SourceCommitted, Published, Acknowledged, LegacyBindingPersisted, LegacyHeadBound, AcknowledgmentPersisted }
internal readonly record struct FileHistoryKey(Guid EvidenceId, Guid? PartitionId);
internal sealed record FileHistoryMutation(long Version, FileHistoryFact Fact, string SourcePath, string? TargetHash, bool Deleted);
internal sealed record FileHistoryHead(FileHistoryKey Key, long HighVersion, long AcknowledgedVersion,
    FileHistoryMutation? Committed, FileHistoryMutation? Prepared);
internal sealed record FileHistoryPrepared(FileHistoryKey Key, long Version);
public sealed record FileHistoryPublication(Guid EvidenceId, long Version, HistorySourceMutation Mutation);
