namespace CanDoItAll.Modules.Processes;

internal static class ProcessDispatchDatabaseRequirementBlocker
{
    public static ProcessStepRunStatus ResolveTargetStatus(ProcessStepRunStatus currentStatus)
    {
        return currentStatus switch
        {
            ProcessStepRunStatus.Ready or ProcessStepRunStatus.WaitingApproval or ProcessStepRunStatus.Blocked => ProcessStepRunStatus.Blocked,
            ProcessStepRunStatus.InProgress or ProcessStepRunStatus.Failed => ProcessStepRunStatus.Failed,
            _ => currentStatus
        };
    }

    public static bool IsUnsupportedNoOpTarget(
        ProcessStepRunStatus currentStatus,
        ProcessStepRunStatus targetStatus)
    {
        return targetStatus == currentStatus &&
               targetStatus is not ProcessStepRunStatus.Blocked and not ProcessStepRunStatus.Failed;
    }

    public static ProcessStepTransitionRequest BuildTransitionRequest(
        Guid stepRunId,
        Guid concurrencyToken,
        ProcessStepRunStatus targetStatus,
        string reason,
        string automationActor)
    {
        return new ProcessStepTransitionRequest
        {
            StepRunId = stepRunId,
            StepRunConcurrencyToken = concurrencyToken,
            TargetStatus = targetStatus,
            Reason = reason,
            DecidedBy = automationActor,
            SuppressAutomationDispatch = true
        };
    }
}
