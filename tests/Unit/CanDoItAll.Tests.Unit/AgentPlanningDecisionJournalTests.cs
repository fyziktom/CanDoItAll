using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using static CanDoItAll.Tests.Support.AgentPlanningDecisionTestData;

namespace CanDoItAll.Tests.Unit;

public sealed class AgentPlanningDecisionJournalTests {
    public enum MalformedInput { UnknownMember, MissingMember, NullWork, WrongCase, UnsupportedVersion, UnknownOwner, EmptyHead, InvalidPolicyRevision, EmptyOrigin, NegativeWork }
    public enum Forgery { Target, PolicyHead, Basis, Origin, SpentWork, Phase }
    [Fact]
    public void Complete_decision_round_trips_and_same_revision_accepts_an_independent_copy() {
        var original = AwaitApproval(Respond(Enter(Attach(Begin(Create())))), TimeSpan.FromSeconds(3));
        var json = JsonSerializer.Serialize(original);
        var copy = JsonSerializer.Deserialize<AgentToolJournalRecord>(json)!;
        copy.Validate();
        AgentToolJournalTransitions.ValidatePersistence(original, copy);
        Assert.Equal(json, JsonSerializer.Serialize(copy));
        Assert.Equal(AgentPlanningGenerationState.AwaitingApproval, copy.PlanningDecision!.Current.State);
        Assert.Equal(TimeSpan.FromSeconds(3).Ticks, copy.PlanningDecision.Work.ClosedWorkTicks);
        Assert.Single(copy.PlanningDecision.Current.Steps[0].Publications);
    }

    [Theory]
    [InlineData(MalformedInput.UnknownMember)]
    [InlineData(MalformedInput.MissingMember)]
    [InlineData(MalformedInput.NullWork)]
    [InlineData(MalformedInput.WrongCase)]
    [InlineData(MalformedInput.UnsupportedVersion)]
    [InlineData(MalformedInput.UnknownOwner)]
    [InlineData(MalformedInput.EmptyHead)]
    [InlineData(MalformedInput.InvalidPolicyRevision)]
    [InlineData(MalformedInput.EmptyOrigin)]
    [InlineData(MalformedInput.NegativeWork)]
    public void Strict_codec_rejects_incomplete_or_forged_provenance_and_graph(MalformedInput change) {
        var decision = Respond(Enter(Attach(Begin(Create())))).PlanningDecision!;
        var json = JsonNode.Parse(AgentPlanningDecisionCodec.Serialize(decision))!.AsObject();
        switch (change) {
            case MalformedInput.UnknownMember:
                json["callerApproval"] = true;
                break;
            case MalformedInput.MissingMember:
                json.Remove("automaticReplans");
                break;
            case MalformedInput.NullWork:
                json["work"] = null;
                break;
            case MalformedInput.WrongCase:
                json["DecisionId"] = json["decisionId"]!.DeepClone();
                json.Remove("decisionId");
                break;
            case MalformedInput.UnsupportedVersion:
                json["schemaVersion"] = 2;
                break;
            case MalformedInput.UnknownOwner:
                json["owner"] = 999;
                break;
            case MalformedInput.EmptyHead:
                json["provenance"]!["workspaceHeadRevision"] = Guid.Empty;
                break;
            case MalformedInput.InvalidPolicyRevision:
                json["provenance"]!["catalogDataRevision"] = 0;
                break;
            case MalformedInput.EmptyOrigin:
                json["generations"]![0]!["steps"]![0]!["originDispatchLeaseId"] = Guid.Empty;
                break;
            case MalformedInput.NegativeWork:
                json["work"]!["closedWorkTicks"] = -1;
                break;
        }
        Assert.Throws<JsonException>(() => AgentPlanningDecisionCodec.Deserialize(json.ToJsonString()));
    }

    [Fact]
    public void Strict_codec_rejects_duplicate_members_and_numeric_strings() {
        var json = AgentPlanningDecisionCodec.Serialize(Create().PlanningDecision!);
        Assert.Throws<JsonException>(() => AgentPlanningDecisionCodec.Deserialize(json.Replace("\"schemaVersion\":1",
            "\"schemaVersion\":1,\"schemaVersion\":1", StringComparison.Ordinal)));
        Assert.Throws<JsonException>(() => AgentPlanningDecisionCodec.Deserialize(json.Replace("\"automaticReplans\":0",
            "\"automaticReplans\":\"0\"", StringComparison.Ordinal)));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    public void Older_journal_cannot_acquire_a_planning_decision_by_replacement(int schema) {
        var planning = Create();
        var prior = planning with {
            SchemaVersion = schema, PlanningDecision = null,
            Support = schema == 5 ? AgentToolAdmissionSupport.EffectJournalOnly : AgentToolAdmissionSupport.Recoverable
        };
        prior.Validate();
        Assert.DoesNotContain("PlanningDecision", JsonSerializer.Serialize(prior), StringComparison.Ordinal);
        Assert.Throws<AgentToolAdmissionException>(() => AgentToolJournalTransitions.ValidatePersistence(prior,
            planning with { Revision = prior.Revision + 1 }));
    }

    [Theory]
    [InlineData(ReadBasisMode.RunSnapshot)]
    [InlineData(ReadBasisMode.StepSnapshot)]
    public void New_step_preserves_or_replaces_basis_without_an_uncharged_work_gap(ReadBasisMode mode) {
        var first = Respond(Enter(Attach(Begin(Create(mode: mode)))), publish: false);
        var before = first.PlanningDecision!;
        var next = AgentPlanningDecisionTransitions.AdvanceStep(before, before.Work.Open!.Id,
            first.ActiveDispatchLeaseId!.Value, TimeSpan.FromSeconds(12), Start.AddSeconds(10));
        var advanced = Changed(first, first with { PlanningDecision = next });
        Assert.Equal(TimeSpan.FromSeconds(12).Ticks, next.Work.ClosedWorkTicks);
        Assert.Equal(next.Work.LastSettlement!.EndedAtUtc, next.Work.Open!.OpenedAtUtc);
        Assert.Equal(before.Work.Open.Id, next.Work.LastSettlement.IntervalId);
        var fresh = new AgentPlanningBasisRecord(Guid.NewGuid(), Start.AddSeconds(10), Envelope("{\"tasks\":[\"changed\"]}"));
        if (mode == ReadBasisMode.RunSnapshot) {
            Assert.Throws<AgentPlanningAdmissionException>(() => Attach(advanced, fresh));
            advanced = Attach(advanced, before.Current.Bases[0]);
            Assert.Single(advanced.PlanningDecision!.Current.Bases);
            Assert.Equal(before.Current.Bases[0].Id, advanced.PlanningDecision.Current.Steps[1].BasisId);
        } else {
            Assert.Throws<InvalidDataException>(() => Attach(advanced, before.Current.Bases[0]));
            advanced = Attach(advanced, fresh);
            Assert.Equal(2, advanced.PlanningDecision!.Current.Bases.Length);
            Assert.Equal(fresh.Id, advanced.PlanningDecision.Current.Steps[1].BasisId);
        }
        var second = Respond(Enter(advanced), publish: false);
        Assert.Equal(2, second.ProviderDispatches.Length);
        Assert.Equal(before.Current.Steps[0].ProviderDispatchId, second.PlanningDecision!.Current.Steps[0].ProviderDispatchId);
    }

    [Fact]
    public void Closed_approval_wait_is_excluded_but_original_basis_age_still_expires() {
        var waiting = AwaitApproval(Respond(Enter(Attach(Begin(Create())))), TimeSpan.FromSeconds(7)).PlanningDecision!;
        var resumed = AgentPlanningDecisionTransitions.ResumeEffects(waiting, waiting.Current.Steps[0].Id,
            Guid.NewGuid(), Start.AddHours(2));
        Assert.Equal(waiting.Work.ClosedWorkTicks, resumed.Work.ClosedWorkTicks);
        AgentPlanningDecisionTransitions.RequireBudget(resumed, TimeSpan.FromSeconds(1), Start.AddHours(2).AddSeconds(1));
        Assert.Equal(AgentPlanningAdmissionRefusal.BasisExpired, Assert.Throws<AgentPlanningAdmissionException>(() =>
            AgentPlanningDecisionTransitions.ResumeEffects(waiting, waiting.Current.Steps[0].Id, Guid.NewGuid(), Start.AddDays(1))).Reason);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Work_limit_uses_greater_of_monotonic_and_utc_and_refuses_the_exact_bound(bool monotonic) {
        var decision = Attach(Begin(Create())).PlanningDecision!;
        var elapsed = monotonic ? TimeSpan.FromMinutes(5) : TimeSpan.Zero;
        var now = monotonic ? Start.AddSeconds(1) : Start.AddMinutes(5);
        Assert.Equal(AgentPlanningAdmissionRefusal.WorkBudgetExhausted, Assert.Throws<AgentPlanningAdmissionException>(() =>
            AgentPlanningDecisionTransitions.EnterProvider(decision, decision.Current.Steps[0].Id,
                new(Guid.NewGuid()), elapsed, now)).Reason);
    }

    [Fact]
    public void Clock_rollback_refuses_new_work_but_cancellation_preserves_the_open_accounting_record() {
        var decision = Attach(Begin(Create(), Start.AddSeconds(1))).PlanningDecision!;
        Assert.Equal(AgentPlanningAdmissionRefusal.ClockInvalid, Assert.Throws<AgentPlanningAdmissionException>(() =>
            AgentPlanningDecisionTransitions.RequireBudget(decision, TimeSpan.FromSeconds(1), Start)).Reason);
        Assert.Equal(AgentPlanningAdmissionRefusal.ClockInvalid, Assert.Throws<AgentPlanningAdmissionException>(() =>
            AgentPlanningDecisionTransitions.RequireBudget(decision, TimeSpan.FromTicks(-1), Start.AddSeconds(2))).Reason);
        var cancelled = AgentPlanningDecisionTransitions.Cancel(decision);
        Assert.Equal(AgentPlanningGenerationState.Cancelled, cancelled.Current.State);
        Assert.Equal(decision.Work.Open, cancelled.Work.Open);
        Assert.Equal(decision.Work.LastObservedAtUtc, cancelled.Work.LastObservedAtUtc);
        Assert.Equal(decision.Work.ClosedWorkTicks, cancelled.Work.ClosedWorkTicks);
    }

    [Fact]
    public void Late_response_and_settlement_add_work_without_reviving_a_cancelled_decision() {
        var entered = Enter(Attach(Begin(Create())));
        var cancelled = Changed(entered, entered with { PlanningDecision = AgentPlanningDecisionTransitions.Cancel(entered.PlanningDecision!) });
        var response = Respond(cancelled, publish: false, at: Start.AddSeconds(2));
        var decision = response.PlanningDecision!;
        var settled = AgentPlanningDecisionTransitions.Settle(decision, decision.Work.Open!.Id, TimeSpan.FromSeconds(3),
            Start.AddSeconds(2), AgentPlanningWorkSettlementReason.Terminal, AgentPlanningGenerationState.Cancelled);
        Changed(response, response with { PlanningDecision = settled });
        Assert.Equal(TimeSpan.FromSeconds(3).Ticks, settled.Work.ClosedWorkTicks);
        Assert.Null(settled.Work.Open);
        Assert.Equal(AgentPlanningGenerationState.Cancelled, settled.Current.State);
        Assert.Throws<AgentPlanningAdmissionException>(() => AgentPlanningDecisionTransitions.Settle(decision, decision.Work.Open.Id,
            TimeSpan.FromSeconds(3), Start.AddSeconds(3), AgentPlanningWorkSettlementReason.Terminal, AgentPlanningGenerationState.Completed));
    }

    [Fact]
    public void Refresh_refuses_exhausted_step_capacity_before_retiring_the_last_review() {
        var journal = Begin(Create());
        for (var index = 0; index < AgentPlanningDecisionRecord.MaximumSteps; index++) {
            journal = Respond(Enter(Attach(journal)), publish: index == AgentPlanningDecisionRecord.MaximumSteps - 1);
            if (index < AgentPlanningDecisionRecord.MaximumSteps - 1) {
                var decision = journal.PlanningDecision!;
                journal = Changed(journal, journal with {
                    PlanningDecision = AgentPlanningDecisionTransitions.AdvanceStep(decision, decision.Work.Open!.Id,
                        journal.ActiveDispatchLeaseId!.Value, TimeSpan.Zero, Start)
                });
            }
        }
        var waiting = AwaitApproval(journal, TimeSpan.Zero).PlanningDecision!;
        Assert.Equal(AgentPlanningAdmissionRefusal.HistoryCapacity, Assert.Throws<AgentPlanningAdmissionException>(() =>
            AgentPlanningDecisionTransitions.NewGeneration(waiting, automatic: false, Start)).Reason);
        Assert.Equal(AgentPlanningGenerationState.AwaitingApproval, waiting.Current.State);
        Assert.Single(waiting.Current.Steps[^1].Publications);
    }

    [Fact]
    public void Automatic_replan_spends_allowance_once_and_never_resets_work() {
        var journal = AwaitApproval(Respond(Enter(Attach(Begin(Create(replans: 1))))), TimeSpan.FromSeconds(8));
        var decision = journal.PlanningDecision!;
        var next = AgentPlanningDecisionTransitions.NewGeneration(decision, automatic: true, Start.AddMinutes(1));
        Assert.Throws<InvalidDataException>(() => Changed(journal, journal with { PlanningDecision = next }));
        var retired = AgentToolJournalTransitions.RejectUndispatchedOnCancellation(journal);
        var refreshed = Changed(journal, retired with { PlanningDecision = next });
        Assert.Equal(1, next.AutomaticReplans);
        Assert.Equal(decision.Work.ClosedWorkTicks, next.Work.ClosedWorkTicks);
        Assert.Equal(AgentPlanningGenerationState.Superseded, next.Generations[0].State);
        var restarted = Respond(Enter(Attach(Begin(refreshed, Start.AddMinutes(1)))));
        var second = AwaitApproval(restarted, TimeSpan.FromSeconds(2));
        Assert.Equal(AgentPlanningAdmissionRefusal.ReplanLimit, Assert.Throws<AgentPlanningAdmissionException>(() =>
            AgentPlanningDecisionTransitions.NewGeneration(second.PlanningDecision!, automatic: true, Start.AddMinutes(2))).Reason);
        Assert.Equal(TimeSpan.FromSeconds(10).Ticks, second.PlanningDecision!.Work.ClosedWorkTicks);
    }

    [Theory]
    [InlineData(Forgery.Target)]
    [InlineData(Forgery.PolicyHead)]
    [InlineData(Forgery.Basis)]
    [InlineData(Forgery.Origin)]
    [InlineData(Forgery.SpentWork)]
    [InlineData(Forgery.Phase)]
    public void Full_record_replacement_cannot_rewrite_original_facts_or_spent_work(Forgery change) {
        var journal = AwaitApproval(Respond(Enter(Attach(Begin(Create())))), TimeSpan.FromSeconds(8));
        var current = journal.PlanningDecision!;
        var changed = change switch {
            Forgery.Target => current with { OriginalTarget = Envelope("{\"target\":\"different\"}") },
            Forgery.PolicyHead => current with { Provenance = current.Provenance! with { WorkspaceHeadRevision = Guid.NewGuid() } },
            Forgery.Basis => current with { Generations = current.Generations.SetItem(0, current.Current with {
                Bases = [current.Current.Bases[0] with { CapturedAtUtc = Start.AddSeconds(1) }]
            }) },
            Forgery.Origin => current with { Generations = current.Generations.SetItem(0, current.Current with {
                Steps = [current.Current.Steps[0] with { OriginDispatchLeaseId = Guid.NewGuid() }]
            }) },
            Forgery.SpentWork => current with { Work = current.Work with { ClosedWorkTicks = TimeSpan.FromSeconds(9).Ticks } },
            Forgery.Phase => current with { Generations = current.Generations.SetItem(0, current.Current with {
                State = AgentPlanningGenerationState.Completed
            }) },
            _ => throw new ArgumentOutOfRangeException(nameof(change))
        };
        if (change is Forgery.Origin or Forgery.Phase) {
            Assert.Throws<InvalidDataException>(() => Changed(journal, journal with { PlanningDecision = changed }));
        } else {
            Assert.Throws<AgentToolAdmissionException>(() => Changed(journal, journal with { PlanningDecision = changed }));
        }
    }

    [Fact]
    public void Counter_overflow_and_repeated_settlement_cannot_reset_spent_work() {
        var original = Create().PlanningDecision!;
        var exhaustedCounter = original with { Work = original.Work with { Revision = long.MaxValue } };
        Assert.Throws<OverflowException>(() => AgentPlanningDecisionTransitions.BeginStep(exhaustedCounter, Guid.NewGuid(), Start));
        var response = Respond(Enter(Attach(Begin(Create())))).PlanningDecision!;
        var settled = AgentPlanningDecisionTransitions.Settle(response, response.Work.Open!.Id, TimeSpan.FromSeconds(2),
            Start.AddSeconds(2), AgentPlanningWorkSettlementReason.AwaitingApproval, AgentPlanningGenerationState.AwaitingApproval);
        Assert.Throws<AgentPlanningAdmissionException>(() => AgentPlanningDecisionTransitions.Settle(settled,
            response.Work.Open.Id, TimeSpan.FromSeconds(2), Start.AddSeconds(3),
            AgentPlanningWorkSettlementReason.AwaitingApproval, AgentPlanningGenerationState.AwaitingApproval));
        Assert.Equal(TimeSpan.FromSeconds(2).Ticks, settled.Work.ClosedWorkTicks);
    }

    [Fact]
    public void Planning_payload_participates_in_the_real_aggregate_journal_size_bound() {
        var first = Respond(Enter(Attach(Begin(Create()))), publish: false);
        var advanced = Changed(first, first with {
            PlanningDecision = AgentPlanningDecisionTransitions.AdvanceStep(first.PlanningDecision!,
                first.PlanningDecision!.Work.Open!.Id, first.ActiveDispatchLeaseId!.Value, TimeSpan.Zero, Start)
        });
        var journal = Respond(Enter(Attach(advanced)));
        var large = new string('x', 6 * 1024 * 1024);
        var decision = journal.PlanningDecision! with { OriginalTarget = Envelope(JsonSerializer.Serialize(large)) };
        Assert.True(AgentPlanningDecisionCodec.Serialize(decision).Length < AgentPlanningDecisionRecord.MaximumUtf8Bytes);
        var oversized = journal with {
            PlanningDecision = decision,
            OriginalInput = journal.OriginalInput! with { Content = large },
            Batches = journal.Batches.Select(batch => batch with { Response = Envelope(JsonSerializer.Serialize(large)) }).ToImmutableArray()
        };
        (oversized with { SchemaVersion = AgentToolJournalRecord.EffectJournalSchemaVersion, PlanningDecision = null }).Validate();
        Assert.Equal("The tool admission journal exceeds its supported serialized payload bound.",
            Assert.Throws<InvalidDataException>(oversized.Validate).Message);
    }
}
