using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public static class WorkflowExternalResponseOperationTransitionRules
{
    public static bool CanTransition(
        WorkflowExternalResponseOperationState current,
        WorkflowExternalResponseOperationState next)
        => (current, next) switch
        {
            (WorkflowExternalResponseOperationState.Accepted, WorkflowExternalResponseOperationState.Claimed) => true,
            (WorkflowExternalResponseOperationState.Accepted, WorkflowExternalResponseOperationState.Cancelled) => true,
            (WorkflowExternalResponseOperationState.Claimed, WorkflowExternalResponseOperationState.Resuming) => true,
            (WorkflowExternalResponseOperationState.Claimed, WorkflowExternalResponseOperationState.FailedTerminal) => true,
            (WorkflowExternalResponseOperationState.Claimed, WorkflowExternalResponseOperationState.Cancelled) => true,
            (WorkflowExternalResponseOperationState.Resuming, WorkflowExternalResponseOperationState.WaitingAgain) => true,
            (WorkflowExternalResponseOperationState.Resuming, WorkflowExternalResponseOperationState.Completed) => true,
            (WorkflowExternalResponseOperationState.Resuming, WorkflowExternalResponseOperationState.Denied) => true,
            (WorkflowExternalResponseOperationState.Resuming, WorkflowExternalResponseOperationState.FailedRetryable) => true,
            (WorkflowExternalResponseOperationState.Resuming, WorkflowExternalResponseOperationState.FailedTerminal) => true,
            (WorkflowExternalResponseOperationState.Resuming, WorkflowExternalResponseOperationState.Cancelled) => true,
            (WorkflowExternalResponseOperationState.FailedRetryable, WorkflowExternalResponseOperationState.Claimed) => true,
            (WorkflowExternalResponseOperationState.FailedRetryable, WorkflowExternalResponseOperationState.FailedTerminal) => true,
            (WorkflowExternalResponseOperationState.FailedRetryable, WorkflowExternalResponseOperationState.Cancelled) => true,
            _ => false
        };

    public static bool IsTerminal(WorkflowExternalResponseOperationState state)
        => state is WorkflowExternalResponseOperationState.WaitingAgain or
            WorkflowExternalResponseOperationState.Completed or
            WorkflowExternalResponseOperationState.Denied or
            WorkflowExternalResponseOperationState.FailedTerminal or
            WorkflowExternalResponseOperationState.Cancelled;

    public static void ThrowIfInvalidTransition(
        WorkflowExternalResponseOperationState current,
        WorkflowExternalResponseOperationState next)
    {
        if (!CanTransition(current, next))
        {
            throw new InvalidOperationException(
                $"Workflow external response operation cannot transition from '{current}' to '{next}'.");
        }
    }
}

public static class WorkflowExternalResponseOperationRecoveryRules
{
    public static bool CanTakeOverExpiredLease(WorkflowExternalResponseOperationState state)
        => state is WorkflowExternalResponseOperationState.Claimed or
            WorkflowExternalResponseOperationState.Resuming;

    public static WorkflowExternalResponseExpiredLeaseRecovery CreateExpiredLeaseRecovery(
        WorkflowExternalResponseOperationState state)
        => state switch
        {
            WorkflowExternalResponseOperationState.Claimed => new(
                state,
                []),
            WorkflowExternalResponseOperationState.Resuming => new(
                state,
                [
                    WorkflowExternalResponseOperationState.FailedRetryable,
                    WorkflowExternalResponseOperationState.Claimed
                ]),
            _ => throw new InvalidOperationException(
                $"Workflow external response operation in state '{state}' has no expired-lease recovery path.")
        };
}
