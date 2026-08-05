using System.Text.Json.Nodes;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Tests.Unit;

public sealed class AgentProjectStructureAccessMetadataTests
{
    [Fact]
    public void Normalize_task_write_implies_read_without_granting_broad_write()
    {
        var settings = AgentProjectStructureAccessMetadata.Normalize(new AgentProjectStructureAccessSettings
        {
            CanRead = false,
            CanWrite = false,
            CanWriteTasks = true
        });

        Assert.True(settings.CanRead);
        Assert.False(settings.CanWrite);
        Assert.True(settings.CanWriteTasks);
    }

    [Fact]
    public void Normalize_non_task_structure_write_implies_read_without_granting_task_or_broad_write()
    {
        var settings = AgentProjectStructureAccessMetadata.Normalize(new AgentProjectStructureAccessSettings
        {
            CanRead = false,
            CanWrite = false,
            CanWriteNonTaskStructure = true,
            CanWriteTasks = false
        });

        Assert.True(settings.CanRead);
        Assert.False(settings.CanWrite);
        Assert.True(settings.CanWriteNonTaskStructure);
        Assert.False(settings.CanWriteTasks);
    }

    [Fact]
    public void Normalize_creation_permissions_imply_read_without_granting_structure_write()
    {
        var settings = AgentProjectStructureAccessMetadata.Normalize(new AgentProjectStructureAccessSettings
        {
            CanCreateProjects = true,
            CanCreateSubprojects = true
        });

        Assert.True(settings.CanRead);
        Assert.False(settings.CanWrite);
        Assert.False(settings.CanWriteNonTaskStructure);
        Assert.False(settings.CanWriteTasks);
        Assert.True(settings.CanCreateProjects);
        Assert.True(settings.CanCreateSubprojects);
    }

    [Fact]
    public void Normalize_all_projects_scope_implies_read_and_discards_explicit_project_ids()
    {
        var settings = AgentProjectStructureAccessMetadata.Normalize(new AgentProjectStructureAccessSettings
        {
            AllowAllProjects = true,
            AllowedProjectIds =
            [
                Guid.Parse("3487184d-3b57-4cb8-9b73-c163ac73d487"),
                Guid.Parse("546612cf-bb74-4b8f-9cf5-714992c75f89")
            ]
        });

        Assert.True(settings.CanRead);
        Assert.True(settings.AllowAllProjects);
        Assert.Empty(settings.AllowedProjectIds);
    }

    [Fact]
    public void Normalize_explicit_project_scope_implies_read_and_canonicalizes_project_ids()
    {
        var firstProjectId = Guid.Parse("3487184d-3b57-4cb8-9b73-c163ac73d487");
        var secondProjectId = Guid.Parse("546612cf-bb74-4b8f-9cf5-714992c75f89");

        var settings = AgentProjectStructureAccessMetadata.Normalize(new AgentProjectStructureAccessSettings
        {
            AllowedProjectIds = [secondProjectId, Guid.Empty, firstProjectId, secondProjectId]
        });

        Assert.True(settings.CanRead);
        Assert.False(settings.AllowAllProjects);
        Assert.Equal([firstProjectId, secondProjectId], settings.AllowedProjectIds);
    }

    [Fact]
    public void Write_persists_only_all_projects_when_both_scope_forms_are_requested()
    {
        var configurationJson = AgentProjectStructureAccessMetadata.Write(
            configurationJson: null,
            settings: new AgentProjectStructureAccessSettings
            {
                AllowAllProjects = true,
                AllowedProjectIds = [Guid.Parse("3487184d-3b57-4cb8-9b73-c163ac73d487")]
            });

        var root = JsonNode.Parse(configurationJson)!.AsObject();
        var projectStructure = root["projectStructure"]!.AsObject();

        Assert.True(projectStructure["canRead"]!.GetValue<bool>());
        Assert.True(projectStructure["allowAllProjects"]!.GetValue<bool>());
        Assert.Empty(projectStructure["allowedProjectIds"]!.AsArray());
    }

    [Fact]
    public void Write_and_read_round_trip_task_write_scope_and_preserve_unrelated_configuration()
    {
        var projectId = Guid.Parse("580e32b9-8955-42c9-9646-0e9675f51f81");
        var configurationJson = AgentProjectStructureAccessMetadata.Write(
            """
            {
              "unrelated": {
                "enabled": true
              }
            }
            """,
            new AgentProjectStructureAccessSettings
            {
                CanWriteTasks = true,
                AllowedProjectIds = [Guid.Empty, projectId, projectId]
            });

        var roundTrip = AgentProjectStructureAccessMetadata.Read(configurationJson);
        var root = JsonNode.Parse(configurationJson)!.AsObject();

        Assert.True(roundTrip.CanRead);
        Assert.False(roundTrip.CanWrite);
        Assert.True(roundTrip.CanWriteTasks);
        Assert.False(roundTrip.AllowAllProjects);
        Assert.Equal([projectId], roundTrip.AllowedProjectIds);
        Assert.True(root["unrelated"]!["enabled"]!.GetValue<bool>());
    }

    [Fact]
    public void Write_and_read_round_trip_non_task_structure_write_without_granting_task_write()
    {
        var configurationJson = AgentProjectStructureAccessMetadata.Write(
            """
            {
              "unrelated": {
                "enabled": true
              }
            }
            """,
            new AgentProjectStructureAccessSettings
            {
                CanWriteNonTaskStructure = true,
                AllowAllProjects = true
            });

        var roundTrip = AgentProjectStructureAccessMetadata.Read(configurationJson);
        var root = JsonNode.Parse(configurationJson)!.AsObject();

        Assert.True(roundTrip.CanRead);
        Assert.False(roundTrip.CanWrite);
        Assert.True(roundTrip.CanWriteNonTaskStructure);
        Assert.False(roundTrip.CanWriteTasks);
        Assert.True(roundTrip.AllowAllProjects);
        Assert.True(root["projectStructure"]!["canWriteNonTaskStructure"]!.GetValue<bool>());
        Assert.True(root["unrelated"]!["enabled"]!.GetValue<bool>());
    }

    [Fact]
    public void Read_legacy_broad_write_configuration_keeps_superuser_semantics()
    {
        const string configurationJson = """
            {
              "projectStructure": {
                "canRead": false,
                "canWrite": true,
                "allowAllProjects": true,
                "allowedProjectIds": []
              }
            }
            """;

        var settings = AgentProjectStructureAccessMetadata.Read(configurationJson);

        Assert.True(settings.CanRead);
        Assert.True(settings.CanWrite);
        Assert.False(settings.CanWriteNonTaskStructure);
        Assert.False(settings.CanWriteTasks);
        Assert.True(settings.CanCreateProjects);
        Assert.True(settings.CanCreateSubprojects);
        Assert.True(settings.AllowAllProjects);
        Assert.True(settings.CanWrite || settings.CanWriteNonTaskStructure || settings.CanWriteTasks);
    }

    [Fact]
    public void Write_and_read_keep_project_and_subproject_creation_independent()
    {
        var configurationJson = AgentProjectStructureAccessMetadata.Write(
            configurationJson: null,
            settings: new AgentProjectStructureAccessSettings
            {
                CanCreateProjects = false,
                CanCreateSubprojects = true,
                AllowAllProjects = true
            });

        var settings = AgentProjectStructureAccessMetadata.Read(configurationJson);
        var root = JsonNode.Parse(configurationJson)!.AsObject();

        Assert.True(settings.CanRead);
        Assert.False(settings.CanWrite);
        Assert.False(settings.CanCreateProjects);
        Assert.True(settings.CanCreateSubprojects);
        Assert.False(root["projectStructure"]!["canCreateProjects"]!.GetValue<bool>());
        Assert.True(root["projectStructure"]!["canCreateSubprojects"]!.GetValue<bool>());
    }

    [Fact]
    public void Read_explicit_creation_denials_do_not_inherit_legacy_broad_write()
    {
        const string configurationJson = """
            {
              "projectStructure": {
                "canRead": true,
                "canWrite": true,
                "canCreateProjects": false,
                "canCreateSubprojects": false,
                "allowAllProjects": true,
                "allowedProjectIds": []
              }
            }
            """;

        var settings = AgentProjectStructureAccessMetadata.Read(configurationJson);

        Assert.True(settings.CanWrite);
        Assert.False(settings.CanCreateProjects);
        Assert.False(settings.CanCreateSubprojects);
    }

    [Fact]
    public void RevokeProject_removes_only_the_exact_project_and_preserves_unrelated_metadata()
    {
        var projectId = Guid.Parse("3487184d-3b57-4cb8-9b73-c163ac73d487");
        var retainedProjectId = Guid.Parse("546612cf-bb74-4b8f-9cf5-714992c75f89");
        var result = AgentProjectStructureAccessMetadata.RevokeProject(
            $$"""
            {
              "unrelated": {
                "projectText": "{{projectId:D}}"
              },
              "projectStructure": {
                "canRead": true,
                "canWrite": false,
                "futureSetting": "preserved",
                "allowAllProjects": false,
                "allowedProjectIds": [
                  "{{projectId:D}}",
                  "{{retainedProjectId:D}}"
                ]
              }
            }
            """,
            projectId);

        var root = JsonNode.Parse(result.ConfigurationJson)!.AsObject();
        var access = AgentProjectStructureAccessMetadata.Read(result.ConfigurationJson);

        Assert.True(result.Changed);
        Assert.Equal([retainedProjectId], access.AllowedProjectIds);
        Assert.Equal(projectId.ToString("D"), root["unrelated"]!["projectText"]!.GetValue<string>());
        Assert.Equal("preserved", root["projectStructure"]!["futureSetting"]!.GetValue<string>());
    }

    [Fact]
    public void RevokeProject_keeps_allow_all_access_and_does_not_rewrite_configuration()
    {
        var projectId = Guid.Parse("3487184d-3b57-4cb8-9b73-c163ac73d487");
        var configurationJson = $$"""
            {
              "projectStructure": {
                "canRead": true,
                "allowAllProjects": true,
                "allowedProjectIds": ["{{projectId:D}}"]
              }
            }
            """;

        var result = AgentProjectStructureAccessMetadata.RevokeProject(
            configurationJson,
            projectId);

        Assert.False(result.Changed);
        Assert.Same(configurationJson, result.ConfigurationJson);
    }

    [Theory]
    [InlineData("not-json")]
    [InlineData("[]")]
    [InlineData("{\"projectStructure\":null}")]
    [InlineData("{\"projectStructure\":{\"canRead\":\"true\"}}")]
    [InlineData("{\"projectStructure\":{\"allowedProjectIds\":[\"not-a-guid\"]}}")]
    public void RevokeProject_rejects_malformed_metadata_without_returning_a_replacement(
        string configurationJson)
    {
        var exception = Assert.Throws<AgentProjectStructureAccessMetadataException>(() =>
            AgentProjectStructureAccessMetadata.RevokeProject(
                configurationJson,
                Guid.Parse("3487184d-3b57-4cb8-9b73-c163ac73d487")));

        Assert.StartsWith(
            "Project-structure access metadata is malformed.",
            exception.Message,
            StringComparison.Ordinal);
        Assert.DoesNotContain(configurationJson, exception.Message, StringComparison.Ordinal);
    }
}
