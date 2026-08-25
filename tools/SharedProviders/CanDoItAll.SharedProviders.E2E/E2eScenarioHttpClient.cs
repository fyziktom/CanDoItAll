using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.SharedProviders.Abstractions;

namespace CanDoItAll.SharedProviders.E2E;

internal sealed class E2eScenarioHttpClient : IDisposable
{
    private const int MaximumResponseBytes = 2 * 1024 * 1024;
    private const int MaximumSseLineBytes = 128 * 1024;
    private const int MaximumSseLineCount = 8192;
    private const int MaximumSseFrameCount = 2048;
    private const int MaximumSseBytes = 2 * 1024 * 1024;
    private static readonly TimeSpan ContentDeadline = TimeSpan.FromSeconds(55);

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters =
        {
            new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower, allowIntegerValues: false)
        }
    };

    private static readonly JsonSerializerOptions AppJsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient client;

    public E2eScenarioHttpClient()
    {
        var handler = new SocketsHttpHandler
        {
            ActivityHeadersPropagator = DistributedContextPropagator.CreateNoOutputPropagator(),
            AllowAutoRedirect = false,
            ConnectTimeout = TimeSpan.FromSeconds(5),
            MaxResponseDrainSize = 0,
            PooledConnectionLifetime = TimeSpan.FromMinutes(2),
            UseCookies = false,
            UseProxy = false
        };
        client = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(55)
        };
    }

    public Task<HttpResponseMessage> GetAsync(
        Uri baseUri,
        string path,
        string? bearerToken,
        string? accessContext,
        CancellationToken cancellationToken)
        => SendAsync(
            baseUri,
            path,
            HttpMethod.Get,
            bearerToken,
            accessContext,
            content: null,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

    public async Task<HttpResponseMessage> GetIfNoneMatchAsync(
        Uri baseUri,
        string path,
        string bearerToken,
        string entityTag,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, Resolve(baseUri, path));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        request.Headers.IfNoneMatch.Add(EntityTagHeaderValue.Parse(entityTag));
        return await client.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
    }

    public Task<HttpResponseMessage> PostJsonAsync<T>(
        Uri baseUri,
        string path,
        string bearerToken,
        string? accessContext,
        T request,
        CancellationToken cancellationToken,
        HttpCompletionOption completionOption = HttpCompletionOption.ResponseHeadersRead)
        => SendAsync(
            baseUri,
            path,
            HttpMethod.Post,
            bearerToken,
            accessContext,
            JsonContent.Create(request, options: JsonOptions),
            completionOption,
            cancellationToken);

    public Task<HttpResponseMessage> PostRawJsonAsync(
        Uri baseUri,
        string path,
        string bearerToken,
        string? accessContext,
        string json,
        CancellationToken cancellationToken)
        => SendAsync(
            baseUri,
            path,
            HttpMethod.Post,
            bearerToken,
            accessContext,
            new StringContent(json, Encoding.UTF8, "application/json"),
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

    public Task<HttpResponseMessage> PostAppJsonAsync<T>(
        Uri baseUri,
        string path,
        string bearerToken,
        string? accessContext,
        T request,
        CancellationToken cancellationToken,
        E2eTraceContext? traceContext = null)
        => SendAsync(
            baseUri,
            path,
            HttpMethod.Post,
            bearerToken,
            accessContext,
            JsonContent.Create(request, options: AppJsonOptions),
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken,
            traceContext);

    public async Task<T> ReadAppJsonAsync<T>(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var json = await ReadBoundedStringAsync(response, cancellationToken);
        try
        {
            return JsonSerializer.Deserialize<T>(json, AppJsonOptions)
                ?? throw new E2eSafeException("An application response contained an empty JSON document.");
        }
        catch (JsonException exception)
        {
            throw new E2eSafeException("An application response contained invalid JSON.", exception);
        }
    }

    public Task<HttpResponseMessage> SendControlAsync<T>(
        Uri baseUri,
        HttpMethod method,
        string path,
        string? bearerToken,
        T? request,
        CancellationToken cancellationToken)
    {
        HttpContent? content = request is null
            ? null
            : JsonContent.Create(request, options: JsonOptions);
        return SendAsync(
            baseUri,
            path,
            method,
            bearerToken,
            accessContext: null,
            content,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
    }

    public async Task<string> ReadBoundedStringAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(response);
        if (response.Content.Headers.ContentLength > MaximumResponseBytes)
        {
            throw new E2eSafeException("A black-box response exceeded the E2E evidence limit.");
        }

        using var contentCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        contentCancellation.CancelAfter(ContentDeadline);
        var contentToken = contentCancellation.Token;
        await using var source = await response.Content.ReadAsStreamAsync(contentToken);
        using var buffer = new MemoryStream();
        var chunk = new byte[16 * 1024];
        while (true)
        {
            var read = await source.ReadAsync(chunk, contentToken);
            if (read == 0)
            {
                break;
            }

            if (buffer.Length + read > MaximumResponseBytes)
            {
                throw new E2eSafeException("A black-box response exceeded the E2E evidence limit.");
            }

            await buffer.WriteAsync(chunk.AsMemory(0, read), contentToken);
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    public async Task<T> ReadJsonAsync<T>(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var json = await ReadBoundedStringAsync(response, cancellationToken);
        try
        {
            return JsonSerializer.Deserialize<T>(json, JsonOptions)
                ?? throw new E2eSafeException("A black-box response contained an empty JSON document.");
        }
        catch (JsonException exception)
        {
            throw new E2eSafeException("A black-box response contained invalid JSON.", exception);
        }
    }

    public async Task<E2eSseObservation> ReadSseAsync(
        Uri baseUri,
        string path,
        string bearerToken,
        object request,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        using var response = await PostJsonAsync(
            baseUri,
            path,
            bearerToken,
            accessContext: null,
            request,
            cancellationToken,
            HttpCompletionOption.ResponseHeadersRead);
        var headersAt = stopwatch.Elapsed;
        if (response.StatusCode != HttpStatusCode.OK ||
            !string.Equals(
                response.Content.Headers.ContentType?.MediaType,
                "text/event-stream",
                StringComparison.OrdinalIgnoreCase))
        {
            return new E2eSseObservation(
                response.StatusCode,
                headersAt,
                FirstDataAt: null,
                CompletedAt: stopwatch.Elapsed,
                DataFrameCount: 0,
                HasDoneFrame: false,
                HasResponsesCompletedEvent: false);
        }

        using var contentCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        contentCancellation.CancelAfter(ContentDeadline);
        var contentToken = contentCancellation.Token;
        await using var stream = await response.Content.ReadAsStreamAsync(contentToken);
        var reader = new BoundedUtf8LineReader(
            stream,
            MaximumSseLineBytes,
            MaximumSseBytes);
        TimeSpan? firstDataAt = null;
        var frameCount = 0;
        var lineCount = 0;
        var hasDone = false;
        var hasCompletedEvent = false;
        string? currentEvent = null;
        while (await reader.ReadLineAsync(contentToken) is { } line)
        {
            lineCount++;
            if (lineCount > MaximumSseLineCount)
            {
                throw new E2eSafeException("An SSE response exceeded the E2E line-count limit.");
            }

            if (line.StartsWith("event: ", StringComparison.Ordinal))
            {
                currentEvent = line[7..];
                continue;
            }

            if (!line.StartsWith("data: ", StringComparison.Ordinal))
            {
                if (line.Length == 0)
                {
                    currentEvent = null;
                }

                continue;
            }

            firstDataAt ??= stopwatch.Elapsed;
            frameCount++;
            if (frameCount > MaximumSseFrameCount)
            {
                throw new E2eSafeException("An SSE response exceeded the E2E frame-count limit.");
            }

            var data = line[6..];
            hasDone |= string.Equals(data, "[DONE]", StringComparison.Ordinal);
            hasCompletedEvent |= string.Equals(
                currentEvent,
                "response.completed",
                StringComparison.Ordinal) || data.Contains(
                "\"type\":\"response.completed\"",
                StringComparison.Ordinal);
        }

        return new E2eSseObservation(
            response.StatusCode,
            headersAt,
            firstDataAt,
            stopwatch.Elapsed,
            frameCount,
            hasDone,
            hasCompletedEvent);
    }

    public async Task<E2eCancellationObservation> CancelAfterFirstSseDataAsync(
        Uri baseUri,
        string path,
        string bearerToken,
        object request,
        CancellationToken cancellationToken)
    {
        using var requestCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        requestCancellation.CancelAfter(ContentDeadline);
        using var response = await PostJsonAsync(
            baseUri,
            path,
            bearerToken,
            accessContext: null,
            request,
            requestCancellation.Token,
            HttpCompletionOption.ResponseHeadersRead);
        if (response.StatusCode != HttpStatusCode.OK)
        {
            return new E2eCancellationObservation(
                response.StatusCode,
                FirstDataReceived: false,
                FirstDataWasNonTerminal: false);
        }

        await using var stream = await response.Content.ReadAsStreamAsync(requestCancellation.Token);
        var reader = new BoundedUtf8LineReader(
            stream,
            MaximumSseLineBytes,
            MaximumSseBytes);
        var firstDataReceived = false;
        var firstDataWasNonTerminal = false;
        var lineCount = 0;
        var frameCount = 0;
        while (await reader.ReadLineAsync(requestCancellation.Token) is { } line)
        {
            lineCount++;
            if (lineCount > MaximumSseLineCount)
            {
                throw new E2eSafeException("An SSE response exceeded the E2E line-count limit.");
            }

            if (!line.StartsWith("data: ", StringComparison.Ordinal))
            {
                continue;
            }

            frameCount++;
            if (frameCount > MaximumSseFrameCount)
            {
                throw new E2eSafeException("An SSE response exceeded the E2E frame-count limit.");
            }

            firstDataReceived = true;
            var data = line[6..];
            firstDataWasNonTerminal = !string.Equals(data, "[DONE]", StringComparison.Ordinal) &&
                !data.Contains("\"type\":\"response.completed\"", StringComparison.Ordinal);
            requestCancellation.Cancel();
            break;
        }

        try
        {
            await stream.DisposeAsync();
        }
        catch (OperationCanceledException) when (requestCancellation.IsCancellationRequested)
        {
        }

        return new E2eCancellationObservation(
            response.StatusCode,
            firstDataReceived,
            firstDataWasNonTerminal);
    }

    public void Dispose()
        => client.Dispose();

    private async Task<HttpResponseMessage> SendAsync(
        Uri baseUri,
        string path,
        HttpMethod method,
        string? bearerToken,
        string? accessContext,
        HttpContent? content,
        HttpCompletionOption completionOption,
        CancellationToken cancellationToken,
        E2eTraceContext? traceContext = null)
    {
        using var request = new HttpRequestMessage(method, Resolve(baseUri, path))
        {
            Content = content
        };
        if (!string.IsNullOrEmpty(bearerToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        }

        if (accessContext is not null)
        {
            request.Headers.TryAddWithoutValidation(
                SharedProviderHeaders.AccessContextReference,
                accessContext);
        }

        traceContext?.Apply(request.Headers);

        return await client.SendAsync(request, completionOption, cancellationToken);
    }

    private static Uri Resolve(Uri baseUri, string path)
        => new(baseUri, path.TrimStart('/'));
}

internal sealed record E2eTraceContext(
    ActivityTraceId TraceId,
    ActivitySpanId ParentSpanId,
    ActivityTraceFlags TraceFlags,
    string TraceState)
{
    public const string TraceParentHeaderName = "traceparent";
    public const string TraceStateHeaderName = "tracestate";
    public const string BaggageHeaderName = "baggage";
    public const string DeterministicTraceState = "sb07=e2e";

    public string TraceParent
        => $"00-{TraceId}-{ParentSpanId}-{(byte)TraceFlags:x2}";

    public static E2eTraceContext Create()
        => new(
            ActivityTraceId.CreateRandom(),
            ActivitySpanId.CreateRandom(),
            ActivityTraceFlags.Recorded,
            DeterministicTraceState);

    public void Apply(HttpRequestHeaders headers)
    {
        ArgumentNullException.ThrowIfNull(headers);
        headers.TryAddWithoutValidation(TraceParentHeaderName, TraceParent);
        headers.TryAddWithoutValidation(TraceStateHeaderName, TraceState);
    }
}

internal sealed class BoundedUtf8LineReader(
    Stream stream,
    int maximumLineBytes,
    int maximumTotalBytes)
{
    private static readonly Encoding StrictUtf8 = new UTF8Encoding(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    private readonly byte[] readBuffer = new byte[4096];
    private readonly List<byte> lineBuffer = [];
    private int readOffset;
    private int readCount;
    private int totalBytes;

    public async ValueTask<string?> ReadLineAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            if (readOffset == readCount)
            {
                readCount = await stream.ReadAsync(readBuffer, cancellationToken);
                readOffset = 0;
                if (readCount == 0)
                {
                    return lineBuffer.Count == 0
                        ? null
                        : DecodeAndClearLine();
                }

                totalBytes = checked(totalBytes + readCount);
                if (totalBytes > maximumTotalBytes)
                {
                    throw new E2eSafeException("An SSE response exceeded the E2E aggregate-byte limit.");
                }
            }

            var value = readBuffer[readOffset++];
            if (value == (byte)'\n')
            {
                return DecodeAndClearLine();
            }

            lineBuffer.Add(value);
            if (lineBuffer.Count > maximumLineBytes)
            {
                throw new E2eSafeException("An SSE response exceeded the E2E line-byte limit.");
            }
        }
    }

    private string DecodeAndClearLine()
    {
        var length = lineBuffer.Count;
        if (length > 0 && lineBuffer[length - 1] == (byte)'\r')
        {
            length--;
        }

        try
        {
            return StrictUtf8.GetString(lineBuffer.ToArray(), 0, length);
        }
        catch (DecoderFallbackException exception)
        {
            throw new E2eSafeException("An SSE response contained invalid UTF-8.", exception);
        }
        finally
        {
            lineBuffer.Clear();
        }
    }
}

internal sealed record E2eSseObservation(
    HttpStatusCode StatusCode,
    TimeSpan HeadersAt,
    TimeSpan? FirstDataAt,
    TimeSpan CompletedAt,
    int DataFrameCount,
    bool HasDoneFrame,
    bool HasResponsesCompletedEvent);

internal sealed record E2eCancellationObservation(
    HttpStatusCode StatusCode,
    bool FirstDataReceived,
    bool FirstDataWasNonTerminal);

internal enum E2eFixtureFailureMode
{
    None,
    BadRequest,
    Unauthorized,
    RateLimited,
    InternalServerError,
    Timeout
}

internal enum E2eFixtureStreamMode
{
    Complete,
    HoldAfterFirstFrame
}

internal enum E2eFixtureSurface
{
    All,
    Models,
    ChatCompletions,
    Responses,
    ImageGenerations,
    ComfyUiSystemStats,
    ComfyUiPrompt,
    ComfyUiHistory,
    ComfyUiView
}

internal sealed record E2eFixtureControlRequest(
    E2eFixtureFailureMode FailureMode,
    E2eFixtureSurface Surface,
    E2eFixtureStreamMode StreamMode = E2eFixtureStreamMode.Complete);

internal sealed record E2eCapturedRequestHeaders(
    IReadOnlyList<string> Names,
    IReadOnlyDictionary<string, IReadOnlyList<string>> SafeValues,
    bool AuthorizationPresent,
    string? AuthorizationScheme,
    bool CookiePresent);

internal sealed record E2eCapturedRequest(
    long Sequence,
    DateTimeOffset ReceivedAtUtc,
    string Method,
    string Path,
    string QueryString,
    E2eCapturedRequestHeaders Headers,
    string Body,
    bool BodyTruncated,
    bool CancellationObserved,
    int? ResponseStatusCode,
    DateTimeOffset? CompletedAtUtc);

internal sealed record E2eCaptureSnapshot(
    int Capacity,
    int Count,
    IReadOnlyList<E2eCapturedRequest> Requests);
