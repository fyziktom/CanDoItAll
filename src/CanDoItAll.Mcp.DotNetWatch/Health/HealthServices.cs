using System.Text.Json;
using CanDoItAll.Mcp.Core.Net;
using CanDoItAll.Mcp.DotNetWatch.Configuration;

namespace CanDoItAll.Mcp.DotNetWatch.Health;

public sealed record HealthSnapshot(
    string Status,
    bool IsReady,
    DateTimeOffset? LastSuccessUtc,
    DateTimeOffset? LastFailureUtc,
    string? LastUrl,
    string? Summary,
    int? WatchIteration,
    int? RuntimePid,
    IReadOnlyList<string> ActiveUrls);

public sealed record RuntimeProbePayload(
    bool IsReady,
    string? Summary,
    int? WatchIteration,
    int? RuntimePid,
    IReadOnlyList<string>? ActiveUrls);

public sealed class HttpHealthProbe(RuntimeConfiguration configuration, HttpProbeService probeService, ILogger<HttpHealthProbe> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<HealthSnapshot> ProbeAsync(IEnumerable<Uri> urls, CancellationToken cancellationToken)
    {
        string? lastUrl = null;
        DateTimeOffset? lastFailure = null;

        foreach (var url in urls)
        {
            cancellationToken.ThrowIfCancellationRequested();

            lastUrl = url.ToString();
            try
            {
                var result = await probeService.ProbeAsync(
                    new HttpProbeRequest(
                        url,
                        ExpectedStatuses: [200],
                        Timeout: configuration.HealthTimeout,
                        AllowInsecureTls: configuration.AcceptInsecureLocalhostHttps,
                        AllowedHosts: configuration.AllowExternalHealthHosts ? null : configuration.AllowedHealthHosts,
                        CaptureTls: false,
                        CaptureBody: true),
                    cancellationToken);

                if (!result.Success || string.IsNullOrWhiteSpace(result.Body))
                {
                    lastFailure = DateTimeOffset.UtcNow;
                    continue;
                }

                var payload = JsonSerializer.Deserialize<RuntimeProbePayload>(result.Body, JsonOptions);
                if (payload?.IsReady == true)
                {
                    return new HealthSnapshot(
                        "Healthy",
                        true,
                        DateTimeOffset.UtcNow,
                        null,
                        url.ToString(),
                        payload.Summary ?? "Ready",
                        payload.WatchIteration,
                        payload.RuntimePid,
                        payload.ActiveUrls ?? []);
                }

                if (TryInterpretGenericHealthPayload(result.Body, out var genericSummary))
                {
                    return new HealthSnapshot(
                        "Healthy",
                        true,
                        DateTimeOffset.UtcNow,
                        null,
                        url.ToString(),
                        genericSummary,
                        payload?.WatchIteration,
                        payload?.RuntimePid,
                        payload?.ActiveUrls is { Count: > 0 } activeUrls ? activeUrls : [url.ToString()]);
                }

                lastFailure = DateTimeOffset.UtcNow;
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Health probe failed for {Url}", url);
                lastFailure = DateTimeOffset.UtcNow;
            }
        }

        return new HealthSnapshot("Unhealthy", false, null, lastFailure, lastUrl, "Health probe did not succeed.", null, null, []);
    }

    private static bool TryInterpretGenericHealthPayload(string body, out string summary)
    {
        summary = "Ready";

        try
        {
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.ValueKind is not JsonValueKind.Object)
            {
                return false;
            }

            var root = document.RootElement;
            if (root.TryGetProperty("status", out var statusProperty) &&
                statusProperty.ValueKind is JsonValueKind.String)
            {
                var statusValue = statusProperty.GetString();
                if (string.Equals(statusValue, "ok", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(statusValue, "healthy", StringComparison.OrdinalIgnoreCase))
                {
                    summary = statusValue!;
                    return true;
                }
            }
        }
        catch (JsonException)
        {
            return false;
        }

        return false;
    }
}
