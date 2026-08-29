namespace CanDoItAll.AgentFramework.ProviderHistory;

public readonly record struct HistoryEntryId(Guid Value) {
    public static HistoryEntryId New() => new(Guid.NewGuid());

    public static HistoryEntryId ForCanonical(HistoryOwnerIdentity identity) {
        var value = FormattableString.Invariant(
            $"{(int)identity.Kind}:{identity.OwnerId.Value.Length}:{identity.OwnerId.Value}:{identity.EvidenceId.Value}");
        return new(new Guid(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(value)).AsSpan(0, 16)));
    }
}
public readonly record struct ProviderRequestId(Guid Value) {
    public static ProviderRequestId New() => new(Guid.NewGuid());
}
public readonly record struct ProviderAttemptId(Guid Value) {
    public static ProviderAttemptId New() => new(Guid.NewGuid());
}
public readonly record struct ProviderIdentity(Guid Value);
public readonly record struct ManagedCredentialId(Guid Value);
public readonly record struct HistoryOwnerId(string Value);
public readonly record struct HistoryEvidenceId(string Value);

public readonly record struct ProviderModelIdentity {
    [System.Text.Json.Serialization.JsonConstructor]
    public ProviderModelIdentity(string value) {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (value.Length > 512) {
            throw new ArgumentOutOfRangeException(nameof(value));
        }
        Value = value;
    }
    public string Value { get; }
}

public readonly record struct HistoryPartition(
    Guid OriginInstanceId, Guid StorageLineageId, string SecurityPartition);

public readonly record struct HistoryExecutionFence(
    long ProfileGeneration, long AuthorizationRevision);

public sealed record CanonicalEvidenceReference(
    HistoryPartition Partition,
    HistorySourceKind Kind,
    HistoryOwnerId Owner,
    HistoryEvidenceId Evidence);

public readonly record struct HistorySourceVersion(long Value);

public sealed record HistoryOwnerLink(
    HistoryEntryId EntryId,
    CanonicalEvidenceReference Source,
    HistorySourceVersion Version,
    HistoryOwnerRole Role,
    HistoryOwnerState State);

public sealed record RemoteRequestReference(Guid ConfiguredSourceId, string PublisherRequestId);
