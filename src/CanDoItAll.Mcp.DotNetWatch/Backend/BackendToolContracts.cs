namespace CanDoItAll.Mcp.DotNetWatch.Backend;

public interface IDotNetWatchToolInvoker
{
    Task<ToolEnvelope<WorkspaceInfoData>> WorkspaceInfoAsync(bool includeHistory = false, bool includeConfigSnapshot = false, CancellationToken cancellationToken = default);

    Task<ToolEnvelope<AppStartData>> AppStartAsync(
        string? projectPath = null,
        AppRunMode? mode = null,
        string? configurationName = null,
        string? framework = null,
        string? launchProfile = null,
        string? workingDirectory = null,
        string[]? arguments = null,
        Dictionary<string, string?>? environmentOverlay = null,
        string[]? urls = null,
        bool reuseIfCompatible = true,
        AppStartConflictPolicy conflictPolicy = AppStartConflictPolicy.Fail,
        AppWaitCondition waitFor = AppWaitCondition.None,
        CancellationToken cancellationToken = default);

    Task<ToolEnvelope<AppStopData>> AppStopAsync(
        string? sessionId = null,
        string reason = "RequestedByClient",
        bool force = false,
        CancellationToken cancellationToken = default);

    Task<ToolEnvelope<AppStatusData>> AppStatusAsync(string? sessionId = null, CancellationToken cancellationToken = default);

    Task<ToolEnvelope<AppWaitData>> AppWaitAsync(
        string? sessionId = null,
        AppWaitCondition condition = AppWaitCondition.Healthy,
        int timeoutMs = 120000,
        int pollIntervalMs = 500,
        long? cursor = null,
        int quietPeriodMs = 2000,
        string? logPattern = null,
        bool caseInsensitive = true,
        CancellationToken cancellationToken = default);

    Task<ToolEnvelope<AppLogsData>> AppLogsAsync(
        string? sessionId = null,
        long? cursor = null,
        int limit = 200,
        bool includeStdOut = true,
        bool includeStdErr = true,
        bool includeSystemEvents = true,
        CancellationToken cancellationToken = default);

    Task<ToolEnvelope<OperationStartData>> SolutionBuildAsync(
        string? targetPath = null,
        string? configurationName = null,
        string? framework = null,
        string[]? arguments = null,
        Dictionary<string, string?>? environmentOverlay = null,
        WhenAppRunningPolicy? whenAppRunning = null,
        bool waitForCompletion = false,
        int? timeoutMs = null,
        CancellationToken cancellationToken = default);

    Task<ToolEnvelope<OperationStartData>> TestsRunAsync(
        string? targetPath = null,
        string? configurationName = null,
        string? framework = null,
        string? filter = null,
        string[]? arguments = null,
        Dictionary<string, string?>? environmentOverlay = null,
        bool collectCoverage = false,
        WhenAppRunningPolicy? whenAppRunning = null,
        string? runnerPreference = null,
        bool waitForCompletion = false,
        int? timeoutMs = null,
        CancellationToken cancellationToken = default);

    Task<ToolEnvelope<OperationStatusData>> OperationStatusAsync(string operationId, CancellationToken cancellationToken = default);

    Task<ToolEnvelope<OperationWaitData>> OperationWaitAsync(
        string operationId,
        int timeoutMs = 1800000,
        int pollIntervalMs = 500,
        CancellationToken cancellationToken = default);

    Task<ToolEnvelope<OperationLogsData>> OperationLogsAsync(
        string operationId,
        long? cursor = null,
        int limit = 200,
        CancellationToken cancellationToken = default);

    Task<ToolEnvelope<CleanupStaleProcessesData>> CleanupStaleProcessesAsync(bool dryRun = false, CancellationToken cancellationToken = default);

    Task<ToolEnvelope<DiagnoseStartFailureData>> DiagnoseStartFailureAsync(
        string? sessionId = null,
        string? operationId = null,
        int maxLogEntries = 200,
        CancellationToken cancellationToken = default);
}

public sealed record WorkspaceInfoRequest(bool IncludeHistory = false, bool IncludeConfigSnapshot = false);

public sealed record AppStartRequest(
    string? ProjectPath = null,
    AppRunMode? Mode = null,
    string? ConfigurationName = null,
    string? Framework = null,
    string? LaunchProfile = null,
    string? WorkingDirectory = null,
    string[]? Arguments = null,
    Dictionary<string, string?>? EnvironmentOverlay = null,
    string[]? Urls = null,
    bool ReuseIfCompatible = true,
    AppStartConflictPolicy ConflictPolicy = AppStartConflictPolicy.Fail,
    AppWaitCondition WaitFor = AppWaitCondition.None);

public sealed record AppStopRequest(
    string? SessionId = null,
    string Reason = "RequestedByClient",
    bool Force = false);

public sealed record AppStatusRequest(string? SessionId = null);

public sealed record AppWaitRequest(
    string? SessionId = null,
    AppWaitCondition Condition = AppWaitCondition.Healthy,
    int TimeoutMs = 120000,
    int PollIntervalMs = 500,
    long? Cursor = null,
    int QuietPeriodMs = 2000,
    string? LogPattern = null,
    bool CaseInsensitive = true);

public sealed record AppLogsRequest(
    string? SessionId = null,
    long? Cursor = null,
    int Limit = 200,
    bool IncludeStdOut = true,
    bool IncludeStdErr = true,
    bool IncludeSystemEvents = true);

public sealed record SolutionBuildRequest(
    string? TargetPath = null,
    string? ConfigurationName = null,
    string? Framework = null,
    string[]? Arguments = null,
    Dictionary<string, string?>? EnvironmentOverlay = null,
    WhenAppRunningPolicy? WhenAppRunning = null,
    bool WaitForCompletion = false,
    int? TimeoutMs = null);

public sealed record TestsRunRequest(
    string? TargetPath = null,
    string? ConfigurationName = null,
    string? Framework = null,
    string? Filter = null,
    string[]? Arguments = null,
    Dictionary<string, string?>? EnvironmentOverlay = null,
    bool CollectCoverage = false,
    WhenAppRunningPolicy? WhenAppRunning = null,
    string? RunnerPreference = null,
    bool WaitForCompletion = false,
    int? TimeoutMs = null);

public sealed record OperationStatusRequest(string OperationId);

public sealed record OperationWaitRequest(
    string OperationId,
    int TimeoutMs = 1800000,
    int PollIntervalMs = 500);

public sealed record OperationLogsRequest(
    string OperationId,
    long? Cursor = null,
    int Limit = 200);

public sealed record CleanupStaleProcessesRequest(bool DryRun = false);

public sealed record DiagnoseStartFailureRequest(
    string? SessionId = null,
    string? OperationId = null,
    int MaxLogEntries = 200);
