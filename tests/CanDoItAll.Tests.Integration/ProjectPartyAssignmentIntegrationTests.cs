using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Projects;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration;

public sealed class ProjectPartyAssignmentIntegrationTests
{
    [Fact]
    public async Task Bridge_persists_project_and_node_assignments_and_enriches_portfolio_context()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var partyDirectoryService = scope.ServiceProvider.GetRequiredService<PartyDirectoryService>();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var bridge = scope.ServiceProvider.GetRequiredService<IProjectPartyIntegrationBridge>();

        var projectId = await CreateProjectAsync(projectsService, "B10 Integration Project");
        var customerId = await CreatePartyAsync(partyDirectoryService, PartyType.Organization, "Acme Customer");
        var deliveryUnitId = await CreatePartyAsync(partyDirectoryService, PartyType.OrganizationUnit, "Platform Guild");
        var ownerId = await CreatePartyAsync(partyDirectoryService, PartyType.Person, "Morgan Owner");

        Assert.True((await bridge.SaveAssignmentAsync(new ProjectPartyAssignmentUpsertRequest
        {
            ProjectId = projectId,
            PartyId = customerId,
            Role = ProjectPartyAssignmentRole.Customer,
            IsPrimary = true,
            Source = "integration-tests"
        })).IsSuccess);
        Assert.True((await bridge.SaveAssignmentAsync(new ProjectPartyAssignmentUpsertRequest
        {
            ProjectId = projectId,
            PartyId = deliveryUnitId,
            Role = ProjectPartyAssignmentRole.DeliveryUnit,
            IsPrimary = true,
            AllocationPercent = 70m,
            Source = "integration-tests"
        })).IsSuccess);
        Assert.True((await bridge.SaveAssignmentAsync(new ProjectPartyAssignmentUpsertRequest
        {
            ProjectId = projectId,
            PartyId = ownerId,
            Role = ProjectPartyAssignmentRole.Manager,
            IsPrimary = true,
            NodeKey = "work-item-alpha",
            Source = "integration-tests"
        })).IsSuccess);

        var quickCreate = await bridge.CreatePartyAsync(new ProjectPartyQuickCreateRequest
        {
            ProjectId = projectId,
            PartyKind = ProjectPartyQuickCreateKind.AiAgent,
            DisplayName = "Review Agent",
            Summary = "Assists with structured review."
        });

        Assert.True(quickCreate.IsSuccess);
        var createdParty = quickCreate.Value;
        Assert.NotNull(createdParty);

        var detailedAssignments = await bridge.ListAssignmentsDetailedAsync(projectId);
        Assert.Equal(3, detailedAssignments.Count);
        Assert.Contains(detailedAssignments, item => item.Role == ProjectPartyAssignmentRole.Customer && item.PartyDisplayName == "Acme Customer");
        Assert.Contains(detailedAssignments, item => item.Role == ProjectPartyAssignmentRole.DeliveryUnit && item.AllocationPercent == 70m);
        Assert.Contains(detailedAssignments, item => item.Role == ProjectPartyAssignmentRole.Manager && item.NodeKey == "work-item-alpha");

        var contexts = await bridge.GetPortfolioContextsAsync([projectId]);
        var context = Assert.Single(contexts).Value;
        Assert.Equal("Acme Customer", context.PrimaryCustomerName);
        Assert.Equal("Platform Guild", context.PrimaryDeliveryUnitName);
        Assert.Contains("Acme Customer", context.SearchText, StringComparison.Ordinal);

        var options = await bridge.ListPartyOptionsAsync(projectId);
        Assert.Contains(options, item => item.PartyId == createdParty!.PartyId && item.PartyTypeLabel == "AI agent");
    }

    private static async Task<Guid> CreateProjectAsync(ProjectsService projectsService, string name)
    {
        var result = await projectsService.SaveAsync(new ProjectEditorModel
        {
            Name = name,
            Description = $"{name} description",
            Objective = $"{name} objective",
            CurrentPhase = "Discovery"
        });

        Assert.True(result.IsSuccess);
        return result.Value;
    }

    private static async Task<Guid> CreatePartyAsync(PartyDirectoryService partyDirectoryService, PartyType partyType, string displayName)
    {
        var result = await partyDirectoryService.SavePartyAsync(new PartyEditorModel
        {
            PartyType = partyType,
            LifecycleStatus = PartyLifecycleStatus.Active,
            DisplayName = displayName,
            Summary = $"{displayName} summary",
            LastChangedBy = "integration-tests"
        });

        Assert.True(result.IsSuccess);
        return result.Value;
    }
}
