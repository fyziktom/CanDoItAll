using System.Net.Http.Json;
using System.Text.Json;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Providers;

public sealed class OllamaProviderDriver(HttpClient httpClient) :
    IProviderHealthDriver,
    IProviderModelCatalogDriver,
    IProviderChatCompletionDriver,
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
            return string.IsNullOrWhiteSpace(result.ResponseText)
                ? new ProviderHealthResult(false, "Ollama health check returned an empty chat response.", suggestedModels)
                : new ProviderHealthResult(true, $"Ollama returned /api/tags and completed a chat probe with model '{model}'.", suggestedModels);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return new ProviderHealthResult(false, $"Ollama health check failed: {exception.Message}", provider.SuggestedModels);
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
        return document.RootElement.TryGetProperty("models", out var models) && models.ValueKind == JsonValueKind.Array
            ? models.EnumerateArray()
                .Select(item => ProviderDriverJson.ReadString(item, "name"))
                .Where(model => !string.IsNullOrWhiteSpace(model))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(model => new ProviderModelDescriptor(
                    model,
                    model,
                    request.Capability,
                    ProviderDispatchLimits.Unbatched(TimeSpan.FromMinutes(5))))
                .ToList()
            : [];
    }

    public async Task<ProviderChatCompletionResult> CompleteChatAsync(
        ProviderChatCompletionRequest request,
        CancellationToken cancellationToken = default)
    {
        var payload = new Dictionary<string, object?>
        {
            ["model"] = request.Model,
            ["messages"] = ProviderDriverProtocol.BuildOllamaChatMessages(request),
            ["stream"] = false
        };
        var options = ResolveChatOptions(request.Provider, request.ModelParameterConfigurationJson);
        if (options.Count > 0)
        {
            payload["options"] = options;
        }

        var think = AgentProviderModelParameterPolicy.ResolveOllamaThinkOrDefault(
            request.Provider.ConfigurationJson,
            request.ModelParameterConfigurationJson);
        payload["think"] = think;

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
        if (!string.IsNullOrWhiteSpace(content))
        {
            return content;
        }

        return ProviderDriverJson.ReadString(message, "thinking");
    }

    private static Dictionary<string, object> ResolveChatOptions(
        ProviderProfile provider,
        string requestModelParameterConfigurationJson)
    {
        var options = new Dictionary<string, object>(StringComparer.Ordinal);
        var numPredict = AgentProviderModelParameterPolicy.ResolveOllamaMaxOutputTokensOrDefault(
            provider.ConfigurationJson,
            requestModelParameterConfigurationJson);
        options[NumPredictSnakePropertyName] = numPredict;

        return options;
    }
}
