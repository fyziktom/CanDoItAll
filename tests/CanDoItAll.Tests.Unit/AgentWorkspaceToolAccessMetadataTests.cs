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
    public void NormalizeExternalTargetAlias_strips_escaped_line_break_annotations()
    {
        var workspaceAlias = AgentWorkspaceToolAccessMetadata.NormalizeExternalTargetAlias(
            "external-target/C/programovani/candoitall-dev-output/pantry-pulse-csharp/nWorkspace alias: external-target/C/programovani/candoitall-dev-output/pantry-pulse-csharp");
        var generatedAlias = AgentWorkspaceToolAccessMetadata.NormalizeExternalTargetAlias(
            "external-target/C/programovani/candoitall-dev-output/pantry-pulse-csharp/nAll generated app source belongs under this directory.");
        var inlineAlias = AgentWorkspaceToolAccessMetadata.NormalizeExternalTargetAlias(
            "external-target/C/programovani/candoitall-dev-output/pantry-pulse-csharp Workspace alias: external-target/C/programovani/candoitall-dev-output/pantry-pulse-csharp All generated app source belongs under");

        Assert.Equal("external-target/C/programovani/candoitall-dev-output/pantry-pulse-csharp", workspaceAlias);
        Assert.Equal("external-target/C/programovani/candoitall-dev-output/pantry-pulse-csharp", generatedAlias);
        Assert.Equal("external-target/C/programovani/candoitall-dev-output/pantry-pulse-csharp", inlineAlias);
    }

    [Fact]
    public void NormalizeExternalTargetAlias_strips_approved_product_root_annotation()
    {
        var alias = AgentWorkspaceToolAccessMetadata.NormalizeExternalTargetAlias(
            "external-target/C/programovani/dotnet-demo/output/codex-live-blazor-20260522-170653 Approved product root for this run");

        Assert.Equal("external-target/C/programovani/dotnet-demo/output/codex-live-blazor-20260522-170653", alias);
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
    public void Write_and_read_round_trip_typed_workspace_tool_profile()
    {
        var configurationJson = AgentWorkspaceToolAccessMetadata.Write(
            "{}",
            AgentWorkspaceToolAccessProfiles.CreateSettings(AgentWorkspaceToolProfileKind.SoftwareDevelopment));

        var settings = AgentWorkspaceToolAccessMetadata.Read(configurationJson);

        Assert.Equal(AgentWorkspaceToolProfileKind.SoftwareDevelopment, settings.Profile);
        Assert.True(settings.CanReadFiles);
        Assert.True(settings.CanWriteFiles);
        Assert.True(settings.CanRunValidationCommands);
        Assert.True(settings.CanRunLocalScripts);
        Assert.True(settings.CanScaffoldProjects);
        Assert.True(settings.CanManageWorkspacePaths);
        Assert.True(settings.CanTransformArtifacts);
        Assert.Contains(AgentWorkspaceToolAccessProfiles.SoftwareDevelopmentProfileKey, configurationJson, StringComparison.Ordinal);
    }

    [Fact]
    public void IsWorkspaceToolAllowed_applies_profile_permissions_by_tool_family()
    {
        var qaProfile = AgentWorkspaceToolAccessProfiles.CreateSettings(AgentWorkspaceToolProfileKind.QualityValidation);
        var businessProfile = AgentWorkspaceToolAccessProfiles.CreateSettings(AgentWorkspaceToolProfileKind.BusinessAnalysis);

        Assert.True(AgentWorkspaceToolAccessMetadata.IsWorkspaceToolAllowed(qaProfile, "workspace_dotnet_build"));
        Assert.True(AgentWorkspaceToolAccessMetadata.IsWorkspaceToolAllowed(qaProfile, "workspace_dotnet_stop"));
        Assert.True(AgentWorkspaceToolAccessMetadata.IsWorkspaceToolAllowed(qaProfile, "workspace_write_file"));
        Assert.False(AgentWorkspaceToolAccessMetadata.IsWorkspaceToolAllowed(qaProfile, "workspace_dotnet_new"));
        Assert.False(AgentWorkspaceToolAccessMetadata.IsWorkspaceToolAllowed(qaProfile, "workspace_delete_path"));
        Assert.True(AgentWorkspaceToolAccessMetadata.IsWorkspaceToolAllowed(businessProfile, "workspace_convert_document"));
        Assert.True(AgentWorkspaceToolAccessMetadata.IsWorkspaceToolAllowed(businessProfile, "workspace_analyze_image"));
        Assert.True(AgentWorkspaceToolAccessMetadata.IsWorkspaceToolAllowed(businessProfile, "workspace_analyze_images"));
        Assert.False(AgentWorkspaceToolAccessMetadata.IsWorkspaceToolAllowed(businessProfile, "workspace_dotnet_run"));
        Assert.False(AgentWorkspaceToolAccessMetadata.IsWorkspaceToolAllowed(businessProfile, "workspace_dotnet_stop"));
        Assert.False(AgentWorkspaceToolAccessMetadata.IsWorkspaceToolAllowed(
            AgentWorkspaceToolAccessProfiles.CreateSettings(AgentWorkspaceToolProfileKind.ReadOnly),
            "workspace_analyze_images"));
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

    [Fact]
    public void GroundPromptExternalTargetAliases_adds_prompt_absolute_path_as_readonly_alias_when_process_step_disallows_product_mutation()
    {
        var metadataJson = ExecutionInvocationMetadata.GroundPromptExternalTargetAliases(
            $"{{\"{ExecutionInvocationMetadata.ProcessStepAllowsProductMutationMetadataKey}\":false}}",
            """inspect C:\programovani\outputsfromtests\dotnet\BikeRepairSlotScheduler before reporting""",
            new AgentWorkspaceToolAccessSettings
            {
                CanReadFiles = true,
                CanWriteFiles = true
            });

        using var document = JsonDocument.Parse(metadataJson);
        var readOnlyAliases = document.RootElement
            .GetProperty(ExecutionInvocationMetadata.ReadOnlyExternalTargetAliasesMetadataKey)
            .EnumerateArray()
            .Select(item => item.GetString())
            .ToArray();

        Assert.Contains("external-target/C/programovani/outputsfromtests/dotnet/BikeRepairSlotScheduler", readOnlyAliases);
        Assert.False(document.RootElement.TryGetProperty(ExecutionInvocationMetadata.AllowedExternalTargetAliasesMetadataKey, out _));
    }

    [Fact]
    public void GroundPromptExternalTargetAliases_keeps_process_free_text_alias_read_only_even_when_product_mutation_is_allowed()
    {
        var metadataJson = ExecutionInvocationMetadata.GroundPromptExternalTargetAliases(
            $"{{\"{ExecutionInvocationMetadata.ProcessStepAllowsProductMutationMetadataKey}\":true}}",
            """implementation prompt mentions C:\programovani\outputsfromtests\dotnet\BikeRepairSlotScheduler""",
            new AgentWorkspaceToolAccessSettings
            {
                CanReadFiles = true,
                CanWriteFiles = true
            });

        using var document = JsonDocument.Parse(metadataJson);
        var readOnlyAliases = document.RootElement
            .GetProperty(ExecutionInvocationMetadata.ReadOnlyExternalTargetAliasesMetadataKey)
            .EnumerateArray()
            .Select(item => item.GetString())
            .ToArray();

        Assert.Contains("external-target/C/programovani/outputsfromtests/dotnet/BikeRepairSlotScheduler", readOnlyAliases);
        Assert.False(document.RootElement.TryGetProperty(ExecutionInvocationMetadata.AllowedExternalTargetAliasesMetadataKey, out _));
    }

    [Fact]
    public void GroundPromptExternalTargetAliases_does_not_add_same_trusted_writable_alias_as_read_only()
    {
        const string writableAlias = "external-target/C/work/apps/Inventory";
        var metadataJson = ExecutionInvocationMetadata.GroundPromptExternalTargetAliases(
            $$"""
              {
                "{{ExecutionInvocationMetadata.ProcessStepAllowsProductMutationMetadataKey}}": true,
                "{{ExecutionInvocationMetadata.AllowedExternalTargetAliasesMetadataKey}}": ["{{writableAlias}}"]
              }
              """,
            "Implement the change in external-target/C/work/apps/Inventory.",
            new AgentWorkspaceToolAccessSettings
            {
                CanReadFiles = true,
                CanWriteFiles = true
            });

        using var document = JsonDocument.Parse(metadataJson);
        var allowedAliases = document.RootElement
            .GetProperty(ExecutionInvocationMetadata.AllowedExternalTargetAliasesMetadataKey)
            .EnumerateArray()
            .Select(item => item.GetString())
            .ToArray();

        Assert.Contains(writableAlias, allowedAliases);
        Assert.False(document.RootElement.TryGetProperty(ExecutionInvocationMetadata.ReadOnlyExternalTargetAliasesMetadataKey, out _));
    }

    [Fact]
    public void GroundPromptExternalTargetAliases_does_not_add_child_alias_covered_by_writable_parent_as_read_only()
    {
        const string writableAlias = "external-target/C/work/apps/Inventory";
        var metadataJson = ExecutionInvocationMetadata.GroundPromptExternalTargetAliases(
            $$"""
              {
                "{{ExecutionInvocationMetadata.ProcessStepAllowsProductMutationMetadataKey}}": true,
                "{{ExecutionInvocationMetadata.AllowedExternalTargetAliasesMetadataKey}}": ["{{writableAlias}}"]
              }
              """,
            "Implement the change in external-target/C/work/apps/Inventory/src/Program.cs.",
            new AgentWorkspaceToolAccessSettings
            {
                CanReadFiles = true,
                CanWriteFiles = true
            });

        using var document = JsonDocument.Parse(metadataJson);

        Assert.False(document.RootElement.TryGetProperty(ExecutionInvocationMetadata.ReadOnlyExternalTargetAliasesMetadataKey, out _));
    }

    [Fact]
    public void GroundPromptExternalTargetAliases_adds_sibling_alias_outside_writable_parent_as_read_only()
    {
        const string writableAlias = "external-target/C/work/apps/Inventory";
        const string siblingAlias = "external-target/C/work/apps/Billing";
        var metadataJson = ExecutionInvocationMetadata.GroundPromptExternalTargetAliases(
            $$"""
              {
                "{{ExecutionInvocationMetadata.ProcessStepAllowsProductMutationMetadataKey}}": true,
                "{{ExecutionInvocationMetadata.AllowedExternalTargetAliasesMetadataKey}}": ["{{writableAlias}}"]
              }
              """,
            $"Compare with {siblingAlias} before implementing.",
            new AgentWorkspaceToolAccessSettings
            {
                CanReadFiles = true,
                CanWriteFiles = true
            });

        using var document = JsonDocument.Parse(metadataJson);
        var readOnlyAliases = document.RootElement
            .GetProperty(ExecutionInvocationMetadata.ReadOnlyExternalTargetAliasesMetadataKey)
            .EnumerateArray()
            .Select(item => item.GetString())
            .ToArray();

        Assert.Contains(siblingAlias, readOnlyAliases);
    }
}
