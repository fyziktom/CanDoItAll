using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.Versioning;
using System.Text;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.Workbench;

public interface IProjectStructureRuntimeLauncher
{
    bool IsAvailable { get; }

    ProjectStructureRuntimeLaunchResolution Resolve(ProjectStructureNode? node);

    ProjectStructureRuntimeLaunchResolution Resolve(
        ProjectObjectType objectType,
        string? objectSubtype,
        string? notes,
        string metadataJson,
        ProjectStructureRuntimePathAuthorityMode pathAuthorityMode);

    Task<ProjectStructureRuntimeLaunchResult> LaunchAsync(ProjectStructureNode node, bool runAsAdministrator, CancellationToken cancellationToken = default);
}

public enum ProjectStructureRuntimePathAuthorityMode
{
    OperatorSelected,
    AgentExecution
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
    ILogger<ProjectStructureRuntimeLauncher> logger,
    IProjectStructureDotNetProjectTargetResolver dotNetProjectTargetResolver) : IProjectStructureRuntimeLauncher
{
    public bool IsAvailable => OperatingSystem.IsWindows();

    public ProjectStructureRuntimeLaunchResolution Resolve(ProjectStructureNode? node)
    {
        if (node is null)
        {
            return Fail("Select a runtime node first.");
        }

        return Resolve(
            node.ObjectType,
            node.ObjectSubtype,
            node.Notes,
            node.MetadataJson,
            ProjectStructureRuntimePathAuthorityMode.OperatorSelected);
    }

    public ProjectStructureRuntimeLaunchResolution Resolve(
        ProjectObjectType objectType,
        string? objectSubtype,
        string? notes,
        string metadataJson,
        ProjectStructureRuntimePathAuthorityMode pathAuthorityMode)
    {
        ProjectObjectMetadataEnvelope metadata;
        try
        {
            metadata = ProjectObjectMetadataSerializer.Parse(metadataJson);
        }
        catch (InvalidOperationException)
        {
            return Fail("Runtime metadata is invalid and must be repaired before this node can be launched.");
        }

        if (!ProjectStructureRuntimeNodeKindPolicy.TryValidateAndApply(
                objectType,
                objectSubtype,
                metadataJson,
                metadata,
                out var kindValidationMessage))
        {
            return Fail(kindValidationMessage);
        }

        ProjectStructureDotNetRuntimeMetadataHydrator.Hydrate(objectType, objectSubtype, notes, metadata);
        return objectType switch
        {
            ProjectObjectType.Script => ResolveScriptPlan(
                objectSubtype ?? string.Empty,
                metadata.Script,
                pathAuthorityMode),
            ProjectObjectType.Environment => ResolveEnvironmentPlan(
                objectSubtype ?? string.Empty,
                metadata.Environment,
                pathAuthorityMode),
            ProjectObjectType.Infrastructure => ResolveInfrastructurePlan(
                objectSubtype ?? string.Empty,
                metadata.Infrastructure,
                pathAuthorityMode),
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
            if (process is null)
            {
                return Task.FromResult(new ProjectStructureRuntimeLaunchResult(false, "PowerShell did not accept the runtime command."));
            }

            logger.LogInformation(
                "Launched runtime PowerShell for node {NodeId} in {WorkingDirectory} using plan {DisplayName}.",
                node.Id,
                plan.WorkingDirectory,
                plan.DisplayName);

            var prefix = runAsAdministrator ? "Opened elevated PowerShell" : "Opened PowerShell";
            return Task.FromResult(new ProjectStructureRuntimeLaunchResult(
                true,
                $"{prefix} and handed off the {plan.DisplayName} command. Verify the terminal output to confirm that the application started successfully."));
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

    private ProjectStructureRuntimeLaunchResolution ResolveScriptPlan(
        string objectSubtype,
        ProjectScriptMetadata? metadata,
        ProjectStructureRuntimePathAuthorityMode pathAuthorityMode)
    {
        metadata ??= new ProjectScriptMetadata();
        var scriptKind = ResolveScriptKind(objectSubtype, metadata);

        ProjectStructureRuntimeLaunchTarget? target = null;
        string? displayCommand = null;
        string displayName;

        if (!string.IsNullOrWhiteSpace(metadata.Command))
        {
            if (ProjectStructureDirectDotNetCommandPolicy.TryClassify(
                    metadata.Command,
                    metadata.Arguments,
                    out _))
            {
                return Fail(ProjectStructureDirectDotNetCommandPolicy.TypedEnvironmentRequiredMessage);
            }

            if (TryResolveScriptWorkingDirectory(
                    metadata,
                    pathAuthorityMode,
                    out var workingDirectory) is { } workingDirectoryFailure)
            {
                return workingDirectoryFailure;
            }

            displayCommand = JoinCommand(metadata.Command, metadata.Arguments);
            displayName = scriptKind switch
            {
                ProjectScriptKind.EfMigration => "EF migration",
                ProjectScriptKind.TailwindWatch => "Tailwind watch",
                ProjectScriptKind.PowerShell => "PowerShell script",
                _ => "script command"
            };

            return Success(CreatePlan(workingDirectory, displayCommand, displayName, target));
        }

        if (scriptKind == ProjectScriptKind.PowerShell && !string.IsNullOrWhiteSpace(metadata.ScriptPath))
        {
            if (TryResolvePowerShellScriptPath(
                    metadata,
                    pathAuthorityMode,
                    out var scriptPath,
                    out var workingDirectory) is { } scriptPathFailure)
            {
                return scriptPathFailure;
            }

            displayCommand = JoinCommand($"& {QuotePowerShell(scriptPath)}", metadata.Arguments);
            displayName = "PowerShell script";
            target = new ProjectStructureRuntimeLaunchTarget("script path", scriptPath, false);

            return Success(CreatePlan(workingDirectory, displayCommand, displayName, target));
        }

        return Fail("Script launch requires a command or PowerShell script path.");
    }

    private ProjectStructureRuntimeLaunchResolution ResolveEnvironmentPlan(
        string objectSubtype,
        ProjectEnvironmentMetadata? metadata,
        ProjectStructureRuntimePathAuthorityMode pathAuthorityMode)
    {
        metadata ??= new ProjectEnvironmentMetadata();
        var environmentKind = ResolveEnvironmentKind(objectSubtype, metadata);

        return environmentKind switch
        {
            ProjectEnvironmentKind.DotNetWatch => ResolveDotNetPlan(
                metadata,
                "dotnet watch",
                isRelease: false,
                isWatch: true,
                pathAuthorityMode: pathAuthorityMode),
            ProjectEnvironmentKind.DotNetRuntime => ResolveDotNetPlan(
                metadata,
                ".NET runtime",
                isRelease: false,
                isWatch: false,
                pathAuthorityMode: pathAuthorityMode),
            ProjectEnvironmentKind.DotNetRelease => ResolveDotNetPlan(
                metadata,
                "release run",
                isRelease: true,
                isWatch: false,
                pathAuthorityMode: pathAuthorityMode),
            ProjectEnvironmentKind.PythonEnvironment => ResolvePythonPlan(metadata, pathAuthorityMode),
            _ => Fail("PowerShell launch is not supported for this environment type.")
        };
    }

    private ProjectStructureRuntimeLaunchResolution ResolveInfrastructurePlan(
        string objectSubtype,
        ProjectInfrastructureMetadata? metadata,
        ProjectStructureRuntimePathAuthorityMode pathAuthorityMode)
    {
        metadata ??= new ProjectInfrastructureMetadata();
        var infrastructureKind = ResolveInfrastructureKind(objectSubtype, metadata);

        return infrastructureKind switch
        {
            ProjectInfrastructureKind.DockerMode => ResolveDockerPlan(metadata, pathAuthorityMode),
            _ => Fail("PowerShell launch is not supported for this infrastructure type.")
        };
    }

    private ProjectStructureRuntimeLaunchResolution ResolveDockerPlan(
        ProjectInfrastructureMetadata metadata,
        ProjectStructureRuntimePathAuthorityMode pathAuthorityMode)
    {
        if (string.IsNullOrWhiteSpace(metadata.RuntimeCommand))
        {
            return Fail("Docker runtime launch requires a runtime command.");
        }

        var workingDirectoryValue = FirstNonEmpty(metadata.WorkingDirectory, metadata.FolderPath, ".");
        if (TryResolveWorkingDirectory(
                workingDirectoryValue,
                "Docker working directory",
                pathAuthorityMode,
                out var workingDirectory) is { } workingDirectoryFailure)
        {
            return workingDirectoryFailure;
        }

        var displayCommand = JoinCommand(metadata.RuntimeCommand, metadata.RuntimeArguments);
        return Success(
            CreatePlan(
                workingDirectory,
                displayCommand,
                "Docker runtime",
                new ProjectStructureRuntimeLaunchTarget("Docker working directory", workingDirectory, true)));
    }

    private ProjectStructureRuntimeLaunchResolution? TryResolveScriptWorkingDirectory(
        ProjectScriptMetadata metadata,
        ProjectStructureRuntimePathAuthorityMode pathAuthorityMode,
        out string workingDirectory)
    {
        var workingDirectoryValue = string.IsNullOrWhiteSpace(metadata.WorkingDirectory)
            ? "."
            : metadata.WorkingDirectory;
        return TryResolveWorkingDirectory(
            workingDirectoryValue,
            "Script working directory",
            pathAuthorityMode,
            out workingDirectory);
    }

    private ProjectStructureRuntimeLaunchResolution? TryResolvePowerShellScriptPath(
        ProjectScriptMetadata metadata,
        ProjectStructureRuntimePathAuthorityMode pathAuthorityMode,
        out string scriptPath,
        out string workingDirectory)
    {
        if (!string.IsNullOrWhiteSpace(metadata.WorkingDirectory))
        {
            if (TryResolveWorkingDirectory(
                    metadata.WorkingDirectory,
                    "Script working directory",
                    pathAuthorityMode,
                    out workingDirectory) is { } workingDirectoryFailure)
            {
                scriptPath = string.Empty;
                return workingDirectoryFailure;
            }

            return TryResolveWorkspacePath(
                metadata.ScriptPath,
                "Script path",
                pathAuthorityMode,
                out scriptPath,
                workingDirectory);
        }

        if (TryResolveWorkspacePath(
                metadata.ScriptPath,
                "Script path",
                pathAuthorityMode,
                out scriptPath) is { } scriptPathFailure)
        {
            workingDirectory = string.Empty;
            return scriptPathFailure;
        }

        workingDirectory = Path.GetDirectoryName(scriptPath) ?? scriptPath;
        return null;
    }

    private ProjectStructureRuntimeLaunchResolution ResolveDotNetPlan(
        ProjectEnvironmentMetadata metadata,
        string displayName,
        bool isRelease,
        bool isWatch,
        ProjectStructureRuntimePathAuthorityMode pathAuthorityMode)
    {
        if (string.IsNullOrWhiteSpace(metadata.ProjectPath))
        {
            return Fail("Runtime launch requires a project path.");
        }

        string? resolvedWorkingDirectory = null;
        if (!string.IsNullOrWhiteSpace(metadata.WorkingDirectory))
        {
            if (TryResolveWorkingDirectory(
                    metadata.WorkingDirectory,
                    "Runtime working directory",
                    pathAuthorityMode,
                    out var explicitWorkingDirectory) is { } workingDirectoryFailure)
            {
                return workingDirectoryFailure;
            }

            resolvedWorkingDirectory = explicitWorkingDirectory;
        }

        if (TryResolveWorkspacePath(
                metadata.ProjectPath,
                "Project path",
                pathAuthorityMode,
                out var projectPath,
                resolvedWorkingDirectory) is { } projectPathFailure)
        {
            return projectPathFailure;
        }

        var projectTarget = dotNetProjectTargetResolver.Resolve(projectPath);
        if (!projectTarget.IsSuccess || string.IsNullOrWhiteSpace(projectTarget.ProjectFilePath))
        {
            return Fail(projectTarget.Message);
        }

        var projectFilePath = projectTarget.ProjectFilePath;
        var workingDirectory = resolvedWorkingDirectory ?? Path.GetDirectoryName(projectFilePath) ?? projectFilePath;
        var commandParts = new List<string> { "dotnet" };
        if (isWatch)
        {
            commandParts.Add("watch");
            commandParts.Add("--project");
            commandParts.Add(QuotePowerShell(projectFilePath));
            commandParts.Add("run");
        }
        else
        {
            commandParts.Add("run");
            commandParts.Add("--project");
            commandParts.Add(QuotePowerShell(projectFilePath));
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
                new ProjectStructureRuntimeLaunchTarget("project file", projectFilePath, false),
                startupLines));
    }

    private ProjectStructureRuntimeLaunchResolution ResolvePythonPlan(
        ProjectEnvironmentMetadata metadata,
        ProjectStructureRuntimePathAuthorityMode pathAuthorityMode)
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

        if (TryResolveWorkspacePath(
                metadata.ProjectPath,
                "Python project path",
                pathAuthorityMode,
                out var projectPath) is { } projectPathFailure)
        {
            return projectPathFailure;
        }

        var projectPathIsDirectory = Directory.Exists(projectPath);
        var projectPathIsFile = File.Exists(projectPath);
        if (!projectPathIsDirectory && !projectPathIsFile)
        {
            return Fail("Python project path does not exist or is not accessible.");
        }

        var workingDirectory = projectPathIsDirectory
            ? projectPath
            : Path.GetDirectoryName(projectPath) ?? projectPath;
        var projectTarget = new ProjectStructureRuntimeLaunchTarget(
            "Python project path",
            projectPath,
            projectPathIsDirectory);
        return metadata.PythonProvider.Value switch
        {
            ProjectPythonProvider.Conda => Success(
                CreatePlan(
                    workingDirectory,
                    $"conda activate {QuotePowerShell(metadata.EnvironmentName.Trim())}",
                    "Conda environment",
                    projectTarget)),
            _ => ResolvePythonVirtualEnvironmentPlan(
                metadata.EnvironmentName,
                workingDirectory,
                pathAuthorityMode)
        };
    }

    private ProjectStructureRuntimeLaunchResolution ResolvePythonVirtualEnvironmentPlan(
        string environmentName,
        string workingDirectory,
        ProjectStructureRuntimePathAuthorityMode pathAuthorityMode)
    {
        string activationPath;
        if (environmentName.Trim().EndsWith(".ps1", StringComparison.OrdinalIgnoreCase))
        {
            if (TryResolveWorkspacePath(
                    environmentName,
                    "Python activation script path",
                    pathAuthorityMode,
                    out activationPath,
                    workingDirectory) is { } activationFailure)
            {
                return activationFailure;
            }
        }
        else
        {
            if (TryResolveWorkspacePath(
                    environmentName,
                    "Python environment path",
                    pathAuthorityMode,
                    out var environmentPath,
                    workingDirectory) is { } environmentFailure)
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

    private static ProjectScriptKind ResolveScriptKind(string objectSubtype, ProjectScriptMetadata metadata)
        => metadata.ScriptKind == default && !string.IsNullOrWhiteSpace(objectSubtype)
            ? ProjectNodeKindRegistry.ResolveScriptKind(objectSubtype)
            : metadata.ScriptKind;

    private static ProjectEnvironmentKind ResolveEnvironmentKind(string objectSubtype, ProjectEnvironmentMetadata metadata)
        => metadata.EnvironmentKind == default && !string.IsNullOrWhiteSpace(objectSubtype)
            ? ProjectNodeKindRegistry.ResolveEnvironmentKind(objectSubtype)
            : metadata.EnvironmentKind;

    private static ProjectInfrastructureKind ResolveInfrastructureKind(string objectSubtype, ProjectInfrastructureMetadata metadata)
        => metadata.InfrastructureKind == default && !string.IsNullOrWhiteSpace(objectSubtype)
            ? ProjectNodeKindRegistry.ResolveInfrastructureKind(objectSubtype)
            : metadata.InfrastructureKind;

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
        ProjectStructureRuntimePathAuthorityMode pathAuthorityMode,
        out string resolvedPath,
        string? basePath = null)
    {
        var resolution = workspacePathAccessGuard.ResolveWorkspacePath(value, basePath);
        if (resolution.IsSuccess)
        {
            if (TryResolveReparseSafePath(
                    resolution.FullPath,
                    out resolvedPath,
                    out var reparseFailureMessage))
            {
                return null;
            }

            return Fail($"{description} {reparseFailureMessage}");
        }

        if (TryResolveExistingLocalDrivePath(
                value,
                basePath,
                pathAuthorityMode,
                out var localPath,
                out var localFailureMessage))
        {
            resolvedPath = localPath;
            return null;
        }

        resolvedPath = string.Empty;
        if (!string.IsNullOrWhiteSpace(localFailureMessage))
        {
            return Fail($"{description} {localFailureMessage}");
        }

        return Fail($"{description} must stay inside the active workspace root.");
    }

    private ProjectStructureRuntimeLaunchResolution? TryResolveWorkingDirectory(
        string value,
        string description,
        ProjectStructureRuntimePathAuthorityMode pathAuthorityMode,
        out string workingDirectory,
        string? basePath = null)
    {
        if (TryResolveWorkspacePath(
                value,
                description,
                pathAuthorityMode,
                out workingDirectory,
                basePath) is { } resolutionFailure)
        {
            return resolutionFailure;
        }

        if (Directory.Exists(workingDirectory))
        {
            return null;
        }

        return File.Exists(workingDirectory)
            ? Fail($"{description} must be a directory, but the configured path is a file.")
            : Fail($"{description} does not exist or is not accessible.");
    }

    private bool TryResolveExistingLocalDrivePath(
        string value,
        string? basePath,
        ProjectStructureRuntimePathAuthorityMode pathAuthorityMode,
        out string resolvedPath,
        out string failureMessage)
    {
        resolvedPath = string.Empty;
        failureMessage = string.Empty;
        if (string.IsNullOrWhiteSpace(value) || LooksLikeUrl(value))
        {
            return false;
        }

        try
        {
            var trimmedValue = value.Trim();
            string candidatePath;
            if (Path.IsPathRooted(trimmedValue))
            {
                candidatePath = Path.GetFullPath(trimmedValue);
            }
            else if (!string.IsNullOrWhiteSpace(basePath))
            {
                candidatePath = Path.GetFullPath(Path.Combine(basePath, trimmedValue));
            }
            else
            {
                return false;
            }

            if (!CanCurrentExecutionReadPath(candidatePath, pathAuthorityMode))
            {
                failureMessage = pathAuthorityMode == ProjectStructureRuntimePathAuthorityMode.AgentExecution
                    ? "is outside the active workspace and is not authorized for this agent execution."
                    : "is outside the active workspace and is not authorized for the current execution.";
                return false;
            }

            if (!TryResolveReparseSafePath(
                    candidatePath,
                    out candidatePath,
                    out var reparseFailureMessage))
            {
                failureMessage = reparseFailureMessage;
                return false;
            }

            if (!Directory.Exists(candidatePath) && !File.Exists(candidatePath))
            {
                failureMessage = "does not exist or is not accessible.";
                return false;
            }

            resolvedPath = candidatePath;
            return true;
        }
        catch (Exception exception) when (
            exception is ArgumentException or IOException or NotSupportedException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static bool TryResolveReparseSafePath(
        string path,
        out string resolvedPath,
        out string failureMessage)
    {
        try
        {
            resolvedPath = FileSystemStoragePathPolicy.ResolveReparseSafeFullPath(path);
            failureMessage = string.Empty;
            return true;
        }
        catch (StorageBrowseException)
        {
            resolvedPath = string.Empty;
            failureMessage = "cannot traverse symbolic links or filesystem reparse points.";
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            resolvedPath = string.Empty;
            failureMessage = "is not accessible to the current process.";
            return false;
        }
        catch (Exception exception) when (
            exception is ArgumentException or IOException or NotSupportedException)
        {
            resolvedPath = string.Empty;
            failureMessage = "could not be inspected safely.";
            return false;
        }
    }

    private bool CanCurrentExecutionReadPath(
        string candidatePath,
        ProjectStructureRuntimePathAuthorityMode pathAuthorityMode)
    {
        if (workspacePathAccessGuard.ResolveWorkspacePath(candidatePath).IsSuccess)
        {
            return true;
        }

        if (WorkspaceExecutionAuditContext.Current is not { } auditScope)
        {
            return pathAuthorityMode == ProjectStructureRuntimePathAuthorityMode.OperatorSelected;
        }

        return new EffectiveExternalTargetAccessScope(
                auditScope.AllowedExternalTargetAliases,
                auditScope.ReadOnlyExternalTargetAliases)
            .CanRead(candidatePath);
    }

    private static bool TargetExists(ProjectStructureRuntimeLaunchTarget target)
        => target.IsDirectory ? Directory.Exists(target.Path) : File.Exists(target.Path);

    private static string JoinCommand(string command, string? arguments)
        => string.IsNullOrWhiteSpace(arguments)
            ? command.Trim()
            : $"{command.Trim()} {arguments.Trim()}";

    private static string FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return string.Empty;
    }

    private static bool LooksLikeUrl(string value)
    {
        var trimmedValue = value.Trim();
        if (Path.IsPathRooted(trimmedValue))
        {
            return false;
        }

        return Uri.TryCreate(trimmedValue, UriKind.Absolute, out var uri) &&
               !string.IsNullOrWhiteSpace(uri.Scheme) &&
               !string.Equals(uri.Scheme, Uri.UriSchemeFile, StringComparison.OrdinalIgnoreCase);
    }

    private static string QuotePowerShell(string value)
        => $"'{value.Replace("'", "''", StringComparison.Ordinal)}'";

    private static string BuildArguments(string startupScript)
    {
        var encodedCommand = Convert.ToBase64String(Encoding.Unicode.GetBytes(startupScript));
        return $"-NoLogo -NoExit -ExecutionPolicy Bypass -EncodedCommand {encodedCommand}";
    }

    private static ProjectStructureRuntimeLaunchResolution Success(ProjectStructureRuntimeLaunchPlan plan)
    {
        if (!Directory.Exists(plan.WorkingDirectory))
        {
            return File.Exists(plan.WorkingDirectory)
                ? Fail("The configured runtime working directory is a file and cannot be used as a process working directory.")
                : Fail("The configured runtime working directory does not exist or is not accessible.");
        }

        if (plan.Target is not null && !TargetExists(plan.Target))
        {
            return Fail($"The configured {plan.Target.Description} does not exist, has the wrong path type, or is not accessible.");
        }

        return new(plan, "Launch plan resolved.");
    }

    private static ProjectStructureRuntimeLaunchResolution Fail(string message)
        => new(null, message);
}
