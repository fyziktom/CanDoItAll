using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Core.Execution;
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
using static CanDoItAll.Modules.Processes.ProcessManagedArtifactFormatter;
using static CanDoItAll.Modules.Processes.ProcessManagedArtifactOutcomeParser;

namespace CanDoItAll.Modules.Processes;

internal sealed class ProcessManagedArtifactService(IWorkspaceFileService workspaceFiles)
{
    internal const string ManagedOutcomeArtifactCapturedHeading = "## Runtime Captured Structured Outcome";
    internal const string ManagedOutcomeArtifactAcceptedHeading = "## Runtime Accepted Completion Gates";

    internal static async Task<IReadOnlyList<ToolExecutionReceiptRecord>> LoadStepCompletionToolReceiptsAsync(
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

    internal ManagedOutcomeArtifactMaterialization MaterializeManagedOutcomeArtifactIfNeeded(
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
                    primaryRef,
                    new ProcessCompletionIssue(
                        "process.adapter.managed_artifact_outcome_append_failed",
                        $"Step '{assignment.StepKey}' recovered a completed primary managed artifact, but the runtime could not append the staged structured outcome to '{primaryRef}': {appendResult.Message}",
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
                    primaryRef,
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
                    primaryRef,
                    new ProcessCompletionIssue(
                        "process.adapter.managed_artifact_outcome_append_failed",
                        $"Step '{assignment.StepKey}' produced a valid structured outcome, but the runtime could not append the staged structured outcome to primary managed artifact '{primaryRef}': {appendResult.Message}",
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
        return ManagedOutcomeArtifactMaterialization.Staged(effectiveOutput, effectiveReceipts, primaryRef);
    }

    internal ProcessCompletionIssue? AcceptManagedOutcomeArtifactIfNeeded(
        ProcessRuntimeStepAssignment assignment,
        ManagedOutcomeArtifactMaterialization materialization,
        Guid executionRunId,
        IReadOnlyList<ToolExecutionReceiptRecord> completionToolReceipts,
        out IReadOnlyList<ToolExecutionReceiptRecord> acceptedToolReceipts)
    {
        acceptedToolReceipts = completionToolReceipts;
        if (!materialization.Lifecycle.RequiresCompletionGateAcceptance ||
            string.IsNullOrWhiteSpace(materialization.Lifecycle.PrimaryRef))
        {
            return null;
        }

        var primaryRef = materialization.Lifecycle.PrimaryRef;
        var appendResult = workspaceFiles.AppendTextFile(
            primaryRef,
            BuildManagedOutcomeArtifactAcceptanceContent(assignment, materialization.Output, primaryRef));
        if (!appendResult.Succeeded)
        {
            return new ProcessCompletionIssue(
                "process.adapter.managed_artifact_acceptance_append_failed",
                $"Step '{assignment.StepKey}' passed completion gates, but the runtime could not append managed artifact acceptance proof to '{primaryRef}': {appendResult.Message}",
                $"{assignment.RunId}:{assignment.StepInstanceId}:managed-artifact-acceptance-append-failed:{primaryRef}:{appendResult.Message}",
                assignment.ProducedArtifactSlotIds,
                ProcessDiagnosticRetrySafety.SafeToRetry,
                ProcessDiagnosticIdempotencyClassification.Idempotent);
        }

        acceptedToolReceipts = completionToolReceipts
            .Append(CreateManagedOutcomeArtifactReceipt(
                executionRunId,
                primaryRef,
                appendResult.Message,
                "workspace_append_file",
                "Process runtime accepted staged managed artifact after completion gates passed."))
            .ToArray();
        return null;
    }

    internal ProcessStepOutcomeResult RecoverUnambiguousBranchOutcomeFromCurrentPrimaryArtifact(
        ProcessRuntimeStepAssignment assignment,
        ProcessStepOutcomeResult output,
        Guid executionRunId,
        IReadOnlyList<ToolExecutionReceiptRecord> currentToolReceipts)
    {
        if (output.Status != ProcessStepOutcomeStatus.Completed ||
            !string.IsNullOrWhiteSpace(output.BranchOutcomeKey) ||
            !currentToolReceipts.Any(receipt => receipt.ExecutionRunId == executionRunId) ||
            !HasManagedArtifactWriteReceipt(
                assignment,
                currentToolReceipts.Where(receipt => receipt.ExecutionRunId == executionRunId).ToArray()))
        {
            return output;
        }

        var primaryRef = BuildManagedStepArtifactPath(assignment);
        var readResult = workspaceFiles.ReadTextFile(primaryRef, maxCharacters: 200000);
        if (!readResult.Succeeded)
        {
            return output;
        }

        var parsedArtifactOutcome = ManagedProcessArtifactOutcomeReader.Read(readResult.Content);
        if (!parsedArtifactOutcome.IsValid ||
            parsedArtifactOutcome.Status != ProcessStepOutcomeStatus.Completed ||
            !parsedArtifactOutcome.HasBranchOutcomeKey)
        {
            return output;
        }

        var matchingDeclaredOutcomes = ProcessBranchOutcomeResolver
            .EnumerateAgentSelectableBranchOutcomes(assignment.Prompt)
            .Where(outcome => string.Equals(
                outcome.Key,
                parsedArtifactOutcome.BranchOutcomeKey,
                StringComparison.OrdinalIgnoreCase))
            .DistinctBy(outcome => outcome.Key, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (matchingDeclaredOutcomes.Length != 1)
        {
            return output;
        }

        var declaredOutcome = matchingDeclaredOutcomes[0];
        var recoveredOutput = new ProcessStepOutcomeResult
        {
            Status = output.Status,
            Reason = output.Reason,
            BranchOutcomeKey = declaredOutcome.Key,
            BranchOutcomeTitle = string.IsNullOrWhiteSpace(output.BranchOutcomeTitle)
                ? declaredOutcome.Title
                : output.BranchOutcomeTitle,
            EvidenceRefs = output.EvidenceRefs,
            AcceptanceCriteriaEvidence = output.AcceptanceCriteriaEvidence,
            NextActions = output.NextActions,
            HumanReadableSummaryMarkdown = output.HumanReadableSummaryMarkdown
        };
        return ProcessAcceptanceCriteriaGate.ValidateAcceptanceCriteriaCompletion(assignment, recoveredOutput) is null
            ? recoveredOutput
            : output;
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

    internal IReadOnlyDictionary<ArtifactSlotId, string> BuildProducedArtifactContentHashes(
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

    internal static ProcessCompletionIssue CreateManagedArtifactReadbackIssue(
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

    internal sealed record ManagedOutcomeArtifactMaterialization(
        ProcessStepOutcomeResult Output,
        IReadOnlyList<ToolExecutionReceiptRecord> ToolReceipts,
        ManagedOutcomeArtifactLifecycle Lifecycle,
        ProcessCompletionIssue? Issue)
    {
        public static ManagedOutcomeArtifactMaterialization Unchanged(
            ProcessStepOutcomeResult output,
            IReadOnlyList<ToolExecutionReceiptRecord> toolReceipts)
            => new(output, toolReceipts, ManagedOutcomeArtifactLifecycle.Unchanged, null);

        public static ManagedOutcomeArtifactMaterialization Staged(
            ProcessStepOutcomeResult output,
            IReadOnlyList<ToolExecutionReceiptRecord> toolReceipts,
            string primaryRef)
            => new(
                output,
                toolReceipts,
                new ManagedOutcomeArtifactLifecycle(
                    ManagedOutcomeArtifactLifecycleState.StructuredOutcomeStaged,
                    primaryRef),
                null);

        public static ManagedOutcomeArtifactMaterialization Failed(
            ProcessStepOutcomeResult output,
            IReadOnlyList<ToolExecutionReceiptRecord> toolReceipts,
            string primaryRef,
            ProcessCompletionIssue issue)
            => new(
                output,
                toolReceipts,
                new ManagedOutcomeArtifactLifecycle(
                    ManagedOutcomeArtifactLifecycleState.MaterializationFailed,
                    primaryRef),
                issue);
    }

    internal sealed record ManagedOutcomeArtifactLifecycle(
        ManagedOutcomeArtifactLifecycleState State,
        string? PrimaryRef)
    {
        public static ManagedOutcomeArtifactLifecycle Unchanged { get; } = new(
            ManagedOutcomeArtifactLifecycleState.Unchanged,
            PrimaryRef: null);

        public bool RequiresCompletionGateAcceptance => State == ManagedOutcomeArtifactLifecycleState.StructuredOutcomeStaged;
    }

    internal enum ManagedOutcomeArtifactLifecycleState
    {
        Unchanged,
        StructuredOutcomeStaged,
        MaterializationFailed
    }
}
