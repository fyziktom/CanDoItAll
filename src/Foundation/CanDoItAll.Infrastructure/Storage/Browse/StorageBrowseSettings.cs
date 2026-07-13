namespace CanDoItAll.Infrastructure.Storage;

public enum StorageBrowseCacheMode
{
    Disabled,
    Memory,
    Hybrid
}

public enum StorageBrowseImmutableVersionPolicy
{
    None,
    RequireProviderVerifiedVersion
}

public sealed class StorageBrowseCacheSettings
{
    public const int AbsoluteMaximumEntries = 10_000;
    public const int AbsoluteMaximumItems = 50_000;
    public const int AbsoluteMaximumContinuations = 10_000;
    public const long AbsoluteMaximumPayloadBytes = 16L * 1024 * 1024;
    public const long AbsoluteMaximumRetainedBytes = 256L * 1024 * 1024;

    public bool Enabled { get; set; }

    public StorageBrowseCacheMode Mode { get; set; } = StorageBrowseCacheMode.Disabled;

    public TimeSpan TimeToLive { get; set; } = TimeSpan.Zero;

    public TimeSpan MaximumLifetime { get; set; } = TimeSpan.Zero;

    public int MaximumPageSize { get; set; } = 100;

    public int MaximumItems { get; set; } = 2_000;

    public int MaximumEntries { get; set; } = 256;

    public int MaximumContinuations { get; set; } = 256;

    public long MaximumPayloadBytes { get; set; } = 1024 * 1024;

    public long MaximumRetainedBytes { get; set; } = 32L * 1024 * 1024;

    public bool AllowForceRefresh { get; set; } = true;

    public StorageBrowseImmutableVersionPolicy ImmutableVersionPolicy { get; set; }

    public void Validate()
    {
        if (!Enum.IsDefined(Mode) || !Enum.IsDefined(ImmutableVersionPolicy))
        {
            throw Invalid("The storage browse cache mode or immutable-version policy is invalid.");
        }

        if (MaximumPageSize is < 1 or > StorageBrowseWorkBudget.AbsoluteMaximumReturnedItems)
        {
            throw Invalid("Storage browse cache page size is outside the supported bounded range.");
        }

        if (MaximumItems < MaximumPageSize || MaximumItems > AbsoluteMaximumItems)
        {
            throw Invalid("Storage browse cache item capacity is outside the supported bounded range.");
        }

        if (MaximumEntries is < 1 or > AbsoluteMaximumEntries ||
            MaximumContinuations < 0 ||
            MaximumContinuations > MaximumEntries ||
            MaximumPayloadBytes is < 4096 or > AbsoluteMaximumPayloadBytes ||
            MaximumRetainedBytes < MaximumPayloadBytes ||
            MaximumRetainedBytes > AbsoluteMaximumRetainedBytes)
        {
            throw Invalid("Storage browse cache retention limits are outside the supported bounded range.");
        }

        if (!Enabled)
        {
            if (Mode != StorageBrowseCacheMode.Disabled ||
                TimeToLive != TimeSpan.Zero ||
                MaximumLifetime != TimeSpan.Zero ||
                ImmutableVersionPolicy != StorageBrowseImmutableVersionPolicy.None)
            {
                throw Invalid("Disabled storage browse caching cannot retain an enabled cache policy.");
            }

            return;
        }

        if (Mode == StorageBrowseCacheMode.Disabled)
        {
            throw Invalid("Enabled storage browse caching requires an explicit supported cache mode.");
        }

        if (Mode == StorageBrowseCacheMode.Hybrid)
        {
            throw Invalid("Hybrid storage browse caching requires a durable shared revision system and is not supported.");
        }

        if (TimeToLive < TimeSpan.FromSeconds(1) || TimeToLive > TimeSpan.FromHours(1))
        {
            throw Invalid("Storage browse cache TTL must be between one second and one hour.");
        }

        if (MaximumLifetime < TimeToLive || MaximumLifetime > TimeSpan.FromHours(24))
        {
            throw Invalid("Storage browse cache maximum lifetime must include the TTL and be no more than 24 hours.");
        }
    }

    private static StorageBrowseException Invalid(string message)
        => new(new StorageBrowseError(StorageBrowseErrorCode.InvalidConfiguration, message));
}
