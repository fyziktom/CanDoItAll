using System.Runtime.CompilerServices;
using System.Text;
using CanDoItAll.SharedProviders.Abstractions;

namespace CanDoItAll.SharedProviders.Http;

internal sealed class SharedProviderSseRelayStream : ISharedProviderRelayStream
{
    private const int MaximumFrames = 100_000;

    private readonly IDisposable transportLifetime;
    private readonly CancellationTokenSource lifetimeSource;
    private readonly CancellationToken callerCancellationToken;
    private readonly SharedProviderRelayOperation operation;
    private readonly SharedProviderRoutingModelId publicModelId;
    private readonly TimeSpan idleTimeout;
    private readonly SharedProviderBoundedLineReader lineReader;
    private readonly TaskCompletionSource<SharedProviderRelayStreamCompletion> completion =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int enumerationStarted;
    private int disposed;

    public SharedProviderSseRelayStream(
        IDisposable transportLifetime,
        Stream responseStream,
        CancellationTokenSource lifetimeSource,
        CancellationToken callerCancellationToken,
        SharedProviderRelayOperation operation,
        SharedProviderRoutingModelId publicModelId,
        TimeSpan idleTimeout,
        SharedProviderRelayResponseHeaders headers)
    {
        ArgumentNullException.ThrowIfNull(transportLifetime);
        ArgumentNullException.ThrowIfNull(responseStream);
        ArgumentNullException.ThrowIfNull(lifetimeSource);
        ArgumentNullException.ThrowIfNull(headers);
        if (idleTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(idleTimeout));
        }

        this.transportLifetime = transportLifetime;
        this.lifetimeSource = lifetimeSource;
        this.callerCancellationToken = callerCancellationToken;
        this.operation = operation;
        this.publicModelId = publicModelId;
        this.idleTimeout = idleTimeout;
        lineReader = new SharedProviderBoundedLineReader(responseStream);
        Headers = headers;
    }

    public SharedProviderRelayResponseHeaders Headers { get; }

    public Task<SharedProviderRelayStreamCompletion> Completion => completion.Task;

    public async IAsyncEnumerable<SharedProviderRelayStreamFrame> ReadFramesAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref enumerationStarted, 1) != 0)
        {
            throw new InvalidOperationException("A shared-provider relay stream can be enumerated only once.");
        }

        var usage = SharedProviderRelayUsage.Unavailable;
        var eventName = default(string);
        var data = default(string);
        var frameCount = 0;
        try
        {
            while (true)
            {
                string? line;
                try
                {
                    line = await lineReader.ReadLineAsync(
                        idleTimeout,
                        lifetimeSource.Token,
                        cancellationToken).ConfigureAwait(false);
                }
                catch (TimeoutException)
                {
                    CompleteFailure(
                        usage,
                        SharedProviderFailureCategory.Timeout,
                        "shared_provider_stream_idle_timeout",
                        "The upstream provider stream became idle.");
                    yield break;
                }
                catch (OperationCanceledException)
                {
                    if (callerCancellationToken.IsCancellationRequested ||
                        cancellationToken.IsCancellationRequested)
                    {
                        CompleteFailure(
                            usage,
                            SharedProviderFailureCategory.Cancelled,
                            "shared_provider_request_cancelled",
                            "The shared-provider request was cancelled.");
                    }
                    else
                    {
                        CompleteFailure(
                            usage,
                            SharedProviderFailureCategory.Timeout,
                            "shared_provider_upstream_timeout",
                            "The upstream provider did not respond before the timeout.");
                    }

                    yield break;
                }
                catch (Exception exception) when (exception is DecoderFallbackException or InvalidDataException)
                {
                    CompleteInvalidStream(usage);
                    yield break;
                }
                catch (Exception exception) when (exception is IOException or HttpRequestException)
                {
                    CompleteFailure(
                        usage,
                        SharedProviderFailureCategory.UpstreamFailure,
                        "shared_provider_upstream_stream_failed",
                        "The upstream provider response stream failed.");
                    yield break;
                }

                if (line is null)
                {
                    CompleteInvalidStream(usage);
                    yield break;
                }

                if (line.Length == 0)
                {
                    if (data is null)
                    {
                        eventName = null;
                        continue;
                    }

                    SharedProviderRelayStreamFrame frame;
                    try
                    {
                        var rewritten = SharedProviderRelayResponsePolicy.RewriteServerSentEventData(
                            data,
                            publicModelId,
                            operation);
                        frame = new SharedProviderRelayStreamFrame(eventName, rewritten);
                    }
                    catch (Exception exception) when (exception is InvalidDataException or ArgumentException)
                    {
                        CompleteInvalidStream(usage);
                        yield break;
                    }

                    frameCount++;
                    if (frameCount > MaximumFrames)
                    {
                        CompleteInvalidStream(usage);
                        yield break;
                    }

                    var frameUsage = SharedProviderRelayUsageExtractor.ExtractServerSentEvents(operation, [frame]);
                    if (frameUsage.Completeness != SharedProviderRelayUsageCompleteness.Unavailable)
                    {
                        usage = frameUsage;
                    }

                    yield return frame;
                    eventName = null;
                    data = null;
                    if (frame.IsDone)
                    {
                        completion.TrySetResult(new SharedProviderRelayStreamCompletion(usage));
                        yield break;
                    }

                    continue;
                }

                if (line[0] == ':')
                {
                    continue;
                }

                var separator = line.IndexOf(':');
                var field = separator < 0 ? line : line[..separator];
                var value = separator < 0
                    ? string.Empty
                    : line[(separator + 1)..].TrimStart(' ');
                switch (field)
                {
                    case "event":
                        eventName = value;
                        break;
                    case "data" when data is null:
                        data = value;
                        break;
                    case "data":
                        CompleteInvalidStream(usage);
                        yield break;
                }
            }
        }
        finally
        {
            if (!completion.Task.IsCompleted)
            {
                CompleteFailure(
                    usage,
                    SharedProviderFailureCategory.Cancelled,
                    "shared_provider_stream_abandoned",
                    "The shared-provider response stream was not completed.");
            }

            await DisposeCoreAsync().ConfigureAwait(false);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (!completion.Task.IsCompleted)
        {
            CompleteFailure(
                SharedProviderRelayUsage.Unavailable,
                SharedProviderFailureCategory.Cancelled,
                "shared_provider_stream_abandoned",
                "The shared-provider response stream was not completed.");
        }

        await DisposeCoreAsync().ConfigureAwait(false);
    }

    private void CompleteInvalidStream(SharedProviderRelayUsage usage)
        => CompleteFailure(
            usage,
            SharedProviderFailureCategory.UpstreamFailure,
            "shared_provider_upstream_stream_invalid",
            "The upstream provider returned an invalid response stream.");

    private void CompleteFailure(
        SharedProviderRelayUsage usage,
        SharedProviderFailureCategory category,
        string code,
        string message)
        => completion.TrySetResult(new SharedProviderRelayStreamCompletion(
            usage,
            new SharedProviderFailure(
                category,
                new SharedProviderFailureCode(code),
                message)));

    private ValueTask DisposeCoreAsync()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0)
        {
            return ValueTask.CompletedTask;
        }

        lifetimeSource.Cancel();
        lineReader.Dispose();
        transportLifetime.Dispose();
        lifetimeSource.Dispose();
        return ValueTask.CompletedTask;
    }
}

internal sealed class SharedProviderBoundedLineReader : IDisposable
{
    private const int BufferLength = 4 * 1024;
    private const int MaximumLineCharacters = SharedProviderRelayStreamFrame.MaximumDataCharacters + 256;

    private readonly StreamReader reader;
    private readonly char[] buffer = new char[BufferLength];
    private int position;
    private int count;

    public SharedProviderBoundedLineReader(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        reader = new StreamReader(
            stream,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true),
            detectEncodingFromByteOrderMarks: false,
            bufferSize: BufferLength,
            leaveOpen: false);
    }

    public async ValueTask<string?> ReadLineAsync(
        TimeSpan idleTimeout,
        CancellationToken lifetimeToken,
        CancellationToken enumerationToken)
    {
        StringBuilder? builder = null;
        while (true)
        {
            if (position == count)
            {
                count = await ReadAsync(idleTimeout, lifetimeToken, enumerationToken).ConfigureAwait(false);
                position = 0;
                if (count == 0)
                {
                    return builder is null ? null : TrimCarriageReturn(builder.ToString());
                }
            }

            var newlineOffset = buffer.AsSpan(position, count - position).IndexOf('\n');
            var segmentLength = newlineOffset < 0 ? count - position : newlineOffset;
            builder ??= new StringBuilder(Math.Min(segmentLength, MaximumLineCharacters));
            if (builder.Length + segmentLength > MaximumLineCharacters)
            {
                throw new InvalidDataException("The upstream SSE line exceeds the relay limit.");
            }

            builder.Append(buffer, position, segmentLength);
            position += segmentLength;
            if (newlineOffset >= 0)
            {
                position++;
                return TrimCarriageReturn(builder.ToString());
            }
        }
    }

    public void Dispose()
        => reader.Dispose();

    private async ValueTask<int> ReadAsync(
        TimeSpan idleTimeout,
        CancellationToken lifetimeToken,
        CancellationToken enumerationToken)
    {
        using var idleSource = CancellationTokenSource.CreateLinkedTokenSource(
            lifetimeToken,
            enumerationToken);
        idleSource.CancelAfter(idleTimeout);
        try
        {
            return await reader.ReadAsync(buffer.AsMemory(), idleSource.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (
            !lifetimeToken.IsCancellationRequested &&
            !enumerationToken.IsCancellationRequested)
        {
            throw new TimeoutException("The upstream SSE read exceeded the idle timeout.");
        }
    }

    private static string TrimCarriageReturn(string line)
        => line.EndsWith('\r') ? line[..^1] : line;
}
