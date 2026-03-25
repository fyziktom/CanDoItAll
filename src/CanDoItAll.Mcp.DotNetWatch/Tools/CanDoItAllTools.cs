using System.ComponentModel;
using CanDoItAll.Mcp.DotNetWatch.Backend;
using ModelContextProtocol.Server;

namespace CanDoItAll.Mcp.DotNetWatch.Tools;

[McpServerToolType]
public sealed class CanDoItAllTools(IDotNetWatchToolInvoker invoker)
{
    [McpServerTool(Name = "candoitall_workspace_info", ReadOnly = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Returns workspace metadata, bridge status, lane capabilities, backend session inventory, and active managed operations for the CanDoItAll development workspace. Prefer one small validated change at a time when watch is healthy.")]
    public Task<ToolEnvelope<WorkspaceInfoData>> WorkspaceInfoAsync(
        bool includeHistory = false,
        bool includeConfigSnapshot = false,
        CancellationToken cancellationToken = default)
        => invoker.WorkspaceInfoAsync(includeHistory, includeConfigSnapshot, cancellationToken);

    [McpServerTool(Name = "candoitall_app_start", ReadOnly = false, Idempotent = false, OpenWorld = false, UseStructuredContent = true)]
    [Description("Starts a managed runtime from a project, published DLL, or executable through the persistent backend. Prefer one nearby change, wait for revision confirmation, then browser-check before widening scope.")]
    public Task<ToolEnvelope<AppStartData>> AppStartAsync(
        string? logicalAppId = null,
        string? projectPath = null,
        AppRunMode? mode = null,
        AppLaunchType launchType = AppLaunchType.Project,
        RuntimeLaneKind? preferredLane = null,
        string? entryPath = null,
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
        CancellationToken cancellationToken = default)
        => invoker.AppStartAsync(logicalAppId, projectPath, mode, launchType, preferredLane, entryPath, configurationName, framework, launchProfile, workingDirectory, arguments, environmentOverlay, urls, reuseIfCompatible, conflictPolicy, waitFor, cancellationToken);

    [McpServerTool(Name = "candoitall_app_stop", ReadOnly = false, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Stops an active backend-owned managed app session. Use explicitly; MCP server re-instancing does not require stopping the app.")]
    public Task<ToolEnvelope<AppStopData>> AppStopAsync(
        string? sessionId = null,
        string reason = "RequestedByClient",
        bool force = false,
        CancellationToken cancellationToken = default)
        => invoker.AppStopAsync(sessionId, reason, force, cancellationToken);

    [McpServerTool(Name = "candoitall_app_status", ReadOnly = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Returns the latest backend-owned managed app session snapshot including logical app id, lane, revision, and atomic rollback state when relevant. Use it to confirm the active revision before making the next nearby change.")]
    public Task<ToolEnvelope<AppStatusData>> AppStatusAsync(string? sessionId = null, CancellationToken cancellationToken = default)
        => invoker.AppStatusAsync(sessionId, cancellationToken);

    [McpServerTool(Name = "candoitall_app_wait", ReadOnly = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Performs a backend-side wait against lifecycle, health, revision, transaction, log, or quiet-period milestones. Prefer waiting for revision confirmation before widening scope.")]
    public Task<ToolEnvelope<AppWaitData>> AppWaitAsync(
        string? sessionId = null,
        AppWaitCondition condition = AppWaitCondition.Healthy,
        int timeoutMs = 120000,
        int pollIntervalMs = 500,
        long? cursor = null,
        int quietPeriodMs = 2000,
        string? logPattern = null,
        bool caseInsensitive = true,
        CancellationToken cancellationToken = default)
        => invoker.AppWaitAsync(sessionId, condition, timeoutMs, pollIntervalMs, cursor, quietPeriodMs, logPattern, caseInsensitive, cancellationToken);

    [McpServerTool(Name = "candoitall_app_logs", ReadOnly = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Reads incrementally from the backend-owned managed app log buffer. Agent-optimized filtering is used by default; pass view=Raw for unfiltered logs.")]
    public Task<ToolEnvelope<AppLogsData>> AppLogsAsync(
        string? sessionId = null,
        long? cursor = null,
        int limit = 200,
        bool includeStdOut = true,
        bool includeStdErr = true,
        bool includeSystemEvents = true,
        LogViewMode view = LogViewMode.AgentOptimized,
        CancellationToken cancellationToken = default)
        => invoker.AppLogsAsync(sessionId, cursor, limit, includeStdOut, includeStdErr, includeSystemEvents, view, cancellationToken);

    [McpServerTool(Name = "candoitall_app_events", ReadOnly = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Reads the structured managed-app event journal incrementally without raw-log parsing.")]
    public Task<ToolEnvelope<AppEventsData>> AppEventsAsync(
        string? logicalAppId = null,
        string? sessionId = null,
        long? cursor = null,
        int limit = 200,
        CancellationToken cancellationToken = default)
        => invoker.AppEventsAsync(logicalAppId, sessionId, cursor, limit, cancellationToken);

    [McpServerTool(Name = "candoitall_app_update_atomic", ReadOnly = false, Idempotent = false, OpenWorld = false, UseStructuredContent = true)]
    [Description("Prepares a published candidate in an inactive slot, validates it on isolated ports, and can commit it as the logical active runtime. Use this when the change set is broader or you need a Codex-safe atomic candidate.")]
    public Task<ToolEnvelope<AtomicUpdateData>> AppUpdateAtomicAsync(
        string? logicalAppId = null,
        string? projectPath = null,
        string configurationName = "Release",
        string? framework = null,
        string[]? arguments = null,
        Dictionary<string, string?>? environmentOverlay = null,
        bool activateOnSuccess = true,
        bool keepPreviousRuntimeWarm = true,
        bool allowRollback = true,
        int? timeoutMs = null,
        CancellationToken cancellationToken = default)
        => invoker.AppUpdateAtomicAsync(logicalAppId, projectPath, configurationName, framework, arguments, environmentOverlay, activateOnSuccess, keepPreviousRuntimeWarm, allowRollback, timeoutMs, cancellationToken);

    [McpServerTool(Name = "candoitall_app_rollback", ReadOnly = false, Idempotent = false, OpenWorld = false, UseStructuredContent = true)]
    [Description("Restores the previous committed logical runtime revision for a managed app when rollback is available.")]
    public Task<ToolEnvelope<AtomicRollbackData>> AppRollbackAsync(
        string? logicalAppId = null,
        string? transactionId = null,
        CancellationToken cancellationToken = default)
        => invoker.AppRollbackAsync(logicalAppId, transactionId, cancellationToken);

    [McpServerTool(Name = "candoitall_solution_build", ReadOnly = false, Idempotent = false, OpenWorld = false, UseStructuredContent = true)]
    [Description("Starts a backend-managed dotnet build operation against the solution or a specified target and applies the configured app preemption policy. Prefer focused validation before broad follow-up edits.")]
    public Task<ToolEnvelope<OperationStartData>> SolutionBuildAsync(
        string? targetPath = null,
        string? configurationName = null,
        string? framework = null,
        string[]? arguments = null,
        Dictionary<string, string?>? environmentOverlay = null,
        WhenAppRunningPolicy? whenAppRunning = null,
        bool waitForCompletion = false,
        int? timeoutMs = null,
        CancellationToken cancellationToken = default)
        => invoker.SolutionBuildAsync(targetPath, configurationName, framework, arguments, environmentOverlay, whenAppRunning, waitForCompletion, timeoutMs, cancellationToken);

    [McpServerTool(Name = "candoitall_tests_run", ReadOnly = false, Idempotent = false, OpenWorld = false, UseStructuredContent = true)]
    [Description("Starts a backend-managed dotnet test operation without using dotnet watch test. Prefer fixing the current failure and rerunning focused validation before widening scope.")]
    public Task<ToolEnvelope<OperationStartData>> TestsRunAsync(
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
        CancellationToken cancellationToken = default)
        => invoker.TestsRunAsync(targetPath, configurationName, framework, filter, arguments, environmentOverlay, collectCoverage, whenAppRunning, runnerPreference, waitForCompletion, timeoutMs, cancellationToken);

    [McpServerTool(Name = "candoitall_operation_status", ReadOnly = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Returns the latest backend-managed build or test operation snapshot.")]
    public Task<ToolEnvelope<OperationStatusData>> OperationStatusAsync(string operationId, CancellationToken cancellationToken = default)
        => invoker.OperationStatusAsync(operationId, cancellationToken);

    [McpServerTool(Name = "candoitall_operation_wait", ReadOnly = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Performs a backend-side wait for a managed build or test operation to finish.")]
    public Task<ToolEnvelope<OperationWaitData>> OperationWaitAsync(
        string operationId,
        int timeoutMs = 1800000,
        int pollIntervalMs = 500,
        CancellationToken cancellationToken = default)
        => invoker.OperationWaitAsync(operationId, timeoutMs, pollIntervalMs, cancellationToken);

    [McpServerTool(Name = "candoitall_operation_logs", ReadOnly = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Reads incrementally from the backend-managed build or test operation log buffer. Agent-optimized filtering is used by default; pass view=Raw for unfiltered logs.")]
    public Task<ToolEnvelope<OperationLogsData>> OperationLogsAsync(
        string operationId,
        long? cursor = null,
        int limit = 200,
        LogViewMode view = LogViewMode.AgentOptimized,
        CancellationToken cancellationToken = default)
        => invoker.OperationLogsAsync(operationId, cursor, limit, view, cancellationToken);

    [McpServerTool(Name = "candoitall_cleanup_stale_processes", ReadOnly = false, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Cleans up stale managed processes that survived a previous backend crash or session termination.")]
    public Task<ToolEnvelope<CleanupStaleProcessesData>> CleanupStaleProcessesAsync(bool dryRun = false, CancellationToken cancellationToken = default)
        => invoker.CleanupStaleProcessesAsync(dryRun, cancellationToken);

    [McpServerTool(Name = "candoitall_diagnose_start_failure", ReadOnly = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Diagnoses the latest failed managed app, build, or test flow using recent backend logs and runtime state.")]
    public Task<ToolEnvelope<DiagnoseStartFailureData>> DiagnoseStartFailureAsync(
        string? sessionId = null,
        string? operationId = null,
        int maxLogEntries = 200,
        CancellationToken cancellationToken = default)
        => invoker.DiagnoseStartFailureAsync(sessionId, operationId, maxLogEntries, cancellationToken);
}
