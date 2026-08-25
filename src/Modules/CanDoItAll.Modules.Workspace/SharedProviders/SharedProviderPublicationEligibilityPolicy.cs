using CanDoItAll.AgentFramework.Models;
using CanDoItAll.SharedProviders.Abstractions;

namespace CanDoItAll.Modules.Workspace;

using AgentFrameworkProviderKind = CanDoItAll.AgentFramework.Models.ProviderKind;

public enum SharedProviderPublicationEligibilityCode
{
    Eligible,
    ProfileDisabled,
    NonProductionProfile,
    ConnectorUnavailable,
    ProfileInvalid,
    SecretReferenceMissing,
    MetadataInvalid,
    RelayUnsupported
}

public sealed record SharedProviderEligibleModel(
    string UpstreamModelId,
    IReadOnlyList<SharedProviderCapability> Capabilities);

public sealed record SharedProviderPublicationEligibility(
    SharedProviderPublicationEligibilityCode Code,
    string SanitizedReason,
    SharedProviderPurpose? Purpose,
    SharedProviderTransport? Transport,
    IReadOnlyList<SharedProviderEligibleModel> Models)
{
    public bool IsEligible => Code == SharedProviderPublicationEligibilityCode.Eligible;
}

public sealed class SharedProviderPublicationEligibilityException(
    Guid providerProfileId,
    SharedProviderPublicationEligibility eligibility)
    : InvalidOperationException(
        $"Provider profile '{providerProfileId:D}' cannot be published: {eligibility.SanitizedReason}")
{
    public Guid ProviderProfileId { get; } = providerProfileId;

    public SharedProviderPublicationEligibility Eligibility { get; } = eligibility;
}

public sealed class SharedProviderPublicationEligibilityPolicy(
    ISharedProviderRelaySupportCatalog relaySupportCatalog)
{
    public SharedProviderPublicationEligibility Evaluate(
        ProviderProfile profile,
        ConnectorPluginManifest? connectorManifest,
        bool requiredSecretExists)
    {
        ArgumentNullException.ThrowIfNull(profile);

        if (!profile.IsEnabled)
        {
            return Failure(
                SharedProviderPublicationEligibilityCode.ProfileDisabled,
                "The provider profile must be enabled before it can be published.");
        }

        if (IsExplicitlyNonProduction(profile))
        {
            return Failure(
                SharedProviderPublicationEligibilityCode.NonProductionProfile,
                "Synthetic, imported, fallback, and non-production provider profiles cannot be published.");
        }

        if (connectorManifest is null ||
            !string.Equals(
                profile.ConnectorPluginKey,
                connectorManifest.PluginKey,
                StringComparison.Ordinal) ||
            !connectorManifest.Capabilities.HasFlag(ConnectorManifestCapability.ProviderExecution))
        {
            return Failure(
                SharedProviderPublicationEligibilityCode.ConnectorUnavailable,
                "The provider connector is unavailable or does not support provider execution.");
        }

        if (!IsBasicProfileValid(profile, connectorManifest))
        {
            return Failure(
                SharedProviderPublicationEligibilityCode.ProfileInvalid,
                "The provider profile has an invalid name, connector schema, endpoint, or timeout configuration.");
        }

        var requiresSecret = connectorManifest.SecretRequirements.Any(requirement => requirement.IsRequired);
        if (requiresSecret &&
            (!profile.ApiKeySecretId.HasValue ||
                profile.ApiKeySecretId.Value == Guid.Empty ||
                !requiredSecretExists))
        {
            return Failure(
                SharedProviderPublicationEligibilityCode.SecretReferenceMissing,
                "The provider connector requires a configured secret reference before publication.");
        }

        if (!SharedProviderProfilePublicationMetadataReader.TryRead(
                profile,
                out var metadata,
                out var metadataFailure))
        {
            return Failure(
                SharedProviderPublicationEligibilityCode.MetadataInvalid,
                metadataFailure);
        }

        if (!TryResolvePurpose(profile, metadata, out var purpose))
        {
            return Failure(
                SharedProviderPublicationEligibilityCode.NonProductionProfile,
                "The provider kind, purpose, and connector provenance are not a supported production combination.");
        }

        if (!relaySupportCatalog.TryGet(
                profile.ConnectorPluginKey,
                purpose,
                out var relayDescriptor) ||
            relayDescriptor.Classification !=
                SharedProviderRelayAdapterClassification.Production)
        {
            return Failure(
                SharedProviderPublicationEligibilityCode.RelayUnsupported,
                $"No shared-provider relay supports connector '{profile.ConnectorPluginKey}' for purpose '{PurposeName(purpose)}'.");
        }

        if (!TryResolveCapabilities(
                profile,
                metadata,
                purpose,
                relayDescriptor.Support,
                out var capabilities))
        {
            return Failure(
                SharedProviderPublicationEligibilityCode.RelayUnsupported,
                $"The shared-provider relay does not support the profile's exact '{PurposeName(purpose)}' operation.");
        }

        var models = metadata.Models
            .Select(model => new SharedProviderEligibleModel(
                model,
                Array.AsReadOnly(capabilities.ToArray())))
            .ToArray();
        return new SharedProviderPublicationEligibility(
            SharedProviderPublicationEligibilityCode.Eligible,
            "The provider profile is eligible for publication.",
            purpose,
            SharedProviderTransport.OpenAiCompatible,
            Array.AsReadOnly(models));
    }

    private static bool IsExplicitlyNonProduction(ProviderProfile profile)
        => profile.Id == ProviderProfileWellKnownIds.RuntimeFallbackOllama ||
            profile.ConnectorPluginKey is
                ScenarioHarnessProviderAdapter.PluginKey or
                ProcessMockProviderAdapter.PluginKey or
                SharedProviderReconciliationCoordinator.ImportedConnectorPluginKey;

    private static bool IsBasicProfileValid(
        ProviderProfile profile,
        ConnectorPluginManifest manifest)
    {
        if (profile.Id == Guid.Empty ||
            profile.Name is not { Length: > 0 and <= 200 } ||
            profile.Name != profile.Name.Trim() ||
            profile.Name.Any(char.IsControl) ||
            !string.Equals(
                profile.ConfigSchemaVersion,
                manifest.ConfigurationSchema.Version,
                StringComparison.Ordinal) ||
            profile.TimeoutSeconds < 5 ||
            !Uri.TryCreate(profile.BaseUrl, UriKind.Absolute, out var baseUri))
        {
            return false;
        }

        return (string.Equals(baseUri.Scheme, Uri.UriSchemeHttp, StringComparison.Ordinal) ||
                string.Equals(baseUri.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal)) &&
            !string.IsNullOrEmpty(baseUri.Host) &&
            string.IsNullOrEmpty(baseUri.UserInfo) &&
            string.IsNullOrEmpty(baseUri.Query) &&
            string.IsNullOrEmpty(baseUri.Fragment);
    }

    private static bool TryResolvePurpose(
        ProviderProfile profile,
        SharedProviderProfilePublicationMetadata metadata,
        out SharedProviderPurpose purpose)
    {
        purpose = default;
        var valid = (profile.ConnectorPluginKey, metadata.ProviderKind, metadata.Purpose) switch
        {
            (OpenAiProviderAdapter.PluginKey, AgentFrameworkProviderKind.OpenAi, ProviderProfilePurpose.Chat) =>
                Assign(SharedProviderPurpose.Chat, out purpose),
            (OpenAiProviderAdapter.PluginKey, AgentFrameworkProviderKind.OpenAi, ProviderProfilePurpose.ImageGeneration) =>
                Assign(SharedProviderPurpose.ImageGeneration, out purpose),
            (OllamaProviderAdapter.PluginKey or OllamaRemoteProviderAdapter.PluginKey,
                AgentFrameworkProviderKind.Ollama,
                ProviderProfilePurpose.Chat) =>
                Assign(SharedProviderPurpose.Chat, out purpose),
            (ComfyUiProviderAdapter.PluginKey,
                AgentFrameworkProviderKind.ComfyUi,
                ProviderProfilePurpose.ImageGeneration) =>
                Assign(SharedProviderPurpose.ImageGeneration, out purpose),
            _ => false
        };

        return valid && metadata.ProviderKind != AgentFrameworkProviderKind.AzureOpenAi;
    }

    private static bool TryResolveCapabilities(
        ProviderProfile profile,
        SharedProviderProfilePublicationMetadata metadata,
        SharedProviderPurpose purpose,
        SharedProviderRelaySupportDescriptor relaySupport,
        out IReadOnlyList<SharedProviderCapability> capabilities)
    {
        var resolved = new List<SharedProviderCapability>();
        if (purpose == SharedProviderPurpose.ImageGeneration)
        {
            var requiredTransport = metadata.ProviderKind switch
            {
                AgentFrameworkProviderKind.OpenAi =>
                    (ProviderTransportKind?)ProviderTransportKind.Responses,
                AgentFrameworkProviderKind.ComfyUi =>
                    ProviderTransportKind.ChatCompletions,
                _ => null
            };
            if (!requiredTransport.HasValue ||
                metadata.Transport != requiredTransport.Value ||
                !relaySupport.Operations.Contains(SharedProviderRelayOperation.ImageGenerations))
            {
                capabilities = [];
                return false;
            }

            resolved.Add(SharedProviderCapability.ImageGenerations);
            if (relaySupport.SupportsBase64Images)
            {
                resolved.Add(SharedProviderCapability.Base64Json);
            }

            capabilities = Array.AsReadOnly(resolved.ToArray());
            return true;
        }

        var requiredOperation = metadata.Transport switch
        {
            ProviderTransportKind.ChatCompletions =>
                (SharedProviderRelayOperation?)
                SharedProviderRelayOperation.ChatCompletions,
            ProviderTransportKind.Responses =>
                SharedProviderRelayOperation.Responses,
            _ => null
        };
        if (!requiredOperation.HasValue ||
            !relaySupport.Operations.Contains(requiredOperation.Value))
        {
            capabilities = [];
            return false;
        }

        resolved.Add(requiredOperation.Value == SharedProviderRelayOperation.ChatCompletions
            ? SharedProviderCapability.ChatCompletions
            : SharedProviderCapability.Responses);
        if (profile.SupportsStreaming &&
            relaySupport.StreamingMode == SharedProviderStreamingMode.ServerSentEvents)
        {
            resolved.Add(SharedProviderCapability.Streaming);
        }

        if (profile.SupportsToolCalling && relaySupport.SupportsFunctionTools)
        {
            resolved.Add(SharedProviderCapability.FunctionTools);
            if (relaySupport.SupportsParallelFunctionTools)
            {
                resolved.Add(SharedProviderCapability.ParallelFunctionTools);
            }
        }

        if (profile.SupportsStructuredOutput && relaySupport.SupportsStructuredOutput)
        {
            resolved.Add(SharedProviderCapability.StructuredOutput);
        }

        if (profile.SupportsVision && relaySupport.SupportsVisionInput)
        {
            resolved.Add(SharedProviderCapability.VisionInput);
        }

        capabilities = Array.AsReadOnly(resolved.ToArray());
        return true;
    }

    private static bool Assign(
        SharedProviderPurpose value,
        out SharedProviderPurpose purpose)
    {
        purpose = value;
        return true;
    }

    private static string PurposeName(SharedProviderPurpose purpose) => purpose switch
    {
        SharedProviderPurpose.Chat => "chat",
        SharedProviderPurpose.ImageGeneration => "image-generation",
        _ => throw new ArgumentOutOfRangeException(nameof(purpose))
    };

    private static SharedProviderPublicationEligibility Failure(
        SharedProviderPublicationEligibilityCode code,
        string sanitizedReason)
        => new(code, sanitizedReason, null, null, []);
}
