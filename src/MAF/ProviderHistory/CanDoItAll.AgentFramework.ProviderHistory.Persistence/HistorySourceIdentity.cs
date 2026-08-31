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
