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
    private readonly bool _healthEnabled;
    private WatchProcessingState _watchState;
    private string _watchSummary;
    private bool _watchPendingChange;
    private int? _expectedWatchIteration;
    private int? _confirmedWatchIteration;
    private int? _runtimePid;
    private int? _restartBaselineRuntimePid;
    private HotReloadOutcome _lastHotReloadOutcome;
    private long? _lastWatchActivitySequence;
    private DateTimeOffset? _lastWatchActivityUtc;

    public AppSession(
        string sessionId,
        AppStartTemplate template,
        string correlationId,
        RingLogBuffer logBuffer,
        bool healthEnabled)
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
        _healthEnabled = healthEnabled;
        State = AppLifecycleState.Starting;
        SessionVersion = 1;
        LastStartUtc = DateTimeOffset.UtcNow;
        _watchState = template.Mode == AppRunMode.WatchRun ? WatchProcessingState.Starting : WatchProcessingState.Idle;
        _watchSummary = template.Mode == AppRunMode.WatchRun ? "Watch process starting." : "Run-once session starting.";
        _watchPendingChange = template.Mode == AppRunMode.WatchRun;
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
            HandleListeningUrl(entry, match.Groups["url"].Value);
            return;
        }

        if (Mode == AppRunMode.WatchRun && TryHandleWatchLifecycle(entry))
        {
            return;
        }

        if (entry.Text.Contains("Build failed", StringComparison.OrdinalIgnoreCase))
        {
            MarkWatchFailure(WatchProcessingState.BuildFailed, "Runtime reported a build failure.", entry);
            return;
        }

        if (entry.Text.StartsWith("fail:", StringComparison.OrdinalIgnoreCase) ||
            entry.Text.Contains("Unhandled exception", StringComparison.OrdinalIgnoreCase))
        {
            MarkWatchFailure(WatchProcessingState.RuntimeFaulted, "Runtime reported a failure.", entry);
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

            _runtimePid = snapshot.RuntimePid ?? _runtimePid;
            SyncObservedWatchIterationLocked(snapshot);

            if (Mode == AppRunMode.WatchRun)
            {
                _watchPendingChange = false;
                _watchState = WatchProcessingState.WaitingForChanges;
                _watchSummary = snapshot.Summary ?? "Watch generation is healthy.";
                _lastWatchActivityUtc = DateTimeOffset.UtcNow;
                _restartBaselineRuntimePid = null;
            }
        }

        Transition(AppLifecycleState.Healthy, snapshot.Summary ?? "Healthy.");
    }

    public void MarkHealthObserved(HealthSnapshot snapshot, string summary)
    {
        lock (_gate)
        {
            LastHealthSnapshot = snapshot with
            {
                Status = snapshot.IsReady ? "Pending" : snapshot.Status,
                Summary = summary
            };

            if (snapshot.RuntimePid.HasValue)
            {
                _runtimePid = snapshot.RuntimePid.Value;
            }

            SyncObservedWatchIterationLocked(snapshot);
        }
    }

    public void MarkHealthFailure(HealthSnapshot snapshot)
    {
        lock (_gate)
        {
            LastHealthSnapshot = snapshot;
            if (snapshot.RuntimePid.HasValue)
            {
                _runtimePid = snapshot.RuntimePid.Value;
            }

            SyncObservedWatchIterationLocked(snapshot);
        }
    }

    public bool ConfirmsCurrentGeneration(HealthSnapshot snapshot)
    {
        lock (_gate)
        {
            if (!snapshot.IsReady || Mode != AppRunMode.WatchRun || !_watchPendingChange)
            {
                return snapshot.IsReady;
            }

            if (_lastHotReloadOutcome == HotReloadOutcome.RestartRequired ||
                _watchState is WatchProcessingState.RestartRequired or WatchProcessingState.ChildExited or WatchProcessingState.Building or WatchProcessingState.Launching)
            {
                if (_expectedWatchIteration.HasValue && snapshot.WatchIteration.HasValue)
                {
                    return snapshot.WatchIteration.Value >= _expectedWatchIteration.Value;
                }

                if (_restartBaselineRuntimePid.HasValue && snapshot.RuntimePid.HasValue)
                {
                    return snapshot.RuntimePid.Value != _restartBaselineRuntimePid.Value;
                }
            }

            return true;
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
            _runtimePid = null;
            _watchPendingChange = false;
            _watchState = WatchProcessingState.Stopped;
            _watchSummary = reason;
        }

        Transition(AppLifecycleState.Stopped, reason);
    }

    public void MarkExitedUnexpectedly(int? exitCode)
    {
        lock (_gate)
        {
            LastExitCode = exitCode;
            Process = null;
            _runtimePid = null;
            _watchPendingChange = false;
            _watchState = WatchProcessingState.Stopped;
            _watchSummary = "Managed process exited unexpectedly.";
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
                        LastHealthSnapshot.Summary,
                        LastHealthSnapshot.IsReady,
                        LastHealthSnapshot.WatchIteration ?? _confirmedWatchIteration,
                        LastHealthSnapshot.RuntimePid ?? _runtimePid),
                _recentEvents.ToArray(),
                Mode == AppRunMode.WatchRun
                    ? new WatchStatusData(
                        _watchState,
                        _watchSummary,
                        _watchPendingChange,
                        Process?.Pid,
                        _runtimePid,
                        _expectedWatchIteration,
                        _confirmedWatchIteration,
                        _lastHotReloadOutcome,
                        _lastWatchActivitySequence,
                        _lastWatchActivityUtc)
                    : null);
        }
    }

    private void HandleListeningUrl(LogEntry entry, string url)
    {
        lock (_gate)
        {
            _observedUrls.Add(url);
            if (Mode == AppRunMode.WatchRun)
            {
                if (_healthEnabled)
                {
                    _watchState = WatchProcessingState.Launching;
                    _watchSummary = "Application is launching.";
                }
                else
                {
                    _watchPendingChange = false;
                    _watchState = WatchProcessingState.WaitingForChanges;
                    _watchSummary = "Application is waiting for changes.";
                    LastHealthSnapshot = new HealthSnapshot(
                        "Ready",
                        true,
                        entry.TimestampUtc,
                        null,
                        url,
                        "Observed listening URL.",
                        _confirmedWatchIteration,
                        _runtimePid,
                        _observedUrls.ToArray());
                }

                _lastWatchActivitySequence = entry.Sequence;
                _lastWatchActivityUtc = entry.TimestampUtc;
            }
        }

        RecordEvent($"Observed URL: {url}");
        Transition(AppLifecycleState.Running, "Application reported a listening URL.");
    }

    private bool TryHandleWatchLifecycle(LogEntry entry)
    {
        var line = NormalizeWatchLine(entry.Text);
        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        if (line.Contains("File updated:", StringComparison.OrdinalIgnoreCase) ||
            line.Contains("File added:", StringComparison.OrdinalIgnoreCase) ||
            line.Contains("File deleted:", StringComparison.OrdinalIgnoreCase))
        {
            BeginWatchChange(WatchProcessingState.ChangeDetected, line, entry);
            return true;
        }

        if (line.Contains("Evaluating projects", StringComparison.OrdinalIgnoreCase))
        {
            if (ShouldBeginImplicitWatchChange())
            {
                BeginWatchChange(WatchProcessingState.EvaluatingProjects, line, entry);
            }
            else
            {
                UpdateWatchPhase(WatchProcessingState.EvaluatingProjects, line, entry);
            }

            return true;
        }

        if (line.Contains("Evaluation completed", StringComparison.OrdinalIgnoreCase))
        {
            UpdateWatchPhase(WatchProcessingState.EvaluatingProjects, line, entry);
            return true;
        }

        if (line.Contains("Loading projects", StringComparison.OrdinalIgnoreCase) ||
            line.Contains("Projects loaded", StringComparison.OrdinalIgnoreCase))
        {
            if (ShouldBeginImplicitWatchChange())
            {
                BeginWatchChange(WatchProcessingState.LoadingProjects, line, entry);
            }
            else
            {
                UpdateWatchPhase(WatchProcessingState.LoadingProjects, line, entry);
            }

            return true;
        }

        if (line.Contains("Building", StringComparison.OrdinalIgnoreCase))
        {
            if (ShouldBeginImplicitWatchChange())
            {
                BeginWatchChange(WatchProcessingState.Building, line, entry);
            }
            else
            {
                UpdateWatchPhase(WatchProcessingState.Building, line, entry, clearRuntimePid: _restartBaselineRuntimePid.HasValue);
            }

            return true;
        }

        if (line.Contains("Hot reload succeeded", StringComparison.OrdinalIgnoreCase))
        {
            UpdateWatchPhase(
                WatchProcessingState.HotReloadSucceeded,
                line,
                entry,
                hotReloadOutcome: HotReloadOutcome.Succeeded,
                healthSummary: "Hot reload succeeded. Waiting for fresh health confirmation.");
            return true;
        }

        if (line.Contains("Hot reload of static assets succeeded", StringComparison.OrdinalIgnoreCase) ||
            line.Contains("No C# changes to apply", StringComparison.OrdinalIgnoreCase))
        {
            MarkWatchChangeApplied(line, entry, HotReloadOutcome.Succeeded);
            return true;
        }

        if (line.Contains("Hot reload failed", StringComparison.OrdinalIgnoreCase) ||
            line.Contains("Unable to apply hot reload", StringComparison.OrdinalIgnoreCase))
        {
            MarkWatchFailure(WatchProcessingState.BuildFailed, "Hot reload failed.", entry, HotReloadOutcome.Failed);
            return true;
        }

        if (line.Contains("Restart is needed to apply the changes", StringComparison.OrdinalIgnoreCase))
        {
            UpdateWatchPhase(
                WatchProcessingState.RestartRequired,
                line,
                entry,
                requiresRestart: true,
                hotReloadOutcome: HotReloadOutcome.RestartRequired,
                healthSummary: "Watch requested a runtime restart.");
            return true;
        }

        if (WatchChildExitRegex().IsMatch(line))
        {
            UpdateWatchPhase(
                WatchProcessingState.ChildExited,
                line,
                entry,
                requiresRestart: true,
                clearRuntimePid: true,
                healthSummary: "Runtime child exited while watch is rebuilding.");
            return true;
        }

        if (line.Contains("Waiting for a file to change", StringComparison.OrdinalIgnoreCase) ||
            line.Contains("Waiting for changes", StringComparison.OrdinalIgnoreCase))
        {
            UpdateWatchPhase(
                WatchProcessingState.WaitingForChanges,
                line,
                entry,
                healthSummary: _watchPendingChange
                    ? "Watch is idle, waiting for the current generation to become healthy."
                    : "Watch is waiting for the next change.");
            if (!_watchPendingChange)
            {
                Transition(AppLifecycleState.Running, "Watch is waiting for changes.");
            }

            return true;
        }

        if (line.Contains("watch : Started", StringComparison.OrdinalIgnoreCase) ||
            line.Contains("Hot reload enabled", StringComparison.OrdinalIgnoreCase))
        {
            UpdateWatchPhase(WatchProcessingState.Starting, line, entry);
            return true;
        }

        if (line.Contains("Build failed", StringComparison.OrdinalIgnoreCase))
        {
            MarkWatchFailure(WatchProcessingState.BuildFailed, "Watch reported a build failure.", entry);
            return true;
        }

        return false;
    }

    private void BeginWatchChange(WatchProcessingState state, string line, LogEntry entry)
    {
        lock (_gate)
        {
            if (!_watchPendingChange)
            {
                SessionVersion++;
            }

            _watchPendingChange = true;
            _watchState = state;
            _watchSummary = line;
            _lastWatchActivitySequence = entry.Sequence;
            _lastWatchActivityUtc = entry.TimestampUtc;
            _lastHotReloadOutcome = HotReloadOutcome.None;
            InvalidateHealthLocked("Watch detected a file change.", entry.TimestampUtc);
            RecordEventLocked(line);
        }

        Transition(AppLifecycleState.Restarting, line);
    }

    private void UpdateWatchPhase(
        WatchProcessingState state,
        string line,
        LogEntry entry,
        bool requiresRestart = false,
        bool clearRuntimePid = false,
        HotReloadOutcome? hotReloadOutcome = null,
        string? healthSummary = null)
    {
        lock (_gate)
        {
            _watchPendingChange = true;
            _watchState = state;
            _watchSummary = line;
            _lastWatchActivitySequence = entry.Sequence;
            _lastWatchActivityUtc = entry.TimestampUtc;

            if (hotReloadOutcome.HasValue)
            {
                _lastHotReloadOutcome = hotReloadOutcome.Value;
            }

            if (requiresRestart)
            {
                _restartBaselineRuntimePid ??= _runtimePid;
                LastRestartUtc = entry.TimestampUtc;
                if (_confirmedWatchIteration.HasValue)
                {
                    var nextExpectedIteration = _confirmedWatchIteration.Value + 1;
                    _expectedWatchIteration = Math.Max(_expectedWatchIteration ?? nextExpectedIteration, nextExpectedIteration);
                }
            }

            if (clearRuntimePid)
            {
                _runtimePid = null;
            }

            InvalidateHealthLocked(healthSummary ?? line, entry.TimestampUtc);
            RecordEventLocked(line);
        }

        Transition(AppLifecycleState.Restarting, line);
    }

    private void MarkWatchFailure(
        WatchProcessingState state,
        string summary,
        LogEntry entry,
        HotReloadOutcome hotReloadOutcome = HotReloadOutcome.None)
    {
        lock (_gate)
        {
            _watchPendingChange = false;
            _watchState = state;
            _watchSummary = summary;
            _lastWatchActivitySequence = entry.Sequence;
            _lastWatchActivityUtc = entry.TimestampUtc;
            _lastHotReloadOutcome = hotReloadOutcome;
            LastHealthSnapshot = new HealthSnapshot(
                "Unhealthy",
                false,
                LastHealthSnapshot?.LastSuccessUtc,
                entry.TimestampUtc,
                LastHealthSnapshot?.LastUrl,
                summary,
                _confirmedWatchIteration,
                _runtimePid,
                _observedUrls.ToArray());
            RecordEventLocked(entry.Text);
        }

        Transition(AppLifecycleState.Failed, summary);
    }

    private void InvalidateHealthLocked(string summary, DateTimeOffset observedAtUtc)
    {
        LastHealthSnapshot = new HealthSnapshot(
            "Pending",
            false,
            LastHealthSnapshot?.LastSuccessUtc,
            observedAtUtc,
            LastHealthSnapshot?.LastUrl,
            summary,
            _confirmedWatchIteration,
            _runtimePid,
            LastHealthSnapshot?.ActiveUrls ?? _observedUrls.ToArray());
    }

    private void MarkWatchChangeApplied(string line, LogEntry entry, HotReloadOutcome hotReloadOutcome)
    {
        AppLifecycleState nextState;

        lock (_gate)
        {
            _watchPendingChange = false;
            _watchState = WatchProcessingState.WaitingForChanges;
            _watchSummary = line;
            _lastWatchActivitySequence = entry.Sequence;
            _lastWatchActivityUtc = entry.TimestampUtc;
            _lastHotReloadOutcome = hotReloadOutcome;

            if (!_healthEnabled && _observedUrls.Count > 0)
            {
                LastHealthSnapshot = new HealthSnapshot(
                    "Ready",
                    true,
                    LastHealthSnapshot?.LastSuccessUtc ?? entry.TimestampUtc,
                    null,
                    LastHealthSnapshot?.LastUrl ?? _observedUrls.LastOrDefault(),
                    "Ready",
                    _confirmedWatchIteration,
                    _runtimePid,
                    _observedUrls.ToArray());
            }

            nextState = _healthEnabled && LastHealthSnapshot?.IsReady == true
                ? AppLifecycleState.Healthy
                : AppLifecycleState.Running;
        }

        Transition(nextState, line);
    }

    private void SyncObservedWatchIterationLocked(HealthSnapshot snapshot)
    {
        if (Mode != AppRunMode.WatchRun || !snapshot.WatchIteration.HasValue)
        {
            return;
        }

        var observedIteration = snapshot.WatchIteration.Value;
        if (_confirmedWatchIteration.HasValue &&
            observedIteration < _confirmedWatchIteration.Value)
        {
            return;
        }

        if (!_watchPendingChange &&
            _confirmedWatchIteration.HasValue &&
            observedIteration > _confirmedWatchIteration.Value)
        {
            SessionVersion += observedIteration - _confirmedWatchIteration.Value;
            _lastWatchActivityUtc = snapshot.LastSuccessUtc ?? snapshot.LastFailureUtc ?? DateTimeOffset.UtcNow;
            RecordEventLocked($"Health probe observed watch iteration {observedIteration}.");
        }

        _confirmedWatchIteration = observedIteration;
        _expectedWatchIteration = Math.Max(_expectedWatchIteration ?? observedIteration, observedIteration);
    }

    private bool ShouldBeginImplicitWatchChange()
    {
        lock (_gate)
        {
            return !_watchPendingChange &&
                   Mode == AppRunMode.WatchRun &&
                   (State is AppLifecycleState.Running or AppLifecycleState.Healthy) &&
                   (_confirmedWatchIteration.HasValue || LastHealthSnapshot?.IsReady == true);
        }
    }

    private static string NormalizeWatchLine(string text)
    {
        const string watchPrefix = "dotnet watch :";
        return text.StartsWith(watchPrefix, StringComparison.OrdinalIgnoreCase)
            ? text[watchPrefix.Length..].Trim()
            : text.Trim();
    }

    private void Transition(AppLifecycleState state, string reason)
    {
        lock (_gate)
        {
            State = state;
            RecordEventLocked(reason);
        }
    }

    private void RecordEvent(string text)
    {
        lock (_gate)
        {
            RecordEventLocked(text);
        }
    }

    private void RecordEventLocked(string text)
    {
        _recentEvents.Add(text);
        while (_recentEvents.Count > 20)
        {
            _recentEvents.RemoveAt(0);
        }
    }

    [GeneratedRegex(@"Now listening on:\s+(?<url>\S+)", RegexOptions.IgnoreCase)]
    private static partial Regex ListeningRegex();

    [GeneratedRegex(@"^\[[^\]]+\]\s+Exited$", RegexOptions.IgnoreCase)]
    private static partial Regex WatchChildExitRegex();
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
    private readonly HashSet<string> _activeSessionIds = new(StringComparer.OrdinalIgnoreCase);
    private string? _defaultSessionId;
    private AppSession? _lastSession;

    public AppSession? GetActiveSession()
    {
        lock (_gate)
        {
            return ResolveDefaultSessionLocked();
        }
    }

    public AppSession? GetById(string? sessionId)
    {
        lock (_gate)
        {
            if (string.IsNullOrWhiteSpace(sessionId))
            {
                return ResolveDefaultSessionLocked() ?? _lastSession;
            }

            _sessions.TryGetValue(sessionId, out var session);
            return session;
        }
    }

    public IReadOnlyList<AppSession> GetActiveSessions()
    {
        lock (_gate)
        {
            return _activeSessionIds
                .Select(id => _sessions.TryGetValue(id, out var session) ? session : null)
                .Where(static session => session is not null)
                .OrderByDescending(static session => session!.LastStartUtc)
                .Cast<AppSession>()
                .ToList();
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
        List<AppSession> conflictingSessions;
        AppSession? reusableSession = null;
        lock (_gate)
        {
            var activeSessions = ResolveActiveSessionsLocked();
            reusableSession = reuseIfCompatible
                ? activeSessions.FirstOrDefault(session => session.IsCompatible(template))
                : null;
            conflictingSessions = activeSessions
                .Where(session => SessionConflicts(session, template))
                .ToList();
        }

        if (reusableSession is not null)
        {
            lock (_gate)
            {
                _defaultSessionId = reusableSession.SessionId;
                _lastSession = reusableSession;
            }

            return (reusableSession, true);
        }

        if (conflictingSessions.Count > 0)
        {
            if (conflictPolicy == AppStartConflictPolicy.Fail)
            {
                throw new ToolInvocationException(
                    "RunningSessionConflict",
                    "One or more managed app sessions conflict with the requested launch.",
                    new
                    {
                        conflictingSessionIds = conflictingSessions.Select(static session => session.SessionId).ToArray(),
                        requestedProjectPath = template.ProjectPath
                    });
            }

            foreach (var conflictingSession in conflictingSessions)
            {
                await StopAsync(conflictingSession.SessionId, "Replacing incompatible session.", force: true, cancellationToken);
            }
        }

        var sessionId = $"app_{Guid.NewGuid():N}";
        var correlationId = $"corr_{Guid.NewGuid():N}";
        var session = new AppSession(sessionId, template, correlationId, new RingLogBuffer(configuration.LogBufferCapacity), configuration.HealthEnabled);

        lock (_gate)
        {
            _sessions[sessionId] = session;
            _activeSessionIds.Add(sessionId);
            _defaultSessionId = sessionId;
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
                    _activeSessionIds.Remove(session.SessionId);
                    if (string.Equals(_defaultSessionId, session.SessionId, StringComparison.OrdinalIgnoreCase))
                    {
                        _defaultSessionId = ResolveActiveSessionsLocked().FirstOrDefault()?.SessionId;
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
            _activeSessionIds.Remove(session.SessionId);
            if (string.Equals(_defaultSessionId, session.SessionId, StringComparison.OrdinalIgnoreCase))
            {
                _defaultSessionId = ResolveActiveSessionsLocked().FirstOrDefault()?.SessionId;
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

    private AppSession? ResolveDefaultSessionLocked()
    {
        if (!string.IsNullOrWhiteSpace(_defaultSessionId) &&
            _sessions.TryGetValue(_defaultSessionId, out var preferredSession) &&
            _activeSessionIds.Contains(_defaultSessionId))
        {
            return preferredSession;
        }

        return ResolveActiveSessionsLocked().FirstOrDefault();
    }

    private List<AppSession> ResolveActiveSessionsLocked()
    {
        return _activeSessionIds
            .Select(id => _sessions.TryGetValue(id, out var session) ? session : null)
            .Where(static session => session is not null)
            .OrderByDescending(static session => session!.LastStartUtc)
            .Cast<AppSession>()
            .ToList();
    }

    private static bool SessionConflicts(AppSession existing, AppStartTemplate requested)
    {
        if (string.Equals(existing.ProjectPath, requested.ProjectPath, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(existing.WorkingDirectory, requested.WorkingDirectory, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (HasUrlOverlap(existing.RequestedUrls, requested.Urls))
        {
            return true;
        }

        return false;
    }

    private static bool HasUrlOverlap(IReadOnlyList<string> left, IReadOnlyList<string> right)
    {
        if (left.Count == 0 || right.Count == 0)
        {
            return false;
        }

        var leftSet = left
            .Where(static url => !string.IsNullOrWhiteSpace(url))
            .Select(NormalizeUrl)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return right
            .Where(static url => !string.IsNullOrWhiteSpace(url))
            .Select(NormalizeUrl)
            .Any(leftSet.Contains);
    }

    private static string NormalizeUrl(string url)
    {
        return url.Trim().TrimEnd('/').ToLowerInvariant();
    }
}
