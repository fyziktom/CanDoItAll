using System.Text.Json.Nodes;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Tests.Unit.Projects;

public sealed class AgentProjectStructureLifetimeMetadataTests {
    [Fact]
    public void Exact_revocation_preserves_new_lifetime_and_unknown_metadata_when_old_cleanup_replays() {
        var first = new AgentProjectStructureLifetime(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var second = new AgentProjectStructureLifetime(first.DatabaseProfileId, first.ProjectId, Guid.NewGuid());
        var configuration = AgentProjectStructureAccessMetadata.GrantProjectLifetime("""{"unrelated":{"keep":true}}""", first);
        configuration = AgentProjectStructureAccessMetadata.GrantProjectLifetime(configuration, second);
        Assert.Equal(configuration, AgentProjectStructureAccessMetadata.GrantProjectLifetime(configuration, second));
        var revoked = AgentProjectStructureAccessMetadata.RevokeProjectLifetime(configuration, AgentProjectStructureRevocationTarget.ForLifetime(first));
        Assert.True(revoked.Changed);
        Assert.True(JsonNode.Parse(revoked.ConfigurationJson)!["unrelated"]!["keep"]!.GetValue<bool>());
        var access = AgentProjectStructureAccessMetadata.Read(revoked.ConfigurationJson);
        Assert.Equal(second, Assert.Single(access.AllowedProjectLifetimes));
        Assert.Equal(second.ProjectId, Assert.Single(access.AllowedProjectIds));
        var replay = AgentProjectStructureAccessMetadata.RevokeProjectLifetime(revoked.ConfigurationJson, AgentProjectStructureRevocationTarget.ForLifetime(first));
        Assert.False(replay.Changed);
        Assert.Equal(revoked.ConfigurationJson, replay.ConfigurationJson);
        var final = AgentProjectStructureAccessMetadata.RevokeProjectLifetime(revoked.ConfigurationJson, AgentProjectStructureRevocationTarget.ForLifetime(second));
        Assert.True(final.Changed);
        Assert.Empty(AgentProjectStructureAccessMetadata.Read(final.ConfigurationJson).AllowedProjectIds);
        Assert.Empty(AgentProjectStructureAccessMetadata.Read(final.ConfigurationJson).AllowedProjectLifetimes);
    }

    [Fact]
    public void Legacy_recovery_removes_only_unbound_ids_and_foreign_profile_lifetime_cannot_remove_a_bound_grant() {
        var lifetime = new AgentProjectStructureLifetime(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var legacy = AgentProjectStructureAccessMetadata.Write(null, new() { AllowedProjectIds = [lifetime.ProjectId] });
        var target = AgentProjectStructureRevocationTarget.UnboundLegacy(lifetime.DatabaseProfileId, lifetime.ProjectId);
        Assert.True(AgentProjectStructureAccessMetadata.RevokeProjectLifetime(legacy, target).Changed);
        var bound = AgentProjectStructureAccessMetadata.GrantProjectLifetime(legacy, lifetime);
        Assert.False(AgentProjectStructureAccessMetadata.RevokeProjectLifetime(bound, target).Changed);
        var foreign = new AgentProjectStructureLifetime(Guid.NewGuid(), lifetime.ProjectId, lifetime.LifetimeId);
        Assert.False(AgentProjectStructureAccessMetadata.RevokeProjectLifetime(bound, AgentProjectStructureRevocationTarget.ForLifetime(foreign)).Changed);
        Assert.True(AgentProjectStructureAccessMetadata.RevokeProject(bound, lifetime.ProjectId).Changed);
    }

    [Fact]
    public void Editor_roundtrip_keeps_lifetime_binding_for_selected_ids_and_explicit_removal_clears_it() {
        var lifetime = new AgentProjectStructureLifetime(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var configuration = AgentProjectStructureAccessMetadata.GrantProjectLifetime(null, lifetime);
        var edited = AgentProjectStructureAccessMetadata.Write(configuration, new() { CanRead = true, AllowedProjectIds = [lifetime.ProjectId] });
        Assert.Equal(lifetime, Assert.Single(AgentProjectStructureAccessMetadata.Read(edited).AllowedProjectLifetimes));
        var removed = AgentProjectStructureAccessMetadata.Write(edited, new() { CanRead = true });
        Assert.Empty(AgentProjectStructureAccessMetadata.Read(removed).AllowedProjectIds);
        Assert.Empty(AgentProjectStructureAccessMetadata.Read(removed).AllowedProjectLifetimes);
    }

    [Fact]
    public void Allow_all_remains_an_explicit_policy_across_lifetime_grant_and_retirement() {
        var lifetime = new AgentProjectStructureLifetime(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var configuration = AgentProjectStructureAccessMetadata.Write(null, new() { AllowAllProjects = true });
        Assert.Equal(configuration, AgentProjectStructureAccessMetadata.GrantProjectLifetime(configuration, lifetime));
        Assert.False(AgentProjectStructureAccessMetadata.RevokeProjectLifetime(configuration, AgentProjectStructureRevocationTarget.ForLifetime(lifetime)).Changed);
        Assert.True(AgentProjectStructureAccessMetadata.Read(configuration).AllowAllProjects);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("[{}]")]
    [InlineData("[null]")]
    public void Malformed_lifetime_metadata_fails_mutation_without_authorizing_a_lenient_read(string invalidLifetimes) {
        var lifetime = new AgentProjectStructureLifetime(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var root = JsonNode.Parse(AgentProjectStructureAccessMetadata.Write(null, new() { CanRead = true, AllowedProjectIds = [lifetime.ProjectId] }))!;
        root["projectStructure"]!["allowedProjectLifetimes"] = JsonNode.Parse(invalidLifetimes);
        var malformed = root.ToJsonString();
        Assert.False(AgentProjectStructureAccessMetadata.Read(malformed).CanRead);
        Assert.Throws<AgentProjectStructureAccessMetadataException>(() => AgentProjectStructureAccessMetadata.GrantProjectLifetime(malformed, lifetime));
        Assert.Throws<AgentProjectStructureAccessMetadataException>(() => AgentProjectStructureAccessMetadata.RevokeProjectLifetime(malformed, AgentProjectStructureRevocationTarget.ForLifetime(lifetime)));
        Assert.Equal(malformed, root.ToJsonString());
    }
}
