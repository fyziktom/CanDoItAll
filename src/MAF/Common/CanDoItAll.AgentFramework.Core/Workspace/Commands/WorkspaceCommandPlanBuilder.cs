using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Git;

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
            spec: commandBuilder.Log(count),
            timeoutSeconds: timeoutSeconds,
            stdoutLimitCharacters: 128 * 1024,
            stderrLimitCharacters: 32 * 1024);
    }

    public WorkspaceCommandPlan BuildGitShow(string revision, string? workingDirectory = null, int timeoutSeconds = 30)
    {
        var workingDirectoryRelative = pathPolicy.ResolveWorkingDirectory(workingDirectory, createIfMissing: false, out var workingDirectoryResolution);
        var commandBuilder = new GitRepositoryCommandBuilder(new GitRepositoryPath(workingDirectoryResolution.FullPath));

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
            spec: commandBuilder.Show(new GitRevision(revision)),
            timeoutSeconds: timeoutSeconds,
            stdoutLimitCharacters: 128 * 1024,
            stderrLimitCharacters: 32 * 1024);
    }

    public WorkspaceCommandPlan BuildGitAdd(string[]? paths, string? workingDirectory = null, int timeoutSeconds = 30)
    {
        var target = BuildGitPathTarget(paths, workingDirectory);

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
            spec: target.CommandBuilder.Add(target.PathSpecs),
            timeoutSeconds: timeoutSeconds,
            stdoutLimitCharacters: 64 * 1024,
            stderrLimitCharacters: 32 * 1024);
    }

    public WorkspaceCommandPlan BuildGitUnstage(string[]? paths, string? workingDirectory = null, int timeoutSeconds = 30)
    {
        var target = BuildGitPathTarget(paths, workingDirectory);

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
            spec: target.CommandBuilder.Unstage(target.PathSpecs),
            timeoutSeconds: timeoutSeconds,
            stdoutLimitCharacters: 64 * 1024,
            stderrLimitCharacters: 32 * 1024);
    }

    public WorkspaceCommandPlan BuildGitCommit(string message, string? workingDirectory = null, int timeoutSeconds = 30)
    {
        var workingDirectoryRelative = pathPolicy.ResolveWorkingDirectory(workingDirectory, createIfMissing: false, out var workingDirectoryResolution);
        var commandBuilder = new GitRepositoryCommandBuilder(new GitRepositoryPath(workingDirectoryResolution.FullPath));

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
            spec: commandBuilder.Commit(message),
            timeoutSeconds: timeoutSeconds,
            stdoutLimitCharacters: 128 * 1024,
            stderrLimitCharacters: 32 * 1024);
    }

    public WorkspaceCommandPlan BuildGitBranchCreate(string branchName, string? workingDirectory = null, int timeoutSeconds = 30)
    {
        var workingDirectoryRelative = pathPolicy.ResolveWorkingDirectory(workingDirectory, createIfMissing: false, out var workingDirectoryResolution);
        var commandBuilder = new GitRepositoryCommandBuilder(new GitRepositoryPath(workingDirectoryResolution.FullPath));

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
            spec: commandBuilder.CreateBranch(new GitBranchName(branchName)),
            timeoutSeconds: timeoutSeconds,
            stdoutLimitCharacters: 64 * 1024,
            stderrLimitCharacters: 32 * 1024);
    }

    public WorkspaceCommandPlan BuildGitSwitch(string branchName, string? workingDirectory = null, int timeoutSeconds = 30)
    {
        var workingDirectoryRelative = pathPolicy.ResolveWorkingDirectory(workingDirectory, createIfMissing: false, out var workingDirectoryResolution);
        var commandBuilder = new GitRepositoryCommandBuilder(new GitRepositoryPath(workingDirectoryResolution.FullPath));

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
            spec: commandBuilder.Switch(new GitBranchName(branchName)),
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

        var boundedStartupTimeoutSeconds = Math.Clamp(startupTimeoutSeconds, 1, 600);
        var artifactPaths = BuildDotnetRunArtifactPaths();
        var script = BuildDotnetHttpRunPowerShellScript(
            target.ProjectArgument,
            target.WorkingDirectoryPath,
            pathPolicy.WorkspaceRoot,
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

    public WorkspaceCommandPlan BuildDotnetStop(string startupReceiptPath, int timeoutSeconds = 30)
    {
        if (string.IsNullOrWhiteSpace(startupReceiptPath))
        {
            throw new InvalidOperationException("workspace_dotnet_stop requires the startup.json path returned by workspace_dotnet_run.");
        }

        var startupReceipt = ResolveExistingWorkspacePath(startupReceiptPath, allowFiles: true, allowDirectories: false);
        if (!string.Equals(Path.GetFileName(startupReceipt.FullPath), "startup.json", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"workspace_dotnet_stop requires a startup.json receipt produced by workspace_dotnet_run. Received '{startupReceipt.RelativePath}'.");
        }

        var boundedTimeoutSeconds = Math.Clamp(timeoutSeconds, 1, 120);
        var artifactPaths = BuildDotnetStopArtifactPaths(startupReceipt);
        var script = BuildDotnetStopPowerShellScript(
            startupReceipt.FullPath,
            artifactPaths.CleanupReceiptFullPath);
        WriteDotnetRunScript(artifactPaths.ScriptFullPath, script);

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
            timeoutSeconds: boundedTimeoutSeconds,
            stdoutLimitCharacters: 64 * 1024,
            stderrLimitCharacters: 64 * 1024);
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
            throw new InvalidOperationException("Provide a template name.");
        }

        var templateSpec = ParseDotnetTemplateSpec(template);
        var normalizedTemplate = templateSpec.Template;
        if (!WorkspaceDotnetNewTemplateCatalog.IsApprovedTemplate(normalizedTemplate))
        {
            throw new InvalidOperationException($"Template '{template}' is not approved. Allowed templates: {string.Join(", ", WorkspaceDotnetNewTemplateCatalog.ApprovedTemplates.OrderBy(item => item, StringComparer.OrdinalIgnoreCase))}.");
        }

        var unapprovedTemplateOption = templateSpec.Options.FirstOrDefault(
            option => !WorkspaceDotnetNewTemplateCatalog.IsApprovedTemplateOption(normalizedTemplate, option));
        if (unapprovedTemplateOption is not null)
        {
            var supportedOptions = WorkspaceDotnetNewTemplateCatalog.GetApprovedTemplateOptions(normalizedTemplate);
            throw new InvalidOperationException(
                $"Template option '{unapprovedTemplateOption}' is not approved for template '{normalizedTemplate}' in workspace_dotnet_new. Allowed template options: {FormatTemplateOptions(supportedOptions)}.");
        }

        if (!WorkspaceDotnetNewTemplateCatalog.TryNormalizeTargetFramework(targetFramework, out var normalizedTargetFramework))
        {
            throw new InvalidOperationException(
                "workspace_dotnet_new targetFramework must be a supported target-framework value such as 'net8.0'.");
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

        var isSolutionTemplate = WorkspaceDotnetNewTemplateCatalog.IsSolutionTemplate(normalizedTemplate);
        if (InspectTopLevelProjectFiles(
                workingDirectoryResolution.FullPath,
                workingDirectoryRelative,
                includeSolutionFiles: !isSolutionTemplate))
        {
            throw new InvalidOperationException(
                $"workspace_dotnet_new is not allowed inside existing .NET project directory '{workingDirectoryRelative}'. Inspect and repair that project in place, or create a sibling project from its parent directory.");
        }

        var targetRelativePath = WorkspacePathPolicy.NormalizeRelativePath(Path.Combine(workingDirectoryRelative == "." ? string.Empty : workingDirectoryRelative, trimmedName));
        var targetPaths = BuildDotnetNewTargetPaths(normalizedTemplate, workingDirectoryRelative, trimmedName, targetRelativePath);
        var targetResolution = pathPolicy.ResolveAccessiblePath(targetRelativePath);
        var targetFullPath = targetResolution.FullPath;
        var targetDirectoryExists = DirectoryExistsForMutationGuard(targetFullPath, targetRelativePath);
        if (targetDirectoryExists &&
            InspectProjectTree(targetFullPath, targetRelativePath))
        {
            throw new InvalidOperationException(
                $"workspace_dotnet_new is not allowed for existing project target '{targetRelativePath}' because it already contains a .NET project or solution file. Inspect and repair the existing scaffold in place instead of re-scaffolding.");
        }

        if (force &&
            targetDirectoryExists &&
            DirectoryHasEntries(targetFullPath, targetRelativePath))
        {
            throw new InvalidOperationException(
                $"workspace_dotnet_new --force is not allowed over existing non-empty target '{targetRelativePath}'. Inspect and repair the existing scaffold, or explicitly delete the target directory first when replacement is intentional.");
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
            throw new InvalidOperationException("Provide a template name.");
        }

        var optionWithoutPrefix = tokens
            .Skip(1)
            .FirstOrDefault(token => !token.StartsWith("-", StringComparison.Ordinal));

        if (optionWithoutPrefix is not null)
        {
            throw new InvalidOperationException(
                $"Template argument '{optionWithoutPrefix}' is not approved for workspace_dotnet_new. Pass only approved option flags after the template name.");
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

    private static bool InspectTopLevelProjectFiles(
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

    private static bool ContainsProjectFileInAccessibleTree(string directory)
    {
        var pendingDirectories = new Stack<string>();
        pendingDirectories.Push(directory);

        while (pendingDirectories.TryPop(out var currentDirectory))
        {
            foreach (var entry in Directory.EnumerateFileSystemEntries(
                         currentDirectory,
                         "*",
                         new EnumerationOptions
                         {
                             RecurseSubdirectories = false,
                             IgnoreInaccessible = false,
                             AttributesToSkip = 0
                         }))
            {
                var attributes = File.GetAttributes(entry);
                if (attributes.HasFlag(FileAttributes.ReparsePoint))
                {
                    throw new IOException("Project-target inspection cannot cross a filesystem reparse point.");
                }

                if (attributes.HasFlag(FileAttributes.Directory))
                {
                    pendingDirectories.Push(entry);
                    continue;
                }

                if (AllowedProjectExtensions.Contains(Path.GetExtension(entry)))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool ContainsTopLevelProjectFile(
        string directory,
        bool includeSolutionFiles = true)
        => Directory.EnumerateFiles(directory, "*.*", SearchOption.TopDirectoryOnly)
            .Any(path => IsProjectFileExtension(Path.GetExtension(path), includeSolutionFiles));

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

    public WorkspaceCommandPlan BuildPythonRunFile(string path, string[]? arguments = null, string? workingDirectory = null, int timeoutSeconds = 300, string? sideEffectManifest = null)
    {
        var scriptResolution = ResolveExistingWorkspacePath(path, allowFiles: true, allowDirectories: false);
        if (!string.Equals(Path.GetExtension(scriptResolution.FullPath), ".py", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Python runner only accepts .py files. '{scriptResolution.RelativePath}' does not use a .py extension.");
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
            throw new InvalidOperationException(failureMessage);
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
        int stderrLimitCharacters,
        ToolExecutionSideEffectMode declaredSideEffectMode = ToolExecutionSideEffectMode.Unspecified)
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
            DeclaredSideEffectMode: declaredSideEffectMode);
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

    private GitPathTarget BuildGitPathTarget(string[]? paths, string? workingDirectory)
    {
        var workingDirectoryRelative = pathPolicy.ResolveWorkingDirectory(workingDirectory, createIfMissing: false, out var workingDirectoryResolution);
        var repositoryPath = new GitRepositoryPath(workingDirectoryResolution.FullPath);
        var commandBuilder = new GitRepositoryCommandBuilder(repositoryPath);
        var resolvedPaths = (paths ?? [])
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => ResolveWorkspacePath(path))
            .DistinctBy(path => path.RelativePath, StringComparer.OrdinalIgnoreCase)
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

    private static GitPathSpec BuildGitPathSpec(
        GitRepositoryPath repositoryPath,
        WorkspacePathResolution resolution)
    {
        var authorization = GitPathAuthorizer.Authorize(repositoryPath, resolution.FullPath);
        if (!authorization.IsAuthorized || authorization.Path is null)
        {
            throw new InvalidOperationException(authorization.ErrorMessage ?? "Git path is not authorized.");
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
        var stamp = $"{DateTimeOffset.UtcNow.UtcDateTime:yyyyMMdd-HHmmssfff}-{Guid.NewGuid():N}";
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

    private DotnetStopArtifactPaths BuildDotnetStopArtifactPaths(WorkspacePathResolution startupReceipt)
    {
        var directoryFullPath = Path.GetDirectoryName(startupReceipt.FullPath)
            ?? throw new InvalidOperationException($"Could not resolve the startup receipt directory for '{startupReceipt.RelativePath}'.");
        var slashIndex = startupReceipt.RelativePath.LastIndexOf('/');
        var directoryRelativePath = slashIndex < 0
            ? "."
            : startupReceipt.RelativePath[..slashIndex];
        var scriptRelativePath = WorkspacePathPolicy.NormalizeRelativePath(Path.Combine(directoryRelativePath, "stop.ps1"));
        var cleanupRelativePath = WorkspacePathPolicy.NormalizeRelativePath(Path.Combine(directoryRelativePath, "cleanup.json"));

        return new DotnetStopArtifactPaths(
            WorkingDirectoryPath: directoryFullPath,
            WorkingDirectoryRelative: pathPolicy.ToDisplayPath(directoryFullPath),
            ScriptFullPath: Path.Combine(directoryFullPath, "stop.ps1"),
            CleanupReceiptFullPath: Path.Combine(directoryFullPath, "cleanup.json"),
            TargetPaths:
            [
                scriptRelativePath,
                cleanupRelativePath
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

    private static string BuildDotnetHttpRunPowerShellScript(
        string projectPath,
        string workingDirectory,
        string workspaceRoot,
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
        builder.AppendLine("$workspaceRoot = " + ToPowerShellSingleQuotedString(workspaceRoot));
        builder.AppendLine("$configuration = " + ToPowerShellSingleQuotedString(configuration));
        builder.AppendLine("$listenUrl = " + ToPowerShellSingleQuotedString(listenUrl ?? string.Empty));
        builder.AppendLine("$probeUrl = " + ToPowerShellSingleQuotedString(probeUrl ?? string.Empty));
        builder.AppendLine("$stdoutLog = " + ToPowerShellSingleQuotedString(stdoutLogPath));
        builder.AppendLine("$stderrLog = " + ToPowerShellSingleQuotedString(stderrLogPath));
        builder.AppendLine("$startupReceipt = " + ToPowerShellSingleQuotedString(startupReceiptPath));
        builder.AppendLine("$cleanupReceipt = Join-Path (Split-Path -Parent $startupReceipt) 'cleanup.json'");
        builder.AppendLine("$startupTimeoutSeconds = " + startupTimeoutSeconds.ToString());
        builder.AppendLine("$noBuild = " + (noBuild ? "$true" : "$false"));
        builder.AppendLine("$keepAlive = " + (keepAlive ? "$true" : "$false"));
        builder.AppendLine("$lifetimeScope = " + ToPowerShellSingleQuotedString(lifetimeScope.ToString()));
        builder.AppendLine("$appProcess = $null");
        builder.AppendLine("$staticWebAssetsAliasMappings = @()");
        builder.AppendLine("function Read-LogTail {");
        builder.AppendLine("    param([string]$Path)");
        builder.AppendLine("    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { return '' }");
        builder.AppendLine("    try {");
        builder.AppendLine("        $content = Get-Content -LiteralPath $Path -Raw -ErrorAction Stop");
        builder.AppendLine("        if ($content.Length -le 4000) { return $content }");
        builder.AppendLine("        return $content.Substring($content.Length - 4000)");
        builder.AppendLine("    } catch { return '' }");
        builder.AppendLine("}");
        builder.AppendLine("function Test-DynamicPortUrl {");
        builder.AppendLine("    param([string]$Url)");
        builder.AppendLine("    if ([string]::IsNullOrWhiteSpace($Url)) { return $false }");
        builder.AppendLine("    try { return ([System.Uri]$Url).Port -eq 0 } catch { return $false }");
        builder.AppendLine("}");
        builder.AppendLine("function Resolve-ListeningUrlFromLog {");
        builder.AppendLine("    param([string]$Path)");
        builder.AppendLine("    $tail = Read-LogTail $Path");
        builder.AppendLine("    if ([string]::IsNullOrWhiteSpace($tail)) { return '' }");
        builder.AppendLine("    $matches = [System.Text.RegularExpressions.Regex]::Matches($tail, 'Now listening on:\\s*(https?://[^\\s]+)', [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)");
        builder.AppendLine("    if ($matches.Count -eq 0) { return '' }");
        builder.AppendLine("    for ($i = $matches.Count - 1; $i -ge 0; $i--) {");
        builder.AppendLine("        $candidate = $matches[$i].Groups[1].Value.TrimEnd('/')");
        builder.AppendLine("        try {");
        builder.AppendLine("            $uri = [System.Uri]$candidate");
        builder.AppendLine("            if ($uri.IsLoopback -and $uri.Port -gt 0) { return $candidate }");
        builder.AppendLine("        } catch { }");
        builder.AppendLine("    }");
        builder.AppendLine("    return ''");
        builder.AppendLine("}");
        builder.AppendLine("function Resolve-EffectiveProbeUrl {");
        builder.AppendLine("    param([string]$CurrentProbeUrl, [string]$CurrentListenUrl, [string]$StdoutPath)");
        builder.AppendLine("    if (-not [string]::IsNullOrWhiteSpace($CurrentProbeUrl) -and -not (Test-DynamicPortUrl $CurrentProbeUrl)) { return $CurrentProbeUrl }");
        builder.AppendLine("    if ([string]::IsNullOrWhiteSpace($CurrentProbeUrl) -and -not [string]::IsNullOrWhiteSpace($CurrentListenUrl) -and -not (Test-DynamicPortUrl $CurrentListenUrl)) { return $CurrentListenUrl }");
        builder.AppendLine("    $loggedUrl = Resolve-ListeningUrlFromLog $StdoutPath");
        builder.AppendLine("    if ([string]::IsNullOrWhiteSpace($loggedUrl)) { return $CurrentProbeUrl }");
        builder.AppendLine("    if (-not [string]::IsNullOrWhiteSpace($CurrentProbeUrl)) {");
        builder.AppendLine("        try {");
        builder.AppendLine("            $requested = [System.Uri]$CurrentProbeUrl");
        builder.AppendLine("            $builder = [System.UriBuilder]::new([System.Uri]$loggedUrl)");
        builder.AppendLine("            $builder.Path = $requested.AbsolutePath");
        builder.AppendLine("            $builder.Query = $requested.Query.TrimStart('?')");
        builder.AppendLine("            return $builder.Uri.AbsoluteUri.TrimEnd('/')");
        builder.AppendLine("        } catch { return $loggedUrl }");
        builder.AppendLine("    }");
        builder.AppendLine("    return $loggedUrl");
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
        builder.AppendLine("function Resolve-StaticWebAssetsAliasMappings {");
        builder.AppendLine("    param([string]$ProjectPath, [string]$WorkspaceRoot, [string]$Configuration)");
        builder.AppendLine("    if (-not ($IsWindows -eq $true -or $env:OS -eq 'Windows_NT')) { return @() }");
        builder.AppendLine("    if ([string]::IsNullOrWhiteSpace($WorkspaceRoot) -or -not (Test-Path -LiteralPath $WorkspaceRoot -PathType Container)) { return @() }");
        builder.AppendLine("    $projectDirectory = Split-Path -Parent $ProjectPath");
        builder.AppendLine("    if ([string]::IsNullOrWhiteSpace($projectDirectory) -or -not (Test-Path -LiteralPath $projectDirectory -PathType Container)) { return @() }");
        builder.AppendLine("    $manifestFiles = [System.Collections.Generic.List[object]]::new()");
        builder.AppendLine("    foreach ($rootName in @('bin', 'obj')) {");
        builder.AppendLine("        $configurationRoot = Join-Path (Join-Path $projectDirectory $rootName) $Configuration");
        builder.AppendLine("        if (-not (Test-Path -LiteralPath $configurationRoot -PathType Container)) { continue }");
        builder.AppendLine("        foreach ($manifestFile in Get-ChildItem -LiteralPath $configurationRoot -Filter '*.staticwebassets*.json' -Recurse -ErrorAction SilentlyContinue) {");
        builder.AppendLine("            [void]$manifestFiles.Add($manifestFile)");
        builder.AppendLine("        }");
        builder.AppendLine("    }");
        builder.AppendLine("    $mappings = [System.Collections.Generic.List[object]]::new()");
        builder.AppendLine("    foreach ($manifestFile in $manifestFiles) {");
        builder.AppendLine("        try {");
        builder.AppendLine("            $manifest = Get-Content -LiteralPath $manifestFile.FullName -Raw -ErrorAction Stop | ConvertFrom-Json -ErrorAction Stop");
        builder.AppendLine("        } catch {");
        builder.AppendLine("            continue");
        builder.AppendLine("        }");
        builder.AppendLine("        if (-not ($manifest.PSObject.Properties.Name -contains 'ContentRoots')) { continue }");
        builder.AppendLine("        foreach ($contentRootValue in @($manifest.ContentRoots)) {");
        builder.AppendLine("            $contentRoot = [string]$contentRootValue");
        builder.AppendLine("            if ([string]::IsNullOrWhiteSpace($contentRoot)) { continue }");
        builder.AppendLine("            if ($contentRoot -notmatch '^[A-Za-z]:[\\\\/]') { continue }");
        builder.AppendLine("            if (Test-Path -LiteralPath $contentRoot -PathType Container) { continue }");
        builder.AppendLine("            $driveLetter = [char]::ToUpperInvariant($contentRoot[0])");
        builder.AppendLine("            $suffix = $contentRoot.Substring(3).TrimStart([char[]]@('\\', '/'))");
        builder.AppendLine("            $workspaceCandidate = if ([string]::IsNullOrWhiteSpace($suffix)) { $WorkspaceRoot } else { Join-Path $WorkspaceRoot $suffix }");
        builder.AppendLine("            if (-not (Test-Path -LiteralPath $workspaceCandidate -PathType Container)) { continue }");
        builder.AppendLine("            $driveName = \"${driveLetter}:\"");
        builder.AppendLine("            if (@($mappings | Where-Object { $_.drive -eq $driveName }).Count -gt 0) { continue }");
        builder.AppendLine("            [void]$mappings.Add([pscustomobject]@{");
        builder.AppendLine("                drive = $driveName");
        builder.AppendLine("                driveLetter = [string]$driveLetter");
        builder.AppendLine("                workspaceRoot = $WorkspaceRoot");
        builder.AppendLine("                manifestPath = $manifestFile.FullName");
        builder.AppendLine("                contentRoot = $contentRoot");
        builder.AppendLine("                verifiedPath = $workspaceCandidate");
        builder.AppendLine("                mounted = $false");
        builder.AppendLine("            })");
        builder.AppendLine("        }");
        builder.AppendLine("    }");
        builder.AppendLine("    return @($mappings)");
        builder.AppendLine("}");
        builder.AppendLine("function Mount-StaticWebAssetsAliasMappings {");
        builder.AppendLine("    param([object[]]$Mappings)");
        builder.AppendLine("    if ($null -eq $Mappings -or $Mappings.Count -eq 0) { return @() }");
        builder.AppendLine("    foreach ($mapping in @($Mappings)) {");
        builder.AppendLine("        if ($null -eq $mapping) { continue }");
        builder.AppendLine("        if (Test-Path -LiteralPath $mapping.contentRoot -PathType Container) { continue }");
        builder.AppendLine("        $existingDrive = Get-PSDrive -Name $mapping.driveLetter -ErrorAction SilentlyContinue");
        builder.AppendLine("        if ($existingDrive -ne $null) {");
        builder.AppendLine("            throw \"Static web assets manifest '$($mapping.manifestPath)' requires $($mapping.drive) for '$($mapping.contentRoot)', but that drive is already assigned and does not expose the expected workspace path '$($mapping.verifiedPath)'.\"");
        builder.AppendLine("        }");
        builder.AppendLine("        $substOutput = & subst $mapping.drive $mapping.workspaceRoot 2>&1");
        builder.AppendLine("        if ($LASTEXITCODE -ne 0) {");
        builder.AppendLine("            throw \"Failed to create static web assets workspace drive alias $($mapping.drive) -> '$($mapping.workspaceRoot)'. $substOutput\"");
        builder.AppendLine("        }");
        builder.AppendLine("        if (-not (Test-Path -LiteralPath $mapping.contentRoot -PathType Container)) {");
        builder.AppendLine("            & subst $mapping.drive /d 2>$null | Out-Null");
        builder.AppendLine("            throw \"Created static web assets workspace drive alias $($mapping.drive), but manifest content root is still unavailable: $($mapping.contentRoot).\"");
        builder.AppendLine("        }");
        builder.AppendLine("        $mapping.mounted = $true");
        builder.AppendLine("    }");
        builder.AppendLine("    return @($Mappings)");
        builder.AppendLine("}");
        builder.AppendLine("function Dismount-StaticWebAssetsAliasMappings {");
        builder.AppendLine("    param([object[]]$Mappings)");
        builder.AppendLine("    if ($null -eq $Mappings -or $Mappings.Count -eq 0) { return }");
        builder.AppendLine("    foreach ($mapping in @($Mappings | Where-Object { $_.mounted })) {");
        builder.AppendLine("        if ($null -eq $mapping) { continue }");
        builder.AppendLine("        try { & subst $mapping.drive /d 2>$null | Out-Null } catch { }");
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
        builder.AppendLine("        workspaceRoot = $workspaceRoot");
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
        builder.AppendLine("        cleanupReceiptPath = $cleanupReceipt");
        builder.AppendLine("        stopTool = 'workspace_dotnet_stop'");
        builder.AppendLine("        stopToolStartupReceiptPath = $startupReceipt");
        builder.AppendLine("        staticWebAssetsAliasMappings = @($staticWebAssetsAliasMappings)");
        builder.AppendLine("        stdoutLog = $stdoutLog");
        builder.AppendLine("        stderrLog = $stderrLog");
        builder.AppendLine("        stdoutTail = Read-LogTail $stdoutLog");
        builder.AppendLine("        stderrTail = Read-LogTail $stderrLog");
        builder.AppendLine("        capturedAtUtc = [DateTimeOffset]::UtcNow.ToString('O')");
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
        builder.AppendLine("    if ([string]::IsNullOrWhiteSpace($probeUrl) -and -not (Test-DynamicPortUrl $listenUrl)) { $probeUrl = $listenUrl }");
        builder.AppendLine("    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $stdoutLog) | Out-Null");
        builder.AppendLine("    if ([string]::IsNullOrWhiteSpace($env:ASPNETCORE_ENVIRONMENT)) { $env:ASPNETCORE_ENVIRONMENT = 'Development' }");
        builder.AppendLine("    if ([string]::IsNullOrWhiteSpace($env:DOTNET_ENVIRONMENT)) { $env:DOTNET_ENVIRONMENT = 'Development' }");
        builder.AppendLine("    if ($noBuild) { $staticWebAssetsAliasMappings = Mount-StaticWebAssetsAliasMappings -Mappings @(Resolve-StaticWebAssetsAliasMappings $projectPath $workspaceRoot $configuration) }");
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
        builder.AppendLine("        $resolvedProbeUrl = Resolve-EffectiveProbeUrl $probeUrl $listenUrl $stdoutLog");
        builder.AppendLine("        if (-not [string]::IsNullOrWhiteSpace($resolvedProbeUrl) -and -not (Test-DynamicPortUrl $resolvedProbeUrl)) {");
        builder.AppendLine("            $probeUrl = $resolvedProbeUrl");
        builder.AppendLine("            if (Test-DynamicPortUrl $listenUrl) { $listenUrl = ([System.Uri]$probeUrl).GetLeftPart([System.UriPartial]::Authority) }");
        builder.AppendLine("        }");
        builder.AppendLine("        if ([string]::IsNullOrWhiteSpace($probeUrl) -or (Test-DynamicPortUrl $probeUrl)) {");
        builder.AppendLine("            $lastError = 'Waiting for dotnet run to report a concrete listening URL.'");
        builder.AppendLine("            Start-Sleep -Milliseconds 500");
        builder.AppendLine("            continue");
        builder.AppendLine("        }");
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
        builder.AppendLine("        $successJson = Write-StartupReceipt $true \"Application started and $probeUrl returned success. The process tree is still running for follow-up browser proof; call workspace_dotnet_stop with startup.json when proof is complete.\"");
        builder.AppendLine("    } else {");
        builder.AppendLine("        $processTreeIds = if ($appProcess -ne $null) { @(Resolve-ProcessTreeIds $appProcess.Id) } else { @() }");
        builder.AppendLine("        Stop-AppProcessTree $processTreeIds");
        builder.AppendLine("        Dismount-StaticWebAssetsAliasMappings $staticWebAssetsAliasMappings");
        builder.AppendLine("        $successJson = Write-StartupReceipt $true \"Application started and $probeUrl returned success. Process tree was stopped after smoke validation.\" ($processTreeIds.Count -gt 0) $processTreeIds");
        builder.AppendLine("    }");
        builder.AppendLine("    Write-Output $successJson");
        builder.AppendLine("} catch {");
        builder.AppendLine("    $message = $_.Exception.Message");
        builder.AppendLine("    $processTreeIds = if ($appProcess -ne $null) { @(Resolve-ProcessTreeIds $appProcess.Id) } else { @() }");
        builder.AppendLine("    Stop-AppProcessTree $processTreeIds");
        builder.AppendLine("    Dismount-StaticWebAssetsAliasMappings $staticWebAssetsAliasMappings");
        builder.AppendLine("    $failureJson = Write-StartupReceipt $false $message ($processTreeIds.Count -gt 0) $processTreeIds");
        builder.AppendLine("    Write-Error $failureJson");
        builder.AppendLine("    exit 1");
        builder.AppendLine("}");
        return builder.ToString();
    }

    private static string BuildDotnetStopPowerShellScript(
        string startupReceiptPath,
        string cleanupReceiptPath)
    {
        var builder = new StringBuilder();
        builder.AppendLine("$ErrorActionPreference = 'Stop'");
        builder.AppendLine("$ProgressPreference = 'SilentlyContinue'");
        builder.AppendLine("$startupReceipt = " + ToPowerShellSingleQuotedString(startupReceiptPath));
        builder.AppendLine("$cleanupReceipt = " + ToPowerShellSingleQuotedString(cleanupReceiptPath));
        builder.AppendLine("$startedAtUtc = [DateTimeOffset]::UtcNow");
        builder.AppendLine("function Test-JsonProperty {");
        builder.AppendLine("    param([object]$Value, [string]$Name)");
        builder.AppendLine("    return $null -ne $Value -and $Value.PSObject.Properties.Name -contains $Name");
        builder.AppendLine("}");
        builder.AppendLine("function Add-ProcessId {");
        builder.AppendLine("    param([System.Collections.Generic.List[int]]$Ids, [object]$Value)");
        builder.AppendLine("    if ($null -eq $Value) { return }");
        builder.AppendLine("    try {");
        builder.AppendLine("        $id = [int]$Value");
        builder.AppendLine("        if ($id -gt 0 -and -not $Ids.Contains($id)) { [void]$Ids.Add($id) }");
        builder.AppendLine("    } catch { }");
        builder.AppendLine("}");
        builder.AppendLine("function Resolve-StartupProcessIds {");
        builder.AppendLine("    param([object]$Startup)");
        builder.AppendLine("    $ids = [System.Collections.Generic.List[int]]::new()");
        builder.AppendLine("    if (Test-JsonProperty $Startup 'appProcessTreeIds') {");
        builder.AppendLine("        foreach ($processId in @($Startup.appProcessTreeIds)) { Add-ProcessId $ids $processId }");
        builder.AppendLine("    }");
        builder.AppendLine("    if (Test-JsonProperty $Startup 'appProcessId') { Add-ProcessId $ids $Startup.appProcessId }");
        builder.AppendLine("    return @($ids)");
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
        builder.AppendLine("    try {");
        builder.AppendLine("        if ($null -eq (Get-Process -Id $RootProcessId -ErrorAction SilentlyContinue)) { return @() }");
        builder.AppendLine("        Add-DescendantProcessIds $RootProcessId");
        builder.AppendLine("        if (-not $orderedIds.Contains($RootProcessId)) { [void]$orderedIds.Add($RootProcessId) }");
        builder.AppendLine("    } catch { }");
        builder.AppendLine("    return @($orderedIds)");
        builder.AppendLine("}");
        builder.AppendLine("function Resolve-ExpandedProcessTreeIds {");
        builder.AppendLine("    param([int[]]$RootProcessIds)");
        builder.AppendLine("    $ids = [System.Collections.Generic.List[int]]::new()");
        builder.AppendLine("    foreach ($processId in @($RootProcessIds)) {");
        builder.AppendLine("        foreach ($treeProcessId in @(Resolve-ProcessTreeIds $processId)) {");
        builder.AppendLine("            if ($treeProcessId -gt 0 -and -not $ids.Contains($treeProcessId)) { [void]$ids.Add($treeProcessId) }");
        builder.AppendLine("        }");
        builder.AppendLine("        if ($processId -gt 0 -and -not $ids.Contains($processId)) { [void]$ids.Add($processId) }");
        builder.AppendLine("    }");
        builder.AppendLine("    return @($ids)");
        builder.AppendLine("}");
        builder.AppendLine("function Stop-AppProcessTree {");
        builder.AppendLine("    param([int[]]$ProcessIds)");
        builder.AppendLine("    $stopped = [System.Collections.Generic.List[int]]::new()");
        builder.AppendLine("    foreach ($processIdToStop in @($ProcessIds)) {");
        builder.AppendLine("        if ($processIdToStop -eq $PID) { continue }");
        builder.AppendLine("        try {");
        builder.AppendLine("            $process = Get-Process -Id $processIdToStop -ErrorAction SilentlyContinue");
        builder.AppendLine("            if ($null -eq $process) { continue }");
        builder.AppendLine("            Stop-Process -Id $processIdToStop -Force -ErrorAction SilentlyContinue");
        builder.AppendLine("            Wait-Process -Id $processIdToStop -Timeout 5 -ErrorAction SilentlyContinue");
        builder.AppendLine("            [void]$stopped.Add($processIdToStop)");
        builder.AppendLine("        } catch { }");
        builder.AppendLine("    }");
        builder.AppendLine("    return @($stopped)");
        builder.AppendLine("}");
        builder.AppendLine("function Resolve-StillRunningProcessIds {");
        builder.AppendLine("    param([int[]]$ProcessIds)");
        builder.AppendLine("    $running = [System.Collections.Generic.List[int]]::new()");
        builder.AppendLine("    foreach ($processId in @($ProcessIds)) {");
        builder.AppendLine("        if ($processId -eq $PID) { continue }");
        builder.AppendLine("        if ($null -ne (Get-Process -Id $processId -ErrorAction SilentlyContinue)) { [void]$running.Add($processId) }");
        builder.AppendLine("    }");
        builder.AppendLine("    return @($running)");
        builder.AppendLine("}");
        builder.AppendLine("function Dismount-StaticWebAssetsAliasMappings {");
        builder.AppendLine("    param([object[]]$Mappings)");
        builder.AppendLine("    $dismounted = [System.Collections.Generic.List[string]]::new()");
        builder.AppendLine("    if ($null -eq $Mappings -or $Mappings.Count -eq 0) { return @($dismounted) }");
        builder.AppendLine("    foreach ($mapping in @($Mappings)) {");
        builder.AppendLine("        if ($null -eq $mapping) { continue }");
        builder.AppendLine("        if (Test-JsonProperty $mapping 'mounted' -and -not ([bool]$mapping.mounted)) { continue }");
        builder.AppendLine("        $drive = if (Test-JsonProperty $mapping 'drive') { [string]$mapping.drive } else { '' }");
        builder.AppendLine("        if ([string]::IsNullOrWhiteSpace($drive)) { continue }");
        builder.AppendLine("        try {");
        builder.AppendLine("            & subst $drive /d 2>$null | Out-Null");
        builder.AppendLine("            if ($LASTEXITCODE -eq 0) { [void]$dismounted.Add($drive) }");
        builder.AppendLine("        } catch { }");
        builder.AppendLine("    }");
        builder.AppendLine("    return @($dismounted)");
        builder.AppendLine("}");
        builder.AppendLine("function Write-CleanupReceipt {");
        builder.AppendLine("    param([bool]$Succeeded, [string]$Message, [int[]]$RequestedProcessIds, [int[]]$ResolvedProcessTreeIds, [int[]]$StoppedProcessIds, [int[]]$StillRunningProcessIds, [string[]]$DismountedDrives)");
        builder.AppendLine("    $completedAtUtc = [DateTimeOffset]::UtcNow");
        builder.AppendLine("    $payload = [ordered]@{");
        builder.AppendLine("        succeeded = $Succeeded");
        builder.AppendLine("        message = $Message");
        builder.AppendLine("        startupReceiptPath = $startupReceipt");
        builder.AppendLine("        cleanupReceiptPath = $cleanupReceipt");
        builder.AppendLine("        requestedProcessIds = @($RequestedProcessIds)");
        builder.AppendLine("        resolvedProcessTreeIds = @($ResolvedProcessTreeIds)");
        builder.AppendLine("        stoppedProcessIds = @($StoppedProcessIds)");
        builder.AppendLine("        stillRunningProcessIds = @($StillRunningProcessIds)");
        builder.AppendLine("        dismountedStaticWebAssetDrives = @($DismountedDrives)");
        builder.AppendLine("        startedAtUtc = $startedAtUtc.ToString('O')");
        builder.AppendLine("        completedAtUtc = $completedAtUtc.ToString('O')");
        builder.AppendLine("    }");
        builder.AppendLine("    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $cleanupReceipt) | Out-Null");
        builder.AppendLine("    $json = $payload | ConvertTo-Json -Depth 8");
        builder.AppendLine("    Set-Content -LiteralPath $cleanupReceipt -Value $json -Encoding UTF8");
        builder.AppendLine("    return $json");
        builder.AppendLine("}");
        builder.AppendLine("function Update-StartupReceiptCleanupState {");
        builder.AppendLine("    param([object]$Startup, [bool]$Succeeded, [int[]]$StoppedProcessIds, [int[]]$StillRunningProcessIds)");
        builder.AppendLine("    try {");
        builder.AppendLine("        $Startup | Add-Member -NotePropertyName cleanupAttempted -NotePropertyValue $true -Force");
        builder.AppendLine("        $Startup | Add-Member -NotePropertyName cleanupReceiptPath -NotePropertyValue $cleanupReceipt -Force");
        builder.AppendLine("        $Startup | Add-Member -NotePropertyName cleanupSucceeded -NotePropertyValue $Succeeded -Force");
        builder.AppendLine("        $Startup | Add-Member -NotePropertyName cleanupStoppedProcessIds -NotePropertyValue @($StoppedProcessIds) -Force");
        builder.AppendLine("        $Startup | Add-Member -NotePropertyName cleanupStillRunningProcessIds -NotePropertyValue @($StillRunningProcessIds) -Force");
        builder.AppendLine("        $Startup | Add-Member -NotePropertyName cleanupCompletedAtUtc -NotePropertyValue ([DateTimeOffset]::UtcNow.ToString('O')) -Force");
        builder.AppendLine("        Set-Content -LiteralPath $startupReceipt -Value ($Startup | ConvertTo-Json -Depth 8) -Encoding UTF8");
        builder.AppendLine("    } catch { }");
        builder.AppendLine("}");
        builder.AppendLine("try {");
        builder.AppendLine("    if (-not (Test-Path -LiteralPath $startupReceipt -PathType Leaf)) { throw \"Startup receipt not found: $startupReceipt\" }");
        builder.AppendLine("    $startup = Get-Content -LiteralPath $startupReceipt -Raw -ErrorAction Stop | ConvertFrom-Json -ErrorAction Stop");
        builder.AppendLine("    $requestedProcessIds = @(Resolve-StartupProcessIds $startup)");
        builder.AppendLine("    $resolvedProcessTreeIds = @(Resolve-ExpandedProcessTreeIds $requestedProcessIds)");
        builder.AppendLine("    $stoppedProcessIds = @(Stop-AppProcessTree $resolvedProcessTreeIds)");
        builder.AppendLine("    $stillRunningProcessIds = @(Resolve-StillRunningProcessIds $resolvedProcessTreeIds)");
        builder.AppendLine("    $mappings = if (Test-JsonProperty $startup 'staticWebAssetsAliasMappings') { @($startup.staticWebAssetsAliasMappings) } else { @() }");
        builder.AppendLine("    $dismountedDrives = @(Dismount-StaticWebAssetsAliasMappings $mappings)");
        builder.AppendLine("    $succeeded = $stillRunningProcessIds.Count -eq 0");
        builder.AppendLine("    $message = if ($succeeded) { \"Stopped recorded workspace_dotnet_run process tree and static web assets aliases.\" } else { \"Failed to stop every recorded workspace_dotnet_run process. Still running process ids: $($stillRunningProcessIds -join ',').\" }");
        builder.AppendLine("    Update-StartupReceiptCleanupState $startup $succeeded $stoppedProcessIds $stillRunningProcessIds");
        builder.AppendLine("    $json = Write-CleanupReceipt $succeeded $message $requestedProcessIds $resolvedProcessTreeIds $stoppedProcessIds $stillRunningProcessIds $dismountedDrives");
        builder.AppendLine("    Write-Output $json");
        builder.AppendLine("    if (-not $succeeded) { exit 1 }");
        builder.AppendLine("} catch {");
        builder.AppendLine("    $message = $_.Exception.Message");
        builder.AppendLine("    $json = Write-CleanupReceipt $false $message @() @() @() @() @()");
        builder.AppendLine("    Write-Error $json");
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
            throw new InvalidOperationException(
                "Script argument paths cannot contain parent traversal segments ('..'). Use a canonical workspace or external-target path.");
        }

        if (!pathPolicy.TryResolveWorkspacePath(
                candidate.Path,
                allowWorkspaceRoot: false,
                out var resolution,
                out var validationMessage))
        {
            throw new InvalidOperationException($"Script argument path is not allowed. {validationMessage}");
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
        string ScriptFullPath,
        string StdoutLogFullPath,
        string StderrLogFullPath,
        string StartupReceiptFullPath,
        IReadOnlyList<string> TargetPaths);

    private sealed record DotnetStopArtifactPaths(
        string WorkingDirectoryPath,
        string WorkingDirectoryRelative,
        string ScriptFullPath,
        string CleanupReceiptFullPath,
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
