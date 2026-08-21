using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;

namespace CanDoItAll.AgentFramework.Core;

internal enum InMemoryWorkflowBoundaryCancellationOutcome
{
    Allowed,
    ActiveResume,
    AlreadyTerminal
}

internal sealed record InMemoryWorkflowBoundaryCancellationResult(
    InMemoryWorkflowBoundaryCancellationOutcome Outcome,
    WorkflowExternalResponseOperationRecord? Operation);

internal sealed record InMemoryWorkflowBoundaryCancellationPlan(
    WorkflowExternalResponseOperationRecord? Operation);

internal static class InMemoryWorkflowExternalResponseCancellation
{
    public static InMemoryWorkflowBoundaryCancellationResult Prepare(
        WorkflowExternalResponseOperationRecord? current,
        DateTimeOffset cancelledAtUtc,
        string safeReason,
        out InMemoryWorkflowBoundaryCancellationPlan? plan)
    {
        plan = null;
        if (current is null)
        {
            plan = new InMemoryWorkflowBoundaryCancellationPlan(Operation: null);
            return new InMemoryWorkflowBoundaryCancellationResult(
                InMemoryWorkflowBoundaryCancellationOutcome.Allowed,
                Operation: null);
        }

        if (current.State == WorkflowExternalResponseOperationState.FailedTerminal)
        {
            plan = new InMemoryWorkflowBoundaryCancellationPlan(Operation: null);
            return new InMemoryWorkflowBoundaryCancellationResult(
                InMemoryWorkflowBoundaryCancellationOutcome.Allowed,
                current);
        }

        if (WorkflowExternalResponseOperationTransitionRules.IsTerminal(current.State))
        {
            return new InMemoryWorkflowBoundaryCancellationResult(
                InMemoryWorkflowBoundaryCancellationOutcome.AlreadyTerminal,
                current);
        }

        if (current.State is (WorkflowExternalResponseOperationState.Claimed or WorkflowExternalResponseOperationState.Resuming) &&
            current.Lease is { } lease &&
            !lease.IsExpired(cancelledAtUtc))
        {
            return new InMemoryWorkflowBoundaryCancellationResult(
                InMemoryWorkflowBoundaryCancellationOutcome.ActiveResume,
                current);
        }

        var final = new WorkflowExternalResponseOperationFinalResult(
            WorkflowExternalResponseOperationState.Cancelled,
            WorkflowExternalResponseOperationOutcomeCode.Cancelled,
            safeReason,
            WorkflowRunState.Cancelled);
        var cancelled = current with
        {
            State = WorkflowExternalResponseOperationState.Cancelled,
            Lease = null,
            CompletedAtUtc = cancelledAtUtc,
            OutcomeCode = WorkflowExternalResponseOperationOutcomeCode.Cancelled,
            SafeMessage = safeReason,
            FinalResult = final,
            ConcurrencyVersion = current.ConcurrencyVersion.Next()
        };
        plan = new InMemoryWorkflowBoundaryCancellationPlan(cancelled);
        return new InMemoryWorkflowBoundaryCancellationResult(
            InMemoryWorkflowBoundaryCancellationOutcome.Allowed,
            cancelled);
    }
}
