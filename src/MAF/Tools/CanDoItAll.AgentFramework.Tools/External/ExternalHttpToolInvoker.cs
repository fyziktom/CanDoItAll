using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using CanDoItAll.AgentFramework.Capabilities.Abstractions;
using CanDoItAll.AgentFramework.Tools.Abstractions;

namespace CanDoItAll.AgentFramework.Tools;

public sealed class ExternalHttpToolInvoker(
    IExternalHttpTransport? transport = null) : IExternalHttpToolInvoker
{
    private readonly IExternalHttpTransport transport = transport ?? new HttpClientExternalHttpTransport();

    public async Task<ToolInvocationResult> InvokeAsync(
        ExternalHttpToolDescriptor descriptor,
        ToolInvocationRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(request);

        ExternalHttpResponse response;
        try
        {
            response = await transport.SendAsync(
                new ExternalHttpRequest(
                    descriptor.Method,
                    descriptor.Endpoint,
                    descriptor.Headers,
                    request.Input.GetRawText(),
                    descriptor.Timeout,
                    descriptor.MaxResponseBytes,
                    request.CorrelationId),
                cancellationToken);
        }
        catch (TimeoutException exception)
        {
            return Failure(
                descriptor,
                request.CorrelationId,
                CapabilityDiagnosticCategory.Timeout,
                "$.timeout",
                $"HTTP invocation to '{descriptor.Endpoint.Host}' exceeded timeout {descriptor.Timeout}. {exception.Message}",
                "Increase the timeout only after confirming the external endpoint is healthy and bounded.",
                timeout: descriptor.Timeout);
        }
        catch (OperationCanceledException)
        {
            return Failure(
                descriptor,
                request.CorrelationId,
                CapabilityDiagnosticCategory.Cancellation,
                "$",
                $"HTTP invocation to '{descriptor.Endpoint.Host}' was cancelled.",
                "Retry only if the caller still owns the setup or tool-call lifecycle.",
                timeout: descriptor.Timeout);
        }
        catch (Exception exception)
        {
            return Failure(
                descriptor,
                request.CorrelationId,
                CapabilityDiagnosticCategory.HttpStatus,
                "$.endpoint",
                $"HTTP invocation to '{descriptor.Endpoint.Host}' failed. {exception.GetType().Name}: {exception.Message}",
                "Check the endpoint, network access, and secret bindings.");
        }

        if ((int)response.StatusCode < 200 || (int)response.StatusCode > 299)
        {
            return Failure(
                descriptor,
                request.CorrelationId,
                CapabilityDiagnosticCategory.HttpStatus,
                "$.statusCode",
                $"HTTP {descriptor.Method} {descriptor.Endpoint.Host}{descriptor.Endpoint.AbsolutePath} returned {(int)response.StatusCode}. Headers: {FormatHeaders(descriptor.Headers)}. Body: {response.Body}",
                "Repair the endpoint, credentials, or setup payload before enabling this external HTTP tool.",
                httpStatusCode: (int)response.StatusCode);
        }

        JsonElement root;
        try
        {
            using var document = JsonDocument.Parse(response.Body);
            root = document.RootElement.Clone();
        }
        catch (JsonException exception)
        {
            return Failure(
                descriptor,
                request.CorrelationId,
                CapabilityDiagnosticCategory.JsonParse,
                "$",
                $"HTTP response from '{descriptor.Endpoint.Host}' was not valid JSON. {exception.Message}. Body: {response.Body}",
                "Return a JSON object matching the external HTTP tool output schema.");
        }

        foreach (var property in descriptor.RequiredOutputProperties)
        {
            if (root.TryGetProperty(property, out _))
            {
                continue;
            }

            return Failure(
                descriptor,
                request.CorrelationId,
                CapabilityDiagnosticCategory.SchemaValidation,
                "$." + property,
                $"HTTP response from '{descriptor.Endpoint.Host}' did not include required property '{property}'. Body: {response.Body}",
                "Return all required output schema properties from the external HTTP tool.");
        }

        return ToolInvocationResult.Success(request.CorrelationId, root);
    }

    private static ToolInvocationResult Failure(
        ExternalHttpToolDescriptor descriptor,
        string correlationId,
        CapabilityDiagnosticCategory category,
        string fieldPath,
        string detail,
        string repairHint,
        int? httpStatusCode = null,
        TimeSpan? timeout = null)
    {
        return ToolInvocationResult.Failure(correlationId,
        [
            ToolDiagnostics.Create(
                category,
                descriptor,
                fieldPath,
                detail,
                repairHint,
                correlationId,
                CapabilityTransportKind.ExternalHttp,
                httpStatusCode: httpStatusCode,
                timeout: timeout)
        ]);
    }

    private static string FormatHeaders(IReadOnlyDictionary<string, string> headers)
        => string.Join(", ", headers.Select(pair => $"{pair.Key}={ToolDiagnostics.Mask(pair.Value)}"));
}

internal sealed class HttpClientExternalHttpTransport : IExternalHttpTransport
{
    private static readonly HttpClient SharedClient = new();

    private readonly HttpClient client;

    public HttpClientExternalHttpTransport()
        : this(SharedClient)
    {
    }

    internal HttpClientExternalHttpTransport(HttpClient client)
    {
        this.client = client;
    }

    public async Task<ExternalHttpResponse> SendAsync(
        ExternalHttpRequest request,
        CancellationToken cancellationToken)
    {
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (request.Timeout > TimeSpan.Zero)
        {
            timeoutSource.CancelAfter(request.Timeout);
        }

        using var message = new HttpRequestMessage(request.Method, request.Endpoint)
        {
            Content = new StringContent(request.JsonBody, Encoding.UTF8, "application/json")
        };
        foreach (var header in request.Headers)
        {
            message.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        try
        {
            using var response = await client.SendAsync(
                message,
                HttpCompletionOption.ResponseHeadersRead,
                timeoutSource.Token);
            await using var bodyStream = await response.Content.ReadAsStreamAsync(timeoutSource.Token);
            var body = await ReadBoundedAsync(bodyStream, request.MaxResponseBytes, timeoutSource.Token);

            return new ExternalHttpResponse(response.StatusCode, body);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && timeoutSource.IsCancellationRequested)
        {
            throw new TimeoutException($"HTTP request exceeded the configured timeout of {request.Timeout}.");
        }
    }

    private static async Task<string> ReadBoundedAsync(
        Stream stream,
        int maxBytes,
        CancellationToken cancellationToken)
    {
        maxBytes = Math.Max(0, maxBytes);
        var buffer = new byte[Math.Min(Math.Max(maxBytes, 1), 8192)];
        using var body = new MemoryStream(capacity: Math.Min(maxBytes, 8192));
        while (body.Length < maxBytes)
        {
            var remaining = maxBytes - (int)body.Length;
            var read = await stream.ReadAsync(buffer.AsMemory(0, Math.Min(buffer.Length, remaining)), cancellationToken);
            if (read == 0)
            {
                break;
            }

            body.Write(buffer, 0, read);
        }

        return Encoding.UTF8.GetString(body.ToArray());
    }
}
