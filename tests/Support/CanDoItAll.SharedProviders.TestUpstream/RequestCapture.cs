using System.Text;

namespace CanDoItAll.SharedProviders.TestUpstream;

public sealed record CapturedRequestHeaders(
    IReadOnlyList<string> Names,
    IReadOnlyDictionary<string, IReadOnlyList<string>> SafeValues,
    bool AuthorizationPresent,
    string? AuthorizationScheme,
    bool CookiePresent);

public sealed record CapturedRequest(
    long Sequence,
    DateTimeOffset ReceivedAtUtc,
    string Method,
    string Path,
    string QueryString,
    CapturedRequestHeaders Headers,
    string Body,
    bool BodyTruncated,
    bool CancellationObserved,
    int? ResponseStatusCode,
    DateTimeOffset? CompletedAtUtc);

public sealed record RequestCaptureSnapshot(
    int Capacity,
    int Count,
    IReadOnlyList<CapturedRequest> Requests);

public sealed record CaptureResetResponse(int RemovedCount);

internal sealed class RequestCaptureStore
{
    private static readonly HashSet<string> SafeValueHeaderNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Accept",
        "Content-Type",
        "traceparent",
        "tracestate",
        "User-Agent",
        "x-request-id"
    };

    private readonly object sync = new();
    private readonly LinkedList<MutableCapturedRequest> captures = new();
    private readonly Dictionary<long, LinkedListNode<MutableCapturedRequest>> capturesBySequence = [];
    private long sequence;

    public async ValueTask<long> AddAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        var (body, truncated) = await ReadBodyAsync(request, cancellationToken);
        var captured = new MutableCapturedRequest(
            Interlocked.Increment(ref sequence),
            DateTimeOffset.UtcNow,
            request.Method,
            request.Path.Value ?? string.Empty,
            request.QueryString.Value ?? string.Empty,
            CaptureHeaders(request),
            body,
            truncated);

        lock (sync)
        {
            var node = captures.AddLast(captured);
            capturesBySequence[captured.Sequence] = node;
            while (captures.Count > FixtureLimits.MaximumCaptures)
            {
                var oldest = captures.First!;
                captures.RemoveFirst();
                capturesBySequence.Remove(oldest.Value.Sequence);
            }
        }

        return captured.Sequence;
    }

    public void MarkCancelled(long captureSequence)
    {
        lock (sync)
        {
            if (capturesBySequence.TryGetValue(captureSequence, out var capture))
            {
                capture.Value.CancellationObserved = true;
            }
        }
    }

    public void Complete(long captureSequence, int responseStatusCode)
    {
        lock (sync)
        {
            if (capturesBySequence.TryGetValue(captureSequence, out var capture))
            {
                capture.Value.ResponseStatusCode = responseStatusCode;
                capture.Value.CompletedAtUtc = DateTimeOffset.UtcNow;
            }
        }
    }

    public RequestCaptureSnapshot GetSnapshot()
    {
        lock (sync)
        {
            var requests = captures.Select(capture => capture.ToSnapshot()).ToArray();
            return new RequestCaptureSnapshot(
                FixtureLimits.MaximumCaptures,
                requests.Length,
                requests);
        }
    }

    public int Reset()
    {
        lock (sync)
        {
            var removedCount = captures.Count;
            captures.Clear();
            capturesBySequence.Clear();
            return removedCount;
        }
    }

    private static async ValueTask<(string Body, bool Truncated)> ReadBodyAsync(
        HttpRequest request,
        CancellationToken cancellationToken)
    {
        if (request.ContentLength is 0 || !request.Body.CanRead)
        {
            return (string.Empty, false);
        }

        request.EnableBuffering(
            FixtureLimits.MaximumCapturedBodyBytes,
            FixtureLimits.MaximumRequestBodyBytes);
        var buffer = new byte[FixtureLimits.MaximumCapturedBodyBytes + 1];
        var totalRead = 0;
        while (totalRead < buffer.Length)
        {
            var bytesRead = await request.Body.ReadAsync(
                buffer.AsMemory(totalRead, buffer.Length - totalRead),
                cancellationToken);
            if (bytesRead == 0)
            {
                break;
            }

            totalRead += bytesRead;
        }

        request.Body.Position = 0;
        var capturedLength = Math.Min(totalRead, FixtureLimits.MaximumCapturedBodyBytes);
        var truncated = totalRead > FixtureLimits.MaximumCapturedBodyBytes ||
            request.ContentLength > FixtureLimits.MaximumCapturedBodyBytes;
        return (Encoding.UTF8.GetString(buffer, 0, capturedLength), truncated);
    }

    private static CapturedRequestHeaders CaptureHeaders(HttpRequest request)
    {
        var names = request.Headers.Keys
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var safeValues = request.Headers
            .Where(header => SafeValueHeaderNames.Contains(header.Key))
            .ToDictionary(
                header => header.Key,
                header => (IReadOnlyList<string>)header.Value
                    .Where(value => value is not null)
                    .Select(value => value!)
                    .ToArray(),
                StringComparer.OrdinalIgnoreCase);
        var authorizationPresent = request.Headers.TryGetValue("Authorization", out var authorization) &&
            authorization.Count > 0;
        return new CapturedRequestHeaders(
            names,
            safeValues,
            authorizationPresent,
            authorizationPresent ? ReadAuthorizationScheme(authorization[0]) : null,
            request.Headers.ContainsKey("Cookie"));
    }

    private static string? ReadAuthorizationScheme(string? authorization)
    {
        if (string.IsNullOrWhiteSpace(authorization))
        {
            return null;
        }

        var separatorIndex = authorization.IndexOf(' ');
        var scheme = separatorIndex < 0 ? authorization : authorization[..separatorIndex];
        return scheme.Length is > 0 and <= 32 && scheme.All(char.IsAsciiLetter)
            ? scheme
            : null;
    }

    private sealed class MutableCapturedRequest(
        long sequence,
        DateTimeOffset receivedAtUtc,
        string method,
        string path,
        string queryString,
        CapturedRequestHeaders headers,
        string body,
        bool bodyTruncated)
    {
        public long Sequence { get; } = sequence;
        public DateTimeOffset ReceivedAtUtc { get; } = receivedAtUtc;
        public string Method { get; } = method;
        public string Path { get; } = path;
        public string QueryString { get; } = queryString;
        public CapturedRequestHeaders Headers { get; } = headers;
        public string Body { get; } = body;
        public bool BodyTruncated { get; } = bodyTruncated;
        public bool CancellationObserved { get; set; }
        public int? ResponseStatusCode { get; set; }
        public DateTimeOffset? CompletedAtUtc { get; set; }

        public CapturedRequest ToSnapshot() => new(
            Sequence,
            ReceivedAtUtc,
            Method,
            Path,
            QueryString,
            Headers,
            Body,
            BodyTruncated,
            CancellationObserved,
            ResponseStatusCode,
            CompletedAtUtc);
    }
}

internal sealed class RequestCaptureMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, RequestCaptureStore store)
    {
        if (context.Request.Path.StartsWithSegments("/_test") ||
            context.Request.Path.Equals("/health"))
        {
            await next(context);
            return;
        }

        var captureSequence = await store.AddAsync(context.Request, context.RequestAborted);
        using var cancellationRegistration = context.RequestAborted.Register(
            () => store.MarkCancelled(captureSequence));
        try
        {
            await next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            store.MarkCancelled(captureSequence);
        }
        finally
        {
            store.Complete(captureSequence, context.Response.StatusCode);
        }
    }
}
