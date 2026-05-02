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
using Microsoft.AspNetCore.Components;
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
        await using var harness = await ComponentTestHarness.CreateAsync();
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
    }

    [Fact]
    public async Task Creates_agentframework_agent_from_canonical_catalog_and_projects_it_into_crm_hr()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var partyDirectoryService = harness.Context.Services.GetRequiredService<PartyDirectoryService>();
        var aiAgentService = harness.Context.Services.GetRequiredService<AiAgentService>();
        var navigation = harness.Context.Services.GetRequiredService<NavigationManager>();

        navigation.NavigateTo("/agents?tab=agents");
        var cut = harness.Context.RenderComponent<AgentsHomePage>();

        cut.WaitForElement("[data-testid='agents-catalog-name']");
        cut.Find("[data-testid='agents-catalog-name']").Change("Release Copilot");
        cut.Find("[data-testid='agents-catalog-role']").Change("Release analyst");
        cut.Find("[data-testid='agents-catalog-summary']").Change("Supports release analysis and deployment notes.");
        cut.Find("[data-testid='agents-catalog-instructions']").Change("Review release scope and produce durable evidence.");
        cut.Find("[data-testid='agents-catalog-save']").Click();

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
        await using var harness = await ComponentTestHarness.CreateAsync();
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

        navigation.NavigateTo($"/crm-hr/agents?partyId={rosterItem.PartyId:D}");

        var cut = harness.Context.RenderComponent<CrmHrAgentsPage>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Directed Runtime Reviewer", cut.Markup);
        });

        cut.WaitForElement("[data-testid='crmhr-agent-open-technical-record']");
        cut.Find("[data-testid='crmhr-agent-open-technical-record']").Click();
        var agentsCut = harness.Context.RenderComponent<AgentsHomePage>();

        agentsCut.WaitForAssertion(() =>
        {
            Assert.Contains("Directed Runtime Reviewer", agentsCut.Markup);
        });

        Assert.EndsWith($"/agents?tab=agents&agentId={technicalAgentId:D}", navigation.Uri, StringComparison.OrdinalIgnoreCase);
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
        await using var harness = await ComponentTestHarness.CreateAsync();
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
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var saveResult = await projectsService.SaveAsync(new ProjectEditorModel
        {
            Name = "Workbench Access Project"
        });
        Assert.True(saveResult.IsSuccess);

        var cut = harness.Context.RenderComponent<AgentCatalogPanel>();

        cut.WaitForElement("[data-testid='agents-catalog-project-structure-access']");

        Assert.Contains("Project Structure Access", cut.Markup);
        Assert.NotNull(cut.Find("[data-testid='agents-catalog-project-structure-read']"));
        Assert.NotNull(cut.Find("[data-testid='agents-catalog-project-structure-write']"));
        Assert.NotNull(cut.Find("[data-testid='agents-catalog-project-structure-load']"));
        Assert.DoesNotContain("Workbench Access Project", cut.Markup);

        cut.Find("[data-testid='agents-catalog-project-structure-load']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Workbench Access Project", cut.Markup);
        });

        Assert.NotNull(cut.Find("[data-testid='agents-catalog-project-structure-projects']"));
    }

    [Fact]
    public async Task Agent_catalog_exposes_process_access_controls_and_process_choices()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var processesService = harness.Context.Services.GetRequiredService<ProcessesService>();
        var saveResult = await processesService.SaveAsync(CreateProcessDefinition("Workbench Access Process"));
        Assert.True(saveResult.IsSuccess);

        var cut = harness.Context.RenderComponent<AgentCatalogPanel>();

        cut.WaitForElement("[data-testid='agents-catalog-process-access']");

        Assert.Contains("Process Access", cut.Markup);
        Assert.NotNull(cut.Find("[data-testid='agents-catalog-process-read']"));
        Assert.NotNull(cut.Find("[data-testid='agents-catalog-process-write']"));
        Assert.NotNull(cut.Find("[data-testid='agents-catalog-process-load']"));
        Assert.DoesNotContain("Workbench Access Process", cut.Markup);

        cut.Find("[data-testid='agents-catalog-process-load']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Workbench Access Process", cut.Markup);
        });

        Assert.NotNull(cut.Find("[data-testid='agents-catalog-processes']"));
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

        var cut = harness.Context.RenderComponent<AgentCatalogPanel>(parameters => parameters
            .Add(component => component.RequestedAgentId, firstAgentId));

        cut.WaitForAssertion(() =>
        {
            Assert.Equal("First Runtime Agent", cut.Find("[data-testid='agents-catalog-name']").GetAttribute("value"));
        });

        cut.SetParametersAndRender(parameters => parameters
            .Add(component => component.RequestedAgentId, secondAgentId));

        cut.WaitForAssertion(() =>
        {
            Assert.Equal("Second Runtime Agent", cut.Find("[data-testid='agents-catalog-name']").GetAttribute("value"));
        });
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

        cut.WaitForElement("[data-testid='agents-catalog-name']");
        Assert.DoesNotContain("Loading canonical agent runtime", cut.Markup);
        Assert.Equal(0, repairService.CallCount);
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
