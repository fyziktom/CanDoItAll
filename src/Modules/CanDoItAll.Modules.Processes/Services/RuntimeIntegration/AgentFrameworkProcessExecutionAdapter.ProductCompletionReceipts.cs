using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.AgentFramework.Hosting;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Builder;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Drivers.Standard;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Projections;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.Processes.Templates;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Modules.Processes;

internal sealed partial class AgentFrameworkProcessExecutionAdapter
{
    private static ProcessCompletionIssue? ValidateProductMutationWriteReceipt(
        ProcessRuntimeStepAssignment assignment,
        ProcessStepOutcomeResult output,
        IReadOnlyList<ToolExecutionReceiptRecord>? toolReceipts)
    {
        if (output.Status != ProcessStepOutcomeStatus.Completed ||
            toolReceipts is null ||
            !AllowsProductMutation(NormalizeOperations(assignment.AllowedOperations), assignment.OperationTargetScope))
        {
            return null;
        }

        var productTargetRefs = ResolveProductTargetReceiptRefs(assignment.LaunchVariables);
        if (productTargetRefs.Count == 0 ||
            HasProductMutationReceipt(toolReceipts, productTargetRefs) ||
            CanAcceptBranchGatedValidationOnlyCompletion(assignment, toolReceipts, productTargetRefs))
        {
            return null;
        }

        var targetSummary = string.Join("; ", productTargetRefs);
        return new ProcessCompletionIssue(
            "process.adapter.product_mutation_receipt_missing",
            $"Step '{assignment.StepKey}' claimed completion for a product-mutating scope but did not produce a successful product-target mutation receipt for {targetSummary}. Retry the same step by mutating the required product source or test files under the grounded product target with a product mutation tool before writing the final managed artifact; writing only artifacts/process-runs/... is not product mutation.",
            $"{assignment.RunId}:{assignment.StepInstanceId}:product-mutation-receipt-missing:{string.Join("|", toolReceipts.Select(receipt => $"{receipt.ToolName}:{receipt.RequestSummary}:{receipt.ExitSummary}"))}",
            [],
            ProcessDiagnosticRetrySafety.SafeToRetry,
            ProcessDiagnosticIdempotencyClassification.Idempotent);
    }

    private static ProcessCompletionIssue? ValidateRequiredProductToolReceipts(
        ProcessRuntimeStepAssignment assignment,
        IReadOnlyList<ToolExecutionReceiptRecord>? toolReceipts)
    {
        var requiredToolReceipts = ResolveProductCompletionRequiredToolReceipts(assignment.LaunchVariables, assignment.StepKey);
        if (!ShouldEnforceRequiredProductToolReceipts(assignment, requiredToolReceipts))
        {
            return null;
        }

        var observedToolReceipts = toolReceipts ?? [];
        var allowFailedExecutionReceipt = AllowsFailedRequiredToolReceipt(assignment);
        var missingToolReceipts = requiredToolReceipts
            .Where(requiredToolReceipt => !HasRequiredToolReceipt(observedToolReceipts, requiredToolReceipt, allowFailedExecutionReceipt))
            .ToArray();
        if (missingToolReceipts.Length == 0)
        {
            return null;
        }

        var missingSummary = string.Join("; ", missingToolReceipts);
        var failedReceiptGuidance = BuildFailedRequiredToolReceiptGuidance(
            assignment,
            observedToolReceipts,
            missingToolReceipts,
            allowFailedExecutionReceipt);
        var missingReceiptGuidance = BuildMissingRequiredToolReceiptGuidance(assignment, missingToolReceipts);
        return new ProcessCompletionIssue(
            "process.adapter.product_required_tool_receipt_missing",
            $"Step '{assignment.StepKey}' claimed completion but required current-run product tool receipt(s) are missing: {missingSummary}.{failedReceiptGuidance}{missingReceiptGuidance}",
            $"{assignment.RunId}:{assignment.StepInstanceId}:product-required-tool-receipt-missing:{missingSummary}:{string.Join("|", observedToolReceipts.Select(receipt => $"{receipt.ToolName}:{receipt.RequestSummary}:{receipt.ExitSummary}"))}",
            assignment.ProducedArtifactSlotIds,
            ProcessDiagnosticRetrySafety.SafeToRetry,
            ProcessDiagnosticIdempotencyClassification.Idempotent);
    }

    private static bool ShouldEnforceRequiredProductToolReceipts(
        ProcessRuntimeStepAssignment assignment,
        IReadOnlyList<string> requiredToolReceipts)
    {
        if (requiredToolReceipts.Count == 0)
        {
            return false;
        }

        var operations = NormalizeOperations(assignment.AllowedOperations);
        return AllowsProductMutation(operations, assignment.OperationTargetScope) ||
               operations.Contains(ProcessOperationContractNames.RunValidation, StringComparer.OrdinalIgnoreCase) ||
               operations.Contains(ProcessOperationContractNames.LaunchRuntime, StringComparer.OrdinalIgnoreCase) ||
               operations.Contains(ProcessOperationContractNames.CaptureRuntimeProof, StringComparer.OrdinalIgnoreCase) ||
               operations.Contains(ProcessOperationContractNames.ExecuteExternalAction, StringComparer.OrdinalIgnoreCase);
    }

    private static bool AllowsFailedRequiredToolReceipt(ProcessRuntimeStepAssignment assignment)
    {
        var operations = NormalizeOperations(assignment.AllowedOperations);
        return operations.Contains(ProcessOperationContractNames.RunValidation, StringComparer.OrdinalIgnoreCase) &&
               !AllowsProductMutation(operations, assignment.OperationTargetScope);
    }

    private static bool HasRequiredToolReceipt(
        IReadOnlyList<ToolExecutionReceiptRecord> toolReceipts,
        string requiredToolReceipt,
        bool allowFailedExecutionReceipt)
    {
        var normalizedRequirement = requiredToolReceipt.Trim();
        if (TryResolveDotNetNewTemplateRequirement(normalizedRequirement, out var requiredTemplate))
        {
            return toolReceipts.Any(receipt =>
                IsSuccessfulReceipt(receipt.ExitSummary) &&
                IsRequiredToolReceiptMatch(receipt, normalizedRequirement));
        }

        return !string.IsNullOrWhiteSpace(normalizedRequirement) &&
               toolReceipts.Any(receipt =>
                   IsRequiredToolReceiptUsable(receipt, allowFailedExecutionReceipt) &&
                   IsRequiredToolReceiptMatch(receipt, normalizedRequirement));
    }

    private static bool IsRequiredToolReceiptMatch(
        ToolExecutionReceiptRecord receipt,
        string normalizedRequirement)
    {
        if (string.IsNullOrWhiteSpace(normalizedRequirement))
        {
            return false;
        }

        if (TryResolveDotNetNewTemplateRequirement(normalizedRequirement, out var requiredTemplate))
        {
            return IsDotNetNewTemplateReceipt(receipt, requiredTemplate);
        }

        if (LooksLikeConcreteToolName(normalizedRequirement))
        {
            return string.Equals(receipt.ToolName, normalizedRequirement, StringComparison.OrdinalIgnoreCase);
        }

        return string.Equals(receipt.ToolName, normalizedRequirement, StringComparison.OrdinalIgnoreCase) ||
               ReceiptText(receipt).Contains(normalizedRequirement, StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksLikeConcreteToolName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return Regex.IsMatch(
            value.Trim(),
            @"^[a-z][a-z0-9]*(?:_[a-z0-9]+)+$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private static string BuildFailedRequiredToolReceiptGuidance(
        ProcessRuntimeStepAssignment assignment,
        IReadOnlyList<ToolExecutionReceiptRecord> observedToolReceipts,
        IReadOnlyList<string> missingToolReceipts,
        bool allowFailedExecutionReceipt)
    {
        if (allowFailedExecutionReceipt || missingToolReceipts.Count == 0 || observedToolReceipts.Count == 0)
        {
            return string.Empty;
        }

        var failedMatches = missingToolReceipts
            .SelectMany(requiredToolReceipt =>
            {
                var normalizedRequirement = requiredToolReceipt.Trim();
                if (string.IsNullOrWhiteSpace(normalizedRequirement))
                {
                    return Array.Empty<string>();
                }

                return observedToolReceipts
                    .Where(receipt =>
                        IsRequiredToolReceiptMatch(receipt, normalizedRequirement) &&
                        !IsSuccessfulReceipt(receipt.ExitSummary) &&
                        !IsConcreteToolBoundaryReceipt(receipt))
                    .Select(receipt => $"{receipt.ToolName} ({SummarizeReceiptExit(receipt.ExitSummary)})");
            })
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(5)
            .ToArray();
        if (failedMatches.Length == 0)
        {
            return string.Empty;
        }

        var operations = NormalizeOperations(assignment.AllowedOperations);
        var repairGuidance = AllowsProductMutation(operations, assignment.OperationTargetScope)
            ? " For product-mutating steps, inspect the failing command output, mutate the product target before rerunning validation, and complete only after the required receipts succeed."
            : " Retry the required commands and complete only after the required receipts succeed.";

        return $" Matching current-run receipt(s) were present but failed: {string.Join("; ", failedMatches)}.{repairGuidance}";
    }

    private static string BuildMissingRequiredToolReceiptGuidance(
        ProcessRuntimeStepAssignment assignment,
        IReadOnlyList<string> missingToolReceipts)
    {
        if (!missingToolReceipts.Any(required =>
                string.Equals(required.Trim(), "workspace_pwsh_run_script", StringComparison.OrdinalIgnoreCase)))
        {
            return string.Empty;
        }

        if (!TryResolveStepScriptLaunchVariableNames(
                assignment,
                out var scriptVariableName,
                out var scriptRefVariableName,
                out var manifestVariableName))
        {
            return " Before retrying, invoke the reviewed current-run helper with workspace_pwsh_run_script and read back the affected product files before rewriting the primary managed artifact.";
        }

        var scriptRef = assignment.LaunchVariables.TryGetValue(scriptRefVariableName, out var configuredScriptRef) &&
                        !string.IsNullOrWhiteSpace(configuredScriptRef)
            ? configuredScriptRef.Trim()
            : scriptRefVariableName;
        var manifestGuidance = string.IsNullOrWhiteSpace(manifestVariableName)
            ? string.Empty
            : $" and sideEffectManifest from {manifestVariableName}";

        return $" Before retrying, write launch variable {scriptVariableName} verbatim to '{scriptRef}', verify that .ps1 ref, invoke workspace_pwsh_run_script with path '{scriptRef}'{manifestGuidance}, then read back the product files and rewrite the primary managed artifact only after the script receipt exists.";
    }

    private static bool TryResolveStepScriptLaunchVariableNames(
        ProcessRuntimeStepAssignment assignment,
        out string scriptVariableName,
        out string scriptRefVariableName,
        out string manifestVariableName)
    {
        var prefix = assignment.StepKey switch
        {
            "create-dotnet-project" => "DotNetCreateProject",
            "add-test-project" => "DotNetAddTestProject",
            "repair-solution-setup" when assignment.LaunchVariables.ContainsKey("DotNetAddTestProjectScriptRef") => "DotNetAddTestProject",
            "repair-solution-setup" => "DotNetCreateProject",
            _ => string.Empty
        };

        if (string.IsNullOrWhiteSpace(prefix))
        {
            scriptVariableName = string.Empty;
            scriptRefVariableName = string.Empty;
            manifestVariableName = string.Empty;
            return false;
        }

        scriptVariableName = $"{prefix}Script";
        scriptRefVariableName = $"{prefix}ScriptRef";
        manifestVariableName = $"{prefix}SideEffectManifest";
        return assignment.LaunchVariables.ContainsKey(scriptVariableName) ||
               assignment.LaunchVariables.ContainsKey(scriptRefVariableName);
    }

    private static string SummarizeReceiptExit(string exitSummary)
    {
        var normalized = NormalizeReceiptCommandText(exitSummary);
        return normalized.Length <= 160 ? normalized : normalized[..160];
    }

    private static bool TryResolveDotNetNewTemplateRequirement(string requiredToolReceipt, out string requiredTemplate)
    {
        requiredTemplate = string.Empty;
        if (string.IsNullOrWhiteSpace(requiredToolReceipt))
        {
            return false;
        }

        const string prefix = "template=";
        if (!requiredToolReceipt.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        requiredTemplate = requiredToolReceipt[prefix.Length..].Trim();
        return !string.IsNullOrWhiteSpace(requiredTemplate);
    }

    private static bool IsDotNetNewTemplateReceipt(
        ToolExecutionReceiptRecord receipt,
        string requiredTemplate)
    {
        if (!string.Equals(receipt.ToolName, "workspace_dotnet_new", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var requestSummary = NormalizeReceiptCommandText(receipt.RequestSummary);
        var requiredTemplateText = NormalizeReceiptCommandText(requiredTemplate);
        if (requestSummary.Length == 0 || requiredTemplateText.Length == 0)
        {
            return false;
        }

        return string.Equals(requestSummary, $"new {requiredTemplateText}", StringComparison.OrdinalIgnoreCase) ||
               requestSummary.StartsWith($"new {requiredTemplateText} ", StringComparison.OrdinalIgnoreCase) ||
               requestSummary.Contains($" template={requiredTemplateText} ", StringComparison.OrdinalIgnoreCase) ||
               requestSummary.Contains($" --template {requiredTemplateText} ", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeReceiptCommandText(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value
            .Replace('"', ' ')
            .Replace('\'', ' ')
            .ReplaceLineEndings(" ")
            .Trim();
        return string.Join(
            " ",
            normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    private static string ReceiptText(ToolExecutionReceiptRecord receipt)
        => $"{receipt.ToolName} {receipt.RequestSummary} {receipt.WorkingDirectory} {receipt.ExitSummary}";

    private static bool TryCreateProductRequiredToolReceiptBlockedRetryIssue(
        ProcessRuntimeStepAssignment assignment,
        ProcessStepOutcomeResult output,
        IReadOnlyList<ToolExecutionReceiptRecord>? toolReceipts,
        out ProcessCompletionIssue issue)
    {
        issue = null!;
        if (output.Status != ProcessStepOutcomeStatus.Blocked)
        {
            return false;
        }

        var requiredToolReceipts = ResolveProductCompletionRequiredToolReceipts(assignment.LaunchVariables, assignment.StepKey);
        if (!ShouldEnforceRequiredProductToolReceipts(assignment, requiredToolReceipts))
        {
            return false;
        }

        var observedToolReceipts = toolReceipts ?? [];
        var allowFailedExecutionReceipt = AllowsFailedRequiredToolReceipt(assignment);
        var missingToolReceipts = requiredToolReceipts
            .Where(requiredToolReceipt => !HasRequiredToolReceipt(observedToolReceipts, requiredToolReceipt, allowFailedExecutionReceipt))
            .ToArray();
        var outputReportsMissingRequiredToolReceipts = OutputReportsMissingRequiredToolReceipts(output, requiredToolReceipts);
        if (missingToolReceipts.Length == 0 && !outputReportsMissingRequiredToolReceipts)
        {
            return false;
        }

        var retryToolReceipts = missingToolReceipts.Length == 0
            ? requiredToolReceipts.Where(requiredToolReceipt => !string.IsNullOrWhiteSpace(requiredToolReceipt)).ToArray()
            : missingToolReceipts;
        var hasRecoverableScriptHelperOrdering = HasRecoverableRequiredScriptHelperOrderingEvidence(
            assignment,
            retryToolReceipts,
            observedToolReceipts);
        var text = string.Join(
            " ",
            EnumerateOutcomeText(output).Where(value => !string.IsNullOrWhiteSpace(value)));
        if (!hasRecoverableScriptHelperOrdering &&
            LooksLikeRightsOrToolBoundary(text) &&
            HasConcreteToolBoundaryReceipt(observedToolReceipts))
        {
            return false;
        }

        var missingSummary = string.Join("; ", retryToolReceipts);
        var primaryRef = BuildManagedStepArtifactPath(assignment);
        var originalReason = string.IsNullOrWhiteSpace(output.Reason)
            ? "The step returned Blocked before all required product tool receipts were present."
            : output.Reason.Trim();
        var receiptGateGuidance = missingToolReceipts.Length == 0
            ? "The step output itself reported missing required receipt evidence even though matching receipt records are present in the current run. Retry the same step and reconcile the primary managed artifact, branch outcome, and evidence refs with those receipts before routing to a branch or manager."
            : $"Step '{assignment.StepKey}' returned Blocked while required current-run product tool receipt(s) are still missing: {missingSummary}. Retry the same step, invoke the missing required tool receipt(s), update primary managed artifact '{primaryRef}', and return Blocked only for a concrete tool, permission, policy, or environment blocker.";
        var failedReceiptGuidance = BuildFailedRequiredToolReceiptGuidance(
            assignment,
            observedToolReceipts,
            retryToolReceipts,
            allowFailedExecutionReceipt);
        var scriptHelperOrderingGuidance = hasRecoverableScriptHelperOrdering
            ? " A required script execution was denied before a current-run helper script was available, but the same run now has a successful helper script write receipt. Retry by verifying that helper path and invoking the missing script execution tool before returning a final status. This is not a manager grant or reassignment case unless the verified retry is denied for a concrete policy, permission, or environment boundary."
            : string.Empty;
        var receiptSummary = string.Join("|", observedToolReceipts.Select(receipt => $"{receipt.ToolName}:{receipt.RequestSummary}:{receipt.ExitSummary}"));
        issue = new ProcessCompletionIssue(
            "process.adapter.product_required_tool_receipt_blocked_retry",
            $"{receiptGateGuidance}{failedReceiptGuidance}{scriptHelperOrderingGuidance} Original reason: {originalReason}",
            $"{assignment.RunId}:{assignment.StepInstanceId}:product-required-tool-receipt-blocked-retry:{missingSummary}:{ComputeHash(receiptSummary)}:{ComputeHash(originalReason)}",
            assignment.ProducedArtifactSlotIds,
            ProcessDiagnosticRetrySafety.SafeToRetry,
            ProcessDiagnosticIdempotencyClassification.Idempotent);
        return true;
    }

    private static bool OutputReportsMissingRequiredToolReceipts(
        ProcessStepOutcomeResult output,
        IReadOnlyList<string> requiredToolReceipts)
    {
        var normalizedText = NormalizeReceiptCommandText(string.Join(
                " ",
                EnumerateOutcomeText(output).Where(value => !string.IsNullOrWhiteSpace(value))))
            .ToLowerInvariant();
        if (normalizedText.Length == 0 ||
            !LooksLikeMissingRequiredEvidence(normalizedText))
        {
            return false;
        }

        return requiredToolReceipts.Any(requiredToolReceipt =>
            EnumerateRequiredToolReceiptSearchTerms(requiredToolReceipt)
                .Any(term => normalizedText.Contains(term, StringComparison.OrdinalIgnoreCase)));
    }

    private static bool LooksLikeMissingRequiredEvidence(string normalizedText)
        => normalizedText.Contains("missing", StringComparison.OrdinalIgnoreCase) ||
           normalizedText.Contains("not yet", StringComparison.OrdinalIgnoreCase) ||
           normalizedText.Contains("not produced", StringComparison.OrdinalIgnoreCase) ||
           normalizedText.Contains("not been produced", StringComparison.OrdinalIgnoreCase) ||
           normalizedText.Contains("no current-run", StringComparison.OrdinalIgnoreCase) ||
           normalizedText.Contains("no current run", StringComparison.OrdinalIgnoreCase);

    private static IEnumerable<string> EnumerateRequiredToolReceiptSearchTerms(string requiredToolReceipt)
    {
        var normalizedRequirement = NormalizeReceiptCommandText(requiredToolReceipt).ToLowerInvariant();
        if (normalizedRequirement.Length == 0)
        {
            yield break;
        }

        yield return normalizedRequirement;
        if (TryResolveDotNetNewTemplateRequirement(normalizedRequirement, out var requiredTemplate))
        {
            yield return NormalizeReceiptCommandText(requiredTemplate).ToLowerInvariant();
            yield return $"template {NormalizeReceiptCommandText(requiredTemplate).ToLowerInvariant()}";
            yield break;
        }

        const string workspaceDotNetPrefix = "workspace_dotnet_";
        if (normalizedRequirement.StartsWith(workspaceDotNetPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var verb = normalizedRequirement[workspaceDotNetPrefix.Length..];
            if (!string.IsNullOrWhiteSpace(verb))
            {
                yield return verb;
                yield return $"dotnet {verb}";
            }
        }
    }

    private static bool HasRecoverableRequiredScriptHelperOrderingEvidence(
        ProcessRuntimeStepAssignment assignment,
        IReadOnlyList<string> missingToolReceipts,
        IReadOnlyList<ToolExecutionReceiptRecord> toolReceipts)
    {
        if (!missingToolReceipts.Any(IsWorkspaceScriptExecutionTool))
        {
            return false;
        }

        return toolReceipts.Any(receipt =>
                   IsWorkspaceScriptExecutionTool(receipt.ToolName) &&
                   !IsSuccessfulReceipt(receipt.ExitSummary)) &&
               toolReceipts.Any(receipt =>
                   IsManagedArtifactWriteTool(receipt.ToolName) &&
                   IsSuccessfulReceipt(receipt.ExitSummary) &&
                   ReceiptTargetsCurrentRunScript(receipt.RequestSummary, assignment.RunId));
    }

    private static bool IsWorkspaceScriptExecutionTool(string toolName)
        => string.Equals(toolName, "workspace_pwsh_run_script", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(toolName, "workspace_python_run_file", StringComparison.OrdinalIgnoreCase);

    private static bool ReceiptTargetsCurrentRunScript(string requestSummary, ProcessRunId runId)
    {
        var normalizedRequest = NormalizeManagedArtifactRef(requestSummary);
        if (!normalizedRequest.Contains($"process-runs/{runId}/", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return normalizedRequest.Contains(".ps1", StringComparison.OrdinalIgnoreCase) ||
               normalizedRequest.Contains(".py", StringComparison.OrdinalIgnoreCase);
    }

}
