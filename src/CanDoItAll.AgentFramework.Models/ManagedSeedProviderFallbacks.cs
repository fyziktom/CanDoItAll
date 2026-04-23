using System.Text.Json;
using System.Text.Json.Nodes;

namespace CanDoItAll.AgentFramework.Models;

public static class ManagedSeedProviderFallbacks
{
    private const string ManagedSeedMarker = "managedSeedVersion";
    private const string OpenAiApiKeyVariableName = "OPENAI_API_KEY";

    private static readonly IReadOnlySet<string> ManagedSeedOpenAiProviderNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "OpenAI default",
        "OpenAI chat completions"
    };

    private static readonly IReadOnlySet<string> ManagedSeedTemplateKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "portfolio-architect",
        "delivery-qa-observer",
        "programming-workspace-analyst",
        "code-review-lead",
        "ui-review-lead",
        "security-reviewer",
        "release-readiness-manager",
        "hr-staffing-manager",
        "research-deep-dive-analyst"
    };

    private static readonly IReadOnlyList<string> ManagedSeedFallbackSuggestedModels =
    [
        "gptoss32k:latest",
        "gptoss64k:latest",
        "gpt-oss:20b",
        "qwen3.5:9b",
        "phi4-16k:latest"
    ];

    public const string FallbackProviderName = "Remote Ollama";
    public const string FallbackBaseUrl = "http://192.168.10.132:11434";
    public const string FallbackModel = "gptoss32k:latest";
    public const int FallbackTimeoutSeconds = 600;

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
            SuggestedModels = ManagedSeedFallbackSuggestedModels
        };
    }

    public static ProviderProfile ApplyForManagedSqliteSeedProvider(
        ProviderProfile provider,
        bool isManagedSqliteProfile)
    {
        ArgumentNullException.ThrowIfNull(provider);

        if (!isManagedSqliteProfile)
        {
            return provider;
        }

        if (IsFallbackProvider(provider))
        {
            return provider with
            {
                DefaultModel = FallbackModel,
                ConfigurationJson = EnsureFallbackTimeout(provider.ConfigurationJson, "managed-sqlite-provider"),
                SuggestedModels = ManagedSeedFallbackSuggestedModels
            };
        }

        if (provider.Kind is not (ProviderKind.OpenAi or ProviderKind.AzureOpenAi))
        {
            return provider;
        }

        if (!ManagedSeedOpenAiProviderNames.Contains(provider.Name))
        {
            return provider;
        }

        if (!provider.BaseUrl.Contains("api.openai.com", StringComparison.OrdinalIgnoreCase))
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
            ConfigurationJson = CreateFallbackConfigurationJson("managed-sqlite-provider"),
            Notes = "Managed SQLite fallback provider resolved from the seed catalog.",
            HealthStatus = "Fallback active",
            SuggestedModels = ManagedSeedFallbackSuggestedModels
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

        return ShouldUseFallback(agent, provider, openAiApiKeyOverride)
            ? FallbackModel
            : string.IsNullOrWhiteSpace(agent.Model)
                ? provider.DefaultModel
                : agent.Model;
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
            return IsManagedSeedAgent(agent) || IsGeneratedManagedSeedFallbackProvider(provider);
        }

        if (!IsManagedSeedOpenAiProvider(provider))
        {
            return false;
        }

        if (IsManagedSeedAgent(agent))
        {
            return true;
        }

        var openAiApiKey = openAiApiKeyOverride ?? Environment.GetEnvironmentVariable(OpenAiApiKeyVariableName);
        return string.IsNullOrWhiteSpace(openAiApiKey);
    }

    public static bool IsManagedSeedAgent(AgentDefinition agent)
    {
        ArgumentNullException.ThrowIfNull(agent);

        return agent.ConfigurationJson.Contains(ManagedSeedMarker, StringComparison.OrdinalIgnoreCase) ||
               ManagedSeedTemplateKeys.Contains(agent.TemplateKey);
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

    private static bool IsGeneratedManagedSeedFallbackProvider(ProviderProfile provider)
    {
        return provider.ConfigurationJson.Contains("\"fallback\"", StringComparison.OrdinalIgnoreCase) ||
               provider.Notes.Contains("managed-seed fallback", StringComparison.OrdinalIgnoreCase) ||
               provider.Notes.Contains("managed SQLite fallback provider", StringComparison.OrdinalIgnoreCase);
    }

    private static string CreateFallbackConfigurationJson(string fallbackReason)
    {
        var configuration = new JsonObject
        {
            ["history"] = "framework-managed",
            ["fallback"] = fallbackReason,
            ["timeoutSeconds"] = FallbackTimeoutSeconds
        };

        return configuration.ToJsonString();
    }

    private static string EnsureFallbackTimeout(string configurationJson, string fallbackReason)
    {
        JsonObject configuration;
        if (string.IsNullOrWhiteSpace(configurationJson))
        {
            configuration = [];
        }
        else
        {
            try
            {
                configuration = JsonNode.Parse(configurationJson) as JsonObject ?? [];
            }
            catch (JsonException)
            {
                configuration = [];
            }
        }

        configuration["history"] ??= "framework-managed";
        configuration["fallback"] ??= fallbackReason;
        configuration["timeoutSeconds"] = FallbackTimeoutSeconds;
        return configuration.ToJsonString();
    }
}
