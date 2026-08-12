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

using static CanDoItAll.Modules.Processes.ProcessAgentRightsDiagnosticPolicy;
using static CanDoItAll.Modules.Processes.ProcessCompletionRuleParser;
using static CanDoItAll.Modules.Processes.ProcessProductCompletionRuleParser;
using static CanDoItAll.Modules.Processes.ProcessProductRootResolver;
using static CanDoItAll.Modules.Processes.ProcessCompletionText;
using static CanDoItAll.Modules.Processes.ProcessExecutionMetadataBuilder;
using static CanDoItAll.Modules.Processes.ProcessExecutionResultFactory;
using static CanDoItAll.Modules.Processes.ProcessManagedArtifactEvidence;

namespace CanDoItAll.Modules.Processes;

internal static class ProcessProductCompletionRetryPolicy
{
    internal static bool TryCreateProductRequiredStateBlockedRetryIssue(
        ProcessRuntimeStepAssignment assignment,
        ProcessStepOutcomeResult output,
        IReadOnlyList<ToolExecutionReceiptRecord>? toolReceipts,
        ProcessProductCompletionPathGate productCompletionPathGate,
        out ProcessCompletionIssue issue)
    {
        issue = null!;
        if (output.Status != ProcessStepOutcomeStatus.Blocked ||
            toolReceipts is null ||
            !AllowsProductMutation(NormalizeOperations(assignment.AllowedOperations), assignment.OperationTargetScope))
        {
            return false;
        }

        var rootResolution = ResolveInspectableProductRoot(assignment.LaunchVariables);
        if (rootResolution.Kind != ProcessProductRootResolutionKind.Resolved)
        {
            return false;
        }

        var productRoot = rootResolution.ProductRoot;

        var requiredIssues = new List<ProcessCompletionIssue>();
        if (productCompletionPathGate.ValidateRequiredProductPaths(
                assignment,
                productRoot) is { } requiredPathIssue)
        {
            requiredIssues.Add(requiredPathIssue);
        }

        if (productCompletionPathGate.ValidateRequiredProductFileContentChecks(
                assignment,
                output,
                productRoot) is { } requiredFileContentIssue)
        {
            requiredIssues.Add(requiredFileContentIssue);
        }

        var retryableIssues = requiredIssues
            .Where(requiredIssue => requiredIssue.RetrySafety == ProcessDiagnosticRetrySafety.SafeToRetry)
            .ToArray();
        if (retryableIssues.Length == 0)
        {
            return false;
        }

        var text = string.Join(
            " ",
            EnumerateOutcomeText(output).Where(value => !string.IsNullOrWhiteSpace(value)));
        if (LooksLikeRightsOrToolBoundary(text) &&
            HasConcreteToolBoundaryReceipt(toolReceipts))
        {
            return false;
        }

        var primaryRef = BuildManagedStepArtifactPath(assignment);
        var requiredSummary = string.Join(" ", retryableIssues.Select(requiredIssue => requiredIssue.Summary));
        var originalReason = string.IsNullOrWhiteSpace(output.Reason)
            ? "The step returned Blocked while declared product state was still incomplete."
            : output.Reason.Trim();
        var receiptSummary = string.Join("|", toolReceipts.Select(receipt => $"{receipt.ToolName}:{receipt.RequestSummary}:{receipt.ExitSummary}"));
        issue = new ProcessCompletionIssue(
            "process.adapter.product_required_state_blocked_retry",
            $"Step '{assignment.StepKey}' returned Blocked while required product output/readback gate(s) are still unsatisfied. Retry the same step, satisfy the declared product path and file-content checks, update primary managed artifact '{primaryRef}', and return Blocked only for a concrete tool, permission, policy, or environment blocker. Missing state: {requiredSummary} Original reason: {originalReason}",
            $"{assignment.RunId}:{assignment.StepInstanceId}:product-required-state-blocked-retry:{ComputeHash(requiredSummary)}:{ComputeHash(receiptSummary)}:{ComputeHash(originalReason)}",
            assignment.ProducedArtifactSlotIds,
            ProcessDiagnosticRetrySafety.SafeToRetry,
            ProcessDiagnosticIdempotencyClassification.Idempotent);
        return true;
    }

    internal static bool IsRetryableProductMutationEvidenceBlocker(
        ProcessRuntimeStepAssignment assignment,
        ProcessStepOutcomeResult output,
        IReadOnlyList<ToolExecutionReceiptRecord>? toolReceipts,
        ProcessToolReceiptPolicyCatalog toolReceiptPolicies)
    {
        if (output.Status != ProcessStepOutcomeStatus.Blocked ||
            toolReceipts is null ||
            !AllowsProductMutation(NormalizeOperations(assignment.AllowedOperations), assignment.OperationTargetScope))
        {
            return false;
        }

        var productTargetRefs = ResolveProductTargetReceiptRefs(assignment.LaunchVariables);
        if (productTargetRefs.Count == 0 ||
            HasProductMutationReceipt(toolReceipts, productTargetRefs, toolReceiptPolicies))
        {
            return false;
        }

        var text = string.Join(
            " ",
            EnumerateOutcomeText(output).Where(value => !string.IsNullOrWhiteSpace(value)));
        if (LooksLikeRightsOrToolBoundary(text) &&
            HasConcreteToolBoundaryReceipt(toolReceipts))
        {
            return false;
        }

        return HasPrimaryManagedEvidence(assignment, output, toolReceipts);
    }

    internal static bool HasPrimaryManagedEvidence(
        ProcessRuntimeStepAssignment assignment,
        ProcessStepOutcomeResult output,
        IReadOnlyList<ToolExecutionReceiptRecord> toolReceipts)
    {
        var primaryRef = NormalizeManagedArtifactRef(BuildManagedStepArtifactPath(assignment));
        return HasManagedArtifactWriteReceipt(toolReceipts, primaryRef) ||
               output.EvidenceRefs.Any(evidenceRef =>
                   !string.IsNullOrWhiteSpace(evidenceRef) &&
                   ReceiptTargetsManagedRef(evidenceRef, primaryRef));
    }

    internal static ProcessCompletionIssue CreateProductMutationEvidenceRetryIssue(
        ProcessRuntimeStepAssignment assignment,
        ProcessStepOutcomeResult output,
        IReadOnlyList<ToolExecutionReceiptRecord> toolReceipts)
    {
        var productTargetRefs = ResolveProductTargetReceiptRefs(assignment.LaunchVariables);
        var targetSummary = productTargetRefs.Count == 0
            ? "the configured product target"
            : string.Join("; ", productTargetRefs);
        var primaryRef = BuildManagedStepArtifactPath(assignment);
        var originalReason = string.IsNullOrWhiteSpace(output.Reason)
            ? "The step returned Blocked after writing managed evidence but before product mutation evidence was present."
            : output.Reason.Trim();
        var receiptSummary = string.Join("|", toolReceipts.Select(receipt => $"{receipt.ToolName}:{receipt.RequestSummary}:{receipt.ExitSummary}"));
        return new ProcessCompletionIssue(
            "process.adapter.product_mutation_blocked_retry",
            $"Step '{assignment.StepKey}' returned Blocked after writing managed evidence but did not produce a successful product-target mutation receipt for {targetSummary}. Retry the same step: apply the requested product changes under the product target, update primary managed artifact '{primaryRef}', and return Blocked only for a concrete tool, permission, policy, or environment blocker. Original reason: {originalReason}",
            $"{assignment.RunId}:{assignment.StepInstanceId}:product-mutation-blocked-retry:{ComputeHash(receiptSummary)}:{ComputeHash(originalReason)}",
            assignment.ProducedArtifactSlotIds,
            ProcessDiagnosticRetrySafety.SafeToRetry,
            ProcessDiagnosticIdempotencyClassification.Idempotent);
    }


}
