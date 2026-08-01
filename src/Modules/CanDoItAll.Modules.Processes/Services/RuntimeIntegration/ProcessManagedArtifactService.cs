using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Core.Execution;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Runtime;

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
    internal const string ManagedArtifactMaterializationTooLargeDiagnosticCode =
        "process.adapter.managed_artifact_materialization_too_large";
    internal const string ManagedArtifactAcceptanceTooLargeDiagnosticCode =
        "process.adapter.managed_artifact_acceptance_too_large";
    private static readonly object ManagedArtifactMutationGate = new();

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
        if (output.Status != ProcessStepOutcomeStatus.Completed)
        {
            return ManagedOutcomeArtifactMaterialization.Unchanged(output, toolReceipts);
        }

        if (assignment.ProducedArtifactSlotIds.Count == 0)
        {
            return ManagedOutcomeArtifactMaterialization.Unchanged(output, toolReceipts);
        }

        var primaryRef = BuildManagedStepArtifactPath(assignment);
        if (!NormalizeOperations(assignment.AllowedOperations).Contains(
                ProcessOperationContractNames.WriteManagedProcessArtifacts,
                StringComparer.OrdinalIgnoreCase))
        {
            return ManagedOutcomeArtifactMaterialization.Failed(
                output,
                toolReceipts,
                primaryRef,
                new ProcessCompletionIssue(
                    "process.adapter.managed_artifact_materialization_not_authorized",
                    $"Step '{assignment.StepKey}' produced a valid structured outcome, but its persisted assignment does not authorize runtime materialization of primary managed artifact '{primaryRef}'.",
                    $"{assignment.RunId}:{assignment.StepInstanceId}:managed-artifact-materialization-not-authorized:{primaryRef}",
                    assignment.ProducedArtifactSlotIds,
                    ProcessDiagnosticRetrySafety.UnsafeToRetry,
                    ProcessDiagnosticIdempotencyClassification.Idempotent));
        }

        var hasManagedEvidence = HasAllManagedArtifactEvidence(assignment, output.EvidenceRefs);
        var hasWriteReceipt = HasManagedArtifactWriteReceipt(assignment, toolReceipts);
        IReadOnlyList<ToolExecutionReceiptRecord> effectiveReceipts = toolReceipts;
        var capturedMarker = BuildManagedOutcomeArtifactLifecycleMarker(
            executionRunId,
            ManagedOutcomeArtifactLifecyclePhase.Captured);
        lock (ManagedArtifactMutationGate)
        {
            if (ContainsLifecycleMarker(primaryRef, capturedMarker))
            {
                if (!hasWriteReceipt)
                {
                    effectiveReceipts = toolReceipts
                        .Append(CreateManagedOutcomeArtifactReceipt(
                            executionRunId,
                            primaryRef,
                            "Reused the managed artifact already staged for this execution run."))
                        .ToArray();
                }
            }
            else if (!hasWriteReceipt)
            {
                var artifactContent = BuildManagedOutcomeArtifactContent(
                    assignment,
                    output,
                    executionRunId,
                    primaryRef);
                var acceptanceContent = BuildManagedOutcomeArtifactAcceptanceContent(
                    assignment,
                    output,
                    executionRunId,
                    primaryRef);
                if (artifactContent.Length + acceptanceContent.Length > WorkspaceFileLimits.MaxTextReadCharacters)
                {
                    return ManagedOutcomeArtifactMaterialization.Failed(
                        output,
                        toolReceipts,
                        primaryRef,
                        new ProcessCompletionIssue(
                            ManagedArtifactMaterializationTooLargeDiagnosticCode,
                            $"Step '{assignment.StepKey}' produced a valid structured outcome, but its runtime-managed artifact would exceed the complete-read limit of {WorkspaceFileLimits.MaxTextReadCharacters} characters.",
                            $"{assignment.RunId}:{assignment.StepInstanceId}:managed-artifact-materialization-too-large:{primaryRef}:{artifactContent.Length}:{acceptanceContent.Length}",
                            assignment.ProducedArtifactSlotIds,
                            ProcessDiagnosticRetrySafety.SafeToRetry,
                            ProcessDiagnosticIdempotencyClassification.Idempotent));
                }

                var writeResult = workspaceFiles.WriteTextFile(
                    primaryRef,
                    artifactContent,
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
                var appendixContent = BuildManagedOutcomeArtifactAppendixContent(
                    assignment,
                    output,
                    executionRunId,
                    primaryRef);
                if (ValidateManagedArtifactAppendBoundary(
                        assignment,
                        primaryRef,
                        appendixContent,
                        ManagedArtifactMaterializationTooLargeDiagnosticCode,
                        "captured structured-outcome appendix") is { } appendBoundaryIssue)
                {
                    return ManagedOutcomeArtifactMaterialization.Failed(
                        output,
                        toolReceipts,
                        primaryRef,
                        appendBoundaryIssue);
                }

                var appendResult = workspaceFiles.AppendTextFile(
                    primaryRef,
                    appendixContent);
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
        }

        var effectiveOutput = hasManagedEvidence
            ? output
            : CopyWithEvidenceRef(output, primaryRef);
        return ManagedOutcomeArtifactMaterialization.Staged(effectiveOutput, effectiveReceipts, primaryRef);
    }

    internal ProcessCompletionIssue? AcceptManagedOutcomeArtifactIfNeeded(
        ProcessRuntimeStepAssignment assignment,
        ManagedOutcomeArtifactMaterialization materialization,
        Guid executionRunId,
        IReadOnlyList<ToolExecutionReceiptRecord> completionToolReceipts,
        out IReadOnlyList<ToolExecutionReceiptRecord> acceptedToolReceipts,
        out IReadOnlyDictionary<ArtifactSlotId, string>? acceptedArtifactContentHashes)
    {
        acceptedToolReceipts = completionToolReceipts;
        acceptedArtifactContentHashes = null;
        if (!materialization.Lifecycle.RequiresCompletionGateAcceptance ||
            string.IsNullOrWhiteSpace(materialization.Lifecycle.PrimaryRef))
        {
            return null;
        }

        var primaryRef = materialization.Lifecycle.PrimaryRef;
        var acceptedMarker = BuildManagedOutcomeArtifactLifecycleMarker(
            executionRunId,
            ManagedOutcomeArtifactLifecyclePhase.Accepted);
        lock (ManagedArtifactMutationGate)
        {
            if (ContainsLifecycleMarker(primaryRef, acceptedMarker))
            {
                acceptedToolReceipts = completionToolReceipts
                    .Append(CreateManagedOutcomeArtifactReceipt(
                        executionRunId,
                        primaryRef,
                        "Reused the managed artifact acceptance already recorded for this execution run.",
                        "workspace_append_file",
                        "Process runtime reused idempotent managed artifact acceptance proof."))
                    .ToArray();
                return BuildAcceptedArtifactContentHashes(
                    assignment,
                    materialization.Output,
                    out acceptedArtifactContentHashes);
            }

            var acceptanceContent = BuildManagedOutcomeArtifactAcceptanceContent(
                assignment,
                materialization.Output,
                executionRunId,
                primaryRef);
            if (ValidateManagedArtifactAppendBoundary(
                    assignment,
                    primaryRef,
                    acceptanceContent,
                    ManagedArtifactAcceptanceTooLargeDiagnosticCode,
                    "completion-gate acceptance appendix") is { } appendBoundaryIssue)
            {
                return appendBoundaryIssue;
            }

            var appendResult = workspaceFiles.AppendTextFile(
                primaryRef,
                acceptanceContent);
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

            return BuildAcceptedArtifactContentHashes(
                assignment,
                materialization.Output,
                out acceptedArtifactContentHashes);
        }
    }

    private ProcessCompletionIssue? ValidateManagedArtifactAppendBoundary(
        ProcessRuntimeStepAssignment assignment,
        string primaryRef,
        string appendixContent,
        string diagnosticCode,
        string appendixDescription)
    {
        var readResult = workspaceFiles.ReadTextFile(
            primaryRef,
            WorkspaceFileLimits.MaxTextReadCharacters);
        if (!readResult.Succeeded || readResult.IsTruncated)
        {
            var readbackMessage = !readResult.Succeeded
                ? readResult.Message
                : $"content exceeds the complete-read limit of {WorkspaceFileLimits.MaxTextReadCharacters} characters";
            return CreateManagedArtifactReadbackIssue(
                assignment,
                primaryRef,
                readbackMessage);
        }

        var resultingCharacterCount = readResult.Content.Length + appendixContent.Length;
        if (resultingCharacterCount <= WorkspaceFileLimits.MaxTextReadCharacters)
        {
            return null;
        }

        return new ProcessCompletionIssue(
            diagnosticCode,
            $"Step '{assignment.StepKey}' cannot append its {appendixDescription} to primary managed artifact '{primaryRef}' because the result would exceed the complete-read limit of {WorkspaceFileLimits.MaxTextReadCharacters} characters. The artifact was left unchanged.",
            $"{assignment.RunId}:{assignment.StepInstanceId}:{diagnosticCode}:{primaryRef}:{resultingCharacterCount}",
            assignment.ProducedArtifactSlotIds,
            ProcessDiagnosticRetrySafety.SafeToRetry,
            ProcessDiagnosticIdempotencyClassification.Idempotent);
    }

    private ProcessCompletionIssue? BuildAcceptedArtifactContentHashes(
        ProcessRuntimeStepAssignment assignment,
        ProcessStepOutcomeResult output,
        out IReadOnlyDictionary<ArtifactSlotId, string> acceptedArtifactContentHashes)
    {
        acceptedArtifactContentHashes = BuildProducedArtifactContentHashes(
            assignment,
            output,
            out var readbackIssue);
        return readbackIssue;
    }

    private bool ContainsLifecycleMarker(string primaryRef, string marker)
    {
        var readResult = workspaceFiles.ReadTextFile(
            primaryRef,
            WorkspaceFileLimits.MaxTextReadCharacters);
        return readResult.Succeeded &&
               !readResult.IsTruncated &&
               readResult.Content.Contains(marker, StringComparison.Ordinal);
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

        var readResult = workspaceFiles.ReadTextFile(
            primaryRef,
            WorkspaceFileLimits.MaxTextReadCharacters);
        if (!readResult.Succeeded || readResult.IsTruncated)
        {
            var readbackMessage = !readResult.Succeeded
                ? readResult.Message
                : $"content exceeds the complete-read limit of {WorkspaceFileLimits.MaxTextReadCharacters} characters";
            issue = CreateManagedArtifactReadbackIssue(
                assignment,
                primaryRef,
                readbackMessage);
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
