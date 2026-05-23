using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workspace;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace CanDoItAll.Tests.Integration;

public sealed class AiAgentProfileIntegrationTests
{
    [Fact]
    public async Task SaveAgentProfileAsync_persists_provider_owner_capabilities_and_supports_project_assignments()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var partyDirectoryService = scope.ServiceProvider.GetRequiredService<PartyDirectoryService>();
        var aiAgentService = scope.ServiceProvider.GetRequiredService<AiAgentService>();
        var workspaceService = scope.ServiceProvider.GetRequiredService<WorkspaceService>();
        var projectPartyIntegrationService = scope.ServiceProvider.GetRequiredService<ProjectPartyIntegrationService>();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();

        var ownerId = await CreatePersonAsync(partyDirectoryService, "Petra Owner", "petra.owner@example.test");
        var agentId = await CreateAgentAsync(partyDirectoryService, "Spec Reviewer");
        var providerSave = await workspaceService.SaveProviderAsync(new CanDoItAll.Modules.Workspace.ProviderProfileEditorModel
        {
            Name = "Integration provider",
            ConnectorPluginKey = OllamaRemoteProviderAdapter.PluginKey,
            ConfigSchemaVersion = "1.0",
            Configuration = new ConnectorConfigState(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["baseUrl"] = "http://ollama.internal",
                ["defaultModel"] = "llama3.1",
                ["timeoutSeconds"] = "45"
            }),
            IsEnabled = true
        });
        Assert.True(providerSave.IsSuccess);

        var saveResult = await aiAgentService.SaveAgentProfileAsync(new AiAgentProfileEditorModel
        {
            PartyId = agentId,
            ProviderProfileId = providerSave.Value,
            DefaultModel = "llama3.2",
            ExecutionMode = AiExecutionMode.Remote,
            OwnerPartyId = ownerId,
            ValidationStatus = AiValidationStatus.Approved,
            LastReviewedOn = new DateOnly(2026, 4, 3),
            Notes = "Approved for specification review and structured analysis.",
            LastChangedBy = "integration-tests",
            Capabilities =
            [
                new AiCapabilityEditorModel
                {
                    Name = "Specification review",
                    Scope = "Requirements and edge-case analysis",
                    ToolAccess = "Readonly docs",
                    Limitations = "No direct customer communication",
                    Notes = "Escalate ambiguity to a human owner."
                }
            ]
        });

        Assert.True(saveResult.IsSuccess);

        var workspace = await aiAgentService.GetAgentWorkspaceAsync(agentId);
        Assert.NotNull(workspace);
        Assert.Equal(providerSave.Value, workspace.Profile.ProviderProfileId);
        Assert.Equal(ownerId, workspace.Profile.OwnerPartyId);
        Assert.Equal(AiValidationStatus.Approved, workspace.Profile.ValidationStatus);
        Assert.Single(workspace.Profile.Capabilities);

        var directoryItems = await partyDirectoryService.ListDirectoryAsync();
        var owner = Assert.Single(directoryItems, item => item.Id == ownerId);
        Assert.Contains(PartyRoleKind.AiSteward, owner.Roles);

        var projectId = Guid.NewGuid();
        await using (var dbContext = await dbContextFactory.CreateDbContextAsync())
        {
            dbContext.Set<ProjectPartyAssignment>().Add(new ProjectPartyAssignment
            {
                ProjectId = projectId,
                PartyId = agentId,
                AssignmentKind = ProjectPartyAssignmentKind.AiAgent,
                NodeKey = "analysis-agent",
                PhaseName = "Discovery",
                IsPrimary = true,
                Source = "integration-tests"
            });
            await dbContext.SaveChangesAsync();
        }

        var assignments = await projectPartyIntegrationService.ListAssignmentsAsync(projectId);
        var assignment = Assert.Single(assignments);
        Assert.Equal(agentId, assignment.PartyId);
        Assert.Equal(ProjectPartyAssignmentKind.AiAgent, assignment.AssignmentKind);
    }

    [Fact]
    public async Task SaveAgentProfileAsync_uses_party_scoped_template_key_for_new_runtime_agent_when_name_matches_template()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var partyDirectoryService = scope.ServiceProvider.GetRequiredService<PartyDirectoryService>();
        var aiAgentService = scope.ServiceProvider.GetRequiredService<AiAgentService>();
        var workspaceFactory = scope.ServiceProvider.GetRequiredService<ICanDoItAllAgentWorkspaceFactory>();
        var workspaceService = workspaceFactory.GetOrganizationWorkspaceService();

        var suffix = Guid.NewGuid().ToString("N");
        var sharedDisplayName = $"Blazor delivery manager AI agent {suffix}";
        var templateEditor = await workspaceService.GetAgentEditorAsync();
        templateEditor.Name = sharedDisplayName;
        templateEditor.RoleTitle = "Delivery manager template";
        templateEditor.Summary = "Reusable delivery manager template.";
        templateEditor.Instructions = "Coordinate Blazor delivery from the template catalog.";
        templateEditor.Status = AgentLifecycleStatus.Active;
        templateEditor.IsTemplate = true;
        templateEditor.TemplateKey = sharedDisplayName;
        var templateAgentId = await workspaceService.SaveAgentAsync(templateEditor);

        var partyId = await CreateAgentAsync(partyDirectoryService, sharedDisplayName);
        var saveResult = await aiAgentService.SaveAgentProfileAsync(new AiAgentProfileEditorModel
        {
            PartyId = partyId,
            ExecutionMode = AiExecutionMode.Remote,
            ValidationStatus = AiValidationStatus.Draft,
            Notes = "Runtime agent intentionally shares a display name with a template.",
            LastChangedBy = "integration-tests"
        });

        Assert.True(saveResult.IsSuccess, string.Join(" | ", saveResult.Errors.Select(error => error.Message)));

        var workspace = await aiAgentService.GetAgentWorkspaceAsync(partyId);
        Assert.NotNull(workspace);
        Assert.True(workspace!.TechnicalAgentId.HasValue);

        var agents = await workspaceService.ListAgentsAsync(includeTemplates: true);
        var templateAgent = Assert.Single(agents, item => item.Id == templateAgentId);
        var runtimeAgent = Assert.Single(agents, item => item.Id == workspace.TechnicalAgentId.Value);

        Assert.True(templateAgent.IsTemplate);
        Assert.False(runtimeAgent.IsTemplate);
        Assert.Equal(sharedDisplayName, runtimeAgent.Name);
        Assert.Equal($"crmhr-ai-resource-{partyId:N}", runtimeAgent.TemplateKey);
        Assert.NotEqual(templateAgent.TemplateKey, runtimeAgent.TemplateKey);
    }

    [Fact]
    public async Task SaveAgentProfileAsync_rejects_non_person_owner()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var partyDirectoryService = scope.ServiceProvider.GetRequiredService<PartyDirectoryService>();
        var aiAgentService = scope.ServiceProvider.GetRequiredService<AiAgentService>();

        var ownerId = await CreateOrganizationAsync(partyDirectoryService, "Ops Partner");
        var agentId = await CreateAgentAsync(partyDirectoryService, "Incident Triage Agent");

        var result = await aiAgentService.SaveAgentProfileAsync(new AiAgentProfileEditorModel
        {
            PartyId = agentId,
            OwnerPartyId = ownerId
        });

        Assert.True(result.IsFailure);
        Assert.Contains(result.Errors, error => error.Code == "crmhr.ai-agent.owner-invalid");
    }

    [Fact]
    public async Task CreateAgentAsync_creates_agentframework_backed_agent_and_excludes_orphan_ai_parties()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var partyDirectoryService = scope.ServiceProvider.GetRequiredService<PartyDirectoryService>();
        var aiAgentService = scope.ServiceProvider.GetRequiredService<AiAgentService>();
        var workspaceFactory = scope.ServiceProvider.GetRequiredService<ICanDoItAllAgentWorkspaceFactory>();
        var workspaceService = workspaceFactory.GetOrganizationWorkspaceService();

        var orphanResult = await partyDirectoryService.SavePartyAsync(new PartyEditorModel
        {
            PartyType = PartyType.AiAgent,
            LifecycleStatus = PartyLifecycleStatus.Active,
            DisplayName = "Orphan CRM Agent",
            Summary = "Exists only in CRM-HR.",
            LastChangedBy = "integration-tests"
        });
        Assert.True(orphanResult.IsSuccess);

        var createResult = await aiAgentService.CreateAgentAsync(
            "Bound Delivery Agent",
            "AI-BIND",
            "Provisioned through CRM-HR and backed by AgentFramework.",
            "integration-tests");
        Assert.True(createResult.IsSuccess);

        var roster = await aiAgentService.ListAgentDirectoryAsync();
        var rosterItem = Assert.Single(roster, item => item.PartyId == createResult.Value);
        var workspace = await aiAgentService.GetAgentWorkspaceAsync(createResult.Value);
        var technicalAgents = await workspaceService.ListAgentsAsync(includeTemplates: false);

        Assert.DoesNotContain(roster, item => item.PartyId == orphanResult.Value);
        Assert.Equal("Bound Delivery Agent", rosterItem.DisplayName);
        Assert.NotNull(rosterItem.TechnicalAgentId);
        Assert.NotNull(workspace);
        Assert.NotNull(workspace!.TechnicalAgentId);
        Assert.Contains(technicalAgents, item => item.Id == rosterItem.TechnicalAgentId);
    }

    [Fact]
    public async Task Projected_agentframework_agents_use_runtime_metadata_in_crm_hr_views()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var aiAgentService = scope.ServiceProvider.GetRequiredService<AiAgentService>();
        var workspaceFactory = scope.ServiceProvider.GetRequiredService<ICanDoItAllAgentWorkspaceFactory>();
        var workspaceService = workspaceFactory.GetOrganizationWorkspaceService();

        var providers = (await workspaceService.ListProvidersAsync()).ToList();
        var capabilities = (await workspaceService.ListCapabilitiesAsync()).ToList();
        Assert.NotEmpty(providers);
        Assert.NotEmpty(capabilities);
        var provider = providers[0];
        var capability = capabilities[0];
        var editor = await workspaceService.GetAgentEditorAsync();
        editor.Name = "Projected Runtime Agent";
        editor.RoleTitle = "Runtime specialist";
        editor.Summary = "Projected from AgentFramework without CRM-HR-owned technical metadata.";
        editor.Instructions = "Execute only through the canonical AgentFramework runtime.";
        editor.Status = AgentLifecycleStatus.Active;
        editor.IsTemplate = false;
        editor.TemplateKey = string.Empty;
        editor.ProviderProfileId = provider.Id;
        editor.SelectedCapabilityIds =
        [
            capability.Id
        ];

        var technicalAgentId = await workspaceService.SaveAgentAsync(editor);

        var roster = await aiAgentService.ListAgentDirectoryAsync();
        var rosterItem = Assert.Single(roster, item => item.TechnicalAgentId == technicalAgentId);
        var workspace = await aiAgentService.GetAgentWorkspaceAsync(rosterItem.PartyId);

        Assert.True(rosterItem.HasProfile);
        Assert.Equal(AiValidationStatus.Draft, rosterItem.ValidationStatus);
        Assert.Equal(AiExecutionMode.Remote, rosterItem.ExecutionMode);
        Assert.Equal(provider.Name, rosterItem.ProviderName);
        Assert.Equal(1, rosterItem.CapabilityCount);
        Assert.NotNull(workspace);
        Assert.Equal(provider.Id, workspace!.Profile.ProviderProfileId);
        Assert.Equal(AiExecutionMode.Remote, workspace.Profile.ExecutionMode);
        Assert.Single(workspace.Profile.Capabilities);
        Assert.Equal(capability.Name, workspace.Profile.Capabilities[0].Name);
    }

    [Fact]
    public async Task SaveAgentAsync_projects_current_profile_agents_into_crm_hr_without_a_repair_read()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var workspaceService = scope.ServiceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();

        var editor = await workspaceService.GetAgentEditorAsync();
        editor.Name = "Current Profile Projected Agent";
        editor.RoleTitle = "Delivery engineer";
        editor.Summary = "Projected directly from the current AgentFramework workspace.";
        editor.Instructions = "Stay canonical inside AgentFramework.";
        editor.Status = AgentLifecycleStatus.Active;
        editor.IsTemplate = false;
        editor.TemplateKey = string.Empty;

        var technicalAgentId = await workspaceService.SaveAgentAsync(editor);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var binding = await dbContext.Set<AiResourceBinding>()
            .SingleAsync(item => item.TechnicalAgentId == technicalAgentId);
        var party = await dbContext.Set<Party>()
            .SingleAsync(item => item.Id == binding.PartyId);

        Assert.Equal(AiResourceBindingStatus.Bound, binding.BindingStatus);
        Assert.Equal("Current Profile Projected Agent", party.DisplayName);
        Assert.Equal("Projected directly from the current AgentFramework workspace.", party.Summary);
        Assert.Equal(PartyLifecycleStatus.Active, party.LifecycleStatus);
    }

    [Fact]
    public async Task SaveAgentAsync_roundtrips_project_structure_access_settings_inside_configuration_json()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var workspaceService = scope.ServiceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();

        var alphaProjectResult = await projectsService.SaveAsync(new ProjectEditorModel
        {
            Name = "Alpha Access Project"
        });
        var betaProjectResult = await projectsService.SaveAsync(new ProjectEditorModel
        {
            Name = "Beta Access Project"
        });

        Assert.True(alphaProjectResult.IsSuccess);
        Assert.True(betaProjectResult.IsSuccess);

        var alphaProjectId = alphaProjectResult.Value;
        var betaProjectId = betaProjectResult.Value;
        var editor = await workspaceService.GetAgentEditorAsync();
        editor.Name = "Project Structure Runtime Agent";
        editor.RoleTitle = "Runtime engineer";
        editor.Summary = "Carries native project structure access settings.";
        editor.Instructions = "Use project structure access only within the assigned scope.";
        editor.Status = AgentLifecycleStatus.Active;
        editor.IsTemplate = false;
        editor.TemplateKey = string.Empty;
        editor.ConfigurationJson = JsonSerializer.Serialize(new
        {
            crmHr = new
            {
                source = "integration-tests"
            }
        });
        editor.ProjectStructureAccess = new AgentProjectStructureAccessSettings
        {
            CanWrite = true,
            AllowAllProjects = true,
            AllowedProjectIds =
            [
                betaProjectId,
                alphaProjectId,
                Guid.Empty,
                betaProjectId
            ]
        };

        var agentId = await workspaceService.SaveAgentAsync(editor);
        var savedEditor = await workspaceService.GetAgentEditorAsync(agentId);

        Assert.True(savedEditor.ProjectStructureAccess.CanRead);
        Assert.True(savedEditor.ProjectStructureAccess.CanWrite);
        Assert.True(savedEditor.ProjectStructureAccess.AllowAllProjects);
        Assert.Equal(
            new[]
            {
                alphaProjectId,
                betaProjectId
            }.OrderBy(item => item).ToList(),
            savedEditor.ProjectStructureAccess.AllowedProjectIds);

        var configurationRoot = JsonNode.Parse(savedEditor.ConfigurationJson)?.AsObject();

        Assert.NotNull(configurationRoot);
        Assert.NotNull(configurationRoot["crmHr"]);
        Assert.NotNull(configurationRoot["projectStructure"]);
    }

    [Fact]
    public async Task SaveAgentAsync_roundtrips_process_access_settings_inside_configuration_json()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var workspaceService = scope.ServiceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();

        var alphaProcessResult = await processesService.SaveAsync(CreateProcessDefinition("Alpha Access Process"));
        var betaProcessResult = await processesService.SaveAsync(CreateProcessDefinition("Beta Access Process"));

        Assert.True(alphaProcessResult.IsSuccess);
        Assert.True(betaProcessResult.IsSuccess);

        var alphaProcessId = alphaProcessResult.Value;
        var betaProcessId = betaProcessResult.Value;
        var editor = await workspaceService.GetAgentEditorAsync();
        editor.Name = "Process Runtime Agent";
        editor.RoleTitle = "Process engineer";
        editor.Summary = "Carries native process access settings.";
        editor.Instructions = "Use process access only within the assigned scope.";
        editor.Status = AgentLifecycleStatus.Active;
        editor.IsTemplate = false;
        editor.TemplateKey = string.Empty;
        editor.ConfigurationJson = JsonSerializer.Serialize(new
        {
            crmHr = new
            {
                source = "integration-tests"
            }
        });
        editor.ProcessAccess = new AgentProcessAccessSettings
        {
            CanWrite = true,
            AllowAllDefinitions = true,
            AllowedDefinitionIds =
            [
                betaProcessId,
                alphaProcessId,
                Guid.Empty,
                betaProcessId
            ]
        };

        var agentId = await workspaceService.SaveAgentAsync(editor);
        var savedEditor = await workspaceService.GetAgentEditorAsync(agentId);

        Assert.True(savedEditor.ProcessAccess.CanRead);
        Assert.True(savedEditor.ProcessAccess.CanWrite);
        Assert.True(savedEditor.ProcessAccess.AllowAllDefinitions);
        Assert.Equal(
            new[]
            {
                alphaProcessId,
                betaProcessId
            }.OrderBy(item => item).ToList(),
            savedEditor.ProcessAccess.AllowedDefinitionIds);

        var configurationRoot = JsonNode.Parse(savedEditor.ConfigurationJson)?.AsObject();

        Assert.NotNull(configurationRoot);
        Assert.NotNull(configurationRoot["crmHr"]);
        Assert.NotNull(configurationRoot["processes"]);
    }

    [Fact]
    public async Task ListAgentDirectoryAsync_prefers_agentframework_party_projection_over_duplicate_crm_binding()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var partyDirectoryService = scope.ServiceProvider.GetRequiredService<PartyDirectoryService>();
        var aiAgentService = scope.ServiceProvider.GetRequiredService<AiAgentService>();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();

        var createResult = await aiAgentService.CreateAgentAsync(
            "Canonical Delivery Agent",
            "AI-CANON",
            "Provisioned through CRM-HR.",
            "integration-tests");
        Assert.True(createResult.IsSuccess);

        var workspace = await aiAgentService.GetAgentWorkspaceAsync(createResult.Value);
        Assert.NotNull(workspace);
        Assert.True(workspace!.TechnicalAgentId.HasValue);

        var stalePartyId = await CreateAgentAsync(partyDirectoryService, "Stale Duplicate Binding");
        var duplicateBindingCreatedAt = DateTimeOffset.UtcNow.AddMinutes(5);
        await using (var dbContext = await dbContextFactory.CreateDbContextAsync())
        {
            dbContext.Set<AiResourceBinding>().Add(new AiResourceBinding
            {
                PartyId = stalePartyId,
                TechnicalAgentId = workspace.TechnicalAgentId,
                BindingStatus = AiResourceBindingStatus.Bound,
                BindingReason = "Stale duplicate CRM binding.",
                LastError = string.Empty,
                CreatedAtUtc = duplicateBindingCreatedAt,
                UpdatedAtUtc = duplicateBindingCreatedAt
            });
            await dbContext.SaveChangesAsync();
        }

        var roster = await aiAgentService.ListAgentDirectoryAsync();
        var canonicalItem = Assert.Single(roster, item => item.TechnicalAgentId == workspace.TechnicalAgentId);

        Assert.Equal(createResult.Value, canonicalItem.PartyId);
        Assert.DoesNotContain(roster, item => item.PartyId == stalePartyId);

        await using var verificationContext = await dbContextFactory.CreateDbContextAsync();
        var staleBinding = await verificationContext.Set<AiResourceBinding>()
            .SingleAsync(item => item.PartyId == stalePartyId);
        Assert.Null(staleBinding.TechnicalAgentId);
        Assert.Equal(AiResourceBindingStatus.Error, staleBinding.BindingStatus);
        Assert.Contains("Superseded by AgentFramework party projection", staleBinding.BindingReason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ListAgentDirectoryAsync_imports_legacy_organization_agents_into_the_current_agentframework_catalog()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var partyDirectoryService = scope.ServiceProvider.GetRequiredService<PartyDirectoryService>();
        var aiAgentService = scope.ServiceProvider.GetRequiredService<AiAgentService>();
        var workspaceFactory = scope.ServiceProvider.GetRequiredService<ICanDoItAllAgentWorkspaceFactory>();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        var repairService = scope.ServiceProvider.GetRequiredService<IAgentFrameworkOrganizationCatalogRepairService>();
        var technicalAgentBridge = scope.ServiceProvider.GetRequiredService<IAiTechnicalAgentBridge>();

        var partyId = await CreateAgentAsync(partyDirectoryService, "Legacy CRM Bound Agent");
        var legacyWorkspace = workspaceFactory.GetWorkspaceService(WorkspaceScopeDescriptor.Organization("legacy-catalog"));
        var editor = await legacyWorkspace.GetAgentEditorAsync();
        editor.Name = "Showcase Lead Engineer";
        editor.RoleTitle = "Lead engineer";
        editor.Summary = "Migrated from a legacy organization workspace.";
        editor.Instructions = "Own the end-to-end technical delivery plan.";
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

        await repairService.EnsureCurrentOrganizationCatalogAsync();
        await technicalAgentBridge.SynchronizeDirectoryProjectionAsync();

        var roster = await aiAgentService.ListAgentDirectoryAsync();
        var currentWorkspace = workspaceFactory.GetOrganizationWorkspaceService();
        var currentAgents = await currentWorkspace.ListAgentsAsync(includeTemplates: false);
        var rosterItem = Assert.Single(roster, item => item.PartyId == partyId);

        Assert.Equal(legacyTechnicalAgentId, rosterItem.TechnicalAgentId);
        Assert.Contains(currentAgents, item => item.Id == legacyTechnicalAgentId && item.Name == "Showcase Lead Engineer");
    }

    [Fact]
    public async Task Explicit_catalog_repair_imports_legacy_organization_agents_added_after_initial_warmup()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var partyDirectoryService = scope.ServiceProvider.GetRequiredService<PartyDirectoryService>();
        var aiAgentService = scope.ServiceProvider.GetRequiredService<AiAgentService>();
        var workspaceFactory = scope.ServiceProvider.GetRequiredService<ICanDoItAllAgentWorkspaceFactory>();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        var repairService = scope.ServiceProvider.GetRequiredService<IAgentFrameworkOrganizationCatalogRepairService>();
        var technicalAgentBridge = scope.ServiceProvider.GetRequiredService<IAiTechnicalAgentBridge>();

        var partyId = await CreateAgentAsync(partyDirectoryService, "Late Legacy CRM Agent");

        var initialRoster = await aiAgentService.ListAgentDirectoryAsync();
        Assert.DoesNotContain(initialRoster, item => item.PartyId == partyId);

        var legacyWorkspace = workspaceFactory.GetWorkspaceService(WorkspaceScopeDescriptor.Organization("late-legacy-catalog"));
        var editor = await legacyWorkspace.GetAgentEditorAsync();
        editor.Name = "Late Imported Engineer";
        editor.RoleTitle = "Lead engineer";
        editor.Summary = "Legacy agent created after the first catalog repair pass.";
        editor.Instructions = "Own the technical delivery lane.";
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
                BindingReason = "Late legacy organization scope binding.",
                LastError = string.Empty,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                UpdatedAtUtc = DateTimeOffset.UtcNow
            });
            await dbContext.SaveChangesAsync();
        }

        await repairService.EnsureCurrentOrganizationCatalogAsync();
        await technicalAgentBridge.SynchronizeDirectoryProjectionAsync();

        var roster = await aiAgentService.ListAgentDirectoryAsync();
        var currentWorkspace = workspaceFactory.GetOrganizationWorkspaceService();
        var currentAgents = await currentWorkspace.ListAgentsAsync(includeTemplates: false);
        var rosterItem = Assert.Single(roster, item => item.PartyId == partyId);

        Assert.Equal(legacyTechnicalAgentId, rosterItem.TechnicalAgentId);
        Assert.Contains(currentAgents, item => item.Id == legacyTechnicalAgentId && item.Name == "Late Imported Engineer");
    }

    [Fact]
    public async Task ListAgentDirectoryAsync_reprojects_party_metadata_from_agentframework_catalog()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var partyDirectoryService = scope.ServiceProvider.GetRequiredService<PartyDirectoryService>();
        var aiAgentService = scope.ServiceProvider.GetRequiredService<AiAgentService>();
        var workspaceFactory = scope.ServiceProvider.GetRequiredService<ICanDoItAllAgentWorkspaceFactory>();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();

        var partyId = await CreateAgentAsync(partyDirectoryService, "Stale CRM Label");
        var currentWorkspace = workspaceFactory.GetOrganizationWorkspaceService();
        var editor = await currentWorkspace.GetAgentEditorAsync();
        editor.Name = "Canonical Framework Reviewer";
        editor.RoleTitle = "Code reviewer";
        editor.Summary = "Canonical AgentFramework summary.";
        editor.Instructions = "Review the real files and durable evidence.";
        editor.Status = AgentLifecycleStatus.Suspended;
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
                source = "agent-framework",
                capabilities = Array.Empty<string>()
            }
        });

        var technicalAgentId = await currentWorkspace.SaveAgentAsync(editor);

        var rosterItem = Assert.Single(
            await aiAgentService.ListAgentDirectoryAsync(),
            item => item.PartyId == partyId);

        Assert.Equal(technicalAgentId, rosterItem.TechnicalAgentId);
        Assert.Equal("Canonical Framework Reviewer", rosterItem.DisplayName);
        Assert.Equal("Canonical AgentFramework summary.", rosterItem.Summary);
        Assert.Equal(PartyLifecycleStatus.Inactive, rosterItem.LifecycleStatus);

        await using var verificationContext = await dbContextFactory.CreateDbContextAsync();
        var party = await verificationContext.Set<Party>()
            .SingleAsync(item => item.Id == partyId);

        Assert.Equal("Canonical Framework Reviewer", party.DisplayName);
        Assert.Equal("Canonical AgentFramework summary.", party.Summary);
        Assert.Equal(PartyLifecycleStatus.Inactive, party.LifecycleStatus);
    }

    private static async Task<Guid> CreatePersonAsync(PartyDirectoryService partyDirectoryService, string displayName, string email)
    {
        var result = await partyDirectoryService.SavePartyAsync(new PartyEditorModel
        {
            PartyType = PartyType.Person,
            LifecycleStatus = PartyLifecycleStatus.Active,
            DisplayName = displayName,
            Summary = $"{displayName} summary",
            LastChangedBy = "integration-tests",
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

    private static async Task<Guid> CreateOrganizationAsync(PartyDirectoryService partyDirectoryService, string displayName)
    {
        var result = await partyDirectoryService.SavePartyAsync(new PartyEditorModel
        {
            PartyType = PartyType.Organization,
            LifecycleStatus = PartyLifecycleStatus.Active,
            DisplayName = displayName,
            Summary = $"{displayName} summary",
            LastChangedBy = "integration-tests"
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
            LastChangedBy = "integration-tests"
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
}
