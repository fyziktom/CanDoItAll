using System.Text.Json;
using System.Text.Json.Nodes;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using static CanDoItAll.Tests.Support.AgentPlanningDecisionTestData;

namespace CanDoItAll.Tests.Unit;

public sealed class AgentInheritedPlanningJournalTests {
    public enum OriginChange { Profile, Namespace, Agent, Source, ParentBinding, BasisId, CaptureTime, BasisDigest, ParentPolicy, Allocation }
    public enum InvalidWire { Downgrade, MissingOrigin, NullOrigin, MissingGrant, ZeroBudget, ZeroGrant, MissingProvenance, MixedProvenance, UnknownMember }

    [Theory]
    [InlineData(ReadBasisMode.RunSnapshot)]
    [InlineData(ReadBasisMode.StepSnapshot)]
    public void Child_keeps_original_parent_policy_and_uses_only_its_finite_work_grant(ReadBasisMode mode) {
        var journal = AgentInheritedPlanningTestData.Create(parentMode: mode);
        journal.Validate();
        var decision = journal.PlanningDecision!;
        var origin = decision.InheritedOrigin!;
        Assert.Null(decision.Provenance);
        Assert.Equal(mode, origin.ParentPolicy.ReadBasisMode);
        Assert.Equal(ReadBasisMode.RunSnapshot, decision.Policy.ReadBasisMode);
        Assert.Equal(origin.Allocation.Grant.WorkTicks, decision.Policy.Budget.MaximumWorkDuration.Ticks);
        Assert.Equal(origin.ParentPolicy.Budget.MaximumBasisAge, decision.Policy.Budget.MaximumBasisAge);
        Assert.Equal(origin.ParentPolicy.PublicationFreshness, decision.Policy.PublicationFreshness);
        Assert.Equal(OnBasisChange.PauseForReview, decision.Policy.OnBasisChange);
        Assert.Equal(0, decision.Policy.Budget.MaximumReplans);
        Assert.True(origin.CapturedAtUtc < decision.StartedAtUtc);
    }

    [Fact]
    public void Inherited_journal_round_trips_original_basis_time_allocation_and_spent_work() {
        var initial = AgentInheritedPlanningTestData.Create();
        var waiting = AwaitApproval(Respond(Enter(Attach(Begin(initial), AgentInheritedPlanningTestData.Basis(initial)))),
            TimeSpan.FromSeconds(3));
        var json = JsonSerializer.Serialize(waiting);
        var copy = JsonSerializer.Deserialize<AgentToolJournalRecord>(json)!;
        AgentToolJournalTransitions.ValidatePersistence(waiting, copy);
        Assert.Equal(json, JsonSerializer.Serialize(copy));
        Assert.Equal(waiting.PlanningDecision!.InheritedOrigin, copy.PlanningDecision!.InheritedOrigin);
        Assert.Equal(TimeSpan.FromSeconds(3).Ticks, copy.PlanningDecision.Work.ClosedWorkTicks);
        Assert.Equal(waiting.PlanningDecision.InheritedOrigin!.CapturedAtUtc, copy.PlanningDecision.Current.Bases.Single().CapturedAtUtc);
        Assert.Equal(2, JsonNode.Parse(AgentPlanningDecisionCodec.Serialize(copy.PlanningDecision))!["schemaVersion"]!.GetValue<int>());
    }

    [Theory]
    [InlineData(InvalidWire.Downgrade)]
    [InlineData(InvalidWire.MissingOrigin)]
    [InlineData(InvalidWire.NullOrigin)]
    [InlineData(InvalidWire.MissingGrant)]
    [InlineData(InvalidWire.ZeroBudget)]
    [InlineData(InvalidWire.ZeroGrant)]
    [InlineData(InvalidWire.MissingProvenance)]
    [InlineData(InvalidWire.MixedProvenance)]
    [InlineData(InvalidWire.UnknownMember)]
    public void Inherited_wire_requires_its_exact_version_origin_and_grant(InvalidWire change) {
        var value = JsonNode.Parse(AgentPlanningDecisionCodec.Serialize(AgentInheritedPlanningTestData.Create().PlanningDecision!))!.AsObject();
        switch (change) {
            case InvalidWire.Downgrade:
                value["schemaVersion"] = 1;
                break;
            case InvalidWire.MissingOrigin:
                value.Remove("inheritedOrigin");
                break;
            case InvalidWire.NullOrigin:
                value["inheritedOrigin"] = null;
                break;
            case InvalidWire.MissingGrant:
                value["inheritedOrigin"]!.AsObject().Remove("grantedWorkTicks");
                break;
            case InvalidWire.ZeroBudget:
                value["inheritedOrigin"]!["budgetId"] = Guid.Empty;
                break;
            case InvalidWire.ZeroGrant:
                value["inheritedOrigin"]!["grantedWorkTicks"] = 0;
                break;
            case InvalidWire.MissingProvenance:
                value.Remove("provenance");
                break;
            case InvalidWire.MixedProvenance:
                value["provenance"] = JsonNode.Parse(AgentPlanningDecisionCodec.Serialize(Create().PlanningDecision!))!["provenance"]!.DeepClone();
                break;
            case InvalidWire.UnknownMember:
                value["inheritedOrigin"]!["refreshAllowed"] = true;
                break;
        }
        Assert.Throws<JsonException>(() => AgentPlanningDecisionCodec.Deserialize(value.ToJsonString()));
    }

    [Theory]
    [InlineData(OriginChange.Profile)]
    [InlineData(OriginChange.Namespace)]
    [InlineData(OriginChange.Agent)]
    [InlineData(OriginChange.Source)]
    [InlineData(OriginChange.ParentBinding)]
    [InlineData(OriginChange.BasisId)]
    [InlineData(OriginChange.CaptureTime)]
    [InlineData(OriginChange.BasisDigest)]
    [InlineData(OriginChange.ParentPolicy)]
    [InlineData(OriginChange.Allocation)]
    public void A_saved_decision_cannot_substitute_any_original_parent_or_funding_fact(OriginChange change) {
        var journal = AgentInheritedPlanningTestData.Create();
        var current = journal.PlanningDecision!;
        var origin = current.InheritedOrigin!;
        var changed = change switch {
            OriginChange.Profile => origin with { Profile = new(Guid.NewGuid(), origin.Profile.Fingerprint, origin.Profile.Generation) },
            OriginChange.Namespace => origin with { WorkspaceNamespaceId = Guid.NewGuid() },
            OriginChange.Agent => origin with { AgentId = Guid.NewGuid() },
            OriginChange.Source => origin with { Source = new(origin.Source.SourceKind, "another", origin.Source.OwnerFingerprint, origin.Source.ExecutionFingerprint) },
            OriginChange.ParentBinding => origin with { ParentBinding = Envelope("{\"parent\":2}") },
            OriginChange.BasisId => origin with { BasisId = Guid.NewGuid() },
            OriginChange.CaptureTime => origin with { CapturedAtUtc = origin.CapturedAtUtc.AddSeconds(-1) },
            OriginChange.BasisDigest => origin with { BasisDigest = Envelope("{\"different\":true}").Digest },
            OriginChange.ParentPolicy => origin with { ParentPolicy = new(ReadBasisMode.RunSnapshot, origin.ParentPolicy.PublicationFreshness,
                origin.ParentPolicy.OnBasisChange, origin.ParentPolicy.Budget) },
            OriginChange.Allocation => origin with { Allocation = origin.Allocation with { Id = new(Guid.NewGuid()) } },
            _ => throw new ArgumentOutOfRangeException(nameof(change))
        };
        var candidate = journal with { Revision = journal.Revision + 1, PlanningDecision = current with { InheritedOrigin = changed } };
        if (change is OriginChange.Profile or OriginChange.Agent or OriginChange.Source) {
            Assert.Throws<InvalidDataException>(() => AgentToolJournalTransitions.ValidatePersistence(journal, candidate));
        } else {
            Assert.Throws<AgentToolAdmissionException>(() => AgentToolJournalTransitions.ValidatePersistence(journal, candidate));
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Attaching_material_cannot_renew_age_replace_identity_or_replace_content(int change) {
        var journal = AgentInheritedPlanningTestData.Create();
        var basis = AgentInheritedPlanningTestData.Basis(journal);
        basis = change switch {
            0 => basis with { CapturedAtUtc = Start },
            1 => basis with { Id = Guid.NewGuid() },
            _ => basis with { Value = Envelope("{\"tasks\":[\"different\"]}") }
        };
        Assert.Throws<InvalidDataException>(() => Attach(Begin(journal), basis));
    }

    [Fact]
    public void Repeated_provider_steps_reuse_one_inherited_basis_and_keep_the_original_age() {
        var initial = AgentInheritedPlanningTestData.Create();
        var basis = AgentInheritedPlanningTestData.Basis(initial);
        var first = Respond(Enter(Attach(Begin(initial), basis)), publish: false);
        var prior = first.PlanningDecision!;
        var next = AgentPlanningDecisionTransitions.AdvanceStep(prior, prior.Work.Open!.Id, first.ActiveDispatchLeaseId!.Value,
            TimeSpan.FromSeconds(2), Start.AddSeconds(2));
        var second = Attach(Changed(first, first with { PlanningDecision = next }), basis);
        Assert.Single(second.PlanningDecision!.Current.Bases);
        Assert.All(second.PlanningDecision.Current.Steps, step => Assert.Equal(basis.Id, step.BasisId));
        Assert.Equal(basis.CapturedAtUtc, second.PlanningDecision.Current.Bases[0].CapturedAtUtc);
        Assert.Equal(TimeSpan.FromSeconds(2).Ticks, second.PlanningDecision.Work.ClosedWorkTicks);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Child_refresh_always_returns_control_to_the_parent(bool automatic) {
        var initial = AgentInheritedPlanningTestData.Create();
        var waiting = AwaitApproval(Respond(Enter(Attach(Begin(initial), AgentInheritedPlanningTestData.Basis(initial)))),
            TimeSpan.FromSeconds(2));
        var failure = Assert.Throws<AgentPlanningAdmissionException>(() =>
            AgentPlanningDecisionTransitions.NewGeneration(waiting.PlanningDecision!, automatic, Start.AddSeconds(3)));
        Assert.Equal(AgentPlanningAdmissionRefusal.ParentReconciliationRequired, failure.Reason);
    }

    [Fact]
    public void Expired_or_future_parent_capture_cannot_start_new_child_work() {
        Assert.Throws<InvalidDataException>(() => AgentInheritedPlanningTestData.Create(capturedAt: Start.AddTicks(1)));
        var expired = Assert.Throws<AgentPlanningAdmissionException>(() => AgentInheritedPlanningTestData.Create(capturedAt: Start.AddMinutes(-10)));
        Assert.Equal(AgentPlanningAdmissionRefusal.BasisExpired, expired.Reason);
        var pending = AgentInheritedPlanningTestData.Create(capturedAt: Start.AddMinutes(-10).AddSeconds(1));
        var prepared = Attach(Begin(pending), AgentInheritedPlanningTestData.Basis(pending));
        Assert.Equal(AgentPlanningAdmissionRefusal.BasisExpired,
            Assert.Throws<AgentPlanningAdmissionException>(() => Enter(prepared, Start.AddSeconds(1))).Reason);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(5)]
    [InlineData(6)]
    public void Inherited_journal_cannot_be_downgraded_or_grafted_onto_existing_admission(int version) {
        var inherited = AgentInheritedPlanningTestData.Create();
        Assert.Throws<InvalidDataException>(() => (inherited with { SchemaVersion = version }).Validate());
        if (version == 6) {
            var interactive = Create();
            Assert.Throws<AgentToolAdmissionException>(() => AgentToolJournalTransitions.ValidatePersistence(interactive, inherited));
            return;
        }
        var original = inherited with { SchemaVersion = version, PlanningDecision = null,
            Support = version == 5 ? AgentToolAdmissionSupport.EffectJournalOnly : AgentToolAdmissionSupport.Recoverable };
        original.Validate();
        Assert.Throws<AgentToolAdmissionException>(() => AgentToolJournalTransitions.ValidatePersistence(original,
            inherited with { Revision = original.Revision + 1 }));
    }

    [Fact]
    public void Original_v1_bytes_remain_exact_and_cannot_accept_even_a_null_inherited_origin() {
        const string original = """
        {"schemaVersion":1,"decisionId":"f2cf3c2c-cb6d-4dbe-b55f-69fae57cd367","session":{"executionRunId":"3885f01d-54ee-41e2-9c16-509fb7ca6227","chatSessionId":"d32985fa-5215-4fb5-8f30-b183f806b3a3","authorityId":{"value":"87ae55e1-b07e-4270-9418-81acc5ca07c2","isEmpty":false}},"startedAtUtc":"1970-01-01T00:00:00+00:00","owner":1,"originalTarget":{"format":"planning-fixture","version":1,"payloadJson":"{\u0022target\u0022:\u0022original\u0022}","digest":{"value":"5f5c580c589fb54f92c10aa9c4dfaee222cbe003d4f0b5a66c1666fba44d8d75"}},"policy":{"schemaVersion":1,"readBasisMode":0,"publicationFreshness":1,"onBasisChange":2,"maximumReplans":1,"maximumWorkDuration":"00:05:00","maximumBasisAge":"1.00:00:00"},"provenance":{"workspaceNamespaceId":"f0cfe446-c6fc-4775-b94f-9a86af4a4fc3","workspaceHeadRevision":"e9a4dd8b-a164-45d1-b3e4-625c2d457a0f","agentId":"08c961de-2ce4-460d-b147-09a22d1e45f0","catalogDataRevision":1,"workspacePolicy":{"schemaVersion":1,"readBasisMode":0,"publicationFreshness":1,"onBasisChange":2,"maximumReplans":1,"maximumWorkDuration":"00:05:00","maximumBasisAge":"1.00:00:00"},"agentOverride":null,"authorizedInvocationOverride":null,"ownerCapabilities":{"supportsRunSnapshot":true,"supportsStepSnapshot":true,"supportsPinnedObservation":false,"supportsCurrentPublication":true,"supportsPauseForReview":true,"supportsReplan":true,"minimumPublicationFreshness":1,"budgetCeiling":{"maximumReplans":1,"maximumWorkDuration":"00:05:00","maximumBasisAge":"1.00:00:00"}},"enclosingBudgets":[]},"currentGeneration":1,"automaticReplans":0,"generations":[{"number":1,"reason":1,"state":1,"closedWorkTicksAtStart":0,"bases":[],"steps":[]}],"work":{"revision":1,"closedWorkTicks":0,"lastObservedAtUtc":"1970-01-01T00:00:00+00:00","open":null,"lastSettlement":null}}
        """;
        Assert.Equal(original, AgentPlanningDecisionCodec.Serialize(AgentPlanningDecisionCodec.Deserialize(original)));
        var malformed = JsonNode.Parse(original)!.AsObject();
        malformed["inheritedOrigin"] = null;
        Assert.Throws<JsonException>(() => AgentPlanningDecisionCodec.Deserialize(malformed.ToJsonString()));
    }
}
