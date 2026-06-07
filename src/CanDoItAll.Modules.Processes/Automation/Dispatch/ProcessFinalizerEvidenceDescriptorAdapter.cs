using CoreFinalizerBlockCauseKind = global::CanDoItAll.Processes.Core.Finalization.ProcessCoreFinalizerBlockCauseKind;
using CoreFinalizerEvidenceDescriptor = global::CanDoItAll.Processes.Core.Finalization.ProcessFinalizerEvidenceDescriptor;
using CoreFinalizerEvidenceDescriptorRules = global::CanDoItAll.Processes.Core.Finalization.ProcessFinalizerEvidenceDescriptorRules;
using CoreFinalizerIntentEvidenceDescriptor = global::CanDoItAll.Processes.Core.Finalization.ProcessFinalizerIntentEvidenceDescriptor;
using CoreFinalizerKind = global::CanDoItAll.Processes.Core.Finalization.ProcessCoreFinalizerKind;
using CoreFinalizerResultEvidenceDescriptor = global::CanDoItAll.Processes.Core.Finalization.ProcessFinalizerResultEvidenceDescriptor;

namespace CanDoItAll.Modules.Processes;

using FinalizerContext = ProcessRunAutomationDispatchService.ProcessStepCompletionFinalizerContext;
using FinalizerResult = ProcessRunAutomationDispatchService.ProcessStepCompletionFinalizerResult;
using ModuleFinalizerKind = ProcessRunAutomationDispatchService.ProcessStepCompletionExecutorKind;

internal static class ProcessFinalizerEvidenceDescriptorAdapter
{
    public static CoreFinalizerEvidenceDescriptor Describe(
        FinalizerContext context,
        FinalizerResult? result)
    {
        return new CoreFinalizerEvidenceDescriptor(
            DescribeIntent(context),
            DescribeResult(result));
    }

    public static CoreFinalizerIntentEvidenceDescriptor DescribeIntent(FinalizerContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return CoreFinalizerEvidenceDescriptorRules.DescribeIntent(
            MapFinalizerKind(context.ExecutorKind),
            context.Candidate.Run.Id,
            context.Candidate.StepRun.Id,
            context.CompletionStatus,
            context.CompletionReason,
            context.SelectedBranchOutcomeId,
            context.ExecutionDetail?.Run.Id,
            context.WorkflowRunId,
            context.SubprocessRunId,
            context.ProjectExecutionArtifacts,
            context.AllowManagerArtifactRecovery,
            context.Trigger,
            context.RenewLeaseAsync is not null,
            context.RecoveryExecutionRunId,
            context.RecoveredForExecutionRunId);
    }

    public static CoreFinalizerResultEvidenceDescriptor DescribeResult(FinalizerResult? result)
    {
        return result is null
            ? CoreFinalizerEvidenceDescriptorRules.DescribeNoResult()
            : CoreFinalizerEvidenceDescriptorRules.DescribeAppliedResult(
                result.CompletionStatus,
                result.CompletionReason,
                MapBlockCauseKind(result.BlockCause),
                result.SelectedBranchOutcomeId,
                result.StepRunConcurrencyToken,
                result.ArtifactValidationResults.Count);
    }

    private static CoreFinalizerKind MapFinalizerKind(ModuleFinalizerKind finalizerKind)
    {
        return finalizerKind switch
        {
            ModuleFinalizerKind.DirectAgent => CoreFinalizerKind.DirectAgent,
            ModuleFinalizerKind.WorkflowBackedRole => CoreFinalizerKind.WorkflowBackedRole,
            ModuleFinalizerKind.SubprocessParent => CoreFinalizerKind.SubprocessParent,
            ModuleFinalizerKind.ManagerArtifactRecovery => CoreFinalizerKind.ManagerArtifactRecovery,
            ModuleFinalizerKind.Manual => CoreFinalizerKind.Manual,
            _ => throw new ArgumentOutOfRangeException(nameof(finalizerKind), finalizerKind, "Unknown process finalizer kind.")
        };
    }

    private static CoreFinalizerBlockCauseKind MapBlockCauseKind(ProcessStepBlockCause? blockCause)
    {
        return blockCause switch
        {
            null => CoreFinalizerBlockCauseKind.None,
            ProcessStepBlockCause.OwnOutput => CoreFinalizerBlockCauseKind.OwnOutput,
            ProcessStepBlockCause.UpstreamInput => CoreFinalizerBlockCauseKind.UpstreamInput,
            ProcessStepBlockCause.RuntimeEvidence => CoreFinalizerBlockCauseKind.RuntimeEvidence,
            ProcessStepBlockCause.PolicyDenied => CoreFinalizerBlockCauseKind.PolicyDenied,
            _ => throw new ArgumentOutOfRangeException(nameof(blockCause), blockCause, "Unknown process step block cause.")
        };
    }
}
