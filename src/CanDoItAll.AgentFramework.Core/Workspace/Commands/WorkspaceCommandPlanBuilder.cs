using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

internal sealed class WorkspaceCommandPlanBuilder
{
    private const string WebProjectSdk = "Microsoft.NET.Sdk.Web";
    private const string BlazorWebAssemblyProjectSdk = "Microsoft.NET.Sdk.BlazorWebAssembly";

    private static readonly HashSet<string> ApprovedDotnetTemplates = new(StringComparer.OrdinalIgnoreCase)
    {
        "blazor",
        "blazorserver",
        "blazorserver-empty",
        "blazorwasm",
        "blazorwasm-empty",
        "classlib",
        "console",
        "mstest",
        "mvc",
        "nunit",
        "razor",
        "sln",
        "web",
        "webapp",
        "webapi",
        "worker",
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

    private static readonly HashSet<string> AllowedRunnableProjectExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".csproj",
        ".fsproj",
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

    public WorkspaceCommandPlan BuildDotnetRun(
        string targetPath,
        string? url = null,
        string configuration = "Debug",
        bool noBuild = true,
        bool waitForHttp = true,
        string? workingDirectory = null,
        int startupTimeoutSeconds = 45,
        int timeoutSeconds = 120,
        bool keepAlive = false,
        WorkspaceProcessLifetimeScope lifetimeScope = WorkspaceProcessLifetimeScope.ExecutionRun)
    {
        var target = BuildDotnetRunnableTarget(targetPath, workingDirectory);
        var urls = ResolveDotnetRunUrls(url);
        var normalizedConfiguration = NormalizeConfiguration(configuration);
        var shouldWaitForHttp = waitForHttp || IsKnownHttpProject(target.ProjectArgument);

        if (!shouldWaitForHttp)
        {
            var arguments = new List<string>
            {
                "run",
                "--project",
                target.ProjectArgument,
                "--configuration",
                normalizedConfiguration
            };
            if (noBuild)
            {
                arguments.Add("--no-build");
            }

            if (!string.IsNullOrWhiteSpace(urls.ListenUrl))
            {
                arguments.Add("--no-launch-profile");
                arguments.Add("--");
                arguments.Add("--urls");
                arguments.Add(urls.ListenUrl);
            }

            return CreatePlan(
                toolName: "workspace_dotnet_run",
                recipeId: "dotnet_run",
                riskClass: "LocalExecution",
                approvalRequired: false,
                networkAllowed: !string.IsNullOrWhiteSpace(urls.ProbeUrl),
                mutatesWorkspace: false,
                targetPaths: target.TargetPaths,
                workingDirectory: target.WorkingDirectoryRelative,
                workingDirectoryPath: target.WorkingDirectoryPath,
                executableCandidates: ["dotnet"],
                arguments: arguments,
                timeoutSeconds: timeoutSeconds,
                stdoutLimitCharacters: 256 * 1024,
                stderrLimitCharacters: 96 * 1024);
        }

        var boundedStartupTimeoutSeconds = Math.Clamp(startupTimeoutSeconds, 1, 600);
        var artifactPaths = BuildDotnetRunArtifactPaths();
        var script = BuildDotnetHttpRunPowerShellScript(
            target.ProjectArgument,
            target.WorkingDirectoryPath,
            normalizedConfiguration,
            noBuild,
            urls.ListenUrl,
            urls.ProbeUrl,
            artifactPaths.StdoutLogFullPath,
            artifactPaths.StderrLogFullPath,
            artifactPaths.StartupReceiptFullPath,
            boundedStartupTimeoutSeconds,
            keepAlive,
            lifetimeScope);
        WriteDotnetRunScript(artifactPaths.ScriptFullPath, script);
        var planTimeoutSeconds = Math.Max(timeoutSeconds, boundedStartupTimeoutSeconds + 10);

        return CreatePlan(
            toolName: "workspace_dotnet_run",
            recipeId: "dotnet_run_http_smoke",
            riskClass: "LocalExecution",
            approvalRequired: false,
            networkAllowed: true,
            mutatesWorkspace: true,
            targetPaths: target.TargetPaths.Concat(artifactPaths.TargetPaths).ToArray(),
            workingDirectory: target.WorkingDirectoryRelative,
            workingDirectoryPath: target.WorkingDirectoryPath,
            executableCandidates: ["pwsh", "powershell"],
            arguments:
            [
                "-NoLogo",
                "-NoProfile",
                "-NonInteractive",
                "-ExecutionPolicy",
                "Bypass",
                "-File",
                artifactPaths.ScriptFullPath
            ],
            timeoutSeconds: planTimeoutSeconds,
            stdoutLimitCharacters: 128 * 1024,
            stderrLimitCharacters: 128 * 1024);
    }

    public WorkspaceCommandPlan BuildDotnetNew(string template, string name, string? parentDirectory = null, bool force = false, int timeoutSeconds = 300)
    {
        if (string.IsNullOrWhiteSpace(template))
        {
            throw new InvalidOperationException("Provide a template name.");
        }

        var normalizedTemplate = template.Trim();
        if (!ApprovedDotnetTemplates.Contains(normalizedTemplate))
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

        var trimmedName = name.Trim();
        var workingDirectoryRelative = pathPolicy.ResolveWorkingDirectory(parentDirectory, createIfMissing: true, out var workingDirectoryResolution);
        if (AllowedProjectExtensions.Contains(Path.GetExtension(workingDirectoryRelative)))
        {
            throw new InvalidOperationException(
                $"workspace_dotnet_new parentDirectory '{workingDirectoryRelative}' ends with a project-file extension. Pass the containing directory as parentDirectory and the project name separately.");
        }

        if (Directory.Exists(workingDirectoryResolution.FullPath) &&
            ContainsTopLevelProjectFile(workingDirectoryResolution.FullPath))
        {
            throw new InvalidOperationException(
                $"workspace_dotnet_new is not allowed inside existing .NET project directory '{workingDirectoryRelative}'. Inspect and repair that project in place, or create a sibling project from its parent directory.");
        }

        var targetRelativePath = WorkspacePathPolicy.NormalizeRelativePath(Path.Combine(workingDirectoryRelative == "." ? string.Empty : workingDirectoryRelative, trimmedName));
        var targetPaths = BuildDotnetNewTargetPaths(normalizedTemplate, workingDirectoryRelative, trimmedName, targetRelativePath);
        var targetFullPath = Path.Combine(workingDirectoryResolution.FullPath, trimmedName);
        if (Directory.Exists(targetFullPath) && ContainsProjectFile(targetFullPath))
        {
            throw new InvalidOperationException(
                $"workspace_dotnet_new is not allowed for existing project target '{targetRelativePath}' because it already contains a .NET project or solution file. Inspect and repair the existing scaffold in place instead of re-scaffolding.");
        }

        if (force &&
            Directory.Exists(targetFullPath) &&
            Directory.EnumerateFileSystemEntries(targetFullPath).Any())
        {
            throw new InvalidOperationException(
                $"workspace_dotnet_new --force is not allowed over existing non-empty target '{targetRelativePath}'. Inspect and repair the existing scaffold, or explicitly delete the target directory first when replacement is intentional.");
        }

        var arguments = new List<string>
        {
            "new",
            normalizedTemplate,
            "-n",
            trimmedName
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
            targetPaths: targetPaths,
            workingDirectory: workingDirectoryRelative,
            workingDirectoryPath: workingDirectoryResolution.FullPath,
            executableCandidates: ["dotnet"],
            arguments: arguments,
            timeoutSeconds: timeoutSeconds,
            stdoutLimitCharacters: 128 * 1024,
            stderrLimitCharacters: 64 * 1024);
    }

    private static IReadOnlyList<string> BuildDotnetNewTargetPaths(
        string template,
        string workingDirectoryRelative,
        string trimmedName,
        string defaultTargetRelativePath)
    {
        if (!string.Equals(template, "sln", StringComparison.OrdinalIgnoreCase))
        {
            return [defaultTargetRelativePath];
        }

        var solutionBasePath = WorkspacePathPolicy.NormalizeRelativePath(Path.Combine(workingDirectoryRelative == "." ? string.Empty : workingDirectoryRelative, trimmedName));
        return
        [
            WorkspacePathPolicy.NormalizeRelativePath(solutionBasePath + ".slnx"),
            WorkspacePathPolicy.NormalizeRelativePath(solutionBasePath + ".sln")
        ];
    }

    private static bool ContainsProjectFile(string directory)
        => Directory.EnumerateFiles(
                directory,
                "*.*",
                new EnumerationOptions
                {
                    RecurseSubdirectories = true,
                    IgnoreInaccessible = true,
                    AttributesToSkip = 0
                })
            .Any(path => AllowedProjectExtensions.Contains(Path.GetExtension(path)));

    private static bool ContainsTopLevelProjectFile(string directory)
        => Directory.EnumerateFiles(directory, "*.*", SearchOption.TopDirectoryOnly)
            .Any(path => AllowedProjectExtensions.Contains(Path.GetExtension(path)));

    public WorkspaceCommandPlan BuildPythonRunFile(string path, string[]? arguments = null, string? workingDirectory = null, int timeoutSeconds = 300, string? sideEffectManifest = null)
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

    public WorkspaceCommandPlan BuildPowerShellRunScript(string path, string[]? arguments = null, string[]? outputPaths = null, string? workingDirectory = null, int timeoutSeconds = 300, string? sideEffectManifest = null)
    {
        var scriptResolution = ResolveExistingWorkspacePath(path, allowFiles: true, allowDirectories: false);
        if (!string.Equals(Path.GetExtension(scriptResolution.FullPath), ".ps1", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"PowerShell runner only accepts .ps1 files. '{scriptResolution.RelativePath}' does not use a .ps1 extension.");
        }

        ValidatePowerShellRunScriptIsBounded(scriptResolution);

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

    private static void ValidatePowerShellRunScriptIsBounded(WorkspacePathResolution scriptResolution)
    {
        var scriptText = File.ReadAllText(scriptResolution.FullPath);
        if (!LooksLikeForegroundBrowserHost(scriptText) ||
            LaunchesLongRunningHostAsChildProcess(scriptText))
        {
            return;
        }

        throw new InvalidOperationException(
            $"PowerShell runner scripts must not run a foreground long-running browser host. '{scriptResolution.RelativePath}' appears to start a static HTTP server inline. Start the server as a background child process, record its URL and process id, and let the helper script exit before browser tools run.");
    }

    private static bool LooksLikeForegroundBrowserHost(string scriptText)
    {
        if (string.IsNullOrWhiteSpace(scriptText))
        {
            return false;
        }

        return ContainsRegex(scriptText, @"\bHttpListener\b") &&
               (ContainsRegex(scriptText, @"\.GetContext(?:Async)?\s*\(") ||
                ContainsRegex(scriptText, @"\bwhile\s*\(")) ||
               ContainsRegex(scriptText, @"\bpython(?:\.exe)?\b[^\r\n;]*\s+-m\s+http\.server\b");
    }

    private static bool LaunchesLongRunningHostAsChildProcess(string scriptText)
    {
        return ContainsRegex(scriptText, @"\bStart-Process\b") ||
               ContainsRegex(scriptText, @"\bStart-Job\b") ||
               ContainsRegex(scriptText, @"\bStart-ThreadJob\b");
    }

    private static bool ContainsRegex(string text, string pattern)
    {
        return Regex.IsMatch(
            text,
            pattern,
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
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

        var resolution = ResolveDotnetTargetPath(
            targetPath,
            AllowedProjectExtensions,
            "Workspace dotnet recipes only allow solution or project targets.",
            "workspace dotnet recipes");

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

    private DotnetRunnableTarget BuildDotnetRunnableTarget(string targetPath, string? workingDirectory)
    {
        if (string.IsNullOrWhiteSpace(targetPath))
        {
            throw new InvalidOperationException("workspace_dotnet_run requires a project file target.");
        }

        var projectResolution = ResolveDotnetTargetPath(
            targetPath,
            AllowedRunnableProjectExtensions,
            "workspace_dotnet_run requires a .csproj, .fsproj, or .vbproj target.",
            "workspace_dotnet_run");

        string workingDirectoryRelative;
        WorkspacePathResolution workingDirectoryResolution;
        if (string.IsNullOrWhiteSpace(workingDirectory))
        {
            var projectDirectory = Path.GetDirectoryName(projectResolution.FullPath)
                ?? throw new InvalidOperationException($"Could not resolve the parent directory for '{projectResolution.RelativePath}'.");
            workingDirectoryRelative = pathPolicy.ResolveWorkingDirectory(pathPolicy.ToDisplayPath(projectDirectory), createIfMissing: false, out workingDirectoryResolution);
        }
        else
        {
            workingDirectoryRelative = pathPolicy.ResolveWorkingDirectory(workingDirectory, createIfMissing: false, out workingDirectoryResolution);
        }

        return new DotnetRunnableTarget(
            ProjectArgument: projectResolution.FullPath,
            WorkingDirectoryPath: workingDirectoryResolution.FullPath,
            WorkingDirectoryRelative: workingDirectoryRelative,
            TargetPaths: [projectResolution.RelativePath]);
    }

    private DotnetRunArtifactPaths BuildDotnetRunArtifactPaths()
    {
        var stamp = DateTimeOffset.UtcNow.UtcDateTime.ToString("yyyyMMdd-HHmmssfff");
        var relativeDirectory = pathPolicy.WorkspaceScope.CombineArtifactPath("process-runs", "dotnet-run", stamp);
        var fullDirectory = Path.GetFullPath(Path.Combine(pathPolicy.WorkspaceRoot, relativeDirectory.Replace('/', Path.DirectorySeparatorChar)));
        var stdoutRelativePath = WorkspacePathPolicy.NormalizeRelativePath(Path.Combine(relativeDirectory, "app.stdout.log"));
        var stderrRelativePath = WorkspacePathPolicy.NormalizeRelativePath(Path.Combine(relativeDirectory, "app.stderr.log"));
        var startupReceiptRelativePath = WorkspacePathPolicy.NormalizeRelativePath(Path.Combine(relativeDirectory, "startup.json"));
        var scriptRelativePath = WorkspacePathPolicy.NormalizeRelativePath(Path.Combine(relativeDirectory, "run.ps1"));

        return new DotnetRunArtifactPaths(
            ScriptFullPath: Path.Combine(fullDirectory, "run.ps1"),
            StdoutLogFullPath: Path.Combine(fullDirectory, "app.stdout.log"),
            StderrLogFullPath: Path.Combine(fullDirectory, "app.stderr.log"),
            StartupReceiptFullPath: Path.Combine(fullDirectory, "startup.json"),
            TargetPaths:
            [
                scriptRelativePath,
                stdoutRelativePath,
                stderrRelativePath,
                startupReceiptRelativePath
            ]);
    }

    private static void WriteDotnetRunScript(string scriptPath, string script)
    {
        var scriptDirectory = Path.GetDirectoryName(scriptPath)
            ?? throw new InvalidOperationException($"Could not resolve dotnet run script directory for '{scriptPath}'.");
        Directory.CreateDirectory(scriptDirectory);
        File.WriteAllText(scriptPath, script, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static DotnetRunUrls ResolveDotnetRunUrls(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return new DotnetRunUrls(null, null);
        }

        var trimmed = url.Trim();
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException("workspace_dotnet_run url must be an absolute http:// or https:// loopback URL.");
        }

        if (!IsLoopbackHost(uri.Host))
        {
            throw new InvalidOperationException("workspace_dotnet_run only accepts loopback URLs such as http://127.0.0.1:<port> or http://localhost:<port>.");
        }

        return new DotnetRunUrls(
            ListenUrl: uri.GetLeftPart(UriPartial.Authority),
            ProbeUrl: trimmed);
    }

    private static bool IsLoopbackHost(string host)
    {
        if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return IPAddress.TryParse(host, out var address) && IPAddress.IsLoopback(address);
    }

    private static bool IsKnownHttpProject(string projectPath)
    {
        if (!File.Exists(projectPath))
        {
            return false;
        }

        try
        {
            var document = XDocument.Load(projectPath);
            var sdk = document.Root?.Attribute("Sdk")?.Value;
            return IsKnownHttpProjectSdk(sdk);
        }
        catch
        {
            return false;
        }
    }

    private static bool IsKnownHttpProjectSdk(string? sdk)
    {
        if (string.IsNullOrWhiteSpace(sdk))
        {
            return false;
        }

        return sdk
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(item =>
                item.StartsWith(WebProjectSdk, StringComparison.OrdinalIgnoreCase) ||
                item.StartsWith(BlazorWebAssemblyProjectSdk, StringComparison.OrdinalIgnoreCase));
    }

    private static string BuildDotnetHttpRunPowerShellScript(
        string projectPath,
        string workingDirectory,
        string configuration,
        bool noBuild,
        string? listenUrl,
        string? probeUrl,
        string stdoutLogPath,
        string stderrLogPath,
        string startupReceiptPath,
        int startupTimeoutSeconds,
        bool keepAlive,
        WorkspaceProcessLifetimeScope lifetimeScope)
    {
        var builder = new StringBuilder();
        builder.AppendLine("$ErrorActionPreference = 'Stop'");
        builder.AppendLine("$ProgressPreference = 'SilentlyContinue'");
        builder.AppendLine("$projectPath = " + ToPowerShellSingleQuotedString(projectPath));
        builder.AppendLine("$workingDirectory = " + ToPowerShellSingleQuotedString(workingDirectory));
        builder.AppendLine("$configuration = " + ToPowerShellSingleQuotedString(configuration));
        builder.AppendLine("$listenUrl = " + ToPowerShellSingleQuotedString(listenUrl ?? string.Empty));
        builder.AppendLine("$probeUrl = " + ToPowerShellSingleQuotedString(probeUrl ?? string.Empty));
        builder.AppendLine("$stdoutLog = " + ToPowerShellSingleQuotedString(stdoutLogPath));
        builder.AppendLine("$stderrLog = " + ToPowerShellSingleQuotedString(stderrLogPath));
        builder.AppendLine("$startupReceipt = " + ToPowerShellSingleQuotedString(startupReceiptPath));
        builder.AppendLine("$startupTimeoutSeconds = " + startupTimeoutSeconds.ToString());
        builder.AppendLine("$noBuild = " + (noBuild ? "$true" : "$false"));
        builder.AppendLine("$keepAlive = " + (keepAlive ? "$true" : "$false"));
        builder.AppendLine("$lifetimeScope = " + ToPowerShellSingleQuotedString(lifetimeScope.ToString()));
        builder.AppendLine("$appProcess = $null");
        builder.AppendLine("function Read-LogTail {");
        builder.AppendLine("    param([string]$Path)");
        builder.AppendLine("    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { return '' }");
        builder.AppendLine("    try {");
        builder.AppendLine("        $content = Get-Content -LiteralPath $Path -Raw -ErrorAction Stop");
        builder.AppendLine("        if ($content.Length -le 4000) { return $content }");
        builder.AppendLine("        return $content.Substring($content.Length - 4000)");
        builder.AppendLine("    } catch { return '' }");
        builder.AppendLine("}");
        builder.AppendLine("function Quote-ProcessArgument {");
        builder.AppendLine("    param([string]$Value)");
        builder.AppendLine("    if ($null -eq $Value -or $Value.Length -eq 0) { return '\"\"' }");
        builder.AppendLine("    if ($Value.IndexOfAny([char[]](\" `\"`t`r`n\")) -lt 0) { return $Value }");
        builder.AppendLine("    return '\"' + $Value.Replace('\"', '\\\"') + '\"'");
        builder.AppendLine("}");
        builder.AppendLine("function Resolve-ProcessTreeIds {");
        builder.AppendLine("    param([int]$RootProcessId)");
        builder.AppendLine("    $orderedIds = [System.Collections.Generic.List[int]]::new()");
        builder.AppendLine("    function Add-DescendantProcessIds {");
        builder.AppendLine("        param([int]$ParentProcessId)");
        builder.AppendLine("        $children = @()");
        builder.AppendLine("        try {");
        builder.AppendLine("            $children = Get-CimInstance Win32_Process -Filter \"ParentProcessId = $ParentProcessId\" -ErrorAction Stop");
        builder.AppendLine("        } catch {");
        builder.AppendLine("            $children = @()");
        builder.AppendLine("        }");
        builder.AppendLine("        foreach ($childProcess in $children) {");
        builder.AppendLine("            $childProcessId = [int]$childProcess.ProcessId");
        builder.AppendLine("            Add-DescendantProcessIds $childProcessId");
        builder.AppendLine("            if (-not $orderedIds.Contains($childProcessId)) { [void]$orderedIds.Add($childProcessId) }");
        builder.AppendLine("        }");
        builder.AppendLine("    }");
        builder.AppendLine("    Add-DescendantProcessIds $RootProcessId");
        builder.AppendLine("    if (-not $orderedIds.Contains($RootProcessId)) { [void]$orderedIds.Add($RootProcessId) }");
        builder.AppendLine("    return @($orderedIds)");
        builder.AppendLine("}");
        builder.AppendLine("function Stop-AppProcessTree {");
        builder.AppendLine("    param([int[]]$ProcessIds)");
        builder.AppendLine("    foreach ($processIdToStop in $ProcessIds) {");
        builder.AppendLine("        try {");
        builder.AppendLine("            Stop-Process -Id $processIdToStop -Force -ErrorAction SilentlyContinue");
        builder.AppendLine("            Wait-Process -Id $processIdToStop -Timeout 5 -ErrorAction SilentlyContinue");
        builder.AppendLine("        } catch { }");
        builder.AppendLine("    }");
        builder.AppendLine("}");
        builder.AppendLine("function Write-StartupReceipt {");
        builder.AppendLine("    param([bool]$Succeeded, [string]$Message, [bool]$CleanupAttempted = $false, [int[]]$CleanupProcessIds = @())");
        builder.AppendLine("    $processTreeIds = if ($CleanupProcessIds.Count -gt 0) { @($CleanupProcessIds) } elseif ($appProcess -ne $null) { @(Resolve-ProcessTreeIds $appProcess.Id) } else { @() }");
        builder.AppendLine("    $payload = [ordered]@{");
        builder.AppendLine("        succeeded = $Succeeded");
        builder.AppendLine("        message = $Message");
        builder.AppendLine("        projectPath = $projectPath");
        builder.AppendLine("        workingDirectory = $workingDirectory");
        builder.AppendLine("        listenUrl = $listenUrl");
        builder.AppendLine("        probeUrl = $probeUrl");
        builder.AppendLine("        hostUrl = $probeUrl");
        builder.AppendLine("        databaseProfileId = $env:CANDOITALL_DATABASE_PROFILE_ID");
        builder.AppendLine("        databaseProfileFingerprint = $env:CANDOITALL_DATABASE_PROFILE_FINGERPRINT");
        builder.AppendLine("        databaseProfileKey = $env:CANDOITALL_DATABASE_PROFILE_KEY");
        builder.AppendLine("        appProcessId = if ($appProcess -ne $null) { $appProcess.Id } else { $null }");
        builder.AppendLine("        appProcessTreeIds = @($processTreeIds)");
        builder.AppendLine("        keepAlive = $keepAlive");
        builder.AppendLine("        lifetimeScope = $lifetimeScope");
        builder.AppendLine("        aspNetCoreEnvironment = $env:ASPNETCORE_ENVIRONMENT");
        builder.AppendLine("        dotnetEnvironment = $env:DOTNET_ENVIRONMENT");
        builder.AppendLine("        cleanupAttempted = $CleanupAttempted");
        builder.AppendLine("        cleanupProcessIds = @($CleanupProcessIds)");
        builder.AppendLine("        cleanupReceiptPath = $startupReceipt");
        builder.AppendLine("        stdoutLog = $stdoutLog");
        builder.AppendLine("        stderrLog = $stderrLog");
        builder.AppendLine("        stdoutTail = Read-LogTail $stdoutLog");
        builder.AppendLine("        stderrTail = Read-LogTail $stderrLog");
        builder.AppendLine("        capturedAtUtc = [DateTimeOffset]::UtcNow.ToString('O')");
        builder.AppendLine("        stopCommand = if ($processTreeIds.Count -gt 0) { 'Stop-Process -Id ' + ($processTreeIds -join ',') + ' -Force' } else { '' }");
        builder.AppendLine("    }");
        builder.AppendLine("    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $startupReceipt) | Out-Null");
        builder.AppendLine("    $json = $payload | ConvertTo-Json -Depth 6");
        builder.AppendLine("    Set-Content -LiteralPath $startupReceipt -Value $json -Encoding UTF8");
        builder.AppendLine("    return $json");
        builder.AppendLine("}");
        builder.AppendLine("try {");
        builder.AppendLine("    if (-not (Test-Path -LiteralPath $projectPath -PathType Leaf)) { throw \"Project file not found: $projectPath\" }");
        builder.AppendLine("    if (-not (Test-Path -LiteralPath $workingDirectory -PathType Container)) { throw \"Working directory not found: $workingDirectory\" }");
        builder.AppendLine("    if ([string]::IsNullOrWhiteSpace($listenUrl)) {");
        builder.AppendLine("        $listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 0)");
        builder.AppendLine("        try {");
        builder.AppendLine("            $listener.Start()");
        builder.AppendLine("            $port = ([System.Net.IPEndPoint]$listener.LocalEndpoint).Port");
        builder.AppendLine("        } finally {");
        builder.AppendLine("            $listener.Stop()");
        builder.AppendLine("        }");
        builder.AppendLine("        $listenUrl = \"http://127.0.0.1:$port\"");
        builder.AppendLine("    }");
        builder.AppendLine("    if ([string]::IsNullOrWhiteSpace($probeUrl)) { $probeUrl = $listenUrl }");
        builder.AppendLine("    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $stdoutLog) | Out-Null");
        builder.AppendLine("    if ([string]::IsNullOrWhiteSpace($env:ASPNETCORE_ENVIRONMENT)) { $env:ASPNETCORE_ENVIRONMENT = 'Development' }");
        builder.AppendLine("    if ([string]::IsNullOrWhiteSpace($env:DOTNET_ENVIRONMENT)) { $env:DOTNET_ENVIRONMENT = 'Development' }");
        builder.AppendLine("    $dotnetPath = (Get-Command dotnet -ErrorAction Stop).Source");
        builder.AppendLine("    $argumentList = @('run', '--project', $projectPath, '--configuration', $configuration, '--no-launch-profile')");
        builder.AppendLine("    if ($noBuild) { $argumentList += '--no-build' }");
        builder.AppendLine("    $argumentList += @('--', '--urls', $listenUrl)");
        builder.AppendLine("    $argumentString = ($argumentList | ForEach-Object { Quote-ProcessArgument $_ }) -join ' '");
        builder.AppendLine("    $startParameters = @{");
        builder.AppendLine("        FilePath = $dotnetPath");
        builder.AppendLine("        ArgumentList = $argumentString");
        builder.AppendLine("        WorkingDirectory = $workingDirectory");
        builder.AppendLine("        RedirectStandardOutput = $stdoutLog");
        builder.AppendLine("        RedirectStandardError = $stderrLog");
        builder.AppendLine("        PassThru = $true");
        builder.AppendLine("    }");
        builder.AppendLine("    if ($IsWindows -or $env:OS -eq 'Windows_NT') { $startParameters['WindowStyle'] = 'Hidden' }");
        builder.AppendLine("    $appProcess = Start-Process @startParameters");
        builder.AppendLine("    $deadline = [DateTimeOffset]::UtcNow.AddSeconds($startupTimeoutSeconds)");
        builder.AppendLine("    $lastError = ''");
        builder.AppendLine("    $ready = $false");
        builder.AppendLine("    while ([DateTimeOffset]::UtcNow -lt $deadline) {");
        builder.AppendLine("        $appProcess.Refresh()");
        builder.AppendLine("        if ($appProcess.HasExited) { throw \"dotnet run exited before $probeUrl returned success. Exit code $($appProcess.ExitCode). stderr tail: $(Read-LogTail $stderrLog)\" }");
        builder.AppendLine("        try {");
        builder.AppendLine("            $response = Invoke-WebRequest -Uri $probeUrl -UseBasicParsing -TimeoutSec 2 -ErrorAction Stop");
        builder.AppendLine("            $statusCode = [int]$response.StatusCode");
        builder.AppendLine("            if ($statusCode -ge 200 -and $statusCode -lt 400) { $ready = $true; break }");
        builder.AppendLine("            $lastError = \"HTTP $statusCode\"");
        builder.AppendLine("        } catch {");
        builder.AppendLine("            $lastError = $_.Exception.Message");
        builder.AppendLine("        }");
        builder.AppendLine("        Start-Sleep -Milliseconds 500");
        builder.AppendLine("    }");
        builder.AppendLine("    if (-not $ready) { throw \"Timed out after $startupTimeoutSeconds second(s) waiting for $probeUrl. Last error: $lastError. stderr tail: $(Read-LogTail $stderrLog)\" }");
        builder.AppendLine("    if ($keepAlive) {");
        builder.AppendLine("        $successJson = Write-StartupReceipt $true \"Application started and $probeUrl returned success. The process tree is still running for follow-up browser proof; use stopCommand from startup.json when proof is complete.\"");
        builder.AppendLine("    } else {");
        builder.AppendLine("        $processTreeIds = if ($appProcess -ne $null) { @(Resolve-ProcessTreeIds $appProcess.Id) } else { @() }");
        builder.AppendLine("        Stop-AppProcessTree $processTreeIds");
        builder.AppendLine("        $successJson = Write-StartupReceipt $true \"Application started and $probeUrl returned success. Process tree was stopped after smoke validation.\" ($processTreeIds.Count -gt 0) $processTreeIds");
        builder.AppendLine("    }");
        builder.AppendLine("    Write-Output $successJson");
        builder.AppendLine("} catch {");
        builder.AppendLine("    $message = $_.Exception.Message");
        builder.AppendLine("    $processTreeIds = if ($appProcess -ne $null) { @(Resolve-ProcessTreeIds $appProcess.Id) } else { @() }");
        builder.AppendLine("    Stop-AppProcessTree $processTreeIds");
        builder.AppendLine("    $failureJson = Write-StartupReceipt $false $message ($processTreeIds.Count -gt 0) $processTreeIds");
        builder.AppendLine("    Write-Error $failureJson");
        builder.AppendLine("    exit 1");
        builder.AppendLine("}");
        return builder.ToString();
    }

    private static string ToPowerShellSingleQuotedString(string value)
        => "'" + value.Replace("'", "''") + "'";

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

    private WorkspacePathResolution ResolveDotnetTargetPath(
        string path,
        IReadOnlySet<string> allowedExtensions,
        string invalidTargetMessage,
        string recipeName)
    {
        var resolution = ResolveExistingWorkspacePath(path, allowFiles: true, allowDirectories: true);
        if (File.Exists(resolution.FullPath))
        {
            var extension = Path.GetExtension(resolution.FullPath);
            if (!allowedExtensions.Contains(extension))
            {
                throw new InvalidOperationException($"{invalidTargetMessage} '{resolution.RelativePath}' uses '{extension}'.");
            }

            return resolution;
        }

        var candidates = Directory.EnumerateFiles(resolution.FullPath, "*", SearchOption.TopDirectoryOnly)
            .Where(file => allowedExtensions.Contains(Path.GetExtension(file)))
            .Select(file => new
            {
                FullPath = Path.GetFullPath(file),
                DisplayPath = pathPolicy.ToDisplayPath(Path.GetFullPath(file)),
                Extension = Path.GetExtension(file)
            })
            .OrderBy(item => item.DisplayPath, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (allowedExtensions.Contains(".sln") || allowedExtensions.Contains(".slnx"))
        {
            var solutionCandidates = candidates
                .Where(item => string.Equals(item.Extension, ".sln", StringComparison.OrdinalIgnoreCase) ||
                               string.Equals(item.Extension, ".slnx", StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (solutionCandidates.Count == 1)
            {
                return ResolveExistingWorkspacePath(solutionCandidates[0].DisplayPath, allowFiles: true, allowDirectories: false);
            }

            if (solutionCandidates.Count > 1)
            {
                throw new InvalidOperationException($"Directory '{resolution.RelativePath}' contains multiple solution files. Pass an explicit solution or project file to {recipeName}.");
            }
        }

        if (candidates.Count == 1)
        {
            return ResolveExistingWorkspacePath(candidates[0].DisplayPath, allowFiles: true, allowDirectories: false);
        }

        if (candidates.Count == 0)
        {
            throw new InvalidOperationException($"Directory '{resolution.RelativePath}' does not contain a supported .NET solution or project file for {recipeName}.");
        }

        throw new InvalidOperationException($"Directory '{resolution.RelativePath}' contains multiple .NET project files. Pass an explicit project file to {recipeName}.");
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

    private sealed record DotnetRunnableTarget(
        string ProjectArgument,
        string WorkingDirectoryPath,
        string WorkingDirectoryRelative,
        IReadOnlyList<string> TargetPaths);

    private sealed record DotnetRunArtifactPaths(
        string ScriptFullPath,
        string StdoutLogFullPath,
        string StderrLogFullPath,
        string StartupReceiptFullPath,
        IReadOnlyList<string> TargetPaths);

    private sealed record DotnetRunUrls(string? ListenUrl, string? ProbeUrl);
}
