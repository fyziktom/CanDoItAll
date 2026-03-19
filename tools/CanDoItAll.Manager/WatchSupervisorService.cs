using System.Diagnostics;
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

        if (line.Contains("Hot reload", StringComparison.OrdinalIgnoreCase))
        {
            return new WatchOutputTransition(WatchState.HotReloadApplied, "Hot reload applied.", requiresReadinessProbe: true);
        }

        if (line.Contains("Waiting for a file to change", StringComparison.OrdinalIgnoreCase))
        {
            return new WatchOutputTransition(WatchState.Launching, "Build completed, waiting for runtime readiness.", requiresReadinessProbe: true);
        }

        if (line.Contains("watch : Started", StringComparison.OrdinalIgnoreCase) ||
            line.Contains("dotnet watch", StringComparison.OrdinalIgnoreCase))
        {
            return new WatchOutputTransition(WatchState.Starting, "Watch process started.");
        }

        if (line.Contains("Unhandled exception", StringComparison.OrdinalIgnoreCase))
        {
            return new WatchOutputTransition(WatchState.RuntimeFaulted, "Runtime fault detected.");
        }

        if (line.Contains("Failed", StringComparison.OrdinalIgnoreCase) ||
            line.Contains("error", StringComparison.OrdinalIgnoreCase))
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
    IConfiguration configuration) : BackgroundService, IWatchSupervisor
{
    private readonly ManagerOptions _options = configuration.GetSection("Manager").Get<ManagerOptions>() ?? new();
    private readonly EventStreamHub<WatchEvent> _eventsHub = new();
    private readonly object _gate = new();
    private readonly List<WatchLogEntry> _logs = [];
    private readonly List<WatchEvent> _events = [];
    private readonly HashSet<string> _activeUrls = [];
    private long _lastLogId;
    private long _lastEventId;
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

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.AutoStartWatch)
        {
            Transition(WatchState.Stopped, "Watch auto-start is disabled.");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            await RunWatchProcessAsync(stoppingToken);
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
        var launchProfileArgument = string.IsNullOrWhiteSpace(_options.WatchLaunchProfile)
            ? string.Empty
            : $" --launch-profile \"{_options.WatchLaunchProfile}\"";

        var startInfo = new ProcessStartInfo("dotnet", $"watch --project \"{watchProjectPath}\" run{launchProfileArgument} --non-interactive")
        {
            WorkingDirectory = workspaceRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        startInfo.Environment["DOTNET_WATCH_SUPPRESS_EMOJIS"] = "1";
        startInfo.Environment["DOTNET_WATCH_SUPPRESS_BROWSER_REFRESH"] = "1";
        ClearInheritedAspNetEnvironment(startInfo);

        using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        process.Start();
        Transition(WatchState.Starting, "Started dotnet watch.");

        var stdoutTask = ReadStreamAsync(process.StandardOutput, false, cancellationToken);
        var stderrTask = ReadStreamAsync(process.StandardError, true, cancellationToken);

        await Task.WhenAll(stdoutTask, stderrTask, process.WaitForExitAsync(cancellationToken));
    }

    private async Task ReadStreamAsync(StreamReader reader, bool isError, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line is null)
            {
                break;
            }

            await HandleWatchLineAsync(line, isError, cancellationToken);
        }
    }

    private async Task HandleWatchLineAsync(string line, bool isError, CancellationToken cancellationToken)
    {
        var logEntry = AppendLog(line, isError);
        var parsedUrl = WatchOutputParser.TryParseUrl(line);
        if (!string.IsNullOrWhiteSpace(parsedUrl))
        {
            lock (_gate)
            {
                _activeUrls.Add(parsedUrl);
            }
        }

        var transition = WatchOutputParser.Parse(line);
        if (transition is null)
        {
            return;
        }

        switch (transition.State)
        {
            case WatchState.Building:
            case WatchState.Starting:
            case WatchState.Restarting:
            case WatchState.BuildFailed:
            case WatchState.RuntimeFaulted:
                await PublishEventAsync(transition.State, transition.Summary, null, null, logEntry.Id, cancellationToken);
                break;
            case WatchState.Launching:
            case WatchState.HotReloadApplied:
                var nextExpectedIteration = (GetStatus().ExpectedWatchIteration ?? 0) + 1;
                await PublishEventAsync(transition.State, transition.Summary, nextExpectedIteration, null, logEntry.Id, cancellationToken);
                if (transition.RequiresReadinessProbe)
                {
                    await ConfirmRuntimeReadinessAsync(cancellationToken);
                }
                break;
        }
    }

    private async Task ConfirmRuntimeReadinessAsync(CancellationToken cancellationToken)
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

                    var expectedIteration = GetStatus().ExpectedWatchIteration;
                    if (expectedIteration.HasValue)
                    {
                        if (!snapshot.WatchIteration.HasValue || snapshot.WatchIteration.Value < expectedIteration.Value)
                        {
                            continue;
                        }
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

        await PublishEventAsync(WatchState.RuntimeFaulted, "Runtime readiness probe timed out.", GetStatus().ExpectedWatchIteration, null, _lastLogId, cancellationToken);
    }

    private IEnumerable<string> GetCandidateReadinessUrls()
    {
        var active = GetStatus().ActiveUrls.Select(url => $"{url.TrimEnd('/')}/_dev/runtime");
        var configured = ResolveConfiguredReadinessUrls();
        return active.Concat(configured).Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private IReadOnlyList<string> ResolveConfiguredReadinessUrls()
    {
        var workspaceRoot = ResolveWorkspaceRoot();
        var watchProjectPath = ResolveWatchProjectPath(workspaceRoot);
        return LaunchProfileSettingsResolver.ResolveRuntimeProbeUrls(watchProjectPath, _options.WatchLaunchProfile)
            .Concat(_options.ReadinessUrls)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private string ResolveWorkspaceRoot() => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, _options.WorkspaceRoot));

    private string ResolveWatchProjectPath(string workspaceRoot) => Path.GetFullPath(Path.Combine(workspaceRoot, _options.WatchProjectPath));

    private static void ClearInheritedAspNetEnvironment(ProcessStartInfo startInfo)
    {
        foreach (var variableName in new[]
                 {
                     "ASPNETCORE_URLS",
                     "ASPNETCORE_HTTP_PORT",
                     "ASPNETCORE_HTTPS_PORT",
                     "ASPNETCORE_HTTP_PORTS",
                     "ASPNETCORE_HTTPS_PORTS",
                     "HTTP_PORTS",
                     "HTTPS_PORTS",
                     "DOTNET_LAUNCH_PROFILE",
                     "LAUNCH_PROFILE"
                 })
        {
            startInfo.Environment.Remove(variableName);
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
