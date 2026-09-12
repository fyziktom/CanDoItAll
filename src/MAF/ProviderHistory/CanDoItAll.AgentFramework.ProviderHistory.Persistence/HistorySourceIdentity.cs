using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;

namespace CanDoItAll.AgentFramework.ProviderHistory.Persistence;

internal static class HistorySourceIdentity {
    internal const int MaximumLinkedEntries = 1000;
    internal static Guid Key(CanonicalEvidenceReference source)
        => new(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new[] {
            source.Partition.OriginInstanceId.ToString("N"), source.Partition.StorageLineageId.ToString("N"),
            source.Partition.SecurityPartition, ((int)source.Kind).ToString(System.Globalization.CultureInfo.InvariantCulture),
            source.Owner.Value, source.Evidence.Value
        })).AsSpan(0, 16));

    internal static string Hash(HistorySourceMutation mutation)
        => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(mutation)));

    internal static bool MatchesHistoricalChatPrice(HistorySourceMutation mutation, string? historicalHash) {
        if (mutation is not {
            Source.Kind: HistorySourceKind.SimpleChat, Kind: HistorySourceMutationKind.Upsert,
            Entry: { Granularity: HistoryGranularity.LegacyAggregate, Price: {
                State: HistoryPriceState.ProviderReported or HistoryPriceState.CalculatedAtExecution, Amount: { } amount
            } } entry
        }) {
            return false;
        }
        // PostgreSQL changed decimal scale; accept only the complete original evidence hash without rewriting it.
        const int maximumDecimalScale = 28;
        for (var scale = 0; scale <= maximumDecimalScale; scale++) {
            var text = amount.ToString($"F{scale}", CultureInfo.InvariantCulture);
            if (!decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var candidate) || candidate != amount) {
                continue;
            }
            if (Hash(mutation with { Entry = entry with { Price = entry.Price with { Amount = candidate } } }) == historicalHash) {
                return true;
            }
        }
        return false;
    }

    internal static void Validate(HistorySourceMutation mutation) {
        var source = mutation.Source;
        if (mutation.Version.Value < 1 || !Enum.IsDefined(source.Kind) || !Enum.IsDefined(mutation.Kind) ||
            !Enum.IsDefined(mutation.Role) || string.IsNullOrWhiteSpace(source.Owner.Value) ||
            string.IsNullOrWhiteSpace(source.Evidence.Value) || source.Owner.Value.Length > 256 ||
            source.Evidence.Value.Length > 256 || mutation.LinkedEntries.Count > MaximumLinkedEntries ||
            mutation.LinkedEntries.Any(id => id.Value == Guid.Empty) ||
            mutation.Attempts.Count > MaximumLinkedEntries ||
            mutation.Attempts.Any(attempt => attempt.Partition != source.Partition ||
                attempt.Granularity != HistoryGranularity.ProviderCallAttempt || attempt.AttemptId is null ||
                attempt.Id.Value == Guid.Empty) ||
            mutation.Attempts.Select(attempt => attempt.Id).Distinct().Count() != mutation.Attempts.Count ||
            mutation.Entry is { } entry && entry.Partition != source.Partition) {
            throw new ArgumentException("History source mutation has invalid identity, version or linked entries.", nameof(mutation));
        }
        if (mutation.Kind == HistorySourceMutationKind.Upsert && mutation.Entry is null && mutation.LinkedEntries.Count == 0 && mutation.Attempts.Count == 0) {
            throw new ArgumentException("An upsert requires exact entry evidence or linked attempt identities.", nameof(mutation));
        }
    }

    internal static void Require(HistorySourceRow row, CanonicalEvidenceReference source) {
        if (row.PartitionId != source.Partition.StorageLineageId || row.Kind != source.Kind ||
            row.OwnerId != source.Owner.Value || row.EvidenceId != source.Evidence.Value) {
            throw new ProviderHistoryException(HistoryFailure.Conflict, "A history source identity conflicts with existing evidence.");
        }
    }
}
