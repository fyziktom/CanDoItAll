using System.Net;
using System.Text;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.AgentFramework.Maf;

internal sealed class OllamaChatResponseException : HttpRequestException
{
    public OllamaChatResponseException(
        HttpStatusCode statusCode,
        string providerDetail,
        bool isTransient)
        : base(
            BuildMessage(statusCode, providerDetail),
            inner: null,
            statusCode)
    {
        ProviderDetail = providerDetail;
        IsTransient = isTransient;
    }

    public bool IsTransient { get; }

    public string ProviderDetail { get; }

    private static string BuildMessage(
        HttpStatusCode statusCode,
        string providerDetail)
    {
        var detail = string.IsNullOrWhiteSpace(providerDetail)
            ? "The provider returned no diagnostic detail."
            : $"Provider detail: {providerDetail}";
        return $"Ollama chat request failed with HTTP {(int)statusCode} ({statusCode}). {detail}";
    }
}

internal sealed class OllamaChatResponseResilienceHandler(
    HttpMessageHandler innerHandler,
    ProviderProfile provider,
    string model,
    ILogger? logger)
    : DelegatingHandler(innerHandler)
{
    private const int MaximumAttempts = 2;
    private const int MaximumProviderDetailCharacters = 1_200;

    private static readonly HashSet<HttpStatusCode> RetryableStatusCodes =
    [
        HttpStatusCode.RequestTimeout,
        HttpStatusCode.InternalServerError,
        HttpStatusCode.BadGateway,
        HttpStatusCode.ServiceUnavailable,
        HttpStatusCode.GatewayTimeout
    ];

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (!IsOllamaChatRequest(request))
        {
            return await base.SendAsync(request, cancellationToken);
        }

        RequestReplayContent? replayContent = null;
        var retried = false;

        for (var attempt = 1; attempt <= MaximumAttempts; attempt++)
        {
            var attemptRequest = attempt == 1
                ? request
                : CreateRetryRequest(
                    request,
                    replayContent ?? throw new InvalidOperationException(
                        "Ollama chat retry content was not captured after the first failed attempt."));
            HttpResponseMessage response;
            try
            {
                response = await base.SendAsync(attemptRequest, cancellationToken);
            }
            catch (HttpRequestException exception) when (
                attempt < MaximumAttempts &&
                !cancellationToken.IsCancellationRequested)
            {
                if (attempt > 1)
                {
                    attemptRequest.Dispose();
                }

                replayContent = await CaptureRequestContentAsync(
                    request.Content,
                    cancellationToken);
                retried = true;
                logger?.LogWarning(
                    exception,
                    "Ollama provider {ProviderName} model {Model} failed before a chat response on attempt {Attempt}/{MaximumAttempts}; retrying the same normalized model turn once.",
                    provider.Name,
                    model,
                    attempt,
                    MaximumAttempts);
                continue;
            }
            catch
            {
                if (attempt > 1)
                {
                    attemptRequest.Dispose();
                }

                throw;
            }

            if (response.IsSuccessStatusCode)
            {
                if (attempt > 1)
                {
                    response.RequestMessage = request;
                    attemptRequest.Dispose();
                }

                if (retried)
                {
                    logger?.LogWarning(
                        "Ollama provider {ProviderName} model {Model} recovered on chat attempt {Attempt}/{MaximumAttempts}.",
                        provider.Name,
                        model,
                        attempt,
                        MaximumAttempts);
                }

                return response;
            }

            var statusCode = response.StatusCode;
            string providerDetail;
            try
            {
                providerDetail = await ReadProviderDetailAsync(response, cancellationToken);
            }
            finally
            {
                response.Dispose();
                if (attempt > 1)
                {
                    attemptRequest.Dispose();
                }
            }

            var isTransient = RetryableStatusCodes.Contains(statusCode);
            if (isTransient && attempt < MaximumAttempts)
            {
                replayContent = await CaptureRequestContentAsync(
                    request.Content,
                    cancellationToken);
                retried = true;
                logger?.LogWarning(
                    "Ollama provider {ProviderName} model {Model} returned transient HTTP {StatusCode} on chat attempt {Attempt}/{MaximumAttempts}; retrying the same normalized model turn once. Provider detail: {ProviderDetail}",
                    provider.Name,
                    model,
                    (int)statusCode,
                    attempt,
                    MaximumAttempts,
                    providerDetail);
                continue;
            }

            throw new OllamaChatResponseException(
                statusCode,
                providerDetail,
                isTransient);
        }

        throw new InvalidOperationException("Ollama chat retry policy exited without a response or exception.");
    }

    private static bool IsOllamaChatRequest(HttpRequestMessage request)
    {
        return request.Method == HttpMethod.Post &&
               request.RequestUri?.AbsolutePath.EndsWith(
                   "/api/chat",
                   StringComparison.OrdinalIgnoreCase) == true;
    }

    private static HttpRequestMessage CreateRetryRequest(
        HttpRequestMessage request,
        RequestReplayContent replayContent)
    {
        var retryRequest = new HttpRequestMessage(request.Method, request.RequestUri)
        {
            Version = request.Version,
            VersionPolicy = request.VersionPolicy
        };
        foreach (var header in request.Headers)
        {
            retryRequest.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        foreach (var option in request.Options)
        {
            retryRequest.Options.Set(
                new HttpRequestOptionsKey<object?>(option.Key),
                option.Value);
        }

        if (replayContent.ContentBytes is null)
        {
            return retryRequest;
        }

        retryRequest.Content = new ByteArrayContent(replayContent.ContentBytes);
        foreach (var header in replayContent.ContentHeaders)
        {
            retryRequest.Content.Headers.TryAddWithoutValidation(header.Key, header.Values);
        }

        return retryRequest;
    }

    private static async Task<RequestReplayContent> CaptureRequestContentAsync(
        HttpContent? content,
        CancellationToken cancellationToken)
    {
        if (content is null)
        {
            return new RequestReplayContent(null, []);
        }

        var contentBytes = await content.ReadAsByteArrayAsync(cancellationToken);
        var contentHeaders = content.Headers
            .Select(header => (header.Key, Values: header.Value.ToArray()))
            .ToArray();
        return new RequestReplayContent(contentBytes, contentHeaders);
    }

    private static async Task<string> ReadProviderDetailAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        string rawBody;
        var wasTruncated = false;
        try
        {
            await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var reader = new StreamReader(
                contentStream,
                Encoding.UTF8,
                detectEncodingFromByteOrderMarks: true,
                leaveOpen: false);
            var buffer = new char[MaximumProviderDetailCharacters + 1];
            var charactersRead = await reader.ReadBlockAsync(
                buffer.AsMemory(),
                cancellationToken);
            rawBody = new string(
                buffer,
                0,
                Math.Min(charactersRead, MaximumProviderDetailCharacters));
            if (charactersRead > MaximumProviderDetailCharacters)
            {
                wasTruncated = true;
            }
        }
        catch (Exception exception) when (
            exception is IOException or InvalidOperationException)
        {
            rawBody = response.ReasonPhrase ?? string.Empty;
        }

        var trimmedBody = rawBody.TrimStart();
        string providerDetail;
        if (trimmedBody.StartsWith('{') || trimmedBody.StartsWith('['))
        {
            providerDetail = wasTruncated
                ? "The provider returned an oversized JSON error body; its contents were omitted."
                : TryReadErrorProperty(rawBody);
            if (string.IsNullOrWhiteSpace(providerDetail))
            {
                providerDetail = "The provider returned malformed JSON error detail or no string 'error' field.";
            }
        }
        else
        {
            providerDetail = wasTruncated
                ? rawBody + "…"
                : rawBody;
        }

        var compactDetail = string.Join(
            ' ',
            providerDetail.Split(
                ['\r', '\n', '\t'],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        var redactedDetail = WorkflowExecutorRedaction.RedactText(compactDetail);
        return redactedDetail.Length <= MaximumProviderDetailCharacters
            ? redactedDetail
            : redactedDetail[..MaximumProviderDetailCharacters] + "…";
    }

    private static string TryReadErrorProperty(string responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
        {
            return string.Empty;
        }

        try
        {
            using var document = JsonDocument.Parse(responseBody);
            return document.RootElement.ValueKind == JsonValueKind.Object &&
                   document.RootElement.TryGetProperty("error", out var error) &&
                   error.ValueKind == JsonValueKind.String
                ? error.GetString() ?? string.Empty
                : string.Empty;
        }
        catch (JsonException)
        {
            return string.Empty;
        }
    }

    private sealed record RequestReplayContent(
        byte[]? ContentBytes,
        IReadOnlyList<(string Key, string[] Values)> ContentHeaders);
}
