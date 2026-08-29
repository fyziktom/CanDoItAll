namespace CanDoItAll.AgentFramework.ProviderHistory;

public sealed class ProviderRequestHistoryService(
    IProviderHistoryAccess access,
    IHistoryReadStore store,
    IHistoryCursorProtector cursors,
    IEnumerable<IProviderHistorySource> sources,
    HistoryAuthorizedOperation operations,
    TimeProvider clock) : IProviderRequestHistory {
    private readonly IReadOnlyDictionary<HistorySourceKind, IProviderHistorySource> sourceReaders = sources.ToDictionary(source => source.Kind);

    public Task<HistoryPage> SearchAsync(ProviderRequestHistoryQuery query, CancellationToken cancellationToken) {
        HistoryContractValidation.Validate(query);
        return operations.RunAsync(HistoryPermission.ReadMetadata, async (context, token) => {
            HistoryQueryBinding.RequireScope(context, query.Scope);
            var binding = HistoryQueryBinding.Create(context, query);
            var position = HistoryQueryBinding.ReadPosition(query.Cursor, binding, query, cursors);
            var page = await store.SearchAsync(context, query, position, token);
            var entries = page.Entries.Take(query.PageSize).ToArray();
            var next = page.Entries.Count > query.PageSize && entries.Length > 0
                ? cursors.Protect(new(1, binding, new(entries[^1].SortAtUtc, entries[^1].Id))) : null;
            return new HistoryPage(entries, next, page.Coverage, page.QueriedAtUtc);
        }, cancellationToken);
    }

    public Task<HistoryMetadata?> GetMetadataAsync(HistoryEntryId entryId, CancellationToken cancellationToken) {
        RequireEntryId(entryId);
        return operations.RunAsync(HistoryPermission.ReadMetadata,
            (context, token) => store.GetMetadataAsync(context, entryId, token), cancellationToken);
    }

    public Task<HistoryDetail> GetDetailAsync(HistoryEntryId entryId, CanonicalEvidenceReference? owner, CancellationToken cancellationToken) {
        RequireEntryId(entryId);
        return operations.RunAsync(HistoryPermission.ReadContent, async (context, token) => {
            var metadata = await store.GetMetadataAsync(context, entryId, token);
            if (metadata is null) {
                return Unavailable(entryId);
            }
            HistoryDetail detail;
            if (owner is null) {
                if (metadata.Entry.DetailState is HistoryDetailState.Canonical or HistoryDetailState.PendingCanonical) {
                    return new(entryId, metadata.Entry.DetailState);
                }
                detail = await store.ReadDetailAsync(context, entryId, token);
            } else {
                var link = metadata.Owners.SingleOrDefault(link => link.Source == owner &&
                    link.Role == HistoryOwnerRole.ContentOwner && link.State == HistoryOwnerState.Linked);
                if (link is null || owner.Partition != context.Partition) {
                    return Unavailable(entryId);
                }
                await access.AuthorizeOwnerAsync(context, owner, token);
                if (!sourceReaders.TryGetValue(owner.Kind, out var reader) ||
                    !Matches(await reader.ReadAsync(owner, token), link)) {
                    return Unavailable(entryId);
                }
                detail = await reader.ReadDetailAsync(owner, entryId, token);
                await access.AuthorizeOwnerAsync(context, owner, token);
                if (!Matches(await reader.ReadAsync(owner, token), link)) {
                    return Unavailable(entryId);
                }
            }
            if (!await store.IsCurrentAsync(context, metadata, owner, token)) {
                return Unavailable(entryId);
            }
            if (detail.ExpiresAtUtc <= clock.GetUtcNow()) {
                return new(entryId, HistoryDetailState.Expired);
            }
            return detail;
        }, cancellationToken);
    }

    private static bool Matches(HistorySourceMutation? mutation, HistoryOwnerLink link) =>
        mutation is { Kind: HistorySourceMutationKind.Upsert } && mutation.Source == link.Source && mutation.Version == link.Version;

    private static HistoryDetail Unavailable(HistoryEntryId entryId) => new(entryId, HistoryDetailState.Unavailable);

    private static void RequireEntryId(HistoryEntryId entryId) {
        if (entryId.Value == Guid.Empty) {
            throw new ProviderHistoryException(HistoryFailure.InvalidQuery, "Select a valid history entry.");
        }
    }
}
