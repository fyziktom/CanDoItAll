using System.Text.RegularExpressions;
using CanDoItAll.Mcp.DotNetWatch.Configuration;
using CanDoItAll.Mcp.DotNetWatch.Diagnostics;
using CanDoItAll.Mcp.DotNetWatch.Health;
using CanDoItAll.Mcp.DotNetWatch.Logging;
using CanDoItAll.Mcp.DotNetWatch.Operations;
using CanDoItAll.Mcp.DotNetWatch.Processes;
using CanDoItAll.Mcp.DotNetWatch.Security;

namespace CanDoItAll.Mcp.DotNetWatch.Runtime;

public sealed class SessionCoordinator(
    RuntimeConfiguration configuration,
    AppRuntimeManager appRuntimeManager,
    WorkspaceExecutionLock executionLock,
    OperationRegistry operationRegistry,
    HttpHealthProbe healthProbe,
    ProcessSupervisor processSupervisor,
    PathGuard pathGuard,
    EnvironmentOverlayFilter environmentOverlayFilter,
    StartFailureDiagnoser diagnoser,
    Persistence.StaleProcessRegistry staleProcessRegistry,
    IProcessTreeTerminator processTreeTerminator,
    ILogger<SessionCoordinator> logger)
{
    public WorkspaceInfoData GetWorkspaceInfo(bool includeConfigSnapshot)
    {
        var activeSession = appRuntimeManager.GetActiveSession()?.ToStatusData();
        var activeOperations = operationRegistry.GetActiveOperations().Select(static operation => operation.ToStatusData()).ToArray();

        return new WorkspaceInfoData(
            configuration.WorkspaceRoot,
            configuration.SolutionPath,
            new DefaultAppInfo(configuration.DefaultApp.ProjectPath, configuration.DefaultApp.Mode, configuration.HealthUrls.Select(static url => url.ToString()).ToArray()),
            configuration.TestProjectPaths,
            activeSession,
            activeOperations,
            Enum.GetNames<WhenAppRunningPolicy>(),
            includeConfigSnapshot ? configuration.CreateRedactedSnapshot() : null);
    }

    public async Task<AppStartData> StartAppAsync(
        string? projectPath,
        AppRunMode? mode,
        string? configurationName,
        string? framework,
        string? launchProfile,
        string? workingDirectory,
        IReadOnlyList<string> arguments,
        IReadOnlyDictionary<string, string?>? environmentOverlay,
        IReadOnlyList<string> urls,
        bool reuseIfCompatible,
        AppStartConflictPolicy conflictPolicy,
        AppWaitCondition waitFor,
        CancellationToken cancellationToken)
    {
        await using var lease = await executionLock.AcquireMutationAsync("app-start", cancellationToken);

        var resolvedProjectPath = pathGuard.ResolveProjectPath(projectPath);
        var template = new AppStartTemplate(
            resolvedProjectPath,
            pathGuard.ResolveWorkingDirectory(workingDirectory, resolvedProjectPath),
            mode ?? configuration.DefaultApp.Mode,
            string.IsNullOrWhiteSpace(configurationName) ? configuration.DefaultApp.Configuration : configurationName,
            string.IsNullOrWhiteSpace(framework) ? configuration.DefaultApp.Framework : framework,
            string.IsNullOrWhiteSpace(launchProfile) ? configuration.DefaultApp.LaunchProfile : launchProfile,
            arguments.Count == 0 ? configuration.DefaultApp.Arguments : arguments,
            environmentOverlayFilter.Merge(configuration.DefaultApp.EnvironmentOverlay, environmentOverlay, configuration.UsePollingFileWatcher),
            urls.Count == 0 ? configuration.DefaultApp.Urls : urls);

        var (session, reused) = await appRuntimeManager.StartAsync(template, reuseIfCompatible, conflictPolicy, cancellationToken);

        if (waitFor != AppWaitCondition.None)
        {
            await WaitForAppAsync(session.SessionId, waitFor, configuration.DefaultAppWaitTimeout, configuration.DefaultPollInterval, null, configuration.DefaultQuietPeriod, null, true, cancellationToken);
        }

        var status = session.ToStatusData();
        return new AppStartData(
            session.SessionId,
            reused,
            session.Mode,
            status.State,
            status.SessionVersion,
            session.ProjectPath,
            status.ObservedUrls,
            session.LogBuffer.CurrentSequence,
            status.LastKnownPid);
    }

    public Task<AppStopData> StopAppAsync(string? sessionId, string reason, bool force, CancellationToken cancellationToken)
    {
        return appRuntimeManager.StopAsync(sessionId, string.IsNullOrWhiteSpace(reason) ? "RequestedByClient" : reason, force, cancellationToken);
    }

    public AppStatusData GetAppStatus(string? sessionId)
    {
        var session = appRuntimeManager.GetById(sessionId)
            ?? throw new ToolInvocationException("SessionNotFound", "No managed app session was found.", new { sessionId });
        return session.ToStatusData();
    }

    public AppLogsData GetAppLogs(string? sessionId, long? cursor, int limit, bool includeStdOut, bool includeStdErr, bool includeSystemEvents)
    {
        var session = appRuntimeManager.GetById(sessionId)
            ?? throw new ToolInvocationException("SessionNotFound", "No managed app session was found.", new { sessionId });

        var result = session.LogBuffer.ReadAfter(cursor, limit, entry =>
        {
            if (entry.Source == "ProcessStdOut")
            {
                return includeStdOut;
            }

            if (entry.Source == "ProcessStdErr")
            {
                return includeStdErr;
            }

            return includeSystemEvents;
        });

        return new AppLogsData(session.SessionId, result.Entries, result.NextCursor, result.Truncated, result.TotalAvailableAfterCursor);
    }

    public async Task<AppWaitData> WaitForAppAsync(
        string? sessionId,
        AppWaitCondition condition,
        TimeSpan timeout,
        TimeSpan pollInterval,
        long? cursor,
        TimeSpan quietPeriod,
        string? logPattern,
        bool caseInsensitive,
        CancellationToken cancellationToken)
    {
        var session = appRuntimeManager.GetById(sessionId)
            ?? throw new ToolInvocationException("SessionNotFound", "No managed app session was found.", new { sessionId });

        var startedAt = DateTimeOffset.UtcNow;
        var regex = string.IsNullOrWhiteSpace(logPattern)
            ? null
            : new Regex(logPattern, caseInsensitive ? RegexOptions.IgnoreCase | RegexOptions.CultureInvariant : RegexOptions.CultureInvariant, TimeSpan.FromSeconds(2));
        LogEntry? matchedLogEntry = null;
        var deadline = startedAt.Add(timeout);

        while (DateTimeOffset.UtcNow <= deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var status = session.ToStatusData();

            if (condition == AppWaitCondition.Running && status.State is AppLifecycleState.Running or AppLifecycleState.Healthy)
            {
                return CreateAppWaitResult(session.SessionId, condition, startedAt, status.State, false, matchedLogEntry, null, session.LogBuffer.CurrentSequence);
            }

            if (condition == AppWaitCondition.Healthy)
            {
                if (status.State == AppLifecycleState.Healthy)
                {
                    return CreateAppWaitResult(session.SessionId, condition, startedAt, status.State, false, matchedLogEntry, null, session.LogBuffer.CurrentSequence);
                }

                if (configuration.HealthEnabled)
                {
                    var probe = await healthProbe.ProbeAsync(configuration.HealthUrls, cancellationToken);
                    if (probe.IsReady)
                    {
                        session.MarkHealthy(probe);
                        return CreateAppWaitResult(session.SessionId, condition, startedAt, AppLifecycleState.Healthy, false, matchedLogEntry, null, session.LogBuffer.CurrentSequence);
                    }

                    session.MarkHealthFailure(probe);
                }
            }

            if (condition == AppWaitCondition.Stopped &&
                status.State is AppLifecycleState.Stopped or AppLifecycleState.ExitedUnexpectedly or AppLifecycleState.Failed)
            {
                return CreateAppWaitResult(session.SessionId, condition, startedAt, status.State, false, matchedLogEntry, null, session.LogBuffer.CurrentSequence);
            }

            if (condition == AppWaitCondition.QuietSinceCursor)
            {
                var effectiveCursor = cursor ?? session.LogBuffer.CurrentSequence;
                var entriesAfterCursor = session.LogBuffer.GetAfter(effectiveCursor);
                var mostRecentEntry = entriesAfterCursor.LastOrDefault();
                if (mostRecentEntry is null)
                {
                    if (DateTimeOffset.UtcNow - startedAt >= quietPeriod)
                    {
                        return CreateAppWaitResult(session.SessionId, condition, startedAt, status.State, false, matchedLogEntry, null, session.LogBuffer.CurrentSequence);
                    }
                }
                else if (DateTimeOffset.UtcNow - mostRecentEntry.TimestampUtc >= quietPeriod)
                {
                    return CreateAppWaitResult(session.SessionId, condition, startedAt, status.State, false, matchedLogEntry, null, session.LogBuffer.CurrentSequence);
                }
            }

            if (condition == AppWaitCondition.LogMatch && regex is not null)
            {
                matchedLogEntry = session.LogBuffer.GetAfter(cursor ?? 0).FirstOrDefault(entry => regex.IsMatch(entry.Text));
                if (matchedLogEntry is not null)
                {
                    return CreateAppWaitResult(session.SessionId, condition, startedAt, status.State, false, matchedLogEntry, null, matchedLogEntry.Sequence);
                }
            }

            await Task.Delay(pollInterval, cancellationToken);
        }

        var currentStatus = session.ToStatusData();
        var hint = condition == AppWaitCondition.Healthy ? "Health probe did not succeed within timeout." : null;
        if (condition == AppWaitCondition.Healthy)
        {
            session.MarkHealthFailure(new HealthSnapshot("Unhealthy", false, null, DateTimeOffset.UtcNow, null, "Health probe did not succeed within timeout.", null, []));
        }

        return CreateAppWaitResult(session.SessionId, condition, startedAt, currentStatus.State, true, matchedLogEntry, hint, session.LogBuffer.CurrentSequence);
    }

    public async Task<OperationStartData> StartBuildAsync(
        string? targetPath,
        string? configurationName,
        string? framework,
        IReadOnlyList<string> arguments,
        IReadOnlyDictionary<string, string?>? environmentOverlay,
        WhenAppRunningPolicy whenAppRunning,
        bool waitForCompletion,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        return await StartOperationAsync(
            OperationType.Build,
            pathGuard.ResolveTargetPath(targetPath, configuration.BuildDefaultTargetPath),
            string.IsNullOrWhiteSpace(configurationName) ? configuration.DefaultApp.Configuration : configurationName,
            framework,
            arguments.Count == 0 ? configuration.BuildExtraArguments : arguments,
            environmentOverlay,
            whenAppRunning,
            configuration.TestRunnerPreference,
            timeout,
            waitForCompletion,
            cancellationToken);
    }

    public async Task<OperationStartData> StartTestsAsync(
        string? targetPath,
        string? configurationName,
        string? framework,
        string? filter,
        IReadOnlyList<string> arguments,
        IReadOnlyDictionary<string, string?>? environmentOverlay,
        WhenAppRunningPolicy whenAppRunning,
        string? runnerPreference,
        bool waitForCompletion,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        List<string> effectiveArguments = [];
        if (arguments.Count > 0)
        {
            effectiveArguments.AddRange(arguments);
        }

        if (!string.IsNullOrWhiteSpace(filter))
        {
            effectiveArguments.Add("--filter");
            effectiveArguments.Add(filter);
        }

        return await StartOperationAsync(
            OperationType.Test,
            pathGuard.ResolveTargetPath(targetPath, configuration.TestDefaultTargetPath ?? configuration.BuildDefaultTargetPath),
            string.IsNullOrWhiteSpace(configurationName) ? configuration.DefaultApp.Configuration : configurationName,
            framework,
            effectiveArguments,
            environmentOverlay,
            whenAppRunning,
            string.IsNullOrWhiteSpace(runnerPreference) ? configuration.TestRunnerPreference : runnerPreference,
            timeout,
            waitForCompletion,
            cancellationToken);
    }

    public OperationStatusData GetOperationStatus(string? operationId)
    {
        var operation = operationRegistry.GetById(operationId)
            ?? throw new ToolInvocationException("OperationNotFound", "No managed operation was found.", new { operationId });
        return operation.ToStatusData();
    }

    public async Task<OperationWaitData> WaitForOperationAsync(string operationId, TimeSpan timeout, TimeSpan pollInterval, CancellationToken cancellationToken)
    {
        var operation = operationRegistry.GetById(operationId)
            ?? throw new ToolInvocationException("OperationNotFound", "No managed operation was found.", new { operationId });
        var startedAt = DateTimeOffset.UtcNow;
        var deadline = startedAt.Add(timeout);

        while (DateTimeOffset.UtcNow <= deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var status = operation.ToStatusData();
            if (status.State is OperationState.Completed or OperationState.Failed or OperationState.TimedOut or OperationState.Cancelled)
            {
                return new OperationWaitData(
                    operation.OperationId,
                    true,
                    false,
                    status.State,
                    status.ElapsedMs,
                    status.ExitCode,
                    status.Summary,
                    status.ResumeOutcome);
            }

            await Task.Delay(pollInterval, cancellationToken);
        }

        var timedOutStatus = operation.ToStatusData();
        return new OperationWaitData(
            operation.OperationId,
            false,
            true,
            timedOutStatus.State,
            timedOutStatus.ElapsedMs,
            timedOutStatus.ExitCode,
            timedOutStatus.Summary,
            timedOutStatus.ResumeOutcome);
    }

    public OperationLogsData GetOperationLogs(string operationId, long? cursor, int limit)
    {
        var operation = operationRegistry.GetById(operationId)
            ?? throw new ToolInvocationException("OperationNotFound", "No managed operation was found.", new { operationId });
        var result = operation.LogBuffer.ReadAfter(cursor, limit);
        return new OperationLogsData(operation.OperationId, result.Entries, result.NextCursor, result.Truncated, result.TotalAvailableAfterCursor);
    }

    public Task<CleanupStaleProcessesData> CleanupStaleProcessesAsync(bool dryRun, CancellationToken cancellationToken)
    {
        return staleProcessRegistry.CleanupAsync(processTreeTerminator, dryRun, cancellationToken);
    }

    public DiagnoseStartFailureData Diagnose(string? sessionId, string? operationId, int maxLogEntries)
    {
        var session = appRuntimeManager.GetById(sessionId);
        var operation = operationRegistry.GetById(operationId) ?? operationRegistry.GetLastFailed();
        if (session is null && operation is null)
        {
            throw new ToolInvocationException("ValidationError", "No failed entity is available for diagnostics.");
        }

        return diagnoser.Diagnose(session, operation, maxLogEntries);
    }

    private async Task<OperationStartData> StartOperationAsync(
        OperationType operationType,
        string targetPath,
        string configurationName,
        string? framework,
        IReadOnlyList<string> arguments,
        IReadOnlyDictionary<string, string?>? environmentOverlay,
        WhenAppRunningPolicy whenAppRunning,
        string? runnerPreference,
        TimeSpan timeout,
        bool waitForCompletion,
        CancellationToken cancellationToken)
    {
        if (whenAppRunning == WhenAppRunningPolicy.ContinueIfSafe)
        {
            throw new ToolInvocationException("UnsupportedPolicy", "ContinueIfSafe is not supported in this MVP implementation.");
        }

        var effectiveTimeout = timeout == TimeSpan.Zero
            ? (operationType == OperationType.Build ? configuration.BuildDefaultTimeout : configuration.TestDefaultTimeout)
            : timeout;

        var operationId = $"op_{Guid.NewGuid():N}";
        var correlationId = $"corr_{Guid.NewGuid():N}";
        var logBuffer = new RingLogBuffer(configuration.LogBufferCapacity);

        AppStartTemplate? resumeTemplate = null;
        string? stoppedSessionId = null;

        var activeSession = appRuntimeManager.GetActiveSession();
        if (activeSession is not null)
        {
            stoppedSessionId = activeSession.SessionId;
            if (whenAppRunning == WhenAppRunningPolicy.Fail)
            {
                throw new ToolInvocationException("RunningSessionConflict", $"Cannot start {operationType} because a managed session is running.", new { sessionId = activeSession.SessionId });
            }

            if (whenAppRunning == WhenAppRunningPolicy.StopAndResume)
            {
                resumeTemplate = activeSession.CreateTemplate();
            }
        }

        var operation = new OperationRecord(
            operationId,
            operationType,
            correlationId,
            targetPath,
            framework,
            configurationName,
            whenAppRunning,
            stoppedSessionId,
            runnerPreference,
            logBuffer,
            effectiveTimeout);
        operationRegistry.Add(operation);

        var lease = await executionLock.AcquireMutationAsync($"{operationType.ToString().ToLowerInvariant()}-{operationId}", cancellationToken);

        if (activeSession is not null && whenAppRunning is WhenAppRunningPolicy.StopAndResume or WhenAppRunningPolicy.StopOnly)
        {
            await appRuntimeManager.StopAsync(activeSession.SessionId, $"{operationType} preemption requested.", force: true, cancellationToken);
        }

        _ = Task.Run(async () =>
        {
            await using var heldLease = lease;
            var environment = environmentOverlayFilter.Merge(
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["DOTNET_CLI_UI_LANGUAGE"] = "en",
                    ["DOTNET_NOLOGO"] = "1",
                    ["DOTNET_SKIP_FIRST_TIME_EXPERIENCE"] = "1",
                    ["DOTNET_CLI_USE_MSBUILD_SERVER"] = "0"
                },
                environmentOverlay,
                includePollingWatcher: false);

            List<string> commandArguments = [operationType == OperationType.Build ? "build" : "test", targetPath, "--configuration", configurationName];
            if (!string.IsNullOrWhiteSpace(framework))
            {
                commandArguments.Add("--framework");
                commandArguments.Add(framework);
            }

            commandArguments.AddRange(arguments);

            try
            {
                var process = await processSupervisor.StartAsync(
                    new ManagedProcessStartInfo(
                        "operation",
                        operationId,
                        "dotnet",
                        commandArguments,
                        configuration.WorkspaceRoot,
                        environment,
                        correlationId,
                        null),
                    operation.LogBuffer,
                    async entry =>
                    {
                        operation.NoteLog(entry);
                        await Task.CompletedTask;
                    },
                    async exitCode =>
                    {
                        operation.MarkCompleted(exitCode, exitCode == 0 ? $"{operationType} succeeded." : $"{operationType} failed.");
                        await Task.CompletedTask;
                    },
                    CancellationToken.None);

                operation.AttachProcess(process);

                using var timeoutCts = new CancellationTokenSource(effectiveTimeout);
                var exitCode = await process.Completion.WaitAsync(timeoutCts.Token);
                if (operation.ToStatusData().State == OperationState.Running)
                {
                    operation.MarkCompleted(exitCode, exitCode == 0 ? $"{operationType} succeeded." : $"{operationType} failed.");
                }
            }
            catch (OperationCanceledException)
            {
                operation.MarkTimedOut();
                var runningProcess = operation.Process;
                if (runningProcess is not null)
                {
                    await runningProcess.StopAsync(force: true, CancellationToken.None);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Managed operation {OperationId} failed", operationId);
                operation.MarkCompleted(-1, $"{operationType} failed: {ex.Message}");
            }
            finally
            {
                if (resumeTemplate is not null)
                {
                    try
                    {
                        var (resumedSession, _) = await appRuntimeManager.StartAsync(resumeTemplate, reuseIfCompatible: false, AppStartConflictPolicy.Replace, CancellationToken.None);
                        operation.SetResumeOutcome(true, true, resumedSession.SessionId);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Failed to resume managed app session after operation {OperationId}", operationId);
                        operation.SetResumeOutcome(true, false, null);
                    }
                }
                else
                {
                    operation.SetResumeOutcome(false, false, null);
                }
            }
        }, CancellationToken.None);

        if (waitForCompletion)
        {
            await WaitForOperationAsync(operationId, effectiveTimeout, configuration.DefaultPollInterval, cancellationToken);
        }

        return new OperationStartData(
            operationId,
            operationType,
            OperationState.Running,
            targetPath,
            runnerPreference,
            new AppPreemptionData(whenAppRunning, stoppedSessionId, resumeTemplate is not null),
            operation.LogBuffer.CurrentSequence);
    }

    private static AppWaitData CreateAppWaitResult(
        string sessionId,
        AppWaitCondition condition,
        DateTimeOffset startedAt,
        AppLifecycleState observedState,
        bool timedOut,
        LogEntry? matchedLogEntry,
        string? diagnosticHint,
        long finalCursor)
    {
        return new AppWaitData(
            sessionId,
            condition,
            !timedOut,
            timedOut,
            (long)(DateTimeOffset.UtcNow - startedAt).TotalMilliseconds,
            observedState,
            finalCursor,
            matchedLogEntry,
            diagnosticHint);
    }
}
