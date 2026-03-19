using System.Diagnostics;
using System.Management;
using System.Runtime.Versioning;

namespace CanDoItAll.Manager;

public sealed record WorkspaceProcessSnapshot(int ProcessId, string Name, string? CommandLine, string? ExecutablePath);

public static class WorkspaceRuntimeProcessTools
{
    public static IReadOnlyList<string> BuildWatchArgumentList(string watchProjectPath, ManagerOptions options)
    {
        var explicitUrls = GetExplicitWatchUrls(options);
        var arguments = new List<string>
        {
            "watch",
            "--non-interactive",
            "--project",
            watchProjectPath
        };

        if (options.WatchSkipRestore)
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
