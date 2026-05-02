using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Tests.Unit;

public sealed class AgentWorkspaceToolAccessMetadataTests
{
    [Fact]
    public void NormalizeExternalTargetAlias_converts_windows_absolute_path_to_external_target_alias()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var alias = AgentWorkspaceToolAccessMetadata.NormalizeExternalTargetAlias(@"C:\repositories\OtherRepo");

        Assert.Equal("external-target/C/repositories/OtherRepo", alias);
    }

    [Fact]
    public void NormalizeExternalTargetAlias_rejects_drive_root()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var alias = AgentWorkspaceToolAccessMetadata.NormalizeExternalTargetAlias(@"C:\");

        Assert.Null(alias);
    }

    [Fact]
    public void IsExternalTargetAliasAllowed_accepts_root_and_children_but_rejects_siblings()
    {
        var allowedAliases = new[]
        {
            "external-target/C/repositories/demo"
        };

        Assert.True(AgentWorkspaceToolAccessMetadata.IsExternalTargetAliasAllowed(
            "external-target/C/repositories/demo",
            allowedAliases));
        Assert.True(AgentWorkspaceToolAccessMetadata.IsExternalTargetAliasAllowed(
            "external-target/C/repositories/demo/src/Program.cs",
            allowedAliases));
        Assert.False(AgentWorkspaceToolAccessMetadata.IsExternalTargetAliasAllowed(
            "external-target/C/repositories/demo-sibling",
            allowedAliases));
    }

    [Fact]
    public void Write_and_read_round_trip_file_and_storage_tool_settings()
    {
        var storageId = Guid.NewGuid();
        var configurationJson = AgentWorkspaceToolAccessMetadata.Write(
            """{"existing":true}""",
            new AgentWorkspaceToolAccessSettings
            {
                CanReadFiles = true,
                CanWriteFiles = true,
                AllowedExternalTargetAliases =
                [
                    "external-target/c/repositories/demo"
                ],
                CanReadStorage = true,
                CanWriteStorage = true,
                AllowedStorageCatalogIds =
                [
                    storageId
                ]
            });

        var settings = AgentWorkspaceToolAccessMetadata.Read(configurationJson);

        Assert.True(settings.CanReadFiles);
        Assert.True(settings.CanWriteFiles);
        Assert.Equal(["external-target/C/repositories/demo"], settings.AllowedExternalTargetAliases);
        Assert.True(settings.CanReadStorage);
        Assert.True(settings.CanWriteStorage);
        Assert.Equal([storageId], settings.AllowedStorageCatalogIds);
        Assert.Contains("\"existing\":true", configurationJson, StringComparison.Ordinal);
    }
}
