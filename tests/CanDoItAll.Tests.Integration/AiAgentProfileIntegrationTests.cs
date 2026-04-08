using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Workspace;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

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
        var providerSave = await workspaceService.SaveProviderAsync(new ProviderProfileEditorModel
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
