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

public interface IProviderAdapter
{
    ProviderKind ProviderKind { get; }

    Task<ProviderHealthResult> CheckHealthAsync(ProviderProfile profile, string? secretValue, CancellationToken cancellationToken = default);

    Task<Result<ProviderExecutionResponse>> SendAsync(
        ProviderProfile profile,
        ProviderExecutionRequest request,
        string? secretValue,
        CancellationToken cancellationToken = default);
}

public sealed class ProviderRegistry(IEnumerable<IProviderAdapter> adapters)
{
    private readonly IReadOnlyDictionary<ProviderKind, IProviderAdapter> _adapters =
        adapters.ToDictionary(adapter => adapter.ProviderKind);

    public IProviderAdapter? Resolve(ProviderKind providerKind)
        => _adapters.GetValueOrDefault(providerKind);

    public IReadOnlyCollection<ProviderKind> RegisteredKinds => _adapters.Keys.ToArray();
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
    public ProviderKind ProviderKind => ProviderKind.OpenAi;

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
    public ProviderKind ProviderKind => ProviderKind.OllamaLocal;

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
    private readonly OllamaProviderAdapter _inner = new(httpClientFactory);

    public ProviderKind ProviderKind => ProviderKind.OllamaRemote;

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

        var adapter = providerRegistry.Resolve(profile.ProviderKind);
        if (adapter is null)
        {
            return Result<ProviderExecutionResponse>.Failure(Error.Failure($"No adapter is registered for {profile.ProviderKind}."));
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
                $"Provider kind: {profile.ProviderKind}. Model: {result.Value!.Model}.",
                Route: "/settings"), cancellationToken);
        }

        return result;
    }
}
