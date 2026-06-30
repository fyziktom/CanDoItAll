using System.Diagnostics;
using System.Threading.Channels;

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
}

/* codex-capsule
kind: service
name: TailwindWatchSupervisorService
summary: Watches Tailwind inputs and scanned component sources, then runs fast Tailwind builds to keep output.css current.
owns: tailwind-build-process, tailwind-source-watchers, tailwind-output-health
deps: ManagerOptions
risks: missing-tailwind-cli, missed-watcher-events, duplicate-builds-under-high-churn
tests: unit:WorkspaceRuntimeProcessToolsTests, unit:ManagerStatusResponseFactoryTests, unit:ManagerDashboardPageTests
inputs: Tailwind CSS files, scanned component sources, output.css timestamps
outputs: TailwindWatchStatusSnapshot, TailwindLogEntry stream
*/
public sealed class TailwindWatchSupervisorService(
    ILogger<TailwindWatchSupervisorService> logger,
    IConfiguration configuration) : BackgroundService
{
    private static readonly HashSet<string> IgnoredPathSegments = new(StringComparer.OrdinalIgnoreCase)
    {
        ".artifacts",
        ".git",
        "bin",
        "node_modules",
        "obj"
    };

    private static readonly HashSet<string> TailwindWorkspaceExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".css"
    };

    private static readonly HashSet<string> TailwindWorkspaceFileNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "package-lock.json",
        "package.json"
    };

    private static readonly HashSet<string> TailwindContentSourceExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cs",
        ".cshtml",
        ".html",
        ".js",
        ".jsx",
        ".razor",
        ".ts",
        ".tsx"
    };

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

        var workspaceRoot = ManagerStatusResponseFactory.ResolveWorkspaceRoot(AppContext.BaseDirectory, _options);
        var tailwindWorkspacePath = ManagerStatusResponseFactory.ResolveTailwindWorkspacePath(workspaceRoot, _options);
        var inputPath = ManagerStatusResponseFactory.ResolveTailwindInputPath(workspaceRoot, _options);
        var outputPath = ManagerStatusResponseFactory.ResolveTailwindOutputPath(workspaceRoot, _options);

        await EnsureTailwindDependenciesAsync(tailwindWorkspacePath, outputPath, stoppingToken);

        var watchRoots = ResolveWatchRoots(workspaceRoot, tailwindWorkspacePath);
        var triggers = Channel.CreateUnbounded<string>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });
        var watchers = CreateWatchers(
            watchRoots,
            outputPath,
            changedPath => triggers.Writer.TryWrite(changedPath));

        if (watchers.Count == 0)
        {
            Transition(TailwindWatchState.Faulted, "Tailwind watch could not start because no source roots were available.", outputPath);
            return;
        }

        try
        {
            await RunTailwindBuildAsync(
                "Initial Tailwind build completed.",
                tailwindWorkspacePath,
                inputPath,
                outputPath,
                stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                var firstChangedPath = await triggers.Reader.ReadAsync(stoppingToken);
                var changedPaths = await DrainChangedPathsAsync(triggers.Reader, firstChangedPath, stoppingToken);
                var changeSummary = BuildChangeSummary(workspaceRoot, changedPaths);

                AppendLog(changeSummary, isError: false);
                EchoTailwindLineToConsole(changeSummary, LogLevel.Information);

                await RunTailwindBuildAsync(
                    changeSummary,
                    tailwindWorkspacePath,
                    inputPath,
                    outputPath,
                    stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        finally
        {
            triggers.Writer.TryComplete();
            foreach (var watcher in watchers)
            {
                watcher.Dispose();
            }

            await StopActiveProcessAsync("Tailwind watch is stopping.", CancellationToken.None);
        }
    }

    private IReadOnlyList<FileSystemWatcher> CreateWatchers(
        IReadOnlyList<TailwindWatchRoot> watchRoots,
        string outputPath,
        Action<string> queueChange)
    {
        var watchers = new List<FileSystemWatcher>();

        foreach (var root in watchRoots)
        {
            if (!Directory.Exists(root.FullPath))
            {
                AppendLog($"Tailwind watch root is missing and will be skipped: {root.FullPath}", isError: false);
                continue;
            }

            var watcher = new FileSystemWatcher(root.FullPath)
            {
                Filter = "*.*",
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.CreationTime |
                               NotifyFilters.DirectoryName |
                               NotifyFilters.FileName |
                               NotifyFilters.LastWrite |
                               NotifyFilters.Size
            };

            watcher.Changed += (_, args) => OnWatchEvent(root, args.FullPath, outputPath, queueChange);
            watcher.Created += (_, args) => OnWatchEvent(root, args.FullPath, outputPath, queueChange);
            watcher.Deleted += (_, args) => OnWatchEvent(root, args.FullPath, outputPath, queueChange);
            watcher.Renamed += (_, args) =>
            {
                OnWatchEvent(root, args.OldFullPath, outputPath, queueChange);
                OnWatchEvent(root, args.FullPath, outputPath, queueChange);
            };
            watcher.Error += (_, args) =>
            {
                var message = args.GetException()?.Message ?? "Unknown file watcher error.";
                var line = $"Tailwind file watcher reported an error under {root.FullPath}: {message}. Scheduling a full rebuild.";
                AppendLog(line, isError: false);
                EchoTailwindLineToConsole(line, LogLevel.Warning);
                queueChange(root.FullPath);
            };

            watcher.EnableRaisingEvents = true;
            watchers.Add(watcher);
        }

        return watchers;
    }

    private async Task<IReadOnlyCollection<string>> DrainChangedPathsAsync(
        ChannelReader<string> reader,
        string firstChangedPath,
        CancellationToken cancellationToken)
    {
        var changedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            firstChangedPath
        };

        var debounceWindow = TimeSpan.FromMilliseconds(Math.Clamp(_options.TailwindWatchDebounceMilliseconds, 50, 2_000));
        var quietUntilUtc = DateTime.UtcNow + debounceWindow;

        while (!cancellationToken.IsCancellationRequested)
        {
            while (reader.TryRead(out var nextChangedPath))
            {
                changedPaths.Add(nextChangedPath);
                quietUntilUtc = DateTime.UtcNow + debounceWindow;
            }

            var remaining = quietUntilUtc - DateTime.UtcNow;
            if (remaining <= TimeSpan.Zero)
            {
                return changedPaths;
            }

            var delay = remaining > TimeSpan.FromMilliseconds(50)
                ? TimeSpan.FromMilliseconds(50)
                : remaining;

            await Task.Delay(delay, cancellationToken);
        }

        return changedPaths;
    }

    private void OnWatchEvent(TailwindWatchRoot root, string fullPath, string outputPath, Action<string> queueChange)
    {
        if (!IsRelevantWatchPath(root, fullPath, outputPath))
        {
            return;
        }

        queueChange(fullPath);
    }

    private static bool IsRelevantWatchPath(TailwindWatchRoot root, string fullPath, string outputPath)
    {
        if (string.IsNullOrWhiteSpace(fullPath))
        {
            return false;
        }

        var normalizedPath = Path.GetFullPath(fullPath);
        if (PathContainsIgnoredSegment(normalizedPath) ||
            string.Equals(normalizedPath, Path.GetFullPath(outputPath), StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var fileName = Path.GetFileName(normalizedPath);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return false;
        }

        var extension = Path.GetExtension(normalizedPath);
        return root.Kind switch
        {
            TailwindWatchRootKind.TailwindWorkspace => TailwindWorkspaceFileNames.Contains(fileName) ||
                                                       TailwindWorkspaceExtensions.Contains(extension),
            TailwindWatchRootKind.ContentSource => TailwindContentSourceExtensions.Contains(extension),
            _ => false
        };
    }

    private static bool PathContainsIgnoredSegment(string fullPath)
    {
        var segments = fullPath.Split(
            new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
            StringSplitOptions.RemoveEmptyEntries);
        return segments.Any(IgnoredPathSegments.Contains);
    }

    private IReadOnlyList<TailwindWatchRoot> ResolveWatchRoots(string workspaceRoot, string tailwindWorkspacePath)
    {
        var roots = new List<TailwindWatchRoot>
        {
            new(tailwindWorkspacePath, TailwindWatchRootKind.TailwindWorkspace)
        };

        foreach (var relativePath in _options.TailwindContentWatchPaths)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                continue;
            }

            var fullPath = Path.GetFullPath(Path.Combine(workspaceRoot, relativePath));
            if (roots.All(existing => !string.Equals(existing.FullPath, fullPath, StringComparison.OrdinalIgnoreCase)))
            {
                roots.Add(new(fullPath, TailwindWatchRootKind.ContentSource));
            }
        }

        return roots;
    }

    private async Task RunTailwindBuildAsync(
        string reason,
        string tailwindWorkspacePath,
        string inputPath,
        string outputPath,
        CancellationToken cancellationToken)
    {
        Transition(TailwindWatchState.Starting, $"{reason} Rebuilding Tailwind output.", outputPath);

        var startInfo = new ProcessStartInfo(WorkspaceRuntimeProcessTools.ResolveTailwindCliPath(tailwindWorkspacePath))
        {
            WorkingDirectory = tailwindWorkspacePath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        var inputArgument = Path.GetRelativePath(tailwindWorkspacePath, inputPath);
        var outputArgument = Path.GetRelativePath(tailwindWorkspacePath, outputPath);
        foreach (var argument in WorkspaceRuntimeProcessTools.BuildTailwindBuildArgumentList(inputArgument, outputArgument))
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        var stopwatch = Stopwatch.StartNew();

        process.Start();
        Interlocked.Exchange(ref _activeProcess, process);

        try
        {
            var stdoutTask = ReadStreamAsync(process.StandardOutput, false, outputPath, cancellationToken);
            var stderrTask = ReadStreamAsync(process.StandardError, true, outputPath, cancellationToken);
            await Task.WhenAll(stdoutTask, stderrTask, process.WaitForExitAsync(cancellationToken));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await TerminateProcessAsync(process, "Tailwind build cancellation requested.", cancellationToken);
            throw;
        }
        finally
        {
            Interlocked.CompareExchange(ref _activeProcess, null, process);
        }

        stopwatch.Stop();
        RefreshOutputSnapshot(outputPath);

        if (process.ExitCode != 0)
        {
            Transition(TailwindWatchState.Faulted, $"Tailwind build failed with code {process.ExitCode}.", outputPath);
            return;
        }

        var outputLastWriteUtc = GetOutputSnapshot(outputPath).OutputLastWriteUtc;
        var completedSummary = outputLastWriteUtc.HasValue
            ? $"{reason} Tailwind output propagated in {stopwatch.ElapsedMilliseconds} ms at {outputLastWriteUtc:O}."
            : $"{reason} Tailwind build completed in {stopwatch.ElapsedMilliseconds} ms, but the output file was not found.";

        Transition(TailwindWatchState.Ready, completedSummary, outputPath);
    }

    private async Task EnsureTailwindDependenciesAsync(string tailwindWorkspacePath, string outputPath, CancellationToken cancellationToken)
    {
        if (!_options.TailwindInstallDependenciesIfMissing)
        {
            return;
        }

        var tailwindCliPath = WorkspaceRuntimeProcessTools.ResolveTailwindCliPath(tailwindWorkspacePath);
        if (File.Exists(tailwindCliPath))
        {
            return;
        }

        Transition(TailwindWatchState.Starting, "Installing Tailwind workspace dependencies.", outputPath);

        ProcessStartInfo startInfo;
        if (OperatingSystem.IsWindows())
        {
            startInfo = new ProcessStartInfo(WorkspaceRuntimeProcessTools.ResolvePowerShellCommand())
            {
                WorkingDirectory = tailwindWorkspacePath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            startInfo.ArgumentList.Add("-NoProfile");
            startInfo.ArgumentList.Add("-ExecutionPolicy");
            startInfo.ArgumentList.Add("Bypass");
            startInfo.ArgumentList.Add("-Command");
            startInfo.ArgumentList.Add("npm install");
        }
        else
        {
            startInfo = new ProcessStartInfo(WorkspaceRuntimeProcessTools.ResolveNpmCommand())
            {
                WorkingDirectory = tailwindWorkspacePath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            startInfo.ArgumentList.Add("install");
        }

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

    private void HandleOutputLine(string line, bool isError, string outputPath)
    {
        var parsedAsError = TailwindWatchOutputParser.IsError(line, isError);
        AppendLog(line, parsedAsError);
        EchoTailwindLineToConsole(line, parsedAsError ? LogLevel.Error : LogLevel.Information);

        if (parsedAsError)
        {
            Transition(TailwindWatchState.Faulted, "Tailwind build reported an error.", outputPath);
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
            logger.LogError("[tailwind] {TailwindLine}", line);
            return;
        }

        if (logLevel == LogLevel.Warning)
        {
            logger.LogWarning("[tailwind] {TailwindLine}", line);
            return;
        }

        logger.LogInformation("[tailwind] {TailwindLine}", line);
    }

    private (bool OutputExists, DateTimeOffset? OutputLastWriteUtc) GetOutputSnapshot(string outputPath)
    {
        var outputExists = File.Exists(outputPath);
        var outputLastWriteUtc = outputExists
            ? new DateTimeOffset(File.GetLastWriteTimeUtc(outputPath), TimeSpan.Zero)
            : (DateTimeOffset?)null;

        return (outputExists, outputLastWriteUtc);
    }

    private void RefreshOutputSnapshot(string outputPath)
    {
        var (outputExists, outputLastWriteUtc) = GetOutputSnapshot(outputPath);

        lock (_gate)
        {
            _status = _status with
            {
                OutputExists = outputExists,
                OutputLastWriteUtc = outputLastWriteUtc
            };
        }
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

    private static string BuildChangeSummary(string workspaceRoot, IReadOnlyCollection<string> changedPaths)
    {
        var relativePaths = changedPaths
            .Select(path => Path.GetRelativePath(workspaceRoot, path))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (relativePaths.Length == 0)
        {
            return "Tailwind sources changed.";
        }

        if (relativePaths.Length == 1)
        {
            return $"Detected Tailwind-relevant change in {relativePaths[0]}.";
        }

        var preview = string.Join(", ", relativePaths.Take(3));
        var remainingCount = relativePaths.Length - 3;
        return remainingCount > 0
            ? $"Detected {relativePaths.Length} Tailwind-relevant changes: {preview}, and {remainingCount} more."
            : $"Detected {relativePaths.Length} Tailwind-relevant changes: {preview}.";
    }

    private sealed record TailwindWatchRoot(string FullPath, TailwindWatchRootKind Kind);

    private enum TailwindWatchRootKind
    {
        TailwindWorkspace,
        ContentSource
    }
}
