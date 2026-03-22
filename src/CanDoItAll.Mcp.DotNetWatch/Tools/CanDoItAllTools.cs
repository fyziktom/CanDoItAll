using System.ComponentModel;
using CanDoItAll.Mcp.DotNetWatch.Backend;
using CanDoItAll.Mcp.DotNetWatch.Configuration;
using ModelContextProtocol.Server;

namespace CanDoItAll.Mcp.DotNetWatch.Tools;

[McpServerToolType]
public sealed class CanDoItAllTools(IDotNetWatchToolInvoker invoker)
{
    [McpServerTool(Name = "candoitall_workspace_info", ReadOnly = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Returns workspace metadata, configured defaults, backend session inventory, and active managed operations for the CanDoItAll development workspace.")]
    public Task<ToolEnvelope<WorkspaceInfoData>> WorkspaceInfoAsync(
        bool includeHistory = false,
        bool includeConfigSnapshot = false,
        CancellationToken cancellationToken = default)
        => invoker.WorkspaceInfoAsync(includeHistory, includeConfigSnapshot, cancellationToken);

    [McpServerTool(Name = "candoitall_app_start", ReadOnly = false, Idempotent = false, OpenWorld = false, UseStructuredContent = true)]
    [Description("Starts the configured CanDoItAll web app under dotnet watch or dotnet run through the persistent backend. Compatible live sessions are reused by default.")]
    public Task<ToolEnvelope<AppStartData>> AppStartAsync(
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
        CancellationToken cancellationToken = default)
        => invoker.AppStartAsync(projectPath, mode, configurationName, framework, launchProfile, workingDirectory, arguments, environmentOverlay, urls, reuseIfCompatible, conflictPolicy, waitFor, cancellationToken);

    [McpServerTool(Name = "candoitall_app_stop", ReadOnly = false, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Stops an active backend-owned managed app session. Use explicitly; MCP server re-instancing does not require stopping the app.")]
    public Task<ToolEnvelope<AppStopData>> AppStopAsync(
        string? sessionId = null,
        string reason = "RequestedByClient",
        bool force = false,
        CancellationToken cancellationToken = default)
        => invoker.AppStopAsync(sessionId, reason, force, cancellationToken);

    [McpServerTool(Name = "candoitall_app_status", ReadOnly = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Returns the latest backend-owned managed app session snapshot. If no sessionId is supplied, the default live session is used.")]
    public Task<ToolEnvelope<AppStatusData>> AppStatusAsync(string? sessionId = null, CancellationToken cancellationToken = default)
        => invoker.AppStatusAsync(sessionId, cancellationToken);

    [McpServerTool(Name = "candoitall_app_wait", ReadOnly = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Performs a backend-side wait against the managed app lifecycle, health signal, log stream, or quiet period.")]
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
    [Description("Reads incrementally from the backend-owned managed app log buffer.")]
    public Task<ToolEnvelope<AppLogsData>> AppLogsAsync(
        string? sessionId = null,
        long? cursor = null,
        int limit = 200,
        bool includeStdOut = true,
        bool includeStdErr = true,
        bool includeSystemEvents = true,
        CancellationToken cancellationToken = default)
        => invoker.AppLogsAsync(sessionId, cursor, limit, includeStdOut, includeStdErr, includeSystemEvents, cancellationToken);

    [McpServerTool(Name = "candoitall_solution_build", ReadOnly = false, Idempotent = false, OpenWorld = false, UseStructuredContent = true)]
    [Description("Starts a backend-managed dotnet build operation against the solution or a specified target and applies the configured app preemption policy.")]
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
    [Description("Starts a backend-managed dotnet test operation without using dotnet watch test.")]
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
    [Description("Reads incrementally from the backend-managed build or test operation log buffer.")]
    public Task<ToolEnvelope<OperationLogsData>> OperationLogsAsync(
        string operationId,
        long? cursor = null,
        int limit = 200,
        CancellationToken cancellationToken = default)
        => invoker.OperationLogsAsync(operationId, cursor, limit, cancellationToken);

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
