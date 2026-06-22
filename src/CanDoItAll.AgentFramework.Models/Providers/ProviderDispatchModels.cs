namespace CanDoItAll.AgentFramework.Models;

public enum AgentProviderCapabilityKind
{
    ModelCatalog,
    ChatCompletion,
    ImageGeneration,
    SpeechToText,
    TextToSpeech,
    Health,
    ModelMaintenance
}

public enum AgentProviderOperationKind
{
    ListModels,
    CompleteChat,
    GenerateImage,
    EditImage,
    TranscribeSpeech,
    SynthesizeSpeech,
    TestHealth,
    CreateOrUpdateModel
}

public sealed record ProviderDispatchQuery(
    ProviderProfile Provider,
    AgentProviderCapabilityKind Capability,
    AgentProviderOperationKind Operation,
    string Model);

public sealed record ProviderDispatchKey(
    Guid ProviderProfileId,
    ProviderKind ProviderKind,
    AgentProviderCapabilityKind Capability,
    AgentProviderOperationKind Operation,
    string SubdriverKind,
    string Model,
    string Discriminator = "")
{
    public static ProviderDispatchKey FromQuery(
        ProviderDispatchQuery query,
        string subdriverKind,
        string discriminator = "")
    {
        ArgumentNullException.ThrowIfNull(query);

        return new ProviderDispatchKey(
            query.Provider.Id,
            query.Provider.Kind,
            query.Capability,
            query.Operation,
            Normalize(subdriverKind),
            Normalize(query.Model),
            Normalize(discriminator));
    }

    private static string Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim();
    }
}

public sealed record ProviderDispatchLimits(
    bool SupportsBatching,
    int MaxBatchSize,
    int MaxInFlightBatches,
    int MaxQueueDepth,
    TimeSpan MaxQueueDelay,
    TimeSpan RequestTimeout)
{
    public static ProviderDispatchLimits Unbatched(TimeSpan? requestTimeout = null)
    {
        return new ProviderDispatchLimits(
            SupportsBatching: false,
            MaxBatchSize: 1,
            MaxInFlightBatches: 1,
            MaxQueueDepth: 0,
            MaxQueueDelay: TimeSpan.Zero,
            RequestTimeout: requestTimeout ?? TimeSpan.FromMinutes(2));
    }

    public static ProviderDispatchLimits Batched(
        int maxBatchSize,
        int maxInFlightBatches,
        int maxQueueDepth,
        TimeSpan maxQueueDelay,
        TimeSpan requestTimeout)
    {
        if (maxBatchSize < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(maxBatchSize), "Batched dispatch requires a batch size greater than one.");
        }

        if (maxInFlightBatches < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxInFlightBatches), "At least one in-flight batch must be allowed.");
        }

        if (maxQueueDepth < maxBatchSize)
        {
            throw new ArgumentOutOfRangeException(nameof(maxQueueDepth), "Queue depth must be at least the max batch size.");
        }

        if (maxQueueDelay < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(maxQueueDelay), "Queue delay cannot be negative.");
        }

        if (requestTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(requestTimeout), "Request timeout must be positive.");
        }

        return new ProviderDispatchLimits(
            SupportsBatching: true,
            MaxBatchSize: maxBatchSize,
            MaxInFlightBatches: maxInFlightBatches,
            MaxQueueDepth: maxQueueDepth,
            MaxQueueDelay: maxQueueDelay,
            RequestTimeout: requestTimeout);
    }
}
