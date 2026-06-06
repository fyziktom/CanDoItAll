namespace CanDoItAll.Modules.Processes;

internal readonly record struct ProcessDispatchTriggerFacts(
    string RawTrigger,
    Guid? TriggerStepRunId,
    string NormalizedTrigger)
{
    public static ProcessDispatchTriggerFacts Create(string trigger, Guid? triggerStepRunId)
    {
        return new ProcessDispatchTriggerFacts(
            trigger,
            triggerStepRunId,
            Normalize(trigger, triggerStepRunId));
    }

    private static string Normalize(string trigger, Guid? triggerStepRunId)
    {
        if (!string.IsNullOrWhiteSpace(trigger))
        {
            return trigger.Trim();
        }

        return triggerStepRunId.HasValue
            ? $"step:{triggerStepRunId.Value:D}"
            : "process-runtime";
    }
}

internal readonly record struct ProcessDispatchRouteSnapshot(
    Guid ProcessRunId,
    Guid StepRunId,
    ProcessRunStatus RunStatus,
    ProcessStepRunStatus StepStatus,
    ProcessStepKind StepKind,
    Guid TechnicalAgentId,
    Guid? RecoveryExecutionRunId,
    DateTimeOffset? CurrentAttemptStartedAtUtc,
    ProcessDispatchTriggerFacts Trigger)
{
    public bool UsesAgentAutomation => TechnicalAgentId != Guid.Empty && !IsSubprocess;

    public bool IsSubprocess => StepKind == ProcessStepKind.Subprocess;

    public bool HasRecoverableExecutionRun => RecoveryExecutionRunId.HasValue;

    public bool RequiresStartTransition => StepStatus != ProcessStepRunStatus.InProgress;

    public bool IsRunEligibleForDispatchCandidate => ProcessDispatchRouteEligibility.IsRunEligibleForDispatchCandidate(RunStatus);

    public bool IsStepStatusDispatchableForRun => ProcessDispatchRouteEligibility.IsStepStatusDispatchableForRun(RunStatus, StepStatus);

    public static ProcessDispatchRouteSnapshot Create(
        ProcessRouteCandidate candidate,
        string trigger,
        Guid? triggerStepRunId)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        return Create(
            candidate.Run.Id,
            candidate.StepRun.Id,
            candidate.Run.Status,
            candidate.StepRun.Status,
            candidate.StepRun.StepKind,
            candidate.TechnicalAgentId,
            candidate.RecoveryExecutionRunId,
            candidate.StepRun.StartedAtUtc,
            trigger,
            triggerStepRunId);
    }

    public static ProcessDispatchRouteSnapshot Create(
        Guid processRunId,
        Guid stepRunId,
        ProcessRunStatus runStatus,
        ProcessStepRunStatus stepStatus,
        ProcessStepKind stepKind,
        Guid technicalAgentId,
        Guid? recoveryExecutionRunId,
        DateTimeOffset? currentAttemptStartedAtUtc,
        string trigger,
        Guid? triggerStepRunId)
    {
        return new ProcessDispatchRouteSnapshot(
            processRunId,
            stepRunId,
            runStatus,
            stepStatus,
            stepKind,
            technicalAgentId,
            recoveryExecutionRunId,
            currentAttemptStartedAtUtc,
            ProcessDispatchTriggerFacts.Create(trigger, triggerStepRunId));
    }
}

internal static class ProcessDispatchRouteEligibility
{
    public static bool IsRunClosedToAutomation(
        ProcessRunStatus? runStatus,
        ProcessStepRunStatus? stepStatus)
    {
        return runStatus is null or ProcessRunStatus.Completed or ProcessRunStatus.Cancelled ||
            runStatus == ProcessRunStatus.Failed && stepStatus != ProcessStepRunStatus.InProgress;
    }

    public static bool IsRunEligibleForDispatchCandidate(ProcessRunStatus? runStatus)
    {
        return runStatus.HasValue &&
            runStatus.Value is not ProcessRunStatus.Completed and not ProcessRunStatus.Cancelled;
    }

    public static bool IsStepStatusDispatchableForRun(
        ProcessRunStatus runStatus,
        ProcessStepRunStatus stepStatus)
    {
        return runStatus == ProcessRunStatus.Failed
            ? stepStatus == ProcessStepRunStatus.InProgress
            : stepStatus is ProcessStepRunStatus.Ready or ProcessStepRunStatus.WaitingApproval or ProcessStepRunStatus.InProgress;
    }
}
