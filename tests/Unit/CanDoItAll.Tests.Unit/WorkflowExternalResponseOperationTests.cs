using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class WorkflowExternalResponseOperationTests
{
    private static readonly HashSet<(WorkflowExternalResponseOperationState Current, WorkflowExternalResponseOperationState Next)> LegalTransitions =
    [
        (WorkflowExternalResponseOperationState.Accepted, WorkflowExternalResponseOperationState.Claimed),
        (WorkflowExternalResponseOperationState.Accepted, WorkflowExternalResponseOperationState.Cancelled),
        (WorkflowExternalResponseOperationState.Claimed, WorkflowExternalResponseOperationState.Resuming),
        (WorkflowExternalResponseOperationState.Claimed, WorkflowExternalResponseOperationState.FailedTerminal),
        (WorkflowExternalResponseOperationState.Claimed, WorkflowExternalResponseOperationState.Cancelled),
        (WorkflowExternalResponseOperationState.Resuming, WorkflowExternalResponseOperationState.WaitingAgain),
        (WorkflowExternalResponseOperationState.Resuming, WorkflowExternalResponseOperationState.Completed),
        (WorkflowExternalResponseOperationState.Resuming, WorkflowExternalResponseOperationState.Denied),
        (WorkflowExternalResponseOperationState.Resuming, WorkflowExternalResponseOperationState.FailedRetryable),
        (WorkflowExternalResponseOperationState.Resuming, WorkflowExternalResponseOperationState.FailedTerminal),
        (WorkflowExternalResponseOperationState.Resuming, WorkflowExternalResponseOperationState.Cancelled),
        (WorkflowExternalResponseOperationState.FailedRetryable, WorkflowExternalResponseOperationState.Claimed),
        (WorkflowExternalResponseOperationState.FailedRetryable, WorkflowExternalResponseOperationState.FailedTerminal),
        (WorkflowExternalResponseOperationState.FailedRetryable, WorkflowExternalResponseOperationState.Cancelled)
    ];

    public static TheoryData<WorkflowExternalResponseOperationState, WorkflowExternalResponseOperationState> EveryTransition()
    {
        var data = new TheoryData<WorkflowExternalResponseOperationState, WorkflowExternalResponseOperationState>();
        foreach (var current in Enum.GetValues<WorkflowExternalResponseOperationState>())
        {
            foreach (var next in Enum.GetValues<WorkflowExternalResponseOperationState>())
            {
                data.Add(current, next);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(EveryTransition))]
    public void TransitionRules_EveryStatePair_MatchesDocumentedStateMachine(
        WorkflowExternalResponseOperationState current,
        WorkflowExternalResponseOperationState next)
    {
        var expected = LegalTransitions.Contains((current, next));

        Assert.Equal(expected, WorkflowExternalResponseOperationTransitionRules.CanTransition(current, next));
        if (expected)
        {
            WorkflowExternalResponseOperationTransitionRules.ThrowIfInvalidTransition(current, next);
            return;
        }

        Assert.Throws<InvalidOperationException>(
            () => WorkflowExternalResponseOperationTransitionRules.ThrowIfInvalidTransition(current, next));
    }

    [Theory]
    [InlineData(WorkflowExternalResponseOperationState.WaitingAgain)]
    [InlineData(WorkflowExternalResponseOperationState.Completed)]
    [InlineData(WorkflowExternalResponseOperationState.Denied)]
    [InlineData(WorkflowExternalResponseOperationState.FailedTerminal)]
    [InlineData(WorkflowExternalResponseOperationState.Cancelled)]
    public void TerminalState_CannotTransitionToAnyState(WorkflowExternalResponseOperationState terminal)
    {
        Assert.True(WorkflowExternalResponseOperationTransitionRules.IsTerminal(terminal));
        Assert.All(
            Enum.GetValues<WorkflowExternalResponseOperationState>(),
            next => Assert.False(WorkflowExternalResponseOperationTransitionRules.CanTransition(terminal, next)));
    }

    [Theory]
    [InlineData(WorkflowExternalResponseOperationState.Accepted)]
    [InlineData(WorkflowExternalResponseOperationState.Claimed)]
    [InlineData(WorkflowExternalResponseOperationState.Resuming)]
    [InlineData(WorkflowExternalResponseOperationState.FailedRetryable)]
    public void NonTerminalState_IsNotReportedAsTerminal(WorkflowExternalResponseOperationState state)
        => Assert.False(WorkflowExternalResponseOperationTransitionRules.IsTerminal(state));

    [Fact]
    public void ExpiredLeaseRecovery_Claimed_ReplacesOnlyLease()
    {
        var recovery = WorkflowExternalResponseOperationRecoveryRules.CreateExpiredLeaseRecovery(
            WorkflowExternalResponseOperationState.Claimed);

        Assert.Equal(WorkflowExternalResponseOperationState.Claimed, recovery.PriorState);
        Assert.Empty(recovery.TransitionPath);
    }

    [Fact]
    public void ExpiredLeaseRecovery_Resuming_UsesLegalRetryablePath()
    {
        var recovery = WorkflowExternalResponseOperationRecoveryRules.CreateExpiredLeaseRecovery(
            WorkflowExternalResponseOperationState.Resuming);

        Assert.Equal(
            [
                WorkflowExternalResponseOperationState.FailedRetryable,
                WorkflowExternalResponseOperationState.Claimed
            ],
            recovery.TransitionPath);
        Assert.True(
            WorkflowExternalResponseOperationTransitionRules.CanTransition(
                recovery.PriorState,
                recovery.TransitionPath[0]));
        Assert.True(
            WorkflowExternalResponseOperationTransitionRules.CanTransition(
                recovery.TransitionPath[0],
                recovery.TransitionPath[1]));
    }
}
