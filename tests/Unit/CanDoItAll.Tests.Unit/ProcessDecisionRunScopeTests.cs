using System.Text.Json;
using System.Text.Json.Nodes;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using static CanDoItAll.Processes.Runtime.ProcessDecisionRunScopeTransitions;

namespace CanDoItAll.Tests.Unit.Processes;

public sealed class ProcessDecisionRunScopeTests {
    [Fact]
    public void First_capture_has_no_fabricated_generation_and_retains_original_time_and_root_funding() {
        var root = Root();
        var initial = Create(root.MaterialScope!);
        Assert.Null(initial.Material);
        Assert.Equal(ProcessDecisionRunScopePhase.AwaitingCapture, initial.Phase);
        var (capturing, reserved, material) = Capture(initial, root);
        Assert.Equal(ProcessDecisionRunScopePhase.Capturing, capturing.Phase);
        Assert.Null(capturing.Material);
        Assert.Throws<InvalidDataException>(() => BeginCapture(initial, initial.Expectation, capturing.Capture!,
            root with { MaterialScope = root.MaterialScope! with { ScopeId = new(Guid.NewGuid()) } }, reserved, [reserved.Allocation], []));
        var state = CompleteCapture(capturing, capturing.Expectation, material, Settle(reserved));
        state.Validate();
        Assert.Equal(3, state.Revision);
        Assert.Equal(1, state.Material!.Stamp.Reference.Generation);
        Assert.Equal(material.CapturedAtUtc, state.Material.CapturedAtUtc);
        Assert.Equal(root.Funding, state.Admission.Funding);
    }

    [Theory]
    [InlineData(ProcessDecisionRunScopePhase.AwaitingCapture)]
    [InlineData(ProcessDecisionRunScopePhase.Capturing)]
    [InlineData(ProcessDecisionRunScopePhase.Reconciling)]
    [InlineData(ProcessDecisionRunScopePhase.Completed)]
    [InlineData(ProcessDecisionRunScopePhase.Cancelled)]
    public void Every_nonactive_scope_refuses_a_new_member(ProcessDecisionRunScopePhase phase) {
        var root = Root();
        var current = Active(root);
        var state = phase switch {
            ProcessDecisionRunScopePhase.AwaitingCapture => Create(root.MaterialScope!),
            ProcessDecisionRunScopePhase.Capturing => Capture(Create(root.MaterialScope!), root).State,
            _ => current with { Phase = phase }
        };
        var failure = Assert.Throws<ProcessDecisionRunScopeException>(() => Join(state, state.Expectation, Member(current)));
        Assert.Equal(ProcessDecisionRunScopeRefusal.InvalidPhase, failure.Reason);
    }

    [Fact]
    public void Joined_members_are_bounded_and_replay_preserves_the_original_occurrence() {
        var current = Active(Root());
        var first = Member(current);
        for (var index = 0; index < ProcessDecisionRunScopeState.MaximumMemberships; index++) {
            current = Join(current, current.Expectation, index == 0 ? first : Member(current)).State;
        }
        current.Validate();
        Assert.Equal(256, current.CurrentMemberships);
        var replay = Join(current, current.Expectation, first, first);
        Assert.False(replay.Changed);
        Assert.Equal(current, replay.State);
        Assert.Equal(ProcessDecisionRunScopeRefusal.MembershipCapacity,
            Assert.Throws<ProcessDecisionRunScopeException>(() => Join(current, current.Expectation, Member(current))).Reason);
        Assert.Equal(ProcessDecisionRunScopeRefusal.OriginalMembershipChanged,
            Assert.Throws<ProcessDecisionRunScopeException>(() => Join(current, current.Expectation, first with { RunId = ProcessRunId.New() }, first)).Reason);
    }

    [Fact]
    public void Stale_scope_revision_cannot_join_or_begin_reconciliation() {
        var current = Active(Root());
        var next = Join(current, current.Expectation, Member(current)).State;
        Assert.Equal(ProcessDecisionRunScopeRefusal.StaleExpectation,
            Assert.Throws<ProcessDecisionRunScopeException>(() => Join(next, current.Expectation, Member(next))).Reason);
        Assert.Throws<ProcessDecisionRunScopeException>(() => BeginReconciliation(next, current.Expectation));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Foreign_scope_generation_or_material_never_authorizes_publication(int change) {
        var current = Active(Root());
        var member = Member(current);
        member = member with { Material = change switch {
            0 => member.Material with { ScopeId = new(Guid.NewGuid()) },
            1 => member.Material with { Generation = 2 },
            _ => member.Material with { MaterialId = new(Guid.NewGuid()) }
        } };
        Assert.Equal(ProcessDecisionRunScopeRefusal.OriginalMembershipChanged,
            Assert.Throws<ProcessDecisionRunScopeException>(() => RequirePublication(current, member)).Reason);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Reconciliation_requires_the_complete_closed_membership_and_cleared_claims(int change) {
        var root = Root();
        var current = Active(root);
        var member = Member(current);
        current = Join(current, current.Expectation, member).State;
        current = BeginReconciliation(current, current.Expectation);
        var decision = ProcessDecisionTransitions.Create(member.Decision.DecisionId, member.Material.MaterialId, new(Guid.NewGuid())) with {
            Phase = change == 1 ? ProcessDecisionPhase.Active : ProcessDecisionPhase.Reconciling,
            ChildCount = change == 3 ? 1 : 0, UnclosedChildCount = change == 3 ? 1 : 0, Revision = 2
        };
        ProcessDecisionRunScopeMemberObservation[] members = change == 0 ? [] : [new(member, decision, change == 2)];
        var balance = DecisionBudgetTransitions.Create(root.BudgetId, root.Policy.Budget);
        var reserved = DecisionBudgetTransitions.Reserve(balance, new(Guid.NewGuid()), new(10, 0));
        var capture = new ProcessDecisionRunScopeCapture(reserved.Allocation.Id, new(Guid.NewGuid()), ProcessDecisionRunCaptureKind.ManagerRefresh);
        Assert.Equal(change == 0 ? ProcessDecisionRunScopeRefusal.IncompleteMembership : ProcessDecisionRunScopeRefusal.UnclosedWork,
            Assert.Throws<ProcessDecisionRunScopeException>(() => BeginCapture(current, current.Expectation, capture, root, reserved, [reserved.Allocation], members)).Reason);
    }

    [Fact]
    public void Unknown_zero_work_reservation_is_not_closed_work() {
        var root = Root(OnBasisChange.Replan);
        var initial = Create(root.MaterialScope!);
        var unknown = DecisionBudgetTransitions.Reserve(DecisionBudgetTransitions.Create(root.BudgetId, root.Policy.Budget), new(Guid.NewGuid()), new(0, 1));
        var reserved = DecisionBudgetTransitions.Reserve(unknown.Balance, new(Guid.NewGuid()), new(10, 0));
        Assert.Equal(ProcessDecisionRunScopeRefusal.UnclosedWork,
            Assert.Throws<ProcessDecisionRunScopeException>(() => BeginCapture(initial, initial.Expectation,
                new(reserved.Allocation.Id, new(Guid.NewGuid()), ProcessDecisionRunCaptureKind.Initial), root, reserved, [unknown.Allocation, reserved.Allocation], [])).Reason);
    }

    [Fact]
    public void Capture_completion_and_abort_require_closed_original_usage_and_preserve_overrun() {
        var root = Root();
        var (state, reserved, material) = Capture(Create(root.MaterialScope!), root);
        Assert.Throws<InvalidDataException>(() => CompleteCapture(state, state.Expectation, material, reserved));
        Assert.Throws<InvalidDataException>(() => AbortCapture(state, state.Expectation, reserved));
        var overrun = Settle(reserved, 11);
        Assert.Equal(DecisionBudgetRefusal.Overrun, Assert.Throws<DecisionBudgetException>(() => CompleteCapture(state, state.Expectation, material, overrun)).Reason);
        var aborted = AbortCapture(state, state.Expectation, overrun);
        aborted.Validate();
        Assert.Null(aborted.Material);
        Assert.Null(aborted.Capture);
        Assert.Equal(ProcessDecisionRunScopePhase.AwaitingCapture, aborted.Phase);
        Assert.True(overrun.Balance.OverrunDetected);
        Assert.Equal(11, overrun.Balance.ConsumedWorkTicks);
        Assert.Equal(1, overrun.Balance.AllocationCount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Capture_rejects_replaced_material_generation_scope_or_content(int change) {
        var root = Root();
        var (state, reserved, material) = Capture(Create(root.MaterialScope!), root);
        material = ProcessDecisionMaterial.FromContent(change == 2 ? material.Scope with { WorkspaceNamespaceId = Guid.NewGuid() } : material.Scope,
            change switch { 0 => material.Reference with { MaterialId = new(Guid.NewGuid()) }, 1 => material.Reference with { Generation = 2 }, _ => material.Reference },
            change == 3 ? new(Guid.NewGuid(), 1) : material.ContentContract!, material.CapturedAtUtc, material.PayloadJson);
        Assert.Equal(ProcessDecisionRunScopeRefusal.OriginalCaptureChanged,
            Assert.Throws<ProcessDecisionRunScopeException>(() => CompleteCapture(state, state.Expectation, material, Settle(reserved))).Reason);
    }

    [Theory]
    [InlineData(ProcessDecisionRunCaptureKind.ManagerRefresh, 0)]
    [InlineData(ProcessDecisionRunCaptureKind.AutomaticReplan, 1)]
    public void Parent_refresh_advances_one_material_and_charges_the_root_once(ProcessDecisionRunCaptureKind kind, int replans) {
        var root = Root(OnBasisChange.Replan);
        var current = Active(root);
        var prior = Member(current);
        current = BeginReconciliation(current, current.Expectation);
        var (capturing, reserved, material) = Capture(current, root, kind);
        var next = CompleteCapture(capturing, capturing.Expectation, material, Settle(reserved, 3, replans));
        next.Validate();
        Assert.Equal(2, next.Material!.Stamp.Reference.Generation);
        Assert.Equal(replans, reserved.Allocation.Grant.Replans);
        Assert.Throws<ProcessDecisionRunScopeException>(() => RequirePublication(next, prior));
        Assert.Equal(0, next.CurrentMemberships);
    }

    [Fact]
    public void Shared_scope_and_step_wire_versions_require_exact_original_binding() {
        var root = Root();
        var encoded = ProcessDecisionRunAdmissionCodec.Serialize(root);
        Assert.Equal(2, JsonNode.Parse(encoded)!["schemaVersion"]!.GetValue<int>());
        var reopened = ProcessDecisionRunAdmissionCodec.Deserialize(encoded);
        Assert.Equal(root.MaterialScope, reopened.MaterialScope);
        var child = ProcessDecisionRunPolicyFixture.Child(root, ProcessStepInstanceId.New());
        var encodedChild = ProcessDecisionDataPolicyAdmissionCodec.Serialize(child);
        Assert.Equal(5, JsonNode.Parse(encodedChild)!["schemaVersion"]!.GetValue<int>());
        reopened.RequireChild(ProcessDecisionDataPolicyAdmissionCodec.Deserialize(encodedChild));
        Assert.Throws<InvalidDataException>(() => root.RequireChild(child with { SharedRunScopeId = null }));
        Assert.Throws<InvalidDataException>(() => root.RequireChild(child with { SharedRunScopeId = new(Guid.NewGuid()) }));
        Assert.Throws<InvalidDataException>(() => root.RequireChild(child with { ContentContract = new(Guid.NewGuid(), 1) }));
    }

    [Theory]
    [InlineData(true, 0)]
    [InlineData(true, 1)]
    [InlineData(true, 2)]
    [InlineData(false, 0)]
    [InlineData(false, 1)]
    [InlineData(false, 2)]
    public void Scope_encoding_cannot_be_removed_downgraded_or_silently_ignored(bool parent, int change) {
        var root = Root();
        var json = JsonNode.Parse(parent ? ProcessDecisionRunAdmissionCodec.Serialize(root)
            : ProcessDecisionDataPolicyAdmissionCodec.Serialize(ProcessDecisionRunPolicyFixture.Child(root, ProcessStepInstanceId.New())))!.AsObject();
        var field = parent ? "materialScope" : "sharedRunScopeId";
        if (change == 0) {
            json.Remove(field);
        } else if (change == 1) {
            json["schemaVersion"] = parent ? 1 : 4;
        } else {
            json[field] = null;
        }
        Assert.Throws<JsonException>(() => {
            if (parent) {
                ProcessDecisionRunAdmissionCodec.Deserialize(json.ToJsonString());
            } else {
                ProcessDecisionDataPolicyAdmissionCodec.Deserialize(json.ToJsonString());
            }
        });
    }

    [Fact]
    public void Membership_and_material_history_have_independent_finite_bounds() {
        var root = Root();
        var current = Active(root);
        current = current with { Revision = 100, Material = current.Material! with { Stamp = current.Material.Stamp with {
            Reference = current.Material.Stamp.Reference with { Generation = ProcessDecisionGeneration.MaximumGenerations }
        } } };
        current = BeginReconciliation(current, current.Expectation);
        Assert.Equal(ProcessDecisionRunScopeRefusal.GenerationCapacity,
            Assert.Throws<ProcessDecisionRunScopeException>(() => Capture(current, root, ProcessDecisionRunCaptureKind.ManagerRefresh)).Reason);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Terminal_result_closes_active_scope_with_canonical_version_two_receipt(bool cancelled) {
        var root = Root();
        var (capturing, reserved, material) = Capture(Create(root.MaterialScope!), root);
        var settled = Settle(reserved);
        var current = CompleteCapture(capturing, capturing.Expectation, material, settled);
        var next = FinishResult(current, current.Expectation, cancelled, [], settled.Balance, [settled.Allocation]);
        ProcessDecisionRunScopeReceipt receipt = new(new(current.Expectation, cancelled
            ? ProcessDecisionRunScopeCommandKind.CancelResult : ProcessDecisionRunScopeCommandKind.CompleteResult), next);
        var encoded = ProcessDecisionRunScopeReceiptCodec.Write(receipt)!;
        Assert.Equal(receipt, ProcessDecisionRunScopeReceiptCodec.Read(encoded));
        var document = JsonNode.Parse(encoded)!.AsObject();
        Assert.Equal(2, document["version"]!.GetValue<int>());
        document["version"] = 1;
        Assert.Throws<InvalidDataException>(() => ProcessDecisionRunScopeReceiptCodec.Read(document.ToJsonString()));
        Assert.Equal(current.Revision + 1, next.Revision);
        Assert.Equal(cancelled ? ProcessDecisionRunScopePhase.Cancelled : ProcessDecisionRunScopePhase.Completed, next.Phase);
        if (cancelled) {
            Assert.Equal(ProcessDecisionRunScopeRefusal.InvalidPhase,
                Assert.Throws<ProcessDecisionRunScopeException>(() => Finish(current, current.Expectation, true, [],
                    settled.Balance, [settled.Allocation])).Reason);
        }
        var oldCommand = receipt with { Command = receipt.Command with { Kind = cancelled
            ? ProcessDecisionRunScopeCommandKind.Cancel : ProcessDecisionRunScopeCommandKind.Complete } };
        var legacy = ProcessDecisionRunScopeReceiptCodec.Write(oldCommand)!;
        Assert.StartsWith("{\"version\":1,", legacy, StringComparison.Ordinal);
        Assert.Equal(legacy, ProcessDecisionRunScopeReceiptCodec.Write(ProcessDecisionRunScopeReceiptCodec.Read(legacy)));
    }

    [Theory]
    [InlineData(TerminalRefusal.StaleScope)]
    [InlineData(TerminalRefusal.OpenClaim)]
    [InlineData(TerminalRefusal.OpenDecision)]
    [InlineData(TerminalRefusal.OpenAllocation)]
    public void Terminal_scope_transition_cannot_skip_original_quiescence(TerminalRefusal refusal) {
        var root = Root();
        var (capturing, reserved, material) = Capture(Create(root.MaterialScope!), root);
        var settled = Settle(reserved);
        var current = CompleteCapture(capturing, capturing.Expectation, material, settled);
        var member = Member(current);
        current = Join(current, current.Expectation, member).State;
        ProcessDecisionGeneration decision = new(member.Decision, 1,
            refusal == TerminalRefusal.OpenDecision ? ProcessDecisionPhase.Active : ProcessDecisionPhase.Completed,
            material.Reference.MaterialId, new(Guid.NewGuid()), 0, 0);
        var balance = settled.Balance;
        List<DecisionBudgetAllocation> allocations = [settled.Allocation];
        if (refusal == TerminalRefusal.OpenAllocation) {
            var open = DecisionBudgetTransitions.Reserve(balance, new(Guid.NewGuid()), new(1, 0));
            balance = open.Balance;
            allocations.Add(open.Allocation);
        }
        var expected = refusal == TerminalRefusal.StaleScope ? current.Expectation with { Revision = current.Revision - 1 } : current.Expectation;
        var failure = Assert.Throws<ProcessDecisionRunScopeException>(() => FinishResult(current, expected, true,
            [new(member, decision, refusal == TerminalRefusal.OpenClaim)], balance, allocations));
        Assert.Equal(refusal == TerminalRefusal.StaleScope ? ProcessDecisionRunScopeRefusal.StaleExpectation
            : ProcessDecisionRunScopeRefusal.UnclosedWork, failure.Reason);
    }

    [Fact]
    public void Uncaptured_cancelled_scope_does_not_fabricate_material_or_allow_a_live_capture() {
        var root = Root();
        var current = Create(root.MaterialScope!);
        var balance = DecisionBudgetTransitions.Create(root.BudgetId, root.Policy.Budget);
        var next = FinishResult(current, current.Expectation, true, [], balance, []);
        next.Validate();
        Assert.Null(next.Material);
        var (capturing, reserved, _) = Capture(current, root);
        Assert.Equal(ProcessDecisionRunScopeRefusal.InvalidPhase,
            Assert.Throws<ProcessDecisionRunScopeException>(() => FinishResult(capturing, capturing.Expectation, true, [],
                reserved.Balance, [reserved.Allocation])).Reason);
    }

    public enum TerminalRefusal { StaleScope, OpenClaim, OpenDecision, OpenAllocation }

    private static ProcessDecisionRunAdmission Root(OnBasisChange change = OnBasisChange.PauseForReview)
        => ProcessDecisionRunPolicyFixture.WithSharedScope(ProcessDecisionRunPolicyFixture.Root(ProcessRunId.New(), ProcessDefinitionVersionId.New(),
            Guid.NewGuid(), ReadBasisMode.RunSnapshot, change));

    private static ProcessDecisionRunScopeState Active(ProcessDecisionRunAdmission root) {
        var (state, reserved, material) = Capture(Create(root.MaterialScope!), root);
        return CompleteCapture(state, state.Expectation, material, Settle(reserved));
    }

    private static (ProcessDecisionRunScopeState State, DecisionBudgetChange Reserved, ProcessDecisionMaterial Material) Capture(
        ProcessDecisionRunScopeState current, ProcessDecisionRunAdmission root, ProcessDecisionRunCaptureKind kind = ProcessDecisionRunCaptureKind.Initial) {
        var reserved = DecisionBudgetTransitions.Reserve(DecisionBudgetTransitions.Create(root.BudgetId, root.Policy.Budget), new(Guid.NewGuid()),
            new(10, kind == ProcessDecisionRunCaptureKind.AutomaticReplan ? 1 : 0));
        var materialId = new ProcessDecisionMaterialId(Guid.NewGuid());
        var next = BeginCapture(current, current.Expectation, new(reserved.Allocation.Id, materialId, kind), root, reserved, [reserved.Allocation], []);
        next.Validate();
        var material = ProcessDecisionMaterial.FromContent(root.MaterialScope!.MaterialScope,
            new(root.MaterialScope.ScopeId, (current.Material?.Stamp.Reference.Generation ?? 0) + 1, materialId), root.MaterialScope.ContentContract,
            ProcessProjectAdmissionFixture.Now, "{\"facts\":[]}");
        return (next, reserved, material);
    }

    private static DecisionBudgetChange Settle(DecisionBudgetChange reserved, long work = 3, int replans = 0)
        => DecisionBudgetTransitions.Settle(reserved.Balance, reserved.Allocation, new(1, new(work, replans)));

    private static ProcessDecisionRunScopeMembership Member(ProcessDecisionRunScopeState current)
        => new(current.Material!.Stamp.Reference, current.Admission.Funding.RootRunId, ProcessStepInstanceId.New(), new(new(Guid.NewGuid()), 1));
}
