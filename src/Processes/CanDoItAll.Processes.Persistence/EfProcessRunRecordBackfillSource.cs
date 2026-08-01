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
    public async Task<IReadOnlyList<ProcessRunRecordSeed>> ListMissingReportableSeedsAsync(
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
        var blockedEventType = ProcessRuntimeEventTypes.ProcessRunBlocked.Value;
        var candidates = await dbContext.RuntimeStates
            .AsNoTracking()
            .Where(state =>
                state.Status == ProcessRuntimeStatus.Completed ||
                state.Status == ProcessRuntimeStatus.Failed ||
                state.Status == ProcessRuntimeStatus.Cancelled ||
                state.Status == ProcessRuntimeStatus.Blocked)
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
                  runtimeEvent.EventType == cancelledEventType) ||
                 (state.Status == ProcessRuntimeStatus.Blocked &&
                  runtimeEvent.EventType == blockedEventType))))
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
        var reportableEventTypes = new[]
        {
            completedEventType,
            failedEventType,
            cancelledEventType,
            blockedEventType
        };
        var reportableEvents = await dbContext.RuntimeEvents
            .AsNoTracking()
            .Where(runtimeEvent =>
                runIds.Contains(runtimeEvent.RunId) &&
                reportableEventTypes.Contains(runtimeEvent.EventType))
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
        var eventsByRunId = reportableEvents
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
                ProcessRuntimeStatus.Blocked => blockedEventType,
                _ => throw new InvalidOperationException(
                    $"Runtime status '{candidate.Status}' is not supported by process run record backfill.")
            };
            var reportableEvent = candidateEvents.FirstOrDefault(runtimeEvent =>
                string.Equals(runtimeEvent.EventType, requiredEventType, StringComparison.Ordinal));
            if (reportableEvent is null)
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
                reportableEvent.OccurredAtUtc,
                reportableEvent.GlobalSequence,
                reportableEvent.RootSequence,
                observedAtUtc)
            {
                Validation = ProcessRunRecordSeedValidation.CurrentReportableSource
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
            ProcessRuntimeStatus.Blocked => ProcessRunDisposition.Blocked,
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
