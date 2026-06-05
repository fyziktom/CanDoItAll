namespace CanDoItAll.Modules.Processes;

internal static class ProcessDispatchStartTransitionPlanner
{
    public static ProcessStepTransitionRequest BuildStartTransitionRequest(
        ProcessDispatchRouteSnapshot routeSnapshot,
        Guid stepRunConcurrencyToken,
        string automationActor)
    {
        if (routeSnapshot.StepRunId == Guid.Empty)
        {
            throw new ArgumentException("Step run id must be set.", nameof(routeSnapshot));
        }

        if (stepRunConcurrencyToken == Guid.Empty)
        {
            throw new ArgumentException("Step run concurrency token must be set.", nameof(stepRunConcurrencyToken));
        }

        if (string.IsNullOrWhiteSpace(automationActor))
        {
            throw new ArgumentException("Automation actor must be set.", nameof(automationActor));
        }

        return new ProcessStepTransitionRequest
        {
            StepRunId = routeSnapshot.StepRunId,
            StepRunConcurrencyToken = stepRunConcurrencyToken,
            TargetStatus = ProcessStepRunStatus.InProgress,
            Reason = $"Started by the durable process automation dispatcher ({routeSnapshot.Trigger.NormalizedTrigger}).",
            DecidedBy = automationActor,
            SuppressAutomationDispatch = true
        };
    }

    public static bool ShouldSkipFreshAutomationDispatch(
        ProcessDispatchRouteSnapshot routeSnapshot,
        DateTimeOffset now,
        TimeSpan freshInProgressRecoveryGracePeriod)
    {
        return ProcessAutomationExecutionRunSelection.ShouldSkipFreshAutomationDispatch(
            routeSnapshot.StepStatus,
            routeSnapshot.RecoveryExecutionRunId,
            routeSnapshot.CurrentAttemptStartedAtUtc,
            now,
            routeSnapshot.Trigger.RawTrigger,
            freshInProgressRecoveryGracePeriod);
    }
}
