using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Providers;

public sealed class OllamaProviderDriver(HttpClient httpClient) :
    IProviderHealthDriver,
    IProviderModelCatalogDriver,
    IProviderChatCompletionDriver,
    IProviderStreamingChatCompletionDriver,
    IProviderModelMaintenanceDriver
{
    private const string NumPredictSnakePropertyName = "num_predict";

    private static readonly IReadOnlySet<AgentProviderCapabilityKind> SupportedCapabilities = new HashSet<AgentProviderCapabilityKind>
    {
        AgentProviderCapabilityKind.Health,
        AgentProviderCapabilityKind.ModelCatalog,
        AgentProviderCapabilityKind.ChatCompletion,
        AgentProviderCapabilityKind.ModelMaintenance
    };

    public ProviderKind ProviderKind => ProviderKind.Ollama;

    public IReadOnlySet<AgentProviderCapabilityKind> Capabilities => SupportedCapabilities;

    public ProviderDispatchLimits GetDispatchLimits(ProviderDispatchQuery query)
    {
        return ProviderDispatchLimits.Unbatched(TimeSpan.FromMinutes(5));
    }

    public async Task<ProviderHealthResult> TestHealthAsync(
        ProviderProfile provider,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var models = await ListModelsAsync(
                new ProviderModelCatalogRequest(provider, AgentProviderCapabilityKind.ChatCompletion),
                cancellationToken).ConfigureAwait(false);
            var suggestedModels = models.Select(model => model.Model).ToList();
            var model = ResolveHealthModel(provider, suggestedModels, "qwen3.5:9b");
            var result = await CompleteChatAsync(
                new ProviderChatCompletionRequest(
                    provider,
                    model,
                    "Reply with a short confirmation.",
                    [],
                    "Reply with the single word OK."),
                cancellationToken).ConfigureAwait(false);
            return (string.IsNullOrWhiteSpace(result.ResponseText)
                ? new ProviderHealthResult(false, "Ollama health check returned an empty chat response.", suggestedModels)
                : new ProviderHealthResult(true, $"Ollama returned /api/tags and completed a chat probe with model '{model}'.", suggestedModels)) with
            {
                ModelThinkingEffortCapabilities = models
                    .Where(item => item.ThinkingEffortCapability is not null)
                    .Select(item => item.ThinkingEffortCapability!)
                    .ToList()
            };
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return new ProviderHealthResult(false, $"Ollama health check failed: {exception.Message}", provider.SuggestedModels)
            {
                ModelThinkingEffortCapabilities = provider.ModelThinkingEffortCapabilities
            };
        }
    }

    public async Task<IReadOnlyList<ProviderModelDescriptor>> ListModelsAsync(
        ProviderModelCatalogRequest request,
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync(BuildEndpoint(request.Provider, "api/tags"), cancellationToken).ConfigureAwait(false);
        await ProviderDriverProtocol.EnsureSuccessAsync(response, "Ollama model catalog", cancellationToken).ConfigureAwait(false);
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (!document.RootElement.TryGetProperty("models", out var models))
        {
            return [];
        }

        if (models.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("Ollama model catalog property 'models' must be an array.");
        }

        var descriptors = new List<ProviderModelDescriptor>();
        var seenModels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in models.EnumerateArray())
        {
            var model = ProviderDriverJson.ReadString(item, "name").Trim();
            if (string.IsNullOrWhiteSpace(model) || !seenModels.Add(model))
            {
                continue;
            }

            descriptors.Add(await CreateModelDescriptorAsync(
                request.Provider,
                item,
                model,
                request.Capability,
                cancellationToken).ConfigureAwait(false));
        }

        return descriptors;
    }

    public async Task<ProviderChatCompletionResult> CompleteChatAsync(
        ProviderChatCompletionRequest request,
        CancellationToken cancellationToken = default)
    {
        var payload = BuildChatPayload(request, stream: false);

        using var response = await httpClient.PostAsJsonAsync(
            BuildEndpoint(request.Provider, "api/chat"),
            payload,
            ProviderDriverJson.Options,
            cancellationToken).ConfigureAwait(false);
        await ProviderDriverProtocol.EnsureSuccessAsync(response, "Ollama chat completion", cancellationToken).ConfigureAwait(false);
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        var message = document.RootElement.TryGetProperty("message", out var messageElement)
            ? messageElement
            : default;
        return new ProviderChatCompletionResult(
            request.Model,
            ReadMessageText(message),
            ProviderDriverJson.ReadInt(document.RootElement, "prompt_eval_count"),
            ProviderDriverJson.ReadInt(document.RootElement, "eval_count"));
    }

    public ProviderChatStreamingMode ResolveStreamingMode(ProviderChatCompletionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ProviderChatStreamingMode.Incremental;
    }

    public async IAsyncEnumerable<ProviderChatStreamingUpdate> StreamChatAsync(
        ProviderChatCompletionRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var payload = BuildChatPayload(request, stream: true);
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, BuildEndpoint(request.Provider, "api/chat"))
        {
            Content = JsonContent.Create(payload, options: ProviderDriverJson.Options)
        };
        using var response = await httpClient.SendAsync(
            httpRequest,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken).ConfigureAwait(false);
        await ProviderDriverProtocol.EnsureSuccessAsync(
            response,
            "Ollama chat completion",
            cancellationToken).ConfigureAwait(false);
        await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await foreach (var update in ProviderStreamingProtocol.ReadOllamaAsync(
            responseStream,
            request.Model,
            cancellationToken).ConfigureAwait(false))
        {
            yield return update;
        }
    }

    public async Task<ProviderModelMaintenanceResult> CreateOrUpdateModelAsync(
        ProviderModelMaintenanceRequest request,
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsJsonAsync(
            BuildEndpoint(request.Provider, "api/create"),
            new
            {
                model = request.Model,
                from = request.BaseModel,
                system = request.SystemPrompt,
                parameters = new
                {
                    num_ctx = request.ContextLength
                },
                stream = false
            },
            ProviderDriverJson.Options,
            cancellationToken).ConfigureAwait(false);
        await ProviderDriverProtocol.EnsureSuccessAsync(response, "Ollama model maintenance", cancellationToken).ConfigureAwait(false);
        var status = string.Empty;
        try
        {
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
            status = ProviderDriverJson.ReadString(document.RootElement, "status");
        }
        catch (JsonException)
        {
        }

        return new ProviderModelMaintenanceResult(
            request.Model,
            request.BaseModel,
            request.SystemPrompt,
            request.ContextLength,
            OllamaModelfileBuilder.Build(request.BaseModel, request.SystemPrompt, request.ContextLength),
            status);
    }

    private static string BuildEndpoint(ProviderProfile provider, string relativePath)
    {
        return $"{provider.BaseUrl.Trim().TrimEnd('/')}/{relativePath.TrimStart('/')}";
    }

    private static Dictionary<string, object?> BuildChatPayload(
        ProviderChatCompletionRequest request,
        bool stream)
    {
        var payload = new Dictionary<string, object?>
        {
            ["model"] = request.Model,
            ["messages"] = ProviderDriverProtocol.BuildOllamaChatMessages(request),
            ["stream"] = stream
        };
        var options = ResolveChatOptions(request.Provider, request.ModelParameterConfigurationJson, request.Temperature);
        if (options.Count > 0)
        {
            payload["options"] = options;
        }

        if (request.ResponseFormat is { RequireJson: true })
        {
            payload["format"] = "json";
        }

        var thinkingEffort = AgentThinkingEffortPolicy.ResolveEffectiveEffort(
            request.Provider,
            request.Model,
            request.ModelParameterConfigurationJson);
        if (thinkingEffort is null)
        {
            return payload;
        }

        var thinkingCapability = AgentThinkingEffortPolicy.ResolveCapability(
            request.Provider,
            request.Model);
        payload["think"] = OllamaThinkingEffortAdapter.ToNativeValue(
            thinkingCapability,
            thinkingEffort.Value);
        return payload;
    }

    private async Task<ProviderModelDescriptor> CreateModelDescriptorAsync(
        ProviderProfile provider,
        JsonElement item,
        string model,
        AgentProviderCapabilityKind capability,
        CancellationToken cancellationToken)
    {
        var modelFamily = item.TryGetProperty("details", out var details) && details.ValueKind == JsonValueKind.Object
            ? ProviderDriverJson.ReadString(details, "family")
            : string.Empty;
        var thinkingStatus = ResolveThinkingStatus(
            item,
            "model catalog",
            AgentThinkingEffortSupportStatus.Unknown);
        if (thinkingStatus == AgentThinkingEffortSupportStatus.Unknown)
        {
            (modelFamily, thinkingStatus) = await ReadModelThinkingDetailsAsync(
                provider,
                model,
                modelFamily,
                cancellationToken).ConfigureAwait(false);
        }

        return new ProviderModelDescriptor(
            model,
            model,
            capability,
            ProviderDispatchLimits.Unbatched(TimeSpan.FromMinutes(5)))
        {
            ThinkingEffortCapability = AgentThinkingEffortPolicy.CreateDiscoveredCapability(
                model,
                modelFamily,
                thinkingStatus)
        };
    }

    private async Task<(string ModelFamily, AgentThinkingEffortSupportStatus ThinkingStatus)>
        ReadModelThinkingDetailsAsync(
        ProviderProfile provider,
        string model,
        string fallbackModelFamily,
        CancellationToken cancellationToken)
    {
        using var response = await httpClient.PostAsJsonAsync(
            BuildEndpoint(provider, "api/show"),
            new
            {
                model,
                verbose = false
            },
            ProviderDriverJson.Options,
            cancellationToken).ConfigureAwait(false);
        await ProviderDriverProtocol.EnsureSuccessAsync(
            response,
            $"Ollama model details for '{model}'",
            cancellationToken).ConfigureAwait(false);
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        var root = document.RootElement;
        var reportedModelFamily = root.TryGetProperty("details", out var details) && details.ValueKind == JsonValueKind.Object
            ? ProviderDriverJson.ReadString(details, "family")
            : string.Empty;
        var modelFamily = string.IsNullOrWhiteSpace(reportedModelFamily)
            ? fallbackModelFamily
            : reportedModelFamily.Trim();
        var thinkingStatus = ResolveThinkingStatus(
            root,
            $"model details for '{model}'",
            AgentThinkingEffortSupportStatus.Unsupported);
        return (modelFamily, thinkingStatus);
    }

    private static AgentThinkingEffortSupportStatus ResolveThinkingStatus(
        JsonElement source,
        string sourceDescription,
        AgentThinkingEffortSupportStatus noThinkingStatus)
    {
        if (!source.TryGetProperty("capabilities", out var capabilities))
        {
            return AgentThinkingEffortSupportStatus.Unknown;
        }

        if (capabilities.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException(
                $"Ollama {sourceDescription} property 'capabilities' must be an array.");
        }

        foreach (var capability in capabilities.EnumerateArray())
        {
            if (capability.ValueKind != JsonValueKind.String)
            {
                throw new InvalidOperationException(
                    $"Ollama {sourceDescription} property 'capabilities' must contain only strings.");
            }

            if (string.Equals(capability.GetString(), "thinking", StringComparison.OrdinalIgnoreCase))
            {
                return AgentThinkingEffortSupportStatus.Supported;
            }
        }

        return noThinkingStatus;
    }

    private static string ResolveHealthModel(
        ProviderProfile provider,
        IReadOnlyList<string> suggestedModels,
        string fallbackModel)
    {
        if (!string.IsNullOrWhiteSpace(provider.DefaultModel))
        {
            return provider.DefaultModel.Trim();
        }

        return suggestedModels.FirstOrDefault(model => !string.IsNullOrWhiteSpace(model))?.Trim()
            ?? fallbackModel;
    }

    private static string ReadMessageText(JsonElement message)
    {
        if (message.ValueKind != JsonValueKind.Object)
        {
            return string.Empty;
        }

        var content = ProviderDriverJson.ReadString(message, "content");
        return content;
    }

    private static Dictionary<string, object> ResolveChatOptions(
        ProviderProfile provider,
        string requestModelParameterConfigurationJson,
        double? temperature)
    {
        var options = new Dictionary<string, object>(StringComparer.Ordinal);
        var numPredict = AgentProviderModelParameterPolicy.ResolveOllamaMaxOutputTokensOrDefault(
            provider.ConfigurationJson,
            requestModelParameterConfigurationJson);
        options[NumPredictSnakePropertyName] = numPredict;
        if (temperature is { } value)
        {
            options["temperature"] = value;
        }

        return options;
    }
}
