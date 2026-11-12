using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Tests.Support;

public static class AgentPlanningDecisionTestData {
    public static readonly DateTimeOffset Start = DateTimeOffset.UnixEpoch;

    public static AgentToolJournalRecord Create(AgentToolSessionAdmission? session = null,
        ReadBasisMode mode = ReadBasisMode.StepSnapshot, int replans = 0) {
        session ??= new(new(Guid.NewGuid(), Guid.NewGuid(), AgentExecutionAuthorityId.Create()), Guid.NewGuid(),
            AgentRuntimeContextPurpose.InteractiveChat, new(Guid.NewGuid(), "planning-fixture", new(1)));
        var budget = new DecisionDataPolicyBudget(replans, TimeSpan.FromMinutes(5), TimeSpan.FromDays(1));
        var policy = new DecisionDataPolicy(mode, PublicationFreshness.RequireCurrent,
            replans == 0 ? OnBasisChange.PauseForReview : OnBasisChange.Replan, budget);
        var provenance = new AgentPlanningPolicyProvenance(Guid.NewGuid(), Guid.NewGuid(), session.AgentId, 1,
            policy, null, null, new(true, true, false, true, true, true, PublicationFreshness.RequireCurrent, budget), []);
        return new(AgentToolJournalRecord.PlanningSchemaVersion, 1, session, [], [],
            Support: AgentToolAdmissionSupport.EffectJournalOnly,
            OriginalInput: new(Guid.NewGuid(), "Prepare a bounded task plan."),
            PlanningDecision: AgentPlanningDecisionTransitions.Create(session.Reference, Envelope("{\"target\":\"original\"}"), provenance, Start));
    }

    public static AgentToolProtocolEnvelope Envelope(string json = "{}")
        => AgentToolProtocolEnvelope.Create("planning-fixture", 1, json);

    public static AgentToolJournalRecord Begin(AgentToolJournalRecord journal, DateTimeOffset? at = null) {
        var lease = Guid.NewGuid();
        return Changed(journal, journal with {
            ActiveDispatchLeaseId = lease,
            PlanningDecision = AgentPlanningDecisionTransitions.BeginStep(journal.PlanningDecision!, lease, at ?? Start)
        });
    }

    public static AgentToolJournalRecord Attach(AgentToolJournalRecord journal, AgentPlanningBasisRecord? basis = null,
        DateTimeOffset? at = null) {
        var decision = journal.PlanningDecision!;
        var step = decision.Current.Steps[^1];
        var now = at ?? decision.Work.LastObservedAtUtc;
        basis ??= new(Guid.NewGuid(), now, Envelope("{\"tasks\":[]}"));
        var segment = new AgentToolInvocationSegment(Guid.NewGuid(), journal.Segments.Length, journal.Batches.Length, null,
            journal.Segments.LastOrDefault()?.Id, PendingApprovals: [], CheckpointKind: AgentToolCheckpointKind.EffectsOnly,
            OriginDispatchLeaseId: step.OriginDispatchLeaseId);
        return Changed(journal, journal with {
            Segments = journal.Segments.Add(segment),
            PlanningDecision = AgentPlanningDecisionTransitions.AttachBasis(decision, step.Id, basis, segment.Id, now)
        });
    }

    public static AgentToolJournalRecord Enter(AgentToolJournalRecord journal, DateTimeOffset? at = null) {
        var decision = journal.PlanningDecision!;
        var step = decision.Current.Steps[^1];
        var dispatch = new AgentToolProviderDispatchRecord(new(Guid.NewGuid()),
            journal.ProviderDispatches.IsDefault ? 0 : journal.ProviderDispatches.Length, step.OriginSegmentId!.Value,
            Envelope("{\"request\":true}").Digest, Envelope("{\"contract\":true}").Digest, [], AgentToolProviderDispatchState.Started);
        return Changed(journal, journal with {
            ProviderDispatches = (journal.ProviderDispatches.IsDefault ? [] : journal.ProviderDispatches).Add(dispatch),
            PlanningDecision = AgentPlanningDecisionTransitions.EnterProvider(decision, step.Id, dispatch.Id,
                TimeSpan.Zero, at ?? decision.Work.LastObservedAtUtc)
        });
    }

    public static AgentToolJournalRecord Respond(AgentToolJournalRecord journal, bool publish = true, DateTimeOffset? at = null) {
        var decision = journal.PlanningDecision!;
        var step = decision.Current.Steps[^1];
        var basis = decision.Current.Bases.Single(item => item.Id == step.BasisId);
        var dispatch = journal.ProviderDispatches.Single(item => item.Id == step.ProviderDispatchId);
        var payload = new AgentToolPreparedPayload("planning_fixture_publish", 2, Envelope().Digest, "{}",
            AgentToolProposalEffect.Mutation, AgentToolProposalRecovery.OwnerReceipt, Envelope("{\"operation\":\"original\"}"));
        var proposal = new AgentToolProposalRecord(new(Guid.NewGuid()), 0, "original-publication", payload, true,
            AgentToolProposalState.Prepared, ExecutionApprovalStatus.Pending, null);
        var batch = new AgentToolBatchRecord(new(Guid.NewGuid()), journal.Batches.Length, dispatch.RequestDigest,
            Envelope("{\"response\":true}"), publish ? [proposal] : [], dispatch.Id);
        return Changed(journal, journal with {
            Batches = journal.Batches.Add(batch),
            ProviderDispatches = journal.ProviderDispatches.SetItem(dispatch.Ordinal, dispatch with {
                State = AgentToolProviderDispatchState.ResponseAdmitted, ResponseBatchId = batch.Id
            }),
            PlanningDecision = AgentPlanningDecisionTransitions.AdmitResponse(decision, step.Id, batch.Id,
                publish ? [new(new(batch.Id, proposal.IntentId, payload.SemanticVersion, payload.Digest),
                    basis.Id, basis.Value.Digest, payload.SourcePreparation!)] : [], at ?? decision.Work.LastObservedAtUtc)
        });
    }

    public static AgentToolJournalRecord AwaitApproval(AgentToolJournalRecord journal, TimeSpan spent) {
        var decision = journal.PlanningDecision!;
        return Changed(journal, journal with {
            PlanningDecision = AgentPlanningDecisionTransitions.Settle(decision, decision.Work.Open!.Id, spent,
                decision.Work.Open.OpenedAtUtc + spent, AgentPlanningWorkSettlementReason.AwaitingApproval,
                AgentPlanningGenerationState.AwaitingApproval)
        });
    }

    public static AgentToolJournalRecord Changed(AgentToolJournalRecord before, AgentToolJournalRecord after) {
        after = after with { Revision = checked(before.Revision + 1) };
        AgentToolJournalTransitions.ValidatePersistence(before, after);
        return after;
    }
}
