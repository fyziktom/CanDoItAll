using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Projections;
using CanDoItAll.Processes.Runtime;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Processes.Persistence;

public sealed class EfProcessRuntimeEventStore(ProcessPersistenceDbContext dbContext) :
    IProcessRuntimeEventStore,
    IProcessRuntimeEventReplayStore
{
    public async Task AppendAsync(
        IReadOnlyList<ProcessRuntimeEventEnvelope> events,
        CancellationToken cancellationToken = default)
    {
        if (events.Count == 0)
        {
            return;
        }

        var rootSequences = new Dictionary<Guid, long>(events.Count);
        foreach (var runtimeEvent in events)
        {
            if (!rootSequences.TryGetValue(runtimeEvent.RootRunId.Value, out var nextRootSequence))
            {
                nextRootSequence = await NextRootSequenceAsync(
                    runtimeEvent.RootRunId.Value,
                    cancellationToken).ConfigureAwait(false);
            }

            dbContext.RuntimeEvents.Add(ProcessPersistenceMappers.ToEventEntity(
                runtimeEvent,
                nextRootSequence));

            rootSequences[runtimeEvent.RootRunId.Value] = nextRootSequence + 1;
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ProcessStoredRuntimeEvent>> ReadAfterGlobalSequenceAsync(
        long globalSequenceExclusive,
        int take,
        CancellationToken cancellationToken = default)
    {
        ValidateTake(take);

        var rows = await dbContext.RuntimeEvents
            .AsNoTracking()
            .Where(runtimeEvent => runtimeEvent.GlobalSequence > globalSequenceExclusive)
            .OrderBy(runtimeEvent => runtimeEvent.GlobalSequence)
            .Take(take)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var events = new List<ProcessStoredRuntimeEvent>(rows.Count);
        foreach (var row in rows)
        {
            events.Add(ProcessPersistenceMappers.ToStoredEvent(row));
        }

        return events;
    }

    public async Task<IReadOnlyList<ProcessStoredRuntimeEvent>> ReadByRootRunAsync(
        ProcessRunId rootRunId,
        long rootSequenceExclusive,
        int take,
        CancellationToken cancellationToken = default)
    {
        ValidateTake(take);

        var rows = await dbContext.RuntimeEvents
            .AsNoTracking()
            .Where(runtimeEvent => runtimeEvent.RootRunId == rootRunId.Value && runtimeEvent.RootSequence > rootSequenceExclusive)
            .OrderBy(runtimeEvent => runtimeEvent.RootSequence)
            .Take(take)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var events = new List<ProcessStoredRuntimeEvent>(rows.Count);
        foreach (var row in rows)
        {
            events.Add(ProcessPersistenceMappers.ToStoredEvent(row));
        }

        return events;
    }

    private async Task<long> NextRootSequenceAsync(Guid rootRunId, CancellationToken cancellationToken)
    {
        var maxSequence = await dbContext.RuntimeEvents
            .Where(runtimeEvent => runtimeEvent.RootRunId == rootRunId)
            .MaxAsync(runtimeEvent => (long?)runtimeEvent.RootSequence, cancellationToken)
            .ConfigureAwait(false);
        return (maxSequence ?? 0) + 1;
    }

    private static void ValidateTake(int take)
    {
        if (take <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(take), take, "Replay read size must be positive.");
        }
    }
}
