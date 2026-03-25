using CanDoItAll.Mcp.DotNetWatch.Configuration;

namespace CanDoItAll.Mcp.DotNetWatch.Security;

public sealed class PathGuard(RuntimeConfiguration configuration)
{
    public string ResolveProjectPath(string? path)
    {
        var resolved = ResolvePath(path ?? configuration.DefaultApp.ProjectPath);
        if (!File.Exists(resolved))
        {
            throw new ToolInvocationException("ValidationError", $"Project path '{resolved}' does not exist.", new { path = resolved });
        }

        EnsureAllowedProject(resolved);
        return resolved;
    }

    public string ResolveTargetPath(string? path, string defaultPath)
    {
        var resolved = ResolveInsideWorkspace(path ?? defaultPath);
        if (string.Equals(resolved, configuration.SolutionPath, StringComparison.OrdinalIgnoreCase))
        {
            return resolved;
        }

        EnsureAllowedProject(resolved);
        return resolved;
    }

    public string ResolveEntryPath(string path)
    {
        var resolved = ResolveInsideWorkspace(path);
        if (!File.Exists(resolved))
        {
            throw new ToolInvocationException("ValidationError", $"Entry path '{resolved}' does not exist.", new { path = resolved });
        }

        return resolved;
    }

    public string ResolveWorkingDirectory(string? workingDirectory, string projectPath)
    {
        var candidate = string.IsNullOrWhiteSpace(workingDirectory)
            ? Path.GetDirectoryName(projectPath)!
            : ResolvePath(workingDirectory);
        EnsureAllowedProject(candidate);
        return candidate;
    }

    public string ResolveInsideWorkspace(string path)
    {
        var resolved = ResolvePath(path);
        EnsureInsideWorkspace(resolved);
        return resolved;
    }

    public void EnsureInsideWorkspace(string path)
    {
        if (!IsPathUnderRoot(path, configuration.WorkspaceRoot))
        {
            throw new ToolInvocationException("PathOutsideWorkspace", $"Path '{path}' resolves outside the workspace.", new { path });
        }
    }

    public void EnsureAllowedProject(string path)
    {
        if (configuration.AllowedProjectRoots.Any(root => IsPathUnderRoot(path, root)) ||
            configuration.AllowedExternalProjectRoots.Any(root => IsPathUnderRoot(path, root)))
        {
            return;
        }

        throw new ToolInvocationException(
            "SecurityViolation",
            $"Path '{path}' is not inside an allowed project root.",
            new
            {
                path,
                allowedRoots = configuration.AllowedProjectRoots.Concat(configuration.AllowedExternalProjectRoots).ToArray()
            });
    }

    private string ResolvePath(string path)
    {
        return Path.IsPathRooted(path)
            ? Path.GetFullPath(path)
            : Path.GetFullPath(Path.Combine(configuration.WorkspaceRoot, path));
    }

    private static bool IsPathUnderRoot(string path, string root)
    {
        var normalizedRoot = Path.TrimEndingDirectorySeparator(root) + Path.DirectorySeparatorChar;
        var normalizedPath = Path.TrimEndingDirectorySeparator(path) + Path.DirectorySeparatorChar;
        return normalizedPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase);
    }
}

public sealed class EnvironmentOverlayFilter(RuntimeConfiguration configuration)
{
    public IReadOnlyDictionary<string, string> Merge(
        IReadOnlyDictionary<string, string> defaults,
        IReadOnlyDictionary<string, string?>? requested,
        bool includePollingWatcher)
    {
        var merged = new Dictionary<string, string>(defaults, StringComparer.OrdinalIgnoreCase);
        if (includePollingWatcher)
        {
            merged["DOTNET_USE_POLLING_FILE_WATCHER"] = "1";
        }

        if (requested is null)
        {
            return merged;
        }

        foreach (var (key, value) in requested)
        {
            if (!configuration.AllowedEnvironmentKeys.Contains(key))
            {
                throw new ToolInvocationException("SecurityViolation", $"Environment key '{key}' is not allowed.", new { key });
            }

            if (value is null)
            {
                continue;
            }

            merged[key] = value;
        }

        return merged;
    }
}
