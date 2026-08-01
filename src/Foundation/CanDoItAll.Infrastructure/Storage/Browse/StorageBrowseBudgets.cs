namespace CanDoItAll.Infrastructure.Storage;

public sealed record StorageBrowseWorkBudget
{
    public const int AbsoluteMaximumReturnedItems = 500;
    public const int AbsoluteMaximumInspectedItems = 100_000;
    public const int AbsoluteMaximumMetadataProbes = 10_000;
    public const int AbsoluteMaximumConcurrentMetadataProbes = 32;

    public static StorageBrowseWorkBudget Default { get; } = new();

    public StorageBrowseWorkBudget(
        int maximumReturnedItems = 100,
        int maximumInspectedItems = 512,
        int maximumMetadataProbes = 100,
        int maximumConcurrentMetadataProbes = 8,
        TimeSpan? maximumDuration = null)
    {
        MaximumReturnedItems = RequireRange(
            maximumReturnedItems,
            1,
            AbsoluteMaximumReturnedItems,
            nameof(maximumReturnedItems));
        MaximumInspectedItems = RequireRange(
            maximumInspectedItems,
            MaximumReturnedItems,
            AbsoluteMaximumInspectedItems,
            nameof(maximumInspectedItems));
        MaximumMetadataProbes = RequireRange(
            maximumMetadataProbes,
            0,
            Math.Min(MaximumInspectedItems, AbsoluteMaximumMetadataProbes),
            nameof(maximumMetadataProbes));
        int maximumProbeConcurrency = MaximumMetadataProbes == 0
            ? 0
            : Math.Min(MaximumMetadataProbes, AbsoluteMaximumConcurrentMetadataProbes);
        MaximumConcurrentMetadataProbes = RequireRange(
            maximumConcurrentMetadataProbes,
            0,
            maximumProbeConcurrency,
            nameof(maximumConcurrentMetadataProbes));
        MaximumDuration = maximumDuration ?? TimeSpan.FromSeconds(5);
        if (MaximumDuration < TimeSpan.FromMilliseconds(50) || MaximumDuration > TimeSpan.FromMinutes(2))
        {
            throw InvalidBudget(nameof(maximumDuration));
        }
    }

    public int MaximumReturnedItems { get; }

    public int MaximumInspectedItems { get; }

    public int MaximumMetadataProbes { get; }

    public int MaximumConcurrentMetadataProbes { get; }

    public TimeSpan MaximumDuration { get; }

    private static int RequireRange(int value, int minimum, int maximum, string parameterName)
    {
        if (value < minimum || value > maximum)
        {
            throw InvalidBudget(parameterName);
        }

        return value;
    }

    private static StorageBrowseException InvalidBudget(string parameterName)
        => new(new StorageBrowseError(
            StorageBrowseErrorCode.InvalidRequest,
            $"Storage browse budget value '{parameterName}' is outside the supported bounded range."));
}

public sealed record StorageBrowseSearchBudget
{
    public static StorageBrowseSearchBudget Default { get; } = new();

    public StorageBrowseSearchBudget(
        int maximumContainers = 100,
        int maximumItems = 10_000,
        int maximumMatches = 500,
        int maximumConcurrency = 4,
        int maximumRetainedSnapshotBytes = 8 * 1024 * 1024,
        TimeSpan? maximumDuration = null)
    {
        if (maximumContainers is < 1 or > 10_000 ||
            maximumItems is < 1 or > 1_000_000 ||
            maximumMatches < 1 ||
            maximumMatches > maximumItems ||
            maximumConcurrency is < 1 or > 32 ||
            maximumRetainedSnapshotBytes is < 1024 or > 128 * 1024 * 1024)
        {
            throw InvalidBudget();
        }

        MaximumDuration = maximumDuration ?? TimeSpan.FromSeconds(10);
        if (MaximumDuration < TimeSpan.FromMilliseconds(50) || MaximumDuration > TimeSpan.FromMinutes(5))
        {
            throw InvalidBudget();
        }

        MaximumContainers = maximumContainers;
        MaximumItems = maximumItems;
        MaximumMatches = maximumMatches;
        MaximumConcurrency = maximumConcurrency;
        MaximumRetainedSnapshotBytes = maximumRetainedSnapshotBytes;
    }

    public int MaximumContainers { get; }

    public int MaximumItems { get; }

    public int MaximumMatches { get; }

    public int MaximumConcurrency { get; }

    public int MaximumRetainedSnapshotBytes { get; }

    public TimeSpan MaximumDuration { get; }

    private static StorageBrowseException InvalidBudget()
        => new(new StorageBrowseError(
            StorageBrowseErrorCode.InvalidRequest,
            "The storage search budget is outside the supported bounded range."));
}

public sealed record StorageBrowseRetentionBudget
{
    public static StorageBrowseRetentionBudget Default { get; } = new();

    public StorageBrowseRetentionBudget(
        int maximumPages = 20,
        int maximumItems = 2_000,
        int maximumContinuations = 20,
        int maximumRetainedBytes = 16 * 1024 * 1024)
    {
        if (maximumPages is < 1 or > 100 ||
            maximumItems is < 1 or > 50_000 ||
            maximumContinuations is < 1 or > 100 ||
            maximumRetainedBytes is < 1024 or > 256 * 1024 * 1024)
        {
            throw new StorageBrowseException(new StorageBrowseError(
                StorageBrowseErrorCode.InvalidRequest,
                "The storage browse retention budget is outside the supported bounded range."));
        }

        MaximumPages = maximumPages;
        MaximumItems = maximumItems;
        MaximumContinuations = maximumContinuations;
        MaximumRetainedBytes = maximumRetainedBytes;
    }

    public int MaximumPages { get; }

    public int MaximumItems { get; }

    public int MaximumContinuations { get; }

    public int MaximumRetainedBytes { get; }
}

