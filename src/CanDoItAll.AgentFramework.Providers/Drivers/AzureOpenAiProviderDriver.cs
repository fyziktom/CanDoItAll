using System.Net.Http.Json;
using System.Text.Json;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Providers;

public sealed class AzureOpenAiProviderDriver(HttpClient httpClient, IProviderDriverCredentialResolver credentialResolver) :
    IProviderHealthDriver,
    IProviderModelCatalogDriver,
    IProviderChatCompletionDriver
{
    private const string DefaultApiVersion = "2024-10-21";

    private static readonly IReadOnlySet<AgentProviderCapabilityKind> SupportedCapabilities = new HashSet<AgentProviderCapabilityKind>
    {
        AgentProviderCapabilityKind.Health,
        AgentProviderCapabilityKind.ModelCatalog,
        AgentProviderCapabilityKind.ChatCompletion
    };

    public ProviderKind ProviderKind => ProviderKind.AzureOpenAi;

    public IReadOnlySet<AgentProviderCapabilityKind> Capabilities => SupportedCapabilities;

    public ProviderDispatchLimits GetDispatchLimits(ProviderDispatchQuery query)
    {
        return ProviderDispatchLimits.Unbatched(TimeSpan.FromMinutes(2));
    }

    public async Task<ProviderHealthResult> TestHealthAsync(
        ProviderProfile provider,
        CancellationToken cancellationToken = default)
    {
        var credential = ResolveCredential(provider);
        var suggestedModels = string.IsNullOrWhiteSpace(provider.DefaultModel)
            ? provider.SuggestedModels
            : [provider.DefaultModel];
        if (!credential.IsResolved)
        {
            return new ProviderHealthResult(false, credential.FailureMessage, suggestedModels);
        }

        try
        {
            var model = string.IsNullOrWhiteSpace(provider.DefaultModel)
                ? provider.SuggestedModels.FirstOrDefault(model => !string.IsNullOrWhiteSpace(model))?.Trim() ?? string.Empty
                : provider.DefaultModel.Trim();
            if (string.IsNullOrWhiteSpace(model))
            {
                return new ProviderHealthResult(false, $"Azure OpenAI provider '{provider.Name}' does not define a deployment model.", suggestedModels);
            }

            var result = await CompleteChatAsync(
                new ProviderChatCompletionRequest(
                    provider,
                    model,
                    "Reply with a short confirmation.",
                    [],
                    "Reply with the single word OK."),
                cancellationToken).ConfigureAwait(false);
            return string.IsNullOrWhiteSpace(result.ResponseText)
                ? new ProviderHealthResult(false, "Azure OpenAI health check returned an empty chat response.", suggestedModels)
                : new ProviderHealthResult(true, $"Azure OpenAI completed a chat probe with deployment '{model}'.", suggestedModels);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return new ProviderHealthResult(false, $"Azure OpenAI health check failed: {exception.Message}", suggestedModels);
        }
    }

    public Task<IReadOnlyList<ProviderModelDescriptor>> ListModelsAsync(
        ProviderModelCatalogRequest request,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<ProviderModelDescriptor> models = string.IsNullOrWhiteSpace(request.Provider.DefaultModel)
            ? []
            :
            [
                new ProviderModelDescriptor(
                    request.Provider.DefaultModel,
                    request.Provider.DefaultModel,
                    request.Capability,
                    ProviderDispatchLimits.Unbatched(TimeSpan.FromMinutes(2)))
            ];
        return Task.FromResult(models);
    }

    public async Task<ProviderChatCompletionResult> CompleteChatAsync(
        ProviderChatCompletionRequest request,
        CancellationToken cancellationToken = default)
    {
        var credential = ResolveCredential(request.Provider);
        if (!credential.IsResolved)
        {
            throw new InvalidOperationException(credential.FailureMessage);
        }

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, BuildChatEndpoint(request.Provider, request.Model));
        httpRequest.Headers.Add("api-key", credential.ApiKey);
        httpRequest.Content = JsonContent.Create(
            new
            {
                messages = ProviderDriverProtocol.BuildChatMessages(request),
                stream = false
            },
            options: ProviderDriverJson.Options);
        using var response = await httpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
        await ProviderDriverProtocol.EnsureSuccessAsync(response, "Azure OpenAI chat completion", cancellationToken).ConfigureAwait(false);

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        var choice = document.RootElement.GetProperty("choices")[0].GetProperty("message");
        var usage = document.RootElement.TryGetProperty("usage", out var usageElement)
            ? usageElement
            : default;
        return new ProviderChatCompletionResult(
            request.Model,
            ProviderDriverJson.ReadString(choice, "content"),
            usage.ValueKind == JsonValueKind.Object ? ProviderDriverJson.ReadInt(usage, "prompt_tokens") : 0,
            usage.ValueKind == JsonValueKind.Object ? ProviderDriverJson.ReadInt(usage, "completion_tokens") : 0);
    }

    private ProviderDriverCredential ResolveCredential(ProviderProfile provider)
    {
        return credentialResolver.Resolve(provider);
    }

    private static string BuildChatEndpoint(ProviderProfile provider, string model)
    {
        var baseUrl = provider.BaseUrl.Trim().TrimEnd('/');
        var apiVersion = ReadApiVersion(provider.ConfigurationJson);
        return $"{baseUrl}/openai/deployments/{Uri.EscapeDataString(model)}/chat/completions?api-version={Uri.EscapeDataString(apiVersion)}";
    }

    private static string ReadApiVersion(string? configurationJson)
    {
        if (string.IsNullOrWhiteSpace(configurationJson))
        {
            return DefaultApiVersion;
        }

        try
        {
            using var document = JsonDocument.Parse(configurationJson);
            return document.RootElement.TryGetProperty("apiVersion", out var apiVersion) &&
                   apiVersion.ValueKind == JsonValueKind.String &&
                   !string.IsNullOrWhiteSpace(apiVersion.GetString())
                ? apiVersion.GetString()!.Trim()
                : DefaultApiVersion;
        }
        catch (JsonException)
        {
            return DefaultApiVersion;
        }
    }
}
