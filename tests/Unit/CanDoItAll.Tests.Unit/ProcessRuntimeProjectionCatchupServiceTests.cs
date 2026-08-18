using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Contracts;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Projections;
using CanDoItAll.Processes.Runtime;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Tests.Unit.Processes;

public sealed class ProcessRuntimeProjectionCatchupServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 24, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Catchup_reads_each_batch_once_and_projects_in_global_sequence_order()
    {
        await using var dbContext = CreateDbContext();
        var projectionStore = new EfProcessProjectionStore(dbContext);
        var runId = ProcessRunId.New();
        var replayStore = new RecordingReplayStore(
            StoredEvent(2, runId, ProcessRuntimeEventTypes.StepRunning),
            StoredEvent(1, runId, ProcessRuntimeEventTypes.ProcessRunActivated));
        var projector = new RecordingProjector();
        var service = new ProcessRuntimeProjectionCatchupService(
            replayStore,
            projectionStore,
            projector,
            new FixedClock());

        var result = await service.CatchUpAsync();
        var offset = await projectionStore.LoadOffsetAsync(
            projector.ProjectorName,
            new ProcessProjectionShardKey("runtime-global"));

        Assert.Equal(ProcessProjectionReplayStatus.Completed, result.Status);
        Assert.Equal(2, result.ProcessedCount);
        Assert.Equal(1, replayStore.ReadAfterGlobalSequenceCallCount);
        Assert.Equal([1, 2], projector.ProjectedGlobalSequences);
        Assert.NotNull(offset);
        Assert.Equal(2, offset.GlobalSequence);
    }

    private static ProcessPersistenceDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ProcessPersistenceDbContext>()
            .UseInMemoryDatabase($"process-projection-catchup-{Guid.NewGuid():N}")
            .Options;
        return new ProcessPersistenceDbContext(options);
    }

    private static ProcessStoredRuntimeEvent StoredEvent(
        long globalSequence,
        ProcessRunId runId,
        ProcessEventType eventType)
    {
        var envelope = new ProcessRuntimeEventEnvelope(
            RuntimeEventId.New(),
            runId,
            runId,
            new ProcessCorrelationId("projection-catchup-test"),
            null,
            new ProcessEventActor(ProcessEventActorKind.System, new ProcessActorId("system")),
            ProcessContractVersions.RuntimeEventEnvelopeV1,
            ProcessEventSensitivity.Normal,
            Now,
            eventType,
            $"hash:{globalSequence}");
        return new ProcessStoredRuntimeEvent(globalSequence, globalSequence, envelope);
    }

    private sealed class RecordingReplayStore(params ProcessStoredRuntimeEvent[] events) : IProcessRuntimeEventReplayStore
    {
        public int ReadAfterGlobalSequenceCallCount { get; private set; }

        public Task<IReadOnlyList<ProcessStoredRuntimeEvent>> ReadAfterGlobalSequenceAsync(
            long globalSequenceExclusive,
            int take,
            CancellationToken cancellationToken = default)
        {
            ReadAfterGlobalSequenceCallCount++;
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

    private sealed class RecordingProjector : IProcessRuntimeProjector
    {
        public ProcessProjectorName ProjectorName { get; } = new("runtime.catchup-test");

        public List<long> ProjectedGlobalSequences { get; } = [];

        public Task ProjectAsync(
            ProcessStoredRuntimeEvent runtimeEvent,
            ProcessProjectionExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            ProjectedGlobalSequences.Add(runtimeEvent.GlobalSequence);
            return Task.CompletedTask;
        }
    }

    private sealed class FixedClock : IProcessProjectionClock
    {
        public DateTimeOffset GetUtcNow() => Now;
    }
}
