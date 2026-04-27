using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using CanDoItAll.Mcp.DotNetWatch.Configuration;
using CanDoItAll.Mcp.DotNetWatch.Health;
using CanDoItAll.Mcp.DotNetWatch.Runtime.Coordination;
using CanDoItAll.Mcp.DotNetWatch.Runtime.LaunchSpecs;
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
    IReadOnlyList<string> Urls)
{
    public string LogicalAppId { get; init; } = BuildLogicalAppId(ProjectPath);

    public AppLaunchType LaunchType { get; init; } = AppLaunchType.Project;

    public RuntimeLaneKind LaneKind { get; init; } = Mode == AppRunMode.WatchRun ? RuntimeLaneKind.SourceWatch : RuntimeLaneKind.SourceRun;

    public string? EntryPath { get; init; }

    public string? SlotId { get; init; }

    public string? ActiveTransactionId { get; init; }

    public string? EndpointLeaseId { get; init; }

    public RuntimeRevisionData? InitialRevision { get; init; }

    public bool RollbackAvailable { get; init; }

    public IReadOnlyList<Uri> HealthUrls { get; init; } = [];

    public AppLaunchSpec ToLaunchSpec()
    {
        return LaunchType switch
        {
            AppLaunchType.Project => new ProjectLaunchSpec(
                LogicalAppId,
                LaneKind,
                ProjectPath,
                WorkingDirectory,
                Configuration,
                Framework,
                LaunchProfile,
                Arguments,
                EnvironmentOverlay,
                Urls,
                HealthUrls),
            AppLaunchType.PublishedDll => new PublishedDllLaunchSpec(
                LogicalAppId,
                LaneKind,
                ProjectPath,
                EntryPath ?? throw new InvalidOperationException("Published DLL launches require EntryPath."),
                WorkingDirectory,
                Configuration,
                Framework,
                Arguments,
                EnvironmentOverlay,
                Urls,
                HealthUrls,
                SlotId),
            AppLaunchType.Executable => new ExecutableLaunchSpec(
                LogicalAppId,
                LaneKind,
                EntryPath ?? throw new InvalidOperationException("Executable launches require EntryPath."),
                WorkingDirectory,
                ProjectPath,
                Configuration,
                Arguments,
                EnvironmentOverlay,
                Urls,
                HealthUrls),
            _ => throw new InvalidOperationException($"Unsupported launch type '{LaunchType}'.")
        };
    }

    private static string BuildLogicalAppId(string projectPath)
    {
        var fileName = Path.GetFileNameWithoutExtension(projectPath);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return "app";
        }

        var builder = new StringBuilder(fileName.Length);
        foreach (var character in fileName)
        {
            builder.Append(char.IsLetterOrDigit(character) ? char.ToLowerInvariant(character) : '-');
        }

        return builder.ToString().Trim('-');
    }
}

public sealed record AppRebuildResult(string SessionId, string Strategy);

public sealed partial class AppSession
{
    private readonly object _gate = new();
    private readonly HashSet<string> _observedUrls = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _recentEvents = [];
    private readonly List<Uri> _healthUrls = [];
    private readonly bool _healthEnabled;
    private RuntimeLaneKind _laneKind;
    private string? _slotId;
    private string? _activeTransactionId;
    private string? _endpointLeaseId;
    private bool _rollbackAvailable;
    private RuntimeRevisionData? _explicitRevision;
    private WatchProcessingState _watchState;
    private string _watchSummary;
    private bool _watchPendingChange;
    private int? _expectedWatchIteration;
    private int? _confirmedWatchIteration;
    private long? _expectedHotReloadGeneration;
    private long? _confirmedHotReloadGeneration;
    private bool? _supportsHotReloadGeneration;
    private int? _runtimePid;
    private int? _restartBaselineRuntimePid;
    private HotReloadOutcome _lastHotReloadOutcome;
    private long? _lastWatchActivitySequence;
    private DateTimeOffset? _lastWatchActivityUtc;
    private bool _watchReadyForHotReload;
    private long? _watchReadyForHotReloadSequence;
    private bool _watchHasReachedReadyState;

    public AppSession(
        string sessionId,
        AppStartTemplate template,
        string correlationId,
        RingLogBuffer logBuffer,
        bool healthEnabled)
    {
        SessionId = sessionId;
        CorrelationId = correlationId;
        LogicalAppId = template.LogicalAppId;
        ProjectPath = template.ProjectPath;
        EntryPath = template.EntryPath;
        WorkingDirectory = template.WorkingDirectory;
        Mode = template.Mode;
        LaunchType = template.LaunchType;
        _laneKind = template.LaneKind;
        _slotId = template.SlotId;
        _activeTransactionId = template.ActiveTransactionId;
        _endpointLeaseId = template.EndpointLeaseId;
        _rollbackAvailable = template.RollbackAvailable;
        _explicitRevision = template.InitialRevision;
        Configuration = template.Configuration;
        Framework = template.Framework;
        LaunchProfile = template.LaunchProfile;
        Arguments = template.Arguments.ToArray();
        EnvironmentOverlay = new Dictionary<string, string>(template.EnvironmentOverlay, StringComparer.OrdinalIgnoreCase);
        RequestedUrls = template.Urls.ToArray();
        _healthUrls.AddRange(template.HealthUrls);
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

    public string LogicalAppId { get; }

    public string ProjectPath { get; }

    public string? EntryPath { get; }

    public string WorkingDirectory { get; }

    public AppRunMode Mode { get; }

    public AppLaunchType LaunchType { get; }

    public string Configuration { get; }

    public string? Framework { get; }

    public string? LaunchProfile { get; }

    public IReadOnlyList<string> Arguments { get; }

    public IReadOnlyDictionary<string, string> EnvironmentOverlay { get; }

    public IReadOnlyList<string> RequestedUrls { get; }

    public IReadOnlyList<Uri> HealthUrls => _healthUrls.ToArray();

    public string? EndpointLeaseId => _endpointLeaseId;

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

    public void UpdateAtomicState(
        RuntimeLaneKind laneKind,
        string? slotId,
        string? activeTransactionId,
        RuntimeRevisionData? revision,
        bool rollbackAvailable)
    {
        lock (_gate)
        {
            _laneKind = laneKind;
            _slotId = slotId;
            _activeTransactionId = activeTransactionId;
            _rollbackAvailable = rollbackAvailable;
            _explicitRevision = revision ?? _explicitRevision;
        }
    }

    public void SetRollbackAvailable(bool rollbackAvailable)
    {
        lock (_gate)
        {
            _rollbackAvailable = rollbackAvailable;
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
        string summary;

        lock (_gate)
        {
            LastHealthSnapshot = snapshot;
            foreach (var url in snapshot.ActiveUrls)
            {
                _observedUrls.Add(url);
            }

            _runtimePid = snapshot.RuntimePid ?? _runtimePid;
            SyncObservedWatchIterationLocked(snapshot);
            SyncObservedHotReloadGenerationLocked(snapshot);

            if (Mode == AppRunMode.WatchRun)
            {
                if (CanConfirmPendingGenerationLocked(snapshot))
                {
                    _watchPendingChange = false;
                    _watchState = WatchProcessingState.WaitingForChanges;
                    _watchSummary = snapshot.Summary ?? "Watch generation is healthy.";
                    _lastWatchActivityUtc = DateTimeOffset.UtcNow;
                    _restartBaselineRuntimePid = null;
                }
                else if (_watchPendingChange)
                {
                    _watchState = _lastHotReloadOutcome == HotReloadOutcome.Succeeded
                        ? WatchProcessingState.HotReloadSucceeded
                        : _watchState;
                    _watchSummary = CreatePendingGenerationSummaryLocked();
                }
            }

            summary = Mode == AppRunMode.WatchRun && _watchPendingChange
                ? _watchSummary
                : snapshot.Summary ?? "Healthy.";
        }

        Transition(AppLifecycleState.Healthy, summary);
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
            SyncObservedHotReloadGenerationLocked(snapshot);
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
            SyncObservedHotReloadGenerationLocked(snapshot);
        }
    }

    public bool ConfirmsCurrentGeneration(HealthSnapshot snapshot)
    {
        lock (_gate)
        {
            return ConfirmsCurrentGenerationLocked(snapshot);
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
            _watchReadyForHotReload = false;
            _watchReadyForHotReloadSequence = null;
        }

        Transition(AppLifecycleState.Stopped, reason);
    }

    public void MarkExitedUnexpectedly(int? exitCode)
    {
        lock (_gate)
        {
            if (State is AppLifecycleState.Stopping or AppLifecycleState.Stopped)
            {
                Process = null;
                _runtimePid = null;
                return;
            }

            LastExitCode = exitCode;
            Process = null;
            _runtimePid = null;
            _watchPendingChange = false;
            _watchState = WatchProcessingState.Stopped;
            _watchSummary = "Managed process exited unexpectedly.";
            _watchReadyForHotReload = false;
            _watchReadyForHotReloadSequence = null;
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
                RequestedUrls.ToArray())
            {
                LogicalAppId = LogicalAppId,
                LaunchType = LaunchType,
                LaneKind = _laneKind,
                EntryPath = EntryPath,
                SlotId = _slotId,
                ActiveTransactionId = _activeTransactionId,
                EndpointLeaseId = _endpointLeaseId,
                InitialRevision = _explicitRevision,
                RollbackAvailable = _rollbackAvailable,
                HealthUrls = _healthUrls.ToArray()
            };
        }
    }

    public bool IsCompatible(AppStartTemplate template)
    {
        lock (_gate)
        {
            return string.Equals(ProjectPath, template.ProjectPath, StringComparison.OrdinalIgnoreCase) &&
                   Mode == template.Mode &&
                   LaunchType == template.LaunchType &&
                   _laneKind == template.LaneKind &&
                   string.Equals(EntryPath, template.EntryPath, StringComparison.OrdinalIgnoreCase) &&
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
            var status = new AppStatusData(
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
                        LastHealthSnapshot.RuntimePid ?? _runtimePid)
                    {
                        HotReloadGeneration = LastHealthSnapshot.HotReloadGeneration ?? _confirmedHotReloadGeneration
                    },
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
                    {
                        ExpectedHotReloadGeneration = _expectedHotReloadGeneration,
                        ConfirmedHotReloadGeneration = _confirmedHotReloadGeneration,
                        IsReadyForHotReload = _watchReadyForHotReload,
                        ReadyForHotReloadSequence = _watchReadyForHotReloadSequence
                    }
                    : null);
            return status with
            {
                LogicalAppId = LogicalAppId,
                LaneKind = _laneKind,
                Revision = CreateRevisionLocked(),
                SlotId = _slotId,
                ActiveTransactionId = _activeTransactionId,
                RollbackAvailable = _rollbackAvailable,
                LaunchType = LaunchType,
                EntryPath = EntryPath
            };
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
                    _watchReadyForHotReload = false;
                    _watchReadyForHotReloadSequence = null;
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
            else if (ShouldTrackPreReadyWatchProgress())
            {
                UpdateWatchProgressWithoutChange(WatchProcessingState.EvaluatingProjects, line, entry);
            }
            else
            {
                UpdateWatchPhase(WatchProcessingState.EvaluatingProjects, line, entry);
            }

            return true;
        }

        if (line.Contains("Evaluation completed", StringComparison.OrdinalIgnoreCase))
        {
            if (ShouldTrackPreReadyWatchProgress())
            {
                UpdateWatchProgressWithoutChange(WatchProcessingState.EvaluatingProjects, line, entry);
            }
            else
            {
                UpdateWatchPhase(WatchProcessingState.EvaluatingProjects, line, entry);
            }
            return true;
        }

        if (line.Contains("Loading projects", StringComparison.OrdinalIgnoreCase) ||
            line.Contains("Projects loaded", StringComparison.OrdinalIgnoreCase))
        {
            if (ShouldBeginImplicitWatchChange())
            {
                BeginWatchChange(WatchProcessingState.LoadingProjects, line, entry);
            }
            else if (ShouldTrackPreReadyWatchProgress())
            {
                UpdateWatchProgressWithoutChange(WatchProcessingState.LoadingProjects, line, entry);
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
            else if (ShouldTrackPreReadyWatchProgress())
            {
                UpdateWatchProgressWithoutChange(WatchProcessingState.Building, line, entry);
            }
            else
            {
                UpdateWatchPhase(WatchProcessingState.Building, line, entry, clearRuntimePid: _restartBaselineRuntimePid.HasValue);
            }

            return true;
        }

        if (line.Contains("Hot reload succeeded", StringComparison.OrdinalIgnoreCase))
        {
            MarkWatchChangeApplied(line, entry, HotReloadOutcome.Succeeded, requiresHealthConfirmation: true);
            return true;
        }

        if (line.Contains("Hot reload of static assets succeeded", StringComparison.OrdinalIgnoreCase) ||
            line.Contains("No C# changes to apply", StringComparison.OrdinalIgnoreCase))
        {
            MarkWatchChangeApplied(line, entry, HotReloadOutcome.Succeeded, requiresHealthConfirmation: false);
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
            if (!_watchPendingChange)
            {
                lock (_gate)
                {
                    _watchState = WatchProcessingState.WaitingForChanges;
                    _watchSummary = line;
                    _lastWatchActivitySequence = entry.Sequence;
                    _lastWatchActivityUtc = entry.TimestampUtc;
                    MarkWatchReadyForHotReloadLocked(entry);
                    RecordEventLocked(line);
                }

                Transition(AppLifecycleState.Running, "Watch is waiting for changes.");
                return true;
            }

            UpdateWatchPhase(
                WatchProcessingState.WaitingForChanges,
                line,
                entry,
                healthSummary: "Watch is idle, waiting for the current generation to become healthy.");
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
            _watchReadyForHotReload = false;
            _watchReadyForHotReloadSequence = null;
            if (_confirmedHotReloadGeneration.HasValue)
            {
                var nextExpectedGeneration = _confirmedHotReloadGeneration.Value + 1;
                _expectedHotReloadGeneration = Math.Max(_expectedHotReloadGeneration ?? nextExpectedGeneration, nextExpectedGeneration);
            }

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
            _watchReadyForHotReload = false;
            _watchReadyForHotReloadSequence = null;

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
            _watchReadyForHotReload = false;
            _watchReadyForHotReloadSequence = null;
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

    private void UpdateWatchProgressWithoutChange(WatchProcessingState state, string line, LogEntry entry)
    {
        lock (_gate)
        {
            _watchState = state;
            _watchSummary = line;
            _lastWatchActivitySequence = entry.Sequence;
            _lastWatchActivityUtc = entry.TimestampUtc;
            _watchReadyForHotReload = false;
            _watchReadyForHotReloadSequence = null;
            RecordEventLocked(line);
        }
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
            LastHealthSnapshot?.ActiveUrls ?? _observedUrls.ToArray())
        {
            HotReloadGeneration = LastHealthSnapshot?.HotReloadGeneration ?? _confirmedHotReloadGeneration
        };
    }

    private void MarkWatchChangeApplied(
        string line,
        LogEntry entry,
        HotReloadOutcome hotReloadOutcome,
        bool requiresHealthConfirmation)
    {
        AppLifecycleState nextState;
        string transitionSummary;

        lock (_gate)
        {
            _lastWatchActivitySequence = entry.Sequence;
            _lastWatchActivityUtc = entry.TimestampUtc;
            _lastHotReloadOutcome = hotReloadOutcome;

            if (_healthEnabled && requiresHealthConfirmation)
            {
                _watchPendingChange = true;
                _watchState = WatchProcessingState.HotReloadSucceeded;
                _watchSummary = CreatePendingGenerationSummaryLocked();
                _watchReadyForHotReload = false;
                _watchReadyForHotReloadSequence = null;
                InvalidateHealthLocked(_watchSummary, entry.TimestampUtc);
                nextState = AppLifecycleState.Running;
                transitionSummary = _watchSummary;
            }
            else
            {
                _watchPendingChange = false;
                _watchState = WatchProcessingState.WaitingForChanges;
                _watchSummary = line;
                _watchReadyForHotReload = true;
                _watchReadyForHotReloadSequence = entry.Sequence;
                _expectedHotReloadGeneration = _confirmedHotReloadGeneration ?? _expectedHotReloadGeneration;
                if (_observedUrls.Count > 0)
                {
                    LastHealthSnapshot = new HealthSnapshot(
                        "Healthy",
                        true,
                        LastHealthSnapshot?.LastSuccessUtc ?? entry.TimestampUtc,
                        null,
                        LastHealthSnapshot?.LastUrl ?? _observedUrls.LastOrDefault(),
                        "Ready",
                        _confirmedWatchIteration,
                        _runtimePid,
                        _observedUrls.ToArray())
                    {
                        HotReloadGeneration = _confirmedHotReloadGeneration
                    };
                }

                nextState = LastHealthSnapshot?.IsReady == true
                    ? AppLifecycleState.Healthy
                    : AppLifecycleState.Running;
                transitionSummary = line;
            }
        }

        Transition(nextState, transitionSummary);
    }

    private bool ProbeMatchesSessionLocked(HealthSnapshot snapshot)
    {
        return string.Equals(snapshot.OwnerKind, "app", StringComparison.OrdinalIgnoreCase) &&
               string.Equals(snapshot.OwnerId, SessionId, StringComparison.OrdinalIgnoreCase);
    }

    private bool CanConfirmPendingGenerationLocked(HealthSnapshot snapshot)
    {
        return !_watchPendingChange || ConfirmsCurrentGenerationLocked(snapshot);
    }

    private bool ConfirmsCurrentGenerationLocked(HealthSnapshot snapshot)
    {
        if (!snapshot.IsReady || !ProbeMatchesSessionLocked(snapshot))
        {
            return false;
        }

        if (Mode != AppRunMode.WatchRun)
        {
            return true;
        }

        if (!_watchPendingChange)
        {
            return true;
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

        if (_supportsHotReloadGeneration == true && _expectedHotReloadGeneration.HasValue)
        {
            return snapshot.HotReloadGeneration.HasValue &&
                   snapshot.HotReloadGeneration.Value >= _expectedHotReloadGeneration.Value;
        }

        return true;
    }

    private string CreatePendingGenerationSummaryLocked()
    {
        if (_expectedHotReloadGeneration.HasValue)
        {
            var currentGeneration = _confirmedHotReloadGeneration ?? LastHealthSnapshot?.HotReloadGeneration;
            if (currentGeneration.HasValue && currentGeneration.Value < _expectedHotReloadGeneration.Value)
            {
                return $"Runtime is still on hot reload generation {currentGeneration.Value}; waiting for {_expectedHotReloadGeneration.Value}.";
            }

            return $"Waiting for hot reload generation {_expectedHotReloadGeneration.Value} to become healthy.";
        }

        if (_watchState == WatchProcessingState.HotReloadSucceeded ||
            _lastHotReloadOutcome == HotReloadOutcome.Succeeded)
        {
            return "Hot reload reported success; waiting for runtime confirmation.";
        }

        if (_lastHotReloadOutcome == HotReloadOutcome.RestartRequired)
        {
            return "Watch has not yet confirmed the replacement runtime generation.";
        }

        return "Waiting for the active dotnet watch generation to become healthy.";
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

        var iterationAdvanced = _confirmedWatchIteration.HasValue &&
                                observedIteration > _confirmedWatchIteration.Value;

        if (!_watchPendingChange &&
            iterationAdvanced)
        {
            var previousConfirmedIteration = _confirmedWatchIteration.GetValueOrDefault();
            SessionVersion += observedIteration - previousConfirmedIteration;
            _lastWatchActivityUtc = snapshot.LastSuccessUtc ?? snapshot.LastFailureUtc ?? DateTimeOffset.UtcNow;
            RecordEventLocked($"Health probe observed watch iteration {observedIteration}.");
        }

        _confirmedWatchIteration = observedIteration;
        _expectedWatchIteration = Math.Max(_expectedWatchIteration ?? observedIteration, observedIteration);

        if (iterationAdvanced)
        {
            _confirmedHotReloadGeneration = snapshot.HotReloadGeneration;
            _expectedHotReloadGeneration = snapshot.HotReloadGeneration;
            if (snapshot.HotReloadGeneration.HasValue)
            {
                _supportsHotReloadGeneration = true;
            }
        }
    }

    private void SyncObservedHotReloadGenerationLocked(HealthSnapshot snapshot)
    {
        if (Mode != AppRunMode.WatchRun || !snapshot.HotReloadGeneration.HasValue)
        {
            return;
        }

        _supportsHotReloadGeneration = true;
        var observedGeneration = snapshot.HotReloadGeneration.Value;
        if (_confirmedHotReloadGeneration.HasValue &&
            observedGeneration < _confirmedHotReloadGeneration.Value)
        {
            return;
        }

        if (!_watchPendingChange &&
            _confirmedHotReloadGeneration.HasValue &&
            observedGeneration > _confirmedHotReloadGeneration.Value)
        {
            _lastWatchActivityUtc = snapshot.LastSuccessUtc ?? snapshot.LastFailureUtc ?? DateTimeOffset.UtcNow;
            RecordEventLocked($"Health probe observed hot reload generation {observedGeneration}.");
        }

        _confirmedHotReloadGeneration = observedGeneration;
        _expectedHotReloadGeneration = Math.Max(_expectedHotReloadGeneration ?? observedGeneration, observedGeneration);
    }

    private bool ShouldBeginImplicitWatchChange()
    {
        lock (_gate)
        {
            return !_watchPendingChange &&
                   _watchHasReachedReadyState &&
                   Mode == AppRunMode.WatchRun &&
                   (State is AppLifecycleState.Running or AppLifecycleState.Healthy) &&
                   (_confirmedWatchIteration.HasValue || LastHealthSnapshot?.IsReady == true);
        }
    }

    private bool ShouldTrackPreReadyWatchProgress()
    {
        lock (_gate)
        {
            return !_watchPendingChange &&
                   !_watchHasReachedReadyState &&
                   Mode == AppRunMode.WatchRun &&
                   (State is AppLifecycleState.Running or AppLifecycleState.Healthy);
        }
    }

    public void MarkManagerRebuildRequested(string summary)
    {
        if (Mode != AppRunMode.WatchRun)
        {
            RecordEvent(summary);
            return;
        }

        lock (_gate)
        {
            if (!_watchPendingChange)
            {
                SessionVersion++;
            }

            _watchPendingChange = true;
            _watchState = WatchProcessingState.Building;
            _watchSummary = summary;
            _lastWatchActivityUtc = DateTimeOffset.UtcNow;
            _lastHotReloadOutcome = HotReloadOutcome.RestartRequired;
            _watchReadyForHotReload = false;
            _watchReadyForHotReloadSequence = null;
            _restartBaselineRuntimePid ??= _runtimePid;
            LastRestartUtc = _lastWatchActivityUtc;

            if (_confirmedWatchIteration.HasValue)
            {
                var nextExpectedIteration = _confirmedWatchIteration.Value + 1;
                _expectedWatchIteration = Math.Max(_expectedWatchIteration ?? nextExpectedIteration, nextExpectedIteration);
            }

            InvalidateHealthLocked(summary, _lastWatchActivityUtc.Value);
            RecordEventLocked(summary);
        }

        Transition(AppLifecycleState.Restarting, summary);
    }

    private RuntimeRevisionData CreateRevisionLocked()
    {
        if (_explicitRevision is not null)
        {
            return _explicitRevision;
        }

        if (_laneKind == RuntimeLaneKind.SourceWatch)
        {
            var iteration = _confirmedWatchIteration ?? _expectedWatchIteration ?? 0;
            var hotReloadGeneration = _confirmedHotReloadGeneration ?? _expectedHotReloadGeneration;
            return new RuntimeRevisionData(
                Kind: hotReloadGeneration.HasValue ? "WatchGeneration" : "WatchIteration",
                Value: hotReloadGeneration.HasValue
                    ? $"{LogicalAppId}:{iteration}:g{hotReloadGeneration.Value}"
                    : $"{LogicalAppId}:{iteration}",
                ObservedUtc: _lastWatchActivityUtc ?? LastStartUtc,
                IsConfirmed: !_watchPendingChange && (iteration > 0 || hotReloadGeneration > 0));
        }

        var pid = Process?.Pid ?? _runtimePid ?? 0;
        return new RuntimeRevisionData(
            Kind: "ProcessInstance",
            Value: $"{LastStartUtc:O}:{pid}",
            ObservedUtc: LastStartUtc,
            IsConfirmed: State is AppLifecycleState.Running or AppLifecycleState.Healthy);
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

    private void MarkWatchReadyForHotReloadLocked(LogEntry entry)
    {
        _watchReadyForHotReload = true;
        _watchReadyForHotReloadSequence = entry.Sequence;
        _watchHasReachedReadyState = true;
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
    RuntimeEndpointAllocator endpointAllocator,
    HttpHealthProbe healthProbe,
    TailwindCompanionCoordinator tailwindCompanionCoordinator,
    ILogger<AppRuntimeManager> logger)
{
    private readonly object _gate = new();
    private readonly ConcurrentDictionary<string, byte> _backgroundHealthChecks = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, TailwindSessionCompanion> _tailwindCompanions = new(StringComparer.OrdinalIgnoreCase);
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

    public AppSession? GetByLogicalAppId(string logicalAppId)
    {
        lock (_gate)
        {
            return ResolveActiveSessionsLocked()
                .FirstOrDefault(session => string.Equals(session.LogicalAppId, logicalAppId, StringComparison.OrdinalIgnoreCase));
        }
    }

    public void SetDefaultSession(string sessionId)
    {
        lock (_gate)
        {
            if (_sessions.ContainsKey(sessionId))
            {
                _defaultSessionId = sessionId;
            }
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
                MaybeScheduleBackgroundHealthProbe(session);
                await Task.CompletedTask;
            },
            async exitCode =>
            {
                logger.LogInformation("Managed app session {SessionId} exited with code {ExitCode}", session.SessionId, exitCode);
                await StopTailwindCompanionAsync(session.SessionId);
                session.MarkExitedUnexpectedly(exitCode);
                endpointAllocator.Release(session.EndpointLeaseId);
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
        try
        {
            var tailwindCompanion = await tailwindCompanionCoordinator.TryStartAsync(session, template, cancellationToken);
            if (tailwindCompanion is not null)
            {
                _tailwindCompanions[session.SessionId] = tailwindCompanion;
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Tailwind companion startup failed for managed app session {SessionId}", session.SessionId);
        }

        return (session, false);
    }

    public async Task<AppStopData> StopAsync(string? sessionId, string reason, bool force, CancellationToken cancellationToken)
    {
        var session = GetById(sessionId)
            ?? throw new ToolInvocationException("SessionNotFound", "No managed session was found.", new { sessionId });

        await StopTailwindCompanionAsync(session.SessionId);

        var process = session.Process;
        if (process is null)
        {
            session.MarkStopped(session.LastExitCode, "Session is already stopped.");
            return new AppStopData(session.SessionId, session.CorrelationId, true, AppLifecycleState.Stopped, true, [], session.LogBuffer.CurrentSequence);
        }

        session.MarkStopping(reason);
        var stopResult = await process.StopAsync(force, cancellationToken);
        session.MarkStopped(stopResult.ExitCode, reason);
        endpointAllocator.Release(session.EndpointLeaseId);

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

    public async Task<AppRebuildResult> RebuildAsync(string? sessionId, CancellationToken cancellationToken)
    {
        var session = GetById(sessionId)
            ?? throw new ToolInvocationException("SessionNotFound", "No managed session was found.", new { sessionId });

        var template = session.CreateTemplate();
        await StopAsync(session.SessionId, "Manager rebuild requested.", force: false, cancellationToken);
        var (replacementSession, _) = await StartAsync(template, reuseIfCompatible: false, AppStartConflictPolicy.Replace, cancellationToken);
        return new AppRebuildResult(replacementSession.SessionId, "graceful-stop-start");
    }

    public async Task<AppRebuildResult> ForceRebuildAsync(string? sessionId, CancellationToken cancellationToken)
    {
        var session = GetById(sessionId)
            ?? throw new ToolInvocationException("SessionNotFound", "No managed session was found.", new { sessionId });

        var template = session.CreateTemplate();
        await StopAsync(session.SessionId, "Manager force rebuild requested.", force: true, cancellationToken);
        var (replacementSession, _) = await StartAsync(template, reuseIfCompatible: false, AppStartConflictPolicy.Replace, cancellationToken);
        return new AppRebuildResult(replacementSession.SessionId, "forced-stop-start");
    }

    private async Task StopTailwindCompanionAsync(string sessionId)
    {
        if (_tailwindCompanions.TryRemove(sessionId, out var companion))
        {
            await companion.DisposeAsync();
        }
    }

    private ManagedProcessStartInfo BuildProcessStartInfo(AppStartTemplate template, AppSession session, string correlationId)
    {
        var launchSpec = template.ToLaunchSpec();
        var environment = environmentOverlayFilter.Merge(
            BuildDefaultEnvironment(template, configuration.UsePollingFileWatcher, configuration.WatchSuppressBrowserRefresh),
            template.EnvironmentOverlay.ToDictionary(static pair => pair.Key, static pair => (string?)pair.Value, StringComparer.OrdinalIgnoreCase),
            configuration.UsePollingFileWatcher && template.LaneKind == RuntimeLaneKind.SourceWatch);

        var ownershipMarkers = ManagedProcessMarkers.CreateApplicationArguments("app", session.SessionId, configuration.WorkspaceRoot, serverInstanceIdentity.Id);
        var applicationArguments = BuildManagedApplicationArguments(template, ownershipMarkers);
        var artifactsRoot = BuildManagedArtifactsRoot(configuration.WorkspaceRoot, template);
        if (template.LaneKind != RuntimeLaneKind.SourceWatch)
        {
            Directory.CreateDirectory(artifactsRoot);
        }

        var processStart = BuildManagedProcessStartArguments(launchSpec, applicationArguments, artifactsRoot);

        return new ManagedProcessStartInfo(
            "app",
            session.SessionId,
            processStart.Command,
            processStart.Arguments,
            launchSpec.WorkingDirectory,
            environment,
            correlationId,
            session.SessionVersion);
    }

    internal static IReadOnlyList<string> BuildManagedApplicationArguments(AppStartTemplate template, IReadOnlyList<string> ownershipMarkers)
    {
        List<string> arguments = [];
        arguments.AddRange(ownershipMarkers);

        if (template.Urls.Count > 0 && !ContainsUrlsArgument(template.Arguments))
        {
            arguments.Add("--urls");
            arguments.Add(string.Join(';', template.Urls));
        }

        arguments.AddRange(template.Arguments);
        return arguments;
    }

    internal static (string Command, IReadOnlyList<string> Arguments) BuildManagedProcessStartArguments(
        AppLaunchSpec launchSpec,
        IReadOnlyList<string> applicationArguments,
        string artifactsRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(artifactsRoot);

        if (launchSpec is ProjectLaunchSpec projectSpec && launchSpec.LaneKind == RuntimeLaneKind.SourceWatch)
        {
            List<string> arguments =
            [
                "watch",
                "--non-interactive",
                "--project", projectSpec.ProjectPath!,
                "run",
                "--configuration", launchSpec.Configuration,
                "--property:UseAppHost=false"
            ];

            if (!string.IsNullOrWhiteSpace(launchSpec.Framework))
            {
                arguments.Add("--framework");
                arguments.Add(launchSpec.Framework);
            }

            if (!string.IsNullOrWhiteSpace(launchSpec.LaunchProfile))
            {
                arguments.Add("--launch-profile");
                arguments.Add(launchSpec.LaunchProfile);
            }

            if (applicationArguments.Count > 0)
            {
                arguments.Add("--");
                arguments.AddRange(applicationArguments);
            }

            return ("dotnet", arguments);
        }

        if (launchSpec is ProjectLaunchSpec runProjectSpec)
        {
            List<string> arguments =
            [
                "run",
                "--project", runProjectSpec.ProjectPath!,
                "--artifacts-path", artifactsRoot,
                "--configuration", launchSpec.Configuration,
                "--property:UseAppHost=false"
            ];

            if (!string.IsNullOrWhiteSpace(launchSpec.Framework))
            {
                arguments.Add("--framework");
                arguments.Add(launchSpec.Framework);
            }

            if (!string.IsNullOrWhiteSpace(launchSpec.LaunchProfile))
            {
                arguments.Add("--launch-profile");
                arguments.Add(launchSpec.LaunchProfile);
            }

            if (applicationArguments.Count > 0)
            {
                arguments.Add("--");
                arguments.AddRange(applicationArguments);
            }

            return ("dotnet", arguments);
        }

        if (launchSpec is PublishedDllLaunchSpec publishedDllSpec)
        {
            List<string> arguments = [publishedDllSpec.EntryPath!];
            arguments.AddRange(applicationArguments);
            return ("dotnet", arguments);
        }

        if (launchSpec is ExecutableLaunchSpec executableSpec)
        {
            List<string> arguments = [];
            arguments.AddRange(applicationArguments);
            return (executableSpec.EntryPath!, arguments);
        }

        throw new InvalidOperationException($"Unsupported launch specification '{launchSpec.GetType().Name}'.");
    }

    internal static string BuildManagedArtifactsRoot(string workspaceRoot, AppStartTemplate template)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);

        var cacheRoot = Path.Combine(workspaceRoot, ".mcp-state", "artifacts", "app-projects");
        var projectName = SanitizePathSegment(Path.GetFileNameWithoutExtension(template.ProjectPath));
        var templateKey = ComputeTemplateKey(template);
        return Path.Combine(cacheRoot, $"{projectName}-{templateKey}");
    }

    internal static IReadOnlyList<string> BuildManagedProcessArguments(
        AppStartTemplate template,
        IReadOnlyList<string> applicationArguments,
        string artifactsRoot)
    {
        return BuildManagedProcessStartArguments(template.ToLaunchSpec(), applicationArguments, artifactsRoot).Arguments;
    }

    internal static IReadOnlyDictionary<string, string> BuildDefaultEnvironment(
        AppStartTemplate template,
        bool usePollingWatcher,
        bool suppressBrowserRefresh)
    {
        var environment = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["DOTNET_CLI_UI_LANGUAGE"] = "en",
            ["DOTNET_NOLOGO"] = "1",
            ["DOTNET_SKIP_FIRST_TIME_EXPERIENCE"] = "1",
            ["DOTNET_WATCH_RESTART_ON_RUDE_EDIT"] = "1",
            ["DOTNET_WATCH_SUPPRESS_LAUNCH_BROWSER"] = "1",
            ["DOTNET_WATCH_SUPPRESS_EMOJIS"] = "1",
            ["DOTNET_CLI_USE_MSBUILD_SERVER"] = "1",
            ["ASPNETCORE_ENVIRONMENT"] = "Development",
            ["DOTNET_ENVIRONMENT"] = "Development",
            ["CanDoItAllMcpLaneKind"] = template.LaneKind.ToString(),
            ["CanDoItAllMcpLaunchType"] = template.LaunchType.ToString()
        };

        if (suppressBrowserRefresh)
        {
            environment["DOTNET_WATCH_SUPPRESS_BROWSER_REFRESH"] = "1";
        }

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

    private void MaybeScheduleBackgroundHealthProbe(AppSession session)
    {
        if (!configuration.HealthEnabled || session.Mode != AppRunMode.WatchRun)
        {
            return;
        }

        var status = session.ToStatusData();
        if (status.Watch?.PendingChange != true)
        {
            return;
        }

        if (status.Watch.State is not WatchProcessingState.Launching and not WatchProcessingState.WaitingForChanges and not WatchProcessingState.HotReloadSucceeded)
        {
            return;
        }

        if (!_backgroundHealthChecks.TryAdd(session.SessionId, 0))
        {
            return;
        }

        _ = Task.Run(() => ProbeSessionHealthAsync(session));
    }

    private async Task ProbeSessionHealthAsync(AppSession session)
    {
        try
        {
            var maxAttempts = Math.Max(configuration.StableHealthSuccessCount * 8, 20);
            for (var attempt = 0; attempt < maxAttempts; attempt++)
            {
                var status = session.ToStatusData();
                if (status.State is AppLifecycleState.Stopped or AppLifecycleState.Failed or AppLifecycleState.ExitedUnexpectedly)
                {
                    return;
                }

                if (status.Watch?.PendingChange != true)
                {
                    return;
                }

                var probe = await healthProbe.ProbeAsync(session.HealthUrls.Count > 0 ? session.HealthUrls : configuration.HealthUrls, CancellationToken.None);
                if (!probe.IsReady)
                {
                    session.MarkHealthFailure(probe);
                    await Task.Delay(configuration.DefaultPollInterval, CancellationToken.None);
                    continue;
                }

                if (!session.ConfirmsCurrentGeneration(probe))
                {
                    session.MarkHealthObserved(probe, "Waiting for the active dotnet watch generation to become healthy.");
                    await Task.Delay(configuration.DefaultPollInterval, CancellationToken.None);
                    continue;
                }

                session.MarkHealthy(probe);
                return;
            }
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Background health reconciliation failed for session {SessionId}", session.SessionId);
        }
        finally
        {
            _backgroundHealthChecks.TryRemove(session.SessionId, out _);
        }
    }

    private static bool ContainsUrlsArgument(IReadOnlyList<string> arguments)
    {
        foreach (var argument in arguments)
        {
            if (string.IsNullOrWhiteSpace(argument))
            {
                continue;
            }

            if (string.Equals(argument, "--urls", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(argument, "urls", StringComparison.OrdinalIgnoreCase) ||
                argument.StartsWith("--urls=", StringComparison.OrdinalIgnoreCase) ||
                argument.StartsWith("urls=", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string ComputeTemplateKey(AppStartTemplate template)
    {
        var payload = string.Join(
            "|",
            template.ProjectPath,
            template.WorkingDirectory,
            template.Mode,
            template.LaunchType,
            template.EntryPath ?? string.Empty,
            template.Configuration,
            template.Framework ?? string.Empty,
            template.LaunchProfile ?? string.Empty);

        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash[..8]).ToLowerInvariant();
    }

    private static string SanitizePathSegment(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "app";
        }

        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            builder.Append(char.IsLetterOrDigit(character) ? char.ToLowerInvariant(character) : '-');
        }

        var result = builder.ToString().Trim('-');
        return string.IsNullOrWhiteSpace(result) ? "app" : result;
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
        if (HasUrlOverlap(existing.RequestedUrls, requested.Urls))
        {
            return true;
        }

        var existingAtomicLane = existing.ToStatusData().LaneKind is RuntimeLaneKind.PublishedCandidate or RuntimeLaneKind.PublishedActive;
        var requestedAtomicLane = requested.LaneKind is RuntimeLaneKind.PublishedCandidate or RuntimeLaneKind.PublishedActive;
        if (existingAtomicLane || requestedAtomicLane)
        {
            return string.Equals(existing.EntryPath, requested.EntryPath, StringComparison.OrdinalIgnoreCase) &&
                   !string.IsNullOrWhiteSpace(existing.EntryPath);
        }

        if (string.Equals(existing.ProjectPath, requested.ProjectPath, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(existing.WorkingDirectory, requested.WorkingDirectory, StringComparison.OrdinalIgnoreCase))
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
