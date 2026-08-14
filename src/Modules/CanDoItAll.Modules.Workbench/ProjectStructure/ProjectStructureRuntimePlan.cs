using System.Text;

namespace CanDoItAll.Modules.Workbench;

public enum ProjectStructureRuntimeHostPlatform
{
    Windows,
    Linux,
    MacOS
}

public enum ProjectStructureRuntimePlanKind
{
    DirectExecutable,
    DotNet,
    Python,
    Docker,
    PowerShellScript,
    PosixShellScript
}

public enum ProjectStructureRuntimeLaunchMode
{
    Direct,
    Terminal,
    Elevated
}

public enum ProjectStructureRuntimeCapabilityStatus
{
    Available,
    DependencyMissing,
    Unsupported,
    Headless,
    PolicyDenied
}

public sealed record ProjectStructureRuntimeCapability(
    ProjectStructureRuntimeCapabilityStatus Status,
    string Message)
{
    public bool IsAvailable => Status == ProjectStructureRuntimeCapabilityStatus.Available;

    public static ProjectStructureRuntimeCapability Available(string message)
        => new(ProjectStructureRuntimeCapabilityStatus.Available, message);

    public static ProjectStructureRuntimeCapability Unavailable(
        ProjectStructureRuntimeCapabilityStatus status,
        string message)
        => new(status, message);
}

public sealed record ProjectStructureRuntimeLaunchCapabilities(
    ProjectStructureRuntimeCapability Direct,
    ProjectStructureRuntimeCapability Terminal,
    ProjectStructureRuntimeCapability Elevation)
{
    public static ProjectStructureRuntimeLaunchCapabilities Unavailable(string message)
    {
        var capability = ProjectStructureRuntimeCapability.Unavailable(
            ProjectStructureRuntimeCapabilityStatus.Unsupported,
            message);
        return new(capability, capability, capability);
    }
}

public sealed record ProjectStructureRuntimeLaunchTarget(string Description, string Path, bool IsDirectory);

public sealed record ProjectStructureRuntimeLaunchPlan(
    ProjectStructureRuntimePlanKind Kind,
    IReadOnlyList<string> ExecutableCandidates,
    IReadOnlyList<string> Arguments,
    IReadOnlyDictionary<string, string?> EnvironmentVariables,
    string WorkingDirectory,
    string DisplayCommand,
    string DisplayName,
    IReadOnlyList<ProjectStructureRuntimeLaunchTarget> Targets,
    bool RequiresApproval,
    bool TerminalOnly)
{
    public ProjectStructureRuntimeLaunchTarget? Target => Targets.FirstOrDefault();
}

internal abstract record ProjectStructureRuntimeNodeDefinition(
    string WorkingDirectory,
    string DisplayName,
    IReadOnlyList<ProjectStructureRuntimeLaunchTarget> Targets);

internal sealed record ProjectStructureDotNetRuntimeDefinition(
    string WorkingDirectory,
    string DisplayName,
    ProjectStructureRuntimeLaunchTarget Target,
    string ProjectFilePath,
    bool IsWatch,
    bool IsRelease,
    string LaunchProfileName,
    string LocalhostUrl)
    : ProjectStructureRuntimeNodeDefinition(WorkingDirectory, DisplayName, [Target]);

internal sealed record ProjectStructurePythonRuntimeDefinition(
    string WorkingDirectory,
    string DisplayName,
    ProjectStructureRuntimeLaunchTarget Target,
    ProjectPythonProvider Provider,
    string EnvironmentPath,
    string EnvironmentName,
    string EntryPoint,
    IReadOnlyList<string> Arguments)
    : ProjectStructureRuntimeNodeDefinition(WorkingDirectory, DisplayName, [Target]);

internal sealed record ProjectStructureDirectRuntimeDefinition(
    ProjectStructureRuntimePlanKind Kind,
    string WorkingDirectory,
    string DisplayName,
    IReadOnlyList<ProjectStructureRuntimeLaunchTarget> Targets,
    string Executable,
    IReadOnlyList<string> Arguments,
    bool RequiresApproval = false,
    bool TerminalOnly = false)
    : ProjectStructureRuntimeNodeDefinition(WorkingDirectory, DisplayName, Targets);

internal sealed record ProjectStructureShellRuntimeDefinition(
    ProjectStructureRuntimePlanKind Kind,
    string WorkingDirectory,
    string DisplayName,
    IReadOnlyList<ProjectStructureRuntimeLaunchTarget> Targets,
    string? ScriptPath,
    string? CommandText,
    IReadOnlyList<string> Arguments)
    : ProjectStructureRuntimeNodeDefinition(WorkingDirectory, DisplayName, Targets);

internal sealed record ProjectStructureRuntimePlanCompilation(
    ProjectStructureRuntimeLaunchPlan? Plan,
    string Message)
{
    public bool IsSuccess => Plan is not null;

    public static ProjectStructureRuntimePlanCompilation Success(ProjectStructureRuntimeLaunchPlan plan)
        => new(plan, "Runtime plan compiled.");

    public static ProjectStructureRuntimePlanCompilation Fail(string message)
        => new(null, message);
}

internal sealed class ProjectStructureRuntimePlanCompiler
{
    public ProjectStructureRuntimePlanCompilation Compile(
        ProjectStructureRuntimeNodeDefinition definition,
        ProjectStructureRuntimeHostPlatform platform)
        => definition switch
        {
            ProjectStructureDotNetRuntimeDefinition dotnet => CompileDotNet(dotnet),
            ProjectStructurePythonRuntimeDefinition python => CompilePython(python, platform),
            ProjectStructureDirectRuntimeDefinition direct => CompileDirect(direct),
            ProjectStructureShellRuntimeDefinition shell => CompileShell(shell, platform),
            _ => ProjectStructureRuntimePlanCompilation.Fail("The runtime-node definition is not supported.")
        };

    private static ProjectStructureRuntimePlanCompilation CompileDotNet(
        ProjectStructureDotNetRuntimeDefinition definition)
    {
        List<string> arguments;
        if (definition.IsWatch)
        {
            arguments = ["watch", "--project", definition.ProjectFilePath, "run"];
        }
        else
        {
            arguments = ["run", "--project", definition.ProjectFilePath];
        }

        if (definition.IsRelease)
        {
            arguments.Add("-c");
            arguments.Add("Release");
        }

        var environment = new Dictionary<string, string?>(StringComparer.Ordinal);
        if (!string.IsNullOrWhiteSpace(definition.LocalhostUrl))
        {
            environment["ASPNETCORE_URLS"] = definition.LocalhostUrl.Trim();
            arguments.Add("--no-launch-profile");
        }
        else if (!string.IsNullOrWhiteSpace(definition.LaunchProfileName))
        {
            arguments.Add("--launch-profile");
            arguments.Add(definition.LaunchProfileName.Trim());
        }

        return Success(
            ProjectStructureRuntimePlanKind.DotNet,
            ["dotnet"],
            arguments,
            environment,
            definition,
            requiresApproval: false,
            terminalOnly: false);
    }

    private static ProjectStructureRuntimePlanCompilation CompilePython(
        ProjectStructurePythonRuntimeDefinition definition,
        ProjectStructureRuntimeHostPlatform platform)
    {
        if (definition.Provider == ProjectPythonProvider.Conda)
        {
            var condaArguments = new List<string>
            {
                "run",
                "--no-capture-output",
                "-n",
                definition.EnvironmentName,
                platform == ProjectStructureRuntimeHostPlatform.Windows ? "python.exe" : "python"
            };
            AddEntryPointAndArguments(condaArguments, definition.EntryPoint, definition.Arguments);
            return Success(
                ProjectStructureRuntimePlanKind.Python,
                ["conda"],
                condaArguments,
                new Dictionary<string, string?>(StringComparer.Ordinal),
                definition,
                requiresApproval: false,
                terminalOnly: string.IsNullOrWhiteSpace(definition.EntryPoint));
        }

        var interpreterPath = ResolvePythonInterpreterPath(definition.EnvironmentPath, platform);
        var arguments = new List<string>();
        AddEntryPointAndArguments(arguments, definition.EntryPoint, definition.Arguments);
        var targets = definition.Targets
            .Append(new ProjectStructureRuntimeLaunchTarget("Python interpreter", interpreterPath, false))
            .ToArray();
        var compiledDefinition = new ProjectStructureDirectRuntimeDefinition(
            ProjectStructureRuntimePlanKind.Python,
            definition.WorkingDirectory,
            definition.DisplayName,
            targets,
            interpreterPath,
            arguments,
            RequiresApproval: false,
            TerminalOnly: string.IsNullOrWhiteSpace(definition.EntryPoint));
        return CompileDirect(compiledDefinition);
    }

    private static ProjectStructureRuntimePlanCompilation CompileDirect(
        ProjectStructureDirectRuntimeDefinition definition)
    {
        if (string.IsNullOrWhiteSpace(definition.Executable))
        {
            return ProjectStructureRuntimePlanCompilation.Fail("A direct runtime plan requires an executable.");
        }

        return Success(
            definition.Kind,
            [definition.Executable.Trim()],
            definition.Arguments,
            new Dictionary<string, string?>(StringComparer.Ordinal),
            definition,
            definition.RequiresApproval,
            definition.TerminalOnly);
    }

    private static ProjectStructureRuntimePlanCompilation CompileShell(
        ProjectStructureShellRuntimeDefinition definition,
        ProjectStructureRuntimeHostPlatform platform)
    {
        if (definition.Kind == ProjectStructureRuntimePlanKind.PosixShellScript &&
            platform == ProjectStructureRuntimeHostPlatform.Windows)
        {
            return ProjectStructureRuntimePlanCompilation.Fail(
                "POSIX shell scripts are not supported on Windows hosts.");
        }

        IReadOnlyList<string> candidates = definition.Kind switch
        {
            ProjectStructureRuntimePlanKind.PowerShellScript when platform == ProjectStructureRuntimeHostPlatform.Windows =>
                ["pwsh", "powershell"],
            ProjectStructureRuntimePlanKind.PowerShellScript => ["pwsh"],
            _ => ["sh"]
        };
        var arguments = new List<string>();
        if (definition.Kind == ProjectStructureRuntimePlanKind.PowerShellScript)
        {
            arguments.Add("-NoLogo");
            arguments.Add("-NoProfile");
            arguments.Add(definition.ScriptPath is null ? "-Command" : "-File");
        }
        else
        {
            arguments.Add(definition.ScriptPath is null ? "-c" : definition.ScriptPath);
        }

        if (definition.ScriptPath is not null &&
            definition.Kind == ProjectStructureRuntimePlanKind.PowerShellScript)
        {
            arguments.Add(definition.ScriptPath);
        }
        else if (definition.CommandText is not null)
        {
            arguments.Add(definition.CommandText);
        }

        arguments.AddRange(definition.Arguments);
        return Success(
            definition.Kind,
            candidates,
            arguments,
            new Dictionary<string, string?>(StringComparer.Ordinal),
            definition,
            requiresApproval: true,
            terminalOnly: false);
    }

    private static ProjectStructureRuntimePlanCompilation Success(
        ProjectStructureRuntimePlanKind kind,
        IReadOnlyList<string> executableCandidates,
        IReadOnlyList<string> arguments,
        IReadOnlyDictionary<string, string?> environment,
        ProjectStructureRuntimeNodeDefinition definition,
        bool requiresApproval,
        bool terminalOnly)
    {
        var displayCommand = FormatDisplayCommand(executableCandidates[0], arguments, environment);
        return ProjectStructureRuntimePlanCompilation.Success(
            new ProjectStructureRuntimeLaunchPlan(
                kind,
                executableCandidates,
                arguments.ToArray(),
                new Dictionary<string, string?>(environment, StringComparer.Ordinal),
                definition.WorkingDirectory,
                displayCommand,
                definition.DisplayName,
                definition.Targets.ToArray(),
                requiresApproval,
                terminalOnly));
    }

    private static void AddEntryPointAndArguments(
        List<string> target,
        string entryPoint,
        IReadOnlyList<string> arguments)
    {
        if (!string.IsNullOrWhiteSpace(entryPoint))
        {
            target.Add(entryPoint);
        }

        target.AddRange(arguments);
    }

    internal static string ResolvePythonInterpreterPath(
        string environmentPath,
        ProjectStructureRuntimeHostPlatform platform)
        => CombineHostPath(
            environmentPath,
            platform,
            platform == ProjectStructureRuntimeHostPlatform.Windows
                ? ["Scripts", "python.exe"]
                : ["bin", "python"]);

    private static string CombineHostPath(
        string root,
        ProjectStructureRuntimeHostPlatform platform,
        IReadOnlyList<string> segments)
    {
        var separator = platform == ProjectStructureRuntimeHostPlatform.Windows ? '\\' : '/';
        var normalizedRoot = platform == ProjectStructureRuntimeHostPlatform.Windows
            ? root.Replace('/', '\\')
            : root;
        var builder = new StringBuilder(normalizedRoot.TrimEnd(separator));
        if (builder.Length == 2 && builder[1] == ':')
        {
            builder.Append(separator);
        }

        foreach (var segment in segments)
        {
            if (builder.Length > 0 && builder[^1] != separator)
            {
                builder.Append(separator);
            }

            builder.Append(segment.Trim(separator));
        }

        return builder.ToString();
    }

    internal static string FormatDisplayCommand(
        string executable,
        IReadOnlyList<string> arguments,
        IReadOnlyDictionary<string, string?> environment)
    {
        var command = string.Join(
            ' ',
            new[] { executable }
                .Concat(arguments)
                .Select(QuoteDisplayToken));
        if (environment.Count == 0)
        {
            return command;
        }

        var environmentDisplay = string.Join(
            ' ',
            environment.Select(pair => $"{pair.Key}={QuoteDisplayToken(pair.Value ?? string.Empty)}"));
        return $"{environmentDisplay} {command}";
    }

    private static string QuoteDisplayToken(string value)
    {
        if (value.Length > 0 && value.All(character =>
                !char.IsWhiteSpace(character) && character is not '"' and not '\''))
        {
            return value;
        }

        return $"\"{value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal)}\"";
    }
}
