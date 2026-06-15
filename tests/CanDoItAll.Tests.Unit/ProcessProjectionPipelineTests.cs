using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Contracts;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Projections;
using CanDoItAll.Processes.Runtime;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Tests.Unit;

public sealed class ProcessProjectionPipelineTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 15, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Replay_worker_projects_live_history_run_detail_and_offsets()
    {
        await using var dbContext = CreateDbContext();
        var store = new EfProcessProjectionStore(dbContext);
        var runId = ProcessRunId.New();
        var replay = new RecordingRuntimeEventReplayStore(
            StoredEvent(1, runId, ProcessRuntimeEventTypes.ProcessRunActivated, Now.AddMinutes(-5)),
            StoredEvent(2, runId, ProcessRuntimeEventTypes.StepRunning, Now.AddMinutes(-4)));
        var clock = new FixedProcessProjectionClock(Now);
        var projector = new ProcessRuntimeProjectionProjector(store, ProcessProjectionJsonCodec.Default, clock);
        var worker = new ProcessProjectionReplayWorker(replay, store, projector, clock);

        var result = await worker.ReplayAsync(new ProcessProjectionReplayRequest(
            ProcessRuntimeProjectionProjector.ProjectorName,
            new ProcessProjectionShardKey("root-alpha"),
            Take: 10,
            LatestKnownGlobalSequence: 2));
        var query = new ProcessRuntimeProjectionQueryService(store, ProcessProjectionJsonCodec.Default, clock);
        var live = await query.GetLiveProcessesAsync(new ProcessLiveProcessesQuery(Now, TimeSpan.FromHours(1), Take: 10));
        var history = await query.GetRunHistoryAsync(new ProcessRunHistoryQuery(runId, Now.AddHours(-1), Now, Take: 10));
        var detail = await query.GetRunDetailAsync(new ProcessRunDetailQuery(runId));
        var offset = await store.LoadOffsetAsync(ProcessRuntimeProjectionProjector.ProjectorName, new ProcessProjectionShardKey("root-alpha"));

        Assert.Equal(ProcessProjectionReplayStatus.Completed, result.Status);
        Assert.Equal(2, result.ProcessedCount);
        Assert.NotNull(offset);
        Assert.Equal(2, offset.GlobalSequence);
        var run = Assert.Single(live.Runs);
        Assert.Equal(runId, run.RunId);
        Assert.True(run.IsActive);
        Assert.Equal(ProcessProjectedRunStatus.Active, run.Status);
        Assert.Equal(2, run.Freshness.SourceGlobalSequence);
        Assert.Equal(0, run.Freshness.Lag.BacklogEventCount);
        Assert.Equal([1, 2], history.Events.Select(runtimeEvent => runtimeEvent.GlobalSequence));
        Assert.NotNull(detail);
        Assert.Equal(runId, detail.RunId);
        Assert.Equal(ProcessProjectedRunStatus.Active, detail.Status);
    }

    [Fact]
    public async Task Replay_worker_dead_letters_failed_projection_without_advancing_offset()
    {
        await using var dbContext = CreateDbContext();
        var store = new EfProcessProjectionStore(dbContext);
        var runId = ProcessRunId.New();
        var replay = new RecordingRuntimeEventReplayStore(
            StoredEvent(3, runId, ProcessRuntimeEventTypes.StepFailed, Now));
        var worker = new ProcessProjectionReplayWorker(
            replay,
            store,
            new ThrowingRuntimeProjector(new ProcessProjectorName("runtime.throwing")),
            new FixedProcessProjectionClock(Now));

        var result = await worker.ReplayAsync(new ProcessProjectionReplayRequest(
            new ProcessProjectorName("runtime.throwing"),
            new ProcessProjectionShardKey("root-alpha"),
            Take: 10,
            LatestKnownGlobalSequence: 3));
        var offset = await store.LoadOffsetAsync(new ProcessProjectorName("runtime.throwing"), new ProcessProjectionShardKey("root-alpha"));
        var deadLetters = await store.ReadDeadLettersAsync(new ProcessProjectorName("runtime.throwing"), new ProcessProjectionShardKey("root-alpha"), 10);

        Assert.Equal(ProcessProjectionReplayStatus.DeadLettered, result.Status);
        Assert.Equal(0, result.ProcessedCount);
        Assert.Null(offset);
        var deadLetter = Assert.Single(deadLetters);
        Assert.Equal(3, deadLetter.GlobalSequence);
        Assert.Equal("InvalidOperationException", deadLetter.ErrorClass);
    }

    [Fact]
    public async Task Live_last_hour_query_excludes_old_completed_runs()
    {
        await using var dbContext = CreateDbContext();
        var store = new EfProcessProjectionStore(dbContext);
        var runId = ProcessRunId.New();
        await ProjectAsync(
            store,
            StoredEvent(1, runId, ProcessRuntimeEventTypes.ProcessRunCompleted, Now.AddHours(-2)),
            latestKnownGlobalSequence: 1);
        var query = new ProcessRuntimeProjectionQueryService(store, ProcessProjectionJsonCodec.Default, new FixedProcessProjectionClock(Now));

        var live = await query.GetLiveProcessesAsync(new ProcessLiveProcessesQuery(Now, TimeSpan.FromHours(1), Take: 10));

        Assert.Empty(live.Runs);
    }

    [Fact]
    public async Task Live_last_hour_query_includes_active_runs_outside_window()
    {
        await using var dbContext = CreateDbContext();
        var store = new EfProcessProjectionStore(dbContext);
        var runId = ProcessRunId.New();
        await ProjectAsync(
            store,
            StoredEvent(1, runId, ProcessRuntimeEventTypes.StepRunning, Now.AddHours(-2)),
            latestKnownGlobalSequence: 1);
        var query = new ProcessRuntimeProjectionQueryService(store, ProcessProjectionJsonCodec.Default, new FixedProcessProjectionClock(Now));

        var live = await query.GetLiveProcessesAsync(new ProcessLiveProcessesQuery(Now, TimeSpan.FromHours(1), Take: 10));

        var run = Assert.Single(live.Runs);
        Assert.Equal(runId, run.RunId);
        Assert.True(run.IsActive);
        Assert.Equal(Now.AddHours(-2), run.LastEventAtUtc);
    }

    [Fact]
    public async Task Projection_freshness_exposes_projector_lag()
    {
        await using var dbContext = CreateDbContext();
        var store = new EfProcessProjectionStore(dbContext);
        var runId = ProcessRunId.New();
        await ProjectAsync(
            store,
            StoredEvent(7, runId, ProcessRuntimeEventTypes.StepRunning, Now.AddMinutes(-1)),
            latestKnownGlobalSequence: 10);
        var query = new ProcessRuntimeProjectionQueryService(store, ProcessProjectionJsonCodec.Default, new FixedProcessProjectionClock(Now));

        var live = await query.GetLiveProcessesAsync(new ProcessLiveProcessesQuery(Now, TimeSpan.FromHours(1), Take: 10));

        var run = Assert.Single(live.Runs);
        Assert.Equal(7, run.Freshness.SourceGlobalSequence);
        Assert.Equal(10, run.Freshness.Lag.LatestKnownGlobalSequence);
        Assert.Equal(3, run.Freshness.Lag.BacklogEventCount);
    }

    [Fact]
    public async Task Restricted_events_project_diagnostic_links_without_raw_payload_detail()
    {
        await using var dbContext = CreateDbContext();
        var store = new EfProcessProjectionStore(dbContext);
        var runId = ProcessRunId.New();
        var restricted = StoredEvent(
            1,
            runId,
            ProcessRuntimeEventTypes.ManagerIncidentRaised,
            Now,
            sensitivity: ProcessEventSensitivity.Restricted,
            payloadHash: "hash:restricted-secret");
        await ProjectAsync(store, restricted, latestKnownGlobalSequence: 1);
        var query = new ProcessRuntimeProjectionQueryService(store, ProcessProjectionJsonCodec.Default, new FixedProcessProjectionClock(Now));

        var history = await query.GetRunHistoryAsync(new ProcessRunHistoryQuery(runId, Now.AddHours(-1), Now.AddHours(1), Take: 10));

        var runtimeEvent = Assert.Single(history.Events);
        Assert.Equal(ProcessProjectedSensitivity.Restricted, runtimeEvent.Sensitivity);
        Assert.Equal($"runtime-event:{restricted.Envelope.EventId}", runtimeEvent.RestrictedDiagnosticReference);
        Assert.DoesNotContain("restricted-secret", runtimeEvent.Summary, StringComparison.Ordinal);
    }

    private static async Task ProjectAsync(
        EfProcessProjectionStore store,
        ProcessStoredRuntimeEvent runtimeEvent,
        long latestKnownGlobalSequence)
    {
        var replay = new RecordingRuntimeEventReplayStore(runtimeEvent);
        var clock = new FixedProcessProjectionClock(Now);
        var projector = new ProcessRuntimeProjectionProjector(store, ProcessProjectionJsonCodec.Default, clock);
        var worker = new ProcessProjectionReplayWorker(replay, store, projector, clock);
        await worker.ReplayAsync(new ProcessProjectionReplayRequest(
            ProcessRuntimeProjectionProjector.ProjectorName,
            new ProcessProjectionShardKey("root-alpha"),
            Take: 10,
            latestKnownGlobalSequence));
    }

    private static ProcessPersistenceDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ProcessPersistenceDbContext>()
            .UseInMemoryDatabase($"process-projections-{Guid.NewGuid():N}")
            .Options;
        return new ProcessPersistenceDbContext(options);
    }

    private static ProcessStoredRuntimeEvent StoredEvent(
        long globalSequence,
        ProcessRunId runId,
        ProcessEventType eventType,
        DateTimeOffset occurredAtUtc,
        ProcessEventSensitivity sensitivity = ProcessEventSensitivity.Normal,
        string payloadHash = "hash:event")
    {
        var envelope = new ProcessRuntimeEventEnvelope(
            RuntimeEventId.New(),
            runId,
            runId,
            new ProcessCorrelationId("corr-alpha"),
            null,
            new ProcessEventActor(ProcessEventActorKind.System, new ProcessActorId("system")),
            ProcessContractVersions.RuntimeEventEnvelopeV1,
            sensitivity,
            occurredAtUtc,
            eventType,
            payloadHash);
        return new ProcessStoredRuntimeEvent(globalSequence, globalSequence, envelope);
    }

    private sealed class RecordingRuntimeEventReplayStore(params ProcessStoredRuntimeEvent[] events) : IProcessRuntimeEventReplayStore
    {
        private readonly IReadOnlyList<ProcessStoredRuntimeEvent> events = events;

        public Task<IReadOnlyList<ProcessStoredRuntimeEvent>> ReadAfterGlobalSequenceAsync(
            long globalSequenceExclusive,
            int take,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<ProcessStoredRuntimeEvent> result = events
                .Where(runtimeEvent => runtimeEvent.GlobalSequence > globalSequenceExclusive)
                .OrderBy(runtimeEvent => runtimeEvent.GlobalSequence)
                .Take(take)
                .ToArray();
            return Task.FromResult(result);
        }

        public Task<IReadOnlyList<ProcessStoredRuntimeEvent>> ReadByRootRunAsync(
            ProcessRunId rootRunId,
            long rootSequenceExclusive,
            int take,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<ProcessStoredRuntimeEvent> result = events
                .Where(runtimeEvent =>
                    runtimeEvent.Envelope.RootRunId == rootRunId &&
                    runtimeEvent.RootSequence > rootSequenceExclusive)
                .OrderBy(runtimeEvent => runtimeEvent.RootSequence)
                .Take(take)
                .ToArray();
            return Task.FromResult(result);
        }
    }

    private sealed class ThrowingRuntimeProjector(ProcessProjectorName projectorName) : IProcessRuntimeProjector
    {
        public ProcessProjectorName ProjectorName { get; } = projectorName;

        public Task ProjectAsync(
            ProcessStoredRuntimeEvent runtimeEvent,
            ProcessProjectionExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException($"Cannot project event {runtimeEvent.Envelope.EventId}.");
        }
    }

    private sealed class FixedProcessProjectionClock(DateTimeOffset utcNow) : IProcessProjectionClock
    {
        public DateTimeOffset GetUtcNow() => utcNow;
    }
}
