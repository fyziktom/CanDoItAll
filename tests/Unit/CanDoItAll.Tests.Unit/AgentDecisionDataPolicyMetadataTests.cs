using System.Text.Json;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Tests.Unit;

public sealed class AgentDecisionDataPolicyMetadataTests {
    [Fact]
    public void Legacy_absence_and_explicit_inheritance_are_distinct_and_supplied_new_identity_policy_refuses() {
        Assert.Null(AgentDecisionDataPolicyMetadata.Read("{}"));
        var inherited = new DecisionDataPolicyOverride();
        var configured = AgentDecisionDataPolicyMetadata.Replace("{\"opaque\":[1,2]}", inherited);
        Assert.Equal(inherited, AgentDecisionDataPolicyMetadata.Read(configured));
        Assert.Throws<AgentDecisionDataPolicyMetadataException>(() => AgentDecisionDataPolicyMetadata.Preserve(null, configured));
        Assert.Equal("{}", AgentDecisionDataPolicyMetadata.Preserve(null, "{}"));
        var preserved = AgentDecisionDataPolicyMetadata.Preserve("{}", configured);
        Assert.Null(AgentDecisionDataPolicyMetadata.Read(preserved));
        using var parsed = JsonDocument.Parse(preserved);
        Assert.Equal(2, parsed.RootElement.GetProperty("opaque").GetArrayLength());
    }

    [Fact]
    public void Existing_reserved_values_are_retained_exactly_even_when_duplicate_and_only_explicit_replacement_repairs() {
        const string first = "{ \"unsupported\" : 7 }";
        const string second = "null";
        var original = $"{{\"{AgentDecisionDataPolicyMetadata.PropertyName}\":{first},\"{AgentDecisionDataPolicyMetadata.PropertyName}\":{second}}}";
        var candidate = AgentDecisionDataPolicyMetadata.Replace("{\"draft\":\"updated\"}", new(ReadBasisMode.StepSnapshot));
        var retained = AgentDecisionDataPolicyMetadata.Preserve(original, candidate);
        using var document = JsonDocument.Parse(retained);
        Assert.Equal(new[] { first, second }, document.RootElement.EnumerateObject()
            .Where(property => property.NameEquals(AgentDecisionDataPolicyMetadata.PropertyName))
            .Select(property => property.Value.GetRawText()));
        Assert.Equal("updated", document.RootElement.GetProperty("draft").GetString());
        var ordinary = AgentDecisionDataPolicyMetadata.ProjectOrdinaryConfiguration(retained);
        using var ordinaryDocument = JsonDocument.Parse(ordinary);
        Assert.Equal("updated", ordinaryDocument.RootElement.GetProperty("draft").GetString());
        Assert.False(ordinaryDocument.RootElement.TryGetProperty(AgentDecisionDataPolicyMetadata.PropertyName, out _));
        Assert.Equal(retained, AgentDecisionDataPolicyMetadata.Preserve(original, ordinary));
        Assert.Throws<AgentDecisionDataPolicyMetadataException>(() => AgentDecisionDataPolicyMetadata.Read(retained));
        var policy = new DecisionDataPolicyOverride(MaximumBasisAge: TimeSpan.FromTicks(123));
        Assert.Equal(policy, AgentDecisionDataPolicyMetadata.Read(AgentDecisionDataPolicyMetadata.Replace(retained, policy)));
    }

    [Fact]
    public void Malformed_containers_and_unsupported_stored_policy_never_become_absence() {
        foreach (var malformed in new[] { "{", "null", "[]", "42" }) {
            Assert.Throws<AgentDecisionDataPolicyMetadataException>(() => AgentDecisionDataPolicyMetadata.Read(malformed));
            Assert.Throws<AgentDecisionDataPolicyMetadataException>(() => AgentDecisionDataPolicyMetadata.ProjectOrdinaryConfiguration(malformed));
            Assert.Throws<AgentDecisionDataPolicyMetadataException>(() => AgentDecisionDataPolicyMetadata.Preserve(malformed, "{}"));
            Assert.Throws<AgentDecisionDataPolicyMetadataException>(() => AgentDecisionDataPolicyMetadata.Preserve("{}", malformed));
        }
        var valid = AgentDecisionDataPolicyMetadata.Replace("{}", new());
        var unsupported = valid.Replace("\"schemaVersion\":1", "\"schemaVersion\":2", StringComparison.Ordinal);
        Assert.NotEqual(valid, unsupported);
        Assert.Throws<AgentDecisionDataPolicyMetadataException>(() => AgentDecisionDataPolicyMetadata.Read(unsupported));
        Assert.Equal(unsupported, AgentDecisionDataPolicyMetadata.Preserve(unsupported, unsupported));
        Assert.Null(AgentDecisionDataPolicyMetadata.Read(" "));
    }

    [Fact]
    public void Acknowledgement_requires_the_exact_original_identity_policy_and_next_revision() {
        var workspace = new AgentExecutionActivityWorkspaceIdentity(Guid.NewGuid(), WorkspaceScopeDescriptor.Sandbox, new(3));
        var expected = new AgentDecisionDataPolicyExpectation(workspace, Guid.NewGuid(), new(9));
        var policy = new DecisionDataPolicyOverride(ReadBasisMode.RunSnapshot, MaximumWorkDuration: TimeSpan.FromTicks(9));
        var command = new ReplaceAgentDecisionDataPolicyCommand(Guid.NewGuid(), expected, policy);
        var accepted = new AgentDecisionDataPolicySnapshot(new(workspace, expected.AgentId, new(10)), policy);
        var outcome = new AgentDecisionDataPolicySaveOutcome.Accepted(command, accepted, followupRequired: true);
        Assert.Same(command, outcome.Command);
        Assert.Same(accepted, outcome.Snapshot);
        Assert.True(outcome.FollowupRequired);
        Assert.Throws<ArgumentException>(() => new AgentDecisionDataPolicySaveOutcome.Accepted(command,
            new(new(workspace, expected.AgentId, new(11)), policy)));
        Assert.Throws<ArgumentException>(() => new AgentDecisionDataPolicySaveOutcome.Accepted(command,
            new(accepted.Expectation, new(ReadBasisMode.StepSnapshot))));
        Assert.Throws<JsonException>(() => new ReplaceAgentDecisionDataPolicyCommand(Guid.NewGuid(), expected,
            new(MaximumReplans: -1)));
    }
}
