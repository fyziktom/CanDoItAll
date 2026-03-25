using CanDoItAll.Mcp.DotNetWatch.Configuration;
using CanDoItAll.Mcp.DotNetWatch.Guidance;
using CanDoItAll.Mcp.DotNetWatch.Runtime;

namespace CanDoItAll.Mcp.DotNetWatch.Backend;

internal sealed class LocalToolInvoker(
    SessionCoordinator coordinator,
    RuntimeConfiguration configuration,
    WorkflowGuidancePolicy guidancePolicy,
    ILogger<LocalToolInvoker> logger)
    : IDotNetWatchToolInvoker
{
    public Task<ToolEnvelope<WorkspaceInfoData>> WorkspaceInfoAsync(bool includeHistory = false, bool includeConfigSnapshot = false, CancellationToken cancellationToken = default)
        => ExecuteAsync("candoitall_workspace_info", _ => Task.FromResult(coordinator.GetWorkspaceInfo(includeHistory, includeConfigSnapshot)), guidancePolicy.ForWorkspace);

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
        => ExecuteAsync(
            "candoitall_app_start",
            _ => coordinator.StartAppAsync(
                logicalAppId,
                projectPath,
                mode,
                launchType,
                preferredLane,
                entryPath,
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
                cancellationToken),
            guidance => guidancePolicy.ForApp(ToSyntheticStatus(guidance)));

    public Task<ToolEnvelope<AppStopData>> AppStopAsync(string? sessionId = null, string reason = "RequestedByClient", bool force = false, CancellationToken cancellationToken = default)
        => ExecuteAsync("candoitall_app_stop", _ => coordinator.StopAppAsync(sessionId, reason, force, cancellationToken));

    public Task<ToolEnvelope<AppStatusData>> AppStatusAsync(string? sessionId = null, CancellationToken cancellationToken = default)
        => ExecuteAsync("candoitall_app_status", _ => Task.FromResult(coordinator.GetAppStatus(sessionId)), guidancePolicy.ForApp);

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
            cancellationToken), guidancePolicy.ForWait);

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
        => ExecuteAsync("candoitall_operation_status", _ => Task.FromResult(coordinator.GetOperationStatus(operationId)), guidancePolicy.ForOperation);

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
        => ExecuteAsync("candoitall_diagnose_start_failure", _ => Task.FromResult(coordinator.Diagnose(sessionId, operationId, maxLogEntries)), guidancePolicy.ForDiagnosis);

    public Task<ToolEnvelope<AppEventsData>> AppEventsAsync(string? logicalAppId = null, string? sessionId = null, long? cursor = null, int limit = 200, CancellationToken cancellationToken = default)
        => ExecuteAsync("candoitall_app_events", _ => Task.FromResult(coordinator.GetAppEvents(logicalAppId, sessionId, cursor, limit)));

    public Task<ToolEnvelope<AtomicUpdateData>> AppUpdateAtomicAsync(string? logicalAppId = null, string? projectPath = null, string configurationName = "Release", string? framework = null, string[]? arguments = null, Dictionary<string, string?>? environmentOverlay = null, bool activateOnSuccess = true, bool keepPreviousRuntimeWarm = true, bool allowRollback = true, int? timeoutMs = null, CancellationToken cancellationToken = default)
        => ExecuteAsync("candoitall_app_update_atomic", _ => coordinator.UpdateAppAtomicAsync(
            logicalAppId,
            projectPath,
            configurationName,
            framework,
            arguments ?? [],
            environmentOverlay,
            activateOnSuccess,
            keepPreviousRuntimeWarm,
            allowRollback,
            timeoutMs.HasValue ? TimeSpan.FromMilliseconds(timeoutMs.Value) : configuration.DefaultOperationWaitTimeout,
            cancellationToken), guidancePolicy.ForAtomic);

    public Task<ToolEnvelope<AtomicRollbackData>> AppRollbackAsync(string? logicalAppId = null, string? transactionId = null, CancellationToken cancellationToken = default)
        => ExecuteAsync("candoitall_app_rollback", _ => coordinator.RollbackAppAsync(logicalAppId, transactionId, cancellationToken), guidancePolicy.ForRollback);

    private async Task<ToolEnvelope<T>> ExecuteAsync<T>(string toolName, Func<string, Task<T>> callback, Func<T, WorkflowGuidanceData?>? guidanceFactory = null)
    {
        var correlationId = CorrelationIdFactory.Create();

        try
        {
            var data = await callback(correlationId);
            var envelope = ToolEnvelope<T>.Success(toolName, correlationId, data);
            if (guidanceFactory is not null && guidancePolicy.ShouldEmit(toolName))
            {
                var guidance = guidanceFactory(data);
                if (guidance is not null)
                {
                    envelope = envelope with { WorkflowGuidance = guidance };
                }
            }

            return envelope;
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

    private static AppStatusData ToSyntheticStatus(AppStartData start)
    {
        return new AppStatusData(
            SessionId: start.SessionId,
            CorrelationId: start.CorrelationId,
            State: start.State,
            Mode: start.Mode,
            ProjectPath: start.ProjectPath,
            SessionVersion: start.SessionVersion,
            LastKnownPid: start.LastKnownPid,
            ObservedUrls: start.ObservedUrls,
            LastExitCode: null,
            LastStartUtc: DateTimeOffset.UtcNow,
            LastRestartUtc: null,
            LastStopUtc: null,
            LastCursor: start.InitialCursor,
            Health: null,
            RecentEvents: [],
            Watch: start.Watch)
        {
            LogicalAppId = start.LogicalAppId,
            LaneKind = start.LaneKind,
            Revision = start.Revision,
            SlotId = start.SlotId,
            ActiveTransactionId = start.ActiveTransactionId,
            LaunchType = start.LaunchType
        };
    }
}
