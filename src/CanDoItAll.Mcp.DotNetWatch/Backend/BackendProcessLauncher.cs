using CanDoItAll.Mcp.DotNetWatch.Configuration;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;

namespace CanDoItAll.Mcp.DotNetWatch.Backend;

internal sealed class BackendProcessLauncher(
    RuntimeConfiguration configuration,
    LaunchContext launchContext,
    ILogger<BackendProcessLauncher> logger)
{
    public void StartDetached(string backendToken)
    {
        var (fileName, arguments) = ResolveCommandLine();
        var startInfo = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? CreateWindowsLauncherStartInfo(fileName, arguments, backendToken)
            : CreateUnixDetachedStartInfo(fileName, arguments, backendToken);

        using var process = new Process
        {
            StartInfo = startInfo
        };

        if (!process.Start())
        {
            throw new InvalidOperationException("Failed to start the backend daemon process.");
        }

        logger.LogInformation("Started detached backend launcher PID {Pid}", process.Id);
    }

    private (string FileName, IReadOnlyList<string> Arguments) ResolveCommandLine()
    {
        var entryAssemblyPath = Path.GetFullPath(Assembly.GetEntryAssembly()?.Location ?? configuration.ServerAssemblyPath);
        if (entryAssemblyPath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
        {
            return ("dotnet", [entryAssemblyPath]);
        }

        return (entryAssemblyPath, Array.Empty<string>());
    }

    private ProcessStartInfo CreateWindowsLauncherStartInfo(string fileName, IReadOnlyList<string> arguments, string backendToken)
    {
        var startInfo = new ProcessStartInfo(fileName)
        {
            WorkingDirectory = configuration.WorkspaceRoot,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        startInfo.ArgumentList.Add("--backend-launcher");
        startInfo.ArgumentList.Add("--settings");
        startInfo.ArgumentList.Add(Path.GetFullPath(launchContext.SettingsPath));
        startInfo.ArgumentList.Add("--backend-token");
        startInfo.ArgumentList.Add(backendToken);

        return startInfo;
    }

    private ProcessStartInfo CreateUnixDetachedStartInfo(string fileName, IReadOnlyList<string> arguments, string backendToken)
    {
        var daemonArguments = arguments
            .Concat(
            [
                "--backend",
                "--settings",
                Path.GetFullPath(launchContext.SettingsPath),
                "--backend-token",
                backendToken
            ])
            .ToArray();
        var escapedCommand = string.Join(
            ' ',
            new[] { QuoteShell(fileName) }.Concat(daemonArguments.Select(QuoteShell)));
        var shellCommand = $"nohup {escapedCommand} >/dev/null 2>&1 &";

        return new ProcessStartInfo("/bin/sh")
        {
            WorkingDirectory = configuration.WorkspaceRoot,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = false,
            RedirectStandardError = false,
            ArgumentList =
            {
                "-c",
                shellCommand
            }
        };
    }

    private static string QuoteShell(string value)
    {
        return $"'{value.Replace("'", "'\"'\"'", StringComparison.Ordinal)}'";
    }
}
