using CanDoItAll.Mcp.Core.Contracts;
using CanDoItAll.Mcp.Core.Observability;
using CanDoItAll.Mcp.LocalRuntime.Persistence;

namespace CanDoItAll.Mcp.DotNetWatch;

public enum AppRunMode
{
    WatchRun,
    RunOnce
}

public enum AppLaunchType
{
    Project,
    PublishedDll,
    Executable
}

public enum RuntimeLaneKind
{
    SourceWatch,
    SourceRun,
    PublishedCandidate,
    PublishedActive,
    ExternalExecutable,
    BuildTest
}

public enum AppStartConflictPolicy
{
    Fail,
    Replace
}

public enum AppLifecycleState
{
    Idle,
    Starting,
    Running,
    Healthy,
    Restarting,
    Stopping,
    Stopped,
    Failed,
    ExitedUnexpectedly
}

public enum AppWaitCondition
{
    None,
    Running,
    Ready,
    Healthy,
    Stopped,
    QuietSinceCursor,
    LogMatch,
    RestartCompleted,
    WatchSettled,
    RevisionConfirmed,
    TransactionPrepared,
    TransactionCommitted,
    RollbackCommitted
}

public enum LogViewMode
{
    AgentOptimized,
    Raw
}

public enum WatchProcessingState
{
    Idle,
    Starting,
    ChangeDetected,
    EvaluatingProjects,
    LoadingProjects,
    Building,
    Launching,
    WaitingForChanges,
    HotReloadSucceeded,
    RestartRequired,
    ChildExited,
    BuildFailed,
    RuntimeFaulted,
    Stopped
}

public enum HotReloadOutcome
{
    None,
    Succeeded,
    Failed,
    RestartRequired
}

public enum WhenAppRunningPolicy
{
    StopAndResume,
    StopOnly,
    Fail,
    ContinueIfSafe
}

public enum OperationType
{
    Build,
    Test
}

public enum OperationState
{
    Queued,
    Running,
    Completed,
    Failed,
    TimedOut,
    Cancelled
}

public enum DiagnosticCategory
{
    PortInUse,
    BuildFailed,
    MissingSdk,
    HealthTimeout,
    ProcessExitedEarly,
    Unknown
}

public sealed record DefaultAppInfo(string ProjectPath, string ProjectPathRelative, AppRunMode Mode, IReadOnlyList<string> HealthUrls);

public sealed record RuntimeRevisionData(
    string Kind,
    string Value,
    DateTimeOffset ObservedUtc,
    bool IsConfirmed);

public sealed record WorkflowGuidanceData(
    string Mode,
    string Next,
    string Verify,
    string? Guard = null,
    string? ReasonCode = null);

public sealed record BridgeStatusData(
    string Mode,
    string? BackendId,
    DateTimeOffset? LastPingUtc,
    DateTimeOffset? LastRepairAttemptUtc,
    string? CurrentShadowSignature,
    string? CurrentShadowDllPath,
    string Health)
{
    public string? CurrentShadowManifestPath { get; init; }
}

public sealed record LaneCapabilityData(
    RuntimeLaneKind LaneKind,
    bool Supported,
    string Summary);

public sealed record SlotSummaryData(
    string LogicalAppId,
    string? ActiveSlotId,
    string? CandidateSlotId,
    string? ActiveTransactionId,
    bool RollbackAvailable);

public sealed record AtomicRuntimeCapabilityData(
    bool Enabled,
    bool RollbackSupported,
    bool EndpointLeasingEnabled);

public sealed record WorkspacePathInfo(string AbsolutePath, string RelativePath);

public sealed record WorkspaceHistoryData(
    IReadOnlyList<AppStatusData> RecentAppSessions,
    IReadOnlyList<OperationStatusData> RecentOperations);

public sealed record WorkspaceInfoData(
    WorkspacePathInfo WorkspaceRoot,
    WorkspacePathInfo SolutionPath,
    DefaultAppInfo DefaultApp,
    IReadOnlyList<WorkspacePathInfo> TestProjects,
    AppStatusData? ActiveAppSession,
    IReadOnlyList<OperationStatusData> ActiveOperations,
    IReadOnlyList<string> SupportedPolicies,
    IReadOnlyDictionary<string, object?>? ConfigSnapshot,
    WorkspaceHistoryData? History)
{
    public IReadOnlyList<AppStatusData> ActiveAppSessions { get; init; } = ActiveAppSession is null ? [] : [ActiveAppSession];

    public BridgeStatusData? Bridge { get; init; }

    public IReadOnlyList<LaneCapabilityData> LaneCapabilities { get; init; } = [];

    public AtomicRuntimeCapabilityData? AtomicRuntime { get; init; }

    public IReadOnlyList<string> ActiveLogicalApps { get; init; } = [];

    public IReadOnlyList<SlotSummaryData> Slots { get; init; } = [];
}

public sealed record HealthData(
    string Status,
    DateTimeOffset? LastSuccessUtc,
    DateTimeOffset? LastFailureUtc,
    string? LastUrl,
    string? Summary,
    bool IsReady,
    int? WatchIteration,
    int? RuntimePid);

public sealed record WatchStatusData(
    WatchProcessingState State,
    string Summary,
    bool PendingChange,
    int? WatcherPid,
    int? RuntimePid,
    int? ExpectedWatchIteration,
    int? ConfirmedWatchIteration,
    HotReloadOutcome LastHotReloadOutcome,
    long? LastActivitySequence,
    DateTimeOffset? LastActivityUtc);

public sealed record AppStatusData(
    string SessionId,
    string CorrelationId,
    AppLifecycleState State,
    AppRunMode Mode,
    string ProjectPath,
    int SessionVersion,
    int? LastKnownPid,
    IReadOnlyList<string> ObservedUrls,
    int? LastExitCode,
    DateTimeOffset LastStartUtc,
    DateTimeOffset? LastRestartUtc,
    DateTimeOffset? LastStopUtc,
    long LastCursor,
    HealthData? Health,
    IReadOnlyList<string> RecentEvents,
    WatchStatusData? Watch)
{
    public string? LogicalAppId { get; init; }

    public RuntimeLaneKind LaneKind { get; init; } = RuntimeLaneKind.SourceWatch;

    public RuntimeRevisionData? Revision { get; init; }

    public string? SlotId { get; init; }

    public string? ActiveTransactionId { get; init; }

    public bool RollbackAvailable { get; init; }

    public AppLaunchType LaunchType { get; init; } = AppLaunchType.Project;

    public string? EntryPath { get; init; }
}

public sealed record AppStartData(
    string SessionId,
    string CorrelationId,
    bool Reused,
    AppRunMode Mode,
    AppLifecycleState State,
    int SessionVersion,
    string ProjectPath,
    IReadOnlyList<string> ObservedUrls,
    long InitialCursor,
    int? LastKnownPid,
    WatchStatusData? Watch)
{
    public string? LogicalAppId { get; init; }

    public RuntimeLaneKind LaneKind { get; init; } = RuntimeLaneKind.SourceWatch;

    public RuntimeRevisionData? Revision { get; init; }

    public string? SlotId { get; init; }

    public string? ActiveTransactionId { get; init; }

    public AppLaunchType LaunchType { get; init; } = AppLaunchType.Project;
}

public sealed record AppStopData(
    string SessionId,
    string CorrelationId,
    bool Stopped,
    AppLifecycleState FinalState,
    bool Graceful,
    IReadOnlyList<int> KilledPids,
    long FinalCursor);

public sealed record AppWaitData(
    string SessionId,
    string CorrelationId,
    AppWaitCondition Condition,
    bool Satisfied,
    bool TimedOut,
    long ElapsedMs,
    AppLifecycleState ObservedState,
    long FinalCursor,
    LogEntry? MatchedLogEntry,
    string? DiagnosticHint,
    HealthData? Health,
    WatchStatusData? Watch)
{
    public string? LogicalAppId { get; init; }

    public RuntimeLaneKind LaneKind { get; init; } = RuntimeLaneKind.SourceWatch;

    public RuntimeRevisionData? Revision { get; init; }

    public string? SlotId { get; init; }

    public string? ActiveTransactionId { get; init; }

    public bool RollbackAvailable { get; init; }
}

public sealed record AppLogsData(
    string SessionId,
    IReadOnlyList<LogEntry> Entries,
    long NextCursor,
    bool Truncated,
    int TotalAvailableAfterCursor,
    LogFilterSummaryData? FilterSummary = null);

public sealed record AppPreemptionData(
    WhenAppRunningPolicy Policy,
    string? StoppedSessionId,
    bool ResumePlanned)
{
    public IReadOnlyList<string> StoppedSessionIds { get; init; } = StoppedSessionId is null ? [] : [StoppedSessionId];
}

public sealed record OperationArtifactData(string Kind, string Path, string RelativePath);

public sealed record OperationStartData(
    string OperationId,
    string CorrelationId,
    OperationType OperationType,
    OperationState State,
    string TargetPath,
    string? Runner,
    AppPreemptionData AppPreemption,
    long InitialCursor);

public sealed record ResumeOutcomeData(
    bool Attempted,
    bool Success,
    string? SessionId)
{
    public IReadOnlyList<string> SessionIds { get; init; } = SessionId is null ? [] : [SessionId];
}

public sealed record TestSummaryData(
    int? Total,
    int? Passed,
    int? Failed,
    int? Skipped);

public sealed record OperationStatusData(
    string OperationId,
    string CorrelationId,
    OperationType OperationType,
    OperationState State,
    DateTimeOffset StartedUtc,
    DateTimeOffset? FinishedUtc,
    long ElapsedMs,
    int? ExitCode,
    string Summary,
    string? Runner,
    ResumeOutcomeData ResumeOutcome,
    long LastCursor,
    TestSummaryData? TestSummary,
    IReadOnlyList<OperationArtifactData> Artifacts)
{
    public RuntimeLaneKind LaneKind { get; init; } = RuntimeLaneKind.BuildTest;
}

public sealed record OperationWaitData(
    string OperationId,
    string CorrelationId,
    bool Completed,
    bool TimedOut,
    OperationState State,
    long ElapsedMs,
    int? ExitCode,
    string Summary,
    ResumeOutcomeData ResumeOutcome,
    TestSummaryData? TestSummary,
    IReadOnlyList<OperationArtifactData> Artifacts)
{
    public RuntimeLaneKind LaneKind { get; init; } = RuntimeLaneKind.BuildTest;
}

public sealed record OperationLogsData(
    string OperationId,
    IReadOnlyList<LogEntry> Entries,
    long NextCursor,
    bool Truncated,
    int TotalAvailableAfterCursor,
    LogFilterSummaryData? FilterSummary = null);

public sealed record LogFilterSummaryData(
    LogViewMode View,
    int ConsumedRawEntryCount,
    int ReturnedEntryCount,
    int SuppressedEntryCount,
    IReadOnlyList<string> Notes);

public sealed record DiagnosticEvidence(long Sequence, string Text);

public sealed record DiagnoseStartFailureData(
    string TargetType,
    string TargetId,
    DiagnosticCategory Category,
    string Confidence,
    string Summary,
    IReadOnlyList<string> RecommendedActions,
    IReadOnlyList<DiagnosticEvidence> Evidence);

public enum AtomicTransactionState
{
    Draft,
    PreparingCandidate,
    CandidateReady,
    Committing,
    Committed,
    RolledBack,
    FailedPrepare,
    FailedCommit,
    Cancelled
}

public sealed record AtomicUpdateData(
    string TransactionId,
    string LogicalAppId,
    string CandidateSessionId,
    string CandidateSlotId,
    string State,
    RuntimeRevisionData CandidateRevision,
    RuntimeRevisionData? ActiveRevision,
    IReadOnlyList<string> ObservedUrls,
    bool Committed,
    bool RollbackAvailable);

public sealed record AtomicRollbackData(
    string LogicalAppId,
    string TransactionId,
    string RestoredSessionId,
    RuntimeRevisionData RestoredRevision,
    RuntimeRevisionData? PreviousRevision,
    bool RollbackAvailable);

public sealed record AppEventData(
    long Sequence,
    DateTimeOffset TimestampUtc,
    string LogicalAppId,
    string SessionId,
    string EventType,
    string Summary,
    RuntimeRevisionData? Revision,
    string? TransactionId,
    string? SlotId);

public sealed record AppEventsData(
    IReadOnlyList<AppEventData> Entries,
    long NextCursor,
    bool Truncated,
    int TotalAvailableAfterCursor);
