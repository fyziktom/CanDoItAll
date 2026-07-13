using CanDoItAll.Infrastructure.Storage;
using System.Text.Json;

namespace CanDoItAll.FileTools.Integration;

internal sealed record CachedStorageBrowsePage(
    string Container,
    CachedStorageBrowsePathSegment[] Path,
    CachedStorageBrowseEntry[] Entries,
    StorageBrowseSortField SortField,
    StorageBrowseSortDirection SortDirection,
    StorageBrowseCompleteness Completeness,
    CachedStorageBrowseMetrics Metrics,
    string? NextCursor,
    string? Consistency)
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public static CachedStorageBrowsePage From(StorageBrowsePage page)
    {
        ArgumentNullException.ThrowIfNull(page);
        return new CachedStorageBrowsePage(
            page.Container.Key,
            page.Path.Select(segment => new CachedStorageBrowsePathSegment(
                segment.DisplayName,
                segment.Container.Key)).ToArray(),
            page.Entries.Select(entry => new CachedStorageBrowseEntry(
                entry.Id.Value,
                entry.Parent.Key,
                entry.Name,
                entry.DisplayPath,
                entry.Kind,
                entry.Capabilities,
                entry.Size,
                entry.CreatedAtUtc,
                entry.ModifiedAtUtc,
                entry.MediaType)).ToArray(),
            page.AppliedSort.Field,
            page.AppliedSort.Direction,
            page.Completeness,
            new CachedStorageBrowseMetrics(
                page.Metrics.ReturnedItems,
                page.Metrics.InspectedItems,
                page.Metrics.MetadataProbes,
                page.Metrics.RetainedStateBytes,
                page.Metrics.Duration.Ticks),
            page.NextCursor?.Token,
            page.Consistency?.Value);
    }

    public StorageBrowsePage ToPage()
        => new(
            new StorageBrowseContainer(Container),
            Path.Select(segment => new StorageBrowsePathSegment(
                segment.DisplayName,
                new StorageBrowseContainer(segment.Container))),
            Entries.Select(entry => new StorageBrowseEntry(
                new StorageBrowseEntryId(entry.Id),
                new StorageBrowseContainer(entry.Parent),
                entry.Name,
                entry.DisplayPath,
                entry.Kind,
                entry.Capabilities,
                entry.Size,
                entry.CreatedAtUtc,
                entry.ModifiedAtUtc,
                entry.MediaType)),
            new StorageBrowseSort(SortField, SortDirection),
            Completeness,
            new StorageBrowseOperationMetrics(
                Metrics.ReturnedItems,
                Metrics.InspectedItems,
                Metrics.MetadataProbes,
                Metrics.RetainedStateBytes,
                TimeSpan.FromTicks(Metrics.DurationTicks)),
            string.IsNullOrWhiteSpace(NextCursor) ? null : new StorageBrowseCursor(NextCursor),
            string.IsNullOrWhiteSpace(Consistency) ? null : new StorageBrowseConsistencyToken(Consistency));

    public byte[] Serialize() => JsonSerializer.SerializeToUtf8Bytes(this, SerializerOptions);

    public static CachedStorageBrowsePage Deserialize(byte[] payload)
        => JsonSerializer.Deserialize<CachedStorageBrowsePage>(payload, SerializerOptions)
            ?? throw new InvalidOperationException("The cached storage browse page is empty.");
}

internal sealed record CachedStorageBrowsePathSegment(string DisplayName, string Container);

internal sealed record CachedStorageBrowseEntry(
    string Id,
    string Parent,
    string Name,
    string DisplayPath,
    StorageBrowseEntryKind Kind,
    StorageBrowseEntryCapability Capabilities,
    long? Size,
    DateTimeOffset? CreatedAtUtc,
    DateTimeOffset? ModifiedAtUtc,
    string? MediaType);

internal sealed record CachedStorageBrowseMetrics(
    int ReturnedItems,
    int InspectedItems,
    int MetadataProbes,
    long RetainedStateBytes,
    long DurationTicks);
