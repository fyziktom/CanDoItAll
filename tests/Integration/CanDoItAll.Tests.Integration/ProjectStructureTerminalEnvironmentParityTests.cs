using System.Diagnostics;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.Modules.Workbench;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Integration.Runtime;

// Stage B terminal parity on a real shell: the command the terminal presenter builds, run by the host's
// actual PowerShell (Windows) or env (Linux, macOS), gives the runtime only the composed environment even
// though the terminal itself starts with the host's full environment, including a secret.
[Trait("Category", "HostPlatform")]
public sealed class ProjectStructureTerminalEnvironmentParityTests
{
    private const string SecretName = "OPENAI_API_KEY";
    private const string SecretValue = "cdia-terminal-parity-secret";

    [Fact]
    public async Task The_terminal_command_removes_host_secrets_before_the_runtime_starts()
    {
        var hostContext = ProjectStructureRuntimeHostContext.CaptureCurrent();
        var workingDirectory = Path.GetTempPath();
        ProcessStartInfo startInfo;
        IReadOnlyDictionary<string, string?> composed;
        if (hostContext.Platform == ProjectStructureRuntimeHostPlatform.Windows)
        {
            var shell = FindOnPath("pwsh.exe") ?? FindOnPath("powershell.exe");
            if (shell is null)
            {
                return;
            }

            var plan = CreatePlan(
                [shell],
                ["-NoProfile", "-NonInteractive", "-Command", "Get-ChildItem Env: | ForEach-Object { $_.Name + '=' + $_.Value }"],
                workingDirectory);
            var presenter = CreatePresenter(hostContext, new ProjectStructureRuntimePresentationOptions());
            composed = ProjectStructureRuntimeEnvironment.Compose(new WorkspaceCommandEnvironmentPolicy(), plan, shell);
            startInfo = presenter.BuildStartInfo(plan, shell, shell);
            // The presenter opens a visible window; run the same command hidden and without -NoExit instead.
            startInfo.UseShellExecute = false;
            startInfo.ArgumentList.Remove("-NoExit");
            startInfo.ArgumentList.Insert(0, "-NonInteractive");
            startInfo.ArgumentList.Insert(0, "-NoProfile");
        }
        else
        {
            var env = FindOnPath("env");
            if (env is null)
            {
                return;
            }

            var plan = CreatePlan([env], [], workingDirectory);
            var presenter = CreatePresenter(hostContext, new ProjectStructureRuntimePresentationOptions
            {
                LinuxTerminalExecutable = env,
                MacOsTerminalExecutable = env
            });
            composed = ProjectStructureRuntimeEnvironment.Compose(new WorkspaceCommandEnvironmentPolicy(), plan, env);
            // The "terminal" is env itself, so it runs `env -i <composed pairs> env` and prints the result.
            startInfo = presenter.BuildStartInfo(plan, env, env);
        }

        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;
        startInfo.Environment[SecretName] = SecretValue;

        using var process = Process.Start(startInfo)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        await process.WaitForExitAsync(timeout.Token);
        var output = await stdout;

        Assert.True(process.ExitCode == 0, $"exit {process.ExitCode}: {await stderr}");
        Assert.DoesNotContain(SecretValue, output, StringComparison.Ordinal);
        var names = output
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Where(line => line.Contains('=', StringComparison.Ordinal))
            .Select(line => line[..line.IndexOf('=', StringComparison.Ordinal)])
            .ToArray();
        Assert.NotEmpty(names);
        Assert.DoesNotContain(SecretName, names);
        var comparer = hostContext.Platform == ProjectStructureRuntimeHostPlatform.Windows
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;
        // A shell may add a few variables of its own; every other name must come from the composed set.
        var shellOwned = new HashSet<string>(["PSModulePath", "POWERSHELL_DISTRIBUTION_CHANNEL", "PWD", "SHLVL", "_"], comparer);
        var unexpected = names.Where(name => !composed.ContainsKey(name) && !shellOwned.Contains(name)).ToArray();
        Assert.True(unexpected.Length == 0, $"Variables outside the composed environment: {string.Join(", ", unexpected)}");
    }

    private static ProjectStructureRuntimeLaunchPlan CreatePlan(
        IReadOnlyList<string> executable,
        IReadOnlyList<string> arguments,
        string workingDirectory)
        => new(
            ProjectStructureRuntimePlanKind.DirectExecutable,
            executable,
            arguments,
            new Dictionary<string, string?>(),
            workingDirectory,
            "terminal parity",
            "Terminal parity",
            [],
            RequiresApproval: false,
            TerminalOnly: false);

    private static ProjectStructureTerminalPresenter CreatePresenter(
        ProjectStructureRuntimeHostContext hostContext,
        ProjectStructureRuntimePresentationOptions options)
        => new(hostContext, options, new UnusedResolver(), NullLogger<ProjectStructureTerminalPresenter>.Instance);

    private static string? FindOnPath(string fileName)
        => (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(directory => Path.Combine(directory.Trim(), fileName))
            .FirstOrDefault(File.Exists);

    private sealed class UnusedResolver : IProjectStructureExecutableResolver
    {
        public ProjectStructureExecutableResolution Resolve(IReadOnlyList<string> candidates, string workingDirectory)
            => throw new InvalidOperationException("The start info is built from explicit paths.");
    }
}
