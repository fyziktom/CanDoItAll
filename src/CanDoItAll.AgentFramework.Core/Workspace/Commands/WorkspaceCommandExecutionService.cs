using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public sealed class WorkspaceCommandExecutionService : IWorkspaceCommandExecutionService
{
    private readonly WorkspaceCommandEnvironmentPolicy environmentPolicy;
    private readonly WorkspaceCommandPlanBuilder planBuilder;
    private readonly WorkspaceCommandProcessRunner processRunner;
    private readonly WorkspaceCommandReceiptWriter receiptWriter;

    public WorkspaceCommandExecutionService(
        string workspaceRoot,
        IWorkspaceProcessHost processHost,
        WorkspaceScopeDescriptor? workspaceScope = null)
    {
        var pathPolicy = new WorkspacePathPolicy(workspaceRoot, workspaceScope);
        environmentPolicy = new WorkspaceCommandEnvironmentPolicy();
        receiptWriter = new WorkspaceCommandReceiptWriter(pathPolicy.WorkspaceRoot, pathPolicy.WorkspaceScope);
        planBuilder = new WorkspaceCommandPlanBuilder(pathPolicy);
        processRunner = new WorkspaceCommandProcessRunner(
            processHost,
            environmentPolicy,
            new WorkspaceExecutableLocator(),
            receiptWriter);
    }

    public ExecutionBoundaryDescriptor DescribeBoundary() => processRunner.DescribeBoundary();

    public WorkspaceCommandExecutionResult GetExecutionBoundary() => processRunner.CreateBoundaryResult();

    public Task<WorkspaceCommandExecutionResult> GitStatus(bool includeBranch = true, string? workingDirectory = null, int timeoutSeconds = 30)
        => ExecutePlanAsync(
            () => planBuilder.BuildGitStatus(includeBranch, workingDirectory, timeoutSeconds),
            "workspace_git_status",
            "git_status",
            "ReadOnly",
            approvalRequired: false);

    public Task<WorkspaceCommandExecutionResult> GitDiff(string? path = null, bool nameOnly = false, string? workingDirectory = null, int timeoutSeconds = 30)
        => ExecutePlanAsync(
            () => planBuilder.BuildGitDiff(path, nameOnly, workingDirectory, timeoutSeconds),
            "workspace_git_diff",
            "git_diff",
            "ReadOnly",
            approvalRequired: false);

    public Task<WorkspaceCommandExecutionResult> DotnetRestore(string? targetPath = null, string? workingDirectory = null, int timeoutSeconds = 600)
        => ExecutePlanAsync(
            () => planBuilder.BuildDotnetRestore(targetPath, workingDirectory, timeoutSeconds),
            "workspace_dotnet_restore",
            "dotnet_restore",
            "LocalExecution",
            approvalRequired: true);

    public Task<WorkspaceCommandExecutionResult> DotnetBuild(string? targetPath = null, string configuration = "Debug", bool noRestore = false, string? workingDirectory = null, int timeoutSeconds = 600)
        => ExecutePlanAsync(
            () => planBuilder.BuildDotnetBuild(targetPath, configuration, noRestore, workingDirectory, timeoutSeconds),
            "workspace_dotnet_build",
            "dotnet_build",
            "LocalExecution",
            approvalRequired: false);

    public Task<WorkspaceCommandExecutionResult> DotnetTest(string? targetPath = null, string configuration = "Debug", string? filter = null, bool noBuild = false, bool noRestore = false, string? workingDirectory = null, int timeoutSeconds = 1200)
        => ExecutePlanAsync(
            () => planBuilder.BuildDotnetTest(targetPath, configuration, filter, noBuild, noRestore, workingDirectory, timeoutSeconds),
            "workspace_dotnet_test",
            "dotnet_test",
            "LocalExecution",
            approvalRequired: false);

    public Task<WorkspaceCommandExecutionResult> DotnetRun(string targetPath, string? url = null, string configuration = "Debug", bool noBuild = true, bool waitForHttp = true, string? workingDirectory = null, int startupTimeoutSeconds = 45, int timeoutSeconds = 120, bool keepAlive = false, WorkspaceProcessLifetimeScope lifetimeScope = WorkspaceProcessLifetimeScope.ExecutionRun)
        => ExecutePlanAsync(
            () => planBuilder.BuildDotnetRun(targetPath, url, configuration, noBuild, waitForHttp, workingDirectory, startupTimeoutSeconds, timeoutSeconds, keepAlive, lifetimeScope),
            "workspace_dotnet_run",
            waitForHttp ? "dotnet_run_http_smoke" : "dotnet_run",
            "LocalExecution",
            approvalRequired: false);

    public Task<WorkspaceCommandExecutionResult> DotnetNew(string template, string name, string? parentDirectory = null, bool force = false, int timeoutSeconds = 300)
        => ExecutePlanAsync(
            () => planBuilder.BuildDotnetNew(template, name, parentDirectory, force, timeoutSeconds),
            "workspace_dotnet_new",
            "dotnet_new",
            "WorkspaceMutation",
            approvalRequired: true);

    public Task<WorkspaceCommandExecutionResult> PythonRunFile(string path, string[]? arguments = null, string? workingDirectory = null, int timeoutSeconds = 300, string? sideEffectManifest = null)
        => ExecutePlanAsync(
            () => planBuilder.BuildPythonRunFile(path, arguments, workingDirectory, timeoutSeconds, sideEffectManifest),
            "workspace_python_run_file",
            "python_run_file",
            "LocalExecution",
            approvalRequired: true);

    public Task<WorkspaceCommandExecutionResult> PowerShellRunScript(string path, string[]? arguments = null, string[]? outputPaths = null, string? workingDirectory = null, int timeoutSeconds = 300, string? sideEffectManifest = null)
        => ExecutePlanAsync(
            () => planBuilder.BuildPowerShellRunScript(path, arguments, outputPaths, workingDirectory, timeoutSeconds, sideEffectManifest),
            "workspace_pwsh_run_script",
            "pwsh_run_script",
            "LocalExecution",
            approvalRequired: true);

    public Task<WorkspaceCommandExecutionResult> ConvertDocumentWithMarkItDown(string sourcePath, string outputPath, int timeoutSeconds = 300)
        => ExecutePlanAsync(
            () => planBuilder.BuildConvertDocumentWithMarkItDown(sourcePath, outputPath, timeoutSeconds),
            "workspace_convert_document",
            "convert_document",
            "LocalExecution:DocumentConversion",
            approvalRequired: true);

    public Task<WorkspaceCommandExecutionResult> InspectSpreadsheetPreview(string path, int maxRows = 8, int maxColumns = 8, int timeoutSeconds = 300)
        => ExecutePlanAsync(
            () => planBuilder.BuildInspectSpreadsheetPreview(path, maxRows, maxColumns, timeoutSeconds),
            "workspace_inspect_spreadsheet",
            "inspect_spreadsheet",
            "LocalExecution:SpreadsheetInspection",
            approvalRequired: false);

    public Task<WorkspaceCommandExecutionResult> RunSkillScript(string skillName, string scriptPath, string[]? arguments = null, string? workingDirectory = null, bool approvalRequired = true, string trustLevel = "FileSkill", IReadOnlyList<string>? allowedExternalRoots = null)
        => ExecutePlanAsync(
            () => planBuilder.BuildSkillScript(scriptPath, arguments, workingDirectory, approvalRequired, trustLevel, allowedExternalRoots),
            "skill_script_run",
            "skill_script_run",
            $"LocalExecution:{trustLevel}",
            approvalRequired);

    public WorkspaceLocalMcpLaunchDescriptor PrepareLocalMcpServerLaunch(string capabilityName, string command, string[]? arguments = null, string? workingDirectory = null, IReadOnlyDictionary<string, string?>? environmentVariables = null, bool approvalRequired = true)
    {
        var normalizedArguments = NormalizeStructuredArguments(arguments);
        var mergedEnvironmentVariables = environmentPolicy.MergeEnvironmentVariables(environmentVariables);

        try
        {
            if (string.IsNullOrWhiteSpace(capabilityName))
            {
                throw new InvalidOperationException("Local MCP launch requires a capability name.");
            }

            if (string.IsNullOrWhiteSpace(command))
            {
                throw new InvalidOperationException($"Local MCP capability '{capabilityName}' is missing a reviewed command descriptor.");
            }

            var normalizedCommand = command.Trim();
            if (!LocalMcpCommandPolicy.IsAllowed(normalizedCommand))
            {
                throw new InvalidOperationException(
                    $"Local MCP capability '{capabilityName}' uses command '{normalizedCommand}', which is outside the approved interpreter policy. Allowed commands: {LocalMcpCommandPolicy.DescribeAllowedCommands()}.");
            }

            var boundary = DescribeBoundary();
            var now = DateTimeOffset.UtcNow;
            var receipt = receiptWriter.PersistDescriptorReceipt(
                toolName: "local_mcp_launch",
                recipeId: "local_mcp_launch",
                riskClass: "LocalExecution:Mcp",
                approvalRequired: approvalRequired,
                workingDirectory: string.IsNullOrWhiteSpace(workingDirectory) ? "." : workingDirectory,
                arguments: normalizedArguments,
                targetPaths: [],
                message: $"Prepared local MCP launch descriptor for '{capabilityName}'.",
                boundary: boundary,
                startedAtUtc: now,
                completedAtUtc: now,
                extraPayload: new
                {
                    capabilityName,
                    command = normalizedCommand,
                    environmentVariableNames = mergedEnvironmentVariables.Keys
                        .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
                        .ToArray()
                });

            return new WorkspaceLocalMcpLaunchDescriptor(
                CapabilityName: capabilityName.Trim(),
                Command: normalizedCommand,
                Arguments: normalizedArguments,
                WorkingDirectory: string.IsNullOrWhiteSpace(workingDirectory) ? null : workingDirectory,
                EnvironmentVariables: mergedEnvironmentVariables,
                ApprovalRequired: approvalRequired,
                RiskClass: "LocalExecution:Mcp",
                Boundary: boundary,
                Receipt: receipt,
                Message: $"Prepared local MCP launch descriptor for '{capabilityName}'.");
        }
        catch (Exception exception)
        {
            return CreateDeniedLaunchDescriptor(
                capabilityName,
                command,
                normalizedArguments,
                workingDirectory,
                mergedEnvironmentVariables,
                approvalRequired,
                exception.Message);
        }
    }

    public WorkspaceCommandExecutionResult RunLegacyCommand(string executable, string arguments = "", string? workingDirectory = null, int timeoutSeconds = 120)
    {
        return processRunner.CreateDeniedResult(
            "workspace_command_run",
            "legacy_command",
            "LocalExecution",
            approvalRequired: true,
            "RunLegacyCommand is retired. Use the reviewed workspace recipe methods instead.");
    }

    private async Task<WorkspaceCommandExecutionResult> ExecutePlanAsync(
        Func<WorkspaceCommandPlan> createPlan,
        string toolName,
        string recipeId,
        string riskClass,
        bool approvalRequired)
    {
        try
        {
            return await processRunner.ExecuteAsync(createPlan()).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            return processRunner.CreateDeniedResult(toolName, recipeId, riskClass, approvalRequired, exception.Message);
        }
    }

    private WorkspaceLocalMcpLaunchDescriptor CreateDeniedLaunchDescriptor(
        string capabilityName,
        string command,
        IReadOnlyList<string> arguments,
        string? workingDirectory,
        IReadOnlyDictionary<string, string?> environmentVariables,
        bool approvalRequired,
        string message)
    {
        var boundary = DescribeBoundary();
        var now = DateTimeOffset.UtcNow;
        var receipt = receiptWriter.CreateAuditedReceipt(
            operation: "local_mcp_launch",
            mutatesWorkspace: false,
            boundary: WorkspaceCommandReceiptWriter.BuildBoundarySummary(boundary),
            outcome: "Denied",
            message: message,
            receiptRelativePath: string.Empty,
            targetPaths: [],
            artifactReferences: [],
            startedAtUtc: now,
            completedAtUtc: now,
            toolFamily: "workspace-process",
            riskClass: "LocalExecution:Mcp",
            approvalMode: approvalRequired ? "Required" : "NotRequired",
            isolationGuarantee: WorkspaceCommandReceiptWriter.BuildBoundarySummary(boundary),
            requestSummary: WorkspaceCommandReceiptWriter.BuildArgumentsSummary(arguments),
            workingDirectory: string.IsNullOrWhiteSpace(workingDirectory) ? "." : workingDirectory,
            exitSummary: "Denied");

        return new WorkspaceLocalMcpLaunchDescriptor(
            CapabilityName: capabilityName,
            Command: command,
            Arguments: arguments,
            WorkingDirectory: workingDirectory,
            EnvironmentVariables: environmentVariables,
            ApprovalRequired: approvalRequired,
            RiskClass: "LocalExecution:Mcp",
            Boundary: boundary,
            Receipt: receipt,
            Message: message);
    }

    private static IReadOnlyList<string> NormalizeStructuredArguments(string[]? arguments)
    {
        return arguments?
            .Where(argument => !string.IsNullOrWhiteSpace(argument))
            .Select(argument => argument.Trim())
            .ToArray()
            ?? [];
    }
}
