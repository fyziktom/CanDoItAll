using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Channels;
using CanDoItAll.Mcp.DotNetWatch.Configuration;

namespace CanDoItAll.Mcp.DotNetWatch.Runtime;

[Flags]
internal enum TailwindWatchRootKind
{
    None = 0,
    Workspace = 1,
    ContentSource = 2
}

internal sealed record TailwindWatchRoot(string FullPath, TailwindWatchRootKind Kind);

internal sealed record TailwindWorkspacePlan(
    string RepoRoot,
    string PackageDirectory,
    string PackageJsonPath,
    string InputPath,
    string OutputPath,
    string ScriptCommand,
    IReadOnlyList<TailwindWatchRoot> WatchRoots);

public sealed class TailwindCompanionCoordinator(
    RuntimeConfiguration configuration,
    ILogger<TailwindCompanionCoordinator> logger)
{
    internal async Task<TailwindSessionCompanion?> TryStartAsync(AppSession session, AppStartTemplate template, CancellationToken cancellationToken)
    {
        if (!configuration.TailwindAutoDetect ||
            template.LaneKind != RuntimeLaneKind.SourceWatch ||
            template.LaunchType != AppLaunchType.Project)
        {
            return null;
        }

        var plan = TailwindWorkspaceDetector.TryDetect(template.ProjectPath, template.WorkingDirectory);
        if (plan is null)
        {
            return null;
        }

        var companion = new TailwindSessionCompanion(session, plan, configuration.TailwindWatchDebounce, logger);
        var started = await companion.StartAsync(cancellationToken);
        return started ? companion : null;
    }
}

internal sealed class TailwindSessionCompanion(
    AppSession session,
    TailwindWorkspacePlan plan,
    TimeSpan debounceWindow,
    ILogger logger) : IAsyncDisposable
{
    private static readonly HashSet<string> IgnoredPathSegments = new(StringComparer.OrdinalIgnoreCase)
    {
        ".artifacts",
        ".git",
        ".mcp-state",
        "bin",
        "node_modules",
        "obj"
    };

    private static readonly HashSet<string> TailwindWorkspaceExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cjs",
        ".css",
        ".js",
        ".json",
        ".mjs",
        ".ts",
        ".yaml",
        ".yml"
    };

    private static readonly HashSet<string> TailwindWorkspaceFileNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "package-lock.json",
        "package.json",
        "pnpm-lock.yaml",
        "yarn.lock"
    };

    private static readonly HashSet<string> TailwindContentSourceExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".astro",
        ".cs",
        ".cshtml",
        ".html",
        ".js",
        ".jsx",
        ".mdx",
        ".razor",
        ".ts",
        ".tsx",
        ".vue"
    };

    private readonly List<FileSystemWatcher> _watchers = [];
    private readonly CancellationTokenSource _stoppingCts = new();
    private readonly Channel<string> _changes = Channel.CreateUnbounded<string>(new UnboundedChannelOptions
    {
        SingleReader = true,
        SingleWriter = false
    });
    private Process? _activeProcess;
    private Task? _loopTask;

    public async Task<bool> StartAsync(CancellationToken cancellationToken)
    {
        var cliPath = TailwindWorkspaceDetector.ResolveTailwindCliPath(plan.PackageDirectory);
        if (!File.Exists(cliPath))
        {
            AppendLog($"Detected Tailwind package at {plan.PackageDirectory}, but the local CLI was not found at {cliPath}.", isError: true);
            return false;
        }

        foreach (var root in plan.WatchRoots)
        {
            if (!Directory.Exists(root.FullPath))
            {
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

            watcher.Changed += (_, args) => OnWatchEvent(root, args.FullPath);
            watcher.Created += (_, args) => OnWatchEvent(root, args.FullPath);
            watcher.Deleted += (_, args) => OnWatchEvent(root, args.FullPath);
            watcher.Renamed += (_, args) =>
            {
                OnWatchEvent(root, args.OldFullPath);
                OnWatchEvent(root, args.FullPath);
            };
            watcher.Error += (_, args) =>
            {
                var message = args.GetException()?.Message ?? "Unknown file watcher error.";
                AppendLog($"File watcher under {root.FullPath} reported an error: {message}. Scheduling a rebuild.", isError: false);
                _changes.Writer.TryWrite(root.FullPath);
            };
            watcher.EnableRaisingEvents = true;
            _watchers.Add(watcher);
        }

        if (_watchers.Count == 0)
        {
            AppendLog($"Detected Tailwind package at {plan.PackageDirectory}, but no usable watch roots were available.", isError: true);
            return false;
        }

        AppendLog(
            $"Companion active. Package={ToRelative(plan.PackageDirectory)}, Input={ToRelative(plan.InputPath)}, Output={ToRelative(plan.OutputPath)}, Roots={string.Join(", ", plan.WatchRoots.Select(root => ToRelative(root.FullPath)))}",
            isError: false);

        await RunBuildAsync("Initial Tailwind build completed.", cancellationToken);
        _loopTask = Task.Run(() => RunLoopAsync(_stoppingCts.Token), CancellationToken.None);
        return true;
    }

    public async ValueTask DisposeAsync()
    {
        _stoppingCts.Cancel();
        _changes.Writer.TryComplete();

        foreach (var watcher in _watchers)
        {
            watcher.Dispose();
        }

        await StopActiveProcessAsync();

        if (_loopTask is not null)
        {
            try
            {
                await _loopTask;
            }
            catch (OperationCanceledException)
            {
            }
            catch (ChannelClosedException)
            {
            }
        }

        _stoppingCts.Dispose();
    }

    private async Task RunLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            string firstChangedPath;
            try
            {
                firstChangedPath = await _changes.Reader.ReadAsync(cancellationToken);
            }
            catch (ChannelClosedException)
            {
                return;
            }

            var changedPaths = await DrainChangedPathsAsync(firstChangedPath, cancellationToken);
            var summary = BuildChangeSummary(changedPaths);
            AppendLog(summary, isError: false);
            await RunBuildAsync(summary, cancellationToken);
        }
    }

    private async Task<IReadOnlyCollection<string>> DrainChangedPathsAsync(string firstChangedPath, CancellationToken cancellationToken)
    {
        var changedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            firstChangedPath
        };

        var quietUntilUtc = DateTime.UtcNow + debounceWindow;
        while (!cancellationToken.IsCancellationRequested)
        {
            while (_changes.Reader.TryRead(out var changedPath))
            {
                changedPaths.Add(changedPath);
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

    private void OnWatchEvent(TailwindWatchRoot root, string fullPath)
    {
        if (!IsRelevantWatchPath(root, fullPath))
        {
            return;
        }

        _changes.Writer.TryWrite(fullPath);
    }

    private bool IsRelevantWatchPath(TailwindWatchRoot root, string fullPath)
    {
        if (string.IsNullOrWhiteSpace(fullPath))
        {
            return false;
        }

        var normalizedPath = Path.GetFullPath(fullPath);
        if (PathContainsIgnoredSegment(normalizedPath) ||
            string.Equals(normalizedPath, Path.GetFullPath(plan.OutputPath), StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var fileName = Path.GetFileName(normalizedPath);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return false;
        }

        var extension = Path.GetExtension(normalizedPath);
        var kind = root.Kind;
        if (kind.HasFlag(TailwindWatchRootKind.Workspace) &&
            (TailwindWorkspaceFileNames.Contains(fileName) ||
             fileName.StartsWith("tailwind.config.", StringComparison.OrdinalIgnoreCase) ||
             fileName.StartsWith("postcss.config.", StringComparison.OrdinalIgnoreCase) ||
             TailwindWorkspaceExtensions.Contains(extension)))
        {
            return true;
        }

        return kind.HasFlag(TailwindWatchRootKind.ContentSource) &&
               TailwindContentSourceExtensions.Contains(extension);
    }

    private async Task RunBuildAsync(string reason, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(plan.OutputPath) ?? plan.PackageDirectory);

        var startInfo = new ProcessStartInfo(TailwindWorkspaceDetector.ResolveTailwindCliPath(plan.PackageDirectory))
        {
            WorkingDirectory = plan.PackageDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add("-i");
        startInfo.ArgumentList.Add(plan.InputPath);
        startInfo.ArgumentList.Add("-o");
        startInfo.ArgumentList.Add(plan.OutputPath);

        using var process = new Process
        {
            StartInfo = startInfo,
            EnableRaisingEvents = true
        };

        var stopwatch = Stopwatch.StartNew();
        process.Start();
        Interlocked.Exchange(ref _activeProcess, process);

        try
        {
            var stdoutTask = ReadStreamAsync(process.StandardOutput, isError: false, cancellationToken);
            var stderrTask = ReadStreamAsync(process.StandardError, isError: true, cancellationToken);
            await Task.WhenAll(stdoutTask, stderrTask, process.WaitForExitAsync(cancellationToken));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await TerminateProcessAsync(process);
            throw;
        }
        finally
        {
            Interlocked.CompareExchange(ref _activeProcess, null, process);
        }

        stopwatch.Stop();
        if (process.ExitCode != 0)
        {
            AppendLog($"Tailwind build failed with exit code {process.ExitCode}.", isError: true);
            return;
        }

        if (File.Exists(plan.OutputPath))
        {
            var outputLastWriteUtc = File.GetLastWriteTimeUtc(plan.OutputPath);
            AppendLog($"{reason} Tailwind output propagated in {stopwatch.ElapsedMilliseconds} ms at {outputLastWriteUtc:O}.", isError: false);
            return;
        }

        AppendLog($"{reason} Tailwind build completed in {stopwatch.ElapsedMilliseconds} ms, but the output file was not found.", isError: true);
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

            var parsedAsError = isError ||
                                line.Contains("error:", StringComparison.OrdinalIgnoreCase) ||
                                line.StartsWith("Error", StringComparison.OrdinalIgnoreCase);
            AppendLog(line, parsedAsError);
        }
    }

    private void AppendLog(string message, bool isError)
    {
        var line = $"[tailwind] {message}";
        session.LogBuffer.Append("Tailwind", "tailwind", session.SessionVersion, session.CorrelationId, line);

        if (isError)
        {
            logger.LogError("[tailwind:{SessionId}] {Message}", session.SessionId, message);
            return;
        }

        logger.LogInformation("[tailwind:{SessionId}] {Message}", session.SessionId, message);
    }

    private static bool PathContainsIgnoredSegment(string fullPath)
    {
        var segments = fullPath.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);
        return segments.Any(IgnoredPathSegments.Contains);
    }

    private string BuildChangeSummary(IReadOnlyCollection<string> changedPaths)
    {
        var relativePaths = changedPaths
            .Select(ToRelative)
            .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase)
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

    private async Task StopActiveProcessAsync()
    {
        var activeProcess = Interlocked.Exchange(ref _activeProcess, null);
        if (activeProcess is null)
        {
            return;
        }

        await TerminateProcessAsync(activeProcess);
    }

    private static async Task TerminateProcessAsync(Process process)
    {
        try
        {
            if (process.HasExited)
            {
                return;
            }

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

    private string ToRelative(string path)
    {
        try
        {
            return Path.GetRelativePath(plan.RepoRoot, path);
        }
        catch
        {
            return path;
        }
    }
}

internal static class TailwindWorkspaceDetector
{
    private static readonly HashSet<string> IgnoredDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        ".artifacts",
        ".git",
        ".idea",
        ".mcp-state",
        ".vs",
        "bin",
        "node_modules",
        "obj"
    };

    private static readonly Regex ImportDirectiveRegex = new("""@import\s+(?<quote>['"])(?<path>[^'"]+)\k<quote>""", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex SourceDirectiveRegex = new("""@source\s+(?<quote>['"])(?<path>[^'"]+)\k<quote>""", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex InputArgumentRegex = new("""(?:^|\s)(?:-i|--input)(?:\s+|=)(?<value>"[^"]+"|'[^']+'|\S+)""", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex OutputArgumentRegex = new("""(?:^|\s)(?:-o|--output)(?:\s+|=)(?<value>"[^"]+"|'[^']+'|\S+)""", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static TailwindWorkspacePlan? TryDetect(string projectPath, string workingDirectory)
    {
        var projectDirectory = Path.GetDirectoryName(projectPath);
        if (string.IsNullOrWhiteSpace(projectDirectory))
        {
            projectDirectory = workingDirectory;
        }

        if (string.IsNullOrWhiteSpace(projectDirectory) || !Directory.Exists(projectDirectory))
        {
            return null;
        }

        var repoRoot = ResolveSearchRoot(projectDirectory);
        var candidates = EnumeratePackageJsonFiles(repoRoot)
            .Select(packageJsonPath => TryCreatePlan(packageJsonPath, repoRoot, projectDirectory))
            .Where(static plan => plan is not null)
            .Cast<TailwindWorkspacePlan>()
            .OrderBy(plan => MeasurePathDistance(projectDirectory, plan.PackageDirectory))
            .ThenBy(plan => plan.PackageDirectory.Length)
            .ToArray();

        return candidates.FirstOrDefault();
    }

    public static string ResolveTailwindCliPath(string packageDirectory)
    {
        return Path.Combine(
            packageDirectory,
            "node_modules",
            ".bin",
            OperatingSystem.IsWindows() ? "tailwindcss.cmd" : "tailwindcss");
    }

    private static TailwindWorkspacePlan? TryCreatePlan(string packageJsonPath, string repoRoot, string projectDirectory)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(File.ReadAllText(packageJsonPath));
        }
        catch
        {
            return null;
        }

        using (document)
        {
            if (!HasTailwindDependency(document.RootElement) ||
                !TryResolveTailwindScript(document.RootElement, out var scriptCommand) ||
                !TryExtractInputOutputPaths(scriptCommand, out var inputArgument, out var outputArgument))
            {
                return null;
            }

            var packageDirectory = Path.GetDirectoryName(packageJsonPath);
            if (string.IsNullOrWhiteSpace(packageDirectory))
            {
                return null;
            }

            var inputPath = ResolvePath(packageDirectory, inputArgument);
            var outputPath = ResolvePath(packageDirectory, outputArgument);
            if (!File.Exists(inputPath) ||
                !IsPathUnderRoot(inputPath, repoRoot) ||
                !IsPathUnderRoot(outputPath, repoRoot))
            {
                return null;
            }

            var watchRoots = BuildWatchRoots(repoRoot, projectDirectory, packageDirectory, inputPath);
            if (watchRoots.Count == 0)
            {
                return null;
            }

            return new TailwindWorkspacePlan(
                repoRoot,
                packageDirectory,
                packageJsonPath,
                inputPath,
                outputPath,
                scriptCommand,
                watchRoots);
        }
    }

    private static IReadOnlyList<TailwindWatchRoot> BuildWatchRoots(
        string repoRoot,
        string projectDirectory,
        string packageDirectory,
        string inputPath)
    {
        var rootKinds = new Dictionary<string, TailwindWatchRootKind>(StringComparer.OrdinalIgnoreCase);

        AddRoot(rootKinds, packageDirectory, TailwindWatchRootKind.Workspace, repoRoot);

        var parsedCssFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var contentRoots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        ParseCssGraph(inputPath, repoRoot, parsedCssFiles, contentRoots);

        foreach (var cssFile in parsedCssFiles)
        {
            var cssDirectory = Path.GetDirectoryName(cssFile);
            if (!string.IsNullOrWhiteSpace(cssDirectory))
            {
                AddRoot(rootKinds, cssDirectory, TailwindWatchRootKind.Workspace, repoRoot);
            }
        }

        foreach (var contentRoot in contentRoots)
        {
            AddRoot(rootKinds, contentRoot, TailwindWatchRootKind.ContentSource, repoRoot);
        }

        if (!rootKinds.Values.Any(kind => kind.HasFlag(TailwindWatchRootKind.ContentSource)))
        {
            AddRoot(rootKinds, packageDirectory, TailwindWatchRootKind.ContentSource, repoRoot);
            AddRoot(rootKinds, projectDirectory, TailwindWatchRootKind.ContentSource, repoRoot);

            var repoSrcRoot = Path.Combine(repoRoot, "src");
            if (Directory.Exists(repoSrcRoot))
            {
                AddRoot(rootKinds, repoSrcRoot, TailwindWatchRootKind.ContentSource, repoRoot);
            }
        }

        return rootKinds
            .Select(static pair => new TailwindWatchRoot(pair.Key, pair.Value))
            .OrderBy(static root => root.FullPath, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static void ParseCssGraph(
        string inputPath,
        string repoRoot,
        HashSet<string> parsedCssFiles,
        HashSet<string> contentRoots)
    {
        var pending = new Stack<string>();
        pending.Push(inputPath);

        while (pending.Count > 0)
        {
            var currentPath = pending.Pop();
            if (!File.Exists(currentPath) || !parsedCssFiles.Add(currentPath))
            {
                continue;
            }

            string contents;
            try
            {
                contents = File.ReadAllText(currentPath);
            }
            catch
            {
                continue;
            }

            var currentDirectory = Path.GetDirectoryName(currentPath) ?? Path.GetDirectoryName(inputPath) ?? repoRoot;

            foreach (Match match in ImportDirectiveRegex.Matches(contents))
            {
                var importedPath = ResolvePathFromDirective(currentDirectory, repoRoot, match.Groups["path"].Value, defaultExtension: ".css");
                if (importedPath is not null)
                {
                    pending.Push(importedPath);
                }
            }

            foreach (Match match in SourceDirectiveRegex.Matches(contents))
            {
                var sourcePath = ResolveWatchTargetPath(currentDirectory, repoRoot, match.Groups["path"].Value);
                if (sourcePath is not null)
                {
                    contentRoots.Add(sourcePath);
                }
            }
        }
    }

    private static string? ResolvePathFromDirective(string baseDirectory, string repoRoot, string rawValue, string? defaultExtension)
    {
        if (string.IsNullOrWhiteSpace(rawValue) ||
            (!rawValue.StartsWith(".", StringComparison.Ordinal) && !Path.IsPathRooted(rawValue)))
        {
            return null;
        }

        var candidate = ResolvePath(baseDirectory, rawValue);
        if (File.Exists(candidate) && IsPathUnderRoot(candidate, repoRoot))
        {
            return candidate;
        }

        if (!string.IsNullOrWhiteSpace(defaultExtension) &&
            string.IsNullOrWhiteSpace(Path.GetExtension(candidate)))
        {
            var extendedCandidate = candidate + defaultExtension;
            if (File.Exists(extendedCandidate) && IsPathUnderRoot(extendedCandidate, repoRoot))
            {
                return extendedCandidate;
            }
        }

        return null;
    }

    private static string? ResolveWatchTargetPath(string baseDirectory, string repoRoot, string rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return null;
        }

        var wildcardIndex = rawValue.IndexOfAny(['*', '?', '{', '[']);
        var normalizedValue = wildcardIndex >= 0
            ? rawValue[..wildcardIndex].TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            : rawValue;
        if (string.IsNullOrWhiteSpace(normalizedValue))
        {
            return null;
        }

        var candidate = ResolvePath(baseDirectory, normalizedValue);
        if (Directory.Exists(candidate) && IsPathUnderRoot(candidate, repoRoot))
        {
            return candidate;
        }

        if (File.Exists(candidate) && IsPathUnderRoot(candidate, repoRoot))
        {
            return Path.GetDirectoryName(candidate);
        }

        return null;
    }

    private static void AddRoot(
        IDictionary<string, TailwindWatchRootKind> rootKinds,
        string rootPath,
        TailwindWatchRootKind kind,
        string repoRoot)
    {
        if (string.IsNullOrWhiteSpace(rootPath))
        {
            return;
        }

        var normalizedPath = Path.GetFullPath(rootPath);
        if (!Directory.Exists(normalizedPath) ||
            !IsPathUnderRoot(normalizedPath, repoRoot) ||
            PathContainsIgnoredSegment(normalizedPath))
        {
            return;
        }

        rootKinds[normalizedPath] = rootKinds.TryGetValue(normalizedPath, out var existing)
            ? existing | kind
            : kind;
    }

    private static bool HasTailwindDependency(JsonElement root)
    {
        return DependencySetContains(root, "dependencies", "@tailwindcss/cli") ||
               DependencySetContains(root, "dependencies", "tailwindcss") ||
               DependencySetContains(root, "devDependencies", "@tailwindcss/cli") ||
               DependencySetContains(root, "devDependencies", "tailwindcss");
    }

    private static bool DependencySetContains(JsonElement root, string propertyName, string dependencyName)
    {
        return root.TryGetProperty(propertyName, out var dependencies) &&
               dependencies.ValueKind == JsonValueKind.Object &&
               dependencies.TryGetProperty(dependencyName, out _);
    }

    private static bool TryResolveTailwindScript(JsonElement root, out string command)
    {
        command = string.Empty;
        if (!root.TryGetProperty("scripts", out var scripts) || scripts.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        var candidates = scripts.EnumerateObject()
            .Select(property => new KeyValuePair<string, string>(property.Name, property.Value.GetString() ?? string.Empty))
            .Where(static pair => !string.IsNullOrWhiteSpace(pair.Value))
            .OrderByDescending(static pair => pair.Key.Contains("build", StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(static pair => pair.Key.Contains("watch", StringComparison.OrdinalIgnoreCase))
            .ThenBy(static pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        foreach (var candidate in candidates)
        {
            if (!candidate.Value.Contains("tailwindcss", StringComparison.OrdinalIgnoreCase) &&
                !candidate.Value.Contains("@tailwindcss/cli", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!TryExtractInputOutputPaths(candidate.Value, out _, out _))
            {
                continue;
            }

            command = candidate.Value;
            return true;
        }

        return false;
    }

    internal static bool TryExtractInputOutputPaths(string command, out string inputPath, out string outputPath)
    {
        inputPath = string.Empty;
        outputPath = string.Empty;

        var inputMatch = InputArgumentRegex.Match(command);
        var outputMatch = OutputArgumentRegex.Match(command);
        if (!inputMatch.Success || !outputMatch.Success)
        {
            return false;
        }

        inputPath = TrimQuotes(inputMatch.Groups["value"].Value);
        outputPath = TrimQuotes(outputMatch.Groups["value"].Value);
        return !string.IsNullOrWhiteSpace(inputPath) && !string.IsNullOrWhiteSpace(outputPath);
    }

    private static IEnumerable<string> EnumeratePackageJsonFiles(string repoRoot)
    {
        var pending = new Stack<string>();
        pending.Push(repoRoot);

        while (pending.Count > 0)
        {
            var directory = pending.Pop();
            IEnumerable<string> packageJsonFiles;
            try
            {
                packageJsonFiles = Directory.EnumerateFiles(directory, "package.json", SearchOption.TopDirectoryOnly);
            }
            catch
            {
                continue;
            }

            foreach (var packageJsonFile in packageJsonFiles)
            {
                yield return packageJsonFile;
            }

            IEnumerable<string> childDirectories;
            try
            {
                childDirectories = Directory.EnumerateDirectories(directory, "*", SearchOption.TopDirectoryOnly);
            }
            catch
            {
                continue;
            }

            foreach (var childDirectory in childDirectories)
            {
                var directoryName = Path.GetFileName(childDirectory);
                if (!string.IsNullOrWhiteSpace(directoryName) && !IgnoredDirectories.Contains(directoryName))
                {
                    pending.Push(childDirectory);
                }
            }
        }
    }

    private static string ResolveSearchRoot(string projectDirectory)
    {
        var current = Path.GetFullPath(projectDirectory);
        var bestMarkerDirectory = current;

        while (!string.IsNullOrWhiteSpace(current))
        {
            if (HasRepoMarker(current))
            {
                bestMarkerDirectory = current;
            }

            var parent = Directory.GetParent(current)?.FullName;
            if (string.IsNullOrWhiteSpace(parent) || string.Equals(parent, current, StringComparison.OrdinalIgnoreCase))
            {
                return bestMarkerDirectory;
            }

            current = parent;
        }

        return bestMarkerDirectory;
    }

    private static bool HasRepoMarker(string directory)
    {
        if (Directory.Exists(Path.Combine(directory, ".git")) ||
            File.Exists(Path.Combine(directory, "Directory.Build.props")) ||
            File.Exists(Path.Combine(directory, "Directory.Build.targets")) ||
            File.Exists(Path.Combine(directory, "package.json")))
        {
            return true;
        }

        try
        {
            return Directory.EnumerateFiles(directory, "*.sln", SearchOption.TopDirectoryOnly).Any() ||
                   Directory.EnumerateFiles(directory, "*.slnx", SearchOption.TopDirectoryOnly).Any();
        }
        catch
        {
            return false;
        }
    }

    private static int MeasurePathDistance(string fromDirectory, string toDirectory)
    {
        try
        {
            var relativePath = Path.GetRelativePath(fromDirectory, toDirectory);
            return relativePath.Split(
                [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                StringSplitOptions.RemoveEmptyEntries)
                .Count(segment => !string.Equals(segment, ".", StringComparison.Ordinal));
        }
        catch
        {
            return int.MaxValue;
        }
    }

    private static bool IsPathUnderRoot(string path, string root)
    {
        var normalizedRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)) + Path.DirectorySeparatorChar;
        var normalizedPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path)) + Path.DirectorySeparatorChar;
        return normalizedPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase);
    }

    private static bool PathContainsIgnoredSegment(string path)
    {
        var segments = path.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);
        return segments.Any(IgnoredDirectories.Contains);
    }

    private static string ResolvePath(string baseDirectory, string path)
    {
        return Path.IsPathRooted(path)
            ? Path.GetFullPath(path)
            : Path.GetFullPath(Path.Combine(baseDirectory, path));
    }

    private static string TrimQuotes(string value)
    {
        return value.Trim().Trim('"', '\'');
    }
}
