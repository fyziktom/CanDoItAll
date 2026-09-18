using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed partial class AgentChatExecutionActivityOrchestratorTests {
    [Fact]
    public async Task Recovery_uses_original_identity_in_a_new_operation_without_send_context_capture_or_approval() {
        var context = CreateContext();
        var original = RecoveryStream(context);
        context.Workspace.RecoveryCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var recovery = context.Orchestrator.StartRunRecovery(context.AgentId, context.Workspace.SessionId,
            context.Workspace.ExecutionRunId, original);
        await context.Workspace.RecoveryStarted.Task.WaitAsync(TestContext.DefaultTimeout);
        Assert.False(recovery.Completion.IsCompleted);
        Assert.NotEqual(original.OperationId, recovery.StreamId.OperationId);
        Assert.Equal(original.DatabaseProfileId, recovery.StreamId.DatabaseProfileId);
        Assert.Equal(original.WorkspaceScope, recovery.StreamId.WorkspaceScope);
        Assert.Equal(original.DatabaseProfileGeneration, recovery.StreamId.DatabaseProfileGeneration);
        var call = Assert.Single(context.Workspace.RecoveryCalls);
        Assert.Equal(context.Workspace.ExecutionRunId, call.RunId);
        Assert.Equal(context.AgentId, call.Operation.AgentId);
        Assert.Equal(context.Workspace.SessionId, call.Operation.ChatSessionId);
        var expected = new ExecutionRunResult(call.RunId, context.Workspace.SessionId, "Recovered original run", null,
            context.Workspace.SendResult.Metric) { State = ExecutionState.Completed };
        context.Workspace.RecoveryCompletion.SetResult(expected);
        Assert.Same(expected, await recovery.Completion.WaitAsync(TestContext.DefaultTimeout));
        Assert.Empty(context.Workspace.SendCalls);
        Assert.Empty(context.Workspace.ApprovalCalls);
        Assert.False(context.ContextRegistry.CaptureStarted.IsCompleted);
        Assert.Equal(AgentExecutionActivityPhase.Completed, (await ReadEventsAsync(context.Coordinator, recovery.StreamId))[^1].Event.Phase);
    }

    [Theory]
    [InlineData(AgentExecutionActivityAccessRejectionReason.DatabaseProfileMismatch)]
    [InlineData(AgentExecutionActivityAccessRejectionReason.DatabaseProfileGenerationMismatch)]
    [InlineData(AgentExecutionActivityAccessRejectionReason.WorkspaceScopeMismatch)]
    public void Recovery_refuses_a_different_profile_scope_or_profile_lifetime_before_owner_dispatch(
        AgentExecutionActivityAccessRejectionReason mismatch) {
        var context = CreateContext();
        var original = RecoveryStream(context);
        if (mismatch == AgentExecutionActivityAccessRejectionReason.DatabaseProfileGenerationMismatch) {
            context.GenerationSource.Generation = new(2);
        }
        var source = new AgentExecutionActivityStreamId(
            mismatch == AgentExecutionActivityAccessRejectionReason.DatabaseProfileMismatch ? Guid.NewGuid() : original.DatabaseProfileId,
            mismatch == AgentExecutionActivityAccessRejectionReason.WorkspaceScopeMismatch ? WorkspaceScopeDescriptor.Sandbox : original.WorkspaceScope,
            original.DatabaseProfileGeneration, original.OperationId);

        var failure = Assert.Throws<AgentExecutionActivityAccessException>(() => context.Orchestrator.StartRunRecovery(
            context.AgentId, context.Workspace.SessionId, context.Workspace.ExecutionRunId, source));

        Assert.Equal(mismatch, failure.Reason);
        Assert.Empty(context.Workspace.RecoveryCalls);
        Assert.Empty(context.Workspace.SendCalls);
        Assert.Empty(context.Workspace.ApprovalCalls);
        Assert.False(context.ContextRegistry.CaptureStarted.IsCompleted);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Recovery_failure_or_cancellation_terminalizes_its_own_activity(bool cancelled) {
        var context = CreateContext();
        Exception expected = cancelled ? new OperationCanceledException("Recovery fixture cancellation") : new IOException("Recovery fixture failure");
        context.Workspace.RecoveryFault = expected;
        var recovery = context.Orchestrator.StartRunRecovery(context.AgentId, context.Workspace.SessionId,
            context.Workspace.ExecutionRunId, RecoveryStream(context));

        Assert.Same(expected, await Assert.ThrowsAnyAsync<Exception>(() => recovery.Completion));

        Assert.Equal(cancelled ? AgentExecutionActivityPhase.Cancelled : AgentExecutionActivityPhase.Failed,
            (await ReadEventsAsync(context.Coordinator, recovery.StreamId))[^1].Event.Phase);
        Assert.Empty(context.Workspace.SendCalls);
        Assert.Empty(context.Workspace.ApprovalCalls);
    }

    [Fact]
    public async Task Recovery_cannot_leave_an_unfinished_activity_after_returning_a_result() {
        var context = CreateContext();
        context.Workspace.LeaveRecoveryUnfinished = true;
        var recovery = context.Orchestrator.StartRunRecovery(context.AgentId, context.Workspace.SessionId,
            context.Workspace.ExecutionRunId, RecoveryStream(context));

        await Assert.ThrowsAsync<InvalidOperationException>(() => recovery.Completion);

        Assert.Equal(AgentExecutionActivityPhase.Failed, (await ReadEventsAsync(context.Coordinator, recovery.StreamId))[^1].Event.Phase);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Recovery_requires_the_original_agent_thread_and_run_identifiers(int emptyIdentifier) {
        var context = CreateContext();

        Assert.Throws<ArgumentException>(() => context.Orchestrator.StartRunRecovery(
            emptyIdentifier == 0 ? Guid.Empty : context.AgentId,
            emptyIdentifier == 1 ? Guid.Empty : context.Workspace.SessionId,
            emptyIdentifier == 2 ? Guid.Empty : context.Workspace.ExecutionRunId, RecoveryStream(context)));

        Assert.Empty(context.Workspace.RecoveryCalls);
    }

    private static AgentExecutionActivityStreamId RecoveryStream(TestContext context)
        => new(context.ProfileId, WorkspaceScopeDescriptor.Organization(context.ProfileId.ToString("N")),
            context.GenerationSource.GetGeneration(), AgentExecutionOperationId.New());

    private sealed class RecoveryProfileGenerationSource : IAgentExecutionProfileGenerationSource {
        public DatabaseProfileGeneration Generation { get; set; } = new(0);
        public DatabaseProfileGeneration GetGeneration() => Generation;
    }
}
