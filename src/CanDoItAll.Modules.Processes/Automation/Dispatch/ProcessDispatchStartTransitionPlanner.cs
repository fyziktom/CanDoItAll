using CanDoItAll.Processes.Core.Routing;

namespace CanDoItAll.Modules.Processes;

internal static class ProcessDispatchStartTransitionPlanner
{
    public static ProcessStepTransitionRequest BuildStartTransitionRequest(
        ProcessDispatchRouteSnapshot routeSnapshot,
        Guid stepRunConcurrencyToken,
        string automationActor)
    {
        return ProcessTransitionIntentAdapters.ToTransitionRequest(
            ProcessDispatchTransitionIntentRules.BuildStartTransitionIntent(
                routeSnapshot,
                stepRunConcurrencyToken,
                automationActor));
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
