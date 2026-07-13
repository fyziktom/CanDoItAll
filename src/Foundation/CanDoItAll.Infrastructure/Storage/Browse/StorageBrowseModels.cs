namespace CanDoItAll.Infrastructure.Storage;

public sealed record StorageBrowseRequest
{
    public StorageBrowseRequest(
        StorageBrowseContainer container,
        int pageSize = 50,
        StorageBrowseCursor? cursor = null,
        StorageBrowseSort? sort = null,
        StorageBrowseMetadataField metadata = StorageBrowseMetadataField.None,
        StorageBrowseWorkBudget? budget = null)
    {
        Container = container ?? throw new ArgumentNullException(nameof(container));
        Budget = budget ?? StorageBrowseWorkBudget.Default;
        if (pageSize < 1 || pageSize > Budget.MaximumReturnedItems)
        {
            throw new StorageBrowseException(new StorageBrowseError(
                StorageBrowseErrorCode.InvalidRequest,
                "The requested storage browse page size exceeds the request budget."));
        }

        const StorageBrowseMetadataField supportedMetadata =
            StorageBrowseMetadataField.Size |
            StorageBrowseMetadataField.CreatedAtUtc |
            StorageBrowseMetadataField.ModifiedAtUtc |
            StorageBrowseMetadataField.MediaType;
        if ((metadata & ~supportedMetadata) != StorageBrowseMetadataField.None)
        {
            throw new StorageBrowseException(new StorageBrowseError(
                StorageBrowseErrorCode.InvalidRequest,
                "The requested storage browse metadata fields are invalid."));
        }

        if (metadata != StorageBrowseMetadataField.None && Budget.MaximumMetadataProbes == 0)
        {
            throw new StorageBrowseException(new StorageBrowseError(
                StorageBrowseErrorCode.InvalidRequest,
                "Metadata was requested with a zero metadata-probe budget."));
        }

        PageSize = pageSize;
        Cursor = cursor;
        Sort = sort ?? StorageBrowseSort.ProviderOrder;
        Metadata = metadata;
    }

    public StorageBrowseContainer Container { get; }

    public int PageSize { get; }

    public StorageBrowseCursor? Cursor { get; }

    public StorageBrowseSort Sort { get; }

    public StorageBrowseMetadataField Metadata { get; }

    public StorageBrowseWorkBudget Budget { get; }
}

public sealed record StorageBrowseSearchRequest
{
    public StorageBrowseSearchRequest(
        string query,
        StorageBrowseRequest browse,
        StorageBrowseSearchBudget? budget = null)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Length > 512)
        {
            throw new StorageBrowseException(new StorageBrowseError(
                StorageBrowseErrorCode.InvalidRequest,
                "A bounded storage search query is required."));
        }

        Query = query.Trim();
        Browse = browse ?? throw new ArgumentNullException(nameof(browse));
        Budget = budget ?? StorageBrowseSearchBudget.Default;
    }

    public string Query { get; }

    public StorageBrowseRequest Browse { get; }

    public StorageBrowseSearchBudget Budget { get; }
}

public sealed record StorageBrowseStatRequest
{
    public StorageBrowseStatRequest(
        StorageBrowseContainer container,
        StorageBrowseEntryId entryId,
        StorageBrowseMetadataField metadata,
        StorageBrowseWorkBudget budget)
    {
        Container = container ?? throw new ArgumentNullException(nameof(container));
        EntryId = entryId ?? throw new ArgumentNullException(nameof(entryId));
        Budget = budget ?? throw new ArgumentNullException(nameof(budget));
        const StorageBrowseMetadataField supportedMetadata =
            StorageBrowseMetadataField.Size |
            StorageBrowseMetadataField.CreatedAtUtc |
            StorageBrowseMetadataField.ModifiedAtUtc |
            StorageBrowseMetadataField.MediaType;
        if ((metadata & ~supportedMetadata) != StorageBrowseMetadataField.None)
        {
            throw new StorageBrowseException(new StorageBrowseError(
                StorageBrowseErrorCode.InvalidRequest,
                "The requested storage stat metadata fields are invalid."));
        }

        Metadata = metadata;
    }

    public StorageBrowseContainer Container { get; }

    public StorageBrowseEntryId EntryId { get; }

    public StorageBrowseMetadataField Metadata { get; }

    public StorageBrowseWorkBudget Budget { get; }
}

public sealed record StorageBrowsePathSegment
{
    public StorageBrowsePathSegment(string displayName, StorageBrowseContainer container)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new ArgumentException("A storage browse path display name is required.", nameof(displayName));
        }

        DisplayName = displayName.Trim();
        Container = container ?? throw new ArgumentNullException(nameof(container));
    }

    public string DisplayName { get; }

    public StorageBrowseContainer Container { get; }
}

public sealed record StorageBrowseEntry
{
    public StorageBrowseEntry(
        StorageBrowseEntryId id,
        StorageBrowseContainer parent,
        string name,
        string displayPath,
        StorageBrowseEntryKind kind,
        StorageBrowseEntryCapability capabilities,
        long? size = null,
        DateTimeOffset? createdAtUtc = null,
        DateTimeOffset? modifiedAtUtc = null,
        string? mediaType = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("A storage browse entry name is required.", nameof(name));
        }

        if (size < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(size));
        }

        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }

        const StorageBrowseEntryCapability supportedCapabilities =
            StorageBrowseEntryCapability.Browse |
            StorageBrowseEntryCapability.Read |
            StorageBrowseEntryCapability.Write |
            StorageBrowseEntryCapability.Delete;
        if ((capabilities & ~supportedCapabilities) != StorageBrowseEntryCapability.None)
        {
            throw new ArgumentOutOfRangeException(nameof(capabilities));
        }

        Id = id ?? throw new ArgumentNullException(nameof(id));
        Parent = parent ?? throw new ArgumentNullException(nameof(parent));
        Name = name.Trim();
        DisplayPath = displayPath ?? string.Empty;
        Kind = kind;
        Capabilities = capabilities;
        Size = size;
        CreatedAtUtc = createdAtUtc;
        ModifiedAtUtc = modifiedAtUtc;
        MediaType = string.IsNullOrWhiteSpace(mediaType) ? null : mediaType.Trim();
    }

    public StorageBrowseEntryId Id { get; }

    public StorageBrowseContainer Parent { get; }

    public string Name { get; }

    public string DisplayPath { get; }

    public StorageBrowseEntryKind Kind { get; }

    public StorageBrowseEntryCapability Capabilities { get; }

    public long? Size { get; }

    public DateTimeOffset? CreatedAtUtc { get; }

    public DateTimeOffset? ModifiedAtUtc { get; }

    public string? MediaType { get; }
}

public sealed record StorageBrowseOperationMetrics
{
    public StorageBrowseOperationMetrics(
        int returnedItems,
        int inspectedItems,
        int metadataProbes,
        long retainedStateBytes,
        TimeSpan duration)
    {
        if (returnedItems < 0 ||
            inspectedItems < returnedItems ||
            metadataProbes < 0 ||
            metadataProbes > inspectedItems ||
            retainedStateBytes < 0 ||
            duration < TimeSpan.Zero)
        {
            throw new StorageBrowseException(new StorageBrowseError(
                StorageBrowseErrorCode.InvalidRequest,
                "Storage browse operation metrics are inconsistent."));
        }

        ReturnedItems = returnedItems;
        InspectedItems = inspectedItems;
        MetadataProbes = metadataProbes;
        RetainedStateBytes = retainedStateBytes;
        Duration = duration;
    }

    public int ReturnedItems { get; }

    public int InspectedItems { get; }

    public int MetadataProbes { get; }

    public long RetainedStateBytes { get; }

    public TimeSpan Duration { get; }
}

public sealed record StorageBrowsePage
{
    public StorageBrowsePage(
        StorageBrowseContainer container,
        IEnumerable<StorageBrowsePathSegment> path,
        IEnumerable<StorageBrowseEntry> entries,
        StorageBrowseSort appliedSort,
        StorageBrowseCompleteness completeness,
        StorageBrowseOperationMetrics metrics,
        StorageBrowseCursor? nextCursor = null,
        StorageBrowseConsistencyToken? consistency = null)
    {
        Container = container ?? throw new ArgumentNullException(nameof(container));
        Path = path?.ToArray() ?? throw new ArgumentNullException(nameof(path));
        Entries = entries?.ToArray() ?? throw new ArgumentNullException(nameof(entries));
        AppliedSort = appliedSort ?? throw new ArgumentNullException(nameof(appliedSort));
        Metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
        if (Entries.Count != Metrics.ReturnedItems)
        {
            throw new StorageBrowseException(new StorageBrowseError(
                StorageBrowseErrorCode.InvalidRequest,
                "Storage browse page entries do not match the reported returned-item count."));
        }

        if (!Enum.IsDefined(completeness))
        {
            throw new ArgumentOutOfRangeException(nameof(completeness));
        }

        Completeness = completeness;
        NextCursor = nextCursor;
        Consistency = consistency;
    }

    public StorageBrowseContainer Container { get; }

    public IReadOnlyList<StorageBrowsePathSegment> Path { get; }

    public IReadOnlyList<StorageBrowseEntry> Entries { get; }

    public StorageBrowseSort AppliedSort { get; }

    public StorageBrowseCompleteness Completeness { get; }

    public StorageBrowseOperationMetrics Metrics { get; }

    public StorageBrowseCursor? NextCursor { get; }

    public StorageBrowseConsistencyToken? Consistency { get; }
}
