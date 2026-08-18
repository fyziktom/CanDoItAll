using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Workspace.ApiAccess;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Contracts;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Projections;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.Web.Api;
using CanDoItAll.Web.Api.Streaming;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Tests.Integration.Api;

public sealed class ApiRunEventAdapterTests
{
    [Fact]
    public async Task Run_specific_sse_endpoints_emit_only_the_exact_run()
    {
        await using var host = await ApiTestHost.CreateAsync(
            jwtEnabled: false,
            useInMemoryDatabase: true);
        var workflowRunId = WorkflowRunId.New();
        var otherWorkflowRunId = WorkflowRunId.New();
        var workflowSink = host.App.Services.GetRequiredService<IWorkflowEventSink>();
        Assert.IsType<WorkflowApiEventSink>(workflowSink);
        using (var scope = host.App.Services.CreateScope())
        {
            Assert.IsType<ApiNotifyingProcessRuntimeProjector>(
                scope.ServiceProvider.GetRequiredService<IProcessRuntimeProjector>());
        }

        await workflowSink.PublishAsync(
            CreateWorkflowEvent(otherWorkflowRunId, WorkflowEventKind.Started, nodeId: null));
        await workflowSink.PublishAsync(
            CreateWorkflowEvent(workflowRunId, WorkflowEventKind.WaitingForInput, new WorkflowNodeId("approval")));

        using var workflowGlobalResponse = await host.Client.SendAsync(
            new HttpRequestMessage(HttpMethod.Get, "/api/workflows/events/stream"),
            HttpCompletionOption.ResponseHeadersRead);
        var workflowGlobalFrame = await ReadSseFrameAsync(workflowGlobalResponse);

        Assert.Contains($"event: {WorkflowRunEventsApi.EventName}", workflowGlobalFrame, StringComparison.Ordinal);

        using var workflowResponse = await host.Client.SendAsync(
            new HttpRequestMessage(
                HttpMethod.Get,
                $"/api/workflows/runs/{workflowRunId.Value:D}/events/stream"),
            HttpCompletionOption.ResponseHeadersRead);
        var workflowFrame = await ReadSseFrameAsync(workflowResponse);

        Assert.Equal(ServerSentEventResponseWriter.ContentType, workflowResponse.Content.Headers.ContentType?.MediaType);
        Assert.Contains($"event: {WorkflowRunEventsApi.EventName}", workflowFrame, StringComparison.Ordinal);
        Assert.Contains(workflowRunId.Value.ToString("D"), workflowFrame, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(otherWorkflowRunId.Value.ToString("D"), workflowFrame, StringComparison.OrdinalIgnoreCase);

        var processRunId = ProcessRunId.New();
        var otherProcessRunId = ProcessRunId.New();
        var processStream = host.App.Services
            .GetRequiredService<ProfileBoundedReplayEventStream<ProcessApiRunEvent>>();
        processStream.Publish(
            ApiNotifyingProcessRuntimeProjector.Map(
                CreateProcessEvent(
                    otherProcessRunId,
                    ProcessRuntimeProjectionEventTypeNames.StepReady,
                    ProcessEventSensitivity.Normal)));
        processStream.Publish(
            ApiNotifyingProcessRuntimeProjector.Map(
                CreateProcessEvent(
                    processRunId,
                    ProcessRuntimeProjectionEventTypeNames.ProcessRunCompleted,
                    ProcessEventSensitivity.Normal)));

        using var processGlobalResponse = await host.Client.SendAsync(
            new HttpRequestMessage(HttpMethod.Get, "/api/processes/events/stream"),
            HttpCompletionOption.ResponseHeadersRead);
        var processGlobalFrame = await ReadSseFrameAsync(processGlobalResponse);

        Assert.Contains($"event: {ProcessRunEventsApi.EventName}", processGlobalFrame, StringComparison.Ordinal);

        using var processResponse = await host.Client.SendAsync(
            new HttpRequestMessage(
                HttpMethod.Get,
                $"/api/processes/runs/{processRunId.Value:D}/events/stream"),
            HttpCompletionOption.ResponseHeadersRead);
        var processFrame = await ReadSseFrameAsync(processResponse);

        Assert.Equal(ServerSentEventResponseWriter.ContentType, processResponse.Content.Headers.ContentType?.MediaType);
        Assert.Contains($"event: {ProcessRunEventsApi.EventName}", processFrame, StringComparison.Ordinal);
        Assert.Contains(processRunId.Value.ToString("D"), processFrame, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(otherProcessRunId.Value.ToString("D"), processFrame, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Workflow_adapter_maps_lifecycle_signal_without_message_or_payload()
    {
        var runId = WorkflowRunId.New();
        var workflowEvent = new WorkflowEventRecord(
            Guid.NewGuid(),
            runId,
            WorkflowEventKind.WaitingForInput,
            new WorkflowNodeId("approval"),
            "sensitive message",
            """{"secret":"must-not-leak"}""",
            DateTimeOffset.UtcNow);

        var result = WorkflowApiEventSink.Map(workflowEvent);

        Assert.Equal(workflowEvent.Id, result.EventId);
        Assert.Equal(runId, result.RunId);
        Assert.Equal(WorkflowApiEventCategory.NeedsAttention, result.Category);
        Assert.True(result.NeedsAttention);
        Assert.False(result.IsTerminal);
        Assert.DoesNotContain(
            typeof(WorkflowApiRunEvent).GetProperties(),
            property => property.Name is nameof(WorkflowEventRecord.Message) or nameof(WorkflowEventRecord.PayloadJson));
    }

    [Fact]
    public void Workflow_adapter_marks_only_run_level_error_as_terminal_failure()
    {
        var runId = WorkflowRunId.New();
        var nodeFailure = CreateWorkflowEvent(runId, WorkflowEventKind.ExecutorFailed, new WorkflowNodeId("executor"));
        var runFailure = CreateWorkflowEvent(runId, WorkflowEventKind.Error, nodeId: null);

        var nodeResult = WorkflowApiEventSink.Map(nodeFailure);
        var runResult = WorkflowApiEventSink.Map(runFailure);

        Assert.Equal(WorkflowApiEventCategory.Failed, nodeResult.Category);
        Assert.False(nodeResult.IsTerminal);
        Assert.Equal(WorkflowApiEventCategory.Failed, runResult.Category);
        Assert.True(runResult.IsTerminal);
    }

    [Fact]
    public void Process_adapter_preserves_sequences_and_masks_restricted_event_details()
    {
        var runId = ProcessRunId.New();
        var runtimeEvent = CreateProcessEvent(
            runId,
            ProcessRuntimeProjectionEventTypeNames.ProcessRunBlocked,
            ProcessEventSensitivity.Restricted);

        var result = ApiNotifyingProcessRuntimeProjector.Map(runtimeEvent);

        Assert.Equal(runtimeEvent.GlobalSequence, result.GlobalSequence);
        Assert.Equal(runtimeEvent.RootSequence, result.RootSequence);
        Assert.Equal(runId, result.RunId);
        Assert.Equal(ProcessApiEventCategory.NeedsAttention, result.Category);
        Assert.Equal(ProcessApiEventSensitivity.Restricted, result.Sensitivity);
        Assert.Equal(ProcessApiEventTypeNames.Restricted, result.EventType);
        Assert.DoesNotContain(
            typeof(ProcessApiRunEvent).GetProperties(),
            property => property.Name is "CorrelationId" or "Actor" or "PayloadHash");
    }

    [Fact]
    public void Process_adapter_maps_initial_lifecycle_events_as_started()
    {
        var runId = ProcessRunId.New();
        var created = ApiNotifyingProcessRuntimeProjector.Map(
            CreateProcessEvent(
                runId,
                ProcessRuntimeEventTypes.ProcessRunCreated.Value,
                ProcessEventSensitivity.Normal));
        var activated = ApiNotifyingProcessRuntimeProjector.Map(
            CreateProcessEvent(
                runId,
                ProcessRuntimeEventTypes.ProcessRunActivated.Value,
                ProcessEventSensitivity.Normal));

        Assert.Equal(ProcessApiEventCategory.Started, created.Category);
        Assert.False(created.IsTerminal);
        Assert.False(created.NeedsAttention);
        Assert.Equal(ProcessApiEventCategory.Started, activated.Category);
        Assert.False(activated.IsTerminal);
        Assert.False(activated.NeedsAttention);
    }

    [Fact]
    public void Process_adapter_exposes_normal_terminal_failure_signal()
    {
        var runId = ProcessRunId.New();
        var runtimeEvent = CreateProcessEvent(
            runId,
            ProcessRuntimeProjectionEventTypeNames.ProcessRunFailed,
            ProcessEventSensitivity.Normal);

        var result = ApiNotifyingProcessRuntimeProjector.Map(runtimeEvent);

        Assert.Equal(ProcessApiEventCategory.Failed, result.Category);
        Assert.Equal(ProcessRuntimeProjectionEventTypeNames.ProcessRunFailed, result.EventType);
        Assert.Equal(ProcessApiEventSensitivity.Normal, result.Sensitivity);
        Assert.True(result.IsTerminal);
        Assert.True(result.NeedsAttention);
    }

    [Fact]
    public async Task Process_decorator_publishes_only_after_projection_succeeds()
    {
        using var stream = CreateProcessEventStream();
        var inner = new RecordingProcessRuntimeProjector();
        var decorator = new ApiNotifyingProcessRuntimeProjector(
            inner,
            stream,
            NullLogger<ApiNotifyingProcessRuntimeProjector>.Instance);
        var runtimeEvent = CreateProcessEvent(
            ProcessRunId.New(),
            ProcessRuntimeProjectionEventTypeNames.ProcessRunCompleted,
            ProcessEventSensitivity.Normal);
        var context = CreateProjectionContext(runtimeEvent.GlobalSequence);

        await decorator.ProjectAsync(runtimeEvent, context);
        var published = await stream
            .OpenCurrent()
            .Reader
            .ReadAsync(0, CancellationToken.None);

        Assert.Same(runtimeEvent, inner.ProjectedEvent);
        var entry = Assert.Single(published.Events);
        Assert.Equal(runtimeEvent.GlobalSequence, entry.Value.GlobalSequence);
        Assert.Equal(ProcessApiEventCategory.Completed, entry.Value.Category);
    }

    [Fact]
    public async Task Process_decorator_does_not_publish_when_projection_fails()
    {
        using var stream = CreateProcessEventStream();
        var inner = new ThrowingProcessRuntimeProjector();
        var decorator = new ApiNotifyingProcessRuntimeProjector(
            inner,
            stream,
            NullLogger<ApiNotifyingProcessRuntimeProjector>.Instance);
        var runtimeEvent = CreateProcessEvent(
            ProcessRunId.New(),
            ProcessRuntimeProjectionEventTypeNames.ProcessRunCompleted,
            ProcessEventSensitivity.Normal);
        var context = CreateProjectionContext(runtimeEvent.GlobalSequence);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => decorator.ProjectAsync(runtimeEvent, context));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await stream
                .OpenCurrent()
                .Reader
                .ReadAsync(0, cancellation.Token));
    }

    private static WorkflowEventRecord CreateWorkflowEvent(
        WorkflowRunId runId,
        WorkflowEventKind kind,
        WorkflowNodeId? nodeId)
    {
        return new WorkflowEventRecord(
            Guid.NewGuid(),
            runId,
            kind,
            nodeId,
            "message",
            "{}",
            DateTimeOffset.UtcNow);
    }

    private static ProcessStoredRuntimeEvent CreateProcessEvent(
        ProcessRunId runId,
        string eventType,
        ProcessEventSensitivity sensitivity)
    {
        return new ProcessStoredRuntimeEvent(
            GlobalSequence: 42,
            RootSequence: 7,
            new ProcessRuntimeEventEnvelope(
                RuntimeEventId.New(),
                runId,
                runId,
                new ProcessCorrelationId("correlation-secret"),
                CausationId: null,
                new ProcessEventActor(
                    ProcessEventActorKind.Manager,
                    new ProcessActorId("actor-secret")),
                ProcessContractVersions.RuntimeEventEnvelopeV1,
                sensitivity,
                DateTimeOffset.UtcNow,
                new ProcessEventType(eventType),
                "payload-hash-secret"));
    }

    private static ProfileBoundedReplayEventStream<ProcessApiRunEvent> CreateProcessEventStream()
    {
        var notifications = new DatabaseSwitchNotificationService();
        return new ProfileBoundedReplayEventStream<ProcessApiRunEvent>(
            new StableDatabaseRuntimeState(),
            notifications,
            Options.Create(new ApiAccessOptions
            {
                ServerSentEvents = new ApiServerSentEventsOptions
                {
                    ReplayCapacity = 8,
                    MaxBatchSize = 8,
                    HeartbeatIntervalSeconds = 1
                }
            }),
            NullLogger<ProfileBoundedReplayEventStream<ProcessApiRunEvent>>.Instance);
    }

    private static ProcessProjectionExecutionContext CreateProjectionContext(long latestKnownGlobalSequence)
    {
        return new ProcessProjectionExecutionContext(
            new ProcessProjectionShardKey("api-test"),
            DateTimeOffset.UtcNow,
            latestKnownGlobalSequence);
    }

    private static async Task<string> ReadSseFrameAsync(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync();
        using var reader = new StreamReader(stream);
        var lines = new List<string>();
        while (true)
        {
            var line = await reader.ReadLineAsync();
            if (line is null || line.Length == 0)
            {
                return string.Join('\n', lines);
            }

            lines.Add(line);
        }
    }

    private sealed class RecordingProcessRuntimeProjector : IProcessRuntimeProjector
    {
        public ProcessProjectorName ProjectorName { get; } = new("recording");

        public ProcessStoredRuntimeEvent? ProjectedEvent { get; private set; }

        public Task ProjectAsync(
            ProcessStoredRuntimeEvent runtimeEvent,
            ProcessProjectionExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            ProjectedEvent = runtimeEvent;
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingProcessRuntimeProjector : IProcessRuntimeProjector
    {
        public ProcessProjectorName ProjectorName { get; } = new("throwing");

        public Task ProjectAsync(
            ProcessStoredRuntimeEvent runtimeEvent,
            ProcessProjectionExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Projection failed.");
        }
    }

    private sealed class StableDatabaseRuntimeState : IDatabaseRuntimeState
    {
        private readonly DatabaseRuntimeSnapshot snapshot = new(
            Guid.NewGuid(),
            "api-run-event-adapter-test",
            Generation: 0);

        public DatabaseRuntimeSnapshot GetSnapshot()
        {
            return snapshot;
        }

        public void MarkCurrentProfile(ResolvedDatabaseProfile profile)
        {
            throw new NotSupportedException();
        }
    }
}
