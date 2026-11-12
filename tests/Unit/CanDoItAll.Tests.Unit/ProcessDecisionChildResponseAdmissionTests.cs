using System.Text.Json;
using System.Text.Json.Nodes;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;

namespace CanDoItAll.Tests.Unit.Processes;

public sealed class ProcessDecisionChildResponseAdmissionTests {
    [Theory]
    [InlineData(ReadBasisMode.RunSnapshot)]
    [InlineData(ReadBasisMode.StepSnapshot)]
    public void Explicit_child_failure_requires_the_new_admission_version_and_retains_original_funding(ReadBasisMode mode) {
        var original = Create(mode);
        var json = ProcessDecisionDataPolicyAdmissionCodec.Serialize(original);
        var wire = JsonNode.Parse(json)!;
        Assert.Equal(ProcessDecisionDataPolicyAdmission.ChildResponseSchemaVersion, wire["schemaVersion"]!.GetValue<int>());
        Assert.Equal((int)OnBasisChange.Fail, wire["childOnBasisChange"]!.GetValue<int>());
        var decoded = ProcessDecisionDataPolicyAdmissionCodec.Deserialize(json);
        Assert.Equal(original.ChildOnBasisChange, decoded.ChildOnBasisChange);
        Assert.Equal(original.Policy, decoded.Policy);
        Assert.Equal(original.Funding, decoded.Funding);
        Assert.Equal(original.SharedRunScopeId, decoded.SharedRunScopeId);
        Assert.Equal(json, ProcessDecisionDataPolicyAdmissionCodec.Serialize(decoded));
    }

    [Theory]
    [InlineData(ReadBasisMode.RunSnapshot)]
    [InlineData(ReadBasisMode.StepSnapshot)]
    public void A_legacy_failure_admission_keeps_its_wire_version_and_does_not_acquire_a_child_grant(ReadBasisMode mode) {
        var original = Create(mode) with { ChildOnBasisChange = null };
        var json = ProcessDecisionDataPolicyAdmissionCodec.Serialize(original);
        var wire = JsonNode.Parse(json)!;
        Assert.Equal(mode == ReadBasisMode.RunSnapshot ? 5 : 4, wire["schemaVersion"]!.GetValue<int>());
        Assert.False(wire.AsObject().ContainsKey("childOnBasisChange"));
        var decoded = ProcessDecisionDataPolicyAdmissionCodec.Deserialize(json);
        Assert.Null(decoded.ChildOnBasisChange);
        Assert.Equal(OnBasisChange.Fail, decoded.Policy.OnBasisChange);
        Assert.Equal(json, ProcessDecisionDataPolicyAdmissionCodec.Serialize(decoded));
    }

    [Theory]
    [InlineData("schemaVersion", "4")]
    [InlineData("schemaVersion", "5")]
    [InlineData("childOnBasisChange", "null")]
    [InlineData("childOnBasisChange", "1")]
    [InlineData("childOnBasisChange", "2")]
    [InlineData("childOnBasisChange", "99")]
    [InlineData("funding", "null")]
    [InlineData("executionContract", "null")]
    public void The_new_wire_cannot_downgrade_omit_funding_or_select_another_response(string property, string value) {
        var wire = JsonNode.Parse(ProcessDecisionDataPolicyAdmissionCodec.Serialize(Create(ReadBasisMode.StepSnapshot)))!;
        wire[property] = JsonNode.Parse(value);
        Assert.Throws<JsonException>(() => ProcessDecisionDataPolicyAdmissionCodec.Deserialize(wire.ToJsonString()));
    }

    [Fact]
    public void The_response_is_mandatory_in_the_new_version_and_must_match_the_resolved_parent() {
        var original = Create(ReadBasisMode.StepSnapshot);
        var wire = JsonNode.Parse(ProcessDecisionDataPolicyAdmissionCodec.Serialize(original))!.AsObject();
        wire.Remove("childOnBasisChange");
        Assert.Throws<JsonException>(() => ProcessDecisionDataPolicyAdmissionCodec.Deserialize(wire.ToJsonString()));
        var pause = new DecisionDataPolicy(original.Policy.ReadBasisMode, original.Policy.PublicationFreshness,
            OnBasisChange.PauseForReview, original.Policy.Budget);
        Assert.Throws<InvalidDataException>(() => (original with { Policy = pause, WorkspacePolicy = pause }).Validate());
    }

    private static ProcessDecisionDataPolicyAdmission Create(ReadBasisMode mode) {
        var policy = new DecisionDataPolicy(mode, PublicationFreshness.RequireCurrent, OnBasisChange.Fail,
            new(0, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(10)));
        return new(Guid.NewGuid(), Guid.NewGuid(), ProcessDefinitionVersionId.New(), ProcessStepInstanceId.New(), policy,
            null, null, null, new(true, true, false, true, true, false, PublicationFreshness.RequireCurrent, policy.Budget), [], policy) {
            ExecutionContract = ProcessDecisionPolicyFixture.Contract(ProcessDecisionChildOwner.Agent),
            MaterialProtocol = ProcessDecisionMaterialProtocol.SharedContentV1,
            ContentContract = ProcessDecisionPolicyFixture.ContentContract(),
            Funding = new(ProcessRunId.New(), new(Guid.NewGuid())),
            SharedRunScopeId = mode == ReadBasisMode.RunSnapshot ? new(Guid.NewGuid()) : null,
            ChildOnBasisChange = OnBasisChange.Fail
        };
    }
}
