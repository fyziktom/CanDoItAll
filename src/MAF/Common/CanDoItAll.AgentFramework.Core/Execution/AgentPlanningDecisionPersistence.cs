using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

internal static class AgentPlanningDecisionPersistence {
    internal static void Validate(AgentPlanningDecisionRecord current, AgentPlanningDecisionRecord proposed,
        AgentToolJournalRecord journal) {
        if (ReferenceEquals(current, proposed)) {
            return;
        }
        if (current.DecisionId != proposed.DecisionId || current.Session != proposed.Session ||
            current.StartedAtUtc != proposed.StartedAtUtc || current.Owner != proposed.Owner ||
            current.OriginalTarget != proposed.OriginalTarget || current.Policy != proposed.Policy ||
            !SameProvenance(current.Provenance, proposed.Provenance) ||
            proposed.CurrentGeneration < current.CurrentGeneration || proposed.CurrentGeneration > current.CurrentGeneration + 1) {
            throw Conflict();
        }
        var advancing = proposed.CurrentGeneration != current.CurrentGeneration;
        if (advancing) {
            var added = proposed.Current;
            if (current.Work.Open is not null || proposed.Work.Open is not null ||
                current.Current.State is not (AgentPlanningGenerationState.AwaitingApproval or AgentPlanningGenerationState.PausedBasisChanged) ||
                added.State != AgentPlanningGenerationState.Active || !added.Bases.IsEmpty || !added.Steps.IsEmpty ||
                added.ClosedWorkTicksAtStart != current.Work.ClosedWorkTicks ||
                proposed.Work.ClosedWorkTicks != current.Work.ClosedWorkTicks ||
                proposed.AutomaticReplans != current.AutomaticReplans + (added.Reason == AgentPlanningGenerationReason.AutomaticReplan ? 1 : 0) ||
                journal.HasUnresolvedEffects || journal.Batches.SelectMany(batch => batch.Proposals).Any(proposal =>
                    proposal.EffectState is AgentToolEffectState.Unknown or AgentToolEffectState.Committed)) {
                throw Conflict();
            }
        } else if (current.AutomaticReplans != proposed.AutomaticReplans) {
            throw Conflict();
        }
        for (var index = 0; index < current.Generations.Length; index++) {
            var previous = current.Generations[index];
            var next = proposed.Generations[index];
            var retired = index < current.CurrentGeneration - 1 || advancing;
            if (previous.Number != next.Number || previous.Reason != next.Reason ||
                previous.ClosedWorkTicksAtStart != next.ClosedWorkTicksAtStart ||
                next.Bases.Length < previous.Bases.Length || next.Bases.Length > previous.Bases.Length + (retired ? 0 : 1) ||
                !previous.Bases.SequenceEqual(next.Bases.Take(previous.Bases.Length)) ||
                next.Steps.Length < previous.Steps.Length || next.Steps.Length > previous.Steps.Length + (retired ? 0 : 1) ||
                (advancing && index == current.CurrentGeneration - 1
                    ? next.State != AgentPlanningGenerationState.Superseded
                    : !CanChangeState(previous.State, next.State))) {
                throw Conflict();
            }
            for (var ordinal = 0; ordinal < previous.Steps.Length; ordinal++) {
                RequireStep(previous.Steps[ordinal], next.Steps[ordinal], retired);
            }
            if (next.Steps.Length > previous.Steps.Length &&
                (previous.State != AgentPlanningGenerationState.Active ||
                    previous.Steps.LastOrDefault() is { State: not AgentPlanningStepState.ResponseAdmitted } ||
                    journal.HasUnresolvedEffects || journal.Batches.SelectMany(batch => batch.Proposals)
                        .Any(proposal => proposal.EffectState is AgentToolEffectState.Unknown or AgentToolEffectState.Committed) ||
                    next.Steps[^1].State != AgentPlanningStepState.CapturingBasis ||
                    proposed.Work.Open?.StepId != next.Steps[^1].Id ||
                    current.Work.Open is { } priorInterval && (proposed.Work.LastSettlement?.IntervalId != priorInterval.Id ||
                        proposed.Work.LastSettlement.Reason != AgentPlanningWorkSettlementReason.StepCompleted))) {
                throw Conflict();
            }
        }
        RequireWork(current.Work, proposed.Work);
        if (current.Work == proposed.Work && !SameHistory(current, proposed)) {
            throw Conflict();
        }
    }

    private static void RequireStep(AgentPlanningStepRecord previous, AgentPlanningStepRecord next, bool retired) {
        var allowed = previous.State == next.State || !retired && (previous.State, next.State) is
            (AgentPlanningStepState.CapturingBasis, AgentPlanningStepState.PreparedInput or AgentPlanningStepState.Retired) or
            (AgentPlanningStepState.PreparedInput, AgentPlanningStepState.ProviderEntered or AgentPlanningStepState.Retired) or
            (AgentPlanningStepState.ProviderEntered, AgentPlanningStepState.ResponseAdmitted);
        if (!allowed || previous.Id != next.Id || previous.Ordinal != next.Ordinal ||
            previous.OriginDispatchLeaseId != next.OriginDispatchLeaseId ||
            previous.BasisId is not null && (next.BasisId != previous.BasisId || next.OriginSegmentId != previous.OriginSegmentId) ||
            previous.ProviderDispatchId is not null && next.ProviderDispatchId != previous.ProviderDispatchId ||
            previous.ResponseBatchId is not null && next.ResponseBatchId != previous.ResponseBatchId ||
            (previous.State == next.State || retired) &&
                (previous.BasisId != next.BasisId || previous.OriginSegmentId != next.OriginSegmentId ||
                    previous.ProviderDispatchId != next.ProviderDispatchId || previous.ResponseBatchId != next.ResponseBatchId ||
                    !previous.Publications.SequenceEqual(next.Publications))) {
            throw Conflict();
        }
    }

    private static bool CanChangeState(AgentPlanningGenerationState previous, AgentPlanningGenerationState next)
        => previous == next || previous switch {
            AgentPlanningGenerationState.Active => next is AgentPlanningGenerationState.AwaitingApproval or
                AgentPlanningGenerationState.PausedBasisChanged or AgentPlanningGenerationState.Completed or
                AgentPlanningGenerationState.Cancelled or AgentPlanningGenerationState.Failed,
            AgentPlanningGenerationState.AwaitingApproval => next is AgentPlanningGenerationState.Active or
                AgentPlanningGenerationState.Cancelled or AgentPlanningGenerationState.Failed,
            AgentPlanningGenerationState.PausedBasisChanged => next is AgentPlanningGenerationState.Cancelled or AgentPlanningGenerationState.Failed,
            AgentPlanningGenerationState.Failed => next == AgentPlanningGenerationState.Cancelled,
            _ => false
        };

    private static void RequireWork(AgentPlanningWorkRecord previous, AgentPlanningWorkRecord next) {
        if (previous == next) {
            return;
        }
        if (next.Revision <= previous.Revision || next.ClosedWorkTicks < previous.ClosedWorkTicks ||
            next.LastObservedAtUtc < previous.LastObservedAtUtc) {
            throw Conflict();
        }
        if (previous.Open is { } open) {
            if (next.Open is { } continuing) {
                if (continuing.Id != open.Id) {
                    RequireSettlement(previous, next, open);
                    if (continuing.StepId == open.StepId || continuing.Generation != open.Generation ||
                        continuing.OpenedAtUtc != next.LastObservedAtUtc ||
                        next.LastSettlement!.Reason != AgentPlanningWorkSettlementReason.StepCompleted) {
                        throw Conflict();
                    }
                    return;
                }
                if (continuing.Id != open.Id || continuing.Generation != open.Generation || continuing.StepId != open.StepId ||
                    continuing.DispatchLeaseId != open.DispatchLeaseId || continuing.OpenedAtUtc != open.OpenedAtUtc ||
                    continuing.Phase < open.Phase || next.ClosedWorkTicks != previous.ClosedWorkTicks || next.LastSettlement != previous.LastSettlement) {
                    throw Conflict();
                }
            } else {
                RequireSettlement(previous, next, open);
            }
        } else if (next.ClosedWorkTicks != previous.ClosedWorkTicks || next.LastSettlement != previous.LastSettlement ||
            next.Open is { } started && (started.OpenedAtUtc != next.LastObservedAtUtc || started.Id == previous.LastSettlement?.IntervalId)) {
            throw Conflict();
        }
    }

    private static void RequireSettlement(AgentPlanningWorkRecord previous, AgentPlanningWorkRecord next, AgentPlanningOpenWorkInterval open) {
        if (next.LastSettlement is not { } settlement || settlement.IntervalId != open.Id ||
            settlement.EndedAtUtc != next.LastObservedAtUtc || settlement.ChargedTicks < (settlement.EndedAtUtc - open.OpenedAtUtc).Ticks ||
            next.ClosedWorkTicks - previous.ClosedWorkTicks != settlement.ChargedTicks) {
            throw Conflict();
        }
    }

    private static bool SameProvenance(AgentPlanningPolicyProvenance left, AgentPlanningPolicyProvenance right)
        => left with { EnclosingBudgets = right.EnclosingBudgets } == right && left.EnclosingBudgets.SequenceEqual(right.EnclosingBudgets);

    private static bool SameHistory(AgentPlanningDecisionRecord left, AgentPlanningDecisionRecord right) {
        if (left.CurrentGeneration != right.CurrentGeneration) {
            return false;
        }
        for (var index = 0; index < left.Generations.Length; index++) {
            var previous = left.Generations[index];
            var next = right.Generations[index];
            if (previous.State != next.State || !previous.Bases.SequenceEqual(next.Bases) || previous.Steps.Length != next.Steps.Length) {
                return false;
            }
            for (var step = 0; step < previous.Steps.Length; step++) {
                var before = previous.Steps[step];
                var after = next.Steps[step];
                if (before with { Publications = after.Publications } != after || !before.Publications.SequenceEqual(after.Publications)) {
                    return false;
                }
            }
        }
        return true;
    }

    private static AgentToolAdmissionException Conflict()
        => new("tool-admission.planning-conflict", "A saved planning decision cannot replace its original basis, history, policy, origin or spent work.");
}
