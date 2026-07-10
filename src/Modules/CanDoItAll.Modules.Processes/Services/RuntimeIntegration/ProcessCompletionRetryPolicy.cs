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
using static CanDoItAll.Modules.Processes.ProcessCompletionText;
using static CanDoItAll.Modules.Processes.ProcessExecutionMetadataBuilder;
using static CanDoItAll.Modules.Processes.ProcessExecutionResultFactory;
using static CanDoItAll.Modules.Processes.ProcessManagedArtifactEvidence;
using static CanDoItAll.Modules.Processes.ProcessManagedArtifactService;
using static CanDoItAll.Modules.Processes.ProcessManagedArtifactFormatter;
using static CanDoItAll.Modules.Processes.ProcessManagedArtifactOutcomeParser;
using static CanDoItAll.Modules.Processes.ProcessRuntimeLifecycleReceiptFacts;

namespace CanDoItAll.Modules.Processes;

internal static class ProcessCompletionRetryPolicy
{
    internal static bool IsRetryableNonTerminalPrimaryArtifactBlocker(
        ProcessRuntimeStepAssignment assignment,
        ProcessStepOutcomeResult output,
        IReadOnlyList<ToolExecutionReceiptRecord>? toolReceipts)
    {
        if (output.Status != ProcessStepOutcomeStatus.Blocked ||
            assignment.ProducedArtifactSlotIds.Count == 0)
        {
            return false;
        }

        var text = string.Join(
            " ",
            EnumerateOutcomeText(output).Where(value => !string.IsNullOrWhiteSpace(value)));
        if (!ContainsNonTerminalStatusDeclaration(text) ||
            LooksLikeRightsOrToolBoundary(text))
        {
            return false;
        }

        var primaryRef = NormalizeManagedArtifactRef(BuildManagedStepArtifactPath(assignment));
        var hasPrimaryEvidenceRef = output.EvidenceRefs.Any(evidenceRef =>
            !string.IsNullOrWhiteSpace(evidenceRef) &&
            ReceiptTargetsManagedRef(evidenceRef, primaryRef));
        var hasPrimaryWriteReceipt = toolReceipts is not null &&
                                     HasManagedArtifactWriteReceipt(toolReceipts, primaryRef);
        return hasPrimaryEvidenceRef || hasPrimaryWriteReceipt;
    }

    internal static bool ContainsNonTerminalStatusDeclaration(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        if (NonTerminalStatusDeclarationRegex().IsMatch(text))
        {
            return true;
        }

        foreach (var rawLine in text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var line = rawLine.Trim().TrimStart('#', '-', '*', ' ');
            var separatorIndex = line.IndexOf(':', StringComparison.Ordinal);
            if (separatorIndex <= 0)
            {
                continue;
            }

            var key = line[..separatorIndex].Trim(' ', '*', '`');
            if (!string.Equals(key, "Status", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var statusValue = line[(separatorIndex + 1)..].Trim();
            var commentIndex = statusValue.IndexOf('#', StringComparison.Ordinal);
            if (commentIndex >= 0)
            {
                statusValue = statusValue[..commentIndex].Trim();
            }

            var normalized = new string(
                statusValue
                    .Where(character => char.IsLetterOrDigit(character))
                    .Select(char.ToLowerInvariant)
                    .ToArray());
            if (normalized is "inprogress" or "progress" or "working" or "running" or "started")
            {
                return true;
            }
        }

        return ContainsAny(
            text,
            "non-terminal status",
            "non terminal status",
            "not a terminal status");
    }

    internal static ProcessCompletionIssue CreateNonTerminalPrimaryArtifactRetryIssue(
        ProcessRuntimeStepAssignment assignment,
        ProcessStepOutcomeResult output)
    {
        var primaryRef = BuildManagedStepArtifactPath(assignment);
        var originalReason = string.IsNullOrWhiteSpace(output.Reason)
            ? "The primary managed artifact declared a non-terminal status."
            : output.Reason.Trim();
        var summary = $"Step '{assignment.StepKey}' wrote primary managed artifact '{primaryRef}' with a non-terminal Status line. Retry the same step: preserve or overwrite that artifact only with a final Status line of Completed, Blocked, Failed, WaitingApproval, or Refused after the step's required work and evidence readbacks are complete. Original reason: {originalReason}";

        return new ProcessCompletionIssue(
            "process.adapter.non_terminal_primary_artifact_retry",
            summary,
            $"{assignment.RunId}:{assignment.StepInstanceId}:non-terminal-primary-artifact:{primaryRef}:{ComputeHash(originalReason)}",
            assignment.ProducedArtifactSlotIds,
            ProcessDiagnosticRetrySafety.SafeToRetry,
            ProcessDiagnosticIdempotencyClassification.Idempotent);
    }

    internal static ProcessStepOutcomeResult CopyAsCompletedBranchOutcome(ProcessStepOutcomeResult output)
    {
        var originalReason = string.IsNullOrWhiteSpace(output.Reason)
            ? "The agent returned Blocked while selecting a branch outcome."
            : output.Reason.Trim();
        return new ProcessStepOutcomeResult
        {
            Status = ProcessStepOutcomeStatus.Completed,
            Reason = $"Runtime routed evidence-backed branch outcome '{output.BranchOutcomeKey.Trim()}' after the agent returned Blocked with a selected branch. Original reason: {originalReason}",
            BranchOutcomeKey = output.BranchOutcomeKey,
            BranchOutcomeTitle = output.BranchOutcomeTitle,
            EvidenceRefs = output.EvidenceRefs,
            NextActions = output.NextActions,
            HumanReadableSummaryMarkdown = output.HumanReadableSummaryMarkdown
        };
    }

    internal static ProcessStepOutcomeResult CopyWithBranchOutcomeKey(
        ProcessStepOutcomeResult output,
        string branchOutcomeKey)
    {
        return new ProcessStepOutcomeResult
        {
            Status = output.Status,
            Reason = output.Reason,
            BranchOutcomeKey = branchOutcomeKey,
            BranchOutcomeTitle = output.BranchOutcomeTitle,
            EvidenceRefs = output.EvidenceRefs,
            NextActions = output.NextActions,
            HumanReadableSummaryMarkdown = output.HumanReadableSummaryMarkdown
        };
    }

    internal static bool IsRetryableManagedArtifactSelfEvidenceBlocker(
        ProcessRuntimeStepAssignment assignment,
        ProcessStepOutcomeResult output)
    {
        if (output.Status != ProcessStepOutcomeStatus.Blocked ||
            assignment.ProducedArtifactSlotIds.Count == 0)
        {
            return false;
        }

        var operations = NormalizeOperations(assignment.AllowedOperations);
        if (operations.Contains(ProcessOperationContractNames.ExecuteExternalAction, StringComparer.OrdinalIgnoreCase) ||
            operations.Contains(ProcessOperationContractNames.LaunchRuntime, StringComparer.OrdinalIgnoreCase) ||
            operations.Contains(ProcessOperationContractNames.CaptureRuntimeProof, StringComparer.OrdinalIgnoreCase) ||
            operations.Contains(ProcessOperationContractNames.RunValidation, StringComparer.OrdinalIgnoreCase) ||
            operations.Contains(ProcessOperationContractNames.MutateProductTarget, StringComparer.OrdinalIgnoreCase) ||
            AllowsProductMutation(operations, assignment.OperationTargetScope))
        {
            return false;
        }

        var text = string.Join(
            " ",
            EnumerateOutcomeText(output).Where(value => !string.IsNullOrWhiteSpace(value)));
        if (LooksLikeRightsOrToolBoundary(text))
        {
            return false;
        }

        return ContainsAny(
            text,
            "No prior assistant text",
            "tool output",
            "process artifact evidence",
            "insufficient evidence",
            "current-run evidence",
            "concrete current-run evidence",
            "managed artifact evidence",
            "evidence reference");
    }

    internal static bool IsRetryableManagedArtifactMissingPrimaryOutputBlocker(
        ProcessRuntimeStepAssignment assignment,
        ProcessStepOutcomeResult output)
    {
        if (output.Status != ProcessStepOutcomeStatus.Blocked ||
            assignment.ProducedArtifactSlotIds.Count == 0)
        {
            return false;
        }

        var operations = NormalizeOperations(assignment.AllowedOperations);
        if (!operations.Contains(ProcessOperationContractNames.WriteManagedProcessArtifacts, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        var text = string.Join(
            " ",
            EnumerateOutcomeText(output).Where(value => !string.IsNullOrWhiteSpace(value)));
        if (LooksLikeRightsOrToolBoundary(text))
        {
            return false;
        }

        return ContainsAny(
                   text,
                   "missing-primary-output",
                   "primary managed output not written",
                   "primary output ref was not created",
                   "primary managed artifact",
                   "required primary managed output",
                   "create the required primary managed")
               && ContainsAny(
                   text,
                   "workspace_write_file",
                   "workspace_append_file",
                   "primary write ref",
                   "managed artifact");
    }

    internal static ProcessCompletionIssue CreateManagedArtifactSelfEvidenceRetryIssue(
        ProcessRuntimeStepAssignment assignment,
        ProcessStepOutcomeResult output)
    {
        var primaryRef = BuildManagedStepArtifactPath(assignment);
        var requiredSlotSummary = assignment.RequiredArtifactSlotIds.Count == 0
            ? "none"
            : string.Join(", ", assignment.RequiredArtifactSlotIds.Select(slotId => slotId.Value.ToString("D")));
        var originalReason = string.IsNullOrWhiteSpace(output.Reason)
            ? "The agent reported a generic insufficient-evidence blocker."
            : output.Reason.Trim();
        var summary = $"Step '{assignment.StepKey}' returned a generic insufficient-evidence Blocked result before producing its required managed artifact. Retry the same step: read any required upstream refs listed in the step brief with workspace_read_file when a stat says the ref exists, do not invent sibling files from artifact expectation keys, then write primary managed artifact '{primaryRef}' with workspace_write_file or workspace_append_file and return Completed only with evidenceRefs containing that ref. Required upstream slot ids: {requiredSlotSummary}. Original reason: {originalReason}";

        return new ProcessCompletionIssue(
            "process.adapter.managed_artifact_self_evidence_retry",
            summary,
            $"{assignment.RunId}:{assignment.StepInstanceId}:managed-artifact-self-evidence:{primaryRef}:{requiredSlotSummary}:{ComputeHash(originalReason)}",
            assignment.ProducedArtifactSlotIds,
            ProcessDiagnosticRetrySafety.SafeToRetry,
            ProcessDiagnosticIdempotencyClassification.Idempotent);
    }

    internal static ProcessCompletionIssue CreateManagedArtifactMissingPrimaryOutputRetryIssue(
        ProcessRuntimeStepAssignment assignment,
        ProcessStepOutcomeResult output)
    {
        var primaryRef = BuildManagedStepArtifactPath(assignment);
        var originalReason = string.IsNullOrWhiteSpace(output.Reason)
            ? "The agent reported that the primary managed output was not written."
            : output.Reason.Trim();
        var summary = $"Step '{assignment.StepKey}' reported a missing primary managed artifact instead of creating its own output. Retry the same step: use the already-read upstream evidence, create primary managed artifact '{primaryRef}' with workspace_write_file or workspace_append_file, re-read or cite that ref, then return Completed with any concrete configured branch outcome only after evidenceRefs contains that ref. Original reason: {originalReason}";

        return new ProcessCompletionIssue(
            "process.adapter.managed_artifact_missing_primary_output_retry",
            summary,
            $"{assignment.RunId}:{assignment.StepInstanceId}:managed-artifact-missing-primary:{primaryRef}:{ComputeHash(originalReason)}",
            assignment.ProducedArtifactSlotIds,
            ProcessDiagnosticRetrySafety.SafeToRetry,
            ProcessDiagnosticIdempotencyClassification.Idempotent);
    }
}
