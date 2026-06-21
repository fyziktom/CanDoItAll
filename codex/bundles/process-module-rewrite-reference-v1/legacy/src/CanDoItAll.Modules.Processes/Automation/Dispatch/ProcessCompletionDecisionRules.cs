namespace CanDoItAll.Modules.Processes;

internal static class ProcessCompletionDecisionRules
{
    internal static bool TryResolveRunStateDecision(
        ProcessCompletionDecisionInput input,
        out ProcessCompletionDecision decision)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (input.RunState != ProcessAutomationExecutionState.Completed)
        {
            decision = new ProcessCompletionDecision(
                input.PendingApprovalCount > 0
                    ? ProcessStepRunStatus.WaitingApproval
                    : input.RunState == ProcessAutomationExecutionState.Failed
                        ? ProcessStepRunStatus.Failed
                        : input.CurrentStepStatus == ProcessStepRunStatus.WaitingApproval
                            ? ProcessStepRunStatus.WaitingApproval
                            : ProcessStepRunStatus.InProgress,
                "Execution run is not terminally completed.");
            return true;
        }

        if (input.PendingApprovalCount > 0)
        {
            decision = new ProcessCompletionDecision(
                ProcessStepRunStatus.WaitingApproval,
                "Execution run has pending approvals.");
            return true;
        }

        if (input.RunOutcome != ProcessAutomationRunOutcome.Succeeded)
        {
            decision = new ProcessCompletionDecision(
                ProcessStepRunStatus.Failed,
                "Execution run completed without a successful outcome.");
            return true;
        }

        decision = default;
        return false;
    }
}

internal sealed record ProcessCompletionDecisionInput(
    ProcessAutomationExecutionState RunState,
    ProcessAutomationRunOutcome? RunOutcome,
    int PendingApprovalCount,
    ProcessStepRunStatus CurrentStepStatus);

internal readonly record struct ProcessCompletionDecision(
    ProcessStepRunStatus Status,
    string Reason);
