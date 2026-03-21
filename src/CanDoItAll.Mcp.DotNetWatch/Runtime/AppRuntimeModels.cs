using System.Text.RegularExpressions;
using CanDoItAll.Mcp.DotNetWatch.Configuration;
using CanDoItAll.Mcp.DotNetWatch.Health;
using CanDoItAll.Mcp.DotNetWatch.Security;

namespace CanDoItAll.Mcp.DotNetWatch.Runtime;

public sealed record AppStartTemplate(
    string ProjectPath,
    string WorkingDirectory,
    AppRunMode Mode,
    string Configuration,
    string? Framework,
    string? LaunchProfile,
    IReadOnlyList<string> Arguments,
    IReadOnlyDictionary<string, string> EnvironmentOverlay,
    IReadOnlyList<string> Urls);

public sealed partial class AppSession
{
    private readonly object _gate = new();
    private readonly HashSet<string> _observedUrls = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _recentEvents = [];

    public AppSession(
        string sessionId,
        AppStartTemplate template,
        string correlationId,
        RingLogBuffer logBuffer)
    {
        SessionId = sessionId;
        CorrelationId = correlationId;
        ProjectPath = template.ProjectPath;
        WorkingDirectory = template.WorkingDirectory;
        Mode = template.Mode;
        Configuration = template.Configuration;
        Framework = template.Framework;
        LaunchProfile = template.LaunchProfile;
        Arguments = template.Arguments.ToArray();
        EnvironmentOverlay = new Dictionary<string, string>(template.EnvironmentOverlay, StringComparer.OrdinalIgnoreCase);
        RequestedUrls = template.Urls.ToArray();
        LogBuffer = logBuffer;
        State = AppLifecycleState.Starting;
        SessionVersion = 1;
        LastStartUtc = DateTimeOffset.UtcNow;
        RecordEvent("Session created.");
    }

    public string SessionId { get; }

    public string CorrelationId { get; }

    public string ProjectPath { get; }

    public string WorkingDirectory { get; }

    public AppRunMode Mode { get; }

    public string Configuration { get; }

    public string? Framework { get; }

    public string? LaunchProfile { get; }

    public IReadOnlyList<string> Arguments { get; }

    public IReadOnlyDictionary<string, string> EnvironmentOverlay { get; }

    public IReadOnlyList<string> RequestedUrls { get; }

    public RingLogBuffer LogBuffer { get; }

    public ManagedProcess? Process { get; private set; }

    public AppLifecycleState State { get; private set; }

    public int SessionVersion { get; private set; }

    public int? LastExitCode { get; private set; }

    public DateTimeOffset LastStartUtc { get; private set; }

    public DateTimeOffset? LastRestartUtc { get; private set; }

    public DateTimeOffset? LastStopUtc { get; private set; }

    public HealthSnapshot? LastHealthSnapshot { get; private set; }

    public void AttachProcess(ManagedProcess process)
    {
        lock (_gate)
        {
            Process = process;
        }
    }

    public void NoteLog(LogEntry entry)
    {
        if (ListeningRegex().Match(entry.Text) is { Success: true } match)
        {
            RecordUrl(match.Groups["url"].Value);
            Transition(AppLifecycleState.Running, "Application reported a listening URL.");
            return;
        }

        if (entry.Text.Contains("Restarting", StringComparison.OrdinalIgnoreCase))
        {
            lock (_gate)
            {
                SessionVersion++;
                LastRestartUtc = DateTimeOffset.UtcNow;
            }

            Transition(AppLifecycleState.Restarting, "Watch restart detected.");
            return;
        }

        if (entry.Text.Contains("Waiting for a file to change", StringComparison.OrdinalIgnoreCase) ||
            entry.Text.Contains("Hot reload enabled", StringComparison.OrdinalIgnoreCase))
        {
            Transition(AppLifecycleState.Running, "Runtime is running.");
            return;
        }

        if (entry.Text.Contains("Build failed", StringComparison.OrdinalIgnoreCase) ||
            entry.Text.StartsWith("fail:", StringComparison.OrdinalIgnoreCase) ||
            entry.Text.Contains("Unhandled exception", StringComparison.OrdinalIgnoreCase))
        {
            Transition(AppLifecycleState.Failed, "Runtime reported a failure.");
        }
    }

    public void MarkHealthy(HealthSnapshot snapshot)
    {
        lock (_gate)
        {
            LastHealthSnapshot = snapshot;
            foreach (var url in snapshot.ActiveUrls)
            {
                _observedUrls.Add(url);
            }
        }

        Transition(AppLifecycleState.Healthy, snapshot.Summary ?? "Healthy.");
    }

    public void MarkHealthFailure(HealthSnapshot snapshot)
    {
        lock (_gate)
        {
            LastHealthSnapshot = snapshot;
        }
    }

    public void MarkStopping(string reason)
    {
        Transition(AppLifecycleState.Stopping, reason);
    }

    public void MarkStopped(int? exitCode, string reason)
    {
        lock (_gate)
        {
            LastExitCode = exitCode;
            LastStopUtc = DateTimeOffset.UtcNow;
            Process = null;
        }

        Transition(AppLifecycleState.Stopped, reason);
    }

    public void MarkExitedUnexpectedly(int? exitCode)
    {
        lock (_gate)
        {
            LastExitCode = exitCode;
            Process = null;
        }

        Transition(AppLifecycleState.ExitedUnexpectedly, "Managed process exited unexpectedly.");
    }

    public AppStartTemplate CreateTemplate()
    {
        lock (_gate)
        {
            return new AppStartTemplate(
                ProjectPath,
                WorkingDirectory,
                Mode,
                Configuration,
                Framework,
                LaunchProfile,
                Arguments.ToArray(),
                new Dictionary<string, string>(EnvironmentOverlay, StringComparer.OrdinalIgnoreCase),
                RequestedUrls.ToArray());
        }
    }

    public bool IsCompatible(AppStartTemplate template)
    {
        lock (_gate)
        {
            return string.Equals(ProjectPath, template.ProjectPath, StringComparison.OrdinalIgnoreCase) &&
                   Mode == template.Mode &&
                   string.Equals(Configuration, template.Configuration, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(Framework, template.Framework, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(LaunchProfile, template.LaunchProfile, StringComparison.OrdinalIgnoreCase) &&
                   Arguments.SequenceEqual(template.Arguments) &&
                   RequestedUrls.SequenceEqual(template.Urls) &&
                   EnvironmentOverlay.OrderBy(static pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                       .SequenceEqual(template.EnvironmentOverlay.OrderBy(static pair => pair.Key, StringComparer.OrdinalIgnoreCase));
        }
    }

    public AppStatusData ToStatusData()
    {
        lock (_gate)
        {
            return new AppStatusData(
                SessionId,
                CorrelationId,
                State,
                Mode,
                ProjectPath,
                SessionVersion,
                Process?.Pid,
                _observedUrls.ToArray(),
                LastExitCode,
                LastStartUtc,
                LastRestartUtc,
                LastStopUtc,
                LogBuffer.CurrentSequence,
                LastHealthSnapshot is null
                    ? null
                    : new HealthData(
                        LastHealthSnapshot.Status,
                        LastHealthSnapshot.LastSuccessUtc,
                        LastHealthSnapshot.LastFailureUtc,
                        LastHealthSnapshot.LastUrl,
                        LastHealthSnapshot.Summary),
                _recentEvents.ToArray());
        }
    }

    private void RecordUrl(string url)
    {
        lock (_gate)
        {
            _observedUrls.Add(url);
        }

        RecordEvent($"Observed URL: {url}");
    }

    private void Transition(AppLifecycleState state, string reason)
    {
        lock (_gate)
        {
            State = state;
            RecordEvent(reason);
        }
    }

    private void RecordEvent(string text)
    {
        _recentEvents.Add(text);
        while (_recentEvents.Count > 20)
        {
            _recentEvents.RemoveAt(0);
        }
    }

    [GeneratedRegex(@"Now listening on:\s+(?<url>\S+)", RegexOptions.IgnoreCase)]
    private static partial Regex ListeningRegex();
}

public sealed class AppRuntimeManager(
    RuntimeConfiguration configuration,
    ServerInstanceIdentity serverInstanceIdentity,
    EnvironmentOverlayFilter environmentOverlayFilter,
    ProcessSupervisor processSupervisor,
    ILogger<AppRuntimeManager> logger)
{
    private readonly object _gate = new();
    private readonly Dictionary<string, AppSession> _sessions = new(StringComparer.OrdinalIgnoreCase);
    private AppSession? _activeSession;
    private AppSession? _lastSession;

    public AppSession? GetActiveSession()
    {
        lock (_gate)
        {
            return _activeSession;
        }
    }

    public AppSession? GetById(string? sessionId)
    {
        lock (_gate)
        {
            if (string.IsNullOrWhiteSpace(sessionId))
            {
                return _activeSession ?? _lastSession;
            }

            _sessions.TryGetValue(sessionId, out var session);
            return session;
        }
    }

    public IReadOnlyList<AppSession> GetAllSessions()
    {
        lock (_gate)
        {
            return _sessions.Values.OrderByDescending(static session => session.LastStartUtc).ToList();
        }
    }

    public async Task<(AppSession Session, bool Reused)> StartAsync(
        AppStartTemplate template,
        bool reuseIfCompatible,
        AppStartConflictPolicy conflictPolicy,
        CancellationToken cancellationToken)
    {
        AppSession? existing;
        lock (_gate)
        {
            existing = _activeSession;
        }

        if (existing is not null)
        {
            if (reuseIfCompatible && existing.IsCompatible(template))
            {
                return (existing, true);
            }

            if (conflictPolicy == AppStartConflictPolicy.Fail)
            {
                throw new ToolInvocationException("RunningSessionConflict", "A managed app session is already running.", new { existingSessionId = existing.SessionId });
            }

            await StopAsync(existing.SessionId, "Replacing incompatible session.", force: true, cancellationToken);
        }

        var sessionId = $"app_{Guid.NewGuid():N}";
        var correlationId = $"corr_{Guid.NewGuid():N}";
        var session = new AppSession(sessionId, template, correlationId, new RingLogBuffer(configuration.LogBufferCapacity));

        lock (_gate)
        {
            _sessions[sessionId] = session;
            _activeSession = session;
            _lastSession = session;
        }

        var process = await processSupervisor.StartAsync(
            BuildProcessStartInfo(template, session, correlationId),
            session.LogBuffer,
            async entry =>
            {
                session.NoteLog(entry);
                await Task.CompletedTask;
            },
            async exitCode =>
            {
                logger.LogInformation("Managed app session {SessionId} exited with code {ExitCode}", session.SessionId, exitCode);
                session.MarkExitedUnexpectedly(exitCode);
                lock (_gate)
                {
                    if (ReferenceEquals(_activeSession, session))
                    {
                        _activeSession = null;
                    }
                }

                await Task.CompletedTask;
            },
            cancellationToken);

        session.AttachProcess(process);
        return (session, false);
    }

    public async Task<AppStopData> StopAsync(string? sessionId, string reason, bool force, CancellationToken cancellationToken)
    {
        var session = GetById(sessionId)
            ?? throw new ToolInvocationException("SessionNotFound", "No managed session was found.", new { sessionId });

        var process = session.Process;
        if (process is null)
        {
            session.MarkStopped(session.LastExitCode, "Session is already stopped.");
            return new AppStopData(session.SessionId, session.CorrelationId, true, AppLifecycleState.Stopped, true, [], session.LogBuffer.CurrentSequence);
        }

        session.MarkStopping(reason);
        var stopResult = await process.StopAsync(force, cancellationToken);
        session.MarkStopped(stopResult.ExitCode, reason);

        lock (_gate)
        {
            if (ReferenceEquals(_activeSession, session))
            {
                _activeSession = null;
            }

            _lastSession = session;
        }

        return new AppStopData(session.SessionId, session.CorrelationId, true, AppLifecycleState.Stopped, stopResult.Graceful, stopResult.KilledPids, session.LogBuffer.CurrentSequence);
    }

    private ManagedProcessStartInfo BuildProcessStartInfo(AppStartTemplate template, AppSession session, string correlationId)
    {
        var environment = environmentOverlayFilter.Merge(
            GetDefaultEnvironment(template, configuration.UsePollingFileWatcher),
            template.EnvironmentOverlay.ToDictionary(static pair => pair.Key, static pair => (string?)pair.Value, StringComparer.OrdinalIgnoreCase),
            configuration.UsePollingFileWatcher);

        var ownershipMarkers = ManagedProcessMarkers.CreateApplicationArguments("app", session.SessionId, configuration.WorkspaceRoot, serverInstanceIdentity.Id);
        string[] arguments;
        if (template.Mode == AppRunMode.WatchRun)
        {
            List<string> watchArguments = ["watch", "--non-interactive", "--project", template.ProjectPath, "run", "--configuration", template.Configuration];
            if (!string.IsNullOrWhiteSpace(template.Framework))
            {
                watchArguments.Add("--framework");
                watchArguments.Add(template.Framework);
            }

            if (!string.IsNullOrWhiteSpace(template.LaunchProfile))
            {
                watchArguments.Add("--launch-profile");
                watchArguments.Add(template.LaunchProfile);
            }

            if (ownershipMarkers.Count > 0 || template.Arguments.Count > 0)
            {
                watchArguments.Add("--");
                watchArguments.AddRange(ownershipMarkers);
                watchArguments.AddRange(template.Arguments);
            }

            arguments = watchArguments.ToArray();
        }
        else
        {
            List<string> runArguments = ["run", "--project", template.ProjectPath, "--configuration", template.Configuration];
            if (!string.IsNullOrWhiteSpace(template.Framework))
            {
                runArguments.Add("--framework");
                runArguments.Add(template.Framework);
            }

            if (!string.IsNullOrWhiteSpace(template.LaunchProfile))
            {
                runArguments.Add("--launch-profile");
                runArguments.Add(template.LaunchProfile);
            }

            if (ownershipMarkers.Count > 0 || template.Arguments.Count > 0)
            {
                runArguments.Add("--");
                runArguments.AddRange(ownershipMarkers);
                runArguments.AddRange(template.Arguments);
            }

            arguments = runArguments.ToArray();
        }

        return new ManagedProcessStartInfo(
            "app",
            session.SessionId,
            "dotnet",
            arguments,
            template.WorkingDirectory,
            environment,
            correlationId,
            session.SessionVersion);
    }

    private static IReadOnlyDictionary<string, string> GetDefaultEnvironment(AppStartTemplate template, bool usePollingWatcher)
    {
        var environment = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["DOTNET_CLI_UI_LANGUAGE"] = "en",
            ["DOTNET_NOLOGO"] = "1",
            ["DOTNET_SKIP_FIRST_TIME_EXPERIENCE"] = "1",
            ["DOTNET_WATCH_RESTART_ON_RUDE_EDIT"] = "1",
            ["DOTNET_WATCH_SUPPRESS_LAUNCH_BROWSER"] = "1",
            ["DOTNET_WATCH_SUPPRESS_BROWSER_REFRESH"] = "1",
            ["DOTNET_WATCH_SUPPRESS_EMOJIS"] = "1",
            ["DOTNET_CLI_USE_MSBUILD_SERVER"] = "0",
            ["ASPNETCORE_ENVIRONMENT"] = "Development",
            ["DOTNET_ENVIRONMENT"] = "Development"
        };

        if (usePollingWatcher)
        {
            environment["DOTNET_USE_POLLING_FILE_WATCHER"] = "1";
        }

        if (template.Urls.Count > 0)
        {
            environment["ASPNETCORE_URLS"] = string.Join(';', template.Urls);
        }

        return environment;
    }
}
