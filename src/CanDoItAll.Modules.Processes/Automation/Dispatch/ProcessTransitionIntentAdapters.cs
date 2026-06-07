namespace CanDoItAll.Modules.Processes;

internal static class ProcessTransitionIntentAdapters
{
    public static ProcessStepTransitionRequest ToTransitionRequest(
        global::CanDoItAll.Processes.Core.Routing.ProcessDispatchStepTransitionIntent transitionIntent)
    {
        return new ProcessStepTransitionRequest
        {
            StepRunId = transitionIntent.StepRunId,
            StepRunConcurrencyToken = transitionIntent.StepRunConcurrencyToken,
            TargetStatus = transitionIntent.TargetStatus,
            Reason = transitionIntent.Reason,
            DecidedBy = transitionIntent.DecidedBy,
            SuppressAutomationDispatch = transitionIntent.SuppressAutomationDispatch
        };
    }

    public static ProcessStepTransitionRequest ToTransitionRequest(
        global::CanDoItAll.Processes.Core.Subprocess.ProcessSubprocessParentTransitionFacts transitionFacts)
    {
        return new ProcessStepTransitionRequest
        {
            StepRunId = transitionFacts.StepRunId,
            StepRunConcurrencyToken = transitionFacts.StepRunConcurrencyToken,
            TargetStatus = transitionFacts.TargetStatus,
            Reason = transitionFacts.Reason,
            DecidedBy = transitionFacts.DecidedBy,
            SuppressAutomationDispatch = transitionFacts.SuppressAutomationDispatch
        };
    }
}
