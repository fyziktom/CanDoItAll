using System.Globalization;
using System.Diagnostics.CodeAnalysis;
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
using CanDoItAll.Processes.Contracts;
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

using static CanDoItAll.Modules.Processes.ProcessAgentRightsDiagnosticPolicy;
using static CanDoItAll.Modules.Processes.ProcessCompletionRuleParser;
using static CanDoItAll.Modules.Processes.ProcessProductCompletionRuleParser;
using static CanDoItAll.Modules.Processes.ProcessProductRootResolver;
using static CanDoItAll.Modules.Processes.ProcessCompletionText;
using static CanDoItAll.Modules.Processes.ProcessExecutionResultFactory;
using static CanDoItAll.Modules.Processes.ProcessManagedArtifactEvidence;
using static CanDoItAll.Modules.Processes.ProcessRequiredReceiptMatcher;

namespace CanDoItAll.Modules.Processes;

internal static class ProcessRequiredReceiptRetryPolicy
{
    internal static bool TryCreateProductRequiredToolReceiptBlockedRetryIssue(
        ProcessRuntimeStepAssignment assignment,
        ProcessStepOutcomeResult output,
        IReadOnlyList<ToolExecutionReceiptRecord>? toolReceipts,
        ProcessToolReceiptPolicyCatalog toolReceiptPolicies,
        out ProcessCompletionIssue issue)
    {
        issue = null!;
        if (output.Status != ProcessStepOutcomeStatus.Blocked)
        {
            return false;
        }

        var requiredToolReceipts = ResolveApplicableProductCompletionRequiredToolReceiptRules(assignment, output.BranchOutcomeKey)
            .Select(rule => rule.ToolReceipt)
            .Where(receipt => !string.IsNullOrWhiteSpace(receipt))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (!ShouldEnforceRequiredProductToolReceipts(assignment, requiredToolReceipts))
        {
            return false;
        }

        var observedToolReceipts = toolReceipts ?? [];
        var allowFailedExecutionReceipt = AllowsFailedRequiredToolReceipt(assignment);
        var missingToolReceipts = requiredToolReceipts
            .Where(requiredToolReceipt => !HasRequiredToolReceipt(
                observedToolReceipts,
                requiredToolReceipt,
                allowFailedExecutionReceipt,
                toolReceiptPolicies))
            .ToArray();
        var outputReportsMissingRequiredToolReceipts = OutputReportsMissingRequiredToolReceipts(
            output,
            requiredToolReceipts,
            toolReceiptPolicies);
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
            allowFailedExecutionReceipt,
            toolReceiptPolicies);
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

    internal static bool TryCreateProcessRequiredToolReceiptBlockedRetryIssue(
        ProcessRuntimeStepAssignment assignment,
        ProcessStepOutcomeResult output,
        IReadOnlyList<ToolExecutionReceiptRecord>? toolReceipts,
        Guid? currentExecutionRunId,
        ProcessToolReceiptPolicyCatalog toolReceiptPolicies,
        out ProcessCompletionIssue issue)
    {
        issue = null!;
        if (output.Status != ProcessStepOutcomeStatus.Blocked)
        {
            return false;
        }

        var activeLaunchContextToolNames = ResolveActiveLaunchContextToolNameSet(assignment);
        var gate = ProcessRequiredToolReceiptGate.Evaluate(
            assignment,
            toolReceipts,
            activeLaunchContextToolNames,
            currentExecutionRunId,
            output.BranchOutcomeKey,
            ResolveEnforcedProductCoveredRuntimeToolNames(
                assignment,
                ResolveApplicableProductCompletionRequiredToolReceiptRules(
                    assignment,
                    output.BranchOutcomeKey)));
        if (gate.RequiredReceipts.Count == 0)
        {
            return false;
        }

        var outputReportsMissingRequiredToolReceipts = OutputReportsMissingRequiredProcessToolReceipts(
            output,
            gate.RequiredReceipts,
            toolReceiptPolicies);
        if (gate.MissingReceipts.Count == 0 && !outputReportsMissingRequiredToolReceipts)
        {
            return false;
        }

        var observedToolReceipts = toolReceipts ?? [];
        var text = string.Join(
            " ",
            EnumerateOutcomeText(output).Where(value => !string.IsNullOrWhiteSpace(value)));
        if (gate.MissingReceipts.Count > 0 &&
            LooksLikeRightsOrToolBoundary(text) &&
            HasConcreteToolBoundaryReceipt(observedToolReceipts))
        {
            return false;
        }

        var retryReceipts = gate.MissingReceipts.Count == 0
            ? gate.RequiredReceipts
            : gate.MissingReceipts;
        var missingSummary = ProcessRequiredToolReceiptGate.FormatMissingSummary(retryReceipts);
        var primaryRef = BuildManagedStepArtifactPath(assignment);
        var originalReason = string.IsNullOrWhiteSpace(output.Reason)
            ? "The step returned Blocked before all required process tool receipts were present."
            : output.Reason.Trim();
        var receiptGateGuidance = gate.MissingReceipts.Count == 0
            ? "The step output itself reported missing required process receipt evidence even though matching receipt records are present in the current run. Retry the same step and reconcile the primary managed artifact, branch outcome, and evidence refs with those receipts before routing to a branch or manager."
            : $"Step '{assignment.StepKey}' returned Blocked while required current-run process tool receipt(s) are still missing: {missingSummary}. Retry the same step, invoke the missing required tool receipt(s), update primary managed artifact '{primaryRef}', and return Blocked only for a concrete tool, permission, policy, environment, provider, or process-contract blocker.";
        var receiptSummary = string.Join("|", observedToolReceipts.Select(receipt =>
            $"{receipt.ToolName}:{receipt.RuntimeToolProviderKey}:{receipt.RequestSummary}:{receipt.ExitSummary}"));
        issue = new ProcessCompletionIssue(
            "process.adapter.required_tool_receipt_blocked_retry",
            $"{receiptGateGuidance} Original reason: {originalReason}",
            $"{assignment.RunId}:{assignment.StepInstanceId}:required-tool-receipt-blocked-retry:{missingSummary}:{ComputeHash(receiptSummary)}:{ComputeHash(originalReason)}",
            assignment.ProducedArtifactSlotIds,
            ProcessDiagnosticRetrySafety.SafeToRetry,
            ProcessDiagnosticIdempotencyClassification.Idempotent);
        return true;
    }

    internal static bool OutputReportsMissingRequiredToolReceipts(
        ProcessStepOutcomeResult output,
        IReadOnlyList<string> requiredToolReceipts,
        ProcessToolReceiptPolicyCatalog toolReceiptPolicies)
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
            EnumerateRequiredToolReceiptSearchTerms(requiredToolReceipt, toolReceiptPolicies)
                .Any(term => normalizedText.Contains(term, StringComparison.OrdinalIgnoreCase)));
    }

    internal static bool LooksLikeMissingRequiredEvidence(string normalizedText)
        => normalizedText.Contains("missing", StringComparison.OrdinalIgnoreCase) ||
           normalizedText.Contains("not yet", StringComparison.OrdinalIgnoreCase) ||
           normalizedText.Contains("not produced", StringComparison.OrdinalIgnoreCase) ||
           normalizedText.Contains("not been produced", StringComparison.OrdinalIgnoreCase) ||
           normalizedText.Contains("no current-run", StringComparison.OrdinalIgnoreCase) ||
           normalizedText.Contains("no current run", StringComparison.OrdinalIgnoreCase);

    internal static IEnumerable<string> EnumerateRequiredToolReceiptSearchTerms(
        string requiredToolReceipt,
        ProcessToolReceiptPolicyCatalog toolReceiptPolicies)
    {
        var normalizedRequirement = NormalizeReceiptCommandText(requiredToolReceipt).ToLowerInvariant();
        if (normalizedRequirement.Length == 0)
        {
            yield break;
        }

        yield return normalizedRequirement;
        foreach (var policyTerm in toolReceiptPolicies.EnumerateRequirementSearchTerms(normalizedRequirement))
        {
            yield return NormalizeReceiptCommandText(policyTerm).ToLowerInvariant();
        }
    }

    internal static IReadOnlySet<string> ResolveActiveLaunchContextToolNameSet(ProcessRuntimeStepAssignment assignment)
    {
        return ProcessRequiredRuntimeToolNames
            .FromProductCompletionRequiredToolReceipts(ResolveProductCompletionRequiredToolReceipts(
                assignment.LaunchVariables,
                assignment.StepKey))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    internal static bool OutputReportsMissingRequiredProcessToolReceipts(
        ProcessStepOutcomeResult output,
        IReadOnlyList<ProcessRequiredToolReceipt> requiredReceipts,
        ProcessToolReceiptPolicyCatalog toolReceiptPolicies)
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

        return requiredReceipts.Any(requiredReceipt =>
            EnumerateRequiredProcessToolReceiptSearchTerms(requiredReceipt, toolReceiptPolicies)
                .Any(term => normalizedText.Contains(term, StringComparison.OrdinalIgnoreCase)));
    }

    internal static IEnumerable<string> EnumerateRequiredProcessToolReceiptSearchTerms(
        ProcessRequiredToolReceipt requiredReceipt,
        ProcessToolReceiptPolicyCatalog toolReceiptPolicies)
    {
        foreach (var value in new[]
                 {
                     requiredReceipt.Key,
                     requiredReceipt.ToolName,
                     requiredReceipt.RuntimeToolProviderKey,
                     requiredReceipt.McpServerKey
                 })
        {
            var normalized = NormalizeReceiptCommandText(value).ToLowerInvariant();
            if (!string.IsNullOrWhiteSpace(normalized))
            {
                yield return normalized;
            }
        }

        if (!string.IsNullOrWhiteSpace(requiredReceipt.ToolName))
        {
            foreach (var term in EnumerateRequiredToolReceiptSearchTerms(requiredReceipt.ToolName, toolReceiptPolicies))
            {
                yield return term;
            }
        }
    }

    internal static bool HasRecoverableRequiredScriptHelperOrderingEvidence(
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

    internal static bool IsWorkspaceScriptExecutionTool(string toolName)
        => string.Equals(toolName, "workspace_pwsh_run_script", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(toolName, "workspace_python_run_file", StringComparison.OrdinalIgnoreCase);

    internal static bool ReceiptTargetsCurrentRunScript(string requestSummary, ProcessRunId runId)
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
