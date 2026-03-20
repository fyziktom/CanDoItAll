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
    RestartCompleted
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

public sealed record ToolEnvelope<T>(
    bool Ok,
    string Tool,
    DateTimeOffset TimestampUtc,
    string CorrelationId,
    T? Data,
    IReadOnlyList<string> Warnings,
    ToolError? Error)
{
    public static ToolEnvelope<T> Success(string tool, string correlationId, T data, IReadOnlyList<string>? warnings = null)
    {
        return new ToolEnvelope<T>(
            true,
            tool,
            DateTimeOffset.UtcNow,
            correlationId,
            data,
            warnings ?? [],
            null);
    }

    public static ToolEnvelope<T> Failure(string tool, string correlationId, ToolError error)
    {
        return new ToolEnvelope<T>(
            false,
            tool,
            DateTimeOffset.UtcNow,
            correlationId,
            default,
            [],
            error);
    }
}

public sealed record ToolError(string Code, string Message, object? Details = null);

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
    WorkspaceHistoryData? History);

public sealed record HealthData(
    string Status,
    DateTimeOffset? LastSuccessUtc,
    DateTimeOffset? LastFailureUtc,
    string? LastUrl,
    string? Summary);

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
    IReadOnlyList<string> RecentEvents);

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
    int? LastKnownPid);

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
    Logging.LogEntry? MatchedLogEntry,
    string? DiagnosticHint);

public sealed record AppLogsData(
    string SessionId,
    IReadOnlyList<Logging.LogEntry> Entries,
    long NextCursor,
    bool Truncated,
    int TotalAvailableAfterCursor);

public sealed record AppPreemptionData(
    WhenAppRunningPolicy Policy,
    string? StoppedSessionId,
    bool ResumePlanned);

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
    string? SessionId);

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
    IReadOnlyList<Logging.LogEntry> Entries,
    long NextCursor,
    bool Truncated,
    int TotalAvailableAfterCursor);

public sealed record CleanupKilledProcessData(int Pid, string OwnerKind, string OwnerId);

public sealed record CleanupSkippedProcessData(int Pid, string Reason);

public sealed record CleanupStaleProcessesData(
    int Checked,
    IReadOnlyList<CleanupKilledProcessData> Killed,
    IReadOnlyList<CleanupSkippedProcessData> Skipped,
    bool DryRun);

public sealed record DiagnosticEvidence(long Sequence, string Text);

public sealed record DiagnoseStartFailureData(
    string TargetType,
    string TargetId,
    DiagnosticCategory Category,
    string Confidence,
    string Summary,
    IReadOnlyList<string> RecommendedActions,
    IReadOnlyList<DiagnosticEvidence> Evidence);

public sealed class ToolInvocationException(string code, string message, object? details = null) : Exception(message)
{
    public string Code { get; } = code;

    public object? Details { get; } = details;
}
