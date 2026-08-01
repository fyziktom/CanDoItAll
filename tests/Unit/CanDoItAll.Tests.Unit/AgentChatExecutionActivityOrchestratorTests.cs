using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.SharedKernel.Streaming;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Unit;

public sealed class AgentChatExecutionActivityOrchestratorTests
{
    [Fact]
    public async Task StartSendMessage_returns_before_context_capture_and_exposes_initial_activity()
    {
        var context = CreateContext(blockContextCapture: true);
        var sessionId = context.Workspace.SessionId;

        var start = Task.Run(
            () => context.Orchestrator.StartSendMessage(
                context.AgentId,
                sessionId,
                "Explain the current selection."));

        await context.ContextRegistry.CaptureStarted.WaitAsync(
            TestContext.DefaultTimeout);
        Assert.True(start.IsCompleted);
        var handle = await start.WaitAsync(TestContext.DefaultTimeout);
        Assert.False(handle.Completion.IsCompleted);

        await using (var reader = context.Coordinator.OpenReader(
                         handle.StreamId,
                         StreamSequence.Beginning))
        {
            var initial = Assert.IsType<
                SequencedStreamEvents<AgentExecutionActivity>>(
                await reader.ReadAsync());
            Assert.Equal(
                new[]
                {
                    AgentExecutionActivityPhase.Accepted,
                    AgentExecutionActivityPhase.CapturingContext
                },
                initial.Items.Select(item => item.Event.Phase));
        }

        await using (var cancelledReader = context.Coordinator.OpenReader(
                         handle.StreamId,
                         new StreamSequence(3)))
        {
            using var cancellation = new CancellationTokenSource();
            var pendingRead = cancelledReader
                .ReadAsync(cancellation.Token)
                .AsTask();
            Assert.False(pendingRead.IsCompleted);

            cancellation.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => pendingRead);
        }

        Assert.False(handle.Completion.IsCompleted);
        context.ContextRegistry.ReleaseCapture();

        var result = await handle.Completion.WaitAsync(
            TestContext.DefaultTimeout);
        Assert.Same(context.Workspace.SendResult, result);
        var events = await ReadEventsAsync(context.Coordinator, handle.StreamId);
        Assert.Equal(
            new[]
            {
                AgentExecutionActivityPhase.Accepted,
                AgentExecutionActivityPhase.CapturingContext,
                AgentExecutionActivityPhase.PreparingInput,
                AgentExecutionActivityPhase.PreparingInput,
                AgentExecutionActivityPhase.ResolvingPreparation,
                AgentExecutionActivityPhase.PersistingResult,
                AgentExecutionActivityPhase.Completed
            },
            events.Select(item => item.Event.Phase));
        var prepared = Assert.Single(
            events,
            item => item.Event.Phase ==
                AgentExecutionActivityPhase.ResolvingPreparation);
        Assert.Equal(
            context.ContextRegistry.Snapshot.Scope.Source,
            prepared.Event.Context?.Source);
        Assert.Equal(
            context.ContextRegistry.Snapshot.Version,
            prepared.Event.Context?.Version);
    }

    [Fact]
    public async Task SendMessageAsync_returns_the_started_operation_completion()
    {
        var context = CreateContext();

        var result = await context.Orchestrator.SendMessageAsync(
            context.AgentId,
            context.Workspace.SessionId,
            "Continue.");

        Assert.Same(context.Workspace.SendResult, result);
        var call = Assert.Single(context.Workspace.SendCalls);
        Assert.Equal(
            call.Operation.StreamId.OperationId,
            call.Options.InitialActivityOperationId);
        Assert.True(call.Operation.IsTerminal);
    }

    [Fact]
    public async Task Typed_send_request_preserves_execution_behavior_and_freezes_attachments()
    {
        var context = CreateContext(blockContextCapture: true);
        var attachmentPaths = new List<string>
        {
            "evidence/first.md"
        };
        var request = new AgentChatSendRequest(
            context.AgentId,
            context.Workspace.SessionId,
            "Inspect the selected run.")
        {
            AttachmentPaths = attachmentPaths,
            Behavior = new AgentChatExecutionBehavior(
                RuntimeToolProvidersEnabled: false,
                WorkspaceToolsEnabled: false,
                ToolCapabilitiesEnabled: false)
        };

        var handle = context.Orchestrator.StartSendMessage(request);
        await context.ContextRegistry.CaptureStarted.WaitAsync(
            TestContext.DefaultTimeout);
        attachmentPaths.Add("evidence/late.md");
        context.ContextRegistry.ReleaseCapture();

        await handle.Completion.WaitAsync(TestContext.DefaultTimeout);
        var call = Assert.Single(context.Workspace.SendCalls);
        Assert.Equal(request.Prompt, call.Prompt);
        Assert.False(call.Options.RuntimeToolProvidersEnabled);
        Assert.False(call.Options.WorkspaceToolsEnabled);
        Assert.False(call.Options.ToolCapabilitiesEnabled);
        Assert.Equal(
            new[] { "evidence/first.md" },
            call.AttachmentPaths);
    }

    [Fact]
    public async Task Context_capture_failure_terminalizes_the_activity_as_failed()
    {
        var context = CreateContext();
        var failure = new InvalidOperationException("Context capture failed.");
        context.ContextRegistry.FailCapture(failure);

        var handle = context.Orchestrator.StartSendMessage(
            context.AgentId,
            context.Workspace.SessionId,
            "Explain.");

        var observed = await Assert.ThrowsAsync<InvalidOperationException>(
            () => handle.Completion);
        Assert.Same(failure, observed);
        Assert.Empty(context.Workspace.SendCalls);

        var events = await ReadEventsAsync(context.Coordinator, handle.StreamId);
        Assert.Equal(
            new[]
            {
                AgentExecutionActivityPhase.Accepted,
                AgentExecutionActivityPhase.CapturingContext,
                AgentExecutionActivityPhase.Failed
            },
            events.Select(item => item.Event.Phase));
        Assert.Equal(
            AgentExecutionActivityTerminalOutcome.Failed,
            events[^1].Event.TerminalOutcome);
        Assert.Equal(
            AgentExecutionActivityFailureCodes.UnhandledExecutionFailure,
            events[^1].Event.ErrorCode);
    }

    [Fact]
    public async Task Approval_continuation_uses_a_distinct_operation_bound_to_the_same_run()
    {
        var context = CreateContext();

        var send = context.Orchestrator.StartSendMessage(
            context.AgentId,
            context.Workspace.SessionId,
            "Use the protected tool.");
        await send.Completion;

        var approval = context.Orchestrator.StartApprovalContinuation(
            context.AgentId,
            context.Workspace.SessionId,
            approved: true);
        await approval.Completion;

        Assert.NotEqual(send.StreamId.OperationId, approval.StreamId.OperationId);
        Assert.Equal(send.StreamId.DatabaseProfileId, approval.StreamId.DatabaseProfileId);
        Assert.Equal(send.StreamId.WorkspaceScope, approval.StreamId.WorkspaceScope);

        var sendCall = Assert.Single(context.Workspace.SendCalls);
        var approvalCall = Assert.Single(context.Workspace.ApprovalCalls);
        Assert.Equal(send.StreamId, sendCall.Operation.StreamId);
        Assert.Equal(approval.StreamId, approvalCall.Operation.StreamId);
        Assert.Equal(
            context.Workspace.SessionId,
            approvalCall.Operation.ChatSessionId);
        Assert.Equal(
            context.Workspace.ExecutionRunId,
            approvalCall.Operation.ExecutionRunId);

        var sendEvents = await ReadEventsAsync(
            context.Coordinator,
            send.StreamId);
        var approvalEvents = await ReadEventsAsync(
            context.Coordinator,
            approval.StreamId);
        Assert.Equal(
            new[]
            {
                AgentExecutionActivityPhase.Accepted,
                AgentExecutionActivityPhase.ResolvingSession,
                AgentExecutionActivityPhase.ResolvingSession,
                AgentExecutionActivityPhase.ResolvingPreparation,
                AgentExecutionActivityPhase.PersistingResult,
                AgentExecutionActivityPhase.Completed
            },
            approvalEvents.Select(item => item.Event.Phase));
        var sendTerminal = sendEvents[^1].Event;
        var approvalTerminal = approvalEvents[^1].Event;
        Assert.Equal(sendTerminal.ChatSessionId, approvalTerminal.ChatSessionId);
        Assert.Equal(sendTerminal.ExecutionRunId, approvalTerminal.ExecutionRunId);
        Assert.Equal(context.Workspace.SessionId, approvalTerminal.ChatSessionId);
        Assert.Equal(context.Workspace.ExecutionRunId, approvalTerminal.ExecutionRunId);
    }

    private static TestContext CreateContext(bool blockContextCapture = false)
    {
        var timeProvider = TimeProvider.System;
        var stream = new PartitionedSequencedStream<
            AgentExecutionActivityStreamId,
            AgentExecutionActivity>(
            new PartitionedSequencedStreamPolicy(
                maxPartitions: 16,
                maxEventsPerPartition: 32,
                maxTerminalPartitions: 16,
                terminalRetention: TimeSpan.FromMinutes(5),
                maxTombstones: 16,
                tombstoneRetention: TimeSpan.FromMinutes(5)),
            timeProvider);
        var coordinator = new AgentExecutionActivityCoordinator(
            stream,
            timeProvider);
        var profileId = Guid.NewGuid();
        var workspaceScope = WorkspaceScopeDescriptor.Organization(
            profileId.ToString("N"));
        var workspace = new ActivityWorkspaceExecutionService(
            Guid.NewGuid(),
            Guid.NewGuid());
        var snapshot = new AgentChatContextSnapshot(
            new AgentChatContextScope(
                AgentChatContextScopeId.Create(),
                new AgentChatContextSource(
                    new AgentChatContextSourceKind("test-surface"),
                    new AgentChatContextSourceId("current-selection")),
                "Current selection",
                accessMode: AgentChatContextScopeAccessMode.Unrestricted),
            [],
            Version: 23,
            CapturedAtUtc: DateTimeOffset.UtcNow);
        var contextRegistry = new ControllableAgentChatContextRegistry(
            blockContextCapture,
            snapshot);
        var orchestrator = new AgentChatExecutionOrchestrator(
            workspace,
            contextRegistry,
            new AgentChatExecutionNotificationHub(
                NullLogger<AgentChatExecutionNotificationHub>.Instance),
            coordinator,
            new TestWorkspaceFactory(workspaceScope),
            new TestDatabaseProfileRuntimeAccessor(profileId),
            new FixedAgentExecutionProfileGenerationSource(
                new DatabaseProfileGeneration(0)),
            timeProvider);
        return new TestContext(
            orchestrator,
            coordinator,
            contextRegistry,
            workspace,
            Guid.NewGuid());
    }

    private static async Task<IReadOnlyList<
        SequencedStreamEnvelope<AgentExecutionActivity>>> ReadEventsAsync(
        AgentExecutionActivityCoordinator coordinator,
        AgentExecutionActivityStreamId streamId)
    {
        await using var reader = coordinator.OpenReader(
            streamId,
            StreamSequence.Beginning);
        var result = Assert.IsType<SequencedStreamEvents<AgentExecutionActivity>>(
            await reader.ReadAsync());
        return result.Items;
    }

    private sealed record TestContext(
        AgentChatExecutionOrchestrator Orchestrator,
        AgentExecutionActivityCoordinator Coordinator,
        ControllableAgentChatContextRegistry ContextRegistry,
        ActivityWorkspaceExecutionService Workspace,
        Guid AgentId)
    {
        public static TimeSpan DefaultTimeout { get; } = TimeSpan.FromSeconds(10);
    }

    private sealed class ControllableAgentChatContextRegistry :
        IAgentChatContextRegistry
    {
        private readonly TaskCompletionSource<bool> captureStarted = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> captureRelease = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private Exception? captureFailure;

        public ControllableAgentChatContextRegistry(
            bool blockCapture,
            AgentChatContextSnapshot snapshot)
        {
            Snapshot = snapshot;
            if (!blockCapture)
            {
                captureRelease.TrySetResult(true);
            }
        }

        public AgentChatContextSnapshot Snapshot { get; }

        public event EventHandler? Changed
        {
            add
            {
            }
            remove
            {
            }
        }

        public Task CaptureStarted => captureStarted.Task;

        public IAgentChatWorkspacePositionLease RegisterWorkspacePosition(
            AgentChatWorkspacePosition position,
            AgentChatNavigationIdentity navigationIdentity)
        {
            throw new NotSupportedException();
        }

        public IAgentChatContextScopeLease ActivateScope(AgentChatContextScope scope)
        {
            throw new NotSupportedException();
        }

        public IAgentChatContextFragmentLease RegisterFragment(
            AgentChatContextScopeId scopeId,
            AgentChatContextFragment fragment)
        {
            throw new NotSupportedException();
        }

        public AgentChatContextSnapshot? Capture()
        {
            return Snapshot;
        }

        public async ValueTask<AgentChatContextSnapshot?> CaptureAsync(
            CancellationToken cancellationToken = default)
        {
            captureStarted.TrySetResult(true);
            await captureRelease.Task.WaitAsync(cancellationToken);
            if (captureFailure is not null)
            {
                throw captureFailure;
            }

            return Snapshot;
        }

        public void ReleaseCapture()
        {
            captureRelease.TrySetResult(true);
        }

        public void FailCapture(Exception exception)
        {
            ArgumentNullException.ThrowIfNull(exception);
            captureFailure = exception;
            ReleaseCapture();
        }
    }

    private sealed class ActivityWorkspaceExecutionService(
        Guid sessionId,
        Guid executionRunId) : IAgentFrameworkWorkspaceActivityExecutionService
    {
        public Guid SessionId { get; } = sessionId;

        public Guid ExecutionRunId { get; } = executionRunId;

        public AgentChatRunResult SendResult { get; } = CreateRunResult(
            sessionId,
            executionRunId);

        public AgentChatRunResult ApprovalResult { get; } = CreateRunResult(
            sessionId,
            executionRunId);

        public List<SendCall> SendCalls { get; } = [];

        public List<ApprovalCall> ApprovalCalls { get; } = [];

        public Task<ExecutionRunResult> ExecuteRunWithinOperationAsync(
            IAgentExecutionActivityOperationLease operation,
            ExecutionRunRequest request,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<ExecutionRunSourceExecutionResult> ExecuteSameSourceRunWithinOperationAsync(
            IAgentExecutionActivityOperationLease operation,
            ExecutionRunSourceKey source,
            ExecutionRunRequest request,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<ExecutionRunResult> ContinueExecutionRunWithinOperationAsync(
            IAgentExecutionActivityOperationLease operation,
            Guid executionRunId,
            bool approved,
            bool autoApprovePendingToolCalls = false,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<AgentChatRunResult> SendMessageWithinOperationAsync(
            IAgentExecutionActivityOperationLease operation,
            Guid agentId,
            Guid? chatSessionId,
            string prompt,
            AgentChatRunOptions options,
            CancellationToken cancellationToken = default,
            IReadOnlyList<string>? attachmentPaths = null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SendCalls.Add(new SendCall(
                operation,
                prompt,
                options,
                attachmentPaths));
            operation.Report(
                AgentExecutionActivityPhase.PreparingInput,
                "Preparing test input.");
            operation.Report(
                AgentExecutionActivityPhase.ResolvingPreparation,
                "Resolving test preparation.");
            BindAndComplete(operation);
            return Task.FromResult(SendResult);
        }

        public Task<AgentChatRunResult> RespondToPendingApprovalsWithinOperationAsync(
            IAgentExecutionActivityOperationLease operation,
            Guid agentId,
            Guid chatSessionId,
            bool approved,
            bool autoApprovePendingToolCalls = false,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ApprovalCalls.Add(new ApprovalCall(operation));
            operation.Report(
                AgentExecutionActivityPhase.ResolvingSession,
                "Loading the test execution run.");
            operation.Report(
                AgentExecutionActivityPhase.ResolvingPreparation,
                "Resolving test continuation preparation.");
            BindAndComplete(operation);
            return Task.FromResult(ApprovalResult);
        }

        private void BindAndComplete(
            IAgentExecutionActivityOperationLease operation)
        {
            if (!operation.ChatSessionId.HasValue)
            {
                operation.BindChatSession(SessionId);
            }

            operation.BindExecutionRun(ExecutionRunId, SessionId);
            operation.Report(
                AgentExecutionActivityPhase.PersistingResult,
                "Persisting test result.");
            operation.Complete("Test operation completed.");
        }
    }

    private sealed record SendCall(
        IAgentExecutionActivityOperationLease Operation,
        string Prompt,
        AgentChatRunOptions Options,
        IReadOnlyList<string>? AttachmentPaths);

    private sealed record ApprovalCall(
        IAgentExecutionActivityOperationLease Operation);

    private sealed class TestWorkspaceFactory(
        WorkspaceScopeDescriptor organizationScope)
        : ICanDoItAllAgentWorkspaceFactory
    {
        public IAgentFrameworkWorkspaceService GetOrganizationWorkspaceService()
        {
            throw new NotSupportedException();
        }

        public IAgentFrameworkWorkspaceService GetWorkspaceService(
            WorkspaceScopeDescriptor scope)
        {
            throw new NotSupportedException();
        }

        public WorkspaceScopeDescriptor GetOrganizationScope()
        {
            return organizationScope;
        }

        public string GetWorkspaceRoot()
        {
            throw new NotSupportedException();
        }
    }

    private sealed class TestDatabaseProfileRuntimeAccessor(Guid profileId)
        : IDatabaseProfileRuntimeAccessor
    {
        private readonly ResolvedDatabaseProfile resolvedProfile = new(
            new DatabaseProfileRecord
            {
                Id = profileId,
                DisplayName = "Test profile",
                ProviderKind = DatabaseProviderKind.InMemory,
                SourceKind = DatabaseProfileSourceKind.InMemory
            },
            DatabaseProfileResolutionSource.ExplicitOverride,
            "test");

        public ResolvedDatabaseProfile ResolveCurrentProfile()
        {
            return resolvedProfile;
        }

        public ResolvedDatabaseProfile ResolveProfile(Guid requestedProfileId)
        {
            if (requestedProfileId != profileId)
            {
                throw new KeyNotFoundException();
            }

            return resolvedProfile;
        }
    }

    private static AgentChatRunResult CreateRunResult(
        Guid sessionId,
        Guid executionRunId)
    {
        var agentId = Guid.NewGuid();
        return new AgentChatRunResult(
            sessionId,
            new ChatMessageRecord(
                Guid.NewGuid(),
                ChatMessageRole.Assistant,
                "Done",
                DateTimeOffset.UtcNow,
                TokenEstimate: 1),
            new AgentRunMetric(
                Guid.NewGuid(),
                agentId,
                sessionId,
                DateTimeOffset.UtcNow,
                RunOutcome.Succeeded,
                "test-provider",
                "test-model",
                DurationMs: 1,
                InputTokens: 1,
                OutputTokens: 1,
                ToolCalls: 0)
            {
                ExecutionRunId = executionRunId
            })
        {
            ExecutionRunId = executionRunId,
            State = ExecutionState.Completed
        };
    }
}
