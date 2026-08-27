namespace CanDoItAll.AgentFramework.Models;

public static class ProviderProfileMetadataPropertyNames
{
    public const string ProviderKind = "agentFrameworkProviderKind";
    public const string ProviderTransport = "providerTransport";
    public const string ProviderPurpose = "providerPurpose";
    public const string SuggestedModels = "suggestedModels";
    public const string ModelThinkingEffortCapabilities = "modelThinkingEffortCapabilities";
    public const string SecretRecordId = "secretRecordId";
}

public static class ProviderProfileWellKnownIds
{
    public static readonly Guid RuntimeFallbackOllama =
        new("12E4C814-E822-0B58-9B9F-52577D7B374E");
}

public enum ProviderNativeToolFamily
{
    CodeInterpreter,
    FileSearch,
    WebSearch,
    HostedMcpServer
}

public enum ProviderNetworkAccessPolicy
{
    Default,
    PublicOnly,
    AllowPrivateNetwork
}

public enum ProviderCredentialPurpose
{
    ProviderApiKey,
    SourceAccessToken
}

public enum ProviderCredentialConsumerKind
{
    ProviderProfile,
    Source
}

public sealed record ProviderCredentialBinding(
    Guid SecretId,
    ProviderCredentialPurpose Purpose,
    ProviderCredentialConsumerKind ConsumerKind,
    Guid ConsumerId);

public sealed class ProviderAudioCapabilityException(
    Guid providerProfileId,
    AgentProviderOperationKind operation) : InvalidOperationException(PublicMessage)
{
    public const string PublicMessage =
        "Audio is not available for this provider profile.";

    public Guid ProviderProfileId { get; } = providerProfileId;

    public AgentProviderOperationKind Operation { get; } = operation;
}

public static class ProviderAudioCapabilityPolicy
{
    public static bool IsAvailable(ProviderProfile provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        return provider.CredentialBinding?.Purpose !=
            ProviderCredentialPurpose.SourceAccessToken;
    }

    public static void EnsureAvailable(
        ProviderProfile provider,
        AgentProviderOperationKind operation)
    {
        if (operation is not AgentProviderOperationKind.TranscribeSpeech and
            not AgentProviderOperationKind.SynthesizeSpeech)
        {
            throw new ArgumentOutOfRangeException(nameof(operation), operation, null);
        }

        if (IsAvailable(provider))
        {
            return;
        }

        throw new ProviderAudioCapabilityException(provider.Id, operation);
    }
}

public sealed record ProviderFeatureConstraints(
    bool AllowsStructuredOutput,
    bool AllowsVision,
    bool AllowsNativeTools,
    bool AllowsHostedMcp,
    bool AllowsServiceManagedHistory,
    bool AllowsCompaction,
    bool AllowsParallelFunctionTools = true);

public sealed record ProviderModelSelectionConstraint
{
    public ProviderModelSelectionConstraint(
        IReadOnlyList<string> allowedModels)
    {
        ArgumentNullException.ThrowIfNull(allowedModels);
        var normalized = allowedModels
            .Select(model => string.IsNullOrWhiteSpace(model)
                ? throw new ArgumentException(
                    "Allowed provider models cannot contain an empty value.",
                    nameof(allowedModels))
                : model.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (normalized.Length == 0)
        {
            throw new ArgumentException(
                "A constrained provider requires at least one allowed model.",
                nameof(allowedModels));
        }

        AllowedModels = Array.AsReadOnly(normalized);
    }

    public IReadOnlyList<string> AllowedModels { get; }

    public bool Allows(string? model)
        => !string.IsNullOrEmpty(model) &&
            AllowedModels.Contains(model, StringComparer.Ordinal);
}

public sealed class ProviderModelSelectionException(
    Guid providerProfileId,
    string requestedModel) : InvalidOperationException(PublicMessage)
{
    public const string PublicMessage =
        "The requested model is not available for this provider profile.";

    public Guid ProviderProfileId { get; } = providerProfileId;

    public string RequestedModel { get; } = requestedModel;
}

public static class ProviderModelSelectionPolicy
{
    public static void EnsureAllowed(
        ProviderProfile provider,
        string? requestedModel)
    {
        ArgumentNullException.ThrowIfNull(provider);
        if (provider.ModelSelectionConstraint is not { } constraint)
        {
            if (provider.CredentialBinding?.Purpose ==
                ProviderCredentialPurpose.SourceAccessToken)
            {
                throw new ProviderModelSelectionException(
                    provider.Id,
                    requestedModel ?? string.Empty);
            }

            return;
        }

        var selectedModel = requestedModel ?? string.Empty;
        if (constraint.Allows(selectedModel))
        {
            return;
        }

        throw new ProviderModelSelectionException(
            provider.Id,
            selectedModel);
    }
}

public sealed record ProviderFeatureMatrix(
    ProviderKind Kind,
    ProviderTransportKind Transport,
    ProviderProfilePurpose Purpose,
    bool SupportsStreaming,
    bool SupportsTools,
    bool SupportsStructuredOutput,
    bool SupportsToolApprovalWrappers,
    bool PreferFrameworkManagedChatHistory,
    bool SupportsBackgroundResponses,
    bool SupportsNativeCodeInterpreter,
    bool SupportsNativeFileSearch,
    bool SupportsNativeWebSearch,
    bool SupportsHostedMcpServer,
    bool SupportsLocalMcpBridge,
    bool SupportsServiceManagedHistory,
    bool SupportsVision,
    bool SupportsCompaction,
    string GitHubCopilotRecommendation,
    bool SupportsFunctionTools = false,
    bool SupportsRunAsyncTypedOutput = false,
    bool SupportsResponseFormatJsonSchema = false,
    bool SupportsToolApprovalRequests = false,
    bool SupportsApprovalRequiredAIFunction = false,
    bool SupportsHostedTools = false,
    bool SupportsHostedMcp = false,
    bool SupportsLocalMcp = false,
    bool SupportsImageGeneration = false,
    bool SupportsParallelFunctionTools = true);

public sealed record ProviderFeatureSupportResult(
    ProviderNativeToolFamily Family,
    bool IsSupported,
    string Summary,
    string Remediation);

public static class ProviderNativeToolKeys
{
    public const string CodeInterpreter = "provider_native_code_interpreter";
    public const string FileSearch = "provider_native_file_search";
    public const string WebSearch = "provider_native_web_search";

    public static bool TryResolveFamily(string? toolKey, out ProviderNativeToolFamily family)
    {
        family = default;
        if (string.IsNullOrWhiteSpace(toolKey))
        {
            return false;
        }

        return toolKey.Trim() switch
        {
            CodeInterpreter or "provider-native-code-interpreter" => Assign(ProviderNativeToolFamily.CodeInterpreter, out family),
            FileSearch or "provider-native-file-search" => Assign(ProviderNativeToolFamily.FileSearch, out family),
            WebSearch or "provider-native-web-search" => Assign(ProviderNativeToolFamily.WebSearch, out family),
            _ => false
        };
    }

    public static string GetDisplayName(ProviderNativeToolFamily family)
    {
        return family switch
        {
            ProviderNativeToolFamily.CodeInterpreter => "provider-native code interpreter",
            ProviderNativeToolFamily.FileSearch => "provider-native file search",
            ProviderNativeToolFamily.WebSearch => "provider-native web search",
            ProviderNativeToolFamily.HostedMcpServer => "provider-native hosted MCP",
            _ => family.ToString()
        };
    }

    private static bool Assign(ProviderNativeToolFamily familyValue, out ProviderNativeToolFamily family)
    {
        family = familyValue;
        return true;
    }
}

public sealed record ProviderModelDisplayMetadata(string Id, string DisplayName);

public sealed record ProviderProfile(
    Guid Id,
    string Name,
    ProviderKind Kind,
    string BaseUrl,
    string ApiKeyEnvironmentVariable,
    string DefaultModel,
    ProviderTransportKind Transport,
    bool IsEnabled,
    bool SupportsStreaming,
    bool SupportsTools,
    bool PreferFrameworkManagedChatHistory,
    bool SupportsBackgroundResponses,
    string ConfigurationJson,
    string Notes,
    string HealthStatus,
    DateTimeOffset? LastCheckedAtUtc,
    IReadOnlyList<string> SuggestedModels,
    ProviderProfilePurpose Purpose = ProviderProfilePurpose.Chat)
{
    public string ConnectorPluginKey { get; init; } = string.Empty;

    public ProviderCredentialBinding? CredentialBinding { get; init; }

    public ProviderNetworkAccessPolicy NetworkAccessPolicy { get; init; }

    public ProviderFeatureConstraints? FeatureConstraints { get; init; }

    public ProviderModelSelectionConstraint? ModelSelectionConstraint
    {
        get;
        init;
    }

    public bool IsPrivateProvider { get; init; }

    public bool IsSourceManaged => CredentialBinding?.Purpose == ProviderCredentialPurpose.SourceAccessToken;

    public IReadOnlyList<ProviderModelDisplayMetadata> ModelCatalog { get; init; } = [];

    public string GetModelDisplayName(string? model) {
        var id = model?.Trim() ?? string.Empty;
        return ModelCatalog.FirstOrDefault(item => string.Equals(item.Id, id, StringComparison.Ordinal))?.DisplayName
            ?? (IsSourceManaged && id.Length > 0 ? "Unavailable shared model" : id);
    }

    public IReadOnlyList<ProviderModelTokenPrice> ModelPrices { get; init; } = [];

    public IReadOnlyList<string> Tags { get; init; } = [];

    public IReadOnlyList<ProviderModelThinkingEffortCapability> ModelThinkingEffortCapabilities { get; init; } = [];
}

public sealed record ProviderHealthResult(
    bool Success,
    string Summary,
    IReadOnlyList<string> SuggestedModels)
{
    public IReadOnlyList<ProviderModelThinkingEffortCapability>? ModelThinkingEffortCapabilities { get; init; }
}
