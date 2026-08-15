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

namespace CanDoItAll.Tests.Unit;

public sealed class LlmChatOperationIdempotencyTests
{
    [Fact]
    public async Task Same_id_and_request_replays_without_a_second_provider_dispatch()
    {
        var harness = await LlmChatOperationHarness.CreateAsync();
        var operationId = LlmChatOperationId.New();
        var command = harness.CreateSendCommand(operationId, "hello");

        var first = await harness.Service.SendAsync(command);
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
        await harness.Service.SendAsync(harness.CreateSendCommand(operationId, "first"));

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
        var first = await harness.Service.SendAsync(command);
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
    public async Task Concurrent_same_request_has_one_atomic_dispatch_claim_winner()
    {
        var harness = await LlmChatOperationHarness.CreateAsync(blockDispatch: true);
        var command = harness.CreateSendCommand(LlmChatOperationId.New(), "hello");

        var first = harness.Service.SendAsync(command);
        await harness.Engine.DispatchStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var replay = await harness.Service.SendAsync(command);

        Assert.True(replay.IsSuccess);
        Assert.Equal(LlmChatOperationStatus.Running, replay.Value!.Operation.Status);
        Assert.Equal(1, harness.Engine.SendCount);

        harness.Engine.ReleaseDispatch();
        Assert.True((await first).IsSuccess);
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

        Assert.True(replay.IsSuccess);
        Assert.Equal(LlmChatOperationStatus.RecoveryRequired, replay.Value!.Operation.Status);
        Assert.Equal(0, harness.Engine.SendCount);
    }
}

public sealed class LlmChatOperationCancellationTests
{
    [Fact]
    public async Task Cancellation_is_persisted_and_reaches_the_current_execution()
    {
        var harness = await LlmChatOperationHarness.CreateAsync(blockUntilCancelled: true);
        var operationId = LlmChatOperationId.New();
        var send = harness.Service.SendAsync(harness.CreateSendCommand(operationId, "hello"));
        await harness.Engine.DispatchStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var requested = await harness.Service.CancelAsync(operationId);
        var completed = await send;

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
        var send = harness.Service.SendAsync(harness.CreateSendCommand(operationId, "hello"));
        await harness.Engine.DispatchStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var requested = await harness.Service.CancelAsync(operationId);
        harness.Engine.ReleaseDispatch();
        var completed = await send;

        Assert.True(requested.IsSuccess);
        Assert.True(completed.IsSuccess);
        Assert.Equal(LlmChatOperationStatus.Cancelled, completed.Value!.Operation.Status);
        Assert.Null(completed.Value.AssistantMessage);
        Assert.True(completed.Value.Operation.CancellationGeneration > 0);
    }
}

public sealed class LlmChatOperationRecoveryTests
{
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
    public async Task Crash_before_dispatch_is_failed_without_guessing_or_abandoning_transcript_data()
    {
        var harness = await LlmChatOperationHarness.CreateAsync();
        var operationId = LlmChatOperationId.New();
        harness.Operations.Seed(harness.CreateOperation(operationId, LlmChatOperationStatus.Running));

        var reconciled = await harness.Service.ReconcileAsync(operationId);

        Assert.True(reconciled.IsSuccess);
        Assert.Equal(LlmChatOperationStatus.Failed, reconciled.Value!.Operation.Status);
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
