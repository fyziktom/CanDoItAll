using System.Text.Json;
using System.Text.Json.Nodes;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;

namespace CanDoItAll.Tests.Unit.Processes;

public sealed class ProcessDecisionRunBudgetTests {
    [Theory]
    [InlineData(ReadBasisMode.RunSnapshot)]
    [InlineData(ReadBasisMode.StepSnapshot)]
    public void Root_and_funded_child_keep_original_provenance_and_distinct_envelopes(ReadBasisMode read) {
        var root = Root(read);
        var encoded = ProcessDecisionRunAdmissionCodec.Serialize(root);
        var reopened = ProcessDecisionRunAdmissionCodec.Deserialize(encoded);
        Assert.Equal(encoded, ProcessDecisionRunAdmissionCodec.Serialize(reopened));
        Assert.Equal(root.Funding, reopened.Funding);
        Assert.Equal(root.Policy, reopened.Policy);
        Assert.Equal(500, reopened.Policy.Budget.MaximumWorkDuration.Ticks);
        var child = ProcessDecisionRunPolicyFixture.Child(reopened, ProcessStepInstanceId.New());
        var childJson = ProcessDecisionDataPolicyAdmissionCodec.Serialize(child);
        Assert.Equal(4, JsonNode.Parse(childJson)!["schemaVersion"]!.GetValue<int>());
        reopened.RequireChild(ProcessDecisionDataPolicyAdmissionCodec.Deserialize(childJson));
        Assert.Equal(childJson, ProcessDecisionDataPolicyAdmissionCodec.Serialize(ProcessDecisionDataPolicyAdmissionCodec.Deserialize(childJson)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(10)]
    [InlineData(11)]
    public void Root_codec_refuses_missing_invalid_or_unsupported_original_facts(int change) {
        var json = JsonNode.Parse(ProcessDecisionRunAdmissionCodec.Serialize(Root()))!.AsObject();
        switch (change) {
            case 0:
                json["schemaVersion"] = 2;
                break;
            case 1:
                json.Remove("runId");
                break;
            case < 8:
                json[new[] { "runId", "budgetId", "databaseProfileId", "workspaceNamespaceId", "workspaceHeadRevision", "definitionVersionId" }[change - 2]] = Guid.Empty;
                break;
            default:
                json[new[] { "workspacePolicy", "capabilities", "policy", "enclosingBudgets" }[change - 8]] = null;
                break;
        }
        Assert.Throws<JsonException>(() => ProcessDecisionRunAdmissionCodec.Deserialize(json.ToJsonString()));
    }

    [Fact]
    public void Root_codec_rejects_duplicate_unknown_and_oversized_records() {
        var encoded = ProcessDecisionRunAdmissionCodec.Serialize(Root());
        Assert.Throws<JsonException>(() => ProcessDecisionRunAdmissionCodec.Deserialize(encoded.Insert(1, "\"schemaVersion\":1,")));
        Assert.Throws<JsonException>(() => ProcessDecisionRunAdmissionCodec.Deserialize(encoded.Insert(1, "\"unknown\":null,")));
        Assert.Throws<JsonException>(() => ProcessDecisionRunAdmissionCodec.Deserialize(encoded.Insert(1, new string(' ', ProcessDecisionRunAdmission.MaximumUtf8Bytes))));
    }

    [Fact]
    public void Root_policy_cannot_be_replaced_independently_of_its_original_limits() {
        var root = Root();
        var changed = root with { Policy = new(root.Policy.ReadBasisMode, root.Policy.PublicationFreshness, root.Policy.OnBasisChange,
            new(0, TimeSpan.FromTicks(501), root.Policy.Budget.MaximumBasisAge)) };
        Assert.Throws<InvalidDataException>(() => ProcessDecisionRunAdmissionCodec.Serialize(changed));
    }

    [Fact]
    public void Root_enclosing_provenance_is_bounded_without_truncation() {
        var root = Root();
        var allowed = root with { EnclosingBudgets = [.. Enumerable.Repeat(root.EnclosingBudgets[0], ProcessDecisionDataPolicyAdmission.MaximumEnclosingPolicies)] };
        Assert.NotEmpty(ProcessDecisionRunAdmissionCodec.Serialize(allowed));
        Assert.Throws<InvalidDataException>(() => ProcessDecisionRunAdmissionCodec.Serialize(allowed with { EnclosingBudgets = [.. allowed.EnclosingBudgets, root.Policy.Budget] }));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Root_admission_cannot_be_owned_by_another_run_descendant_or_definition(int change) {
        var initial = ProcessProjectAdmissionFixture.Initial();
        var root = ProcessDecisionRunPolicyFixture.Root(initial.OriginalState.RunId, initial.InitialPlan!.Definition.VersionId, Guid.NewGuid());
        root.RequireRoot(initial.OriginalState, initial.InitialPlan.Definition.VersionId);
        var state = change switch {
            0 => initial.OriginalState with { RunId = ProcessRunId.New() },
            1 => initial.OriginalState with { RootRunId = ProcessRunId.New() },
            _ => initial.OriginalState
        };
        Assert.Throws<InvalidDataException>(() => root.RequireRoot(state,
            change == 2 ? ProcessDefinitionVersionId.New() : initial.InitialPlan.Definition.VersionId));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void Child_cannot_drop_or_replace_original_root_funding_or_provenance(int change) {
        var root = Root();
        var child = ProcessDecisionRunPolicyFixture.Child(root, ProcessStepInstanceId.New());
        var changed = change switch {
            0 => child with { Funding = null },
            1 => child with { Funding = new(ProcessRunId.New(), root.BudgetId) },
            2 => child with { Funding = new(root.RunId, new(Guid.NewGuid())) },
            3 => child with { WorkspaceHeadRevision = Guid.NewGuid() },
            4 => child with { WorkspaceNamespaceId = Guid.NewGuid() },
            _ => child with { EnclosingBudgets = [] }
        };
        Assert.Throws<InvalidDataException>(() => root.RequireChild(changed));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Shared_run_policy_remains_parent_owned_even_if_a_child_owner_supports_a_different_policy(int change) {
        var root = Root(ReadBasisMode.RunSnapshot);
        var child = ProcessDecisionRunPolicyFixture.Child(root, ProcessStepInstanceId.New());
        var preference = change switch {
            0 => new DecisionDataPolicyOverride(ReadBasisMode: ReadBasisMode.StepSnapshot),
            1 => new DecisionDataPolicyOverride(PublicationFreshness: PublicationFreshness.PinnedObservation),
            _ => new DecisionDataPolicyOverride(OnBasisChange: OnBasisChange.Fail)
        };
        var owner = child.OwnerCapabilities with { SupportsPinnedObservation = true, MinimumPublicationFreshness = PublicationFreshness.PinnedObservation };
        child = child with { StepOverride = preference, OwnerCapabilities = owner,
            Policy = DecisionDataPolicyResolver.Resolve(child.WorkspacePolicy, owner, definition: child.DefinitionOverride,
                step: preference, authorizedInvocation: child.AuthorizedInvocationOverride, enclosingBudgets: child.EnclosingBudgets) };
        child.Validate();
        Assert.Throws<InvalidDataException>(() => root.RequireChild(child));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void Funded_envelope_cannot_drop_downgrade_or_corrupt_its_funding(int change) {
        var child = ProcessDecisionRunPolicyFixture.Child(Root(), ProcessStepInstanceId.New());
        var json = JsonNode.Parse(ProcessDecisionDataPolicyAdmissionCodec.Serialize(child))!.AsObject();
        switch (change) {
            case 0:
                json.Remove("funding");
                break;
            case 1:
                json["funding"] = null;
                break;
            case 2:
                json["funding"]!["rootRunId"] = Guid.Empty;
                break;
            case 3:
                json["funding"]!["budgetId"] = Guid.Empty;
                break;
            case 4:
                json["schemaVersion"] = 3;
                break;
            case 5:
                json.Remove("materialProtocol");
                break;
        }
        Assert.Throws<JsonException>(() => ProcessDecisionDataPolicyAdmissionCodec.Deserialize(json.ToJsonString()));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Legacy_envelopes_reject_even_a_null_funding_field(int schema) {
        var child = ProcessDecisionRunPolicyFixture.Child(Root(), ProcessStepInstanceId.New()) with { Funding = null };
        child = schema switch {
            1 => child with { MaterialProtocol = null, ContentContract = null, ExecutionContract = null },
            2 => child with { MaterialProtocol = null, ContentContract = null },
            _ => child
        };
        var json = JsonNode.Parse(ProcessDecisionDataPolicyAdmissionCodec.Serialize(child))!.AsObject();
        Assert.Equal(schema, json["schemaVersion"]!.GetValue<int>());
        json["funding"] = null;
        Assert.Throws<JsonException>(() => ProcessDecisionDataPolicyAdmissionCodec.Deserialize(json.ToJsonString()));
    }

    [Fact]
    public void Funding_requires_an_explicit_material_execution_admission() {
        var child = ProcessDecisionRunPolicyFixture.Child(Root(), ProcessStepInstanceId.New()) with { MaterialProtocol = null, ContentContract = null };
        Assert.Throws<InvalidDataException>(() => child.Validate());
    }

    [Fact]
    public void Funding_identity_matches_independent_network_order_sha256_vector() {
        var parent = new DecisionBudgetId(Guid.Parse("00112233-4455-6677-8899-aabbccddeeff"));
        var child = new DecisionBudgetId(Guid.Parse("11223344-5566-7788-99aa-bbccddeeff00"));
        var allocation = new DecisionBudgetAllocationId(Guid.Parse("22334455-6677-8899-aabb-ccddeeff0011"));
        var identity = ProcessDecisionFundingIdentity.Derive(parent, child, allocation);
        Assert.Equal(Guid.Parse("adb15c42-eb3e-8026-9641-0aad767becf5"), identity.Value);
        Assert.NotEqual(identity, ProcessDecisionFundingIdentity.Derive(child, parent, allocation));
        Assert.NotEqual(identity, ProcessDecisionFundingIdentity.Derive(parent, child, new(Guid.NewGuid())));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Funding_identity_rejects_incomplete_or_self_funding_addresses(int change) {
        DecisionBudgetId parent = new(Guid.NewGuid());
        DecisionBudgetId child = new(Guid.NewGuid());
        DecisionBudgetAllocationId allocation = new(Guid.NewGuid());
        Assert.Throws<InvalidDataException>(() => ProcessDecisionFundingIdentity.Derive(change == 0 ? default : parent,
            change == 1 ? default : change == 3 ? parent : child, change == 2 ? default : allocation));
    }

    [Fact]
    public void Run_accounting_uses_a_null_step_and_never_an_empty_synthetic_step() {
        ProcessDecisionBudgetReservation command = new(new(null, new(Guid.NewGuid()), 1), new(Guid.NewGuid()), new(1, 0));
        command.Validate();
        Assert.Throws<InvalidDataException>(() => (command with { Expectation = command.Expectation with { StepInstanceId = default(ProcessStepInstanceId) } }).Validate());
    }

    private static ProcessDecisionRunAdmission Root(ReadBasisMode read = ReadBasisMode.StepSnapshot)
        => ProcessDecisionRunPolicyFixture.Root(ProcessRunId.New(), ProcessDefinitionVersionId.New(), Guid.NewGuid(), read);
}
