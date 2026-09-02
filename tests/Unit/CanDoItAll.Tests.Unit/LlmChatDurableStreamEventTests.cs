using CanDoItAll.AgentFramework.Llm.Abstractions;
using CanDoItAll.AgentFramework.Providers;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Application;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Common;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Operations;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Persistence;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Runtime;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Ports;

namespace CanDoItAll.Tests.Unit.LlmChats;

public sealed class LlmChatDurableStreamEventTests
{
    [Fact]
    public void Coalescer_combines_small_deltas_and_splits_utf8_without_breaking_runes()
    {
        var options = new LlmChatStreamingOptions
        {
            MinimumChunkBytes = 6,
            MaximumChunkBytes = 8,
            MaximumCoalescingDelay = TimeSpan.FromSeconds(1)
        };
        var coalescer = new LlmChatTextDeltaCoalescer(options);
        var now = new DateTimeOffset(2026, 8, 15, 12, 0, 0, TimeSpan.Zero);

        Assert.Empty(coalescer.Append("a", now));
        Assert.Empty(coalescer.Append("b", now.AddMilliseconds(10)));
        var chunks = coalescer.Append("😀c", now.AddMilliseconds(20));

        var chunk = Assert.Single(chunks);
        Assert.Equal("ab😀c", chunk);
        Assert.True(System.Text.Encoding.UTF8.GetByteCount(chunk) <= options.MaximumChunkBytes);
    }

    [Fact]
    public async Task Failed_stream_keeps_coalesced_partial_output_as_incomplete_noncanonical_evidence()
    {
        var now = new DateTimeOffset(2026, 8, 15, 12, 0, 0, TimeSpan.Zero);
        var timeProvider = new FixedTimeProvider(now);
        var operationId = LlmChatOperationId.New();
        var ownerId = LlmChatExecutionOwnerId.New();
        var executionLease = new LlmChatExecutionLeaseIdentity(operationId, ownerId, 1);
        var runtimeIdentity = new LlmChatRuntimeIdentity(Guid.NewGuid(), new string('a', 64), 1);
        var operations = new InMemoryLlmChatOperationRepository();
        operations.Seed(new LlmChatOperation(
            operationId,
            LlmChatConversationId.New(),
            LlmChatOperationKind.SendTurn,
            new LlmChatRequestFingerprint(new string('a', 64)),
            1,
            LlmChatOperationStatus.Running,
            now,
            0)
        {
            TurnAdmittedAtUtc = now,
            ProviderDispatchStartedAtUtc = now,
            ExecutionOwnerId = ownerId,
            ExecutionEpoch = executionLease.Epoch,
            ClaimedAtUtc = now,
            HeartbeatAtUtc = now,
            LeaseExpiresAtUtc = now.AddMinutes(1)
        });
        var unitOfWork = new InlineLlmChatUnitOfWork();
        var operationScope = new LlmChatOperationScopeAccessor();
        var eventRepository = new InMemoryLlmChatOperationEventRepository(operations);
        var options = new LlmChatStreamingOptions
        {
            MinimumChunkBytes = 32,
            MaximumChunkBytes = 64
        };
        var journal = new LlmChatOperationEventJournal(
            operations,
            eventRepository,
            unitOfWork,
            new NoopLlmChatOperationEventSignal(),
            operationScope,
            options,
            timeProvider);
        var pipeline = new LlmChatStreamingPipeline(
            journal,
            options,
            timeProvider,
            new LlmChatStreamingConsumerState());
        using var executionScope = operationScope.Push(new LlmChatOperationExecutionContext(
            operationId,
            runtimeIdentity)
        {
            ExecutionLease = executionLease
        });

        var exception = await Assert.ThrowsAsync<LlmChatConversationEngineException>(() => pipeline.ConsumeAsync(
            operationId,
            FailedUpdates(now)));
        Assert.Equal(LlmChatErrorCodes.ProviderUnavailable, exception.Code);
        var invocationRepository = new InMemoryLlmChatInvocationRecordRepository();
        var evidence = new LlmChatOperationEvidenceService(
            operations,
            invocationRepository,
            unitOfWork,
            operationScope,
            timeProvider,
            journal);
        await evidence.CompleteFailureAsync(
            operationId,
            now.AddSeconds(1),
            LlmChatErrorCodes.ProviderUnavailable);

        var page = await journal.ListAfterAsync(operationId, 0, 10);
        Assert.NotNull(page);
        Assert.Equal("partial output", Assert.Single(page.Events.OfType<LlmChatOperationTextDeltaEvent>()).Text);
        var terminal = Assert.Single(page.Events.OfType<LlmChatOperationStateChangedEvent>());
        Assert.Equal(LlmChatOperationStatus.Failed, terminal.Status);
        Assert.True(terminal.IsOutputIncomplete);
        Assert.Equal(LlmUsage.Zero, terminal.Usage);
    }

    [Fact]
    public void Event_failure_codes_are_redacted_to_stable_product_codes()
    {
        var operationEvent = new LlmChatOperationStateChangedEvent(
            LlmChatOperationId.New(),
            1,
            LlmChatOperationStatus.Failed,
            DateTimeOffset.UtcNow,
            "raw provider error with secret=credential",
            usage: LlmUsage.Zero);

        Assert.Equal(LlmChatErrorCodes.StorageCorrupted, operationEvent.FailureCode);
        Assert.DoesNotContain("credential", operationEvent.FailureCode, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Pipeline_flushes_a_small_delta_when_the_coalescing_window_expires()
    {
        var now = DateTimeOffset.UtcNow;
        var operationId = LlmChatOperationId.New();
        var ownerId = LlmChatExecutionOwnerId.New();
        var executionLease = new LlmChatExecutionLeaseIdentity(operationId, ownerId, 1);
        var operations = new InMemoryLlmChatOperationRepository();
        operations.Seed(new LlmChatOperation(
            operationId,
            LlmChatConversationId.New(),
            LlmChatOperationKind.SendTurn,
            new LlmChatRequestFingerprint(new string('a', 64)),
            1,
            LlmChatOperationStatus.Running,
            now,
            0)
        {
            ExecutionOwnerId = ownerId,
            ExecutionEpoch = executionLease.Epoch,
            ClaimedAtUtc = now,
            HeartbeatAtUtc = now,
            LeaseExpiresAtUtc = now.AddMinutes(1),
            TurnAdmittedAtUtc = now,
            ProviderDispatchStartedAtUtc = now
        });
        var operationScope = new LlmChatOperationScopeAccessor();
        var eventRepository = new InMemoryLlmChatOperationEventRepository(operations);
        var options = new LlmChatStreamingOptions
        {
            MinimumChunkBytes = 32,
            MaximumChunkBytes = 64,
            MaximumCoalescingDelay = TimeSpan.FromMilliseconds(25)
        };
        var signal = new LlmChatOperationEventSignal(TimeProvider.System);
        var journal = new LlmChatOperationEventJournal(
            operations,
            eventRepository,
            new InlineLlmChatUnitOfWork(),
            signal,
            operationScope,
            options,
            TimeProvider.System);
        var pipeline = new LlmChatStreamingPipeline(
            journal,
            options,
            TimeProvider.System,
            new LlmChatStreamingConsumerState());
        var waiting = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var executionScope = operationScope.Push(new LlmChatOperationExecutionContext(
            operationId,
            new LlmChatRuntimeIdentity(Guid.NewGuid(), new string('b', 64), 1))
        {
            ExecutionLease = executionLease
        });
        var consumeTask = pipeline.ConsumeAsync(operationId, PausedUpdates(waiting, release, now));

        await waiting.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await journal.WaitAsync(operationId, 0, TimeSpan.FromSeconds(5));
        var page = await journal.ListAfterAsync(operationId, 0, 10);
        Assert.NotNull(page);
        var delta = Assert.Single(page.Events.OfType<LlmChatOperationTextDeltaEvent>());
        Assert.Equal("small", delta.Text);

        release.SetResult();
        var result = await consumeTask.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("small", result.ResponseText);
    }

    [Fact]
    public async Task Pipeline_observes_an_in_flight_move_before_disposing_a_cancelled_stream()
    {
        var now = DateTimeOffset.UtcNow;
        var operationId = LlmChatOperationId.New();
        var ownerId = LlmChatExecutionOwnerId.New();
        var executionLease = new LlmChatExecutionLeaseIdentity(operationId, ownerId, 1);
        var operations = new InMemoryLlmChatOperationRepository();
        operations.Seed(new LlmChatOperation(
            operationId,
            LlmChatConversationId.New(),
            LlmChatOperationKind.SendTurn,
            new LlmChatRequestFingerprint(new string('a', 64)),
            1,
            LlmChatOperationStatus.Running,
            now,
            0)
        {
            ExecutionOwnerId = ownerId,
            ExecutionEpoch = executionLease.Epoch,
            ClaimedAtUtc = now,
            HeartbeatAtUtc = now,
            LeaseExpiresAtUtc = now.AddMinutes(1),
            TurnAdmittedAtUtc = now,
            ProviderDispatchStartedAtUtc = now
        });
        var operationScope = new LlmChatOperationScopeAccessor();
        var options = new LlmChatStreamingOptions
        {
            MinimumChunkBytes = 32,
            MaximumChunkBytes = 64,
            MaximumCoalescingDelay = TimeSpan.FromSeconds(10)
        };
        var journal = new LlmChatOperationEventJournal(
            operations,
            new InMemoryLlmChatOperationEventRepository(operations),
            new CancellationAwareUnitOfWork(),
            new NoopLlmChatOperationEventSignal(),
            operationScope,
            options,
            TimeProvider.System);
        var pipeline = new LlmChatStreamingPipeline(
            journal,
            options,
            TimeProvider.System,
            new LlmChatStreamingConsumerState());
        var stream = new DelayedCancellationStream();
        using var cancellation = new CancellationTokenSource();
        using var executionScope = operationScope.Push(new LlmChatOperationExecutionContext(
            operationId,
            new LlmChatRuntimeIdentity(Guid.NewGuid(), new string('b', 64), 1))
        {
            ExecutionLease = executionLease
        });
        var consumeTask = pipeline.ConsumeAsync(operationId, stream, cancellation.Token);
        await stream.SecondMoveStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await consumeTask.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.True(stream.IsDisposed);
        Assert.False(stream.DisposedWithMoveInFlight);
    }

    [Fact]
    public async Task Stream_limit_records_one_consistent_failure_across_all_evidence()
    {
        var now = new DateTimeOffset(2026, 8, 15, 12, 0, 0, TimeSpan.Zero);
        var timeProvider = new FixedTimeProvider(now);
        var provider = ProviderRuntimeTestData.CreateProvider();
        var operationId = LlmChatOperationId.New();
        var ownerId = LlmChatExecutionOwnerId.New();
        var executionLease = new LlmChatExecutionLeaseIdentity(operationId, ownerId, 1);
        var operations = new InMemoryLlmChatOperationRepository();
        operations.Seed(new LlmChatOperation(
            operationId,
            LlmChatConversationId.New(),
            LlmChatOperationKind.SendTurn,
            new LlmChatRequestFingerprint(new string('a', 64)),
            0,
            LlmChatOperationStatus.Running,
            now,
            0)
        {
            TurnAdmittedAtUtc = now,
            ExecutionOwnerId = ownerId,
            ExecutionEpoch = executionLease.Epoch,
            ClaimedAtUtc = now,
            HeartbeatAtUtc = now,
            LeaseExpiresAtUtc = now.AddMinutes(1)
        });
        var unitOfWork = new InlineLlmChatUnitOfWork();
        var operationScope = new LlmChatOperationScopeAccessor();
        var invocations = new InMemoryLlmChatInvocationRecordRepository();
        var eventRepository = new InMemoryLlmChatOperationEventRepository(operations);
        var options = new LlmChatStreamingOptions
        {
            MinimumChunkBytes = 1,
            MaximumChunkBytes = 8,
            MaximumResponseCharacters = 4,
            MaximumResponseBytes = 8
        };
        var journal = new LlmChatOperationEventJournal(
            operations,
            eventRepository,
            unitOfWork,
            new NoopLlmChatOperationEventSignal(),
            operationScope,
            options,
            timeProvider);
        var evidence = new LlmChatOperationEvidenceService(
            operations,
            invocations,
            unitOfWork,
            operationScope,
            timeProvider,
            journal);
        var consumerState = new LlmChatStreamingConsumerState();
        var audited = new AuditedLlmChatStreamingInvocationPort(
            new SequenceStreamingInvocationPort(
            [
                new LlmStreamingAttemptStarted(
                    1,
                    provider.Id,
                    provider.Kind,
                    "safe-model",
                    LlmStreamingDeliveryMode.Incremental,
                    now),
                new LlmStreamingTextDelta(1, "12345", 1),
                new LlmStreamingCompleted(
                    1,
                    "safe-model",
                    "stop",
                    LlmUsage.Zero,
                    LlmStreamingDeliveryMode.Incremental,
                    now.AddSeconds(1))
            ]),
            evidence,
            new ProviderModelCapabilityResolver(),
            operationScope,
            timeProvider,
            consumerState);
        var pipeline = new LlmChatStreamingPipeline(journal, options, timeProvider, consumerState);
        var request = new LlmInvocationRequest(
            provider,
            "safe-model",
            [new LlmMessage(LlmMessageRole.User, "hello")],
            correlationId: operationId.ToString());
        using var scope = operationScope.Push(new LlmChatOperationExecutionContext(
            operationId,
            new LlmChatRuntimeIdentity(Guid.NewGuid(), new string('b', 64), 1))
        {
            ExecutionLease = executionLease
        });

        var exception = await Assert.ThrowsAsync<LlmChatConversationEngineException>(() =>
            pipeline.ConsumeAsync(operationId, audited.StreamAsync(request)));
        Assert.Equal(LlmChatErrorCodes.StreamLimitExceeded, exception.Code);
        await evidence.CompleteFailureAsync(operationId, now.AddSeconds(2), exception.Code);

        var invocation = Assert.Single(await invocations.ListAsync(operationId));
        Assert.Equal(LlmChatInvocationOutcome.Failed, invocation.Outcome);
        Assert.Equal(LlmChatErrorCodes.StreamLimitExceeded, invocation.FailureCode);
        var page = await journal.ListAfterAsync(operationId, 0, 10);
        Assert.NotNull(page);
        var attempt = Assert.Single(page.Events.OfType<LlmChatOperationAttemptFinishedEvent>());
        Assert.Equal(LlmChatErrorCodes.StreamLimitExceeded, attempt.FailureCode);
        Assert.Equal("safe-model", attempt.Model);
        Assert.Equal(LlmStreamingDeliveryMode.Incremental, attempt.DeliveryMode);
        var terminal = Assert.Single(page.Events.OfType<LlmChatOperationStateChangedEvent>());
        Assert.Equal(LlmChatOperationStatus.Failed, terminal.Status);
        Assert.Equal(LlmChatErrorCodes.StreamLimitExceeded, terminal.FailureCode);
        Assert.Equal(
            LlmChatOperationStatus.Failed,
            (await operations.TryGetAsync(operationId))!.Status);
    }

    [Fact]
    public async Task Signal_state_evicts_many_completed_operations_without_lost_terminal_replay()
    {
        var now = new DateTimeOffset(2026, 8, 15, 12, 0, 0, TimeSpan.Zero);
        var timeProvider = new MutableEventTimeProvider(now);
        var lease = new MutableLlmChatRuntimeLease();
        var operationId = LlmChatOperationId.New();
        var operations = new InMemoryLlmChatOperationRepository();
        var operation = new LlmChatOperation(
            operationId,
            LlmChatConversationId.New(),
            LlmChatOperationKind.SendTurn,
            new LlmChatRequestFingerprint(new string('a', 64)),
            0,
            LlmChatOperationStatus.Succeeded,
            now,
            0)
        {
            CompletedAtUtc = now,
            ResultingTranscriptRevision = 1,
            AssistantEntryId = Guid.NewGuid()
        };
        operations.Seed(operation);
        var scope = new LlmChatOperationScopeAccessor();
        var events = new InMemoryLlmChatOperationEventRepository(operations);
        var signal = new LlmChatOperationEventSignal(timeProvider);
        var options = new LlmChatStreamingOptions();
        var journal = new LlmChatOperationEventJournal(
            operations,
            events,
            new InlineLlmChatUnitOfWork(),
            signal,
            scope,
            options,
            timeProvider);
        using (scope.Push(new LlmChatOperationExecutionContext(operationId, lease.Identity)))
        {
            await journal.AppendStateChangedAsync(operation, "model", LlmUsage.Zero);
        }

        foreach (var value in Enumerable.Range(1, 5_000))
        {
            signal.Publish(lease.Identity, new LlmChatOperationId(GuidFromInt32(value)), 1);
        }

        timeProvider.Advance(TimeSpan.FromMinutes(6));
        signal.Publish(lease.Identity, LlmChatOperationId.New(), 1);
        Assert.InRange(GetSignalStateCount(signal), 1, 4_096);

        var factory = new LlmChatOperationEventStreamSessionFactory(
            new TestLlmChatRuntimeLeaseFactory(lease),
            scope,
            journal,
            options);
        var opened = await factory.OpenAsync(operationId);
        Assert.True(opened.IsSuccess);
        await using var session = opened.Value!;
        var page = await session.ReadAsync(0, 10, TimeSpan.FromSeconds(1));
        Assert.True(page.Operation.IsTerminal);
        Assert.Single(page.Events);
    }

    [Fact]
    public async Task Event_session_disposal_releases_follower_lease_without_requesting_operation_cancellation()
    {
        var now = new DateTimeOffset(2026, 8, 16, 12, 0, 0, TimeSpan.Zero);
        var operationId = LlmChatOperationId.New();
        var operations = new InMemoryLlmChatOperationRepository();
        operations.Seed(new LlmChatOperation(
            operationId,
            LlmChatConversationId.New(),
            LlmChatOperationKind.SendTurn,
            new LlmChatRequestFingerprint(new string('a', 64)),
            0,
            LlmChatOperationStatus.Running,
            now,
            0));
        var lease = new MutableLlmChatRuntimeLease();
        var scope = new LlmChatOperationScopeAccessor();
        var options = new LlmChatStreamingOptions();
        var factory = new LlmChatOperationEventStreamSessionFactory(
            new TestLlmChatRuntimeLeaseFactory(lease),
            scope,
            LlmChatOperationEventTestFactory.Create(
                operations,
                new InlineLlmChatUnitOfWork(),
                scope,
                new FixedTimeProvider(now),
                options),
            options);

        var opened = await factory.OpenAsync(operationId);
        Assert.True(opened.IsSuccess);
        await opened.Value!.DisposeAsync();

        var operation = await operations.TryGetAsync(operationId);
        Assert.NotNull(operation);
        Assert.Equal(LlmChatOperationStatus.Running, operation.Status);
        Assert.Equal(0, operation.CancellationGeneration);
        Assert.Equal(1, lease.DisposeCount);
    }

    [Fact]
    public void Retention_schedule_evicts_old_profile_generations()
    {
        var schedule = new LlmChatOperationEventRetentionSchedule();
        var now = new DateTimeOffset(2026, 8, 15, 12, 0, 0, TimeSpan.Zero);
        var profileId = Guid.NewGuid();
        var previous = new LlmChatRuntimeIdentity(profileId, new string('a', 64), 1);
        var current = new LlmChatRuntimeIdentity(profileId, new string('b', 64), 2);
        var interval = TimeSpan.FromHours(1);
        Assert.True(schedule.TryAcquire(previous, now, interval));
        schedule.Complete(previous, now, interval, retryImmediately: false);

        Assert.True(schedule.TryAcquire(current, now.AddMinutes(1), interval));
        schedule.Complete(current, now.AddMinutes(1), interval, retryImmediately: false);

        Assert.True(schedule.TryAcquire(previous, now.AddMinutes(2), interval));
        schedule.Complete(previous, now.AddMinutes(2), interval, retryImmediately: false);
        foreach (var value in Enumerable.Range(1, 200))
        {
            var identity = new LlmChatRuntimeIdentity(
                GuidFromInt32(10_000 + value),
                new string('c', 64),
                1);
            Assert.True(schedule.TryAcquire(identity, now.AddMinutes(3), interval));
            schedule.Complete(identity, now.AddMinutes(3), interval, retryImmediately: false);
        }

        Assert.InRange(GetScheduleStateCount(schedule), 1, 128);
    }

    [Fact]
    public async Task Eviction_racing_wait_and_publish_remains_poll_correct()
    {
        var signal = new LlmChatOperationEventSignal(TimeProvider.System);
        var identity = new LlmChatRuntimeIdentity(Guid.NewGuid(), new string('a', 64), 1);
        var operationId = LlmChatOperationId.New();
        var waiting = signal.WaitAsync(identity, operationId, 0, TimeSpan.FromSeconds(5)).AsTask();

        var pressure = Task.Run(() =>
        {
            foreach (var value in Enumerable.Range(1, 5_000))
            {
                signal.Publish(identity, new LlmChatOperationId(GuidFromInt32(value)), 1);
            }
        });
        signal.Publish(identity, operationId, 1);

        await waiting.WaitAsync(TimeSpan.FromSeconds(5));
        await pressure.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.InRange(GetSignalStateCount(signal), 1, 4_096);
    }

    private static async IAsyncEnumerable<LlmStreamingUpdate> FailedUpdates(DateTimeOffset now)
    {
        await Task.Yield();
        yield return new LlmStreamingTextDelta(1, "partial ", 1);
        yield return new LlmStreamingTextDelta(1, "output", 2);
        yield return new LlmStreamingFailed(
            1,
            LlmInvocationFailureKind.ProviderFailure,
            LlmUsage.Zero,
            false,
            now.AddSeconds(1));
    }

    private static async IAsyncEnumerable<LlmStreamingUpdate> PausedUpdates(
        TaskCompletionSource waiting,
        TaskCompletionSource release,
        DateTimeOffset now)
    {
        yield return new LlmStreamingTextDelta(1, "small", 1);
        waiting.SetResult();
        await release.Task;
        yield return new LlmStreamingCompleted(
            1,
            "model",
            "stop",
            LlmUsage.Zero,
            LlmStreamingDeliveryMode.Incremental,
            now.AddSeconds(1));
    }

    private sealed class DelayedCancellationStream :
        IAsyncEnumerable<LlmStreamingUpdate>,
        IAsyncEnumerator<LlmStreamingUpdate>
    {
        private CancellationToken cancellationToken;
        private TaskCompletionSource<bool>? pendingMove;
        private CancellationTokenRegistration registration;
        private int moveCount;

        public TaskCompletionSource SecondMoveStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool IsDisposed { get; private set; }

        public bool DisposedWithMoveInFlight { get; private set; }

        public LlmStreamingUpdate Current { get; private set; } = null!;

        public IAsyncEnumerator<LlmStreamingUpdate> GetAsyncEnumerator(
            CancellationToken cancellationToken = default)
        {
            this.cancellationToken = cancellationToken;
            return this;
        }

        public ValueTask<bool> MoveNextAsync()
        {
            if (moveCount++ == 0)
            {
                Current = new LlmStreamingTextDelta(1, "small", 1);
                return ValueTask.FromResult(true);
            }

            pendingMove = new(TaskCreationOptions.RunContinuationsAsynchronously);
            registration = cancellationToken.Register(() => _ = CompleteCancellationAsync());
            SecondMoveStarted.TrySetResult();
            return new(pendingMove.Task);
        }

        public ValueTask DisposeAsync()
        {
            IsDisposed = true;
            registration.Dispose();
            if (pendingMove?.Task.IsCompleted == false)
            {
                DisposedWithMoveInFlight = true;
                throw new NotSupportedException("The stream cannot be disposed while MoveNext is active.");
            }

            return ValueTask.CompletedTask;
        }

        private async Task CompleteCancellationAsync()
        {
            await Task.Delay(TimeSpan.FromMilliseconds(50));
            pendingMove!.TrySetCanceled(cancellationToken);
        }
    }

    private sealed class CancellationAwareUnitOfWork : ILlmChatUnitOfWork
    {
        public Task<T> ExecuteAsync<T>(
            Func<CancellationToken, Task<T>> operation,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return operation(cancellationToken);
        }

        public void RegisterPostCommit(Action callback)
        {
            ArgumentNullException.ThrowIfNull(callback);
            callback();
        }
    }

    private static Guid GuidFromInt32(int value)
    {
        Span<byte> bytes = stackalloc byte[16];
        BitConverter.TryWriteBytes(bytes, value);
        return new Guid(bytes);
    }

    private static int GetSignalStateCount(LlmChatOperationEventSignal signal)
    {
        var field = typeof(LlmChatOperationEventSignal).GetField(
            "_states",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var states = field?.GetValue(signal)
            ?? throw new InvalidOperationException("Signal state storage was not found.");
        return (int)(states.GetType().GetProperty("Count")?.GetValue(states)
            ?? throw new InvalidOperationException("Signal state count was not found."));
    }

    private static int GetScheduleStateCount(LlmChatOperationEventRetentionSchedule schedule)
    {
        var field = typeof(LlmChatOperationEventRetentionSchedule).GetField(
            "_states",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var states = field?.GetValue(schedule)
            ?? throw new InvalidOperationException("Retention schedule storage was not found.");
        return (int)(states.GetType().GetProperty("Count")?.GetValue(states)
            ?? throw new InvalidOperationException("Retention schedule state count was not found."));
    }

    private sealed class MutableEventTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        private DateTimeOffset _utcNow = utcNow;

        public override DateTimeOffset GetUtcNow()
            => _utcNow;

        public void Advance(TimeSpan duration)
            => _utcNow += duration;
    }
}
