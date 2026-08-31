using CanDoItAll.AgentFramework.Models;
using CanDoItAll.SharedProviders.Abstractions;

namespace CanDoItAll.Modules.AgentFramework.ProviderManagement;

using AgentFrameworkProviderKind = CanDoItAll.AgentFramework.Models.ProviderKind;
using PersistedProviderKind = CanDoItAll.Modules.AgentFramework.ProviderManagement.ProviderKind;
using PersistedProviderProfile = CanDoItAll.Modules.AgentFramework.ProviderManagement.ProviderProfile;

public enum SharedProviderRuntimeProfileAvailability
{
    Available,
    ProviderProfileMissing,
    ImportMissing,
    SourceMissing,
    RelationshipMismatch,
    SourceConfigurationInvalid,
    ProfileCacheIntegrityMismatch,
    SnapshotInvalid,
    LocalProfileDisabled,
    SourceDisabled,
    SourceNeverSynchronized,
    SourceOffline,
    AuthorizationFailed,
    SourceIdentityMismatch,
    IncompatibleContract,
    Retired,
    Unpublished,
    Missing,
    PublicationUnavailable
}

public sealed record SharedProviderEffectiveRuntimeProfile(
    Guid ProviderProfileId,
    string Name,
    AgentFrameworkProviderKind Kind,
    Uri BaseUri,
    Guid SourceTokenSecretReferenceId,
    SharedProviderSourceNetworkPolicy NetworkPolicy,
    Guid SourceId,
    SharedProviderSourceInstanceId SourceInstanceId,
    Guid ImportId,
    SharedProviderPublicationId PublicationId,
    SharedProviderPublicRevision Revision,
    SharedProviderRoutingModelId DefaultModelId,
    ProviderTransportKind Transport,
    ProviderProfilePurpose Purpose,
    bool IsEnabled,
    int TimeoutSeconds,
    bool SupportsStreaming,
    bool SupportsTools,
    bool SupportsParallelTools,
    bool SupportsStructuredOutput,
    bool SupportsVision,
    bool SupportsBase64Images,
    bool PreferFrameworkManagedChatHistory,
    bool SupportsBackgroundResponses,
    IReadOnlyList<SharedProviderCatalogModel> Models,
    string ConnectorPluginKey,
    IReadOnlyList<string> Tags) {
    public bool IsPrivateProvider { get; init; }
}

public sealed record SharedProviderRuntimeProfileMaterializationResult
{
    private SharedProviderRuntimeProfileMaterializationResult(
        SharedProviderRuntimeProfileAvailability availability,
        SharedProviderEffectiveRuntimeProfile? profile)
    {
        Availability = availability;
        Profile = profile;
    }

    public SharedProviderRuntimeProfileAvailability Availability { get; }

    public SharedProviderEffectiveRuntimeProfile? Profile { get; }

    public bool IsAvailable =>
        Availability == SharedProviderRuntimeProfileAvailability.Available &&
        Profile is not null;

    internal static SharedProviderRuntimeProfileMaterializationResult Available(
        SharedProviderEffectiveRuntimeProfile profile)
        => new(
            SharedProviderRuntimeProfileAvailability.Available,
            profile ?? throw new ArgumentNullException(nameof(profile)));

    internal static SharedProviderRuntimeProfileMaterializationResult Unavailable(
        SharedProviderRuntimeProfileAvailability availability,
        SharedProviderEffectiveRuntimeProfile? profile = null)
    {
        if (availability == SharedProviderRuntimeProfileAvailability.Available)
        {
            throw new ArgumentOutOfRangeException(
                nameof(availability),
                availability,
                "An unavailable result requires a non-available reason.");
        }

        return new SharedProviderRuntimeProfileMaterializationResult(
            availability,
            profile);
    }
}

public sealed class SharedProviderRuntimeProfileMaterializer
{
    private const int MaximumProfileNameLength = 200;
    private const int MaximumSourceAddressLength = 2_048;

    public SharedProviderRuntimeProfileMaterializationResult Materialize(
        PersistedProviderProfile? profile,
        SharedProviderImport? import,
        SharedProviderSource? source) {
        var (availability, shape) = Validate(profile, import, source);
        if (shape is null) {
            return Unavailable(availability);
        }

        profile = shape.Profile;
        import = shape.Import;
        source = shape.Source;
        var publication = shape.Publication;
        var baseUri = shape.BaseUri;
        var sourceInstanceId = shape.SourceInstanceId;
        var transport = shape.Transport;
        var purpose = shape.Purpose;
        var models = CopyModels(publication.Models);
        var effectiveProfile = new SharedProviderEffectiveRuntimeProfile(
            profile.Id,
            profile.Name,
            AgentFrameworkProviderKind.OpenAi,
            baseUri,
            source.ApiTokenSecretId,
            source.AllowInsecurePrivateNetwork
                ? SharedProviderSourceNetworkPolicy.AllowPrivateNetwork
                : SharedProviderSourceNetworkPolicy.PublicOnly,
            source.Id,
            sourceInstanceId,
            import.Id,
            publication.PublicationId,
            publication.Revision,
            publication.DefaultModelId,
            transport,
            purpose,
            profile.IsEnabled,
            profile.TimeoutSeconds,
            SupportsEveryModel(publication, SharedProviderCapability.Streaming),
            SupportsEveryModel(publication, SharedProviderCapability.FunctionTools),
            SupportsEveryModel(publication, SharedProviderCapability.ParallelFunctionTools),
            SupportsEveryModel(publication, SharedProviderCapability.StructuredOutput),
            SupportsEveryModel(publication, SharedProviderCapability.VisionInput),
            SupportsEveryModel(publication, SharedProviderCapability.Base64Json),
            true,
            false,
            models,
            SharedProviderReconciliationCoordinator.ImportedConnectorPluginKey,
            BuildTags(source, publication, transport, purpose, availability)) {
            IsPrivateProvider = publication.IsPrivateProvider
        };
        return availability == SharedProviderRuntimeProfileAvailability.Available
            ? SharedProviderRuntimeProfileMaterializationResult.Available(
                effectiveProfile)
            : SharedProviderRuntimeProfileMaterializationResult.Unavailable(
                availability,
                effectiveProfile);
    }

    internal (SharedProviderRuntimeProfileAvailability Availability, SharedProviderValidatedRuntimeShape? Shape) Validate(
        PersistedProviderProfile? profile,
        SharedProviderImport? import,
        SharedProviderSource? source) {
        if (profile is null) {
            return (
                SharedProviderRuntimeProfileAvailability.ProviderProfileMissing, null);
        }

        if (import is null) {
            return (SharedProviderRuntimeProfileAvailability.ImportMissing, null);
        }

        if (source is null) {
            return (SharedProviderRuntimeProfileAvailability.SourceMissing, null);
        }

        if (!HasValidRelationship(profile, import, source)) {
            return (
                SharedProviderRuntimeProfileAvailability.RelationshipMismatch, null);
        }

        if (!TryResolveSourceBaseUri(source, out var sourceBaseUri) ||
            source.ApiTokenSecretId == Guid.Empty) {
            return (
                SharedProviderRuntimeProfileAvailability.SourceConfigurationInvalid, null);
        }

        var sourceStatusAvailability = ResolveSourceStatusAvailability(source);
        if (sourceStatusAvailability is
            SharedProviderRuntimeProfileAvailability.SourceNeverSynchronized or
            SharedProviderRuntimeProfileAvailability.SourceConfigurationInvalid) {
            return (sourceStatusAvailability, null);
        }

        if (source.RemoteInstanceId is not { } sourceInstanceId ||
            sourceInstanceId.Value == Guid.Empty) {
            return (
                SharedProviderRuntimeProfileAvailability.SourceConfigurationInvalid, null);
        }

        var importAvailability = ResolveImportAvailability(import);
        if (importAvailability ==
            SharedProviderRuntimeProfileAvailability.RelationshipMismatch) {
            return (importAvailability, null);
        }

        if (!SharedProviderPublicationSnapshotReader.TryRead(import, out var publication) ||
            !TryResolveRuntimeShape(
                publication,
                out var transport,
                out var purpose)) {
            return (
                SharedProviderRuntimeProfileAvailability.SnapshotInvalid, null);
        }

        var baseUri = SharedProviderRoutes.ResolveOpenAiBase(sourceBaseUri);
        if (!HasValidProfileCache(profile, source, publication, baseUri)) {
            return (
                SharedProviderRuntimeProfileAvailability.ProfileCacheIntegrityMismatch, null);
        }

        var availability = ResolveOperationalAvailability(
            profile,
            source,
            sourceStatusAvailability,
            importAvailability,
            publication);
        return (availability, new SharedProviderValidatedRuntimeShape(
            profile, import, source, publication, baseUri, sourceInstanceId, transport, purpose));
    }

    private static SharedProviderRuntimeProfileMaterializationResult Unavailable(
        SharedProviderRuntimeProfileAvailability availability)
        => SharedProviderRuntimeProfileMaterializationResult.Unavailable(availability);

    private static bool HasValidRelationship(
        PersistedProviderProfile profile,
        SharedProviderImport import,
        SharedProviderSource source)
        => profile.Id != Guid.Empty &&
            import.Id != Guid.Empty &&
            source.Id != Guid.Empty &&
            import.ProviderProfileId == profile.Id &&
            import.SourceId == source.Id;

    private static bool TryResolveSourceBaseUri(
        SharedProviderSource source,
        out Uri sourceBaseUri)
    {
        sourceBaseUri = null!;
        if (source.BaseUri is not { Length: > 0 and <= MaximumSourceAddressLength } ||
            !Uri.TryCreate(source.BaseUri, UriKind.Absolute, out var parsed) ||
            (!string.Equals(
                    parsed.Scheme,
                    Uri.UriSchemeHttp,
                    StringComparison.Ordinal) &&
                !string.Equals(
                    parsed.Scheme,
                    Uri.UriSchemeHttps,
                    StringComparison.Ordinal)) ||
            string.IsNullOrEmpty(parsed.Host) ||
            !string.IsNullOrEmpty(parsed.UserInfo) ||
            !string.IsNullOrEmpty(parsed.Query) ||
            !string.IsNullOrEmpty(parsed.Fragment) ||
            parsed.Scheme == Uri.UriSchemeHttp &&
                !source.AllowInsecurePrivateNetwork &&
                !parsed.IsLoopback)
        {
            return false;
        }

        var canonical = parsed.GetLeftPart(UriPartial.Path);
        if (!canonical.EndsWith("/", StringComparison.Ordinal))
        {
            canonical += '/';
        }

        if (!string.Equals(canonical, source.BaseUri, StringComparison.Ordinal))
        {
            return false;
        }

        sourceBaseUri = parsed;
        return true;
    }

    private static SharedProviderRuntimeProfileAvailability ResolveSourceStatusAvailability(
        SharedProviderSource source)
    {
        return source.Status switch
        {
            SharedProviderSourceStatus.Available =>
                SharedProviderRuntimeProfileAvailability.Available,
            SharedProviderSourceStatus.NeverSynchronized =>
                SharedProviderRuntimeProfileAvailability.SourceNeverSynchronized,
            SharedProviderSourceStatus.SourceOffline =>
                SharedProviderRuntimeProfileAvailability.SourceOffline,
            SharedProviderSourceStatus.AuthorizationFailed =>
                SharedProviderRuntimeProfileAvailability.AuthorizationFailed,
            SharedProviderSourceStatus.SourceIdentityMismatch =>
                SharedProviderRuntimeProfileAvailability.SourceIdentityMismatch,
            SharedProviderSourceStatus.IncompatibleContract =>
                SharedProviderRuntimeProfileAvailability.IncompatibleContract,
            _ => SharedProviderRuntimeProfileAvailability.SourceConfigurationInvalid
        };
    }

    private static SharedProviderRuntimeProfileAvailability ResolveImportAvailability(
        SharedProviderImport import)
    {
        if (import.SelectionState == SharedProviderSelectionState.Retired)
        {
            return SharedProviderRuntimeProfileAvailability.Retired;
        }

        if (import.SelectionState != SharedProviderSelectionState.Selected)
        {
            return SharedProviderRuntimeProfileAvailability.RelationshipMismatch;
        }

        return import.AvailabilityState switch
        {
            SharedProviderAvailabilityState.Available =>
                SharedProviderRuntimeProfileAvailability.Available,
            SharedProviderAvailabilityState.Unpublished =>
                SharedProviderRuntimeProfileAvailability.Unpublished,
            SharedProviderAvailabilityState.Missing =>
                SharedProviderRuntimeProfileAvailability.Missing,
            SharedProviderAvailabilityState.SourceOffline =>
                SharedProviderRuntimeProfileAvailability.SourceOffline,
            SharedProviderAvailabilityState.AuthorizationFailed =>
                SharedProviderRuntimeProfileAvailability.AuthorizationFailed,
            SharedProviderAvailabilityState.SourceIdentityMismatch =>
                SharedProviderRuntimeProfileAvailability.SourceIdentityMismatch,
            SharedProviderAvailabilityState.IncompatibleContract =>
                SharedProviderRuntimeProfileAvailability.IncompatibleContract,
            _ => SharedProviderRuntimeProfileAvailability.RelationshipMismatch
        };
    }

    private static SharedProviderRuntimeProfileAvailability ResolveOperationalAvailability(
        PersistedProviderProfile profile,
        SharedProviderSource source,
        SharedProviderRuntimeProfileAvailability sourceStatusAvailability,
        SharedProviderRuntimeProfileAvailability importAvailability,
        SharedProviderCatalogPublication publication)
    {
        if (!profile.IsEnabled)
        {
            return SharedProviderRuntimeProfileAvailability.LocalProfileDisabled;
        }

        if (!source.IsEnabled)
        {
            return SharedProviderRuntimeProfileAvailability.SourceDisabled;
        }

        if (sourceStatusAvailability !=
            SharedProviderRuntimeProfileAvailability.Available)
        {
            return sourceStatusAvailability;
        }

        if (importAvailability != SharedProviderRuntimeProfileAvailability.Available)
        {
            return importAvailability;
        }

        return publication.Health.State == SharedProviderHealthState.Unavailable
            ? SharedProviderRuntimeProfileAvailability.PublicationUnavailable
            : SharedProviderRuntimeProfileAvailability.Available;
    }

    private static bool TryResolveRuntimeShape(
        SharedProviderCatalogPublication publication,
        out ProviderTransportKind transport,
        out ProviderProfilePurpose purpose)
    {
        if (publication.Purpose == SharedProviderPurpose.ImageGeneration)
        {
            transport = ProviderTransportKind.Responses;
            purpose = ProviderProfilePurpose.ImageGeneration;
            return publication.Models.All(model =>
                model.Capabilities.Contains(
                    SharedProviderCapability.ImageGenerations));
        }

        purpose = ProviderProfilePurpose.Chat;
        var operationKinds = publication.Models
            .Select(model =>
            {
                var hasResponses = model.Capabilities.Contains(
                    SharedProviderCapability.Responses);
                var hasChatCompletions = model.Capabilities.Contains(
                    SharedProviderCapability.ChatCompletions);
                return (hasResponses, hasChatCompletions) switch
                {
                    (true, false) => ProviderTransportKind.Responses,
                    (false, true) => ProviderTransportKind.ChatCompletions,
                    _ => (ProviderTransportKind?)null
                };
            })
            .Distinct()
            .ToArray();
        if (operationKinds is not [{ } operationKind])
        {
            transport = default;
            return false;
        }

        transport = operationKind;
        return true;
    }

    private static bool HasValidProfileCache(
        PersistedProviderProfile profile,
        SharedProviderSource source,
        SharedProviderCatalogPublication publication,
        Uri expectedBaseUri)
    {
        var defaultModel = publication.Models.SingleOrDefault(model =>
            model.Id == publication.DefaultModelId);
        if (defaultModel is null)
        {
            return false;
        }

        var capabilities = defaultModel.Capabilities;
        return profile.Name is
                { Length: > 0 and <= MaximumProfileNameLength } &&
            profile.Name == profile.Name.Trim() &&
            !profile.Name.Any(char.IsControl) &&
            profile.ProviderKind == PersistedProviderKind.OpenAi &&
            string.Equals(
                profile.ConnectorPluginKey,
                SharedProviderReconciliationCoordinator.ImportedConnectorPluginKey,
                StringComparison.Ordinal) &&
            string.Equals(
                profile.ConfigSchemaVersion,
                SharedProviderReconciliationCoordinator.ImportedConfigurationSchemaVersion,
                StringComparison.Ordinal) &&
            string.Equals(
                profile.BaseUrl,
                expectedBaseUri.AbsoluteUri,
                StringComparison.Ordinal) &&
            profile.ApiKeySecretId == source.ApiTokenSecretId &&
            string.Equals(
                profile.DefaultModel,
                publication.DefaultModelId.Value,
                StringComparison.Ordinal) &&
            profile.TimeoutSeconds >= 5 &&
            profile.SupportsStreaming == capabilities.Contains(
                SharedProviderCapability.Streaming) &&
            profile.SupportsToolCalling == capabilities.Contains(
                SharedProviderCapability.FunctionTools) &&
            profile.SupportsStructuredOutput == capabilities.Contains(
                SharedProviderCapability.StructuredOutput) &&
            profile.SupportsVision == capabilities.Contains(
                SharedProviderCapability.VisionInput);
    }

    private static bool SupportsEveryModel(
        SharedProviderCatalogPublication publication,
        SharedProviderCapability capability)
        => publication.Models.All(model => model.Capabilities.Contains(capability));

    private static IReadOnlyList<SharedProviderCatalogModel> CopyModels(
        IEnumerable<SharedProviderCatalogModel> models)
        => Array.AsReadOnly(models
            .OrderBy(model => model.Id.Value, StringComparer.Ordinal)
            .Select(model => model with
            {
                Thinking = model.Thinking?.Snapshot(),
                Capabilities = Array.AsReadOnly(model.Capabilities
                    .OrderBy(capability => capability)
                    .ToArray())
            })
            .ToArray());

    private static IReadOnlyList<string> BuildTags(
        SharedProviderSource source,
        SharedProviderCatalogPublication publication,
        ProviderTransportKind transport,
        ProviderProfilePurpose purpose,
        SharedProviderRuntimeProfileAvailability availability)
        => Array.AsReadOnly(new[]
        {
            AvailabilityTag(availability),
            purpose == ProviderProfilePurpose.Chat ? "chat" : "image-generation",
            $"publication:{publication.PublicationId}",
            "shared",
            $"source:{source.Id:D}",
            transport == ProviderTransportKind.Responses
                ? "responses"
                : "chat-completions"
        }.Order(StringComparer.Ordinal).ToArray());

    private static string AvailabilityTag(
        SharedProviderRuntimeProfileAvailability availability)
        => availability switch
        {
            SharedProviderRuntimeProfileAvailability.Available => "available",
            SharedProviderRuntimeProfileAvailability.LocalProfileDisabled =>
                "local-profile-disabled",
            SharedProviderRuntimeProfileAvailability.SourceDisabled =>
                "source-disabled",
            SharedProviderRuntimeProfileAvailability.SourceOffline =>
                "source-offline",
            SharedProviderRuntimeProfileAvailability.AuthorizationFailed =>
                "authorization-failed",
            SharedProviderRuntimeProfileAvailability.SourceIdentityMismatch =>
                "source-identity-mismatch",
            SharedProviderRuntimeProfileAvailability.IncompatibleContract =>
                "incompatible-contract",
            SharedProviderRuntimeProfileAvailability.Retired => "retired",
            SharedProviderRuntimeProfileAvailability.Unpublished => "unpublished",
            SharedProviderRuntimeProfileAvailability.Missing => "missing",
            SharedProviderRuntimeProfileAvailability.PublicationUnavailable =>
                "publication-unavailable",
            _ => throw new ArgumentOutOfRangeException(
                nameof(availability),
                availability,
                "Only a structurally valid runtime projection can have tags.")
        };
}
