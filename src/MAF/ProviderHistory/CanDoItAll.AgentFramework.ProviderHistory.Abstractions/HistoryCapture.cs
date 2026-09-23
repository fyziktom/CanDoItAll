namespace CanDoItAll.AgentFramework.ProviderHistory;

public interface IProviderHistoryCapture {
    Task BeginAsync(HistoryAttemptStart start, HistoryCurrentTurn? currentTurn, CancellationToken cancellationToken);
    Task CompleteAsync(HistoryAttemptStart start, HistoryAttemptCompletion completion,
        string? currentResponse, CancellationToken cancellationToken);
}

public enum HistorySourceMutationKind { Upsert, Delete }
public sealed record HistorySourceMutation(
    CanonicalEvidenceReference Source,
    HistorySourceVersion Version,
    HistorySourceMutationKind Kind,
    HistoryEntry? Entry,
    IReadOnlyList<HistoryEntryId> LinkedEntries) {
    public HistoryOwnerRole Role { get; init; } = HistoryOwnerRole.ContentOwner;
    public IReadOnlyList<HistoryEntry> Attempts { get; init; } = [];
}

public interface IProviderHistorySource {
    HistorySourceKind Kind { get; }
    Task<HistorySourceMutation?> ReadAsync(CanonicalEvidenceReference source, CancellationToken cancellationToken);
    Task<HistoryDetail> ReadDetailAsync(CanonicalEvidenceReference source, HistoryEntryId entryId, CancellationToken cancellationToken);
}
