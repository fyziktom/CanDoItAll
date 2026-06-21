namespace CanDoItAll.Modules.Processes;

internal static class ProcessAutomationExecutionRunSelection
{
    private const string HostRestartCancelledRunMarker = "host restarted before the run completed";

    public static bool HasBlockingAutomationExecutionRun(
        IReadOnlyList<ProcessAutomationExecutionRunRecord> executionRuns,
        DateTimeOffset now,
        string automationActor,
        TimeSpan staleExecutionRunTimeout)
    {
        return ResolveBlockingAutomationExecutionRunId(
            executionRuns,
            now,
            automationActor,
            staleExecutionRunTimeout).HasValue;
    }

    public static Guid? ResolveBlockingAutomationExecutionRunId(
        IReadOnlyList<ProcessAutomationExecutionRunRecord> executionRuns,
        DateTimeOffset now,
        string automationActor,
        TimeSpan staleExecutionRunTimeout)
    {
        ArgumentNullException.ThrowIfNull(executionRuns);

        return executionRuns
            .Where(executionRun => IsBlockingAutomationExecutionRun(
                executionRun,
                now,
                automationActor,
                staleExecutionRunTimeout))
            .OrderByDescending(ResolveLastProgressAtUtc)
            .ThenByDescending(executionRun => executionRun.CreatedAtUtc)
            .Select(executionRun => (Guid?)executionRun.Id)
            .FirstOrDefault();
    }

    public static Guid? ResolveBlockingAutomationExecutionRunId(
        DateTimeOffset? currentAttemptStartedAtUtc,
        IReadOnlyList<ProcessAutomationExecutionRunRecord> executionRuns,
        DateTimeOffset now,
        string automationActor,
        TimeSpan staleExecutionRunTimeout)
    {
        ArgumentNullException.ThrowIfNull(executionRuns);

        return executionRuns
            .Where(executionRun =>
                IsBlockingAutomationExecutionRun(
                    executionRun,
                    now,
                    automationActor,
                    staleExecutionRunTimeout) &&
                IsRecoverableExecutionRunForCurrentAttempt(executionRun, currentAttemptStartedAtUtc))
            .OrderByDescending(ResolveLastProgressAtUtc)
            .ThenByDescending(executionRun => executionRun.CreatedAtUtc)
            .Select(executionRun => (Guid?)executionRun.Id)
            .FirstOrDefault();
    }

    public static Guid? ResolveRecoverableAutomationExecutionRunId(
        ProcessStepRunStatus stepStatus,
        DateTimeOffset? currentAttemptStartedAtUtc,
        IReadOnlyList<ProcessAutomationExecutionRunRecord> executionRuns,
        string automationActor)
    {
        ArgumentNullException.ThrowIfNull(executionRuns);

        if (stepStatus != ProcessStepRunStatus.InProgress)
        {
            return null;
        }

        return executionRuns
            .Where(executionRun =>
                IsAutomationActorExecutionRun(executionRun, automationActor) &&
                executionRun.State is ProcessAutomationExecutionState.Completed or ProcessAutomationExecutionState.Failed &&
                !IsHostRestartCancelledRun(executionRun) &&
                IsRecoverableExecutionRunForCurrentAttempt(executionRun, currentAttemptStartedAtUtc))
            .OrderByDescending(executionRun => executionRun.CompletedAtUtc ?? executionRun.UpdatedAtUtc)
            .ThenByDescending(executionRun => executionRun.UpdatedAtUtc)
            .ThenByDescending(executionRun => executionRun.CreatedAtUtc)
            .Select(executionRun => (Guid?)executionRun.Id)
            .FirstOrDefault();
    }

    public static ProcessAutomationExecutionRunRecord? ResolveCompetingActiveAutomationExecutionRun(
        IReadOnlyList<ProcessAutomationExecutionRunRecord> executionRuns,
        Guid currentExecutionRunId,
        DateTimeOffset? currentAttemptStartedAtUtc,
        DateTimeOffset now,
        string automationActor,
        TimeSpan staleExecutionRunTimeout)
    {
        ArgumentNullException.ThrowIfNull(executionRuns);

        return executionRuns
            .Where(executionRun => executionRun.Id != currentExecutionRunId)
            .Where(executionRun =>
                IsBlockingAutomationExecutionRun(
                    executionRun,
                    now,
                    automationActor,
                    staleExecutionRunTimeout) &&
                IsRecoverableExecutionRunForCurrentAttempt(executionRun, currentAttemptStartedAtUtc))
            .OrderByDescending(ResolveLastProgressAtUtc)
            .ThenByDescending(executionRun => executionRun.CreatedAtUtc)
            .FirstOrDefault();
    }

    public static bool ShouldSkipAutomationCompletionTransition(
        ProcessStepRunStatus currentStatus,
        ProcessStepRunStatus requestedStatus)
    {
        if (currentStatus == requestedStatus)
        {
            return true;
        }

        return currentStatus is not ProcessStepRunStatus.InProgress and not ProcessStepRunStatus.WaitingApproval;
    }

    public static bool IsConcurrentAutomationSessionBusyException(
        Exception exception,
        IReadOnlySet<string> busyMessages)
    {
        ArgumentNullException.ThrowIfNull(exception);
        ArgumentNullException.ThrowIfNull(busyMessages);

        return exception is InvalidOperationException &&
               busyMessages.Contains(exception.Message.Trim());
    }

    public static bool ShouldSkipFreshAutomationDispatch(
        ProcessStepRunStatus currentStatus,
        Guid? recoverableExecutionRunId,
        DateTimeOffset? currentAttemptStartedAtUtc,
        DateTimeOffset now,
        string trigger,
        TimeSpan freshInProgressRecoveryGracePeriod)
    {
        if (currentStatus != ProcessStepRunStatus.InProgress)
        {
            return false;
        }

        if (!IsRecoveryTrigger(trigger))
        {
            return false;
        }

        if (recoverableExecutionRunId.HasValue)
        {
            return false;
        }

        if (!currentAttemptStartedAtUtc.HasValue)
        {
            return false;
        }

        return now - currentAttemptStartedAtUtc.Value < freshInProgressRecoveryGracePeriod;
    }

    public static bool IsBlockingAutomationExecutionRun(
        ProcessAutomationExecutionRunRecord executionRun,
        DateTimeOffset now,
        string automationActor,
        TimeSpan staleExecutionRunTimeout)
    {
        ArgumentNullException.ThrowIfNull(executionRun);

        return IsAutomationActorExecutionRun(executionRun, automationActor) &&
               executionRun.State is not ProcessAutomationExecutionState.Completed and not ProcessAutomationExecutionState.Failed &&
               !IsStaleAutomationExecutionRun(executionRun, now, staleExecutionRunTimeout);
    }

    public static bool IsStaleAutomationExecutionRun(
        ProcessAutomationExecutionRunRecord executionRun,
        DateTimeOffset now,
        TimeSpan staleExecutionRunTimeout)
    {
        ArgumentNullException.ThrowIfNull(executionRun);

        if (executionRun.PendingApprovals.Count > 0)
        {
            return false;
        }

        return now - ResolveLastProgressAtUtc(executionRun) >= staleExecutionRunTimeout;
    }

    public static bool IsRecoverableExecutionRunForCurrentAttempt(
        ProcessAutomationExecutionRunRecord executionRun,
        DateTimeOffset? currentAttemptStartedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(executionRun);

        if (!currentAttemptStartedAtUtc.HasValue)
        {
            return true;
        }

        var executionAttemptStartedAtUtc = executionRun.StartedAtUtc ?? executionRun.CreatedAtUtc;
        return executionAttemptStartedAtUtc >= currentAttemptStartedAtUtc.Value;
    }

    public static bool IsRecoveryTrigger(string trigger)
    {
        return string.Equals(
            trigger?.Trim(),
            "runtime-recovery-scan",
            StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsAutomationActorExecutionRun(
        ProcessAutomationExecutionRunRecord executionRun,
        string automationActor)
    {
        return string.Equals(executionRun.RequestedBy, automationActor, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsHostRestartCancelledRun(ProcessAutomationExecutionRunRecord executionRun)
    {
        return executionRun.State == ProcessAutomationExecutionState.Failed &&
               executionRun.Outcome == ProcessAutomationRunOutcome.Cancelled &&
               executionRun.ResultSummary.Contains(HostRestartCancelledRunMarker, StringComparison.OrdinalIgnoreCase);
    }

    private static DateTimeOffset ResolveLastProgressAtUtc(ProcessAutomationExecutionRunRecord executionRun)
    {
        return executionRun.UpdatedAtUtc == default
            ? executionRun.CreatedAtUtc
            : executionRun.UpdatedAtUtc;
    }
}
