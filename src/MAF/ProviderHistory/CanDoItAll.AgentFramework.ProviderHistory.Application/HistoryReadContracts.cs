namespace CanDoItAll.AgentFramework.ProviderHistory;

public sealed record HistoryPagePosition(DateTimeOffset SortAtUtc, HistoryEntryId EntryId);
public sealed record HistoryIndexPage(IReadOnlyList<HistoryEntry> Entries, HistoryCoverage Coverage, DateTimeOffset QueriedAtUtc);
public sealed record HistoryPageCursor(int Version, string Binding, HistoryPagePosition Position);

public interface IHistoryReadStore {
    Task<HistoryIndexPage> SearchAsync(HistoryAccessContext context, ProviderRequestHistoryQuery query,
        HistoryPagePosition? position, CancellationToken cancellationToken);
    Task<HistoryMetadata?> GetMetadataAsync(HistoryAccessContext context, HistoryEntryId entryId, CancellationToken cancellationToken);
    Task<HistoryDetail> ReadDetailAsync(HistoryAccessContext context, HistoryEntryId entryId, CancellationToken cancellationToken);
    Task<bool> IsCurrentAsync(HistoryAccessContext context, HistoryMetadata metadata,
        CanonicalEvidenceReference? owner, CancellationToken cancellationToken);
}

public interface IHistoryCursorProtector {
    string Protect(HistoryPageCursor cursor);
    HistoryPageCursor Unprotect(string cursor);
}
