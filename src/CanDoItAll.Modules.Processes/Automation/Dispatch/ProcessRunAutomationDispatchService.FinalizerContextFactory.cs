namespace CanDoItAll.Modules.Processes;

internal static class ProcessDispatchFinalizerContextFactory
{
    public static ProcessRunAutomationDispatchService.ProcessStepCompletionFinalizerContext ForManagerArtifactRecovery(
        ProcessRunAutomationDispatchService.DispatchCandidate candidate,
        ProcessRunAutomationDispatchService.DispatchExecutionOutcome recoveryOutcome,
        string trigger,
        Func<CancellationToken, Task> renewLeaseAsync)
    {
        return new ProcessRunAutomationDispatchService.ProcessStepCompletionFinalizerContext(
            ExecutorKind: ProcessRunAutomationDispatchService.ProcessStepCompletionExecutorKind.ManagerArtifactRecovery,
            Candidate: candidate,
            CompletionStatus: recoveryOutcome.CompletionStatus,
            CompletionReason: recoveryOutcome.CompletionReason,
            SelectedBranchOutcomeId: recoveryOutcome.SelectedBranchOutcomeId,
            ExecutionDetail: recoveryOutcome.Detail,
            WorkflowRunId: null,
            SubprocessRunId: null,
            ResponseText: recoveryOutcome.ResponseText,
            ProjectExecutionArtifacts: false,
            AllowManagerArtifactRecovery: false,
            Trigger: trigger,
            RenewLeaseAsync: renewLeaseAsync,
            RecoveryExecutionRunId: recoveryOutcome.Detail.Run.Id,
            RecoveredForExecutionRunId: candidate.RecoveryExecutionRunId);
    }

    public static ProcessRunAutomationDispatchService.ProcessStepCompletionFinalizerContext ForDirectAgent(
        ProcessRunAutomationDispatchService.DispatchCandidate candidate,
        ProcessRunAutomationDispatchService.DispatchExecutionOutcome executionOutcome,
        string trigger,
        Func<CancellationToken, Task> renewLeaseAsync)
    {
        return new ProcessRunAutomationDispatchService.ProcessStepCompletionFinalizerContext(
            ExecutorKind: ProcessRunAutomationDispatchService.ProcessStepCompletionExecutorKind.DirectAgent,
            Candidate: candidate,
            CompletionStatus: executionOutcome.CompletionStatus,
            CompletionReason: executionOutcome.CompletionReason,
            SelectedBranchOutcomeId: executionOutcome.SelectedBranchOutcomeId,
            ExecutionDetail: executionOutcome.Detail,
            WorkflowRunId: null,
            SubprocessRunId: null,
            ResponseText: executionOutcome.ResponseText,
            ProjectExecutionArtifacts: true,
            AllowManagerArtifactRecovery: true,
            Trigger: trigger,
            RenewLeaseAsync: renewLeaseAsync);
    }

    public static ProcessRunAutomationDispatchService.ProcessStepCompletionFinalizerContext ForWorkflow(
        ProcessRunAutomationDispatchService.DispatchCandidate candidate,
        ProcessWorkflowExecutionOutcome workflowOutcome)
    {
        return new ProcessRunAutomationDispatchService.ProcessStepCompletionFinalizerContext(
            ProcessRunAutomationDispatchService.ProcessStepCompletionExecutorKind.WorkflowBackedRole,
            candidate,
            workflowOutcome.CompletionStatus,
            workflowOutcome.CompletionReason,
            SelectedBranchOutcomeId: null,
            ExecutionDetail: null,
            WorkflowRunId: workflowOutcome.Link?.WorkflowRunId,
            SubprocessRunId: null,
            ResponseText: workflowOutcome.CompletionReason,
            ProjectExecutionArtifacts: false,
            AllowManagerArtifactRecovery: false,
            Trigger: "workflow-execution-outcome",
            RenewLeaseAsync: null);
    }

    public static ProcessRunAutomationDispatchService.ProcessStepCompletionFinalizerContext ForSubprocess(
        ProcessRunAutomationDispatchService.DispatchCandidate candidate,
        Guid subprocessRunId,
        ProcessStepRunStatus terminalStatus,
        string transitionReason)
    {
        return new ProcessRunAutomationDispatchService.ProcessStepCompletionFinalizerContext(
            ProcessRunAutomationDispatchService.ProcessStepCompletionExecutorKind.SubprocessParent,
            candidate,
            terminalStatus,
            transitionReason,
            SelectedBranchOutcomeId: null,
            ExecutionDetail: null,
            WorkflowRunId: null,
            SubprocessRunId: subprocessRunId,
            ResponseText: transitionReason,
            ProjectExecutionArtifacts: false,
            AllowManagerArtifactRecovery: false,
            Trigger: "subprocess-execution-outcome",
            RenewLeaseAsync: null);
    }
}
