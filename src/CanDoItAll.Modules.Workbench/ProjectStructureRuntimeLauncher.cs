using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.Versioning;
using System.Text;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.Workbench;

public interface IProjectStructureRuntimeLauncher
{
    bool IsAvailable { get; }

    ProjectStructureRuntimeLaunchResolution Resolve(ProjectStructureNode? node);

    Task<ProjectStructureRuntimeLaunchResult> LaunchAsync(ProjectStructureNode node, bool runAsAdministrator, CancellationToken cancellationToken = default);
}

public sealed record ProjectStructureRuntimeLaunchTarget(string Description, string Path, bool IsDirectory);

public sealed record ProjectStructureRuntimeLaunchPlan(
    string WorkingDirectory,
    string StartupScript,
    string DisplayCommand,
    string DisplayName,
    ProjectStructureRuntimeLaunchTarget? Target);

public sealed record ProjectStructureRuntimeLaunchResolution(ProjectStructureRuntimeLaunchPlan? Plan, string Message)
{
    public bool IsSuccess => Plan is not null;
}

public sealed record ProjectStructureRuntimeLaunchResult(bool IsSuccess, string Message);

public sealed class ProjectStructureRuntimeLauncher(
    IWorkspacePathAccessGuard workspacePathAccessGuard,
    ILogger<ProjectStructureRuntimeLauncher> logger) : IProjectStructureRuntimeLauncher
{
    public bool IsAvailable => OperatingSystem.IsWindows();

    public ProjectStructureRuntimeLaunchResolution Resolve(ProjectStructureNode? node)
    {
        if (node is null)
        {
            return Fail("Select a runtime node first.");
        }

        var metadata = ProjectObjectMetadataSerializer.Parse(node.MetadataJson);
        return node.ObjectType switch
        {
            ProjectObjectType.Script => ResolveScriptPlan(metadata.Script),
            ProjectObjectType.Environment => ResolveEnvironmentPlan(metadata.Environment),
            _ => Fail("PowerShell launch is only available for runtime-capable nodes.")
        };
    }

    public Task<ProjectStructureRuntimeLaunchResult> LaunchAsync(ProjectStructureNode node, bool runAsAdministrator, CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsWindows())
        {
            return Task.FromResult(new ProjectStructureRuntimeLaunchResult(false, "PowerShell launch is not available on this host."));
        }

        var resolution = Resolve(node);
        if (!resolution.IsSuccess || resolution.Plan is null)
        {
            return Task.FromResult(new ProjectStructureRuntimeLaunchResult(false, resolution.Message));
        }

        var plan = resolution.Plan;
        if (!Directory.Exists(plan.WorkingDirectory))
        {
            return Task.FromResult(new ProjectStructureRuntimeLaunchResult(false, "The configured working directory is no longer available on disk."));
        }

        if (plan.Target is not null && !TargetExists(plan.Target))
        {
            return Task.FromResult(new ProjectStructureRuntimeLaunchResult(false, $"The configured {plan.Target.Description} is no longer available on disk."));
        }

        try
        {
            using var process = StartPowerShell(plan, runAsAdministrator);
            logger.LogInformation(
                "Launched runtime PowerShell for node {NodeId} in {WorkingDirectory} using plan {DisplayName}.",
                node.Id,
                plan.WorkingDirectory,
                plan.DisplayName);

            var prefix = runAsAdministrator ? "Opened elevated PowerShell" : "Opened PowerShell";
            return Task.FromResult(new ProjectStructureRuntimeLaunchResult(true, $"{prefix} and started {plan.DisplayName}."));
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            var prefix = runAsAdministrator ? "The elevated PowerShell launch" : "The PowerShell launch";
            return Task.FromResult(new ProjectStructureRuntimeLaunchResult(false, $"{prefix} was canceled."));
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Failed to launch runtime PowerShell for node {NodeId} in {WorkingDirectory} using plan {DisplayName}.",
                node.Id,
                plan.WorkingDirectory,
                plan.DisplayName);
            return Task.FromResult(new ProjectStructureRuntimeLaunchResult(false, "PowerShell could not be launched on this host."));
        }
    }

    [SupportedOSPlatform("windows")]
    private static Process? StartPowerShell(ProjectStructureRuntimeLaunchPlan plan, bool runAsAdministrator)
        => Process.Start(BuildStartInfo(plan, runAsAdministrator));

    private ProjectStructureRuntimeLaunchResolution ResolveScriptPlan(ProjectScriptMetadata? metadata)
    {
        if (metadata is null)
        {
            return Fail("Script metadata is missing.");
        }

        if (string.IsNullOrWhiteSpace(metadata.WorkingDirectory))
        {
            return Fail("Script launch requires a working directory.");
        }

        if (TryResolveWorkspacePath(metadata.WorkingDirectory, "Script working directory", out var workingDirectory) is { } workingDirectoryFailure)
        {
            return workingDirectoryFailure;
        }

        ProjectStructureRuntimeLaunchTarget? target = null;
        string? displayCommand = null;
        string displayName;

        if (!string.IsNullOrWhiteSpace(metadata.Command))
        {
            displayCommand = JoinCommand(metadata.Command, metadata.Arguments);
            displayName = metadata.ScriptKind switch
            {
                ProjectScriptKind.EfMigration => "EF migration",
                ProjectScriptKind.TailwindWatch => "Tailwind watch",
                ProjectScriptKind.PowerShell => "PowerShell script",
                _ => "script command"
            };
        }
        else if (metadata.ScriptKind == ProjectScriptKind.PowerShell && !string.IsNullOrWhiteSpace(metadata.ScriptPath))
        {
            if (TryResolveWorkspacePath(metadata.ScriptPath, "Script path", out var scriptPath, workingDirectory) is { } scriptPathFailure)
            {
                return scriptPathFailure;
            }

            displayCommand = JoinCommand($"& {QuotePowerShell(scriptPath)}", metadata.Arguments);
            displayName = "PowerShell script";
            target = new ProjectStructureRuntimeLaunchTarget("script path", scriptPath, false);
        }
        else
        {
            return Fail("Script launch requires a command or PowerShell script path.");
        }

        return Success(CreatePlan(workingDirectory, displayCommand, displayName, target));
    }

    private ProjectStructureRuntimeLaunchResolution ResolveEnvironmentPlan(ProjectEnvironmentMetadata? metadata)
    {
        if (metadata is null)
        {
            return Fail("Environment metadata is missing.");
        }

        return metadata.EnvironmentKind switch
        {
            ProjectEnvironmentKind.DotNetWatch => ResolveDotNetPlan(metadata, "dotnet watch", isRelease: false, isWatch: true),
            ProjectEnvironmentKind.DotNetRuntime => ResolveDotNetPlan(metadata, ".NET runtime", isRelease: false, isWatch: false),
            ProjectEnvironmentKind.DotNetRelease => ResolveDotNetPlan(metadata, "release run", isRelease: true, isWatch: false),
            ProjectEnvironmentKind.PythonEnvironment => ResolvePythonPlan(metadata),
            _ => Fail("PowerShell launch is not supported for this environment type.")
        };
    }

    private ProjectStructureRuntimeLaunchResolution ResolveDotNetPlan(ProjectEnvironmentMetadata metadata, string displayName, bool isRelease, bool isWatch)
    {
        if (string.IsNullOrWhiteSpace(metadata.ProjectPath))
        {
            return Fail("Runtime launch requires a project path.");
        }

        if (TryResolveWorkspacePath(metadata.ProjectPath, "Project path", out var projectPath) is { } projectPathFailure)
        {
            return projectPathFailure;
        }

        var workingDirectory = ResolveWorkingDirectoryFromProjectPath(projectPath);
        var commandParts = new List<string> { "dotnet" };
        if (isWatch)
        {
            commandParts.Add("watch");
            commandParts.Add("--project");
            commandParts.Add(QuotePowerShell(projectPath));
            commandParts.Add("run");
        }
        else
        {
            commandParts.Add("run");
            commandParts.Add("--project");
            commandParts.Add(QuotePowerShell(projectPath));
        }

        if (isRelease)
        {
            commandParts.Add("-c");
            commandParts.Add("Release");
        }

        var startupLines = new List<string>();
        if (!string.IsNullOrWhiteSpace(metadata.LocalhostUrl))
        {
            startupLines.Add($"$env:ASPNETCORE_URLS = {QuotePowerShell(metadata.LocalhostUrl.Trim())}");
            commandParts.Add("--no-launch-profile");
        }
        else if (!string.IsNullOrWhiteSpace(metadata.LaunchProfileName))
        {
            commandParts.Add("--launch-profile");
            commandParts.Add(QuotePowerShell(metadata.LaunchProfileName.Trim()));
        }

        return Success(
            CreatePlan(
                workingDirectory,
                string.Join(' ', commandParts),
                displayName,
                new ProjectStructureRuntimeLaunchTarget("project path", projectPath, IsDirectoryTarget(projectPath)),
                startupLines));
    }

    private ProjectStructureRuntimeLaunchResolution ResolvePythonPlan(ProjectEnvironmentMetadata metadata)
    {
        if (string.IsNullOrWhiteSpace(metadata.ProjectPath))
        {
            return Fail("Python environment launch requires a project path.");
        }

        if (string.IsNullOrWhiteSpace(metadata.EnvironmentName))
        {
            return Fail("Python environment launch requires an environment name.");
        }

        if (!metadata.PythonProvider.HasValue)
        {
            return Fail("Python environment launch requires a provider.");
        }

        if (TryResolveWorkspacePath(metadata.ProjectPath, "Python project path", out var projectPath) is { } projectPathFailure)
        {
            return projectPathFailure;
        }

        var workingDirectory = ResolveWorkingDirectoryFromProjectPath(projectPath);
        return metadata.PythonProvider.Value switch
        {
            ProjectPythonProvider.Conda => Success(
                CreatePlan(
                    workingDirectory,
                    $"conda activate {QuotePowerShell(metadata.EnvironmentName.Trim())}",
                    "Conda environment",
                    null)),
            _ => ResolvePythonVirtualEnvironmentPlan(metadata.EnvironmentName, workingDirectory)
        };
    }

    private ProjectStructureRuntimeLaunchResolution ResolvePythonVirtualEnvironmentPlan(string environmentName, string workingDirectory)
    {
        string activationPath;
        if (environmentName.Trim().EndsWith(".ps1", StringComparison.OrdinalIgnoreCase))
        {
            if (TryResolveWorkspacePath(environmentName, "Python activation script path", out activationPath, workingDirectory) is { } activationFailure)
            {
                return activationFailure;
            }
        }
        else
        {
            if (TryResolveWorkspacePath(environmentName, "Python environment path", out var environmentPath, workingDirectory) is { } environmentFailure)
            {
                return environmentFailure;
            }

            activationPath = Path.Combine(environmentPath, "Scripts", "Activate.ps1");
        }

        return Success(
            CreatePlan(
                workingDirectory,
                $"& {QuotePowerShell(activationPath)}",
                "Python environment",
                new ProjectStructureRuntimeLaunchTarget("Python environment path", activationPath, false)));
    }

    [SupportedOSPlatform("windows")]
    private static ProcessStartInfo BuildStartInfo(ProjectStructureRuntimeLaunchPlan plan, bool runAsAdministrator)
        => new()
        {
            FileName = "powershell.exe",
            Arguments = BuildArguments(plan.StartupScript),
            UseShellExecute = true,
            Verb = runAsAdministrator ? "runas" : string.Empty,
            WorkingDirectory = plan.WorkingDirectory
        };

    private ProjectStructureRuntimeLaunchPlan CreatePlan(
        string workingDirectory,
        string displayCommand,
        string displayName,
        ProjectStructureRuntimeLaunchTarget? target,
        IEnumerable<string>? startupLines = null)
    {
        var lines = new List<string>
        {
            $"Set-Location -LiteralPath {QuotePowerShell(workingDirectory)}"
        };

        if (startupLines is not null)
        {
            lines.AddRange(startupLines);
        }

        lines.Add(displayCommand);
        return new ProjectStructureRuntimeLaunchPlan(
            workingDirectory,
            string.Join(Environment.NewLine, lines),
            displayCommand,
            displayName,
            target);
    }

    private ProjectStructureRuntimeLaunchResolution? TryResolveWorkspacePath(
        string value,
        string description,
        out string resolvedPath,
        string? basePath = null)
    {
        var resolution = workspacePathAccessGuard.ResolveWorkspacePath(value, basePath);
        if (resolution.IsSuccess)
        {
            resolvedPath = resolution.FullPath;
            return null;
        }

        resolvedPath = string.Empty;
        return Fail($"{description} must stay inside the active workspace root.");
    }

    private static string ResolveWorkingDirectoryFromProjectPath(string projectPath)
    {
        if (LooksLikeProjectFile(projectPath))
        {
            return Path.GetDirectoryName(projectPath) ?? projectPath;
        }

        return projectPath;
    }

    private static bool LooksLikeProjectFile(string path)
        => path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase) ||
           path.EndsWith(".fsproj", StringComparison.OrdinalIgnoreCase) ||
           path.EndsWith(".vbproj", StringComparison.OrdinalIgnoreCase) ||
           path.EndsWith(".sln", StringComparison.OrdinalIgnoreCase) ||
           path.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase);

    private static bool IsDirectoryTarget(string path)
        => !LooksLikeProjectFile(path);

    private static bool TargetExists(ProjectStructureRuntimeLaunchTarget target)
        => target.IsDirectory ? Directory.Exists(target.Path) : File.Exists(target.Path);

    private static string JoinCommand(string command, string? arguments)
        => string.IsNullOrWhiteSpace(arguments)
            ? command.Trim()
            : $"{command.Trim()} {arguments.Trim()}";

    private static string QuotePowerShell(string value)
        => $"'{value.Replace("'", "''", StringComparison.Ordinal)}'";

    private static string BuildArguments(string startupScript)
    {
        var encodedCommand = Convert.ToBase64String(Encoding.Unicode.GetBytes(startupScript));
        return $"-NoLogo -NoExit -ExecutionPolicy Bypass -EncodedCommand {encodedCommand}";
    }

    private static ProjectStructureRuntimeLaunchResolution Success(ProjectStructureRuntimeLaunchPlan plan)
        => new(plan, "Launch plan resolved.");

    private static ProjectStructureRuntimeLaunchResolution Fail(string message)
        => new(null, message);
}
