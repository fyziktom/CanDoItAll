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
        Assert.False(settings.CanWriteTasks);
        Assert.True(settings.AllowAllProjects);
        Assert.True(settings.CanWrite || settings.CanWriteTasks);
    }
}
