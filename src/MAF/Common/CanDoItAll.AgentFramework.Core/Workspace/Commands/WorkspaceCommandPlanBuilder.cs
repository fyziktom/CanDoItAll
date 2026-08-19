using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Git;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.AgentFramework.Core;

internal sealed class WorkspaceCommandPlanBuilder
{
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
    private readonly Func<string, bool> projectTreeInspector;

    public WorkspaceCommandPlanBuilder(
        WorkspacePathPolicy pathPolicy,
        Func<string, bool>? projectTreeInspector = null)
    {
        this.pathPolicy = pathPolicy;
        this.projectTreeInspector = projectTreeInspector ?? ContainsProjectFileInAccessibleTree;
    }

    public WorkspaceCommandPlan BuildGitStatus(bool includeBranch = true, string? workingDirectory = null, int timeoutSeconds = 30)
    {
        var workingDirectoryRelative = pathPolicy.ResolveWorkingDirectory(workingDirectory, createIfMissing: false, out var workingDirectoryResolution);
        var commandBuilder = new GitRepositoryCommandBuilder(new GitRepositoryPath(workingDirectoryResolution.FullPath));
        var spec = commandBuilder.Status(includeBranch);

        return CreateGitPlan(
            toolName: ToolContractCatalog.WorkspaceGitStatus,
            recipeId: "git_status",
            riskClass: "ReadOnly",
            approvalRequired: false,
            networkAllowed: false,
            mutatesWorkspace: false,
            targetPaths: [],
            workingDirectory: workingDirectoryRelative,
            workingDirectoryPath: workingDirectoryResolution.FullPath,
            spec: spec,
            timeoutSeconds: timeoutSeconds,
            stdoutLimitCharacters: 64 * 1024,
            stderrLimitCharacters: 32 * 1024);
    }

    public WorkspaceCommandPlan BuildGitDiff(string? path = null, bool nameOnly = false, string? workingDirectory = null, int timeoutSeconds = 30)
    {
        var workingDirectoryRelative = pathPolicy.ResolveWorkingDirectory(workingDirectory, createIfMissing: false, out var workingDirectoryResolution);
        var repositoryPath = new GitRepositoryPath(workingDirectoryResolution.FullPath);
        var commandBuilder = new GitRepositoryCommandBuilder(repositoryPath);
        var outputMode = nameOnly
            ? GitDiffOutputMode.NameOnly
            : GitDiffOutputMode.Stat;
        var options = new GitDiffOptions(outputMode);
        IReadOnlyList<string> targetPaths = [];

        if (!string.IsNullOrWhiteSpace(path))
        {
            var targetResolution = ResolveWorkspacePath(path);
            options = new GitDiffOptions(Path: BuildGitPathSpec(repositoryPath, targetResolution));
            targetPaths = [targetResolution.RelativePath];
        }

        return CreateGitPlan(
            toolName: ToolContractCatalog.WorkspaceGitDiff,
            recipeId: "git_diff",
            riskClass: "ReadOnly",
            approvalRequired: false,
            networkAllowed: false,
            mutatesWorkspace: false,
            targetPaths: targetPaths,
            workingDirectory: workingDirectoryRelative,
            workingDirectoryPath: workingDirectoryResolution.FullPath,
            spec: commandBuilder.Diff(options),
            timeoutSeconds: timeoutSeconds,
            stdoutLimitCharacters: 128 * 1024,
            stderrLimitCharacters: 32 * 1024);
    }

    public WorkspaceCommandPlan BuildGitLog(int count = 10, string? workingDirectory = null, int timeoutSeconds = 30)
    {
        var workingDirectoryRelative = pathPolicy.ResolveWorkingDirectory(workingDirectory, createIfMissing: false, out var workingDirectoryResolution);
        var commandBuilder = new GitRepositoryCommandBuilder(new GitRepositoryPath(workingDirectoryResolution.FullPath));
        var specification = CreateValidatedGitInput(
            () => commandBuilder.Log(count),
            "Git log count must be greater than zero.");

        return CreateGitPlan(
            toolName: ToolContractCatalog.WorkspaceGitLog,
            recipeId: "git_log",
            riskClass: "ReadOnly",
            approvalRequired: false,
            networkAllowed: false,
            mutatesWorkspace: false,
            targetPaths: [],
            workingDirectory: workingDirectoryRelative,
            workingDirectoryPath: workingDirectoryResolution.FullPath,
            spec: specification,
            timeoutSeconds: timeoutSeconds,
            stdoutLimitCharacters: 128 * 1024,
            stderrLimitCharacters: 32 * 1024);
    }

    public WorkspaceCommandPlan BuildGitShow(string revision, string? workingDirectory = null, int timeoutSeconds = 30)
    {
        var workingDirectoryRelative = pathPolicy.ResolveWorkingDirectory(workingDirectory, createIfMissing: false, out var workingDirectoryResolution);
        var commandBuilder = new GitRepositoryCommandBuilder(new GitRepositoryPath(workingDirectoryResolution.FullPath));
        var specification = CreateValidatedGitInput(
            () => commandBuilder.Show(new GitRevision(revision)),
            "The Git revision is invalid. Provide a non-empty revision without leading dashes, whitespace, or control characters.");

        return CreateGitPlan(
            toolName: ToolContractCatalog.WorkspaceGitShow,
            recipeId: "git_show",
            riskClass: "ReadOnly",
            approvalRequired: false,
            networkAllowed: false,
            mutatesWorkspace: false,
            targetPaths: [],
            workingDirectory: workingDirectoryRelative,
            workingDirectoryPath: workingDirectoryResolution.FullPath,
            spec: specification,
            timeoutSeconds: timeoutSeconds,
            stdoutLimitCharacters: 128 * 1024,
            stderrLimitCharacters: 32 * 1024);
    }

    public WorkspaceCommandPlan BuildGitAdd(string[]? paths, string? workingDirectory = null, int timeoutSeconds = 30)
    {
        var target = BuildGitPathTarget(paths, workingDirectory);
        var specification = CreateValidatedGitInput(
            () => target.CommandBuilder.Add(target.PathSpecs),
            "Git add requires at least one authorized repository-relative path.");

        return CreateGitPlan(
            toolName: ToolContractCatalog.WorkspaceGitAdd,
            recipeId: "git_add",
            riskClass: "WorkspaceMutation:Git",
            approvalRequired: true,
            networkAllowed: false,
            mutatesWorkspace: true,
            targetPaths: target.TargetPaths,
            workingDirectory: target.WorkingDirectoryRelative,
            workingDirectoryPath: target.WorkingDirectoryPath,
            spec: specification,
            timeoutSeconds: timeoutSeconds,
            stdoutLimitCharacters: 64 * 1024,
            stderrLimitCharacters: 32 * 1024);
    }

    public WorkspaceCommandPlan BuildGitUnstage(string[]? paths, string? workingDirectory = null, int timeoutSeconds = 30)
    {
        var target = BuildGitPathTarget(paths, workingDirectory);
        var specification = CreateValidatedGitInput(
            () => target.CommandBuilder.Unstage(target.PathSpecs),
            "Git unstage requires at least one authorized repository-relative path.");

        return CreateGitPlan(
            toolName: ToolContractCatalog.WorkspaceGitUnstage,
            recipeId: "git_unstage",
            riskClass: "WorkspaceMutation:Git",
            approvalRequired: true,
            networkAllowed: false,
            mutatesWorkspace: true,
            targetPaths: target.TargetPaths,
            workingDirectory: target.WorkingDirectoryRelative,
            workingDirectoryPath: target.WorkingDirectoryPath,
            spec: specification,
            timeoutSeconds: timeoutSeconds,
            stdoutLimitCharacters: 64 * 1024,
            stderrLimitCharacters: 32 * 1024);
    }

    public WorkspaceCommandPlan BuildGitCommit(string message, string? workingDirectory = null, int timeoutSeconds = 30)
    {
        var workingDirectoryRelative = pathPolicy.ResolveWorkingDirectory(workingDirectory, createIfMissing: false, out var workingDirectoryResolution);
        var commandBuilder = new GitRepositoryCommandBuilder(new GitRepositoryPath(workingDirectoryResolution.FullPath));
        var specification = CreateValidatedGitInput(
            () => commandBuilder.Commit(message),
            "Git commit requires a non-empty commit message.");

        return CreateGitPlan(
            toolName: ToolContractCatalog.WorkspaceGitCommit,
            recipeId: "git_commit",
            riskClass: "WorkspaceMutation:Git",
            approvalRequired: true,
            networkAllowed: false,
            mutatesWorkspace: true,
            targetPaths: [],
            workingDirectory: workingDirectoryRelative,
            workingDirectoryPath: workingDirectoryResolution.FullPath,
            spec: specification,
            timeoutSeconds: timeoutSeconds,
            stdoutLimitCharacters: 128 * 1024,
            stderrLimitCharacters: 32 * 1024);
    }

    public WorkspaceCommandPlan BuildGitBranchCreate(string branchName, string? workingDirectory = null, int timeoutSeconds = 30)
    {
        var workingDirectoryRelative = pathPolicy.ResolveWorkingDirectory(workingDirectory, createIfMissing: false, out var workingDirectoryResolution);
        var commandBuilder = new GitRepositoryCommandBuilder(new GitRepositoryPath(workingDirectoryResolution.FullPath));
        var specification = CreateValidatedGitInput(
            () => commandBuilder.CreateBranch(new GitBranchName(branchName)),
            "The Git branch name is invalid. Provide a non-empty branch name without leading dashes, whitespace, or control characters.");

        return CreateGitPlan(
            toolName: ToolContractCatalog.WorkspaceGitBranchCreate,
            recipeId: "git_branch_create",
            riskClass: "WorkspaceMutation:Git",
            approvalRequired: true,
            networkAllowed: false,
            mutatesWorkspace: true,
            targetPaths: [],
            workingDirectory: workingDirectoryRelative,
            workingDirectoryPath: workingDirectoryResolution.FullPath,
            spec: specification,
            timeoutSeconds: timeoutSeconds,
            stdoutLimitCharacters: 64 * 1024,
            stderrLimitCharacters: 32 * 1024);
    }

    public WorkspaceCommandPlan BuildGitSwitch(string branchName, string? workingDirectory = null, int timeoutSeconds = 30)
    {
        var workingDirectoryRelative = pathPolicy.ResolveWorkingDirectory(workingDirectory, createIfMissing: false, out var workingDirectoryResolution);
        var commandBuilder = new GitRepositoryCommandBuilder(new GitRepositoryPath(workingDirectoryResolution.FullPath));
        var specification = CreateValidatedGitInput(
            () => commandBuilder.Switch(new GitBranchName(branchName)),
            "The Git branch name is invalid. Provide a non-empty branch name without leading dashes, whitespace, or control characters.");

        return CreateGitPlan(
            toolName: ToolContractCatalog.WorkspaceGitSwitch,
            recipeId: "git_switch",
            riskClass: "WorkspaceMutation:Git",
            approvalRequired: true,
            networkAllowed: false,
            mutatesWorkspace: true,
            targetPaths: [],
            workingDirectory: workingDirectoryRelative,
            workingDirectoryPath: workingDirectoryResolution.FullPath,
            spec: specification,
            timeoutSeconds: timeoutSeconds,
            stdoutLimitCharacters: 64 * 1024,
            stderrLimitCharacters: 32 * 1024);
    }

    public WorkspaceCommandPlan BuildDotnetRestore(string? targetPath = null, string? workingDirectory = null, int timeoutSeconds = 600)
    {
        var target = BuildDotnetTarget(targetPath, workingDirectory);
        var arguments = new List<string> { "restore" };
        arguments.AddRange(target.TargetArguments);
        arguments.Add("--disable-build-servers");
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

        arguments.Add("--disable-build-servers");
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

    public WorkspaceCommandPlan BuildDotnetTest(string? targetPath = null, string configuration = "Debug", string? filter = null, bool noBuild = false, bool noRestore = false, string? workingDirectory = null, int timeoutSeconds = 300)
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
        var shouldWaitForHttp = waitForHttp;

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

        var managedUrls = ResolveManagedDotnetRunUrls(urls);
        var boundedStartupTimeoutSeconds = Math.Clamp(startupTimeoutSeconds, 1, 600);
        var artifactPaths = BuildDotnetRunArtifactPaths();
        var managedArguments = new List<string>
        {
            "run",
            "--project",
            target.ProjectArgument,
            "--configuration",
            normalizedConfiguration,
            "--no-launch-profile"
        };
        if (noBuild)
        {
            managedArguments.Add("--no-build");
        }

        managedArguments.Add("--");
        managedArguments.Add("--urls");
        managedArguments.Add(managedUrls.ListenUrl!);
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
            executableCandidates: ["dotnet"],
            arguments: managedArguments,
            timeoutSeconds: planTimeoutSeconds,
            stdoutLimitCharacters: 128 * 1024,
            stderrLimitCharacters: 128 * 1024,
            environmentVariables: new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["ASPNETCORE_ENVIRONMENT"] = "Development",
                ["DOTNET_ENVIRONMENT"] = "Development"
            },
            dotnetRunLifecycle: new WorkspaceDotnetRunLifecyclePlan(
                managedUrls.ListenUrl!,
                managedUrls.ProbeUrl!,
                boundedStartupTimeoutSeconds,
                keepAlive,
                lifetimeScope,
                artifactPaths.StdoutLogFullPath,
                artifactPaths.StdoutLogRelativePath,
                artifactPaths.StderrLogFullPath,
                artifactPaths.StderrLogRelativePath,
                artifactPaths.StartupReceiptFullPath,
                artifactPaths.StartupReceiptRelativePath,
                artifactPaths.CleanupReceiptFullPath,
                artifactPaths.CleanupReceiptRelativePath));
    }

    public WorkspaceCommandPlan BuildDotnetStop(string startupReceiptPath, int timeoutSeconds = 30)
    {
        if (string.IsNullOrWhiteSpace(startupReceiptPath))
        {
            throw WorkspaceCommandInputException.Create(
                "workspace_dotnet_stop requires the startup.json path returned by workspace_dotnet_run.",
                "Provide the workspace-relative startup.json receipt path returned by workspace_dotnet_run.");
        }

        var startupReceipt = ResolveExistingWorkspacePath(startupReceiptPath, allowFiles: true, allowDirectories: false);
        if (!string.Equals(Path.GetFileName(startupReceipt.FullPath), "startup.json", StringComparison.OrdinalIgnoreCase))
        {
            throw WorkspaceCommandInputException.Create(
                $"workspace_dotnet_stop requires a startup.json receipt produced by workspace_dotnet_run. Received '{startupReceipt.RelativePath}'.",
                "The supplied path is not a startup.json receipt. Use the receipt returned by workspace_dotnet_run.");
        }

        var boundedTimeoutSeconds = Math.Clamp(timeoutSeconds, 1, 120);
        var artifactPaths = BuildDotnetStopArtifactPaths(startupReceipt);
        return CreatePlan(
            toolName: "workspace_dotnet_stop",
            recipeId: "dotnet_stop",
            riskClass: "LocalExecution",
            approvalRequired: false,
            networkAllowed: false,
            mutatesWorkspace: true,
            targetPaths: new[] { startupReceipt.RelativePath }.Concat(artifactPaths.TargetPaths).ToArray(),
            workingDirectory: artifactPaths.WorkingDirectoryRelative,
            workingDirectoryPath: artifactPaths.WorkingDirectoryPath,
            executableCandidates: [],
            arguments: [],
            timeoutSeconds: boundedTimeoutSeconds,
            stdoutLimitCharacters: 64 * 1024,
            stderrLimitCharacters: 64 * 1024,
            dotnetStopLifecycle: new WorkspaceDotnetStopLifecyclePlan(
                startupReceipt.FullPath,
                startupReceipt.RelativePath,
                artifactPaths.CleanupReceiptFullPath,
                artifactPaths.CleanupReceiptRelativePath));
    }

    public WorkspaceCommandPlan BuildDotnetNew(
        string template,
        string name,
        string? parentDirectory = null,
        bool force = false,
        int timeoutSeconds = 300,
        string? targetFramework = null)
    {
        if (string.IsNullOrWhiteSpace(template))
        {
            throw WorkspaceCommandInputException.Create(
                "Provide a template name.",
                "Provide an approved .NET template name.");
        }

        var templateSpec = ParseDotnetTemplateSpec(template);
        var normalizedTemplate = templateSpec.Template;
        if (!WorkspaceDotnetNewTemplateCatalog.IsApprovedTemplate(normalizedTemplate))
        {
            throw WorkspaceCommandInputException.Create(
                $"Template '{template}' is not approved.",
                $"The requested .NET template is not approved. Allowed templates: {string.Join(", ", WorkspaceDotnetNewTemplateCatalog.ApprovedTemplates.OrderBy(item => item, StringComparer.OrdinalIgnoreCase))}.");
        }

        var unapprovedTemplateOption = templateSpec.Options.FirstOrDefault(
            option => !WorkspaceDotnetNewTemplateCatalog.IsApprovedTemplateOption(normalizedTemplate, option));
        if (unapprovedTemplateOption is not null)
        {
            var supportedOptions = WorkspaceDotnetNewTemplateCatalog.GetApprovedTemplateOptions(normalizedTemplate);
            throw WorkspaceCommandInputException.Create(
                $"Template option '{unapprovedTemplateOption}' is not approved for template '{normalizedTemplate}' in workspace_dotnet_new.",
                $"The requested template option is not approved. Allowed template options: {FormatTemplateOptions(supportedOptions)}.");
        }

        if (!WorkspaceDotnetNewTemplateCatalog.TryNormalizeTargetFramework(targetFramework, out var normalizedTargetFramework))
        {
            throw WorkspaceCommandInputException.Create(
                "workspace_dotnet_new targetFramework must be a supported target-framework value.",
                "workspace_dotnet_new targetFramework must be a supported value such as 'net8.0'.");
        }

        string trimmedName = name?.Trim() ?? string.Empty;
        if (trimmedName.Length == 0 || !PortablePhysicalFileNamePolicy.IsPortable(trimmedName))
        {
            throw WorkspaceCommandInputException.Create(
                "Provide a project name without path separators or invalid file-name characters.",
                "Provide a project name without path separators or invalid file-name characters.");
        }

        var workingDirectoryRelative = pathPolicy.ResolveWorkingDirectory(parentDirectory, createIfMissing: true, out var workingDirectoryResolution);
        if (AllowedProjectExtensions.Contains(Path.GetExtension(workingDirectoryRelative)))
        {
            throw WorkspaceCommandInputException.Create(
                $"workspace_dotnet_new parentDirectory '{workingDirectoryRelative}' ends with a project-file extension.",
                "workspace_dotnet_new parentDirectory must identify a directory. Pass the containing directory and project name separately.");
        }

        var isSolutionTemplate = WorkspaceDotnetNewTemplateCatalog.IsSolutionTemplate(normalizedTemplate);
        if (InspectTopLevelProjectFiles(
                workingDirectoryResolution.FullPath,
                workingDirectoryRelative,
                includeSolutionFiles: !isSolutionTemplate))
        {
            throw WorkspaceCommandInputException.Create(
                $"workspace_dotnet_new is not allowed inside existing .NET project directory '{workingDirectoryRelative}'.",
                "workspace_dotnet_new cannot scaffold inside an existing .NET project directory. Repair that project in place or choose its parent directory for a sibling project.");
        }

        var targetRelativePath = WorkspacePathPolicy.NormalizeRelativePath(Path.Combine(workingDirectoryRelative == "." ? string.Empty : workingDirectoryRelative, trimmedName));
        var targetPaths = BuildDotnetNewTargetPaths(normalizedTemplate, workingDirectoryRelative, trimmedName, targetRelativePath);
        var targetResolution = pathPolicy.ResolveAccessiblePath(targetRelativePath);
        var targetFullPath = targetResolution.FullPath;
        var targetDirectoryExists = DirectoryExistsForMutationGuard(targetFullPath, targetRelativePath);
        if (targetDirectoryExists &&
            InspectProjectTree(targetFullPath, targetRelativePath))
        {
            throw WorkspaceCommandInputException.Create(
                $"workspace_dotnet_new target '{targetRelativePath}' already contains a .NET project or solution file.",
                "The requested target already contains a .NET project or solution. Inspect and repair the existing scaffold instead of re-scaffolding it.");
        }

        if (force &&
            targetDirectoryExists &&
            DirectoryHasEntries(targetFullPath, targetRelativePath))
        {
            throw WorkspaceCommandInputException.Create(
                $"workspace_dotnet_new --force is not allowed over existing non-empty target '{targetRelativePath}'.",
                "workspace_dotnet_new --force cannot replace a non-empty target. Repair it in place or explicitly remove the intended target before retrying.");
        }

        var arguments = new List<string>
        {
            "new",
            normalizedTemplate
        };
        arguments.AddRange(templateSpec.Options);
        if (!string.IsNullOrWhiteSpace(normalizedTargetFramework))
        {
            arguments.Add("--framework");
            arguments.Add(normalizedTargetFramework);
        }
        arguments.Add("-n");
        arguments.Add(trimmedName);
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
        if (!WorkspaceDotnetNewTemplateCatalog.IsSolutionTemplate(template))
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

    private static DotnetNewTemplateSpec ParseDotnetTemplateSpec(string template)
    {
        var tokens = Regex.Split(template.Trim(), @"\s+")
            .Where(token => !string.IsNullOrWhiteSpace(token))
            .ToArray();

        if (tokens.Length == 0)
        {
            throw WorkspaceCommandInputException.Create(
                "Provide a template name.",
                "Provide an approved .NET template name.");
        }

        var optionWithoutPrefix = tokens
            .Skip(1)
            .FirstOrDefault(token => !token.StartsWith("-", StringComparison.Ordinal));

        if (optionWithoutPrefix is not null)
        {
            throw WorkspaceCommandInputException.Create(
                $"Template argument '{optionWithoutPrefix}' is not approved for workspace_dotnet_new.",
                "A positional template argument is not approved. Pass only approved option flags after the template name.");
        }

        return new DotnetNewTemplateSpec(tokens[0], tokens.Skip(1).ToArray());
    }

    private static string FormatTemplateOptions(IReadOnlyList<string> options)
        => options.Count == 0
            ? "none"
            : string.Join(", ", options.OrderBy(item => item, StringComparer.OrdinalIgnoreCase));

    private bool InspectProjectTree(string directory, string displayPath)
    {
        try
        {
            return projectTreeInspector(directory);
        }
        catch (UnauthorizedAccessException)
        {
            throw WorkspaceToolAccessDeniedException.InaccessiblePath(displayPath);
        }
        catch (IOException)
        {
            throw WorkspaceToolAccessDeniedException.InaccessiblePath(displayPath);
        }
    }

    private bool InspectTopLevelProjectFiles(
        string directory,
        string displayPath,
        bool includeSolutionFiles)
    {
        try
        {
            return ContainsTopLevelProjectFile(directory, includeSolutionFiles);
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException)
        {
            throw WorkspaceToolAccessDeniedException.InaccessiblePath(displayPath);
        }
    }

    private static bool DirectoryHasEntries(string directory, string displayPath)
    {
        try
        {
            return Directory.EnumerateFileSystemEntries(directory).Any();
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException)
        {
            throw WorkspaceToolAccessDeniedException.InaccessiblePath(displayPath);
        }
    }

    private static bool DirectoryExistsForMutationGuard(string directory, string displayPath)
    {
        try
        {
            return File.GetAttributes(directory).HasFlag(FileAttributes.Directory);
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return false;
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException)
        {
            throw WorkspaceToolAccessDeniedException.InaccessiblePath(displayPath);
        }
    }

    private bool ContainsProjectFileInAccessibleTree(string directory)
    {
        var physicalPathPolicy = pathPolicy.GetPhysicalPathPolicy(directory);
        var pendingDirectories = new Stack<string>();
        pendingDirectories.Push(directory);

        while (pendingDirectories.TryPop(out var currentDirectory))
        {
            var entries = Directory.EnumerateFileSystemEntries(
                    currentDirectory,
                    "*",
                    new EnumerationOptions
                    {
                        RecurseSubdirectories = false,
                        IgnoreInaccessible = false,
                        AttributesToSkip = 0
                    })
                .OrderBy(
                    entry => NormalizeEnumerationKey(Path.GetRelativePath(directory, entry)),
                    StringComparer.Ordinal)
                .ThenBy(entry => entry, StringComparer.Ordinal)
                .ToArray();
            var childDirectories = new List<string>();
            foreach (var entry in entries)
            {
                physicalPathPolicy.EnsureSafePath(entry);
                var attributes = File.GetAttributes(entry);
                if (attributes.HasFlag(FileAttributes.Directory))
                {
                    childDirectories.Add(entry);
                    continue;
                }

                if (AllowedProjectExtensions.Contains(Path.GetExtension(entry)))
                {
                    return true;
                }
            }

            for (var index = childDirectories.Count - 1; index >= 0; index--)
            {
                pendingDirectories.Push(childDirectories[index]);
            }
        }

        return false;
    }

    private bool ContainsTopLevelProjectFile(
        string directory,
        bool includeSolutionFiles = true)
    {
        var physicalPathPolicy = pathPolicy.GetPhysicalPathPolicy(directory);
        return Directory.EnumerateFiles(directory, "*.*", SearchOption.TopDirectoryOnly)
            .OrderBy(Path.GetFileName, StringComparer.Ordinal)
            .ThenBy(path => path, StringComparer.Ordinal)
            .Any(path =>
            {
                physicalPathPolicy.EnsureSafePath(path);
                return IsProjectFileExtension(Path.GetExtension(path), includeSolutionFiles);
            });
    }

    private static bool IsProjectFileExtension(
        string extension,
        bool includeSolutionFiles)
    {
        if (!AllowedProjectExtensions.Contains(extension))
        {
            return false;
        }

        return includeSolutionFiles || !IsSolutionExtension(extension);
    }

    private static bool IsSolutionExtension(string extension)
        => string.Equals(extension, ".sln", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(extension, ".slnx", StringComparison.OrdinalIgnoreCase);

    private static string NormalizeEnumerationKey(string path)
        => path
            .Replace(Path.DirectorySeparatorChar, '/')
            .Replace(Path.AltDirectorySeparatorChar, '/');

    public WorkspaceCommandPlan BuildPythonRunFile(string path, string[]? arguments = null, string? workingDirectory = null, int timeoutSeconds = 300, string? sideEffectManifest = null)
    {
        var scriptResolution = ResolveExistingWorkspacePath(path, allowFiles: true, allowDirectories: false);
        if (!string.Equals(Path.GetExtension(scriptResolution.FullPath), ".py", StringComparison.OrdinalIgnoreCase))
        {
            throw WorkspaceCommandInputException.Create(
                $"Python runner path '{scriptResolution.RelativePath}' does not use a .py extension.",
                "Python runner only accepts an existing .py file. Correct the script path and retry.");
        }

        var workingDirectoryRelative = ResolveScriptWorkingDirectory(workingDirectory, scriptResolution.FullPath, allowedExternalRoots: null, out var workingDirectoryPath);
        var normalizedArguments = new List<string> { scriptResolution.FullPath };
        normalizedArguments.AddRange(NormalizeScriptArguments(arguments));
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
            stderrLimitCharacters: 64 * 1024,
            declaredSideEffectMode: ResolveDeclaredScriptSideEffectMode(sideEffectManifest));
    }

    public WorkspaceCommandPlan BuildPowerShellRunScript(string path, string[]? arguments = null, string[]? outputPaths = null, string? workingDirectory = null, int timeoutSeconds = 300, string? sideEffectManifest = null)
    {
        var scriptResolution = ResolveExistingWorkspacePath(path, allowFiles: true, allowDirectories: false);
        if (!string.Equals(Path.GetExtension(scriptResolution.FullPath), ".ps1", StringComparison.OrdinalIgnoreCase))
        {
            throw WorkspaceCommandInputException.Create(
                $"PowerShell runner path '{scriptResolution.RelativePath}' does not use a .ps1 extension.",
                "PowerShell runner only accepts an existing .ps1 file. Correct the script path and retry.");
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
        normalizedArguments.AddRange(NormalizeScriptArguments(arguments));
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
            stderrLimitCharacters: 64 * 1024,
            declaredSideEffectMode: ResolveDeclaredScriptSideEffectMode(sideEffectManifest));
    }

    private static ToolExecutionSideEffectMode ResolveDeclaredScriptSideEffectMode(string? sideEffectManifest)
    {
        if (string.IsNullOrWhiteSpace(sideEffectManifest))
        {
            return ToolExecutionSideEffectMode.Unspecified;
        }

        if (!GovernedScriptSideEffectManifest.TryParse(
                sideEffectManifest,
                out var manifest,
                out var failureMessage))
        {
            throw WorkspaceCommandInputException.Create(
                failureMessage,
                "The script side-effect manifest is invalid. Provide a supported structured side-effect declaration and retry.");
        }

        return manifest.Mode switch
        {
            GovernedScriptSideEffectMode.NoMutation => ToolExecutionSideEffectMode.NoMutation,
            GovernedScriptSideEffectMode.ManagedProcessArtifacts => ToolExecutionSideEffectMode.ManagedProcessArtifacts,
            GovernedScriptSideEffectMode.ExternalArtifactDestination => ToolExecutionSideEffectMode.ExternalArtifactDestination,
            GovernedScriptSideEffectMode.ProductMutation => ToolExecutionSideEffectMode.ProductMutation,
            _ => throw new InvalidOperationException(
                $"Unsupported governed script side-effect mode '{manifest.Mode}'.")
        };
    }

    private static void ValidatePowerShellRunScriptIsBounded(WorkspacePathResolution scriptResolution)
    {
        var scriptText = File.ReadAllText(scriptResolution.FullPath);
        if (!LooksLikeForegroundBrowserHost(scriptText) ||
            LaunchesLongRunningHostAsChildProcess(scriptText))
        {
            return;
        }

        throw WorkspaceCommandInputException.Create(
            $"PowerShell runner script '{scriptResolution.RelativePath}' appears to start a foreground long-running browser host.",
            "PowerShell runner scripts must not run a foreground long-running browser host. Start it as a background child process, record its URL and process id, and let the helper script exit.");
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

    public WorkspaceCommandPlan BuildInspectSpreadsheetPreview(string path, int maxRows = 8, int maxColumns = 8, int timeoutSeconds = 300)
    {
        var sourceResolution = ResolveExistingWorkspacePath(path, allowFiles: true, allowDirectories: false);
        var extension = Path.GetExtension(sourceResolution.FullPath);
        if (!AllowedSpreadsheetExtensions.Contains(extension))
        {
            throw WorkspaceCommandInputException.Create(
                $"Spreadsheet inspector path '{sourceResolution.RelativePath}' uses unsupported extension '{extension}'.",
                "Spreadsheet inspector supports existing .xls, .xlsx, .csv, and .tsv files. Correct the path and retry.");
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
            _ => throw WorkspaceCommandInputException.Create(
                $"Skill script '{resolution.DisplayPath}' uses unsupported extension '{extension}'.",
                "Skill scripts must use a supported .py, .ps1, .sh, or .js extension.")
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
        int stderrLimitCharacters,
        ToolExecutionSideEffectMode declaredSideEffectMode = ToolExecutionSideEffectMode.Unspecified,
        IReadOnlyDictionary<string, string?>? environmentVariables = null,
        WorkspaceDotnetRunLifecyclePlan? dotnetRunLifecycle = null,
        WorkspaceDotnetStopLifecyclePlan? dotnetStopLifecycle = null)
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
            StderrLimitCharacters: stderrLimitCharacters,
            EnvironmentVariables: environmentVariables,
            DeclaredSideEffectMode: declaredSideEffectMode,
            DotnetRunLifecycle: dotnetRunLifecycle,
            DotnetStopLifecycle: dotnetStopLifecycle);
    }

    private WorkspaceCommandPlan CreateGitPlan(
        string toolName,
        string recipeId,
        string riskClass,
        bool approvalRequired,
        bool networkAllowed,
        bool mutatesWorkspace,
        IReadOnlyList<string> targetPaths,
        string workingDirectory,
        string workingDirectoryPath,
        GitCommandSpec spec,
        int timeoutSeconds,
        int stdoutLimitCharacters,
        int stderrLimitCharacters)
    {
        return CreatePlan(
            toolName,
            recipeId,
            riskClass,
            approvalRequired,
            networkAllowed,
            mutatesWorkspace,
            targetPaths,
            workingDirectory,
            workingDirectoryPath,
            executableCandidates: [spec.Executable],
            arguments: spec.Arguments.Select(argument => argument.Value).ToArray(),
            timeoutSeconds,
            stdoutLimitCharacters,
            stderrLimitCharacters);
    }

    private static GitCommandSpec CreateValidatedGitInput(
        Func<GitCommandSpec> createSpecification,
        string safeMessage)
    {
        try
        {
            return createSpecification();
        }
        catch (ArgumentException exception)
        {
            throw WorkspaceCommandInputException.Create(
                exception.Message,
                safeMessage,
                exception);
        }
    }

    private GitPathTarget BuildGitPathTarget(string[]? paths, string? workingDirectory)
    {
        var workingDirectoryRelative = pathPolicy.ResolveWorkingDirectory(workingDirectory, createIfMissing: false, out var workingDirectoryResolution);
        var repositoryPath = new GitRepositoryPath(workingDirectoryResolution.FullPath);
        var commandBuilder = new GitRepositoryCommandBuilder(repositoryPath);
        var resolvedPaths = (paths ?? [])
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => ResolveWorkspacePath(path))
            .DistinctBy(path => path.RelativePath, ExternalTargetAliasCodec.EqualityComparer)
            .ToArray();
        var pathSpecs = resolvedPaths
            .Select(path => BuildGitPathSpec(repositoryPath, path))
            .ToArray();

        return new GitPathTarget(
            commandBuilder,
            workingDirectoryResolution.FullPath,
            workingDirectoryRelative,
            pathSpecs,
            resolvedPaths.Select(path => path.RelativePath).ToArray());
    }

    private GitPathSpec BuildGitPathSpec(
        GitRepositoryPath repositoryPath,
        WorkspacePathResolution resolution)
    {
        var authorization = GitPathAuthorizer.Authorize(
            repositoryPath,
            resolution.FullPath,
            pathPolicy.PhysicalPathPolicyFactory);
        if (!authorization.IsAuthorized || authorization.Path is null)
        {
            throw WorkspaceCommandInputException.Create(
                authorization.ErrorMessage ?? "Git path is not authorized.",
                "The Git path is not an authorized repository-relative path. Choose a path inside the current repository.");
        }

        return authorization.Path;
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
            throw WorkspaceCommandInputException.Create(
                "workspace_dotnet_run requires a project file target.",
                "workspace_dotnet_run requires an existing .csproj, .fsproj, or .vbproj target.");
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
        var stamp = $"{DateTimeOffset.UtcNow.UtcDateTime:yyyyMMdd-HHmmssfff}-{Guid.NewGuid():N}";
        var relativeDirectory = pathPolicy.WorkspaceScope.CombineArtifactPath("process-runs", "dotnet-run", stamp);
        var fullDirectory = Path.GetFullPath(Path.Combine(pathPolicy.WorkspaceRoot, relativeDirectory.Replace('/', Path.DirectorySeparatorChar)));
        var stdoutRelativePath = WorkspacePathPolicy.NormalizeRelativePath(Path.Combine(relativeDirectory, "app.stdout.log"));
        var stderrRelativePath = WorkspacePathPolicy.NormalizeRelativePath(Path.Combine(relativeDirectory, "app.stderr.log"));
        var startupReceiptRelativePath = WorkspacePathPolicy.NormalizeRelativePath(Path.Combine(relativeDirectory, "startup.json"));
        var cleanupReceiptRelativePath = WorkspacePathPolicy.NormalizeRelativePath(Path.Combine(relativeDirectory, "cleanup.json"));

        return new DotnetRunArtifactPaths(
            StdoutLogFullPath: Path.Combine(fullDirectory, "app.stdout.log"),
            StdoutLogRelativePath: stdoutRelativePath,
            StderrLogFullPath: Path.Combine(fullDirectory, "app.stderr.log"),
            StderrLogRelativePath: stderrRelativePath,
            StartupReceiptFullPath: Path.Combine(fullDirectory, "startup.json"),
            StartupReceiptRelativePath: startupReceiptRelativePath,
            CleanupReceiptFullPath: Path.Combine(fullDirectory, "cleanup.json"),
            CleanupReceiptRelativePath: cleanupReceiptRelativePath,
            TargetPaths:
            [
                stdoutRelativePath,
                stderrRelativePath,
                startupReceiptRelativePath
            ]);
    }

    private DotnetStopArtifactPaths BuildDotnetStopArtifactPaths(WorkspacePathResolution startupReceipt)
    {
        var directoryFullPath = Path.GetDirectoryName(startupReceipt.FullPath)
            ?? throw new InvalidOperationException($"Could not resolve the startup receipt directory for '{startupReceipt.RelativePath}'.");
        var slashIndex = startupReceipt.RelativePath.LastIndexOf('/');
        var directoryRelativePath = slashIndex < 0
            ? "."
            : startupReceipt.RelativePath[..slashIndex];
        var cleanupRelativePath = WorkspacePathPolicy.NormalizeRelativePath(Path.Combine(directoryRelativePath, "cleanup.json"));

        return new DotnetStopArtifactPaths(
            WorkingDirectoryPath: directoryFullPath,
            WorkingDirectoryRelative: pathPolicy.ToDisplayPath(directoryFullPath),
            CleanupReceiptFullPath: Path.Combine(directoryFullPath, "cleanup.json"),
            CleanupReceiptRelativePath: cleanupRelativePath,
            TargetPaths:
            [
                cleanupRelativePath
            ]);
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
            throw WorkspaceCommandInputException.Create(
                "workspace_dotnet_run url must be an absolute http:// or https:// loopback URL.",
                "workspace_dotnet_run url must be an absolute http:// or https:// loopback URL.");
        }

        if (!IsLoopbackHost(uri.Host))
        {
            throw WorkspaceCommandInputException.Create(
                "workspace_dotnet_run only accepts loopback URLs.",
                "workspace_dotnet_run only accepts loopback URLs such as http://127.0.0.1:<port> or http://localhost:<port>.");
        }

        return new DotnetRunUrls(
            ListenUrl: uri.GetLeftPart(UriPartial.Authority),
            ProbeUrl: trimmed);
    }

    private static DotnetRunUrls ResolveManagedDotnetRunUrls(DotnetRunUrls requested)
    {
        if (string.IsNullOrWhiteSpace(requested.ListenUrl))
        {
            var reservedPort = ReserveLoopbackPort();
            var url = $"http://127.0.0.1:{reservedPort}";
            return new DotnetRunUrls(url, url);
        }

        var listenUri = new Uri(requested.ListenUrl, UriKind.Absolute);
        if (listenUri.Port != 0)
        {
            return requested;
        }

        var dynamicPort = ReserveLoopbackPort();
        var concreteListenUrl = new UriBuilder(listenUri)
        {
            Port = dynamicPort
        }.Uri.GetLeftPart(UriPartial.Authority);
        var concreteProbeUrl = string.IsNullOrWhiteSpace(requested.ProbeUrl)
            ? concreteListenUrl
            : new UriBuilder(new Uri(requested.ProbeUrl, UriKind.Absolute))
            {
                Port = dynamicPort
            }.Uri.AbsoluteUri;
        return new DotnetRunUrls(concreteListenUrl, concreteProbeUrl);
    }

    private static int ReserveLoopbackPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        try
        {
            listener.Start();
            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }
        finally
        {
            listener.Stop();
        }
    }

    private static bool IsLoopbackHost(string host)
    {
        if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return IPAddress.TryParse(host, out var address) && IPAddress.IsLoopback(address);
    }

    private string ResolveOutputWorkspacePath(string outputPath, out string outputFullPath)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            throw WorkspaceCommandInputException.Create(
                "Provide a workspace-relative output path.",
                "Provide a non-empty workspace-relative output path.");
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

        throw WorkspaceCommandInputException.Create(
            $"Path '{resolution.RelativePath}' does not exist.",
            "The requested command input path does not exist. Correct the path and retry.");
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
                throw WorkspaceCommandInputException.Create(
                    $"{invalidTargetMessage} '{resolution.RelativePath}' uses '{extension}'.",
                    $"{invalidTargetMessage} Correct the target path and retry.");
            }

            return resolution;
        }

        var physicalPathPolicy = pathPolicy.GetPhysicalPathPolicy(resolution.FullPath);
        var candidates = Directory.EnumerateFiles(resolution.FullPath, "*", SearchOption.TopDirectoryOnly)
            .Where(file => allowedExtensions.Contains(Path.GetExtension(file)))
            .Select(file =>
            {
                physicalPathPolicy.EnsureSafePath(file);
                var fullPath = Path.GetFullPath(file);
                return new
                {
                    FullPath = fullPath,
                    DisplayPath = pathPolicy.ToDisplayPath(fullPath),
                    Extension = Path.GetExtension(fullPath)
                };
            })
            .OrderBy(item => item.DisplayPath, StringComparer.Ordinal)
            .ThenBy(item => item.FullPath, StringComparer.Ordinal)
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
                throw WorkspaceCommandInputException.Create(
                    $"Directory '{resolution.RelativePath}' contains multiple solution files.",
                    $"The target directory contains multiple solution files. Pass one explicit solution or project file to {recipeName}.");
            }
        }

        if (candidates.Count == 1)
        {
            return ResolveExistingWorkspacePath(candidates[0].DisplayPath, allowFiles: true, allowDirectories: false);
        }

        if (candidates.Count == 0)
        {
            throw WorkspaceCommandInputException.Create(
                $"Directory '{resolution.RelativePath}' does not contain a supported .NET solution or project file for {recipeName}.",
                $"The target directory does not contain a supported .NET solution or project file for {recipeName}. Correct the target path and retry.");
        }

        throw WorkspaceCommandInputException.Create(
            $"Directory '{resolution.RelativePath}' contains multiple .NET project files.",
            $"The target directory contains multiple .NET project files. Pass one explicit project file to {recipeName}.");
    }

    private WorkspacePathResolution ResolveWorkspacePath(string path)
    {
        if (!pathPolicy.TryResolveWorkspacePath(path, allowWorkspaceRoot: false, out var resolution, out var validationMessage))
        {
            throw WorkspaceCommandInputException.Create(
                validationMessage,
                "The command path is invalid or outside the allowed workspace scope. Correct the path and retry.");
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
            .Distinct(ExternalTargetAliasCodec.EqualityComparer)
            .ToList();
    }

    private WorkspacePathResolution ResolveExistingPath(string path, bool allowFiles, bool allowDirectories, IReadOnlyList<string>? allowedExternalRoots = null)
        => pathPolicy.ResolveExistingPath(path, allowFiles, allowDirectories, allowedExternalRoots);

    private IReadOnlyList<string> NormalizeScriptArguments(string[]? arguments)
        => NormalizeStructuredArguments(arguments)
            .Select(NormalizeScriptArgument)
            .ToArray();

    private string NormalizeScriptArgument(string argument)
    {
        if (!WorkspaceScriptArgumentPathParser.TryParse(argument, out var candidate))
        {
            return argument;
        }

        if (WorkspaceScriptArgumentPathParser.ContainsParentTraversal(candidate.Path))
        {
            throw WorkspaceCommandInputException.Create(
                "Script argument paths cannot contain parent traversal segments ('..').",
                "Script argument paths cannot contain parent traversal segments ('..'). Use a canonical workspace or external-target path.");
        }

        if (!pathPolicy.TryResolveWorkspacePath(
                candidate.Path,
                allowWorkspaceRoot: false,
                out var resolution,
                out var validationMessage))
        {
            throw WorkspaceCommandInputException.Create(
                $"Script argument path is not allowed. {validationMessage}",
                "The script argument path is outside the allowed workspace scope. Use a canonical workspace or authorized external-target path.");
        }

        return candidate.ReplacePath(resolution.FullPath);
    }

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
        string StdoutLogFullPath,
        string StdoutLogRelativePath,
        string StderrLogFullPath,
        string StderrLogRelativePath,
        string StartupReceiptFullPath,
        string StartupReceiptRelativePath,
        string CleanupReceiptFullPath,
        string CleanupReceiptRelativePath,
        IReadOnlyList<string> TargetPaths);

    private sealed record DotnetStopArtifactPaths(
        string WorkingDirectoryPath,
        string WorkingDirectoryRelative,
        string CleanupReceiptFullPath,
        string CleanupReceiptRelativePath,
        IReadOnlyList<string> TargetPaths);

    private sealed record DotnetNewTemplateSpec(
        string Template,
        IReadOnlyList<string> Options);

    private sealed record DotnetRunUrls(string? ListenUrl, string? ProbeUrl);

    private sealed record GitPathTarget(
        GitRepositoryCommandBuilder CommandBuilder,
        string WorkingDirectoryPath,
        string WorkingDirectoryRelative,
        IReadOnlyList<GitPathSpec> PathSpecs,
        IReadOnlyList<string> TargetPaths);
}
