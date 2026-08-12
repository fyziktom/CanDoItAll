using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Channels;

namespace CanDoItAll.Manager;

public enum WatchState
{
    Idle,
    Starting,
    Building,
    Launching,
    Ready,
    HotReloadApplied,
    Restarting,
    BuildFailed,
    RuntimeFaulted,
    Stopped
}

public sealed record WatchLogEntry(long Id, DateTimeOffset TimestampUtc, string Line, bool IsError);

public sealed record WatchEvent(
    long EventId,
    string CorrelationId,
    WatchState State,
    DateTimeOffset TimestampUtc,
    int? ExpectedWatchIteration,
    int? ConfirmedWatchIteration,
    string Summary,
    long RawLineId);

public sealed record WatchStatusSnapshot(
    WatchState State,
    string Summary,
    long LastEventId,
    long LastLogId,
    int? ExpectedWatchIteration,
    int? ConfirmedWatchIteration,
    DateTimeOffset StartedAtUtc,
    IReadOnlyList<string> ActiveUrls);

public sealed record RuntimeProbeSnapshot(
    [property: JsonPropertyName("isReady")] bool IsReady,
    [property: JsonPropertyName("summary")] string? Summary,
    [property: JsonPropertyName("watchIteration")] int? WatchIteration,
    [property: JsonPropertyName("activeUrls")] IReadOnlyList<string>? ActiveUrls);

public sealed class WatchOutputTransition(WatchState state, string summary, bool requiresReadinessProbe = false)
{
    public WatchState State { get; } = state;

    public string Summary { get; } = summary;

    public bool RequiresReadinessProbe { get; } = requiresReadinessProbe;
}

public interface IWatchSupervisor
{
    WatchStatusSnapshot GetStatus();

    IReadOnlyList<WatchLogEntry> GetLogs(int take = 200);

    ChannelReader<WatchEvent> Subscribe(out Guid subscriptionId);

    void Unsubscribe(Guid subscriptionId);

    Task<WatchStatusSnapshot?> WaitForReadyAsync(long afterEventId, TimeSpan timeout, CancellationToken cancellationToken);

    Task ProcessWatchLineAsync(string line, bool isError = false, CancellationToken cancellationToken = default);
}

public static partial class WatchOutputParser
{
    [GeneratedRegex(@"Now listening on:\s+(?<url>\S+)", RegexOptions.IgnoreCase)]
    private static partial Regex ListeningRegex();

    [GeneratedRegex(@"\berror\b(?:\s+\S+:|:)", RegexOptions.IgnoreCase)]
    private static partial Regex ErrorRegex();

    public static WatchOutputTransition? Parse(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return null;
        }

        if (line.Contains("Building", StringComparison.OrdinalIgnoreCase))
        {
            return new WatchOutputTransition(WatchState.Building, "Build started.");
        }

        if (line.Contains("Hot reload of changes", StringComparison.OrdinalIgnoreCase) ||
            line.Contains("Hot reload applied", StringComparison.OrdinalIgnoreCase))
        {
            return new WatchOutputTransition(WatchState.HotReloadApplied, "Hot reload applied.", requiresReadinessProbe: true);
        }

        if (line.Contains("Waiting for a file to change", StringComparison.OrdinalIgnoreCase) ||
            line.Contains("Waiting for changes", StringComparison.OrdinalIgnoreCase))
        {
            return new WatchOutputTransition(WatchState.Launching, "Build completed, waiting for runtime readiness.", requiresReadinessProbe: true);
        }

        if (line.Contains("watch : Started", StringComparison.OrdinalIgnoreCase) ||
            line.Contains("Hot reload enabled", StringComparison.OrdinalIgnoreCase))
        {
            return new WatchOutputTransition(WatchState.Starting, "Watch process started.");
        }

        if (line.Contains("Unhandled exception", StringComparison.OrdinalIgnoreCase))
        {
            return new WatchOutputTransition(WatchState.RuntimeFaulted, "Runtime fault detected.");
        }

        if (line.StartsWith("fail:", StringComparison.OrdinalIgnoreCase))
        {
            return new WatchOutputTransition(WatchState.RuntimeFaulted, "Runtime fault detected.");
        }

        if (line.Contains("Build failed", StringComparison.OrdinalIgnoreCase) ||
            ErrorRegex().IsMatch(line))
        {
            return new WatchOutputTransition(WatchState.BuildFailed, "Build or runtime error detected.");
        }

        if (ListeningRegex().IsMatch(line))
        {
            return new WatchOutputTransition(WatchState.Launching, "Application is launching.", requiresReadinessProbe: true);
        }

        if (line.Contains("Restarting", StringComparison.OrdinalIgnoreCase))
        {
            return new WatchOutputTransition(WatchState.Restarting, "Watch is restarting.");
        }

        return null;
    }

    public static string? TryParseUrl(string line)
        => ListeningRegex().Match(line) is { Success: true } match ? match.Groups["url"].Value : null;
}

/* codex-capsule
kind: service
name: WatchSupervisorService
summary: Supervises dotnet watch, normalizes its output, confirms runtime readiness, and exposes logs and events.
owns: watch-state, watch-events, ready-waits
deps: ManagerOptions, IHttpClientFactory
risks: false-ready, watch-crash-loop, stale-url-list
tests: unit:WatchOutputParserTests, integration:WatchSupervisorServiceTests
inputs: dotnet-watch output, runtime readiness probe
outputs: WatchStatusSnapshot, WatchEvent stream
*/
public sealed class WatchSupervisorService(
    ILogger<WatchSupervisorService> logger,
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    IManagerProcessCoordinator processCoordinator) : BackgroundService, IWatchSupervisor
{
    private static readonly TimeSpan ShutdownPhaseTimeout = TimeSpan.FromSeconds(15);
    private readonly ManagerOptions _options = configuration.GetSection("Manager").Get<ManagerOptions>() ?? new();
    private readonly EventStreamHub<WatchEvent> _eventsHub = new();
    private readonly object _gate = new();
    private readonly List<WatchLogEntry> _logs = [];
    private readonly List<WatchEvent> _events = [];
    private readonly HashSet<string> _activeUrls = [];
    private IManagerProcessLease? _activeWatchProcess;
    private long _lastLogId;
    private long _lastEventId;
    private int? _startupExpectedIteration;
    private int _automaticRecoveryRequested;
    private WatchStatusSnapshot _status = new(WatchState.Idle, "Idle", 0, 0, null, null, DateTimeOffset.UtcNow, []);

    public WatchStatusSnapshot GetStatus()
    {
        lock (_gate)
        {
            return _status with { ActiveUrls = _activeUrls.ToArray() };
        }
    }

    public IReadOnlyList<WatchLogEntry> GetLogs(int take = 200)
    {
        lock (_gate)
        {
            return _logs.OrderByDescending(item => item.Id).Take(Math.Clamp(take, 1, 500)).ToList();
        }
    }

    public ChannelReader<WatchEvent> Subscribe(out Guid subscriptionId) => _eventsHub.Subscribe(out subscriptionId);

    public void Unsubscribe(Guid subscriptionId) => _eventsHub.Unsubscribe(subscriptionId);

    public async Task<WatchStatusSnapshot?> WaitForReadyAsync(long afterEventId, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow.Add(timeout);
        while (DateTimeOffset.UtcNow < deadline && !cancellationToken.IsCancellationRequested)
        {
            var snapshot = GetStatus();
            if (snapshot.State == WatchState.Ready && snapshot.LastEventId > afterEventId)
            {
                return snapshot;
            }

            await Task.Delay(200, cancellationToken);
        }

        return null;
    }

    public Task ProcessWatchLineAsync(string line, bool isError = false, CancellationToken cancellationToken = default)
        => HandleWatchLineAsync(line, isError, cancellationToken);

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        using (var stopTimeout = new CancellationTokenSource(ShutdownPhaseTimeout))
        {
            try
            {
                await base.StopAsync(stopTimeout.Token);
            }
            catch (OperationCanceledException) when (stopTimeout.IsCancellationRequested)
            {
                logger.LogWarning("Timed out while stopping the Manager watch background loop; mandatory process cleanup will continue.");
            }
        }

        await StopActiveWatchProcessAsync("Manager shutdown requested.", CancellationToken.None);
        using var cleanupTimeout = new CancellationTokenSource(ShutdownPhaseTimeout);
        try
        {
            await CleanupWorkspaceProcessesAsync("Manager shutdown cleanup.", cleanupTimeout.Token);
        }
        catch (OperationCanceledException) when (cleanupTimeout.IsCancellationRequested)
        {
            logger.LogError("Timed out while reconciling registered Manager watch processes during shutdown.");
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.AutoStartWatch)
        {
            Transition(WatchState.Stopped, "Watch auto-start is disabled.");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunWatchProcessAsync(stoppingToken);
            }
            catch (InvalidOperationException)
            {
                logger.LogError("Watch supervision stopped because process ownership or launch evidence was unavailable.");
                Transition(WatchState.RuntimeFaulted, "Watch process could not be started safely.");
                return;
            }
            if (!stoppingToken.IsCancellationRequested)
            {
                Transition(WatchState.Stopped, "Watch process exited. Restarting soon.");
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
        }
    }

    private async Task RunWatchProcessAsync(CancellationToken cancellationToken)
    {
        var workspaceRoot = ResolveWorkspaceRoot();
        var watchProjectPath = ResolveWatchProjectPath(workspaceRoot);
        Interlocked.Exchange(ref _automaticRecoveryRequested, 0);
        await CleanupWorkspaceProcessesAsync("Preparing a fresh watch launch.", cancellationToken);

        var process = await processCoordinator.StartAsync(
            new ManagerProcessLaunchRequest(
                ManagerProcessPurpose.DotnetWatch,
                "workspace_dotnet_manager_watch",
                "manager.dotnet-watch.v1",
                "dotnet",
                WorkspaceRuntimeProcessTools.BuildWatchArgumentList(workspaceRoot, watchProjectPath, _options),
                workspaceRoot,
                WorkspaceRuntimeProcessTools.BuildWatchEnvironmentVariables(_options, ResolveWatchEnvironmentName())
                    .ToDictionary(pair => pair.Key, pair => (string?)pair.Value, StringComparer.OrdinalIgnoreCase),
                workspaceRoot,
                "WatchSupervisorService"),
            cancellationToken);
        Interlocked.Exchange(ref _activeWatchProcess, process);
        Transition(WatchState.Starting, "Started dotnet watch.");

        var outputTask = ManagerProcessOutputPump.PumpAsync(process, HandleWatchLineAsync, cancellationToken);

        try
        {
            await Task.WhenAll(outputTask, process.WaitForExitAsync(cancellationToken));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await process.TerminateAsync("watch-cancelled", CancellationToken.None);
        }
        finally
        {
            Interlocked.CompareExchange(ref _activeWatchProcess, null, process);
            if (!process.HasExited)
            {
                await process.TerminateAsync("watch-cycle-cleanup", CancellationToken.None);
            }

            await process.DisposeAsync();
        }
    }

    private async Task HandleWatchLineAsync(string line, bool isError, CancellationToken cancellationToken)
    {
        var transition = WatchOutputParser.Parse(line);
        var logLevel = ClassifyWatchLine(line, isError, transition);
        var requiresWorkspaceRecovery = WorkspaceRuntimeProcessTools.RequiresWorkspaceRecovery(line);
        var logEntry = AppendLog(line, logLevel >= LogLevel.Error);
        EchoWatchLineToConsole(line, logLevel);
        var parsedUrl = WatchOutputParser.TryParseUrl(line);
        if (!string.IsNullOrWhiteSpace(parsedUrl))
        {
            lock (_gate)
            {
                _activeUrls.Add(parsedUrl);
            }
        }

        if (transition is null)
        {
            if (requiresWorkspaceRecovery)
            {
                await RequestWorkspaceRecoveryAsync(line, cancellationToken);
            }

            return;
        }

        switch (transition.State)
        {
            case WatchState.Building:
                ResetStartupIteration();
                await PublishEventAsync(transition.State, transition.Summary, null, null, logEntry.Id, cancellationToken);
                break;
            case WatchState.Starting:
            case WatchState.Restarting:
            case WatchState.BuildFailed:
            case WatchState.RuntimeFaulted:
                if (transition.State is WatchState.Starting or WatchState.Restarting)
                {
                    ResetStartupIteration();
                    ClearActiveUrls();
                }

                await PublishEventAsync(transition.State, transition.Summary, null, null, logEntry.Id, cancellationToken);
                break;
            case WatchState.Launching:
                var startupExpectedIteration = GetOrCreateStartupExpectedIteration();
                if (IsIterationAlreadyReady(startupExpectedIteration))
                {
                    break;
                }

                await PublishEventAsync(transition.State, transition.Summary, startupExpectedIteration, null, logEntry.Id, cancellationToken);
                if (transition.RequiresReadinessProbe && ShouldProbeReadiness(parsedUrl))
                {
                    await ConfirmRuntimeReadinessAsync(startupExpectedIteration, cancellationToken);
                }

                break;
            case WatchState.HotReloadApplied:
                var nextExpectedIteration = GetNextExpectedIteration();
                await PublishEventAsync(transition.State, transition.Summary, nextExpectedIteration, null, logEntry.Id, cancellationToken);
                if (transition.RequiresReadinessProbe && ShouldProbeReadiness(parsedUrl))
                {
                    await ConfirmRuntimeReadinessAsync(nextExpectedIteration, cancellationToken);
                }

                break;
        }

        if (requiresWorkspaceRecovery)
        {
            await RequestWorkspaceRecoveryAsync(line, cancellationToken);
        }
    }

    private void EchoWatchLineToConsole(string line, LogLevel logLevel)
    {
        if (!_options.WatchEchoOutputToConsole || string.IsNullOrWhiteSpace(line))
        {
            return;
        }

        if (logLevel >= LogLevel.Error)
        {
            logger.LogError("[watch] {WatchLine}", line);
            return;
        }

        if (logLevel == LogLevel.Warning)
        {
            logger.LogWarning("[watch] {WatchLine}", line);
            return;
        }

        logger.LogInformation("[watch] {WatchLine}", line);
    }

    private static LogLevel ClassifyWatchLine(string line, bool isError, WatchOutputTransition? transition)
    {
        if (transition is { State: WatchState.BuildFailed or WatchState.RuntimeFaulted })
        {
            return LogLevel.Error;
        }

        if (line.StartsWith("crit:", StringComparison.OrdinalIgnoreCase) ||
            line.StartsWith("fail:", StringComparison.OrdinalIgnoreCase))
        {
            return LogLevel.Error;
        }

        if (line.StartsWith("warn:", StringComparison.OrdinalIgnoreCase))
        {
            return LogLevel.Warning;
        }

        if (isError &&
            (line.Contains("Unhandled exception", StringComparison.OrdinalIgnoreCase) ||
             line.Contains("Build FAILED", StringComparison.OrdinalIgnoreCase) ||
             line.Contains(" error ", StringComparison.OrdinalIgnoreCase) ||
             line.Contains(": error ", StringComparison.OrdinalIgnoreCase)))
        {
            return LogLevel.Error;
        }

        return LogLevel.Information;
    }

    private bool ShouldProbeReadiness(string? parsedUrl)
    {
        if (!string.IsNullOrWhiteSpace(parsedUrl))
        {
            return true;
        }

        lock (_gate)
        {
            return _activeUrls.Count > 0;
        }
    }

    private async Task ConfirmRuntimeReadinessAsync(int expectedIteration, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(5);
        var timeout = DateTimeOffset.UtcNow.AddSeconds(_options.ReadinessTimeoutSeconds);

        while (DateTimeOffset.UtcNow < timeout && !cancellationToken.IsCancellationRequested)
        {
            foreach (var url in GetCandidateReadinessUrls())
            {
                try
                {
                    var snapshot = await client.GetFromJsonAsync<RuntimeProbeSnapshot>(url, cancellationToken);
                    if (snapshot?.IsReady != true)
                    {
                        continue;
                    }

                    if (!snapshot.WatchIteration.HasValue || snapshot.WatchIteration.Value < expectedIteration)
                    {
                        continue;
                    }

                    if (snapshot.ActiveUrls is not null)
                    {
                        lock (_gate)
                        {
                            foreach (var activeUrl in snapshot.ActiveUrls)
                            {
                                _activeUrls.Add(activeUrl);
                            }
                        }
                    }

                    await PublishEventAsync(
                        WatchState.Ready,
                        snapshot.Summary ?? "Ready",
                        expectedIteration,
                        snapshot.WatchIteration,
                        _lastLogId,
                        cancellationToken);
                    return;
                }
                catch (Exception ex)
                {
                    logger.LogDebug(ex, "Runtime readiness probe failed for {Url}", url);
                }
            }

            await Task.Delay(250, cancellationToken);
        }

        await PublishEventAsync(WatchState.RuntimeFaulted, "Runtime readiness probe timed out.", expectedIteration, null, _lastLogId, cancellationToken);
    }

    private IEnumerable<string> GetCandidateReadinessUrls()
    {
        var active = GetStatus().ActiveUrls.Select(url => $"{url.TrimEnd('/')}/_dev/runtime");
        var configured = ResolveConfiguredReadinessUrls();
        return active.Concat(configured).Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private IReadOnlyList<string> ResolveConfiguredReadinessUrls()
    {
        var configuredApplicationUrls = ManagerStatusResponseFactory.ResolveConfiguredApplicationUrls(
            ResolveWatchProjectPath(ResolveWorkspaceRoot()),
            _options);
        var configured = configuredApplicationUrls
            .Select(url => $"{url.TrimEnd('/')}/_dev/runtime")
            .ToArray();

        return configured
            .Concat(_options.ReadinessUrls)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private string ResolveWorkspaceRoot() => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, _options.WorkspaceRoot));

    private string ResolveWatchProjectPath(string workspaceRoot) => Path.GetFullPath(Path.Combine(workspaceRoot, _options.WatchProjectPath));

    private string ResolveWatchEnvironmentName()
        => Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
           ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
           ?? "Development";

    private async Task RequestWorkspaceRecoveryAsync(string line, CancellationToken cancellationToken)
    {
        if (Interlocked.Exchange(ref _automaticRecoveryRequested, 1) != 0)
        {
            return;
        }

        logger.LogWarning("Detected a recoverable workspace runtime conflict. Recycling dotnet watch. Trigger: {WatchLine}", line);
        await CleanupWorkspaceProcessesAsync("Automatic recovery from a locked output or port conflict.", cancellationToken);
    }

    private async Task CleanupWorkspaceProcessesAsync(string reason, CancellationToken cancellationToken)
    {
        if (!_options.CleanupWorkspaceProcessesOnStart)
        {
            return;
        }

        var results = await processCoordinator.ReclaimRegisteredAsync(
            ManagerProcessPurpose.DotnetWatch,
            "watch-recovery",
            cancellationToken);
        if (results.Count == 0)
        {
            return;
        }

        logger.LogWarning(
            "Processed {ProcessCount} registered Manager watch process record(s). Reason: {Reason}. ResidualCount={ResidualCount}.",
            results.Count,
            reason,
            results.Count(result => result.ResidualProcessPossible));
    }

    private async Task StopActiveWatchProcessAsync(string reason, CancellationToken cancellationToken)
    {
        var activeProcess = Interlocked.Exchange(ref _activeWatchProcess, null);
        if (activeProcess is null)
        {
            return;
        }

        logger.LogInformation(
            "Stopping registered Manager watch process. LeaseId={LeaseId}. Reason={Reason}",
            activeProcess.Record.LeaseId,
            reason);
        try
        {
            await activeProcess.TerminateAsync("watch-stop", CancellationToken.None);
        }
        finally
        {
            await activeProcess.DisposeAsync();
        }
    }

    private WatchLogEntry AppendLog(string line, bool isError)
    {
        lock (_gate)
        {
            var entry = new WatchLogEntry(++_lastLogId, DateTimeOffset.UtcNow, line, isError);
            _logs.Add(entry);
            if (_logs.Count > 500)
            {
                _logs.RemoveRange(0, _logs.Count - 500);
            }

            _status = _status with { LastLogId = entry.Id };
            return entry;
        }
    }

    private void ResetStartupIteration()
    {
        lock (_gate)
        {
            _startupExpectedIteration = null;
        }
    }

    private void ClearActiveUrls()
    {
        lock (_gate)
        {
            _activeUrls.Clear();
        }
    }

    private int GetOrCreateStartupExpectedIteration()
    {
        lock (_gate)
        {
            _startupExpectedIteration ??= GetNextExpectedIterationLocked();
            return _startupExpectedIteration.Value;
        }
    }

    private int GetNextExpectedIteration()
    {
        lock (_gate)
        {
            return GetNextExpectedIterationLocked();
        }
    }

    private int GetNextExpectedIterationLocked()
        => Math.Max(_status.ExpectedWatchIteration ?? 0, _status.ConfirmedWatchIteration ?? 0) + 1;

    private bool IsIterationAlreadyReady(int expectedIteration)
    {
        lock (_gate)
        {
            return _status.State == WatchState.Ready &&
                   _status.ExpectedWatchIteration == expectedIteration &&
                   _status.ConfirmedWatchIteration == expectedIteration;
        }
    }

    private void Transition(WatchState state, string summary)
    {
        lock (_gate)
        {
            _status = _status with
            {
                State = state,
                Summary = summary
            };
        }
    }

    private async Task PublishEventAsync(
        WatchState state,
        string summary,
        int? expectedIteration,
        int? confirmedIteration,
        long rawLineId,
        CancellationToken cancellationToken)
    {
        WatchEvent watchEvent;
        lock (_gate)
        {
            watchEvent = new WatchEvent(
                ++_lastEventId,
                Guid.NewGuid().ToString("N"),
                state,
                DateTimeOffset.UtcNow,
                expectedIteration,
                confirmedIteration,
                summary,
                rawLineId);

            _events.Add(watchEvent);
            if (_events.Count > 200)
            {
                _events.RemoveRange(0, _events.Count - 200);
            }

            _status = _status with
            {
                State = state,
                Summary = summary,
                LastEventId = watchEvent.EventId,
                ExpectedWatchIteration = expectedIteration ?? _status.ExpectedWatchIteration,
                ConfirmedWatchIteration = confirmedIteration ?? _status.ConfirmedWatchIteration,
                ActiveUrls = _activeUrls.ToArray()
            };
        }

        await _eventsHub.PublishAsync(watchEvent, cancellationToken);
    }
}
