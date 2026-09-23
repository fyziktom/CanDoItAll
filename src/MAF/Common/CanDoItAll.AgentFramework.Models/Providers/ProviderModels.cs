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

/// <summary>
/// Network addresses that calls through a provider profile may reach, as a JSON integer: 0 Default (local profiles;
/// no shared-source network policy applies), 1 PublicOnly (public addresses only), 2 AllowPrivateNetwork (private
/// network addresses allowed). Profiles imported from a shared provider source take the policy of their source.
/// </summary>
public enum ProviderNetworkAccessPolicy
{
    Default,
    PublicOnly,
    AllowPrivateNetwork
}

/// <summary>
/// What a provider credential is, as a JSON integer: 0 ProviderApiKey (an API key of the provider), 1 SourceAccessToken
/// (the access token of a shared provider source).
/// </summary>
public enum ProviderCredentialPurpose
{
    ProviderApiKey,
    SourceAccessToken
}

/// <summary>
/// Owner of a provider credential, as a JSON integer: 0 ProviderProfile, 1 Source (a shared provider source).
/// </summary>
public enum ProviderCredentialConsumerKind
{
    ProviderProfile,
    Source
}

/// <summary>
/// Credential of a provider profile imported from a shared provider source: the source's access token, kept as a
/// stored secret whose value is never returned. Null for local profiles.
/// </summary>
/// <param name="SecretId">Identifier of the stored secret.</param>
/// <param name="Purpose">
/// What the secret is, as a JSON integer: 0 ProviderApiKey, 1 SourceAccessToken (always for imported profiles).
/// </param>
/// <param name="ConsumerKind">
/// Owner of the credential, as a JSON integer: 0 ProviderProfile, 1 Source (always for imported profiles).
/// </param>
/// <param name="ConsumerId">Identifier of the owner, here the shared provider source.</param>
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

/// <summary>
/// Features that a shared provider source allows for a provider profile imported from it; null for local profiles.
/// </summary>
/// <param name="AllowsStructuredOutput">True when every published model supports structured JSON output.</param>
/// <param name="AllowsVision">True when every published model accepts image input.</param>
/// <param name="AllowsNativeTools">
/// Whether provider-native tools (code interpreter, file search, web search) may be used; false for imported profiles.
/// </param>
/// <param name="AllowsHostedMcp">Whether provider-hosted MCP servers may be used; false for imported profiles.</param>
/// <param name="AllowsServiceManagedHistory">
/// Whether the provider may keep the chat history; false for imported profiles.
/// </param>
/// <param name="AllowsCompaction">
/// Whether provider-side conversation compaction may be used; false for imported profiles.
/// </param>
/// <param name="AllowsParallelFunctionTools">
/// True when every published model supports parallel function tool calls.
/// </param>
public sealed record ProviderFeatureConstraints(
    bool AllowsStructuredOutput,
    bool AllowsVision,
    bool AllowsNativeTools,
    bool AllowsHostedMcp,
    bool AllowsServiceManagedHistory,
    bool AllowsCompaction,
    bool AllowsParallelFunctionTools = true);

/// <summary>
/// Models that a provider profile imported from a shared provider source may use; null for local profiles, which
/// accept any model identifier.
/// </summary>
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

    /// <summary>
    /// Identifiers of the models the source publishes, compared exactly; requests for any other model are refused.
    /// </summary>
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

/// <summary>
/// Display name of a model published by a shared provider source.
/// </summary>
/// <param name="Id">Model identifier to use in requests.</param>
/// <param name="DisplayName">Name to show for the model.</param>
public sealed record ProviderModelDisplayMetadata(string Id, string DisplayName);

/// <summary>
/// A model provider profile: a configured connection to a model provider, listed by <c>GET /api/agents/providers</c>
/// and referenced by agents through <c>providerProfileId</c>. The model is chosen separately within the profile.
/// Local profiles are edited with <c>GET /api/agents/providers/{providerId}/editor</c> and
/// <c>POST /api/agents/providers</c>; profiles imported from a shared provider source (<c>isSourceManaged</c> true)
/// are managed by that source. Publishing a local profile to other hosts is a separate shared-provider publication.
/// </summary>
/// <param name="Id">Identifier of the profile.</param>
/// <param name="Name">Display name of the profile.</param>
/// <param name="Kind">
/// Provider kind, as a JSON integer: 0 OpenAi (OpenAI or an OpenAI-compatible endpoint; also every imported profile),
/// 1 AzureOpenAi, 2 Ollama, 3 ComfyUi.
/// </param>
/// <param name="BaseUrl">Base URL of the provider's API.</param>
/// <param name="ApiKeyEnvironmentVariable">
/// Reference to the stored credential, <c>secret:</c> followed by the secret's GUID, or an empty string when the
/// profile has none. The credential itself is never returned.
/// </param>
/// <param name="DefaultModel">Model used when an agent or request names none.</param>
/// <param name="Transport">
/// API style used to call the models, as a JSON integer: 0 Responses, 1 ChatCompletions (always for Ollama).
/// </param>
/// <param name="IsEnabled">
/// True when the profile can be used. An imported profile is false whenever its import is unavailable; the reason is
/// in <c>healthStatus</c>.
/// </param>
/// <param name="SupportsStreaming">True when responses are streamed from the provider.</param>
/// <param name="SupportsTools">True when the profile's models may be given tools.</param>
/// <param name="PreferFrameworkManagedChatHistory">
/// Set by the server: true when the product keeps and resends the chat history, as for Ollama, ComfyUI and the
/// ChatCompletions transport.
/// </param>
/// <param name="SupportsBackgroundResponses">
/// Set by the server: true only for OpenAI profiles on the Responses transport.
/// </param>
/// <param name="ConfigurationJson">
/// Additional settings as the text of a JSON object, for example <c>timeoutSeconds</c> (the request timeout in
/// seconds); returned as stored apart from the secret reference.
/// </param>
/// <param name="Notes">
/// Display name of the provider connector for local profiles, or a fixed description for imported profiles and the
/// built-in fallback profile; not a stored free-text note.
/// </param>
/// <param name="HealthStatus">
/// For local profiles the result of the last health check, or <c>Not checked</c>; for imported profiles the
/// availability of the import, for example <c>Available</c> or <c>SourceOffline</c>.
/// </param>
/// <param name="LastCheckedAtUtc">
/// When the last health check ran, in UTC; null when the profile was never checked and for imported profiles.
/// </param>
/// <param name="SuggestedModels">Models offered when choosing a model for the profile.</param>
/// <param name="Purpose">What the profile is used for, as a JSON integer: 0 Chat, 1 ImageGeneration.</param>
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
    /// <summary>
    /// Key of the connector that manages the profile, for example <c>provider.openai</c>, <c>provider.ollama.local</c>,
    /// <c>provider.ollama.remote</c>, <c>provider.comfyui.local</c> or <c>provider.candoitall-shared</c> (imported
    /// profiles).
    /// </summary>
    public string ConnectorPluginKey { get; init; } = string.Empty;

    /// <summary>
    /// Credential of an imported profile (its shared provider source's access token); null for local profiles.
    /// </summary>
    public ProviderCredentialBinding? CredentialBinding { get; init; }

    /// <summary>
    /// Network addresses the profile may reach, as a JSON integer: 0 Default (local profiles), 1 PublicOnly,
    /// 2 AllowPrivateNetwork; imported profiles take the policy of their source.
    /// </summary>
    public ProviderNetworkAccessPolicy NetworkAccessPolicy { get; init; }

    /// <summary>Features the shared provider source allows for an imported profile; null for local profiles.</summary>
    public ProviderFeatureConstraints? FeatureConstraints { get; init; }

    /// <summary>Models an imported profile may use; null for local profiles.</summary>
    public ProviderModelSelectionConstraint? ModelSelectionConstraint
    {
        get;
        init;
    }

    /// <summary>
    /// True for a privately hosted provider; always true for Ollama and ComfyUI profiles.
    /// </summary>
    public bool IsPrivateProvider { get; init; }

    /// <summary>
    /// True when the profile is imported from a shared provider source and managed by it; such profiles cannot be
    /// saved or deleted here.
    /// </summary>
    public bool IsSourceManaged => CredentialBinding?.Purpose == ProviderCredentialPurpose.SourceAccessToken;

    /// <summary>
    /// Display names of the models published by the shared provider source; empty for local profiles.
    /// </summary>
    public IReadOnlyList<ProviderModelDisplayMetadata> ModelCatalog { get; init; } = [];

    public string GetModelDisplayName(string? model) {
        var id = model?.Trim() ?? string.Empty;
        return ModelCatalog.FirstOrDefault(item => string.Equals(item.Id, id, StringComparison.Ordinal))?.DisplayName
            ?? (IsSourceManaged && id.Length > 0 ? "Unavailable shared model" : id);
    }

    /// <summary>
    /// Token prices of the profile's models, used to estimate run costs; the default model's row comes first.
    /// </summary>
    public IReadOnlyList<ProviderModelTokenPrice> ModelPrices { get; init; } = [];

    /// <summary>
    /// Version marker of the prices, recorded in the provider request history to tell which prices were used; null
    /// for profiles without one.
    /// </summary>
    public string? PricingSourceRevision { get; init; }

    /// <summary>
    /// Labels of the profile, lowercase and sorted, for example <c>openai</c>, <c>local</c> or <c>chat</c>.
    /// </summary>
    public IReadOnlyList<string> Tags { get; init; } = [];

    /// <summary>
    /// Thinking-effort capabilities stored for the profile's models, for example those discovered by a health check.
    /// Which capability applies to a model also depends on the provider kind and the profile configuration.
    /// </summary>
    public IReadOnlyList<ProviderModelThinkingEffortCapability> ModelThinkingEffortCapabilities { get; init; } = [];
}

/// <summary>
/// Result of a provider profile health check, returned by <c>POST /api/agents/providers/{providerId}/test</c>.
/// </summary>
/// <param name="Success">True when the provider answered the check as expected.</param>
/// <param name="Summary">Human-readable result of the check; do not parse it.</param>
/// <param name="SuggestedModels">Models the provider reported during the check; may be empty.</param>
public sealed record ProviderHealthResult(
    bool Success,
    string Summary,
    IReadOnlyList<string> SuggestedModels)
{
    /// <summary>
    /// Thinking-effort capabilities discovered for the provider's models during the check, or null when the check
    /// does not discover them.
    /// </summary>
    public IReadOnlyList<ProviderModelThinkingEffortCapability>? ModelThinkingEffortCapabilities { get; init; }
}
