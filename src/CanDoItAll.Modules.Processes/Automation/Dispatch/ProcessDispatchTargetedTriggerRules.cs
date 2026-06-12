namespace CanDoItAll.Modules.Processes;

internal static class ProcessDispatchTargetedTriggerRules
{
    public static bool IsSubprocessStatusNotification(string trigger)
    {
        return !string.IsNullOrWhiteSpace(trigger) &&
            trigger.Trim().StartsWith("subprocess-run:", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsSubprocessStatusNotificationDispatchable(
        ProcessRunStatus runStatus,
        string trigger)
    {
        return (runStatus is ProcessRunStatus.Active or ProcessRunStatus.Blocked or ProcessRunStatus.Failed) &&
            IsSubprocessStatusNotification(trigger);
    }

    public static bool IsSubprocessStatusNotificationDispatchable(
        ProcessRunStatus runStatus,
        ProcessStepRunStatus stepStatus,
        ProcessStepKind stepKind,
        string trigger)
    {
        return IsSubprocessStatusNotificationDispatchable(runStatus, trigger) &&
            stepStatus is ProcessStepRunStatus.Blocked or ProcessStepRunStatus.Failed &&
            stepKind == ProcessStepKind.Subprocess &&
            IsSubprocessStatusNotification(trigger);
    }
}
