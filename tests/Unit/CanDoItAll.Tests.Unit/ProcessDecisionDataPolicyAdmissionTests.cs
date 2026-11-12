using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;

namespace CanDoItAll.Tests.Unit.Processes;

public sealed class ProcessDecisionDataPolicyAdmissionTests {
    [Fact]
    public void Round_trip_preserves_original_preferences_and_intersected_limits() {
        var original = Policy();
        var json = ProcessDecisionDataPolicyAdmissionCodec.Serialize(original);
        var decoded = ProcessDecisionDataPolicyAdmissionCodec.Deserialize(json);
        decoded.RequireBinding(original.DefinitionVersionId, original.StepInstanceId);
        Assert.Equal(json, ProcessDecisionDataPolicyAdmissionCodec.Serialize(decoded));
        Assert.Equal(ReadBasisMode.RunSnapshot, decoded.Policy.ReadBasisMode);
        Assert.Equal(OnBasisChange.Replan, decoded.Policy.OnBasisChange);
        Assert.Equal(1, decoded.Policy.Budget.MaximumReplans);
        Assert.Equal(TimeSpan.FromMinutes(2), decoded.Policy.Budget.MaximumWorkDuration);
        Assert.Equal(TimeSpan.FromMinutes(30), decoded.Policy.Budget.MaximumBasisAge);
        Assert.Throws<InvalidDataException>(() => decoded.RequireBinding(ProcessDefinitionVersionId.New(), original.StepInstanceId));
        Assert.Throws<InvalidDataException>(() => decoded.RequireBinding(original.DefinitionVersionId, ProcessStepInstanceId.New()));
    }

    [Fact]
    public void Every_required_member_and_nested_limit_is_strict() {
        var valid = JsonNode.Parse(ProcessDecisionDataPolicyAdmissionCodec.Serialize(Policy()))!.AsObject();
        foreach (var path in new[] { "", "capabilities", "capabilities.budgetCeiling", "definitionOverride", "stepOverride", "authorizedInvocationOverride", "workspacePolicy", "policy" }) {
            var objectNode = At(valid, path);
            foreach (var key in objectNode.Select(item => item.Key).ToArray()) {
                var missing = valid.DeepClone().AsObject();
                At(missing, path).Remove(key);
                Assert.Throws<JsonException>(() => ProcessDecisionDataPolicyAdmissionCodec.Deserialize(missing.ToJsonString()));
            }
            var unknown = valid.DeepClone().AsObject();
            At(unknown, path)["unrecognized"] = 1;
            Assert.Throws<JsonException>(() => ProcessDecisionDataPolicyAdmissionCodec.Deserialize(unknown.ToJsonString()));
            var duplicate = valid.ToJsonString().Replace(objectNode.ToJsonString(),
                "{\"" + objectNode.First().Key + "\":null," + objectNode.ToJsonString()[1..], StringComparison.Ordinal);
            Assert.Throws<JsonException>(() => ProcessDecisionDataPolicyAdmissionCodec.Deserialize(duplicate));
        }
    }

    [Theory]
    [InlineData("schemaVersion", "2")]
    [InlineData("schemaVersion", "\"1\"")]
    [InlineData("workspaceNamespaceId", "\"00000000-0000-0000-0000-000000000000\"")]
    [InlineData("definitionVersionId", "\"00000000-0000-0000-0000-000000000000\"")]
    [InlineData("enclosingBudgets", "null")]
    [InlineData("enclosingBudgets", "[null]")]
    [InlineData("capabilities", "null")]
    [InlineData("policy", "null")]
    public void Invalid_protocol_values_are_refused(string property, string value) {
        var json = JsonNode.Parse(ProcessDecisionDataPolicyAdmissionCodec.Serialize(Policy()))!.AsObject();
        json[property] = JsonNode.Parse(value);
        Assert.Throws<JsonException>(() => ProcessDecisionDataPolicyAdmissionCodec.Deserialize(json.ToJsonString()));
    }

    [Fact]
    public void Forged_resolution_unsupported_owner_and_excessive_hierarchy_are_refused_before_encoding() {
        var original = Policy();
        Assert.Throws<InvalidDataException>(() => ProcessDecisionDataPolicyAdmissionCodec.Serialize(original with { Policy = original.WorkspacePolicy }));
        Assert.Throws<DecisionDataPolicyException>(() => ProcessDecisionDataPolicyAdmissionCodec.Serialize(original with {
            OwnerCapabilities = original.OwnerCapabilities with { SupportsRunSnapshot = false }
        }));
        Assert.Throws<InvalidDataException>(() => ProcessDecisionDataPolicyAdmissionCodec.Serialize(original with { EnclosingBudgets = default }));
        Assert.Throws<InvalidDataException>(() => ProcessDecisionDataPolicyAdmissionCodec.Serialize(original with {
            EnclosingBudgets = Enumerable.Repeat(original.EnclosingBudgets[0], ProcessDecisionDataPolicyAdmission.MaximumEnclosingPolicies + 1).ToImmutableArray()
        }));
        Assert.Throws<JsonException>(() => ProcessDecisionDataPolicyAdmissionCodec.Deserialize(new string(' ', ProcessDecisionDataPolicyAdmission.MaximumUtf8Bytes + 1)));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ProcessDecisionDataPolicyAdmission>("null"));
    }

    [Fact]
    public void Legacy_prepared_launch_keeps_exact_saved_bytes_hash_and_initial_commit_compatibility() {
        using var fixture = typeof(ProcessDecisionDataPolicyAdmissionTests).Assembly.GetManifestResourceStream("ProcessPreparedLaunchLegacy.json");
        Assert.NotNull(fixture);
        var legacy = JsonSerializer.Deserialize<ProcessPreparedLaunchEntity>(fixture)!;
        var saved = ProcessPreparedLaunchCodec.Read(legacy);
        Assert.Equal("sha256:fdc5c26cb66ba10c3b0156c67f36d8acb4d3e63ef9a3d474fa93f29592893567", saved.PreparationFingerprint);
        Assert.All(saved.Preparation.InitialCommit.InitialAssignments!, assignment => Assert.Null(assignment.DecisionDataPolicy));
        var encoded = ProcessPreparedLaunchCodec.ToEntity(saved.Preparation);
        Assert.Equal(legacy.PayloadJson, encoded.PayloadJson);
        Assert.Equal(legacy.PreparationFingerprint, encoded.PreparationFingerprint);
        ProcessPreparedLaunchCodec.RequireInitialCommit(saved, ProcessPreparedLaunchFixture.Commit(saved));
    }

    [Fact]
    public void New_preparation_binds_policy_to_original_definition_step_and_fingerprint() {
        var preparation = ProcessPreparedLaunchFixture.Create();
        var assignment = Assert.Single(preparation.InitialCommit.InitialAssignments!);
        var policy = ProcessDecisionPolicyFixture.Create(preparation.InitialCommit.InitialPlan!.Definition.VersionId, assignment.StepInstanceId);
        var admitted = preparation with { InitialCommit = preparation.InitialCommit with {
            InitialAssignments = [assignment with { DecisionDataPolicy = policy }]
        } };
        var entity = ProcessPreparedLaunchCodec.ToEntity(admitted);
        var decoded = ProcessPreparedLaunchCodec.Read(entity);
        Assert.NotEqual(ProcessPreparedLaunchCodec.ToEntity(preparation).PreparationFingerprint, entity.PreparationFingerprint);
        Assert.Equal(entity.PayloadJson, ProcessPreparedLaunchCodec.ToEntity(decoded.Preparation).PayloadJson);
        Assert.Throws<InvalidDataException>(() => ProcessPreparedLaunchCodec.ToEntity(admitted with {
            InitialCommit = admitted.InitialCommit with { InitialAssignments = [assignment with {
                DecisionDataPolicy = policy with { DefinitionVersionId = ProcessDefinitionVersionId.New() }
            }] }
        }));
        Assert.Throws<InvalidDataException>(() => EfProcessRuntimeStepAssignmentStore.ToEntity(assignment with {
            DecisionDataPolicy = policy with { StepInstanceId = ProcessStepInstanceId.New() }
        }));
    }

    private static ProcessDecisionDataPolicyAdmission Policy()
        => ProcessDecisionPolicyFixture.Create(ProcessDefinitionVersionId.New(), ProcessStepInstanceId.New());

    private static JsonObject At(JsonObject root, string path) {
        var selected = root;
        foreach (var part in path.Split('.', StringSplitOptions.RemoveEmptyEntries)) {
            selected = selected[part]!.AsObject();
        }
        return selected;
    }
}
