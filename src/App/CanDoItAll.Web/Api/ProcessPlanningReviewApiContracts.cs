using System.Globalization;
using System.Text.Json.Serialization;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Web.Api;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record ProcessPlanningReviewApiExpectation(Guid RootRunId, Guid RunId, Guid StepInstanceId,
    Guid ExecutionRunId, Guid DecisionId, int Generation, string DecisionRevision, string StrategyId,
    Guid WaitingResultKey, Guid BudgetId, string BudgetRevision) {
    internal ProcessDecisionReviewRequest ToDomain(Guid actionId) {
        ProcessDecisionReference reference = new(new(DecisionId), Generation);
        ProcessDecisionReviewRequest request = new(
            new(new(RootRunId), new(RunId), new(StepInstanceId), reference, new(ExecutionRunId)),
            new(StrategyId), new(WaitingResultKey), new(actionId), new(reference, ParseRevision(DecisionRevision)),
            new(new(StepInstanceId), new(BudgetId), ParseRevision(BudgetRevision)));
        request.Validate();
        return request;
    }

    internal static ProcessPlanningReviewApiExpectation From(ProcessDecisionReviewRequest value)
        => new(value.Child.RootRunId.Value, value.Child.RunId.Value, value.Child.StepInstanceId.Value,
            value.Child.ChildId.Value, value.Child.Reference.DecisionId.Value, value.Child.Reference.Generation,
            value.Decision.Revision.ToString(CultureInfo.InvariantCulture), value.StrategyId.Value,
            value.WaitingResultKey.Value, value.Budget.BudgetId.Value, value.Budget.Revision.ToString(CultureInfo.InvariantCulture));

    internal static ProcessPlanningReviewApiExpectation From(ProcessDecisionPendingReview value)
        => new(value.Child.RootRunId.Value, value.Child.RunId.Value, value.Child.StepInstanceId.Value,
            value.Child.ChildId.Value, value.Child.Reference.DecisionId.Value, value.Child.Reference.Generation,
            value.Decision.Revision.ToString(CultureInfo.InvariantCulture), value.StrategyId.Value,
            value.WaitingResultKey.Value, value.Budget.BudgetId.Value, value.Budget.Revision.ToString(CultureInfo.InvariantCulture));

    private static long ParseRevision(string text) {
        if (text is null || !long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var value) ||
                value < 1 || text != value.ToString(CultureInfo.InvariantCulture)) {
            throw new ArgumentException("Revisions require canonical positive Int64 decimal strings.");
        }
        return value;
    }
}

internal sealed record ProcessPlanningChildApiView(Guid Id, ExecutionState State, RunOutcome? Outcome, string Revision) {
    internal static ProcessPlanningChildApiView From(ExecutionRunRecord value)
        => new(value.Id, value.State, value.Outcome, value.Revision.ToString(CultureInfo.InvariantCulture));
}

internal sealed record ProcessPlanningReviewApiView(ProcessPlanningChildApiView Run, ProcessPlanningReviewState State,
    ProcessPlanningReviewApiExpectation? Process, AgentPlanningDecisionExpectation Expected,
    AgentPlanningPublicationReview? Publication, IReadOnlyList<AgentPlanningReviewChoice> AvailableChoices,
    DecisionDataPolicy ParentPolicy, DecisionDataPolicy ChildPolicy, DateTimeOffset ObservedAtUtc, DateTimeOffset? BasisCapturedAtUtc,
    TimeSpan ChargedWork, TimeSpan? RemainingWork, bool HasUnsettledWork, string? RefusalCode) {
    internal static ProcessPlanningReviewApiView From(ProjectTaskPlanProcessReview value)
        => new(ProcessPlanningChildApiView.From(value.Run), value.State,
            value.Process is null ? null : ProcessPlanningReviewApiExpectation.From(value.Process), value.Expected,
            value.Publication, value.AvailableChoices, value.Run.ToolAdmission!.PlanningDecision!.InheritedOrigin!.ParentPolicy,
            value.Run.ToolAdmission.PlanningDecision.Policy,
            value.ObservedAtUtc, value.BasisCapturedAtUtc, value.ChargedWork, value.RemainingWork, value.HasUnsettledWork, value.RefusalCode);
}

internal sealed record ProcessPlanningReviewApiResult(Guid ActionId, ProcessPlanningChildApiView Agent, bool Replayed,
    ProcessRuntimeTransitionOutcome Completion, bool AccountingPending, IReadOnlyList<string> RefusalCodes, bool RequiresRuntimeReload) {
    internal static ProcessPlanningReviewApiResult From(ProjectTaskPlanProcessReviewResult value)
        => new(value.ActionId, ProcessPlanningChildApiView.From(value.Agent.Run), value.Agent.Replayed,
            value.Completion.Outcome, !value.Completion.Succeeded, value.Completion.Diagnostics.Select(item => item.Code).ToArray(), true);
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record ProcessPlanningReviewApiSubmission(AgentInheritedPlanningReviewRequest Agent,
    ProcessPlanningReviewApiExpectation Process, AgentExecutionOperationId? ActivityOperationId = null) {
    internal const int MaximumUtf8Bytes = 24 * 1024;

    internal ProjectTaskPlanProcessReviewRequest ToDomain(Guid runId, Guid stepInstanceId, Guid executionRunId) {
        ArgumentNullException.ThrowIfNull(Agent);
        ArgumentNullException.ThrowIfNull(Process);
        if (runId == Guid.Empty || stepInstanceId == Guid.Empty || executionRunId == Guid.Empty ||
                Process.RunId != runId || Process.StepInstanceId != stepInstanceId || Process.ExecutionRunId != executionRunId ||
                Agent.Expected.ExecutionRunId != executionRunId || ActivityOperationId is { Value: var activity } && activity == Guid.Empty) {
            throw new ArgumentException("Review requires the exact displayed Process run, step, Agent child and activity.");
        }
        return new(Agent, Process.ToDomain(Agent.ActionId));
    }
}
