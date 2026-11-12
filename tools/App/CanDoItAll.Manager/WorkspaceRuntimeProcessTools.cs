using System.Xml.Linq;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.Infrastructure;
using CanDoItAll.Infrastructure.FileSystem;

namespace CanDoItAll.Manager;

public static class WorkspaceRuntimeProcessTools
{
    private static readonly IPhysicalFileSystemPathPolicyFactory PhysicalPathPolicyFactory =
        new PhysicalFileSystemPathPolicyFactory();

    private static readonly string[] RestoreInputFileNames =
    [
        "Directory.Build.props",
        "Directory.Build.targets",
        "Directory.Packages.props",
        "Packages.props",
        "NuGet.Config",
        "nuget.config",
        "packages.lock.json"
    ];

    public static IReadOnlyList<string> BuildWatchArgumentList(string workspaceRoot, string watchProjectPath, ManagerOptions options)
    {
        var explicitUrls = GetExplicitWatchUrls(options);
        var arguments = new List<string>
        {
            "watch",
            "--non-interactive",
            "--project",
            watchProjectPath
        };

        if (ShouldSkipRestore(workspaceRoot, watchProjectPath, options))
        {
            arguments.Add("--no-restore");
        }

        if (options.WatchDisableBuildServers)
        {
            arguments.Add("--disable-build-servers");
        }
        
        arguments.Add("run");

        if (explicitUrls.Count > 0 || string.IsNullOrWhiteSpace(options.WatchLaunchProfile))
        {
            arguments.Add("--no-launch-profile");
        }
        else
        {
            arguments.Add("--launch-profile");
            arguments.Add(options.WatchLaunchProfile.Trim());
        }

        return arguments;
    }

    public static string? BuildWatchUrlsEnvironmentValue(ManagerOptions options)
    {
        var urls = GetExplicitWatchUrls(options);

        return urls.Count == 0 ? null : string.Join(';', urls);
    }

    public static IReadOnlyDictionary<string, string> BuildWatchEnvironmentVariables(ManagerOptions options, string environmentName)
    {
        var variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["DOTNET_WATCH_SUPPRESS_EMOJIS"] = "1",
            ["ASPNETCORE_ENVIRONMENT"] = environmentName,
            ["DOTNET_ENVIRONMENT"] = environmentName
        };

        if (options.WatchSuppressBrowserRefresh)
        {
            variables["DOTNET_WATCH_SUPPRESS_BROWSER_REFRESH"] = "1";
        }

        if (options.WatchDisableBuildServers)
        {
            variables["DOTNET_CLI_USE_MSBUILD_SERVER"] = "0";
        }

        if (options.WatchDisableAppHost)
        {
            variables["UseAppHost"] = "false";
        }

        if (options.WatchDisableSharedCompilation)
        {
            variables["UseSharedCompilation"] = "false";
        }

        if (options.WatchDetailedErrorsEnabled)
        {
            variables["DetailedErrors"] = "true";
            variables["ASPNETCORE_DETAILEDERRORS"] = "true";
        }

        var watchUrls = BuildWatchUrlsEnvironmentValue(options);
        if (!string.IsNullOrWhiteSpace(watchUrls))
        {
            variables["ASPNETCORE_URLS"] = watchUrls;
        }

        return variables;
    }

    public static string ResolveTailwindCliShimPath(string tailwindWorkspacePath)
        => Path.Combine(
            tailwindWorkspacePath,
            "node_modules",
            ".bin",
            OperatingSystem.IsWindows() ? "tailwindcss.cmd" : "tailwindcss");

    public static ManagerExecutablePlan BuildNpmCommandPlan(string workingDirectory, IReadOnlyList<string> npmArguments)
    {
        var locator = new WorkspaceExecutableLocator();
        return CreateNpmCommandPlan(
            locator.ResolveExecutablePath(["node"], workingDirectory),
            locator.ResolveExecutablePath(["npm"], workingDirectory),
            npmArguments);
    }

    // npm is launched through its JavaScript entry point so no command shell sits between the Manager
    // and npm; npm itself still runs the package script through its configured script shell.
    internal static ManagerExecutablePlan CreateNpmCommandPlan(
        string nodePath,
        string npmPath,
        IReadOnlyList<string> npmArguments)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nodePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(npmPath);
        ArgumentNullException.ThrowIfNull(npmArguments);
        var npmCliPath = TryResolveNpmCliScriptPath(npmPath);
        if (npmCliPath is not null)
        {
            return new ManagerExecutablePlan(nodePath, [npmCliPath, .. npmArguments]);
        }

        if (OperatingSystem.IsWindows())
        {
            throw new InvalidOperationException(
                $"The npm installation at '{npmPath}' does not expose npm-cli.js, so the Manager cannot run npm without a command shell.");
        }

        return new ManagerExecutablePlan(npmPath, npmArguments);
    }

    private static string? TryResolveNpmCliScriptPath(string npmPath)
    {
        if (IsJavaScriptFile(npmPath))
        {
            return npmPath;
        }

        var linkTarget = TryResolveFinalLinkTarget(npmPath);
        if (linkTarget is not null && IsJavaScriptFile(linkTarget) && File.Exists(linkTarget))
        {
            return linkTarget;
        }

        var npmDirectory = Path.GetDirectoryName(Path.GetFullPath(npmPath)) ?? string.Empty;
        string[] candidates =
        [
            Path.Combine(npmDirectory, "node_modules", "npm", "bin", "npm-cli.js"),
            Path.Combine(npmDirectory, "..", "lib", "node_modules", "npm", "bin", "npm-cli.js")
        ];
        return candidates
            .Select(Path.GetFullPath)
            .FirstOrDefault(File.Exists);
    }

    private static string? TryResolveFinalLinkTarget(string path)
    {
        try
        {
            return File.ResolveLinkTarget(path, returnFinalTarget: true)?.FullName;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static bool IsJavaScriptFile(string path)
        => string.Equals(Path.GetExtension(path), ".js", StringComparison.OrdinalIgnoreCase);

    public static IReadOnlyList<string> GetExplicitWatchUrls(ManagerOptions options)
        => options.WatchUrls
            .Where(url => !string.IsNullOrWhiteSpace(url))
            .Select(url => url.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    public static bool RequiresWorkspaceRecovery(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        return line.Contains("address already in use", StringComparison.OrdinalIgnoreCase) ||
               line.Contains("error CS2012", StringComparison.OrdinalIgnoreCase) ||
               line.Contains("error MSB3021", StringComparison.OrdinalIgnoreCase) ||
               line.Contains("error MSB3027", StringComparison.OrdinalIgnoreCase) ||
               (line.Contains("Cannot open", StringComparison.OrdinalIgnoreCase) &&
                (line.Contains("user-mapped section open", StringComparison.OrdinalIgnoreCase) ||
                 line.Contains("being used by another process", StringComparison.OrdinalIgnoreCase)));
    }

    private static bool ShouldSkipRestore(string workspaceRoot, string watchProjectPath, ManagerOptions options)
    {
        if (!options.WatchSkipRestore || string.IsNullOrWhiteSpace(watchProjectPath) || !File.Exists(watchProjectPath))
        {
            return false;
        }

        try
        {
            var workspacePolicy = PhysicalPathPolicyFactory.Create(workspaceRoot);
            var projectPaths = EnumerateRestoreProjectPaths(watchProjectPath, workspacePolicy).ToArray();
            if (projectPaths.Length == 0)
            {
                return false;
            }

            var latestRestoreInputUtc = EnumerateRestoreInputFiles(workspacePolicy, projectPaths)
                .Select(File.GetLastWriteTimeUtc)
                .DefaultIfEmpty(DateTime.MinValue)
                .Max();

            foreach (var projectPath in projectPaths)
            {
                var assetsPath = Path.Combine(Path.GetDirectoryName(projectPath)!, "obj", "project.assets.json");
                if (!File.Exists(assetsPath))
                {
                    return false;
                }

                var assetsTimestampUtc = File.GetLastWriteTimeUtc(assetsPath);
                if (assetsTimestampUtc < File.GetLastWriteTimeUtc(projectPath) ||
                    assetsTimestampUtc < latestRestoreInputUtc)
                {
                    return false;
                }
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static IEnumerable<string> EnumerateRestoreInputFiles(
        IPhysicalFileSystemPathPolicy workspacePolicy,
        IReadOnlyList<string> projectPaths)
    {
        var seen = new HashSet<string>(workspacePolicy.PathComparer);
        foreach (var projectPath in projectPaths)
        {
            if (seen.Add(projectPath))
            {
                yield return projectPath;
            }

            var projectDirectory = Path.GetDirectoryName(projectPath);
            if (string.IsNullOrWhiteSpace(projectDirectory))
            {
                continue;
            }

            foreach (var directory in EnumerateDirectoriesUpToWorkspaceRoot(projectDirectory, workspacePolicy))
            {
                foreach (var fileName in RestoreInputFileNames)
                {
                    var candidatePath = Path.Combine(directory, fileName);
                    if (File.Exists(candidatePath) && seen.Add(candidatePath))
                    {
                        yield return candidatePath;
                    }
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(workspacePolicy.RootPath))
        {
            var globalJsonPath = Path.Combine(workspacePolicy.RootPath, "global.json");
            if (File.Exists(globalJsonPath) && seen.Add(globalJsonPath))
            {
                yield return globalJsonPath;
            }
        }
    }

    private static IEnumerable<string> EnumerateDirectoriesUpToWorkspaceRoot(
        string startDirectory,
        IPhysicalFileSystemPathPolicy workspacePolicy)
    {
        var current = Path.GetFullPath(startDirectory)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (!workspacePolicy.IsWithinRoot(current))
        {
            yield break;
        }

        while (!string.IsNullOrWhiteSpace(current))
        {
            yield return current;

            if (workspacePolicy.PathComparer.Equals(current, workspacePolicy.RootPath))
            {
                yield break;
            }

            var parent = Directory.GetParent(current)?.FullName?
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (string.IsNullOrWhiteSpace(parent) ||
                workspacePolicy.PathComparer.Equals(parent, current))
            {
                yield break;
            }

            current = parent;
        }
    }

    private static IEnumerable<string> EnumerateRestoreProjectPaths(
        string watchProjectPath,
        IPhysicalFileSystemPathPolicy workspacePolicy)
    {
        var pending = new Stack<string>();
        var seen = new HashSet<string>(workspacePolicy.PathComparer);
        pending.Push(Path.GetFullPath(watchProjectPath));

        while (pending.Count > 0)
        {
            var projectPath = pending.Pop();
            if (!workspacePolicy.IsWithinRoot(projectPath) ||
                !seen.Add(projectPath) ||
                !File.Exists(projectPath))
            {
                continue;
            }

            yield return projectPath;

            var projectDirectory = Path.GetDirectoryName(projectPath);
            if (string.IsNullOrWhiteSpace(projectDirectory))
            {
                continue;
            }

            XDocument document;
            try
            {
                document = XDocument.Load(projectPath);
            }
            catch
            {
                continue;
            }

            foreach (var projectReference in document
                         .Descendants()
                         .Where(static element => element.Name.LocalName == "ProjectReference"))
            {
                var includePath = projectReference.Attribute("Include")?.Value;
                if (string.IsNullOrWhiteSpace(includePath))
                {
                    continue;
                }

                pending.Push(Path.GetFullPath(includePath, projectDirectory));
            }
        }
    }

}

public sealed record ManagerExecutablePlan(
    string ExecutablePath,
    IReadOnlyList<string> Arguments);
