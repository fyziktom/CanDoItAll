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
        Assert.Equal(AgentToolCompletionFailureKind.RequiredMutation, assessment.FailureKind);
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
        Assert.Equal(AgentToolCompletionFailureKind.None, assessment.FailureKind);
        Assert.Equal(RunOutcome.Succeeded, assessment.Outcome);
        Assert.Empty(assessment.FailureSummary);
    }

    [Fact]
    public void Later_committed_attempt_for_the_same_operation_resolves_a_typed_no_effect_rejection()
    {
        var assessment = AgentToolCompletionAssessment.Create(
            [
                CreateMutationTrace(
                    sequence: 1,
                    AgentToolInvocationOutcome.Failed,
                    AgentToolEffectState.None,
                    correlationKey: "operation-a",
                    failureMessage: "The asset content was rejected before anything was stored."),
                CreateMutationTrace(
                    sequence: 2,
                    AgentToolInvocationOutcome.Succeeded,
                    AgentToolEffectState.Committed,
                    correlationKey: "operation-a")
            ],
            pendingApprovalCount: 0,
            portableOutputValid: true);

        Assert.Equal(ExecutionState.Completed, assessment.State);
        Assert.Equal(AgentToolCompletionFailureKind.None, assessment.FailureKind);
        Assert.Equal(RunOutcome.Succeeded, assessment.Outcome);
        Assert.Empty(assessment.FailureSummary);
    }

    [Fact]
    public void Typed_no_effect_rejection_without_a_later_committed_attempt_finishes_failed()
    {
        var assessment = AgentToolCompletionAssessment.Create(
            [CreateMutationTrace(
                sequence: 1,
                AgentToolInvocationOutcome.Failed,
                AgentToolEffectState.None,
                correlationKey: "operation-a",
                failureMessage: "The asset content was rejected before anything was stored.")],
            pendingApprovalCount: 0,
            portableOutputValid: true);

        Assert.Equal(ExecutionState.Failed, assessment.State);
        Assert.Equal(AgentToolCompletionFailureKind.RequiredMutation, assessment.FailureKind);
        Assert.Equal(RunOutcome.Failed, assessment.Outcome);
        Assert.Contains("rejected before anything was stored", assessment.FailureSummary, StringComparison.Ordinal);
    }

    [Fact]
    public void The_latest_unresolved_attempt_explains_the_failure_after_the_agent_corrected_earlier_input()
    {
        var assessment = AgentToolCompletionAssessment.Create(
            [
                CreateMutationTrace(
                    sequence: 1,
                    AgentToolInvocationOutcome.Failed,
                    AgentToolEffectState.NotCommitted,
                    correlationKey: "operation-a",
                    failureMessage: "Proposed progress must be between 0 and 100 percent."),
                CreateMutationTrace(
                    sequence: 2,
                    AgentToolInvocationOutcome.Failed,
                    AgentToolEffectState.NotCommitted,
                    correlationKey: "operation-a",
                    failureMessage: "Work item 'missing' was not found."),
                CreateMutationTrace(
                    sequence: 3,
                    AgentToolInvocationOutcome.Failed,
                    AgentToolEffectState.NotCommitted,
                    correlationKey: "operation-b",
                    failureMessage: "The task starts before its predecessor finishes.")
            ],
            pendingApprovalCount: 0,
            portableOutputValid: true);

        Assert.Equal(ExecutionState.Failed, assessment.State);
        Assert.Equal(AgentToolCompletionFailureKind.RequiredMutation, assessment.FailureKind);
        Assert.Equal(
            "Required mutation 'project_structure_asset_create' did not complete: The task starts before its predecessor finishes.",
            assessment.FailureSummary);
    }

    [Fact]
    public void An_uncertain_effect_is_named_before_a_later_rejection_with_no_effect()
    {
        var assessment = AgentToolCompletionAssessment.Create(
            [
                CreateMutationTrace(
                    sequence: 1,
                    AgentToolInvocationOutcome.Unknown,
                    AgentToolEffectState.Unknown,
                    correlationKey: "operation-a",
                    failureMessage: "The write may have been stored."),
                CreateMutationTrace(
                    sequence: 2,
                    AgentToolInvocationOutcome.Failed,
                    AgentToolEffectState.None,
                    correlationKey: "operation-b",
                    failureMessage: "The target was rejected before anything was stored.")
            ],
            pendingApprovalCount: 0,
            portableOutputValid: true);

        Assert.Equal(ExecutionState.Failed, assessment.State);
        Assert.Contains("The write may have been stored.", assessment.FailureSummary, StringComparison.Ordinal);
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
        Assert.Equal(AgentToolCompletionFailureKind.RequiredMutation, assessment.FailureKind);
        Assert.Equal(RunOutcome.Failed, assessment.Outcome);
    }

    [Fact]
    public void Later_committed_attempt_of_another_tool_does_not_resolve_the_rejection()
    {
        var assessment = AgentToolCompletionAssessment.Create(
            [
                CreateMutationTrace(
                    sequence: 1,
                    AgentToolInvocationOutcome.Failed,
                    AgentToolEffectState.None,
                    correlationKey: "operation-a",
                    toolName: "workspace_write_file"),
                CreateMutationTrace(
                    sequence: 2,
                    AgentToolInvocationOutcome.Succeeded,
                    AgentToolEffectState.Committed,
                    correlationKey: "operation-a",
                    toolName: "workspace_delete_path")
            ],
            pendingApprovalCount: 0,
            portableOutputValid: true);

        Assert.Equal(ExecutionState.Failed, assessment.State);
        Assert.Equal(AgentToolCompletionFailureKind.RequiredMutation, assessment.FailureKind);
    }

    [Fact]
    public void Resolved_rejection_does_not_hide_an_unresolved_mutation_elsewhere()
    {
        var assessment = AgentToolCompletionAssessment.Create(
            [
                CreateMutationTrace(
                    sequence: 1,
                    AgentToolInvocationOutcome.Failed,
                    AgentToolEffectState.None,
                    correlationKey: "operation-a"),
                CreateMutationTrace(
                    sequence: 2,
                    AgentToolInvocationOutcome.Succeeded,
                    AgentToolEffectState.Committed,
                    correlationKey: "operation-a"),
                CreateMutationTrace(
                    sequence: 3,
                    AgentToolInvocationOutcome.Succeeded,
                    AgentToolEffectState.Unknown,
                    correlationKey: "operation-b")
            ],
            pendingApprovalCount: 0,
            portableOutputValid: true);

        Assert.Equal(ExecutionState.Failed, assessment.State);
        Assert.Equal(AgentToolCompletionFailureKind.RequiredMutation, assessment.FailureKind);
    }

    [Fact]
    public void Successful_read_does_not_resolve_an_unresolved_mutation()
    {
        var assessment = AgentToolCompletionAssessment.Create(
            [
                CreateMutationTrace(
                    sequence: 1,
                    AgentToolInvocationOutcome.Failed,
                    AgentToolEffectState.None,
                    correlationKey: "operation-a"),
                CreateMutationTrace(
                    sequence: 2,
                    AgentToolInvocationOutcome.Succeeded,
                    AgentToolEffectState.None,
                    correlationKey: "operation-a",
                    classification: ToolInvocationClassification.Read)
            ],
            pendingApprovalCount: 0,
            portableOutputValid: true);

        Assert.Equal(ExecutionState.Failed, assessment.State);
        Assert.Equal(AgentToolCompletionFailureKind.RequiredMutation, assessment.FailureKind);
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
        Assert.Equal(AgentToolCompletionFailureKind.RequiredMutation, assessment.FailureKind);
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
        Assert.Equal(AgentToolCompletionFailureKind.None, assessment.FailureKind);
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
        Assert.Equal(AgentToolCompletionFailureKind.RequiredMutation, assessment.FailureKind);
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
        Assert.Equal(AgentToolCompletionFailureKind.None, assessment.FailureKind);
        Assert.Equal(RunOutcome.Succeeded, assessment.Outcome);
        Assert.Empty(assessment.FailureSummary);
    }

    [Fact]
    public void Invalid_portable_output_keeps_its_distinct_validation_failure_kind() {
        var assessment = AgentToolCompletionAssessment.Create([], 0, portableOutputValid: false);
        Assert.Equal(ExecutionState.Failed, assessment.State);
        Assert.Equal(AgentToolCompletionFailureKind.PortableOutputValidation, assessment.FailureKind);
        Assert.Empty(assessment.FailureSummary);
    }

    private static AgentToolInvocationTrace CreateMutationTrace(
        int sequence,
        AgentToolInvocationOutcome outcome,
        AgentToolEffectState effectState,
        string correlationKey,
        string failureMessage = "",
        string toolName = "project_structure_asset_create",
        ToolInvocationClassification classification = ToolInvocationClassification.Mutation)
    {
        var startedAtUtc = DateTimeOffset.UtcNow.AddSeconds(sequence);
        return new AgentToolInvocationTrace(
            toolName,
            classification,
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