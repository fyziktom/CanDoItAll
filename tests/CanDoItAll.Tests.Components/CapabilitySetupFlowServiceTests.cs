using Bunit;
using CanDoItAll.AgentFramework.Capabilities.Abstractions;
using CanDoItAll.AgentFramework.Capabilities.Templates;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.AgentFramework.Pages.Components;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components;

public sealed class CapabilitySetupFlowServiceTests
{
    [Fact]
    public async Task Tool_setup_test_returns_typed_json_parse_diagnostic_before_process_launch()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var setupFlowService = harness.Context.Services.GetRequiredService<IAgentCapabilitySetupFlowService>();

        var result = await setupFlowService.TestToolSetupAsync(new CapabilityToolSetupTestRequest
        {
            Capability = new CapabilityEditorModel
            {
                Kind = CanDoItAll.AgentFramework.Models.CapabilityKind.Tool,
                Key = "external-audit",
                Name = "External Audit",
                EndpointOrPath = "dotnet",
                ConfigurationJson = """
                {
                  "toolKind": "externalProcess",
                  "runtimeToolName": "external_audit",
                  "implementationKey": "external.audit",
                  "externalProcess": {
                    "command": "dotnet",
                    "workingDirectory": ".",
                    "allowedExecutableNames": [ "dotnet" ],
                    "requiredOutputProperties": [ "ok" ]
                  }
                }
                """
            },
            JsonInput = "{not-json"
        });

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Category == CapabilityDiagnosticCategory.JsonParse &&
            diagnostic.FieldPath == "$.jsonInput");
    }

    [Fact]
    public async Task Mcp_setup_test_reports_missing_runtime_adapter_as_typed_diagnostic()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var setupFlowService = harness.Context.Services.GetRequiredService<IAgentCapabilitySetupFlowService>();

        var result = await setupFlowService.TestMcpSetupAsync(new CapabilityMcpSetupTestRequest
        {
            Capability = new CapabilityEditorModel
            {
                Kind = CanDoItAll.AgentFramework.Models.CapabilityKind.McpServer,
                Key = "sample-mcp",
                Name = "Sample MCP",
                ConfigurationJson = """
                {
                  "transport": "logical",
                  "serverName": "sample-mcp",
                  "allowedTools": [ "sample_tool" ]
                }
                """
            }
        });

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Category == CapabilityDiagnosticCategory.ImplementationMissing &&
            diagnostic.FieldPath == "$.mcpSetupService");
    }

    [Fact]
    public async Task Access_preview_denies_matching_external_action_tool()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var workspaceService = harness.Context.Services.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var setupFlowService = harness.Context.Services.GetRequiredService<IAgentCapabilitySetupFlowService>();
        var capabilityId = await workspaceService.SaveCapabilityAsync(new CapabilityEditorModel
        {
            Kind = CanDoItAll.AgentFramework.Models.CapabilityKind.Tool,
            Key = "external-audit",
            Name = "External Audit",
            EndpointOrPath = "dotnet",
            Tags = ["external"],
            ConfigurationJson = """
            {
              "toolKind": "externalProcess",
              "runtimeToolName": "external_audit",
              "implementationKey": "external.audit",
              "operationClassifications": [ "externalAction" ],
              "externalProcess": {
                "command": "dotnet",
                "workingDirectory": "."
              }
            }
            """
        });

        var result = await setupFlowService.PreviewAccessAsync(new CapabilityAccessPreviewRequest
        {
            CapabilityIds = [capabilityId],
            Policy = new CapabilityAccessPolicyTemplateDto
            {
                DefaultEffect = "inherit",
                Rules =
                [
                    new CapabilityAccessRuleTemplateDto
                    {
                        Id = "deny-external-action",
                        Effect = "deny",
                        Scope = "uiPreview",
                        Selector = new CapabilitySelectorTemplateDto
                        {
                            Kind = "operationClassification",
                            Value = "externalAction"
                        },
                        Reason = "External action tools require explicit approval."
                    }
                ]
            }
        });

        Assert.True(result.ValidationResult.IsValid);
        Assert.Empty(result.EffectiveSet.AllowedCapabilities);
        Assert.Contains(result.EffectiveSet.Diagnostics, diagnostic =>
            diagnostic.Identity.Key == CapabilityKey.Create("external-audit") &&
            diagnostic.Category == CapabilityDiagnosticCategory.AccessPolicy);
    }

    [Fact]
    public async Task Agent_capabilities_panel_renders_tool_creation_and_access_preview_controls()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var workspaceService = harness.Context.Services.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var capabilityId = await workspaceService.SaveCapabilityAsync(new CapabilityEditorModel
        {
            Kind = CanDoItAll.AgentFramework.Models.CapabilityKind.Tool,
            Key = "external-audit",
            Name = "External Audit",
            EndpointOrPath = "dotnet",
            ConfigurationJson = """
            {
              "toolKind": "externalProcess",
              "runtimeToolName": "external_audit",
              "implementationKey": "external.audit",
              "externalProcess": {
                "command": "dotnet",
                "workingDirectory": "."
              }
            }
            """
        });
        await workspaceService.SaveAgentAsync(new AgentEditorModel
        {
            Name = "Capability Tester",
            RoleTitle = "Runtime tester",
            Summary = "Tests capability setup UI.",
            Instructions = "Inspect capability setup.",
            SelectedCapabilityIds = [capabilityId]
        });

        var cut = harness.Context.RenderComponent<AgentCapabilitiesPanel>();

        cut.WaitForElement("[data-testid='agents-capability-new-tool']");
        cut.WaitForElement("[data-testid='agents-capability-access-preview']");
        Assert.NotNull(cut.Find("[data-testid='agents-capability-type-filter']"));
    }
}
