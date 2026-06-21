namespace CanDoItAll.Modules.Processes;

internal sealed partial class ProcessRunAutomationDispatchService
{
    private static ProcessStepTransitionRequest BuildFinalizedStepTransitionRequest(
        DispatchCandidate candidate,
        ProcessStepCompletionFinalizerResult finalizerResult)
    {
        return new ProcessStepTransitionRequest
        {
            StepRunId = candidate.StepRun.Id,
            StepRunConcurrencyToken = finalizerResult.StepRunConcurrencyToken,
            TargetStatus = finalizerResult.CompletionStatus,
            Reason = finalizerResult.CompletionReason,
            BlockCause = finalizerResult.BlockCause,
            SelectedBranchOutcomeId = finalizerResult.SelectedBranchOutcomeId,
            DecidedBy = AutomationActor,
            SuppressAutomationDispatch = finalizerResult.CompletionStatus != ProcessStepRunStatus.Completed,
            ArtifactValidationExecutorKind = finalizerResult.ArtifactValidationContext.ExecutorKind,
            ArtifactValidationExecutionRunId = finalizerResult.ArtifactValidationContext.ExecutionRunId,
            ArtifactValidationWorkflowRunId = finalizerResult.ArtifactValidationContext.WorkflowRunId,
            ArtifactValidationSubprocessRunId = finalizerResult.ArtifactValidationContext.SubprocessRunId,
            ArtifactValidationRecoveryExecutionRunId = finalizerResult.ArtifactValidationContext.RecoveryExecutionRunId,
            ArtifactValidationRecoveredForExecutionRunId = finalizerResult.ArtifactValidationContext.RecoveredForExecutionRunId
        };
    }

    private static ProcessStepTransitionArtifactValidationContext BuildStepTransitionArtifactValidationContext(
        ProcessStepCompletionFinalizerContext context)
    {
        return new ProcessStepTransitionArtifactValidationContext(
            context.ExecutorKind,
            context.ExecutionDetail?.Run.Id,
            context.WorkflowRunId,
            context.SubprocessRunId,
            context.RecoveryExecutionRunId,
            context.RecoveredForExecutionRunId);
    }
}
