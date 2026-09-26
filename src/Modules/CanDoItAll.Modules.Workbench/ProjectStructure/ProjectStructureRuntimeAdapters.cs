using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.Versioning;
using CanDoItAll.AgentFramework.Core;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.Workbench;

public sealed class ProjectStructureRuntimePresentationOptions
{
    public const string SectionName = "Workbench:RuntimePresentation";

    public bool EnableWindowsTerminal { get; init; } = true;

    public string LinuxTerminalExecutable { get; init; } = string.Empty;

    public IReadOnlyList<string> LinuxTerminalArgumentPrefix { get; init; } = [];

    public string MacOsTerminalExecutable { get; init; } = string.Empty;

    public IReadOnlyList<string> MacOsTerminalArgumentPrefix { get; init; } = [];

    public static ProjectStructureRuntimePresentationOptions Default { get; } = new();
}

internal sealed record ProjectStructureRuntimeHostContext(ProjectStructureRuntimeHostPlatform Platform)
{
    public static ProjectStructureRuntimeHostContext CaptureCurrent()
        => new(
            OperatingSystem.IsWindows()
                ? ProjectStructureRuntimeHostPlatform.Windows
                : OperatingSystem.IsLinux()
                    ? ProjectStructureRuntimeHostPlatform.Linux
                    : OperatingSystem.IsMacOS()
                        ? ProjectStructureRuntimeHostPlatform.MacOS
                        : throw new PlatformNotSupportedException(
                            "Workbench runtime execution is supported only on Windows, Linux, and macOS."));
}

internal sealed record ProjectStructureExecutableResolution(
    string? ExecutablePath,
    string Message)
{
    public bool IsSuccess => !string.IsNullOrWhiteSpace(ExecutablePath);
}

internal interface IProjectStructureExecutableResolver
{
    ProjectStructureExecutableResolution Resolve(
        IReadOnlyList<string> candidates,
        string workingDirectory);
}

internal sealed class ProjectStructureExecutableResolver(
    WorkspaceExecutableLocator executableLocator) : IProjectStructureExecutableResolver
{
    public ProjectStructureExecutableResolution Resolve(
        IReadOnlyList<string> candidates,
        string workingDirectory)
    {
        try
        {
            var path = executableLocator.ResolveExecutablePath(candidates, workingDirectory);
            return new(path, "Executable dependency is available.");
        }
        catch (WorkspaceExecutableResolutionException exception)
        {
            // Program names only (never folders): this message also reaches agents through node guidance.
            var kind = exception.Failure == WorkspaceExecutableResolutionFailure.Missing
                ? WorkspaceProcessStartFailureKind.ExecutableNotFound
                : WorkspaceProcessStartFailureKind.ExecutableNotRunnable;
            var explanation = WorkspaceLaunchExplanations.For(kind);
            return new(
                null,
                $"{WorkspaceLaunchExplanations.WithProgramNames(explanation.AgentText, candidates)} {explanation.OperatorRemediation}");
        }
        catch (Exception exception) when (
            exception is ArgumentException or IOException or InvalidOperationException or NotSupportedException or UnauthorizedAccessException)
        {
            return new(null, "The required executable dependency is missing, inaccessible, or invalid for this host.");
        }
    }
}

internal interface IProjectStructureRuntimeExecutionAdapter
{
    bool IsRunning(string nodeId);

    ProjectStructureRuntimeExitRecord? GetLastExit(string nodeId);

    Task<ProjectStructureRuntimeExitRecord?> WaitForExitAsync(
        string nodeId,
        TimeSpan timeout,
        CancellationToken cancellationToken);

    ProjectStructureRuntimeCapability Probe(ProjectStructureRuntimeLaunchPlan plan);

    Task<ProjectStructureRuntimeLaunchResult> LaunchAsync(
        ProjectStructureRuntimeLaunchPlan plan,
        string nodeId,
        CancellationToken cancellationToken);

    Task<ProjectStructureRuntimeLaunchResult> StopAsync(
        string nodeId,
        CancellationToken cancellationToken);
}

internal sealed class ProjectStructureRuntimeExecutionAdapter(
    IProjectStructureRuntimeSessionRegistry sessionRegistry,
    IProjectStructureExecutableResolver executableResolver,
    ILogger<ProjectStructureRuntimeExecutionAdapter> logger,
    TimeSpan? earlyExitObservationWindow = null) : IProjectStructureRuntimeExecutionAdapter
{
    private const int OutputLimitCharacters = 16 * 1024;
    private static readonly TimeSpan DefaultEarlyExitObservationWindow = TimeSpan.FromSeconds(3);
    private readonly WorkspaceCommandEnvironmentPolicy environmentPolicy = new();

    public bool IsRunning(string nodeId) => sessionRegistry.IsRunning(nodeId);

    public ProjectStructureRuntimeExitRecord? GetLastExit(string nodeId) => sessionRegistry.GetLastExit(nodeId);

    public Task<ProjectStructureRuntimeExitRecord?> WaitForExitAsync(
        string nodeId,
        TimeSpan timeout,
        CancellationToken cancellationToken)
        => sessionRegistry.WaitForExitAsync(nodeId, timeout, cancellationToken);

    public ProjectStructureRuntimeCapability Probe(ProjectStructureRuntimeLaunchPlan plan)
    {
        if (plan.TerminalOnly)
        {
            return ProjectStructureRuntimeCapability.Unavailable(
                ProjectStructureRuntimeCapabilityStatus.PolicyDenied,
                "This runtime node requires an interactive terminal because it has no headless entry point.");
        }

        var resolution = executableResolver.Resolve(plan.ExecutableCandidates, plan.WorkingDirectory);
        return resolution.IsSuccess
            ? ProjectStructureRuntimeCapability.Available("Direct headless execution is available.")
            : ProjectStructureRuntimeCapability.Unavailable(
                ProjectStructureRuntimeCapabilityStatus.DependencyMissing,
                resolution.Message);
    }

    public async Task<ProjectStructureRuntimeLaunchResult> LaunchAsync(
        ProjectStructureRuntimeLaunchPlan plan,
        string nodeId,
        CancellationToken cancellationToken)
    {
        var resolution = executableResolver.Resolve(plan.ExecutableCandidates, plan.WorkingDirectory);
        if (!resolution.IsSuccess || resolution.ExecutablePath is null)
        {
            return new(false, resolution.Message);
        }

        try
        {
            var environment = ProjectStructureRuntimeEnvironment.Compose(
                environmentPolicy,
                plan,
                resolution.ExecutablePath);
            var start = await sessionRegistry.StartSessionAsync(
                nodeId,
                new WorkspaceProcessSessionRequest(
                    "workbench-runtime-node",
                    $"workbench-runtime:{plan.Kind}",
                    resolution.ExecutablePath,
                    plan.Arguments,
                    plan.WorkingDirectory,
                    environment,
                    OutputLimitCharacters,
                    OutputLimitCharacters,
                    StderrCaptureMode: WorkspaceProcessTextCaptureMode.Tail),
                cancellationToken).ConfigureAwait(false);
            if (!start.IsSuccess || start.Identity is null)
            {
                return new(false, start.Message);
            }

            logger.LogInformation(
                "Started Workbench runtime node {NodeId} as owned process {ProcessId} using plan {PlanKind}.",
                nodeId,
                start.Identity.ProcessId,
                plan.Kind);
            var earlyExit = await sessionRegistry.WaitForExitAsync(
                nodeId,
                earlyExitObservationWindow ?? DefaultEarlyExitObservationWindow,
                cancellationToken).ConfigureAwait(false);
            if (earlyExit is not null && earlyExit.ExitCode != 0)
            {
                return new(false, $"{plan.DisplayName} exited immediately. {earlyExit.Describe()}");
            }

            return new(
                true,
                $"Started {plan.DisplayName} directly as process {start.Identity.ProcessId}. Workbench owns the session until it exits or is stopped.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new(false, "The runtime launch was canceled before ownership could be handed off.");
        }
        catch (WorkspaceProcessStartException exception)
        {
            logger.LogWarning(
                exception,
                "The Workbench runtime process for node {NodeId} could not be started using plan {PlanKind} ({FailureKind}). {OperatorDetail}",
                nodeId,
                plan.Kind,
                exception.FailureKind,
                exception.OperatorDetail);
            // Operator surface (the launch is always operator-initiated): the folder and program are named.
            return new(
                false,
                $"{plan.DisplayName} could not start. " +
                WorkspaceLaunchExplanations.DescribeForOperator(
                    exception.FailureKind,
                    Path.GetFileName(resolution.ExecutablePath),
                    plan.WorkingDirectory));
        }
    }

    public Task<ProjectStructureRuntimeLaunchResult> StopAsync(
        string nodeId,
        CancellationToken cancellationToken)
        => sessionRegistry.StopSessionAsync(nodeId, cancellationToken);

}

/// <summary>
/// The one environment composition for Workbench runtime launches, direct and terminal alike. The
/// process host starts from an empty environment, so without the inherited allowlist (PATH, TEMP,
/// USERPROFILE, SystemRoot, ...) toolchains such as dotnet run fail before the application starts.
/// </summary>
internal static class ProjectStructureRuntimeEnvironment
{
    public static IReadOnlyDictionary<string, string?> Compose(
        WorkspaceCommandEnvironmentPolicy environmentPolicy,
        ProjectStructureRuntimeLaunchPlan plan,
        string? executablePath)
        => environmentPolicy.MergeEnvironmentVariables(
            plan.EnvironmentVariables,
            ResolveEnvironmentProfile(plan.Kind),
            executablePath);

    private static string? ResolveEnvironmentProfile(ProjectStructureRuntimePlanKind kind)
        => kind switch
        {
            ProjectStructureRuntimePlanKind.DotNet => "workspace_dotnet_run",
            ProjectStructureRuntimePlanKind.Python => "workspace_python_run_file",
            ProjectStructureRuntimePlanKind.PowerShellScript => "workspace_pwsh_run_script",
            ProjectStructureRuntimePlanKind.Docker => "docker",
            _ => null
        };
}

internal interface IProjectStructureTerminalPresenter
{
    ProjectStructureRuntimeCapability Probe(ProjectStructureRuntimeLaunchPlan plan);

    Task<ProjectStructureRuntimeLaunchResult> OpenAsync(
        ProjectStructureRuntimeLaunchPlan plan,
        string nodeId,
        CancellationToken cancellationToken);
}

internal sealed class ProjectStructureTerminalPresenter(
    ProjectStructureRuntimeHostContext hostContext,
    ProjectStructureRuntimePresentationOptions options,
    IProjectStructureExecutableResolver executableResolver,
    ILogger<ProjectStructureTerminalPresenter> logger) : IProjectStructureTerminalPresenter
{
    public ProjectStructureRuntimeCapability Probe(ProjectStructureRuntimeLaunchPlan plan)
    {
        var dependency = executableResolver.Resolve(plan.ExecutableCandidates, plan.WorkingDirectory);
        if (!dependency.IsSuccess)
        {
            return ProjectStructureRuntimeCapability.Unavailable(
                ProjectStructureRuntimeCapabilityStatus.DependencyMissing,
                dependency.Message);
        }

        var terminalCandidates = ResolveTerminalCandidates();
        if (terminalCandidates.Count == 0)
        {
            return ProjectStructureRuntimeCapability.Unavailable(
                ProjectStructureRuntimeCapabilityStatus.Headless,
                hostContext.Platform switch
                {
                    ProjectStructureRuntimeHostPlatform.Linux =>
                        "No Linux terminal is configured; direct headless execution remains available when the runtime has an entry point.",
                    ProjectStructureRuntimeHostPlatform.MacOS =>
                        "macOS terminal presentation is disabled until an explicitly configured adapter is validated on an actual macOS host.",
                    _ => "Terminal presentation is disabled for this host."
                });
        }

        var terminal = executableResolver.Resolve(terminalCandidates, plan.WorkingDirectory);
        return terminal.IsSuccess
            ? ProjectStructureRuntimeCapability.Available("Interactive terminal presentation is available.")
            : ProjectStructureRuntimeCapability.Unavailable(
                ProjectStructureRuntimeCapabilityStatus.DependencyMissing,
                "The configured terminal executable is missing or inaccessible.");
    }

    public Task<ProjectStructureRuntimeLaunchResult> OpenAsync(
        ProjectStructureRuntimeLaunchPlan plan,
        string nodeId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (OperatingSystem.IsBrowser())
        {
            return Task.FromResult(new ProjectStructureRuntimeLaunchResult(
                false,
                "Interactive terminal presentation is unavailable in a browser process."));
        }

        var runtime = executableResolver.Resolve(plan.ExecutableCandidates, plan.WorkingDirectory);
        var terminalCandidates = ResolveTerminalCandidates();
        var terminal = executableResolver.Resolve(terminalCandidates, plan.WorkingDirectory);
        if (!runtime.IsSuccess || runtime.ExecutablePath is null ||
            terminalCandidates.Count == 0 || !terminal.IsSuccess || terminal.ExecutablePath is null)
        {
            return Task.FromResult(new ProjectStructureRuntimeLaunchResult(
                false,
                "Interactive terminal presentation is unavailable on this host."));
        }

        try
        {
            var startInfo = BuildStartInfo(plan, terminal.ExecutablePath, runtime.ExecutablePath);
            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return Task.FromResult(new ProjectStructureRuntimeLaunchResult(
                    false,
                    "The configured terminal did not accept the runtime plan."));
            }

            logger.LogInformation(
                "Opened terminal presentation for Workbench runtime node {NodeId} using plan {PlanKind}.",
                nodeId,
                plan.Kind);
            return Task.FromResult(new ProjectStructureRuntimeLaunchResult(
                true,
                $"Opened {plan.DisplayName} in the configured terminal. The terminal is presentation only; the typed plan remains authoritative."));
        }
        catch (Exception exception) when (
            exception is Win32Exception or InvalidOperationException or NotSupportedException)
        {
            logger.LogWarning(
                exception,
                "Terminal presentation failed for Workbench runtime node {NodeId} using plan {PlanKind}.",
                nodeId,
                plan.Kind);
            return Task.FromResult(new ProjectStructureRuntimeLaunchResult(
                false,
                "The configured terminal could not be opened on this host."));
        }
    }

    internal const string TerminalEnvironmentExplanation =
        "CanDoItAll passed only its allowed environment variables to this terminal (host secrets are removed). " +
        "If a tool needs another variable, the operator can add its name under AgentFramework:ProcessEnvironment:AdditionalInheritedNames.";

    private readonly WorkspaceCommandEnvironmentPolicy environmentPolicy = new();

    private IReadOnlyList<string> ResolveTerminalCandidates()
        => hostContext.Platform switch
        {
            ProjectStructureRuntimeHostPlatform.Windows when options.EnableWindowsTerminal =>
                ["pwsh", "powershell"],
            ProjectStructureRuntimeHostPlatform.Linux when !string.IsNullOrWhiteSpace(options.LinuxTerminalExecutable) =>
                [options.LinuxTerminalExecutable],
            ProjectStructureRuntimeHostPlatform.MacOS when !string.IsNullOrWhiteSpace(options.MacOsTerminalExecutable) =>
                [options.MacOsTerminalExecutable],
            _ => []
        };

    [UnsupportedOSPlatform("browser")]
    internal ProcessStartInfo BuildStartInfo(
        ProjectStructureRuntimeLaunchPlan plan,
        string terminalPath,
        string runtimePath)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = terminalPath,
            WorkingDirectory = plan.WorkingDirectory,
            UseShellExecute = false
        };
        // The terminal gets the same composed environment as a direct launch; host secrets never reach it.
        var environment = ProjectStructureRuntimeEnvironment.Compose(environmentPolicy, plan, runtimePath);
        if (hostContext.Platform == ProjectStructureRuntimeHostPlatform.Windows)
        {
            // ShellExecute cannot pass an environment, so the PowerShell prelude replaces it.
            startInfo.UseShellExecute = true;
            startInfo.ArgumentList.Add("-NoLogo");
            startInfo.ArgumentList.Add("-NoExit");
            startInfo.ArgumentList.Add("-Command");
            startInfo.ArgumentList.Add(BuildPowerShellPresentationCommand(plan, runtimePath, environment));
            return startInfo;
        }

        var prefix = hostContext.Platform == ProjectStructureRuntimeHostPlatform.Linux
            ? options.LinuxTerminalArgumentPrefix
            : options.MacOsTerminalArgumentPrefix;
        foreach (var argument in prefix)
        {
            startInfo.ArgumentList.Add(argument);
        }

        startInfo.ArgumentList.Add("env");
        startInfo.ArgumentList.Add("-i");
        foreach (var pair in environment
                     .Where(pair => pair.Value is not null)
                     .OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            startInfo.ArgumentList.Add($"{pair.Key}={pair.Value}");
        }

        startInfo.ArgumentList.Add(runtimePath);
        foreach (var argument in plan.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        return startInfo;
    }

    internal static string BuildPowerShellPresentationCommand(
        ProjectStructureRuntimeLaunchPlan plan,
        string runtimePath,
        IReadOnlyDictionary<string, string?> environment)
    {
        var allowed = environment
            .Where(pair => pair.Value is not null)
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .ToArray();
        var keepList = string.Join(",", allowed.Select(pair => QuotePowerShell(pair.Key)));
        var commands = new[]
            {
                // Remove every inherited variable outside the composed set (the terminal starts with the
                // host's full environment), then apply the composed values. Remove-Item deletes the variable;
                // SetEnvironmentVariable($name, $null) would leave it defined and empty, because PowerShell
                // passes $null to a string parameter as "".
                $"$cdiaKeep = @({keepList}); Get-ChildItem Env: | Where-Object {{ $cdiaKeep -notcontains $_.Name }} | ForEach-Object {{ Remove-Item -LiteralPath ('Env:' + $_.Name) -ErrorAction SilentlyContinue }}; Remove-Variable cdiaKeep"
            }
            .Concat(allowed.Select(pair =>
                $"[System.Environment]::SetEnvironmentVariable({QuotePowerShell(pair.Key)}, {QuotePowerShell(pair.Value!)})"))
            .Append($"Write-Host {QuotePowerShell(TerminalEnvironmentExplanation)}")
            .Append($"Set-Location -LiteralPath {QuotePowerShell(plan.WorkingDirectory)}")
            .Append(
                string.Join(
                    ' ',
                    new[] { "&", QuotePowerShell(runtimePath) }
                        .Concat(plan.Arguments.Select(QuotePowerShell))));
        return string.Join("; ", commands);
    }

    private static string QuotePowerShell(string value)
        => $"'{value.Replace("'", "''", StringComparison.Ordinal)}'";
}

internal interface IProjectStructureRuntimeElevationAdapter
{
    ProjectStructureRuntimeCapability Probe(ProjectStructureRuntimeLaunchPlan plan);

    Task<ProjectStructureRuntimeLaunchResult> LaunchAsync(
        ProjectStructureRuntimeLaunchPlan plan,
        string nodeId,
        CancellationToken cancellationToken);
}

internal sealed class ProjectStructureRuntimeElevationAdapter(
    ProjectStructureRuntimeHostContext hostContext,
    IProjectStructureExecutableResolver executableResolver,
    ILogger<ProjectStructureRuntimeElevationAdapter> logger) : IProjectStructureRuntimeElevationAdapter
{
    public ProjectStructureRuntimeCapability Probe(ProjectStructureRuntimeLaunchPlan plan)
    {
        if (hostContext.Platform != ProjectStructureRuntimeHostPlatform.Windows)
        {
            return ProjectStructureRuntimeCapability.Unavailable(
                ProjectStructureRuntimeCapabilityStatus.Unsupported,
                "Elevated launch is unavailable by default on Linux and macOS; no sudo, pkexec, or AppleScript fallback is used.");
        }

        if (plan.TerminalOnly || plan.EnvironmentVariables.Count > 0)
        {
            return ProjectStructureRuntimeCapability.Unavailable(
                ProjectStructureRuntimeCapabilityStatus.PolicyDenied,
                "This runtime plan cannot be elevated without changing its typed environment or interaction contract.");
        }

        var resolution = executableResolver.Resolve(plan.ExecutableCandidates, plan.WorkingDirectory);
        return resolution.IsSuccess
            ? ProjectStructureRuntimeCapability.Available("Explicit Windows runas launch is available.")
            : ProjectStructureRuntimeCapability.Unavailable(
                ProjectStructureRuntimeCapabilityStatus.DependencyMissing,
                resolution.Message);
    }

    public Task<ProjectStructureRuntimeLaunchResult> LaunchAsync(
        ProjectStructureRuntimeLaunchPlan plan,
        string nodeId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (OperatingSystem.IsBrowser())
        {
            return Task.FromResult(new ProjectStructureRuntimeLaunchResult(
                false,
                "Elevated launch is unavailable in a browser process."));
        }

        var capability = Probe(plan);
        var resolution = executableResolver.Resolve(plan.ExecutableCandidates, plan.WorkingDirectory);
        if (!capability.IsAvailable || !resolution.IsSuccess || resolution.ExecutablePath is null)
        {
            return Task.FromResult(new ProjectStructureRuntimeLaunchResult(false, capability.Message));
        }

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = resolution.ExecutablePath,
                WorkingDirectory = plan.WorkingDirectory,
                UseShellExecute = true,
                Verb = "runas"
            };
            foreach (var argument in plan.Arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return Task.FromResult(new ProjectStructureRuntimeLaunchResult(
                    false,
                    "Windows did not accept the elevated runtime launch."));
            }

            logger.LogInformation(
                "Started explicitly elevated Workbench runtime node {NodeId} using plan {PlanKind}.",
                nodeId,
                plan.Kind);
            return Task.FromResult(new ProjectStructureRuntimeLaunchResult(
                true,
                $"Started {plan.DisplayName} through the explicitly authorized Windows runas capability."));
        }
        catch (Win32Exception exception) when (exception.NativeErrorCode == 1223)
        {
            return Task.FromResult(new ProjectStructureRuntimeLaunchResult(
                false,
                "The elevated runtime launch was canceled."));
        }
        catch (Exception exception) when (
            exception is Win32Exception or InvalidOperationException or NotSupportedException)
        {
            logger.LogWarning(
                exception,
                "Elevated launch failed for Workbench runtime node {NodeId} using plan {PlanKind}.",
                nodeId,
                plan.Kind);
            return Task.FromResult(new ProjectStructureRuntimeLaunchResult(
                false,
                "The explicitly authorized Windows runas launch failed."));
        }
    }
}
