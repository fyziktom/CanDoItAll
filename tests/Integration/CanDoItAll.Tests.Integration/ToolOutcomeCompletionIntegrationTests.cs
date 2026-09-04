using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Tests.Integration.AgentFramework;

public sealed class ToolOutcomeCompletionIntegrationTests
{
    [Fact]
    public void Failure_followed_by_valid_assistant_prose_finishes_failed()
    {
        var assessment = AgentToolCompletionAssessment.Create(
            [CreateMutationTrace(
                sequence: 1,
                AgentToolInvocationOutcome.Failed,
                AgentToolEffectState.NotCommitted,
                correlationKey: "operation-a",
                failureMessage: "Required argument '$.request.parentNodeKey' is missing.")],
            pendingApprovalCount: 0,
            portableOutputValid: true);

        Assert.Equal(ExecutionState.Failed, assessment.State);
        Assert.Equal(RunOutcome.Failed, assessment.Outcome);
        Assert.Contains("did not complete", assessment.FailureSummary, StringComparison.Ordinal);
    }

    [Fact]
    public void Later_committed_attempt_for_the_same_operation_resolves_a_preinvoke_failure()
    {
        var assessment = AgentToolCompletionAssessment.Create(
            [
                CreateMutationTrace(
                    sequence: 1,
                    AgentToolInvocationOutcome.Failed,
                    AgentToolEffectState.NotCommitted,
                    correlationKey: "operation-a"),
                CreateMutationTrace(
                    sequence: 2,
                    AgentToolInvocationOutcome.Succeeded,
                    AgentToolEffectState.Committed,
                    correlationKey: "operation-a")
            ],
            pendingApprovalCount: 0,
            portableOutputValid: true);

        Assert.Equal(ExecutionState.Completed, assessment.State);
        Assert.Equal(RunOutcome.Succeeded, assessment.Outcome);
        Assert.Empty(assessment.FailureSummary);
    }

    [Fact]
    public void Later_success_for_an_unrelated_operation_does_not_resolve_the_failure()
    {
        var assessment = AgentToolCompletionAssessment.Create(
            [
                CreateMutationTrace(
                    sequence: 1,
                    AgentToolInvocationOutcome.Failed,
                    AgentToolEffectState.NotCommitted,
                    correlationKey: "operation-a"),
                CreateMutationTrace(
                    sequence: 2,
                    AgentToolInvocationOutcome.Succeeded,
                    AgentToolEffectState.Committed,
                    correlationKey: "operation-b")
            ],
            pendingApprovalCount: 0,
            portableOutputValid: true);

        Assert.Equal(ExecutionState.Failed, assessment.State);
        Assert.Equal(RunOutcome.Failed, assessment.Outcome);
    }

    [Fact]
    public void Successful_result_with_unknown_commit_state_finishes_failed()
    {
        var assessment = AgentToolCompletionAssessment.Create(
            [CreateMutationTrace(
                sequence: 1,
                AgentToolInvocationOutcome.Succeeded,
                AgentToolEffectState.Unknown,
                correlationKey: "operation-a")],
            pendingApprovalCount: 0,
            portableOutputValid: true);

        Assert.Equal(ExecutionState.Failed, assessment.State);
        Assert.Equal(RunOutcome.Failed, assessment.Outcome);
    }

    [Fact]
    public void Pending_approval_retains_waiting_state()
    {
        var assessment = AgentToolCompletionAssessment.Create(
            [],
            pendingApprovalCount: 1,
            portableOutputValid: true);

        Assert.Equal(ExecutionState.WaitingOnTool, assessment.State);
        Assert.Null(assessment.Outcome);
    }

    [Fact]
    public void Cancelled_mutation_trace_cannot_finish_successfully()
    {
        var assessment = AgentToolCompletionAssessment.Create(
            [CreateMutationTrace(
                sequence: 1,
                AgentToolInvocationOutcome.Cancelled,
                AgentToolEffectState.Unknown,
                correlationKey: "operation-a")],
            pendingApprovalCount: 0,
            portableOutputValid: true);

        Assert.Equal(ExecutionState.Failed, assessment.State);
        Assert.Equal(RunOutcome.Failed, assessment.Outcome);
    }

    [Fact]
    public void Valid_answer_without_a_mutation_finishes_successfully()
    {
        var assessment = AgentToolCompletionAssessment.Create(
            [],
            pendingApprovalCount: 0,
            portableOutputValid: true);

        Assert.Equal(ExecutionState.Completed, assessment.State);
        Assert.Equal(RunOutcome.Succeeded, assessment.Outcome);
        Assert.Empty(assessment.FailureSummary);
    }

    private static AgentToolInvocationTrace CreateMutationTrace(
        int sequence,
        AgentToolInvocationOutcome outcome,
        AgentToolEffectState effectState,
        string correlationKey,
        string failureMessage = "")
    {
        var startedAtUtc = DateTimeOffset.UtcNow.AddSeconds(sequence);
        return new AgentToolInvocationTrace(
            "project_structure_asset_create",
            ToolInvocationClassification.Mutation,
            sequence,
            startedAtUtc,
            startedAtUtc.AddMilliseconds(100),
            Succeeded: outcome == AgentToolInvocationOutcome.Succeeded,
            FailureMessage: failureMessage)
        {
            Outcome = outcome,
            EffectState = effectState,
            OperationCorrelationKey = correlationKey
        };
    }
}