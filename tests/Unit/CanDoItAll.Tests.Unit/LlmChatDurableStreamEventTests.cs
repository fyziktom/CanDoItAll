using CanDoItAll.AgentFramework.Llm.Abstractions;
using CanDoItAll.Modules.LlmChats.Application;
using CanDoItAll.Modules.LlmChats.Common;
using CanDoItAll.Modules.LlmChats.Operations;
using CanDoItAll.Modules.LlmChats.Persistence;
using CanDoItAll.Modules.LlmChats.Ports;

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
        var pipeline = new LlmChatStreamingPipeline(journal, options, timeProvider);
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
        var journal = new LlmChatOperationEventJournal(
            operations,
            eventRepository,
            new InlineLlmChatUnitOfWork(),
            new NoopLlmChatOperationEventSignal(),
            operationScope,
            options,
            TimeProvider.System);
        var pipeline = new LlmChatStreamingPipeline(journal, options, TimeProvider.System);
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
        await Task.Delay(TimeSpan.FromMilliseconds(200));
        var page = await journal.ListAfterAsync(operationId, 0, 10);
        Assert.NotNull(page);
        var delta = Assert.Single(page.Events.OfType<LlmChatOperationTextDeltaEvent>());
        Assert.Equal("small", delta.Text);

        release.SetResult();
        var result = await consumeTask.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("small", result.ResponseText);
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
}
