namespace CanDoItAll.Modules.Processes;

internal static class ProcessSubprocessLifecycleRules
{
    public static ProcessStepTransitionRequest BuildStartTransitionRequest(
        ProcessStepRun stepRun,
        string normalizedTrigger,
        string automationActor)
    {
        ArgumentNullException.ThrowIfNull(stepRun);

        return new ProcessStepTransitionRequest
        {
            StepRunId = stepRun.Id,
            StepRunConcurrencyToken = stepRun.ConcurrencyToken,
            TargetStatus = ProcessStepRunStatus.InProgress,
            Reason = $"Started subprocess by the durable process automation dispatcher ({normalizedTrigger}).",
            DecidedBy = automationActor,
            SuppressAutomationDispatch = true
        };
    }

    public static ProcessStepTransitionRequest BuildEnsureFailureBlockTransitionRequest(
        ProcessStepRun stepRun,
        string reason,
        string automationActor)
    {
        ArgumentNullException.ThrowIfNull(stepRun);

        return new ProcessStepTransitionRequest
        {
            StepRunId = stepRun.Id,
            TargetStatus = ProcessStepRunStatus.Blocked,
            Reason = reason,
            DecidedBy = automationActor,
            SuppressAutomationDispatch = true
        };
    }

    public static ProcessStepTransitionRequest BuildCapabilityGapBlockTransitionRequest(
        ProcessStepRun stepRun,
        string reason,
        string automationActor)
    {
        ArgumentNullException.ThrowIfNull(stepRun);

        return new ProcessStepTransitionRequest
        {
            StepRunId = stepRun.Id,
            TargetStatus = ProcessStepRunStatus.Blocked,
            Reason = reason,
            DecidedBy = automationActor,
            SuppressAutomationDispatch = true
        };
    }

    public static ProcessStepTransitionRequest BuildTerminalMirrorTransitionRequest(
        ProcessStepRun stepRun,
        ProcessSubprocessRunStartResult subprocessRun,
        ProcessStepRunStatus terminalStatus,
        string automationActor)
    {
        ArgumentNullException.ThrowIfNull(stepRun);
        ArgumentNullException.ThrowIfNull(subprocessRun);

        return new ProcessStepTransitionRequest
        {
            StepRunId = stepRun.Id,
            TargetStatus = terminalStatus,
            Reason = BuildParentTransitionReason(subprocessRun),
            DecidedBy = automationActor,
            SuppressAutomationDispatch = terminalStatus != ProcessStepRunStatus.Completed
        };
    }

    public static ProcessStepRunStatus? ResolveParentStepStatus(ProcessRunStatus subprocessStatus)
    {
        return subprocessStatus switch
        {
            ProcessRunStatus.Completed => ProcessStepRunStatus.Completed,
            ProcessRunStatus.Blocked => ProcessStepRunStatus.Blocked,
            ProcessRunStatus.Cancelled or ProcessRunStatus.Failed => ProcessStepRunStatus.Failed,
            _ => null
        };
    }

    public static string BuildParentTransitionReason(ProcessSubprocessRunStartResult subprocessRun)
    {
        ArgumentNullException.ThrowIfNull(subprocessRun);

        return subprocessRun.Status switch
        {
            ProcessRunStatus.Completed => $"Subprocess run '{subprocessRun.RunName}' completed.",
            ProcessRunStatus.Blocked => $"Subprocess run '{subprocessRun.RunName}' is blocked.",
            ProcessRunStatus.Cancelled => $"Subprocess run '{subprocessRun.RunName}' was cancelled.",
            ProcessRunStatus.Failed => $"Subprocess run '{subprocessRun.RunName}' failed.",
            _ => $"Subprocess run '{subprocessRun.RunName}' is {subprocessRun.Status}."
        };
    }
}
