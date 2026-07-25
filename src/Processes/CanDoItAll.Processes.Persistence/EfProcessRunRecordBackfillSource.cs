using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Projections;
using CanDoItAll.Processes.Runtime;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Processes.Persistence;

public sealed class EfProcessRunRecordBackfillSource(
    ProcessPersistenceDbContext dbContext,
    TimeProvider timeProvider) :
    IProcessRunRecordBackfillSource
{
    public async Task<IReadOnlyList<ProcessRunRecordSeed>> ListMissingTerminalSeedsAsync(
        int take,
        CancellationToken cancellationToken = default)
    {
        if (take is <= 0 or > ProcessRunRecordPayloadLimits.MaximumClaimBatchSize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(take),
                take,
                $"Process run record backfill size must be between 1 and {ProcessRunRecordPayloadLimits.MaximumClaimBatchSize}.");
        }

        var completedEventType = ProcessRuntimeEventTypes.ProcessRunCompleted.Value;
        var failedEventType = ProcessRuntimeEventTypes.ProcessRunFailed.Value;
        var cancelledEventType = ProcessRuntimeEventTypes.ProcessRunCancelled.Value;
        var candidates = await dbContext.RuntimeStates
            .AsNoTracking()
            .Where(state =>
                state.Status == ProcessRuntimeStatus.Completed ||
                state.Status == ProcessRuntimeStatus.Failed ||
                state.Status == ProcessRuntimeStatus.Cancelled)
            .Where(state => !dbContext.RunRecords.Any(record =>
                record.RunId == state.RunId &&
                record.LifecycleState == ProcessRunRecordLifecycleState.Current))
            .Where(state => dbContext.RuntimeEvents.Any(runtimeEvent =>
                runtimeEvent.RunId == state.RunId &&
                ((state.Status == ProcessRuntimeStatus.Completed &&
                  runtimeEvent.EventType == completedEventType) ||
                 (state.Status == ProcessRuntimeStatus.Failed &&
                  runtimeEvent.EventType == failedEventType) ||
                 (state.Status == ProcessRuntimeStatus.Cancelled &&
                  runtimeEvent.EventType == cancelledEventType))))
            .OrderByDescending(state => state.UpdatedAtUtc)
            .ThenBy(state => state.RunId)
            .Take(take)
            .Select(state => new ProcessRunBackfillCandidate(
                state.RunId,
                state.RootRunId,
                state.PlanId,
                state.Status))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        if (candidates.Count == 0)
        {
            return [];
        }

        var runIds = candidates.Select(candidate => candidate.RunId).ToArray();
        var terminalEventTypes = new[]
        {
            completedEventType,
            failedEventType,
            cancelledEventType
        };
        var terminalEvents = await dbContext.RuntimeEvents
            .AsNoTracking()
            .Where(runtimeEvent =>
                runIds.Contains(runtimeEvent.RunId) &&
                terminalEventTypes.Contains(runtimeEvent.EventType))
            .GroupBy(runtimeEvent => new
            {
                runtimeEvent.RunId,
                runtimeEvent.EventType
            })
            .Select(group => group
                .OrderByDescending(runtimeEvent => runtimeEvent.GlobalSequence)
                .Select(runtimeEvent => new ProcessRunBackfillEvent(
                    runtimeEvent.RunId,
                    runtimeEvent.EventType,
                    runtimeEvent.GlobalSequence,
                    runtimeEvent.RootSequence,
                    runtimeEvent.OccurredAtUtc))
                .First())
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var eventsByRunId = terminalEvents
            .GroupBy(runtimeEvent => runtimeEvent.RunId)
            .ToDictionary(group => group.Key, group => group.ToArray());
        var seeds = new List<ProcessRunRecordSeed>(candidates.Count);
        var observedAtUtc = timeProvider.GetUtcNow();
        foreach (var candidate in candidates)
        {
            if (!eventsByRunId.TryGetValue(candidate.RunId, out var candidateEvents))
            {
                continue;
            }

            var requiredEventType = candidate.Status switch
            {
                ProcessRuntimeStatus.Completed => completedEventType,
                ProcessRuntimeStatus.Failed => failedEventType,
                ProcessRuntimeStatus.Cancelled => cancelledEventType,
                _ => throw new InvalidOperationException(
                    $"Runtime status '{candidate.Status}' is not supported by process run record backfill.")
            };
            var terminalEvent = candidateEvents.FirstOrDefault(runtimeEvent =>
                string.Equals(runtimeEvent.EventType, requiredEventType, StringComparison.Ordinal));
            if (terminalEvent is null)
            {
                continue;
            }

            seeds.Add(new ProcessRunRecordSeed(
                new ProcessRunRecordIdentity(
                    new ProcessRunId(candidate.RunId),
                    new ProcessRunId(candidate.RootRunId),
                    ParentRunId: null,
                    new ProcessInstancePlanId(candidate.PlanId),
                    DefinitionId: null,
                    DefinitionVersionId: null,
                    ProjectId: null),
                MapDisposition(candidate.Status),
                terminalEvent.OccurredAtUtc,
                terminalEvent.GlobalSequence,
                terminalEvent.RootSequence,
                observedAtUtc)
            {
                Validation = ProcessRunRecordSeedValidation.CurrentTerminalSource
            });
        }

        return seeds;
    }

    private static ProcessRunDisposition MapDisposition(ProcessRuntimeStatus status)
    {
        return status switch
        {
            ProcessRuntimeStatus.Completed => ProcessRunDisposition.Succeeded,
            ProcessRuntimeStatus.Failed => ProcessRunDisposition.Failed,
            ProcessRuntimeStatus.Cancelled => ProcessRunDisposition.Cancelled,
            _ => throw new ArgumentOutOfRangeException(
                nameof(status),
                status,
                "Runtime status is not reportable as a process run record.")
        };
    }

    private sealed record ProcessRunBackfillCandidate(
        Guid RunId,
        Guid RootRunId,
        Guid PlanId,
        ProcessRuntimeStatus Status);

    private sealed record ProcessRunBackfillEvent(
        Guid RunId,
        string EventType,
        long GlobalSequence,
        long RootSequence,
        DateTimeOffset OccurredAtUtc);
}
