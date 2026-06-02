using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Modules.Processes;

public interface IProcessObservationService
{
    Task<ProcessDashboardObservationSnapshot> GetDashboardSnapshotAsync(
        ProcessObservationDashboardQuery query,
        CancellationToken cancellationToken = default);

    Task<ProcessLiveObservationSnapshot> GetLiveSnapshotAsync(
        ProcessLiveObservationQuery query,
        CancellationToken cancellationToken = default);

    Task<ProcessRunObservationSnapshot> GetRunSnapshotAsync(
        ProcessRunObservationQuery query,
        CancellationToken cancellationToken = default);

    Task<ProcessStageObservationSnapshot> GetStageSnapshotAsync(
        ProcessStageObservationQuery query,
        CancellationToken cancellationToken = default);

    Task<ProcessObservationTimelinePage> GetTimelinePageAsync(
        ProcessObservationTimelineQuery query,
        CancellationToken cancellationToken = default);

    Task<ProcessObservationDialogPayload> GetDialogPayloadAsync(
        ProcessObservationDialogQuery query,
        CancellationToken cancellationToken = default);
}

public interface IProcessObservationInvalidator
{
    void NotifyDefinitionChanged(ProcessDefinitionObservationKey key);

    void NotifyRunChanged(ProcessRunObservationKey key);

    void NotifyProjectChanged(Guid? projectId);

    void NotifyAgentExecutionChanged(Guid? processRunId, Guid? processStepRunId);

    void Clear();
}

public readonly record struct ProcessDefinitionObservationKey(
    Guid? ProjectId,
    Guid DefinitionId);

public readonly record struct ProcessRunObservationKey(
    Guid? ProjectId,
    Guid DefinitionId,
    Guid RunId);

public readonly record struct ProcessObservationDefinitionSetKey(string Value)
{
    public static ProcessObservationDefinitionSetKey From(IEnumerable<Guid> definitionIds)
    {
        ArgumentNullException.ThrowIfNull(definitionIds);

        var ids = NormalizeDefinitionIds(definitionIds);
        var values = new string[ids.Count];
        for (var index = 0; index < ids.Count; index++)
        {
            values[index] = ids[index].ToString("N");
        }

        var value = string.Join(",", values);
        return new ProcessObservationDefinitionSetKey(value);
    }

    private static List<Guid> NormalizeDefinitionIds(IEnumerable<Guid> definitionIds)
    {
        var ids = new List<Guid>();
        var seenIds = new HashSet<Guid>();
        foreach (var definitionId in definitionIds)
        {
            if (definitionId == Guid.Empty || !seenIds.Add(definitionId))
            {
                continue;
            }

            ids.Add(definitionId);
        }

        ids.Sort();
        return ids;
    }
}

public sealed record ProcessObservationDashboardQuery(
    Guid? ProjectId,
    IReadOnlyCollection<Guid> DefinitionIds,
    Guid? SelectedDefinitionId = null,
    bool IncludeRuns = false,
    bool IncludeActiveRunSummaries = false,
    bool IncludeAnalytics = false,
    bool ForceRefresh = false)
{
    public IReadOnlyList<Guid> GetNormalizedDefinitionIds()
    {
        var ids = new List<Guid>();
        var seenIds = new HashSet<Guid>();
        foreach (var definitionId in DefinitionIds)
        {
            if (definitionId == Guid.Empty || !seenIds.Add(definitionId))
            {
                continue;
            }

            ids.Add(definitionId);
        }

        ids.Sort();
        return ids;
    }
}

public sealed record ProcessLiveObservationQuery(
    Guid? ProjectId,
    ProcessLiveHistoryWindow HistoryWindow = ProcessLiveHistoryWindow.LiveHour,
    Guid? ProcessRunId = null,
    bool ForceRefresh = false,
    Guid? ProcessDefinitionId = null);

public sealed record ProcessRunObservationQuery(
    Guid RunId,
    Guid? ProjectId = null,
    bool ForceRefresh = false);

public sealed record ProcessStageObservationQuery(
    Guid RunId,
    Guid StepRunId,
    Guid? ProjectId = null,
    bool ForceRefresh = false);

public sealed record ProcessObservationTimelineQuery(
    Guid RunId,
    Guid? StepRunId = null,
    int Skip = 0,
    int Take = 50,
    Guid? ProjectId = null,
    bool ForceRefresh = false);

public sealed record ProcessObservationDialogQuery(
    Guid? ProjectId,
    ProcessObservationDialogDescriptor Descriptor,
    bool ForceRefresh = false);

public sealed record ProcessDashboardObservationSnapshot(
    Guid? ProjectId,
    ProcessRuntimeStateOverview RuntimeStateOverview,
    IReadOnlyList<ProcessRunListItem> Runs,
    IReadOnlyList<ProcessActiveRunSummaryViewModel> ActiveRunSummaries,
    ProcessAnalyticsSummary? Analytics,
    IReadOnlyList<ProcessObservationDialogDescriptor> DialogDescriptors,
    ProcessObservationSnapshotRevision Revision,
    ProcessObservationStaleness Staleness)
{
    public static ProcessDashboardObservationSnapshot Empty(
        Guid? projectId,
        ProcessObservationSnapshotRevision revision,
        ProcessObservationStaleness staleness)
    {
        return new ProcessDashboardObservationSnapshot(
            projectId,
            ProcessRuntimeStateOverview.Empty(projectId),
            [],
            [],
            null,
            [],
            revision,
            staleness);
    }
}

public sealed record ProcessLiveObservationSnapshot(
    Guid? ProjectId,
    ProcessLiveHistoryWindow HistoryWindow,
    Guid? ProcessRunId,
    IReadOnlyList<ProcessLiveProcessOption> ProcessOptions,
    IReadOnlyList<ProcessLiveRunCard> Runs,
    IReadOnlyList<ProcessLiveEscalationCard> Escalations,
    IReadOnlyList<ProcessLiveRunEventCard> RunEvents,
    IReadOnlyList<ProcessLiveAgentCard> ActiveAgents,
    ProcessLiveStats Stats,
    IReadOnlyList<ProcessLiveMetricPoint> MetricPoints,
    IReadOnlyList<ProcessLiveToolUsage> ToolUsage,
    ProcessObservationSnapshotRevision Revision,
    ProcessObservationStaleness Staleness)
{
    public static ProcessLiveObservationSnapshot Empty(
        Guid? projectId,
        ProcessLiveHistoryWindow historyWindow,
        Guid? processRunId,
        ProcessObservationSnapshotRevision revision,
        ProcessObservationStaleness staleness)
    {
        return new ProcessLiveObservationSnapshot(
            projectId,
            historyWindow,
            processRunId,
            [],
            [],
            [],
            [],
            [],
            ProcessLiveStats.Empty,
            [],
            [],
            revision,
            staleness);
    }
}

public sealed record ProcessLiveProcessOption(
    Guid RunId,
    string RunName,
    string DefinitionName,
    ProcessRunStatus Status,
    DateTimeOffset UpdatedAtUtc);

public sealed record ProcessLiveRunCard(
    Guid RunId,
    Guid DefinitionId,
    string DefinitionName,
    string RunName,
    ProcessRunStatus Status,
    DateTimeOffset UpdatedAtUtc,
    int CompletedStepCount,
    int TotalStepCount,
    int BlockedStepCount,
    int CapabilityGapCount,
    decimal EstimatedCost,
    decimal ActualCost,
    int ActiveExecutionCount,
    int PendingApprovalCount,
    int PendingOutboxCount,
    int DeadLetteredOutboxCount,
    int BlockedOrFailedStepCount,
    Guid? ManagerAgentId,
    string ManagerAgentName,
    string HealthSummary);

public sealed record ProcessLiveEscalationCard(
    string Key,
    Guid RunId,
    Guid DefinitionId,
    string DefinitionName,
    string RunName,
    ProcessRunStatus RunStatus,
    Guid EscalationId,
    Guid? StepRunId,
    string StepTitle,
    ProcessEscalationKind Kind,
    ProcessEscalationSeverity Severity,
    ProcessEscalationStatus Status,
    string Title,
    string Reason,
    string Owner,
    string SourceExecutionRunId,
    string SourceApprovalId,
    string SourceToolName,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    DateTimeOffset? DueAtUtc,
    Guid? ManagerAgentId,
    string ManagerAgentName);

public sealed record ProcessLiveRunEventCard(
    string Key,
    Guid RunId,
    Guid DefinitionId,
    string DefinitionName,
    string RunName,
    ProcessRunStatus Status,
    string Title,
    string Summary,
    string Icon,
    DateTimeOffset OccurredAtUtc,
    Guid? ManagerAgentId,
    string ManagerAgentName);

public sealed record ProcessLiveAgentCard(
    Guid RunId,
    string RunName,
    Guid ExecutionRunId,
    Guid AgentId,
    string AgentName,
    string AgentRoleTitle,
    string StepTitle,
    ExecutionState State,
    RunOutcome? Outcome,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    string StatusBadgeText,
    string StatusTone);

public sealed record ProcessLiveStats(
    int ObservedRunCount,
    int RunningRunCount,
    int BlockedRunCount,
    int FailedRunCount,
    int ActiveAgentCount,
    int PendingApprovalCount,
    int PendingOutboxCount,
    int DeadLetteredOutboxCount,
    long DurationMs,
    int InputTokens,
    int CachedInputTokens,
    int OutputTokens,
    int ToolCalls,
    decimal EstimatedCost,
    decimal ActualCost,
    ProviderUsageSummary ProviderUsage)
{
    public static ProcessLiveStats Empty { get; } = new(
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0m,
        0m,
        new ProviderUsageSummary(0, 0, 0, 0, 0, 0, 0, 0, 0m));

    public int TotalTokens => InputTokens + OutputTokens;
}

public sealed record ProcessLiveMetricPoint(
    DateTimeOffset TimestampUtc,
    int InputTokens,
    int CachedInputTokens,
    int OutputTokens,
    long DurationMs,
    int ToolCalls)
{
    public int TotalTokens => InputTokens + OutputTokens;
}

public sealed record ProcessLiveToolUsage(
    string ToolName,
    string ToolFamily,
    int CallCount,
    DateTimeOffset LastUsedAtUtc);

public sealed record ProcessRunObservationSnapshot(
    Guid RunId,
    ProcessWorkspaceRunDetails Details,
    ProcessObservationSnapshotRevision Revision,
    ProcessObservationStaleness Staleness);

public sealed record ProcessStageObservationSnapshot(
    Guid RunId,
    Guid StepRunId,
    ProcessStepRunViewModel? Stage,
    IReadOnlyList<ProcessObservationTimelineItem> Timeline,
    ProcessObservationSnapshotRevision Revision,
    ProcessObservationStaleness Staleness);

public sealed record ProcessObservationTimelinePage(
    Guid RunId,
    Guid? StepRunId,
    int Skip,
    int Take,
    int TotalCount,
    IReadOnlyList<ProcessObservationTimelineItem> Items,
    ProcessObservationSnapshotRevision Revision,
    ProcessObservationStaleness Staleness);

public sealed record ProcessObservationTimelineItem(
    ProcessAttemptTimelineKind Kind,
    Guid? StepRunId,
    string StepTitle,
    Guid? ExecutionRunId,
    Guid? OutboxRecordId,
    Guid? EscalationId,
    string Title,
    string StatusText,
    string Tone,
    string Summary,
    DateTimeOffset OccurredAtUtc);

public sealed record ProcessObservationDialogDescriptor(
    ProcessObservationDialogKind Kind,
    ProcessObservationFocusKind FocusKind,
    Guid? ProcessRunId,
    Guid? StepRunId,
    string Title,
    string Subtitle);

public sealed record ProcessObservationDialogPayload(
    ProcessObservationDialogDescriptor Descriptor,
    ProcessRunObservationSnapshot? RunSnapshot,
    ProcessStageObservationSnapshot? StageSnapshot,
    ProcessObservationTimelinePage? TimelinePage,
    ProcessObservationSnapshotRevision Revision,
    ProcessObservationStaleness Staleness);

public enum ProcessObservationDialogKind
{
    RunDetails,
    RunSteps,
    StageDetails,
    Timeline,
    AgentExecution,
    Escalation,
    Outbox
}

public enum ProcessObservationFocusKind
{
    Dashboard,
    Run,
    Stage,
    QualityReview,
    Timeline,
    AgentExecution,
    Escalation,
    Outbox
}

public enum ProcessLiveHistoryWindow
{
    LiveHour,
    OneDay,
    SevenDays,
    ThirtyDays,
    ThreeMonths,
    OneYear,
    All
}

public enum ProcessObservationFreshness
{
    Fresh,
    Cached
}

public sealed record ProcessObservationSnapshotRevision(
    DateTimeOffset ObservedAtUtc,
    DateTimeOffset? SourceMaxUpdatedAtUtc,
    string Value)
{
    public static ProcessObservationSnapshotRevision Create(
        DateTimeOffset observedAtUtc,
        DateTimeOffset? sourceMaxUpdatedAtUtc = null)
    {
        var revisionSource = sourceMaxUpdatedAtUtc ?? observedAtUtc;
        return new ProcessObservationSnapshotRevision(
            observedAtUtc,
            sourceMaxUpdatedAtUtc,
            revisionSource.UtcTicks.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }
}

public sealed record ProcessObservationStaleness(
    ProcessObservationFreshness Freshness,
    DateTimeOffset ObservedAtUtc,
    DateTimeOffset ExpiresAtUtc)
{
    public TimeSpan AgeAt(DateTimeOffset now)
    {
        return now <= ObservedAtUtc
            ? TimeSpan.Zero
            : now - ObservedAtUtc;
    }

    public bool IsExpired(DateTimeOffset now)
    {
        return now >= ExpiresAtUtc;
    }
}
