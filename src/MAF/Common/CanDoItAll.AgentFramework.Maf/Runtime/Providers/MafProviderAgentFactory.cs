using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Providers;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OllamaSharp;
using OllamaSharp.Models;
using OpenAI;
using OpenAI.Chat;
using OpenAI.Responses;
using System.ClientModel.Primitives;
using System.Text.Json;

namespace CanDoItAll.AgentFramework.Maf;

internal interface IMafProviderCredentialService
{
    ProviderCredentialResolution Resolve(ProviderProfile provider);

    string ResolveOpenAiCredentialOverride(ProviderProfile provider);
}

internal interface IMafProviderAgentFactory
{
    AIAgent CreateFrameworkAgent(
        ProviderProfile provider,
        string model,
        ChatClientAgentOptions options,
        bool frameworkManagedHistory,
        bool allowBackgroundResponses);
}

internal sealed class MafProviderCredentialService(
    IAgentProviderCredentialResolver? credentialResolver = null,
    IConfiguration? configuration = null) : IMafProviderCredentialService
{
    private static readonly IAgentProviderCredentialResolver FallbackProviderCredentialResolver = new EnvironmentVariableAgentProviderCredentialResolver();

    public ProviderCredentialResolution Resolve(ProviderProfile provider)
    {
        ArgumentNullException.ThrowIfNull(provider);

        var primaryResolution = credentialResolver?.Resolve(provider);
        if (primaryResolution is { IsResolved: true })
        {
            return primaryResolution;
        }

        if (HasExplicitCredentialBinding(provider))
        {
            return primaryResolution ?? new ProviderCredentialResolution(
                string.Empty,
                "explicit provider credential binding",
                "The explicitly bound provider credential could not be resolved.");
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

        return "resolved";
    }

    private ProviderCredentialResolution TryResolveConfigurationCredential(ProviderProfile provider)
    {
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

            yield break;
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

    private static bool HasExplicitCredentialBinding(ProviderProfile provider)
    {
        if (!string.IsNullOrWhiteSpace(provider.ApiKeyEnvironmentVariable) &&
            provider.ApiKeyEnvironmentVariable.Trim().StartsWith(
                "secret:",
                StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(provider.ConfigurationJson))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(provider.ConfigurationJson);
            return document.RootElement.ValueKind == JsonValueKind.Object &&
                   document.RootElement.EnumerateObject().Any(property =>
                       string.Equals(
                           property.Name,
                           ProviderProfileMetadataPropertyNames.SecretRecordId,
                           StringComparison.OrdinalIgnoreCase) &&
                       property.Value.ValueKind == JsonValueKind.String &&
                       !string.IsNullOrWhiteSpace(property.Value.GetString()));
        }
        catch (JsonException)
        {
            return false;
        }
    }
}

internal sealed class MafProviderAgentFactory : IMafProviderAgentFactory
{
    private readonly IMafProviderCredentialService credentialService;
    private readonly IMafProviderStreamingDispatchGate dispatchGate;
    private readonly IProviderHttpClientSelector? httpClientSelector;
    private readonly ILoggerFactory loggerFactory;

    public MafProviderAgentFactory(
        IMafProviderCredentialService credentialService,
        IMafProviderStreamingDispatchGate dispatchGate,
        ILoggerFactory? loggerFactory = null,
        IProviderHttpClientSelector? httpClientSelector = null)
    {
        this.credentialService = credentialService ?? throw new ArgumentNullException(nameof(credentialService));
        this.dispatchGate = dispatchGate ?? throw new ArgumentNullException(nameof(dispatchGate));
        this.loggerFactory = loggerFactory ?? NullLoggerFactory.Instance;
        this.httpClientSelector = httpClientSelector;
    }

    public AIAgent CreateFrameworkAgent(
        ProviderProfile provider,
        string model,
        ChatClientAgentOptions options,
        bool frameworkManagedHistory,
        bool allowBackgroundResponses)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(options);

        ValidateProviderAvailability(provider, model);
        ValidateModel(provider, model);
        ValidateProviderKind(provider, model);
        ValidateProviderTransport(provider, model);
        ValidateRuntimeSettings(provider, model);

        return provider.Kind switch
        {
            ProviderKind.OpenAi => CreateOpenAiAgent(provider, model, options, frameworkManagedHistory, allowBackgroundResponses),
            ProviderKind.AzureOpenAi => CreateAzureOpenAiAgent(provider, model, options, frameworkManagedHistory, allowBackgroundResponses),
            ProviderKind.Ollama => CreateOllamaAgent(provider, model, options, allowBackgroundResponses),
            _ => throw new MafProviderConfigurationException(
                provider,
                model,
                MafProviderConfigurationFailureReason.UnsupportedProviderKind)
        };
    }

    private AIAgent CreateOpenAiAgent(
        ProviderProfile provider,
        string model,
        ChatClientAgentOptions options,
        bool frameworkManagedHistory,
        bool allowBackgroundResponses)
    {
        var credential = credentialService.Resolve(provider);
        if (!credential.IsResolved)
        {
            throw new MafProviderConfigurationException(
                provider,
                model,
                MafProviderConfigurationFailureReason.MissingCredential);
        }

        var clientOptions = CreateOpenAiClientOptions(provider, model);
        var client = new OpenAIClient(
            credential: new System.ClientModel.ApiKeyCredential(credential.ApiKey),
            options: clientOptions);

        return provider.Transport switch
        {
            ProviderTransportKind.ChatCompletions => client
                .GetChatClient(model)
                .AsAIAgent(
                    options: options,
                    clientFactory: chatClient => AddRuntimePolicies(chatClient, provider, model, allowBackgroundResponses)),
            ProviderTransportKind.Responses when frameworkManagedHistory => AddRuntimePolicies(
                    client
                        .GetResponsesClient()
                        .AsIChatClientWithStoredOutputDisabled(
                            model: model,
                            includeReasoningEncryptedContent: ShouldIncludeReasoningEncryptedContentForStoredOutputDisabledResponses(provider, frameworkManagedHistory)),
                    provider,
                    model,
                    allowBackgroundResponses)
                .AsAIAgent(options: options),
            ProviderTransportKind.Responses => client
                .GetResponsesClient()
                .AsAIAgent(
                    options: options,
                    model: model,
                    clientFactory: chatClient => AddRuntimePolicies(chatClient, provider, model, allowBackgroundResponses)),
            _ => throw new MafProviderConfigurationException(
                provider,
                model,
                MafProviderConfigurationFailureReason.UnsupportedTransport)
        };
    }

    private AIAgent CreateAzureOpenAiAgent(
        ProviderProfile provider,
        string model,
        ChatClientAgentOptions options,
        bool frameworkManagedHistory,
        bool allowBackgroundResponses)
    {
        var credential = credentialService.Resolve(provider);
        if (!credential.IsResolved)
        {
            throw new MafProviderConfigurationException(
                provider,
                model,
                MafProviderConfigurationFailureReason.MissingCredential);
        }

        var client = new OpenAIClient(
            credential: new System.ClientModel.ApiKeyCredential(credential.ApiKey),
            options: new OpenAIClientOptions
            {
                Endpoint = ResolveAzureOpenAiEndpoint(provider, model),
                NetworkTimeout = MafProviderRuntimeSettings.ResolveNetworkTimeout(provider)
            });

        return provider.Transport switch
        {
            ProviderTransportKind.ChatCompletions => client
                .GetChatClient(model)
                .AsAIAgent(
                    options: options,
                    clientFactory: chatClient => AddRuntimePolicies(chatClient, provider, model, allowBackgroundResponses)),
            ProviderTransportKind.Responses when frameworkManagedHistory => AddRuntimePolicies(
                    client
                        .GetResponsesClient()
                        .AsIChatClientWithStoredOutputDisabled(
                            model: model,
                            includeReasoningEncryptedContent: ShouldIncludeReasoningEncryptedContentForStoredOutputDisabledResponses(provider, frameworkManagedHistory)),
                    provider,
                    model,
                    allowBackgroundResponses)
                .AsAIAgent(options: options),
            ProviderTransportKind.Responses => client
                .GetResponsesClient()
                .AsAIAgent(
                    options: options,
                    model: model,
                    clientFactory: chatClient => AddRuntimePolicies(chatClient, provider, model, allowBackgroundResponses)),
            _ => throw new MafProviderConfigurationException(
                provider,
                model,
                MafProviderConfigurationFailureReason.UnsupportedTransport)
        };
    }

    private AIAgent CreateOllamaAgent(
        ProviderProfile provider,
        string model,
        ChatClientAgentOptions options,
        bool allowBackgroundResponses)
    {
        var resilienceLogger = loggerFactory.CreateLogger<OllamaChatResponseResilienceHandler>();
        var httpClient = new HttpClient(
            new OllamaToolResultProtocolHandler(
                new OllamaChatResponseResilienceHandler(
                    new HttpClientHandler(),
                    provider,
                    model,
                    resilienceLogger)))
        {
            BaseAddress = ResolveProviderEndpoint(provider, model),
            Timeout = MafProviderRuntimeSettings.ResolveNetworkTimeout(provider)
        };
        IChatClient chatClient = AddProviderTransportBoundary(
            new OllamaApiClient(httpClient, model, jsonSerializerContext: null),
            provider,
            model);
        chatClient = new DefaultOllamaOptionsChatClient(
            chatClient,
            options.ChatOptions?.MaxOutputTokens ??
            AgentProviderModelParameterPolicy.ResolveOllamaMaxOutputTokensOrDefault(
                provider.ConfigurationJson,
                string.Empty),
            ResolveDefaultOllamaThinkingValue(provider, model, options.ChatOptions));
        return AddRuntimePolicies(chatClient, provider, model, allowBackgroundResponses)
            .AsAIAgent(options: options);
    }

    private static object? ResolveDefaultOllamaThinkingValue(
        ProviderProfile provider,
        string model,
        ChatOptions? chatOptions)
    {
        if (chatOptions?.AdditionalProperties?.TryGetValue(
                OllamaOption.Think.Name,
                out var effectiveThinkingValue) == true)
        {
            return effectiveThinkingValue;
        }

        var providerThinkingEffort = AgentThinkingEffortPolicy.ResolveEffectiveEffort(
            provider,
            model,
            string.Empty);
        return providerThinkingEffort is null
            ? null
            : OllamaThinkingEffortAdapter.ToNativeValue(
                AgentThinkingEffortPolicy.ResolveCapability(provider, model),
                providerThinkingEffort.Value);
    }

    private IChatClient AddRuntimePolicies(
        IChatClient chatClient,
        ProviderProfile provider,
        string model,
        bool allowBackgroundResponses)
    {
        chatClient = AddProviderTransportBoundary(chatClient, provider, model);
        chatClient = AddOpenAiChatCompletionsCompatibility(
            chatClient,
            provider,
            model);
        if (chatClient.GetService<EmptyCompletionRetryChatClient>() is not null)
        {
            throw new InvalidOperationException(
                $"Provider '{provider.Name}' model '{model}' already contains empty-completion recovery.");
        }

        var logger = loggerFactory.CreateLogger<EmptyCompletionRetryChatClient>();
        return new EmptyCompletionRetryChatClient(
            chatClient,
            provider,
            model,
            allowBackgroundResponses,
            logger);
    }

    private IChatClient AddProviderTransportBoundary(
        IChatClient chatClient,
        ProviderProfile provider,
        string model)
    {
        if (chatClient.GetService<MafProviderTransportBoundaryChatClient>() is not null)
        {
            return chatClient;
        }

        return new MafProviderTransportBoundaryChatClient(
            chatClient,
            provider,
            model,
            dispatchGate,
            logger: loggerFactory.CreateLogger<MafProviderTransportBoundaryChatClient>());
    }

    private IChatClient AddOpenAiChatCompletionsCompatibility(
        IChatClient chatClient,
        ProviderProfile provider,
        string model)
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

        var logger = loggerFactory.CreateLogger<OpenAiChatCompletionsCompatibilityChatClient>();
        return new OpenAiChatCompletionsCompatibilityChatClient(
            chatClient,
            provider,
            model,
            logger);
    }

    private OpenAIClientOptions CreateOpenAiClientOptions(
        ProviderProfile provider,
        string model)
    {
        ArgumentNullException.ThrowIfNull(provider);

        var options = new OpenAIClientOptions
        {
            NetworkTimeout = MafProviderRuntimeSettings.ResolveNetworkTimeout(provider)
        };
        if (!MafProviderRuntimeSettings.ShouldUseDefaultOpenAiEndpoint(provider.BaseUrl))
        {
            options.Endpoint = ResolveProviderEndpoint(provider, model);
        }

        if (httpClientSelector?.TryGetClient(provider, out var httpClient) == true)
        {
            options.Transport = new HttpClientPipelineTransport(httpClient);
        }

        return options;
    }

    private static Uri ResolveAzureOpenAiEndpoint(
        ProviderProfile provider,
        string model)
    {
        try
        {
            _ = ResolveProviderEndpoint(provider, model);
            return AzureOpenAiEndpoint.Parse(provider).V1Endpoint;
        }
        catch (MafProviderConfigurationException)
        {
            throw;
        }
        catch (InvalidOperationException exception)
        {
            throw new MafProviderConfigurationException(
                provider,
                model,
                MafProviderConfigurationFailureReason.InvalidEndpoint,
                exception);
        }
    }

    private static Uri ResolveProviderEndpoint(
        ProviderProfile provider,
        string model)
    {
        var baseUrl = provider.BaseUrl?.Trim();
        if (string.IsNullOrWhiteSpace(baseUrl) ||
            !Uri.TryCreate(baseUrl, UriKind.Absolute, out var endpoint) ||
            endpoint.Scheme is not ("http" or "https") ||
            !string.IsNullOrEmpty(endpoint.UserInfo) ||
            !string.IsNullOrEmpty(endpoint.Query) ||
            !string.IsNullOrEmpty(endpoint.Fragment))
        {
            throw new MafProviderConfigurationException(
                provider,
                model,
                MafProviderConfigurationFailureReason.InvalidEndpoint);
        }

        return endpoint;
    }

    private static void ValidateRuntimeSettings(
        ProviderProfile provider,
        string model)
    {
        try
        {
            _ = MafProviderRuntimeSettings.ResolveNetworkTimeout(provider);
        }
        catch (ProviderProfileValidationException exception)
        {
            throw new MafProviderConfigurationException(
                provider,
                model,
                MafProviderConfigurationFailureReason.InvalidRuntimeSettings,
                exception);
        }
    }

    private static void ValidateModel(
        ProviderProfile provider,
        string model)
    {
        if (!string.IsNullOrWhiteSpace(model))
        {
            ProviderModelSelectionPolicy.EnsureAllowed(provider, model);
            return;
        }

        throw new MafProviderConfigurationException(
            provider,
            string.Empty,
            MafProviderConfigurationFailureReason.InvalidModel);
    }

    private static void ValidateProviderAvailability(
        ProviderProfile provider,
        string model)
    {
        if (provider.IsEnabled)
        {
            return;
        }

        throw new MafProviderConfigurationException(
            provider,
            model,
            MafProviderConfigurationFailureReason.ProviderUnavailable);
    }

    private static void ValidateProviderKind(
        ProviderProfile provider,
        string model)
    {
        if (provider.Kind is ProviderKind.OpenAi or ProviderKind.AzureOpenAi or ProviderKind.Ollama)
        {
            return;
        }

        throw new MafProviderConfigurationException(
            provider,
            model,
            MafProviderConfigurationFailureReason.UnsupportedProviderKind);
    }

    private static void ValidateProviderTransport(
        ProviderProfile provider,
        string model)
    {
        var supported = provider.Kind switch
        {
            ProviderKind.OpenAi or ProviderKind.AzureOpenAi =>
                provider.Transport is ProviderTransportKind.ChatCompletions or ProviderTransportKind.Responses,
            ProviderKind.Ollama => provider.Transport == ProviderTransportKind.ChatCompletions,
            _ => false
        };
        if (supported)
        {
            return;
        }

        throw new MafProviderConfigurationException(
            provider,
            model,
            MafProviderConfigurationFailureReason.UnsupportedTransport);
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
    object? defaultThinkingValue) : DelegatingChatClient(innerClient)
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

        if (defaultThinkingValue is not null &&
            !normalizedOptions.AdditionalProperties.ContainsKey(OllamaOption.Think.Name))
        {
            normalizedOptions.AddOllamaOption(OllamaOption.Think, defaultThinkingValue);
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
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new ProviderProfileValidationException(
                    "Provider runtime configuration must be a JSON object.");
            }

            if (!document.RootElement.TryGetProperty("timeoutSeconds", out var timeoutElement))
            {
                return ModelNetworkTimeout;
            }

            if (timeoutElement.ValueKind != JsonValueKind.Number ||
                !timeoutElement.TryGetInt32(out var timeoutSeconds) ||
                timeoutSeconds is < 5 or > 3600)
            {
                throw new ProviderProfileValidationException(
                    "Provider timeoutSeconds must be an integer between 5 and 3600.");
            }

            return TimeSpan.FromSeconds(timeoutSeconds);
        }
        catch (JsonException exception)
        {
            throw new ProviderProfileValidationException(
                "Provider runtime configuration must contain valid JSON.",
                exception);
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
