using System.Text.Json;
using System.Text.Json.Nodes;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;

namespace CanDoItAll.Tests.Unit;

public sealed class AgentInheritedPlanningResponseTests {
    [Theory]
    [InlineData(OnBasisChange.Fail)]
    [InlineData(OnBasisChange.PauseForReview)]
    [InlineData(OnBasisChange.Replan)]
    public void Legacy_origins_keep_pause_behavior_and_do_not_emit_a_new_wire_member(OnBasisChange parentResponse) {
        var journal = Create(parentResponse, null);
        var decision = journal.PlanningDecision!;
        Assert.Equal(OnBasisChange.PauseForReview, decision.Policy.OnBasisChange);
        var json = AgentPlanningDecisionCodec.Serialize(decision);
        Assert.False(JsonNode.Parse(json)!["inheritedOrigin"]!.AsObject().ContainsKey("childOnBasisChange"));
        var copy = AgentPlanningDecisionCodec.Deserialize(json);
        Assert.Equal(json, AgentPlanningDecisionCodec.Serialize(copy));
        Assert.Equal(decision.InheritedOrigin, copy.InheritedOrigin);
        Assert.Null(copy.InheritedOrigin!.ChildOnBasisChange);
        Assert.Equal(OnBasisChange.PauseForReview, copy.Policy.OnBasisChange);
    }

    [Theory]
    [InlineData(ReadBasisMode.RunSnapshot)]
    [InlineData(ReadBasisMode.StepSnapshot)]
    public void Explicit_failure_round_trips_the_original_grant_without_child_refresh_rights(ReadBasisMode mode) {
        var journal = Create(OnBasisChange.Fail, OnBasisChange.Fail, mode);
        journal.Validate();
        var decision = journal.PlanningDecision!;
        var copy = JsonSerializer.Deserialize<AgentToolJournalRecord>(JsonSerializer.Serialize(journal))!;
        AgentToolJournalTransitions.ValidatePersistence(journal, copy);
        var inherited = copy.PlanningDecision!;
        Assert.Equal(decision.InheritedOrigin, inherited.InheritedOrigin);
        Assert.Equal(OnBasisChange.Fail, inherited.Policy.OnBasisChange);
        Assert.Equal(ReadBasisMode.RunSnapshot, inherited.Policy.ReadBasisMode);
        Assert.Equal(mode, inherited.InheritedOrigin!.ParentPolicy.ReadBasisMode);
        Assert.Equal(inherited.InheritedOrigin.Allocation.Grant.WorkTicks, inherited.Policy.Budget.MaximumWorkDuration.Ticks);
        Assert.Equal(0, inherited.Policy.Budget.MaximumReplans);
        Assert.Equal(0, inherited.InheritedOrigin.Allocation.Grant.Replans);
        Assert.Equal(1, inherited.CurrentGeneration);
    }

    [Theory]
    [InlineData(OnBasisChange.Fail, OnBasisChange.PauseForReview)]
    [InlineData(OnBasisChange.Fail, OnBasisChange.Replan)]
    [InlineData(OnBasisChange.Fail, (OnBasisChange)99)]
    [InlineData(OnBasisChange.PauseForReview, OnBasisChange.Fail)]
    [InlineData(OnBasisChange.Replan, OnBasisChange.Fail)]
    public void A_child_cannot_select_an_unsupported_response_or_differ_from_its_parent(OnBasisChange parent, OnBasisChange child) {
        Assert.Throws<InvalidDataException>(() => Create(parent, child));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_saved_origin_cannot_enable_or_remove_delegated_failure(bool originallyEnabled) {
        var journal = Create(OnBasisChange.Fail, originallyEnabled ? OnBasisChange.Fail : null);
        var original = journal.PlanningDecision!;
        var changedOrigin = original.InheritedOrigin! with { ChildOnBasisChange = originallyEnabled ? null : OnBasisChange.Fail };
        var candidate = journal with {
            Revision = journal.Revision + 1,
            PlanningDecision = original with { InheritedOrigin = changedOrigin, Policy = changedOrigin.ResolvePolicy() }
        };
        candidate.Validate();
        Assert.Throws<AgentToolAdmissionException>(() => AgentToolJournalTransitions.ValidatePersistence(journal, candidate));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Failure_wire_cannot_omit_or_null_the_original_response(bool explicitNull) {
        var decision = Create(OnBasisChange.Fail, OnBasisChange.Fail).PlanningDecision!;
        var wire = JsonNode.Parse(AgentPlanningDecisionCodec.Serialize(decision))!;
        var origin = wire["inheritedOrigin"]!.AsObject();
        if (explicitNull) {
            origin["childOnBasisChange"] = null;
        } else {
            origin.Remove("childOnBasisChange");
        }
        Assert.Throws<JsonException>(() => AgentPlanningDecisionCodec.Deserialize(wire.ToJsonString()));
    }

    private static AgentToolJournalRecord Create(OnBasisChange parentResponse, OnBasisChange? childResponse,
        ReadBasisMode mode = ReadBasisMode.StepSnapshot) {
        var journal = AgentInheritedPlanningTestData.Create(parentMode: mode);
        var original = journal.PlanningDecision!;
        var origin = original.InheritedOrigin!;
        origin = origin with {
            ParentPolicy = new(mode, origin.ParentPolicy.PublicationFreshness, parentResponse,
                new(parentResponse == OnBasisChange.Replan ? 2 : 0, origin.ParentPolicy.Budget.MaximumWorkDuration,
                    origin.ParentPolicy.Budget.MaximumBasisAge)),
            ChildOnBasisChange = childResponse
        };
        return journal with { PlanningDecision = AgentPlanningDecisionTransitions.CreateInherited(original.Session,
            original.OriginalTarget, original.Owner, origin, original.StartedAtUtc) };
    }
}
