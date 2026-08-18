using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Projects;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration.CrmHr;

public sealed class OpportunityConversionIntegrationTests
{
    [Fact]
    public async Task Won_opportunity_conversion_creates_project_and_preserves_party_assignments()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var partyDirectoryService = scope.ServiceProvider.GetRequiredService<PartyDirectoryService>();
        var crmService = scope.ServiceProvider.GetRequiredService<CrmService>();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var projectPartyIntegrationService = scope.ServiceProvider.GetRequiredService<ProjectPartyIntegrationService>();

        var accountId = await CreatePartyAsync(
            partyDirectoryService,
            "Fabrikam Delivery",
            PartyType.Organization,
            PartyLifecycleStatus.Active,
            PartyRoleKind.Customer,
            "crm@fabrikam.example");
        var ownerId = await CreatePartyAsync(
            partyDirectoryService,
            "Nina Owner",
            PartyType.Person,
            PartyLifecycleStatus.Active,
            PartyRoleKind.AccountManager,
            "nina.owner@example.test");
        var deliveryUnitId = await CreatePartyAsync(
            partyDirectoryService,
            "Delivery Unit West",
            PartyType.OrganizationUnit,
            PartyLifecycleStatus.Active,
            PartyRoleKind.DeliveryUnit,
            "unit.west@example.test");
        var partnerId = await CreatePartyAsync(
            partyDirectoryService,
            "Contoso Partner",
            PartyType.Organization,
            PartyLifecycleStatus.Active,
            PartyRoleKind.Partner,
            "partner@contoso.example");
        var billingContactId = await CreatePartyAsync(
            partyDirectoryService,
            "Bri Billing",
            PartyType.Person,
            PartyLifecycleStatus.Active,
            PartyRoleKind.CustomerContact,
            "billing@example.test");

        var saveResult = await crmService.SaveOpportunityAsync(new CrmOpportunityEditorModel
        {
            AccountPartyId = accountId,
            Title = "Managed platform handoff",
            Stage = OpportunityStage.Won,
            OpportunitySource = OpportunitySource.Partner,
            OwnerPartyId = ownerId,
            DeliveryUnitPartyId = deliveryUnitId,
            CurrencyCode = "USD",
            Amount = 180000m,
            ProbabilityPercent = 100,
            ExpectedCloseOn = new DateOnly(2026, 7, 1),
            Summary = "Closed won managed platform engagement.",
            Notes = "Ready for project handoff.",
            PartnerContributionSummary = "Partner led procurement and sponsor alignment.",
            Parties =
            [
                new CrmOpportunityPartyLinkEditorModel
                {
                    PartyId = partnerId,
                    Role = OpportunityPartyRole.Partner
                },
                new CrmOpportunityPartyLinkEditorModel
                {
                    PartyId = billingContactId,
                    Role = OpportunityPartyRole.BillingContact
                }
            ],
            StageNotes = "Closed won after procurement sign-off.",
            LastChangedBy = "integration-tests"
        });

        Assert.True(saveResult.IsSuccess);
        var savedOpportunity = await crmService.GetOpportunityAsync(saveResult.Value);
        Assert.NotNull(savedOpportunity);

        var conversionResult = await crmService.ConvertOpportunityToProjectAsync(new CrmOpportunityConversionEditorModel
        {
            OpportunityId = saveResult.Value,
            ExpectedUpdatedAtUtc = savedOpportunity!.UpdatedAtUtc,
            ProjectName = "Fabrikam Platform Handoff",
            ProjectDescription = "Platform delivery context created from the won CRM opportunity.",
            ProjectObjective = "Start structured delivery with preserved commercial relationships.",
            CurrentPhase = "Sales handoff",
            LastChangedBy = "integration-tests"
        });

        Assert.True(conversionResult.IsSuccess);
        var conversion = conversionResult.Value;
        Assert.NotNull(conversion);

        var workspace = await crmService.GetAccountWorkspaceAsync(accountId);
        Assert.NotNull(workspace);
        var opportunity = await crmService.GetOpportunityAsync(saveResult.Value);
        Assert.NotNull(opportunity);
        Assert.Equal(conversion!.ProjectId, opportunity.LinkedProjectId);

        var project = await projectsService.GetAsync(conversion.ProjectId);
        Assert.Equal("Fabrikam Platform Handoff", project.Name);
        Assert.Equal(ProjectStatus.Active, project.Status);
        Assert.Equal("Sales handoff", project.CurrentPhase);

        var assignments = await projectPartyIntegrationService.ListAssignmentsDetailedAsync(conversion.ProjectId);
        Assert.Contains(assignments, item => item.PartyId == accountId && item.Role == ProjectPartyAssignmentRole.Customer && item.IsPrimary);
        Assert.Contains(assignments, item => item.PartyId == ownerId && item.Role == ProjectPartyAssignmentRole.Manager && item.IsPrimary);
        Assert.Contains(assignments, item => item.PartyId == deliveryUnitId && item.Role == ProjectPartyAssignmentRole.DeliveryUnit && item.IsPrimary);
        Assert.Contains(assignments, item => item.PartyId == partnerId && item.Role == ProjectPartyAssignmentRole.Partner);
        Assert.Contains(assignments, item => item.PartyId == billingContactId && item.Role == ProjectPartyAssignmentRole.BillingContact);

        var projectSummary = (await projectsService.ListAsync()).Single(item => item.Id == conversion.ProjectId);
        Assert.Equal("Fabrikam Delivery", projectSummary.PrimaryCustomerName);
        Assert.Equal("Delivery Unit West", projectSummary.PrimaryDeliveryUnitName);
        Assert.Equal("Nina Owner", projectSummary.PrimaryOwnerName);
        Assert.Contains(projectSummary.RelatedParties!, item => item.DisplayName == "Contoso Partner");

        var activity = await crmService.SearchAccountActivityAsync(
            new CrmActivityHistoryQuery(accountId));
        Assert.Contains(
            activity.Items,
            item => item.Title.Contains("Converted opportunity", StringComparison.Ordinal));
    }

    private static async Task<Guid> CreatePartyAsync(
        PartyDirectoryService partyDirectoryService,
        string displayName,
        PartyType partyType,
        PartyLifecycleStatus lifecycleStatus,
        PartyRoleKind roleKind,
        string email)
    {
        var result = await partyDirectoryService.SavePartyAsync(new PartyEditorModel
        {
            PartyType = partyType,
            LifecycleStatus = lifecycleStatus,
            DisplayName = displayName,
            Summary = $"{displayName} summary",
            LastChangedBy = "integration-tests",
            Roles =
            [
                new PartyRoleAssignmentEditorModel
                {
                    RoleKind = roleKind,
                    Title = roleKind.ToString(),
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
}
