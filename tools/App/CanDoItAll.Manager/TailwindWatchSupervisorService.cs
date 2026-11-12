using System.Globalization;

namespace CanDoItAll.Manager;

public enum TailwindWatchState
{
    Idle,
    Starting,
    Ready,
    Faulted,
    Stopped
}

public sealed record TailwindLogEntry(long Id, DateTimeOffset TimestampUtc, string Line, bool IsError);

public sealed record TailwindWatchStatusSnapshot(
    TailwindWatchState State,
    string Summary,
    long LastLogId,
    DateTimeOffset StartedAtUtc,
    bool OutputExists,
    DateTimeOffset? OutputLastWriteUtc)
{
    public string WatchCommand { get; init; } = string.Empty;

    public DateTimeOffset? LastBuildUtc { get; init; }

    public int BuildCount { get; init; }

    public int RestartCount { get; init; }
}

/* codex-capsule
kind: service
name: TailwindWatchSupervisorService
summary: Runs the Tailwind workspace's npm watch script with stdin held open and reports build health from its output.
owns: tailwind-watch-process, tailwind-dependency-install, tailwind-output-health
deps: ManagerOptions, IManagerProcessCoordinator
risks: missing-node-or-npm, watch-exit-restart-loop, output-capture-limit
tests: unit:TailwindWatchSupervisorServiceTests, unit:TailwindWatchOutputTests, unit:ManagerStatusResponseFactoryTests, unit:ManagerDashboardPageTests
inputs: npm watch script output, output.css timestamps
outputs: TailwindWatchStatusSnapshot, TailwindLogEntry stream
*/
public sealed class TailwindWatchSupervisorService : BackgroundService
{
    private const string LeaseOwner = "TailwindWatchSupervisorService";
    private const int WatchOutputLimitCharacters = 1024 * 1024;
    private static readonly TimeSpan ShutdownPhaseTimeout = TimeSpan.FromSeconds(15);
    private readonly ILogger<TailwindWatchSupervisorService> _logger;
    private readonly IManagerProcessCoordinator _processCoordinator;
    private readonly Func<string, IReadOnlyList<string>, ManagerExecutablePlan> _npmCommandPlanner;
    private readonly TailwindWatchRestartPolicy _restartPolicy;
    private readonly ManagerOptions _options;
    private readonly object _gate = new();
    private readonly List<TailwindLogEntry> _logs = [];
    private IManagerProcessLease? _activeProcess;
    private long _lastLogId;
    private TailwindWatchStatusSnapshot _status = new(TailwindWatchState.Idle, "Idle", 0, DateTimeOffset.UtcNow, false, null);

    public TailwindWatchSupervisorService(
        ILogger<TailwindWatchSupervisorService> logger,
        IConfiguration configuration,
        IManagerProcessCoordinator processCoordinator)
        : this(
            logger,
            configuration,
            processCoordinator,
            WorkspaceRuntimeProcessTools.BuildNpmCommandPlan,
            TailwindWatchRestartPolicy.Default)
    {
    }

    internal TailwindWatchSupervisorService(
        ILogger<TailwindWatchSupervisorService> logger,
        IConfiguration configuration,
        IManagerProcessCoordinator processCoordinator,
        Func<string, IReadOnlyList<string>, ManagerExecutablePlan> npmCommandPlanner,
        TailwindWatchRestartPolicy restartPolicy)
    {
        _logger = logger;
        _processCoordinator = processCoordinator;
        _npmCommandPlanner = npmCommandPlanner;
        _restartPolicy = restartPolicy;
        _options = configuration.GetSection("Manager").Get<ManagerOptions>() ?? new();
    }

    public TailwindWatchStatusSnapshot GetStatus()
    {
        lock (_gate)
        {
            return _status;
        }
    }

    public IReadOnlyList<TailwindLogEntry> GetLogs(int take = 200)
    {
        lock (_gate)
        {
            return _logs.OrderByDescending(item => item.Id).Take(Math.Clamp(take, 1, 500)).ToList();
        }
    }

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
                _logger.LogWarning("Timed out while stopping the Manager Tailwind background loop; mandatory process cleanup will continue.");
            }
        }

        await StopActiveProcessAsync("Manager shutdown requested.", CancellationToken.None);
        using var cleanupTimeout = new CancellationTokenSource(ShutdownPhaseTimeout);
        try
        {
            await CleanupRegisteredProcessesAsync(cleanupTimeout.Token);
        }
        catch (OperationCanceledException) when (cleanupTimeout.IsCancellationRequested)
        {
            _logger.LogError("Timed out while reconciling registered Manager Tailwind processes during shutdown.");
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.AutoStartWatch)
        {
            Transition(TailwindWatchState.Stopped, "Tailwind watch did not start because dotnet watch auto-start is disabled.");
            return;
        }

        if (!_options.AutoStartTailwindWatch)
        {
            Transition(TailwindWatchState.Stopped, "Tailwind auto-start is disabled.");
            return;
        }

        var workspaceRoot = ManagerStatusResponseFactory.ResolveWorkspaceRoot(AppContext.BaseDirectory, _options);
        var tailwindWorkspacePath = ManagerStatusResponseFactory.ResolveTailwindWorkspacePath(workspaceRoot, _options);
        var outputPath = ManagerStatusResponseFactory.ResolveTailwindOutputPath(workspaceRoot, _options);
        if (string.IsNullOrWhiteSpace(_options.TailwindWatchScript))
        {
            Transition(TailwindWatchState.Faulted, "Tailwind watch cannot start because Manager:TailwindWatchScript is empty.", outputPath);
            return;
        }

        try
        {
            if (!await EnsureTailwindDependenciesAsync(workspaceRoot, tailwindWorkspacePath, outputPath, stoppingToken))
            {
                return;
            }

            await SuperviseWatchAsync(
                workspaceRoot,
                tailwindWorkspacePath,
                _options.TailwindWatchScript.Trim(),
                outputPath,
                stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            Transition(TailwindWatchState.Stopped, "Tailwind watch stopped with the Manager.", outputPath);
        }
        catch (InvalidOperationException exception)
        {
            _logger.LogError(exception, "Tailwind supervision stopped because its process could not be started safely.");
            Transition(TailwindWatchState.Faulted, $"Tailwind watch could not be started safely: {exception.Message}", outputPath);
        }
        finally
        {
            await StopActiveProcessAsync("Tailwind watch is stopping.", CancellationToken.None);
        }
    }

    private async Task SuperviseWatchAsync(
        string workspaceRoot,
        string tailwindWorkspacePath,
        string watchScript,
        string outputPath,
        CancellationToken stoppingToken)
    {
        TimeSpan? restartDelay = null;
        var restartCount = 0;
        while (true)
        {
            var run = await RunWatchProcessAsync(
                workspaceRoot,
                tailwindWorkspacePath,
                watchScript,
                outputPath,
                restartCount,
                stoppingToken);
            restartCount++;
            if (run.Completion == ManagerProcessOutputPumpCompletion.CaptureLimitReached)
            {
                continue;
            }

            restartDelay = _restartPolicy.NextDelay(restartDelay, run.Duration);
            Transition(
                TailwindWatchState.Faulted,
                $"{WithDetail($"Tailwind watch exited with code {run.ExitCode}", run.LastError)}. Restarting in {FormatDuration(restartDelay.Value)}.",
                outputPath);
            await Task.Delay(restartDelay.Value, stoppingToken);
        }
    }

    private async Task<TailwindWatchRun> RunWatchProcessAsync(
        string workspaceRoot,
        string tailwindWorkspacePath,
        string watchScript,
        string outputPath,
        int restartCount,
        CancellationToken stoppingToken)
    {
        var plan = _npmCommandPlanner(tailwindWorkspacePath, ["run", watchScript]);
        var watchCommand = $"npm run {watchScript}";
        var tracker = new TailwindBuildCycleTracker();
        var process = await _processCoordinator.StartAsync(
            new ManagerProcessLaunchRequest(
                ManagerProcessPurpose.TailwindWatch,
                "manager_tailwind_watch",
                "manager.tailwind-watch.v1",
                plan.ExecutablePath,
                plan.Arguments,
                tailwindWorkspacePath,
                BuildNpmEnvironmentVariables(),
                workspaceRoot,
                LeaseOwner,
                WatchOutputLimitCharacters,
                WatchOutputLimitCharacters,
                HoldStandardInputOpen: true),
            stoppingToken);
        Interlocked.Exchange(ref _activeProcess, process);
        var startedAtUtc = DateTimeOffset.UtcNow;
        lock (_gate)
        {
            _status = _status with
            {
                StartedAtUtc = startedAtUtc,
                WatchCommand = watchCommand,
                LastBuildUtc = null,
                BuildCount = 0,
                RestartCount = restartCount
            };
        }

        Transition(
            TailwindWatchState.Starting,
            $"Started '{watchCommand}' in {tailwindWorkspacePath}; waiting for the first Tailwind build.",
            outputPath);
        try
        {
            var completion = await ManagerProcessOutputPump.PumpAsync(
                process,
                (line, _, _) =>
                {
                    HandleWatchOutputLine(line, tracker, watchCommand, outputPath);
                    return Task.CompletedTask;
                },
                stoppingToken);
            if (completion == ManagerProcessOutputPumpCompletion.CaptureLimitReached)
            {
                const string line = "Tailwind watch output reached the Manager capture limit; restarting the watch so its health stays observable.";
                AppendLog(line, isError: false);
                EchoTailwindLineToConsole(line, LogLevel.Warning);
                Transition(TailwindWatchState.Starting, line, outputPath);
                await process.TerminateAsync("tailwind-watch-capture-limit", CancellationToken.None);
                return new TailwindWatchRun(completion, null, DateTimeOffset.UtcNow - startedAtUtc, tracker.LastError);
            }

            var result = await process.WaitForExitAsync(stoppingToken);
            return new TailwindWatchRun(completion, result.ExitCode, DateTimeOffset.UtcNow - startedAtUtc, tracker.LastError);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            await process.TerminateAsync("tailwind-watch-cancelled", CancellationToken.None);
            throw;
        }
        finally
        {
            Interlocked.CompareExchange(ref _activeProcess, null, process);
            await process.DisposeAsync();
        }
    }

    private void HandleWatchOutputLine(
        string rawLine,
        TailwindBuildCycleTracker tracker,
        string watchCommand,
        string outputPath)
    {
        var line = TailwindWatchOutputParser.Parse(rawLine);
        if (line.Text.Length == 0)
        {
            return;
        }

        var isError = line.Kind == TailwindWatchLineKind.Error;
        AppendLog(line.Text, isError);
        EchoTailwindLineToConsole(line.Text, isError ? LogLevel.Error : LogLevel.Information);
        switch (line.Kind)
        {
            case TailwindWatchLineKind.Error:
                if (tracker.ObserveError(line))
                {
                    Transition(TailwindWatchState.Faulted, WithDetail("Tailwind reported an error", tracker.LastError) + ".", outputPath);
                }

                break;
            case TailwindWatchLineKind.BuildCompleted:
                HandleBuildCompleted(line, tracker, watchCommand, outputPath);
                break;
        }
    }

    private void HandleBuildCompleted(
        TailwindWatchLine line,
        TailwindBuildCycleTracker tracker,
        string watchCommand,
        string outputPath)
    {
        var failedWith = tracker.LastError;
        if (tracker.CompleteCycle() == TailwindBuildCycleOutcome.Failed)
        {
            Transition(
                TailwindWatchState.Faulted,
                $"{WithDetail("Tailwind build failed", failedWith)}. '{watchCommand}' keeps running and recovers on the next successful build.",
                outputPath);
            return;
        }

        var completedAtUtc = DateTimeOffset.UtcNow;
        lock (_gate)
        {
            _status = _status with
            {
                LastBuildUtc = completedAtUtc,
                BuildCount = _status.BuildCount + 1
            };
        }

        if (!File.Exists(outputPath))
        {
            Transition(
                TailwindWatchState.Faulted,
                $"'{watchCommand}' reported a finished build, but the configured output {outputPath} does not exist. Check Manager:TailwindOutputPath against the npm script.",
                outputPath);
            return;
        }

        Transition(
            TailwindWatchState.Ready,
            $"'{watchCommand}' is running; last build finished in {line.Duration} at {completedAtUtc:O}.",
            outputPath);
    }

    private async Task<bool> EnsureTailwindDependenciesAsync(
        string workspaceRoot,
        string tailwindWorkspacePath,
        string outputPath,
        CancellationToken cancellationToken)
    {
        var tailwindCliPath = WorkspaceRuntimeProcessTools.ResolveTailwindCliShimPath(tailwindWorkspacePath);
        if (File.Exists(tailwindCliPath))
        {
            return true;
        }

        if (!_options.TailwindInstallDependenciesIfMissing)
        {
            Transition(
                TailwindWatchState.Faulted,
                $"Tailwind dependencies are not installed: {tailwindCliPath} is missing. Run 'npm install' in {tailwindWorkspacePath} or enable Manager:TailwindInstallDependenciesIfMissing.",
                outputPath);
            return false;
        }

        Transition(TailwindWatchState.Starting, $"Installing Tailwind workspace dependencies in {tailwindWorkspacePath}.", outputPath);
        var plan = _npmCommandPlanner(tailwindWorkspacePath, ["install"]);
        await using var process = await _processCoordinator.StartAsync(
            new ManagerProcessLaunchRequest(
                ManagerProcessPurpose.TailwindDependencyInstall,
                "manager_npm_install",
                "manager.tailwind-dependencies.v1",
                plan.ExecutablePath,
                plan.Arguments,
                tailwindWorkspacePath,
                BuildNpmEnvironmentVariables(),
                workspaceRoot,
                LeaseOwner),
            cancellationToken);
        var outputTask = ManagerProcessOutputPump.PumpAsync(
            process,
            (line, _, _) =>
            {
                HandleInstallOutputLine(line);
                return Task.CompletedTask;
            },
            cancellationToken);
        var result = await process.WaitForExitAsync(cancellationToken);
        await outputTask;
        if (result.ExitCode != 0)
        {
            Transition(TailwindWatchState.Faulted, $"Tailwind dependency install failed with code {result.ExitCode}.", outputPath);
            return false;
        }

        if (!File.Exists(tailwindCliPath))
        {
            Transition(
                TailwindWatchState.Faulted,
                $"'npm install' finished, but {tailwindCliPath} is still missing. Check the Tailwind CLI dependency in {tailwindWorkspacePath}.",
                outputPath);
            return false;
        }

        return true;
    }

    private void HandleInstallOutputLine(string rawLine)
    {
        var line = TailwindWatchOutputParser.Parse(rawLine);
        if (line.Text.Length == 0)
        {
            return;
        }

        var isError = line.Kind == TailwindWatchLineKind.Error;
        AppendLog(line.Text, isError);
        EchoTailwindLineToConsole(line.Text, isError ? LogLevel.Error : LogLevel.Information);
    }

    private async Task CleanupRegisteredProcessesAsync(CancellationToken cancellationToken)
    {
        foreach (var purpose in new[]
                 {
                     ManagerProcessPurpose.TailwindWatch,
                     ManagerProcessPurpose.TailwindDependencyInstall
                 })
        {
            await _processCoordinator.ReclaimRegisteredAsync(
                purpose,
                "tailwind-shutdown-recovery",
                cancellationToken);
        }
    }

    private void EchoTailwindLineToConsole(string line, LogLevel logLevel)
    {
        if (!_options.TailwindEchoOutputToConsole || string.IsNullOrWhiteSpace(line))
        {
            return;
        }

        if (logLevel >= LogLevel.Error)
        {
            _logger.LogError("[tailwind] {TailwindLine}", line);
            return;
        }

        if (logLevel == LogLevel.Warning)
        {
            _logger.LogWarning("[tailwind] {TailwindLine}", line);
            return;
        }

        _logger.LogInformation("[tailwind] {TailwindLine}", line);
    }

    private static (bool OutputExists, DateTimeOffset? OutputLastWriteUtc) GetOutputSnapshot(string outputPath)
    {
        var outputExists = File.Exists(outputPath);
        var outputLastWriteUtc = outputExists
            ? new DateTimeOffset(File.GetLastWriteTimeUtc(outputPath), TimeSpan.Zero)
            : (DateTimeOffset?)null;

        return (outputExists, outputLastWriteUtc);
    }

    private void Transition(TailwindWatchState state, string summary, string outputPath)
    {
        var (outputExists, outputLastWriteUtc) = GetOutputSnapshot(outputPath);

        lock (_gate)
        {
            _status = _status with
            {
                State = state,
                Summary = summary,
                OutputExists = outputExists,
                OutputLastWriteUtc = outputLastWriteUtc
            };
        }
    }

    private void Transition(TailwindWatchState state, string summary)
        => Transition(state, summary, ResolveTailwindOutputPath());

    private TailwindLogEntry AppendLog(string line, bool isError)
    {
        lock (_gate)
        {
            var entry = new TailwindLogEntry(++_lastLogId, DateTimeOffset.UtcNow, line, isError);
            _logs.Add(entry);
            if (_logs.Count > 500)
            {
                _logs.RemoveRange(0, _logs.Count - 500);
            }

            _status = _status with { LastLogId = entry.Id };
            return entry;
        }
    }

    private async Task StopActiveProcessAsync(string reason, CancellationToken cancellationToken)
    {
        var activeProcess = Interlocked.Exchange(ref _activeProcess, null);
        if (activeProcess is null)
        {
            return;
        }

        _logger.LogInformation(
            "Stopping registered Manager Tailwind process. LeaseId={LeaseId}. Reason={Reason}",
            activeProcess.Record.LeaseId,
            reason);
        try
        {
            await activeProcess.TerminateAsync("tailwind-stop", cancellationToken);
        }
        finally
        {
            await activeProcess.DisposeAsync();
        }
    }

    private string ResolveTailwindOutputPath()
        => ManagerStatusResponseFactory.ResolveTailwindOutputPath(
            ManagerStatusResponseFactory.ResolveWorkspaceRoot(AppContext.BaseDirectory, _options),
            _options);

    private static IReadOnlyDictionary<string, string?> BuildNpmEnvironmentVariables()
        => new Dictionary<string, string?>
        {
            ["NO_COLOR"] = "1",
            ["npm_config_update_notifier"] = "false"
        };

    private static string WithDetail(string message, string? detail)
        => string.IsNullOrWhiteSpace(detail) ? message : $"{message}: {detail.TrimEnd('.')}";

    private static string FormatDuration(TimeSpan duration)
        => duration.TotalSeconds < 60
            ? $"{duration.TotalSeconds.ToString("0.#", CultureInfo.InvariantCulture)} s"
            : $"{duration.TotalMinutes.ToString("0.#", CultureInfo.InvariantCulture)} min";

    private sealed record TailwindWatchRun(
        ManagerProcessOutputPumpCompletion Completion,
        int? ExitCode,
        TimeSpan Duration,
        string? LastError);
}
