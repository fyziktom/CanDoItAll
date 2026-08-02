using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Providers;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OllamaSharp;
using OllamaSharp.Models;
using OpenAI;
using OpenAI.Chat;
using OpenAI.Responses;
using System.Text.Json;

namespace CanDoItAll.AgentFramework.Maf;

internal interface IMafProviderCredentialService
{
    ProviderCredentialResolution Resolve(ProviderProfile provider);

    string ResolveOpenAiCredentialOverride(ProviderProfile provider);

    void PromoteResolvedProviderCredentialEnvironment(ProviderProfile provider);

    void PromoteProviderCredentialEnvironment(ProviderProfile provider, ProviderCredentialResolution credential);
}

internal interface IMafProviderAgentFactory
{
    AIAgent CreateFrameworkAgent(
        ProviderProfile provider,
        string model,
        ChatClientAgentOptions options,
        bool frameworkManagedHistory,
        bool allowBackgroundResponses,
        IServiceProvider services);
}

internal sealed class MafProviderCredentialService(IServiceProvider services) : IMafProviderCredentialService
{
    private static readonly IAgentProviderCredentialResolver FallbackProviderCredentialResolver = new EnvironmentVariableAgentProviderCredentialResolver();

    public ProviderCredentialResolution Resolve(ProviderProfile provider)
    {
        ArgumentNullException.ThrowIfNull(provider);

        var primaryResolution = services.GetService<IAgentProviderCredentialResolver>()?.Resolve(provider);
        if (primaryResolution is { IsResolved: true })
        {
            return primaryResolution;
        }

        var configurationFallback = TryResolveConfigurationCredential(provider);
        if (configurationFallback.IsResolved)
        {
            return configurationFallback;
        }

        var environmentFallback = FallbackProviderCredentialResolver.Resolve(provider);
        if (environmentFallback.IsResolved)
        {
            return environmentFallback;
        }

        if (primaryResolution is null)
        {
            return environmentFallback;
        }

        return BuildUnresolvedCredentialResult(provider, primaryResolution, environmentFallback);
    }

    public string ResolveOpenAiCredentialOverride(ProviderProfile provider)
    {
        ArgumentNullException.ThrowIfNull(provider);

        if (provider.Kind is not (ProviderKind.OpenAi or ProviderKind.AzureOpenAi))
        {
            return "resolved";
        }

        var credential = Resolve(provider);
        if (!credential.IsResolved)
        {
            return string.Empty;
        }

        PromoteProviderCredentialEnvironment(provider, credential);
        return "resolved";
    }

    public void PromoteResolvedProviderCredentialEnvironment(ProviderProfile provider)
    {
        ArgumentNullException.ThrowIfNull(provider);

        if (provider.Kind is not (ProviderKind.OpenAi or ProviderKind.AzureOpenAi))
        {
            return;
        }

        var credential = Resolve(provider);
        if (credential.IsResolved)
        {
            PromoteProviderCredentialEnvironment(provider, credential);
        }
    }

    public void PromoteProviderCredentialEnvironment(
        ProviderProfile provider,
        ProviderCredentialResolution credential)
    {
        ArgumentNullException.ThrowIfNull(provider);

        if (!credential.IsResolved ||
            !credential.ShouldPromoteToProcessEnvironment)
        {
            return;
        }

        AgentProviderEnvironmentCredential.PromoteProcessValue(provider.ApiKeyEnvironmentVariable, credential.ApiKey);
        if (provider.Kind == ProviderKind.OpenAi)
        {
            AgentProviderEnvironmentCredential.PromoteProcessValue(MafProviderRuntimeSettings.OpenAiApiKeyEnvironmentVariable, credential.ApiKey);
        }
    }

    private ProviderCredentialResolution TryResolveConfigurationCredential(ProviderProfile provider)
    {
        var configuration = services.GetService<IConfiguration>();
        if (configuration is null)
        {
            return new ProviderCredentialResolution(string.Empty, "application configuration", "Application configuration is not available.");
        }

        foreach (var key in EnumerateCredentialConfigurationKeys(provider))
        {
            var configuredValue = configuration[key];
            if (string.IsNullOrWhiteSpace(configuredValue))
            {
                continue;
            }

            var trimmedValue = configuredValue.Trim();
            PromoteResolvedConfigurationCredential(provider, key, trimmedValue);
            return new ProviderCredentialResolution(
                trimmedValue,
                $"application configuration key '{key}'",
                string.Empty);
        }

        return new ProviderCredentialResolution(
            string.Empty,
            "application configuration",
            "No matching application configuration key contained a usable API key.");
    }

    private static IEnumerable<string> EnumerateCredentialConfigurationKeys(ProviderProfile provider)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(provider.ApiKeyEnvironmentVariable))
        {
            var providerKey = provider.ApiKeyEnvironmentVariable.Trim();
            if (seen.Add(providerKey))
            {
                yield return providerKey;
            }
        }

        if (provider.Kind is ProviderKind.OpenAi or ProviderKind.AzureOpenAi &&
            seen.Add(MafProviderRuntimeSettings.OpenAiApiKeyEnvironmentVariable))
        {
            yield return MafProviderRuntimeSettings.OpenAiApiKeyEnvironmentVariable;
        }
    }

    private static ProviderCredentialResolution BuildUnresolvedCredentialResult(
        ProviderProfile provider,
        ProviderCredentialResolution primaryResolution,
        ProviderCredentialResolution environmentFallback)
    {
        var failureMessages = new[]
            {
                primaryResolution.FailureMessage,
                environmentFallback.FailureMessage
            }
            .Where(message => !string.IsNullOrWhiteSpace(message))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var failureMessage = failureMessages.Count == 0
            ? $"Provider '{provider.Name}' did not resolve a usable API key."
            : string.Join(" ", failureMessages);

        return new ProviderCredentialResolution(
            string.Empty,
            primaryResolution.ResolutionSource,
            failureMessage);
    }

    private static void PromoteResolvedConfigurationCredential(
        ProviderProfile provider,
        string configurationKey,
        string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(provider.ApiKeyEnvironmentVariable))
        {
            AgentProviderEnvironmentCredential.PromoteProcessValue(provider.ApiKeyEnvironmentVariable, value);
        }

        if (provider.Kind is ProviderKind.OpenAi or ProviderKind.AzureOpenAi ||
            string.Equals(configurationKey, MafProviderRuntimeSettings.OpenAiApiKeyEnvironmentVariable, StringComparison.OrdinalIgnoreCase))
        {
            AgentProviderEnvironmentCredential.PromoteProcessValue(MafProviderRuntimeSettings.OpenAiApiKeyEnvironmentVariable, value);
        }
    }
}

internal sealed class MafProviderAgentFactory(IMafProviderCredentialService credentialService) : IMafProviderAgentFactory
{
    public AIAgent CreateFrameworkAgent(
        ProviderProfile provider,
        string model,
        ChatClientAgentOptions options,
        bool frameworkManagedHistory,
        bool allowBackgroundResponses,
        IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(services);

        return provider.Kind switch
        {
            ProviderKind.OpenAi => CreateOpenAiAgent(provider, model, options, frameworkManagedHistory, allowBackgroundResponses, services),
            ProviderKind.AzureOpenAi => CreateAzureOpenAiAgent(provider, model, options, frameworkManagedHistory, allowBackgroundResponses, services),
            ProviderKind.Ollama => CreateOllamaAgent(provider, model, options, allowBackgroundResponses, services),
            _ => throw new InvalidOperationException($"Unsupported provider kind '{provider.Kind}'.")
        };
    }

    private AIAgent CreateOpenAiAgent(
        ProviderProfile provider,
        string model,
        ChatClientAgentOptions options,
        bool frameworkManagedHistory,
        bool allowBackgroundResponses,
        IServiceProvider services)
    {
        var credential = credentialService.Resolve(provider);
        if (!credential.IsResolved)
        {
            throw new InvalidOperationException(credential.FailureMessage);
        }

        credentialService.PromoteProviderCredentialEnvironment(provider, credential);
        var clientOptions = CreateOpenAiClientOptions(provider);
        var client = new OpenAIClient(
            credential: new System.ClientModel.ApiKeyCredential(credential.ApiKey),
            options: clientOptions);

        return provider.Transport switch
        {
            ProviderTransportKind.ChatCompletions => client
                .GetChatClient(model)
                .AsAIAgent(
                    options: options,
                    clientFactory: chatClient => AddRuntimePolicies(chatClient, provider, model, allowBackgroundResponses, services),
                    services: services),
            ProviderTransportKind.Responses when frameworkManagedHistory => AddRuntimePolicies(
                    client
                        .GetResponsesClient()
                        .AsIChatClientWithStoredOutputDisabled(
                            model: model,
                            includeReasoningEncryptedContent: ShouldIncludeReasoningEncryptedContentForStoredOutputDisabledResponses(provider, frameworkManagedHistory)),
                    provider,
                    model,
                    allowBackgroundResponses,
                    services)
                .AsAIAgent(options: options, services: services),
            ProviderTransportKind.Responses => client
                .GetResponsesClient()
                .AsAIAgent(
                    options: options,
                    model: model,
                    clientFactory: chatClient => AddRuntimePolicies(chatClient, provider, model, allowBackgroundResponses, services),
                    services: services),
            _ => throw new InvalidOperationException($"Unsupported transport '{provider.Transport}' for provider '{provider.Name}'.")
        };
    }

    private AIAgent CreateAzureOpenAiAgent(
        ProviderProfile provider,
        string model,
        ChatClientAgentOptions options,
        bool frameworkManagedHistory,
        bool allowBackgroundResponses,
        IServiceProvider services)
    {
        var credential = credentialService.Resolve(provider);
        if (!credential.IsResolved)
        {
            throw new InvalidOperationException(credential.FailureMessage);
        }

        credentialService.PromoteProviderCredentialEnvironment(provider, credential);
        var client = new OpenAIClient(
            credential: new System.ClientModel.ApiKeyCredential(credential.ApiKey),
            options: new OpenAIClientOptions
            {
                Endpoint = AzureOpenAiEndpoint.Parse(provider).V1Endpoint,
                NetworkTimeout = MafProviderRuntimeSettings.ResolveNetworkTimeout(provider)
            });

        return provider.Transport switch
        {
            ProviderTransportKind.ChatCompletions => client
                .GetChatClient(model)
                .AsAIAgent(
                    options: options,
                    clientFactory: chatClient => AddRuntimePolicies(chatClient, provider, model, allowBackgroundResponses, services),
                    services: services),
            ProviderTransportKind.Responses when frameworkManagedHistory => AddRuntimePolicies(
                    client
                        .GetResponsesClient()
                        .AsIChatClientWithStoredOutputDisabled(
                            model: model,
                            includeReasoningEncryptedContent: ShouldIncludeReasoningEncryptedContentForStoredOutputDisabledResponses(provider, frameworkManagedHistory)),
                    provider,
                    model,
                    allowBackgroundResponses,
                    services)
                .AsAIAgent(options: options, services: services),
            ProviderTransportKind.Responses => client
                .GetResponsesClient()
                .AsAIAgent(
                    options: options,
                    model: model,
                    clientFactory: chatClient => AddRuntimePolicies(chatClient, provider, model, allowBackgroundResponses, services),
                    services: services),
            _ => throw new InvalidOperationException($"Unsupported transport '{provider.Transport}' for provider '{provider.Name}'.")
        };
    }

    private static AIAgent CreateOllamaAgent(
        ProviderProfile provider,
        string model,
        ChatClientAgentOptions options,
        bool allowBackgroundResponses,
        IServiceProvider services)
    {
        var httpClient = new HttpClient
        {
            BaseAddress = new Uri(provider.BaseUrl, UriKind.Absolute),
            Timeout = MafProviderRuntimeSettings.ResolveNetworkTimeout(provider)
        };
        IChatClient chatClient = new DefaultOllamaOptionsChatClient(
            new OllamaApiClient(httpClient, model, jsonSerializerContext: null),
            AgentProviderModelParameterPolicy.ResolveOllamaMaxOutputTokensOrDefault(provider.ConfigurationJson, string.Empty),
            AgentProviderModelParameterPolicy.ResolveOllamaThinkOrDefault(provider.ConfigurationJson, string.Empty));
        return AddRuntimePolicies(chatClient, provider, model, allowBackgroundResponses, services)
            .AsAIAgent(options: options);
    }

    private static IChatClient AddRuntimePolicies(
        IChatClient chatClient,
        ProviderProfile provider,
        string model,
        bool allowBackgroundResponses,
        IServiceProvider services)
    {
        chatClient = AddOpenAiChatCompletionsCompatibility(
            chatClient,
            provider,
            model,
            services);
        if (chatClient.GetService<EmptyCompletionRetryChatClient>() is not null)
        {
            throw new InvalidOperationException(
                $"Provider '{provider.Name}' model '{model}' already contains empty-completion recovery.");
        }

        var logger = services
            .GetService<Microsoft.Extensions.Logging.ILoggerFactory>()
            ?.CreateLogger<EmptyCompletionRetryChatClient>();
        return new EmptyCompletionRetryChatClient(
            chatClient,
            provider,
            model,
            allowBackgroundResponses,
            logger);
    }

    private static IChatClient AddOpenAiChatCompletionsCompatibility(
        IChatClient chatClient,
        ProviderProfile provider,
        string model,
        IServiceProvider services)
    {
        if (provider.Kind != ProviderKind.OpenAi ||
            provider.Transport != ProviderTransportKind.ChatCompletions)
        {
            return chatClient;
        }

        if (chatClient.GetService<OpenAiChatCompletionsCompatibilityChatClient>() is not null)
        {
            throw new InvalidOperationException(
                $"Provider '{provider.Name}' model '{model}' already contains OpenAI Chat Completions compatibility handling.");
        }

        var logger = services
            .GetService<ILoggerFactory>()
            ?.CreateLogger<OpenAiChatCompletionsCompatibilityChatClient>();
        return new OpenAiChatCompletionsCompatibilityChatClient(
            chatClient,
            provider,
            model,
            logger);
    }

    private static OpenAIClientOptions CreateOpenAiClientOptions(ProviderProfile provider)
    {
        ArgumentNullException.ThrowIfNull(provider);

        var options = new OpenAIClientOptions
        {
            NetworkTimeout = MafProviderRuntimeSettings.ResolveNetworkTimeout(provider)
        };
        if (!MafProviderRuntimeSettings.ShouldUseDefaultOpenAiEndpoint(provider.BaseUrl))
        {
            options.Endpoint = new Uri(provider.BaseUrl, UriKind.Absolute);
        }

        return options;
    }

    private static bool ShouldIncludeReasoningEncryptedContentForStoredOutputDisabledResponses(
        ProviderProfile provider,
        bool frameworkManagedHistory)
    {
        ArgumentNullException.ThrowIfNull(provider);

        if (!frameworkManagedHistory)
        {
            return false;
        }

        if (provider.Transport != ProviderTransportKind.Responses)
        {
            return false;
        }

        return false;
    }
}

internal sealed class DefaultOllamaOptionsChatClient(
    IChatClient innerClient,
    int defaultMaxOutputTokens,
    bool defaultThink) : DelegatingChatClient(innerClient)
{
    public override Task<ChatResponse> GetResponseAsync(
        IEnumerable<Microsoft.Extensions.AI.ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        return base.GetResponseAsync(messages, NormalizeOptions(options), cancellationToken);
    }

    public override IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<Microsoft.Extensions.AI.ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        return base.GetStreamingResponseAsync(messages, NormalizeOptions(options), cancellationToken);
    }

    private ChatOptions NormalizeOptions(ChatOptions? options)
    {
        var normalizedOptions = options?.Clone() ?? new ChatOptions();
        normalizedOptions.MaxOutputTokens ??= defaultMaxOutputTokens;
        normalizedOptions.AdditionalProperties ??= [];

        if (!normalizedOptions.AdditionalProperties.ContainsKey(OllamaOption.NumPredict.Name))
        {
            normalizedOptions.AddOllamaOption(OllamaOption.NumPredict, normalizedOptions.MaxOutputTokens.Value);
        }

        if (!normalizedOptions.AdditionalProperties.ContainsKey(OllamaOption.Think.Name))
        {
            normalizedOptions.AddOllamaOption(OllamaOption.Think, defaultThink);
        }

        return normalizedOptions;
    }
}

internal static class MafProviderRuntimeSettings
{
    public const string OpenAiApiKeyEnvironmentVariable = "OPENAI_API_KEY";

    private static readonly TimeSpan ModelNetworkTimeout = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan StreamingIdleTimeoutGrace = TimeSpan.FromSeconds(30);

    public static TimeSpan ResolveNetworkTimeout(ProviderProfile provider)
    {
        ArgumentNullException.ThrowIfNull(provider);

        if (string.IsNullOrWhiteSpace(provider.ConfigurationJson))
        {
            return ModelNetworkTimeout;
        }

        try
        {
            using var document = JsonDocument.Parse(provider.ConfigurationJson);
            if (!document.RootElement.TryGetProperty("timeoutSeconds", out var timeoutElement) ||
                !timeoutElement.TryGetInt32(out var timeoutSeconds))
            {
                return ModelNetworkTimeout;
            }

            return TimeSpan.FromSeconds(Math.Clamp(timeoutSeconds, 5, 3600));
        }
        catch (JsonException)
        {
            return ModelNetworkTimeout;
        }
    }

    public static TimeSpan ResolveStreamingIdleTimeout(ProviderProfile provider)
        => ResolveNetworkTimeout(provider) + StreamingIdleTimeoutGrace;

    public static TimeSpan ResolveStreamingAbsoluteTimeout(ProviderProfile provider)
        => TimeSpan.FromTicks(checked(ResolveStreamingIdleTimeout(provider).Ticks * 2));

    public static bool ShouldUseDefaultOpenAiEndpoint(string? baseUrl)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return true;
        }

        var normalized = baseUrl.Trim().TrimEnd('/');
        return normalized.Equals("https://api.openai.com", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("https://api.openai.com/v1", StringComparison.OrdinalIgnoreCase);
    }
}
