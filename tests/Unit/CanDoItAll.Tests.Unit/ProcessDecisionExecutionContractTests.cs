using System.Text.Json;
using System.Text.Json.Nodes;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;

namespace CanDoItAll.Tests.Unit.Processes;

public sealed class ProcessDecisionExecutionContractTests {
    [Fact]
    public void Version_one_has_exact_original_encoding_without_an_execution_field() {
        var id = Guid.Parse("00112233-4455-6677-8899-aabbccddeeff");
        var policy = DecisionDataPolicy.PlanningDefault(TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(30));
        ProcessDecisionDataPolicyAdmission admission = new(id, id, new(id), new(id), policy, null, null, null,
            new(true, true, false, true, true, false, PublicationFreshness.RequireCurrent, policy.Budget), [], policy);
        const string expected = """
            {"schemaVersion":1,"workspaceNamespaceId":"00112233-4455-6677-8899-aabbccddeeff","workspaceHeadRevision":"00112233-4455-6677-8899-aabbccddeeff","definitionVersionId":"00112233-4455-6677-8899-aabbccddeeff","stepInstanceId":"00112233-4455-6677-8899-aabbccddeeff","workspacePolicy":{"schemaVersion":1,"readBasisMode":1,"publicationFreshness":1,"onBasisChange":1,"maximumReplans":0,"maximumWorkDuration":"00:02:00","maximumBasisAge":"00:30:00"},"definitionOverride":null,"stepOverride":null,"authorizedInvocationOverride":null,"capabilities":{"supportsRunSnapshot":true,"supportsStepSnapshot":true,"supportsPinnedObservation":false,"supportsCurrentPublication":true,"supportsPauseForReview":true,"supportsReplan":false,"minimumPublicationFreshness":1,"budgetCeiling":{"maximumReplans":0,"maximumWorkDuration":"00:02:00","maximumBasisAge":"00:30:00"}},"enclosingBudgets":[],"policy":{"schemaVersion":1,"readBasisMode":1,"publicationFreshness":1,"onBasisChange":1,"maximumReplans":0,"maximumWorkDuration":"00:02:00","maximumBasisAge":"00:30:00"}}
            """;
        Assert.Equal(expected, ProcessDecisionDataPolicyAdmissionCodec.Serialize(admission));
        Assert.Equal(expected, ProcessDecisionDataPolicyAdmissionCodec.Serialize(ProcessDecisionDataPolicyAdmissionCodec.Deserialize(expected)));
        Assert.Throws<InvalidDataException>(() => admission.RequireExecutionContract(ProcessDecisionPolicyFixture.Contract(ProcessDecisionChildOwner.Agent)));
    }

    [Theory]
    [InlineData(ProcessDecisionChildOwner.Agent)]
    [InlineData(ProcessDecisionChildOwner.Workflow)]
    public void Version_two_retains_exact_contract_and_policy(ProcessDecisionChildOwner executor) {
        var original = Policy() with { ExecutionContract = ProcessDecisionPolicyFixture.Contract(executor) };
        var encoded = ProcessDecisionDataPolicyAdmissionCodec.Serialize(original);
        var decoded = ProcessDecisionDataPolicyAdmissionCodec.Deserialize(encoded);
        decoded.RequireBinding(original.DefinitionVersionId, original.StepInstanceId);
        decoded.RequireExecutionContract(original.ExecutionContract);
        Assert.Equal(original.Policy, decoded.Policy);
        Assert.Equal(encoded, ProcessDecisionDataPolicyAdmissionCodec.Serialize(decoded));
        Assert.Equal(2, JsonNode.Parse(encoded)!["schemaVersion"]!.GetValue<int>());
    }

    [Fact]
    public void Declaration_does_not_match_another_identity_version_or_executor() {
        var contract = ProcessDecisionPolicyFixture.Contract(ProcessDecisionChildOwner.Agent);
        var admission = Policy() with { ExecutionContract = contract };
        foreach (var other in new[] { contract with { ContractId = Guid.NewGuid() }, contract with { Version = 2 },
            contract with { Executor = ProcessDecisionChildOwner.Workflow } }) {
            Assert.Throws<InvalidDataException>(() => admission.RequireExecutionContract(other));
        }
        Assert.Throws<ArgumentNullException>(() => admission.RequireExecutionContract(null!));
        Assert.Throws<InvalidDataException>(() => admission.RequireExecutionContract(contract with { Version = 0 }));
    }

    [Fact]
    public void Required_contract_members_unknown_fields_and_duplicates_are_refused() {
        var original = Encoded();
        var contract = original["executionContract"]!.AsObject();
        foreach (var member in contract.Select(item => item.Key).ToArray()) {
            var missing = original.DeepClone();
            missing["executionContract"]!.AsObject().Remove(member);
            Assert.Throws<JsonException>(() => Read(missing));
            var duplicate = original.ToJsonString().Replace(contract.ToJsonString(),
                "{\"" + member + "\":null," + contract.ToJsonString()[1..], StringComparison.Ordinal);
            Assert.Throws<JsonException>(() => ProcessDecisionDataPolicyAdmissionCodec.Deserialize(duplicate));
        }
        contract["unrecognized"] = true;
        Assert.Throws<JsonException>(() => Read(original));
    }

    [Theory]
    [InlineData("contractId", "\"00000000-0000-0000-0000-000000000000\"")]
    [InlineData("contractId", "\"invalid\"")]
    [InlineData("version", "0")]
    [InlineData("version", "-1")]
    [InlineData("version", "\"1\"")]
    [InlineData("version", "null")]
    [InlineData("executor", "99")]
    [InlineData("executor", "\"Agent\"")]
    public void Invalid_contract_values_fail_before_admission(string member, string value) {
        var json = Encoded();
        json["executionContract"]![member] = JsonNode.Parse(value);
        Assert.Throws<JsonException>(() => Read(json));
    }

    [Theory]
    [InlineData("3")]
    [InlineData("0")]
    [InlineData("\"2\"")]
    [InlineData("null")]
    public void Unsupported_envelope_version_is_never_a_legacy_fallback(string value) {
        var json = Encoded();
        json["schemaVersion"] = JsonNode.Parse(value);
        Assert.Throws<JsonException>(() => Read(json));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Version_one_refuses_any_execution_field_even_null(bool nullContract) {
        var json = Encoded();
        json["schemaVersion"] = 1;
        if (nullContract) {
            json["executionContract"] = null;
        }
        Assert.Throws<JsonException>(() => Read(json));
    }

    [Fact]
    public void Version_two_requires_a_non_null_contract() {
        var json = Encoded();
        json.Remove("executionContract");
        Assert.Throws<JsonException>(() => Read(json));
        json["executionContract"] = null;
        Assert.Throws<JsonException>(() => Read(json));
    }

    [Theory]
    [InlineData(ProcessDecisionChildOwner.Agent)]
    [InlineData(ProcessDecisionChildOwner.Workflow)]
    public void Prepared_launch_fingerprint_binds_contract_and_refuses_commit_substitution(ProcessDecisionChildOwner executor) {
        var preparation = ProcessPreparedLaunchFixture.Create();
        var assignment = Assert.Single(preparation.InitialCommit.InitialAssignments!);
        var policy = ProcessDecisionPolicyFixture.Create(preparation.InitialCommit.InitialPlan!.Definition.VersionId, assignment.StepInstanceId);
        var legacy = preparation with { InitialCommit = preparation.InitialCommit with { InitialAssignments = [assignment with { DecisionDataPolicy = policy }] } };
        var contract = ProcessDecisionPolicyFixture.Contract(executor);
        var versioned = legacy with { InitialCommit = legacy.InitialCommit with {
            InitialAssignments = [assignment with { DecisionDataPolicy = policy with { ExecutionContract = contract } }]
        } };
        var original = ProcessPreparedLaunchCodec.Read(ProcessPreparedLaunchCodec.ToEntity(versioned));
        Assert.NotEqual(ProcessPreparedLaunchCodec.ToEntity(legacy).PreparationFingerprint, original.PreparationFingerprint);
        foreach (var replacement in new ProcessDecisionExecutionContract?[] { null, contract with { ContractId = Guid.NewGuid() },
            contract with { Version = 2 }, contract with { Executor = executor == ProcessDecisionChildOwner.Agent ? ProcessDecisionChildOwner.Workflow : ProcessDecisionChildOwner.Agent } }) {
            var changed = versioned with { InitialCommit = versioned.InitialCommit with {
                InitialAssignments = [assignment with { DecisionDataPolicy = policy with { ExecutionContract = replacement } }]
            } };
            Assert.NotEqual(original.PreparationFingerprint, ProcessPreparedLaunchCodec.ToEntity(changed).PreparationFingerprint);
            Assert.Throws<ProcessLaunchIntentConflictException>(() => ProcessPreparedLaunchCodec.RequireInitialCommit(original,
                ProcessPreparedLaunchFixture.Commit(original) with { InitialAssignments = changed.InitialCommit.InitialAssignments }));
        }
    }

    private static ProcessDecisionDataPolicyAdmission Policy()
        => ProcessDecisionPolicyFixture.Create(ProcessDefinitionVersionId.New(), ProcessStepInstanceId.New());

    private static JsonObject Encoded() => JsonNode.Parse(ProcessDecisionDataPolicyAdmissionCodec.Serialize(Policy() with {
        ExecutionContract = ProcessDecisionPolicyFixture.Contract(ProcessDecisionChildOwner.Agent)
    }))!.AsObject();

    private static ProcessDecisionDataPolicyAdmission Read(JsonNode json)
        => ProcessDecisionDataPolicyAdmissionCodec.Deserialize(json.ToJsonString());
}
