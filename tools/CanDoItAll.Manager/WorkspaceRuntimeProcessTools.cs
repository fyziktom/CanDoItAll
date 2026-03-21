using System.Diagnostics;
using System.Management;
using System.Runtime.Versioning;
using System.Xml.Linq;

namespace CanDoItAll.Manager;

public sealed record WorkspaceProcessSnapshot(int ProcessId, string Name, string? CommandLine, string? ExecutablePath);

public static class WorkspaceRuntimeProcessTools
{
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
            var projectPaths = EnumerateRestoreProjectPaths(watchProjectPath).ToArray();
            if (projectPaths.Length == 0)
            {
                return false;
            }

            var latestRestoreInputUtc = EnumerateRestoreInputFiles(workspaceRoot, projectPaths)
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

    private static IEnumerable<string> EnumerateRestoreInputFiles(string workspaceRoot, IReadOnlyList<string> projectPaths)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
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

            foreach (var directory in EnumerateDirectoriesUpToWorkspaceRoot(projectDirectory, workspaceRoot))
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

        if (!string.IsNullOrWhiteSpace(workspaceRoot))
        {
            var globalJsonPath = Path.Combine(workspaceRoot, "global.json");
            if (File.Exists(globalJsonPath) && seen.Add(globalJsonPath))
            {
                yield return globalJsonPath;
            }
        }
    }

    private static IEnumerable<string> EnumerateDirectoriesUpToWorkspaceRoot(string startDirectory, string workspaceRoot)
    {
        var current = Path.GetFullPath(startDirectory)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var normalizedWorkspaceRoot = string.IsNullOrWhiteSpace(workspaceRoot)
            ? null
            : Path.GetFullPath(workspaceRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        while (!string.IsNullOrWhiteSpace(current))
        {
            yield return current;

            if (normalizedWorkspaceRoot is not null &&
                string.Equals(current, normalizedWorkspaceRoot, StringComparison.OrdinalIgnoreCase))
            {
                yield break;
            }

            var parent = Directory.GetParent(current)?.FullName?
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (string.IsNullOrWhiteSpace(parent) ||
                string.Equals(parent, current, StringComparison.OrdinalIgnoreCase))
            {
                yield break;
            }

            current = parent;
        }
    }

    private static IEnumerable<string> EnumerateRestoreProjectPaths(string watchProjectPath)
    {
        var pending = new Stack<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        pending.Push(Path.GetFullPath(watchProjectPath));

        while (pending.Count > 0)
        {
            var projectPath = pending.Pop();
            if (!seen.Add(projectPath) || !File.Exists(projectPath))
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

    public static bool IsWorkspaceOwnedProcess(WorkspaceProcessSnapshot process, string watchProjectPath)
    {
        if (process.ProcessId <= 0)
        {
            return false;
        }

        var projectName = Path.GetFileNameWithoutExtension(watchProjectPath);
        var projectDirectory = Path.GetDirectoryName(watchProjectPath) ?? string.Empty;

        if (string.Equals(process.Name, projectName, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(process.Name, $"{projectName}.exe", StringComparison.OrdinalIgnoreCase))
        {
            return string.IsNullOrWhiteSpace(process.ExecutablePath) ||
                   process.ExecutablePath.Contains(projectDirectory, StringComparison.OrdinalIgnoreCase);
        }

        if (!string.Equals(process.Name, "dotnet", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(process.Name, "dotnet.exe", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var commandLine = process.CommandLine ?? string.Empty;
        if (!commandLine.Contains(watchProjectPath, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return commandLine.Contains("dotnet-watch.dll", StringComparison.OrdinalIgnoreCase) ||
               commandLine.Contains("watch --project", StringComparison.OrdinalIgnoreCase) ||
               commandLine.Contains("DOTNET_WATCH=1", StringComparison.OrdinalIgnoreCase);
    }

    public static IReadOnlyList<WorkspaceProcessSnapshot> EnumerateWorkspaceOwnedProcesses(string watchProjectPath, int currentProcessId)
    {
        if (OperatingSystem.IsWindows())
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT ProcessId, Name, CommandLine, ExecutablePath FROM Win32_Process");
                return searcher.Get()
                    .OfType<ManagementObject>()
                    .Select(ToSnapshot)
                    .Where(snapshot => snapshot is not null)
                    .Select(snapshot => snapshot!)
                    .Where(snapshot => snapshot.ProcessId != currentProcessId)
                    .Where(snapshot => IsWorkspaceOwnedProcess(snapshot, watchProjectPath))
                    .ToArray();
            }
            catch
            {
                return [];
            }
        }

        var projectName = Path.GetFileNameWithoutExtension(watchProjectPath);
        return Process.GetProcessesByName(projectName)
            .Where(process => process.Id != currentProcessId)
            .Select(ToSnapshot)
            .Where(snapshot => snapshot is not null)
            .Select(snapshot => snapshot!)
            .Where(snapshot => IsWorkspaceOwnedProcess(snapshot, watchProjectPath))
            .ToArray();
    }

    [SupportedOSPlatform("windows")]
    private static WorkspaceProcessSnapshot? ToSnapshot(ManagementObject managementObject)
    {
        try
        {
            return new WorkspaceProcessSnapshot(
                Convert.ToInt32(managementObject["ProcessId"]),
                Convert.ToString(managementObject["Name"]) ?? string.Empty,
                Convert.ToString(managementObject["CommandLine"]),
                Convert.ToString(managementObject["ExecutablePath"]));
        }
        catch
        {
            return null;
        }
    }

    private static WorkspaceProcessSnapshot? ToSnapshot(Process process)
    {
        try
        {
            var executablePath = process.MainModule?.FileName;
            return new WorkspaceProcessSnapshot(process.Id, process.ProcessName, null, executablePath);
        }
        catch
        {
            return null;
        }
    }
}
