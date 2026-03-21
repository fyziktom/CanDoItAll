using System.Text.RegularExpressions;
using CanDoItAll.Mcp.DotNetWatch.Configuration;
using CanDoItAll.Mcp.DotNetWatch.Diagnostics;
using CanDoItAll.Mcp.DotNetWatch.Health;
using CanDoItAll.Mcp.DotNetWatch.Operations;
using CanDoItAll.Mcp.DotNetWatch.Security;

namespace CanDoItAll.Mcp.DotNetWatch.Runtime;

public sealed class SessionCoordinator(
    RuntimeConfiguration configuration,
    ServerInstanceIdentity serverInstanceIdentity,
    AppRuntimeManager appRuntimeManager,
    WorkspaceExecutionLock executionLock,
    OperationRegistry operationRegistry,
    HttpHealthProbe healthProbe,
    ProcessSupervisor processSupervisor,
    PathGuard pathGuard,
    EnvironmentOverlayFilter environmentOverlayFilter,
    StartFailureDiagnoser diagnoser,
    StaleProcessRegistry staleProcessRegistry,
    IProcessTreeTerminator processTreeTerminator,
    ILogger<SessionCoordinator> logger)
{
    private const int WorkspaceHistoryLimit = 10;

    public WorkspaceInfoData GetWorkspaceInfo(bool includeHistory, bool includeConfigSnapshot)
    {
        var activeSession = appRuntimeManager.GetActiveSession()?.ToStatusData();
        var activeOperations = operationRegistry.GetActiveOperations().Select(static operation => operation.ToStatusData()).ToArray();
        var history = includeHistory
            ? new WorkspaceHistoryData(
                appRuntimeManager.GetAllSessions().Take(WorkspaceHistoryLimit).Select(static session => session.ToStatusData()).ToArray(),
                operationRegistry.GetAllOperations().Take(WorkspaceHistoryLimit).Select(static operation => operation.ToStatusData()).ToArray())
            : null;

        return new WorkspaceInfoData(
            new WorkspacePathInfo(configuration.WorkspaceRoot, "."),
            CreatePathInfo(configuration.SolutionPath),
            new DefaultAppInfo(
                configuration.DefaultApp.ProjectPath,
                configuration.GetRelativePath(configuration.DefaultApp.ProjectPath),
                configuration.DefaultApp.Mode,
                configuration.HealthUrls.Select(static url => url.ToString()).ToArray()),
            configuration.TestProjectPaths.Select(CreatePathInfo).ToArray(),
            activeSession,
            activeOperations,
            Enum.GetNames<WhenAppRunningPolicy>(),
            includeConfigSnapshot ? configuration.CreateRedactedSnapshot() : null,
            history);
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
            var waitResult = await WaitForAppAsync(
                session.SessionId,
                waitFor,
                configuration.DefaultAppWaitTimeout,
                configuration.DefaultPollInterval,
                null,
                configuration.DefaultQuietPeriod,
                null,
                true,
                cancellationToken);

            if (!waitResult.Satisfied)
            {
                throw new ToolInvocationException(
                    CreateAppWaitFailureCode(waitResult),
                    $"Managed app session did not satisfy wait condition '{waitFor}'.",
                    waitResult);
            }
        }

        var status = session.ToStatusData();
        return new AppStartData(
            session.SessionId,
            session.CorrelationId,
            reused,
            session.Mode,
            status.State,
            status.SessionVersion,
            session.ProjectPath,
            status.ObservedUrls,
            session.LogBuffer.CurrentSequence,
            status.LastKnownPid,
            status.Watch);
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
        var deadline = startedAt.Add(timeout);
        var regex = string.IsNullOrWhiteSpace(logPattern)
            ? null
            : new Regex(logPattern, caseInsensitive ? RegexOptions.IgnoreCase | RegexOptions.CultureInvariant : RegexOptions.CultureInvariant, TimeSpan.FromSeconds(2));
        LogEntry? matchedLogEntry = null;
        var quietCursor = cursor ?? session.LogBuffer.CurrentSequence;
        var baselineStatus = session.ToStatusData();
        var restartBaselineVersion = baselineStatus.SessionVersion;
        var baselineLastRestartUtc = baselineStatus.LastRestartUtc;
        var baselineConfirmedIteration = baselineStatus.Watch?.ConfirmedWatchIteration;
        var baselineRuntimePid = baselineStatus.Watch?.RuntimePid;

        while (DateTimeOffset.UtcNow <= deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var status = session.ToStatusData();

            if (condition == AppWaitCondition.Running &&
                status.State is AppLifecycleState.Running or AppLifecycleState.Healthy or AppLifecycleState.Restarting)
            {
                return CreateAppWaitResult(status, condition, startedAt, true, false, matchedLogEntry, null, session.LogBuffer.CurrentSequence);
            }

            if (condition == AppWaitCondition.Ready)
            {
                if (status.State == AppLifecycleState.Healthy && !IsWatchPending(status))
                {
                    return CreateAppWaitResult(status, condition, startedAt, true, false, matchedLogEntry, null, session.LogBuffer.CurrentSequence);
                }

                if (!configuration.HealthEnabled &&
                    !IsWatchPending(status) &&
                    status.ObservedUrls.Count > 0 &&
                    status.State is AppLifecycleState.Running or AppLifecycleState.Healthy)
                {
                    return CreateAppWaitResult(status, condition, startedAt, true, false, matchedLogEntry, null, session.LogBuffer.CurrentSequence);
                }
            }

            if (condition is AppWaitCondition.Healthy or AppWaitCondition.Ready)
            {
                if (status.State == AppLifecycleState.Healthy && !IsWatchPending(status))
                {
                    return CreateAppWaitResult(status, condition, startedAt, true, false, matchedLogEntry, null, session.LogBuffer.CurrentSequence);
                }

                var healthResult = await EvaluateHealthAsync(session, condition == AppWaitCondition.Ready, cancellationToken);
                if (healthResult.IsSatisfied)
                {
                    status = session.ToStatusData();
                    return CreateAppWaitResult(status, condition, startedAt, true, false, matchedLogEntry, null, session.LogBuffer.CurrentSequence);
                }
            }

            if (condition == AppWaitCondition.Stopped &&
                status.State is AppLifecycleState.Stopped or AppLifecycleState.ExitedUnexpectedly or AppLifecycleState.Failed)
            {
                return CreateAppWaitResult(status, condition, startedAt, true, false, matchedLogEntry, null, session.LogBuffer.CurrentSequence);
            }

            if (condition == AppWaitCondition.RestartCompleted &&
                HasRestartCompleted(status, restartBaselineVersion, baselineLastRestartUtc, baselineConfirmedIteration, baselineRuntimePid))
            {
                if (!configuration.HealthEnabled || status.State == AppLifecycleState.Healthy)
                {
                    return CreateAppWaitResult(status, condition, startedAt, true, false, matchedLogEntry, null, session.LogBuffer.CurrentSequence);
                }

                var restartHealth = await EvaluateHealthAsync(session, readyCanUseObservedUrl: false, cancellationToken);
                if (restartHealth.IsSatisfied)
                {
                    status = session.ToStatusData();
                    return CreateAppWaitResult(status, condition, startedAt, true, false, matchedLogEntry, null, session.LogBuffer.CurrentSequence);
                }
            }

            if (condition is AppWaitCondition.QuietSinceCursor or AppWaitCondition.WatchSettled)
            {
                var quietSatisfied = HasQuietPeriodElapsed(session, quietCursor, quietPeriod, startedAt, requirePostCursorActivity: cursor.HasValue);

                if (quietSatisfied)
                {
                    if (configuration.HealthEnabled &&
                        status.State is AppLifecycleState.Running or AppLifecycleState.Restarting or AppLifecycleState.Healthy)
                    {
                        var healthResult = await EvaluateHealthAsync(session, false, cancellationToken);
                        if (healthResult.IsSatisfied)
                        {
                            status = session.ToStatusData();
                        }
                    }

                    var watchSettled = !IsWatchPending(status);
                    if (watchSettled &&
                        (!configuration.HealthEnabled ||
                         status.State == AppLifecycleState.Healthy ||
                         (condition == AppWaitCondition.WatchSettled && status.State == AppLifecycleState.Running)))
                    {
                        return CreateAppWaitResult(status, condition, startedAt, true, false, matchedLogEntry, null, session.LogBuffer.CurrentSequence);
                    }
                }
            }

            if (condition == AppWaitCondition.LogMatch && regex is not null)
            {
                matchedLogEntry = session.LogBuffer.GetAfter(cursor ?? 0).FirstOrDefault(entry => regex.IsMatch(entry.Text));
                if (matchedLogEntry is not null)
                {
                    return CreateAppWaitResult(status, condition, startedAt, true, false, matchedLogEntry, null, matchedLogEntry.Sequence);
                }
            }

            if (condition != AppWaitCondition.Stopped &&
                status.State is AppLifecycleState.Failed or AppLifecycleState.ExitedUnexpectedly)
            {
                var hint = $"Session moved to '{status.State}' while waiting for '{condition}'.";
                return CreateAppWaitResult(status, condition, startedAt, false, false, matchedLogEntry, hint, session.LogBuffer.CurrentSequence);
            }

            await Task.Delay(pollInterval, cancellationToken);
        }

        var currentStatus = session.ToStatusData();
        var timeoutHint = CreateTimeoutHint(condition);
        if (condition is AppWaitCondition.Healthy or AppWaitCondition.Ready or AppWaitCondition.WatchSettled or AppWaitCondition.RestartCompleted)
        {
            session.MarkHealthFailure(new HealthSnapshot(
                "Unhealthy",
                false,
                currentStatus.Health?.LastSuccessUtc,
                DateTimeOffset.UtcNow,
                currentStatus.Health?.LastUrl,
                timeoutHint,
                currentStatus.Watch?.ConfirmedWatchIteration,
                currentStatus.Watch?.RuntimePid,
                currentStatus.ObservedUrls));
            currentStatus = session.ToStatusData();
        }

        return CreateAppWaitResult(currentStatus, condition, startedAt, false, true, matchedLogEntry, timeoutHint, session.LogBuffer.CurrentSequence);
    }

    public Task<OperationStartData> StartBuildAsync(
        string? targetPath,
        string? configurationName,
        string? framework,
        IReadOnlyList<string> arguments,
        IReadOnlyDictionary<string, string?>? environmentOverlay,
        WhenAppRunningPolicy? whenAppRunning,
        TimeSpan? timeout,
        bool waitForCompletion,
        CancellationToken cancellationToken)
    {
        return StartOperationAsync(
            OperationType.Build,
            pathGuard.ResolveTargetPath(targetPath, configuration.BuildDefaultTargetPath),
            string.IsNullOrWhiteSpace(configurationName) ? configuration.DefaultApp.Configuration : configurationName,
            framework,
            arguments.Count == 0 ? configuration.BuildExtraArguments : arguments,
            environmentOverlay,
            whenAppRunning ?? configuration.BuildDefaultWhenAppRunning,
            runnerPreference: null,
            timeout ?? configuration.BuildDefaultTimeout,
            waitForCompletion,
            cancellationToken);
    }

    public Task<OperationStartData> StartTestsAsync(
        string? targetPath,
        string? configurationName,
        string? framework,
        string? filter,
        IReadOnlyList<string> arguments,
        IReadOnlyDictionary<string, string?>? environmentOverlay,
        WhenAppRunningPolicy? whenAppRunning,
        string? runnerPreference,
        TimeSpan? timeout,
        bool waitForCompletion,
        CancellationToken cancellationToken)
    {
        List<string> effectiveArguments = [];
        if (arguments.Count > 0)
        {
            effectiveArguments.AddRange(arguments);
        }

        var effectiveFilter = string.IsNullOrWhiteSpace(filter) ? configuration.TestDefaultFilter : filter;
        if (!string.IsNullOrWhiteSpace(effectiveFilter))
        {
            effectiveArguments.Add("--filter");
            effectiveArguments.Add(effectiveFilter);
        }

        return StartOperationAsync(
            OperationType.Test,
            pathGuard.ResolveTargetPath(targetPath, configuration.TestDefaultTargetPath ?? configuration.BuildDefaultTargetPath),
            string.IsNullOrWhiteSpace(configurationName) ? configuration.DefaultApp.Configuration : configurationName,
            framework,
            effectiveArguments,
            environmentOverlay,
            whenAppRunning ?? configuration.TestDefaultWhenAppRunning,
            string.IsNullOrWhiteSpace(runnerPreference) ? configuration.TestRunnerPreference : runnerPreference,
            timeout ?? configuration.TestDefaultTimeout,
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
                return CreateOperationWaitResult(status, completed: true, timedOut: false);
            }

            await Task.Delay(pollInterval, cancellationToken);
        }

        return CreateOperationWaitResult(operation.ToStatusData(), completed: false, timedOut: true);
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
        WhenAppRunningPolicy requestedPolicy,
        string? runnerPreference,
        TimeSpan timeout,
        bool waitForCompletion,
        CancellationToken cancellationToken)
    {
        var operationId = $"op_{Guid.NewGuid():N}";
        var correlationId = $"corr_{Guid.NewGuid():N}";
        var logBuffer = new RingLogBuffer(configuration.LogBufferCapacity);
        var effectiveRunner = operationType == OperationType.Test ? ResolveTestRunner(targetPath, runnerPreference) : null;

        AppStartTemplate? resumeTemplate = null;
        string? stoppedSessionId = null;
        var effectivePolicy = requestedPolicy;

        var activeSession = appRuntimeManager.GetActiveSession();
        if (activeSession is not null)
        {
            stoppedSessionId = activeSession.SessionId;
            effectivePolicy = ResolveWhenAppRunningPolicy(requestedPolicy, operationType, targetPath, activeSession);
            if (effectivePolicy == WhenAppRunningPolicy.Fail)
            {
                throw new ToolInvocationException(
                    "RunningSessionConflict",
                    $"Cannot start {operationType} because a managed session is running.",
                    new
                    {
                        sessionId = activeSession.SessionId,
                        currentHolder = executionLock.GetCurrentHolder(),
                        requestedPolicy,
                        effectivePolicy,
                        activeProjectPath = activeSession.ProjectPath,
                        targetPath
                    });
            }

            if (effectivePolicy == WhenAppRunningPolicy.StopAndResume)
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
            effectivePolicy,
            stoppedSessionId,
            effectiveRunner,
            logBuffer,
            timeout);
        operationRegistry.Add(operation);

        var lease = await executionLock.AcquireMutationAsync($"{operationType}:{operationId}", cancellationToken);

        if (activeSession is not null && effectivePolicy is WhenAppRunningPolicy.StopAndResume or WhenAppRunningPolicy.StopOnly)
        {
            await appRuntimeManager.StopAsync(activeSession.SessionId, $"{operationType} preemption requested.", force: false, cancellationToken);
        }

        _ = Task.Run(async () =>
        {
            await using var heldLease = lease;
            var artifactsRoot = Path.Combine(configuration.WorkspaceRoot, ".mcp-state", "artifacts", operationId);
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

            commandArguments.Add("--artifacts-path");
            commandArguments.Add(artifactsRoot);
            commandArguments.AddRange(ManagedProcessMarkers.CreateMsBuildPropertyArguments("operation", operationId, configuration.WorkspaceRoot, serverInstanceIdentity.Id));
            commandArguments.AddRange(arguments);

            if (operationType == OperationType.Test && !commandArguments.Contains("--results-directory", StringComparer.OrdinalIgnoreCase))
            {
                var resultsDirectory = Path.Combine(artifactsRoot, "test-results");
                Directory.CreateDirectory(resultsDirectory);
                commandArguments.Add("--results-directory");
                commandArguments.Add(resultsDirectory);
            }

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

                using var timeoutCts = new CancellationTokenSource(timeout);
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
                operation.SetArtifacts(CollectArtifacts(artifactsRoot));

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
            await WaitForOperationAsync(operationId, timeout, configuration.DefaultPollInterval, cancellationToken);
        }

        var status = operation.ToStatusData();
        return new OperationStartData(
            operationId,
            correlationId,
            status.OperationType,
            status.State,
            targetPath,
            effectiveRunner,
            new AppPreemptionData(effectivePolicy, stoppedSessionId, resumeTemplate is not null),
            operation.LogBuffer.CurrentSequence);
    }

    private OperationStatusData[] GetHistoryOperations()
    {
        return operationRegistry.GetAllOperations().Take(WorkspaceHistoryLimit).Select(static operation => operation.ToStatusData()).ToArray();
    }

    private IEnumerable<OperationArtifactData> CollectArtifacts(string artifactsRoot)
    {
        if (!Directory.Exists(artifactsRoot))
        {
            return [];
        }

        return Directory.GetFiles(artifactsRoot, "*", SearchOption.AllDirectories)
            .Select(path => new OperationArtifactData("file", path, configuration.GetRelativePath(path)))
            .ToArray();
    }

    private WorkspacePathInfo CreatePathInfo(string path)
    {
        return new WorkspacePathInfo(path, configuration.GetRelativePath(path));
    }

    private async Task<(bool IsSatisfied, bool IsReady)> EvaluateHealthAsync(AppSession session, bool readyCanUseObservedUrl, CancellationToken cancellationToken)
    {
        var status = session.ToStatusData();
        if (status.State == AppLifecycleState.Healthy && !IsWatchPending(status))
        {
            return (true, true);
        }

        if (readyCanUseObservedUrl && status.ObservedUrls.Count > 0 && !configuration.HealthEnabled && !IsWatchPending(status))
        {
            return (true, true);
        }

        if (!configuration.HealthEnabled)
        {
            return (false, false);
        }

        var successes = 0;
        for (var attempt = 0; attempt < configuration.StableHealthSuccessCount; attempt++)
        {
            var probe = await healthProbe.ProbeAsync(configuration.HealthUrls, cancellationToken);
            if (!probe.IsReady)
            {
                session.MarkHealthFailure(probe);
                return (false, false);
            }

            if (!session.ConfirmsCurrentGeneration(probe))
            {
                session.MarkHealthObserved(probe, CreatePendingGenerationSummary(session.ToStatusData().Watch, probe));
                return (false, true);
            }

            successes++;
            session.MarkHealthy(probe);
        }

        return (successes >= configuration.StableHealthSuccessCount, true);
    }

    private string ResolveTestRunner(string targetPath, string? runnerPreference)
    {
        if (!string.IsNullOrWhiteSpace(runnerPreference) &&
            !string.Equals(runnerPreference, "Auto", StringComparison.OrdinalIgnoreCase))
        {
            return runnerPreference.Trim();
        }

        try
        {
            if (File.Exists(targetPath))
            {
                var projectText = File.ReadAllText(targetPath);
                if (projectText.Contains("Microsoft.Testing.Platform", StringComparison.OrdinalIgnoreCase) ||
                    projectText.Contains("MSTest.Sdk", StringComparison.OrdinalIgnoreCase) ||
                    projectText.Contains("UseMicrosoftTestingPlatformRunner", StringComparison.OrdinalIgnoreCase))
                {
                    return "MicrosoftTestingPlatform";
                }
            }

            var currentDirectory = File.Exists(targetPath) ? Path.GetDirectoryName(targetPath) : targetPath;
            while (!string.IsNullOrWhiteSpace(currentDirectory) &&
                   currentDirectory.StartsWith(configuration.WorkspaceRoot, StringComparison.OrdinalIgnoreCase))
            {
                var globalJsonPath = Path.Combine(currentDirectory, "global.json");
                if (File.Exists(globalJsonPath))
                {
                    var globalJson = File.ReadAllText(globalJsonPath);
                    if (globalJson.Contains("Microsoft.Testing.Platform", StringComparison.OrdinalIgnoreCase) ||
                        globalJson.Contains("\"test\"", StringComparison.OrdinalIgnoreCase) && globalJson.Contains("runner", StringComparison.OrdinalIgnoreCase))
                    {
                        return "MicrosoftTestingPlatform";
                    }

                    break;
                }

                currentDirectory = Directory.GetParent(currentDirectory)?.FullName;
            }
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Failed to auto-detect test runner for {TargetPath}", targetPath);
        }

        return "VSTest";
    }

    private WhenAppRunningPolicy ResolveWhenAppRunningPolicy(
        WhenAppRunningPolicy requestedPolicy,
        OperationType operationType,
        string targetPath,
        AppSession activeSession)
    {
        if (requestedPolicy != WhenAppRunningPolicy.ContinueIfSafe)
        {
            return requestedPolicy;
        }

        if (string.Equals(targetPath, configuration.SolutionPath, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(targetPath, activeSession.ProjectPath, StringComparison.OrdinalIgnoreCase))
        {
            return WhenAppRunningPolicy.StopAndResume;
        }

        var activeProjectDirectory = Path.GetDirectoryName(activeSession.ProjectPath) ?? configuration.WorkspaceRoot;
        if (targetPath.StartsWith(activeProjectDirectory, StringComparison.OrdinalIgnoreCase))
        {
            return WhenAppRunningPolicy.StopAndResume;
        }

        return operationType == OperationType.Build
            ? WhenAppRunningPolicy.StopAndResume
            : WhenAppRunningPolicy.ContinueIfSafe;
    }

    private static OperationWaitData CreateOperationWaitResult(OperationStatusData status, bool completed, bool timedOut)
    {
        return new OperationWaitData(
            status.OperationId,
            status.CorrelationId,
            completed,
            timedOut,
            status.State,
            status.ElapsedMs,
            status.ExitCode,
            status.Summary,
            status.ResumeOutcome,
            status.TestSummary,
            status.Artifacts);
    }

    private static string CreateTimeoutHint(AppWaitCondition condition)
    {
        return condition switch
        {
            AppWaitCondition.Healthy => "Health probe did not succeed within timeout.",
            AppWaitCondition.Ready => "Application did not become ready within timeout.",
            AppWaitCondition.QuietSinceCursor => "Application logs did not settle within the requested quiet period.",
            AppWaitCondition.WatchSettled => "The active dotnet watch generation did not settle within timeout.",
            AppWaitCondition.RestartCompleted => "The active dotnet watch restart did not complete within timeout.",
            _ => $"Wait condition '{condition}' was not satisfied within timeout."
        };
    }

    private static string CreateAppWaitFailureCode(AppWaitData waitResult)
    {
        if (waitResult.TimedOut)
        {
            return waitResult.Condition switch
            {
                AppWaitCondition.Healthy or AppWaitCondition.Ready => "HealthTimeout",
                _ => "Timeout"
            };
        }

        return waitResult.ObservedState switch
        {
            AppLifecycleState.ExitedUnexpectedly or AppLifecycleState.Failed => "ProcessExitedEarly",
            _ => "ValidationError"
        };
    }

    private static AppWaitData CreateAppWaitResult(
        AppStatusData status,
        AppWaitCondition condition,
        DateTimeOffset startedAt,
        bool satisfied,
        bool timedOut,
        LogEntry? matchedLogEntry,
        string? diagnosticHint,
        long finalCursor)
    {
        return new AppWaitData(
            status.SessionId,
            status.CorrelationId,
            condition,
            satisfied,
            timedOut,
            (long)(DateTimeOffset.UtcNow - startedAt).TotalMilliseconds,
            status.State,
            finalCursor,
            matchedLogEntry,
            diagnosticHint,
            status.Health,
            status.Watch);
    }

    private static bool HasQuietPeriodElapsed(AppSession session, long cursor, TimeSpan quietPeriod, DateTimeOffset startedAt, bool requirePostCursorActivity)
    {
        var entriesAfterCursor = session.LogBuffer.GetAfter(cursor);
        if (requirePostCursorActivity && entriesAfterCursor.Count == 0)
        {
            return false;
        }

        var mostRecentEntry = entriesAfterCursor.LastOrDefault();
        return mostRecentEntry is null
            ? DateTimeOffset.UtcNow - startedAt >= quietPeriod
            : DateTimeOffset.UtcNow - mostRecentEntry.TimestampUtc >= quietPeriod;
    }

    private static bool HasRestartCompleted(
        AppStatusData status,
        int baselineVersion,
        DateTimeOffset? baselineLastRestartUtc,
        int? baselineConfirmedIteration,
        int? baselineRuntimePid)
    {
        if (IsWatchPending(status))
        {
            return false;
        }

        var confirmedIterationAdvanced = status.Watch?.ConfirmedWatchIteration is int confirmedIteration &&
                                         (!baselineConfirmedIteration.HasValue || confirmedIteration > baselineConfirmedIteration.Value);
        var runtimePidChanged = status.Watch?.RuntimePid is int runtimePid &&
                                baselineRuntimePid.HasValue &&
                                runtimePid != baselineRuntimePid.Value;
        var restartUtcAdvanced = status.LastRestartUtc.HasValue &&
                                 (!baselineLastRestartUtc.HasValue || status.LastRestartUtc > baselineLastRestartUtc);

        return status.SessionVersion > baselineVersion ||
               confirmedIterationAdvanced ||
               runtimePidChanged ||
               restartUtcAdvanced;
    }

    private static bool IsWatchPending(AppStatusData status)
        => status.Watch?.PendingChange == true;

    private static string CreatePendingGenerationSummary(WatchStatusData? watch, HealthSnapshot probe)
    {
        if (watch?.ExpectedWatchIteration is int expectedIteration &&
            probe.WatchIteration is int confirmedIteration &&
            confirmedIteration < expectedIteration)
        {
            return $"Runtime is still on watch iteration {confirmedIteration}; waiting for {expectedIteration}.";
        }

        if (watch?.LastHotReloadOutcome == HotReloadOutcome.RestartRequired)
        {
            return "Watch has not yet confirmed the replacement runtime generation.";
        }

        return "Waiting for the active dotnet watch generation to become healthy.";
    }

}
