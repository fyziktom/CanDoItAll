using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public sealed class WorkspaceCommandExecutionService :
    IWorkspaceCommandExecutionService,
    IWorkspaceExecutionRunProcessLeaseCleanupExecutor
{
    private readonly WorkspaceCommandEnvironmentPolicy environmentPolicy;
    private readonly WorkspaceCommandPlanBuilder planBuilder;
    private readonly WorkspaceCommandProcessRunner processRunner;
    private readonly WorkspaceCommandReceiptWriter receiptWriter;
    private readonly WorkspaceExecutionRunProcessLeaseStore processLeaseStore;

    public WorkspaceCommandExecutionService(
        string workspaceRoot,
        IWorkspaceProcessHost processHost,
        WorkspaceScopeDescriptor? workspaceScope = null,
        IEnumerable<IWorkspaceCommandReceiptLifecycleFactExtractor>? lifecycleFactExtractors = null)
    {
        var pathPolicy = new WorkspacePathPolicy(workspaceRoot, workspaceScope);
        environmentPolicy = new WorkspaceCommandEnvironmentPolicy();
        processLeaseStore = new WorkspaceExecutionRunProcessLeaseStore(
            pathPolicy.WorkspaceRoot,
            pathPolicy.WorkspaceScope);
        receiptWriter = new WorkspaceCommandReceiptWriter(
            pathPolicy.WorkspaceRoot,
            pathPolicy.WorkspaceScope,
            lifecycleFactExtractors);
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
            ToolContractCatalog.WorkspaceGitStatus,
            "git_status",
            "ReadOnly",
            approvalRequired: false);

    public Task<WorkspaceCommandExecutionResult> GitDiff(string? path = null, bool nameOnly = false, string? workingDirectory = null, int timeoutSeconds = 30)
        => ExecutePlanAsync(
            () => planBuilder.BuildGitDiff(path, nameOnly, workingDirectory, timeoutSeconds),
            ToolContractCatalog.WorkspaceGitDiff,
            "git_diff",
            "ReadOnly",
            approvalRequired: false);

    public Task<WorkspaceCommandExecutionResult> GitLog(int count = 10, string? workingDirectory = null, int timeoutSeconds = 30)
        => ExecutePlanAsync(
            () => planBuilder.BuildGitLog(count, workingDirectory, timeoutSeconds),
            ToolContractCatalog.WorkspaceGitLog,
            "git_log",
            "ReadOnly",
            approvalRequired: false);

    public Task<WorkspaceCommandExecutionResult> GitShow(string revision, string? workingDirectory = null, int timeoutSeconds = 30)
        => ExecutePlanAsync(
            () => planBuilder.BuildGitShow(revision, workingDirectory, timeoutSeconds),
            ToolContractCatalog.WorkspaceGitShow,
            "git_show",
            "ReadOnly",
            approvalRequired: false);

    public Task<WorkspaceCommandExecutionResult> GitAdd(string[]? paths, string? workingDirectory = null, int timeoutSeconds = 30)
        => ExecutePlanAsync(
            () => planBuilder.BuildGitAdd(paths, workingDirectory, timeoutSeconds),
            ToolContractCatalog.WorkspaceGitAdd,
            "git_add",
            "WorkspaceMutation:Git",
            approvalRequired: true);

    public Task<WorkspaceCommandExecutionResult> GitUnstage(string[]? paths, string? workingDirectory = null, int timeoutSeconds = 30)
        => ExecutePlanAsync(
            () => planBuilder.BuildGitUnstage(paths, workingDirectory, timeoutSeconds),
            ToolContractCatalog.WorkspaceGitUnstage,
            "git_unstage",
            "WorkspaceMutation:Git",
            approvalRequired: true);

    public Task<WorkspaceCommandExecutionResult> GitCommit(string message, string? workingDirectory = null, int timeoutSeconds = 30)
        => ExecutePlanAsync(
            () => planBuilder.BuildGitCommit(message, workingDirectory, timeoutSeconds),
            ToolContractCatalog.WorkspaceGitCommit,
            "git_commit",
            "WorkspaceMutation:Git",
            approvalRequired: true);

    public Task<WorkspaceCommandExecutionResult> GitBranchCreate(string branchName, string? workingDirectory = null, int timeoutSeconds = 30)
        => ExecutePlanAsync(
            () => planBuilder.BuildGitBranchCreate(branchName, workingDirectory, timeoutSeconds),
            ToolContractCatalog.WorkspaceGitBranchCreate,
            "git_branch_create",
            "WorkspaceMutation:Git",
            approvalRequired: true);

    public Task<WorkspaceCommandExecutionResult> GitSwitch(string branchName, string? workingDirectory = null, int timeoutSeconds = 30)
        => ExecutePlanAsync(
            () => planBuilder.BuildGitSwitch(branchName, workingDirectory, timeoutSeconds),
            ToolContractCatalog.WorkspaceGitSwitch,
            "git_switch",
            "WorkspaceMutation:Git",
            approvalRequired: true);

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

    public Task<WorkspaceCommandExecutionResult> DotnetTest(string? targetPath = null, string configuration = "Debug", string? filter = null, bool noBuild = false, bool noRestore = false, string? workingDirectory = null, int timeoutSeconds = 300)
        => ExecutePlanAsync(
            () => planBuilder.BuildDotnetTest(targetPath, configuration, filter, noBuild, noRestore, workingDirectory, timeoutSeconds),
            "workspace_dotnet_test",
            "dotnet_test",
            "LocalExecution",
            approvalRequired: false);

    public async Task<WorkspaceCommandExecutionResult> DotnetRun(string targetPath, string? url = null, string configuration = "Debug", bool noBuild = true, bool waitForHttp = true, string? workingDirectory = null, int startupTimeoutSeconds = 45, int timeoutSeconds = 120, bool keepAlive = false, WorkspaceProcessLifetimeScope lifetimeScope = WorkspaceProcessLifetimeScope.ExecutionRun)
    {
        var auditScope = WorkspaceExecutionAuditContext.Current;
        if (keepAlive &&
            lifetimeScope == WorkspaceProcessLifetimeScope.ExecutionRun &&
            auditScope is null)
        {
            return processRunner.CreateDeniedResult(
                "workspace_dotnet_run",
                waitForHttp ? "dotnet_run_http_smoke" : "dotnet_run",
                "LocalExecution",
                approvalRequired: false,
                "A kept-alive ExecutionRun workspace process requires an active execution-run audit context.");
        }

        var recipeId = waitForHttp ? "dotnet_run_http_smoke" : "dotnet_run";
        WorkspaceCommandPlan plan;
        try
        {
            plan = planBuilder.BuildDotnetRun(
                targetPath,
                url,
                configuration,
                noBuild,
                waitForHttp,
                workingDirectory,
                startupTimeoutSeconds,
                timeoutSeconds,
                keepAlive,
                lifetimeScope);
        }
        catch (Exception exception) when (
            WorkspaceCommandFailureBoundary.TryGetSafeMessage(exception, out _))
        {
            return processRunner.CreateDeniedResult(
                "workspace_dotnet_run",
                recipeId,
                "LocalExecution",
                approvalRequired: false,
                GetSafeFailureMessage(exception));
        }

        if (!keepAlive ||
            lifetimeScope != WorkspaceProcessLifetimeScope.ExecutionRun)
        {
            return await ExecutePlanAsync(
                () => plan,
                "workspace_dotnet_run",
                recipeId,
                "LocalExecution",
                approvalRequired: false);
        }

        WorkspaceExecutionRunProcessLeaseStore.ValidateAuditIdentity(auditScope!);

        string startupReceiptPath;
        var registeredAtUtc = DateTimeOffset.UtcNow;
        try
        {
            startupReceiptPath = processLeaseStore.ResolveSingleStartupReceiptPath(
                plan.TargetPaths,
                "Kept-alive workspace_dotnet_run plan");
            processLeaseStore.RegisterPending(
                auditScope!.ExecutionRunId,
                startupReceiptPath,
                registeredAtUtc,
                registeredAtUtc.AddSeconds(Math.Clamp(startupTimeoutSeconds, 1, 600)));
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(
                "The kept-alive process was not started because its pending ExecutionRun lease could not be persisted.",
                exception);
        }

        WorkspaceCommandExecutionResult result;
        try
        {
            result = await processRunner
                .ExecuteAsync(plan)
                .ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(
                "The kept-alive launch terminated before its durable lease could be activated. The pending lease was retained for terminal recovery.",
                exception);
        }

        if (!result.Succeeded)
        {
            try
            {
                processLeaseStore.Remove(
                    auditScope!.ExecutionRunId,
                    startupReceiptPath);
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException(
                    "The launch failed and its pending ExecutionRun lease could not be removed. The lease was retained for terminal recovery.",
                    exception);
            }

            return result;
        }

        try
        {
            var resultStartupReceiptPath = ResolveSingleStartupReceiptPath(
                result,
                "Successful kept-alive workspace_dotnet_run");
            if (!string.Equals(
                startupReceiptPath,
                resultStartupReceiptPath,
                StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Successful launch receipt identity '{resultStartupReceiptPath}' does not match pending lease identity '{startupReceiptPath}'.");
            }

            processLeaseStore.Activate(
                auditScope!.ExecutionRunId,
                startupReceiptPath,
                DateTimeOffset.UtcNow);
            return result;
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(
                "The kept-alive process started, but its pending ExecutionRun lease could not be activated. The pending lease was retained for terminal recovery.",
                exception);
        }
    }

    public async Task<WorkspaceCommandExecutionResult> DotnetStop(string startupReceiptPath, int timeoutSeconds = 30)
        => await DotnetStopAndRemoveLeaseAsync(
            startupReceiptPath,
            timeoutSeconds,
            ownerExecutionRunId: null)
            .ConfigureAwait(false);

    private async Task<WorkspaceCommandExecutionResult> DotnetStopAndRemoveLeaseAsync(
        string startupReceiptPath,
        int timeoutSeconds,
        Guid? ownerExecutionRunId)
    {
        var result = await ExecutePlanAsync(
            () => planBuilder.BuildDotnetStop(startupReceiptPath, timeoutSeconds),
            "workspace_dotnet_stop",
            "dotnet_stop",
            "LocalExecution",
            approvalRequired: false);
        if (!result.Succeeded)
        {
            return result;
        }

        try
        {
            var auditScope = WorkspaceExecutionAuditContext.Current;
            var resolvedOwnerExecutionRunId = ownerExecutionRunId;
            if (!resolvedOwnerExecutionRunId.HasValue &&
                auditScope is not null)
            {
                WorkspaceExecutionRunProcessLeaseStore.ValidateAuditIdentity(auditScope);
                resolvedOwnerExecutionRunId = auditScope.ExecutionRunId;
            }

            var canonicalStartupReceiptPath = ResolveSingleStartupReceiptPath(
                result,
                "Successful workspace_dotnet_stop");
            if (resolvedOwnerExecutionRunId.HasValue)
            {
                processLeaseStore.Remove(
                    resolvedOwnerExecutionRunId.Value,
                    canonicalStartupReceiptPath);
            }

            return result;
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(
                "The process stopped, but its durable ExecutionRun lease could not be removed. The lease was retained for terminal recovery.",
                exception);
        }
    }

    async Task<WorkspaceExecutionRunProcessCleanupResult>
        IWorkspaceExecutionRunProcessLeaseCleanupExecutor.CleanupAsync(
            Guid executionRunId)
        => await CleanupLeasesAsync(executionRunId).ConfigureAwait(false);

    private async Task<WorkspaceExecutionRunProcessCleanupResult> CleanupLeasesAsync(
        Guid executionRunId)
    {
        WorkspaceExecutionRunProcessLeaseLoadResult loaded;
        try
        {
            loaded = processLeaseStore.Load(executionRunId);
        }
        catch (Exception)
        {
            return new WorkspaceExecutionRunProcessCleanupResult(
                executionRunId,
                [],
                [new WorkspaceExecutionRunProcessCleanupFailure(
                    string.Empty,
                    WorkspaceCommandFailureBoundary.CleanupLoadFailureMessage)]);
        }

        var cleanedPaths = new List<string>();
        var failures = loaded.Failures.ToList();
        foreach (var lease in loaded.Leases)
        {
            var leaseIdentityPath = processLeaseStore.GetLeaseFilePath(
                lease.ExecutionRunId,
                lease.StartupReceiptPath);
            using var cleanupLease = WorkspaceExecutionRunProcessLeaseCleanupCoordinator.Acquire(
                leaseIdentityPath,
                () => CleanupLeaseAsync(lease));
            WorkspaceExecutionRunProcessLeaseCleanupAttempt attempt;
            try
            {
                attempt = await cleanupLease.Task
                    .ConfigureAwait(false);
            }
            catch (Exception)
            {
                attempt = new WorkspaceExecutionRunProcessLeaseCleanupAttempt(
                    Succeeded: false,
                    lease.StartupReceiptPath,
                    WorkspaceCommandFailureBoundary.CleanupAttemptFailureMessage);
            }

            if (attempt.Succeeded)
            {
                cleanedPaths.Add(attempt.StartupReceiptPath);
            }
            else
            {
                failures.Add(new WorkspaceExecutionRunProcessCleanupFailure(
                    attempt.StartupReceiptPath,
                    attempt.Message));
            }
        }

        return new WorkspaceExecutionRunProcessCleanupResult(
            executionRunId,
            cleanedPaths,
            failures);
    }

    private async Task<WorkspaceExecutionRunProcessLeaseCleanupAttempt> CleanupLeaseAsync(
        WorkspaceExecutionRunProcessLease lease)
    {
        try
        {
            if (!processLeaseStore.HasLease(
                lease.ExecutionRunId,
                lease.StartupReceiptPath))
            {
                return new WorkspaceExecutionRunProcessLeaseCleanupAttempt(
                    Succeeded: true,
                    lease.StartupReceiptPath,
                    "The durable lease was already cleaned by another cleanup owner.");
            }

            using var durableClaim = await processLeaseStore
                .AcquireCleanupClaimAsync(
                    lease.ExecutionRunId,
                    lease.StartupReceiptPath)
                .ConfigureAwait(false);
            if (durableClaim is null)
            {
                return new WorkspaceExecutionRunProcessLeaseCleanupAttempt(
                    Succeeded: true,
                    lease.StartupReceiptPath,
                    "The durable lease was already cleaned by another cleanup owner.");
            }

            if (!await processLeaseStore
                .WaitForPendingStartupReceiptAsync(lease)
                .ConfigureAwait(false))
            {
                return new WorkspaceExecutionRunProcessLeaseCleanupAttempt(
                    Succeeded: false,
                    lease.StartupReceiptPath,
                    $"Pending workspace process lease did not produce its startup receipt by the bounded recovery deadline '{lease.StartupReceiptDeadlineUtc:O}'. The durable lease was retained.");
            }

            var stopResult = await DotnetStopAndRemoveLeaseAsync(
                lease.StartupReceiptPath,
                timeoutSeconds: 30,
                lease.ExecutionRunId)
                .ConfigureAwait(false);
            return new WorkspaceExecutionRunProcessLeaseCleanupAttempt(
                stopResult.Succeeded,
                lease.StartupReceiptPath,
                stopResult.Message);
        }
        catch (Exception)
        {
            return new WorkspaceExecutionRunProcessLeaseCleanupAttempt(
                Succeeded: false,
                lease.StartupReceiptPath,
                WorkspaceCommandFailureBoundary.CleanupAttemptFailureMessage);
        }
    }

    public Task<WorkspaceCommandExecutionResult> DotnetNew(
        string template,
        string name,
        string? parentDirectory = null,
        bool force = false,
        int timeoutSeconds = 300,
        string? targetFramework = null)
        => ExecutePlanAsync(
            () => planBuilder.BuildDotnetNew(template, name, parentDirectory, force, timeoutSeconds, targetFramework),
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
                throw WorkspaceCommandInputException.Create(
                    "Local MCP launch requires a capability name.",
                    "Local MCP launch requires a non-empty capability name.");
            }

            if (string.IsNullOrWhiteSpace(command))
            {
                throw WorkspaceCommandInputException.Create(
                    $"Local MCP capability '{capabilityName}' is missing a reviewed command descriptor.",
                    "Local MCP launch requires a reviewed command descriptor.");
            }

            var normalizedCommand = command.Trim();
            if (!LocalMcpCommandPolicy.IsAllowed(normalizedCommand))
            {
                throw WorkspaceCommandInputException.Create(
                    $"Local MCP capability '{capabilityName}' uses command '{normalizedCommand}', which is outside the approved interpreter policy.",
                    $"The local MCP command is outside the approved interpreter policy. Allowed commands: {LocalMcpCommandPolicy.DescribeAllowedCommands()}.");
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
        catch (Exception exception) when (
            WorkspaceCommandFailureBoundary.TryGetSafeMessage(exception, out _))
        {
            return CreateDeniedLaunchDescriptor(
                capabilityName,
                command,
                normalizedArguments,
                workingDirectory,
                mergedEnvironmentVariables,
                approvalRequired,
                GetSafeFailureMessage(exception));
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
        WorkspaceCommandPlan plan;
        try
        {
            plan = createPlan();
        }
        catch (Exception exception) when (
            WorkspaceCommandFailureBoundary.TryGetSafeMessage(exception, out _))
        {
            return processRunner.CreateDeniedResult(
                toolName,
                recipeId,
                riskClass,
                approvalRequired,
                GetSafeFailureMessage(exception));
        }

        try
        {
            return await processRunner.ExecuteAsync(plan).ConfigureAwait(false);
        }
        catch (Exception exception) when (
            WorkspaceCommandFailureBoundary.TryGetSafeMessage(exception, out _))
        {
            return processRunner.CreateDeniedResult(
                toolName,
                recipeId,
                riskClass,
                approvalRequired,
                GetSafeFailureMessage(exception));
        }
    }

    private static string GetSafeFailureMessage(Exception exception)
    {
        if (WorkspaceCommandFailureBoundary.TryGetSafeMessage(exception, out var safeMessage))
        {
            return safeMessage;
        }

        throw new InvalidOperationException(
            "The workspace command failure was not approved for model-visible projection.",
            exception);
    }

    private string ResolveSingleStartupReceiptPath(
        WorkspaceCommandExecutionResult result,
        string operation)
        => processLeaseStore.ResolveSingleStartupReceiptPath(
            result.Receipt.TargetPaths,
            operation);

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
