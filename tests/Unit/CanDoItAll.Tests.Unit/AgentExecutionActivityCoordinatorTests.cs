using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.SharedKernel.Streaming;

namespace CanDoItAll.Tests.Unit;

public sealed class AgentExecutionActivityCoordinatorTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 7, 27, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Admit_operation_publishes_accepted_as_first_sequenced_event()
    {
        var context = CreateContext();
        var sessionId = Guid.NewGuid();

        using var operation = Admit(
            context,
            sessionId,
            "Operation accepted.");

        var duplicate = Assert.IsType<AgentExecutionActivityRejected>(
            context.Coordinator.AdmitOperation(
                context.StreamId,
                context.AgentId,
                sessionId,
                "Duplicate."));
        Assert.Equal(
            AgentExecutionActivityAdmissionRejectionReason.DuplicateOperation,
            duplicate.Reason);

        var events = await ReadEventsAsync(context);
        var envelope = Assert.Single(events);
        Assert.Equal(StreamSequence.First, envelope.Sequence);
        Assert.Equal(AgentExecutionActivityPhase.Accepted, envelope.Event.Phase);
        Assert.Equal(Now, envelope.Event.OccurredAtUtc);
        Assert.Equal(context.AgentId, envelope.Event.AgentId);
        Assert.Equal(sessionId, envelope.Event.ChatSessionId);
        Assert.Null(envelope.Event.ExecutionRunId);
        Assert.Equal("Operation accepted.", envelope.Event.Message);
        Assert.False(envelope.Event.IsTerminal);

        operation.Cancel("Test cleanup.");
    }

    [Fact]
    public async Task Operation_binds_agent_and_typed_context_after_unknown_acceptance()
    {
        var context = CreateContext();
        var source = new AgentChatContextSource(
            new AgentChatContextSourceKind("project-structure"),
            new AgentChatContextSourceId("project-42"));
        using var operation = Assert.IsType<AgentExecutionActivityAdmitted>(
            context.Coordinator.AdmitOperation(
                context.StreamId,
                agentId: null,
                chatSessionId: null,
                "Accepted."))
            .Operation;

        operation.BindAgent(context.AgentId);
        operation.BindContext(source, version: 17);
        operation.Report(
            AgentExecutionActivityPhase.ResolvingPreparation,
            "Resolving preparation.");

        var events = await ReadEventsAsync(context);
        Assert.Null(events[0].Event.AgentId);
        Assert.Null(events[0].Event.Context);
        Assert.Equal(context.AgentId, events[1].Event.AgentId);
        Assert.Equal(
            new AgentExecutionActivityContextIdentity(source, 17),
            events[1].Event.Context);
    }

    [Fact]
    public async Task Overlong_accepted_message_is_rejected_before_stream_admission()
    {
        var context = CreateContext();
        var overlongMessage = new string(
            'x',
            AgentExecutionActivityLimits.MaximumMessageLength + 1);

        Assert.Throws<ArgumentException>(
            () => context.Coordinator.AdmitOperation(
                context.StreamId,
                context.AgentId,
                chatSessionId: null,
                overlongMessage));

        using var operation = Admit(context, message: "Valid admission.");
        var accepted = Assert.Single(await ReadEventsAsync(context));
        Assert.Equal(AgentExecutionActivityPhase.Accepted, accepted.Event.Phase);
        Assert.Equal("Valid admission.", accepted.Event.Message);

        operation.Cancel("Test cleanup.");
    }

    [Fact]
    public async Task Report_allows_forward_progress_and_streaming_tool_cycles()
    {
        var context = CreateContext();

        using var operation = Admit(context);
        operation.Report(AgentExecutionActivityPhase.CapturingContext, "Capturing.");
        operation.Report(AgentExecutionActivityPhase.ResolvingSession, "Session.");
        operation.Report(AgentExecutionActivityPhase.PreparingInput, "Input.");
        operation.Report(AgentExecutionActivityPhase.ResolvingPreparation, "Preparing.");
        operation.Report(AgentExecutionActivityPhase.ResolvingProvider, "Provider.");
        operation.Report(AgentExecutionActivityPhase.CreatingExecution, "Execution.");
        operation.Report(AgentExecutionActivityPhase.PreparingCapabilities, "Capabilities.");
        operation.Report(AgentExecutionActivityPhase.PreparingRuntime, "Runtime.");
        operation.Report(AgentExecutionActivityPhase.WaitingForProvider, "Waiting.");
        operation.Report(AgentExecutionActivityPhase.Streaming, "Streaming.");
        operation.Report(AgentExecutionActivityPhase.UsingTool, "Using tool.");
        operation.Report(AgentExecutionActivityPhase.Streaming, "Streaming again.");
        operation.Report(AgentExecutionActivityPhase.UsingTool, "Using another tool.");
        operation.Report(AgentExecutionActivityPhase.Streaming, "Final stream.");
        operation.Report(AgentExecutionActivityPhase.PersistingResult, "Persisting.");
        operation.Complete("Completed.");

        var events = await ReadEventsAsync(context);
        Assert.Equal(
            new[]
            {
                AgentExecutionActivityPhase.Accepted,
                AgentExecutionActivityPhase.CapturingContext,
                AgentExecutionActivityPhase.ResolvingSession,
                AgentExecutionActivityPhase.PreparingInput,
                AgentExecutionActivityPhase.ResolvingPreparation,
                AgentExecutionActivityPhase.ResolvingProvider,
                AgentExecutionActivityPhase.CreatingExecution,
                AgentExecutionActivityPhase.PreparingCapabilities,
                AgentExecutionActivityPhase.PreparingRuntime,
                AgentExecutionActivityPhase.WaitingForProvider,
                AgentExecutionActivityPhase.Streaming,
                AgentExecutionActivityPhase.UsingTool,
                AgentExecutionActivityPhase.Streaming,
                AgentExecutionActivityPhase.UsingTool,
                AgentExecutionActivityPhase.Streaming,
                AgentExecutionActivityPhase.PersistingResult,
                AgentExecutionActivityPhase.Completed
            },
            events.Select(item => item.Event.Phase));
        Assert.Equal(
            Enumerable.Range(1, events.Count).Select(value => (long)value),
            events.Select(item => item.Sequence.Value));
        Assert.Equal(
            AgentExecutionActivityTerminalOutcome.Succeeded,
            events[^1].Event.TerminalOutcome);
    }

    [Fact]
    public async Task Report_allows_runtime_reentry_after_persisting_for_auto_approval()
    {
        var context = CreateContext();

        using var operation = Admit(context);
        operation.Report(
            AgentExecutionActivityPhase.PersistingResult,
            "Persisting approval state.");
        operation.Report(
            AgentExecutionActivityPhase.PreparingRuntime,
            "Resuming the runtime after auto-approval.");
        operation.Report(
            AgentExecutionActivityPhase.WaitingForProvider,
            "Waiting for the provider.");
        operation.Report(
            AgentExecutionActivityPhase.Streaming,
            "Streaming the continuation.");
        operation.Report(
            AgentExecutionActivityPhase.PersistingResult,
            "Persisting the completed result.");
        operation.Complete("Completed.");

        var events = await ReadEventsAsync(context);
        Assert.Equal(
            new[]
            {
                AgentExecutionActivityPhase.Accepted,
                AgentExecutionActivityPhase.PersistingResult,
                AgentExecutionActivityPhase.PreparingRuntime,
                AgentExecutionActivityPhase.WaitingForProvider,
                AgentExecutionActivityPhase.Streaming,
                AgentExecutionActivityPhase.PersistingResult,
                AgentExecutionActivityPhase.Completed
            },
            events.Select(item => item.Event.Phase));
    }

    [Fact]
    public async Task Report_allows_runtime_reentry_after_awaiting_approval_for_auto_approval()
    {
        var context = CreateContext();

        using var operation = Admit(context);
        operation.Report(
            AgentExecutionActivityPhase.AwaitingApproval,
            "Approval required.");
        operation.Report(
            AgentExecutionActivityPhase.PreparingRuntime,
            "Resuming the runtime after auto-approval.");
        operation.Report(
            AgentExecutionActivityPhase.WaitingForProvider,
            "Waiting for the provider.");
        operation.Report(
            AgentExecutionActivityPhase.Streaming,
            "Streaming the continuation.");
        operation.Report(
            AgentExecutionActivityPhase.PersistingResult,
            "Persisting the completed result.");
        operation.Complete("Completed.");

        var events = await ReadEventsAsync(context);
        Assert.Equal(
            new[]
            {
                AgentExecutionActivityPhase.Accepted,
                AgentExecutionActivityPhase.AwaitingApproval,
                AgentExecutionActivityPhase.PreparingRuntime,
                AgentExecutionActivityPhase.WaitingForProvider,
                AgentExecutionActivityPhase.Streaming,
                AgentExecutionActivityPhase.PersistingResult,
                AgentExecutionActivityPhase.Completed
            },
            events.Select(item => item.Event.Phase));
    }

    [Fact]
    public async Task Report_allows_native_approval_wait_after_session_persistence()
    {
        var context = CreateContext();

        using var operation = Admit(context);
        operation.Report(
            AgentExecutionActivityPhase.PersistingResult,
            "Persisting native approval binding state.");
        operation.Report(
            AgentExecutionActivityPhase.AwaitingApproval,
            "Approval required.");
        operation.Suspend("Operation suspended for approval.");

        var events = await ReadEventsAsync(context);
        Assert.Equal(
            new[]
            {
                AgentExecutionActivityPhase.Accepted,
                AgentExecutionActivityPhase.PersistingResult,
                AgentExecutionActivityPhase.AwaitingApproval,
                AgentExecutionActivityPhase.AwaitingApproval
            },
            events.Select(item => item.Event.Phase));
        Assert.Equal(
            AgentExecutionActivityTerminalOutcome.Suspended,
            events[^1].Event.TerminalOutcome);
    }

    [Fact]
    public async Task Report_rejects_backward_transition_without_appending_it()
    {
        var context = CreateContext();

        using var operation = Admit(context);
        operation.Report(
            AgentExecutionActivityPhase.PersistingResult,
            "Persisting.");

        Assert.Throws<InvalidOperationException>(
            () => operation.Report(
                AgentExecutionActivityPhase.PreparingInput,
                "Too late."));

        operation.Complete("Completed.");
        var events = await ReadEventsAsync(context);
        Assert.Equal(
            new[]
            {
                AgentExecutionActivityPhase.Accepted,
                AgentExecutionActivityPhase.PersistingResult,
                AgentExecutionActivityPhase.Completed
            },
            events.Select(item => item.Event.Phase));
    }

    [Fact]
    public async Task Binding_is_single_assignment_and_rejects_session_mismatch()
    {
        var context = CreateContext();
        var sessionId = Guid.NewGuid();
        var otherSessionId = Guid.NewGuid();
        var runId = Guid.NewGuid();

        using var operation = Admit(context);
        operation.BindChatSession(sessionId);

        Assert.Throws<InvalidOperationException>(
            () => operation.BindChatSession(sessionId));
        Assert.Throws<InvalidOperationException>(
            () => operation.BindExecutionRun(runId, otherSessionId));

        operation.BindExecutionRun(runId, sessionId);

        Assert.Equal(sessionId, operation.ChatSessionId);
        Assert.Equal(runId, operation.ExecutionRunId);
        Assert.Throws<InvalidOperationException>(
            () => operation.BindExecutionRun(Guid.NewGuid(), sessionId));

        operation.Report(
            AgentExecutionActivityPhase.CapturingContext,
            "Bound metadata.");
        operation.Cancel("Cancelled.");

        var events = await ReadEventsAsync(context);
        Assert.Null(events[0].Event.ChatSessionId);
        Assert.Null(events[0].Event.ExecutionRunId);
        Assert.All(
            events.Skip(1),
            item =>
            {
                Assert.Equal(sessionId, item.Event.ChatSessionId);
                Assert.Equal(runId, item.Event.ExecutionRunId);
            });
    }

    [Fact]
    public async Task Terminalization_rejects_duplicate_and_keeps_first_terminal_event()
    {
        var context = CreateContext();

        using var operation = Admit(context);
        operation.Report(
            AgentExecutionActivityPhase.PersistingResult,
            "Persisting.");
        operation.Complete("Completed first.");

        Assert.Throws<InvalidOperationException>(
            () => operation.Cancel("Cancelled second."));

        var events = await ReadEventsAsync(context);
        Assert.Equal(3, events.Count);
        Assert.Equal("Completed first.", events[^1].Event.Message);
        Assert.Equal(
            AgentExecutionActivityTerminalOutcome.Succeeded,
            events[^1].Event.TerminalOutcome);
    }

    [Fact]
    public async Task Dispose_cancels_unterminated_operation()
    {
        var context = CreateContext();
        var operation = Admit(context);

        operation.Dispose();

        var events = await ReadEventsAsync(context);
        Assert.Equal(2, events.Count);
        Assert.Equal(
            AgentExecutionActivityPhase.Cancelled,
            events[^1].Event.Phase);
        Assert.Equal(
            AgentExecutionActivityTerminalOutcome.Cancelled,
            events[^1].Event.TerminalOutcome);
        Assert.True(events[^1].Event.IsTerminal);
    }

    [Fact]
    public async Task Suspend_maps_to_terminal_awaiting_approval()
    {
        var context = CreateContext();

        using var operation = Admit(context);
        operation.Report(
            AgentExecutionActivityPhase.AwaitingApproval,
            "Approval required.");
        operation.Suspend("Operation suspended.");

        var terminal = (await ReadEventsAsync(context))[^1].Event;
        Assert.Equal(
            AgentExecutionActivityPhase.AwaitingApproval,
            terminal.Phase);
        Assert.Equal(
            AgentExecutionActivityTerminalOutcome.Suspended,
            terminal.TerminalOutcome);
        Assert.True(terminal.IsTerminal);
    }

    [Fact]
    public async Task Fail_preserves_typed_error_code()
    {
        var context = CreateContext();

        using var operation = Admit(context);
        operation.Report(
            AgentExecutionActivityPhase.ResolvingProvider,
            "Resolving provider.");
        operation.Fail("Provider failed.", "provider-unavailable");

        var terminal = (await ReadEventsAsync(context))[^1].Event;
        Assert.Equal(AgentExecutionActivityPhase.Failed, terminal.Phase);
        Assert.Equal(
            AgentExecutionActivityTerminalOutcome.Failed,
            terminal.TerminalOutcome);
        Assert.Equal("provider-unavailable", terminal.ErrorCode);
    }

    [Fact]
    public async Task Terminal_partition_replays_events_then_reports_completion()
    {
        var context = CreateContext();

        using var operation = Admit(context);
        operation.Report(
            AgentExecutionActivityPhase.PersistingResult,
            "Persisting.");
        operation.Complete("Completed.");

        await using var reader = context.Coordinator.OpenReader(
            context.StreamId,
            StreamSequence.Beginning);
        var replay = Assert.IsType<SequencedStreamEvents<AgentExecutionActivity>>(
            await reader.ReadAsync());
        var completed = Assert.IsType<SequencedStreamCompleted<AgentExecutionActivity>>(
            await reader.ReadAsync());

        Assert.Equal(3, replay.Items.Count);
        Assert.Equal(replay.Items[^1].Sequence, completed.LastSequence);

        await using var secondReader = context.Coordinator.OpenReader(
            context.StreamId,
            StreamSequence.Beginning);
        var secondReplay = Assert.IsType<SequencedStreamEvents<AgentExecutionActivity>>(
            await secondReader.ReadAsync());
        Assert.Equal(replay.Items, secondReplay.Items);
    }

    private static TestContext CreateContext()
    {
        var timeProvider = new FixedTimeProvider(Now);
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
        return new TestContext(
            coordinator,
            new AgentExecutionActivityStreamId(
                Guid.NewGuid(),
                WorkspaceScopeDescriptor.Project("project-42"),
                new DatabaseProfileGeneration(0),
                AgentExecutionOperationId.New()),
            Guid.NewGuid());
    }

    private static IAgentExecutionActivityOperationLease Admit(
        TestContext context,
        Guid? sessionId = null,
        string message = "Accepted.")
    {
        var admission = Assert.IsType<AgentExecutionActivityAdmitted>(
            context.Coordinator.AdmitOperation(
                context.StreamId,
                context.AgentId,
                sessionId,
                message));
        Assert.Equal(context.StreamId, admission.StreamId);
        return admission.Operation;
    }

    private static async Task<IReadOnlyList<
        SequencedStreamEnvelope<AgentExecutionActivity>>> ReadEventsAsync(
        TestContext context)
    {
        await using var reader = context.Coordinator.OpenReader(
            context.StreamId,
            StreamSequence.Beginning);
        var result = Assert.IsType<SequencedStreamEvents<AgentExecutionActivity>>(
            await reader.ReadAsync());
        return result.Items;
    }

    private sealed record TestContext(
        AgentExecutionActivityCoordinator Coordinator,
        AgentExecutionActivityStreamId StreamId,
        Guid AgentId);

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow()
        {
            return utcNow;
        }
    }
}
