using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class WorkflowProgressEventIdentityTests {
    private static readonly DateTimeOffset Now = new(2026, 9, 23, 10, 0, 0, TimeSpan.Zero);
    private static readonly WorkflowNodeId NodeId = new("work");

    [Theory]
    [InlineData(WorkflowNodeExecutionProgressState.Started)]
    [InlineData(WorkflowNodeExecutionProgressState.Completed)]
    [InlineData(WorkflowNodeExecutionProgressState.Failed)]
    public async Task Adapter_forwards_the_canonical_event_including_payload_policy_metadata(
        WorkflowNodeExecutionProgressState state) {
        var definition = CreateDefinition();
        var run = CreateRun(definition);
        var store = new InMemoryWorkflowRunStore();
        await store.SaveRunAsync(run);
        var sink = new RecordingSink();
        var observer = new WorkflowBackendProgressEventObserver(run.RunId, definition,
            WorkflowPreviewSimulationPlan.Empty, new WorkflowPayloadPolicyService(), null,
            new StoreBackedWorkflowNodeExecutionProgressObserver(run, store, null, sink));
        var payload = new string('x', 100_000);
        var progress = CreateProgress(definition, run.RunId, state) with {
            PayloadJson = payload,
            ErrorMessage = payload
        };

        await observer.RecordAsync(progress);

        var canonical = Assert.Single(observer.Events);
        Assert.Same(canonical, Assert.Single(sink.Events));
        Assert.Equal(canonical, Assert.Single(await store.ListEventsAsync(run.RunId)));
        Assert.Equal(WorkflowReadEvidenceDurability.Persisted, observer.ReadEvidenceDurability);
        if (state != WorkflowNodeExecutionProgressState.Started) {
            var artifact = Assert.Single(observer.Artifacts);
            var envelope = JsonSerializer.Deserialize<WorkflowEventPayloadEnvelope>(
                canonical.PayloadJson, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            Assert.NotNull(envelope);
            Assert.Equal(artifact.StoragePath, envelope.Reference);
            Assert.True(envelope.InlineTruncated);
            Assert.Equal(payload.Length, envelope.InlineCharacters);
        }
    }

    [Fact]
    public async Task Progress_only_observer_receives_bounded_payload() {
        var definition = CreateDefinition();
        var run = CreateRun(definition);
        var next = new RecordingProgressObserver();
        var observer = new WorkflowBackendProgressEventObserver(run.RunId, definition,
            WorkflowPreviewSimulationPlan.Empty, new WorkflowPayloadPolicyService(), null, next);
        var payload = new string('x', 100_000);

        await observer.RecordAsync(CreateProgress(definition, run.RunId,
            WorkflowNodeExecutionProgressState.Completed) with { PayloadJson = payload });

        var forwarded = Assert.Single(next.Progress);
        Assert.True(forwarded.PayloadJson.Length < payload.Length);
        Assert.Equal(WorkflowReadEvidenceDurability.Transient, observer.ReadEvidenceDurability);
    }

    [Fact]
    public async Task Direct_progress_still_persists_and_publishes() {
        var definition = CreateDefinition();
        var run = CreateRun(definition);
        var store = new InMemoryWorkflowRunStore();
        await store.SaveRunAsync(run);
        var sink = new RecordingSink();
        var observer = new StoreBackedWorkflowNodeExecutionProgressObserver(run, store, null, sink);

        await observer.RecordAsync(CreateProgress(definition, run.RunId, WorkflowNodeExecutionProgressState.Started));

        var recorded = Assert.Single(await store.ListEventsAsync(run.RunId));
        Assert.Equal(WorkflowEventKind.ExecutorInvoked, recorded.Kind);
        Assert.Equal(recorded, Assert.Single(sink.Events));
    }

    [Fact]
    public async Task Start_preserves_distinct_executions_with_identical_node_kind_and_timestamp() {
        var definition = CreateDefinition();
        var store = new InMemoryWorkflowRunStore();
        var sink = new RecordingSink();
        var runtime = WorkflowRuntimeManager.CreateInMemory([new RepeatedProgressBackend()], store, sink);

        var run = await runtime.StartAsync(definition, new WorkflowRunStartRequest(
            definition.Id, definition.VersionId, "{}", WorkflowRuntimeBackendKind.InProcess, null, null));

        var events = (await store.ListEventsAsync(run.RunId))
            .Where(value => value.Kind == WorkflowEventKind.ExecutorInvoked).ToArray();
        Assert.Equal(2, events.Length);
        Assert.Equal(2, events.Select(value => value.Id).Distinct().Count());
        Assert.Equal(events, sink.Events.Where(value => value.Kind == WorkflowEventKind.ExecutorInvoked));
    }

    private static WorkflowDefinition CreateDefinition() {
        var node = new WorkflowNode(NodeId, WorkflowNodeKind.Executor, "Work", [],
            new WorkflowNodeSettings(null, null, null, null, string.Empty,
                WorkflowValueShape.Text, WorkflowValueShape.Text));
        return new WorkflowDefinition(WorkflowId.New(), WorkflowVersionId.New(), "Progress identity",
            string.Empty, WorkflowLifecycleStatus.Draft, new WorkflowGraph(NodeId, [node], []),
            new WorkflowRuntimePolicy(WorkflowRuntimeBackendKind.InProcess, true, false, false, false), Now, Now);
    }

    private static WorkflowRunSnapshot CreateRun(WorkflowDefinition definition)
        => new(WorkflowRunId.New(), definition.Id, definition.VersionId, WorkflowRunState.Running,
            WorkflowRuntimeBackendKind.InProcess, string.Empty, string.Empty, Now, Now);

    private static WorkflowNodeExecutionProgress CreateProgress(WorkflowDefinition definition,
        WorkflowRunId runId, WorkflowNodeExecutionProgressState state)
        => new(definition.Id, definition.VersionId, runId, NodeId, state, Now);

    private sealed class RecordingSink : IWorkflowEventSink {
        public List<WorkflowEventRecord> Events { get; } = [];

        public Task PublishAsync(WorkflowEventRecord workflowEvent, CancellationToken cancellationToken = default) {
            Events.Add(workflowEvent);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingProgressObserver : IWorkflowNodeExecutionProgressObserver {
        public List<WorkflowNodeExecutionProgress> Progress { get; } = [];

        public ValueTask RecordAsync(WorkflowNodeExecutionProgress progress, CancellationToken cancellationToken = default) {
            Progress.Add(progress);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class RepeatedProgressBackend : IWorkflowExecutionBackend {
        public WorkflowRuntimeBackendDescriptor Descriptor { get; } = new(
            WorkflowRuntimeBackendKind.InProcess, "Repeated progress", false, true, false, true, string.Empty);

        public async Task<WorkflowBackendStartResult> StartAsync(WorkflowDefinition definition,
            WorkflowRunStartRequest request, WorkflowRunId runId, CancellationToken cancellationToken = default) {
            var progress = CreateProgress(definition, runId, WorkflowNodeExecutionProgressState.Started);
            var first = new WorkflowEventRecord(Guid.NewGuid(), runId, WorkflowEventKind.ExecutorInvoked,
                NodeId, "Started", "{}", Now);
            await WorkflowNodeExecutionProgressScope.Current!.RecordEventAsync(progress, first, cancellationToken);
            var second = first with { Id = Guid.NewGuid() };
            return new WorkflowBackendStartResult(CreateRun(definition) with {
                RunId = runId,
                State = WorkflowRunState.Completed
            }, [first, second], [], []);
        }
    }
}
