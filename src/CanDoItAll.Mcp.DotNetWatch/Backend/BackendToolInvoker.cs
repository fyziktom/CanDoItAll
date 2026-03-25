using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.Mcp.DotNetWatch.Bridge;

namespace CanDoItAll.Mcp.DotNetWatch.Backend;

internal sealed class BackendToolInvoker(
    BridgeRepairCoordinator repairCoordinator)
    : IDotNetWatchToolInvoker
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    public Task<ToolEnvelope<WorkspaceInfoData>> WorkspaceInfoAsync(bool includeHistory = false, bool includeConfigSnapshot = false, CancellationToken cancellationToken = default)
        => PostAsync<WorkspaceInfoRequest, WorkspaceInfoData>("workspace-info", new WorkspaceInfoRequest(includeHistory, includeConfigSnapshot), allowRepair: true, attachIdempotencyKey: false, cancellationToken);

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
        => PostAsync<AppStartRequest, AppStartData>(
            "app-start",
            new AppStartRequest(logicalAppId, projectPath, mode, launchType, preferredLane, entryPath, configurationName, framework, launchProfile, workingDirectory, arguments, environmentOverlay, urls, reuseIfCompatible, conflictPolicy, waitFor),
            allowRepair: true,
            attachIdempotencyKey: true,
            cancellationToken);

    public Task<ToolEnvelope<AppStopData>> AppStopAsync(string? sessionId = null, string reason = "RequestedByClient", bool force = false, CancellationToken cancellationToken = default)
        => PostAsync<AppStopRequest, AppStopData>("app-stop", new AppStopRequest(sessionId, reason, force), allowRepair: true, attachIdempotencyKey: true, cancellationToken);

    public Task<ToolEnvelope<AppStatusData>> AppStatusAsync(string? sessionId = null, CancellationToken cancellationToken = default)
        => PostAsync<AppStatusRequest, AppStatusData>("app-status", new AppStatusRequest(sessionId), allowRepair: true, attachIdempotencyKey: false, cancellationToken);

    public Task<ToolEnvelope<AppWaitData>> AppWaitAsync(string? sessionId = null, AppWaitCondition condition = AppWaitCondition.Healthy, int timeoutMs = 120000, int pollIntervalMs = 500, long? cursor = null, int quietPeriodMs = 2000, string? logPattern = null, bool caseInsensitive = true, CancellationToken cancellationToken = default)
        => PostAsync<AppWaitRequest, AppWaitData>("app-wait", new AppWaitRequest(sessionId, condition, timeoutMs, pollIntervalMs, cursor, quietPeriodMs, logPattern, caseInsensitive), allowRepair: true, attachIdempotencyKey: false, cancellationToken);

    public Task<ToolEnvelope<AppLogsData>> AppLogsAsync(string? sessionId = null, long? cursor = null, int limit = 200, bool includeStdOut = true, bool includeStdErr = true, bool includeSystemEvents = true, LogViewMode view = LogViewMode.AgentOptimized, CancellationToken cancellationToken = default)
        => PostAsync<AppLogsRequest, AppLogsData>("app-logs", new AppLogsRequest(sessionId, cursor, limit, includeStdOut, includeStdErr, includeSystemEvents, view), allowRepair: true, attachIdempotencyKey: false, cancellationToken);

    public Task<ToolEnvelope<OperationStartData>> SolutionBuildAsync(string? targetPath = null, string? configurationName = null, string? framework = null, string[]? arguments = null, Dictionary<string, string?>? environmentOverlay = null, WhenAppRunningPolicy? whenAppRunning = null, bool waitForCompletion = false, int? timeoutMs = null, CancellationToken cancellationToken = default)
        => PostAsync<SolutionBuildRequest, OperationStartData>("solution-build", new SolutionBuildRequest(targetPath, configurationName, framework, arguments, environmentOverlay, whenAppRunning, waitForCompletion, timeoutMs), allowRepair: true, attachIdempotencyKey: true, cancellationToken);

    public Task<ToolEnvelope<OperationStartData>> TestsRunAsync(string? targetPath = null, string? configurationName = null, string? framework = null, string? filter = null, string[]? arguments = null, Dictionary<string, string?>? environmentOverlay = null, bool collectCoverage = false, WhenAppRunningPolicy? whenAppRunning = null, string? runnerPreference = null, bool waitForCompletion = false, int? timeoutMs = null, CancellationToken cancellationToken = default)
        => PostAsync<TestsRunRequest, OperationStartData>("tests-run", new TestsRunRequest(targetPath, configurationName, framework, filter, arguments, environmentOverlay, collectCoverage, whenAppRunning, runnerPreference, waitForCompletion, timeoutMs), allowRepair: true, attachIdempotencyKey: true, cancellationToken);

    public Task<ToolEnvelope<OperationStatusData>> OperationStatusAsync(string operationId, CancellationToken cancellationToken = default)
        => PostAsync<OperationStatusRequest, OperationStatusData>("operation-status", new OperationStatusRequest(operationId), allowRepair: true, attachIdempotencyKey: false, cancellationToken);

    public Task<ToolEnvelope<OperationWaitData>> OperationWaitAsync(string operationId, int timeoutMs = 1800000, int pollIntervalMs = 500, CancellationToken cancellationToken = default)
        => PostAsync<OperationWaitRequest, OperationWaitData>("operation-wait", new OperationWaitRequest(operationId, timeoutMs, pollIntervalMs), allowRepair: true, attachIdempotencyKey: false, cancellationToken);

    public Task<ToolEnvelope<OperationLogsData>> OperationLogsAsync(string operationId, long? cursor = null, int limit = 200, LogViewMode view = LogViewMode.AgentOptimized, CancellationToken cancellationToken = default)
        => PostAsync<OperationLogsRequest, OperationLogsData>("operation-logs", new OperationLogsRequest(operationId, cursor, limit, view), allowRepair: true, attachIdempotencyKey: false, cancellationToken);

    public Task<ToolEnvelope<CleanupStaleProcessesData>> CleanupStaleProcessesAsync(bool dryRun = false, CancellationToken cancellationToken = default)
        => PostAsync<CleanupStaleProcessesRequest, CleanupStaleProcessesData>("cleanup-stale-processes", new CleanupStaleProcessesRequest(dryRun), allowRepair: true, attachIdempotencyKey: true, cancellationToken);

    public Task<ToolEnvelope<DiagnoseStartFailureData>> DiagnoseStartFailureAsync(string? sessionId = null, string? operationId = null, int maxLogEntries = 200, CancellationToken cancellationToken = default)
        => PostAsync<DiagnoseStartFailureRequest, DiagnoseStartFailureData>("diagnose-start-failure", new DiagnoseStartFailureRequest(sessionId, operationId, maxLogEntries), allowRepair: true, attachIdempotencyKey: false, cancellationToken);

    public Task<ToolEnvelope<AppEventsData>> AppEventsAsync(string? logicalAppId = null, string? sessionId = null, long? cursor = null, int limit = 200, CancellationToken cancellationToken = default)
        => PostAsync<AppEventsRequest, AppEventsData>("app-events", new AppEventsRequest(logicalAppId, sessionId, cursor, limit), allowRepair: true, attachIdempotencyKey: false, cancellationToken);

    public Task<ToolEnvelope<AtomicUpdateData>> AppUpdateAtomicAsync(string? logicalAppId = null, string? projectPath = null, string configurationName = "Release", string? framework = null, string[]? arguments = null, Dictionary<string, string?>? environmentOverlay = null, bool activateOnSuccess = true, bool keepPreviousRuntimeWarm = true, bool allowRollback = true, int? timeoutMs = null, CancellationToken cancellationToken = default)
        => PostAsync<AtomicUpdateRequest, AtomicUpdateData>("app-update-atomic", new AtomicUpdateRequest(logicalAppId, projectPath, configurationName, framework, arguments, environmentOverlay, activateOnSuccess, keepPreviousRuntimeWarm, allowRollback, timeoutMs), allowRepair: true, attachIdempotencyKey: true, cancellationToken);

    public Task<ToolEnvelope<AtomicRollbackData>> AppRollbackAsync(string? logicalAppId = null, string? transactionId = null, CancellationToken cancellationToken = default)
        => PostAsync<AtomicRollbackRequest, AtomicRollbackData>("app-rollback", new AtomicRollbackRequest(logicalAppId, transactionId), allowRepair: true, attachIdempotencyKey: true, cancellationToken);

    private async Task<ToolEnvelope<TResponse>> PostAsync<TRequest, TResponse>(string route, TRequest request, bool allowRepair, bool attachIdempotencyKey, CancellationToken cancellationToken)
    {
        var toolName = $"candoitall_{route.Replace('-', '_')}";
        var correlationId = CorrelationIdFactory.Create();

        try
        {
            var bridgeResult = await repairCoordinator.SendAsync(route, request!, allowRepair, attachIdempotencyKey, cancellationToken);
            using var response = bridgeResult.Response;
            response.EnsureSuccessStatusCode();

            var envelope = await response.Content.ReadFromJsonAsync<ToolEnvelope<TResponse>>(JsonOptions, cancellationToken)
                ?? throw new InvalidOperationException($"Backend response for route '{route}' was empty.");

            if (typeof(TResponse) == typeof(WorkspaceInfoData) &&
                envelope.Data is WorkspaceInfoData workspaceInfo)
            {
                var updated = workspaceInfo with
                {
                    Bridge = repairCoordinator.CreateStatus()
                };
                return (ToolEnvelope<TResponse>)(object)(envelope with { Data = (TResponse)(object)updated });
            }

            return envelope;
        }
        catch (ToolInvocationException ex)
        {
            return ToolEnvelope<TResponse>.Failure(toolName, correlationId, new ToolError(ex.Code, ex.Message, ex.Details));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return ToolEnvelope<TResponse>.Failure(toolName, correlationId, new ToolError("BridgeRepairFailed", ex.Message));
        }
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
