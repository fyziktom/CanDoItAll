using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Runtime.Abstractions;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.AgentFramework.Core;

internal sealed class AgentPlanningPublicationCompletion(AgentToolAdmissionJournal journal, AgentPlanningDecisionOwner owner) {
    internal async Task<AgentPlanningPublicationResult> DispatchAsync(ExecutionRunRecord original,
        AgentToolRunLease lease, AgentSavedEffectDispatchRequest request, IAgentPlanningStepParticipant participant,
        IAgentContinuationRuntime continuation, CancellationToken cancellationToken) {
        var binding = request.Binding;
        var response = await continuation.DispatchSavedEffectAsync(request, cancellationToken);
        using var settlement = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var acknowledged = (await journal.ReadPlanningSnapshotAsync(lease, settlement.Token)).Run;
        var proposal = AgentToolJournalTransitions.RequireProposal(acknowledged.ToolAdmission!, binding);
        if (response.Binding != binding || response.RunRevision != acknowledged.Revision ||
            response.JournalRevision != acknowledged.ToolAdmission!.Revision || response.State != proposal.State ||
            response.EffectState != proposal.EffectState) {
            throw new AgentToolAdmissionException("planning.unsupported",
                "The publication acknowledgement no longer matches its original journal.");
        }
        var assessment = participant.ClassifyPublication(AgentToolSessionObservation.From(original), proposal.Payload, response);
        if (!Enum.IsDefined(assessment.Outcome)) {
            throw new AgentToolAdmissionException("planning.unsupported",
                "The planning participant returned an unsupported publication outcome.");
        }
        var work = lease.PlanningWork ?? throw new InvalidOperationException("Saved planning dispatch has no active work interval.");
        var decision = acknowledged.ToolAdmission.PlanningDecision!;
        var completed = assessment.Outcome == AgentPlanningPublicationOutcome.BasisChanged && decision.Policy.OnBasisChange != OnBasisChange.Fail
            ? await owner.PauseForBasisChangeAsync(work, binding, settlement.Token)
            : await owner.SettleTerminalAsync(work, assessment.Outcome == AgentPlanningPublicationOutcome.Committed
                ? AgentPlanningGenerationState.Completed : AgentPlanningGenerationState.Failed, settlement.Token);
        return new(completed, assessment);
    }
}

internal sealed record AgentPlanningPublicationResult(ExecutionRunRecord Run, AgentPlanningPublicationAssessment Assessment);
