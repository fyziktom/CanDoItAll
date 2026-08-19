using System.Collections.Concurrent;
using System.Threading.Channels;
using Bunit;
using CanDoItAll.AgentFramework.Components;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.SharedKernel.Streaming;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.AgentFramework;

public sealed class AgentExecutionActivityStatusTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 7, 27, 12, 30, 0, TimeSpan.Zero);

    [Fact]
    public void Shows_first_typed_activity_for_the_exact_operation_before_run_identity_exists()
    {
        using var context = CreateContext(out var activityReader);
        var streamId = CreateStreamId();
        var cut = RenderStatus(context, streamId);

        var initialStatus = cut.Find("[data-testid='agent-execution-activity-status']");
        Assert.Equal(
            streamId.OperationId.ToString(),
            initialStatus.GetAttribute("data-activity-operation-id"));
        Assert.Equal("status", initialStatus.GetAttribute("role"));
        Assert.Equal("polite", initialStatus.GetAttribute("aria-live"));

        activityReader.Publish(
            streamId,
            CreateActivity(
                AgentExecutionActivityPhase.Accepted,
                "Agent request accepted."));

        cut.WaitForAssertion(() =>
        {
            Assert.Equal(
                "Accepted",
                cut.Find("[data-testid='agent-execution-activity-phase']")
                    .TextContent
                    .Trim());
            Assert.Equal(
                "Agent request accepted.",
                cut.Find("[data-testid='agent-execution-activity-message']")
                    .TextContent
                    .Trim());
        });
        Assert.Equal(streamId, activityReader.LastOpenedStreamId);
        Assert.Null(activityReader.LastPublishedActivity?.ChatSessionId);
        Assert.Null(activityReader.LastPublishedActivity?.ExecutionRunId);
    }

    [Theory]
    [InlineData(
        AgentExecutionActivityPhase.PreparingCapabilities,
        null,
        "Initializing tools")]
    [InlineData(
        AgentExecutionActivityPhase.AwaitingApproval,
        AgentExecutionActivityTerminalOutcome.Suspended,
        "Approval required")]
    [InlineData(
        AgentExecutionActivityPhase.Completed,
        AgentExecutionActivityTerminalOutcome.Succeeded,
        "Completed")]
    [InlineData(
        AgentExecutionActivityPhase.Failed,
        AgentExecutionActivityTerminalOutcome.Failed,
        "Failed")]
    [InlineData(
        AgentExecutionActivityPhase.Cancelled,
        AgentExecutionActivityTerminalOutcome.Cancelled,
        "Cancelled")]
    public void Maps_typed_activity_phase_without_parsing_message_text(
        AgentExecutionActivityPhase phase,
        AgentExecutionActivityTerminalOutcome? outcome,
        string expectedLabel)
    {
        using var context = CreateContext(out var activityReader);
        var streamId = CreateStreamId();
        var cut = RenderStatus(context, streamId);
        const string deliberatelyUnrelatedMessage =
            "This message contains no phase-name convention.";

        activityReader.Publish(
            streamId,
            CreateActivity(
                phase,
                deliberatelyUnrelatedMessage,
                outcome));

        cut.WaitForAssertion(() =>
        {
            Assert.Equal(
                expectedLabel,
                cut.Find("[data-testid='agent-execution-activity-phase']")
                    .TextContent
                    .Trim());
            Assert.Equal(
                deliberatelyUnrelatedMessage,
                cut.Find("[data-testid='agent-execution-activity-message']")
                    .TextContent
                    .Trim());
        });
    }

    [Fact]
    public void Reports_a_sequence_gap_and_bounds_the_public_message()
    {
        using var context = CreateContext(out var activityReader);
        var streamId = CreateStreamId();
        var cut = RenderStatus(context, streamId);

        activityReader.Publish(
            streamId,
            new SequencedStreamGap<AgentExecutionActivity>(
                StreamSequence.Beginning,
                new StreamSequence(4)));
        cut.WaitForAssertion(() => Assert.Equal(
            "Earlier updates skipped",
            cut.Find("[data-testid='agent-execution-activity-gap']")
                .TextContent
                .Trim()));

        activityReader.Publish(
            streamId,
            CreateActivity(
                AgentExecutionActivityPhase.Streaming,
                string.Concat(
                    "  ",
                    string.Concat(Enumerable.Repeat("response ", 40)),
                    "\r\ncontinues  ")));

        cut.WaitForAssertion(() =>
        {
            var message = cut.Find("[data-testid='agent-execution-activity-message']")
                .TextContent
                .Trim();
            Assert.Equal(240, message.Length);
            Assert.EndsWith("…", message, StringComparison.Ordinal);
            Assert.DoesNotContain('\r', message);
            Assert.DoesNotContain('\n', message);
        });
    }

    [Fact]
    public void Switching_operation_disposes_the_old_reader_and_fences_late_updates()
    {
        using var context = CreateContext(out var activityReader);
        var firstStreamId = CreateStreamId();
        var secondStreamId = CreateStreamId();
        var cut = RenderStatus(context, firstStreamId);

        activityReader.Publish(
            firstStreamId,
            CreateActivity(
                AgentExecutionActivityPhase.Accepted,
                "First operation."));
        cut.WaitForAssertion(() => Assert.Equal(
            "Accepted",
            cut.Find("[data-testid='agent-execution-activity-phase']")
                .TextContent
                .Trim()));

        cut.Render(parameters => parameters
            .Add(component => component.StreamId, secondStreamId));

        cut.WaitForAssertion(() =>
        {
            Assert.Equal(1, activityReader.GetDisposeCount(firstStreamId));
            Assert.Equal(
                secondStreamId.OperationId.ToString(),
                cut.Find("[data-testid='agent-execution-activity-status']")
                    .GetAttribute("data-activity-operation-id"));
        });

        activityReader.Publish(
            firstStreamId,
            CreateActivity(
                AgentExecutionActivityPhase.Failed,
                "Late first-operation failure.",
                AgentExecutionActivityTerminalOutcome.Failed));
        activityReader.Publish(
            secondStreamId,
            CreateActivity(
                AgentExecutionActivityPhase.PreparingCapabilities,
                "Second operation capabilities."));

        cut.WaitForAssertion(() =>
        {
            Assert.Equal(
                "Initializing tools",
                cut.Find("[data-testid='agent-execution-activity-phase']")
                    .TextContent
                    .Trim());
            Assert.DoesNotContain(
                "Late first-operation failure.",
                cut.Markup,
                StringComparison.Ordinal);
        });
    }

    [Fact]
    public void Distinguishes_profile_change_cancellation_from_unknown_stream()
    {
        using var profileContext = CreateContext(out var profileReader);
        var profileStreamId = CreateStreamId();
        var profileCut = RenderStatus(profileContext, profileStreamId);

        profileReader.FailRead(
            profileStreamId,
            new OperationCanceledException("Profile generation changed."));

        profileCut.WaitForAssertion(() =>
        {
            Assert.Equal(
                "Updates unavailable",
                profileCut.Find("[data-testid='agent-execution-activity-phase']")
                    .TextContent
                    .Trim());
            Assert.Contains(
                "database profile changed",
                profileCut.Find("[data-testid='agent-execution-activity-message']")
                    .TextContent,
                StringComparison.Ordinal);
        });

        using var unknownContext = CreateContext(out var unknownReader);
        var unknownStreamId = CreateStreamId();
        var unknownCut = RenderStatus(unknownContext, unknownStreamId);
        unknownReader.Publish(
            unknownStreamId,
            new SequencedStreamUnknown<AgentExecutionActivity>());

        unknownCut.WaitForAssertion(() =>
        {
            Assert.Equal(
                "Updates unavailable",
                unknownCut.Find("[data-testid='agent-execution-activity-phase']")
                    .TextContent
                    .Trim());
            Assert.Equal(
                "Live agent activity is no longer available.",
                unknownCut.Find("[data-testid='agent-execution-activity-message']")
                    .TextContent
                    .Trim());
        });
    }

    private static BunitContext CreateContext(
        out ControlledActivityReader activityReader)
    {
        var context = new BunitContext();
        context.Services.AddLogging();
        context.Services.AddCanDoItAllBaseLib();
        activityReader = new ControlledActivityReader();
        context.Services.AddSingleton<IAgentExecutionActivityReader>(activityReader);
        return context;
    }

    private static IRenderedComponent<AgentExecutionActivityStatus> RenderStatus(
        BunitContext context,
        AgentExecutionActivityStreamId streamId)
    {
        return context.Render<AgentExecutionActivityStatus>(
            parameters => parameters
                .Add(component => component.StreamId, streamId));
    }

    private static AgentExecutionActivityStreamId CreateStreamId()
    {
        var profileId = Guid.NewGuid();
        return new AgentExecutionActivityStreamId(
            profileId,
            WorkspaceScopeDescriptor.Organization(profileId.ToString("N")),
            new DatabaseProfileGeneration(4),
            AgentExecutionOperationId.New());
    }

    private static AgentExecutionActivity CreateActivity(
        AgentExecutionActivityPhase phase,
        string message,
        AgentExecutionActivityTerminalOutcome? outcome = null)
    {
        return new AgentExecutionActivity(
            phase,
            Now,
            Guid.NewGuid(),
            message,
            terminalOutcome: outcome,
            errorCode: outcome == AgentExecutionActivityTerminalOutcome.Failed
                ? AgentExecutionActivityFailureCodes.UnhandledExecutionFailure
                : null);
    }

    private sealed class ControlledActivityReader : IAgentExecutionActivityReader
    {
        private readonly ConcurrentDictionary<
            AgentExecutionActivityStreamId,
            StreamState> states = new();
        private readonly ConcurrentDictionary<
            AgentExecutionActivityStreamId,
            int> disposeCounts = new();

        public AgentExecutionActivityStreamId? LastOpenedStreamId { get; private set; }

        public AgentExecutionActivity? LastPublishedActivity { get; private set; }

        public ISequencedStreamReader<AgentExecutionActivity> OpenReader(
            AgentExecutionActivityStreamId streamId,
            StreamSequence fromInclusive)
        {
            Assert.Equal(StreamSequence.Beginning, fromInclusive);
            LastOpenedStreamId = streamId;
            var state = states.GetOrAdd(streamId, static _ => new StreamState());
            return new ControlledSequencedStreamReader(
                state,
                () => disposeCounts.AddOrUpdate(
                    streamId,
                    1,
                    static (_, count) => count + 1));
        }

        public void Publish(
            AgentExecutionActivityStreamId streamId,
            AgentExecutionActivity activity)
        {
            LastPublishedActivity = activity;
            Publish(
                streamId,
                new SequencedStreamEvents<AgentExecutionActivity>(
                [
                    new SequencedStreamEnvelope<AgentExecutionActivity>(
                        StreamSequence.First,
                        activity)
                ]));
        }

        public void Publish(
            AgentExecutionActivityStreamId streamId,
            SequencedStreamReadResult<AgentExecutionActivity> result)
        {
            var state = states.GetOrAdd(streamId, static _ => new StreamState());
            Assert.True(state.Instructions.Writer.TryWrite(
                ReaderInstruction.FromResult(result)));
        }

        public void FailRead(
            AgentExecutionActivityStreamId streamId,
            Exception exception)
        {
            var state = states.GetOrAdd(streamId, static _ => new StreamState());
            Assert.True(state.Instructions.Writer.TryWrite(
                ReaderInstruction.FromException(exception)));
        }

        public int GetDisposeCount(
            AgentExecutionActivityStreamId streamId)
        {
            return disposeCounts.TryGetValue(streamId, out var count)
                ? count
                : 0;
        }
    }

    private sealed class ControlledSequencedStreamReader(
        StreamState state,
        Action onDispose)
        : ISequencedStreamReader<AgentExecutionActivity>
    {
        private StreamSequence nextSequence = StreamSequence.Beginning;
        private int disposed;

        public StreamSequence NextSequence => nextSequence;

        public async ValueTask<SequencedStreamReadResult<AgentExecutionActivity>> ReadAsync(
            CancellationToken cancellationToken = default)
        {
            var instruction = await state.Instructions.Reader.ReadAsync(cancellationToken);
            if (instruction.Exception is not null)
            {
                throw instruction.Exception;
            }

            var result = instruction.Result
                ?? throw new InvalidOperationException("A reader instruction has no result.");
            if (result is SequencedStreamEvents<AgentExecutionActivity> events)
            {
                nextSequence = new StreamSequence(
                    events.Items[^1].Sequence.Value + 1);
            }

            return result;
        }

        public ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref disposed, 1) == 0)
            {
                onDispose();
            }

            return ValueTask.CompletedTask;
        }
    }

    private sealed class StreamState
    {
        public Channel<ReaderInstruction> Instructions { get; } =
            Channel.CreateUnbounded<ReaderInstruction>(
                new UnboundedChannelOptions
                {
                    SingleReader = true,
                    SingleWriter = false,
                    AllowSynchronousContinuations = false
                });
    }

    private sealed record ReaderInstruction(
        SequencedStreamReadResult<AgentExecutionActivity>? Result,
        Exception? Exception)
    {
        public static ReaderInstruction FromResult(
            SequencedStreamReadResult<AgentExecutionActivity> result)
        {
            return new ReaderInstruction(result, null);
        }

        public static ReaderInstruction FromException(Exception exception)
        {
            return new ReaderInstruction(null, exception);
        }
    }
}
