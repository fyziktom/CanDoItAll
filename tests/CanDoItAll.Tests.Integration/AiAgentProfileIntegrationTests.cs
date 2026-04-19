using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Workspace;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;

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
}
