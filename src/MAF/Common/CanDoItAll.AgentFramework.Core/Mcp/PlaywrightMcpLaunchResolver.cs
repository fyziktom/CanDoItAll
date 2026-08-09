using System.Diagnostics;

namespace CanDoItAll.AgentFramework.Core;

public sealed record PlaywrightMcpLaunchResolution(
    string Command,
    IReadOnlyList<string> Arguments);

public static class PlaywrightMcpLaunchResolver
{
    private const string PlaywrightMcpPackagePrefix = "@playwright/mcp";

    public static async Task<PlaywrightMcpLaunchResolution?> TryResolveAsync(
        string workspaceRoot,
        string command,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        if (!IsNpxCommand(command) ||
            !TrySplitPlaywrightMcpArguments(arguments, out var packageSpec, out var serverArguments))
        {
            return null;
        }

        var toolRoot = Path.Combine(workspaceRoot, ".agent-tools", "npm", "playwright-mcp");
        var packageRoot = Path.Combine(toolRoot, "node_modules", "@playwright", "mcp");
        var cliPath = Path.Combine(packageRoot, "cli.js");
        if (!File.Exists(cliPath) &&
            !TryResolveCachedNpxPlaywrightMcpCli(out cliPath))
        {
            await EnsurePlaywrightMcpPackageInstalledAsync(
                    toolRoot,
                    packageSpec,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (!File.Exists(cliPath))
        {
            throw new InvalidOperationException(
                $"Playwright MCP package '{packageSpec}' was installed under '{toolRoot}', but '{cliPath}' was not created.");
        }

        return new PlaywrightMcpLaunchResolution(
            ResolveNodeCommand(),
            [cliPath, ..serverArguments]);
    }

    private static async Task EnsurePlaywrightMcpPackageInstalledAsync(
        string toolRoot,
        string packageSpec,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(toolRoot);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(5));

        var startInfo = new ProcessStartInfo
        {
            FileName = ResolveNpmCommand(),
            WorkingDirectory = toolRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add("install");
        startInfo.ArgumentList.Add("--prefix");
        startInfo.ArgumentList.Add(toolRoot);
        startInfo.ArgumentList.Add("--no-audit");
        startInfo.ArgumentList.Add("--fund=false");
        startInfo.ArgumentList.Add(packageSpec);

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Could not start npm while preparing Playwright MCP package '{packageSpec}'.");

        var stdoutTask = process.StandardOutput.ReadToEndAsync(timeout.Token);
        var stderrTask = process.StandardError.ReadToEndAsync(timeout.Token);
        try
        {
            await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
            var stdout = await stdoutTask.ConfigureAwait(false);
            var stderr = await stderrTask.ConfigureAwait(false);
            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"npm install for Playwright MCP package '{packageSpec}' failed with exit code {process.ExitCode}. stdout: {TrimProcessOutput(stdout)} stderr: {TrimProcessOutput(stderr)}");
            }
        }
        catch (OperationCanceledException)
        {
            TryKillProcessTree(process);
            if (!cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException($"Timed out preparing Playwright MCP package '{packageSpec}' under '{toolRoot}'.");
            }

            throw;
        }
    }

    private static bool IsNpxCommand(string command)
    {
        var fileName = Path.GetFileName(command.Trim());
        return string.Equals(fileName, "npx", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(fileName, "npx.cmd", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(fileName, "npx.exe", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(fileName, "npx.ps1", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TrySplitPlaywrightMcpArguments(
        IReadOnlyList<string> arguments,
        out string packageSpec,
        out IReadOnlyList<string> serverArguments)
    {
        packageSpec = string.Empty;
        serverArguments = [];

        var values = arguments
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Trim())
            .ToArray();
        var packageIndex = Array.FindIndex(
            values,
            item => item.StartsWith(PlaywrightMcpPackagePrefix, StringComparison.OrdinalIgnoreCase));
        if (packageIndex < 0)
        {
            return false;
        }

        packageSpec = values[packageIndex];
        serverArguments = values
            .Where((_, index) => index != packageIndex)
            .Where(item => !IsNpxOnlyArgument(item))
            .ToArray();
        return true;
    }

    private static bool TryResolveCachedNpxPlaywrightMcpCli(out string cliPath)
    {
        cliPath = string.Empty;

        foreach (var cacheRoot in ResolveNpmCacheRoots())
        {
            var npxRoot = Path.Combine(cacheRoot, "_npx");
            if (!Directory.Exists(npxRoot))
            {
                continue;
            }

            var candidate = Directory.EnumerateFiles(npxRoot, "cli.js", SearchOption.AllDirectories)
                .Where(path => path.Contains($"{Path.DirectorySeparatorChar}@playwright{Path.DirectorySeparatorChar}mcp{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
                .Select(path => new FileInfo(path))
                .Where(file => file.Exists)
                .OrderByDescending(file => file.LastWriteTimeUtc)
                .ThenBy(file => file.Name, StringComparer.Ordinal)
                .ThenBy(file => file.FullName, StringComparer.Ordinal)
                .FirstOrDefault();
            if (candidate is null)
            {
                continue;
            }

            cliPath = candidate.FullName;
            return true;
        }

        return false;
    }

    private static IReadOnlyList<string> ResolveNpmCacheRoots()
    {
        var roots = new List<string>();
        AddRoot(Environment.GetEnvironmentVariable("npm_config_cache"));
        if (OperatingSystem.IsWindows())
        {
            AddRoot(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "npm-cache"));
        }
        else
        {
            AddRoot(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".npm"));
        }

        return roots;

        void AddRoot(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            var fullPath = Path.GetFullPath(path);
            if (!roots.Contains(fullPath, StringComparer.OrdinalIgnoreCase))
            {
                roots.Add(fullPath);
            }
        }
    }

    private static bool IsNpxOnlyArgument(string argument)
        => string.Equals(argument, "--yes", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(argument, "-y", StringComparison.OrdinalIgnoreCase);

    private static string ResolveNodeCommand()
        => OperatingSystem.IsWindows() ? "node.exe" : "node";

    private static string ResolveNpmCommand()
        => OperatingSystem.IsWindows() ? "npm.cmd" : "npm";

    private static string TrimProcessOutput(string value)
    {
        const int maxCharacters = 2000;
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= maxCharacters
            ? trimmed
            : trimmed[^maxCharacters..];
    }

    private static void TryKillProcessTree(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
        }
    }
}
