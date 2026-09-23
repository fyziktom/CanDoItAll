using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using CanDoItAll.Modules.AgentFramework.ProviderManagement;
using CanDoItAll.SharedProviders.Abstractions;
using CanDoItAll.SharedProviders.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Tests.Integration;

public sealed class SharedProviderStreamingIntegrationTests(
    SharedProviderStreamingApiFixture fixture) :
    IClassFixture<SharedProviderStreamingApiFixture>
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(5);

    [Theory]
    [InlineData("response.completed", "completed", true)]
    [InlineData("response.failed", "failed", false)]
    [InlineData("response.incomplete", "incomplete", false)]
    [InlineData("response.completed", "in_progress", false)]
    public async Task Responses_terminal_event_completes_without_chat_done_marker(string type, string status, bool succeeds) {
        var upstreamStream = ChunkSequenceStream.FromUtf8(
            $"event: {type}\ndata: {{\"type\":\"{type}\",\"response\":{{\"id\":\"resp_terminal\",\"status\":\"{status}\",\"usage\":{{\"input_tokens\":7,\"output_tokens\":11}}}}}}\n\n");
        await using var dispatcher = DispatcherHarness.Create(_ => CreateSseResponse(upstreamStream));

        var (frames, completion) = await ReadStreamAsync(await dispatcher.DispatchAsync(SharedProviderRelayOperation.Responses));

        Assert.Equal(succeeds, completion.Failure is null);
        Assert.DoesNotContain(frames, frame => frame.IsDone);
        Assert.Equal(7, completion.Usage.InputTokens);
        Assert.Equal(11, completion.Usage.OutputTokens);
        Assert.Equal(SharedProviderRelayUsageCompleteness.Complete, completion.Usage.Completeness);
        Assert.Equal(succeeds ? 1 : 0, frames.Count);
        Assert.True(upstreamStream.IsDisposed);
    }

    [Fact]
    public Task ChatCompletions_FlushesFirstChunkBeforeStreamCompletes()
        => AssertFirstChunkBeforeCompletionAsync(
            SharedProviderRoutes.ChatCompletions,
            SharedProviderRelayOperation.ChatCompletions,
            new SharedProviderRelayStreamFrame(
                eventName: null,
                "{\"id\":\"chatcmpl-first\",\"choices\":[{\"delta\":{\"content\":\"first\"}}]}"));

    [Fact]
    public Task Responses_FlushesFirstChunkBeforeStreamCompletes()
        => AssertFirstChunkBeforeCompletionAsync(
            SharedProviderRoutes.Responses,
            SharedProviderRelayOperation.Responses,
            new SharedProviderRelayStreamFrame(
                "response.output_text.delta",
                "{\"type\":\"response.output_text.delta\",\"delta\":\"first\"}"));

    [Fact]
    public async Task StreamingResponse_PreservesEventOrderAndTerminalDone()
    {
        var frames = new[]
        {
            new SharedProviderRelayStreamFrame(
                "response.created",
                "{\"type\":\"response.created\",\"sequence_number\":0}"),
            new SharedProviderRelayStreamFrame(
                "response.output_text.delta",
                "{\"type\":\"response.output_text.delta\",\"sequence_number\":1,\"delta\":\"hello\"}"),
            new SharedProviderRelayStreamFrame(eventName: null, "[DONE]")
        };
        var relayStream = new CompletedRelayStream(
            frames,
            new SharedProviderRelayStreamCompletion(SharedProviderRelayUsage.Unavailable));
        fixture.Relay.ConfigureResult(new SharedProviderRelayDispatchResult.Streaming(relayStream));

        using var request = CreateJsonPost(SharedProviderRoutes.Responses);
        using var response = await fixture.Host.Client.SendAsync(request);
        string body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            "event: response.created\n" +
            "data: {\"type\":\"response.created\",\"sequence_number\":0}\n\n" +
            "event: response.output_text.delta\n" +
            "data: {\"type\":\"response.output_text.delta\",\"sequence_number\":1,\"delta\":\"hello\"}\n\n" +
            "data: [DONE]\n\n",
            body);
        Assert.Equal(1, CountOccurrences(body, "data: [DONE]"));
        Assert.True(relayStream.Disposed.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task Dispatcher_ReassemblesSplitUtf8WithoutCorruptingFrame()
    {
        const string expectedText = "Až😀B";
        byte[] upstreamBytes = Encoding.UTF8.GetBytes(
            "data: {\"id\":\"chatcmpl-utf8\",\"model\":\"upstream-model\",\"choices\":[{\"delta\":{\"content\":\"" +
            expectedText +
            "\"}}]}\n\ndata: [DONE]\n\n");
        byte[] scalar = Encoding.UTF8.GetBytes("😀");
        int scalarOffset = upstreamBytes.AsSpan().IndexOf(scalar);
        Assert.True(scalarOffset >= 0);
        var upstreamStream = new ChunkSequenceStream(
        [
            upstreamBytes.AsMemory(0, scalarOffset + 1),
            upstreamBytes.AsMemory(scalarOffset + 1, 1),
            upstreamBytes.AsMemory(scalarOffset + 2)
        ]);

        await using var dispatcher = DispatcherHarness.Create(
            _ => CreateSseResponse(upstreamStream));
        var result = await dispatcher.DispatchAsync(SharedProviderRelayOperation.ChatCompletions);
        var (frames, completion) = await ReadStreamAsync(result);

        Assert.Null(completion.Failure);
        Assert.Equal(2, frames.Count);
        using var document = JsonDocument.Parse(frames[0].Data);
        Assert.Equal(
            expectedText,
            document.RootElement
                .GetProperty("choices")[0]
                .GetProperty("delta")
                .GetProperty("content")
                .GetString());
        Assert.Equal(
            dispatcher.ModelId.Value,
            document.RootElement.GetProperty("model").GetString());
        Assert.True(frames[1].IsDone);
        Assert.True(upstreamStream.IsDisposed);
    }

    [Fact]
    public async Task Dispatcher_ExtractsTerminalUsage()
    {
        var upstreamStream = ChunkSequenceStream.FromUtf8(
            "data: {\"id\":\"chatcmpl-usage\",\"model\":\"upstream-model\",\"choices\":[],\"usage\":{\"prompt_tokens\":7,\"completion_tokens\":11}}\n\n" +
            "data: [DONE]\n\n");
        await using var dispatcher = DispatcherHarness.Create(
            _ => CreateSseResponse(upstreamStream));

        var result = await dispatcher.DispatchAsync(SharedProviderRelayOperation.ChatCompletions);
        var (frames, completion) = await ReadStreamAsync(result);

        Assert.True(frames[^1].IsDone);
        Assert.Null(completion.Failure);
        Assert.Equal(SharedProviderRelayUsageCompleteness.Complete, completion.Usage.Completeness);
        Assert.Equal(7, completion.Usage.InputTokens);
        Assert.Equal(11, completion.Usage.OutputTokens);
        Assert.Null(completion.Usage.ImageCount);
    }

    [Fact]
    public async Task Dispatcher_LeavesMissingUsageUnavailable()
    {
        var upstreamStream = ChunkSequenceStream.FromUtf8(
            "event: response.completed\n" +
            "data: {\"type\":\"response.completed\",\"response\":{\"id\":\"resp-no-usage\",\"status\":\"completed\",\"model\":\"upstream-model\"}}\n\n" +
            "data: [DONE]\n\n");
        await using var dispatcher = DispatcherHarness.Create(
            _ => CreateSseResponse(upstreamStream));

        var result = await dispatcher.DispatchAsync(SharedProviderRelayOperation.Responses);
        var (_, completion) = await ReadStreamAsync(result);

        Assert.Null(completion.Failure);
        Assert.Equal(SharedProviderRelayUsageCompleteness.Unavailable, completion.Usage.Completeness);
        Assert.Null(completion.Usage.InputTokens);
        Assert.Null(completion.Usage.OutputTokens);
        Assert.Null(completion.Usage.ImageCount);
    }

    [Fact]
    public async Task DownstreamCancellation_CancelsAndDisposesUpstreamStream()
    {
        var relayStream = new CancellationAwareRelayStream(new SharedProviderRelayStreamFrame(
            eventName: null,
            "{\"id\":\"chatcmpl-cancel\",\"choices\":[{\"delta\":{\"content\":\"first\"}}]}"));
        fixture.Relay.ConfigureResult(new SharedProviderRelayDispatchResult.Streaming(relayStream));
        using var requestCancellation = new CancellationTokenSource();
        using var request = CreateJsonPost(SharedProviderRoutes.ChatCompletions);
        HttpResponseMessage? response = null;
        Stream? body = null;
        try
        {
            response = await fixture.Host.Client.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                requestCancellation.Token);
            body = await response.Content.ReadAsStreamAsync();
            using var reader = new StreamReader(
                body,
                Encoding.UTF8,
                detectEncodingFromByteOrderMarks: false,
                leaveOpen: true);
            Assert.Equal(
                "data: {\"id\":\"chatcmpl-cancel\",\"choices\":[{\"delta\":{\"content\":\"first\"}}]}",
                await reader.ReadLineAsync().WaitAsync(TestTimeout));

            requestCancellation.Cancel();
            await body.DisposeAsync();
            body = null;
            response.Dispose();
            response = null;

            await relayStream.CancellationObserved.WaitAsync(TestTimeout);
            await relayStream.Disposed.WaitAsync(TestTimeout);
            var completion = await relayStream.Completion.WaitAsync(TestTimeout);
            Assert.Equal(SharedProviderFailureCategory.Cancelled, completion.Failure?.Category);
        }
        finally
        {
            requestCancellation.Cancel();
            relayStream.ForceCancel();
            if (body is not null)
            {
                await body.DisposeAsync();
            }

            response?.Dispose();
        }
    }

    [Fact]
    public async Task PostHeaderIdleTimeout_KeepsSseStatusAndFailsCompletion()
    {
        var upstreamStream = new PrefixThenTimeoutStream(Encoding.UTF8.GetBytes(
            "data: {\"id\":\"chatcmpl-before-idle\",\"model\":\"upstream-model\",\"choices\":[{\"delta\":{\"content\":\"first\"}}]}\n\n"));
        await using var dispatcher = DispatcherHarness.Create(
            _ => CreateSseResponse(upstreamStream));
        var capturedStream = new TaskCompletionSource<ISharedProviderRelayStream>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Relay.Configure(async (request, cancellationToken) =>
        {
            var result = await dispatcher.DispatchAsync(request.Operation, cancellationToken);
            if (result is SharedProviderRelayDispatchResult.Streaming streaming)
            {
                capturedStream.TrySetResult(streaming.Stream);
            }

            return result;
        });

        using var request = CreateJsonPost(SharedProviderRoutes.ChatCompletions);
        using var response = await fixture.Host.Client.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead);
        await using var bodyStream = await response.Content.ReadAsStreamAsync();
        using var reader = new StreamReader(bodyStream);
        string? body = await reader.ReadLineAsync().WaitAsync(TestTimeout);
        var readFailure = await Record.ExceptionAsync(() => reader.ReadToEndAsync().WaitAsync(TestTimeout));
        Assert.True(readFailure is IOException or HttpRequestException);
        Assert.NotNull(body);
        var relayStream = await capturedStream.Task.WaitAsync(TestTimeout);
        var completion = await relayStream.Completion.WaitAsync(TestTimeout);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/event-stream", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("chatcmpl-before-idle", body, StringComparison.Ordinal);
        Assert.DoesNotContain("\"error\"", body, StringComparison.Ordinal);
        Assert.DoesNotContain("[DONE]", body, StringComparison.Ordinal);
        Assert.Equal(SharedProviderFailureCategory.Timeout, completion.Failure?.Category);
        Assert.Equal("shared_provider_stream_idle_timeout", completion.Failure?.Code.Value);
        Assert.Equal(SharedProviderRelayUsageCompleteness.Unavailable, completion.Usage.Completeness);
        await upstreamStream.TimeoutObserved.WaitAsync(TestTimeout);
        Assert.True(upstreamStream.IsDisposed);
    }

    [Fact]
    public async Task PreHeaderTimeout_ReturnsTypedGatewayTimeout()
    {
        fixture.Relay.Configure(static (_, _) =>
            throw new OperationCanceledException("private upstream timeout detail"));

        using var request = CreateJsonPost(SharedProviderRoutes.Responses);
        using var response = await fixture.Host.Client.SendAsync(request);
        string body = await response.Content.ReadAsStringAsync();
        var envelope = JsonSerializer.Deserialize<SharedProviderOpenAiErrorEnvelope>(
            body,
            SharedProviderProtocolJson.Options);

        Assert.Equal(HttpStatusCode.GatewayTimeout, response.StatusCode);
        Assert.NotNull(envelope);
        Assert.Equal("shared_provider_relay_timeout", envelope.Error.Code);
        Assert.Equal(SharedProviderOpenAiConstants.TimeoutErrorType, envelope.Error.Type);
        Assert.DoesNotContain("private", body, StringComparison.OrdinalIgnoreCase);
        AssertInferenceSecurityHeaders(response);
    }

    [Fact]
    public async Task StreamingResponse_AppliesSafeHeadersWithoutForwardingPrivateUpstreamHeaders()
    {
        var upstreamStream = ChunkSequenceStream.FromUtf8("data: [DONE]\n\n");
        await using var dispatcher = DispatcherHarness.Create(_ =>
        {
            var response = CreateSseResponse(upstreamStream);
            Assert.True(response.Headers.TryAddWithoutValidation(
                "x-request-id",
                "safe-upstream-request-id"));
            Assert.True(response.Headers.TryAddWithoutValidation("retry-after", "3"));
            Assert.True(response.Headers.TryAddWithoutValidation(
                "server",
                "private-upstream-server/9.9"));
            Assert.True(response.Headers.TryAddWithoutValidation(
                "set-cookie",
                "private-session=secret"));
            Assert.True(response.Headers.TryAddWithoutValidation(
                "location",
                "http://10.0.0.7/private"));
            Assert.True(response.Headers.TryAddWithoutValidation(
                "www-authenticate",
                "Bearer realm=private"));
            Assert.True(response.Headers.TryAddWithoutValidation(
                "x-private-upstream",
                "private-value"));
            return response;
        });
        var dispatchResult = await dispatcher.DispatchAsync(SharedProviderRelayOperation.ChatCompletions);
        var streaming = Assert.IsType<SharedProviderRelayDispatchResult.Streaming>(dispatchResult);
        Assert.Equal("safe-upstream-request-id", streaming.Stream.Headers.UpstreamRequestId);
        Assert.Equal(TimeSpan.FromSeconds(3), streaming.Stream.Headers.RetryAfter);
        fixture.Relay.ConfigureResult(streaming);

        using var request = CreateJsonPost(SharedProviderRoutes.ChatCompletions);
        using var response = await fixture.Host.Client.SendAsync(request);
        string body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("data: [DONE]\n\n", body);
        AssertInferenceSecurityHeaders(response);
        Assert.True(response.Headers.CacheControl?.Private);
        Assert.True(response.Headers.CacheControl?.NoCache);
        Assert.Contains(response.Headers.Pragma, value => value.Name == "no-cache");
        Assert.Equal("no", Assert.Single(response.Headers.GetValues("X-Accel-Buffering")));
        Assert.Equal("text/event-stream", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("utf-8", response.Content.Headers.ContentType?.CharSet);
        Assert.False(response.Headers.Contains("set-cookie"));
        Assert.False(response.Headers.Contains("location"));
        Assert.False(response.Headers.Contains("www-authenticate"));
        Assert.False(response.Headers.Contains("x-private-upstream"));
        Assert.DoesNotContain(
            response.Headers.Server,
            value => value.ToString().Contains("private-upstream-server", StringComparison.Ordinal));
        string serializedHeaders = string.Join(
            '\n',
            response.Headers.SelectMany(header => header.Value)
                .Concat(response.Content.Headers.SelectMany(header => header.Value)));
        Assert.DoesNotContain("10.0.0.7", serializedHeaders, StringComparison.Ordinal);
        Assert.DoesNotContain("private-session", serializedHeaders, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Dispatcher_RejectsMalformedAndOversizedSseFramesBoundedly()
    {
        byte[] prefix = Encoding.UTF8.GetBytes("data: {\"content\":\"");
        byte[] suffix = Encoding.UTF8.GetBytes("\"}\n\n");
        byte[] malformed = new byte[prefix.Length + 2 + suffix.Length];
        prefix.CopyTo(malformed, 0);
        malformed[prefix.Length] = 0xc3;
        malformed[prefix.Length + 1] = 0x28;
        suffix.CopyTo(malformed, prefix.Length + 2);
        var malformedStream = new ChunkSequenceStream([malformed]);
        await AssertInvalidStreamAsync(malformedStream);

        string oversizedLine = "data: " +
            new string('x', SharedProviderRelayStreamFrame.MaximumDataCharacters + 512) +
            "\n\n";
        var oversizedStream = ChunkSequenceStream.FromUtf8(oversizedLine);
        await AssertInvalidStreamAsync(oversizedStream);
    }

    [Fact]
    public async Task Dispatcher_MidstreamFailureEmitsNoSyntheticDoneAndFailsCompletion()
    {
        var malformedStream = ChunkSequenceStream.FromUtf8(
            "data: {\"id\":\"chatcmpl-before-failure\",\"model\":\"upstream-model\",\"choices\":[{\"delta\":{\"content\":\"first\"}}]}\n\n" +
            "data: {malformed}\n\n" +
            "data: [DONE]\n\n");
        await using var dispatcher = DispatcherHarness.Create(
            _ => CreateSseResponse(malformedStream));

        var result = await dispatcher.DispatchAsync(SharedProviderRelayOperation.ChatCompletions);
        var (frames, completion) = await ReadStreamAsync(result);

        var frame = Assert.Single(frames);
        Assert.Contains("chatcmpl-before-failure", frame.Data, StringComparison.Ordinal);
        Assert.DoesNotContain(frames, candidate => candidate.IsDone);
        Assert.Equal(SharedProviderFailureCategory.UpstreamFailure, completion.Failure?.Category);
        Assert.Equal("shared_provider_upstream_stream_invalid", completion.Failure?.Code.Value);
        Assert.Equal(SharedProviderRelayUsageCompleteness.Unavailable, completion.Usage.Completeness);
        Assert.True(malformedStream.IsDisposed);

        var transportFailureStream = ChunkSequenceStream.FromUtf8ThenThrow(
            "data: {\"id\":\"chatcmpl-before-transport-failure\",\"model\":\"upstream-model\",\"choices\":[{\"delta\":{\"content\":\"first\"}}]}\n\n",
            new IOException("Deterministic upstream transport failure."));
        await using var transportDispatcher = DispatcherHarness.Create(
            _ => CreateSseResponse(transportFailureStream));

        var transportResult = await transportDispatcher.DispatchAsync(
            SharedProviderRelayOperation.ChatCompletions);
        var (transportFrames, transportCompletion) = await ReadStreamAsync(transportResult);

        var transportFrame = Assert.Single(transportFrames);
        Assert.Contains("chatcmpl-before-transport-failure", transportFrame.Data, StringComparison.Ordinal);
        Assert.DoesNotContain(transportFrames, candidate => candidate.IsDone);
        Assert.Equal(
            SharedProviderFailureCategory.UpstreamFailure,
            transportCompletion.Failure?.Category);
        Assert.Equal(
            "shared_provider_upstream_stream_failed",
            transportCompletion.Failure?.Code.Value);
        Assert.Equal(
            SharedProviderRelayUsageCompleteness.Unavailable,
            transportCompletion.Usage.Completeness);
        Assert.True(transportFailureStream.IsDisposed);
    }

    private async Task AssertFirstChunkBeforeCompletionAsync(
        string route,
        SharedProviderRelayOperation expectedOperation,
        SharedProviderRelayStreamFrame firstFrame)
    {
        var relayStream = new GatedRelayStream(
            firstFrame,
            [new SharedProviderRelayStreamFrame(eventName: null, "[DONE]")]);
        fixture.Relay.ConfigureResult(new SharedProviderRelayDispatchResult.Streaming(relayStream));
        using var request = CreateJsonPost(route);
        using var response = await fixture.Host.Client.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead);
        await using var body = await response.Content.ReadAsStreamAsync();
        using var reader = new StreamReader(
            body,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: false,
            leaveOpen: true);
        try
        {
            if (firstFrame.EventName is { } eventName)
            {
                Assert.Equal(
                    $"event: {eventName}",
                    await reader.ReadLineAsync().WaitAsync(TestTimeout));
            }

            Assert.Equal(
                $"data: {firstFrame.Data}",
                await reader.ReadLineAsync().WaitAsync(TestTimeout));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.False(relayStream.Completion.IsCompleted);
            Assert.Equal(expectedOperation, Assert.Single(fixture.Relay.Requests).Operation);

            relayStream.Release();
            string remainder = await reader.ReadToEndAsync().WaitAsync(TestTimeout);
            Assert.Contains("data: [DONE]", remainder, StringComparison.Ordinal);
            Assert.Null((await relayStream.Completion.WaitAsync(TestTimeout)).Failure);
            await relayStream.Disposed.WaitAsync(TestTimeout);
        }
        finally
        {
            relayStream.Release();
        }
    }

    private static async Task AssertInvalidStreamAsync(ChunkSequenceStream upstreamStream)
    {
        await using var dispatcher = DispatcherHarness.Create(
            _ => CreateSseResponse(upstreamStream));
        var result = await dispatcher.DispatchAsync(SharedProviderRelayOperation.ChatCompletions);
        var (frames, completion) = await ReadStreamAsync(result);

        Assert.Empty(frames);
        Assert.Equal(SharedProviderFailureCategory.UpstreamFailure, completion.Failure?.Category);
        Assert.Equal("shared_provider_upstream_stream_invalid", completion.Failure?.Code.Value);
        Assert.Equal(SharedProviderRelayUsageCompleteness.Unavailable, completion.Usage.Completeness);
        Assert.True(upstreamStream.IsDisposed);
    }

    private static async Task<(IReadOnlyList<SharedProviderRelayStreamFrame> Frames,
        SharedProviderRelayStreamCompletion Completion)> ReadStreamAsync(
        SharedProviderRelayDispatchResult result)
    {
        var streaming = Assert.IsType<SharedProviderRelayDispatchResult.Streaming>(result);
        await using var relayStream = streaming.Stream;
        var frames = new List<SharedProviderRelayStreamFrame>();
        await foreach (var frame in relayStream.ReadFramesAsync())
        {
            frames.Add(frame);
        }

        var completion = await relayStream.Completion.WaitAsync(TestTimeout);
        return (frames, completion);
    }

    private static HttpRequestMessage CreateJsonPost(string route)
        => new(HttpMethod.Post, route)
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json")
        };

    private static HttpResponseMessage CreateSseResponse(Stream stream)
    {
        var content = new StreamContent(stream);
        content.Headers.ContentType = new MediaTypeHeaderValue("text/event-stream");
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = content
        };
    }

    private static void AssertInferenceSecurityHeaders(HttpResponseMessage response)
    {
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.False(response.Headers.Contains("ETag"));
        Assert.Equal(
            "nosniff",
            Assert.Single(response.Headers.GetValues("X-Content-Type-Options")));
        Assert.False(string.IsNullOrWhiteSpace(
            Assert.Single(response.Headers.GetValues(SharedProviderHeaders.RequestId))));
    }

    private static int CountOccurrences(string value, string candidate)
    {
        var count = 0;
        var offset = 0;
        while ((offset = value.IndexOf(candidate, offset, StringComparison.Ordinal)) >= 0)
        {
            count++;
            offset += candidate.Length;
        }

        return count;
    }
}

public sealed class SharedProviderStreamingApiFixture : IAsyncLifetime
{
    private ApiTestHost? host;

    internal ApiTestHost Host
        => host ?? throw new InvalidOperationException("The streaming API host is not initialized.");

    internal ReplaceableRelayApplicationService Relay { get; } = new();

    public async Task InitializeAsync()
    {
        host = await ApiTestHost.CreateAsync(
            jwtEnabled: false,
            services =>
            {
                services.RemoveAll<ISharedProviderRelayApplicationService>();
                services.AddSingleton<ISharedProviderRelayApplicationService>(Relay);
            },
            useInMemoryDatabase: true);
    }

    public async Task DisposeAsync()
    {
        if (host is not null)
        {
            await host.DisposeAsync();
        }
    }
}

internal sealed class ReplaceableRelayApplicationService : ISharedProviderRelayApplicationService
{
    private Func<SharedProviderRelayApplicationRequest, CancellationToken,
        ValueTask<SharedProviderRelayDispatchResult>> handler = static (_, _) =>
            ValueTask.FromResult<SharedProviderRelayDispatchResult>(
                new SharedProviderRelayDispatchResult.Failed(new SharedProviderFailure(
                    SharedProviderFailureCategory.Unavailable,
                    new SharedProviderFailureCode("stream_fixture_not_configured"),
                    "The streaming test fixture is not configured.")));

    public ConcurrentQueue<SharedProviderRelayApplicationRequest> Requests { get; } = new();

    public void Configure(
        Func<SharedProviderRelayApplicationRequest, CancellationToken,
            ValueTask<SharedProviderRelayDispatchResult>> nextHandler)
    {
        ArgumentNullException.ThrowIfNull(nextHandler);
        while (Requests.TryDequeue(out _))
        {
        }

        Interlocked.Exchange(ref handler, nextHandler);
    }

    public void ConfigureResult(SharedProviderRelayDispatchResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        Configure((_, _) => ValueTask.FromResult(result));
    }

    public ValueTask<SharedProviderRelayDispatchResult> InvokeAsync(
        SharedProviderRelayApplicationRequest request,
        CancellationToken cancellationToken = default)
    {
        Requests.Enqueue(request);
        return Volatile.Read(ref handler)(request, cancellationToken);
    }
}

internal sealed class GatedRelayStream(
    SharedProviderRelayStreamFrame firstFrame,
    IReadOnlyList<SharedProviderRelayStreamFrame> remainingFrames) :
    ISharedProviderRelayStream
{
    private readonly TaskCompletionSource<bool> release = new(
        TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource<SharedProviderRelayStreamCompletion> completion = new(
        TaskCreationOptions.RunContinuationsAsynchronously);

    public SharedProviderRelayResponseHeaders Headers => SharedProviderRelayResponseHeaders.Empty;

    public Task<SharedProviderRelayStreamCompletion> Completion => completion.Task;

    private TaskCompletionSource<bool> DisposalSignal { get; } = new(
        TaskCreationOptions.RunContinuationsAsynchronously);

    public Task Disposed => DisposalSignal.Task;

    public void Release()
        => release.TrySetResult(true);

    public async IAsyncEnumerable<SharedProviderRelayStreamFrame> ReadFramesAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        yield return firstFrame;
        await release.Task.WaitAsync(cancellationToken);
        foreach (var frame in remainingFrames)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return frame;
        }

        completion.TrySetResult(new SharedProviderRelayStreamCompletion(
            SharedProviderRelayUsage.Unavailable));
    }

    public ValueTask DisposeAsync()
    {
        if (!completion.Task.IsCompleted)
        {
            completion.TrySetResult(CancelledCompletion());
        }

        DisposalSignal.TrySetResult(true);
        return ValueTask.CompletedTask;
    }

    private static SharedProviderRelayStreamCompletion CancelledCompletion()
        => new(
            SharedProviderRelayUsage.Unavailable,
            new SharedProviderFailure(
                SharedProviderFailureCategory.Cancelled,
                new SharedProviderFailureCode("stream_fixture_cancelled"),
                "The streaming test fixture was cancelled."));
}

internal sealed class CompletedRelayStream(
    IReadOnlyList<SharedProviderRelayStreamFrame> frames,
    SharedProviderRelayStreamCompletion completion) :
    ISharedProviderRelayStream
{
    public SharedProviderRelayResponseHeaders Headers => SharedProviderRelayResponseHeaders.Empty;

    public Task<SharedProviderRelayStreamCompletion> Completion { get; } = Task.FromResult(completion);

    private TaskCompletionSource<bool> DisposalSignal { get; } = new(
        TaskCreationOptions.RunContinuationsAsynchronously);

    public Task Disposed => DisposalSignal.Task;

    public async IAsyncEnumerable<SharedProviderRelayStreamFrame> ReadFramesAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (var frame in frames)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return frame;
        }

        await Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        DisposalSignal.TrySetResult(true);
        return ValueTask.CompletedTask;
    }
}

internal sealed class CancellationAwareRelayStream(
    SharedProviderRelayStreamFrame firstFrame) : ISharedProviderRelayStream
{
    private readonly TaskCompletionSource<SharedProviderRelayStreamCompletion> completion = new(
        TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly CancellationTokenSource forceCancellation = new();
    private int disposed;

    public SharedProviderRelayResponseHeaders Headers => SharedProviderRelayResponseHeaders.Empty;

    public Task<SharedProviderRelayStreamCompletion> Completion => completion.Task;

    private TaskCompletionSource<bool> CancellationSignal { get; } = new(
        TaskCreationOptions.RunContinuationsAsynchronously);

    private TaskCompletionSource<bool> DisposalSignal { get; } = new(
        TaskCreationOptions.RunContinuationsAsynchronously);

    public Task CancellationObserved => CancellationSignal.Task;

    public Task Disposed => DisposalSignal.Task;

    public void ForceCancel()
    {
        try
        {
            forceCancellation.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }
    }

    public async IAsyncEnumerable<SharedProviderRelayStreamFrame> ReadFramesAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        yield return firstFrame;
        await WaitForCancellationAsync(cancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0)
        {
            return ValueTask.CompletedTask;
        }

        forceCancellation.Cancel();
        completion.TrySetResult(CreateCancelledCompletion());
        DisposalSignal.TrySetResult(true);
        forceCancellation.Dispose();
        return ValueTask.CompletedTask;
    }

    private async Task WaitForCancellationAsync(CancellationToken cancellationToken)
    {
        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            forceCancellation.Token);
        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, linkedCancellation.Token);
        }
        catch (OperationCanceledException) when (linkedCancellation.IsCancellationRequested)
        {
            completion.TrySetResult(CreateCancelledCompletion());
            CancellationSignal.TrySetResult(true);
            throw;
        }
    }

    private static SharedProviderRelayStreamCompletion CreateCancelledCompletion()
        => new(
            SharedProviderRelayUsage.Unavailable,
            new SharedProviderFailure(
                SharedProviderFailureCategory.Cancelled,
                new SharedProviderFailureCode("shared_provider_request_cancelled"),
                "The shared-provider request was cancelled."));
}

internal sealed class DispatcherHarness : IAsyncDisposable
{
    private const string UpstreamModel = "upstream-model";

    private static readonly SharedProviderPublicationId PublicationId = new(
        Guid.Parse("ed256cb8-2a84-44ef-a16b-63e2a4623467"));

    private readonly DeterministicHttpMessageHandler handler;
    private readonly ServiceProvider provider;
    private readonly AsyncServiceScope scope;
    private readonly SharedProviderRelayAdapterDescriptor descriptor;
    private readonly TimeSpan timeout;
    private readonly ISharedProviderRelayDispatcher dispatcher;
    private readonly ISharedProviderRelayRequestPolicy requestPolicy;

    private DispatcherHarness(
        Func<HttpRequestMessage, HttpResponseMessage> responseFactory,
        TimeSpan timeout)
    {
        ArgumentNullException.ThrowIfNull(responseFactory);
        handler = new DeterministicHttpMessageHandler(
            (request, _) => Task.FromResult(responseFactory(request)));
        this.timeout = timeout;
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<ISharedProviderImageCapabilityRelay, EmptyImageCapabilityRelay>();
        services.AddSharedProviderHttpDescriptors();
        services.AddSingleton<IProviderInferenceRelayRuntime, DirectProviderInferenceRelayRuntime>();
        services.RemoveAll<IHttpClientFactory>();
        services.AddSingleton<IHttpClientFactory>(new DeterministicHttpClientFactory(handler));
        provider = services.BuildServiceProvider();
        scope = provider.CreateAsyncScope();
        dispatcher = scope.ServiceProvider.GetRequiredService<ISharedProviderRelayDispatcher>();
        requestPolicy = scope.ServiceProvider.GetRequiredService<ISharedProviderRelayRequestPolicy>();
        var catalog = scope.ServiceProvider.GetRequiredService<ISharedProviderRelaySupportCatalog>();
        descriptor = catalog.TryGet("provider.openai", SharedProviderPurpose.Chat, out var resolved)
            ? resolved
            : throw new InvalidOperationException("The production OpenAI chat relay descriptor is absent.");
        ModelId = SharedProviderRoutingModelIdCodec.Create(PublicationId, UpstreamModel);
    }

    public SharedProviderRoutingModelId ModelId { get; }

    public static DispatcherHarness Create(
        Func<HttpRequestMessage, HttpResponseMessage> responseFactory,
        TimeSpan? timeout = null)
        => new(
            responseFactory,
            timeout ?? TimeSpan.FromSeconds(5));

    public ValueTask<SharedProviderRelayDispatchResult> DispatchAsync(
        SharedProviderRelayOperation operation,
        CancellationToken cancellationToken = default,
        bool stream = true)
    {
        string payload = operation switch
        {
            SharedProviderRelayOperation.ChatCompletions =>
                $$"""{"model":"{{ModelId.Value}}","messages":[{"role":"user","content":"hello"}],"stream":{{(stream ? "true" : "false")}}}""",
            SharedProviderRelayOperation.Responses =>
                $$"""{"model":"{{ModelId.Value}}","input":"hello","stream":{{(stream ? "true" : "false")}}}""",
            _ => throw new ArgumentOutOfRangeException(nameof(operation))
        };
        var policyResult = requestPolicy.Normalize(
            operation,
            Encoding.UTF8.GetBytes(payload),
            descriptor.Support);
        var normalized = policyResult is SharedProviderRelayRequestPolicyResult.Accepted accepted
            ? accepted.Request
            : throw new InvalidOperationException("The deterministic relay request was rejected.");
        var target = new SharedProviderRelayTarget(
            PublicationId,
            Guid.Parse("de4fe287-6a87-4d75-a91f-7909c8cfd6f0"),
            descriptor.ConnectorPluginKey,
            descriptor.Purpose,
            new Uri("https://private-upstream.example.test/tenant/v1"),
            UpstreamModel,
            ModelId,
            timeout,
            "{}",
            new SharedProviderRelayCredential("private-test-secret"),
            descriptor.Support);
        return dispatcher.DispatchAsync(
            new SharedProviderRelayDispatchRequest(target, normalized),
            cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await scope.DisposeAsync();
        await provider.DisposeAsync();
        handler.Dispose();
    }
}

internal sealed class DeterministicHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
{
    public HttpClient CreateClient(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return new HttpClient(handler, disposeHandler: false)
        {
            Timeout = Timeout.InfiniteTimeSpan
        };
    }
}

internal sealed class DeterministicHttpMessageHandler(
    Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responseFactory) :
    HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
        => responseFactory(request, cancellationToken);
}

internal sealed class EmptyImageCapabilityRelay : ISharedProviderImageCapabilityRelay
{
    public ValueTask<IReadOnlyList<SharedProviderGeneratedImage>> GenerateAsync(
        SharedProviderImageCapabilityRequest request,
        CancellationToken cancellationToken = default)
        => ValueTask.FromResult<IReadOnlyList<SharedProviderGeneratedImage>>([]);
}

internal sealed class ChunkSequenceStream : Stream
{
    private readonly IReadOnlyList<byte[]> chunks;
    private readonly Exception? terminalException;
    private readonly Task? afterFirstChunk;
    private int chunkIndex;
    private int chunkOffset;

    public ChunkSequenceStream(
        IReadOnlyList<ReadOnlyMemory<byte>> chunks,
        Exception? terminalException = null,
        Task? afterFirstChunk = null)
    {
        ArgumentNullException.ThrowIfNull(chunks);
        if (chunks.Count == 0 || chunks.Any(chunk => chunk.IsEmpty))
        {
            throw new ArgumentException("At least one non-empty byte chunk is required.", nameof(chunks));
        }

        this.chunks = chunks.Select(chunk => chunk.ToArray()).ToArray();
        this.terminalException = terminalException;
        this.afterFirstChunk = afterFirstChunk;
    }

    public bool IsDisposed { get; private set; }

    public override bool CanRead => !IsDisposed;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public static ChunkSequenceStream FromUtf8(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new ChunkSequenceStream([Encoding.UTF8.GetBytes(value)]);
    }

    public static ChunkSequenceStream FromUtf8ThenThrow(string value, Exception terminalException)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(terminalException);
        return new ChunkSequenceStream(
            [Encoding.UTF8.GetBytes(value)],
            terminalException);
    }

    public override int Read(byte[] buffer, int offset, int count)
        => ReadCore(buffer.AsSpan(offset, count));

    public override async ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();
        if (chunkIndex > 0 && afterFirstChunk is not null) {
            await afterFirstChunk.WaitAsync(cancellationToken);
        }
        return ReadCore(buffer.Span);
    }

    public override Task<int> ReadAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken)
    {
        return ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
    }

    public override void Flush()
    {
    }

    public override long Seek(long offset, SeekOrigin origin)
        => throw new NotSupportedException();

    public override void SetLength(long value)
        => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count)
        => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        IsDisposed = true;
        base.Dispose(disposing);
    }

    private int ReadCore(Span<byte> destination)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        if (chunkIndex >= chunks.Count)
        {
            if (terminalException is not null)
            {
                throw terminalException;
            }

            return 0;
        }

        var chunk = chunks[chunkIndex];
        int copied = Math.Min(destination.Length, chunk.Length - chunkOffset);
        chunk.AsSpan(chunkOffset, copied).CopyTo(destination);
        chunkOffset += copied;
        if (chunkOffset == chunk.Length)
        {
            chunkIndex++;
            chunkOffset = 0;
        }

        return copied;
    }
}

internal sealed class PrefixThenTimeoutStream(byte[] prefix) : Stream
{
    private int offset;
    private readonly TaskCompletionSource<bool> timeoutObserved = new(
        TaskCreationOptions.RunContinuationsAsynchronously);

    public Task TimeoutObserved => timeoutObserved.Task;

    public bool IsDisposed { get; private set; }

    public override bool CanRead => !IsDisposed;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int bufferOffset, int count)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        if (offset >= prefix.Length)
        {
            throw new InvalidOperationException("The idle test stream must be consumed asynchronously.");
        }

        int copied = Math.Min(count, prefix.Length - offset);
        prefix.AsSpan(offset, copied).CopyTo(buffer.AsSpan(bufferOffset, copied));
        offset += copied;
        return copied;
    }

    public override async ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        cancellationToken.ThrowIfCancellationRequested();
        if (offset < prefix.Length)
        {
            int copied = Math.Min(buffer.Length, prefix.Length - offset);
            prefix.AsSpan(offset, copied).CopyTo(buffer.Span);
            offset += copied;
            return copied;
        }

        await Task.Yield();
        timeoutObserved.TrySetResult(true);
        throw new TimeoutException("The deterministic upstream stream became idle.");
    }

    public override async Task<int> ReadAsync(
        byte[] buffer,
        int bufferOffset,
        int count,
        CancellationToken cancellationToken)
        => await ReadAsync(
            buffer.AsMemory(bufferOffset, count),
            cancellationToken);

    public override void Flush()
    {
    }

    public override long Seek(long streamOffset, SeekOrigin origin)
        => throw new NotSupportedException();

    public override void SetLength(long value)
        => throw new NotSupportedException();

    public override void Write(byte[] buffer, int bufferOffset, int count)
        => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        IsDisposed = true;
        base.Dispose(disposing);
    }
}
