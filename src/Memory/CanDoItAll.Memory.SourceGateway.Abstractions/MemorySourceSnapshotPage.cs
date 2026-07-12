namespace CanDoItAll.Memory.SourceGateway;

public static class MemorySourceSnapshotPage
{
    public const int DefaultTake = 250;
    public const int MaxTake = 1000;

    public static IReadOnlyList<MemorySourceItem> Apply(
        IReadOnlyList<MemorySourceItem> items,
        MemorySourceSnapshotCursor? cursor,
        int? take,
        MemorySourceKind sourceKind,
        Guid scopeId,
        string providerVersion,
        out MemorySourceSnapshotCursor? nextCursor,
        out bool hasMore,
        string snapshotAnchor = "")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerVersion);
        var orderedItems = items
            .OrderBy(item => item.Id.Value, StringComparer.Ordinal)
            .ToList();
        var descriptor = MemorySourceSnapshotCursor.ReadDescriptorOrThrow(
            cursor,
            sourceKind,
            scopeId,
            providerVersion);
        var startIndex = descriptor?.Position ?? 0;
        if (descriptor is not null)
        {
            ValidateAnchor(
                orderedItems,
                descriptor,
                cursor!.Value,
                sourceKind,
                scopeId,
                providerVersion);
        }

        var pageSize = NormalizeTake(take);
        var page = orderedItems
            .Skip(startIndex)
            .Take(pageSize)
            .ToList();
        hasMore = startIndex + page.Count < orderedItems.Count;
        nextCursor = hasMore && page.Count > 0
            ? MemorySourceSnapshotCursor.Create(
                sourceKind,
                scopeId,
                providerVersion,
                startIndex + page.Count,
                page[^1].Id,
                snapshotAnchor)
            : null;
        return page;
    }

    public static int NormalizeTake(int? take)
        => Math.Clamp(take ?? DefaultTake, 1, MaxTake);

    private static void ValidateAnchor(
        IReadOnlyList<MemorySourceItem> orderedItems,
        MemorySourceSnapshotCursorDescriptor descriptor,
        MemorySourceSnapshotCursor cursor,
        MemorySourceKind sourceKind,
        Guid scopeId,
        string providerVersion)
    {
        var anchorIndex = descriptor.Position - 1;
        if (anchorIndex < orderedItems.Count &&
            orderedItems[anchorIndex].Id == descriptor.LastItemId)
        {
            return;
        }

        MemorySourceSnapshotCursor.ThrowStaleAnchor(
            cursor,
            sourceKind,
            scopeId,
            providerVersion,
            "Memory source snapshot cursor anchor is stale or no longer matches the ordered source item at the recorded position.");
    }
}
