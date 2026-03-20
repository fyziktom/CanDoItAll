using System.ComponentModel;
using CanDoItAll.Mcp.DotNetWatch.Configuration;
using CanDoItAll.Mcp.DotNetWatch.Runtime;
using ModelContextProtocol.Server;

namespace CanDoItAll.Mcp.DotNetWatch.Tools;

[McpServerToolType]
public sealed class CanDoItAllTools(SessionCoordinator coordinator, ILogger<CanDoItAllTools> logger)
{
    [McpServerTool(Name = "candoitall_workspace_info", ReadOnly = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Returns workspace metadata, configured defaults, and active managed sessions or operations for the CanDoItAll development workspace.")]
    public Task<ToolEnvelope<WorkspaceInfoData>> WorkspaceInfoAsync(
        bool includeHistory = false,
        bool includeConfigSnapshot = false,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync("candoitall_workspace_info", _ =>
        {
            var data = coordinator.GetWorkspaceInfo(includeHistory, includeConfigSnapshot);
            return Task.FromResult(data);
        });
    }

    [McpServerTool(Name = "candoitall_app_start", ReadOnly = false, Idempotent = false, OpenWorld = false, UseStructuredContent = true)]
    [Description("Starts the configured CanDoItAll web app under dotnet watch or dotnet run and returns the managed session metadata.")]
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
    {
        return ExecuteAsync("candoitall_app_start", _ => coordinator.StartAppAsync(
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
    }

    [McpServerTool(Name = "candoitall_app_stop", ReadOnly = false, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Stops the active or specified managed app session and cleans up its process tree.")]
    public Task<ToolEnvelope<AppStopData>> AppStopAsync(
        string? sessionId = null,
        string reason = "RequestedByClient",
        bool force = false,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync("candoitall_app_stop", _ => coordinator.StopAppAsync(sessionId, reason, force, cancellationToken));
    }

    [McpServerTool(Name = "candoitall_app_status", ReadOnly = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Returns the latest managed app session snapshot.")]
    public Task<ToolEnvelope<AppStatusData>> AppStatusAsync(string? sessionId = null, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync("candoitall_app_status", _ => Task.FromResult(coordinator.GetAppStatus(sessionId)));
    }

    [McpServerTool(Name = "candoitall_app_wait", ReadOnly = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Performs a server-side wait against the managed app lifecycle, health signal, log stream, or quiet period.")]
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
    {
        return ExecuteAsync("candoitall_app_wait", _ => coordinator.WaitForAppAsync(
            sessionId,
            condition,
            TimeSpan.FromMilliseconds(timeoutMs),
            TimeSpan.FromMilliseconds(pollIntervalMs),
            cursor,
            TimeSpan.FromMilliseconds(quietPeriodMs),
            logPattern,
            caseInsensitive,
            cancellationToken));
    }

    [McpServerTool(Name = "candoitall_app_logs", ReadOnly = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Reads incrementally from the managed app log buffer.")]
    public Task<ToolEnvelope<AppLogsData>> AppLogsAsync(
        string? sessionId = null,
        long? cursor = null,
        int limit = 200,
        bool includeStdOut = true,
        bool includeStdErr = true,
        bool includeSystemEvents = true,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync("candoitall_app_logs", _ => Task.FromResult(coordinator.GetAppLogs(sessionId, cursor, limit, includeStdOut, includeStdErr, includeSystemEvents)));
    }

    [McpServerTool(Name = "candoitall_solution_build", ReadOnly = false, Idempotent = false, OpenWorld = false, UseStructuredContent = true)]
    [Description("Starts a managed dotnet build operation against the solution or a specified target and applies the configured app preemption policy.")]
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
    {
        return ExecuteAsync("candoitall_solution_build", _ => coordinator.StartBuildAsync(
            targetPath,
            configurationName,
            framework,
            arguments ?? [],
            environmentOverlay,
            whenAppRunning,
            timeoutMs.HasValue ? TimeSpan.FromMilliseconds(timeoutMs.Value) : null,
            waitForCompletion,
            cancellationToken));
    }

    [McpServerTool(Name = "candoitall_tests_run", ReadOnly = false, Idempotent = false, OpenWorld = false, UseStructuredContent = true)]
    [Description("Starts a managed dotnet test operation without using dotnet watch test.")]
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

    [McpServerTool(Name = "candoitall_operation_status", ReadOnly = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Returns the latest build or test operation snapshot.")]
    public Task<ToolEnvelope<OperationStatusData>> OperationStatusAsync(string operationId, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync("candoitall_operation_status", _ => Task.FromResult(coordinator.GetOperationStatus(operationId)));
    }

    [McpServerTool(Name = "candoitall_operation_wait", ReadOnly = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Performs a server-side wait for a managed build or test operation to finish.")]
    public Task<ToolEnvelope<OperationWaitData>> OperationWaitAsync(
        string operationId,
        int timeoutMs = 1800000,
        int pollIntervalMs = 500,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync("candoitall_operation_wait", _ => coordinator.WaitForOperationAsync(
            operationId,
            TimeSpan.FromMilliseconds(timeoutMs),
            TimeSpan.FromMilliseconds(pollIntervalMs),
            cancellationToken));
    }

    [McpServerTool(Name = "candoitall_operation_logs", ReadOnly = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Reads incrementally from the managed build or test operation log buffer.")]
    public Task<ToolEnvelope<OperationLogsData>> OperationLogsAsync(
        string operationId,
        long? cursor = null,
        int limit = 200,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync("candoitall_operation_logs", _ => Task.FromResult(coordinator.GetOperationLogs(operationId, cursor, limit)));
    }

    [McpServerTool(Name = "candoitall_cleanup_stale_processes", ReadOnly = false, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Cleans up stale managed processes that survived a previous MCP server crash or session termination.")]
    public Task<ToolEnvelope<CleanupStaleProcessesData>> CleanupStaleProcessesAsync(bool dryRun = false, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync("candoitall_cleanup_stale_processes", _ => coordinator.CleanupStaleProcessesAsync(dryRun, cancellationToken));
    }

    [McpServerTool(Name = "candoitall_diagnose_start_failure", ReadOnly = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Diagnoses the latest failed managed app start, build, or test flow using recent logs and runtime state.")]
    public Task<ToolEnvelope<DiagnoseStartFailureData>> DiagnoseStartFailureAsync(
        string? sessionId = null,
        string? operationId = null,
        int maxLogEntries = 200,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync("candoitall_diagnose_start_failure", _ => Task.FromResult(coordinator.Diagnose(sessionId, operationId, maxLogEntries)));
    }

    private async Task<ToolEnvelope<T>> ExecuteAsync<T>(string toolName, Func<string, Task<T>> callback)
    {
        var correlationId = $"corr_{Guid.NewGuid():N}";

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
