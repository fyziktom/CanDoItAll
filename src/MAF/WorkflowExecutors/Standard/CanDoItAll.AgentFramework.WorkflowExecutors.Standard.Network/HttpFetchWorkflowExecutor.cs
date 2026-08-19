using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Security.Abstractions;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Network;

public sealed class HttpFetchWorkflowExecutor(
    ISecretRuntimeResolver? secretResolver = null,
    IWorkspaceFileService? files = null) : IWorkflowExecutor
{
    public WorkflowExecutorDescriptor Descriptor => BuiltInWorkflowExecutorDescriptors.HttpFetch;

    public async ValueTask<WorkflowNodeExecutionResult> ExecuteAsync(
        WorkflowExecutorExecutionContext context,
        WorkflowNodeInput input,
        CancellationToken cancellationToken = default)
    {
        var settings = WorkflowExecutorJson.Deserialize<WorkflowHttpExecutorSettings>(context.SettingsJson);
        var resolvedUrl = ResolveUrl(settings, input);
        if (!Uri.TryCreate(resolvedUrl, UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("http" or "https"))
        {
            throw new InvalidOperationException("HTTP executor requires an absolute http or https URL.");
        }

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

        await ApplySecretHeaderAsync(request, settings.SecretHeader, context, cancellationToken);
        await EnsureAllowedTargetAsync(uri, settings.AllowPrivateNetworkTargets, cancellationToken);

        if (!string.IsNullOrEmpty(settings.Body) && settings.Method is not WorkflowHttpMethodKind.Get)
        {
            request.Content = new StringContent(settings.Body, Encoding.UTF8, "application/json");
        }

        using var client = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        var body = await ReadBoundedBodyAsync(response, maxBytes, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"HTTP executor received {(int)response.StatusCode} {response.ReasonPhrase} from '{uri}'.");
        }

        var outputPath = string.Empty;
        if (settings.DownloadToWorkspace)
        {
            if (files is null)
            {
                throw new InvalidOperationException("HTTP download-to-workspace requires a registered workspace file service.");
            }

            outputPath = ResolveOutputPath(settings, uri);
            var writeResult = files.WriteTextFile(outputPath, body.Text, settings.Overwrite);
            EnsureSucceeded(writeResult);
        }

        var result = new
        {
            url = uri.ToString(),
            statusCode = (int)response.StatusCode,
            reasonPhrase = response.ReasonPhrase ?? string.Empty,
            contentType = response.Content.Headers.ContentType?.ToString() ?? string.Empty,
            body.Text,
            body.IsTruncated,
            downloadToWorkspace = settings.DownloadToWorkspace,
            outputPath,
            inputPayload = settings.IncludeInputPayload ? input.PayloadJson : string.Empty,
            headers = response.Headers
                .Concat(response.Content.Headers)
                .ToDictionary(header => header.Key, header => string.Join(",", header.Value), StringComparer.OrdinalIgnoreCase)
        };

        return WorkflowExecutorJson.Result(context, result);
    }

    private static HttpMethod ToHttpMethod(WorkflowHttpMethodKind method)
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

    private static T EnsureSucceeded<T>(T result)
    {
        var succeededProperty = typeof(T).GetProperty("Succeeded");
        var messageProperty = typeof(T).GetProperty("Message");
        if (succeededProperty?.GetValue(result) is false)
        {
            var message = messageProperty?.GetValue(result)?.ToString() ?? "HTTP workspace write failed.";
            throw new InvalidOperationException(message);
        }

        return result;
    }

    private async Task ApplySecretHeaderAsync(
        HttpRequestMessage request,
        WorkflowHttpSecretHeaderBinding binding,
        WorkflowExecutorExecutionContext context,
        CancellationToken cancellationToken)
    {
        if (binding.SecretId is not { } secretId)
        {
            return;
        }

        if (secretResolver is null)
        {
            throw new InvalidOperationException("HTTP executor secret header binding requires a registered secret runtime resolver.");
        }

        var headerName = NormalizeHeaderName(binding.HeaderName);
        var secretValue = await secretResolver.ResolveValueAsync(
            new SecretRuntimeRequest(
                secretId,
                string.IsNullOrWhiteSpace(binding.Purpose)
                    ? WorkflowSecretPurposes.HttpHeader
                    : binding.Purpose.Trim(),
                [secretId],
                ConsumerType: SecretRuntimeConsumerTypes.WorkflowHttpExecutor,
                ConsumerId: SecretRuntimeConsumerIds.WorkflowNode(context.Definition.Id.Value, context.Node.Id.Value)),
            cancellationToken);
        if (string.IsNullOrWhiteSpace(secretValue))
        {
            throw new InvalidOperationException($"HTTP executor secret '{secretId:D}' was not found or did not contain a usable value.");
        }

        var headerValue = FormatSecretHeaderValue(binding, secretValue);
        request.Headers.Remove(headerName);
        if (!request.Headers.TryAddWithoutValidation(headerName, headerValue))
        {
            throw new InvalidOperationException($"HTTP executor could not apply secret header '{headerName}'.");
        }
    }

    private static string NormalizeHeaderName(string headerName)
    {
        if (string.IsNullOrWhiteSpace(headerName))
        {
            throw new InvalidOperationException("HTTP executor secret header name is required.");
        }

        var normalized = headerName.Trim();
        if (normalized.Contains('\r', StringComparison.Ordinal) ||
            normalized.Contains('\n', StringComparison.Ordinal))
        {
            throw new InvalidOperationException("HTTP executor secret header name cannot contain line breaks.");
        }

        return normalized;
    }

    private static string FormatSecretHeaderValue(
        WorkflowHttpSecretHeaderBinding binding,
        string secretValue)
    {
        if (secretValue.Contains('\r', StringComparison.Ordinal) ||
            secretValue.Contains('\n', StringComparison.Ordinal))
        {
            throw new InvalidOperationException("HTTP executor secret header value cannot contain line breaks.");
        }

        return binding.ValueFormat switch
        {
            WorkflowHttpSecretValueFormat.Raw => secretValue,
            WorkflowHttpSecretValueFormat.Bearer => $"Bearer {secretValue}",
            WorkflowHttpSecretValueFormat.Basic => $"Basic {secretValue}",
            WorkflowHttpSecretValueFormat.CustomPrefix => $"{NormalizeHeaderPrefix(binding.CustomPrefix)} {secretValue}",
            _ => throw new InvalidOperationException($"HTTP secret header value format '{binding.ValueFormat}' is not supported.")
        };
    }

    private static string NormalizeHeaderPrefix(string prefix)
    {
        if (string.IsNullOrWhiteSpace(prefix))
        {
            throw new InvalidOperationException("HTTP executor custom secret header prefix is required.");
        }

        var normalized = prefix.Trim();
        if (normalized.Contains('\r', StringComparison.Ordinal) ||
            normalized.Contains('\n', StringComparison.Ordinal))
        {
            throw new InvalidOperationException("HTTP executor custom secret header prefix cannot contain line breaks.");
        }

        return normalized;
    }

    private static string ResolveUrl(
        WorkflowHttpExecutorSettings settings,
        WorkflowNodeInput input)
    {
        if (!string.IsNullOrWhiteSpace(settings.Url))
        {
            return settings.Url.Trim();
        }

        var url = ResolveInputJsonString(input, settings.UrlJsonPath, nameof(settings.UrlJsonPath));
        return string.IsNullOrWhiteSpace(url)
            ? throw new InvalidOperationException("HTTP executor setting 'Url' or 'UrlJsonPath' is required.")
            : url.Trim();
    }

    private static string? ResolveInputJsonString(
        WorkflowNodeInput input,
        string jsonPath,
        string settingName)
    {
        if (string.IsNullOrWhiteSpace(jsonPath))
        {
            return null;
        }

        if (!WorkflowRoutingValidation.TryParseJsonPath(jsonPath.Trim(), out var path, out var pathError))
        {
            throw new InvalidOperationException($"HTTP executor setting '{settingName}' has invalid JSON path: {pathError}.");
        }

        if (string.IsNullOrWhiteSpace(input.PayloadJson))
        {
            throw new InvalidOperationException($"HTTP executor setting '{settingName}' requires a workflow JSON payload.");
        }

        using var document = JsonDocument.Parse(input.PayloadJson);
        if (!TryResolve(document.RootElement, path, out var value))
        {
            throw new InvalidOperationException($"HTTP executor setting '{settingName}' path '{jsonPath}' was not found in the workflow payload.");
        }

        return value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : value.GetRawText();
    }

    private static bool TryResolve(
        JsonElement root,
        IReadOnlyList<BuiltInJsonPathSegment> path,
        out JsonElement value)
    {
        value = root;
        foreach (var segment in path)
        {
            if (segment.PropertyName is not null)
            {
                if (value.ValueKind != JsonValueKind.Object ||
                    !value.TryGetProperty(segment.PropertyName, out value))
                {
                    return false;
                }

                continue;
            }

            if (segment.Index is not { } targetIndex || value.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            var currentIndex = 0;
            var matched = false;
            foreach (var item in value.EnumerateArray())
            {
                if (currentIndex == targetIndex)
                {
                    value = item;
                    matched = true;
                    break;
                }

                currentIndex++;
            }

            if (!matched)
            {
                return false;
            }
        }

        return true;
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
