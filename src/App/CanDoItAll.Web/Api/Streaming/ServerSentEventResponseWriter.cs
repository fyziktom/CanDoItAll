using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.SharedKernel;
using CanDoItAll.Web.Api;
using Microsoft.AspNetCore.Http.Features;

namespace CanDoItAll.Web.Api.Streaming;

public static class ServerSentEventResponseWriter
{
    public const string ContentType = "text/event-stream";
    public const string GapEventName = "stream.gap";
    public const string InvalidCursorCode = "sse.cursor-invalid";

    private static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();
    private static readonly ReadOnlyMemory<byte> FrameTerminator = "\n\n"u8.ToArray();

    public static void Prepare(HttpResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);
        if (response.HasStarted)
        {
            throw new InvalidOperationException("The SSE response has already started.");
        }

        response.StatusCode = StatusCodes.Status200OK;
        response.ContentType = $"{ContentType}; charset=utf-8";
        response.Headers.CacheControl = "no-cache, no-store";
        response.Headers.Pragma = "no-cache";
        response.Headers["X-Accel-Buffering"] = "no";
        response.Headers["X-Content-Type-Options"] = "nosniff";
        response.HttpContext.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();
    }

    public static Task WriteAsync<T>(
        HttpContext context,
        IBoundedReplayEventReader<T> stream,
        string eventName,
        Func<T, bool> filter)
        => WriteAsync(
            context,
            stream,
            eventName,
            filter,
            CancellationToken.None);

    public static Task WriteAsync<T>(
        HttpContext context,
        IBoundedReplayEventReader<T> stream,
        string eventName,
        Func<T, bool> filter,
        CancellationToken streamLifetime)
    {
        ValidateEventName(eventName);
        return WriteCoreAsync(
            context,
            stream,
            filter,
            _ => eventName,
            static _ => false,
            streamLifetime,
            InvalidCursorCode);
    }

    public static Task WriteAsync<T>(
        HttpContext context,
        IBoundedReplayEventReader<T> stream,
        Func<T, string> eventNameSelector,
        Func<T, bool> terminalPredicate,
        CancellationToken streamLifetime,
        string invalidCursorCode)
        => WriteCoreAsync(
            context,
            stream,
            static _ => true,
            eventNameSelector,
            terminalPredicate,
            streamLifetime,
            invalidCursorCode);

    private static async Task WriteCoreAsync<T>(
        HttpContext context,
        IBoundedReplayEventReader<T> stream,
        Func<T, bool> filter,
        Func<T, string> eventNameSelector,
        Func<T, bool> terminalPredicate,
        CancellationToken streamLifetime,
        string invalidCursorCode)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(filter);
        ArgumentNullException.ThrowIfNull(eventNameSelector);
        ArgumentNullException.ThrowIfNull(terminalPredicate);
        ArgumentException.ThrowIfNullOrWhiteSpace(invalidCursorCode);

        if (!ServerSentEventCursor.TryResolve(
                context.Request,
                out var afterExclusive,
                out var cursorError))
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(
                new ApiErrorResponse(
                    [new ApiErrorItem(invalidCursorCode, cursorError!, ErrorSeverity.Error)]),
                context.RequestAborted);
            return;
        }

        Prepare(context.Response);
        using var lifetime = streamLifetime.CanBeCanceled
            ? CancellationTokenSource.CreateLinkedTokenSource(
                context.RequestAborted,
                streamLifetime)
            : null;
        var cancellationToken = lifetime?.Token ?? context.RequestAborted;
        try
        {
            await context.Response.StartAsync(cancellationToken);
            await context.Response.Body.FlushAsync(cancellationToken);
            using var heartbeatTimer = new PeriodicTimer(stream.HeartbeatInterval);
            Task<bool>? pendingHeartbeat = null;
            while (!cancellationToken.IsCancellationRequested)
            {
                BoundedReplayReadResult<T> result;
                var read = stream.ReadAsync(afterExclusive, cancellationToken);
                if (read.IsCompletedSuccessfully)
                {
                    result = read.Result;
                }
                else
                {
                    var pendingRead = read.AsTask();
                    while (!pendingRead.IsCompleted)
                    {
                        pendingHeartbeat ??= heartbeatTimer
                            .WaitForNextTickAsync(cancellationToken)
                            .AsTask();
                        if (await Task.WhenAny(pendingRead, pendingHeartbeat) == pendingRead)
                        {
                            break;
                        }

                        if (!await pendingHeartbeat)
                        {
                            return;
                        }

                        pendingHeartbeat = null;
                        await WriteHeartbeatAsync(context.Response, cancellationToken);
                    }

                    result = await pendingRead;
                }

                if (result.Gap is not null)
                {
                    await WriteGapAsync(context.Response, result.Gap, cancellationToken);
                    afterExclusive = result.Gap.ResumeAfterSequence;
                }

                foreach (var entry in result.Events)
                {
                    afterExclusive = entry.Sequence;
                    if (filter(entry.Value))
                    {
                        var eventName = eventNameSelector(entry.Value);
                        ValidateEventName(eventName);
                        await WriteEventAsync(
                            context.Response,
                            entry.Sequence,
                            eventName,
                            entry.Value,
                            cancellationToken);
                        if (terminalPredicate(entry.Value))
                        {
                            return;
                        }
                    }
                }

                if (result.IsCompleted)
                {
                    return;
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        finally
        {
            if (streamLifetime.IsCancellationRequested &&
                !context.RequestAborted.IsCancellationRequested &&
                context.Response.HasStarted)
            {
                await context.Response.CompleteAsync();
            }
        }
    }

    public static async Task WriteEventAsync<T>(
        HttpResponse response,
        long id,
        string eventName,
        T data,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentOutOfRangeException.ThrowIfNegative(id);
        ArgumentNullException.ThrowIfNull(data);
        ValidateEventName(eventName);

        await response.WriteAsync(
            string.Create(
                CultureInfo.InvariantCulture,
                $"id: {id}\n"),
            cancellationToken);
        await WriteEventBodyAsync(
            response,
            eventName,
            data,
            cancellationToken);
    }

    public static Task WriteEventAsync<T>(
        HttpResponse response,
        string eventName,
        T data,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentNullException.ThrowIfNull(data);
        ValidateEventName(eventName);

        return WriteEventBodyAsync(
            response,
            eventName,
            data,
            cancellationToken);
    }

    private static async Task WriteEventBodyAsync<T>(
        HttpResponse response,
        string eventName,
        T data,
        CancellationToken cancellationToken)
    {
        await response.WriteAsync(
            $"event: {eventName}\ndata: ",
            cancellationToken);
        await JsonSerializer.SerializeAsync(
            response.Body,
            data,
            SerializerOptions,
            cancellationToken);
        await response.Body.WriteAsync(FrameTerminator, cancellationToken);
        await response.Body.FlushAsync(cancellationToken);
    }

    public static async Task WriteHeartbeatAsync(
        HttpResponse response,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(response);

        await response.WriteAsync(
            string.Create(
                CultureInfo.InvariantCulture,
                $": heartbeat {DateTimeOffset.UtcNow:O}\n\n"),
            cancellationToken);
        await response.Body.FlushAsync(cancellationToken);
    }

    public static Task WriteGapAsync(
        HttpResponse response,
        ReplayGap gap,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(gap);
        return WriteEventAsync(
            response,
            gap.ResumeAfterSequence,
            GapEventName,
            gap,
            cancellationToken);
    }

    private static void ValidateEventName(string eventName)
    {
        if (string.IsNullOrWhiteSpace(eventName))
        {
            throw new ArgumentException("An SSE event name is required.", nameof(eventName));
        }

        if (eventName.Contains('\r', StringComparison.Ordinal) ||
            eventName.Contains('\n', StringComparison.Ordinal))
        {
            throw new ArgumentException("An SSE event name cannot contain a line break.", nameof(eventName));
        }
    }

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return options;
    }
}
