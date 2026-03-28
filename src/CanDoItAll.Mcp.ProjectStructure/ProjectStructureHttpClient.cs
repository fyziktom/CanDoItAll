using System.Net.Http.Json;
using System.Text.Json;
using CanDoItAll.Mcp.Core.Contracts;
using CanDoItAll.Modules.Workbench;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Mcp.ProjectStructure;

public sealed class ProjectStructureHttpClient
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient httpClient;
    private readonly RuntimeConfiguration runtimeConfiguration;
    private readonly ILogger<ProjectStructureHttpClient> logger;

    public ProjectStructureHttpClient(
        HttpClient httpClient,
        RuntimeConfiguration runtimeConfiguration,
        ILogger<ProjectStructureHttpClient> logger)
    {
        this.httpClient = httpClient;
        this.runtimeConfiguration = runtimeConfiguration;
        this.logger = logger;

        httpClient.BaseAddress = runtimeConfiguration.BaseAddress;
        httpClient.Timeout = runtimeConfiguration.Timeout;
    }

    public async Task<TResponse> GetAsync<TResponse>(string path, int? estimatedMinutes = null, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        ApplyHeaders(request, estimatedMinutes);
        return (await SendAsync<TResponse>(request, allowEmptyBody: false, cancellationToken))!;
    }

    public async Task<TResponse?> GetOptionalAsync<TResponse>(string path, int? estimatedMinutes = null, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        ApplyHeaders(request, estimatedMinutes);
        return await SendAsync<TResponse>(request, allowEmptyBody: true, cancellationToken);
    }

    public async Task<TResponse> PostAsync<TRequest, TResponse>(string path, TRequest payload, int? estimatedMinutes = null, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(payload, options: SerializerOptions)
        };
        ApplyHeaders(request, estimatedMinutes);
        return (await SendAsync<TResponse>(request, allowEmptyBody: false, cancellationToken))!;
    }

    public async Task<TResponse?> PostOptionalAsync<TRequest, TResponse>(string path, TRequest payload, int? estimatedMinutes = null, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(payload, options: SerializerOptions)
        };
        ApplyHeaders(request, estimatedMinutes);
        return await SendAsync<TResponse>(request, allowEmptyBody: true, cancellationToken);
    }

    public async Task<TResponse> PutAsync<TRequest, TResponse>(string path, TRequest payload, int? estimatedMinutes = null, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, path)
        {
            Content = JsonContent.Create(payload, options: SerializerOptions)
        };
        ApplyHeaders(request, estimatedMinutes);
        return (await SendAsync<TResponse>(request, allowEmptyBody: false, cancellationToken))!;
    }

    private async Task<TResponse?> SendAsync<TResponse>(HttpRequestMessage request, bool allowEmptyBody, CancellationToken cancellationToken)
    {
        using var response = await httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            ThrowToolError(response.StatusCode, body);
        }

        if (string.IsNullOrWhiteSpace(body))
        {
            if (allowEmptyBody)
            {
                return default;
            }

            throw new ToolInvocationException("RemoteApiEmpty", $"The remote project-structure API returned an empty payload for '{request.RequestUri}'.");
        }

        if (typeof(TResponse) == typeof(string))
        {
            return (TResponse)(object)body;
        }

        var payload = JsonSerializer.Deserialize<TResponse>(body, SerializerOptions);
        if (payload is null)
        {
            throw new ToolInvocationException("RemoteApiEmpty", $"The remote project-structure API returned an empty payload for '{request.RequestUri}'.");
        }

        return payload;
    }

    private void ApplyHeaders(HttpRequestMessage request, int? estimatedMinutes)
    {
        request.Headers.Add(ProjectStructureAgentHttpHeaders.AgentId, runtimeConfiguration.AgentId);
        request.Headers.Add(ProjectStructureAgentHttpHeaders.AgentName, runtimeConfiguration.AgentName);
        request.Headers.Add(ProjectStructureAgentHttpHeaders.MachineName, runtimeConfiguration.MachineName);
        request.Headers.Add(ProjectStructureAgentHttpHeaders.RepositoryRoot, runtimeConfiguration.RepositoryRoot);
        request.Headers.Add(ProjectStructureAgentHttpHeaders.BranchName, runtimeConfiguration.BranchName);
        request.Headers.Add(ProjectStructureAgentHttpHeaders.SessionId, runtimeConfiguration.SessionId);
        request.Headers.Add(ProjectStructureAgentHttpHeaders.AgentToken, runtimeConfiguration.AgentToken);
        if (estimatedMinutes.HasValue)
        {
            request.Headers.Add(ProjectStructureAgentHttpHeaders.EstimatedMinutes, estimatedMinutes.Value.ToString());
        }
    }

    private void ThrowToolError(System.Net.HttpStatusCode statusCode, string body)
    {
        try
        {
            var envelope = JsonSerializer.Deserialize<RemoteErrorEnvelope>(body, SerializerOptions);
            if (envelope?.Error is not null && !string.IsNullOrWhiteSpace(envelope.Error.ErrorCode))
            {
                logger.LogWarning(
                    "Project-structure API returned {StatusCode} with {ErrorCode}.",
                    (int)statusCode,
                    envelope.Error.ErrorCode);
                throw new ToolInvocationException(envelope.Error.ErrorCode, envelope.Error.Message, envelope.Error.Details);
            }
        }
        catch (JsonException)
        {
        }

        throw new ToolInvocationException(
            "RemoteApiFailed",
            $"The remote project-structure API returned {(int)statusCode} ({statusCode}).",
            new
            {
                StatusCode = (int)statusCode,
                Body = body
            });
    }

    private sealed record RemoteErrorEnvelope(RemoteErrorPayload? Error);

    private sealed record RemoteErrorPayload(string ErrorCode, string Message, JsonElement? Details);
}
