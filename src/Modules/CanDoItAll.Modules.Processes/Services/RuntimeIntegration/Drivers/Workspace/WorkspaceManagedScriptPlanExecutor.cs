using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Processes.Core;

using static CanDoItAll.Modules.Processes.ProcessRuntimeOwnedToolReceiptFactory;

namespace CanDoItAll.Modules.Processes;

internal sealed class WorkspaceManagedScriptPlanExecutor(
    IWorkspaceFileService workspaceFiles,
    IWorkspaceCommandExecutionService workspaceCommands)
{
    private const int MaximumReadbackCharacters = 200000;
    private const string PostconditionVerifiedRiskClass = "RuntimeOwned:PostconditionVerified";

    internal async Task<WorkspaceManagedScriptPlanExecutionResult> ExecuteAsync(
        WorkspaceManagedScriptPlanExecutionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        if (!TryValidateRequest(request, out var validationIssue))
        {
            return Failure(
                [],
                $"Managed script plan cannot execute: {validationIssue}",
                $"{request.FailureEvidencePrefix}:validation:{validationIssue}",
                ProcessRuntimeOwnedStepFailures.ContractInvalid);
        }

        var receipts = new List<ToolExecutionReceiptRecord>();
        var writeScript = workspaceFiles.WriteTextFile(request.ScriptRef, request.Script, overwrite: true);
        receipts.Add(From(request.ExecutionRunId, writeScript));
        if (!writeScript.Succeeded)
        {
            return Failure(
                receipts,
                $"Managed script plan could not write helper script '{request.ScriptRef}': {writeScript.Message}",
                $"{request.FailureEvidencePrefix}:script-write:{request.ScriptRef}:{writeScript.Message}",
                ResolveExecutionFailure(request, writeScript.Receipt.Outcome));
        }

        var scriptStat = workspaceFiles.StatPath(request.ScriptRef);
        receipts.Add(From(request.ExecutionRunId, scriptStat));
        if (!scriptStat.Succeeded ||
            !scriptStat.Exists ||
            !string.Equals(scriptStat.PathKind, "file", StringComparison.OrdinalIgnoreCase))
        {
            return Failure(
                receipts,
                $"Managed script plan could not verify helper script '{request.ScriptRef}': {scriptStat.Message}",
                $"{request.FailureEvidencePrefix}:script-stat:{request.ScriptRef}:{scriptStat.Message}",
                ResolveExecutionFailure(request, scriptStat.Receipt.Outcome));
        }

        cancellationToken.ThrowIfCancellationRequested();
        var scriptRun = await workspaceCommands.PowerShellRunScript(
                request.ScriptRef,
                arguments: null,
                outputPaths: [request.OutputPath],
                workingDirectory: request.WorkingDirectory,
                timeoutSeconds: 300,
                sideEffectManifest: request.SideEffectManifest)
            .ConfigureAwait(false);
        receipts.Add(From(request.ExecutionRunId, scriptRun));
        if (!TryValidateReadbacks(
                request.ProductRoot,
                request.ReadbackChecks,
                receipts,
                request.ExecutionRunId,
                out var readbackIssue,
                out var readbackFailure,
                out var positivelyVerifiedRequiredChecks))
        {
            if (!scriptRun.Succeeded)
            {
                return Failure(
                    receipts,
                    $"Managed script plan helper failed and its contracted postconditions were not satisfied: {scriptRun.Message} Readback: {readbackIssue}",
                    $"{request.FailureEvidencePrefix}:script-run:{scriptRun.ExitCode}:{scriptRun.Message}:{scriptRun.StderrPreview}:readback:{readbackIssue}",
                    ResolveExecutionFailure(request, scriptRun.Receipt.Outcome));
            }

            return Failure(
                receipts,
                readbackIssue,
                $"{request.FailureEvidencePrefix}:readback:{readbackIssue}",
                ApplyReadbackPolicy(request, readbackFailure));
        }

        if (!scriptRun.Succeeded)
        {
            if (!CanReconcileFailedExecution(
                    request.ExecutionPolicy,
                    scriptRun,
                    positivelyVerifiedRequiredChecks))
            {
                return Failure(
                    receipts,
                    "Managed script plan helper failed. Its postconditions were inspected, but the declared execution policy or available positive evidence does not authorize failed-command reconciliation.",
                    $"{request.FailureEvidencePrefix}:script-run-not-reconcilable:{scriptRun.Receipt.Outcome}:{scriptRun.ExitCode}:positive-required-readbacks:{positivelyVerifiedRequiredChecks}",
                    ResolveExecutionFailure(request, scriptRun.Receipt.Outcome));
            }

            receipts.Add(CreatePostconditionVerifiedReceipt(request, scriptRun));
            return new WorkspaceManagedScriptPlanExecutionResult(
                true,
                receipts,
                "Managed script plan helper reported failure, but every contracted rooted postcondition was independently verified.",
                $"{request.FailureEvidencePrefix}:postcondition-reconciled:{scriptRun.ExitCode}:{string.Join("|", receipts.Select(receipt => $"{receipt.ToolName}:{receipt.ExitSummary}"))}",
                null);
        }

        return new WorkspaceManagedScriptPlanExecutionResult(
            true,
            receipts,
            "Managed script plan completed and satisfied all rooted readback checks.",
            $"{request.FailureEvidencePrefix}:succeeded:{string.Join("|", receipts.Select(receipt => $"{receipt.ToolName}:{receipt.ExitSummary}"))}",
            null);
    }

    private static ToolExecutionReceiptRecord CreatePostconditionVerifiedReceipt(
        WorkspaceManagedScriptPlanExecutionRequest request,
        WorkspaceCommandExecutionResult scriptRun)
        => new(
            Guid.NewGuid(),
            request.ExecutionRunId,
            "process-runtime",
            scriptRun.ToolName,
            PostconditionVerifiedRiskClass,
            "NotRequired",
            "Runtime-owned managed script reconciliation independently verified every contracted rooted postcondition.",
            $"{scriptRun.ArgumentsSummary} postcondition-reconciled",
            scriptRun.WorkingDirectory,
            $"Succeeded: Contracted postconditions were verified after helper exit {scriptRun.ExitCode}; the failed execution receipt remains preserved.",
            scriptRun.Receipt.StartedAtUtc,
            scriptRun.Receipt.CompletedAtUtc)
        {
            DeclaredSideEffectMode = scriptRun.Receipt.DeclaredSideEffectMode
        };

    private static bool TryValidateRequest(
        WorkspaceManagedScriptPlanExecutionRequest request,
        out string issue)
    {
        if (request.ExecutionRunId == Guid.Empty)
        {
            issue = "an execution run id is required.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(request.ScriptRef) ||
            string.IsNullOrWhiteSpace(request.Script) ||
            string.IsNullOrWhiteSpace(request.SideEffectManifest) ||
            string.IsNullOrWhiteSpace(request.WorkingDirectory) ||
            string.IsNullOrWhiteSpace(request.OutputPath) ||
            string.IsNullOrWhiteSpace(request.FailureEvidencePrefix))
        {
            issue = "script, script reference, side-effect manifest, working directory, output path, and evidence prefix are required.";
            return false;
        }

        if (!TryNormalizeProductRoot(request.ProductRoot, out var productRoot))
        {
            issue = "the declared product root is invalid.";
            return false;
        }

        if (request.ReadbackChecks is null || request.ReadbackChecks.Count == 0)
        {
            issue = "at least one rooted readback check is required.";
            return false;
        }

        if (request.ExecutionPolicy is null)
        {
            issue = "an explicit managed-script execution policy is required.";
            return false;
        }

        foreach (var check in request.ReadbackChecks)
        {
            if (check.PathCandidates is null ||
                check.PathCandidates.Count == 0 ||
                check.PathCandidates.Any(string.IsNullOrWhiteSpace) ||
                check.RequiredTextAnyGroups is null ||
                check.RequiredTextAnyGroups.Count == 0 ||
                check.RequiredTextAnyGroups.Any(group => group is null || group.Count == 0 || group.Any(string.IsNullOrWhiteSpace)))
            {
                issue = "every rooted readback check requires non-empty path candidates and text groups.";
                return false;
            }

            foreach (var candidate in check.PathCandidates)
            {
                if (!TryResolveReadbackAlias(productRoot, candidate, out _, out issue))
                {
                    return false;
                }
            }
        }

        issue = string.Empty;
        return true;
    }

    private bool TryValidateReadbacks(
        string productRoot,
        IReadOnlyList<WorkspaceManagedScriptReadbackCheck> checks,
        List<ToolExecutionReceiptRecord> receipts,
        Guid executionRunId,
        out string issue,
        out ProcessRuntimeOwnedStepFailure failure,
        out int positivelyVerifiedRequiredChecks)
    {
        positivelyVerifiedRequiredChecks = 0;
        if (!TryNormalizeProductRoot(productRoot, out var normalizedProductRoot))
        {
            issue = "Required readback cannot resolve ProductRoot.";
            failure = ProcessRuntimeOwnedStepFailures.ContractInvalid;
            return false;
        }

        foreach (var check in checks)
        {
            var foundExistingCandidate = false;
            var foundUnavailableCandidate = false;
            var foundContentMismatch = false;
            var candidateIssues = new List<string>();
            var satisfied = false;
            foreach (var candidate in check.PathCandidates)
            {
                if (!TryResolveReadbackAlias(normalizedProductRoot, candidate, out var alias, out issue))
                {
                    failure = ProcessRuntimeOwnedStepFailures.ContractInvalid;
                    return false;
                }

                var statResult = workspaceFiles.StatPath(alias);
                receipts.Add(From(executionRunId, statResult));
                if (!statResult.Succeeded)
                {
                    foundUnavailableCandidate = true;
                    candidateIssues.Add(
                        $"'{DescribeCandidatePath(normalizedProductRoot, candidate)}' could not be inspected: {statResult.Message}");
                    continue;
                }

                if (!statResult.Exists)
                {
                    continue;
                }

                foundExistingCandidate = true;
                if (!string.Equals(statResult.PathKind, "file", StringComparison.OrdinalIgnoreCase))
                {
                    foundUnavailableCandidate = true;
                    candidateIssues.Add(
                        $"'{DescribeCandidatePath(normalizedProductRoot, candidate)}' is not a file");
                    continue;
                }

                var readResult = workspaceFiles.ReadTextFile(alias, MaximumReadbackCharacters);
                receipts.Add(From(executionRunId, readResult));
                if (!readResult.Succeeded)
                {
                    foundUnavailableCandidate = true;
                    candidateIssues.Add(
                        $"'{DescribeCandidatePath(normalizedProductRoot, candidate)}' could not be read: {readResult.Message}");
                    continue;
                }

                if (readResult.IsTruncated)
                {
                    foundUnavailableCandidate = true;
                    candidateIssues.Add(
                        $"'{DescribeCandidatePath(normalizedProductRoot, candidate)}' exceeded the managed read limit");
                    continue;
                }

                var normalizedContent = NormalizeReadbackText(readResult.Content);
                var hasRequiredText = check.RequiredTextAnyGroups.All(group =>
                    group.Any(value => normalizedContent.Contains(
                        NormalizeReadbackText(value),
                        StringComparison.OrdinalIgnoreCase)));
                if (hasRequiredText)
                {
                    satisfied = true;
                    break;
                }

                foundContentMismatch = true;
                candidateIssues.Add(
                    $"'{DescribeCandidatePath(normalizedProductRoot, candidate)}' did not contain the required content");
            }

            if (satisfied)
            {
                if (check.MustExist)
                {
                    positivelyVerifiedRequiredChecks++;
                }

                continue;
            }

            if (!foundExistingCandidate && !foundUnavailableCandidate)
            {
                if (!check.MustExist)
                {
                    continue;
                }

                issue =
                    $"Required readback path candidates were not found under ProductRoot: {DescribeCandidatePaths(normalizedProductRoot, check.PathCandidates)}.";
                failure = ProcessRuntimeOwnedStepFailures.ReadbackPathMissing;
                return false;
            }

            issue =
                $"Required readback content was not found in any usable candidate: {string.Join("; ", candidateIssues)}.";
            if (foundUnavailableCandidate)
            {
                failure = ProcessRuntimeOwnedStepFailures.ReadbackUnavailable;
                return false;
            }

            if (foundContentMismatch)
            {
                failure = ProcessRuntimeOwnedStepFailures.ReadbackContentMissing;
                return false;
            }

            failure = ProcessRuntimeOwnedStepFailures.ReadbackUnavailable;
            return false;
        }

        issue = string.Empty;
        failure = null!;
        return true;
    }

    private static bool CanReconcileFailedExecution(
        WorkspaceManagedScriptPlanExecutionPolicy policy,
        WorkspaceCommandExecutionResult scriptRun,
        int positivelyVerifiedRequiredChecks)
        => policy.Idempotency == ProcessToolOperationIdempotencyPolicy.CurrentRunRepeatable &&
           policy.FailureReconciliation ==
               ProcessToolOperationFailureReconciliationPolicy.AuthoritativeReadbackConvergence &&
           string.Equals(scriptRun.Receipt.Outcome, "Failed", StringComparison.OrdinalIgnoreCase) &&
           positivelyVerifiedRequiredChecks > 0;

    private static ProcessRuntimeOwnedStepFailure ResolveExecutionFailure(
        WorkspaceManagedScriptPlanExecutionRequest request,
        string? outcome)
        => ProcessRuntimeOwnedStepFailures.ResolveExecutionFailure(
            outcome,
            request.ExecutionPolicy.Idempotency);

    private static ProcessRuntimeOwnedStepFailure ApplyReadbackPolicy(
        WorkspaceManagedScriptPlanExecutionRequest request,
        ProcessRuntimeOwnedStepFailure failure)
        => failure.Code == ProcessRuntimeOwnedStepFailures.ReadbackPathMissing.Code ||
           failure.Code == ProcessRuntimeOwnedStepFailures.ReadbackContentMissing.Code
            ? ProcessRuntimeOwnedStepFailures.ApplyDeclaredIdempotency(
                failure,
                request.ExecutionPolicy.Idempotency)
            : failure;

    private static string DescribeCandidatePaths(
        string productRoot,
        IReadOnlyList<string> candidates)
        => string.Join(
            ", ",
            candidates
                .Select(candidate => $"'{DescribeCandidatePath(productRoot, candidate)}'")
                .Distinct(StringComparer.OrdinalIgnoreCase));

    private static string DescribeCandidatePath(string productRoot, string candidate)
    {
        var resolvedPath = Path.IsPathRooted(candidate)
            ? Path.GetFullPath(candidate)
            : Path.GetFullPath(Path.Combine(productRoot, candidate));
        return Path.GetRelativePath(productRoot, resolvedPath).Replace('\\', '/');
    }

    private static bool TryResolveReadbackAlias(
        string productRoot,
        string candidate,
        out string alias,
        out string issue)
    {
        alias = string.Empty;
        issue = string.Empty;
        if (string.IsNullOrWhiteSpace(candidate))
        {
            issue = "Required readback path is empty.";
            return false;
        }

        string resolvedPath;
        try
        {
            resolvedPath = Path.IsPathRooted(candidate)
                ? Path.GetFullPath(candidate)
                : Path.GetFullPath(Path.Combine(productRoot, candidate));
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            issue = "Required readback path is invalid.";
            return false;
        }

        if (!IsPathWithinProductRoot(resolvedPath, productRoot))
        {
            issue = "Required readback path escapes ProductRoot.";
            return false;
        }

        alias = AgentWorkspaceToolAccessMetadata.NormalizeExternalTargetAlias(resolvedPath) ?? string.Empty;
        if (string.IsNullOrWhiteSpace(alias))
        {
            issue = "Required readback path cannot be represented as a managed external target alias.";
            return false;
        }

        return true;
    }

    private static bool TryNormalizeProductRoot(string productRoot, out string normalizedProductRoot)
    {
        normalizedProductRoot = string.Empty;
        if (string.IsNullOrWhiteSpace(productRoot) || !Path.IsPathRooted(productRoot))
        {
            return false;
        }

        try
        {
            normalizedProductRoot = Path.GetFullPath(productRoot);
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }

    private static bool IsPathWithinProductRoot(string path, string productRoot)
        => path.StartsWith(EnsureTrailingDirectorySeparator(productRoot), StringComparison.OrdinalIgnoreCase);

    private static string EnsureTrailingDirectorySeparator(string path)
        => path.EndsWith(Path.DirectorySeparatorChar) || path.EndsWith(Path.AltDirectorySeparatorChar)
            ? path
            : path + Path.DirectorySeparatorChar;

    private static string NormalizeReadbackText(string value)
        => value.Replace('\\', '/').ReplaceLineEndings("\n");

    private static WorkspaceManagedScriptPlanExecutionResult Failure(
        IReadOnlyList<ToolExecutionReceiptRecord> receipts,
        string summary,
        string evidence,
        ProcessRuntimeOwnedStepFailure failure)
        => new(false, receipts, summary, evidence, failure);
}
