using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.SharedProviders.Abstractions;

namespace CanDoItAll.Modules.AgentFramework.ProviderManagement;

public enum SharedProviderSourceStatus
{
    NeverSynchronized,
    Available,
    SourceOffline,
    AuthorizationFailed,
    SourceIdentityMismatch,
    IncompatibleContract
}

public enum SharedProviderCatalogIdentityAcceptance
{
    Accepted,
    IdentityMismatch
}

public enum SharedProviderSelectionState
{
    Selected,
    Retired
}

public enum SharedProviderAvailabilityState
{
    Available,
    Unpublished,
    Missing,
    SourceOffline,
    AuthorizationFailed,
    SourceIdentityMismatch,
    IncompatibleContract
}

public enum SharedProviderInvocationOutcome
{
    InProgress,
    Succeeded,
    Failed,
    Cancelled
}

public enum SharedProviderMetadataCompleteness
{
    Unavailable,
    Partial,
    Complete
}

public sealed record SharedProviderInvocationCompletion(
    SharedProviderInvocationOutcome Outcome,
    DateTimeOffset CompletedAtUtc,
    SharedProviderFailureCategory? FailureCategory,
    long? InputTokenCount,
    long? OutputTokenCount,
    SharedProviderMetadataCompleteness UsageCompleteness,
    decimal? Price,
    SharedProviderMetadataCompleteness PricingCompleteness)
{
    public int? ImageCount { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SharedProviderRemotePublicationSnapshot(
    [property: JsonPropertyName("schemaVersion")]
    SharedProviderProtocolVersion SchemaVersion,
    [property: JsonPropertyName("publication")]
    SharedProviderCatalogPublication Publication);

public sealed record SharedProviderRemotePublicationState
{
    public const int MaximumSnapshotBytes = 256 * 1024;

    private SharedProviderRemotePublicationState(
        SharedProviderPublicationId publicationId,
        string displayName,
        SharedProviderPublicRevision revision,
        SharedProviderPurpose purpose,
        SharedProviderTransport transport,
        SharedProviderRoutingModelId defaultModelId,
        string catalogSnapshotJson)
    {
        PublicationId = publicationId;
        DisplayName = displayName;
        Revision = revision;
        Purpose = purpose;
        Transport = transport;
        DefaultModelId = defaultModelId;
        CatalogSnapshotJson = catalogSnapshotJson;
    }

    public SharedProviderPublicationId PublicationId { get; }

    public string DisplayName { get; }

    public SharedProviderPublicRevision Revision { get; }

    public SharedProviderPurpose Purpose { get; }

    public SharedProviderTransport Transport { get; }

    public SharedProviderRoutingModelId DefaultModelId { get; }

    public string CatalogSnapshotJson { get; }

    public static SharedProviderRemotePublicationState Create(
        SharedProviderCatalogPublication publication)
    {
        ArgumentNullException.ThrowIfNull(publication);

        var expectedRevision = SharedProviderCanonicalRevision.ComputePublication(publication);
        if (publication.Revision != expectedRevision)
        {
            throw new ArgumentException(
                "The remote publication revision does not match its public representation.",
                nameof(publication));
        }

        var snapshotJson = JsonSerializer.Serialize(
            new SharedProviderRemotePublicationSnapshot(
                SharedProviderProtocolVersion.Current,
                publication),
            SharedProviderProtocolJson.Options);
        if (Encoding.UTF8.GetByteCount(snapshotJson) > MaximumSnapshotBytes)
        {
            throw new ArgumentException(
                $"The remote publication snapshot exceeds {MaximumSnapshotBytes} UTF-8 bytes.",
                nameof(publication));
        }

        return new SharedProviderRemotePublicationState(
            publication.PublicationId,
            publication.DisplayName,
            publication.Revision,
            publication.Purpose,
            publication.Transport,
            publication.DefaultModelId,
            snapshotJson);
    }
}
