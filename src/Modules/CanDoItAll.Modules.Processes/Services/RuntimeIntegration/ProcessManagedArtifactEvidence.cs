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
using static CanDoItAll.Modules.Processes.ProcessCompletionText;
using static CanDoItAll.Modules.Processes.ProcessExecutionMetadataBuilder;

namespace CanDoItAll.Modules.Processes;

internal static class ProcessManagedArtifactEvidence
{
    internal static ProcessCompletionIssue? ValidateManagedArtifactCompletion(
        ProcessRuntimeStepAssignment assignment,
        ProcessStepOutcomeResult output)
    {
        if (output.Status != ProcessStepOutcomeStatus.Completed ||
            assignment.ProducedArtifactSlotIds.Count == 0)
        {
            return null;
        }

        var evidenceRefs = output.EvidenceRefs
            .Where(evidenceRef => !string.IsNullOrWhiteSpace(evidenceRef))
            .Select(NormalizeManagedArtifactRef)
            .Where(evidenceRef => evidenceRef.Length > 0)
            .ToArray();
        var missingSlotIds = assignment.ProducedArtifactSlotIds
            .Where(slotId => !HasManagedArtifactEvidence(assignment, slotId, evidenceRefs))
            .Distinct()
            .ToArray();
        if (missingSlotIds.Length == 0)
        {
            return null;
        }

        var expectedRefs = missingSlotIds
            .SelectMany(slotId => EnumerateManagedArtifactEvidenceRefs(assignment, slotId))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return new ProcessCompletionIssue(
            "process.adapter.produced_artifact_evidence_missing",
            $"Step '{assignment.StepKey}' claimed completion but did not return a managed artifact evidence ref for produced slot(s): {string.Join(", ", missingSlotIds)}. Expected one of: {string.Join("; ", expectedRefs)}",
            $"{assignment.RunId}:{assignment.StepInstanceId}:produced-artifact-evidence-missing:{string.Join(",", missingSlotIds)}:{string.Join("|", output.EvidenceRefs)}",
            missingSlotIds,
            ProcessDiagnosticRetrySafety.SafeToRetry,
            ProcessDiagnosticIdempotencyClassification.Idempotent);
    }

    internal static ProcessCompletionIssue? ValidateManagedArtifactWriteReceipt(
        ProcessRuntimeStepAssignment assignment,
        IReadOnlyList<ToolExecutionReceiptRecord>? toolReceipts)
    {
        if (assignment.ProducedArtifactSlotIds.Count == 0 ||
            toolReceipts is null)
        {
            return null;
        }

        var primaryRef = NormalizeManagedArtifactRef(BuildManagedStepArtifactPath(assignment));
        if (HasManagedArtifactWriteReceipt(toolReceipts, primaryRef))
        {
            return null;
        }

        return new ProcessCompletionIssue(
            ProcessCompletionDiagnosticCodes.ManagedArtifactWriteReceiptMissing,
            $"Step '{assignment.StepKey}' claimed completion but did not produce a successful workspace_write_file or workspace_append_file receipt for primary managed artifact '{BuildManagedStepArtifactPath(assignment)}'.",
            $"{assignment.RunId}:{assignment.StepInstanceId}:produced-artifact-write-receipt-missing:{string.Join("|", toolReceipts.Select(receipt => $"{receipt.ToolName}:{receipt.RequestSummary}:{receipt.ExitSummary}"))}",
            assignment.ProducedArtifactSlotIds,
            ProcessDiagnosticRetrySafety.SafeToRetry,
            ProcessDiagnosticIdempotencyClassification.Idempotent);
    }

    internal static bool HasManagedArtifactWriteReceipt(
        ProcessRuntimeStepAssignment assignment,
        IReadOnlyList<ToolExecutionReceiptRecord> toolReceipts)
        => HasManagedArtifactWriteReceipt(
            toolReceipts,
            NormalizeManagedArtifactRef(BuildManagedStepArtifactPath(assignment)));

    internal static bool HasManagedArtifactWriteReceipt(
        IReadOnlyList<ToolExecutionReceiptRecord> toolReceipts,
        string primaryRef)
        => toolReceipts.Any(receipt =>
            IsManagedArtifactWriteTool(receipt.ToolName) &&
            IsSuccessfulReceipt(receipt.ExitSummary) &&
            ReceiptTargetsManagedRef(receipt.RequestSummary, primaryRef));

    internal static bool IsManagedArtifactWriteTool(string toolName)
        => string.Equals(toolName, "workspace_write_file", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(toolName, "workspace_append_file", StringComparison.OrdinalIgnoreCase);

    internal static bool HasProductMutationReceipt(
        IReadOnlyList<ToolExecutionReceiptRecord> toolReceipts,
        IReadOnlyList<string> productTargetRefs,
        ProcessToolReceiptPolicyCatalog toolReceiptPolicies)
        => toolReceipts.Any(receipt =>
            toolReceiptPolicies.IsProductMutationReceipt(receipt) &&
            IsSuccessfulReceipt(receipt.ExitSummary) &&
            (ReceiptTargetsAnyProductRef(receipt.RequestSummary, productTargetRefs) ||
             ReceiptTargetsAnyProductRef(receipt.WorkingDirectory, productTargetRefs)));

    internal static bool HasProductValidationReceipt(
        IReadOnlyList<ToolExecutionReceiptRecord> toolReceipts,
        IReadOnlyList<string> productTargetRefs,
        ProcessToolReceiptPolicyCatalog toolReceiptPolicies)
        => toolReceipts.Any(receipt =>
            toolReceiptPolicies.IsProductValidationTool(receipt.ToolName) &&
            IsSuccessfulReceipt(receipt.ExitSummary) &&
            (ReceiptTargetsAnyProductRef(receipt.RequestSummary, productTargetRefs) ||
             ReceiptTargetsAnyProductRef(receipt.WorkingDirectory, productTargetRefs)));

    internal static bool IsSuccessfulReceipt(string exitSummary)
        => exitSummary.StartsWith("Succeeded", StringComparison.OrdinalIgnoreCase);

    internal static bool IsRequiredToolReceiptUsable(
        ToolExecutionReceiptRecord receipt,
        bool allowFailedExecutionReceipt)
    {
        if (IsSuccessfulReceipt(receipt.ExitSummary))
        {
            return true;
        }

        return allowFailedExecutionReceipt && !IsConcreteToolBoundaryReceipt(receipt);
    }

    internal static bool HasConcreteToolBoundaryReceipt(IReadOnlyList<ToolExecutionReceiptRecord> toolReceipts)
        => toolReceipts.Any(IsConcreteToolBoundaryReceipt);

    internal static bool IsConcreteToolBoundaryReceipt(ToolExecutionReceiptRecord receipt)
        => ContainsAny(
            $"{receipt.RequestSummary} {receipt.ExitSummary}",
            "PolicyDenied",
            "blocked by policy",
            "not authorized to use tool",
            "access denied",
            "workspace boundary",
            "outside the current run boundary",
            "denied tool",
            "tool is not part of the composed capability set");

    internal static bool ReceiptTargetsAnyProductRef(
        string requestSummary,
        IReadOnlyList<string> productTargetRefs)
    {
        var normalizedRequest = NormalizeReceiptPathText(requestSummary);
        return productTargetRefs.Any(productTargetRef =>
            normalizedRequest.Contains(NormalizeReceiptPathText(productTargetRef), StringComparison.OrdinalIgnoreCase));
    }

    internal static bool ReceiptTargetsManagedRef(string requestSummary, string expectedRef)
    {
        var normalizedRequest = NormalizeManagedArtifactRef(requestSummary);
        var expectedTail = expectedRef.StartsWith("artifacts/", StringComparison.OrdinalIgnoreCase)
            ? expectedRef["artifacts".Length..]
            : expectedRef;
        return string.Equals(normalizedRequest, expectedRef, StringComparison.OrdinalIgnoreCase) ||
               normalizedRequest.Contains(expectedRef, StringComparison.OrdinalIgnoreCase) ||
               normalizedRequest.Contains(expectedTail, StringComparison.OrdinalIgnoreCase);
    }

    internal static IReadOnlyList<string> ResolveProductTargetReceiptRefs(
        IReadOnlyDictionary<string, string> launchVariables)
    {
        return launchVariables
            .Where(item => TrustedExternalTargetVariableNames.Contains(item.Key))
            .SelectMany(item => EnumerateProductTargetReceiptRefs(item.Value))
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    internal static IEnumerable<string> EnumerateProductTargetReceiptRefs(string value)
    {
        var normalizedAlias = AgentWorkspaceToolAccessMetadata.NormalizeExternalTargetAlias(value);
        if (!string.IsNullOrWhiteSpace(normalizedAlias) &&
            normalizedAlias.StartsWith("external-target/", StringComparison.OrdinalIgnoreCase))
        {
            yield return normalizedAlias;
        }

        if (!string.IsNullOrWhiteSpace(value))
        {
            yield return value;
        }
    }

    internal static string NormalizeReceiptPathText(string value)
        => value
            .Trim()
            .Replace('\\', '/')
            .Replace("\"", string.Empty, StringComparison.Ordinal)
            .Replace("'", string.Empty, StringComparison.Ordinal);

    internal static bool HasManagedArtifactEvidence(
        ProcessRuntimeStepAssignment assignment,
        ArtifactSlotId slotId,
        IReadOnlyList<string> evidenceRefs)
    {
        if (evidenceRefs.Count == 0)
        {
            return false;
        }

        var stepPath = NormalizeManagedArtifactRef(BuildManagedStepArtifactPath(assignment));
        var slotRoot = NormalizeManagedArtifactRef(BuildManagedSlotArtifactRoot(assignment, slotId));
        var stepRoot = NormalizeManagedArtifactRef(BuildManagedStepArtifactRoot(assignment));
        var stepRootPrefix = stepRoot + "/";
        return evidenceRefs.Any(evidenceRef =>
            string.Equals(evidenceRef, stepPath, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(evidenceRef, slotRoot, StringComparison.OrdinalIgnoreCase) ||
            evidenceRef.StartsWith(stepRootPrefix, StringComparison.OrdinalIgnoreCase));
    }

    internal static IEnumerable<string> EnumerateManagedArtifactEvidenceRefs(
        ProcessRuntimeStepAssignment assignment,
        ArtifactSlotId slotId)
    {
        yield return BuildManagedStepArtifactPath(assignment);
        yield return BuildManagedSlotArtifactRoot(assignment, slotId);
        yield return BuildManagedStepArtifactRoot(assignment) + "/";
    }

    internal static string BuildManagedStepArtifactPath(ProcessRuntimeStepAssignment assignment)
        => $"{BuildManagedArtifactRoot(assignment)}/steps/{SanitizeManagedArtifactPathSegment(assignment.StepKey)}.md";

    internal static string BuildManagedSlotArtifactRoot(
        ProcessRuntimeStepAssignment assignment,
        ArtifactSlotId slotId)
        => $"{BuildManagedArtifactRoot(assignment)}/{slotId}";

    internal static string BuildManagedStepArtifactRoot(ProcessRuntimeStepAssignment assignment)
        => $"{BuildManagedArtifactRoot(assignment)}/{SanitizeManagedArtifactPathSegment(assignment.StepKey)}";

    internal static string BuildManagedArtifactRoot(ProcessRuntimeStepAssignment assignment)
        => $"artifacts/process-runs/{assignment.RunId.Value:D}";

    internal static string NormalizeManagedArtifactRef(string value)
    {
        var normalized = value.Trim().Replace('\\', '/');
        while (normalized.StartsWith("./", StringComparison.Ordinal))
        {
            normalized = normalized[2..];
        }

        normalized = normalized.TrimEnd('/');
        if (normalized.StartsWith("artifacts/scopes/", StringComparison.OrdinalIgnoreCase))
        {
            var processRunsIndex = normalized.IndexOf("/process-runs/", StringComparison.OrdinalIgnoreCase);
            if (processRunsIndex >= 0)
            {
                return "artifacts" + normalized[processRunsIndex..];
            }
        }

        return normalized;
    }

    internal static string SanitizeManagedArtifactPathSegment(string value)
    {
        var sanitized = ManagedArtifactPathSegmentInvalidCharactersRegex()
            .Replace(value.Trim(), "-")
            .Trim('-', '.', '_');
        return string.IsNullOrWhiteSpace(sanitized)
            ? "step"
            : sanitized;
    }

}
