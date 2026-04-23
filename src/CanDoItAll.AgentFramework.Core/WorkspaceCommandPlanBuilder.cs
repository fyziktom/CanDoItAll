using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

internal sealed class WorkspaceCommandPlanBuilder
{
    private static readonly HashSet<string> ApprovedDotnetTemplates = new(StringComparer.OrdinalIgnoreCase)
    {
        "blazor",
        "classlib",
        "console",
        "webapi",
        "xunit"
    };

    private static readonly HashSet<string> AllowedProjectExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".csproj",
        ".fsproj",
        ".sln",
        ".slnx",
        ".vbproj"
    };

    private static readonly HashSet<string> AllowedSpreadsheetExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".csv",
        ".tsv",
        ".xls",
        ".xlsx"
    };

    private readonly WorkspacePathPolicy pathPolicy;

    public WorkspaceCommandPlanBuilder(WorkspacePathPolicy pathPolicy)
    {
        this.pathPolicy = pathPolicy;
    }

    public WorkspaceCommandPlan BuildGitStatus(bool includeBranch = true, string? workingDirectory = null, int timeoutSeconds = 30)
    {
        var workingDirectoryRelative = pathPolicy.ResolveWorkingDirectory(workingDirectory, createIfMissing: false, out var workingDirectoryResolution);
        var arguments = new List<string> { "status", "--short" };
        if (includeBranch)
        {
            arguments.Add("--branch");
        }

        return CreatePlan(
            toolName: "workspace_git_status",
            recipeId: "git_status",
            riskClass: "ReadOnly",
            approvalRequired: false,
            networkAllowed: false,
            mutatesWorkspace: false,
            targetPaths: [],
            workingDirectory: workingDirectoryRelative,
            workingDirectoryPath: workingDirectoryResolution.FullPath,
            executableCandidates: ["git"],
            arguments: arguments,
            timeoutSeconds: timeoutSeconds,
            stdoutLimitCharacters: 64 * 1024,
            stderrLimitCharacters: 32 * 1024);
    }

    public WorkspaceCommandPlan BuildGitDiff(string? path = null, bool nameOnly = false, string? workingDirectory = null, int timeoutSeconds = 30)
    {
        var workingDirectoryRelative = pathPolicy.ResolveWorkingDirectory(workingDirectory, createIfMissing: false, out var workingDirectoryResolution);
        var arguments = new List<string> { "diff" };
        IReadOnlyList<string> targetPaths = [];

        if (!string.IsNullOrWhiteSpace(path))
        {
            var targetResolution = ResolveExistingWorkspacePath(path, allowFiles: true, allowDirectories: true);
            arguments.Add("--");
            arguments.Add(Path.GetRelativePath(workingDirectoryResolution.FullPath, targetResolution.FullPath));
            targetPaths = [targetResolution.RelativePath];
        }
        else
        {
            arguments.Add(nameOnly ? "--name-only" : "--stat");
        }

        return CreatePlan(
            toolName: "workspace_git_diff",
            recipeId: "git_diff",
            riskClass: "ReadOnly",
            approvalRequired: false,
            networkAllowed: false,
            mutatesWorkspace: false,
            targetPaths: targetPaths,
            workingDirectory: workingDirectoryRelative,
            workingDirectoryPath: workingDirectoryResolution.FullPath,
            executableCandidates: ["git"],
            arguments: arguments,
            timeoutSeconds: timeoutSeconds,
            stdoutLimitCharacters: 128 * 1024,
            stderrLimitCharacters: 32 * 1024);
    }

    public WorkspaceCommandPlan BuildDotnetRestore(string? targetPath = null, string? workingDirectory = null, int timeoutSeconds = 600)
    {
        var target = BuildDotnetTarget(targetPath, workingDirectory);
        var arguments = new List<string> { "restore" };
        arguments.AddRange(target.TargetArguments);
        return CreatePlan(
            toolName: "workspace_dotnet_restore",
            recipeId: "dotnet_restore",
            riskClass: "LocalExecution",
            approvalRequired: true,
            networkAllowed: true,
            mutatesWorkspace: false,
            targetPaths: target.TargetPaths,
            workingDirectory: target.WorkingDirectoryRelative,
            workingDirectoryPath: target.WorkingDirectoryPath,
            executableCandidates: ["dotnet"],
            arguments: arguments,
            timeoutSeconds: timeoutSeconds,
            stdoutLimitCharacters: 256 * 1024,
            stderrLimitCharacters: 64 * 1024);
    }

    public WorkspaceCommandPlan BuildDotnetBuild(string? targetPath = null, string configuration = "Debug", bool noRestore = false, string? workingDirectory = null, int timeoutSeconds = 600)
    {
        var target = BuildDotnetTarget(targetPath, workingDirectory);
        var arguments = new List<string> { "build" };
        arguments.AddRange(target.TargetArguments);
        arguments.Add("-c");
        arguments.Add(NormalizeConfiguration(configuration));
        if (noRestore)
        {
            arguments.Add("--no-restore");
        }

        return CreatePlan(
            toolName: "workspace_dotnet_build",
            recipeId: "dotnet_build",
            riskClass: "LocalExecution",
            approvalRequired: false,
            networkAllowed: false,
            mutatesWorkspace: false,
            targetPaths: target.TargetPaths,
            workingDirectory: target.WorkingDirectoryRelative,
            workingDirectoryPath: target.WorkingDirectoryPath,
            executableCandidates: ["dotnet"],
            arguments: arguments,
            timeoutSeconds: timeoutSeconds,
            stdoutLimitCharacters: 256 * 1024,
            stderrLimitCharacters: 64 * 1024);
    }

    public WorkspaceCommandPlan BuildDotnetTest(string? targetPath = null, string configuration = "Debug", string? filter = null, bool noBuild = false, bool noRestore = false, string? workingDirectory = null, int timeoutSeconds = 1200)
    {
        var target = BuildDotnetTarget(targetPath, workingDirectory);
        var arguments = new List<string> { "test" };
        arguments.AddRange(target.TargetArguments);
        arguments.Add("-c");
        arguments.Add(NormalizeConfiguration(configuration));
        if (noBuild)
        {
            arguments.Add("--no-build");
        }

        if (noRestore)
        {
            arguments.Add("--no-restore");
        }

        if (!string.IsNullOrWhiteSpace(filter))
        {
            arguments.Add("--filter");
            arguments.Add(filter.Trim());
        }

        return CreatePlan(
            toolName: "workspace_dotnet_test",
            recipeId: "dotnet_test",
            riskClass: "LocalExecution",
            approvalRequired: false,
            networkAllowed: false,
            mutatesWorkspace: false,
            targetPaths: target.TargetPaths,
            workingDirectory: target.WorkingDirectoryRelative,
            workingDirectoryPath: target.WorkingDirectoryPath,
            executableCandidates: ["dotnet"],
            arguments: arguments,
            timeoutSeconds: timeoutSeconds,
            stdoutLimitCharacters: 512 * 1024,
            stderrLimitCharacters: 96 * 1024);
    }

    public WorkspaceCommandPlan BuildDotnetNew(string template, string name, string? parentDirectory = null, bool force = false, int timeoutSeconds = 300)
    {
        if (string.IsNullOrWhiteSpace(template))
        {
            throw new InvalidOperationException("Provide a template name.");
        }

        if (!ApprovedDotnetTemplates.Contains(template.Trim()))
        {
            throw new InvalidOperationException($"Template '{template}' is not approved. Allowed templates: {string.Join(", ", ApprovedDotnetTemplates.OrderBy(item => item, StringComparer.OrdinalIgnoreCase))}.");
        }

        if (string.IsNullOrWhiteSpace(name)
            || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
            || name.Contains(Path.DirectorySeparatorChar)
            || name.Contains(Path.AltDirectorySeparatorChar))
        {
            throw new InvalidOperationException("Provide a project name without path separators or invalid file-name characters.");
        }

        var workingDirectoryRelative = pathPolicy.ResolveWorkingDirectory(parentDirectory, createIfMissing: true, out var workingDirectoryResolution);
        var targetRelativePath = WorkspacePathPolicy.NormalizeRelativePath(Path.Combine(workingDirectoryRelative == "." ? string.Empty : workingDirectoryRelative, name.Trim()));

        var arguments = new List<string>
        {
            "new",
            template.Trim(),
            "-n",
            name.Trim()
        };
        if (force)
        {
            arguments.Add("--force");
        }

        return CreatePlan(
            toolName: "workspace_dotnet_new",
            recipeId: "dotnet_new",
            riskClass: "WorkspaceMutation",
            approvalRequired: true,
            networkAllowed: false,
            mutatesWorkspace: true,
            targetPaths: [targetRelativePath],
            workingDirectory: workingDirectoryRelative,
            workingDirectoryPath: workingDirectoryResolution.FullPath,
            executableCandidates: ["dotnet"],
            arguments: arguments,
            timeoutSeconds: timeoutSeconds,
            stdoutLimitCharacters: 128 * 1024,
            stderrLimitCharacters: 64 * 1024);
    }

    public WorkspaceCommandPlan BuildPythonRunFile(string path, string[]? arguments = null, string? workingDirectory = null, int timeoutSeconds = 300)
    {
        var scriptResolution = ResolveExistingWorkspacePath(path, allowFiles: true, allowDirectories: false);
        if (!string.Equals(Path.GetExtension(scriptResolution.FullPath), ".py", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Python runner only accepts .py files. '{scriptResolution.RelativePath}' does not use a .py extension.");
        }

        var workingDirectoryRelative = ResolveScriptWorkingDirectory(workingDirectory, scriptResolution.FullPath, allowedExternalRoots: null, out var workingDirectoryPath);
        var normalizedArguments = new List<string> { scriptResolution.FullPath };
        normalizedArguments.AddRange(NormalizeStructuredArguments(arguments));
        return CreatePlan(
            toolName: "workspace_python_run_file",
            recipeId: "python_run_file",
            riskClass: "LocalExecution",
            approvalRequired: true,
            networkAllowed: false,
            mutatesWorkspace: false,
            targetPaths: [scriptResolution.RelativePath],
            workingDirectory: workingDirectoryRelative,
            workingDirectoryPath: workingDirectoryPath,
            executableCandidates: ["python"],
            arguments: normalizedArguments,
            timeoutSeconds: timeoutSeconds,
            stdoutLimitCharacters: 128 * 1024,
            stderrLimitCharacters: 64 * 1024);
    }

    public WorkspaceCommandPlan BuildPowerShellRunScript(string path, string[]? arguments = null, string[]? outputPaths = null, string? workingDirectory = null, int timeoutSeconds = 300)
    {
        var scriptResolution = ResolveExistingWorkspacePath(path, allowFiles: true, allowDirectories: false);
        if (!string.Equals(Path.GetExtension(scriptResolution.FullPath), ".ps1", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"PowerShell runner only accepts .ps1 files. '{scriptResolution.RelativePath}' does not use a .ps1 extension.");
        }

        var workingDirectoryRelative = ResolveScriptWorkingDirectory(workingDirectory, scriptResolution.FullPath, allowedExternalRoots: null, out var workingDirectoryPath);
        var resolvedOutputPaths = ResolveWorkspacePaths(outputPaths);
        var normalizedArguments = new List<string>
        {
            "-NoLogo",
            "-NoProfile",
            "-NonInteractive",
            "-File",
            scriptResolution.FullPath
        };
        normalizedArguments.AddRange(NormalizeStructuredArguments(arguments));
        return CreatePlan(
            toolName: "workspace_pwsh_run_script",
            recipeId: "pwsh_run_script",
            riskClass: "LocalExecution",
            approvalRequired: true,
            networkAllowed: false,
            mutatesWorkspace: resolvedOutputPaths.Count > 0,
            targetPaths: resolvedOutputPaths.Count > 0
                ? resolvedOutputPaths
                : [scriptResolution.RelativePath],
            workingDirectory: workingDirectoryRelative,
            workingDirectoryPath: workingDirectoryPath,
            executableCandidates: ["pwsh", "powershell"],
            arguments: normalizedArguments,
            timeoutSeconds: timeoutSeconds,
            stdoutLimitCharacters: 128 * 1024,
            stderrLimitCharacters: 64 * 1024);
    }

    public WorkspaceCommandPlan BuildConvertDocumentWithMarkItDown(string sourcePath, string outputPath, int timeoutSeconds = 300)
    {
        var sourceResolution = ResolveExistingWorkspacePath(sourcePath, allowFiles: true, allowDirectories: false);
        var outputRelativePath = ResolveOutputWorkspacePath(outputPath, out var outputFullPath);
        var outputDirectory = Path.GetDirectoryName(outputFullPath);
        if (!string.IsNullOrWhiteSpace(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        return CreatePlan(
            toolName: "workspace_convert_document",
            recipeId: "convert_document",
            riskClass: "LocalExecution:DocumentConversion",
            approvalRequired: true,
            networkAllowed: false,
            mutatesWorkspace: true,
            targetPaths: [sourceResolution.RelativePath, outputRelativePath],
            workingDirectory: ".",
            workingDirectoryPath: pathPolicy.WorkspaceRoot,
            executableCandidates: ["python"],
            arguments: ["-m", "markitdown", sourceResolution.FullPath, "-o", outputFullPath],
            timeoutSeconds: timeoutSeconds,
            stdoutLimitCharacters: 64 * 1024,
            stderrLimitCharacters: 64 * 1024);
    }

    public WorkspaceCommandPlan BuildInspectSpreadsheetPreview(string path, int maxRows = 8, int maxColumns = 8, int timeoutSeconds = 300)
    {
        var sourceResolution = ResolveExistingWorkspacePath(path, allowFiles: true, allowDirectories: false);
        var extension = Path.GetExtension(sourceResolution.FullPath);
        if (!AllowedSpreadsheetExtensions.Contains(extension))
        {
            throw new InvalidOperationException($"Spreadsheet inspector supports .xls, .xlsx, .csv, and .tsv files. '{sourceResolution.RelativePath}' uses '{extension}'.");
        }

        return CreatePlan(
            toolName: "workspace_inspect_spreadsheet",
            recipeId: "inspect_spreadsheet",
            riskClass: "LocalExecution:SpreadsheetInspection",
            approvalRequired: false,
            networkAllowed: false,
            mutatesWorkspace: false,
            targetPaths: [sourceResolution.RelativePath],
            workingDirectory: ".",
            workingDirectoryPath: pathPolicy.WorkspaceRoot,
            executableCandidates: ["python"],
            arguments:
            [
                "-c",
                WorkspaceSpreadsheetPreviewScript.Content,
                sourceResolution.FullPath,
                Math.Max(1, maxRows).ToString(),
                Math.Max(1, maxColumns).ToString()
            ],
            timeoutSeconds: timeoutSeconds,
            stdoutLimitCharacters: 64 * 1024,
            stderrLimitCharacters: 32 * 1024);
    }

    public WorkspaceCommandPlan BuildSkillScript(string scriptPath, string[]? arguments = null, string? workingDirectory = null, bool approvalRequired = true, string trustLevel = "FileSkill", IReadOnlyList<string>? allowedExternalRoots = null)
    {
        var resolution = ResolveExistingPath(scriptPath, allowFiles: true, allowDirectories: false, allowedExternalRoots);
        var extension = Path.GetExtension(resolution.FullPath).ToLowerInvariant();
        var normalizedArguments = NormalizeStructuredArguments(arguments);
        var scriptWorkingDirectory = ResolveScriptWorkingDirectory(workingDirectory, resolution.FullPath, allowedExternalRoots, out var workingDirectoryPath);

        return extension switch
        {
            ".py" => CreatePlan(
                toolName: "skill_script_run",
                recipeId: "skill_script_python",
                riskClass: $"LocalExecution:{trustLevel}",
                approvalRequired: approvalRequired,
                networkAllowed: false,
                mutatesWorkspace: false,
                targetPaths: [resolution.DisplayPath],
                workingDirectory: scriptWorkingDirectory,
                workingDirectoryPath: workingDirectoryPath,
                executableCandidates: ["python"],
                arguments: [resolution.FullPath, .. normalizedArguments],
                timeoutSeconds: 300,
                stdoutLimitCharacters: 128 * 1024,
                stderrLimitCharacters: 64 * 1024),
            ".ps1" => CreatePlan(
                toolName: "skill_script_run",
                recipeId: "skill_script_pwsh",
                riskClass: $"LocalExecution:{trustLevel}",
                approvalRequired: approvalRequired,
                networkAllowed: false,
                mutatesWorkspace: false,
                targetPaths: [resolution.DisplayPath],
                workingDirectory: scriptWorkingDirectory,
                workingDirectoryPath: workingDirectoryPath,
                executableCandidates: ["pwsh", "powershell"],
                arguments: ["-NoLogo", "-NoProfile", "-NonInteractive", "-File", resolution.FullPath, .. normalizedArguments],
                timeoutSeconds: 300,
                stdoutLimitCharacters: 128 * 1024,
                stderrLimitCharacters: 64 * 1024),
            ".sh" => CreatePlan(
                toolName: "skill_script_run",
                recipeId: "skill_script_bash",
                riskClass: $"LocalExecution:{trustLevel}",
                approvalRequired: approvalRequired,
                networkAllowed: false,
                mutatesWorkspace: false,
                targetPaths: [resolution.DisplayPath],
                workingDirectory: scriptWorkingDirectory,
                workingDirectoryPath: workingDirectoryPath,
                executableCandidates: ["bash"],
                arguments: [resolution.FullPath, .. normalizedArguments],
                timeoutSeconds: 300,
                stdoutLimitCharacters: 128 * 1024,
                stderrLimitCharacters: 64 * 1024),
            ".js" => CreatePlan(
                toolName: "skill_script_run",
                recipeId: "skill_script_node",
                riskClass: $"LocalExecution:{trustLevel}",
                approvalRequired: approvalRequired,
                networkAllowed: false,
                mutatesWorkspace: false,
                targetPaths: [resolution.DisplayPath],
                workingDirectory: scriptWorkingDirectory,
                workingDirectoryPath: workingDirectoryPath,
                executableCandidates: ["node"],
                arguments: [resolution.FullPath, .. normalizedArguments],
                timeoutSeconds: 300,
                stdoutLimitCharacters: 128 * 1024,
                stderrLimitCharacters: 64 * 1024),
            _ => throw new InvalidOperationException($"Skill script '{resolution.DisplayPath}' uses unsupported extension '{extension}'.")
        };
    }

    private WorkspaceCommandPlan CreatePlan(
        string toolName,
        string recipeId,
        string riskClass,
        bool approvalRequired,
        bool networkAllowed,
        bool mutatesWorkspace,
        IReadOnlyList<string> targetPaths,
        string workingDirectory,
        string workingDirectoryPath,
        IReadOnlyList<string> executableCandidates,
        IReadOnlyList<string> arguments,
        int timeoutSeconds,
        int stdoutLimitCharacters,
        int stderrLimitCharacters)
    {
        var externalRootsAllowed = WorkspacePathPolicy.IsExternalTargetAliasPath(workingDirectory)
            || targetPaths.Any(WorkspacePathPolicy.IsExternalTargetAliasPath);
        var decision = new ToolExecutionDecision(
            ToolName: toolName,
            RecipeId: recipeId,
            RiskClass: riskClass,
            Allowed: true,
            ApprovalRequired: approvalRequired,
            NetworkAllowed: networkAllowed,
            ExternalRootsAllowed: externalRootsAllowed,
            Reason: externalRootsAllowed
                ? $"Recipe '{recipeId}' passed workspace policy checks and may access mapped external-target aliases."
                : $"Recipe '{recipeId}' passed workspace policy checks.");

        return new WorkspaceCommandPlan(
            Decision: decision,
            MutatesWorkspace: mutatesWorkspace,
            TargetPaths: targetPaths,
            WorkspaceRootPath: pathPolicy.WorkspaceRoot,
            WorkingDirectory: workingDirectory,
            WorkingDirectoryPath: workingDirectoryPath,
            ExecutableCandidates: executableCandidates,
            Arguments: arguments,
            TimeoutSeconds: Math.Clamp(timeoutSeconds, 1, 3600),
            StdoutLimitCharacters: stdoutLimitCharacters,
            StderrLimitCharacters: stderrLimitCharacters);
    }

    private (string WorkingDirectoryPath, string WorkingDirectoryRelative, IReadOnlyList<string> TargetArguments, IReadOnlyList<string> TargetPaths) BuildDotnetTarget(string? targetPath, string? workingDirectory)
    {
        var workingDirectoryRelative = pathPolicy.ResolveWorkingDirectory(workingDirectory, createIfMissing: false, out var workingDirectoryResolution);
        if (string.IsNullOrWhiteSpace(targetPath))
        {
            return (workingDirectoryResolution.FullPath, workingDirectoryRelative, [], []);
        }

        var resolution = ResolveExistingWorkspacePath(targetPath, allowFiles: true, allowDirectories: false);
        var extension = Path.GetExtension(resolution.FullPath);
        if (!AllowedProjectExtensions.Contains(extension))
        {
            throw new InvalidOperationException($"Workspace dotnet recipes only allow solution or project targets. '{resolution.RelativePath}' uses '{extension}'.");
        }

        // External-target aliases should be executed via absolute paths because the
        // process workspace and the external target can live in unrelated roots.
        // A relative traversal is brittle and has already produced broken dotnet
        // invocations against mapped targets such as external-target/C/...
        var targetArgument = resolution.IsWorkspacePath && workingDirectoryResolution.IsWorkspacePath
            ? Path.GetRelativePath(workingDirectoryResolution.FullPath, resolution.FullPath)
            : resolution.FullPath;

        return (
            workingDirectoryResolution.FullPath,
            workingDirectoryRelative,
            [targetArgument],
            [resolution.RelativePath]);
    }

    private string ResolveOutputWorkspacePath(string outputPath, out string outputFullPath)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            throw new InvalidOperationException("Provide a workspace-relative output path.");
        }

        var resolution = ResolveWorkspacePath(outputPath);
        outputFullPath = resolution.FullPath;
        return resolution.RelativePath;
    }

    private string ResolveScriptWorkingDirectory(string? workingDirectory, string scriptPath, IReadOnlyList<string>? allowedExternalRoots, out string workingDirectoryPath)
    {
        if (!string.IsNullOrWhiteSpace(workingDirectory))
        {
            var workingDirectoryDisplay = pathPolicy.ResolveWorkingDirectory(workingDirectory, createIfMissing: false, out var workingDirectoryResolution, allowedExternalRoots);
            workingDirectoryPath = workingDirectoryResolution.FullPath;
            return workingDirectoryDisplay;
        }

        workingDirectoryPath = Path.GetDirectoryName(scriptPath) ?? pathPolicy.WorkspaceRoot;
        return pathPolicy.ToDisplayPath(workingDirectoryPath);
    }

    private WorkspacePathResolution ResolveExistingWorkspacePath(string path, bool allowFiles, bool allowDirectories)
    {
        var resolution = ResolveWorkspacePath(path);
        if (allowFiles && File.Exists(resolution.FullPath))
        {
            return resolution;
        }

        if (allowDirectories && Directory.Exists(resolution.FullPath))
        {
            return resolution;
        }

        throw new InvalidOperationException($"Path '{resolution.RelativePath}' does not exist.");
    }

    private WorkspacePathResolution ResolveWorkspacePath(string path)
    {
        if (!pathPolicy.TryResolveWorkspacePath(path, allowWorkspaceRoot: false, out var resolution, out var validationMessage))
        {
            throw new InvalidOperationException(validationMessage);
        }

        return resolution;
    }

    private IReadOnlyList<string> ResolveWorkspacePaths(string[]? paths)
    {
        if (paths is null || paths.Length == 0)
        {
            return [];
        }

        return paths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(ResolveWorkspacePath)
            .Select(resolution => resolution.RelativePath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private WorkspacePathResolution ResolveExistingPath(string path, bool allowFiles, bool allowDirectories, IReadOnlyList<string>? allowedExternalRoots = null)
        => pathPolicy.ResolveExistingPath(path, allowFiles, allowDirectories, allowedExternalRoots);

    private static string[] NormalizeStructuredArguments(string[]? arguments)
    {
        return arguments?
            .Where(argument => !string.IsNullOrWhiteSpace(argument))
            .Select(argument => argument.Trim())
            .ToArray()
            ?? [];
    }

    private static string NormalizeConfiguration(string configuration)
    {
        return string.Equals(configuration, "Release", StringComparison.OrdinalIgnoreCase)
            ? "Release"
            : "Debug";
    }
}
