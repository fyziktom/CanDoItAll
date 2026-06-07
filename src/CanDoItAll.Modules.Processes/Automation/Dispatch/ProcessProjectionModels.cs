using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Modules.Processes;

internal sealed record ProcessProjectionRunSnapshot(
    Guid Id,
    ProcessAutomationExecutionState State,
    ProcessAutomationRunOutcome? Outcome,
    string InputSummary,
    string ResultSummary,
    string? SerializedSessionStateJson,
    IReadOnlyList<ProcessAutomationExecutionArtifact> Artifacts);

internal sealed record ProcessProjectionObservationSnapshot(
    IReadOnlyList<string> SuccessfulWorkspaceFileMutationReceiptPaths,
    IReadOnlyDictionary<string, IReadOnlyList<string>> SuccessfulBrowserToolOutputFiles,
    string? ProviderNativeBrowserWorkingDirectory);

internal sealed record ProcessProjectionStepSnapshot(
    Guid Id,
    int Sequence,
    string Title,
    string CurrentExecutorName,
    string DecisionSummary);

internal sealed record ProcessProjectionCandidateSnapshot(
    Guid RunId,
    Guid? ProjectId,
    ProcessProjectionStepSnapshot Step,
    IReadOnlyList<ProcessArtifactExpectationSnapshot> ExpectedArtifacts,
    ProcessProjectionMutableCandidateState MutableState)
{
    public string CurrentRunManagedArtifactRoot { get; } = WorkspaceScopeDescriptor.NormalizeRelativePath(
        Path.Combine("artifacts", "process-runs", RunId.ToString("D")));
}

internal sealed class ProcessProjectionMutableCandidateState
{
    private readonly HashSet<Guid> recordedArtifactExpectationIds;
    private readonly HashSet<string> externalReferenceKeys;

    public ProcessProjectionMutableCandidateState(
        HashSet<Guid> recordedArtifactExpectationIds,
        HashSet<string> externalReferenceKeys)
    {
        this.recordedArtifactExpectationIds = recordedArtifactExpectationIds;
        this.externalReferenceKeys = externalReferenceKeys;
    }

    public IReadOnlySet<Guid> RecordedArtifactExpectationIds => recordedArtifactExpectationIds;

    public IReadOnlySet<string> ExternalReferenceKeys => externalReferenceKeys;

    public bool HasRecordedArtifactExpectation(Guid artifactExpectationId)
    {
        return recordedArtifactExpectationIds.Contains(artifactExpectationId);
    }

    public bool HasExternalReferenceKey(string externalReferenceKey)
    {
        return externalReferenceKeys.Contains(externalReferenceKey);
    }

    public void AddProjection(string externalReferenceKey, Guid? artifactExpectationId)
    {
        externalReferenceKeys.Add(externalReferenceKey);
        if (artifactExpectationId is { } expectationId)
        {
            recordedArtifactExpectationIds.Add(expectationId);
        }
    }
}

internal sealed record ProcessProjectionStepDispatchClaim(
    Guid StepRunId,
    string ClaimToken);

internal sealed record ProcessProjectionLineageInput(
    Guid? RecoveryExecutionRunId,
    Guid? RecoveredForExecutionRunId,
    Guid? ReworkPacketId)
{
    public static ProcessProjectionLineageInput Empty { get; } = new(null, null, null);

    public ProcessArtifactRecoveryProjectionContext ToRecoveryContext()
    {
        return new ProcessArtifactRecoveryProjectionContext(
            RecoveryExecutionRunId,
            RecoveredForExecutionRunId,
            ReworkPacketId);
    }
}

internal readonly record struct ProcessProjectionProcessMockArtifact(
    string RoleKey,
    string? BranchOutcomeKey,
    string RelativePath,
    string ContentSignalText);

internal sealed record ProcessProjectionSessionFileContent(
    string Path,
    string Content);

internal static class ProcessProjectionSnapshotBuilderAdapter
{
    public static ProcessProjectionCandidateSnapshot FromDispatchCandidate(
        ProcessRunAutomationDispatchService.DispatchCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        return new ProcessProjectionCandidateSnapshot(
            candidate.Run.Id,
            candidate.Run.ProjectId,
            new ProcessProjectionStepSnapshot(
                candidate.StepRun.Id,
                candidate.StepRun.Sequence,
                candidate.StepRun.Title,
                candidate.StepRun.CurrentExecutorName,
                candidate.StepRun.DecisionSummary),
            candidate.ExpectedArtifacts
                .Select(ProcessArtifactValidationSnapshotBuilder.FromDispatchExpectation)
                .ToList(),
            new ProcessProjectionMutableCandidateState(
                candidate.RecordedArtifactExpectationIds,
                candidate.ExternalReferenceKeys));
    }

    public static ProcessProjectionRunSnapshot FromExecutionDetail(ProcessAutomationExecutionRunDetail detail)
    {
        ArgumentNullException.ThrowIfNull(detail);

        return new ProcessProjectionRunSnapshot(
            detail.Run.Id,
            detail.Run.State,
            detail.Run.Outcome,
            detail.Run.InputSummary,
            detail.Run.ResultSummary,
            detail.Run.SerializedSessionStateJson,
            detail.Artifacts);
    }

    public static ProcessProjectionObservationSnapshot FromExecutionDetailObservations(
        ProcessAutomationExecutionRunDetail detail)
    {
        ArgumentNullException.ThrowIfNull(detail);

        var canTrustCompletedInternalToolLogs = detail.Run.State == ProcessAutomationExecutionState.Completed &&
                                                detail.Run.Outcome == ProcessAutomationRunOutcome.Succeeded;
        return new ProcessProjectionObservationSnapshot(
            ProcessAutomationReceiptObservationHelper.ResolveSuccessfulReceipts(detail)
                .Where(IsSuccessfulWorkspaceFileMutationReceipt)
                .SelectMany(ResolveManagedWorkspacePathsFromReceipt)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList(),
            ProcessAutomationObservationSnapshot
                .Create(detail, canTrustCompletedInternalToolLogs)
                .BrowserToolOutputFiles,
            ProcessProviderNativeBrowserOutputFacts.ResolveProviderNativeBrowserWorkingDirectory(detail));
    }

    public static ProcessProjectionStepDispatchClaim FromDispatchClaim(
        ProcessRunAutomationDispatchService.ProcessStepDispatchClaim dispatchClaim)
    {
        ArgumentNullException.ThrowIfNull(dispatchClaim);

        return new ProcessProjectionStepDispatchClaim(
            dispatchClaim.StepRunId,
            dispatchClaim.ClaimToken);
    }

    public static ProcessRunAutomationDispatchService.ProcessStepDispatchClaim ToDispatchClaim(
        ProcessProjectionStepDispatchClaim dispatchClaim)
    {
        ArgumentNullException.ThrowIfNull(dispatchClaim);

        return new ProcessRunAutomationDispatchService.ProcessStepDispatchClaim(
            dispatchClaim.StepRunId,
            dispatchClaim.ClaimToken);
    }

    public static ProcessProjectionLineageInput FromDispatchLineage(
        ProcessRunAutomationDispatchService.ArtifactProjectionLineage? lineage)
    {
        return lineage is null
            ? ProcessProjectionLineageInput.Empty
            : new ProcessProjectionLineageInput(
                lineage.RecoveryExecutionRunId,
                lineage.RecoveredForExecutionRunId,
                lineage.ReworkPacketId);
    }

    private static bool IsSuccessfulWorkspaceFileMutationReceipt(ProcessAutomationToolExecutionReceipt receipt)
    {
        var toolName = ProcessToolReceiptFacts.NormalizeToolToken(receipt.ToolName);
        return (string.Equals(toolName, "workspace_write_file", StringComparison.Ordinal) ||
                string.Equals(toolName, "workspace_append_file", StringComparison.Ordinal)) &&
               !ProcessToolReceiptFacts.IsFailedReceipt(receipt);
    }

    private static IReadOnlyList<string> ResolveManagedWorkspacePathsFromReceipt(ProcessAutomationToolExecutionReceipt receipt)
    {
        var text = string.Join(
            Environment.NewLine,
            [
                receipt.RequestSummary,
                receipt.ExitSummary
            ]);
        return ProcessConcreteProductPathRules.ResolveWorkspacePathsFromToolRequest(text);
    }
}
