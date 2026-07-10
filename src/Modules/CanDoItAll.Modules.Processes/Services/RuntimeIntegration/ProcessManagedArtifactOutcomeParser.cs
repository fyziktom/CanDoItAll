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
using static CanDoItAll.Modules.Processes.ProcessExecutionResultFactory;
using static CanDoItAll.Modules.Processes.ProcessManagedArtifactEvidence;
using static CanDoItAll.Modules.Processes.ProcessRuntimeLifecycleReceiptFacts;

namespace CanDoItAll.Modules.Processes;

internal static class ProcessManagedArtifactOutcomeParser
{
    internal static bool TryReadManagedArtifactStatus(
        string content,
        out ProcessStepOutcomeStatus status)
    {
        status = default;
        foreach (var rawLine in content.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
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

            return TryMapManagedArtifactStatus(line[(separatorIndex + 1)..], out status);
        }

        return false;
    }

    internal static bool TryMapManagedArtifactStatus(
        string value,
        out ProcessStepOutcomeStatus status)
    {
        status = default;
        var normalized = NormalizeManagedArtifactStatusValue(value);
        status = normalized switch
        {
            "completed" or "complete" or "succeeded" or "success" => ProcessStepOutcomeStatus.Completed,
            "blocked" or "waiting" or "waitingonchild" or "waitingforchild" => ProcessStepOutcomeStatus.Blocked,
            "failed" or "failure" => ProcessStepOutcomeStatus.Failed,
            "waitingapproval" or "pendingapproval" => ProcessStepOutcomeStatus.WaitingApproval,
            "refused" or "rejected" => ProcessStepOutcomeStatus.Refused,
            _ => default
        };
        return normalized is "completed" or "complete" or "succeeded" or "success" or
            "blocked" or "waiting" or "waitingonchild" or "waitingforchild" or
            "failed" or "failure" or
            "waitingapproval" or "pendingapproval" or
            "refused" or "rejected";
    }

    internal static string NormalizeManagedArtifactStatusValue(string value)
    {
        var trimmed = value.Trim().Trim('*', '`', '.', ';');
        var commentIndex = trimmed.IndexOf('#', StringComparison.Ordinal);
        if (commentIndex >= 0)
        {
            trimmed = trimmed[..commentIndex].Trim();
        }

        return new string(
            trimmed
                .Where(character => char.IsLetterOrDigit(character))
                .Select(char.ToLowerInvariant)
                .ToArray());
    }

    internal static bool ManagedArtifactBelongsToStep(
        string content,
        ProcessRuntimeStepAssignment assignment)
    {
        return ContainsManagedArtifactField(content, "Run id", assignment.RunId.Value.ToString("D")) &&
            ContainsManagedArtifactField(content, "Step id", assignment.StepInstanceId.Value.ToString("D")) &&
            ContainsManagedArtifactField(content, "Step key", assignment.StepKey);
    }

    internal static bool ContainsManagedArtifactField(
        string content,
        string key,
        string expectedValue)
    {
        foreach (var rawLine in content.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var line = rawLine.Trim().TrimStart('#', '-', '*', ' ');
            var separatorIndex = line.IndexOf(':', StringComparison.Ordinal);
            if (separatorIndex <= 0)
            {
                continue;
            }

            var fieldKey = line[..separatorIndex].Trim(' ', '*', '`');
            if (!string.Equals(fieldKey, key, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var value = line[(separatorIndex + 1)..].Trim(' ', '*', '`');
            return string.Equals(value, expectedValue, StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    internal static ProcessStepOutcomeResult CopyAsCompletedFromPrimaryManagedArtifact(
        ProcessStepOutcomeResult output,
        string primaryRef)
    {
        var originalReason = string.IsNullOrWhiteSpace(output.Reason)
            ? "The primary managed artifact already declares a completed outcome for this step."
            : output.Reason.Trim();
        return new ProcessStepOutcomeResult
        {
            Status = ProcessStepOutcomeStatus.Completed,
            Reason = $"Runtime staged the completed primary managed artifact after the finalizer returned a nonterminal retry outcome. Original reason: {originalReason}",
            BranchOutcomeKey = output.BranchOutcomeKey,
            BranchOutcomeTitle = output.BranchOutcomeTitle,
            EvidenceRefs = output.EvidenceRefs
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Append(primaryRef)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            NextActions = [],
            HumanReadableSummaryMarkdown = output.HumanReadableSummaryMarkdown
        };
    }

    internal static bool IsPureManagedArtifactSelfEvidenceBlocker(
        ProcessRuntimeStepAssignment assignment,
        ProcessStepOutcomeResult output)
    {
        if (output.Status != ProcessStepOutcomeStatus.Blocked ||
            assignment.ProducedArtifactSlotIds.Count == 0 ||
            assignment.RequiredArtifactSlotIds.Count > 0)
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

        if (LooksLikeMissingOwnPrimaryManagedArtifact(assignment, text))
        {
            return true;
        }

        return ContainsAny(
            text,
            "concrete current-run evidence",
            "current run evidence",
            "current-run evidence",
            "managed artifact evidence",
            "managed artifact ref",
            "own-output write ref",
            "evidence reference");
    }

    internal static bool LooksLikeMissingOwnPrimaryManagedArtifact(
        ProcessRuntimeStepAssignment assignment,
        string text)
    {
        if (!ContainsAny(
                text,
                "does not exist",
                "not found",
                "failed to find",
                "could not find",
                "cannot find",
                "missing file"))
        {
            return false;
        }

        var normalizedText = text.Replace('\\', '/');
        var primaryRef = BuildManagedStepArtifactPath(assignment);
        if (normalizedText.Contains(primaryRef, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var processRunSuffix = $"process-runs/{assignment.RunId.Value:D}/steps/{SanitizeManagedArtifactPathSegment(assignment.StepKey)}.md";
        return normalizedText.Contains(processRunSuffix, StringComparison.OrdinalIgnoreCase);
    }

    internal static bool HasAllManagedArtifactEvidence(
        ProcessRuntimeStepAssignment assignment,
        IReadOnlyList<string> evidenceRefs)
    {
        var normalizedEvidenceRefs = evidenceRefs
            .Where(evidenceRef => !string.IsNullOrWhiteSpace(evidenceRef))
            .Select(NormalizeManagedArtifactRef)
            .Where(evidenceRef => evidenceRef.Length > 0)
            .ToArray();
        return assignment.ProducedArtifactSlotIds.All(slotId =>
            HasManagedArtifactEvidence(assignment, slotId, normalizedEvidenceRefs));
    }

    internal static ProcessStepOutcomeResult CopyWithEvidenceRef(
        ProcessStepOutcomeResult output,
        string evidenceRef)
    {
        var evidenceRefs = output.EvidenceRefs
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Append(evidenceRef)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return new ProcessStepOutcomeResult
        {
            Status = output.Status,
            Reason = output.Reason,
            BranchOutcomeKey = output.BranchOutcomeKey,
            BranchOutcomeTitle = output.BranchOutcomeTitle,
            EvidenceRefs = evidenceRefs,
            NextActions = output.NextActions,
            HumanReadableSummaryMarkdown = output.HumanReadableSummaryMarkdown
        };
    }

    internal static ProcessStepOutcomeResult CopyAsCompletedWithEvidenceRef(
        ProcessStepOutcomeResult output,
        string evidenceRef)
    {
        var originalReason = string.IsNullOrWhiteSpace(output.Reason)
            ? "The agent reported a self-evidence blocker for a pure managed-artifact producer step."
            : output.Reason.Trim();
        return new ProcessStepOutcomeResult
        {
            Status = ProcessStepOutcomeStatus.Completed,
            Reason = $"Runtime materialized the pure managed-artifact producer outcome after the agent reported a self-evidence blocker. Original reason: {originalReason}",
            BranchOutcomeKey = output.BranchOutcomeKey,
            BranchOutcomeTitle = output.BranchOutcomeTitle,
            EvidenceRefs = [evidenceRef],
            NextActions = [],
            HumanReadableSummaryMarkdown = output.HumanReadableSummaryMarkdown
        };
    }
}
