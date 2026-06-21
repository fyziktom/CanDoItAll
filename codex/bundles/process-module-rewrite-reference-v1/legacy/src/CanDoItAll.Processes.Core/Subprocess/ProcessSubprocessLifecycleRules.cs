using CanDoItAll.Processes.Contracts;

namespace CanDoItAll.Processes.Core.Subprocess;

public readonly record struct ProcessSubprocessRunFacts(
    Guid RunId,
    string RunName,
    ProcessRunStatus Status);

public readonly record struct ProcessSubprocessParentTransitionFacts(
    Guid StepRunId,
    Guid? StepRunConcurrencyToken,
    ProcessStepRunStatus TargetStatus,
    string Reason,
    string DecidedBy,
    bool SuppressAutomationDispatch);

public static class ProcessSubprocessLifecycleRules
{
    public static ProcessSubprocessParentTransitionFacts BuildStartTransitionFacts(
        Guid stepRunId,
        Guid concurrencyToken,
        string normalizedTrigger,
        string automationActor)
    {
        return new ProcessSubprocessParentTransitionFacts(
            stepRunId,
            concurrencyToken,
            ProcessStepRunStatus.InProgress,
            $"Started subprocess by the durable process automation dispatcher ({normalizedTrigger}).",
            automationActor,
            SuppressAutomationDispatch: true);
    }

    public static ProcessSubprocessParentTransitionFacts BuildBlockTransitionFacts(
        Guid stepRunId,
        string reason,
        string automationActor)
    {
        return new ProcessSubprocessParentTransitionFacts(
            stepRunId,
            null,
            ProcessStepRunStatus.Blocked,
            reason,
            automationActor,
            SuppressAutomationDispatch: true);
    }

    public static ProcessSubprocessParentTransitionFacts BuildTerminalMirrorTransitionFacts(
        Guid stepRunId,
        ProcessSubprocessRunFacts subprocessRun,
        ProcessStepRunStatus terminalStatus,
        string automationActor)
    {
        return new ProcessSubprocessParentTransitionFacts(
            stepRunId,
            null,
            terminalStatus,
            BuildParentTransitionReason(subprocessRun),
            automationActor,
            terminalStatus != ProcessStepRunStatus.Completed);
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

    public static string BuildParentTransitionReason(ProcessSubprocessRunFacts subprocessRun)
    {
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
