using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Workspace;
using CanDoItAll.SharedProviders.Abstractions;
using System.Text.Json;

namespace CanDoItAll.Modules.AgentFramework;

using AgentFrameworkProviderKind = CanDoItAll.AgentFramework.Models.ProviderKind;
using AgentFrameworkProviderProfile = CanDoItAll.AgentFramework.Models.ProviderProfile;
using WorkspaceProviderProfile = CanDoItAll.Modules.Workspace.ProviderProfile;

internal sealed class WorkspaceAgentProviderProfileMapper(
    ProviderRegistry providerRegistry,
    IProviderProfileService providerProfileService)
{
    private const string OpenAiChatCompletionsProviderName =
        "OpenAI chat completions";

    public AgentFrameworkProviderProfile Map(WorkspaceProviderProfile provider)
    {
        ArgumentNullException.ThrowIfNull(provider);

        var legacyMappedKind = ResolveMappedProviderKind(
            provider.ConnectorPluginKey);
        var mappedKind = AgentFrameworkProviderMetadata.ResolveProviderKind(
            provider,
            legacyMappedKind);
        var legacyMappedTransport = ResolveLegacyMappedTransport(provider);
        var mappedTransport = AgentFrameworkProviderMetadata.ResolveTransport(
            provider,
            legacyMappedTransport);
        var legacyMappedPurpose = ResolveLegacyMappedPurpose(provider);
        var mappedPurpose = AgentFrameworkProviderMetadata.ResolvePurpose(
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
            AgentFrameworkProviderMetadata.BuildConfigurationJson(provider),
            providerRegistry.Resolve(provider)?.Manifest.DisplayName ??
            provider.ConnectorPluginKey,
            provider.LastHealthStatus ?? "Not checked",
            provider.LastHealthCheckAtUtc,
            ResolveWorkspaceProviderSuggestedModels(
                provider,
                mappedKind,
                mappedPurpose),
            mappedPurpose)
        {
            ConnectorPluginKey = provider.ConnectorPluginKey,
            Tags = ResolveWorkspaceProviderTags(
                provider,
                mappedKind,
                mappedTransport),
            ModelThinkingEffortCapabilities =
                AgentFrameworkProviderMetadata.ReadThinkingEffortCapabilities(
                    provider.ExtraSettingsJson)
        };

        return providerProfileService.NormalizeImportedProfile(mappedProvider);
    }

    public AgentFrameworkProviderProfile MapShared(
        SharedProviderRuntimeProfileMaterializationResult materialization)
    {
        ArgumentNullException.ThrowIfNull(materialization);
        var provider = materialization.Profile
            ?? throw new InvalidOperationException(
                "A shared-provider runtime profile cannot be projected from an invalid source graph.");
        var configurationJson = JsonSerializer.Serialize(new
        {
            timeoutSeconds = Math.Max(5, provider.TimeoutSeconds)
        });
        var allowedModels = provider.Models
            .Select(model => model.Id.Value)
            .ToArray();
        return new AgentFrameworkProviderProfile(
            provider.ProviderProfileId,
            provider.Name,
            provider.Kind,
            provider.BaseUri.AbsoluteUri,
            AgentFrameworkProviderMetadata.CreateSecretReference(
                provider.SourceTokenSecretReferenceId),
            provider.DefaultModelId.Value,
            provider.Transport,
            materialization.IsAvailable,
            provider.SupportsStreaming,
            provider.SupportsTools,
            provider.PreferFrameworkManagedChatHistory,
            provider.SupportsBackgroundResponses,
            configurationJson,
            "Source-managed CanDoItAll shared provider.",
            materialization.Availability.ToString(),
            null,
            allowedModels,
            provider.Purpose)
        {
            ConnectorPluginKey = provider.ConnectorPluginKey,
            CredentialBinding = new ProviderCredentialBinding(
                provider.SourceTokenSecretReferenceId,
                ProviderCredentialPurpose.SourceAccessToken,
                ProviderCredentialConsumerKind.Source,
                provider.SourceId),
            NetworkAccessPolicy = provider.NetworkPolicy switch
            {
                SharedProviderSourceNetworkPolicy.PublicOnly =>
                    ProviderNetworkAccessPolicy.PublicOnly,
                SharedProviderSourceNetworkPolicy.AllowPrivateNetwork =>
                    ProviderNetworkAccessPolicy.AllowPrivateNetwork,
                _ => throw new InvalidOperationException(
                    "The shared-provider source network policy is invalid.")
            },
            FeatureConstraints = new ProviderFeatureConstraints(
                provider.SupportsStructuredOutput,
                provider.SupportsVision,
                AllowsNativeTools: false,
                AllowsHostedMcp: false,
                AllowsServiceManagedHistory: false,
                AllowsCompaction: false),
            ModelSelectionConstraint =
                new ProviderModelSelectionConstraint(allowedModels),
            IsPrivateProvider = false,
            ModelPrices = [],
            Tags = provider.Tags
        };
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
            ConnectorPluginKey = OllamaRemoteProviderAdapter.PluginKey,
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
            ScenarioHarnessProviderAdapter.PluginKey =>
                ScenarioHarnessProviderAdapter.DefaultModel,
            ProcessMockProviderAdapter.PluginKey =>
                ProcessMockProviderAdapter.DefaultModel,
            OpenAiProviderAdapter.PluginKey =>
                OpenAiProviderAdapter.DefaultModel,
            ComfyUiProviderAdapter.PluginKey =>
                ComfyUiProviderAdapter.DefaultModel,
            _ => "llama3.1"
        };
    }

    private static IReadOnlyList<string>
        ResolveWorkspaceProviderSuggestedModels(
            WorkspaceProviderProfile provider,
            AgentFrameworkProviderKind mappedKind,
            ProviderProfilePurpose mappedPurpose)
    {
        IReadOnlyList<string> defaultModels =
            string.IsNullOrWhiteSpace(provider.DefaultModel)
                ? []
                : [provider.DefaultModel.Trim()];
        var persistedModels =
            AgentFrameworkProviderMetadata.ReadSuggestedModels(provider);
        if (provider.ConnectorPluginKey != OpenAiProviderAdapter.PluginKey ||
            mappedKind != AgentFrameworkProviderKind.OpenAi ||
            mappedPurpose != ProviderProfilePurpose.Chat)
        {
            return defaultModels
                .Concat(persistedModels)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        return ManagedSeedProviderFallbacks.OpenAiSuggestedModels
            .Concat(persistedModels)
            .Concat(defaultModels)
            .Where(model => !string.IsNullOrWhiteSpace(model))
            .Select(model => model.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IReadOnlyList<string> ResolveWorkspaceProviderTags(
        WorkspaceProviderProfile provider,
        AgentFrameworkProviderKind mappedKind,
        ProviderTransportKind mappedTransport)
    {
        var storedTags = AgentFrameworkProviderMetadata.ReadTags(provider);
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
            ScenarioHarnessProviderAdapter.PluginKey =>
                AgentFrameworkProviderKind.OpenAi,
            ProcessMockProviderAdapter.PluginKey =>
                AgentFrameworkProviderKind.OpenAi,
            OpenAiProviderAdapter.PluginKey =>
                AgentFrameworkProviderKind.OpenAi,
            ComfyUiProviderAdapter.PluginKey =>
                AgentFrameworkProviderKind.ComfyUi,
            OllamaProviderAdapter.PluginKey or
                OllamaRemoteProviderAdapter.PluginKey =>
                AgentFrameworkProviderKind.Ollama,
            _ => throw new InvalidOperationException(
                $"No AgentFramework provider kind mapping exists for connector plugin '{connectorPluginKey}'.")
        };
    }

    private static ProviderTransportKind ResolveLegacyMappedTransport(
        WorkspaceProviderProfile provider)
    {
        return provider.ConnectorPluginKey switch
        {
            ScenarioHarnessProviderAdapter.PluginKey =>
                ProviderTransportKind.Responses,
            ProcessMockProviderAdapter.PluginKey =>
                ProviderTransportKind.Responses,
            OpenAiProviderAdapter.PluginKey
                when IsOpenAiChatCompletionsProvider(provider) =>
                ProviderTransportKind.ChatCompletions,
            OpenAiProviderAdapter.PluginKey =>
                ProviderTransportKind.Responses,
            ComfyUiProviderAdapter.PluginKey =>
                ProviderTransportKind.ChatCompletions,
            OllamaProviderAdapter.PluginKey or
                OllamaRemoteProviderAdapter.PluginKey =>
                ProviderTransportKind.ChatCompletions,
            _ => throw new InvalidOperationException(
                $"No AgentFramework provider transport mapping exists for connector plugin '{provider.ConnectorPluginKey}'.")
        };
    }

    private static ProviderProfilePurpose ResolveLegacyMappedPurpose(
        WorkspaceProviderProfile provider)
    {
        if (provider.ConnectorPluginKey == ComfyUiProviderAdapter.PluginKey)
        {
            return ProviderProfilePurpose.ImageGeneration;
        }

        return provider.ConnectorPluginKey == OpenAiProviderAdapter.PluginKey &&
               LooksLikeLegacyOpenAiImageGenerationProvider(provider)
            ? ProviderProfilePurpose.ImageGeneration
            : ProviderProfilePurpose.Chat;
    }

    private static bool LooksLikeLegacyOpenAiImageGenerationProvider(
        WorkspaceProviderProfile provider)
    {
        if (provider.Name.Contains("image", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (LooksLikeOpenAiImageGenerationModel(provider.DefaultModel))
        {
            return true;
        }

        return AgentFrameworkProviderMetadata.ReadTags(provider)
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
        WorkspaceProviderProfile provider)
    {
        return provider.ApiKeySecretId.HasValue
            ? AgentFrameworkProviderMetadata.CreateSecretReference(provider.ApiKeySecretId.Value)
            : string.Empty;
    }

    private static bool IsOpenAiChatCompletionsProvider(
        WorkspaceProviderProfile provider)
    {
        return string.Equals(
            provider.Name,
            OpenAiChatCompletionsProviderName,
            StringComparison.OrdinalIgnoreCase);
    }
}
