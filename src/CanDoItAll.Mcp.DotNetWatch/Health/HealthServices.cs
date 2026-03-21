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
}
