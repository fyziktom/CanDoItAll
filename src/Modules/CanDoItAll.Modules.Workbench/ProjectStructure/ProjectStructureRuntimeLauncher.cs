using CanDoItAll.SharedKernel;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.Workbench;

public interface IProjectStructureRuntimeLauncher
{
    bool IsAvailable { get; }

    bool IsRunning(string nodeId) => false;

    ProjectStructureRuntimeLaunchResolution Resolve(ProjectStructureNode? node);

    ProjectStructureRuntimeLaunchResolution Resolve(
        ProjectObjectType objectType,
        string? objectSubtype,
        string? notes,
        string metadataJson,
        ProjectStructureRuntimePathAuthorityMode pathAuthorityMode);

    Task<ProjectStructureRuntimeLaunchResult> LaunchAsync(
        ProjectStructureNode node,
        bool runAsAdministrator,
        CancellationToken cancellationToken = default)
        => LaunchAsync(
            node,
            runAsAdministrator
                ? ProjectStructureRuntimeLaunchMode.Elevated
                : ProjectStructureRuntimeLaunchMode.Direct,
            ProjectStructureRuntimeLaunchApproval.NotGranted,
            cancellationToken);

    Task<ProjectStructureRuntimeLaunchResult> LaunchAsync(
        ProjectStructureNode node,
        ProjectStructureRuntimeLaunchMode mode,
        CancellationToken cancellationToken = default)
        => LaunchAsync(
            node,
            mode,
            ProjectStructureRuntimeLaunchApproval.NotGranted,
            cancellationToken);

    Task<ProjectStructureRuntimeLaunchResult> LaunchAsync(
        ProjectStructureNode node,
        ProjectStructureRuntimeLaunchMode mode,
        ProjectStructureRuntimeLaunchApproval approval,
        CancellationToken cancellationToken = default);

    Task<ProjectStructureRuntimeLaunchResult> StopAsync(
        ProjectStructureNode node,
        CancellationToken cancellationToken = default)
        => Task.FromResult(new ProjectStructureRuntimeLaunchResult(
            false,
            "This runtime launcher does not own a stoppable Workbench session."));
}

public enum ProjectStructureRuntimeLaunchApproval
{
    NotGranted,
    OperatorConfirmed
}

public enum ProjectStructureRuntimePathAuthorityMode
{
    OperatorSelected,
    AgentExecution
}

public sealed record ProjectStructureRuntimeLaunchResolution(
    ProjectStructureRuntimeLaunchPlan? Plan,
    string Message,
    ProjectStructureRuntimeLaunchCapabilities? Capabilities = null)
{
    public bool IsSuccess => Plan is not null;

    public ProjectStructureRuntimeLaunchCapabilities EffectiveCapabilities =>
        Capabilities ?? ProjectStructureRuntimeLaunchCapabilities.Unavailable(Message);
}

public sealed record ProjectStructureRuntimeLaunchResult(bool IsSuccess, string Message);

internal sealed class ProjectStructureRuntimeLauncher(
    ProjectStructureRuntimePathResolver pathResolver,
    ILogger<ProjectStructureRuntimeLauncher> logger,
    IProjectStructureDotNetProjectTargetResolver dotNetProjectTargetResolver,
    ProjectStructureRuntimePlanCompiler planCompiler,
    ProjectStructureRuntimeHostContext hostContext,
    IProjectStructureRuntimeExecutionAdapter executionAdapter,
    IProjectStructureTerminalPresenter terminalPresenter,
    IProjectStructureRuntimeElevationAdapter elevationAdapter) : IProjectStructureRuntimeLauncher
{
    public bool IsAvailable => true;

    public bool IsRunning(string nodeId) => executionAdapter.IsRunning(nodeId);

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
            _ => Fail("Runtime launch is only available for runtime-capable nodes.")
        };
    }

    public Task<ProjectStructureRuntimeLaunchResult> LaunchAsync(
        ProjectStructureNode node,
        bool runAsAdministrator,
        CancellationToken cancellationToken = default)
        => LaunchAsync(
            node,
            runAsAdministrator
                ? ProjectStructureRuntimeLaunchMode.Elevated
                : ProjectStructureRuntimeLaunchMode.Direct,
            cancellationToken);

    public async Task<ProjectStructureRuntimeLaunchResult> LaunchAsync(
        ProjectStructureNode node,
        ProjectStructureRuntimeLaunchMode mode,
        CancellationToken cancellationToken = default)
        => await LaunchAsync(
            node,
            mode,
            ProjectStructureRuntimeLaunchApproval.NotGranted,
            cancellationToken).ConfigureAwait(false);

    public async Task<ProjectStructureRuntimeLaunchResult> LaunchAsync(
        ProjectStructureNode node,
        ProjectStructureRuntimeLaunchMode mode,
        ProjectStructureRuntimeLaunchApproval approval,
        CancellationToken cancellationToken = default)
    {
        var resolution = Resolve(node);
        if (!resolution.IsSuccess || resolution.Plan is null)
        {
            return new(false, resolution.Message);
        }

        var plan = resolution.Plan;
        if (plan.RequiresApproval && approval != ProjectStructureRuntimeLaunchApproval.OperatorConfirmed)
        {
            return new(
                false,
                "This explicit script plan requires operator confirmation for each launch.");
        }

        if (ValidatePlanTargets(plan) is { } targetFailure)
        {
            return new(false, targetFailure);
        }

        var capability = mode switch
        {
            ProjectStructureRuntimeLaunchMode.Direct => resolution.EffectiveCapabilities.Direct,
            ProjectStructureRuntimeLaunchMode.Terminal => resolution.EffectiveCapabilities.Terminal,
            ProjectStructureRuntimeLaunchMode.Elevated => resolution.EffectiveCapabilities.Elevation,
            _ => ProjectStructureRuntimeCapability.Unavailable(
                ProjectStructureRuntimeCapabilityStatus.Unsupported,
                "The requested runtime launch mode is not supported.")
        };
        if (!capability.IsAvailable)
        {
            return new(false, capability.Message);
        }

        logger.LogInformation(
            "Launching Workbench runtime node {NodeId} through {LaunchMode} using plan {PlanKind}.",
            node.Id,
            mode,
            plan.Kind);
        return mode switch
        {
            ProjectStructureRuntimeLaunchMode.Direct =>
                await executionAdapter.LaunchAsync(plan, node.Id, cancellationToken).ConfigureAwait(false),
            ProjectStructureRuntimeLaunchMode.Terminal =>
                await terminalPresenter.OpenAsync(plan, node.Id, cancellationToken).ConfigureAwait(false),
            ProjectStructureRuntimeLaunchMode.Elevated =>
                await elevationAdapter.LaunchAsync(plan, node.Id, cancellationToken).ConfigureAwait(false),
            _ => new(false, "The requested runtime launch mode is not supported.")
        };
    }

    public Task<ProjectStructureRuntimeLaunchResult> StopAsync(
        ProjectStructureNode node,
        CancellationToken cancellationToken = default)
        => executionAdapter.StopAsync(node.Id, cancellationToken);

    private ProjectStructureRuntimeLaunchResolution ResolveScriptPlan(
        string objectSubtype,
        ProjectScriptMetadata? metadata,
        ProjectStructureRuntimePathAuthorityMode pathAuthorityMode)
    {
        metadata ??= new ProjectScriptMetadata();
        var scriptKind = ResolveScriptKind(objectSubtype, metadata);
        if (ProjectStructureDirectDotNetCommandPolicy.TryClassify(
                metadata.Command,
                metadata.Arguments,
                out _))
        {
            return Fail(ProjectStructureDirectDotNetCommandPolicy.TypedEnvironmentRequiredMessage);
        }

        var workingDirectoryResolution = ResolveScriptWorkingDirectory(metadata, pathAuthorityMode);
        if (!workingDirectoryResolution.IsSuccess || workingDirectoryResolution.Path is null)
        {
            return Fail(workingDirectoryResolution.Message);
        }

        var workingDirectory = workingDirectoryResolution.Path;
        return scriptKind switch
        {
            ProjectScriptKind.PowerShell => ResolveExplicitShellPlan(
                ProjectStructureRuntimePlanKind.PowerShellScript,
                metadata,
                workingDirectory,
                pathAuthorityMode),
            ProjectScriptKind.PosixShell => ResolveExplicitShellPlan(
                ProjectStructureRuntimePlanKind.PosixShellScript,
                metadata,
                workingDirectory,
                pathAuthorityMode),
            _ => ResolveDirectScriptPlan(scriptKind, metadata, workingDirectory, pathAuthorityMode)
        };
    }

    private ProjectStructureRuntimeLaunchResolution ResolveDirectScriptPlan(
        ProjectScriptKind scriptKind,
        ProjectScriptMetadata metadata,
        string workingDirectory,
        ProjectStructureRuntimePathAuthorityMode pathAuthorityMode)
    {
        ProjectStructureLegacyRuntimeCommandMigrationResult migration;
        IReadOnlyList<ProjectStructureRuntimeLaunchTarget> targets = [];
        if (!string.IsNullOrWhiteSpace(metadata.Command))
        {
            migration = ProjectStructureLegacyRuntimeCommandMigrator.TryMigrate(
                metadata.Command,
                metadata.Arguments);
            if (!migration.IsSuccess || migration.Executable is null)
            {
                return Fail(migration.Message);
            }
        }
        else if (!string.IsNullOrWhiteSpace(metadata.ScriptPath))
        {
            var scriptPath = pathResolver.Resolve(
                metadata.ScriptPath,
                "Script path",
                pathAuthorityMode,
                ProjectStructureRuntimePathKind.File,
                string.IsNullOrWhiteSpace(metadata.WorkingDirectory) ? null : workingDirectory);
            if (!scriptPath.IsSuccess || scriptPath.Path is null)
            {
                return Fail(scriptPath.Message);
            }

            if (!ProjectStructureLegacyRuntimeCommandMigrator.TryTokenizeArguments(
                    metadata.Arguments,
                    out var arguments,
                    out var argumentFailure))
            {
                return Fail(argumentFailure);
            }

            migration = ProjectStructureLegacyRuntimeCommandMigrationResult.Success(
                scriptPath.Path,
                arguments,
                wasWrapped: false);
            targets = [new ProjectStructureRuntimeLaunchTarget("script path", scriptPath.Path, false)];
        }
        else
        {
            return Fail("Script launch requires a typed command or explicit script path.");
        }

        var displayName = scriptKind switch
        {
            ProjectScriptKind.EfMigration => "EF migration",
            ProjectScriptKind.TailwindWatch => "Tailwind watch",
            _ => "script command"
        };
        return Compile(
            new ProjectStructureDirectRuntimeDefinition(
                ProjectStructureRuntimePlanKind.DirectExecutable,
                workingDirectory,
                displayName,
                targets,
                migration.Executable!,
                migration.Arguments));
    }

    private ProjectStructureRuntimeLaunchResolution ResolveExplicitShellPlan(
        ProjectStructureRuntimePlanKind planKind,
        ProjectScriptMetadata metadata,
        string initialWorkingDirectory,
        ProjectStructureRuntimePathAuthorityMode pathAuthorityMode)
    {
        if (planKind == ProjectStructureRuntimePlanKind.PowerShellScript &&
            string.IsNullOrWhiteSpace(metadata.ScriptPath) &&
            ProjectStructureLegacyRuntimeCommandMigrator.ContainsEncodedPowerShellOption(metadata.Command))
        {
            return Fail("Encoded shell content cannot be inspected safely and requires operator repair.");
        }

        string? scriptPath = null;
        var workingDirectory = initialWorkingDirectory;
        IReadOnlyList<ProjectStructureRuntimeLaunchTarget> targets = [];
        if (!string.IsNullOrWhiteSpace(metadata.ScriptPath))
        {
            var scriptResolution = pathResolver.Resolve(
                metadata.ScriptPath,
                "Script path",
                pathAuthorityMode,
                ProjectStructureRuntimePathKind.File,
                string.IsNullOrWhiteSpace(metadata.WorkingDirectory) ? null : initialWorkingDirectory);
            if (!scriptResolution.IsSuccess || scriptResolution.Path is null)
            {
                return Fail(scriptResolution.Message);
            }

            scriptPath = scriptResolution.Path;
            if (string.IsNullOrWhiteSpace(metadata.WorkingDirectory))
            {
                workingDirectory = Path.GetDirectoryName(scriptPath) ?? initialWorkingDirectory;
            }

            targets = [new ProjectStructureRuntimeLaunchTarget("script path", scriptPath, false)];
        }

        if (scriptPath is null && string.IsNullOrWhiteSpace(metadata.Command))
        {
            return Fail("Explicit shell launch requires a command or script path.");
        }

        if (!ProjectStructureLegacyRuntimeCommandMigrator.TryTokenizeArguments(
                metadata.Arguments,
                out var arguments,
                out var argumentFailure))
        {
            return Fail(argumentFailure);
        }

        var commandText = scriptPath is null
            ? string.Join(
                ' ',
                new[] { metadata.Command }
                    .Concat(arguments)
                    .Where(value => !string.IsNullOrWhiteSpace(value)))
            : null;
        var shellArguments = scriptPath is null ? Array.Empty<string>() : arguments;
        return Compile(
            new ProjectStructureShellRuntimeDefinition(
                planKind,
                workingDirectory,
                planKind == ProjectStructureRuntimePlanKind.PowerShellScript
                    ? "PowerShell script"
                    : "POSIX shell script",
                targets,
                scriptPath,
                commandText,
                shellArguments));
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
                pathAuthorityMode),
            ProjectEnvironmentKind.DotNetRuntime => ResolveDotNetPlan(
                metadata,
                ".NET runtime",
                isRelease: false,
                isWatch: false,
                pathAuthorityMode),
            ProjectEnvironmentKind.DotNetRelease => ResolveDotNetPlan(
                metadata,
                "release run",
                isRelease: true,
                isWatch: false,
                pathAuthorityMode),
            ProjectEnvironmentKind.PythonEnvironment => ResolvePythonPlan(metadata, pathAuthorityMode),
            _ => Fail("Runtime launch is not supported for this environment type.")
        };
    }

    private ProjectStructureRuntimeLaunchResolution ResolveInfrastructurePlan(
        string objectSubtype,
        ProjectInfrastructureMetadata? metadata,
        ProjectStructureRuntimePathAuthorityMode pathAuthorityMode)
    {
        metadata ??= new ProjectInfrastructureMetadata();
        return ResolveInfrastructureKind(objectSubtype, metadata) == ProjectInfrastructureKind.DockerMode
            ? ResolveDockerPlan(metadata, pathAuthorityMode)
            : Fail("Runtime launch is not supported for this infrastructure type.");
    }

    private ProjectStructureRuntimeLaunchResolution ResolveDockerPlan(
        ProjectInfrastructureMetadata metadata,
        ProjectStructureRuntimePathAuthorityMode pathAuthorityMode)
    {
        if (string.IsNullOrWhiteSpace(metadata.RuntimeCommand))
        {
            return Fail("Docker runtime launch requires a runtime command.");
        }

        var workingDirectory = pathResolver.Resolve(
            FirstNonEmpty(metadata.WorkingDirectory, metadata.FolderPath, "."),
            "Docker working directory",
            pathAuthorityMode,
            ProjectStructureRuntimePathKind.Directory);
        if (!workingDirectory.IsSuccess || workingDirectory.Path is null)
        {
            return Fail(workingDirectory.Message);
        }

        var migration = ProjectStructureLegacyRuntimeCommandMigrator.TryMigrate(
            metadata.RuntimeCommand,
            metadata.RuntimeArguments);
        if (!migration.IsSuccess || migration.Executable is null)
        {
            return Fail(migration.Message);
        }

        return Compile(
            new ProjectStructureDirectRuntimeDefinition(
                ProjectStructureRuntimePlanKind.Docker,
                workingDirectory.Path,
                "Docker runtime",
                [new ProjectStructureRuntimeLaunchTarget(
                    "Docker working directory",
                    workingDirectory.Path,
                    true)],
                migration.Executable,
                migration.Arguments));
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

        string? explicitWorkingDirectory = null;
        if (!string.IsNullOrWhiteSpace(metadata.WorkingDirectory))
        {
            var workingDirectoryResolution = pathResolver.Resolve(
                metadata.WorkingDirectory,
                "Runtime working directory",
                pathAuthorityMode,
                ProjectStructureRuntimePathKind.Directory);
            if (!workingDirectoryResolution.IsSuccess || workingDirectoryResolution.Path is null)
            {
                return Fail(workingDirectoryResolution.Message);
            }

            explicitWorkingDirectory = workingDirectoryResolution.Path;
        }

        var projectPath = pathResolver.Resolve(
            metadata.ProjectPath,
            "Project path",
            pathAuthorityMode,
            ProjectStructureRuntimePathKind.FileOrDirectory,
            explicitWorkingDirectory);
        if (!projectPath.IsSuccess || projectPath.Path is null)
        {
            return Fail(projectPath.Message);
        }

        var projectTarget = dotNetProjectTargetResolver.Resolve(projectPath.Path);
        if (!projectTarget.IsSuccess || string.IsNullOrWhiteSpace(projectTarget.ProjectFilePath))
        {
            return Fail(projectTarget.Message);
        }

        var projectFilePath = projectTarget.ProjectFilePath;
        var workingDirectory = explicitWorkingDirectory ??
                               Path.GetDirectoryName(projectFilePath) ??
                               projectFilePath;
        return Compile(
            new ProjectStructureDotNetRuntimeDefinition(
                workingDirectory,
                displayName,
                new ProjectStructureRuntimeLaunchTarget("project file", projectFilePath, false),
                projectFilePath,
                isWatch,
                isRelease,
                metadata.LaunchProfileName,
                metadata.LocalhostUrl));
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

        var projectPath = pathResolver.Resolve(
            metadata.ProjectPath,
            "Python project path",
            pathAuthorityMode,
            ProjectStructureRuntimePathKind.FileOrDirectory);
        if (!projectPath.IsSuccess || projectPath.Path is null)
        {
            return Fail(projectPath.Message);
        }

        var workingDirectory = projectPath.IsDirectory
            ? projectPath.Path
            : Path.GetDirectoryName(projectPath.Path) ?? projectPath.Path;
        var projectTarget = new ProjectStructureRuntimeLaunchTarget(
            "Python project path",
            projectPath.Path,
            projectPath.IsDirectory);
        var entryPoint = ResolvePythonEntryPoint(metadata, projectPath, workingDirectory, pathAuthorityMode);
        if (!entryPoint.IsSuccess && !string.IsNullOrWhiteSpace(metadata.EntryPoint))
        {
            return Fail(entryPoint.Message);
        }

        if (!ProjectStructureLegacyRuntimeCommandMigrator.TryTokenizeArguments(
                metadata.Arguments,
                out var arguments,
                out var argumentFailure))
        {
            return Fail(argumentFailure);
        }

        if (metadata.PythonProvider == ProjectPythonProvider.Conda)
        {
            return Compile(
                new ProjectStructurePythonRuntimeDefinition(
                    workingDirectory,
                    "Conda environment",
                    projectTarget,
                    ProjectPythonProvider.Conda,
                    string.Empty,
                    metadata.EnvironmentName.Trim(),
                    entryPoint.Path ?? string.Empty,
                    arguments));
        }

        var environmentPath = ResolvePythonEnvironmentPath(
            metadata.EnvironmentName,
            workingDirectory,
            pathAuthorityMode);
        if (!environmentPath.IsSuccess || environmentPath.Path is null)
        {
            return Fail(environmentPath.Message);
        }

        var interpreterPath = ProjectStructureRuntimePlanCompiler.ResolvePythonInterpreterPath(
            environmentPath.Path,
            hostContext.Platform);
        var interpreter = pathResolver.Resolve(
            interpreterPath,
            "Python interpreter",
            pathAuthorityMode,
            ProjectStructureRuntimePathKind.File);
        if (!interpreter.IsSuccess)
        {
            return Fail(interpreter.Message);
        }

        return Compile(
            new ProjectStructurePythonRuntimeDefinition(
                workingDirectory,
                "Python environment",
                projectTarget,
                ProjectPythonProvider.Python,
                environmentPath.Path,
                metadata.EnvironmentName.Trim(),
                entryPoint.Path ?? string.Empty,
                arguments));
    }

    private ProjectStructureRuntimePathResolution ResolvePythonEntryPoint(
        ProjectEnvironmentMetadata metadata,
        ProjectStructureRuntimePathResolution projectPath,
        string workingDirectory,
        ProjectStructureRuntimePathAuthorityMode pathAuthorityMode)
    {
        if (!string.IsNullOrWhiteSpace(metadata.EntryPoint))
        {
            return pathResolver.Resolve(
                metadata.EntryPoint,
                "Python entry point",
                pathAuthorityMode,
                ProjectStructureRuntimePathKind.File,
                workingDirectory);
        }

        return projectPath.IsDirectory
            ? new ProjectStructureRuntimePathResolution(null, false, "Python interactive environment has no headless entry point.")
            : projectPath;
    }

    private ProjectStructureRuntimePathResolution ResolvePythonEnvironmentPath(
        string environmentName,
        string workingDirectory,
        ProjectStructureRuntimePathAuthorityMode pathAuthorityMode)
    {
        if (!environmentName.Trim().EndsWith(".ps1", StringComparison.OrdinalIgnoreCase))
        {
            return pathResolver.Resolve(
                environmentName,
                "Python environment path",
                pathAuthorityMode,
                ProjectStructureRuntimePathKind.Directory,
                workingDirectory);
        }

        var activation = pathResolver.Resolve(
            environmentName,
            "Python activation script path",
            pathAuthorityMode,
            ProjectStructureRuntimePathKind.File,
            workingDirectory);
        if (!activation.IsSuccess || activation.Path is null)
        {
            return activation;
        }

        var scriptsDirectory = Path.GetDirectoryName(activation.Path);
        var environmentDirectory = scriptsDirectory is null
            ? null
            : Path.GetDirectoryName(scriptsDirectory);
        return string.IsNullOrWhiteSpace(environmentDirectory)
            ? new ProjectStructureRuntimePathResolution(
                null,
                false,
                "Python activation script path does not identify a virtual-environment root.")
            : new ProjectStructureRuntimePathResolution(
                environmentDirectory,
                true,
                "Legacy activation path migrated to the virtual-environment root.");
    }

    private ProjectStructureRuntimePathResolution ResolveScriptWorkingDirectory(
        ProjectScriptMetadata metadata,
        ProjectStructureRuntimePathAuthorityMode pathAuthorityMode)
    {
        if (string.IsNullOrWhiteSpace(metadata.WorkingDirectory) &&
            !string.IsNullOrWhiteSpace(metadata.ScriptPath))
        {
            var scriptPath = pathResolver.Resolve(
                metadata.ScriptPath,
                "Script path",
                pathAuthorityMode,
                ProjectStructureRuntimePathKind.File);
            if (scriptPath.IsSuccess && scriptPath.Path is not null)
            {
                var directory = Path.GetDirectoryName(scriptPath.Path);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    return new(directory, true, "Script working directory resolved from the script path.");
                }
            }
        }

        return pathResolver.Resolve(
            string.IsNullOrWhiteSpace(metadata.WorkingDirectory) ? "." : metadata.WorkingDirectory,
            "Script working directory",
            pathAuthorityMode,
            ProjectStructureRuntimePathKind.Directory);
    }

    private ProjectStructureRuntimeLaunchResolution Compile(ProjectStructureRuntimeNodeDefinition definition)
    {
        var compilation = planCompiler.Compile(definition, hostContext.Platform);
        if (!compilation.IsSuccess || compilation.Plan is null)
        {
            return Fail(compilation.Message);
        }

        var plan = compilation.Plan;
        if (ValidatePlanTargets(plan) is { } targetFailure)
        {
            return Fail(targetFailure);
        }

        var capabilities = new ProjectStructureRuntimeLaunchCapabilities(
            executionAdapter.Probe(plan),
            terminalPresenter.Probe(plan),
            elevationAdapter.Probe(plan));
        return new(plan, "Runtime launch plan resolved.", capabilities);
    }

    private static string? ValidatePlanTargets(ProjectStructureRuntimeLaunchPlan plan)
    {
        if (!Directory.Exists(plan.WorkingDirectory))
        {
            return File.Exists(plan.WorkingDirectory)
                ? "The configured runtime working directory is a file and cannot be used as a process working directory."
                : "The configured runtime working directory does not exist or is not accessible.";
        }

        foreach (var target in plan.Targets)
        {
            var exists = target.IsDirectory
                ? Directory.Exists(target.Path)
                : File.Exists(target.Path);
            if (!exists)
            {
                return $"The configured {target.Description} does not exist, has the wrong path type, or is not accessible.";
            }
        }

        return null;
    }

    private static ProjectScriptKind ResolveScriptKind(
        string objectSubtype,
        ProjectScriptMetadata metadata)
        => metadata.ScriptKind == default && !string.IsNullOrWhiteSpace(objectSubtype)
            ? ProjectNodeKindRegistry.ResolveScriptKind(objectSubtype)
            : metadata.ScriptKind;

    private static ProjectEnvironmentKind ResolveEnvironmentKind(
        string objectSubtype,
        ProjectEnvironmentMetadata metadata)
        => metadata.EnvironmentKind == default && !string.IsNullOrWhiteSpace(objectSubtype)
            ? ProjectNodeKindRegistry.ResolveEnvironmentKind(objectSubtype)
            : metadata.EnvironmentKind;

    private static ProjectInfrastructureKind ResolveInfrastructureKind(
        string objectSubtype,
        ProjectInfrastructureMetadata metadata)
        => metadata.InfrastructureKind == default && !string.IsNullOrWhiteSpace(objectSubtype)
            ? ProjectNodeKindRegistry.ResolveInfrastructureKind(objectSubtype)
            : metadata.InfrastructureKind;

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

    private static ProjectStructureRuntimeLaunchResolution Fail(string message)
        => new(null, message, ProjectStructureRuntimeLaunchCapabilities.Unavailable(message));
}
