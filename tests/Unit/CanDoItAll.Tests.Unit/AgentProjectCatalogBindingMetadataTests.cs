using System.Text.Json.Nodes;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Tests.Unit.Projects;

public sealed class AgentProjectCatalogBindingMetadataTests {
    [Fact]
    public void Binding_preserves_unknown_fields_and_raw_read_flag_without_changing_existing_identifiers() {
        var lifetime = new AgentProjectStructureLifetime(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var original = new JsonObject {
            ["customProvider"] = new JsonObject { ["saved"] = "keep" },
            ["projectStructure"] = new JsonObject {
                ["canRead"] = false,
                ["allowedProjectIds"] = new JsonArray(lifetime.ProjectId.ToString("D")),
                ["unknownScopeField"] = new JsonObject { ["value"] = 42 }
            }
        };
        var bound = AgentProjectStructureAccessMetadata.BindProjectLifetimes(original.ToJsonString(), [lifetime]);
        var parsed = JsonNode.Parse(bound)!;
        Assert.Equal("keep", parsed["customProvider"]!["saved"]!.GetValue<string>());
        Assert.False(parsed["projectStructure"]!["canRead"]!.GetValue<bool>());
        Assert.Equal(42, parsed["projectStructure"]!["unknownScopeField"]!["value"]!.GetValue<int>());
        Assert.Equal(lifetime, Assert.Single(AgentProjectStructureAccessMetadata.ReadBindingScopeForMutation(bound).Lifetimes));
        Assert.Equal(bound, AgentProjectStructureAccessMetadata.BindProjectLifetimes(bound, [lifetime]));
    }

    [Fact]
    public void Intentional_lifetime_grant_preserves_effective_read_for_raw_false_and_legacy_no_op_cases() {
        var lifetime = new AgentProjectStructureLifetime(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var original = new JsonObject { ["projectStructure"] = new JsonObject {
            ["canRead"] = false, ["allowedProjectIds"] = new JsonArray(lifetime.ProjectId.ToString("D"))
        } }.ToJsonString();
        var granted = AgentProjectStructureAccessMetadata.GrantProjectLifetime(original, lifetime);
        Assert.True(JsonNode.Parse(granted)!["projectStructure"]!["canRead"]!.GetValue<bool>());
        Assert.True(AgentProjectStructureAccessMetadata.Read(granted).CanRead);
        var rawFalse = JsonNode.Parse(granted)!;
        rawFalse["projectStructure"]!["canRead"] = false;
        var repeated = AgentProjectStructureAccessMetadata.GrantProjectLifetime(rawFalse.ToJsonString(), lifetime);
        Assert.Equal(rawFalse.ToJsonString(), repeated);
        Assert.True(AgentProjectStructureAccessMetadata.Read(repeated).CanRead);
        var all = "{\"projectStructure\":{\"canRead\":false,\"allowAllProjects\":true}}";
        Assert.Equal(all, AgentProjectStructureAccessMetadata.GrantProjectLifetime(all, lifetime));
        Assert.True(AgentProjectStructureAccessMetadata.Read(all).CanRead);
    }

    [Fact]
    public void Binding_rejects_unselected_or_malformed_lifetime_data_before_transforming_configuration() {
        var lifetime = new AgentProjectStructureLifetime(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        Assert.Throws<ArgumentException>(() => AgentProjectStructureAccessMetadata.BindProjectLifetimes("{}", [lifetime]));
        var malformed = "{\"projectStructure\":{\"allowedProjectLifetimes\":{}}}";
        Assert.Throws<AgentProjectStructureAccessMetadataException>(() => AgentProjectStructureAccessMetadata.ReadBindingScopeForMutation(malformed));
        Assert.Throws<AgentProjectStructureAccessMetadataException>(() => AgentProjectStructureAccessMetadata.BindProjectLifetimes(malformed, []));
    }
}
