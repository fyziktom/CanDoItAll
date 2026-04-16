using Bunit;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.CrmHr.Pages;
using CanDoItAll.Modules.Workspace;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components;

public sealed class AiAgentsPageTests
{
    [Fact]
    public async Task Existing_technical_agents_are_projected_into_crm_hr_agent_roster()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var workspaceFactory = harness.Context.Services.GetRequiredService<ICanDoItAllAgentWorkspaceFactory>();
        var partyDirectoryService = harness.Context.Services.GetRequiredService<PartyDirectoryService>();
        var aiAgentService = harness.Context.Services.GetRequiredService<AiAgentService>();
        var workspaceService = workspaceFactory.GetOrganizationWorkspaceService();

        var editor = await workspaceService.GetAgentEditorAsync();
        editor.Name = "Runtime Calculator Builder";
        editor.RoleTitle = "UI builder";
        editor.Summary = "Builds SSR calculator surfaces through the technical agent catalog.";
        editor.Instructions = "Focus on calculator delivery tasks.";
        editor.Status = CanDoItAll.AgentFramework.Models.AgentLifecycleStatus.Active;
        editor.IsTemplate = false;
        editor.TemplateKey = string.Empty;
        editor.Tags =
        [
            "showcase",
            "calculator"
        ];

        var technicalAgentId = await workspaceService.SaveAgentAsync(editor);

        var cut = harness.Context.RenderComponent<CrmHrAgentsPage>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Runtime Calculator Builder", cut.Markup);
            Assert.DoesNotContain("No AI agents yet", cut.Markup);
        });

        var projectedItem = Assert.Single(
            await aiAgentService.ListAgentDirectoryAsync(),
            item => item.TechnicalAgentId == technicalAgentId);
        var workspace = await aiAgentService.GetAgentWorkspaceAsync(projectedItem.PartyId);
        var parties = await partyDirectoryService.ListDirectoryAsync();

        Assert.NotNull(workspace);
        Assert.Equal(technicalAgentId, workspace!.TechnicalAgentId);
        Assert.Contains(
            parties,
            item => item.DisplayName == "Runtime Calculator Builder" && item.PartyType == PartyType.AiAgent);
    }

    [Fact]
    public async Task Creates_ai_agent_party_from_agents_page()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var partyDirectoryService = harness.Context.Services.GetRequiredService<PartyDirectoryService>();

        var cut = harness.Context.RenderComponent<CrmHrAgentsPage>();

        cut.Find("[data-testid='crmhr-agent-name']").Change("Release Copilot");
        cut.Find("[data-testid='crmhr-agent-code']").Change("AI-REL");
        cut.Find("[data-testid='crmhr-agent-summary']").Change("Supports release analysis and deployment notes.");
        cut.Find("[data-testid='crmhr-agent-save-button']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("AI agent created.", cut.Markup);
            Assert.Contains("Release Copilot", cut.Markup);
        });

        var directoryItems = await partyDirectoryService.ListDirectoryAsync();
        Assert.Contains(
            directoryItems,
            item => item.DisplayName == "Release Copilot" && item.PartyType == PartyType.AiAgent);
    }

    [Fact]
    public async Task Saves_ai_agent_profile_with_provider_owner_and_capabilities()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var partyDirectoryService = harness.Context.Services.GetRequiredService<PartyDirectoryService>();
        var aiAgentService = harness.Context.Services.GetRequiredService<AiAgentService>();
        var workspaceService = harness.Context.Services.GetRequiredService<WorkspaceService>();

        var ownerId = await CreatePersonAsync(partyDirectoryService, "Nika Steward", "nika.steward@example.test");
        var agentId = await CreateAgentAsync(partyDirectoryService, "Delivery Analyst");
        var providerSave = await workspaceService.SaveProviderAsync(new ProviderProfileEditorModel
        {
            Name = "Local agent provider",
            ConnectorPluginKey = OllamaProviderAdapter.PluginKey,
            ConfigSchemaVersion = "1.0",
            Configuration = new ConnectorConfigState(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["baseUrl"] = "http://localhost:11434",
                ["defaultModel"] = "llama3.1",
                ["timeoutSeconds"] = "30"
            }),
            IsEnabled = true
        });
        Assert.True(providerSave.IsSuccess);

        var navigation = harness.Context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo($"/crm-hr/agents?partyId={agentId}");

        var cut = harness.Context.RenderComponent<CrmHrAgentsPage>();

        cut.WaitForElement("[data-testid='crmhr-agent-provider']");
        cut.Find("[data-testid='crmhr-agent-provider']").Change(providerSave.Value.ToString());
        cut.Find("[data-testid='crmhr-agent-default-model']").Change("llama3.2");
        cut.Find("[data-testid='crmhr-agent-execution-mode']").Change(AiExecutionMode.ThirdParty.ToString());
        cut.Find("[data-testid='crmhr-agent-owner']").Change(ownerId.ToString());
        cut.Find("[data-testid='crmhr-agent-validation-status']").Change(AiValidationStatus.ReviewRequired.ToString());
        cut.Find("[data-testid='crmhr-agent-last-reviewed-on']").Change("2026-04-03");
        cut.Find("[data-testid='crmhr-agent-notes']").Change("Escalate to human review before production-facing output.");
        cut.Find("[data-testid='crmhr-agent-capability-add']").Click();
        cut.WaitForElement("[data-testid='crmhr-agent-capability-name-0']");
        cut.Find("[data-testid='crmhr-agent-capability-name-0']").Change("Release analysis");
        cut.Find("[data-testid='crmhr-agent-capability-scope-0']").Change("Release notes and deployment readiness");
        cut.Find("[data-testid='crmhr-agent-capability-tool-access-0']").Change("Repository metadata");
        cut.Find("[data-testid='crmhr-agent-capability-limitations-0']").Change("No direct production execution");
        cut.Find("[data-testid='crmhr-agent-capability-notes-0']").Change("Requires steward approval for go-live changes.");
        cut.Find("[data-testid='crmhr-agent-profile-save-button']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("AI agent profile saved.", cut.Markup);
            Assert.Contains("Local agent provider", cut.Markup);
            Assert.Contains("Nika Steward", cut.Markup);
        });

        var workspace = await aiAgentService.GetAgentWorkspaceAsync(agentId);
        Assert.NotNull(workspace);
        Assert.Equal(providerSave.Value, workspace.Profile.ProviderProfileId);
        Assert.Equal(ownerId, workspace.Profile.OwnerPartyId);
        Assert.Equal("llama3.2", workspace.Profile.DefaultModel);
        Assert.Equal(AiExecutionMode.ThirdParty, workspace.Profile.ExecutionMode);
        Assert.Equal(AiValidationStatus.ReviewRequired, workspace.Profile.ValidationStatus);
        Assert.Single(workspace.Profile.Capabilities);
        Assert.Equal("Release analysis", workspace.Profile.Capabilities[0].Name);
    }

    private static async Task<Guid> CreatePersonAsync(PartyDirectoryService partyDirectoryService, string displayName, string email)
    {
        var result = await partyDirectoryService.SavePartyAsync(new PartyEditorModel
        {
            PartyType = PartyType.Person,
            LifecycleStatus = PartyLifecycleStatus.Active,
            DisplayName = displayName,
            Summary = $"{displayName} summary",
            LastChangedBy = "component-tests",
            Roles =
            [
                new PartyRoleAssignmentEditorModel
                {
                    RoleKind = PartyRoleKind.Employee,
                    Title = "Employee",
                    IsPrimary = true
                }
            ],
            ContactPoints =
            [
                new PartyContactPointEditorModel
                {
                    ContactType = PartyContactType.Email,
                    Label = "Primary email",
                    Value = email,
                    NormalizedValue = email.ToLowerInvariant(),
                    IsPrimary = true,
                    IsPublic = true
                }
            ]
        });

        Assert.True(result.IsSuccess);
        return result.Value;
    }

    private static async Task<Guid> CreateAgentAsync(PartyDirectoryService partyDirectoryService, string displayName)
    {
        var result = await partyDirectoryService.SavePartyAsync(new PartyEditorModel
        {
            PartyType = PartyType.AiAgent,
            LifecycleStatus = PartyLifecycleStatus.Active,
            DisplayName = displayName,
            Summary = $"{displayName} summary",
            LastChangedBy = "component-tests"
        });

        Assert.True(result.IsSuccess);
        return result.Value;
    }
}
