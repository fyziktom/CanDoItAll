using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Globalization;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Security;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.AgentFramework.ProviderManagement;

public sealed record ProviderModelPricingDiscoveryResult(
    IReadOnlyList<ProviderDiscoveredModelPrice> Models,
    string Message);

public static class ProviderConnectorFieldKeys
{
    public const string BaseUrl = "baseUrl";
    public const string DefaultModel = "defaultModel";
    public const string TimeoutSeconds = "timeoutSeconds";
    public const string ComfyUiWorkflowTemplateJson = "workflowTemplateJson";
    public const string ComfyUiWorkflowTemplatePath = "workflowTemplatePath";
    public const string ComfyUiPositivePromptNodeId = "positivePromptNodeId";
    public const string ComfyUiNegativePromptNodeId = "negativePromptNodeId";
    public const string ComfyUiOutputNodeId = "outputNodeId";
    public const string ComfyUiPollIntervalMilliseconds = "pollIntervalMilliseconds";
}

// Temporary compatibility implementation; BR04 removes direct provider inference adapters.
internal interface IProviderAdapter : IConnectorPlugin
{
    ProviderKind? LegacyProviderKind { get; }

    Task<ProviderHealthCheckResult> CheckHealthAsync(ProviderProfile profile, string? secretValue, CancellationToken cancellationToken = default);

    Task<Result<ProviderPromptExecutionResponse>> SendAsync(
        ProviderProfile profile,
        ProviderPromptExecutionRequest request,
        string? secretValue,
        CancellationToken cancellationToken = default);
}

internal interface IProviderModelPricingSource
{
    Task<Result<ProviderModelPricingDiscoveryResult>> DiscoverModelPricingAsync(
        ProviderProfile profile,
        string? secretValue,
        CancellationToken cancellationToken = default);
}

internal sealed class ProviderRegistry(IEnumerable<IProviderAdapter> adapters) :
    IConnectorManifestSource,
    IProviderManifestCatalog
{
    private readonly IReadOnlyDictionary<string, IProviderAdapter> adaptersByKey =
        adapters.ToDictionary(adapter => adapter.Manifest.PluginKey, StringComparer.OrdinalIgnoreCase);

    private readonly IReadOnlyDictionary<ProviderKind, IProviderAdapter> adaptersByLegacyKind = adapters
        .Where(adapter => adapter.LegacyProviderKind.HasValue)
        .GroupBy(adapter => adapter.LegacyProviderKind!.Value)
        .ToDictionary(group => group.Key, group => group.Last());

    public IProviderAdapter? Resolve(ProviderProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        if (!string.IsNullOrWhiteSpace(profile.ConnectorPluginKey))
        {
            return Resolve(profile.ConnectorPluginKey);
        }

        return profile.ProviderKind.HasValue
            ? adaptersByLegacyKind.GetValueOrDefault(profile.ProviderKind.Value)
            : null;
    }

    public IProviderAdapter? Resolve(string? connectorPluginKey, ProviderKind? legacyProviderKind = null)
    {
        if (!string.IsNullOrWhiteSpace(connectorPluginKey) &&
            adaptersByKey.TryGetValue(connectorPluginKey.Trim(), out var pluginByKey))
        {
            return pluginByKey;
        }

        return string.IsNullOrWhiteSpace(connectorPluginKey) && legacyProviderKind.HasValue
            ? adaptersByLegacyKind.GetValueOrDefault(legacyProviderKind.Value)
            : null;
    }

    public bool TryResolve(string? connectorPluginKey, out IProviderAdapter adapter)
        => TryResolve(connectorPluginKey, legacyProviderKind: null, out adapter);

    public bool TryResolve(string? connectorPluginKey, ProviderKind? legacyProviderKind, out IProviderAdapter adapter)
    {
        adapter = null!;

        var resolved = Resolve(connectorPluginKey, legacyProviderKind);
        if (resolved is not null)
        {
            adapter = resolved;
            return true;
        }

        return false;
    }

    public string? ResolveLegacyPluginKey(ProviderKind providerKind)
    {
        return adaptersByLegacyKind.TryGetValue(providerKind, out var adapter)
            ? adapter.Manifest.PluginKey
            : null;
    }

    public IReadOnlyCollection<ProviderKind> RegisteredLegacyKinds => adaptersByLegacyKind.Keys.ToArray();

    public IReadOnlyList<ConnectorPluginManifest> ListManifests()
    {
        return adaptersByKey.Values
            .Select(adapter => adapter.Manifest)
            .OrderBy(manifest => manifest.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public ConnectorPluginManifest? ResolveManifest(
        string? connectorPluginKey,
        ProviderKind? legacyProviderKind = null)
        => Resolve(connectorPluginKey, legacyProviderKind)?.Manifest;
}

/* codex-capsule
kind: adapter
name: OpenAiProviderAdapter
summary: Performs basic health and prompt send calls against the OpenAI HTTP API through the neutral provider contract.
owns: openai-health, openai-send
deps: IHttpClientFactory
risks: api-shape-drift, wrong-base-url
tests: unit:ProviderAdapterTests
inputs: ProviderProfile, ProviderPromptExecutionRequest
outputs: ProviderPromptExecutionResponse
*/
internal sealed class OpenAiProviderAdapter(IHttpClientFactory httpClientFactory) : IProviderAdapter, IProviderModelPricingSource
{
    public const string PluginKey = ProviderConnectorKeys.OpenAi;
    public const string DefaultModel = ProviderConnectorDefaults.OpenAiModel;
    private static readonly string[] ModelIdPropertyNames = ["id", "model"];
    private static readonly string[] InputPricePropertyNames = ["inputPerMillionTokensUsd", "input_per_million_tokens_usd"];
    private static readonly string[] CachedInputPricePropertyNames = ["cachedInputPerMillionTokensUsd", "cached_input_per_million_tokens_usd"];
    private static readonly string[] OutputPricePropertyNames = ["outputPerMillionTokensUsd", "output_per_million_tokens_usd"];

    private static readonly ConnectorPluginManifest PluginManifest = new(
        PluginKey,
        "OpenAI provider",
        "1.0.0",
        ConnectorManifestCapability.ProviderExecution | ConnectorManifestCapability.AgentExposure,
        new ConnectorConfigurationSchema(
            "1.0",
            [
                new ConnectorConfigFieldDescriptor(ProviderConnectorFieldKeys.BaseUrl, "Base URL", ConnectorConfigFieldType.Url, true, "OpenAI-compatible API root."),
                new ConnectorConfigFieldDescriptor(ProviderConnectorFieldKeys.DefaultModel, "Default model", ConnectorConfigFieldType.Text, true, "Model name used when the request does not override it."),
                new ConnectorConfigFieldDescriptor(ProviderConnectorFieldKeys.TimeoutSeconds, "Timeout", ConnectorConfigFieldType.Number, true, "HTTP timeout in seconds.")
            ]),
        [
            new ConnectorSecretRequirement("apiKey", "API key", true, "Bearer token for the provider API.")
        ],
        new ConnectorHealthCheckDescriptor("GET /models", "Verifies that the provider accepts the configured API key and responds to model discovery."),
        new ConnectorAgentExposure("workspace.prompt.send", true, true, "Allows agent-triggered prompt execution through the provider profile."),
        null);

    public ConnectorPluginManifest Manifest => PluginManifest;

    public ProviderKind? LegacyProviderKind => ProviderKind.OpenAi;

    public async Task<ProviderHealthCheckResult> CheckHealthAsync(ProviderProfile profile, string? secretValue, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(secretValue))
        {
            return new ProviderHealthCheckResult(false, "OpenAI profiles require an API key secret.");
        }

        using var client = CreateClient(profile, secretValue);
        using var response = await client.GetAsync(GetModelsUrl(profile.BaseUrl), cancellationToken);
        return new ProviderHealthCheckResult(response.IsSuccessStatusCode, response.IsSuccessStatusCode ? "Healthy" : $"HTTP {(int)response.StatusCode}");
    }

    public async Task<Result<ProviderPromptExecutionResponse>> SendAsync(
        ProviderProfile profile,
        ProviderPromptExecutionRequest request,
        string? secretValue,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(secretValue))
        {
            return Result<ProviderPromptExecutionResponse>.Failure(Error.Validation("OpenAI profiles require an API key secret."));
        }

        using var client = CreateClient(profile, secretValue);
        using var response = await client.PostAsJsonAsync(
            GetResponsesUrl(profile.BaseUrl),
            new
            {
                model = string.IsNullOrWhiteSpace(request.ModelOverride) ? profile.DefaultModel : request.ModelOverride,
                input = request.Prompt
            },
            cancellationToken);

        var payload = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return Result<ProviderPromptExecutionResponse>.Failure(Error.Failure($"OpenAI call failed with HTTP {(int)response.StatusCode}."));
        }

        var output = TryReadOpenAiOutput(payload);
        return Result<ProviderPromptExecutionResponse>.Success(new ProviderPromptExecutionResponse(
            profile.Name,
            string.IsNullOrWhiteSpace(request.ModelOverride) ? profile.DefaultModel : request.ModelOverride!,
            output,
            request.OutputFormat,
            request.ContainsSensitiveContent,
            request.ContainsSensitiveContent ? "Sensitive content was included in the outbound payload." : null));
    }

    public async Task<Result<ProviderModelPricingDiscoveryResult>> DiscoverModelPricingAsync(
        ProviderProfile profile,
        string? secretValue,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(secretValue))
        {
            return Result<ProviderModelPricingDiscoveryResult>.Failure(
                Error.Validation("OpenAI profiles require an API key secret before model pricing can be loaded from the provider API."));
        }

        using var client = CreateClient(profile, secretValue);
        using var response = await client.GetAsync(GetModelsUrl(profile.BaseUrl), cancellationToken);
        var payload = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return Result<ProviderModelPricingDiscoveryResult>.Failure(
                Error.Failure($"OpenAI model pricing discovery failed with HTTP {(int)response.StatusCode}."));
        }

        return Result<ProviderModelPricingDiscoveryResult>.Success(ReadOpenAiModelPricing(payload));
    }

    private HttpClient CreateClient(ProviderProfile profile, string secretValue)
    {
        var client = httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(Math.Max(5, profile.TimeoutSeconds));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", secretValue);
        return client;
    }

    private static string GetModelsUrl(string baseUrl)
        => NormalizeRoot(baseUrl).TrimEnd('/') + "/models";

    private static string GetResponsesUrl(string baseUrl)
        => NormalizeRoot(baseUrl).TrimEnd('/') + "/responses";

    private static string NormalizeRoot(string baseUrl)
    {
        var normalized = baseUrl.Trim().TrimEnd('/');
        return normalized.EndsWith("/models", StringComparison.OrdinalIgnoreCase)
            ? normalized[..^"/models".Length]
            : normalized;
    }

    private static string TryReadOpenAiOutput(string payload)
    {
        using var document = JsonDocument.Parse(payload);
        if (document.RootElement.TryGetProperty("output_text", out var outputTextElement) &&
            outputTextElement.ValueKind == JsonValueKind.String)
        {
            return outputTextElement.GetString() ?? string.Empty;
        }

        if (document.RootElement.TryGetProperty("output", out var outputElement) &&
            outputElement.ValueKind == JsonValueKind.Array &&
            outputElement.GetArrayLength() > 0)
        {
            var first = outputElement[0];
            if (first.TryGetProperty("content", out var contentElement) &&
                contentElement.ValueKind == JsonValueKind.Array &&
                contentElement.GetArrayLength() > 0)
            {
                var content = contentElement[0];
                if (content.TryGetProperty("text", out var textElement) && textElement.ValueKind == JsonValueKind.String)
                {
                    return textElement.GetString() ?? string.Empty;
                }
            }
        }

        return payload;
    }

    private static ProviderModelPricingDiscoveryResult ReadOpenAiModelPricing(string payload)
    {
        using var document = JsonDocument.Parse(payload);
        var discoveredModels = EnumerateOpenAiModelElements(document.RootElement)
            .Select(ReadOpenAiModelPricing)
            .Where(model => !string.IsNullOrWhiteSpace(model.Model))
            .OrderBy(model => model.Model, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var explicitPriceCount = discoveredModels.Count(model => model.HasExplicitPrices);
        var message = explicitPriceCount > 0
            ? $"Loaded exact pricing for {explicitPriceCount} model(s) from provider API."
            : $"Loaded {discoveredModels.Count} model name(s); provider API did not return exact price metadata.";

        return new ProviderModelPricingDiscoveryResult(discoveredModels, message);
    }

    private static IEnumerable<JsonElement> EnumerateOpenAiModelElements(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Object &&
            root.TryGetProperty("data", out var dataElement) &&
            dataElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in dataElement.EnumerateArray())
            {
                yield return item;
            }
        }
        else if (root.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in root.EnumerateArray())
            {
                yield return item;
            }
        }
    }

    private static ProviderDiscoveredModelPrice ReadOpenAiModelPricing(JsonElement element)
    {
        var model = ResolveModelName(element);
        if (string.IsNullOrWhiteSpace(model))
        {
            return new ProviderDiscoveredModelPrice(string.Empty, null, null, null);
        }

        return TryReadExplicitPrice(element, model, out var discoveredPrice)
            ? discoveredPrice
            : new ProviderDiscoveredModelPrice(model, null, null, null);
    }

    private static string ResolveModelName(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.String)
        {
            return element.GetString()?.Trim() ?? string.Empty;
        }

        if (element.ValueKind != JsonValueKind.Object)
        {
            return string.Empty;
        }

        foreach (var propertyName in ModelIdPropertyNames)
        {
            if (element.TryGetProperty(propertyName, out var property) &&
                property.ValueKind == JsonValueKind.String)
            {
                return property.GetString()?.Trim() ?? string.Empty;
            }
        }

        return string.Empty;
    }

    private static bool TryReadExplicitPrice(
        JsonElement element,
        string model,
        out ProviderDiscoveredModelPrice discoveredPrice)
    {
        if (element.ValueKind == JsonValueKind.Object &&
            element.TryGetProperty("pricing", out var pricingElement) &&
            TryReadExplicitPriceCore(pricingElement, model, out discoveredPrice))
        {
            return true;
        }

        return TryReadExplicitPriceCore(element, model, out discoveredPrice);
    }

    private static bool TryReadExplicitPriceCore(
        JsonElement element,
        string model,
        out ProviderDiscoveredModelPrice discoveredPrice)
    {
        discoveredPrice = default!;
        if (element.ValueKind != JsonValueKind.Object ||
            !TryReadNonNegativeDecimal(element, InputPricePropertyNames, out var inputPrice) ||
            !TryReadNonNegativeDecimal(element, CachedInputPricePropertyNames, out var cachedInputPrice) ||
            !TryReadNonNegativeDecimal(element, OutputPricePropertyNames, out var outputPrice))
        {
            return false;
        }

        discoveredPrice = new ProviderDiscoveredModelPrice(
            model,
            inputPrice,
            cachedInputPrice,
            outputPrice);
        return true;
    }

    private static bool TryReadNonNegativeDecimal(
        JsonElement element,
        IReadOnlyList<string> propertyNames,
        out decimal value)
    {
        value = 0m;
        foreach (var propertyName in propertyNames)
        {
            if (!element.TryGetProperty(propertyName, out var property))
            {
                continue;
            }

            if (property.ValueKind == JsonValueKind.Number &&
                property.TryGetDecimal(out var numericValue) &&
                numericValue >= 0m)
            {
                value = numericValue;
                return true;
            }

            if (property.ValueKind == JsonValueKind.String &&
                decimal.TryParse(
                    property.GetString(),
                    NumberStyles.Number,
                    CultureInfo.InvariantCulture,
                    out var stringValue) &&
                stringValue >= 0m)
            {
                value = stringValue;
                return true;
            }
        }

        return false;
    }
}

internal sealed class ScenarioHarnessProviderAdapter : IProviderAdapter
{
    public const string PluginKey = ProviderConnectorKeys.ScenarioHarness;
    public const string BaseUrl = ProviderConnectorDefaults.ScenarioHarnessBaseUrl;
    public const string DefaultModel = ProviderConnectorDefaults.ScenarioHarnessModel;

    private static readonly ConnectorPluginManifest PluginManifest = new(
        PluginKey,
        "Scenario harness provider",
        "1.0.0",
        ConnectorManifestCapability.ProviderExecution | ConnectorManifestCapability.AgentExposure,
        new ConnectorConfigurationSchema(
            "1.0",
            [
                new ConnectorConfigFieldDescriptor(ProviderConnectorFieldKeys.BaseUrl, "Base URL", ConnectorConfigFieldType.Url, true, "Deterministic scenario harness endpoint."),
                new ConnectorConfigFieldDescriptor(ProviderConnectorFieldKeys.DefaultModel, "Default model", ConnectorConfigFieldType.Text, true, "Scenario harness model alias."),
                new ConnectorConfigFieldDescriptor(ProviderConnectorFieldKeys.TimeoutSeconds, "Timeout", ConnectorConfigFieldType.Number, true, "Execution timeout in seconds.")
            ]),
        [],
        new ConnectorHealthCheckDescriptor("scenario-harness", "Confirms that the deterministic scenario harness route is configured."),
        new ConnectorAgentExposure("workspace.prompt.send", true, true, "Allows deterministic scenario execution without an external secret."),
        null);

    public ConnectorPluginManifest Manifest => PluginManifest;

    public ProviderKind? LegacyProviderKind => null;

    public Task<ProviderHealthCheckResult> CheckHealthAsync(
        ProviderProfile profile,
        string? secretValue,
        CancellationToken cancellationToken = default)
    {
        var isConfigured = string.Equals(profile.BaseUrl, BaseUrl, StringComparison.OrdinalIgnoreCase);
        var message = isConfigured
            ? "Scenario harness provider is available for deterministic proof runs."
            : $"Scenario harness profiles must use '{BaseUrl}'.";

        return Task.FromResult(new ProviderHealthCheckResult(isConfigured, message));
    }

    public Task<Result<ProviderPromptExecutionResponse>> SendAsync(
        ProviderProfile profile,
        ProviderPromptExecutionRequest request,
        string? secretValue,
        CancellationToken cancellationToken = default)
    {
        if (!string.Equals(profile.BaseUrl, BaseUrl, StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(Result<ProviderPromptExecutionResponse>.Failure(
                Error.Validation($"Scenario harness profiles must use '{BaseUrl}'.")));
        }

        return Task.FromResult(Result<ProviderPromptExecutionResponse>.Success(new ProviderPromptExecutionResponse(
            profile.Name,
            string.IsNullOrWhiteSpace(request.ModelOverride) ? ResolveModel(profile) : request.ModelOverride!,
            "Scenario harness provider routes execution through the integrated AgentFramework runtime. Run SC03, SC04, SC10, or SC11 from the integrated shell instead of a raw provider send.",
            request.OutputFormat,
            request.ContainsSensitiveContent,
            request.ContainsSensitiveContent ? "Sensitive content was included in the deterministic scenario request." : null)));
    }

    private static string ResolveModel(ProviderProfile profile)
    {
        return string.IsNullOrWhiteSpace(profile.DefaultModel)
            ? DefaultModel
            : profile.DefaultModel;
    }
}

internal sealed class ProcessMockProviderAdapter : IProviderAdapter
{
    public const string PluginKey = ProviderConnectorKeys.ProcessMock;
    public const string BaseUrl = ProviderConnectorDefaults.ProcessMockBaseUrl;
    public const string DefaultModel = ProviderConnectorDefaults.ProcessMockModel;

    private static readonly ConnectorPluginManifest PluginManifest = new(
        PluginKey,
        "Process mock provider",
        "1.0.0",
        ConnectorManifestCapability.ProviderExecution | ConnectorManifestCapability.AgentExposure,
        new ConnectorConfigurationSchema(
            "1.0",
            [
                new ConnectorConfigFieldDescriptor(ProviderConnectorFieldKeys.BaseUrl, "Base URL", ConnectorConfigFieldType.Url, true, "Deterministic process mock endpoint."),
                new ConnectorConfigFieldDescriptor(ProviderConnectorFieldKeys.DefaultModel, "Default model", ConnectorConfigFieldType.Text, true, "Process mock model alias."),
                new ConnectorConfigFieldDescriptor(ProviderConnectorFieldKeys.TimeoutSeconds, "Timeout", ConnectorConfigFieldType.Number, true, "Execution timeout in seconds.")
            ]),
        [],
        new ConnectorHealthCheckDescriptor("process-mock", "Confirms that deterministic process mock routing is configured."),
        new ConnectorAgentExposure("workspace.prompt.send", true, true, "Allows deterministic process-flow execution without an external secret."),
        null);

    public ConnectorPluginManifest Manifest => PluginManifest;

    public ProviderKind? LegacyProviderKind => null;

    public Task<ProviderHealthCheckResult> CheckHealthAsync(
        ProviderProfile profile,
        string? secretValue,
        CancellationToken cancellationToken = default)
    {
        var isConfigured = string.Equals(profile.BaseUrl, BaseUrl, StringComparison.OrdinalIgnoreCase);
        var message = isConfigured
            ? "Process mock provider is available for deterministic process automation flow tuning."
            : $"Process mock profiles must use '{BaseUrl}'.";

        return Task.FromResult(new ProviderHealthCheckResult(isConfigured, message));
    }

    public Task<Result<ProviderPromptExecutionResponse>> SendAsync(
        ProviderProfile profile,
        ProviderPromptExecutionRequest request,
        string? secretValue,
        CancellationToken cancellationToken = default)
    {
        if (!string.Equals(profile.BaseUrl, BaseUrl, StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(Result<ProviderPromptExecutionResponse>.Failure(
                Error.Validation($"Process mock profiles must use '{BaseUrl}'.")));
        }

        return Task.FromResult(Result<ProviderPromptExecutionResponse>.Success(new ProviderPromptExecutionResponse(
            profile.Name,
            string.IsNullOrWhiteSpace(request.ModelOverride) ? ResolveModel(profile) : request.ModelOverride!,
            "Process mock provider routes execution through the AgentFramework process mock runtime. Use role-specific process mock agents from a process run instead of raw provider send.",
            request.OutputFormat,
            request.ContainsSensitiveContent,
            request.ContainsSensitiveContent ? "Sensitive content was included in the deterministic process mock request." : null)));
    }

    private static string ResolveModel(ProviderProfile profile)
    {
        return string.IsNullOrWhiteSpace(profile.DefaultModel)
            ? DefaultModel
            : profile.DefaultModel;
    }
}

internal sealed class ComfyUiProviderAdapter(IHttpClientFactory httpClientFactory) : IProviderAdapter
{
    public const string PluginKey = ProviderConnectorKeys.ComfyUi;
    public const string DefaultModel = ProviderConnectorDefaults.ComfyUiModel;

    private static readonly ConnectorPluginManifest PluginManifest = new(
        PluginKey,
        "ComfyUI local image provider",
        "1.0.0",
        ConnectorManifestCapability.ProviderExecution | ConnectorManifestCapability.AgentExposure,
        new ConnectorConfigurationSchema(
            "1.0",
            [
                new ConnectorConfigFieldDescriptor(ProviderConnectorFieldKeys.BaseUrl, "Base URL", ConnectorConfigFieldType.Url, true, "ComfyUI API root, usually http://127.0.0.1:8188."),
                new ConnectorConfigFieldDescriptor(ProviderConnectorFieldKeys.DefaultModel, "Workflow alias", ConnectorConfigFieldType.Text, true, "Human-readable workflow/model alias stored on the provider profile."),
                new ConnectorConfigFieldDescriptor(ProviderConnectorFieldKeys.TimeoutSeconds, "Timeout", ConnectorConfigFieldType.Number, true, "Maximum image-generation wait time in seconds."),
                new ConnectorConfigFieldDescriptor(ProviderConnectorFieldKeys.ComfyUiWorkflowTemplateJson, "Workflow template JSON", ConnectorConfigFieldType.Json, false, "ComfyUI API workflow JSON. Leave empty when using a workflow template path."),
                new ConnectorConfigFieldDescriptor(ProviderConnectorFieldKeys.ComfyUiWorkflowTemplatePath, "Workflow template path", ConnectorConfigFieldType.Text, false, "Local path to a ComfyUI API workflow JSON file. Used when inline workflow JSON is empty."),
                new ConnectorConfigFieldDescriptor(ProviderConnectorFieldKeys.ComfyUiPositivePromptNodeId, "Positive prompt node", ConnectorConfigFieldType.Text, true, "ComfyUI workflow node id whose inputs.text receives the image prompt."),
                new ConnectorConfigFieldDescriptor(ProviderConnectorFieldKeys.ComfyUiNegativePromptNodeId, "Negative prompt node", ConnectorConfigFieldType.Text, false, "Optional ComfyUI workflow node id whose inputs.text receives the configured negative prompt."),
                new ConnectorConfigFieldDescriptor(ProviderConnectorFieldKeys.ComfyUiOutputNodeId, "Output node", ConnectorConfigFieldType.Text, false, "Optional node id to restrict image outputs read from history."),
                new ConnectorConfigFieldDescriptor(ProviderConnectorFieldKeys.ComfyUiPollIntervalMilliseconds, "Poll interval", ConnectorConfigFieldType.Number, true, "History polling interval in milliseconds.")
            ]),
        [],
        new ConnectorHealthCheckDescriptor("GET /system_stats", "Verifies that the local ComfyUI endpoint is reachable."),
        new ConnectorAgentExposure("image_generation", true, true, "Allows image generation through a configured ComfyUI workflow provider profile."),
        null);

    public ConnectorPluginManifest Manifest => PluginManifest;

    public ProviderKind? LegacyProviderKind => null;

    public async Task<ProviderHealthCheckResult> CheckHealthAsync(
        ProviderProfile profile,
        string? secretValue,
        CancellationToken cancellationToken = default)
    {
        using var client = httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(Math.Max(5, profile.TimeoutSeconds));
        try
        {
            using var response = await client.GetAsync($"{profile.BaseUrl.TrimEnd('/')}/system_stats", cancellationToken);
            return new ProviderHealthCheckResult(
                response.IsSuccessStatusCode,
                response.IsSuccessStatusCode ? "Healthy" : $"HTTP {(int)response.StatusCode}");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return new ProviderHealthCheckResult(false, $"ComfyUI health check failed: {exception.Message}");
        }
    }

    public Task<Result<ProviderPromptExecutionResponse>> SendAsync(
        ProviderProfile profile,
        ProviderPromptExecutionRequest request,
        string? secretValue,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Result<ProviderPromptExecutionResponse>.Failure(
            Error.Validation("ComfyUI provider profiles are image-generation only and cannot run generic chat prompt execution.")));
    }
}

internal sealed class OllamaProviderAdapter(IHttpClientFactory httpClientFactory) : IProviderAdapter, IProviderModelPricingSource
{
    public const string PluginKey = ProviderConnectorKeys.Ollama;

    private static readonly ConnectorPluginManifest PluginManifest = new(
        PluginKey,
        "Ollama local provider",
        "1.0.0",
        ConnectorManifestCapability.ProviderExecution | ConnectorManifestCapability.AgentExposure,
        new ConnectorConfigurationSchema(
            "1.0",
            [
                new ConnectorConfigFieldDescriptor(ProviderConnectorFieldKeys.BaseUrl, "Base URL", ConnectorConfigFieldType.Url, true, "Ollama API root."),
                new ConnectorConfigFieldDescriptor(ProviderConnectorFieldKeys.DefaultModel, "Default model", ConnectorConfigFieldType.Text, true, "Model used for prompt execution."),
                new ConnectorConfigFieldDescriptor(ProviderConnectorFieldKeys.TimeoutSeconds, "Timeout", ConnectorConfigFieldType.Number, true, "HTTP timeout in seconds.")
            ]),
        [],
        new ConnectorHealthCheckDescriptor("GET /api/tags", "Verifies that the Ollama endpoint is reachable and returns model metadata."),
        new ConnectorAgentExposure("workspace.prompt.send", true, true, "Allows agent-triggered prompt execution through the provider profile."),
        null);

    public ConnectorPluginManifest Manifest => PluginManifest;

    public ProviderKind? LegacyProviderKind => ProviderKind.OllamaLocal;

    public async Task<ProviderHealthCheckResult> CheckHealthAsync(ProviderProfile profile, string? secretValue, CancellationToken cancellationToken = default)
    {
        using var client = CreateClient(profile);
        using var response = await client.GetAsync($"{profile.BaseUrl.TrimEnd('/')}/api/tags", cancellationToken);
        return new ProviderHealthCheckResult(response.IsSuccessStatusCode, response.IsSuccessStatusCode ? "Healthy" : $"HTTP {(int)response.StatusCode}");
    }

    public async Task<Result<ProviderPromptExecutionResponse>> SendAsync(
        ProviderProfile profile,
        ProviderPromptExecutionRequest request,
        string? secretValue,
        CancellationToken cancellationToken = default)
    {
        using var client = CreateClient(profile);
        using var response = await client.PostAsJsonAsync(
            $"{profile.BaseUrl.TrimEnd('/')}/api/generate",
            new
            {
                model = string.IsNullOrWhiteSpace(request.ModelOverride) ? profile.DefaultModel : request.ModelOverride,
                prompt = request.Prompt,
                stream = false
            },
            cancellationToken);

        var payload = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return Result<ProviderPromptExecutionResponse>.Failure(Error.Failure($"Ollama call failed with HTTP {(int)response.StatusCode}."));
        }

        using var document = JsonDocument.Parse(payload);
        var output = document.RootElement.TryGetProperty("response", out var responseElement) && responseElement.ValueKind == JsonValueKind.String
            ? responseElement.GetString() ?? string.Empty
            : payload;

        return Result<ProviderPromptExecutionResponse>.Success(new ProviderPromptExecutionResponse(
            profile.Name,
            string.IsNullOrWhiteSpace(request.ModelOverride) ? profile.DefaultModel : request.ModelOverride!,
            output,
            request.OutputFormat,
            request.ContainsSensitiveContent,
            request.ContainsSensitiveContent ? "Sensitive content was included in the outbound payload." : null));
    }

    public async Task<Result<ProviderModelPricingDiscoveryResult>> DiscoverModelPricingAsync(
        ProviderProfile profile,
        string? secretValue,
        CancellationToken cancellationToken = default)
    {
        using var client = CreateClient(profile);
        using var response = await client.GetAsync($"{profile.BaseUrl.TrimEnd('/')}/api/tags", cancellationToken);
        var payload = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return Result<ProviderModelPricingDiscoveryResult>.Failure(
                Error.Failure($"Ollama model discovery failed with HTTP {(int)response.StatusCode}."));
        }

        var models = ReadOllamaModelNames(payload)
            .Select(model => new ProviderDiscoveredModelPrice(model, null, null, null))
            .ToList();
        return Result<ProviderModelPricingDiscoveryResult>.Success(new ProviderModelPricingDiscoveryResult(
            models,
            $"Loaded {models.Count} Ollama model name(s); Ollama does not expose token pricing, so prices remain editable settings."));
    }

    private HttpClient CreateClient(ProviderProfile profile)
    {
        var client = httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(Math.Max(5, profile.TimeoutSeconds));
        return client;
    }

    private static IReadOnlyList<string> ReadOllamaModelNames(string payload)
    {
        using var document = JsonDocument.Parse(payload);
        return EnumerateOllamaModelElements(document.RootElement)
            .Select(ResolveOllamaModelName)
            .Where(model => !string.IsNullOrWhiteSpace(model))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(model => model, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IEnumerable<JsonElement> EnumerateOllamaModelElements(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Object &&
            root.TryGetProperty("models", out var modelsElement) &&
            modelsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in modelsElement.EnumerateArray())
            {
                yield return item;
            }
        }
        else if (root.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in root.EnumerateArray())
            {
                yield return item;
            }
        }
    }

    private static string ResolveOllamaModelName(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.String)
        {
            return element.GetString()?.Trim() ?? string.Empty;
        }

        if (element.ValueKind != JsonValueKind.Object)
        {
            return string.Empty;
        }

        if (element.TryGetProperty("name", out var nameElement) &&
            nameElement.ValueKind == JsonValueKind.String)
        {
            return nameElement.GetString()?.Trim() ?? string.Empty;
        }

        if (element.TryGetProperty("model", out var modelElement) &&
            modelElement.ValueKind == JsonValueKind.String)
        {
            return modelElement.GetString()?.Trim() ?? string.Empty;
        }

        return string.Empty;
    }
}

internal sealed class OllamaRemoteProviderAdapter(IHttpClientFactory httpClientFactory) : IProviderAdapter, IProviderModelPricingSource
{
    public const string PluginKey = ProviderConnectorKeys.OllamaRemote;

    private static readonly ConnectorPluginManifest PluginManifest = new(
        PluginKey,
        "Ollama remote provider",
        "1.0.0",
        ConnectorManifestCapability.ProviderExecution | ConnectorManifestCapability.AgentExposure,
        new ConnectorConfigurationSchema(
            "1.0",
            [
                new ConnectorConfigFieldDescriptor(ProviderConnectorFieldKeys.BaseUrl, "Base URL", ConnectorConfigFieldType.Url, true, "Remote Ollama API root."),
                new ConnectorConfigFieldDescriptor(ProviderConnectorFieldKeys.DefaultModel, "Default model", ConnectorConfigFieldType.Text, true, "Model used for prompt execution."),
                new ConnectorConfigFieldDescriptor(ProviderConnectorFieldKeys.TimeoutSeconds, "Timeout", ConnectorConfigFieldType.Number, true, "HTTP timeout in seconds.")
            ]),
        [],
        new ConnectorHealthCheckDescriptor("GET /api/tags", "Verifies that the remote Ollama endpoint is reachable."),
        new ConnectorAgentExposure("workspace.prompt.send", true, true, "Allows agent-triggered prompt execution through the provider profile."),
        null);

    private readonly OllamaProviderAdapter _inner = new(httpClientFactory);

    public ConnectorPluginManifest Manifest => PluginManifest;

    public ProviderKind? LegacyProviderKind => ProviderKind.OllamaRemote;

    public Task<ProviderHealthCheckResult> CheckHealthAsync(ProviderProfile profile, string? secretValue, CancellationToken cancellationToken = default)
        => _inner.CheckHealthAsync(profile, secretValue, cancellationToken);

    public Task<Result<ProviderPromptExecutionResponse>> SendAsync(
        ProviderProfile profile,
        ProviderPromptExecutionRequest request,
        string? secretValue,
        CancellationToken cancellationToken = default)
        => _inner.SendAsync(profile, request, secretValue, cancellationToken);

    public Task<Result<ProviderModelPricingDiscoveryResult>> DiscoverModelPricingAsync(
        ProviderProfile profile,
        string? secretValue,
        CancellationToken cancellationToken = default)
        => _inner.DiscoverModelPricingAsync(profile, secretValue, cancellationToken);
}

/* codex-capsule
kind: service
name: ProviderExecutionService
summary: Resolves provider profiles, secrets, and adapters to execute generated prompts through a neutral send contract.
owns: profile-resolution, adapter-dispatch, prompt-usage-send-boundary
deps: AppDbContext, ProviderRegistry, SecretService, IActivityStream
risks: missing-secret, unsupported-provider
tests: unit:ProviderExecutionServiceTests
inputs: ProviderPromptExecutionRequest
outputs: ProviderPromptExecutionResponse
*/
