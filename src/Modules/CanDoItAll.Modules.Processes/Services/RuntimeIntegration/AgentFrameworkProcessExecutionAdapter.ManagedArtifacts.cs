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
    private static async Task<IReadOnlyList<ToolExecutionReceiptRecord>> LoadStepCompletionToolReceiptsAsync(
        IAgentExecutionHistoryReader workspaceService,
        ProcessRuntimeStepAssignment assignment,
        Guid currentExecutionRunId,
        IReadOnlyList<ToolExecutionReceiptRecord> currentToolReceipts,
        CancellationToken cancellationToken)
    {
        if (currentToolReceipts.Count == 0 &&
            !AllowsProductMutation(NormalizeOperations(assignment.AllowedOperations), assignment.OperationTargetScope))
        {
            return currentToolReceipts;
        }

        var stepRuns = await workspaceService.ListExecutionRunsAsync(
                new ExecutionRunQuery(
                    Take: 20,
                    ProcessRunId: assignment.RunId.Value.ToString("D"),
                    ProcessStepId: assignment.StepInstanceId.Value.ToString("D")),
                cancellationToken)
            .ConfigureAwait(false);

        if (stepRuns.Count <= 1)
        {
            return currentToolReceipts;
        }

        var receiptById = new Dictionary<Guid, ToolExecutionReceiptRecord>();
        foreach (var receipt in currentToolReceipts)
        {
            receiptById[receipt.Id] = receipt;
        }

        foreach (var stepRun in stepRuns)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (stepRun.Id == currentExecutionRunId)
            {
                continue;
            }

            var detail = await workspaceService
                .GetExecutionRunDetailAsync(stepRun.Id, cancellationToken)
                .ConfigureAwait(false);
            foreach (var receipt in detail.ToolReceipts)
            {
                receiptById.TryAdd(receipt.Id, receipt);
            }
        }

        return receiptById.Values
            .OrderBy(receipt => receipt.StartedAtUtc)
            .ThenBy(receipt => receipt.Id)
            .ToArray();
    }

    private ManagedOutcomeArtifactMaterialization MaterializeManagedOutcomeArtifactIfNeeded(
        ProcessRuntimeStepAssignment assignment,
        ProcessStepOutcomeResult output,
        Guid executionRunId,
        IReadOnlyList<ToolExecutionReceiptRecord> toolReceipts)
    {
        var primaryRef = BuildManagedStepArtifactPath(assignment);
        var acceptedCompletedPrimaryArtifact = false;
        if (output.Status != ProcessStepOutcomeStatus.Completed &&
            TryReadCompletedPrimaryManagedArtifactOutcome(assignment, output, primaryRef, out var completedOutput))
        {
            output = completedOutput;
            acceptedCompletedPrimaryArtifact = true;
        }

        var isSelfEvidenceBlocker = IsPureManagedArtifactSelfEvidenceBlocker(assignment, output);
        if (output.Status != ProcessStepOutcomeStatus.Completed &&
            !isSelfEvidenceBlocker)
        {
            return ManagedOutcomeArtifactMaterialization.Unchanged(output, toolReceipts);
        }

        if (assignment.ProducedArtifactSlotIds.Count == 0)
        {
            return ManagedOutcomeArtifactMaterialization.Unchanged(output, toolReceipts);
        }

        var hasManagedEvidence = HasAllManagedArtifactEvidence(assignment, output.EvidenceRefs);
        var hasWriteReceipt = HasManagedArtifactWriteReceipt(assignment, toolReceipts);
        IReadOnlyList<ToolExecutionReceiptRecord> effectiveReceipts = toolReceipts;
        if (!hasWriteReceipt && acceptedCompletedPrimaryArtifact)
        {
            var appendResult = workspaceFiles.AppendTextFile(
                primaryRef,
                BuildManagedOutcomeArtifactAppendixContent(assignment, output, primaryRef));
            if (!appendResult.Succeeded)
            {
                return ManagedOutcomeArtifactMaterialization.Failed(
                    output,
                    toolReceipts,
                    new ProcessCompletionIssue(
                        "process.adapter.managed_artifact_outcome_append_failed",
                        $"Step '{assignment.StepKey}' recovered a completed primary managed artifact, but the runtime could not append the validated outcome to '{primaryRef}': {appendResult.Message}",
                        $"{assignment.RunId}:{assignment.StepInstanceId}:managed-artifact-outcome-append-failed:{primaryRef}:{appendResult.Message}",
                        assignment.ProducedArtifactSlotIds,
                        ProcessDiagnosticRetrySafety.SafeToRetry,
                        ProcessDiagnosticIdempotencyClassification.Idempotent));
            }

            effectiveReceipts = toolReceipts
                .Append(CreateManagedOutcomeArtifactReceipt(
                    executionRunId,
                    primaryRef,
                    appendResult.Message,
                    "workspace_append_file"))
                .ToArray();
        }
        else if (!hasWriteReceipt)
        {
            var writeResult = workspaceFiles.WriteTextFile(
                primaryRef,
                BuildManagedOutcomeArtifactContent(assignment, output, primaryRef),
                overwrite: true);
            if (!writeResult.Succeeded)
            {
                return ManagedOutcomeArtifactMaterialization.Failed(
                    output,
                    toolReceipts,
                    new ProcessCompletionIssue(
                        "process.adapter.managed_artifact_materialization_failed",
                        $"Step '{assignment.StepKey}' produced a valid structured outcome, but the runtime could not persist the primary managed artifact '{primaryRef}': {writeResult.Message}",
                        $"{assignment.RunId}:{assignment.StepInstanceId}:managed-artifact-materialization-failed:{primaryRef}:{writeResult.Message}",
                        assignment.ProducedArtifactSlotIds,
                        ProcessDiagnosticRetrySafety.SafeToRetry,
                        ProcessDiagnosticIdempotencyClassification.Idempotent));
            }

            effectiveReceipts = toolReceipts
                .Append(CreateManagedOutcomeArtifactReceipt(executionRunId, primaryRef, writeResult.Message))
                .ToArray();
        }
        else
        {
            var appendResult = workspaceFiles.AppendTextFile(
                primaryRef,
                BuildManagedOutcomeArtifactAppendixContent(assignment, output, primaryRef));
            if (!appendResult.Succeeded)
            {
                return ManagedOutcomeArtifactMaterialization.Failed(
                    output,
                    toolReceipts,
                    new ProcessCompletionIssue(
                        "process.adapter.managed_artifact_outcome_append_failed",
                        $"Step '{assignment.StepKey}' produced a valid structured outcome, but the runtime could not append the validated outcome to primary managed artifact '{primaryRef}': {appendResult.Message}",
                        $"{assignment.RunId}:{assignment.StepInstanceId}:managed-artifact-outcome-append-failed:{primaryRef}:{appendResult.Message}",
                        assignment.ProducedArtifactSlotIds,
                        ProcessDiagnosticRetrySafety.SafeToRetry,
                        ProcessDiagnosticIdempotencyClassification.Idempotent));
            }

            effectiveReceipts = toolReceipts
                .Append(CreateManagedOutcomeArtifactReceipt(
                    executionRunId,
                    primaryRef,
                    appendResult.Message,
                    "workspace_append_file"))
                .ToArray();
        }

        var effectiveOutput = isSelfEvidenceBlocker
            ? CopyAsCompletedWithEvidenceRef(output, primaryRef)
            : hasManagedEvidence
                ? output
                : CopyWithEvidenceRef(output, primaryRef);
        return ManagedOutcomeArtifactMaterialization.Succeeded(effectiveOutput, effectiveReceipts);
    }

    private bool TryReadCompletedPrimaryManagedArtifactOutcome(
        ProcessRuntimeStepAssignment assignment,
        ProcessStepOutcomeResult output,
        string primaryRef,
        out ProcessStepOutcomeResult completedOutput)
    {
        completedOutput = default!;

        var readResult = workspaceFiles.ReadTextFile(primaryRef, maxCharacters: 200000);
        if (!readResult.Succeeded ||
            !TryReadManagedArtifactStatus(readResult.Content, out var artifactStatus) ||
            artifactStatus != ProcessStepOutcomeStatus.Completed ||
            !ManagedArtifactBelongsToStep(readResult.Content, assignment))
        {
            return false;
        }

        completedOutput = CopyAsCompletedFromPrimaryManagedArtifact(output, primaryRef);
        return true;
    }

    private static bool TryReadManagedArtifactStatus(
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

    private static bool TryMapManagedArtifactStatus(
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

    private static string NormalizeManagedArtifactStatusValue(string value)
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

    private static bool ManagedArtifactBelongsToStep(
        string content,
        ProcessRuntimeStepAssignment assignment)
    {
        return ContainsManagedArtifactField(content, "Run id", assignment.RunId.Value.ToString("D")) &&
            ContainsManagedArtifactField(content, "Step id", assignment.StepInstanceId.Value.ToString("D")) &&
            ContainsManagedArtifactField(content, "Step key", assignment.StepKey);
    }

    private static bool ContainsManagedArtifactField(
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

    private static ProcessStepOutcomeResult CopyAsCompletedFromPrimaryManagedArtifact(
        ProcessStepOutcomeResult output,
        string primaryRef)
    {
        var originalReason = string.IsNullOrWhiteSpace(output.Reason)
            ? "The primary managed artifact already declares a completed outcome for this step."
            : output.Reason.Trim();
        return new ProcessStepOutcomeResult
        {
            Status = ProcessStepOutcomeStatus.Completed,
            Reason = $"Runtime accepted the completed primary managed artifact after the finalizer returned a nonterminal retry outcome. Original reason: {originalReason}",
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

    private static bool IsPureManagedArtifactSelfEvidenceBlocker(
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

    private static bool LooksLikeMissingOwnPrimaryManagedArtifact(
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

    private static bool HasAllManagedArtifactEvidence(
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

    private static ProcessStepOutcomeResult CopyWithEvidenceRef(
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

    private static ProcessStepOutcomeResult CopyAsCompletedWithEvidenceRef(
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

    private static string BuildManagedOutcomeArtifactContent(
        ProcessRuntimeStepAssignment assignment,
        ProcessStepOutcomeResult output,
        string primaryRef)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"# {assignment.StepKey} Process Step Outcome");
        builder.AppendLine();
        builder.AppendLine("Runtime persisted this managed artifact from the validated structured process step outcome.");
        builder.AppendLine();
        builder.AppendLine($"- Run id: {assignment.RunId.Value:D}");
        builder.AppendLine($"- Step id: {assignment.StepInstanceId.Value:D}");
        builder.AppendLine($"- Step key: {assignment.StepKey}");
        builder.AppendLine($"- Executor: {assignment.ExecutorDisplayName}");
        builder.AppendLine($"- Status: {output.Status}");
        builder.AppendLine($"- Primary managed ref: {primaryRef}");
        builder.AppendLine($"- Persisted at UTC: {DateTimeOffset.UtcNow:u}");
        builder.AppendLine();
        builder.AppendLine("## Reason");
        builder.AppendLine();
        builder.AppendLine(output.Reason.Trim());
        builder.AppendLine();
        if (!string.IsNullOrWhiteSpace(output.BranchOutcomeKey) ||
            !string.IsNullOrWhiteSpace(output.BranchOutcomeTitle))
        {
            builder.AppendLine("## Branch Outcome");
            builder.AppendLine();
            if (!string.IsNullOrWhiteSpace(output.BranchOutcomeKey))
            {
                builder.AppendLine($"- Key: {output.BranchOutcomeKey.Trim()}");
            }

            if (!string.IsNullOrWhiteSpace(output.BranchOutcomeTitle))
            {
                builder.AppendLine($"- Title: {output.BranchOutcomeTitle.Trim()}");
            }

            builder.AppendLine();
        }

        if (!string.IsNullOrWhiteSpace(output.HumanReadableSummaryMarkdown))
        {
            builder.AppendLine("## Summary");
            builder.AppendLine();
            builder.AppendLine(output.HumanReadableSummaryMarkdown.Trim());
            builder.AppendLine();
        }

        AppendList(builder, "Agent Evidence Refs", output.EvidenceRefs);
        AppendList(builder, "Next Actions", output.NextActions);
        return builder.ToString();
    }

    private static string BuildManagedOutcomeArtifactAppendixContent(
        ProcessRuntimeStepAssignment assignment,
        ProcessStepOutcomeResult output,
        string primaryRef)
    {
        var builder = new StringBuilder();
        builder.AppendLine();
        builder.AppendLine("---");
        builder.AppendLine();
        builder.AppendLine("## Runtime Validated Structured Outcome");
        builder.AppendLine();
        builder.AppendLine("The process runtime appended this section after validating the structured process step outcome.");
        builder.AppendLine();
        builder.AppendLine($"- Run id: {assignment.RunId.Value:D}");
        builder.AppendLine($"- Step id: {assignment.StepInstanceId.Value:D}");
        builder.AppendLine($"- Step key: {assignment.StepKey}");
        builder.AppendLine($"- Executor: {assignment.ExecutorDisplayName}");
        builder.AppendLine($"- Status: {output.Status}");
        builder.AppendLine($"- Primary managed ref: {primaryRef}");
        builder.AppendLine($"- Appended at UTC: {DateTimeOffset.UtcNow:u}");
        builder.AppendLine();
        builder.AppendLine("### Reason");
        builder.AppendLine();
        builder.AppendLine(output.Reason.Trim());
        builder.AppendLine();

        if (!string.IsNullOrWhiteSpace(output.BranchOutcomeKey) ||
            !string.IsNullOrWhiteSpace(output.BranchOutcomeTitle))
        {
            builder.AppendLine("### Branch Outcome");
            builder.AppendLine();
            if (!string.IsNullOrWhiteSpace(output.BranchOutcomeKey))
            {
                builder.AppendLine($"- Key: {output.BranchOutcomeKey.Trim()}");
            }

            if (!string.IsNullOrWhiteSpace(output.BranchOutcomeTitle))
            {
                builder.AppendLine($"- Title: {output.BranchOutcomeTitle.Trim()}");
            }

            builder.AppendLine();
        }

        if (!string.IsNullOrWhiteSpace(output.HumanReadableSummaryMarkdown))
        {
            builder.AppendLine("### Summary");
            builder.AppendLine();
            builder.AppendLine(output.HumanReadableSummaryMarkdown.Trim());
            builder.AppendLine();
        }

        AppendList(builder, "Agent Evidence Refs", output.EvidenceRefs);
        AppendList(builder, "Next Actions", output.NextActions);
        return builder.ToString();
    }

    private static void AppendList(
        StringBuilder builder,
        string heading,
        IReadOnlyList<string> values)
    {
        var items = values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .ToArray();
        if (items.Length == 0)
        {
            return;
        }

        builder.AppendLine($"## {heading}");
        builder.AppendLine();
        foreach (var item in items)
        {
            builder.AppendLine($"- {item}");
        }

        builder.AppendLine();
    }

    private static ToolExecutionReceiptRecord CreateManagedOutcomeArtifactReceipt(
        Guid executionRunId,
        string primaryRef,
        string writeMessage,
        string toolName = "workspace_write_file")
        => new(
            Guid.NewGuid(),
            executionRunId,
            "process-runtime",
            toolName,
            "ManagedProcessArtifact",
            "NotRequired",
            "Process runtime persisted validated structured step outcome.",
            primaryRef,
            ".",
            $"Succeeded: {writeMessage}",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);

    private IReadOnlyDictionary<ArtifactSlotId, string> BuildProducedArtifactContentHashes(
        ProcessRuntimeStepAssignment assignment,
        ProcessStepOutcomeResult output,
        out ProcessCompletionIssue? issue)
    {
        issue = null;
        if (assignment.ProducedArtifactSlotIds.Count == 0)
        {
            return new Dictionary<ArtifactSlotId, string>();
        }

        var primaryRef = BuildManagedStepArtifactPath(assignment);
        if (!output.EvidenceRefs.Any(evidenceRef =>
                string.Equals(
                    NormalizeManagedArtifactRef(evidenceRef),
                    NormalizeManagedArtifactRef(primaryRef),
                    StringComparison.OrdinalIgnoreCase)))
        {
            return new Dictionary<ArtifactSlotId, string>();
        }

        var readResult = workspaceFiles.ReadTextFile(primaryRef, maxCharacters: 200000);
        if (!readResult.Succeeded)
        {
            issue = CreateManagedArtifactReadbackIssue(assignment, primaryRef, readResult.Message);
            return new Dictionary<ArtifactSlotId, string>();
        }

        var contentHash = ComputeHash(readResult.Content);
        return assignment.ProducedArtifactSlotIds
            .Distinct()
            .ToDictionary(slotId => slotId, _ => contentHash);
    }

    private static ProcessCompletionIssue CreateManagedArtifactReadbackIssue(
        ProcessRuntimeStepAssignment assignment,
        string primaryRef,
        string readbackMessage)
    {
        var summary = $"Step '{assignment.StepKey}' produced managed artifact evidence '{primaryRef}', but the runtime could not read it back to compute a content-grounded artifact hash: {readbackMessage}";
        return new ProcessCompletionIssue(
            "process.adapter.managed_artifact_readback_failed",
            summary,
            $"{assignment.RunId.Value:D}:{assignment.StepInstanceId.Value:D}:{primaryRef}:{readbackMessage}",
            assignment.ProducedArtifactSlotIds,
            ProcessDiagnosticRetrySafety.UnsafeToRetry,
            ProcessDiagnosticIdempotencyClassification.Unknown);
    }

    private sealed record ManagedOutcomeArtifactMaterialization(
        ProcessStepOutcomeResult Output,
        IReadOnlyList<ToolExecutionReceiptRecord> ToolReceipts,
        ProcessCompletionIssue? Issue)
    {
        public static ManagedOutcomeArtifactMaterialization Unchanged(
            ProcessStepOutcomeResult output,
            IReadOnlyList<ToolExecutionReceiptRecord> toolReceipts)
            => new(output, toolReceipts, null);

        public static ManagedOutcomeArtifactMaterialization Succeeded(
            ProcessStepOutcomeResult output,
            IReadOnlyList<ToolExecutionReceiptRecord> toolReceipts)
            => new(output, toolReceipts, null);

        public static ManagedOutcomeArtifactMaterialization Failed(
            ProcessStepOutcomeResult output,
            IReadOnlyList<ToolExecutionReceiptRecord> toolReceipts,
            ProcessCompletionIssue issue)
            => new(output, toolReceipts, issue);
    }

}
