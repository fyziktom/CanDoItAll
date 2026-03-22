using CanDoItAll.Mcp.Core.Contracts;
using CanDoItAll.Mcp.Core.Observability;
using CanDoItAll.Mcp.LocalRuntime.Persistence;

namespace CanDoItAll.Mcp.DotNetWatch;

public enum AppRunMode
{
    WatchRun,
    RunOnce
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
    WatchSettled
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
    WatchStatusData? Watch);

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
    WatchStatusData? Watch);

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
    WatchStatusData? Watch);

public sealed record AppLogsData(
    string SessionId,
    IReadOnlyList<LogEntry> Entries,
    long NextCursor,
    bool Truncated,
    int TotalAvailableAfterCursor);

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
    IReadOnlyList<OperationArtifactData> Artifacts);

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
    IReadOnlyList<OperationArtifactData> Artifacts);

public sealed record OperationLogsData(
    string OperationId,
    IReadOnlyList<LogEntry> Entries,
    long NextCursor,
    bool Truncated,
    int TotalAvailableAfterCursor);

public sealed record DiagnosticEvidence(long Sequence, string Text);

public sealed record DiagnoseStartFailureData(
    string TargetType,
    string TargetId,
    DiagnosticCategory Category,
    string Confidence,
    string Summary,
    IReadOnlyList<string> RecommendedActions,
    IReadOnlyList<DiagnosticEvidence> Evidence);
