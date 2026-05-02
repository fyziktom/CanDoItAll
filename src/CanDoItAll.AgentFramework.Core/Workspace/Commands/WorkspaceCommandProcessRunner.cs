using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

internal sealed class WorkspaceCommandProcessRunner
{
    private readonly IWorkspaceProcessHost processHost;
    private readonly WorkspaceCommandEnvironmentPolicy environmentPolicy;
    private readonly WorkspaceExecutableLocator executableLocator;
    private readonly WorkspaceCommandReceiptWriter receiptWriter;

    public WorkspaceCommandProcessRunner(
        IWorkspaceProcessHost processHost,
        WorkspaceCommandEnvironmentPolicy environmentPolicy,
        WorkspaceExecutableLocator executableLocator,
        WorkspaceCommandReceiptWriter receiptWriter)
    {
        this.processHost = processHost;
        this.environmentPolicy = environmentPolicy;
        this.executableLocator = executableLocator;
        this.receiptWriter = receiptWriter;
    }

    public ExecutionBoundaryDescriptor DescribeBoundary() => processHost.DescribeBoundary();

    public WorkspaceCommandExecutionResult CreateBoundaryResult()
    {
        var boundary = DescribeBoundary();
        var now = DateTimeOffset.UtcNow;
        var message = $"Workspace command host '{boundary.HostLabel}' reports isolation mode '{boundary.Mode}' (host-enforced: {boundary.IsEnforcedByHost.ToString().ToLowerInvariant()}).";
        var receipt = receiptWriter.CreateAuditedReceipt(
            operation: "workspace_execution_boundary",
            mutatesWorkspace: false,
            boundary: WorkspaceCommandReceiptWriter.BuildBoundarySummary(boundary),
            outcome: "Succeeded",
            message: message,
            receiptRelativePath: string.Empty,
            targetPaths: [],
            artifactReferences: [],
            startedAtUtc: now,
            completedAtUtc: now,
            toolFamily: "workspace-process",
            riskClass: "ReadOnly",
            approvalMode: "NotRequired",
            isolationGuarantee: WorkspaceCommandReceiptWriter.BuildBoundarySummary(boundary),
            requestSummary: "execution boundary",
            workingDirectory: ".",
            exitSummary: "Succeeded");

        return new WorkspaceCommandExecutionResult(
            Succeeded: true,
            Message: message,
            Receipt: receipt,
            ToolName: "workspace_execution_boundary",
            RecipeId: "execution_boundary",
            RiskClass: "ReadOnly",
            ApprovalRequired: false,
            Boundary: boundary,
            WorkingDirectory: ".",
            ArgumentsSummary: "(none)",
            ExitCode: 0,
            StdoutPreview: string.Empty,
            StderrPreview: string.Empty,
            StdoutTruncated: false,
            StderrTruncated: false);
    }

    public WorkspaceCommandExecutionResult CreateDeniedResult(string toolName, string recipeId, string riskClass, bool approvalRequired, string message)
    {
        var boundary = DescribeBoundary();
        var now = DateTimeOffset.UtcNow;
        var receipt = receiptWriter.CreateAuditedReceipt(
            operation: toolName,
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
            riskClass: riskClass,
            approvalMode: approvalRequired ? "Required" : "NotRequired",
            isolationGuarantee: WorkspaceCommandReceiptWriter.BuildBoundarySummary(boundary),
            requestSummary: recipeId,
            workingDirectory: ".",
            exitSummary: "Denied");

        return new WorkspaceCommandExecutionResult(
            Succeeded: false,
            Message: message,
            Receipt: receipt,
            ToolName: toolName,
            RecipeId: recipeId,
            RiskClass: riskClass,
            ApprovalRequired: approvalRequired,
            Boundary: boundary,
            WorkingDirectory: ".",
            ArgumentsSummary: string.Empty,
            ExitCode: -1,
            StdoutPreview: string.Empty,
            StderrPreview: string.Empty,
            StdoutTruncated: false,
            StderrTruncated: false);
    }

    public async Task<WorkspaceCommandExecutionResult> ExecuteAsync(WorkspaceCommandPlan plan, CancellationToken cancellationToken = default)
    {
        var executablePath = executableLocator.ResolveExecutablePath(plan.ExecutableCandidates);
        using var pathAliasSession = WorkspacePathAliasSession.TryCreate(
            plan.WorkspaceRootPath,
            plan.WorkingDirectoryPath,
            plan.Arguments);
        var effectiveWorkingDirectoryPath = pathAliasSession?.RewritePath(plan.WorkingDirectoryPath) ?? plan.WorkingDirectoryPath;
        var effectiveArguments = pathAliasSession?.RewriteArguments(plan.Arguments) ?? plan.Arguments;
        var processResult = await processHost.ExecuteAsync(
            new WorkspaceProcessExecutionRequest(
                ToolName: plan.Decision.ToolName,
                RecipeId: plan.Decision.RecipeId,
                ExecutablePath: executablePath,
                Arguments: effectiveArguments,
                WorkingDirectory: effectiveWorkingDirectoryPath,
                EnvironmentVariables: environmentPolicy.MergeEnvironmentVariables(plan.EnvironmentVariables),
                TimeoutSeconds: plan.TimeoutSeconds,
                StdoutLimitCharacters: plan.StdoutLimitCharacters,
                StderrLimitCharacters: plan.StderrLimitCharacters),
            cancellationToken).ConfigureAwait(false);

        var effectiveProcessResult = NormalizeProcessResult(plan.Decision.ToolName, processResult);
        var succeeded = effectiveProcessResult.Started && !effectiveProcessResult.TimedOut && effectiveProcessResult.ExitCode == 0;
        if (effectiveProcessResult.TimedOut)
        {
            AgentFrameworkTelemetry.RecordCommandTimeout(plan.Decision.RecipeId, plan.Decision.RiskClass);
        }

        var message = !effectiveProcessResult.Started
            ? $"Recipe '{plan.Decision.RecipeId}' failed to start: {effectiveProcessResult.FailureMessage}"
            : effectiveProcessResult.TimedOut
                ? $"Recipe '{plan.Decision.RecipeId}' timed out after {plan.TimeoutSeconds} second(s)."
                : succeeded
                    ? $"Recipe '{plan.Decision.RecipeId}' completed successfully."
                    : $"Recipe '{plan.Decision.RecipeId}' failed with exit code {effectiveProcessResult.ExitCode}.";
        var receipt = receiptWriter.PersistProcessReceipt(
            plan.Decision.ToolName,
            plan.Decision.RecipeId,
            plan.Decision,
            plan.WorkingDirectory,
            plan.Arguments,
            plan.TargetPaths,
            plan.MutatesWorkspace,
            message,
            effectiveProcessResult);
        var resultMessage = AppendFailureDiagnosticHint(message, receipt, succeeded);

        return new WorkspaceCommandExecutionResult(
            Succeeded: succeeded,
            Message: resultMessage,
            Receipt: receipt,
            ToolName: plan.Decision.ToolName,
            RecipeId: plan.Decision.RecipeId,
            RiskClass: plan.Decision.RiskClass,
            ApprovalRequired: plan.Decision.ApprovalRequired,
            Boundary: effectiveProcessResult.Boundary,
            WorkingDirectory: plan.WorkingDirectory,
            ArgumentsSummary: WorkspaceCommandReceiptWriter.BuildArgumentsSummary(plan.Arguments),
            ExitCode: effectiveProcessResult.ExitCode,
            StdoutPreview: effectiveProcessResult.Stdout,
            StderrPreview: effectiveProcessResult.Stderr,
            StdoutTruncated: effectiveProcessResult.StdoutTruncated,
            StderrTruncated: effectiveProcessResult.StderrTruncated);
    }

    private static string AppendFailureDiagnosticHint(
        string message,
        WorkspaceToolReceipt receipt,
        bool succeeded)
    {
        if (succeeded)
        {
            return message;
        }

        var stdoutPath = receipt.ArtifactReferences.FirstOrDefault(item =>
            item.DisplayName.Contains("stdout", StringComparison.OrdinalIgnoreCase))?.RelativePath;
        var stderrPath = receipt.ArtifactReferences.FirstOrDefault(item =>
            item.DisplayName.Contains("stderr", StringComparison.OrdinalIgnoreCase))?.RelativePath;
        if (string.IsNullOrWhiteSpace(stdoutPath) && string.IsNullOrWhiteSpace(stderrPath))
        {
            return message;
        }

        return $"{message} Inspect captured diagnostics before editing or retrying. stdout: {stdoutPath ?? "(none)"}; stderr: {stderrPath ?? "(none)"}.";
    }

    private static WorkspaceProcessExecutionResult NormalizeProcessResult(
        string toolName,
        WorkspaceProcessExecutionResult processResult)
    {
        if (!string.Equals(toolName, "workspace_pwsh_run_script", StringComparison.OrdinalIgnoreCase) ||
            !processResult.Started ||
            processResult.TimedOut ||
            processResult.ExitCode != 0 ||
            !ContainsPowerShellErrorRecord(processResult.Stderr))
        {
            return processResult;
        }

        var failureMessage = string.IsNullOrWhiteSpace(processResult.FailureMessage)
            ? "PowerShell reported errors on stderr despite exit code 0."
            : $"PowerShell reported errors on stderr despite exit code 0. {processResult.FailureMessage}";

        return processResult with
        {
            ExitCode = 1,
            FailureMessage = failureMessage
        };
    }

    private static bool ContainsPowerShellErrorRecord(string? stderr)
    {
        if (string.IsNullOrWhiteSpace(stderr))
        {
            return false;
        }

        return stderr.Contains("WriteError:", StringComparison.OrdinalIgnoreCase) ||
               stderr.Contains("ParserError:", StringComparison.OrdinalIgnoreCase) ||
               stderr.Contains("RuntimeException:", StringComparison.OrdinalIgnoreCase) ||
               stderr.Contains("CommandNotFoundException:", StringComparison.OrdinalIgnoreCase) ||
               stderr.Contains("ParameterBindingException:", StringComparison.OrdinalIgnoreCase) ||
               stderr.Contains("FullyQualifiedErrorId", StringComparison.OrdinalIgnoreCase) ||
               stderr.Contains("Cannot overwrite variable PID", StringComparison.OrdinalIgnoreCase);
    }
}
