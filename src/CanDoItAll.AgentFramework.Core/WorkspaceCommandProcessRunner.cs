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
        var processResult = await processHost.ExecuteAsync(
            new WorkspaceProcessExecutionRequest(
                ToolName: plan.Decision.ToolName,
                RecipeId: plan.Decision.RecipeId,
                ExecutablePath: executablePath,
                Arguments: plan.Arguments,
                WorkingDirectory: plan.WorkingDirectoryPath,
                EnvironmentVariables: environmentPolicy.MergeEnvironmentVariables(plan.EnvironmentVariables),
                TimeoutSeconds: plan.TimeoutSeconds,
                StdoutLimitCharacters: plan.StdoutLimitCharacters,
                StderrLimitCharacters: plan.StderrLimitCharacters),
            cancellationToken).ConfigureAwait(false);

        var succeeded = processResult.Started && !processResult.TimedOut && processResult.ExitCode == 0;
        if (processResult.TimedOut)
        {
            AgentFrameworkTelemetry.RecordCommandTimeout(plan.Decision.RecipeId, plan.Decision.RiskClass);
        }

        var message = !processResult.Started
            ? $"Recipe '{plan.Decision.RecipeId}' failed to start: {processResult.FailureMessage}"
            : processResult.TimedOut
                ? $"Recipe '{plan.Decision.RecipeId}' timed out after {plan.TimeoutSeconds} second(s)."
                : succeeded
                    ? $"Recipe '{plan.Decision.RecipeId}' completed successfully."
                    : $"Recipe '{plan.Decision.RecipeId}' failed with exit code {processResult.ExitCode}.";
        var receipt = receiptWriter.PersistProcessReceipt(
            plan.Decision.ToolName,
            plan.Decision.RecipeId,
            plan.Decision,
            plan.WorkingDirectory,
            plan.Arguments,
            plan.TargetPaths,
            plan.MutatesWorkspace,
            message,
            processResult);

        return new WorkspaceCommandExecutionResult(
            Succeeded: succeeded,
            Message: message,
            Receipt: receipt,
            ToolName: plan.Decision.ToolName,
            RecipeId: plan.Decision.RecipeId,
            RiskClass: plan.Decision.RiskClass,
            ApprovalRequired: plan.Decision.ApprovalRequired,
            Boundary: processResult.Boundary,
            WorkingDirectory: plan.WorkingDirectory,
            ArgumentsSummary: WorkspaceCommandReceiptWriter.BuildArgumentsSummary(plan.Arguments),
            ExitCode: processResult.ExitCode,
            StdoutPreview: processResult.Stdout,
            StderrPreview: processResult.Stderr,
            StdoutTruncated: processResult.StdoutTruncated,
            StderrTruncated: processResult.StderrTruncated);
    }
}
