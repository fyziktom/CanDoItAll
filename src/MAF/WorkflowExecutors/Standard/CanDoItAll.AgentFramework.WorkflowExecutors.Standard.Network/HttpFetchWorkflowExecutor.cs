using System.Net;
using System.Net.Sockets;
using System.Text;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Network;

public sealed class HttpFetchWorkflowExecutor(
    IWorkflowHttpSecretHeaderApplier? secretHeaders = null,
    IWorkspaceFileService? files = null) : IWorkflowExecutor
{
    public static WorkflowExecutorDescriptor CredentialAwareDescriptor { get; } = BuiltInWorkflowExecutorDescriptors.HttpFetch with {
        ProviderReadOwner = WorkflowHttpSecretUse.DisclosureOwner
    };

    public WorkflowExecutorDescriptor Descriptor => CredentialAwareDescriptor;

    public async ValueTask<WorkflowNodeExecutionResult> ExecuteAsync(
        WorkflowExecutorExecutionContext context,
        WorkflowNodeInput input,
        CancellationToken cancellationToken = default)
    {
        var settings = WorkflowExecutorJson.Deserialize<WorkflowHttpExecutorSettings>(context.SettingsJson);
        var uri = WorkflowHttpRequestUriResolver.Resolve(settings, input);

        var maxBytes = Math.Clamp(settings.MaxResponseBytes, 1024, 5 * 1024 * 1024);
        using var request = new HttpRequestMessage(ToHttpMethod(settings.Method), uri);
        foreach (var header in settings.Headers)
        {
            if (string.IsNullOrWhiteSpace(header.Key))
            {
                continue;
            }

            request.Headers.TryAddWithoutValidation(header.Key.Trim(), header.Value);
        }

        using var secretUse = settings.SecretHeader.SecretId is null ? null : await (secretHeaders
            ?? throw new InvalidOperationException("HTTP executor secret header binding requires a registered Workflow owner header applier."))
            .ApplyAsync(context, input, request, cancellationToken);
        await EnsureAllowedTargetAsync(uri, settings.AllowPrivateNetworkTargets, cancellationToken);

        if (!string.IsNullOrEmpty(settings.Body) && settings.Method is not WorkflowHttpMethodKind.Get)
        {
            request.Content = new StringContent(settings.Body, Encoding.UTF8, "application/json");
        }

        using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = secretUse is null }) { Timeout = Timeout.InfiniteTimeSpan };
        using var response = await SendAsync(client, request, secretUse, cancellationToken);
        var body = await ReadBoundedBodyAsync(response, maxBytes, cancellationToken);
        body.Text = secretUse?.Redact(body.Text, body.IsTruncated) ?? body.Text;
        var reason = secretUse?.Redact(response.ReasonPhrase) ?? response.ReasonPhrase ?? string.Empty;
        if (secretUse is not null && response.Headers.Location is not null && (int)response.StatusCode is >= 300 and < 400) {
            throw new InvalidOperationException("Credentialed HTTP redirects require review and approval of the exact redirect destination; no redirect request was sent.");
        }
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"HTTP executor received {(int)response.StatusCode} {reason} from '{secretUse?.Redact(uri.ToString()) ?? uri.ToString()}'.");
        }

        var outputPath = string.Empty;
        if (settings.DownloadToWorkspace)
        {
            if (files is null)
            {
                throw new InvalidOperationException("HTTP download-to-workspace requires a registered workspace file service.");
            }

            outputPath = secretUse?.Redact(ResolveOutputPath(settings, uri)) ?? ResolveOutputPath(settings, uri);
            var writeResult = files.WriteTextFile(outputPath, body.Text, settings.Overwrite);
            EnsureSucceeded(writeResult);
        }

        var result = new
        {
            url = secretUse?.Redact(uri.ToString()) ?? uri.ToString(),
            statusCode = (int)response.StatusCode,
            reasonPhrase = reason,
            contentType = secretUse?.Redact(response.Content.Headers.ContentType?.ToString()) ?? response.Content.Headers.ContentType?.ToString() ?? string.Empty,
            body.Text,
            body.IsTruncated,
            downloadToWorkspace = settings.DownloadToWorkspace,
            outputPath,
            inputPayload = settings.IncludeInputPayload ? secretUse?.Redact(input.PayloadJson) ?? input.PayloadJson : string.Empty,
            headers = response.Headers
                .Concat(response.Content.Headers)
                .GroupBy(header => secretUse?.Redact(header.Key) ?? header.Key, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => string.Join(",", group.Select(header =>
                    secretUse?.RedactHeader(header.Key, string.Join(",", header.Value)) ?? string.Join(",", header.Value))), StringComparer.OrdinalIgnoreCase)
        };

        return WorkflowExecutorJson.Result(context, result) with {
            ProviderReadEvidence = secretUse?.Evidence is { } evidence ? [evidence] : []
        };
    }

    private static async Task<HttpResponseMessage> SendAsync(HttpClient client, HttpRequestMessage request,
        WorkflowHttpSecretUse? secretUse, CancellationToken cancellationToken) {
        try {
            return await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        } catch (HttpRequestException exception) when (secretUse is not null) {
            throw new HttpRequestException(secretUse.Redact(exception.Message), inner: null, exception.StatusCode);
        }
    }

    public static HttpMethod ToHttpMethod(WorkflowHttpMethodKind method)
        => method switch
        {
            WorkflowHttpMethodKind.Get => HttpMethod.Get,
            WorkflowHttpMethodKind.Post => HttpMethod.Post,
            WorkflowHttpMethodKind.Put => HttpMethod.Put,
            WorkflowHttpMethodKind.Patch => HttpMethod.Patch,
            WorkflowHttpMethodKind.Delete => HttpMethod.Delete,
            _ => throw new InvalidOperationException($"HTTP method '{method}' is not supported.")
        };

    private static async Task EnsureAllowedTargetAsync(
        Uri uri,
        bool allowPrivateNetworkTargets,
        CancellationToken cancellationToken)
    {
        if (allowPrivateNetworkTargets)
        {
            return;
        }

        if (string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("HTTP executor blocks localhost targets by default.");
        }

        IPAddress[] addresses;
        if (IPAddress.TryParse(uri.Host, out var literalAddress))
        {
            addresses = [literalAddress];
        }
        else
        {
            try
            {
                addresses = await Dns.GetHostAddressesAsync(uri.DnsSafeHost, cancellationToken);
            }
            catch (SocketException exception)
            {
                throw new InvalidOperationException($"HTTP executor could not resolve host '{uri.DnsSafeHost}'.", exception);
            }
        }

        if (addresses.Any(IsBlockedAddress))
        {
            throw new InvalidOperationException("HTTP executor blocks loopback, private, and link-local network targets by default.");
        }
    }

    private static bool IsBlockedAddress(IPAddress address)
    {
        if (IPAddress.IsLoopback(address))
        {
            return true;
        }

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var bytes = address.GetAddressBytes();
            return bytes[0] == 10 ||
                   bytes[0] == 127 ||
                   bytes[0] == 169 && bytes[1] == 254 ||
                   bytes[0] == 172 && bytes[1] is >= 16 and <= 31 ||
                   bytes[0] == 192 && bytes[1] == 168;
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            return address.IsIPv6LinkLocal ||
                   address.IsIPv6SiteLocal ||
                   address.IsIPv6UniqueLocal ||
                   address.Equals(IPAddress.IPv6Loopback);
        }

        return true;
    }

    private static string ResolveOutputPath(
        WorkflowHttpExecutorSettings settings,
        Uri uri)
    {
        if (!string.IsNullOrWhiteSpace(settings.OutputPath))
        {
            return settings.OutputPath.Trim();
        }

        string fileName = Uri.UnescapeDataString(uri.Segments.LastOrDefault()?.Trim('/') ?? string.Empty);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            fileName = "download.txt";
        }

        return $"downloads/{PortablePhysicalFileNamePolicy.Encode(fileName).PhysicalName}";
    }

    private static void EnsureSucceeded(WorkspaceFileMutationResult result) {
        if (!result.Succeeded) {
            throw new InvalidOperationException(result.Message);
        }
    }

    private static async Task<(string Text, bool IsTruncated)> ReadBoundedBodyAsync(
        HttpResponseMessage response,
        int maxBytes,
        CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var memory = new MemoryStream(capacity: Math.Min(maxBytes, 8192));
        var buffer = new byte[8192];
        var truncated = false;

        while (true)
        {
            var read = await stream.ReadAsync(buffer, cancellationToken);
            if (read == 0)
            {
                break;
            }

            var remaining = maxBytes - (int)memory.Length;
            if (read > remaining)
            {
                memory.Write(buffer, 0, Math.Max(remaining, 0));
                truncated = true;
                break;
            }

            memory.Write(buffer, 0, read);
        }

        return (Encoding.UTF8.GetString(memory.ToArray()), truncated);
    }
}
