using System.Text.Json.Nodes;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;

namespace CanDoItAll.Tests.Unit.Processes;

public sealed class ProcessDecisionChildMaterialBindingTests {
    [Theory]
    [InlineData(false, ProcessDecisionChildOwner.Agent)]
    [InlineData(true, ProcessDecisionChildOwner.Agent)]
    [InlineData(false, ProcessDecisionChildOwner.Workflow)]
    [InlineData(true, ProcessDecisionChildOwner.Workflow)]
    public void Canonical_wire_reopens_every_original_identity_and_exact_grant(bool step, ProcessDecisionChildOwner executor) {
        var original = Binding(step, executor);
        var wire = ProcessDecisionChildMaterialBindingCodec.Serialize(original);
        var reopened = ProcessDecisionChildMaterialBindingCodec.Deserialize(wire);
        Assert.Equal(original, reopened);
        Assert.Equal(wire, ProcessDecisionChildMaterialBindingCodec.Serialize(reopened));
        Assert.Equal(12345678901234567, reopened.Child.Grant.WorkTicks);
        Assert.True(wire.Length < 2048);
        Assert.DoesNotContain("payloadJson", wire, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("version")]
    [InlineData("missing")]
    [InlineData("null")]
    [InlineData("unknown")]
    [InlineData("duplicate")]
    [InlineData("whitespace")]
    [InlineData("budget")]
    [InlineData("grant")]
    [InlineData("executor")]
    [InlineData("scope-owner")]
    [InlineData("scope-step")]
    [InlineData("fingerprint")]
    [InlineData("oversize")]
    [InlineData("root-null")]
    public void Malformed_or_noncanonical_wire_is_refused(string change) {
        var wire = ProcessDecisionChildMaterialBindingCodec.Serialize(Binding());
        var value = JsonNode.Parse(wire)!.AsObject();
        switch (change) {
            case "version":
                value["version"] = 2;
                break;
            case "missing":
                value.Remove("materialStepId");
                break;
            case "null":
                value["fingerprint"] = null;
                break;
            case "unknown":
                value["extra"] = true;
                break;
            case "budget":
                value["budgetId"] = Guid.Empty;
                break;
            case "grant":
                value["grantedWorkTicks"] = -1;
                break;
            case "executor":
                value["executor"] = 99;
                break;
            case "scope-owner":
                value["ownerRunId"] = Guid.NewGuid();
                break;
            case "scope-step":
                value["materialStepId"] = Guid.NewGuid();
                break;
            case "fingerprint":
                value["fingerprint"] = new string('a', 64);
                break;
        }
        var changed = change switch {
            "duplicate" => wire.Insert(1, "\"version\":1,"),
            "whitespace" => " " + wire,
            "oversize" => new string(' ', ProcessDecisionChildMaterialBindingCodec.MaximumUtf8Bytes + 1),
            "root-null" => "null",
            _ => value.ToJsonString()
        };
        Assert.Throws<InvalidDataException>(() => ProcessDecisionChildMaterialBindingCodec.Deserialize(changed));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Typed_binding_cannot_mix_child_material_scope_or_executor(int change) {
        var original = Binding();
        var changed = change switch {
            0 => original with { Child = original.Child with { MaterialId = new(Guid.NewGuid()) } },
            1 => original with { Scope = original.Scope with { Id = new(Guid.NewGuid()) } },
            2 => original with { Scope = original.Scope with { RootRunId = new(Guid.NewGuid()) } },
            _ => original with { ExecutionContract = original.ExecutionContract with { Executor = ProcessDecisionChildOwner.Workflow } }
        };
        Assert.Throws<InvalidDataException>(() => ProcessDecisionChildMaterialBindingCodec.Serialize(changed));
    }

    private static ProcessDecisionChildMaterialBinding Binding(bool step = true, ProcessDecisionChildOwner executor = ProcessDecisionChildOwner.Agent) {
        ProcessDecisionChildAddress address = new(new(Guid.NewGuid()), new(Guid.NewGuid()), new(Guid.NewGuid()),
            new(new(Guid.NewGuid()), 2), new(Guid.NewGuid()));
        ProcessDecisionMaterialScope scope = new(new(Guid.NewGuid()), Guid.NewGuid(), Guid.NewGuid(), address.RootRunId,
            step ? address.RunId : address.RootRunId, step ? address.StepInstanceId : null);
        ProcessDecisionMaterialReference material = new(scope.Id, 3, new(Guid.NewGuid()));
        return new(new(address, material.MaterialId, executor, new(Guid.NewGuid()), new(Guid.NewGuid()), new(12345678901234567, 0)),
            scope, new(material, new string('A', 64)), ProcessDecisionPolicyFixture.Contract(executor));
    }
}
