using CanDoItAll.AgentFramework.Llm.Abstractions;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Providers;
using CanDoItAll.Modules.LlmChats.Application;
using CanDoItAll.Modules.LlmChats.Common;
using CanDoItAll.Modules.LlmChats.Conversations;
using CanDoItAll.Modules.LlmChats.Operations;
using CanDoItAll.Modules.LlmChats.Persistence;
using CanDoItAll.Modules.LlmChats.Ports;
using System.Reflection;

namespace CanDoItAll.Tests.Unit.LlmChats;

public sealed class LlmChatOperationIdempotencyTests
{
    [Fact]
    public async Task Same_id_and_request_replays_without_a_second_provider_dispatch()
    {
        var harness = await LlmChatOperationHarness.CreateAsync();
        var operationId = LlmChatOperationId.New();
        var command = harness.CreateSendCommand(operationId, "hello");

        var first = await harness.SendAndDispatchAsync(command);
        var replay = await harness.Service.SendAsync(command);

        Assert.True(first.IsSuccess);
        Assert.True(replay.IsSuccess);
        Assert.Equal(LlmChatOperationStatus.Succeeded, replay.Value!.Operation.Status);
        Assert.Equal(first.Value!.AssistantMessage, replay.Value.AssistantMessage);
        Assert.Equal(1, harness.Engine.SendCount);
    }

    [Fact]
    public async Task Same_id_with_a_different_request_conflicts_before_dispatch()
    {
        var harness = await LlmChatOperationHarness.CreateAsync();
        var operationId = LlmChatOperationId.New();
        await harness.SendAndDispatchAsync(harness.CreateSendCommand(operationId, "first"));

        var conflict = await harness.Service.SendAsync(harness.CreateSendCommand(operationId, "different"));

        Assert.True(conflict.IsFailure);
        Assert.Equal(LlmChatErrorCodes.OperationIdConflict, Assert.Single(conflict.Errors).Code);
        Assert.Equal(1, harness.Engine.SendCount);
    }

    [Fact]
    public async Task Same_id_and_request_replays_after_the_conversation_is_archived()
    {
        var harness = await LlmChatOperationHarness.CreateAsync();
        var command = harness.CreateSendCommand(LlmChatOperationId.New(), "hello");
        var first = await harness.SendAndDispatchAsync(command);
        var conversation = await harness.Conversations.TryGetAsync(harness.ConversationId);
        Assert.NotNull(conversation);
        harness.Conversations.Seed(new LlmChatConversation(
            conversation.Id,
            conversation.DefinitionId,
            conversation.DefinitionRevision,
            conversation.Title,
            LlmChatConversationStatus.Archived,
            conversation.Origin,
            conversation.CreatedAtUtc,
            conversation.UpdatedAtUtc,
            conversation.ConcurrencyToken + 1));

        var replay = await harness.Service.SendAsync(command);

        Assert.True(first.IsSuccess);
        Assert.True(replay.IsSuccess);
        Assert.Equal(LlmChatOperationStatus.Succeeded, replay.Value!.Operation.Status);
        Assert.Equal(1, harness.Engine.SendCount);
    }
}

public sealed class LlmChatOperationDispatchClaimTests
{
    [Fact]
    public async Task Admission_is_rejected_when_no_dispatcher_executor_is_available()
    {
        var harness = await LlmChatOperationHarness.CreateAsync(executorAvailable: false);
        var command = harness.CreateSendCommand(LlmChatOperationId.New(), "hello");

        var result = await harness.Service.SendAsync(command);

        Assert.True(result.IsFailure);
        Assert.Equal(LlmChatErrorCodes.DispatcherUnavailable, Assert.Single(result.Errors).Code);
        Assert.Null(await harness.Operations.TryGetAsync(command.OperationId));
        Assert.Equal(0, harness.Engine.SendCount);
    }

    [Fact]
    public async Task Concurrent_same_request_has_one_atomic_dispatch_claim_winner()
    {
        var harness = await LlmChatOperationHarness.CreateAsync(blockDispatch: true);
        var command = harness.CreateSendCommand(LlmChatOperationId.New(), "hello");

        var first = await harness.Service.SendAsync(command);
        var dispatch = harness.DispatchAsync(command.OperationId);
        await harness.Engine.DispatchStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var replay = await harness.Service.SendAsync(command);

        Assert.True(replay.IsSuccess);
        Assert.Equal(LlmChatOperationStatus.Running, replay.Value!.Operation.Status);
        Assert.Equal(1, harness.Engine.SendCount);

        harness.Engine.ReleaseDispatch();
        await dispatch;
        Assert.True(first.IsSuccess);
        Assert.Equal(LlmChatOperationStatus.Succeeded, (await harness.Service.GetAsync(command.OperationId)).Value!.Operation.Status);
    }

    [Fact]
    public async Task Durable_dispatch_started_evidence_never_redispatches_on_retry()
    {
        var harness = await LlmChatOperationHarness.CreateAsync();
        var operationId = LlmChatOperationId.New();
        harness.Operations.Seed(harness.CreateOperation(
            operationId,
            LlmChatOperationStatus.Running) with
        {
            TurnAdmittedAtUtc = harness.Now,
            ProviderDispatchStartedAtUtc = harness.Now
        });

        var replay = await harness.Service.SendAsync(harness.CreateSendCommand(operationId, "hello"));
        await harness.DispatchAsync(operationId);
        var recovered = await harness.Service.GetAsync(operationId);

        Assert.True(replay.IsSuccess);
        Assert.Equal(LlmChatOperationStatus.Running, replay.Value!.Operation.Status);
        Assert.Equal(LlmChatOperationStatus.RecoveryRequired, recovered.Value!.Operation.Status);
        Assert.Equal(0, harness.Engine.SendCount);
    }
}

public sealed class LlmChatExecutionLeaseTests
{
    [Fact]
    public async Task Live_lease_allows_only_one_owner()
    {
        var context = CreateContext();

        var first = await context.Service.TryClaimAsync(context.OperationId, LlmChatExecutionOwnerId.New());
        var second = await context.Service.TryClaimAsync(context.OperationId, LlmChatExecutionOwnerId.New());

        Assert.True(first.Claimed);
        Assert.False(second.Claimed);
        Assert.Equal(1, first.Lease!.Value.Epoch);
    }

    [Fact]
    public async Task Expired_pre_dispatch_lease_is_reclaimed_with_a_new_epoch()
    {
        var context = CreateContext();
        var first = await context.Service.TryClaimAsync(context.OperationId, LlmChatExecutionOwnerId.New());
        context.Time.Advance(TimeSpan.FromSeconds(2));

        var second = await context.Service.TryClaimAsync(context.OperationId, LlmChatExecutionOwnerId.New());

        Assert.True(first.Claimed);
        Assert.True(second.Claimed);
        Assert.Equal(2, second.Lease!.Value.Epoch);
    }

    [Fact]
    public async Task Expired_post_dispatch_lease_requires_recovery_and_is_not_reclaimed()
    {
        var context = CreateContext();
        var first = await context.Service.TryClaimAsync(context.OperationId, LlmChatExecutionOwnerId.New());
        var claimed = first.Operation!;
        var dispatchStarted = claimed with
        {
            ProviderDispatchStartedAtUtc = context.Time.GetUtcNow(),
            DispatchPhase = LlmChatDispatchPhase.ProviderDispatchStarted,
            ConcurrencyToken = claimed.ConcurrencyToken + 1
        };
        Assert.True(await context.Operations.TryReplaceOwnedAsync(
            dispatchStarted,
            claimed.ConcurrencyToken,
            first.Lease!.Value,
            context.Time.GetUtcNow()));
        context.Time.Advance(TimeSpan.FromSeconds(2));

        var second = await context.Service.TryClaimAsync(context.OperationId, LlmChatExecutionOwnerId.New());

        Assert.False(second.Claimed);
        Assert.True(second.Recovered);
        Assert.Equal(LlmChatOperationStatus.RecoveryRequired, second.Operation!.Status);
    }

    private static LeaseTestContext CreateContext()
    {
        var now = new DateTimeOffset(2026, 8, 14, 12, 0, 0, TimeSpan.Zero);
        var time = new ManualTimeProvider(now);
        var operations = new InMemoryLlmChatOperationRepository();
        var operationId = LlmChatOperationId.New();
        operations.Seed(new LlmChatOperation(
            operationId,
            LlmChatConversationId.New(),
            LlmChatOperationKind.SendTurn,
            new LlmChatRequestFingerprint(new string('e', 64)),
            1,
            LlmChatOperationStatus.Pending,
            now,
            0)
        {
            TurnAdmittedAtUtc = now
        });
        var options = new LlmChatExecutionLeaseOptions
        {
            PollInterval = TimeSpan.FromMilliseconds(100),
            HeartbeatInterval = TimeSpan.FromMilliseconds(100),
            LeaseDuration = TimeSpan.FromSeconds(1)
        };
        var unitOfWork = new InlineLlmChatUnitOfWork();
        var scope = new LlmChatOperationScopeAccessor();
        return new LeaseTestContext(
            operationId,
            operations,
            time,
            new LlmChatExecutionLeaseService(
                operations,
                unitOfWork,
                options,
                time,
                LlmChatOperationEventTestFactory.Create(operations, unitOfWork, scope, time)));
    }

    private sealed record LeaseTestContext(
        LlmChatOperationId OperationId,
        InMemoryLlmChatOperationRepository Operations,
        ManualTimeProvider Time,
        LlmChatExecutionLeaseService Service);
}

public sealed class LlmChatOperationCancellationTests
{
    [Fact]
    public async Task Cancellation_is_persisted_and_reaches_the_current_execution()
    {
        var harness = await LlmChatOperationHarness.CreateAsync(blockUntilCancelled: true);
        var operationId = LlmChatOperationId.New();
        await harness.Service.SendAsync(harness.CreateSendCommand(operationId, "hello"));
        var dispatch = harness.DispatchAsync(operationId);
        await harness.Engine.DispatchStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var requested = await harness.Service.CancelAsync(operationId);
        await dispatch;
        var completed = await harness.Service.GetAsync(operationId);

        Assert.True(requested.IsSuccess);
        Assert.NotNull(requested.Value!.Operation.CancellationRequestedAtUtc);
        Assert.True(completed.IsSuccess);
        Assert.Equal(LlmChatOperationStatus.Cancelled, completed.Value!.Operation.Status);
        Assert.False(harness.Cancellations.IsRegistered(operationId));
    }

    [Fact]
    public async Task Cancellation_committed_before_finalization_prevents_success()
    {
        var harness = await LlmChatOperationHarness.CreateAsync(blockDispatch: true);
        var operationId = LlmChatOperationId.New();
        await harness.Service.SendAsync(harness.CreateSendCommand(operationId, "hello"));
        var dispatch = harness.DispatchAsync(operationId);
        await harness.Engine.DispatchStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var requested = await harness.Service.CancelAsync(operationId);
        harness.Engine.ReleaseDispatch();
        await dispatch;
        var completed = await harness.Service.GetAsync(operationId);

        Assert.True(requested.IsSuccess);
        Assert.True(completed.IsSuccess);
        Assert.Equal(LlmChatOperationStatus.Cancelled, completed.Value!.Operation.Status);
        Assert.Null(completed.Value.AssistantMessage);
        Assert.True(completed.Value.Operation.CancellationGeneration > 0);
    }
}

public sealed class LlmChatStreamingOperationPipelineTests
{
    [Fact]
    public async Task Partial_provider_failure_compensates_turn_without_committing_assistant_message()
    {
        var harness = await LlmChatOperationHarness.CreateAsync(failAfterPartial: true);
        var operationId = LlmChatOperationId.New();

        var result = await harness.SendAndDispatchAsync(
            harness.CreateSendCommand(operationId, "hello"));
        var turn = await harness.Engine.InspectTurnAsync(harness.ConversationId, operationId);

        Assert.True(result.IsSuccess);
        Assert.Equal(LlmChatOperationStatus.Failed, result.Value!.Operation.Status);
        Assert.Equal(LlmChatErrorCodes.ProviderUnavailable, result.Value.Operation.FailureCode);
        Assert.NotNull(turn);
        Assert.False(turn.HasExactActiveTurn);
        Assert.Null(turn.Assistant);
        Assert.Equal(1, harness.Engine.AbandonCount);
    }
}

public sealed class LlmChatOperationRecoveryTests
{
    [Fact]
    public async Task Missing_local_registration_does_not_recover_or_abandon_a_live_remote_lease()
    {
        var harness = await LlmChatOperationHarness.CreateAsync();
        var operationId = LlmChatOperationId.New();
        harness.Operations.Seed(harness.CreateOperation(operationId, LlmChatOperationStatus.Running) with
        {
            TurnAdmittedAtUtc = harness.Now,
            ProviderDispatchStartedAtUtc = harness.Now,
            ExecutionOwnerId = LlmChatExecutionOwnerId.New(),
            ExecutionEpoch = 1,
            ClaimedAtUtc = harness.Now,
            HeartbeatAtUtc = harness.Now,
            LeaseExpiresAtUtc = harness.Now.AddMinutes(1),
            DispatchPhase = LlmChatDispatchPhase.ProviderDispatchStarted
        });
        harness.Engine.SeedActiveTurn(operationId);

        var reconciled = await harness.Service.ReconcileAsync(operationId);
        var abandoned = await harness.Service.AbandonActiveTurnAsync(
            new AbandonLlmChatActiveTurnCommand(harness.ConversationId, operationId));

        Assert.True(reconciled.IsSuccess);
        Assert.Equal(LlmChatOperationStatus.Running, reconciled.Value!.Operation.Status);
        Assert.True(abandoned.IsFailure);
        Assert.Equal(0, harness.Engine.AbandonCount);
    }

    [Fact]
    public async Task Assistant_commit_reconciles_an_unfinished_operation_to_success()
    {
        var harness = await LlmChatOperationHarness.CreateAsync();
        var operationId = LlmChatOperationId.New();
        var operation = harness.CreateOperation(operationId, LlmChatOperationStatus.Running) with
        {
            TurnAdmittedAtUtc = harness.Now,
            ProviderDispatchStartedAtUtc = harness.Now,
            ProviderDispatchReturnedAtUtc = harness.Now
        };
        harness.Operations.Seed(operation);
        harness.Engine.SeedAssistantEvidence(operationId, "recovered answer");

        var reconciled = await harness.Service.ReconcileAsync(operationId);

        Assert.True(reconciled.IsSuccess);
        Assert.Equal(LlmChatOperationStatus.Succeeded, reconciled.Value!.Operation.Status);
        Assert.Equal("recovered answer", reconciled.Value.AssistantMessage!.Content);
        Assert.Equal(0, harness.Engine.SendCount);
    }

    [Fact]
    public async Task Exact_active_turn_after_dispatch_requires_explicit_recovery_before_abandonment()
    {
        var harness = await LlmChatOperationHarness.CreateAsync();
        var operationId = LlmChatOperationId.New();
        harness.Operations.Seed(harness.CreateOperation(operationId, LlmChatOperationStatus.Running) with
        {
            TurnAdmittedAtUtc = harness.Now,
            ProviderDispatchStartedAtUtc = harness.Now
        });
        harness.Engine.SeedActiveTurn(operationId);

        var reconciled = await harness.Service.ReconcileAsync(operationId);
        var abandoned = await harness.Service.AbandonActiveTurnAsync(
            new AbandonLlmChatActiveTurnCommand(harness.ConversationId, operationId));

        Assert.True(reconciled.IsSuccess);
        Assert.Equal(LlmChatOperationStatus.RecoveryRequired, reconciled.Value!.Operation.Status);
        Assert.True(abandoned.IsSuccess);
        Assert.Equal(LlmChatOperationStatus.Failed, abandoned.Value!.Operation.Status);
        Assert.Equal(1, harness.Engine.AbandonCount);
    }

    [Fact]
    public async Task Crash_before_dispatch_remains_reclaimable_without_guessing_or_abandoning_transcript_data()
    {
        var harness = await LlmChatOperationHarness.CreateAsync();
        var operationId = LlmChatOperationId.New();
        harness.Operations.Seed(harness.CreateOperation(operationId, LlmChatOperationStatus.Running));

        var reconciled = await harness.Service.ReconcileAsync(operationId);

        Assert.True(reconciled.IsSuccess);
        Assert.Equal(LlmChatOperationStatus.Running, reconciled.Value!.Operation.Status);
        Assert.Equal(0, harness.Engine.AbandonCount);
    }

    [Fact]
    public async Task Failed_compensation_escalates_to_recovery_instead_of_terminal_failure()
    {
        var harness = await LlmChatOperationHarness.CreateAsync();
        var operationId = LlmChatOperationId.New();
        harness.Operations.Seed(harness.CreateOperation(operationId, LlmChatOperationStatus.Running) with
        {
            TurnAdmittedAtUtc = harness.Now,
            ProviderDispatchStartedAtUtc = harness.Now,
            ProviderDispatchReturnedAtUtc = harness.Now
        });
        harness.Engine.SeedActiveTurn(operationId);
        harness.Engine.FailCompensation = true;
        await harness.Invocations.AppendAsync(new LlmChatInvocationRecord(
            operationId,
            harness.Revision.ProviderProfileId,
            harness.Revision.ProviderKind,
            harness.Revision.ProviderName,
            harness.Revision.Model,
            harness.Revision.Settings.ThinkingEffort,
            AgentReasoningEffortLevel.Low,
            1,
            LlmUsage.Zero,
            LlmChatInvocationOutcome.Failed,
            LlmChatErrorCodes.ProviderUnavailable,
            harness.Now,
            harness.Now,
            operationId.ToString()));

        var reconciled = await harness.Service.ReconcileAsync(operationId);

        Assert.True(reconciled.IsSuccess);
        Assert.Equal(LlmChatOperationStatus.RecoveryRequired, reconciled.Value!.Operation.Status);
    }
}

public sealed class LlmChatOperationReducerTests
{
    [Fact]
    public void Successful_attempt_requires_live_result_to_commit_and_restart_requires_recovery()
    {
        var operation = new LlmChatOperation(
            LlmChatOperationId.New(),
            LlmChatConversationId.New(),
            LlmChatOperationKind.SendTurn,
            new LlmChatRequestFingerprint(new string('a', 64)),
            1,
            LlmChatOperationStatus.Running,
            DateTimeOffset.UtcNow,
            0);
        var durable = new LlmChatOperationDurableEvidence(
            operation,
            true,
            false,
            null,
            LlmChatInvocationOutcome.Succeeded,
            string.Empty);

        var restart = LlmChatOperationReducer.Reduce(durable);
        var live = LlmChatOperationReducer.Reduce(durable with { HasPendingAssistantResult = true });

        Assert.Equal(LlmChatOperationDecisionKind.RequireRecovery, restart.Kind);
        Assert.Equal(LlmChatOperationDecisionKind.CommitSucceeded, live.Kind);
    }
}

public sealed class LlmChatOperationTransitionRegressionTests
{
    [Fact]
    public void Committed_cancellation_cannot_transition_to_succeeded()
    {
        var now = DateTimeOffset.UtcNow;
        var operation = new LlmChatOperation(
            LlmChatOperationId.New(),
            LlmChatConversationId.New(),
            LlmChatOperationKind.SendTurn,
            new LlmChatRequestFingerprint(new string('d', 64)),
            1,
            LlmChatOperationStatus.CancellationRequested,
            now,
            0)
        {
            CancellationRequestedAtUtc = now
        };

        var transitions = typeof(LlmChatOperation).Assembly.GetType(
            "CanDoItAll.Modules.LlmChats.Operations.LlmChatOperationTransitions",
            throwOnError: true)!;
        var completeTranscript = transitions.GetMethod(
            "CompleteTranscript",
            BindingFlags.Public | BindingFlags.Static)!;

        var exception = Assert.Throws<TargetInvocationException>(() => completeTranscript.Invoke(
            null,
            [operation, now.AddSeconds(1), 3L, Guid.NewGuid()]));

        Assert.IsType<InvalidOperationException>(exception.InnerException);
    }
}

public sealed class LlmChatInvocationAuditTests
{
    [Fact]
    public async Task Streaming_audit_records_each_actual_attempt_with_its_own_usage_and_ordinal()
    {
        var now = new DateTimeOffset(2026, 8, 15, 12, 0, 0, TimeSpan.Zero);
        var provider = ProviderRuntimeTestData.CreateProvider();
        var operationId = LlmChatOperationId.New();
        var operations = new InMemoryLlmChatOperationRepository();
        operations.Seed(new LlmChatOperation(
            operationId,
            LlmChatConversationId.New(),
            LlmChatOperationKind.SendTurn,
            new LlmChatRequestFingerprint(new string('b', 64)),
            1,
            LlmChatOperationStatus.Running,
            now,
            0)
        {
            TurnAdmittedAtUtc = now
        });
        var invocations = new InMemoryLlmChatInvocationRecordRepository();
        var scope = new LlmChatOperationScopeAccessor();
        var unitOfWork = new InlineLlmChatUnitOfWork();
        var timeProvider = new FixedTimeProvider(now.AddSeconds(3));
        var evidence = new LlmChatOperationEvidenceService(
            operations,
            invocations,
            unitOfWork,
            scope,
            timeProvider,
            LlmChatOperationEventTestFactory.Create(operations, unitOfWork, scope, timeProvider));
        var failedUsage = new LlmUsage(2, 1);
        var successfulUsage = new LlmUsage(3, 2, 1);
        var inner = new SequenceStreamingInvocationPort(
        [
            new LlmStreamingAttemptStarted(
                1,
                provider.Id,
                provider.Kind,
                "model-fast",
                LlmStreamingDeliveryMode.Incremental,
                now),
            new LlmStreamingFailed(
                1,
                LlmInvocationFailureKind.ProviderFailure,
                failedUsage,
                true,
                now.AddSeconds(1)) { AttemptUsage = failedUsage },
            new LlmStreamingAttemptStarted(
                2,
                provider.Id,
                provider.Kind,
                "model-fast",
                LlmStreamingDeliveryMode.Incremental,
                now.AddSeconds(1)),
            new LlmStreamingTextDelta(2, "answer", 1),
            new LlmStreamingCompleted(
                2,
                "model-fast",
                "stop",
                failedUsage.Add(successfulUsage),
                LlmStreamingDeliveryMode.Incremental,
                now.AddSeconds(2)) { AttemptUsage = successfulUsage }
        ]);
        var port = new AuditedLlmChatStreamingInvocationPort(
            inner,
            evidence,
            new ProviderModelCapabilityResolver(),
            scope,
            new FixedTimeProvider(now.AddSeconds(3)));
        var request = new LlmInvocationRequest(
            provider,
            "model-fast",
            [new LlmMessage(LlmMessageRole.User, "hello")],
            correlationId: operationId.ToString());
        using var operation = scope.Push(new LlmChatOperationExecutionContext(
            operationId,
            new LlmChatRuntimeIdentity(
                ProviderRuntimeTestData.RuntimeIdentity.ActiveProfileId!.Value,
                ProviderRuntimeTestData.RuntimeIdentity.ActiveFingerprint!,
                ProviderRuntimeTestData.RuntimeIdentity.Generation)));

        await foreach (var _ in port.StreamAsync(request))
        {
        }

        var records = await invocations.ListAsync(operationId);
        Assert.Collection(
            records,
            record =>
            {
                Assert.Equal(1, record.Ordinal);
                Assert.Equal(LlmChatInvocationOutcome.Failed, record.Outcome);
                Assert.Equal(failedUsage, record.Usage);
            },
            record =>
            {
                Assert.Equal(2, record.Ordinal);
                Assert.Equal(LlmChatInvocationOutcome.Succeeded, record.Outcome);
                Assert.Equal(successfulUsage, record.Usage);
            });
    }

    [Fact]
    public async Task Failed_provider_usage_is_retained_outside_the_transcript()
    {
        var context = LlmChatInvocationAuditHarness.Create(
            requestedEffort: null,
            invoke: request => throw new LlmInvocationException(
                LlmInvocationFailureKind.ProviderFailure,
                request.Provider.Name,
                request.Model,
                request.CorrelationId,
                usage: new LlmUsage(7, 3, 2)));

        await Assert.ThrowsAsync<LlmInvocationException>(() => context.InvokeAsync());

        var record = Assert.Single(await context.Invocations.ListAsync(context.OperationId));
        Assert.Equal(LlmChatInvocationOutcome.Failed, record.Outcome);
        Assert.Equal(new LlmUsage(7, 3, 2), record.Usage);
    }

    [Theory]
    [InlineData(null, AgentReasoningEffortLevel.Low)]
    [InlineData(AgentReasoningEffortLevel.None, AgentReasoningEffortLevel.None)]
    public async Task Audit_distinguishes_provider_default_from_explicit_none(
        AgentReasoningEffortLevel? requested,
        AgentReasoningEffortLevel expectedEffective)
    {
        var context = LlmChatInvocationAuditHarness.Create(
            requested,
            request => new LlmInvocationResult(request.Model, "answer", new LlmUsage(2, 1)));

        await context.InvokeAsync();

        var record = Assert.Single(await context.Invocations.ListAsync(context.OperationId));
        Assert.Equal(requested, record.RequestedThinkingEffort);
        Assert.Equal(expectedEffective, record.EffectiveThinkingEffort);
    }

    [Fact]
    public async Task Deadline_is_a_failed_attempt_for_both_direct_and_recovery_reduction()
    {
        var context = LlmChatInvocationAuditHarness.Create(
            requestedEffort: null,
            invoke: request => throw new LlmInvocationException(
                LlmInvocationFailureKind.DeadlineExceeded,
                request.Provider.Name,
                request.Model,
                request.CorrelationId));

        await Assert.ThrowsAsync<LlmInvocationException>(() => context.InvokeAsync());

        var record = Assert.Single(await context.Invocations.ListAsync(context.OperationId));
        Assert.Equal(LlmChatInvocationOutcome.Failed, record.Outcome);
        Assert.Equal(LlmChatErrorCodes.DeadlineExceeded, record.FailureCode);
    }
}
