using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
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

    [Fact]
    public void GroundPromptExternalTargetAliases_adds_prompt_absolute_path_as_allowed_alias_for_write_enabled_agent()
    {
        var metadataJson = ExecutionInvocationMetadata.GroundPromptExternalTargetAliases(
            "{}",
            """analyze "C:\programovani\outputsfromtests\dotnet\BikeRepairSlotScheduler" and add architecture""",
            new AgentWorkspaceToolAccessSettings
            {
                CanReadFiles = true,
                CanWriteFiles = true
            });

        using var document = JsonDocument.Parse(metadataJson);
        var aliases = document.RootElement
            .GetProperty(ExecutionInvocationMetadata.AllowedExternalTargetAliasesMetadataKey)
            .EnumerateArray()
            .Select(item => item.GetString())
            .ToArray();

        Assert.Contains("external-target/C/programovani/outputsfromtests/dotnet/BikeRepairSlotScheduler", aliases);
        Assert.False(document.RootElement.TryGetProperty(ExecutionInvocationMetadata.ReadOnlyExternalTargetAliasesMetadataKey, out _));
    }

    [Fact]
    public void GroundPromptExternalTargetAliases_adds_prompt_absolute_path_as_readonly_alias_for_read_only_agent()
    {
        var metadataJson = ExecutionInvocationMetadata.GroundPromptExternalTargetAliases(
            "{}",
            """inspect C:\programovani\outputsfromtests\dotnet\BikeRepairSlotScheduler before reporting""",
            new AgentWorkspaceToolAccessSettings
            {
                CanReadFiles = true,
                CanWriteFiles = false
            });

        using var document = JsonDocument.Parse(metadataJson);
        var aliases = document.RootElement
            .GetProperty(ExecutionInvocationMetadata.ReadOnlyExternalTargetAliasesMetadataKey)
            .EnumerateArray()
            .Select(item => item.GetString())
            .ToArray();

        Assert.Contains("external-target/C/programovani/outputsfromtests/dotnet/BikeRepairSlotScheduler", aliases);
    }
}
