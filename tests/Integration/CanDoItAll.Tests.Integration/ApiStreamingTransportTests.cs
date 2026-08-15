using System.Text;
using CanDoItAll.AgentFramework.Llm.Abstractions;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.LlmChats.Common;
using CanDoItAll.Modules.LlmChats.Operations;
using CanDoItAll.Modules.Workspace.ApiAccess;
using CanDoItAll.Web.Api;
using CanDoItAll.Web.Api.Streaming;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Tests.Integration.Api;

public sealed class ApiStreamingTransportTests
{
    [Fact]
    public async Task ReadAsync_reports_gap_and_replays_only_the_bounded_window()
    {
        var stream = CreateStream(replayCapacity: 3, maxBatchSize: 2);
        stream.Publish("one");
        stream.Publish("two");
        stream.Publish("three");
        stream.Publish("four");

        var first = await stream.ReadAsync(0, CancellationToken.None);

        var gap = Assert.IsType<ReplayGap>(first.Gap);
        Assert.Equal(ReplayGapReason.CursorBeforeRetention, gap.Reason);
        Assert.Equal(2, gap.FirstAvailableSequence);
        Assert.Equal(4, gap.LastAvailableSequence);
        Assert.Equal(1, gap.ResumeAfterSequence);
        Assert.Collection(
            first.Events,
            item =>
            {
                Assert.Equal(2, item.Sequence);
                Assert.Equal("two", item.Value);
            },
            item =>
            {
                Assert.Equal(3, item.Sequence);
                Assert.Equal("three", item.Value);
            });

        var second = await stream.ReadAsync(3, CancellationToken.None);

        Assert.Null(second.Gap);
        var last = Assert.Single(second.Events);
        Assert.Equal(4, last.Sequence);
        Assert.Equal("four", last.Value);
    }

    [Fact]
    public async Task ReadAsync_wakes_all_waiters_after_publication()
    {
        var stream = CreateStream();
        var firstRead = stream.ReadAsync(0, CancellationToken.None).AsTask();
        var secondRead = stream.ReadAsync(0, CancellationToken.None).AsTask();

        Assert.False(firstRead.IsCompleted);
        Assert.False(secondRead.IsCompleted);

        var sequence = stream.Publish("published");
        var results = await Task.WhenAll(firstRead, secondRead);

        Assert.Equal(1, sequence);
        Assert.All(results, result =>
        {
            var item = Assert.Single(result.Events);
            Assert.Equal(sequence, item.Sequence);
            Assert.Equal("published", item.Value);
        });
    }

    [Fact]
    public async Task ReadAsync_honors_subscriber_cancellation_without_affecting_other_readers()
    {
        var stream = CreateStream();
        using var cancelledReader = new CancellationTokenSource();
        var cancelledRead = stream.ReadAsync(0, cancelledReader.Token).AsTask();
        var activeRead = stream.ReadAsync(0, CancellationToken.None).AsTask();

        cancelledReader.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelledRead);
        stream.Publish("still-active");
        var result = await activeRead;
        Assert.Equal("still-active", Assert.Single(result.Events).Value);
    }

    [Fact]
    public async Task ReadAsync_reports_a_restart_gap_for_a_future_cursor()
    {
        var stream = CreateStream();
        stream.Publish("current");

        var result = await stream.ReadAsync(42, CancellationToken.None);

        var gap = Assert.IsType<ReplayGap>(result.Gap);
        Assert.Equal(ReplayGapReason.CursorAheadOfStream, gap.Reason);
        Assert.Equal(42, gap.RequestedAfterSequence);
        Assert.Equal(1, gap.ResumeAfterSequence);
        Assert.Empty(result.Events);
    }

    [Fact]
    public void Cursor_rejects_conflicting_query_and_header_values()
    {
        var context = new DefaultHttpContext();
        context.Request.QueryString = new QueryString("?after=3");
        context.Request.Headers[ServerSentEventCursor.LastEventIdHeaderName] = "4";

        var succeeded = ServerSentEventCursor.TryResolve(
            context.Request,
            out var cursor,
            out var error);

        Assert.False(succeeded);
        Assert.Equal(0, cursor);
        Assert.Contains("must identify the same cursor", error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Writer_emits_valid_sse_framing_and_proxy_headers()
    {
        var context = new DefaultHttpContext();
        await using var body = new MemoryStream();
        context.Response.Body = body;
        ServerSentEventResponseWriter.Prepare(context.Response);

        await ServerSentEventResponseWriter.WriteEventAsync(
            context.Response,
            7,
            "test.changed",
            new TestPayload("ready"),
            CancellationToken.None);

        body.Position = 0;
        var frame = await new StreamReader(body, Encoding.UTF8).ReadToEndAsync(
            CancellationToken.None);
        Assert.Equal("text/event-stream; charset=utf-8", context.Response.ContentType);
        Assert.Equal("no", context.Response.Headers["X-Accel-Buffering"]);
        Assert.Equal(
            "id: 7\nevent: test.changed\ndata: {\"state\":\"ready\"}\n\n",
            frame);
    }

    [Fact]
    public async Task Writer_emits_api_only_event_without_mutating_replay_cursor()
    {
        var context = new DefaultHttpContext();
        await using var body = new MemoryStream();
        context.Response.Body = body;

        await ServerSentEventResponseWriter.WriteEventAsync(
            context.Response,
            "test.completed",
            new TestPayload("done"),
            CancellationToken.None);

        body.Position = 0;
        var frame = await new StreamReader(body, Encoding.UTF8).ReadToEndAsync(
            CancellationToken.None);
        Assert.Equal(
            "event: test.completed\ndata: {\"state\":\"done\"}\n\n",
            frame);
        Assert.DoesNotContain("id:", frame, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Profile_stream_rotates_replay_and_preserves_monotonic_cursor_on_switch()
    {
        var firstProfileId = Guid.NewGuid();
        var secondProfileId = Guid.NewGuid();
        var runtimeState = new MutableDatabaseRuntimeState(firstProfileId);
        var notifications = new DatabaseSwitchNotificationService();
        using var stream = new ProfileBoundedReplayEventStream<string>(
            runtimeState,
            notifications,
            Options.Create(new ApiAccessOptions
            {
                ServerSentEvents = new ApiServerSentEventsOptions
                {
                    ReplayCapacity = 8,
                    MaxBatchSize = 4,
                    HeartbeatIntervalSeconds = 1
                }
            }),
            NullLogger<ProfileBoundedReplayEventStream<string>>.Instance);
        var firstLease = stream.OpenCurrent();
        var firstSequence = stream.Publish("profile-a");

        runtimeState.Switch(secondProfileId, generation: 1);
        notifications.Publish(new DatabaseProfileChangedNotification(
            firstProfileId,
            "profile-a",
            secondProfileId,
            "profile-b",
            Generation: 1));

        Assert.True(firstLease.ProfileLifetime.IsCancellationRequested);
        var secondLease = stream.OpenCurrent();
        var secondSequence = stream.Publish("profile-b");
        var replay = await secondLease.Reader.ReadAsync(
            firstSequence,
            CancellationToken.None);

        Assert.Equal(secondProfileId, secondLease.ProfileId);
        Assert.True(secondSequence > firstSequence);
        Assert.Null(replay.Gap);
        var entry = Assert.Single(replay.Events);
        Assert.Equal(secondSequence, entry.Sequence);
        Assert.Equal("profile-b", entry.Value);
    }

    [Fact]
    public void Profile_stream_rotates_fail_closed_when_runtime_snapshot_advances_first()
    {
        var firstProfileId = Guid.NewGuid();
        var runtimeState = new MutableDatabaseRuntimeState(firstProfileId);
        using var stream = new ProfileBoundedReplayEventStream<string>(
            runtimeState,
            new DatabaseSwitchNotificationService(),
            Options.Create(new ApiAccessOptions()),
            NullLogger<ProfileBoundedReplayEventStream<string>>.Instance);
        var firstLease = stream.OpenCurrent();
        stream.Publish("profile-a");

        var secondProfileId = Guid.NewGuid();
        runtimeState.Switch(secondProfileId, generation: 1);
        var secondLease = stream.OpenCurrent();

        Assert.True(firstLease.ProfileLifetime.IsCancellationRequested);
        Assert.Equal(secondProfileId, secondLease.ProfileId);
        Assert.Equal(1, secondLease.Generation);
    }

    [Fact]
    public void Profile_stream_ignores_stale_out_of_order_switch_notifications()
    {
        var firstProfileId = Guid.NewGuid();
        var secondProfileId = Guid.NewGuid();
        var thirdProfileId = Guid.NewGuid();
        var runtimeState = new MutableDatabaseRuntimeState(firstProfileId);
        var notifications = new DatabaseSwitchNotificationService();
        using var stream = new ProfileBoundedReplayEventStream<string>(
            runtimeState,
            notifications,
            Options.Create(new ApiAccessOptions()),
            NullLogger<ProfileBoundedReplayEventStream<string>>.Instance);
        stream.OpenCurrent();

        runtimeState.Switch(secondProfileId, generation: 1);
        notifications.Publish(new DatabaseProfileChangedNotification(
            firstProfileId,
            "profile-a",
            secondProfileId,
            "profile-b",
            Generation: 1));
        runtimeState.Switch(thirdProfileId, generation: 2);
        notifications.Publish(new DatabaseProfileChangedNotification(
            secondProfileId,
            "profile-b",
            thirdProfileId,
            "profile-c",
            Generation: 2));
        var currentLease = stream.OpenCurrent();

        notifications.Publish(new DatabaseProfileChangedNotification(
            firstProfileId,
            "profile-a",
            secondProfileId,
            "profile-b",
            Generation: 1));

        var leaseAfterStaleNotification = stream.OpenCurrent();
        Assert.False(currentLease.ProfileLifetime.IsCancellationRequested);
        Assert.Equal(thirdProfileId, leaseAfterStaleNotification.ProfileId);
        Assert.Equal(2, leaseAfterStaleNotification.Generation);
    }

    [Fact]
    public async Task Streaming_writer_emits_heartbeats_and_treats_disconnect_as_normal_completion()
    {
        var context = new DefaultHttpContext();
        await using var body = new MemoryStream();
        using var disconnected = new CancellationTokenSource(TimeSpan.FromMilliseconds(150));
        context.Response.Body = body;
        context.RequestAborted = disconnected.Token;
        var stream = new BoundedReplayEventStream<string>(
            replayCapacity: 8,
            maxBatchSize: 4,
            heartbeatInterval: TimeSpan.FromMilliseconds(10));

        await ServerSentEventResponseWriter.WriteAsync(
            context,
            stream,
            "test.changed",
            _ => true);

        body.Position = 0;
        var output = await new StreamReader(body, Encoding.UTF8).ReadToEndAsync(
            CancellationToken.None);
        Assert.Contains(": heartbeat ", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Streaming_writer_treats_profile_switch_before_response_start_as_normal_completion()
    {
        var context = new DefaultHttpContext();
        await using var body = new MemoryStream();
        using var switchedProfile = new CancellationTokenSource();
        context.Response.Body = body;
        switchedProfile.Cancel();
        var stream = CreateStream();

        await ServerSentEventResponseWriter.WriteAsync(
            context,
            stream,
            "test.changed",
            _ => true,
            switchedProfile.Token);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
    }

    [Fact]
    public async Task Streaming_writer_finishes_an_in_progress_frame_before_profile_switch_closes_the_response()
    {
        var context = new DefaultHttpContext();
        await using var body = new BlockingFirstWriteStream();
        using var switchedProfile = new CancellationTokenSource();
        context.Response.Body = body;
        var stream = CreateStream();
        stream.Publish("visible");

        var writing = ServerSentEventResponseWriter.WriteAsync(
            context,
            stream,
            "test.changed",
            _ => true,
            switchedProfile.Token);
        await body.WaitForFirstWriteAsync();

        switchedProfile.Cancel();
        body.ReleaseWrite();
        await writing.WaitAsync(TimeSpan.FromSeconds(10));

        body.Position = 0;
        var output = await new StreamReader(body, Encoding.UTF8).ReadToEndAsync();
        Assert.Contains("event: test.changed", output, StringComparison.Ordinal);
        Assert.Contains("data: \"visible\"\n\n", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Streaming_writer_drains_an_in_progress_read_before_profile_switch_releases_the_request_scope()
    {
        var context = new DefaultHttpContext();
        await using var body = new MemoryStream();
        using var switchedProfile = new CancellationTokenSource();
        context.Response.Body = body;
        var stream = new DelayedCancellationReader();

        var writing = ServerSentEventResponseWriter.WriteAsync(
            context,
            stream,
            "test.changed",
            _ => true,
            switchedProfile.Token);
        await stream.WaitForReadAsync();

        switchedProfile.Cancel();
        await stream.WaitForCancellationAsync();
        Assert.False(writing.IsCompleted);

        stream.ReleaseRead();
        await writing.WaitAsync(TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task Streaming_writer_emits_typed_public_envelopes_and_closes_at_terminal_event()
    {
        var now = DateTimeOffset.UtcNow;
        var operation = new LlmChatOperation(
            LlmChatOperationId.New(),
            LlmChatConversationId.New(),
            LlmChatOperationKind.SendTurn,
            new LlmChatRequestFingerprint(new string('a', 64)),
            0,
            LlmChatOperationStatus.Succeeded,
            now,
            0) with
        {
            CompletedAtUtc = now
        };
        var stream = new BoundedReplayEventStream<LlmChatOperationEventApiResponse>(
            replayCapacity: 8,
            maxBatchSize: 4,
            heartbeatInterval: TimeSpan.FromSeconds(1));
        stream.Publish(LlmChatOperationEventApiMapper.ToResponse(
            operation,
            new LlmChatOperationTextDeltaEvent(operation.Id, 1, 1, "visible", now),
            aggregateCharacterCount: 7));
        stream.Publish(LlmChatOperationEventApiMapper.ToResponse(
            operation,
            new LlmChatOperationStateChangedEvent(
                operation.Id,
                2,
                LlmChatOperationStatus.Succeeded,
                now,
                "",
                "model",
                new LlmUsage(1, 1)),
            aggregateCharacterCount: 7));
        stream.Publish(LlmChatOperationEventApiMapper.ToResponse(
            operation,
            new LlmChatOperationTextDeltaEvent(operation.Id, 3, 1, "unreachable", now),
            aggregateCharacterCount: 18));
        var context = new DefaultHttpContext();
        await using var body = new MemoryStream();
        context.Response.Body = body;

        await ServerSentEventResponseWriter.WriteAsync(
            context,
            stream,
            static item => item.EventKind,
            static item => item.IsTerminal,
            CancellationToken.None,
            LlmChatErrorCodes.StreamCursorInvalid);

        body.Position = 0;
        var output = await new StreamReader(body, Encoding.UTF8).ReadToEndAsync();
        Assert.Contains("event: llm.response.delta", output, StringComparison.Ordinal);
        Assert.Contains("event: llm.operation.succeeded", output, StringComparison.Ordinal);
        Assert.Contains("\"schema\":\"candoitall.llm-chat-operation-event.v1\"", output, StringComparison.Ordinal);
        Assert.DoesNotContain("\"eventName\"", output, StringComparison.Ordinal);
        Assert.DoesNotContain("\"isTerminal\"", output, StringComparison.Ordinal);
        Assert.DoesNotContain("unreachable", output, StringComparison.Ordinal);

        foreach (var terminalStatus in new[]
                 {
                     LlmChatOperationStatus.Succeeded,
                     LlmChatOperationStatus.Failed,
                     LlmChatOperationStatus.Cancelled,
                     LlmChatOperationStatus.RecoveryRequired
                 })
        {
            var terminal = LlmChatOperationEventApiMapper.ToResponse(
                operation,
                new LlmChatOperationStateChangedEvent(
                    operation.Id,
                    4,
                    terminalStatus,
                    now,
                    terminalStatus == LlmChatOperationStatus.Succeeded
                        ? ""
                        : LlmChatErrorCodes.OperationRecoveryRequired,
                    terminalStatus == LlmChatOperationStatus.Succeeded ? "model" : "",
                    terminalStatus == LlmChatOperationStatus.RecoveryRequired
                        ? null
                        : new LlmUsage(1, 1)),
                aggregateCharacterCount: 7);
            Assert.True(terminal.IsTerminal);
        }
    }

    [Fact]
    public async Task Command_cleanup_is_bounded_when_a_runtime_ignores_cancellation()
    {
        using var commandLifetime = new CancellationTokenSource();
        using var releaseCancellation = new ManualResetEventSlim();
        using var cancellationRegistration = commandLifetime.Token.Register(
            releaseCancellation.Wait);
        var completion = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var logger = new RecordingLogger();

        await ApiCommandTaskLifetime.CancelAndObserveAsync(
            commandLifetime,
            completion.Task,
            logger,
            Guid.NewGuid(),
            TimeSpan.FromMilliseconds(10));

        Assert.True(commandLifetime.IsCancellationRequested);
        const string accountKeySecret = "late-account-key-secret";
        const string awsAccessKeySecret = "late-aws-access-key-secret";
        const string passwordSecret = "late-password-secret";
        const string sasSignatureSecret = "late-sas-signature-secret";
        var expectedFailure = new InvalidOperationException(
            $"AccountKey={accountKeySecret}; AWSAccessKeyId={awsAccessKeySecret}; " +
            $"Pwd={passwordSecret}; sig={sasSignatureSecret}.");
        completion.SetException(expectedFailure);
        var observedFailure = await logger.Failure.Task.WaitAsync(
            TimeSpan.FromSeconds(1));

        Assert.Null(observedFailure.Exception);
        Assert.Contains(
            nameof(InvalidOperationException),
            observedFailure.Message,
            StringComparison.Ordinal);
        Assert.DoesNotContain("AccountKey", observedFailure.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("AWSAccessKeyId", observedFailure.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("Pwd=", observedFailure.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("sig=", observedFailure.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(accountKeySecret, observedFailure.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(awsAccessKeySecret, observedFailure.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(passwordSecret, observedFailure.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(sasSignatureSecret, observedFailure.Message, StringComparison.Ordinal);
        Assert.False(releaseCancellation.IsSet);
        releaseCancellation.Set();
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => completion.Task);
    }

    private static BoundedReplayEventStream<string> CreateStream(
        int replayCapacity = 8,
        int maxBatchSize = 4)
    {
        return new BoundedReplayEventStream<string>(
            replayCapacity,
            maxBatchSize,
            TimeSpan.FromSeconds(5));
    }

    private sealed record TestPayload(string State);

    private sealed class RecordingLogger : ILogger
    {
        public TaskCompletionSource<CapturedLog> Failure { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public IDisposable BeginScope<TState>(TState state)
            where TState : notnull
        {
            return NoopDisposable.Instance;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (logLevel >= LogLevel.Error)
            {
                Failure.TrySetResult(new CapturedLog(
                    formatter(state, exception),
                    exception));
            }
        }
    }

    private sealed record CapturedLog(string Message, Exception? Exception);

    private sealed class NoopDisposable : IDisposable
    {
        public static NoopDisposable Instance { get; } = new();

        public void Dispose()
        {
        }
    }

    private sealed class BlockingFirstWriteStream : MemoryStream
    {
        private readonly TaskCompletionSource firstWriteStarted = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource writeRelease = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private int writeStarted;

        public Task WaitForFirstWriteAsync()
            => firstWriteStarted.Task;

        public void ReleaseWrite()
            => writeRelease.TrySetResult();

        public override async ValueTask WriteAsync(
            ReadOnlyMemory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            if (Interlocked.Exchange(ref writeStarted, 1) == 0)
            {
                firstWriteStarted.TrySetResult();
                await writeRelease.Task.WaitAsync(cancellationToken);
            }

            await base.WriteAsync(buffer, cancellationToken);
        }
    }

    private sealed class DelayedCancellationReader : IBoundedReplayEventReader<string>
    {
        private readonly TaskCompletionSource readStarted = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource cancellationObserved = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource readRelease = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public TimeSpan HeartbeatInterval => TimeSpan.FromHours(1);

        public Task WaitForReadAsync()
            => readStarted.Task;

        public Task WaitForCancellationAsync()
            => cancellationObserved.Task;

        public void ReleaseRead()
            => readRelease.TrySetResult();

        public async ValueTask<BoundedReplayReadResult<string>> ReadAsync(
            long afterExclusive,
            CancellationToken cancellationToken)
        {
            readStarted.TrySetResult();
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                cancellationObserved.TrySetResult();
                await readRelease.Task;
                throw;
            }

            throw new InvalidOperationException("The controlled read must be cancelled.");
        }
    }

    private sealed class MutableDatabaseRuntimeState(Guid profileId) : IDatabaseRuntimeState
    {
        private DatabaseRuntimeSnapshot snapshot = new(
            profileId,
            "profile-a",
            Generation: 0);

        public DatabaseRuntimeSnapshot GetSnapshot()
        {
            return Volatile.Read(ref snapshot);
        }

        public void MarkCurrentProfile(ResolvedDatabaseProfile profile)
        {
            ArgumentNullException.ThrowIfNull(profile);
            Switch(profile.Profile.Id, GetSnapshot().Generation);
        }

        public void Switch(Guid nextProfileId, long generation)
        {
            Volatile.Write(
                ref snapshot,
                new DatabaseRuntimeSnapshot(
                    nextProfileId,
                    $"profile-{generation}",
                    generation));
        }
    }
}
