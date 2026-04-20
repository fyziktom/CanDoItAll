namespace CanDoItAll.Modules.Processes;

internal static class ProcessStepRunTransitions
{
    public static bool IsAllowed(ProcessStepRunStatus currentStatus, ProcessStepRunStatus targetStatus)
    {
        if (currentStatus == targetStatus)
        {
            return true;
        }

        return currentStatus switch
        {
            ProcessStepRunStatus.Pending => targetStatus == ProcessStepRunStatus.Ready,
            ProcessStepRunStatus.Ready => targetStatus is ProcessStepRunStatus.InProgress or ProcessStepRunStatus.Blocked or ProcessStepRunStatus.Refused or ProcessStepRunStatus.Skipped or ProcessStepRunStatus.WaitingApproval,
            ProcessStepRunStatus.WaitingApproval => targetStatus is ProcessStepRunStatus.InProgress or ProcessStepRunStatus.Completed or ProcessStepRunStatus.Blocked or ProcessStepRunStatus.Refused,
            ProcessStepRunStatus.InProgress => targetStatus is ProcessStepRunStatus.Completed or ProcessStepRunStatus.Blocked or ProcessStepRunStatus.Refused or ProcessStepRunStatus.Failed,
            ProcessStepRunStatus.Blocked => targetStatus is ProcessStepRunStatus.Ready or ProcessStepRunStatus.InProgress or ProcessStepRunStatus.Refused or ProcessStepRunStatus.Failed,
            ProcessStepRunStatus.Failed => targetStatus == ProcessStepRunStatus.InProgress,
            _ => false
        };
    }
}
