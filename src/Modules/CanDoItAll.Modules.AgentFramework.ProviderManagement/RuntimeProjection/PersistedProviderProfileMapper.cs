using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.Persistence;

namespace CanDoItAll.Modules.AgentFramework.ProviderManagement;

using AgentFrameworkProviderKind = CanDoItAll.AgentFramework.Models.ProviderKind;
using AgentFrameworkProviderProfile = CanDoItAll.AgentFramework.Models.ProviderProfile;

public sealed class ProviderProfileMapper(
    IProviderManifestCatalog providerManifestCatalog,
    IProviderProfileService providerProfileService)
{
    private const string OpenAiChatCompletionsProviderName =
        "OpenAI chat completions";

    public AgentFrameworkProviderProfile Map(ProviderProfile provider)
    {
        ArgumentNullException.ThrowIfNull(provider);

        var legacyMappedKind = ResolveMappedProviderKind(
            provider.ConnectorPluginKey);
        var mappedKind = ProviderMetadata.ResolveProviderKind(
            provider,
            legacyMappedKind);
        var legacyMappedTransport = ResolveLegacyMappedTransport(provider);
        var mappedTransport = ProviderMetadata.ResolveTransport(
            provider,
            legacyMappedTransport);
        var legacyMappedPurpose = ResolveLegacyMappedPurpose(provider);
        var mappedPurpose = ProviderMetadata.ResolvePurpose(
            provider,
            legacyMappedPurpose);
        var preferFrameworkManagedChatHistory =
            mappedKind is AgentFrameworkProviderKind.Ollama
                or AgentFrameworkProviderKind.ComfyUi ||
            mappedTransport == ProviderTransportKind.ChatCompletions;
        var supportsBackgroundResponses =
            mappedKind == AgentFrameworkProviderKind.OpenAi &&
            mappedTransport == ProviderTransportKind.Responses;
        var mappedProvider = new AgentFrameworkProviderProfile(
            provider.Id,
            provider.Name,
            mappedKind,
            provider.BaseUrl,
            ResolveSecretReference(provider),
            provider.DefaultModel,
            mappedTransport,
            provider.IsEnabled,
            provider.SupportsStreaming,
            provider.SupportsToolCalling,
            preferFrameworkManagedChatHistory,
            supportsBackgroundResponses,
            ProviderMetadata.BuildConfigurationJson(provider),
            providerManifestCatalog.ResolveManifest(
                provider.ConnectorPluginKey,
                provider.ProviderKind)?.DisplayName ??
            provider.ConnectorPluginKey,
            provider.LastHealthStatus ?? "Not checked",
            provider.LastHealthCheckAtUtc,
            ProviderModelCatalogPolicy.Resolve(
                provider.ConnectorPluginKey,
                mappedKind,
                mappedPurpose,
                provider.DefaultModel,
                ProviderMetadata.ReadSuggestedModels(provider)),
            mappedPurpose)
        {
            ConnectorPluginKey = provider.ConnectorPluginKey,
            Tags = ResolvePersistedProviderTags(
                provider,
                mappedKind,
                mappedTransport),
            ModelThinkingEffortCapabilities =
                ProviderMetadata.ReadThinkingEffortCapabilities(
                    provider.ExtraSettingsJson)
        };

        return providerProfileService.NormalizeImportedProfile(mappedProvider);
    }

    public AgentFrameworkProviderProfile CreateRuntimeFallback()
    {
        return new AgentFrameworkProviderProfile(
            ProviderProfileWellKnownIds.RuntimeFallbackOllama,
            ManagedSeedProviderFallbacks.FallbackProviderName,
            AgentFrameworkProviderKind.Ollama,
            ManagedSeedProviderFallbacks.FallbackBaseUrl,
            string.Empty,
            ManagedSeedProviderFallbacks.FallbackModel,
            ProviderTransportKind.ChatCompletions,
            true,
            true,
            true,
            true,
            false,
            ManagedSeedProviderFallbacks.CreateFallbackConfigurationJson(
                "runtime-remote-ollama"),
            "Remote Ollama fallback provider kept available for seeded agents.",
            "Not checked",
            null,
            [
                ManagedSeedProviderFallbacks.FallbackModel,
                "qwen3.5:9b",
                "gemma3-12b-128k:latest",
                "deepseek-r1:8b-32k",
                "phi4-16k"
            ])
        {
            ConnectorPluginKey = OllamaRemoteProviderAdministrationConnector.PluginKey,
            IsPrivateProvider = true,
            ModelPrices = ProviderPricingDefaults.CreateDefaultPrices(
                AgentFrameworkProviderKind.Ollama,
                ManagedSeedProviderFallbacks.FallbackModel),
            Tags = ["ollama", "remote", "fallback", "chat"]
        };
    }

    public static string ResolveDefaultModel(string connectorPluginKey)
    {
        return connectorPluginKey switch
        {
            ScenarioHarnessProviderAdministrationConnector.PluginKey =>
                ScenarioHarnessProviderAdministrationConnector.DefaultModel,
            ProcessMockProviderAdministrationConnector.PluginKey =>
                ProcessMockProviderAdministrationConnector.DefaultModel,
            OpenAiProviderAdministrationConnector.PluginKey =>
                OpenAiProviderAdministrationConnector.DefaultModel,
            ComfyUiProviderAdministrationConnector.PluginKey =>
                ComfyUiProviderAdministrationConnector.DefaultModel,
            _ => "llama3.1"
        };
    }

    private static IReadOnlyList<string> ResolvePersistedProviderTags(
        ProviderProfile provider,
        AgentFrameworkProviderKind mappedKind,
        ProviderTransportKind mappedTransport)
    {
        var storedTags = ProviderMetadata.ReadTags(provider);
        if (storedTags.Count > 0)
        {
            return storedTags;
        }

        var tags = new List<string>
        {
            mappedKind switch
            {
                AgentFrameworkProviderKind.Ollama => "ollama",
                AgentFrameworkProviderKind.ComfyUi => "comfyui",
                _ => "openai"
            },
            provider.BaseUrl.Contains(
                    "127.0.0.1",
                    StringComparison.OrdinalIgnoreCase) ||
                provider.BaseUrl.Contains(
                    "localhost",
                    StringComparison.OrdinalIgnoreCase)
                ? "local"
                : "cloud",
            mappedTransport == ProviderTransportKind.Responses
                ? "responses"
                : "chat-completions",
            mappedKind == AgentFrameworkProviderKind.ComfyUi
                ? "image-generation"
                : "chat"
        };
        return tags
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static AgentFrameworkProviderKind ResolveMappedProviderKind(
        string connectorPluginKey)
    {
        return connectorPluginKey switch
        {
            ScenarioHarnessProviderAdministrationConnector.PluginKey =>
                AgentFrameworkProviderKind.OpenAi,
            ProcessMockProviderAdministrationConnector.PluginKey =>
                AgentFrameworkProviderKind.OpenAi,
            OpenAiProviderAdministrationConnector.PluginKey =>
                AgentFrameworkProviderKind.OpenAi,
            ComfyUiProviderAdministrationConnector.PluginKey =>
                AgentFrameworkProviderKind.ComfyUi,
            OllamaProviderAdministrationConnector.PluginKey or
                OllamaRemoteProviderAdministrationConnector.PluginKey =>
                AgentFrameworkProviderKind.Ollama,
            _ => throw new InvalidOperationException(
                $"No AgentFramework provider kind mapping exists for connector plugin '{connectorPluginKey}'.")
        };
    }

    private static ProviderTransportKind ResolveLegacyMappedTransport(
        ProviderProfile provider)
    {
        return provider.ConnectorPluginKey switch
        {
            ScenarioHarnessProviderAdministrationConnector.PluginKey =>
                ProviderTransportKind.Responses,
            ProcessMockProviderAdministrationConnector.PluginKey =>
                ProviderTransportKind.Responses,
            OpenAiProviderAdministrationConnector.PluginKey
                when IsOpenAiChatCompletionsProvider(provider) =>
                ProviderTransportKind.ChatCompletions,
            OpenAiProviderAdministrationConnector.PluginKey =>
                ProviderTransportKind.Responses,
            ComfyUiProviderAdministrationConnector.PluginKey =>
                ProviderTransportKind.ChatCompletions,
            OllamaProviderAdministrationConnector.PluginKey or
                OllamaRemoteProviderAdministrationConnector.PluginKey =>
                ProviderTransportKind.ChatCompletions,
            _ => throw new InvalidOperationException(
                $"No AgentFramework provider transport mapping exists for connector plugin '{provider.ConnectorPluginKey}'.")
        };
    }

    private static ProviderProfilePurpose ResolveLegacyMappedPurpose(
        ProviderProfile provider)
    {
        if (provider.ConnectorPluginKey == ComfyUiProviderAdministrationConnector.PluginKey)
        {
            return ProviderProfilePurpose.ImageGeneration;
        }

        return provider.ConnectorPluginKey == OpenAiProviderAdministrationConnector.PluginKey &&
               LooksLikeLegacyOpenAiImageGenerationProvider(provider)
            ? ProviderProfilePurpose.ImageGeneration
            : ProviderProfilePurpose.Chat;
    }

    private static bool LooksLikeLegacyOpenAiImageGenerationProvider(
        ProviderProfile provider)
    {
        if (provider.Name.Contains("image", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (LooksLikeOpenAiImageGenerationModel(provider.DefaultModel))
        {
            return true;
        }

        return ProviderMetadata.ReadTags(provider)
            .Any(tag =>
                string.Equals(
                    tag,
                    "image",
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(
                    tag,
                    "image-generation",
                    StringComparison.OrdinalIgnoreCase));
    }

    private static bool LooksLikeOpenAiImageGenerationModel(string? model)
    {
        if (string.IsNullOrWhiteSpace(model))
        {
            return false;
        }

        var normalizedModel = model.Trim();
        return normalizedModel.StartsWith(
                   "gpt-image",
                   StringComparison.OrdinalIgnoreCase) ||
               normalizedModel.StartsWith(
                   "dall-e",
                   StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolveSecretReference(
        ProviderProfile provider)
    {
        return provider.ApiKeySecretId.HasValue
            ? ProviderMetadata.CreateSecretReference(provider.ApiKeySecretId.Value)
            : string.Empty;
    }

    private static bool IsOpenAiChatCompletionsProvider(
        ProviderProfile provider)
    {
        return string.Equals(
            provider.Name,
            OpenAiChatCompletionsProviderName,
            StringComparison.OrdinalIgnoreCase);
    }
}
