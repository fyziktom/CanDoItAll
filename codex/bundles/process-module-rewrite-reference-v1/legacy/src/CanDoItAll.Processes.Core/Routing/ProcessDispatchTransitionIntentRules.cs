using CanDoItAll.Processes.Contracts;

namespace CanDoItAll.Processes.Core.Routing;

public readonly record struct ProcessDispatchStepTransitionIntent(
    Guid StepRunId,
    Guid? StepRunConcurrencyToken,
    ProcessStepRunStatus TargetStatus,
    string Reason,
    string DecidedBy,
    bool SuppressAutomationDispatch);

public static class ProcessDispatchTransitionIntentRules
{
    public static ProcessDispatchStepTransitionIntent BuildStartTransitionIntent(
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

        return new ProcessDispatchStepTransitionIntent(
            routeSnapshot.StepRunId,
            stepRunConcurrencyToken,
            ProcessStepRunStatus.InProgress,
            $"Started by the durable process automation dispatcher ({routeSnapshot.Trigger.NormalizedTrigger}).",
            automationActor,
            SuppressAutomationDispatch: true);
    }
}
