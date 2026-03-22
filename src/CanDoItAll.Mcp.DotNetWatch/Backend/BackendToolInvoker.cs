using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CanDoItAll.Mcp.DotNetWatch.Backend;

internal sealed class BackendToolInvoker(
    BackendConnectionManager connectionManager,
    IHttpClientFactory httpClientFactory)
    : IDotNetWatchToolInvoker
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    public Task<ToolEnvelope<WorkspaceInfoData>> WorkspaceInfoAsync(bool includeHistory = false, bool includeConfigSnapshot = false, CancellationToken cancellationToken = default)
        => PostAsync<WorkspaceInfoRequest, WorkspaceInfoData>("workspace-info", new WorkspaceInfoRequest(includeHistory, includeConfigSnapshot), cancellationToken);

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
        => PostAsync<AppStartRequest, AppStartData>(
            "app-start",
            new AppStartRequest(projectPath, mode, configurationName, framework, launchProfile, workingDirectory, arguments, environmentOverlay, urls, reuseIfCompatible, conflictPolicy, waitFor),
            cancellationToken);

    public Task<ToolEnvelope<AppStopData>> AppStopAsync(string? sessionId = null, string reason = "RequestedByClient", bool force = false, CancellationToken cancellationToken = default)
        => PostAsync<AppStopRequest, AppStopData>("app-stop", new AppStopRequest(sessionId, reason, force), cancellationToken);

    public Task<ToolEnvelope<AppStatusData>> AppStatusAsync(string? sessionId = null, CancellationToken cancellationToken = default)
        => PostAsync<AppStatusRequest, AppStatusData>("app-status", new AppStatusRequest(sessionId), cancellationToken);

    public Task<ToolEnvelope<AppWaitData>> AppWaitAsync(string? sessionId = null, AppWaitCondition condition = AppWaitCondition.Healthy, int timeoutMs = 120000, int pollIntervalMs = 500, long? cursor = null, int quietPeriodMs = 2000, string? logPattern = null, bool caseInsensitive = true, CancellationToken cancellationToken = default)
        => PostAsync<AppWaitRequest, AppWaitData>("app-wait", new AppWaitRequest(sessionId, condition, timeoutMs, pollIntervalMs, cursor, quietPeriodMs, logPattern, caseInsensitive), cancellationToken);

    public Task<ToolEnvelope<AppLogsData>> AppLogsAsync(string? sessionId = null, long? cursor = null, int limit = 200, bool includeStdOut = true, bool includeStdErr = true, bool includeSystemEvents = true, LogViewMode view = LogViewMode.AgentOptimized, CancellationToken cancellationToken = default)
        => PostAsync<AppLogsRequest, AppLogsData>("app-logs", new AppLogsRequest(sessionId, cursor, limit, includeStdOut, includeStdErr, includeSystemEvents, view), cancellationToken);

    public Task<ToolEnvelope<OperationStartData>> SolutionBuildAsync(string? targetPath = null, string? configurationName = null, string? framework = null, string[]? arguments = null, Dictionary<string, string?>? environmentOverlay = null, WhenAppRunningPolicy? whenAppRunning = null, bool waitForCompletion = false, int? timeoutMs = null, CancellationToken cancellationToken = default)
        => PostAsync<SolutionBuildRequest, OperationStartData>("solution-build", new SolutionBuildRequest(targetPath, configurationName, framework, arguments, environmentOverlay, whenAppRunning, waitForCompletion, timeoutMs), cancellationToken);

    public Task<ToolEnvelope<OperationStartData>> TestsRunAsync(string? targetPath = null, string? configurationName = null, string? framework = null, string? filter = null, string[]? arguments = null, Dictionary<string, string?>? environmentOverlay = null, bool collectCoverage = false, WhenAppRunningPolicy? whenAppRunning = null, string? runnerPreference = null, bool waitForCompletion = false, int? timeoutMs = null, CancellationToken cancellationToken = default)
        => PostAsync<TestsRunRequest, OperationStartData>("tests-run", new TestsRunRequest(targetPath, configurationName, framework, filter, arguments, environmentOverlay, collectCoverage, whenAppRunning, runnerPreference, waitForCompletion, timeoutMs), cancellationToken);

    public Task<ToolEnvelope<OperationStatusData>> OperationStatusAsync(string operationId, CancellationToken cancellationToken = default)
        => PostAsync<OperationStatusRequest, OperationStatusData>("operation-status", new OperationStatusRequest(operationId), cancellationToken);

    public Task<ToolEnvelope<OperationWaitData>> OperationWaitAsync(string operationId, int timeoutMs = 1800000, int pollIntervalMs = 500, CancellationToken cancellationToken = default)
        => PostAsync<OperationWaitRequest, OperationWaitData>("operation-wait", new OperationWaitRequest(operationId, timeoutMs, pollIntervalMs), cancellationToken);

    public Task<ToolEnvelope<OperationLogsData>> OperationLogsAsync(string operationId, long? cursor = null, int limit = 200, LogViewMode view = LogViewMode.AgentOptimized, CancellationToken cancellationToken = default)
        => PostAsync<OperationLogsRequest, OperationLogsData>("operation-logs", new OperationLogsRequest(operationId, cursor, limit, view), cancellationToken);

    public Task<ToolEnvelope<CleanupStaleProcessesData>> CleanupStaleProcessesAsync(bool dryRun = false, CancellationToken cancellationToken = default)
        => PostAsync<CleanupStaleProcessesRequest, CleanupStaleProcessesData>("cleanup-stale-processes", new CleanupStaleProcessesRequest(dryRun), cancellationToken);

    public Task<ToolEnvelope<DiagnoseStartFailureData>> DiagnoseStartFailureAsync(string? sessionId = null, string? operationId = null, int maxLogEntries = 200, CancellationToken cancellationToken = default)
        => PostAsync<DiagnoseStartFailureRequest, DiagnoseStartFailureData>("diagnose-start-failure", new DiagnoseStartFailureRequest(sessionId, operationId, maxLogEntries), cancellationToken);

    private async Task<ToolEnvelope<TResponse>> PostAsync<TRequest, TResponse>(string route, TRequest request, CancellationToken cancellationToken)
    {
        var connection = connectionManager.GetRequiredConnection();
        using var client = httpClientFactory.CreateClient(nameof(BackendToolInvoker));
        client.BaseAddress = new Uri(connection.BaseUrl, UriKind.Absolute);
        client.DefaultRequestHeaders.Add(BackendAuth.HeaderName, connection.AuthToken);

        using var response = await client.PostAsJsonAsync($"/api/tools/{route}", request, JsonOptions, cancellationToken);
        response.EnsureSuccessStatusCode();

        var envelope = await response.Content.ReadFromJsonAsync<ToolEnvelope<TResponse>>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException($"Backend response for route '{route}' was empty.");

        return envelope;
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
