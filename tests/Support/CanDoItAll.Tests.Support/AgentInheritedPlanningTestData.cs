using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Tests.Support;

public static class AgentInheritedPlanningTestData {
    public const string SourceKind = "planning-parent-fixture";
    public static readonly DateTimeOffset Start = DateTimeOffset.UnixEpoch;

    public static AgentToolJournalRecord Create(AgentToolSessionAdmission? session = null,
        ReadBasisMode parentMode = ReadBasisMode.StepSnapshot, DateTimeOffset? capturedAt = null) {
        var source = new AgentToolBackgroundSourceBinding(SourceKind, Guid.NewGuid().ToString("N"),
            AgentPlanningDecisionTestData.Envelope("{\"owner\":1}").Digest, AgentPlanningDecisionTestData.Envelope("{\"execution\":1}").Digest);
        session ??= new(new(Guid.NewGuid(), Guid.Empty, default, source), Guid.NewGuid(),
            AgentRuntimeContextPurpose.GovernedProcessAutomation, new(Guid.NewGuid(), "inherited-planning-fixture", new(1)));
        var parent = new DecisionDataPolicy(parentMode, PublicationFreshness.RequireCurrent, OnBasisChange.Replan,
            new(2, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(10)));
        var origin = new AgentPlanningInheritedOrigin(session.Profile, Guid.NewGuid(), session.AgentId,
            session.Reference.BackgroundSource!, AgentPlanningDecisionTestData.Envelope("{\"parent\":1}"),
            Guid.NewGuid(), capturedAt ?? Start.AddMinutes(-1), Material().Digest, parent,
            new(new(Guid.NewGuid()), new(Guid.NewGuid()), new(TimeSpan.FromSeconds(30).Ticks, 0)));
        return new(AgentToolJournalRecord.InheritedPlanningSchemaVersion, 1, session, [], [],
            Support: AgentToolAdmissionSupport.EffectJournalOnly, BackgroundInput: new("Prepare a bounded task plan."),
            PlanningDecision: AgentPlanningDecisionTransitions.CreateInherited(session.Reference,
                AgentPlanningDecisionTestData.Envelope("{\"target\":\"inherited\"}"), AgentPlanningOwner.ProjectTaskPlan, origin, Start));
    }

    public static AgentPlanningBasisRecord Basis(AgentToolJournalRecord journal) {
        var origin = journal.PlanningDecision!.InheritedOrigin!;
        return new(origin.BasisId, origin.CapturedAtUtc, Material());
    }

    private static AgentToolProtocolEnvelope Material() => AgentPlanningDecisionTestData.Envelope("{\"tasks\":[\"original\"]}");
}
