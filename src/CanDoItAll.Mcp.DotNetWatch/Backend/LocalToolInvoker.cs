using CanDoItAll.Mcp.DotNetWatch.Runtime;

namespace CanDoItAll.Mcp.DotNetWatch.Backend;

internal sealed class LocalToolInvoker(SessionCoordinator coordinator, ILogger<LocalToolInvoker> logger) : IDotNetWatchToolInvoker
{
    public Task<ToolEnvelope<WorkspaceInfoData>> WorkspaceInfoAsync(bool includeHistory = false, bool includeConfigSnapshot = false, CancellationToken cancellationToken = default)
        => ExecuteAsync("candoitall_workspace_info", _ => Task.FromResult(coordinator.GetWorkspaceInfo(includeHistory, includeConfigSnapshot)));

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
        => ExecuteAsync("candoitall_app_start", _ => coordinator.StartAppAsync(
            projectPath,
            mode,
            configurationName,
            framework,
            launchProfile,
            workingDirectory,
            arguments ?? [],
            environmentOverlay,
            urls ?? [],
            reuseIfCompatible,
            conflictPolicy,
            waitFor,
            cancellationToken));

    public Task<ToolEnvelope<AppStopData>> AppStopAsync(string? sessionId = null, string reason = "RequestedByClient", bool force = false, CancellationToken cancellationToken = default)
        => ExecuteAsync("candoitall_app_stop", _ => coordinator.StopAppAsync(sessionId, reason, force, cancellationToken));

    public Task<ToolEnvelope<AppStatusData>> AppStatusAsync(string? sessionId = null, CancellationToken cancellationToken = default)
        => ExecuteAsync("candoitall_app_status", _ => Task.FromResult(coordinator.GetAppStatus(sessionId)));

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
        => ExecuteAsync("candoitall_app_wait", _ => coordinator.WaitForAppAsync(
            sessionId,
            condition,
            TimeSpan.FromMilliseconds(timeoutMs),
            TimeSpan.FromMilliseconds(pollIntervalMs),
            cursor,
            TimeSpan.FromMilliseconds(quietPeriodMs),
            logPattern,
            caseInsensitive,
            cancellationToken));

    public Task<ToolEnvelope<AppLogsData>> AppLogsAsync(
        string? sessionId = null,
        long? cursor = null,
        int limit = 200,
        bool includeStdOut = true,
        bool includeStdErr = true,
        bool includeSystemEvents = true,
        LogViewMode view = LogViewMode.AgentOptimized,
        CancellationToken cancellationToken = default)
        => ExecuteAsync("candoitall_app_logs", _ => Task.FromResult(coordinator.GetAppLogs(sessionId, cursor, limit, includeStdOut, includeStdErr, includeSystemEvents, view)));

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
        => ExecuteAsync("candoitall_solution_build", _ => coordinator.StartBuildAsync(
            targetPath,
            configurationName,
            framework,
            arguments ?? [],
            environmentOverlay,
            whenAppRunning,
            timeoutMs.HasValue ? TimeSpan.FromMilliseconds(timeoutMs.Value) : null,
            waitForCompletion,
            cancellationToken));

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
    {
        var effectiveArguments = arguments?.ToList() ?? [];
        if (collectCoverage)
        {
            effectiveArguments.Add("--collect");
            effectiveArguments.Add("XPlat Code Coverage");
        }

        return ExecuteAsync("candoitall_tests_run", _ => coordinator.StartTestsAsync(
            targetPath,
            configurationName,
            framework,
            filter,
            effectiveArguments,
            environmentOverlay,
            whenAppRunning,
            runnerPreference,
            timeoutMs.HasValue ? TimeSpan.FromMilliseconds(timeoutMs.Value) : null,
            waitForCompletion,
            cancellationToken));
    }

    public Task<ToolEnvelope<OperationStatusData>> OperationStatusAsync(string operationId, CancellationToken cancellationToken = default)
        => ExecuteAsync("candoitall_operation_status", _ => Task.FromResult(coordinator.GetOperationStatus(operationId)));

    public Task<ToolEnvelope<OperationWaitData>> OperationWaitAsync(string operationId, int timeoutMs = 1800000, int pollIntervalMs = 500, CancellationToken cancellationToken = default)
        => ExecuteAsync("candoitall_operation_wait", _ => coordinator.WaitForOperationAsync(
            operationId,
            TimeSpan.FromMilliseconds(timeoutMs),
            TimeSpan.FromMilliseconds(pollIntervalMs),
            cancellationToken));

    public Task<ToolEnvelope<OperationLogsData>> OperationLogsAsync(string operationId, long? cursor = null, int limit = 200, LogViewMode view = LogViewMode.AgentOptimized, CancellationToken cancellationToken = default)
        => ExecuteAsync("candoitall_operation_logs", _ => Task.FromResult(coordinator.GetOperationLogs(operationId, cursor, limit, view)));

    public Task<ToolEnvelope<CleanupStaleProcessesData>> CleanupStaleProcessesAsync(bool dryRun = false, CancellationToken cancellationToken = default)
        => ExecuteAsync("candoitall_cleanup_stale_processes", _ => coordinator.CleanupStaleProcessesAsync(dryRun, cancellationToken));

    public Task<ToolEnvelope<DiagnoseStartFailureData>> DiagnoseStartFailureAsync(string? sessionId = null, string? operationId = null, int maxLogEntries = 200, CancellationToken cancellationToken = default)
        => ExecuteAsync("candoitall_diagnose_start_failure", _ => Task.FromResult(coordinator.Diagnose(sessionId, operationId, maxLogEntries)));

    private async Task<ToolEnvelope<T>> ExecuteAsync<T>(string toolName, Func<string, Task<T>> callback)
    {
        var correlationId = CorrelationIdFactory.Create();

        try
        {
            var data = await callback(correlationId);
            return ToolEnvelope<T>.Success(toolName, correlationId, data);
        }
        catch (ToolInvocationException ex)
        {
            logger.LogWarning(ex, "{ToolName} failed with a tool error: {Code}", toolName, ex.Code);
            return ToolEnvelope<T>.Failure(toolName, correlationId, new ToolError(ex.Code, ex.Message, ex.Details));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{ToolName} failed unexpectedly", toolName);
            return ToolEnvelope<T>.Failure(toolName, correlationId, new ToolError("InternalError", ex.Message));
        }
    }
}
