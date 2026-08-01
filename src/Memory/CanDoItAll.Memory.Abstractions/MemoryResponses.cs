namespace CanDoItAll.Memory.Abstractions;

public sealed record MemoryContextPack(
    MemoryContextPackId ContextPackId,
    string Summary,
    IReadOnlyList<MemoryContextSection> Sections,
    IReadOnlyList<MemoryWarning> Warnings,
    decimal ProviderConfidence,
    MemoryFeedbackHandle? FeedbackHandle);

public sealed record MemoryContextSection(
    string Title,
    string Text,
    IReadOnlyList<MemoryCitation> Citations,
    decimal Confidence);

public sealed record MemoryCitation(
    string SourceRef,
    string Label);

public sealed record MemoryWarning(
    MemoryWarningKind Kind,
    string Message);

public sealed record MemoryOperationAccepted(
    MemoryOperationId OperationId,
    string StatusPath,
    DateTimeOffset ExpiresAtUtc,
    TimeSpan PollAfter,
    bool CallbackAvailable);

public sealed record MemoryOperationResult(
    MemoryOperationId OperationId,
    MemoryOperationStatus Status,
    MemoryPayload? Output,
    IReadOnlyList<MemoryWarning> Warnings,
    IReadOnlyList<MemoryFeedbackHandle> FeedbackHandles,
    IReadOnlyList<string> SourceRefs);

public sealed record MemoryProviderEvent(
    MemoryProviderEventId EventId,
    MemoryProviderEventKind EventKind,
    MemoryCorrelationId CorrelationId,
    MemoryCausationId CausationId,
    string Message,
    MemoryPayload Payload);

public sealed record MemoryProviderHealth(
    MemoryProviderHealthStatus Status,
    string? LastErrorCategory,
    MemoryProviderManifest CapabilitySnapshot);

public sealed record MemoryProviderManifest(
    MemoryProviderKind ProviderKind,
    MemoryProtocolVersion ProtocolVersion,
    IReadOnlyList<MemoryCapabilityDescriptor> Capabilities,
    MemoryProviderInteractionSupport InteractionSupport,
    IReadOnlyList<MemoryProviderUiSurface> UiSurfaces,
    MemoryProviderLimits Limits,
    MemoryExtensionData Extensions);

public sealed record MemoryCapabilityDescriptor(
    MemoryCapabilityId Id,
    string Version,
    bool Supported);

public sealed record MemoryProviderInteractionSupport(
    bool SupportsSynchronousQueries,
    bool SupportsAsynchronousOperations,
    bool SupportsSourceRequests,
    bool SupportsFeedback,
    bool SupportsProviderEvents)
{
    public static readonly MemoryProviderInteractionSupport SyncQueryOnly =
        new(
            SupportsSynchronousQueries: true,
            SupportsAsynchronousOperations: false,
            SupportsSourceRequests: false,
            SupportsFeedback: false,
            SupportsProviderEvents: false);
}

public enum MemoryProviderUiSurfaceKind
{
    RazorComponentLibrary = 0,
    Iframe = 1,
    ExternalUrl = 2
}

public sealed record MemoryProviderUiSurface(
    MemoryProviderUiSurfaceKind Kind,
    string Name,
    string? ComponentKey,
    string? UrlSettingKey,
    MemoryCapabilityId CapabilityId);

public sealed record MemoryProviderLimits
{
    public MemoryProviderLimits(
        int maxContextSections,
        int maxSourceItems,
        int maxInFlightOperations,
        TimeSpan operationTimeout)
    {
        if (maxContextSections < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxContextSections), "At least one context section must be allowed.");
        }

        if (maxSourceItems < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxSourceItems), "Source item limit cannot be negative.");
        }

        if (maxInFlightOperations < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxInFlightOperations), "At least one in-flight operation must be allowed.");
        }

        if (operationTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(operationTimeout), "Operation timeout must be positive.");
        }

        MaxContextSections = maxContextSections;
        MaxSourceItems = maxSourceItems;
        MaxInFlightOperations = maxInFlightOperations;
        OperationTimeout = operationTimeout;
    }

    public static readonly MemoryProviderLimits Default =
        new(
            maxContextSections: 12,
            maxSourceItems: 100,
            maxInFlightOperations: 4,
            operationTimeout: TimeSpan.FromMinutes(2));

    public int MaxContextSections { get; }

    public int MaxSourceItems { get; }

    public int MaxInFlightOperations { get; }

    public TimeSpan OperationTimeout { get; }
}
