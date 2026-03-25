using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.Mcp.DotNetWatch.Backend;
using CanDoItAll.Mcp.DotNetWatch.Configuration;

namespace CanDoItAll.Mcp.DotNetWatch.Bridge;

internal sealed record BridgeRepairState(
    DateTimeOffset? LastPingUtc,
    DateTimeOffset? LastRepairAttemptUtc,
    string Health,
    string? LastFailureCode,
    string? LastFailureMessage);

internal sealed record BridgeSendResult(
    HttpResponseMessage Response,
    BackendConnectionInfo Connection,
    bool Repaired);

internal sealed class BridgeRepairCoordinator(
    RuntimeConfiguration configuration,
    BackendConnectionManager connectionManager,
    IHttpClientFactory httpClientFactory,
    ILogger<BridgeRepairCoordinator> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
    private readonly object _gate = new();
    private BridgeRepairState _state = new(null, null, "Unknown", null, null);

    public BridgeStatusData CreateStatus()
    {
        lock (_gate)
        {
            var connection = connectionManager.TryGetCurrentConnection();
            var shadowInfo = ReadShadowManifest();
            return new BridgeStatusData(
                Mode: "DetachedBackend",
                BackendId: connection?.Registration.BackendId,
                LastPingUtc: _state.LastPingUtc,
                LastRepairAttemptUtc: _state.LastRepairAttemptUtc,
                CurrentShadowSignature: shadowInfo.Signature,
                CurrentShadowDllPath: shadowInfo.ShadowDllPath,
                Health: _state.Health)
            {
                CurrentShadowManifestPath = shadowInfo.ManifestPath
            };
        }
    }

    public async Task<BridgeSendResult> SendAsync(
        string route,
        object request,
        bool allowRepair,
        bool attachIdempotencyKey,
        CancellationToken cancellationToken)
    {
        var previousConnection = connectionManager.TryGetCurrentConnection();
        var current = await connectionManager.EnsureReadyAsync(cancellationToken);
        var ensuredRepair = previousConnection is not null &&
                            (previousConnection.Registration.ProcessId != current.Registration.ProcessId ||
                             !string.Equals(previousConnection.Registration.BackendId, current.Registration.BackendId, StringComparison.Ordinal));
        var requestId = attachIdempotencyKey ? $"req_{Guid.NewGuid():N}" : null;

        try
        {
            var response = await SendOnceAsync(current, route, request, requestId, cancellationToken);
            MarkHealthy(ensuredRepair);
            return new BridgeSendResult(response, current, Repaired: ensuredRepair);
        }
        catch (Exception ex) when (allowRepair && IsRepairable(ex))
        {
            logger.LogWarning(ex, "Bridge send failed for route {Route}; attempting repair.", route);
            MarkFailure(ClassifyFailureCode(ex), ex.Message);
            var repaired = await connectionManager.TryRepairAsync(cancellationToken);
            if (!repaired)
            {
                throw CreateTypedToolException(ex, "BridgeRepairFailed", "The stdio bridge could not repair the detached backend connection.");
            }

            var next = connectionManager.GetRequiredConnection();
            try
            {
                var retryResponse = await SendOnceAsync(next, route, request, requestId, cancellationToken);
                MarkHealthy(repaired: true);
                return new BridgeSendResult(retryResponse, next, Repaired: true);
            }
            catch (Exception retryEx) when (IsRepairable(retryEx))
            {
                MarkFailure(ClassifyFailureCode(retryEx), retryEx.Message, repairedAttempt: true);
                throw CreateTypedToolException(retryEx, ClassifyFailureCode(retryEx), "The stdio bridge repaired the backend reference but the call still failed.");
            }
        }
    }

    private async Task<HttpResponseMessage> SendOnceAsync(
        BackendConnectionInfo connection,
        string route,
        object request,
        string? requestId,
        CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient(nameof(BridgeRepairCoordinator));
        client.BaseAddress = new Uri(connection.BaseUrl, UriKind.Absolute);
        client.Timeout = Timeout.InfiniteTimeSpan;
        client.DefaultRequestHeaders.Remove(BackendAuth.HeaderName);
        client.DefaultRequestHeaders.Add(BackendAuth.HeaderName, connection.AuthToken);
        if (!string.IsNullOrWhiteSpace(requestId))
        {
            client.DefaultRequestHeaders.Remove("X-CanDoItAll-RequestId");
            client.DefaultRequestHeaders.Add("X-CanDoItAll-RequestId", requestId);
        }

        using var pingClient = httpClientFactory.CreateClient($"{nameof(BridgeRepairCoordinator)}-ping");
        pingClient.BaseAddress = new Uri(connection.BaseUrl, UriKind.Absolute);
        pingClient.Timeout = configuration.BridgePingTimeout;
        pingClient.DefaultRequestHeaders.Authorization = null;
        pingClient.DefaultRequestHeaders.Remove(BackendAuth.HeaderName);
        pingClient.DefaultRequestHeaders.Add(BackendAuth.HeaderName, connection.AuthToken);

        using var pingResponse = await pingClient.GetAsync("/api/backend/ping", cancellationToken);
        if (pingResponse.StatusCode == HttpStatusCode.Unauthorized)
        {
            throw new HttpRequestException("Backend auth mismatch.", inner: null, statusCode: HttpStatusCode.Unauthorized);
        }

        if (!pingResponse.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Backend ping failed with status code {(int)pingResponse.StatusCode}.", inner: null, statusCode: pingResponse.StatusCode);
        }

        using var response = await client.PostAsJsonAsync($"/api/tools/{route}", request, JsonOptions, cancellationToken);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            throw new HttpRequestException("Backend auth mismatch.", inner: null, statusCode: HttpStatusCode.Unauthorized);
        }

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            throw new HttpRequestException("Backend tool endpoint is unavailable.", inner: null, statusCode: HttpStatusCode.NotFound);
        }

        if ((int)response.StatusCode >= 500)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException($"Backend tool endpoint failed with {(int)response.StatusCode}: {body}", inner: null, statusCode: response.StatusCode);
        }

        var payload = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        var replay = new HttpResponseMessage(response.StatusCode)
        {
            Content = new ByteArrayContent(payload),
            RequestMessage = response.RequestMessage,
            ReasonPhrase = response.ReasonPhrase,
            Version = response.Version
        };

        foreach (var header in response.Headers)
        {
            replay.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        foreach (var header in response.Content.Headers)
        {
            replay.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        return replay;
    }

    private static bool IsRepairable(Exception ex)
        => ex is HttpRequestException or TaskCanceledException or OperationCanceledException;

    private static string ClassifyFailureCode(Exception ex)
    {
        return ex switch
        {
            HttpRequestException { StatusCode: HttpStatusCode.Unauthorized } => "BackendAuthMismatch",
            HttpRequestException { StatusCode: HttpStatusCode.NotFound } => "BackendUnavailable",
            HttpRequestException => "BackendUnavailable",
            TaskCanceledException => "BridgeRepairFailed",
            _ => "BridgeRepairFailed"
        };
    }

    private static ToolInvocationException CreateTypedToolException(Exception ex, string code, string message)
    {
        return new ToolInvocationException(code, message, new
        {
            exceptionType = ex.GetType().Name,
            exceptionMessage = ex.Message
        });
    }

    private void MarkHealthy(bool repaired = false)
    {
        lock (_gate)
        {
            _state = _state with
            {
                LastPingUtc = DateTimeOffset.UtcNow,
                Health = repaired ? "Repaired" : "Healthy",
                LastFailureCode = null,
                LastFailureMessage = null,
                LastRepairAttemptUtc = repaired ? DateTimeOffset.UtcNow : _state.LastRepairAttemptUtc
            };
        }
    }

    private void MarkFailure(string failureCode, string failureMessage, bool repairedAttempt = false)
    {
        lock (_gate)
        {
            _state = _state with
            {
                Health = repairedAttempt ? "RepairFailed" : "Degraded",
                LastFailureCode = failureCode,
                LastFailureMessage = failureMessage,
                LastRepairAttemptUtc = DateTimeOffset.UtcNow
            };
        }
    }

    private (string ManifestPath, string? Signature, string? ShadowDllPath) ReadShadowManifest()
    {
        var manifestPath = Path.Combine(configuration.WorkspaceRoot, ".artifacts", "mcp-server-shadow", "current.json");
        if (!File.Exists(manifestPath))
        {
            return (manifestPath, null, null);
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(manifestPath));
            var root = document.RootElement;
            var signature = root.TryGetProperty("signature", out var signatureElement) ? signatureElement.GetString() : null;
            var dllPath = root.TryGetProperty("shadowDllPath", out var dllElement) ? dllElement.GetString() : null;
            return (manifestPath, signature, dllPath);
        }
        catch
        {
            return (manifestPath, null, null);
        }
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
