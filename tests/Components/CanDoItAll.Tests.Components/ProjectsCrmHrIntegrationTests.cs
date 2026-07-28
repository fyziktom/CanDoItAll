using Bunit;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Projects.Pages;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components;

public sealed class ProjectsCrmHrIntegrationTests
{
    [Fact]
    public async Task Projects_page_shows_related_parties_and_filters_by_selected_relationship()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var partyDirectoryService = harness.Context.Services.GetRequiredService<PartyDirectoryService>();
        var bridge = harness.Context.Services.GetRequiredService<IProjectPartyIntegrationBridge>();

        var projectId = await CreateProjectAsync(projectsService, "CRM-HR Project Card");
        var customerId = await CreatePartyAsync(partyDirectoryService, PartyType.Organization, "Northwind Customer");
        var deliveryUnitId = await CreatePartyAsync(partyDirectoryService, PartyType.OrganizationUnit, "Delivery Guild");
        var ownerId = await CreatePartyAsync(partyDirectoryService, PartyType.Person, "Jordan Owner");

        await bridge.SaveAssignmentAsync(new ProjectPartyAssignmentUpsertRequest
        {
            ProjectId = projectId,
            PartyId = customerId,
            Role = ProjectPartyAssignmentRole.Customer,
            IsPrimary = true,
            Source = "component-tests"
        });
        await bridge.SaveAssignmentAsync(new ProjectPartyAssignmentUpsertRequest
        {
            ProjectId = projectId,
            PartyId = deliveryUnitId,
            Role = ProjectPartyAssignmentRole.DeliveryUnit,
            IsPrimary = true,
            Source = "component-tests"
        });
        await bridge.SaveAssignmentAsync(new ProjectPartyAssignmentUpsertRequest
        {
            ProjectId = projectId,
            PartyId = ownerId,
            Role = ProjectPartyAssignmentRole.Manager,
            IsPrimary = true,
            Source = "component-tests"
        });

        var cut = harness.Context.Render<ProjectsPage>();

        cut.WaitForAssertion(() =>
        {
            var projectCard = cut.FindAll("[data-testid='project-card']")
                .Single(card => card.TextContent.Contains("CRM-HR Project Card", StringComparison.Ordinal));

            Assert.Contains("Customer: Northwind Customer", projectCard.TextContent);
            Assert.Contains("Delivery unit: Delivery Guild", projectCard.TextContent);
            Assert.Contains("Owner: Jordan Owner", projectCard.TextContent);
        });

        cut.Find("[data-testid='project-related-party-category-filter']").Change(ProjectPartyPortfolioCategory.Customer.ToString());
        cut.Find("[data-testid='project-related-party-value-filter']").Change("Northwind Customer");

        cut.WaitForAssertion(() =>
        {
            var cards = cut.FindAll("[data-testid='project-card']");
            Assert.Single(cards);
            Assert.Contains("CRM-HR Project Card", cards[0].TextContent);
        });
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
            LastChangedBy = "component-tests"
        });

        Assert.True(result.IsSuccess);
        return result.Value;
    }
}
