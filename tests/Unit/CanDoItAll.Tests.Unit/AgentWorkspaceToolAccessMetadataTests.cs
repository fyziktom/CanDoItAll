using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.Storage;

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

        var alias = AgentWorkspaceToolAccessMetadata.NormalizeExternalTargetAlias(
            @"C:\repositories\OtherRepo",
            new ExternalTargetPathRegistry());

        Assert.Matches("^external-target/v1/[0-9a-f]{24}$", alias!);
        Assert.DoesNotContain("repositories", alias, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void NormalizeExternalTargetAlias_rejects_drive_root()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var alias = AgentWorkspaceToolAccessMetadata.NormalizeExternalTargetAlias(
            @"C:\",
            new ExternalTargetPathRegistry());

        Assert.Null(alias);
    }

    [Theory]
    [InlineData("external-target/C/repositories/demo/../secret")]
    [InlineData("external-target/C/repositories/demo/./src")]
    [InlineData("external-target/C/repositories/demo/..")]
    [InlineData("external-target/C/repositories/demo/.")]
    [InlineData("external-target/C/repositories/demo/..,")]
    [InlineData(@"external-target\C\repositories\demo\..\secret")]
    public void NormalizeExternalTargetAlias_rejects_dot_segments(string alias)
    {
        var normalized = AgentWorkspaceToolAccessMetadata.NormalizeExternalTargetAlias(alias);

        Assert.Null(normalized);
        Assert.False(AgentWorkspaceToolAccessMetadata.IsExternalTargetAliasAllowed(
            alias,
            ["external-target/C/repositories/demo"]));
    }

    [Fact]
    public void NormalizeExternalTargetAlias_preserves_valid_nested_alias()
    {
        var alias = AgentWorkspaceToolAccessMetadata.NormalizeExternalTargetAlias(
            "external-target/c/repositories/demo/src/Calculator/Calculator.csproj");

        Assert.Equal(
            "external-target/C/repositories/demo/src/Calculator/Calculator.csproj",
            alias);
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
    public void Effective_external_target_access_unions_configured_and_run_aliases()
    {
        var configured = new AgentWorkspaceToolAccessSettings
        {
            AllowedExternalTargetAliases =
            [
                "external-target/C/repositories/configured"
            ]
        };

        var access = EffectiveExternalTargetAccessResolver.Resolve(
            configured,
            ["external-target/C/repositories/run-write"],
            [
                "external-target/C/repositories/run-read",
                "external-target/C/repositories/configured/docs"
            ]);

        Assert.Equal(
            [
                "external-target/C/repositories/configured",
                "external-target/C/repositories/run-write"
            ],
            access.WritableAliases);
        Assert.Equal(
            [
                "external-target/C/repositories/run-read",
                "external-target/C/repositories/configured/docs"
            ],
            access.ReadOnlyAliases);
        Assert.True(access.CanWrite("external-target/C/repositories/configured/src/Program.cs"));
        Assert.False(access.CanWrite("external-target/C/repositories/configured/docs/architecture.md"));
        Assert.True(access.CanWrite("external-target/C/repositories/run-write/src/Program.cs"));
        Assert.True(access.CanRead("external-target/C/repositories/run-read/README.md"));
        Assert.False(access.CanWrite("external-target/C/repositories/run-read/README.md"));
    }

    [Fact]
    public void Effective_external_target_access_preserves_writable_child_under_readonly_parent()
    {
        var access = EffectiveExternalTargetAccessResolver.Resolve(
            new AgentWorkspaceToolAccessSettings(),
            ["external-target/C/repositories/products/Calculator"],
            ["external-target/C/repositories/products"]);

        Assert.True(access.CanWrite("external-target/C/repositories/products/Calculator/src/Program.cs"));
        Assert.True(access.CanRead("external-target/C/repositories/products/Inventory/README.md"));
        Assert.False(access.CanWrite("external-target/C/repositories/products/Inventory/README.md"));
    }

    [Fact]
    public void Effective_external_target_access_readonly_invocation_restricts_configured_child_and_allows_explicit_writable_descendant()
    {
        var configured = new AgentWorkspaceToolAccessSettings
        {
            AllowedExternalTargetAliases =
            [
                "external-target/C/repositories/products/Calculator"
            ]
        };

        var access = EffectiveExternalTargetAccessResolver.Resolve(
            configured,
            ["external-target/C/repositories/products/Calculator/generated"],
            ["external-target/C/repositories/products"]);

        Assert.Equal(
            ["external-target/C/repositories/products/Calculator/generated"],
            access.WritableAliases);
        Assert.Equal(
            ["external-target/C/repositories/products"],
            access.ReadOnlyAliases);
        Assert.True(access.CanRead("external-target/C/repositories/products/Calculator/src/Program.cs"));
        Assert.False(access.CanWrite("external-target/C/repositories/products/Calculator/src/Program.cs"));
        Assert.True(access.CanWrite("external-target/C/repositories/products/Calculator/generated/output.json"));
    }

    [Fact]
    public void Write_and_read_round_trip_file_and_storage_tool_settings()
    {
        var storageId = Guid.NewGuid();
        var externalRoot = Path.Combine(Path.GetTempPath(), "agent-access-round-trip", Guid.NewGuid().ToString("N"));
        var externalTargetRegistry = new ExternalTargetPathRegistry();
        var externalAlias = AgentWorkspaceToolAccessMetadata.NormalizeExternalTargetAlias(
            externalRoot,
            externalTargetRegistry)!;
        var configurationJson = AgentWorkspaceToolAccessMetadata.Write(
            """{"existing":true}""",
            new AgentWorkspaceToolAccessSettings
            {
                CanReadFiles = true,
                CanWriteFiles = true,
                AllowedExternalTargetAliases =
                [
                    externalAlias
                ],
                CanReadStorage = true,
                CanWriteStorage = true,
                AllowedStorageCatalogIds =
                [
                    storageId
                ]
            },
            externalTargetRegistry);

        var settings = AgentWorkspaceToolAccessMetadata.Read(configurationJson);

        Assert.True(settings.CanReadFiles);
        Assert.True(settings.CanWriteFiles);
        Assert.Equal([externalAlias], settings.AllowedExternalTargetAliases);
        Assert.StartsWith("external-target/v1/", externalAlias, StringComparison.Ordinal);
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
        var readOnlyProfile = AgentWorkspaceToolAccessProfiles.CreateSettings(AgentWorkspaceToolProfileKind.ReadOnly);

        Assert.True(AgentWorkspaceToolAccessMetadata.IsWorkspaceToolAllowed(readOnlyProfile, "workspace_list_directory"));
        Assert.True(AgentWorkspaceToolAccessMetadata.IsWorkspaceToolAllowed(readOnlyProfile, "workspace_hash_path"));
        Assert.True(AgentWorkspaceToolAccessMetadata.IsWorkspaceToolAllowed(qaProfile, "workspace_dotnet_build"));
        Assert.True(AgentWorkspaceToolAccessMetadata.IsWorkspaceToolAllowed(qaProfile, "workspace_dotnet_stop"));
        Assert.True(AgentWorkspaceToolAccessMetadata.IsWorkspaceToolAllowed(qaProfile, "workspace_write_file"));
        Assert.True(AgentWorkspaceToolAccessMetadata.IsWorkspaceToolAllowed(qaProfile, "workspace_write_spreadsheet"));
        Assert.True(AgentWorkspaceToolAccessMetadata.IsWorkspaceToolAllowed(qaProfile, "workspace_zip_path"));
        Assert.True(AgentWorkspaceToolAccessMetadata.IsWorkspaceToolAllowed(qaProfile, "workspace_unzip_archive"));
        Assert.False(AgentWorkspaceToolAccessMetadata.IsWorkspaceToolAllowed(qaProfile, "workspace_dotnet_new"));
        Assert.False(AgentWorkspaceToolAccessMetadata.IsWorkspaceToolAllowed(qaProfile, "workspace_delete_path"));
        Assert.True(AgentWorkspaceToolAccessMetadata.IsWorkspaceToolAllowed(businessProfile, "workspace_convert_document"));
        Assert.True(AgentWorkspaceToolAccessMetadata.IsWorkspaceToolAllowed(businessProfile, "workspace_spreadsheet_function_catalog"));
        Assert.True(AgentWorkspaceToolAccessMetadata.IsWorkspaceToolAllowed(businessProfile, "workspace_read_spreadsheet_range"));
        Assert.True(AgentWorkspaceToolAccessMetadata.IsWorkspaceToolAllowed(businessProfile, "workspace_write_spreadsheet"));
        Assert.True(AgentWorkspaceToolAccessMetadata.IsWorkspaceToolAllowed(businessProfile, "workspace_analyze_image"));
        Assert.True(AgentWorkspaceToolAccessMetadata.IsWorkspaceToolAllowed(businessProfile, "workspace_analyze_images"));
        Assert.False(AgentWorkspaceToolAccessMetadata.IsWorkspaceToolAllowed(businessProfile, "workspace_dotnet_run"));
        Assert.False(AgentWorkspaceToolAccessMetadata.IsWorkspaceToolAllowed(businessProfile, "workspace_dotnet_stop"));
        Assert.False(AgentWorkspaceToolAccessMetadata.IsWorkspaceToolAllowed(
            AgentWorkspaceToolAccessProfiles.CreateSettings(AgentWorkspaceToolProfileKind.ReadOnly),
            "workspace_analyze_images"));
    }

    [Fact]
    public void IsWorkspaceToolAllowed_classifies_git_read_and_mutation_tools()
    {
        var readOnly = AgentWorkspaceToolAccessProfiles.CreateSettings(AgentWorkspaceToolProfileKind.ReadOnly);
        var softwareDevelopment = AgentWorkspaceToolAccessProfiles.CreateSettings(AgentWorkspaceToolProfileKind.SoftwareDevelopment);

        Assert.True(AgentWorkspaceToolAccessMetadata.IsWorkspaceToolAllowed(readOnly, "workspace_git_status"));
        Assert.True(AgentWorkspaceToolAccessMetadata.IsWorkspaceToolAllowed(readOnly, "workspace_git_diff"));
        Assert.True(AgentWorkspaceToolAccessMetadata.IsWorkspaceToolAllowed(readOnly, "workspace_git_log"));
        Assert.True(AgentWorkspaceToolAccessMetadata.IsWorkspaceToolAllowed(readOnly, "workspace_git_show"));
        Assert.False(AgentWorkspaceToolAccessMetadata.IsWorkspaceToolAllowed(readOnly, "workspace_git_add"));
        Assert.False(AgentWorkspaceToolAccessMetadata.IsWorkspaceToolAllowed(readOnly, "workspace_git_unstage"));
        Assert.False(AgentWorkspaceToolAccessMetadata.IsWorkspaceToolAllowed(readOnly, "workspace_git_commit"));
        Assert.False(AgentWorkspaceToolAccessMetadata.IsWorkspaceToolAllowed(readOnly, "workspace_git_branch_create"));
        Assert.False(AgentWorkspaceToolAccessMetadata.IsWorkspaceToolAllowed(readOnly, "workspace_git_switch"));

        Assert.True(AgentWorkspaceToolAccessMetadata.IsWorkspaceToolAllowed(softwareDevelopment, "workspace_git_add"));
        Assert.True(AgentWorkspaceToolAccessMetadata.IsWorkspaceToolAllowed(softwareDevelopment, "workspace_git_unstage"));
        Assert.True(AgentWorkspaceToolAccessMetadata.IsWorkspaceToolAllowed(softwareDevelopment, "workspace_git_commit"));
        Assert.True(AgentWorkspaceToolAccessMetadata.IsWorkspaceToolAllowed(softwareDevelopment, "workspace_git_branch_create"));
        Assert.True(AgentWorkspaceToolAccessMetadata.IsWorkspaceToolAllowed(softwareDevelopment, "workspace_git_switch"));
    }

    [Fact]
    public void GroundPromptExternalTargetAliases_adds_prompt_absolute_path_as_allowed_alias_for_write_enabled_agent()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var externalTargetRegistry = new ExternalTargetPathRegistry();
        var expectedAlias = AgentWorkspaceToolAccessMetadata.NormalizeExternalTargetAlias(
            @"C:\programovani\outputsfromtests\dotnet\BikeRepairSlotScheduler",
            externalTargetRegistry);
        var metadataJson = ExecutionInvocationMetadata.GroundPromptExternalTargetAliases(
            "{}",
            """analyze "C:\programovani\outputsfromtests\dotnet\BikeRepairSlotScheduler" and add architecture""",
            new AgentWorkspaceToolAccessSettings
            {
                CanReadFiles = true,
                CanWriteFiles = true
            },
            externalTargetRegistry);

        using var document = JsonDocument.Parse(metadataJson);
        var aliases = document.RootElement
            .GetProperty(ExecutionInvocationMetadata.AllowedExternalTargetAliasesMetadataKey)
            .EnumerateArray()
            .Select(item => item.GetString())
            .ToArray();

        Assert.Contains(expectedAlias, aliases);
        Assert.True(document.RootElement.TryGetProperty(
            ExecutionInvocationMetadata.ExternalTargetRootBindingsMetadataKey,
            out var bindings));
        Assert.Equal(JsonValueKind.Array, bindings.ValueKind);
        Assert.DoesNotContain(
            @"C:\programovani\outputsfromtests\dotnet\BikeRepairSlotScheduler",
            metadataJson,
            StringComparison.OrdinalIgnoreCase);
        Assert.False(document.RootElement.TryGetProperty(ExecutionInvocationMetadata.ReadOnlyExternalTargetAliasesMetadataKey, out _));
    }

    [Fact]
    public void GroundPromptExternalTargetAliases_adds_prompt_absolute_path_as_readonly_alias_for_read_only_agent()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var externalTargetRegistry = new ExternalTargetPathRegistry();
        var expectedAlias = AgentWorkspaceToolAccessMetadata.NormalizeExternalTargetAlias(
            @"C:\programovani\outputsfromtests\dotnet\BikeRepairSlotScheduler",
            externalTargetRegistry);
        var metadataJson = ExecutionInvocationMetadata.GroundPromptExternalTargetAliases(
            "{}",
            """inspect C:\programovani\outputsfromtests\dotnet\BikeRepairSlotScheduler before reporting""",
            new AgentWorkspaceToolAccessSettings
            {
                CanReadFiles = true,
                CanWriteFiles = false
            },
            externalTargetRegistry);

        using var document = JsonDocument.Parse(metadataJson);
        var aliases = document.RootElement
            .GetProperty(ExecutionInvocationMetadata.ReadOnlyExternalTargetAliasesMetadataKey)
            .EnumerateArray()
            .Select(item => item.GetString())
            .ToArray();

        Assert.Contains(expectedAlias, aliases);
    }

    [Fact]
    public void GroundPromptExternalTargetAliases_adds_prompt_absolute_path_as_readonly_alias_when_process_step_disallows_product_mutation()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var externalTargetRegistry = new ExternalTargetPathRegistry();
        var expectedAlias = AgentWorkspaceToolAccessMetadata.NormalizeExternalTargetAlias(
            @"C:\programovani\outputsfromtests\dotnet\BikeRepairSlotScheduler",
            externalTargetRegistry);
        var metadataJson = ExecutionInvocationMetadata.GroundPromptExternalTargetAliases(
            $"{{\"{ExecutionInvocationMetadata.ProcessStepAllowsProductMutationMetadataKey}\":false}}",
            """inspect C:\programovani\outputsfromtests\dotnet\BikeRepairSlotScheduler before reporting""",
            new AgentWorkspaceToolAccessSettings
            {
                CanReadFiles = true,
                CanWriteFiles = true
            },
            externalTargetRegistry);

        using var document = JsonDocument.Parse(metadataJson);
        var readOnlyAliases = document.RootElement
            .GetProperty(ExecutionInvocationMetadata.ReadOnlyExternalTargetAliasesMetadataKey)
            .EnumerateArray()
            .Select(item => item.GetString())
            .ToArray();

        Assert.Contains(expectedAlias, readOnlyAliases);
        Assert.False(document.RootElement.TryGetProperty(ExecutionInvocationMetadata.AllowedExternalTargetAliasesMetadataKey, out _));
    }

    [Fact]
    public void GroundPromptExternalTargetAliases_keeps_process_free_text_alias_read_only_even_when_product_mutation_is_allowed()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var externalTargetRegistry = new ExternalTargetPathRegistry();
        var expectedAlias = AgentWorkspaceToolAccessMetadata.NormalizeExternalTargetAlias(
            @"C:\programovani\outputsfromtests\dotnet\BikeRepairSlotScheduler",
            externalTargetRegistry);
        var metadataJson = ExecutionInvocationMetadata.GroundPromptExternalTargetAliases(
            $"{{\"{ExecutionInvocationMetadata.ProcessStepAllowsProductMutationMetadataKey}\":true}}",
            """implementation prompt mentions C:\programovani\outputsfromtests\dotnet\BikeRepairSlotScheduler""",
            new AgentWorkspaceToolAccessSettings
            {
                CanReadFiles = true,
                CanWriteFiles = true
            },
            externalTargetRegistry);

        using var document = JsonDocument.Parse(metadataJson);
        var readOnlyAliases = document.RootElement
            .GetProperty(ExecutionInvocationMetadata.ReadOnlyExternalTargetAliasesMetadataKey)
            .EnumerateArray()
            .Select(item => item.GetString())
            .ToArray();

        Assert.Contains(expectedAlias, readOnlyAliases);
        Assert.False(document.RootElement.TryGetProperty(ExecutionInvocationMetadata.AllowedExternalTargetAliasesMetadataKey, out _));
    }

    [Fact]
    public void GroundPromptExternalTargetAliases_does_not_add_same_trusted_writable_alias_as_read_only()
    {
        var externalTargetRegistry = new ExternalTargetPathRegistry();
        var writablePath = Path.Combine(Path.GetTempPath(), "work", "apps", "Inventory");
        Assert.True(externalTargetRegistry.TryCreateAlias(writablePath, out var writableAlias));
        var metadataJson = ExecutionInvocationMetadata.GroundPromptExternalTargetAliases(
            $$"""
              {
                "{{ExecutionInvocationMetadata.ProcessStepAllowsProductMutationMetadataKey}}": true,
                "{{ExecutionInvocationMetadata.AllowedExternalTargetAliasesMetadataKey}}": ["{{writableAlias}}"]
              }
              """,
            $"Implement the change in {writableAlias}.",
            new AgentWorkspaceToolAccessSettings
            {
                CanReadFiles = true,
                CanWriteFiles = true
            },
            externalTargetRegistry);

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
        var externalTargetRegistry = new ExternalTargetPathRegistry();
        var writablePath = Path.Combine(Path.GetTempPath(), "work", "apps", "Inventory");
        var childPath = Path.Combine(writablePath, "src", "Program.cs");
        Assert.True(externalTargetRegistry.TryCreateAlias(writablePath, out var writableAlias));
        Assert.True(externalTargetRegistry.TryCreateAlias(childPath, out var childAlias));
        var metadataJson = ExecutionInvocationMetadata.GroundPromptExternalTargetAliases(
            $$"""
              {
                "{{ExecutionInvocationMetadata.ProcessStepAllowsProductMutationMetadataKey}}": true,
                "{{ExecutionInvocationMetadata.AllowedExternalTargetAliasesMetadataKey}}": ["{{writableAlias}}"]
              }
              """,
            $"Implement the change in {childAlias}.",
            new AgentWorkspaceToolAccessSettings
            {
                CanReadFiles = true,
                CanWriteFiles = true
            },
            externalTargetRegistry);

        using var document = JsonDocument.Parse(metadataJson);

        Assert.False(document.RootElement.TryGetProperty(ExecutionInvocationMetadata.ReadOnlyExternalTargetAliasesMetadataKey, out _));
    }

    [Fact]
    public void GroundPromptExternalTargetAliases_adds_sibling_alias_outside_writable_parent_as_read_only()
    {
        var externalTargetRegistry = new ExternalTargetPathRegistry();
        var appsPath = Path.Combine(Path.GetTempPath(), "work", "apps");
        Assert.True(externalTargetRegistry.TryCreateAlias(Path.Combine(appsPath, "Inventory"), out var writableAlias));
        Assert.True(externalTargetRegistry.TryCreateAlias(Path.Combine(appsPath, "Billing"), out var siblingAlias));
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
            },
            externalTargetRegistry);

        using var document = JsonDocument.Parse(metadataJson);
        var readOnlyAliases = document.RootElement
            .GetProperty(ExecutionInvocationMetadata.ReadOnlyExternalTargetAliasesMetadataKey)
            .EnumerateArray()
            .Select(item => item.GetString())
            .ToArray();

        Assert.Contains(siblingAlias, readOnlyAliases);
    }

    [Fact]
    public void ApplyExternalTargetRootBindings_rejects_malformed_persisted_authority_metadata()
    {
        var binding = new ExternalTargetRootBinding(
            "0123456789abcdef01234567",
            "windows",
            "protected-token");
        var metadataJson = $$"""
          {
            "{{ExecutionInvocationMetadata.ExternalTargetRootBindingsMetadataKey}}": {
              "rootId": "not-an-array"
            }
          }
          """;

        var exception = Assert.Throws<InvalidOperationException>(() =>
            ExecutionInvocationMetadata.ApplyExternalTargetRootBindings(
                metadataJson,
                [binding]));

        Assert.Contains("malformed", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ResolveExternalTargetRootBindings_rejects_malformed_persisted_authority_record()
    {
        var run = CreateExecutionRun(
            $$"""
              {
                "{{ExecutionInvocationMetadata.ExternalTargetRootBindingsMetadataKey}}": [
                  {
                    "rootId": "invalid",
                    "hostPlatform": "windows",
                    "protectedRootToken": "protected-token"
                  }
                ]
              }
              """);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            ExecutionInvocationMetadata.ResolveExternalTargetRootBindings(run));

        Assert.Contains("malformed", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Execution_events_keep_opaque_aliases_without_external_root_binding_authority()
    {
        const string alias = "external-target/v1/0123456789abcdef01234567";
        const string protectedToken = "protected-root-token-must-not-leak";
        var binding = new ExternalTargetRootBinding(
            "0123456789abcdef01234567",
            OperatingSystem.IsWindows() ? "windows" : "linux",
            protectedToken);
        var metadataJson = ExecutionInvocationMetadata.ApplyExternalTargetRootBindings(
            $$"""
              {
                "{{ExecutionInvocationMetadata.ReadOnlyExternalTargetAliasesMetadataKey}}": ["{{alias}}"]
              }
              """,
            [binding]);
        var run = CreateExecutionRun(metadataJson);
        var entry = new ExecutionLogEntry(
            Guid.NewGuid(),
            run.AgentId,
            run.ChatSessionId,
            DateTimeOffset.UtcNow,
            ExecutionState.Running,
            "test",
            "event projection");
        var factory = typeof(AgentFrameworkWorkspaceExecutionService).GetMethod(
            "CreateExecutionEvent",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        var executionEvent = Assert.IsType<ExecutionEvent>(factory?.Invoke(null, [run, entry]));

        Assert.Contains(alias, executionEvent.MetadataJson, StringComparison.Ordinal);
        Assert.DoesNotContain(protectedToken, executionEvent.MetadataJson, StringComparison.Ordinal);
        using var document = JsonDocument.Parse(executionEvent.MetadataJson);
        Assert.False(document.RootElement.TryGetProperty(
            ExecutionInvocationMetadata.ExternalTargetRootBindingsMetadataKey,
            out _));
    }

    private static ExecutionRunRecord CreateExecutionRun(string metadataJson)
    {
        var now = DateTimeOffset.UtcNow;
        return new ExecutionRunRecord(
            Guid.NewGuid(),
            Guid.NewGuid(),
            null,
            "External target binding test",
            "chat-session",
            Guid.NewGuid().ToString("N"),
            string.Empty,
            string.Empty,
            "test",
            "interactive",
            metadataJson,
            string.Empty,
            string.Empty,
            "test-provider",
            "test-model",
            ExecutionState.Idle,
            null,
            now,
            now,
            null,
            null,
            string.Empty,
            null,
            []);
    }
}
