using CanDoItAll.Processes.Contracts;

namespace CanDoItAll.Processes.Core.Finalization;

public enum ProcessCoreFinalizerKind
{
    DirectAgent = 0,
    WorkflowBackedRole = 1,
    SubprocessParent = 2,
    ManagerArtifactRecovery = 3,
    Manual = 4
}

public enum ProcessCoreFinalizerBlockCauseKind
{
    None = 0,
    OwnOutput = 1,
    UpstreamInput = 2,
    RuntimeEvidence = 3,
    PolicyDenied = 4
}

public sealed record ProcessFinalizerIntentEvidenceDescriptor(
    ProcessCoreFinalizerKind FinalizerKind,
    Guid ProcessRunId,
    Guid StepRunId,
    ProcessStepRunStatus CompletionStatus,
    string CompletionReason,
    Guid? SelectedBranchOutcomeId,
    Guid? ExecutionRunId,
    Guid? WorkflowRunId,
    Guid? SubprocessRunId,
    bool ProjectsExecutionArtifacts,
    bool AllowsManagerArtifactRecovery,
    string Trigger,
    bool RequiresLeaseRenewal,
    Guid? RecoveryExecutionRunId,
    Guid? RecoveredForExecutionRunId);

public sealed record ProcessFinalizerResultEvidenceDescriptor(
    bool HasResult,
    bool ShouldApplyTransition,
    ProcessStepRunStatus? CompletionStatus,
    string CompletionReason,
    ProcessCoreFinalizerBlockCauseKind BlockCauseKind,
    Guid? SelectedBranchOutcomeId,
    Guid? StepRunConcurrencyToken,
    int ArtifactValidationResultCount,
    bool HasArtifactValidationResults);

public sealed record ProcessFinalizerEvidenceDescriptor(
    ProcessFinalizerIntentEvidenceDescriptor Intent,
    ProcessFinalizerResultEvidenceDescriptor Result);

public static class ProcessFinalizerEvidenceDescriptorRules
{
    public static ProcessFinalizerIntentEvidenceDescriptor DescribeIntent(
        ProcessCoreFinalizerKind finalizerKind,
        Guid processRunId,
        Guid stepRunId,
        ProcessStepRunStatus completionStatus,
        string completionReason,
        Guid? selectedBranchOutcomeId,
        Guid? executionRunId,
        Guid? workflowRunId,
        Guid? subprocessRunId,
        bool projectsExecutionArtifacts,
        bool allowsManagerArtifactRecovery,
        string trigger,
        bool requiresLeaseRenewal,
        Guid? recoveryExecutionRunId,
        Guid? recoveredForExecutionRunId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(completionReason);
        ArgumentException.ThrowIfNullOrWhiteSpace(trigger);

        return new ProcessFinalizerIntentEvidenceDescriptor(
            finalizerKind,
            processRunId,
            stepRunId,
            completionStatus,
            completionReason.Trim(),
            selectedBranchOutcomeId,
            executionRunId,
            workflowRunId,
            subprocessRunId,
            projectsExecutionArtifacts,
            allowsManagerArtifactRecovery,
            trigger.Trim(),
            requiresLeaseRenewal,
            recoveryExecutionRunId,
            recoveredForExecutionRunId);
    }

    public static ProcessFinalizerResultEvidenceDescriptor DescribeNoResult()
    {
        return new ProcessFinalizerResultEvidenceDescriptor(
            HasResult: false,
            ShouldApplyTransition: false,
            CompletionStatus: null,
            CompletionReason: string.Empty,
            ProcessCoreFinalizerBlockCauseKind.None,
            SelectedBranchOutcomeId: null,
            StepRunConcurrencyToken: null,
            ArtifactValidationResultCount: 0,
            HasArtifactValidationResults: false);
    }

    public static ProcessFinalizerResultEvidenceDescriptor DescribeAppliedResult(
        ProcessStepRunStatus completionStatus,
        string completionReason,
        ProcessCoreFinalizerBlockCauseKind blockCauseKind,
        Guid? selectedBranchOutcomeId,
        Guid stepRunConcurrencyToken,
        int artifactValidationResultCount)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(completionReason);
        ArgumentOutOfRangeException.ThrowIfNegative(artifactValidationResultCount);

        return new ProcessFinalizerResultEvidenceDescriptor(
            HasResult: true,
            ShouldApplyTransition: true,
            completionStatus,
            completionReason.Trim(),
            blockCauseKind,
            selectedBranchOutcomeId,
            stepRunConcurrencyToken,
            artifactValidationResultCount,
            artifactValidationResultCount > 0);
    }
}
