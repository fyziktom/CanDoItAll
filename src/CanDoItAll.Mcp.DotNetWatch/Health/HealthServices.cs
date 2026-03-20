using System.Net.Security;
using System.Text.Json;
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
    IReadOnlyList<string> ActiveUrls);

public sealed record RuntimeProbePayload(bool IsReady, string? Summary, int? WatchIteration, IReadOnlyList<string>? ActiveUrls);

public sealed class HttpHealthProbe(RuntimeConfiguration configuration, ILogger<HttpHealthProbe> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _client = CreateHttpClient(configuration);

    public async Task<HealthSnapshot> ProbeAsync(IEnumerable<Uri> urls, CancellationToken cancellationToken)
    {
        string? lastUrl = null;
        DateTimeOffset? lastFailure = null;

        foreach (var url in urls)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!configuration.AllowExternalHealthHosts && !configuration.AllowedHealthHosts.Contains(url.Host))
            {
                throw new ToolInvocationException("SecurityViolation", $"Health probe URL host '{url.Host}' is not allowed.", new { host = url.Host });
            }

            lastUrl = url.ToString();
            try
            {
                using var response = await _client.GetAsync(url, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    lastFailure = DateTimeOffset.UtcNow;
                    continue;
                }

                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                var payload = await JsonSerializer.DeserializeAsync<RuntimeProbePayload>(stream, JsonOptions, cancellationToken);
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

        return new HealthSnapshot("Unhealthy", false, null, lastFailure, lastUrl, "Health probe did not succeed.", null, []);
    }

    private static HttpClient CreateHttpClient(RuntimeConfiguration configuration)
    {
        var handler = new HttpClientHandler();
        handler.ServerCertificateCustomValidationCallback = (request, _, _, errors) =>
        {
            if (errors == SslPolicyErrors.None)
            {
                return true;
            }

            if (!configuration.AcceptInsecureLocalhostHttps || request?.RequestUri is null)
            {
                return false;
            }

            var host = request.RequestUri.Host;
            return configuration.AllowedHealthHosts.Contains(host);
        };

        return new HttpClient(handler)
        {
            Timeout = configuration.HealthTimeout
        };
    }
}
