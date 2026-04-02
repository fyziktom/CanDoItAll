using System.Diagnostics;

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
    DateTimeOffset? OutputLastWriteUtc);

public static class TailwindWatchOutputParser
{
    public static bool IsError(string line, bool isError)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        if (line.Contains("npm ERR!", StringComparison.OrdinalIgnoreCase) ||
            line.StartsWith("Error", StringComparison.OrdinalIgnoreCase) ||
            line.Contains("error:", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return isError &&
               (line.Contains("failed", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("cannot", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("error", StringComparison.OrdinalIgnoreCase));
    }

    public static bool IndicatesRebuild(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        return line.Contains("watch", StringComparison.OrdinalIgnoreCase) ||
               line.Contains("building", StringComparison.OrdinalIgnoreCase) ||
               line.Contains("rebuilding", StringComparison.OrdinalIgnoreCase);
    }
}

/* codex-capsule
kind: service
name: TailwindWatchSupervisorService
summary: Supervises the Tailwind CLI watch process, keeps recent logs, and reports output propagation status.
owns: tailwind-watch-process, tailwind-output-health
deps: ManagerOptions
risks: missing-npm, stale-output-file, duplicate-watchers-after-crash
tests: unit:ManagerStatusResponseFactoryTests, unit:ManagerDashboardPageTests
inputs: npm tailwind:watch output, output.css timestamps
outputs: TailwindWatchStatusSnapshot, TailwindLogEntry stream
*/
public sealed class TailwindWatchSupervisorService(
    ILogger<TailwindWatchSupervisorService> logger,
    IConfiguration configuration) : BackgroundService
{
    private readonly ManagerOptions _options = configuration.GetSection("Manager").Get<ManagerOptions>() ?? new();
    private readonly object _gate = new();
    private readonly List<TailwindLogEntry> _logs = [];
    private Process? _activeProcess;
    private long _lastLogId;
    private TailwindWatchStatusSnapshot _status = new(TailwindWatchState.Idle, "Idle", 0, DateTimeOffset.UtcNow, false, null);

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
        await StopActiveProcessAsync("Manager shutdown requested.", cancellationToken);
        await base.StopAsync(cancellationToken);
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

        while (!stoppingToken.IsCancellationRequested)
        {
            await RunTailwindWatchProcessAsync(stoppingToken);
            if (!stoppingToken.IsCancellationRequested)
            {
                Transition(TailwindWatchState.Stopped, "Tailwind watch exited. Restarting soon.");
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
        }
    }

    private async Task RunTailwindWatchProcessAsync(CancellationToken cancellationToken)
    {
        var workspaceRoot = ManagerStatusResponseFactory.ResolveWorkspaceRoot(AppContext.BaseDirectory, _options);
        var tailwindWorkspacePath = ManagerStatusResponseFactory.ResolveTailwindWorkspacePath(workspaceRoot, _options);
        var outputPath = ManagerStatusResponseFactory.ResolveTailwindOutputPath(workspaceRoot, _options);

        await EnsureTailwindDependenciesAsync(tailwindWorkspacePath, outputPath, cancellationToken);

        var startInfo = new ProcessStartInfo(WorkspaceRuntimeProcessTools.ResolveNpmCommand())
        {
            WorkingDirectory = workspaceRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var argument in WorkspaceRuntimeProcessTools.BuildTailwindWatchArgumentList())
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        process.Start();
        Interlocked.Exchange(ref _activeProcess, process);
        Transition(TailwindWatchState.Starting, "Started Tailwind watch.", outputPath);

        var stdoutTask = ReadStreamAsync(process.StandardOutput, false, outputPath, cancellationToken);
        var stderrTask = ReadStreamAsync(process.StandardError, true, outputPath, cancellationToken);
        var outputMonitorTask = MonitorOutputAsync(process, outputPath, cancellationToken);

        try
        {
            await Task.WhenAll(stdoutTask, stderrTask, outputMonitorTask, process.WaitForExitAsync(cancellationToken));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await TerminateProcessAsync(process, "Tailwind watch cancellation requested.", cancellationToken);
        }
        finally
        {
            Interlocked.CompareExchange(ref _activeProcess, null, process);

            if (!cancellationToken.IsCancellationRequested)
            {
                Transition(
                    process.ExitCode == 0 ? TailwindWatchState.Stopped : TailwindWatchState.Faulted,
                    process.ExitCode == 0 ? "Tailwind watch exited." : $"Tailwind watch exited with code {process.ExitCode}.",
                    outputPath);
            }

            await TerminateProcessAsync(process, "Tailwind watch cleanup.", cancellationToken);
        }
    }

    private async Task EnsureTailwindDependenciesAsync(string tailwindWorkspacePath, string outputPath, CancellationToken cancellationToken)
    {
        if (!_options.TailwindInstallDependenciesIfMissing)
        {
            return;
        }

        var tailwindCliPath = Path.Combine(
            tailwindWorkspacePath,
            "node_modules",
            ".bin",
            OperatingSystem.IsWindows() ? "tailwindcss.cmd" : "tailwindcss");
        if (File.Exists(tailwindCliPath))
        {
            return;
        }

        Transition(TailwindWatchState.Starting, "Installing Tailwind workspace dependencies.", outputPath);

        var startInfo = new ProcessStartInfo(WorkspaceRuntimeProcessTools.ResolveNpmCommand())
        {
            WorkingDirectory = tailwindWorkspacePath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add("install");

        using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        process.Start();

        var stdoutTask = ReadStreamAsync(process.StandardOutput, false, outputPath, cancellationToken);
        var stderrTask = ReadStreamAsync(process.StandardError, true, outputPath, cancellationToken);

        await Task.WhenAll(stdoutTask, stderrTask, process.WaitForExitAsync(cancellationToken));
        if (process.ExitCode != 0)
        {
            Transition(TailwindWatchState.Faulted, $"Tailwind dependency install failed with code {process.ExitCode}.", outputPath);
            throw new InvalidOperationException($"Tailwind dependency install failed with code {process.ExitCode}.");
        }
    }

    private async Task ReadStreamAsync(StreamReader reader, bool isError, string outputPath, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line is null)
            {
                break;
            }

            HandleOutputLine(line, isError, outputPath);
        }
    }

    private async Task MonitorOutputAsync(Process process, string outputPath, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                if (process.HasExited)
                {
                    break;
                }
            }
            catch (InvalidOperationException)
            {
                break;
            }

            RefreshOutputSnapshot(outputPath);
            await Task.Delay(500, cancellationToken);
        }
    }

    private void HandleOutputLine(string line, bool isError, string outputPath)
    {
        var parsedAsError = TailwindWatchOutputParser.IsError(line, isError);
        AppendLog(line, parsedAsError);
        EchoTailwindLineToConsole(line, parsedAsError ? LogLevel.Error : LogLevel.Information);

        if (parsedAsError)
        {
            Transition(TailwindWatchState.Faulted, "Tailwind watch reported an error.", outputPath);
            return;
        }

        if (TailwindWatchOutputParser.IndicatesRebuild(line))
        {
            Transition(TailwindWatchState.Starting, "Tailwind is rebuilding styles.", outputPath);
        }

        RefreshOutputSnapshot(outputPath);
    }

    private void EchoTailwindLineToConsole(string line, LogLevel logLevel)
    {
        if (!_options.TailwindEchoOutputToConsole || string.IsNullOrWhiteSpace(line))
        {
            return;
        }

        if (logLevel >= LogLevel.Error)
        {
            logger.LogError("[tailwind] {TailwindLine}", line);
            return;
        }

        logger.LogInformation("[tailwind] {TailwindLine}", line);
    }

    private void RefreshOutputSnapshot(string outputPath)
    {
        var outputExists = File.Exists(outputPath);
        var outputLastWriteUtc = outputExists
            ? new DateTimeOffset(File.GetLastWriteTimeUtc(outputPath), TimeSpan.Zero)
            : (DateTimeOffset?)null;

        lock (_gate)
        {
            var outputChanged = outputExists &&
                                (!_status.OutputLastWriteUtc.HasValue || outputLastWriteUtc > _status.OutputLastWriteUtc.Value);

            var nextState = _status.State;
            var summary = _status.Summary;
            if (_status.State != TailwindWatchState.Faulted &&
                _activeProcess is { HasExited: false } &&
                outputExists &&
                (outputChanged || _status.State is TailwindWatchState.Idle or TailwindWatchState.Starting or TailwindWatchState.Stopped))
            {
                nextState = TailwindWatchState.Ready;
                summary = outputChanged
                    ? $"Tailwind output propagated to {Path.GetFileName(outputPath)} at {outputLastWriteUtc:O}."
                    : "Tailwind watch is running and the stylesheet output is present.";
            }

            _status = _status with
            {
                State = nextState,
                Summary = summary,
                OutputExists = outputExists,
                OutputLastWriteUtc = outputLastWriteUtc
            };
        }
    }

    private void Transition(TailwindWatchState state, string summary, string outputPath)
    {
        var outputExists = File.Exists(outputPath);
        var outputLastWriteUtc = outputExists
            ? new DateTimeOffset(File.GetLastWriteTimeUtc(outputPath), TimeSpan.Zero)
            : (DateTimeOffset?)null;

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

        await TerminateProcessAsync(activeProcess, reason, cancellationToken);
    }

    private async Task TerminateProcessAsync(Process process, string reason, CancellationToken cancellationToken)
    {
        try
        {
            if (process.HasExited)
            {
                return;
            }

            logger.LogInformation("Stopping Tailwind process {ProcessId}. Reason: {Reason}", process.Id, reason);
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync(CancellationToken.None);
        }
        catch (InvalidOperationException)
        {
        }
        catch (ArgumentException)
        {
        }
    }

    private string ResolveTailwindOutputPath()
        => ManagerStatusResponseFactory.ResolveTailwindOutputPath(
            ManagerStatusResponseFactory.ResolveWorkspaceRoot(AppContext.BaseDirectory, _options),
            _options);
}
