using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Core;

namespace CanDoItAll.Processes.Projections;

public sealed class ProcessRuntimeProjectionProjector(
    IProcessProjectionStore projectionStore,
    ProcessProjectionJsonCodec jsonCodec,
    IProcessProjectionClock clock) : IProcessRuntimeProjector
{
    public static ProcessProjectorName ProjectorName { get; } = new("runtime.projections");

    ProcessProjectorName IProcessRuntimeProjector.ProjectorName => ProjectorName;

    public async Task ProjectAsync(
        ProcessStoredRuntimeEvent runtimeEvent,
        ProcessProjectionExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(runtimeEvent);
        ArgumentNullException.ThrowIfNull(context);

        var liveKey = ProcessRuntimeProjectionKeys.Live(runtimeEvent.Envelope.RunId);
        var previousSnapshot = await projectionStore
            .LoadSnapshotAsync(ProjectorName, liveKey, cancellationToken)
            .ConfigureAwait(false);
        var previous = previousSnapshot is null
            ? null
            : jsonCodec.ReadSnapshot<ProcessLiveProcessSnapshot>(previousSnapshot);
        var timelineEvent = CreateTimelineEvent(runtimeEvent);
        var projectedStatus = DetermineStatus(runtimeEvent.Envelope.EventType, previous?.Status ?? ProcessProjectedRunStatus.Unknown);
        var freshness = CreateFreshness(runtimeEvent.GlobalSequence, context);
        var recentEvents = AppendRecentEvent(previous?.RecentEvents, timelineEvent);
        var incidents = AppendIncident(previous?.Incidents, runtimeEvent);
        var firstEventAtUtc = previous?.FirstEventAtUtc ?? runtimeEvent.Envelope.OccurredAtUtc;

        var liveSnapshot = new ProcessLiveProcessSnapshot(
            runtimeEvent.Envelope.RootRunId,
            runtimeEvent.Envelope.RunId,
            projectedStatus,
            IsActive(projectedStatus),
            firstEventAtUtc,
            runtimeEvent.Envelope.OccurredAtUtc,
            freshness,
            recentEvents,
            incidents);
        var detail = new ProcessRunDetailProjection(
            runtimeEvent.Envelope.RootRunId,
            runtimeEvent.Envelope.RunId,
            projectedStatus,
            firstEventAtUtc,
            runtimeEvent.Envelope.OccurredAtUtc,
            freshness,
            recentEvents);
        var runtimeCanvas = new ProcessRuntimeCanvasProjection(
            runtimeEvent.Envelope.RootRunId,
            runtimeEvent.Envelope.RunId,
            freshness,
            [
                new ProcessRuntimeCanvasNodeProjection(
                    runtimeEvent.Envelope.RunId.ToString(),
                    "Run",
                    projectedStatus,
                    IsActive(projectedStatus))
            ]);
        var artifactMap = new ProcessArtifactMapProjection(
            runtimeEvent.Envelope.RootRunId,
            runtimeEvent.Envelope.RunId,
            freshness,
            []);

        await projectionStore
            .UpsertSnapshotAsync(jsonCodec.CreateSnapshot(ProjectorName, liveKey, liveSnapshot, clock.GetUtcNow()), cancellationToken)
            .ConfigureAwait(false);
        await projectionStore
            .UpsertSnapshotAsync(jsonCodec.CreateSnapshot(ProjectorName, ProcessRuntimeProjectionKeys.RunDetail(runtimeEvent.Envelope.RunId), detail, clock.GetUtcNow()), cancellationToken)
            .ConfigureAwait(false);
        await projectionStore
            .UpsertSnapshotAsync(jsonCodec.CreateSnapshot(ProjectorName, ProcessRuntimeProjectionKeys.RuntimeCanvas(runtimeEvent.Envelope.RunId), runtimeCanvas, clock.GetUtcNow()), cancellationToken)
            .ConfigureAwait(false);
        await projectionStore
            .UpsertSnapshotAsync(jsonCodec.CreateSnapshot(ProjectorName, ProcessRuntimeProjectionKeys.ArtifactMap(runtimeEvent.Envelope.RunId), artifactMap, clock.GetUtcNow()), cancellationToken)
            .ConfigureAwait(false);
        await projectionStore
            .AppendHistoryAsync(
                jsonCodec.CreateHistoryRecord(
                    ProjectorName,
                    ProcessRuntimeProjectionKeys.Timeline(runtimeEvent.GlobalSequence),
                    runtimeEvent,
                    new ProcessTimelineEventProjection(
                        timelineEvent.EventId,
                        timelineEvent.GlobalSequence,
                        timelineEvent.RootRunId,
                        timelineEvent.RunId,
                        timelineEvent.EventType,
                        timelineEvent.OccurredAtUtc,
                        timelineEvent.Sensitivity,
                        timelineEvent.Summary,
                        timelineEvent.RestrictedDiagnosticReference)),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static ProcessLiveRunEventProjection CreateTimelineEvent(ProcessStoredRuntimeEvent runtimeEvent)
    {
        var sensitivity = runtimeEvent.Envelope.Sensitivity == ProcessEventSensitivity.Restricted
            ? ProcessProjectedSensitivity.Restricted
            : ProcessProjectedSensitivity.Normal;
        var restrictedReference = sensitivity == ProcessProjectedSensitivity.Restricted
            ? $"runtime-event:{runtimeEvent.Envelope.EventId}"
            : null;
        var summary = sensitivity == ProcessProjectedSensitivity.Restricted
            ? "Restricted runtime event"
            : runtimeEvent.Envelope.EventType.Value;

        return new ProcessLiveRunEventProjection(
            runtimeEvent.Envelope.EventId,
            runtimeEvent.GlobalSequence,
            runtimeEvent.Envelope.RootRunId,
            runtimeEvent.Envelope.RunId,
            runtimeEvent.Envelope.EventType.Value,
            runtimeEvent.Envelope.OccurredAtUtc,
            sensitivity,
            summary,
            restrictedReference);
    }

    private static IReadOnlyList<ProcessLiveRunEventProjection> AppendRecentEvent(
        IReadOnlyList<ProcessLiveRunEventProjection>? previous,
        ProcessLiveRunEventProjection next)
    {
        const int maxRecentEvents = 20;
        var events = new List<ProcessLiveRunEventProjection>(Math.Min((previous?.Count ?? 0) + 1, maxRecentEvents));
        if (previous is not null)
        {
            var skip = Math.Max(0, previous.Count - (maxRecentEvents - 1));
            for (var index = skip; index < previous.Count; index++)
            {
                events.Add(previous[index]);
            }
        }

        events.Add(next);
        return events;
    }

    private static IReadOnlyList<ProcessIncidentProjection> AppendIncident(
        IReadOnlyList<ProcessIncidentProjection>? previous,
        ProcessStoredRuntimeEvent runtimeEvent)
    {
        var incidents = previous is null
            ? []
            : new List<ProcessIncidentProjection>(previous);
        if (!string.Equals(runtimeEvent.Envelope.EventType.Value, ProcessRuntimeProjectionEventTypeNames.ManagerIncidentRaised, StringComparison.Ordinal))
        {
            return incidents;
        }

        incidents.Add(new ProcessIncidentProjection(
            runtimeEvent.Envelope.EventId.ToString(),
            runtimeEvent.Envelope.RootRunId,
            runtimeEvent.Envelope.RunId,
            "ManagerIncident",
            "NeedsAttention",
            "Raised",
            "Manager incident raised",
            $"runtime-event:{runtimeEvent.Envelope.EventId}",
            runtimeEvent.Envelope.OccurredAtUtc));
        return incidents;
    }

    private static ProcessProjectionFreshness CreateFreshness(
        long sourceGlobalSequence,
        ProcessProjectionExecutionContext context)
    {
        var backlog = context.LatestKnownGlobalSequence - sourceGlobalSequence;
        return new ProcessProjectionFreshness(
            context.ObservedAtUtc,
            sourceGlobalSequence,
            new ProcessProjectionLag(
                context.LatestKnownGlobalSequence,
                sourceGlobalSequence,
                backlog <= 0 ? 0 : checked((int)backlog)));
    }

    private static ProcessProjectedRunStatus DetermineStatus(
        ProcessEventType eventType,
        ProcessProjectedRunStatus previousStatus)
    {
        return eventType.Value switch
        {
            ProcessRuntimeProjectionEventTypeNames.ProcessRunReactivated => ProcessProjectedRunStatus.Active,
            ProcessRuntimeProjectionEventTypeNames.ProcessRunCompleted => ProcessProjectedRunStatus.Completed,
            ProcessRuntimeProjectionEventTypeNames.ProcessRunFailed => ProcessProjectedRunStatus.Failed,
            ProcessRuntimeProjectionEventTypeNames.ProcessRunCancelled => ProcessProjectedRunStatus.Cancelled,
            ProcessRuntimeProjectionEventTypeNames.StepReady => ProcessProjectedRunStatus.Active,
            ProcessRuntimeProjectionEventTypeNames.StepWaiting => ProcessProjectedRunStatus.Active,
            ProcessRuntimeProjectionEventTypeNames.StepBlocked => ProcessProjectedRunStatus.NeedsAttention,
            ProcessRuntimeProjectionEventTypeNames.StepReworkRequested => ProcessProjectedRunStatus.NeedsAttention,
            ProcessRuntimeProjectionEventTypeNames.ManagerIncidentRaised => ProcessProjectedRunStatus.NeedsAttention,
            ProcessRuntimeProjectionEventTypeNames.ManagerLoopBudgetEscalated => ProcessProjectedRunStatus.NeedsAttention,
            ProcessRuntimeProjectionEventTypeNames.ManagerRecoveryDenied => ProcessProjectedRunStatus.NeedsAttention,
            ProcessRuntimeProjectionEventTypeNames.ManagerBranchDecisionRejected => ProcessProjectedRunStatus.NeedsAttention,
            _ => previousStatus is ProcessProjectedRunStatus.Completed or ProcessProjectedRunStatus.Failed or ProcessProjectedRunStatus.Cancelled
                ? previousStatus
                : ProcessProjectedRunStatus.Active
        };
    }

    private static bool IsActive(ProcessProjectedRunStatus status)
    {
        return status is ProcessProjectedRunStatus.Active or ProcessProjectedRunStatus.NeedsAttention;
    }
}

public static class ProcessRuntimeProjectionEventTypeNames
{
    public const string ProcessRunReactivated = "ProcessRunReactivated";

    public const string ProcessRunCompleted = "ProcessRunCompleted";

    public const string ProcessRunFailed = "ProcessRunFailed";

    public const string ProcessRunCancelled = "ProcessRunCancelled";

    public const string StepBlocked = "StepBlocked";

    public const string StepReady = "StepReady";

    public const string StepWaiting = "StepWaiting";

    public const string StepReworkRequested = "StepReworkRequested";

    public const string ManagerIncidentRaised = "ManagerIncidentRaised";

    public const string ManagerLoopBudgetEscalated = "ManagerLoopBudgetEscalated";

    public const string ManagerRecoveryDenied = "ManagerRecoveryDenied";

    public const string ManagerBranchDecisionRejected = "ManagerBranchDecisionRejected";
}

public static class ProcessRuntimeProjectionKeys
{
    public static ProcessProjectionKeyPrefix LivePrefix { get; } = new("live:run:");

    public static ProcessProjectionKey Live(ProcessRunId runId) => new($"{LivePrefix.Value}{runId}");

    public static ProcessProjectionKey RunDetail(ProcessRunId runId) => new($"run-detail:{runId}");

    public static ProcessProjectionKey RuntimeCanvas(ProcessRunId runId) => new($"runtime-canvas:{runId}");

    public static ProcessProjectionKey ArtifactMap(ProcessRunId runId) => new($"artifact-map:{runId}");

    public static ProcessProjectionKey Timeline(long globalSequence) => new($"timeline:{globalSequence:D20}");
}
