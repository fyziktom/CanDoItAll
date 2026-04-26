using CanDoItAll.Mcp.DotNetWatch.Backend;

namespace CanDoItAll.Mcp.DotNetWatch;

internal static partial class Program
{
    private static void MapToolRoutes(WebApplication app, IDotNetWatchToolInvoker invoker)
    {
        app.MapPost("/api/tools/workspace-info", (HttpContext httpContext, WorkspaceInfoRequest request, BackendRequestReplayStore replayStore, CancellationToken cancellationToken) =>
            ExecuteToolRouteAsync(httpContext, replayStore, "workspace-info", request, token => invoker.WorkspaceInfoAsync(request.IncludeHistory, request.IncludeConfigSnapshot, token), cancellationToken));
        app.MapPost("/api/tools/app-start", (HttpContext httpContext, AppStartRequest request, BackendRequestReplayStore replayStore, CancellationToken cancellationToken) =>
            ExecuteToolRouteAsync(httpContext, replayStore, "app-start", request, token => invoker.AppStartAsync(request.LogicalAppId, request.ProjectPath, request.Mode, request.LaunchType, request.PreferredLane, request.EntryPath, request.ConfigurationName, request.Framework, request.LaunchProfile, request.WorkingDirectory, request.Arguments, request.EnvironmentOverlay, request.Urls, request.ReuseIfCompatible, request.ConflictPolicy, request.WaitFor, token), cancellationToken));
        app.MapPost("/api/tools/app-stop", (HttpContext httpContext, AppStopRequest request, BackendRequestReplayStore replayStore, CancellationToken cancellationToken) =>
            ExecuteToolRouteAsync(httpContext, replayStore, "app-stop", request, token => invoker.AppStopAsync(request.SessionId, request.Reason, request.Force, token), cancellationToken));
        app.MapPost("/api/tools/app-status", (HttpContext httpContext, AppStatusRequest request, BackendRequestReplayStore replayStore, CancellationToken cancellationToken) =>
            ExecuteToolRouteAsync(httpContext, replayStore, "app-status", request, token => invoker.AppStatusAsync(request.SessionId, token), cancellationToken));
        app.MapPost("/api/tools/app-wait", (HttpContext httpContext, AppWaitRequest request, BackendRequestReplayStore replayStore, CancellationToken cancellationToken) =>
            ExecuteToolRouteAsync(httpContext, replayStore, "app-wait", request, token => invoker.AppWaitAsync(request.SessionId, request.Condition, request.TimeoutMs, request.PollIntervalMs, request.Cursor, request.QuietPeriodMs, request.LogPattern, request.CaseInsensitive, token), cancellationToken));
        app.MapPost("/api/tools/app-logs", (HttpContext httpContext, AppLogsRequest request, BackendRequestReplayStore replayStore, CancellationToken cancellationToken) =>
            ExecuteToolRouteAsync(httpContext, replayStore, "app-logs", request, token => invoker.AppLogsAsync(request.SessionId, request.Cursor, request.Limit, request.IncludeStdOut, request.IncludeStdErr, request.IncludeSystemEvents, request.View, token), cancellationToken));
        app.MapPost("/api/tools/app-events", (HttpContext httpContext, AppEventsRequest request, BackendRequestReplayStore replayStore, CancellationToken cancellationToken) =>
            ExecuteToolRouteAsync(httpContext, replayStore, "app-events", request, token => invoker.AppEventsAsync(request.LogicalAppId, request.SessionId, request.Cursor, request.Limit, token), cancellationToken));
        app.MapPost("/api/tools/app-update-atomic", (HttpContext httpContext, AtomicUpdateRequest request, BackendRequestReplayStore replayStore, CancellationToken cancellationToken) =>
            ExecuteToolRouteAsync(httpContext, replayStore, "app-update-atomic", request, token => invoker.AppUpdateAtomicAsync(request.LogicalAppId, request.ProjectPath, request.ConfigurationName, request.Framework, request.Arguments, request.EnvironmentOverlay, request.ActivateOnSuccess, request.KeepPreviousRuntimeWarm, request.AllowRollback, request.TimeoutMs, token), cancellationToken));
        app.MapPost("/api/tools/app-rollback", (HttpContext httpContext, AtomicRollbackRequest request, BackendRequestReplayStore replayStore, CancellationToken cancellationToken) =>
            ExecuteToolRouteAsync(httpContext, replayStore, "app-rollback", request, token => invoker.AppRollbackAsync(request.LogicalAppId, request.TransactionId, token), cancellationToken));
        app.MapPost("/api/tools/solution-build", (HttpContext httpContext, SolutionBuildRequest request, BackendRequestReplayStore replayStore, CancellationToken cancellationToken) =>
            ExecuteToolRouteAsync(httpContext, replayStore, "solution-build", request, token => invoker.SolutionBuildAsync(request.TargetPath, request.ConfigurationName, request.Framework, request.Arguments, request.EnvironmentOverlay, request.WhenAppRunning, request.WaitForCompletion, request.TimeoutMs, token), cancellationToken));
        app.MapPost("/api/tools/tests-run", (HttpContext httpContext, TestsRunRequest request, BackendRequestReplayStore replayStore, CancellationToken cancellationToken) =>
            ExecuteToolRouteAsync(httpContext, replayStore, "tests-run", request, token => invoker.TestsRunAsync(request.TargetPath, request.ConfigurationName, request.Framework, request.Filter, request.Arguments, request.EnvironmentOverlay, request.CollectCoverage, request.WhenAppRunning, request.RunnerPreference, request.WaitForCompletion, request.TimeoutMs, token), cancellationToken));
        app.MapPost("/api/tools/operation-status", (HttpContext httpContext, OperationStatusRequest request, BackendRequestReplayStore replayStore, CancellationToken cancellationToken) =>
            ExecuteToolRouteAsync(httpContext, replayStore, "operation-status", request, token => invoker.OperationStatusAsync(request.OperationId, token), cancellationToken));
        app.MapPost("/api/tools/operation-wait", (HttpContext httpContext, OperationWaitRequest request, BackendRequestReplayStore replayStore, CancellationToken cancellationToken) =>
            ExecuteToolRouteAsync(httpContext, replayStore, "operation-wait", request, token => invoker.OperationWaitAsync(request.OperationId, request.TimeoutMs, request.PollIntervalMs, token), cancellationToken));
        app.MapPost("/api/tools/operation-logs", (HttpContext httpContext, OperationLogsRequest request, BackendRequestReplayStore replayStore, CancellationToken cancellationToken) =>
            ExecuteToolRouteAsync(httpContext, replayStore, "operation-logs", request, token => invoker.OperationLogsAsync(request.OperationId, request.Cursor, request.Limit, request.View, token), cancellationToken));
        app.MapPost("/api/tools/cleanup-stale-processes", (HttpContext httpContext, CleanupStaleProcessesRequest request, BackendRequestReplayStore replayStore, CancellationToken cancellationToken) =>
            ExecuteToolRouteAsync(httpContext, replayStore, "cleanup-stale-processes", request, token => invoker.CleanupStaleProcessesAsync(request.DryRun, token), cancellationToken));
        app.MapPost("/api/tools/diagnose-start-failure", (HttpContext httpContext, DiagnoseStartFailureRequest request, BackendRequestReplayStore replayStore, CancellationToken cancellationToken) =>
            ExecuteToolRouteAsync(httpContext, replayStore, "diagnose-start-failure", request, token => invoker.DiagnoseStartFailureAsync(request.SessionId, request.OperationId, request.MaxLogEntries, token), cancellationToken));
    }

    private static async Task<IResult> ExecuteToolRouteAsync<TRequest, TResponse>(
        HttpContext httpContext,
        BackendRequestReplayStore replayStore,
        string route,
        TRequest request,
        Func<CancellationToken, Task<TResponse>> callback,
        CancellationToken cancellationToken)
    {
        var requestId = httpContext.Request.Headers["X-CanDoItAll-RequestId"].FirstOrDefault();
        var json = await replayStore.ExecuteJsonAsync(route, requestId, request, callback, cancellationToken);
        return Results.Text(json, "application/json; charset=utf-8");
    }
}