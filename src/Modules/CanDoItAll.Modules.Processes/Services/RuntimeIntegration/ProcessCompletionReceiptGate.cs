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

using static CanDoItAll.Modules.Processes.ProcessCompletionText;
using static CanDoItAll.Modules.Processes.ProcessExecutionMetadataBuilder;
using static CanDoItAll.Modules.Processes.ProcessManagedArtifactEvidence;
using static CanDoItAll.Modules.Processes.ProcessRequiredReceiptMatcher;
using static CanDoItAll.Modules.Processes.ProcessRequiredReceiptRetryPolicy;

namespace CanDoItAll.Modules.Processes;

internal static class ProcessCompletionReceiptGate
{
    internal static ProcessCompletionIssue? ValidateProductMutationWriteReceipt(
        ProcessRuntimeStepAssignment assignment,
        ProcessStepOutcomeResult output,
        IReadOnlyList<ToolExecutionReceiptRecord>? toolReceipts,
        ProcessToolReceiptPolicyCatalog toolReceiptPolicies)
    {
        if (output.Status != ProcessStepOutcomeStatus.Completed ||
            toolReceipts is null ||
            !AllowsProductMutation(NormalizeOperations(assignment.AllowedOperations), assignment.OperationTargetScope))
        {
            return null;
        }

        var productTargetRefs = ResolveProductTargetReceiptRefs(assignment.LaunchVariables);
        if (productTargetRefs.Count == 0 ||
            HasProductMutationReceipt(toolReceipts, productTargetRefs, toolReceiptPolicies) ||
            CanAcceptBranchGatedValidationOnlyCompletion(assignment, toolReceipts, productTargetRefs, toolReceiptPolicies))
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

    internal static ProcessCompletionIssue? ValidateRequiredProductToolReceipts(
        ProcessRuntimeStepAssignment assignment,
        ProcessStepOutcomeResult output,
        IReadOnlyList<ToolExecutionReceiptRecord>? toolReceipts,
        IReadOnlyList<ProductCompletionRequiredToolReceiptRule> requiredToolReceiptRules,
        ProcessToolReceiptPolicyCatalog toolReceiptPolicies)
    {
        var requiredToolReceipts = requiredToolReceiptRules
            .Select(rule => rule.ToolReceipt)
            .Where(receipt => !string.IsNullOrWhiteSpace(receipt))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (!ShouldEnforceRequiredProductToolReceipts(assignment, requiredToolReceipts))
        {
            return null;
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
        if (missingToolReceipts.Length == 0)
        {
            return null;
        }

        var missingSummary = string.Join("; ", missingToolReceipts);
        var failedReceiptGuidance = BuildFailedRequiredToolReceiptGuidance(
            assignment,
            observedToolReceipts,
            missingToolReceipts,
            allowFailedExecutionReceipt,
            toolReceiptPolicies);
        var missingReceiptGuidance = BuildMissingRequiredToolReceiptGuidance(
            assignment,
            missingToolReceipts,
            toolReceiptPolicies);
        return new ProcessCompletionIssue(
            "process.adapter.product_required_tool_receipt_missing",
            $"Step '{assignment.StepKey}' claimed completion for branch '{output.BranchOutcomeKey}' but required current-run product tool receipt(s) are missing: {missingSummary}.{failedReceiptGuidance}{missingReceiptGuidance}",
            $"{assignment.RunId}:{assignment.StepInstanceId}:product-required-tool-receipt-missing:{missingSummary}:{string.Join("|", observedToolReceipts.Select(receipt => $"{receipt.ToolName}:{receipt.RequestSummary}:{receipt.ExitSummary}"))}",
            assignment.ProducedArtifactSlotIds,
            ProcessDiagnosticRetrySafety.SafeToRetry,
            ProcessDiagnosticIdempotencyClassification.Idempotent);
    }

    internal static ProcessCompletionIssue? ValidateRequiredProcessToolReceipts(
        ProcessRuntimeStepAssignment assignment,
        ProcessStepOutcomeResult output,
        IReadOnlyList<ToolExecutionReceiptRecord>? toolReceipts,
        Guid? currentExecutionRunId,
        IReadOnlySet<string> productCoveredToolNames)
    {
        var activeLaunchContextToolNames = ResolveActiveLaunchContextToolNameSet(assignment);
        var gate = ProcessRequiredToolReceiptGate.Evaluate(
            assignment,
            toolReceipts,
            activeLaunchContextToolNames,
            currentExecutionRunId,
            output.BranchOutcomeKey,
            productCoveredToolNames);
        if (gate.IsSatisfied)
        {
            return null;
        }

        var observedToolReceipts = toolReceipts ?? [];
        var missingSummary = ProcessRequiredToolReceiptGate.FormatMissingSummary(gate.MissingReceipts);
        return new ProcessCompletionIssue(
            "process.adapter.required_tool_receipt_missing",
            $"Step '{assignment.StepKey}' claimed completion for branch '{output.BranchOutcomeKey}' but required current-run process tool receipt(s) are missing: {missingSummary}. Retry the same step, invoke the missing required tool(s), cite the receipt refs in the managed artifact, and complete only after the typed process capability scope receipt contract is satisfied.",
            $"{assignment.RunId}:{assignment.StepInstanceId}:required-tool-receipt-missing:{missingSummary}:{string.Join("|", observedToolReceipts.Select(receipt => $"{receipt.ToolName}:{receipt.RuntimeToolProviderKey}:{receipt.RequestSummary}:{receipt.ExitSummary}"))}",
            assignment.ProducedArtifactSlotIds,
            ProcessDiagnosticRetrySafety.SafeToRetry,
            ProcessDiagnosticIdempotencyClassification.Idempotent);
    }
}
