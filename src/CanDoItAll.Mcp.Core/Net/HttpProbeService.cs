using System.Diagnostics;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using CanDoItAll.Mcp.Core.Contracts;

namespace CanDoItAll.Mcp.Core.Net;

public sealed record HttpProbeRequest(
    Uri Url,
    IReadOnlyList<int>? ExpectedStatuses = null,
    TimeSpan? Timeout = null,
    bool AllowInsecureTls = false,
    IReadOnlyCollection<string>? AllowedHosts = null,
    bool CaptureTls = true,
    bool CaptureBody = false);

public sealed record TlsCertificateSummary(
    string Host,
    bool Valid,
    string? Subject,
    string? Issuer,
    DateTimeOffset? NotBefore,
    DateTimeOffset? NotAfter,
    string? Thumbprint,
    string? CommonName);

public sealed record HttpProbeResult(
    Uri Url,
    bool Success,
    int? StatusCode,
    long DurationMs,
    string? Summary,
    TlsCertificateSummary? Tls,
    string? Body);

public sealed class HttpProbeService(ILogger<HttpProbeService> logger)
{
    public async Task<HttpProbeResult> ProbeAsync(HttpProbeRequest request, CancellationToken cancellationToken)
    {
        if (request.AllowedHosts is not null &&
            request.AllowedHosts.Count > 0 &&
            !request.AllowedHosts.Contains(request.Url.Host, StringComparer.OrdinalIgnoreCase))
        {
            throw new ToolInvocationException("SecurityViolation", $"HTTP probe URL host '{request.Url.Host}' is not allowed.", new { host = request.Url.Host });
        }

        TlsCertificateSummary? capturedTls = null;
        var stopwatch = Stopwatch.StartNew();
        var timeout = request.Timeout ?? TimeSpan.FromSeconds(10);
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        var handler = new HttpClientHandler();
        handler.ServerCertificateCustomValidationCallback = (_, certificate, _, errors) =>
        {
            if (request.CaptureTls && certificate is not null)
            {
                capturedTls = CreateSummary(request.Url.Host, certificate, errors == SslPolicyErrors.None);
            }

            return errors == SslPolicyErrors.None || request.AllowInsecureTls;
        };

        using var client = new HttpClient(handler)
        {
            Timeout = timeout
        };

        try
        {
            using var response = await client.GetAsync(request.Url, timeoutCts.Token);
            stopwatch.Stop();

            var expectedStatuses = request.ExpectedStatuses is { Count: > 0 }
                ? request.ExpectedStatuses
                : [200];
            var success = expectedStatuses.Contains((int)response.StatusCode);
            return new HttpProbeResult(
                request.Url,
                success,
                (int)response.StatusCode,
                stopwatch.ElapsedMilliseconds,
                success ? "HTTP probe succeeded." : $"HTTP probe returned {(int)response.StatusCode}.",
                capturedTls,
                request.CaptureBody ? await response.Content.ReadAsStringAsync(timeoutCts.Token) : null);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or AuthenticationException)
        {
            stopwatch.Stop();
            logger.LogDebug(ex, "HTTP probe failed for {Url}", request.Url);
            return new HttpProbeResult(
                request.Url,
                false,
                null,
                stopwatch.ElapsedMilliseconds,
                ex.Message,
                capturedTls,
                null);
        }
    }

    private static TlsCertificateSummary CreateSummary(string host, X509Certificate certificate, bool valid)
    {
        var typedCertificate = certificate as X509Certificate2 ?? new X509Certificate2(certificate);
        return new TlsCertificateSummary(
            host,
            valid,
            typedCertificate.Subject,
            typedCertificate.Issuer,
            typedCertificate.NotBefore,
            typedCertificate.NotAfter,
            typedCertificate.Thumbprint,
            typedCertificate.GetNameInfo(X509NameType.DnsName, false));
    }
}

public sealed class TlsCertificateInspector(ILogger<TlsCertificateInspector> logger)
{
    public Task<TlsCertificateSummary?> InspectAsync(Uri uri, CancellationToken cancellationToken)
    {
        var port = uri.Port > 0 ? uri.Port : 443;
        return InspectAsync(uri.Host, port, cancellationToken);
    }

    public async Task<TlsCertificateSummary?> InspectAsync(string host, int port, CancellationToken cancellationToken)
    {
        try
        {
            using var tcpClient = new TcpClient();
            await tcpClient.ConnectAsync(host, port, cancellationToken);
            await using var networkStream = tcpClient.GetStream();

            SslPolicyErrors observedErrors = SslPolicyErrors.None;
            X509Certificate2? certificate = null;
            using var sslStream = new SslStream(
                networkStream,
                leaveInnerStreamOpen: false,
                (_, presentedCertificate, _, errors) =>
                {
                    observedErrors = errors;
                    if (presentedCertificate is not null)
                    {
                        certificate = new X509Certificate2(presentedCertificate);
                    }

                    return true;
                });

            await sslStream.AuthenticateAsClientAsync(
                new SslClientAuthenticationOptions
                {
                    TargetHost = host
                },
                cancellationToken);

            if (certificate is null)
            {
                return null;
            }

            return new TlsCertificateSummary(
                host,
                observedErrors == SslPolicyErrors.None,
                certificate.Subject,
                certificate.Issuer,
                certificate.NotBefore,
                certificate.NotAfter,
                certificate.Thumbprint,
                certificate.GetNameInfo(X509NameType.DnsName, false));
        }
        catch (Exception ex) when (ex is IOException or SocketException or AuthenticationException)
        {
            logger.LogDebug(ex, "TLS inspection failed for {Host}:{Port}", host, port);
            return null;
        }
    }
}
