using Bunit;
using CanDoItAll.Modules.Workspace;
using CanDoItAll.Modules.Workspace.Pages;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components;

public sealed class SettingsPageProjectStructureAgentTests
{
    [Fact]
    public async Task Settings_page_renders_project_structure_agent_surface_with_profile_and_setup_guidance()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var administrationService = harness.Context.Services.GetRequiredService<ProjectStructureAgentAdministrationService>();

        await administrationService.SaveSettingsAsync(new ProjectStructureAgentWorkspaceSettingsModel
        {
            CentralBaseUrl = "http://main-machine:5032",
            InstallScriptPath = @"tools\Install-CanDoItAllProjectStructureMcp.ps1",
            SetupReadmePath = @"docs\project-structure-mcp-setup.md"
        });

        var saveResult = await administrationService.SaveProfileAsync(new ProjectStructureAgentProfileEditorModel
        {
            Name = "Component Test Agent",
            Description = "Component coverage for the project-structure MCP settings surface.",
            CapabilityMask = ProjectStructureAgentCapability.All,
            GenerateNewToken = true
        });

        Assert.True(saveResult.IsSuccess);

        var cut = harness.Context.RenderComponent<SettingsPage>();

        cut.FindAll("button")
            .First(button => button.TextContent.Contains("Project Structure MCP", StringComparison.Ordinal))
            .Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Project-structure MCP profile", cut.Markup);
            Assert.Contains("Component Test Agent", cut.Markup);
            Assert.Contains("project-structure-setup-guide", cut.Markup);
            Assert.Contains("Humans are trying to make their surrounding better", cut.Markup);
            Assert.Contains("project-structure-profile-save", cut.Markup);
        });
    }
}
