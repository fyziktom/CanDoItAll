using System.Text.Json;
using System.Text.Json.Nodes;

namespace CanDoItAll.AgentFramework.Models;

public static class ManagedSeedProviderFallbacks
{
    private const string OpenAiApiKeyVariableName = "OPENAI_API_KEY";
    private const string ProviderRepairFallbackOverrideMarker = "providerRepairFallbackOverride";

    private static readonly IReadOnlySet<string> ManagedSeedOpenAiProviderNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "OpenAI default",
        "OpenAI chat completions"
    };

    private static readonly IReadOnlySet<string> ManagedSeedTemplateKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "portfolio-architect",
        "delivery-manager",
        "delivery-qa-observer",
        "programming-workspace-analyst",
        "code-review-lead",
        "ui-review-lead",
        "security-reviewer",
        "release-readiness-manager",
        "hr-staffing-manager",
        "research-deep-dive-analyst",
        "dotnet-solution-architect",
        "dotnet-application-developer",
        "blazor-application-developer",
        "dotnet-qa-review-lead",
        "javascript-solution-architect",
        "javascript-application-developer",
        "javascript-qa-review-lead",
        "business-strategist",
        "financial-strategist",
        "marketing-specialist",
        "mail-triage-analyst",
        "spreadsheet-analyst",
        "app-screenshot-capture-agent",
        "screenshot-review-storage-agent",
        "layout-image-generation-agent"
    };

    private static readonly IReadOnlyList<string> ManagedSeedFallbackSuggestedModels =
    [
        "gptoss32k:latest",
        "gptoss64k:latest",
        "gpt-oss:20b",
        "qwen3.5:9b",
        "phi4-16k:latest"
    ];

    private static readonly IReadOnlyList<string> ManagedSeedOpenAiSuggestedModels =
    [
        OpenAiDefaultModel,
        OpenAiModelIds.Gpt56,
        OpenAiModelIds.Gpt56Luna,
        OpenAiModelIds.Gpt56Terra,
        OpenAiModelIds.Gpt56Sol,
        "gpt-5.4",
        "gpt-5-mini",
        "gpt-4.1-mini",
        "gpt-4.1"
    ];

    public const string OpenAiDefaultProviderName = "OpenAI default";
    public const string OpenAiChatCompletionsProviderName = "OpenAI chat completions";
    public const string OpenAiBaseUrl = "https://api.openai.com/v1";
    public const string OpenAiDefaultModel = "gpt-5.4-mini";
    public const int OpenAiDefaultTimeoutSeconds = 120;
    public const string DefaultReasoningEffort = "medium";
    public const string FallbackProviderName = "Remote Ollama";
    public const string FallbackBaseUrl = "http://192.168.10.132:11434";
    public const string FallbackModel = "gptoss32k:latest";
    public const int FallbackTimeoutSeconds = 600;
    public const int FallbackMaxOutputTokens = 4096;

    public static IReadOnlyList<string> OpenAiSuggestedModels => ManagedSeedOpenAiSuggestedModels;

    public static ProviderProfile Apply(
        AgentDefinition agent,
        ProviderProfile provider,
        string? openAiApiKeyOverride = null)
    {
        ArgumentNullException.ThrowIfNull(agent);
        ArgumentNullException.ThrowIfNull(provider);

        if (!ShouldUseFallback(agent, provider, openAiApiKeyOverride))
        {
            return provider;
        }

        return provider with
        {
            Name = FallbackProviderName,
            Kind = ProviderKind.Ollama,
            BaseUrl = FallbackBaseUrl,
            ApiKeyEnvironmentVariable = string.Empty,
            DefaultModel = FallbackModel,
            Transport = ProviderTransportKind.ChatCompletions,
            IsEnabled = true,
            SupportsStreaming = true,
            SupportsTools = true,
            PreferFrameworkManagedChatHistory = true,
            SupportsBackgroundResponses = false,
            ConfigurationJson = CreateFallbackConfigurationJson("managed-seed-openai"),
            Notes = "Managed-seed fallback provider used for generated seed agents.",
            HealthStatus = "Fallback active",
            SuggestedModels = ManagedSeedFallbackSuggestedModels,
            Tags = ["ollama", "remote", "fallback", "chat"]
        };
    }

    public static ProviderProfile ResolvePreferredProvider(
        AgentDefinition agent,
        ProviderProfile? registryProvider,
        ProviderProfile? catalogShadowProvider,
        string? openAiApiKeyOverride = null)
    {
        ArgumentNullException.ThrowIfNull(agent);

        if (registryProvider is not null)
        {
            return Apply(agent, registryProvider, openAiApiKeyOverride);
        }

        if (catalogShadowProvider is not null)
        {
            return Apply(agent, catalogShadowProvider, openAiApiKeyOverride);
        }

        throw new InvalidOperationException("The selected agent does not have a provider profile.");
    }

    public static string ResolveModel(
        AgentDefinition agent,
        ProviderProfile provider,
        string? openAiApiKeyOverride = null)
    {
        ArgumentNullException.ThrowIfNull(agent);
        ArgumentNullException.ThrowIfNull(provider);

        if (IsFallbackProvider(provider) &&
            !IsProviderSupportedModel(agent.Model, provider))
        {
            return provider.DefaultModel;
        }

        if (ShouldUseFallback(agent, provider, openAiApiKeyOverride))
        {
            return FallbackModel;
        }

        if (string.IsNullOrWhiteSpace(agent.Model) ||
            IsOpenAiProvider(provider) && IsKnownOllamaFallbackModel(agent.Model) ||
            provider.Kind == ProviderKind.Ollama &&
            IsKnownManagedSeedOpenAiModel(agent.Model) &&
            !IsProviderSupportedModel(agent.Model, provider))
        {
            return provider.DefaultModel;
        }

        return agent.Model;
    }

    public static string EnsureDefaultReasoningConfigurationJson(
        string configurationJson,
        string? history = null)
    {
        var canonicalConfigurationJson = AgentThinkingEffortPolicy.WriteProviderDefault(
            configurationJson,
            AgentReasoningEffortLevel.Medium);
        if (!string.IsNullOrWhiteSpace(history))
        {
            var configuration = JsonNode.Parse(canonicalConfigurationJson)!.AsObject();
            configuration["history"] ??= history;
            return configuration.ToJsonString();
        }

        return canonicalConfigurationJson;
    }

    public static bool ShouldUseFallback(
        AgentDefinition agent,
        ProviderProfile provider,
        string? openAiApiKeyOverride = null)
    {
        ArgumentNullException.ThrowIfNull(agent);
        ArgumentNullException.ThrowIfNull(provider);

        if (IsFallbackProvider(provider))
        {
            return false;
        }

        if (!IsManagedSeedOpenAiProvider(provider))
        {
            return false;
        }

        return false;
    }

    public static bool IsManagedSeedAgent(AgentDefinition agent)
    {
        ArgumentNullException.ThrowIfNull(agent);

        return AgentManagedSeedCustomizationMetadata.HasManagedSeedOwnership(agent.ConfigurationJson) ||
               ManagedSeedTemplateKeys.Contains(agent.TemplateKey);
    }

    public static bool HasProviderRepairFallbackOverride(AgentDefinition agent)
    {
        ArgumentNullException.ThrowIfNull(agent);

        return HasProviderRepairFallbackOverride(agent.ConfigurationJson);
    }

    public static bool HasProviderRepairFallbackOverride(string configurationJson)
    {
        var configuration = ParseConfigurationObject(configurationJson);
        return configuration[ProviderRepairFallbackOverrideMarker] is JsonValue value &&
               value.TryGetValue<bool>(out var isEnabled) &&
               isEnabled;
    }

    public static string EnableProviderRepairFallbackOverride(string configurationJson)
    {
        var configuration = ParseConfigurationObject(configurationJson);
        configuration[ProviderRepairFallbackOverrideMarker] = true;
        return configuration.ToJsonString();
    }

    private static bool IsFallbackProvider(ProviderProfile provider)
    {
        return provider.Kind == ProviderKind.Ollama &&
               string.Equals(provider.Name, FallbackProviderName, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(provider.BaseUrl, FallbackBaseUrl, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsManagedSeedOpenAiProvider(ProviderProfile provider)
    {
        return provider.Kind is ProviderKind.OpenAi or ProviderKind.AzureOpenAi &&
               ManagedSeedOpenAiProviderNames.Contains(provider.Name) &&
               provider.BaseUrl.Contains("api.openai.com", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsGeneratedManagedSeedFallbackProvider(ProviderProfile provider)
    {
        return IsFallbackProvider(provider) ||
               provider.ConfigurationJson.Contains("\"fallback\"", StringComparison.OrdinalIgnoreCase) ||
               provider.Notes.Contains("managed-seed fallback", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsOpenAiProvider(ProviderProfile provider)
    {
        return provider.Kind is ProviderKind.OpenAi or ProviderKind.AzureOpenAi;
    }

    private static bool IsKnownOllamaFallbackModel(string model)
    {
        return ManagedSeedFallbackSuggestedModels.Contains(model, StringComparer.OrdinalIgnoreCase);
    }

    private static bool IsKnownManagedSeedOpenAiModel(string model)
    {
        return ManagedSeedOpenAiSuggestedModels.Contains(model, StringComparer.OrdinalIgnoreCase);
    }

    private static bool IsProviderSupportedModel(
        string model,
        ProviderProfile provider)
    {
        return !string.IsNullOrWhiteSpace(model) &&
               (string.Equals(model, provider.DefaultModel, StringComparison.OrdinalIgnoreCase) ||
                provider.SuggestedModels.Contains(model, StringComparer.OrdinalIgnoreCase));
    }

    private static ProviderProfile CreateOpenAiDefaultProvider(ProviderProfile provider)
    {
        return provider with
        {
            Name = OpenAiDefaultProviderName,
            Kind = ProviderKind.OpenAi,
            BaseUrl = OpenAiBaseUrl,
            ApiKeyEnvironmentVariable = OpenAiApiKeyVariableName,
            DefaultModel = OpenAiDefaultModel,
            Transport = ProviderTransportKind.Responses,
            IsEnabled = true,
            SupportsStreaming = true,
            SupportsTools = true,
            PreferFrameworkManagedChatHistory = false,
            SupportsBackgroundResponses = true,
            ConfigurationJson = CreateOpenAiConfigurationJson("service-managed"),
            Notes = "OpenAI Responses provider selected for managed seed agents.",
            HealthStatus = "OpenAI active",
            SuggestedModels = ManagedSeedOpenAiSuggestedModels,
            Tags = ["openai", "cloud", "responses", "chat"]
        };
    }

    public static string CreateFallbackConfigurationJson(string fallbackReason)
        => EnsureFallbackRuntimeConfigurationJson("{}", fallbackReason);

    public static string EnsureFallbackRuntimeConfigurationJson(string configurationJson, string fallbackReason)
    {
        var configuration = JsonNode.Parse(
            AgentThinkingEffortPolicy.WriteProviderDefault(configurationJson, null))!.AsObject();

        configuration["history"] = "framework-managed";
        configuration["fallback"] = fallbackReason;
        configuration["timeoutSeconds"] = FallbackTimeoutSeconds;

        var modelParameters = configuration[AgentProviderModelParameterPolicy.ModelParametersConfigurationPropertyName] as JsonObject ?? [];
        modelParameters[AgentProviderModelParameterPolicy.OllamaNumPredictConfigurationPropertyName] = FallbackMaxOutputTokens;
        configuration[AgentProviderModelParameterPolicy.ModelParametersConfigurationPropertyName] = modelParameters;

        return configuration.ToJsonString();
    }

    private static string CreateOpenAiConfigurationJson(string history)
    {
        return EnsureDefaultReasoningConfigurationJson("{}", history);
    }

    private static JsonObject ParseConfigurationObject(string configurationJson)
    {
        if (string.IsNullOrWhiteSpace(configurationJson))
        {
            return [];
        }

        try
        {
            return JsonNode.Parse(configurationJson) as JsonObject ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
