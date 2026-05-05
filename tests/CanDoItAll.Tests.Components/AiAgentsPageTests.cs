using Bunit;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.AgentFramework.Pages;
using CanDoItAll.Modules.AgentFramework.Pages.Components;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.CrmHr.Pages;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workspace;
using CanDoItAll.Tests.Support;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;

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
        editor.Name = "Runtime Workflow Builder";
        editor.RoleTitle = "UI builder";
        editor.Summary = "Builds SSR workflow surfaces through the technical agent catalog.";
        editor.Instructions = "Focus on workflow delivery tasks.";
        editor.Status = CanDoItAll.AgentFramework.Models.AgentLifecycleStatus.Active;
        editor.IsTemplate = false;
        editor.TemplateKey = string.Empty;
        editor.Tags =
        [
            "showcase",
            "workflow"
        ];

        var technicalAgentId = await workspaceService.SaveAgentAsync(editor);

        var cut = harness.Context.RenderComponent<CrmHrAgentsPage>();
        Assert.DoesNotContain("Create technical agents in AgentFramework", cut.Markup);

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Runtime Workflow Builder", cut.Markup);
            Assert.DoesNotContain("No projected AI agents", cut.Markup);
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
            item => item.DisplayName == "Runtime Workflow Builder" && item.PartyType == PartyType.AiAgent);
    }

    [Fact]
    public async Task Agent_roster_excludes_ai_parties_without_agentframework_profiles()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("candoitall-component-tests");
        var activeProfile = testEnvironment.CreateInMemoryProfile("primary", $"roster-excludes-{Guid.NewGuid():N}");
        await using var harness = await ComponentTestHarness.CreateAsync(options: new TestHarnessOptions
        {
            TestEnvironment = testEnvironment,
            ActiveProfile = activeProfile
        });
        var workspaceFactory = harness.Context.Services.GetRequiredService<ICanDoItAllAgentWorkspaceFactory>();
        var partyDirectoryService = harness.Context.Services.GetRequiredService<PartyDirectoryService>();
        var aiAgentService = harness.Context.Services.GetRequiredService<AiAgentService>();
        var workspaceService = workspaceFactory.GetOrganizationWorkspaceService();

        await CreateAgentAsync(partyDirectoryService, "CRM Only Agent");

        var editor = await workspaceService.GetAgentEditorAsync();
        editor.Name = "AgentFramework QA";
        editor.RoleTitle = "QA";
        editor.Summary = "Lives in AgentFramework and should be visible in CRM-HR.";
        editor.Status = CanDoItAll.AgentFramework.Models.AgentLifecycleStatus.Active;
        editor.IsTemplate = false;
        editor.TemplateKey = string.Empty;

        await workspaceService.SaveAgentAsync(editor);

        var cut = harness.Context.RenderComponent<CrmHrAgentsPage>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("AgentFramework QA", cut.Markup);
            Assert.DoesNotContain("CRM Only Agent", cut.Markup);
        });

        var roster = await aiAgentService.ListAgentDirectoryAsync();
        Assert.Contains(roster, item => item.DisplayName == "AgentFramework QA");
        Assert.DoesNotContain(roster, item => item.DisplayName == "CRM Only Agent");
        cut.Dispose();
    }

    [Fact]
    public async Task Creates_agentframework_agent_from_canonical_catalog_and_projects_it_into_crm_hr()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var partyDirectoryService = harness.Context.Services.GetRequiredService<PartyDirectoryService>();
        var aiAgentService = harness.Context.Services.GetRequiredService<AiAgentService>();
        var navigation = harness.Context.Services.GetRequiredService<NavigationManager>();

        navigation.NavigateTo("/agents?tab=agents");
        var host = harness.Context.RenderComponent<DialogHost>();
        var cut = harness.Context.RenderComponent<AgentsHomePage>();

        cut.WaitForElement("[data-testid='agents-catalog-new']");
        cut.Find("[data-testid='agents-catalog-new']").Click();
        host.WaitForElement("[data-testid='agents-catalog-name']");
        host.Find("[data-testid='agents-catalog-name']").Change("Release Copilot");
        host.Find("[data-testid='agents-catalog-role']").Change("Release analyst");
        host.Find("[data-testid='agents-catalog-summary']").Change("Supports release analysis and deployment notes.");
        host.Find("[data-testid='agents-catalog-instructions']").Change("Review release scope and produce durable evidence.");
        host.Find("[data-testid='agents-catalog-save']").Click();

        var notificationService = harness.Context.Services.GetRequiredService<NotificationService>();
        cut.WaitForAssertion(() =>
        {
            Assert.Contains(notificationService.Messages, message => message.Summary == "Agent saved");
            Assert.Contains("Release Copilot", cut.Markup);
        });

        var rosterItem = Assert.Single(
            await aiAgentService.ListAgentDirectoryAsync(),
            item => item.DisplayName == "Release Copilot");
        var workspace = await aiAgentService.GetAgentWorkspaceAsync(rosterItem.PartyId);
        var directoryItems = await partyDirectoryService.ListDirectoryAsync();

        Assert.Contains(
            directoryItems,
            item => item.DisplayName == "Release Copilot" && item.PartyType == PartyType.AiAgent);

        Assert.NotNull(rosterItem.TechnicalAgentId);
        Assert.NotNull(workspace);
        Assert.NotNull(workspace!.TechnicalAgentId);
    }

    [Fact]
    public async Task Crm_hr_agents_page_routes_technical_editing_to_agentframework()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("candoitall-component-tests");
        var activeProfile = testEnvironment.CreateInMemoryProfile("primary", $"crmhr-agent-route-{Guid.NewGuid():N}");
        await using var harness = await ComponentTestHarness.CreateAsync(options: new TestHarnessOptions
        {
            TestEnvironment = testEnvironment,
            ActiveProfile = activeProfile
        });
        var aiAgentService = harness.Context.Services.GetRequiredService<AiAgentService>();
        var workspaceFactory = harness.Context.Services.GetRequiredService<ICanDoItAllAgentWorkspaceFactory>();
        var navigation = harness.Context.Services.GetRequiredService<NavigationManager>();
        var workspaceService = workspaceFactory.GetOrganizationWorkspaceService();
        var editor = await workspaceService.GetAgentEditorAsync();
        editor.Name = "Directed Runtime Reviewer";
        editor.RoleTitle = "Reviewer";
        editor.Summary = "Technical record that CRM-HR should route back into AgentFramework.";
        editor.Instructions = "Own review findings and explicit residual risk.";
        editor.Status = AgentLifecycleStatus.Active;
        editor.IsTemplate = false;
        editor.TemplateKey = string.Empty;

        var technicalAgentId = await workspaceService.SaveAgentAsync(editor);
        var rosterItem = Assert.Single(
            await aiAgentService.ListAgentDirectoryAsync(),
            item => item.TechnicalAgentId == technicalAgentId);

        navigation.NavigateTo("/crm-hr/agents");
        navigation.NavigateTo(navigation.GetUriWithQueryParameter("partyId", rosterItem.PartyId));

        var cut = harness.Context.RenderComponent<CrmHrAgentsPage>();
        cut.Instance.PartyIdQuery = rosterItem.PartyId;
        await cut.InvokeAsync(() => cut.Instance.SetParametersAsync(ParameterView.Empty));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Directed Runtime Reviewer", cut.Markup);
        });

        cut.WaitForElement("[data-testid='crmhr-agent-open-technical-record']");
        cut.Find("[data-testid='crmhr-agent-open-technical-record']").Click();

        Assert.EndsWith($"/agents?tab=agents&agentId={technicalAgentId:D}", navigation.Uri, StringComparison.OrdinalIgnoreCase);
        cut.Dispose();
    }

    [Fact]
    public async Task Agents_page_imports_legacy_crm_hr_agents_into_the_visible_agent_catalog()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var workspaceFactory = harness.Context.Services.GetRequiredService<ICanDoItAllAgentWorkspaceFactory>();
        var partyDirectoryService = harness.Context.Services.GetRequiredService<PartyDirectoryService>();
        var dbContextFactory = harness.Context.Services.GetRequiredService<IDbContextFactory<AppDbContext>>();
        var navigation = harness.Context.Services.GetRequiredService<NavigationManager>();

        var partyId = await CreateAgentAsync(partyDirectoryService, "Legacy catalog party");
        var legacyWorkspace = workspaceFactory.GetWorkspaceService(WorkspaceScopeDescriptor.Organization("legacy-catalog"));
        var editor = await legacyWorkspace.GetAgentEditorAsync();
        editor.Name = "Showcase Lead Engineer";
        editor.RoleTitle = "Lead engineer";
        editor.Summary = "Legacy agent that must be promoted into the current AgentFramework catalog.";
        editor.Instructions = "Own the Blazor SSR delivery path.";
        editor.Status = AgentLifecycleStatus.Active;
        editor.IsTemplate = false;
        editor.TemplateKey = string.Empty;
        editor.Tags =
        [
            "crm-hr",
            $"party-{partyId:N}"
        ];
        editor.ConfigurationJson = JsonSerializer.Serialize(new
        {
            crmHr = new
            {
                partyId,
                executionMode = "Remote",
                source = "crm-hr",
                capabilities = Array.Empty<string>()
            }
        });

        var legacyTechnicalAgentId = await legacyWorkspace.SaveAgentAsync(editor);

        await using (var dbContext = await dbContextFactory.CreateDbContextAsync())
        {
            dbContext.Set<AiResourceBinding>().Add(new AiResourceBinding
            {
                PartyId = partyId,
                TechnicalAgentId = legacyTechnicalAgentId,
                BindingStatus = AiResourceBindingStatus.Bound,
                BindingReason = "Legacy organization scope binding.",
                LastError = string.Empty,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                UpdatedAtUtc = DateTimeOffset.UtcNow
            });
            await dbContext.SaveChangesAsync();
        }

        await RepairAndSynchronizeAsync(harness.Context.Services);

        navigation.NavigateTo("/agents?tab=agents");
        var cut = harness.Context.RenderComponent<AgentsHomePage>();
        cut.WaitForAssertion(() =>
        {
            Assert.DoesNotContain("Loading canonical agent runtime", cut.Markup);
        });

        var currentWorkspace = workspaceFactory.GetOrganizationWorkspaceService();
        var currentAgents = await currentWorkspace.ListAgentsAsync(includeTemplates: false);

        Assert.Contains(currentAgents, item => item.Id == legacyTechnicalAgentId && item.Name == "Showcase Lead Engineer");

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Showcase Lead Engineer", cut.Markup);
        });

        cut.Dispose();
    }

    [Fact]
    public async Task Agents_page_exposes_feed_defaults_action()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var navigation = harness.Context.Services.GetRequiredService<NavigationManager>();

        navigation.NavigateTo("/agents?tab=agents");
        var cut = harness.Context.RenderComponent<AgentsHomePage>();

        cut.WaitForAssertion(() =>
        {
            Assert.NotNull(cut.Find("[data-testid='agents-shell-feed-defaults']"));
        });
    }

    [Fact]
    public async Task Current_projected_agents_visible_in_crm_hr_must_also_be_visible_in_agents_page()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("candoitall-component-tests");
        var activeProfile = testEnvironment.CreateInMemoryProfile("primary", $"cross-surface-{Guid.NewGuid():N}");
        await using var harness = await ComponentTestHarness.CreateAsync(options: new TestHarnessOptions
        {
            TestEnvironment = testEnvironment,
            ActiveProfile = activeProfile
        });
        var workspaceFactory = harness.Context.Services.GetRequiredService<ICanDoItAllAgentWorkspaceFactory>();
        var aiAgentService = harness.Context.Services.GetRequiredService<AiAgentService>();
        var navigation = harness.Context.Services.GetRequiredService<NavigationManager>();
        var workspaceService = workspaceFactory.GetOrganizationWorkspaceService();

        var editor = await workspaceService.GetAgentEditorAsync();
        editor.Name = "Cross Surface Runtime Agent";
        editor.RoleTitle = "Lead engineer";
        editor.Summary = "Current canonical agent that must appear in both CRM-HR and AgentFramework.";
        editor.Instructions = "Own the Blazor SSR delivery path.";
        editor.Status = AgentLifecycleStatus.Active;
        editor.IsTemplate = false;
        editor.TemplateKey = string.Empty;

        var technicalAgentId = await workspaceService.SaveAgentAsync(editor);
        var rosterItem = Assert.Single(
            await aiAgentService.ListAgentDirectoryAsync(),
            item => item.TechnicalAgentId == technicalAgentId);

        navigation.NavigateTo($"/crm-hr/agents?partyId={rosterItem.PartyId:D}");
        var crmCut = harness.Context.RenderComponent<CrmHrAgentsPage>();
        crmCut.WaitForAssertion(() =>
        {
            Assert.Contains("Cross Surface Runtime Agent", crmCut.Markup);
        });

        navigation.NavigateTo("/agents?tab=agents");
        var agentsCut = harness.Context.RenderComponent<AgentsHomePage>();
        agentsCut.WaitForAssertion(() =>
        {
            Assert.Contains("Cross Surface Runtime Agent", agentsCut.Markup);
        });

        var currentAgents = await workspaceService.ListAgentsAsync(includeTemplates: false);

        Assert.Contains(currentAgents, item => item.Id == technicalAgentId && item.Name == "Cross Surface Runtime Agent");

        crmCut.Dispose();
        agentsCut.Dispose();
    }

    [Fact]
    public async Task Agent_catalog_uses_loading_state_instead_of_false_empty_state_while_refreshing()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var workspaceFactory = harness.Context.Services.GetRequiredService<ICanDoItAllAgentWorkspaceFactory>();
        var workspaceService = workspaceFactory.GetOrganizationWorkspaceService();

        var editor = await workspaceService.GetAgentEditorAsync();
        editor.Name = "Visible Runtime Engineer";
        editor.RoleTitle = "Lead engineer";
        editor.Summary = "Should remain visible while the technical catalog refreshes.";
        editor.Instructions = "Keep the agent runtime explicit.";
        editor.Status = AgentLifecycleStatus.Active;
        editor.IsTemplate = false;
        editor.TemplateKey = string.Empty;

        await workspaceService.SaveAgentAsync(editor);

        var cut = harness.Context.RenderComponent<AgentCatalogPanel>(
            parameters => parameters.Add(component => component.SkipCatalogRepair, true));
        Assert.DoesNotContain("Create the first technical agent", cut.Markup);

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Visible Runtime Engineer", cut.Markup);
            Assert.DoesNotContain("No technical agents", cut.Markup);
        });
    }

    [Fact]
    public async Task Agent_catalog_exposes_project_structure_access_controls_and_project_choices()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("candoitall-component-tests");
        var activeProfile = testEnvironment.CreateInMemoryProfile("primary", $"project-access-{Guid.NewGuid():N}");
        await using var harness = await ComponentTestHarness.CreateAsync(options: new TestHarnessOptions
        {
            TestEnvironment = testEnvironment,
            ActiveProfile = activeProfile
        });
        var workspaceFactory = harness.Context.Services.GetRequiredService<ICanDoItAllAgentWorkspaceFactory>();
        var workspaceService = workspaceFactory.GetOrganizationWorkspaceService();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var saveResult = await projectsService.SaveAsync(new ProjectEditorModel
        {
            Name = "Workbench Access Project"
        });
        Assert.True(saveResult.IsSuccess);

        var editor = await workspaceService.GetAgentEditorAsync();
        editor.Name = "Project Access Dialog Agent";
        editor.RoleTitle = "Access tester";
        editor.Summary = "Uses project structure access controls.";
        editor.Instructions = "Load project access choices.";
        editor.Status = AgentLifecycleStatus.Active;
        editor.IsTemplate = false;
        editor.TemplateKey = string.Empty;
        var agentId = await workspaceService.SaveAgentAsync(editor);

        var host = harness.Context.RenderComponent<DialogHost>();

        var dialogTask = OpenAgentDetailsDialog(harness.Context, agentId);
        host.WaitForElement("[data-testid='agents-details-tabs']");
        SelectDialogTab(host, "Project Structure Access");
        host.WaitForElement("[data-testid='agents-catalog-project-structure-access']");

        Assert.Contains("Project Structure Access", host.Markup);
        Assert.NotNull(host.Find("[data-testid='agents-catalog-project-structure-read']"));
        Assert.NotNull(host.Find("[data-testid='agents-catalog-project-structure-write']"));
        Assert.NotNull(host.Find("[data-testid='agents-catalog-project-structure-load']"));
        Assert.DoesNotContain("Workbench Access Project", host.Markup);

        host.Find("[data-testid='agents-catalog-project-structure-load']").Click();

        host.WaitForAssertion(() =>
        {
            Assert.Contains("Workbench Access Project", host.Markup);
        });

        Assert.NotNull(host.Find("[data-testid='agents-catalog-project-structure-projects']"));
        harness.Context.Services.GetRequiredService<DialogService>().CloseAll();
        await dialogTask.WaitAsync(TimeSpan.FromSeconds(2));
        host.Dispose();
    }

    [Fact]
    public async Task Agent_catalog_exposes_process_access_controls_and_process_choices()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var workspaceFactory = harness.Context.Services.GetRequiredService<ICanDoItAllAgentWorkspaceFactory>();
        var workspaceService = workspaceFactory.GetOrganizationWorkspaceService();
        var processesService = harness.Context.Services.GetRequiredService<ProcessesService>();
        var saveResult = await processesService.SaveAsync(CreateProcessDefinition("Workbench Access Process"));
        Assert.True(saveResult.IsSuccess);

        var editor = await workspaceService.GetAgentEditorAsync();
        editor.Name = "Process Access Dialog Agent";
        editor.RoleTitle = "Access tester";
        editor.Summary = "Uses process access controls.";
        editor.Instructions = "Load process access choices.";
        editor.Status = AgentLifecycleStatus.Active;
        editor.IsTemplate = false;
        editor.TemplateKey = string.Empty;
        var agentId = await workspaceService.SaveAgentAsync(editor);

        var host = harness.Context.RenderComponent<DialogHost>();

        var dialogTask = OpenAgentDetailsDialog(harness.Context, agentId);
        host.WaitForElement("[data-testid='agents-details-tabs']");
        SelectDialogTab(host, "Process Access");
        host.WaitForElement("[data-testid='agents-catalog-process-access']");

        Assert.Contains("Process Access", host.Markup);
        Assert.NotNull(host.Find("[data-testid='agents-catalog-process-read']"));
        Assert.NotNull(host.Find("[data-testid='agents-catalog-process-write']"));
        Assert.NotNull(host.Find("[data-testid='agents-catalog-process-load']"));
        Assert.DoesNotContain("Workbench Access Process", host.Markup);

        host.Find("[data-testid='agents-catalog-process-load']").Click();

        host.WaitForAssertion(() =>
        {
            Assert.Contains("Workbench Access Process", host.Markup);
        });

        Assert.NotNull(host.Find("[data-testid='agents-catalog-processes']"));
        harness.Context.Services.GetRequiredService<DialogService>().CloseAll();
        await dialogTask.WaitAsync(TimeSpan.FromSeconds(2));
        host.Dispose();
    }

    [Fact]
    public async Task Agent_catalog_double_click_opens_tabbed_details_dialog_with_roomy_identity_fields()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var workspaceFactory = harness.Context.Services.GetRequiredService<ICanDoItAllAgentWorkspaceFactory>();
        var workspaceService = workspaceFactory.GetOrganizationWorkspaceService();
        var editor = await workspaceService.GetAgentEditorAsync();
        editor.Name = "Dialog Runtime Agent";
        editor.RoleTitle = "Dialog specialist";
        editor.Summary = "Agent opened from the card grid.";
        editor.Instructions = "Keep modal editing visible and explicit.";
        editor.Status = AgentLifecycleStatus.Active;
        editor.IsTemplate = false;
        editor.TemplateKey = string.Empty;
        await workspaceService.SaveAgentAsync(editor);

        var host = harness.Context.RenderComponent<DialogHost>();
        var cut = harness.Context.RenderComponent<AgentCatalogPanel>(
            parameters => parameters.Add(component => component.SkipCatalogRepair, true));

        cut.WaitForElement("[data-testid='agents-catalog-card']");
        cut.FindAll("[data-testid='agents-catalog-card']")
            .First(card => card.TextContent.Contains("Dialog Runtime Agent", StringComparison.Ordinal))
            .TriggerEvent("ondblclick", new MouseEventArgs());

        host.WaitForElement("[data-testid='agents-details-tabs']");
        Assert.Contains("Identity", host.Markup);
        Assert.Contains("Runtime", host.Markup);
        Assert.Contains("Project Structure Access", host.Markup);
        Assert.Contains("Skills and MCP", host.Markup);
        Assert.Contains("agent-details-dialog__summary-textarea", host.Find("[data-testid='agents-catalog-summary']").GetAttribute("class"));
        Assert.Contains("agent-details-dialog__instructions-textarea", host.Find("[data-testid='agents-catalog-instructions']").GetAttribute("class"));
        harness.Context.Services.GetRequiredService<DialogService>().CloseAll();
        host.Dispose();
        cut.Dispose();
    }

    [Fact]
    public async Task Agent_details_dialog_assigns_available_skill_or_mcp_capability()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("candoitall-component-tests");
        var activeProfile = testEnvironment.CreateInMemoryProfile("primary", $"capability-dialog-{Guid.NewGuid():N}");
        await using var harness = await ComponentTestHarness.CreateAsync(options: new TestHarnessOptions
        {
            TestEnvironment = testEnvironment,
            ActiveProfile = activeProfile
        });
        var workspaceFactory = harness.Context.Services.GetRequiredService<ICanDoItAllAgentWorkspaceFactory>();
        var workspaceService = workspaceFactory.GetOrganizationWorkspaceService();

        var editor = await workspaceService.GetAgentEditorAsync();
        editor.Name = "Capability Dialog Agent";
        editor.RoleTitle = "Capability tester";
        editor.Summary = "Uses modal capability assignment.";
        editor.Instructions = "Attach cataloged capabilities from the dialog.";
        editor.Status = AgentLifecycleStatus.Active;
        editor.IsTemplate = false;
        editor.TemplateKey = string.Empty;
        var agentId = await workspaceService.SaveAgentAsync(editor);

        var capabilityId = await workspaceService.SaveCapabilityAsync(new CapabilityEditorModel
        {
            Kind = CapabilityKind.McpServer,
            Key = "dialog-mcp-proof",
            Name = "Dialog MCP Proof",
            Description = "Capability assignment proof for the tabbed dialog.",
            EndpointOrPath = "stdio://dialog-proof",
            ConfigurationJson = "{}",
            IsBuiltIn = false
        });

        var host = harness.Context.RenderComponent<DialogHost>();

        var dialogTask = OpenAgentDetailsDialog(harness.Context, agentId);
        host.WaitForElement("[data-testid='agents-details-tabs']");
        SelectDialogTab(host, "Skills and MCP");
        host.WaitForElement("[data-testid='agents-details-capability-list']");
        Assert.Contains("Dialog MCP Proof", host.Markup);

        host.FindAll("[data-testid='agents-details-toggle-capability']")
            .First(button => button.TextContent.Contains("Assign", StringComparison.Ordinal))
            .Click();

        await Task.Delay(50);
        var updatedAgent = (await workspaceService.ListAgentsAsync(includeTemplates: false))
            .Single(agent => agent.Id == agentId);
        Assert.Contains(updatedAgent.Capabilities, capability => capability.CapabilityId == capabilityId);
        harness.Context.Services.GetRequiredService<DialogService>().CloseAll();
        await dialogTask.WaitAsync(TimeSpan.FromSeconds(2));
        host.Dispose();
    }

    [Fact]
    public async Task Agent_catalog_switches_requested_agent_without_reloading_the_editor_from_storage()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var workspaceFactory = harness.Context.Services.GetRequiredService<ICanDoItAllAgentWorkspaceFactory>();
        var workspaceService = workspaceFactory.GetOrganizationWorkspaceService();

        var firstEditor = await workspaceService.GetAgentEditorAsync();
        firstEditor.Name = "First Runtime Agent";
        firstEditor.RoleTitle = "First role";
        firstEditor.Summary = "First summary";
        firstEditor.Instructions = "First instructions";
        firstEditor.Status = AgentLifecycleStatus.Active;
        firstEditor.IsTemplate = false;
        firstEditor.TemplateKey = string.Empty;
        var firstAgentId = await workspaceService.SaveAgentAsync(firstEditor);

        var secondEditor = await workspaceService.GetAgentEditorAsync();
        secondEditor.Name = "Second Runtime Agent";
        secondEditor.RoleTitle = "Second role";
        secondEditor.Summary = "Second summary";
        secondEditor.Instructions = "Second instructions";
        secondEditor.Status = AgentLifecycleStatus.Active;
        secondEditor.IsTemplate = false;
        secondEditor.TemplateKey = string.Empty;
        var secondAgentId = await workspaceService.SaveAgentAsync(secondEditor);

        var host = harness.Context.RenderComponent<DialogHost>();
        var dialogService = harness.Context.Services.GetRequiredService<DialogService>();

        var firstDialogTask = OpenAgentDetailsDialog(harness.Context, firstAgentId);

        host.WaitForAssertion(() =>
        {
            Assert.Equal("First Runtime Agent", host.Find("[data-testid='agents-catalog-name']").GetAttribute("value"));
        });

        dialogService.CloseAll();
        await firstDialogTask.WaitAsync(TimeSpan.FromSeconds(2));

        var secondDialogTask = OpenAgentDetailsDialog(harness.Context, secondAgentId);

        host.WaitForAssertion(() =>
        {
            Assert.Equal("Second Runtime Agent", host.Find("[data-testid='agents-catalog-name']").GetAttribute("value"));
        });

        dialogService.CloseAll();
        await secondDialogTask.WaitAsync(TimeSpan.FromSeconds(2));
        host.Dispose();
    }

    [Fact]
    public async Task Agents_page_uses_loading_counts_until_the_shell_summary_finishes_loading()
    {
        var repairService = new CountingRepairService();
        await using var harness = await ComponentTestHarness.CreateAsync(services =>
        {
            services.AddScoped<IAgentFrameworkOrganizationCatalogRepairService>(_ => repairService);
        });
        var navigation = harness.Context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/agents");

        var cut = harness.Context.RenderComponent<AgentsHomePage>();

        Assert.Contains("Loading canonical agent runtime", cut.Markup);
        Assert.Contains("...", cut.Markup);

        cut.WaitForAssertion(() =>
        {
            Assert.DoesNotContain("Loading canonical agent runtime", cut.Markup);
        });
    }

    [Fact]
    public async Task Agents_page_does_not_start_catalog_repair_when_opening_the_agent_panel()
    {
        var repairService = new BlockingRepairService();
        await using var harness = await ComponentTestHarness.CreateAsync(services =>
        {
            services.AddScoped<IAgentFrameworkOrganizationCatalogRepairService>(_ => repairService);
        });
        var navigation = harness.Context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/agents?tab=agents");

        var cut = harness.Context.RenderComponent<AgentsHomePage>();

        cut.WaitForElement("[data-testid='agents-catalog-search']");
        Assert.DoesNotContain("Loading canonical agent runtime", cut.Markup);
        Assert.Equal(0, repairService.CallCount);
    }

    private static void SelectDialogTab(IRenderedFragment host, string tabText)
    {
        host.FindAll("[role='tab']")
            .First(tab => tab.TextContent.Contains(tabText, StringComparison.Ordinal))
            .Click();
    }

    private static Task<object?> OpenAgentDetailsDialog(TestContext context, Guid? agentId = null)
    {
        var dialogService = context.Services.GetRequiredService<DialogService>();
        return dialogService.OpenAsync<AgentDetailsDialog>(
            agentId.HasValue ? "Agent details" : "New technical agent",
            new Dictionary<string, object?>
            {
                [nameof(AgentDetailsDialog.AgentId)] = agentId
            },
            new DialogOptions
            {
                Eyebrow = "Technical editor",
                Subtitle = "Edit identity, runtime, access policy, skills, and MCP servers for this technical agent.",
                Size = ModalSize.Full,
                DenseChrome = true,
                AriaLabel = "Agent details editor",
                TestId = "agents-details-dialog"
            });
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

    private static ProcessDefinitionEditorModel CreateProcessDefinition(string name)
    {
        var roleId = Guid.NewGuid();
        var stepId = Guid.NewGuid();

        return new ProcessDefinitionEditorModel
        {
            Name = name,
            Summary = $"{name} summary",
            ValueStatement = "Deliver the expected process outcome.",
            CustomerName = "Internal customer",
            OwnerName = "Process owner",
            GovernanceNotes = "Follow the standard governance path.",
            ChangeSummary = "Initial draft.",
            GovernancePolicySummary = "Review before irreversible changes.",
            ConstitutionRuleSummary = "Escalate exceptions explicitly.",
            OperatingModeSummary = "Assisted execution.",
            SimulationReadinessSummary = "Ready for controlled execution.",
            Roles =
            [
                new ProcessRoleEditorModel
                {
                    Id = roleId,
                    Key = "owner",
                    DisplayName = "Owner",
                    Purpose = "Owns the process outcome."
                }
            ],
            Steps =
            [
                new ProcessStepEditorModel
                {
                    Id = stepId,
                    Key = "plan",
                    Title = "Plan work",
                    InputContractSummary = "Structured request",
                    OutputContractSummary = "Approved plan",
                    EvidenceContractSummary = "Decision note",
                    RoleAssignments =
                    [
                        new ProcessStepRoleRequirementEditorModel
                        {
                            RoleRequirementId = roleId,
                            ResponsibilityKind = ProcessResponsibilityKind.Responsible,
                            IsRequired = true
                        }
                    ]
                }
            ]
        };
    }

    private static async Task RepairAndSynchronizeAsync(IServiceProvider serviceProvider)
    {
        var repairService = serviceProvider.GetRequiredService<IAgentFrameworkOrganizationCatalogRepairService>();
        var technicalAgentBridge = serviceProvider.GetRequiredService<IAiTechnicalAgentBridge>();
        await repairService.EnsureCurrentOrganizationCatalogAsync();
        await technicalAgentBridge.SynchronizeDirectoryProjectionAsync();
    }

    private sealed class BlockingRepairService : IAgentFrameworkOrganizationCatalogRepairService
    {
        private readonly TaskCompletionSource repairGate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int CallCount { get; private set; }

        public Task EnsureCurrentOrganizationCatalogAsync(CancellationToken cancellationToken = default)
        {
            CallCount++;
            return repairGate.Task.WaitAsync(cancellationToken);
        }

        public void Release()
        {
            repairGate.TrySetResult();
        }
    }

    private sealed class CountingRepairService : IAgentFrameworkOrganizationCatalogRepairService
    {
        public int CallCount { get; private set; }

        public Task EnsureCurrentOrganizationCatalogAsync(CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.CompletedTask;
        }
    }
}
