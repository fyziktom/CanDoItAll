using System.Collections.Concurrent;
using System.Runtime.Versioning;
using System.Security;
using System.Text.Json;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Runtime.Abstractions;
using Microsoft.Win32;

namespace CanDoItAll.AgentFramework.Core;

public sealed class ProviderProfileService : IProviderProfileService
{
    private const string GitHubCopilotRecommendation = "Deferred in remediation-bundle-v3. Keep GitHub Copilot as a future provider extension seam until package/runtime/credential proof exists inside this repository.";
    private const string SupportsVisionConfigurationKey = "supportsVision";
    private const string VisionTag = "vision";
    private const string MultimodalTag = "multimodal";

    private static readonly IReadOnlySet<string> OllamaVisionModelPrefixes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "bakllava",
        "gemma3",
        "gemma4",
        "llama3.2-vision",
        "llava",
        "minicpm",
        "minicpm-v",
        "moondream",
        "qwen3.5"
    };

    public ProviderProfileEditorModel CreateEditor(ProviderProfile? provider = null)
    {
        return provider is null
            ? new ProviderProfileEditorModel()
            : ProviderProfileEditorModel.FromDefinition(NormalizeImportedProfile(provider));
    }

    public ProviderProfile CreateProfile(ProviderProfileEditorModel model, ProviderProfile? current = null)
    {
        ArgumentNullException.ThrowIfNull(model);

        ValidateEditorSelections(model);
        var normalizedName = NormalizeRequiredName(model.Name);
        var normalizedBaseUrl = NormalizeEditorBaseUrl(model.BaseUrl);
        var normalizedConfigurationJson =
            NormalizeEditorConfigurationJson(model.ConfigurationJson);
        var providerIdentityChanged = current is not null &&
                                      (current.Kind != model.Kind ||
                                       !string.Equals(
                                           NormalizeBaseUrl(current.BaseUrl),
                                           normalizedBaseUrl,
                                           StringComparison.OrdinalIgnoreCase));
        IEnumerable<ProviderModelThinkingEffortCapability>? retainedThinkingEffortCapabilities =
            model.ModelThinkingEffortCapabilities ?? current?.ModelThinkingEffortCapabilities;
        if (providerIdentityChanged)
        {
            retainedThinkingEffortCapabilities = retainedThinkingEffortCapabilities?
                .Where(item => item.Source != AgentThinkingEffortCapabilitySource.Discovered);
        }

        var modelPrices = ProviderPricingDefaults.NormalizeModelPrices(
            model.Kind,
            model.DefaultModel,
            ProviderPricingDefaults.FromEditorModels(model.ModelPrices));
        if (!ProviderPricingDefaults.TryValidateModelPrices(modelPrices, out var validationMessage))
        {
            throw new ProviderProfileValidationException(validationMessage);
        }

        var profile = new ProviderProfile(
            Id: model.Id ?? Guid.NewGuid(),
            Name: normalizedName,
            Kind: model.Kind,
            BaseUrl: normalizedBaseUrl,
            ApiKeyEnvironmentVariable: NormalizeEnvironmentVariable(model.ApiKeyEnvironmentVariable),
            DefaultModel: NormalizeText(model.DefaultModel),
            Transport: model.Transport,
            Purpose: model.Purpose,
            IsEnabled: model.IsEnabled,
            SupportsStreaming: model.SupportsStreaming,
            SupportsTools: model.SupportsTools,
            PreferFrameworkManagedChatHistory: model.PreferFrameworkManagedChatHistory,
            SupportsBackgroundResponses: model.SupportsBackgroundResponses,
            ConfigurationJson: normalizedConfigurationJson,
            Notes: NormalizeText(model.Notes),
            HealthStatus: NormalizeHealthStatus(current?.HealthStatus),
            LastCheckedAtUtc: current?.LastCheckedAtUtc,
            SuggestedModels: NormalizeSuggestedModels(model.SuggestedModels))
        {
            IsPrivateProvider = ProviderPricingDefaults.ResolveIsPrivateProvider(model.Kind, model.IsPrivateProvider),
            ModelPrices = modelPrices,
            Tags = NormalizeTags(model.Tags),
            ModelThinkingEffortCapabilities = NormalizeEditorThinkingEffortCapabilities(
                retainedThinkingEffortCapabilities)
        };

        return NormalizeImportedProfileCore(
            profile,
            allowLegacyKindMigration: false);
    }

    public ProviderProfile NormalizeImportedProfile(ProviderProfile provider)
    {
        ArgumentNullException.ThrowIfNull(provider);

        return NormalizeImportedProfileCore(
            provider,
            allowLegacyKindMigration: !HasExplicitProviderKind(provider.ConfigurationJson));
    }

    private ProviderProfile NormalizeImportedProfileCore(
        ProviderProfile provider,
        bool allowLegacyKindMigration)
    {
        ArgumentNullException.ThrowIfNull(provider);

        var normalizedKind = provider.Kind;
        var normalizedTransport = provider.Transport;
        var normalizedPurpose = Enum.IsDefined(provider.Purpose)
            ? provider.Purpose
            : ProviderProfilePurpose.Chat;
        var normalizedPreferFrameworkManagedHistory = provider.PreferFrameworkManagedChatHistory;
        var normalizedBackgroundResponses = provider.SupportsBackgroundResponses;

        if (allowLegacyKindMigration &&
            provider.Kind == ProviderKind.AzureOpenAi &&
            LooksLikeLegacyOllama(provider))
        {
            normalizedKind = ProviderKind.Ollama;
            normalizedTransport = ProviderTransportKind.ChatCompletions;
            normalizedPreferFrameworkManagedHistory = true;
            normalizedBackgroundResponses = false;
        }
        else if (allowLegacyKindMigration &&
                 provider.Kind == ProviderKind.AzureOpenAi &&
                 LooksLikeLegacyOpenAi(provider))
        {
            normalizedKind = ProviderKind.OpenAi;
        }

        if (normalizedKind == ProviderKind.Ollama)
        {
            normalizedTransport = ProviderTransportKind.ChatCompletions;
            normalizedBackgroundResponses = false;
        }
        else if (normalizedTransport != ProviderTransportKind.Responses)
        {
            normalizedBackgroundResponses = false;
        }

        var metadata = ProviderPricingMetadata.Read(provider.ConfigurationJson);
        var configuredPrices = provider.ModelPrices.Count > 0
            ? provider.ModelPrices
            : metadata.ModelPrices;
        var normalizedModelPrices = ProviderPricingDefaults.NormalizeModelPrices(
            normalizedKind,
            provider.DefaultModel,
            configuredPrices);

        return provider with
        {
            Name = NormalizeText(provider.Name),
            Kind = normalizedKind,
            BaseUrl = NormalizeBaseUrl(provider.BaseUrl),
            ApiKeyEnvironmentVariable = NormalizeEnvironmentVariable(provider.ApiKeyEnvironmentVariable),
            DefaultModel = NormalizeText(provider.DefaultModel),
            Transport = normalizedTransport,
            Purpose = normalizedPurpose,
            PreferFrameworkManagedChatHistory = normalizedPreferFrameworkManagedHistory,
            SupportsBackgroundResponses = normalizedBackgroundResponses,
            ConfigurationJson = NormalizeConfigurationJson(provider.ConfigurationJson),
            Notes = NormalizeText(provider.Notes),
            HealthStatus = NormalizeHealthStatus(provider.HealthStatus),
            IsPrivateProvider = ProviderPricingDefaults.ResolveIsPrivateProvider(
                normalizedKind,
                metadata.IsPrivateProvider ?? provider.IsPrivateProvider),
            SuggestedModels = NormalizeSuggestedModels(provider.SuggestedModels),
            ModelPrices = normalizedModelPrices,
            Tags = NormalizeTags(provider.Tags),
            ModelThinkingEffortCapabilities = NormalizeThinkingEffortCapabilities(
                provider.ModelThinkingEffortCapabilities)
        };
    }

    public ProviderProfile ApplyHealthResult(ProviderProfile provider, ProviderHealthResult result, DateTimeOffset checkedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(result);

        var refreshedThinkingEffortCapabilities = result.ModelThinkingEffortCapabilities ??
                                                  provider.ModelThinkingEffortCapabilities;
        return NormalizeImportedProfile(provider with
        {
            HealthStatus = NormalizeHealthStatus(result.Summary),
            LastCheckedAtUtc = checkedAtUtc,
            SuggestedModels = NormalizeSuggestedModels(result.SuggestedModels),
            ModelThinkingEffortCapabilities = NormalizeThinkingEffortCapabilities(
                refreshedThinkingEffortCapabilities)
        });
    }

    public ProviderProfile ApplyProviderModelMaintenanceResult(ProviderProfile provider, ProviderModelMaintenanceEditorResult result, DateTimeOffset checkedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(result);

        return NormalizeImportedProfile(provider with
        {
            DefaultModel = NormalizeText(result.ModelName),
            HealthStatus = NormalizeHealthStatus(string.IsNullOrWhiteSpace(result.StatusMessage)
                ? $"Created or updated model '{result.ModelName}'."
                : result.StatusMessage),
            LastCheckedAtUtc = checkedAtUtc,
            SuggestedModels = provider.SuggestedModels
                .Append(result.ModelName)
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Select(item => item.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList()
        });
    }

    public string GetIdentityKey(ProviderProfile provider)
    {
        ArgumentNullException.ThrowIfNull(provider);

        return WorkspaceCatalogIdentityNormalizer.GetProviderIdentityKey(NormalizeImportedProfile(provider));
    }

    public ProviderFeatureMatrix ResolveFeatureMatrix(ProviderProfile provider)
    {
        ArgumentNullException.ThrowIfNull(provider);

        return ResolveFeatureMatrixCore(provider, requireSelectedModelMatch: false);
    }

    public ProviderFeatureMatrix ResolveFeatureMatrixForModel(ProviderProfile provider, string? model)
    {
        ArgumentNullException.ThrowIfNull(provider);

        var normalizedProvider = NormalizeImportedProfile(provider);
        var normalizedModel = NormalizeText(model);
        if (string.IsNullOrWhiteSpace(normalizedModel))
        {
            return ResolveFeatureMatrix(normalizedProvider);
        }

        return ResolveFeatureMatrixCore(normalizedProvider with
        {
            DefaultModel = normalizedModel,
            SuggestedModels = [normalizedModel]
        }, requireSelectedModelMatch: true);
    }

    private ProviderFeatureMatrix ResolveFeatureMatrixCore(
        ProviderProfile provider,
        bool requireSelectedModelMatch)
    {
        var normalizedProvider = NormalizeImportedProfile(provider);
        var supportsOpenAiFamily = normalizedProvider.Kind is ProviderKind.OpenAi or ProviderKind.AzureOpenAi;
        var supportsOllamaStructuredOutput = normalizedProvider.Kind == ProviderKind.Ollama;
        var supportsResponsesNativeTools = normalizedProvider.SupportsTools
            && normalizedProvider.Transport == ProviderTransportKind.Responses
            && supportsOpenAiFamily;
        var supportsServiceManagedHistory = supportsOpenAiFamily
            && normalizedProvider.Transport == ProviderTransportKind.Responses
            && !normalizedProvider.PreferFrameworkManagedChatHistory;
        var supportsFunctionTools = normalizedProvider.SupportsTools;
        var supportsResponseFormatJsonSchema = supportsOllamaStructuredOutput ||
                                               supportsOpenAiFamily &&
                                               normalizedProvider.Transport is ProviderTransportKind.Responses or ProviderTransportKind.ChatCompletions;
        var supportsStructuredOutput = supportsResponseFormatJsonSchema;
        var supportsToolApprovalRequests = supportsOpenAiFamily &&
                                           normalizedProvider.SupportsTools &&
                                           normalizedProvider.Transport is ProviderTransportKind.Responses or ProviderTransportKind.ChatCompletions;
        var supportsVision = supportsOpenAiFamily || SupportsConfiguredVision(normalizedProvider, requireSelectedModelMatch);
        var supportsHostedMcpServer = supportsResponsesNativeTools;
        var supportsCompaction = supportsOpenAiFamily;
        if (normalizedProvider.FeatureConstraints is { } constraints)
        {
            supportsResponseFormatJsonSchema &=
                constraints.AllowsStructuredOutput;
            supportsStructuredOutput &= constraints.AllowsStructuredOutput;
            supportsVision &= constraints.AllowsVision;
            supportsResponsesNativeTools &= constraints.AllowsNativeTools;
            supportsHostedMcpServer = supportsResponsesNativeTools &&
                constraints.AllowsHostedMcp;
            supportsServiceManagedHistory &=
                constraints.AllowsServiceManagedHistory;
            supportsCompaction &= constraints.AllowsCompaction;
        }

        return new ProviderFeatureMatrix(
            Kind: normalizedProvider.Kind,
            Transport: normalizedProvider.Transport,
            Purpose: normalizedProvider.Purpose,
            SupportsStreaming: normalizedProvider.SupportsStreaming,
            SupportsTools: normalizedProvider.SupportsTools,
            SupportsStructuredOutput: supportsStructuredOutput,
            SupportsToolApprovalWrappers: supportsToolApprovalRequests,
            PreferFrameworkManagedChatHistory: normalizedProvider.PreferFrameworkManagedChatHistory,
            SupportsBackgroundResponses: normalizedProvider.SupportsBackgroundResponses,
            SupportsNativeCodeInterpreter: supportsResponsesNativeTools,
            SupportsNativeFileSearch: supportsResponsesNativeTools,
            SupportsNativeWebSearch: supportsResponsesNativeTools,
            SupportsHostedMcpServer: supportsHostedMcpServer,
            SupportsLocalMcpBridge: normalizedProvider.SupportsTools,
            SupportsServiceManagedHistory: supportsServiceManagedHistory,
            SupportsVision: supportsVision,
            SupportsCompaction: supportsCompaction,
            GitHubCopilotRecommendation: GitHubCopilotRecommendation,
            SupportsFunctionTools: supportsFunctionTools,
            SupportsRunAsyncTypedOutput: supportsStructuredOutput,
            SupportsResponseFormatJsonSchema: supportsResponseFormatJsonSchema,
            SupportsToolApprovalRequests: supportsToolApprovalRequests,
            SupportsApprovalRequiredAIFunction: supportsToolApprovalRequests,
            SupportsHostedTools: supportsResponsesNativeTools,
            SupportsHostedMcp: supportsHostedMcpServer,
            SupportsLocalMcp: normalizedProvider.SupportsTools,
            SupportsImageGeneration: normalizedProvider.Purpose == ProviderProfilePurpose.ImageGeneration);
    }

    private static bool SupportsConfiguredVision(
        ProviderProfile provider,
        bool requireSelectedModelMatch)
    {
        if (provider.Kind == ProviderKind.Ollama)
        {
            var hasVisionModel = IsOllamaVisionModel(provider.DefaultModel) ||
                                 provider.SuggestedModels.Any(IsOllamaVisionModel);
            if (requireSelectedModelMatch)
            {
                return hasVisionModel;
            }

            if (hasVisionModel)
            {
                return true;
            }
        }

        if (provider.Tags.Any(tag =>
                string.Equals(tag, VisionTag, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(tag, MultimodalTag, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        if (TryReadConfigurationBoolean(provider.ConfigurationJson, SupportsVisionConfigurationKey, out var configuredSupportsVision) &&
            configuredSupportsVision)
        {
            return true;
        }

        return false;
    }

    private static bool IsOllamaVisionModel(string? model)
    {
        if (string.IsNullOrWhiteSpace(model))
        {
            return false;
        }

        var normalized = model.Trim();
        return OllamaVisionModelPrefixes.Any(prefix =>
            normalized.Equals(prefix, StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith(prefix + ":", StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith(prefix + "-", StringComparison.OrdinalIgnoreCase));
    }

    private static bool TryReadConfigurationBoolean(
        string? configurationJson,
        string propertyName,
        out bool value)
    {
        value = false;
        if (string.IsNullOrWhiteSpace(configurationJson))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(configurationJson);
            if (!document.RootElement.TryGetProperty(propertyName, out var property))
            {
                return false;
            }

            switch (property.ValueKind)
            {
                case JsonValueKind.True:
                    value = true;
                    return true;
                case JsonValueKind.False:
                    value = false;
                    return true;
                case JsonValueKind.String:
                    return bool.TryParse(property.GetString(), out value);
                default:
                    return false;
            }
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public ProviderFeatureSupportResult GetNativeToolSupport(ProviderProfile provider, ProviderNativeToolFamily family)
    {
        ArgumentNullException.ThrowIfNull(provider);

        var normalizedProvider = NormalizeImportedProfile(provider);
        var matrix = ResolveFeatureMatrix(normalizedProvider);
        if (!normalizedProvider.SupportsTools)
        {
            return new ProviderFeatureSupportResult(
                family,
                IsSupported: false,
                Summary: $"Provider '{normalizedProvider.Name}' is currently marked as not supporting tools.",
                Remediation: "Enable tools on the provider profile or choose a provider profile that allows tool usage.");
        }

        var isSupported = family switch
        {
            ProviderNativeToolFamily.CodeInterpreter => matrix.SupportsNativeCodeInterpreter,
            ProviderNativeToolFamily.FileSearch => matrix.SupportsNativeFileSearch,
            ProviderNativeToolFamily.WebSearch => matrix.SupportsNativeWebSearch,
            ProviderNativeToolFamily.HostedMcpServer => matrix.SupportsHostedMcpServer,
            _ => false
        };

        if (isSupported)
        {
            return new ProviderFeatureSupportResult(
                family,
                IsSupported: true,
                Summary: $"Provider '{normalizedProvider.Name}' supports {ProviderNativeToolKeys.GetDisplayName(family)} through the {normalizedProvider.Transport} transport.",
                Remediation: "Attach the capability directly and prefer it over a local wrapper when it avoids unnecessary shell work.");
        }

        if (normalizedProvider.Transport != ProviderTransportKind.Responses &&
            normalizedProvider.Kind is ProviderKind.OpenAi or ProviderKind.AzureOpenAi)
        {
            return new ProviderFeatureSupportResult(
                family,
                IsSupported: false,
                Summary: $"Provider '{normalizedProvider.Name}' uses the {normalizedProvider.Transport} transport, but {ProviderNativeToolKeys.GetDisplayName(family)} is exposed only through Responses-backed OpenAI or Azure OpenAI providers in this repository.",
                Remediation: ResolveNativeToolFallbackGuidance(family, "Switch this provider profile to the Responses transport"));
        }

        if (normalizedProvider.Kind == ProviderKind.Ollama)
        {
            return new ProviderFeatureSupportResult(
                family,
                IsSupported: false,
                Summary: $"Provider '{normalizedProvider.Name}' uses the Ollama integration, which is intentionally limited to local wrapper tools in this repository.",
                Remediation: ResolveNativeToolFallbackGuidance(family, "Keep using the existing workspace_* tools"));
        }

        return new ProviderFeatureSupportResult(
            family,
            IsSupported: false,
            Summary: $"Provider '{normalizedProvider.Name}' does not currently advertise support for {ProviderNativeToolKeys.GetDisplayName(family)}.",
            Remediation: ResolveNativeToolFallbackGuidance(family, "Use a supported Responses-backed OpenAI or Azure OpenAI provider"));
    }

    public string DescribeSupportedNativeToolFamilies(ProviderProfile provider)
    {
        ArgumentNullException.ThrowIfNull(provider);

        var matrix = ResolveFeatureMatrix(provider);
        var supportedFamilies = new List<string>();
        if (matrix.SupportsNativeCodeInterpreter)
        {
            supportedFamilies.Add(ProviderNativeToolKeys.GetDisplayName(ProviderNativeToolFamily.CodeInterpreter));
        }

        if (matrix.SupportsNativeFileSearch)
        {
            supportedFamilies.Add(ProviderNativeToolKeys.GetDisplayName(ProviderNativeToolFamily.FileSearch));
        }

        if (matrix.SupportsNativeWebSearch)
        {
            supportedFamilies.Add(ProviderNativeToolKeys.GetDisplayName(ProviderNativeToolFamily.WebSearch));
        }

        if (matrix.SupportsHostedMcpServer)
        {
            supportedFamilies.Add(ProviderNativeToolKeys.GetDisplayName(ProviderNativeToolFamily.HostedMcpServer));
        }

        return supportedFamilies.Count == 0
            ? "none"
            : string.Join(", ", supportedFamilies);
    }

    private static IReadOnlyList<string> NormalizeSuggestedModels(IEnumerable<string>? models)
    {
        return models?
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList()
            ?? [];
    }

    private static IReadOnlyList<ProviderModelThinkingEffortCapability> NormalizeThinkingEffortCapabilities(
        IEnumerable<ProviderModelThinkingEffortCapability>? capabilities,
        Func<string, Exception>? validationExceptionFactory = null)
    {
        var configuredCapabilities = capabilities?.ToList() ?? [];
        if (configuredCapabilities.Any(item =>
                item is null ||
                string.IsNullOrWhiteSpace(item.Model)))
        {
            throw CreateValidationException(
                "Provider thinking-effort capabilities must identify a model.");
        }

        var normalizedCapabilities = configuredCapabilities
            .Select(AgentThinkingEffortPolicy.NormalizeCapability)
            .ToList();
        var duplicateModel = normalizedCapabilities
            .GroupBy(item => item.Model, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1)?.Key;
        if (!string.IsNullOrWhiteSpace(duplicateModel))
        {
            throw CreateValidationException(
                $"Provider thinking-effort capabilities contain duplicate model '{duplicateModel}'.");
        }

        return normalizedCapabilities
            .OrderBy(item => item.Model, StringComparer.OrdinalIgnoreCase)
            .ToList();

        Exception CreateValidationException(string message)
        {
            return validationExceptionFactory?.Invoke(message) ??
                   new InvalidOperationException(message);
        }
    }

    private static IReadOnlyList<ProviderModelThinkingEffortCapability> NormalizeEditorThinkingEffortCapabilities(
        IEnumerable<ProviderModelThinkingEffortCapability>? capabilities)
        => NormalizeThinkingEffortCapabilities(
            capabilities,
            static message => new ProviderProfileValidationException(message));

    private static IReadOnlyList<string> NormalizeTags(IEnumerable<string>? tags)
    {
        return tags?
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Trim().TrimStart('#').ToLowerInvariant())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
            .ToList()
            ?? [];
    }

    private static string NormalizeBaseUrl(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().TrimEnd('/');
    }

    private static string NormalizeEditorBaseUrl(string? value)
    {
        var normalized = NormalizeBaseUrl(value);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new ProviderProfileValidationException(
                "Provider base URL is required.");
        }

        if (!Uri.TryCreate(normalized, UriKind.Absolute, out var uri) ||
            !string.IsNullOrEmpty(uri.UserInfo) ||
            !string.IsNullOrEmpty(uri.Query) ||
            !string.IsNullOrEmpty(uri.Fragment))
        {
            throw new ProviderProfileValidationException(
                "Provider base URL must be an absolute URI without user information, a query, or a fragment.");
        }

        return normalized;
    }

    private static string NormalizeRequiredName(string? value)
    {
        var normalized = NormalizeText(value);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new ProviderProfileValidationException(
                "Provider profile name is required.");
        }

        return normalized;
    }

    private static string NormalizeEditorConfigurationJson(string? value)
    {
        var normalized = NormalizeConfigurationJson(value);
        try
        {
            using var document = JsonDocument.Parse(normalized);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new ProviderProfileValidationException(
                    "Provider configuration must be a JSON object.");
            }

            return normalized;
        }
        catch (JsonException exception)
        {
            throw new ProviderProfileValidationException(
                "Provider configuration must be a valid JSON object.",
                exception);
        }
    }

    private static void ValidateEditorSelections(ProviderProfileEditorModel model)
    {
        if (model.Id == Guid.Empty)
        {
            throw new ProviderProfileValidationException(
                "Provider id cannot be empty.");
        }

        if (!Enum.IsDefined(model.Kind))
        {
            throw new ProviderProfileValidationException(
                "Provider kind is invalid.");
        }

        if (!Enum.IsDefined(model.Transport))
        {
            throw new ProviderProfileValidationException(
                "Provider transport is invalid.");
        }

        if (!Enum.IsDefined(model.Purpose))
        {
            throw new ProviderProfileValidationException(
                "Provider purpose is invalid.");
        }

        if (model.Kind == ProviderKind.Ollama &&
            model.Transport != ProviderTransportKind.ChatCompletions)
        {
            throw new ProviderProfileValidationException(
                "Ollama provider profiles require the Chat Completions transport.");
        }
    }

    private static string NormalizeConfigurationJson(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? "{}"
            : value.Trim();
    }

    private static string NormalizeEnvironmentVariable(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim();
    }

    private static string NormalizeHealthStatus(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? "Not checked"
            : value.Trim();
    }

    private static string NormalizeText(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim();
    }

    private static bool LooksLikeLegacyOllama(ProviderProfile provider)
    {
        return provider.Name.Contains("ollama", StringComparison.OrdinalIgnoreCase)
            || provider.BaseUrl.Contains("11434", StringComparison.OrdinalIgnoreCase)
            || provider.BaseUrl.Contains("ollama", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasExplicitProviderKind(string? configurationJson)
    {
        if (string.IsNullOrWhiteSpace(configurationJson) ||
            !configurationJson.Contains(
                ProviderProfileMetadataPropertyNames.ProviderKind,
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(configurationJson);
            return document.RootElement.ValueKind == JsonValueKind.Object &&
                   document.RootElement.EnumerateObject().Any(property =>
                       string.Equals(
                           property.Name,
                           ProviderProfileMetadataPropertyNames.ProviderKind,
                           StringComparison.OrdinalIgnoreCase));
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool LooksLikeLegacyOpenAi(ProviderProfile provider)
    {
        return provider.Name.Contains("openai", StringComparison.OrdinalIgnoreCase)
            || provider.BaseUrl.Contains("api.openai.com", StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolveNativeToolFallbackGuidance(ProviderNativeToolFamily family, string primaryAction)
    {
        return family switch
        {
            ProviderNativeToolFamily.CodeInterpreter => $"{primaryAction}, or keep using bounded workspace tools such as `workspace_python_run_file`, `workspace_dotnet_build`, and `workspace_dotnet_test`.",
            ProviderNativeToolFamily.FileSearch => $"{primaryAction}, or fall back to `workspace_search` for local repository search.",
            ProviderNativeToolFamily.WebSearch => $"{primaryAction}, or use a host-provided external-search integration instead of inventing a local shell workaround.",
            ProviderNativeToolFamily.HostedMcpServer => $"{primaryAction}, or stay on the existing local or HTTP MCP configuration path instead of claiming hosted MCP support.",
            _ => primaryAction + "."
        };
    }
}

public sealed class ProviderDiagnosticsService(
    IProviderDiagnosticsRuntime diagnosticsRuntime,
    IProviderModelAdministrationRuntime modelAdministrationRuntime) : IProviderDiagnosticsService
{
    public Task<ProviderHealthResult> TestProviderAsync(
        ProviderProfile provider,
        CancellationToken cancellationToken = default)
    {
        return diagnosticsRuntime.TestHealthAsync(provider, cancellationToken);
    }

    public Task<ProviderTestChatResult> RunProviderTestChatAsync(
        ProviderProfile provider,
        ProviderTestChatRequest request,
        CancellationToken cancellationToken = default)
    {
        return diagnosticsRuntime.RunProbeAsync(provider, request, cancellationToken);
    }

    public Task<ProviderModelMaintenanceEditorResult> CreateOrUpdateProviderModelAsync(
        ProviderProfile provider,
        ProviderModelMaintenanceEditorRequest request,
        CancellationToken cancellationToken = default)
    {
        return modelAdministrationRuntime.CreateOrUpdateModelAsync(provider, request, cancellationToken);
    }
}

public sealed class EnvironmentVariableAgentProviderCredentialResolver : IAgentProviderCredentialResolver
{
    public ProviderCredentialResolution Resolve(ProviderProfile provider)
    {
        ArgumentNullException.ThrowIfNull(provider);

        if (string.IsNullOrWhiteSpace(provider.ApiKeyEnvironmentVariable))
        {
            return new ProviderCredentialResolution(
                string.Empty,
                "not configured",
                "No API key environment variable is configured for this provider. Hosts can replace IAgentProviderCredentialResolver to bridge enterprise credentials.");
        }

        var normalizedVariableName = provider.ApiKeyEnvironmentVariable.Trim();
        var apiKey = AgentProviderEnvironmentCredential.Resolve(normalizedVariableName);
        return string.IsNullOrWhiteSpace(apiKey)
            ? new ProviderCredentialResolution(
                string.Empty,
                $"environment variable '{normalizedVariableName}'",
                $"Environment variable '{normalizedVariableName}' is not set. {AgentProviderEnvironmentCredential.DescribePresence(normalizedVariableName)}")
            : new ProviderCredentialResolution(
                apiKey,
                $"environment variable '{normalizedVariableName}'",
                string.Empty);
    }
}

public static class AgentProviderEnvironmentCredential
{
    private static readonly ConcurrentDictionary<string, string> ApplicationConfigurationCache = new(StringComparer.OrdinalIgnoreCase);

    public static string Resolve(
        string variableName)
    {
        if (string.IsNullOrWhiteSpace(variableName))
        {
            return string.Empty;
        }

        var normalizedVariableName = variableName.Trim();
        var processValue = Environment.GetEnvironmentVariable(normalizedVariableName);
        if (!string.IsNullOrWhiteSpace(processValue))
        {
            return processValue.Trim();
        }

        var scopedValue = FirstNonEmpty(
            Environment.GetEnvironmentVariable(normalizedVariableName, EnvironmentVariableTarget.User),
            Environment.GetEnvironmentVariable(normalizedVariableName, EnvironmentVariableTarget.Machine),
            ResolveFromWindowsRegistry(normalizedVariableName),
            ResolveFromApplicationConfiguration(normalizedVariableName));
        if (string.IsNullOrWhiteSpace(scopedValue))
        {
            return string.Empty;
        }

        return scopedValue.Trim();
    }

    private static string? FirstNonEmpty(
        params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
    }

    private static string ResolveFromWindowsRegistry(
        string variableName)
    {
        if (!OperatingSystem.IsWindows())
        {
            return string.Empty;
        }

        return ReadRegistryEnvironmentVariable(Registry.CurrentUser, "Environment", variableName) ??
               ReadRegistryEnvironmentVariable(RegistryHive.CurrentUser, RegistryView.Registry64, "Environment", variableName) ??
               ReadRegistryEnvironmentVariable(RegistryHive.CurrentUser, RegistryView.Registry32, "Environment", variableName) ??
               ReadRegistryEnvironmentVariable(Registry.LocalMachine, @"SYSTEM\CurrentControlSet\Control\Session Manager\Environment", variableName) ??
               ReadRegistryEnvironmentVariable(RegistryHive.LocalMachine, RegistryView.Registry64, @"SYSTEM\CurrentControlSet\Control\Session Manager\Environment", variableName) ??
               ReadRegistryEnvironmentVariable(RegistryHive.LocalMachine, RegistryView.Registry32, @"SYSTEM\CurrentControlSet\Control\Session Manager\Environment", variableName) ??
               string.Empty;
    }

    [SupportedOSPlatform("windows")]
    private static string? ReadRegistryEnvironmentVariable(
        RegistryHive hive,
        RegistryView view,
        string subKeyName,
        string variableName)
    {
        try
        {
            using var root = RegistryKey.OpenBaseKey(hive, view);
            return ReadRegistryEnvironmentVariable(root, subKeyName, variableName);
        }
        catch (IOException)
        {
            return null;
        }
        catch (SecurityException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    [SupportedOSPlatform("windows")]
    private static string? ReadRegistryEnvironmentVariable(
        RegistryKey root,
        string subKeyName,
        string variableName)
    {
        try
        {
            using var key = root.OpenSubKey(subKeyName, writable: false);
            return key?.GetValue(variableName) as string;
        }
        catch (IOException)
        {
            return null;
        }
        catch (SecurityException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    public static string DescribePresence(
        string variableName)
    {
        if (string.IsNullOrWhiteSpace(variableName))
        {
            return "No environment variable name was provided.";
        }

        var normalizedVariableName = variableName.Trim();
        return $"Checked scopes: process={HasValue(Environment.GetEnvironmentVariable(normalizedVariableName))}, user={HasValue(Environment.GetEnvironmentVariable(normalizedVariableName, EnvironmentVariableTarget.User))}, machine={HasValue(Environment.GetEnvironmentVariable(normalizedVariableName, EnvironmentVariableTarget.Machine))}, registry={HasValue(ResolveFromWindowsRegistry(normalizedVariableName))}, appsettings={HasValue(ResolveFromApplicationConfiguration(normalizedVariableName))}.";
    }

    private static string ResolveFromApplicationConfiguration(
        string variableName)
    {
        if (string.IsNullOrWhiteSpace(variableName))
        {
            return string.Empty;
        }

        return ApplicationConfigurationCache.GetOrAdd(
            variableName.Trim(),
            ResolveFromApplicationConfigurationCore);
    }

    private static string ResolveFromApplicationConfigurationCore(
        string variableName)
    {
        foreach (var path in EnumerateAppSettingsCandidates())
        {
            try
            {
                if (!File.Exists(path))
                {
                    continue;
                }

                using var stream = File.OpenRead(path);
                using var document = JsonDocument.Parse(stream);
                if (!document.RootElement.TryGetProperty(variableName, out var property) ||
                    property.ValueKind != JsonValueKind.String)
                {
                    continue;
                }

                var value = property.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value.Trim();
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
            catch (JsonException)
            {
            }
        }

        return string.Empty;
    }

    private static IEnumerable<string> EnumerateAppSettingsCandidates()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var directory in EnumerateCandidateDirectories())
        {
            foreach (var fileName in EnumerateCandidateAppSettingsFileNames())
            {
                var path = Path.Combine(directory, fileName);
                if (seen.Add(path))
                {
                    yield return path;
                }
            }
        }
    }

    private static IEnumerable<string> EnumerateCandidateDirectories()
    {
        foreach (var root in new[] { AppContext.BaseDirectory, Environment.CurrentDirectory })
        {
            if (string.IsNullOrWhiteSpace(root))
            {
                continue;
            }

            var current = Path.GetFullPath(root);
            for (var depth = 0; depth < 6 && !string.IsNullOrWhiteSpace(current); depth++)
            {
                if (Directory.Exists(current))
                {
                    yield return current;
                }

                current = Directory.GetParent(current)?.FullName ?? string.Empty;
            }
        }
    }

    private static IEnumerable<string> EnumerateCandidateAppSettingsFileNames()
    {
        yield return "appsettings.json";

        var environmentName = FirstNonEmpty(
            Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT"),
            Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"));
        if (!string.IsNullOrWhiteSpace(environmentName))
        {
            yield return $"appsettings.{environmentName.Trim()}.json";
        }
        else
        {
            yield return "appsettings.Development.json";
        }
    }

    private static bool HasValue(
        string? value)
    {
        return !string.IsNullOrWhiteSpace(value);
    }
}
