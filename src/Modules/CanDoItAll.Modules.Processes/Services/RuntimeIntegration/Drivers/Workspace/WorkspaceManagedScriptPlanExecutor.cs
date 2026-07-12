using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

using static CanDoItAll.Modules.Processes.ProcessRuntimeOwnedToolReceiptFactory;

namespace CanDoItAll.Modules.Processes;

internal sealed class WorkspaceManagedScriptPlanExecutor(
    IWorkspaceFileService workspaceFiles,
    IWorkspaceCommandExecutionService workspaceCommands)
{
    private const int MaximumReadbackCharacters = 200000;

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
                $"{request.FailureEvidencePrefix}:validation:{validationIssue}");
        }

        var receipts = new List<ToolExecutionReceiptRecord>();
        var writeScript = workspaceFiles.WriteTextFile(request.ScriptRef, request.Script, overwrite: true);
        receipts.Add(From(request.ExecutionRunId, writeScript));
        if (!writeScript.Succeeded)
        {
            return Failure(
                receipts,
                $"Managed script plan could not write helper script '{request.ScriptRef}': {writeScript.Message}",
                $"{request.FailureEvidencePrefix}:script-write:{request.ScriptRef}:{writeScript.Message}");
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
                $"{request.FailureEvidencePrefix}:script-stat:{request.ScriptRef}:{scriptStat.Message}");
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
        if (!scriptRun.Succeeded)
        {
            return Failure(
                receipts,
                $"Managed script plan helper failed: {scriptRun.Message}",
                $"{request.FailureEvidencePrefix}:script-run:{scriptRun.ExitCode}:{scriptRun.Message}:{scriptRun.StderrPreview}");
        }

        if (!TryValidateReadbacks(request.ProductRoot, request.ReadbackChecks, receipts, request.ExecutionRunId, out var readbackIssue))
        {
            return Failure(
                receipts,
                readbackIssue,
                $"{request.FailureEvidencePrefix}:readback:{readbackIssue}");
        }

        return new WorkspaceManagedScriptPlanExecutionResult(
            true,
            receipts,
            "Managed script plan completed and satisfied all rooted readback checks.",
            $"{request.FailureEvidencePrefix}:succeeded:{string.Join("|", receipts.Select(receipt => $"{receipt.ToolName}:{receipt.ExitSummary}"))}");
    }

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
        out string issue)
    {
        if (!TryNormalizeProductRoot(productRoot, out var normalizedProductRoot))
        {
            issue = "Required readback cannot resolve ProductRoot.";
            return false;
        }

        foreach (var check in checks)
        {
            var foundReadableCandidate = false;
            foreach (var candidate in check.PathCandidates)
            {
                if (!TryResolveReadbackAlias(normalizedProductRoot, candidate, out var alias, out issue))
                {
                    return false;
                }

                var readResult = workspaceFiles.ReadTextFile(alias, MaximumReadbackCharacters);
                receipts.Add(From(executionRunId, readResult));
                if (!readResult.Succeeded)
                {
                    continue;
                }

                foundReadableCandidate = true;
                if (readResult.IsTruncated)
                {
                    issue = "Required readback content exceeded the managed read limit.";
                    return false;
                }

                var normalizedContent = NormalizeReadbackText(readResult.Content);
                var hasRequiredText = check.RequiredTextAnyGroups.All(group =>
                    group.Any(value => normalizedContent.Contains(
                        NormalizeReadbackText(value),
                        StringComparison.OrdinalIgnoreCase)));
                if (!hasRequiredText)
                {
                    issue = "Required readback content was not found.";
                    return false;
                }

                break;
            }

            if (!foundReadableCandidate && check.MustExist)
            {
                issue = "Required readback path was not found.";
                return false;
            }
        }

        issue = string.Empty;
        return true;
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
        string evidence)
        => new(false, receipts, summary, evidence);
}
