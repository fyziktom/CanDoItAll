using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Security;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Workspace;

public sealed record ProviderExecutionRequest(
    Guid ProviderProfileId,
    string Prompt,
    string? ModelOverride = null,
    string OutputFormat = "Markdown",
    bool ContainsSensitiveContent = false);

public sealed record ProviderExecutionResponse(
    string ProviderName,
    string Model,
    string OutputText,
    string OutputFormat,
    bool ContainsWarnings,
    string? WarningSummary = null);

internal static class ProviderConnectorFieldKeys
{
    public const string BaseUrl = "baseUrl";
    public const string DefaultModel = "defaultModel";
    public const string TimeoutSeconds = "timeoutSeconds";
}

public interface IProviderAdapter : IConnectorPlugin
{
    ProviderKind? LegacyProviderKind { get; }

    Task<ProviderHealthResult> CheckHealthAsync(ProviderProfile profile, string? secretValue, CancellationToken cancellationToken = default);

    Task<Result<ProviderExecutionResponse>> SendAsync(
        ProviderProfile profile,
        ProviderExecutionRequest request,
        string? secretValue,
        CancellationToken cancellationToken = default);
}

public sealed class ProviderRegistry(IEnumerable<IProviderAdapter> adapters) : IConnectorManifestSource
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

        return profile.ProviderKind is var legacyProviderKind
            ? adaptersByLegacyKind.GetValueOrDefault(legacyProviderKind)
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
}

/* codex-capsule
kind: adapter
name: OpenAiProviderAdapter
summary: Performs basic health and prompt send calls against the OpenAI HTTP API through the neutral provider contract.
owns: openai-health, openai-send
deps: IHttpClientFactory
risks: api-shape-drift, wrong-base-url
tests: unit:ProviderAdapterTests
inputs: ProviderProfile, ProviderExecutionRequest
outputs: ProviderExecutionResponse
*/
public sealed class OpenAiProviderAdapter(IHttpClientFactory httpClientFactory) : IProviderAdapter
{
    public const string PluginKey = "provider.openai";

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

    public async Task<ProviderHealthResult> CheckHealthAsync(ProviderProfile profile, string? secretValue, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(secretValue))
        {
            return new ProviderHealthResult(false, "OpenAI profiles require an API key secret.");
        }

        using var client = CreateClient(profile, secretValue);
        using var response = await client.GetAsync(GetModelsUrl(profile.BaseUrl), cancellationToken);
        return new ProviderHealthResult(response.IsSuccessStatusCode, response.IsSuccessStatusCode ? "Healthy" : $"HTTP {(int)response.StatusCode}");
    }

    public async Task<Result<ProviderExecutionResponse>> SendAsync(
        ProviderProfile profile,
        ProviderExecutionRequest request,
        string? secretValue,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(secretValue))
        {
            return Result<ProviderExecutionResponse>.Failure(Error.Validation("OpenAI profiles require an API key secret."));
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
            return Result<ProviderExecutionResponse>.Failure(Error.Failure($"OpenAI call failed with HTTP {(int)response.StatusCode}."));
        }

        var output = TryReadOpenAiOutput(payload);
        return Result<ProviderExecutionResponse>.Success(new ProviderExecutionResponse(
            profile.Name,
            string.IsNullOrWhiteSpace(request.ModelOverride) ? profile.DefaultModel : request.ModelOverride!,
            output,
            request.OutputFormat,
            request.ContainsSensitiveContent,
            request.ContainsSensitiveContent ? "Sensitive content was included in the outbound payload." : null));
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
}

public sealed class OllamaProviderAdapter(IHttpClientFactory httpClientFactory) : IProviderAdapter
{
    public const string PluginKey = "provider.ollama.local";

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

    public async Task<ProviderHealthResult> CheckHealthAsync(ProviderProfile profile, string? secretValue, CancellationToken cancellationToken = default)
    {
        using var client = CreateClient(profile);
        using var response = await client.GetAsync($"{profile.BaseUrl.TrimEnd('/')}/api/tags", cancellationToken);
        return new ProviderHealthResult(response.IsSuccessStatusCode, response.IsSuccessStatusCode ? "Healthy" : $"HTTP {(int)response.StatusCode}");
    }

    public async Task<Result<ProviderExecutionResponse>> SendAsync(
        ProviderProfile profile,
        ProviderExecutionRequest request,
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
            return Result<ProviderExecutionResponse>.Failure(Error.Failure($"Ollama call failed with HTTP {(int)response.StatusCode}."));
        }

        using var document = JsonDocument.Parse(payload);
        var output = document.RootElement.TryGetProperty("response", out var responseElement) && responseElement.ValueKind == JsonValueKind.String
            ? responseElement.GetString() ?? string.Empty
            : payload;

        return Result<ProviderExecutionResponse>.Success(new ProviderExecutionResponse(
            profile.Name,
            string.IsNullOrWhiteSpace(request.ModelOverride) ? profile.DefaultModel : request.ModelOverride!,
            output,
            request.OutputFormat,
            request.ContainsSensitiveContent,
            request.ContainsSensitiveContent ? "Sensitive content was included in the outbound payload." : null));
    }

    private HttpClient CreateClient(ProviderProfile profile)
    {
        var client = httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(Math.Max(5, profile.TimeoutSeconds));
        return client;
    }
}

public sealed class OllamaRemoteProviderAdapter(IHttpClientFactory httpClientFactory) : IProviderAdapter
{
    public const string PluginKey = "provider.ollama.remote";

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

    public Task<ProviderHealthResult> CheckHealthAsync(ProviderProfile profile, string? secretValue, CancellationToken cancellationToken = default)
        => _inner.CheckHealthAsync(profile, secretValue, cancellationToken);

    public Task<Result<ProviderExecutionResponse>> SendAsync(
        ProviderProfile profile,
        ProviderExecutionRequest request,
        string? secretValue,
        CancellationToken cancellationToken = default)
        => _inner.SendAsync(profile, request, secretValue, cancellationToken);
}

/* codex-capsule
kind: service
name: ProviderExecutionService
summary: Resolves provider profiles, secrets, and adapters to execute generated prompts through a neutral send contract.
owns: profile-resolution, adapter-dispatch, prompt-usage-send-boundary
deps: AppDbContext, ProviderRegistry, SecretService, IActivityStream
risks: missing-secret, unsupported-provider
tests: unit:ProviderExecutionServiceTests
inputs: ProviderExecutionRequest
outputs: ProviderExecutionResponse
*/
public sealed class ProviderExecutionService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    ProviderRegistry providerRegistry,
    SecretService secretService,
    IActivityStream activityStream)
{
    public async Task<Result<ProviderExecutionResponse>> SendAsync(ProviderExecutionRequest request, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var profile = await dbContext.Set<ProviderProfile>()
            .FirstOrDefaultAsync(item => item.Id == request.ProviderProfileId && item.IsEnabled, cancellationToken);

        if (profile is null)
        {
            return Result<ProviderExecutionResponse>.Failure(Error.Validation("Provider profile not found or disabled."));
        }

        var adapter = providerRegistry.Resolve(profile);
        if (adapter is null)
        {
            return Result<ProviderExecutionResponse>.Failure(Error.Failure(
                $"No adapter is registered for provider profile '{profile.Name}'."));
        }

        string? secretValue = null;
        if (profile.ApiKeySecretId.HasValue)
        {
            secretValue = (await secretService.GetAsync(profile.ApiKeySecretId.Value, cancellationToken))?.SecretValue;
        }

        var result = await adapter.SendAsync(profile, request, secretValue, cancellationToken);
        if (result.IsSuccess)
        {
            await activityStream.RecordAsync(new ActivityWriteRequest(
                "providers",
                "send",
                $"Sent prompt through {profile.Name}",
                $"Plugin: {adapter.Manifest.PluginKey}. Model: {result.Value!.Model}.",
                Route: "/settings"), cancellationToken);
        }

        return result;
    }
}


