using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class AgentChatBackgroundExecutionTests {
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);
    private static readonly AgentChatOwnerId Owner = new(new string('a', 64));
    private static readonly AgentChatOwnerId OtherOwner = new(new string('b', 64));

    [Fact]
    public async Task Accepted_execution_is_discoverable_without_its_original_view_and_releases_its_scope_once() {
        var factory = new ScopeFactory();
        await using var executions = Create(factory);
        var operation = new Activity();
        var completion = Send(executions, operation);
        await factory.Executor.Started.Task.WaitAsync(Timeout);

        var recovered = Assert.Single(executions.Snapshot(Owner, operation.StreamId.DatabaseProfileId,
            operation.StreamId.DatabaseProfileGeneration));
        Assert.Equal(operation.StreamId, recovered.StreamId);
        Assert.Equal(operation.ExecutionRunId, recovered.ExecutionRunId);
        Assert.Equal(ActiveAgentChatRunState.Running, recovered.RunState);
        Assert.Equal(0, factory.Disposals);

        factory.Executor.Release.TrySetResult();
        await completion.WaitAsync(Timeout);
        Assert.Equal(1, factory.Disposals);
        Assert.Equal(ActiveAgentChatRunState.Idle, Assert.Single(executions.Snapshot(Owner,
            operation.StreamId.DatabaseProfileId, operation.StreamId.DatabaseProfileGeneration)).RunState);
        Assert.True(executions.TryDismiss(Owner, operation.StreamId.DatabaseProfileId,
            operation.StreamId.DatabaseProfileGeneration, operation.AgentId!.Value, operation.ChatSessionId!.Value));
        Assert.Empty(executions.Snapshot(Owner, operation.StreamId.DatabaseProfileId, operation.StreamId.DatabaseProfileGeneration));
    }

    [Fact]
    public async Task Another_owner_cannot_discover_or_continue_the_original_conversation() {
        var factory = new ScopeFactory();
        await using var executions = Create(factory);
        var original = new Activity();
        var completion = Send(executions, original);
        await factory.Executor.Started.Task.WaitAsync(Timeout);

        Assert.Empty(executions.Snapshot(OtherOwner, original.StreamId.DatabaseProfileId,
            original.StreamId.DatabaseProfileGeneration));
        var foreign = new Activity(original);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => executions.ApproveAsync(
            new(OtherOwner), foreign, [], false));
        Assert.False(executions.RequestCancellation(OtherOwner, original.StreamId.DatabaseProfileId,
            original.StreamId.DatabaseProfileGeneration, original.AgentId!.Value, original.ChatSessionId!.Value));
        Assert.False(completion.IsCompleted);
        factory.Executor.Release.TrySetResult();
        await completion.WaitAsync(Timeout);
    }

    [Fact]
    public async Task Concurrent_send_is_rejected_without_disposing_the_original_execution() {
        var factory = new ScopeFactory();
        await using var executions = Create(factory);
        var original = new Activity();
        var completion = Send(executions, original);
        await factory.Executor.Started.Task.WaitAsync(Timeout);

        await Assert.ThrowsAsync<AgentChatAdmissionConflictException>(() => Send(executions, new Activity(original)));
        Assert.False(executions.TryDismiss(Owner, original.StreamId.DatabaseProfileId,
            original.StreamId.DatabaseProfileGeneration, original.AgentId!.Value, original.ChatSessionId!.Value));
        Assert.Equal(1, factory.Creations);
        Assert.Equal(0, factory.Disposals);
        factory.Executor.Release.TrySetResult();
        await completion.WaitAsync(Timeout);
    }

    [Fact]
    public async Task Approval_after_reconnect_uses_the_original_execution_scope() {
        var factory = new ScopeFactory();
        factory.Executor.WaitForApproval = true;
        factory.Executor.Release.TrySetResult();
        await using var executions = Create(factory);
        var original = new Activity();
        await Send(executions, original).WaitAsync(Timeout);

        Assert.Equal(0, factory.Disposals);
        Assert.Equal(ActiveAgentChatRunState.AwaitingApproval, Assert.Single(executions.Snapshot(Owner,
            original.StreamId.DatabaseProfileId, original.StreamId.DatabaseProfileGeneration)).RunState);
        Assert.False(executions.TryDismiss(Owner, original.StreamId.DatabaseProfileId,
            original.StreamId.DatabaseProfileGeneration, original.AgentId!.Value, original.ChatSessionId!.Value));
        await Assert.ThrowsAsync<AgentChatAdmissionConflictException>(() => Send(executions, new Activity(original)));
        await executions.ApproveAsync(new(Owner), new Activity(original), [], false).WaitAsync(Timeout);

        Assert.Equal(1, factory.Executor.Approvals);
        Assert.Equal(1, factory.Creations);
        Assert.Equal(1, factory.Disposals);
    }

    [Fact]
    public async Task Profile_generation_filters_prevent_discovery_and_cancellation_of_another_lifetime() {
        var factory = new ScopeFactory();
        await using var executions = Create(factory);
        var operation = new Activity();
        var completion = Send(executions, operation);
        await factory.Executor.Started.Task.WaitAsync(Timeout);

        var newerGeneration = new DatabaseProfileGeneration(operation.StreamId.DatabaseProfileGeneration.Value + 1);
        Assert.Empty(executions.Snapshot(Owner, operation.StreamId.DatabaseProfileId, newerGeneration));
        Assert.False(executions.RequestCancellation(Owner, operation.StreamId.DatabaseProfileId, newerGeneration,
            operation.AgentId!.Value, operation.ChatSessionId!.Value));
        factory.Executor.Release.TrySetResult();
        await completion.WaitAsync(Timeout);
    }

    [Fact]
    public async Task Slow_terminal_cleanup_keeps_the_chat_running_and_conflicting_prompt_definitely_unsent() {
        var factory = new ScopeFactory { HoldDisposal = true };
        factory.Executor.Release.TrySetResult();
        await using var executions = Create(factory);
        var original = new Activity();
        var completion = Send(executions, original);
        await factory.Disposed.Task.WaitAsync(Timeout);
        try {
            var snapshot = Assert.Single(executions.Snapshot(Owner,
                original.StreamId.DatabaseProfileId, original.StreamId.DatabaseProfileGeneration));
            Assert.Equal(ActiveAgentChatRunState.Running, snapshot.RunState);
            Assert.Null(snapshot.CompletionNotification);
            Assert.False(completion.IsCompleted);
            var conflict = await Assert.ThrowsAsync<AgentChatAdmissionConflictException>(() => Send(executions, new Activity(original)));
            Assert.Equal(original.AgentId, conflict.AgentId);
            Assert.Equal(original.ChatSessionId, conflict.ChatSessionId);

            var submissions = new AgentChatSubmissionRetention();
            Assert.True(submissions.TryReserve(original.AgentId!.Value, original.ChatSessionId, null,
                original.StreamId.DatabaseProfileGeneration, "Kept draft", "Kept draft", [], out var submission));
            submissions.RecordFailure(submission!.Id, conflict);
            Assert.Equal(AgentChatSubmissionState.NotSent, Assert.Single(submissions.Snapshot()).State);
        } finally {
            factory.ReleaseDisposal.TrySetResult();
        }
        await completion.WaitAsync(Timeout);
        Assert.Equal(ActiveAgentChatRunState.Idle, Assert.Single(executions.Snapshot(Owner,
            original.StreamId.DatabaseProfileId, original.StreamId.DatabaseProfileGeneration)).RunState);
    }

    [Fact]
    public async Task A_detached_new_turn_clears_the_previous_return_location() {
        var factory = new ScopeFactory();
        factory.Executor.Release.TrySetResult();
        await using var executions = Create(factory);
        var original = new Activity();
        var context = new AgentChatContextSnapshot(new AgentChatContextScope(
            AgentChatContextScopeId.Create(),
            new AgentChatContextSource(new("fixture"), new("source")),
            "Original source",
            accessMode: AgentChatContextScopeAccessMode.Unrestricted,
            surfacePosition: new AgentChatSurfacePosition("workspace", "agents", "catalog", "/agents")),
            [], 1, DateTimeOffset.UtcNow);
        await executions.SendAsync(new(Owner), original,
            new(original.AgentId!.Value, original.ChatSessionId, "Use this source."), new(context, null))
            .WaitAsync(Timeout);
        Assert.NotNull(Assert.Single(executions.Snapshot(Owner,
            original.StreamId.DatabaseProfileId, original.StreamId.DatabaseProfileGeneration)).ReturnLocation);

        await Send(executions, new Activity(original)).WaitAsync(Timeout);

        Assert.Null(Assert.Single(executions.Snapshot(Owner,
            original.StreamId.DatabaseProfileId, original.StreamId.DatabaseProfileGeneration)).ReturnLocation);
    }

    [Fact]
    public async Task Rejected_approval_preserves_the_original_scope_for_a_valid_retry() {
        var factory = new ScopeFactory();
        factory.Executor.WaitForApproval = true;
        factory.Executor.Release.TrySetResult();
        await using var executions = Create(factory);
        var original = new Activity();
        await Send(executions, original).WaitAsync(Timeout);

        factory.Executor.RejectApproval = true;
        await Assert.ThrowsAsync<InvalidOperationException>(() => executions.ApproveAsync(
            new(Owner), new Activity(original), [], false));
        Assert.Equal(0, factory.Disposals);
        Assert.Equal(ActiveAgentChatRunState.AwaitingApproval, Assert.Single(executions.Snapshot(Owner,
            original.StreamId.DatabaseProfileId, original.StreamId.DatabaseProfileGeneration)).RunState);

        factory.Executor.RejectApproval = false;
        await executions.ApproveAsync(new(Owner), new Activity(original), [], false).WaitAsync(Timeout);
        Assert.Equal(1, factory.Creations);
        Assert.Equal(1, factory.Disposals);
    }

    [Fact]
    public async Task Cancelled_approval_does_not_leave_the_retained_execution_scope_with_a_cancelled_token() {
        var factory = new ScopeFactory();
        factory.Executor.WaitForApproval = true;
        factory.Executor.HoldApproval = true;
        factory.Executor.Release.TrySetResult();
        await using var executions = Create(factory);
        var original = new Activity();
        await Send(executions, original).WaitAsync(Timeout);
        var approval = executions.ApproveAsync(new(Owner), new Activity(original), [], false);
        await factory.Executor.ApprovalStarted.Task.WaitAsync(Timeout);

        Assert.True(executions.RequestCancellation(Owner, original.StreamId.DatabaseProfileId,
            original.StreamId.DatabaseProfileGeneration, original.AgentId!.Value, original.ChatSessionId!.Value));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => approval.WaitAsync(Timeout));
        Assert.Equal(0, factory.Disposals);

        factory.Executor.HoldApproval = false;
        await executions.ApproveAsync(new(Owner), new Activity(original), [], false).WaitAsync(Timeout);
        Assert.Equal(1, factory.Creations);
        Assert.Equal(1, factory.Disposals);
    }

    [Fact]
    public async Task Execution_does_not_inherit_the_submitting_views_ambient_context() {
        var ambient = new AsyncLocal<string?> { Value = "retired-circuit" };
        var factory = new ScopeFactory();
        factory.Executor.ReadAmbient = () => ambient.Value;
        factory.Executor.Release.TrySetResult();
        await using var executions = Create(factory);

        await Send(executions, new Activity()).WaitAsync(Timeout);

        Assert.Null(factory.Executor.ObservedAmbient);
        Assert.Equal("retired-circuit", ambient.Value);
    }

    [Fact]
    public async Task Profile_switch_releases_waiting_scopes_and_rejects_queued_old_generation_work() {
        var factory = new ScopeFactory();
        factory.Executor.WaitForApproval = true;
        factory.Executor.Release.TrySetResult();
        var notifications = new DatabaseSwitchNotificationService();
        await using var executions = new AgentChatBackgroundExecution(factory, TimeProvider.System,
            NullLogger<AgentChatBackgroundExecution>.Instance, notifications);
        await executions.StartAsync(CancellationToken.None);
        var original = new Activity();
        await Send(executions, original).WaitAsync(Timeout);

        notifications.Publish(new(original.StreamId.DatabaseProfileId, "old-profile", Guid.NewGuid(), "new-profile", 2));
        await factory.Disposed.Task.WaitAsync(Timeout);

        Assert.Equal(1, factory.Disposals);
        Assert.Empty(executions.Snapshot(Owner, original.StreamId.DatabaseProfileId, original.StreamId.DatabaseProfileGeneration));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Send(executions, new Activity(original)));
        Assert.Equal(1, factory.Creations);
    }

    [Fact]
    public async Task Explicit_cancellation_stops_only_the_owned_operation_and_cleans_up() {
        var factory = new ScopeFactory();
        await using var executions = Create(factory);
        var operation = new Activity();
        var completion = Send(executions, operation);
        await factory.Executor.Started.Task.WaitAsync(Timeout);

        Assert.True(executions.RequestCancellation(Owner, operation.StreamId.DatabaseProfileId,
            operation.StreamId.DatabaseProfileGeneration, operation.AgentId!.Value, operation.ChatSessionId!.Value));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => completion.WaitAsync(Timeout));
        Assert.Equal(1, factory.Disposals);
        Assert.True(operation.IsTerminal);
    }

    [Fact]
    public async Task Host_shutdown_observes_active_execution_and_rejects_new_admission() {
        var factory = new ScopeFactory();
        await using var executions = Create(factory);
        var operation = new Activity();
        var completion = Send(executions, operation);
        await factory.Executor.Started.Task.WaitAsync(Timeout);

        await executions.StopAsync(CancellationToken.None).WaitAsync(Timeout);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => completion);
        Assert.Equal(1, factory.Disposals);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => Send(executions, new Activity()));
    }

    [Fact]
    public async Task Host_shutdown_releases_an_inactive_scope_retained_for_approval() {
        var factory = new ScopeFactory();
        factory.Executor.WaitForApproval = true;
        factory.Executor.Release.TrySetResult();
        await using var executions = Create(factory);
        await Send(executions, new Activity()).WaitAsync(Timeout);
        Assert.Equal(0, factory.Disposals);

        await executions.StopAsync(CancellationToken.None).WaitAsync(Timeout);
        await executions.StopAsync(CancellationToken.None).WaitAsync(Timeout);

        Assert.Equal(1, factory.Disposals);
    }

    [Fact]
    public async Task Pending_conversations_are_not_evicted_to_admit_more_work() {
        var factory = new ScopeFactory();
        factory.Executor.WaitForApproval = true;
        factory.Executor.Release.TrySetResult();
        await using var executions = Create(factory);
        for (var index = 0; index < AgentChatBackgroundExecution.MaximumOwnerConversations; index++) {
            await Send(executions, new Activity()).WaitAsync(Timeout);
        }

        await Assert.ThrowsAsync<InvalidOperationException>(() => Send(executions, new Activity()));

        Assert.Equal(AgentChatBackgroundExecution.MaximumOwnerConversations, factory.Creations);
        Assert.Equal(0, factory.Disposals);
    }

    [Fact]
    public async Task Failed_scope_construction_disposes_async_only_dependencies_and_preserves_the_failure() {
        var resource = new AsyncResource();
        var failure = new InvalidOperationException("Executor construction failed.");
        var services = new ServiceCollection();
        services.AddScoped(_ => resource);
        services.AddScoped<IAgentChatOperationExecutor>(provider => {
            _ = provider.GetRequiredService<AsyncResource>();
            throw failure;
        });
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        var factory = new AgentChatExecutionScopeFactory(provider.GetRequiredService<IServiceScopeFactory>());

        var observed = await Assert.ThrowsAsync<InvalidOperationException>(() => factory.CreateAsync().AsTask());

        Assert.Same(failure, observed);
        Assert.Equal(1, resource.Disposals);
    }

    [Fact]
    public async Task Broken_view_observer_cannot_fail_the_execution() {
        var factory = new ScopeFactory();
        factory.Executor.Release.TrySetResult();
        await using var executions = Create(factory);
        executions.Changed += (_, _) => throw new ObjectDisposedException("retired-view");

        var result = await Send(executions, new Activity()).WaitAsync(Timeout);

        Assert.Equal(ExecutionState.Completed, result.State);
        Assert.Equal(1, factory.Disposals);
    }

    private static AgentChatBackgroundExecution Create(ScopeFactory factory)
        => new(factory, TimeProvider.System, NullLogger<AgentChatBackgroundExecution>.Instance);

    private static Task<AgentChatRunResult> Send(AgentChatBackgroundExecution executions, Activity operation)
        => executions.SendAsync(new(Owner), operation,
            new(operation.AgentId!.Value, operation.ChatSessionId, "Continue the bounded task."), new(null, null));

    private sealed class ScopeFactory : IAgentChatExecutionScopeFactory {
        public Executor Executor { get; } = new();
        public int Creations { get; private set; }
        public int Disposals { get; private set; }
        public bool HoldDisposal { get; set; }
        public TaskCompletionSource ReleaseDisposal { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Disposed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public ValueTask<IAgentChatExecutionScope> CreateAsync() {
            Creations++;
            return ValueTask.FromResult<IAgentChatExecutionScope>(new Scope(Executor, async () => {
                Disposals++;
                Disposed.TrySetResult();
                if (HoldDisposal) {
                    await ReleaseDisposal.Task;
                }
            }));
        }
    }

    private sealed class Scope(IAgentChatOperationExecutor executor, Func<ValueTask> disposed) : IAgentChatExecutionScope {
        public IAgentChatOperationExecutor Executor { get; } = executor;
        public ValueTask DisposeAsync() => disposed();
    }

    private sealed class AsyncResource : IAsyncDisposable {
        public int Disposals { get; private set; }
        public ValueTask DisposeAsync() {
            Disposals++;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class Executor : IAgentChatOperationExecutor {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ApprovalStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ApprovalRelease { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool WaitForApproval { get; set; }
        public bool RejectApproval { get; set; }
        public bool HoldApproval { get; set; }
        public Func<string?>? ReadAmbient { get; set; }
        public string? ObservedAmbient { get; private set; }
        public int Approvals { get; private set; }
        public AgentTurnContextCaptureResult? LastCapture => null;

        public async Task<AgentChatRunResult> SendAsync(IAgentExecutionActivityOperationLease operation,
            AgentChatSendRequest request, AgentTurnContextObservation? observation, CancellationToken cancellationToken) {
            ObservedAmbient = ReadAmbient?.Invoke();
            operation.BindExecutionRun(Guid.NewGuid(), request.ChatSessionId);
            Started.TrySetResult();
            await Release.Task.WaitAsync(cancellationToken);
            if (WaitForApproval) {
                operation.Report(AgentExecutionActivityPhase.AwaitingApproval, "Approval required.");
                operation.Suspend("Waiting for approval.");
            } else {
                operation.Complete("Finished.");
            }
            return Result(operation, WaitForApproval ? ExecutionState.WaitingOnTool : ExecutionState.Completed);
        }

        public async Task<AgentChatRunResult> ApproveAsync(IAgentExecutionActivityOperationLease operation, Guid agentId,
            Guid sessionId, IReadOnlyList<PendingToolApprovalDecision> decisions, bool autoApprove, CancellationToken cancellationToken) {
            if (RejectApproval) {
                throw new InvalidOperationException("The original approval proposal changed.");
            }
            ApprovalStarted.TrySetResult();
            if (HoldApproval) {
                await ApprovalRelease.Task.WaitAsync(cancellationToken);
            }
            Approvals++;
            operation.BindExecutionRun(Guid.NewGuid(), sessionId);
            operation.Complete("Approved original operation.");
            return Result(operation, ExecutionState.Completed);
        }

        public Task<ExecutionRunResult> RecoverAsync(IAgentExecutionActivityOperationLease operation, Guid runId,
            CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ExecutionRunDetail> PlanAsync(IAgentExecutionActivityOperationLease operation, AgentPlanningActionRequest request,
            CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ExecutionRunDetail> CancelPendingAsync(IAgentExecutionActivityOperationLease operation, Guid runId,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        private static AgentChatRunResult Result(IAgentExecutionActivityOperationLease operation, ExecutionState state) {
            var now = DateTimeOffset.UtcNow;
            return new(operation.ChatSessionId!.Value,
                new ChatMessageRecord(Guid.NewGuid(), ChatMessageRole.Assistant, "Finished.", now, TokenEstimate: 1),
                new AgentRunMetric(Guid.NewGuid(), operation.AgentId!.Value, operation.ChatSessionId!.Value, now,
                    RunOutcome.Succeeded, "fixture", "fixture", DurationMs: 1, InputTokens: 1, OutputTokens: 1, ToolCalls: 0)) {
                ExecutionRunId = operation.ExecutionRunId!.Value,
                State = state
            };
        }
    }

    private sealed class Activity : IAgentExecutionActivityOperationLease {
        public Activity() {
            var profile = Guid.NewGuid();
            StreamId = new(profile, WorkspaceScopeDescriptor.Organization(profile.ToString("N")),
                new DatabaseProfileGeneration(1), AgentExecutionOperationId.New());
            AgentId = Guid.NewGuid();
            ChatSessionId = Guid.NewGuid();
        }

        public Activity(Activity original) {
            StreamId = new(original.StreamId.DatabaseProfileId, original.StreamId.WorkspaceScope,
                original.StreamId.DatabaseProfileGeneration, AgentExecutionOperationId.New());
            AgentId = original.AgentId;
            ChatSessionId = original.ChatSessionId;
        }

        public AgentExecutionActivityStreamId StreamId { get; }
        public Guid? AgentId { get; private set; }
        public Guid? ChatSessionId { get; private set; }
        public Guid? ExecutionRunId { get; private set; }
        public bool IsTerminal { get; private set; }
        public void BindAgent(Guid agentId) => AgentId = agentId;
        public void BindContext(AgentChatContextSource source, long version) { }
        public void BindChatSession(Guid sessionId) => ChatSessionId = sessionId;
        public void BindExecutionRun(Guid runId, Guid? sessionId = null) {
            ExecutionRunId = runId;
            ChatSessionId = sessionId ?? ChatSessionId;
        }
        public void Report(AgentExecutionActivityPhase phase, string message) { }
        public void Complete(string message) => IsTerminal = true;
        public void Fail(string message, string? errorCode = null) => IsTerminal = true;
        public void Cancel(string message) => IsTerminal = true;
        public void Suspend(string message) => IsTerminal = true;
        public void Dispose() { }
    }
}
