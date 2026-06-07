namespace CanDoItAll.Modules.Processes;

internal static class ProcessSubprocessLifecycleRules
{
    public static ProcessStepTransitionRequest BuildStartTransitionRequest(
        ProcessRouteStepSnapshot stepRun,
        string normalizedTrigger,
        string automationActor)
    {
        ArgumentNullException.ThrowIfNull(stepRun);

        return BuildStartTransitionRequest(
            stepRun.Id,
            stepRun.ConcurrencyToken,
            normalizedTrigger,
            automationActor);
    }

    public static ProcessStepTransitionRequest BuildStartTransitionRequest(
        ProcessStepRun stepRun,
        string normalizedTrigger,
        string automationActor)
    {
        ArgumentNullException.ThrowIfNull(stepRun);

        return BuildStartTransitionRequest(
            stepRun.Id,
            stepRun.ConcurrencyToken,
            normalizedTrigger,
            automationActor);
    }

    private static ProcessStepTransitionRequest BuildStartTransitionRequest(
        Guid stepRunId,
        Guid concurrencyToken,
        string normalizedTrigger,
        string automationActor)
    {
        return ProcessTransitionIntentAdapters.ToTransitionRequest(
            global::CanDoItAll.Processes.Core.Subprocess.ProcessSubprocessLifecycleRules.BuildStartTransitionFacts(
                stepRunId,
                concurrencyToken,
                normalizedTrigger,
                automationActor));
    }

    public static ProcessStepTransitionRequest BuildEnsureFailureBlockTransitionRequest(
        ProcessRouteStepSnapshot stepRun,
        string reason,
        string automationActor)
    {
        ArgumentNullException.ThrowIfNull(stepRun);

        return BuildBlockTransitionRequest(stepRun.Id, reason, automationActor);
    }

    public static ProcessStepTransitionRequest BuildEnsureFailureBlockTransitionRequest(
        ProcessStepRun stepRun,
        string reason,
        string automationActor)
    {
        ArgumentNullException.ThrowIfNull(stepRun);

        return BuildBlockTransitionRequest(stepRun.Id, reason, automationActor);
    }

    public static ProcessStepTransitionRequest BuildCapabilityGapBlockTransitionRequest(
        ProcessRouteStepSnapshot stepRun,
        string reason,
        string automationActor)
    {
        ArgumentNullException.ThrowIfNull(stepRun);

        return BuildBlockTransitionRequest(stepRun.Id, reason, automationActor);
    }

    public static ProcessStepTransitionRequest BuildCapabilityGapBlockTransitionRequest(
        ProcessStepRun stepRun,
        string reason,
        string automationActor)
    {
        ArgumentNullException.ThrowIfNull(stepRun);

        return BuildBlockTransitionRequest(stepRun.Id, reason, automationActor);
    }

    private static ProcessStepTransitionRequest BuildBlockTransitionRequest(
        Guid stepRunId,
        string reason,
        string automationActor)
    {
        return ProcessTransitionIntentAdapters.ToTransitionRequest(
            global::CanDoItAll.Processes.Core.Subprocess.ProcessSubprocessLifecycleRules.BuildBlockTransitionFacts(
                stepRunId,
                reason,
                automationActor));
    }

    public static ProcessStepTransitionRequest BuildTerminalMirrorTransitionRequest(
        ProcessRouteStepSnapshot stepRun,
        ProcessSubprocessRunStartResult subprocessRun,
        ProcessStepRunStatus terminalStatus,
        string automationActor)
    {
        ArgumentNullException.ThrowIfNull(stepRun);

        return BuildTerminalMirrorTransitionRequest(
            stepRun.Id,
            subprocessRun,
            terminalStatus,
            automationActor);
    }

    public static ProcessStepTransitionRequest BuildTerminalMirrorTransitionRequest(
        ProcessStepRun stepRun,
        ProcessSubprocessRunStartResult subprocessRun,
        ProcessStepRunStatus terminalStatus,
        string automationActor)
    {
        ArgumentNullException.ThrowIfNull(stepRun);

        return BuildTerminalMirrorTransitionRequest(
            stepRun.Id,
            subprocessRun,
            terminalStatus,
            automationActor);
    }

    private static ProcessStepTransitionRequest BuildTerminalMirrorTransitionRequest(
        Guid stepRunId,
        ProcessSubprocessRunStartResult subprocessRun,
        ProcessStepRunStatus terminalStatus,
        string automationActor)
    {
        ArgumentNullException.ThrowIfNull(subprocessRun);

        return ProcessTransitionIntentAdapters.ToTransitionRequest(
            global::CanDoItAll.Processes.Core.Subprocess.ProcessSubprocessLifecycleRules.BuildTerminalMirrorTransitionFacts(
                stepRunId,
                new global::CanDoItAll.Processes.Core.Subprocess.ProcessSubprocessRunFacts(
                    subprocessRun.RunId,
                    subprocessRun.RunName,
                    subprocessRun.Status),
                terminalStatus,
                automationActor));
    }

    public static ProcessStepRunStatus? ResolveParentStepStatus(ProcessRunStatus subprocessStatus)
    {
        return global::CanDoItAll.Processes.Core.Subprocess.ProcessSubprocessLifecycleRules.ResolveParentStepStatus(subprocessStatus);
    }

    public static string BuildParentTransitionReason(ProcessSubprocessRunStartResult subprocessRun)
    {
        ArgumentNullException.ThrowIfNull(subprocessRun);

        return global::CanDoItAll.Processes.Core.Subprocess.ProcessSubprocessLifecycleRules.BuildParentTransitionReason(
            new global::CanDoItAll.Processes.Core.Subprocess.ProcessSubprocessRunFacts(
                subprocessRun.RunId,
                subprocessRun.RunName,
                subprocessRun.Status));
    }
}
