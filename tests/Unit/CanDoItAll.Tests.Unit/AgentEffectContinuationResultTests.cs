using System.Collections.Immutable;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Tests.Unit;

public sealed class AgentEffectContinuationResultTests {
    [Fact]
    public void Normal_results_reject_missing_metric_or_chat_message() {
        var continuation = Continuation();
        var message = Message();
        Assert.Throws<ArgumentNullException>(() => new ExecutionRunResult(continuation.ExecutionRunId,
            continuation.ChatSessionId, string.Empty, null, null!));
        Assert.Throws<ArgumentNullException>(() => new AgentChatRunResult(continuation.ChatSessionId!.Value, message, null!));
        Assert.Throws<ArgumentNullException>(() => new AgentChatRunResult(continuation.ChatSessionId!.Value, null!, Metric()));
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        Assert.Throws<ArgumentNullException>(() => JsonSerializer.Deserialize<ExecutionRunResult>(
            "{\"executionRunId\":\"" + continuation.ExecutionRunId + "\",\"responseText\":\"\",\"metric\":null}", options));
    }

    [Fact]
    public void Effect_factory_requires_typed_waiting_and_never_provider_output() {
        var continuation = Continuation();
        var result = ExecutionRunResult.ForEffectContinuation(continuation);
        var chat = AgentChatRunResult.ForEffectContinuation(continuation);
        Assert.Equal(ExecutionState.WaitingOnTool, result.State);
        Assert.Equal(continuation.ExecutionRunId, chat.ExecutionRunId);
        Assert.Same(continuation, result.EffectContinuation);
        Assert.Null(result.Metric);
        Assert.Null(chat.Metric);
        Assert.Null(result.AssistantMessage);
        Assert.Null(chat.AssistantMessage);
        Assert.Null(result.StructuredOutput);
        Assert.Null(result.ContextCompletionNotification);
        Assert.Equal(string.Empty, result.ResponseText);
        Assert.Throws<ArgumentException>(() => result with { State = ExecutionState.Completed });
        Assert.Throws<ArgumentException>(() => chat with { State = ExecutionState.Completed });
        Assert.Throws<ArgumentException>(() => chat with { ExecutionRunId = Guid.NewGuid() });
        Assert.Throws<ArgumentException>(() => new ExecutionRunResult(continuation.ExecutionRunId, continuation.ChatSessionId,
            "invented output", null, null, continuation));
        Assert.Throws<ArgumentException>(() => new AgentChatRunResult(continuation.ChatSessionId!.Value, Message(), null, continuation));
        var unconfirmed = Continuation(AgentEffectCheckpointPersistence.Unconfirmed);
        Assert.Throws<ArgumentException>(() => ExecutionRunResult.ForEffectContinuation(unconfirmed));
        Assert.Throws<ArgumentException>(() => AgentChatRunResult.ForEffectContinuation(unconfirmed));
        Assert.Same(unconfirmed, new AgentEffectReconciliationIncompleteException(unconfirmed).Continuation);
        Assert.Throws<ArgumentException>(() => new AgentEffectReconciliationIncompleteException(continuation));
        var snapshot = new AgentEffectContinuation(continuation.ExecutionRunId, continuation.ChatSessionId,
            continuation.RunRevision, continuation.JournalRevision, continuation.Requirement, continuation.Counts,
            0, 0, AgentEffectReconciliationStop.SnapshotRead, AgentEffectCheckpointPersistence.Confirmed, []);
        Assert.Throws<ArgumentException>(() => ExecutionRunResult.ForEffectContinuation(snapshot));
        Assert.Throws<ArgumentException>(() => AgentChatRunResult.ForEffectContinuation(snapshot));
        Assert.Throws<ArgumentException>(() => new ExecutionRunResult(snapshot.ExecutionRunId, snapshot.ChatSessionId, string.Empty, null, null, snapshot));
        Assert.Throws<ArgumentException>(() => new AgentChatRunResult(snapshot.ChatSessionId!.Value, null, null, snapshot));
        Assert.Throws<ArgumentException>(() => new AgentEffectReconciliationCleanupException(snapshot, new IOException("Not a published checkpoint.")));
        var restored = JsonSerializer.Deserialize<AgentEffectContinuation>(JsonSerializer.Serialize(snapshot))!;
        Assert.Equal(AgentEffectReconciliationStop.SnapshotRead, restored.Stop);
        Assert.Equal(AgentEffectCheckpointPersistence.Confirmed, restored.Persistence);
    }

    [Fact]
    public void Legacy_result_and_activity_serialization_remain_compatible() {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var metric = Metric();
        var normal = new ExecutionRunResult(Guid.NewGuid(), metric.ChatSessionId, "actual output", Message(), metric) {
            State = ExecutionState.Completed
        };
        var normalJson = JsonSerializer.Serialize(normal, options);
        Assert.DoesNotContain("effectContinuation", normalJson, StringComparison.Ordinal);
        Assert.Equal(normal, JsonSerializer.Deserialize<ExecutionRunResult>(normalJson, options));
        var chat = new AgentChatRunResult(metric.ChatSessionId!.Value, Message(), metric) {
            ExecutionRunId = normal.ExecutionRunId, State = ExecutionState.Completed
        };
        Assert.Equal(chat, JsonSerializer.Deserialize<AgentChatRunResult>(JsonSerializer.Serialize(chat, options), options));
        var effect = ExecutionRunResult.ForEffectContinuation(Continuation());
        var restored = JsonSerializer.Deserialize<ExecutionRunResult>(JsonSerializer.Serialize(effect, options), options)!;
        Assert.Equal(effect.ExecutionRunId, restored.ExecutionRunId);
        Assert.Equal(effect.State, restored.State);
        Assert.Null(restored.Metric);
        Assert.Equal(JsonSerializer.Serialize(effect.EffectContinuation, options), JsonSerializer.Serialize(restored.EffectContinuation, options));
        Assert.Equal(12, (int)AgentExecutionActivityPhase.AwaitingApproval);
        Assert.Equal(16, (int)AgentExecutionActivityPhase.Cancelled);
    }

    [Fact]
    public void Awaiting_continuation_phase_roundtrips_and_rejects_invalid_terminal_pairs() {
        var continuation = Continuation();
        var activity = new AgentExecutionActivity(AgentExecutionActivityPhase.AwaitingContinuation, DateTimeOffset.UtcNow,
            Guid.NewGuid(), "Explicit continuation required.", continuation.ChatSessionId, continuation.ExecutionRunId,
            AgentExecutionActivityTerminalOutcome.Suspended);
        Assert.Equal(17, (int)activity.Phase);
        Assert.True(activity.IsTerminal);
        Assert.Equal(activity, JsonSerializer.Deserialize<AgentExecutionActivity>(JsonSerializer.Serialize(activity)));
        Assert.Throws<ArgumentException>(() => new AgentExecutionActivity(AgentExecutionActivityPhase.AwaitingContinuation,
            DateTimeOffset.UtcNow, Guid.NewGuid(), "Not completed.", terminalOutcome: AgentExecutionActivityTerminalOutcome.Succeeded));
        AgentExecutionActivityTransitionRules.EnsureProgressTransition(AgentExecutionActivityPhase.ResolvingSession,
            AgentExecutionActivityPhase.AwaitingContinuation);
        AgentExecutionActivityTransitionRules.EnsureTerminalTransition(AgentExecutionActivityPhase.AwaitingContinuation,
            AgentExecutionActivityTerminalOutcome.Suspended);
        Assert.Throws<InvalidOperationException>(() => AgentExecutionActivityTransitionRules.EnsureTerminalTransition(
            AgentExecutionActivityPhase.AwaitingContinuation, AgentExecutionActivityTerminalOutcome.Succeeded));
    }

    private static AgentEffectContinuation Continuation(AgentEffectCheckpointPersistence persistence = AgentEffectCheckpointPersistence.Confirmed)
        => new(Guid.NewGuid(), Guid.NewGuid(), 2, 2, AgentToolContinuationRequirement.None, new(1, 1, 0, 0, 0, 0, 0),
            0, 0, AgentEffectReconciliationStop.ExhaustedSnapshot, persistence, ImmutableArray<AgentEffectReceiptSummary>.Empty);

    private static ChatMessageRecord Message() => new(Guid.NewGuid(), ChatMessageRole.Assistant, "Actual reply.", DateTimeOffset.UtcNow, 3);

    private static AgentRunMetric Metric() => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow,
        RunOutcome.Succeeded, "actual-provider", "actual-model", 50, 2, 3, 0);
}
