using System.Text.Json;
using System.Text.Json.Nodes;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.Tests.Support;

namespace CanDoItAll.Tests.Unit.Processes;

public sealed class ProcessDecisionMaterialProtocolTests {
    [Theory]
    [InlineData(ProcessDecisionChildOwner.Agent)]
    [InlineData(ProcessDecisionChildOwner.Workflow)]
    public void Shared_content_admission_keeps_business_schema_and_executor_capability_separate(ProcessDecisionChildOwner executor) {
        var original = Admission() with { MaterialProtocol = ProcessDecisionMaterialProtocol.SharedContentV1,
            ContentContract = ProcessDecisionPolicyFixture.ContentContract(), ExecutionContract = ProcessDecisionPolicyFixture.Contract(executor) };
        var encoded = ProcessDecisionDataPolicyAdmissionCodec.Serialize(original);
        var decoded = ProcessDecisionDataPolicyAdmissionCodec.Deserialize(encoded);
        Assert.Equal(original.ContentContract, decoded.ContentContract);
        Assert.Equal(original.ExecutionContract, decoded.ExecutionContract);
        Assert.Equal(encoded, ProcessDecisionDataPolicyAdmissionCodec.Serialize(decoded));
        Assert.Equal(2, JsonNode.Parse(encoded)!["materialProtocol"]!.GetValue<int>());
        decoded.RequireExecutionContract(original.ExecutionContract);
        Assert.Throws<InvalidDataException>(() => decoded.RequireExecutionContract(original.ExecutionContract with {
            Executor = executor == ProcessDecisionChildOwner.Agent ? ProcessDecisionChildOwner.Workflow : ProcessDecisionChildOwner.Agent
        }));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Shared_content_refuses_missing_or_invalid_content_schema(int change) {
        var admission = Admission() with { MaterialProtocol = ProcessDecisionMaterialProtocol.SharedContentV1, ContentContract = ProcessDecisionPolicyFixture.ContentContract() };
        var json = JsonNode.Parse(ProcessDecisionDataPolicyAdmissionCodec.Serialize(admission))!;
        switch (change) {
            case 0:
                json.AsObject().Remove("contentContract");
                break;
            case 1:
                json["contentContract"] = null;
                break;
            case 2:
                json["contentContract"]!["schemaId"] = Guid.Empty;
                break;
            case 3:
                json["contentContract"]!["version"] = 0;
                break;
        }
        Assert.Throws<JsonException>(() => ProcessDecisionDataPolicyAdmissionCodec.Deserialize(json.ToJsonString()));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Earlier_envelopes_cannot_smuggle_even_a_null_content_contract(int schema) {
        var json = Json();
        json["schemaVersion"] = schema;
        json["contentContract"] = null;
        if (schema < 3) {
            json.AsObject().Remove("materialProtocol");
        }
        if (schema == 1) {
            json.AsObject().Remove("executionContract");
        }
        Assert.Throws<JsonException>(() => ProcessDecisionDataPolicyAdmissionCodec.Deserialize(json.ToJsonString()));
    }

    [Fact]
    public void Canonical_material_uses_a_distinct_envelope_and_preserves_old_encoding_when_absent() {
        var admitted = Admission();
        var encoded = ProcessDecisionDataPolicyAdmissionCodec.Serialize(admitted);
        var json = JsonNode.Parse(encoded)!;
        Assert.Equal(3, json["schemaVersion"]!.GetValue<int>());
        Assert.Equal(1, json["materialProtocol"]!.GetValue<int>());
        Assert.Equal(encoded, ProcessDecisionDataPolicyAdmissionCodec.Serialize(ProcessDecisionDataPolicyAdmissionCodec.Deserialize(encoded)));
        var old = ProcessDecisionDataPolicyAdmissionCodec.Serialize(admitted with { MaterialProtocol = null });
        Assert.Equal(2, JsonNode.Parse(old)!["schemaVersion"]!.GetValue<int>());
        Assert.DoesNotContain("materialProtocol", old, StringComparison.Ordinal);
        Assert.NotEqual(encoded, old);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(4)]
    public void Canonical_material_cannot_be_downgraded_or_relabelled_as_a_future_envelope(int schema) {
        var json = Json();
        json["schemaVersion"] = schema;
        Assert.Throws<JsonException>(() => ProcessDecisionDataPolicyAdmissionCodec.Deserialize(json.ToJsonString()));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("2")]
    [InlineData("null")]
    [InlineData("\"1\"")]
    public void Unsupported_or_missing_protocol_values_are_refused(string value) {
        var json = Json();
        json["materialProtocol"] = JsonNode.Parse(value);
        Assert.Throws<JsonException>(() => ProcessDecisionDataPolicyAdmissionCodec.Deserialize(json.ToJsonString()));
    }

    [Fact]
    public void New_envelope_cannot_omit_its_material_requirement() {
        var json = Json();
        json.AsObject().Remove("materialProtocol");
        Assert.Throws<JsonException>(() => ProcessDecisionDataPolicyAdmissionCodec.Deserialize(json.ToJsonString()));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void Legacy_envelopes_cannot_smuggle_a_null_protocol_field(int schema) {
        var json = Json();
        json["schemaVersion"] = schema;
        json["materialProtocol"] = null;
        if (schema == 1) {
            json.AsObject().Remove("executionContract");
        }
        Assert.Throws<JsonException>(() => ProcessDecisionDataPolicyAdmissionCodec.Deserialize(json.ToJsonString()));
    }

    [Fact]
    public void Canonical_material_cannot_be_declared_without_its_executor_contract() {
        Assert.Throws<InvalidDataException>(() => ProcessDecisionDataPolicyAdmissionCodec.Serialize(Admission() with { ExecutionContract = null }));
    }

    private static ProcessDecisionDataPolicyAdmission Admission()
        => ProcessDecisionPolicyFixture.Create(ProcessDefinitionVersionId.New(), ProcessStepInstanceId.New()) with {
            ExecutionContract = ProcessDecisionPolicyFixture.Contract(ProcessDecisionChildOwner.Agent),
            MaterialProtocol = ProcessDecisionMaterialProtocol.CanonicalSnapshotV1
        };
    private static JsonNode Json() => JsonNode.Parse(ProcessDecisionDataPolicyAdmissionCodec.Serialize(Admission()))!;
}
