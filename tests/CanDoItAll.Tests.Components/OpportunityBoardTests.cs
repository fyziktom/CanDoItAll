using Bunit;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.CrmHr.Pages;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components;

public sealed class OpportunityBoardTests
{
    [Fact]
    public async Task Crm_page_tracks_opportunity_stage_history_partner_context_and_loss_reason()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var partyDirectoryService = harness.Context.Services.GetRequiredService<PartyDirectoryService>();
        var crmService = harness.Context.Services.GetRequiredService<CrmService>();

        var accountId = await CreatePartyAsync(
            partyDirectoryService,
            "Northwind Advisory",
            PartyType.Organization,
            PartyLifecycleStatus.Prospect,
            PartyRoleKind.Customer,
            "crm@northwind.example");
        var ownerId = await CreatePartyAsync(
            partyDirectoryService,
            "Alicia Owner",
            PartyType.Person,
            PartyLifecycleStatus.Active,
            PartyRoleKind.AccountManager,
            "alicia.owner@example.test");
        var deliveryUnitId = await CreatePartyAsync(
            partyDirectoryService,
            "Delivery Unit East",
            PartyType.OrganizationUnit,
            PartyLifecycleStatus.Active,
            PartyRoleKind.DeliveryUnit,
            "delivery.east@example.test");
        var partnerId = await CreatePartyAsync(
            partyDirectoryService,
            "Partner Growth",
            PartyType.Organization,
            PartyLifecycleStatus.Active,
            PartyRoleKind.Partner,
            "partner@example.test");

        var cut = harness.Context.RenderComponent<CrmHrCrmPage>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Northwind Advisory", cut.Markup);
        });

        cut.Find("[data-testid='crmhr-opportunity-title']").Change("Retention expansion");
        cut.Find("[data-testid='crmhr-opportunity-source']").Change(OpportunitySource.Partner.ToString());
        cut.Find("[data-testid='crmhr-opportunity-owner']").Change(ownerId.ToString());
        cut.Find("[data-testid='crmhr-opportunity-delivery-unit']").Change(deliveryUnitId.ToString());
        cut.Find("[data-testid='crmhr-opportunity-currency']").Change("EUR");
        cut.Find("[data-testid='crmhr-opportunity-amount']").Change("125000");
        cut.Find("[data-testid='crmhr-opportunity-probability']").Change("40");
        cut.Find("[data-testid='crmhr-opportunity-close-date']").Change("2026-05-15");
        cut.Find("[data-testid='crmhr-opportunity-summary']").Change("Expansion into managed delivery.");
        cut.Find("[data-testid='crmhr-opportunity-notes']").Change("Partner introduced the account team.");
        cut.Find("[data-testid='crmhr-opportunity-partner-contribution']").Change("Introduced sponsor and local procurement lead.");
        cut.Find("[data-testid='crmhr-opportunity-party-add']").Click();
        cut.WaitForElement("[data-testid='crmhr-opportunity-party-0']");
        cut.Find("[data-testid='crmhr-opportunity-party-0']").Change(partnerId.ToString());
        cut.Find("[data-testid='crmhr-opportunity-party-role-0']").Change(OpportunityPartyRole.Partner.ToString());
        cut.Find("[data-testid='crmhr-opportunity-save-button']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Opportunity saved.", cut.Markup);
            Assert.Contains("Retention expansion", cut.Markup);
        });

        var workspace = await crmService.GetAccountWorkspaceAsync(accountId);
        Assert.NotNull(workspace);
        var opportunity = Assert.Single(workspace!.Opportunities);
        Assert.Equal(OpportunityStage.Identified, opportunity.Stage);
        Assert.Single(opportunity.StageHistory);
        Assert.Equal("Introduced sponsor and local procurement lead.", opportunity.PartnerContributionSummary);

        cut.Find($"[data-testid='crmhr-opportunity-advance-{opportunity.Id:N}']").Click();
        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Qualified", cut.Markup);
        });

        cut.Find("[data-testid='crmhr-opportunity-stage']").Change(OpportunityStage.Lost.ToString());
        cut.WaitForElement("[data-testid='crmhr-opportunity-lost-reason']");
        cut.Find("[data-testid='crmhr-opportunity-lost-reason']").Change("Budget shifted to another quarter.");
        cut.Find("[data-testid='crmhr-opportunity-competitor']").Change("Contoso Advisory");
        cut.Find("[data-testid='crmhr-opportunity-stage-notes']").Change("Lost after procurement rebaseline.");
        cut.Find("[data-testid='crmhr-opportunity-save-button']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Budget shifted to another quarter.", cut.Markup);
            Assert.Contains("Contoso Advisory", cut.Markup);
            Assert.Contains("Retention expansion", cut.Find("[data-testid='crmhr-opportunity-column-lost']").TextContent);
            Assert.True(cut.FindAll("[data-testid='crmhr-opportunity-history-item']").Count >= 3);
        });

        workspace = await crmService.GetAccountWorkspaceAsync(accountId);
        Assert.NotNull(workspace);
        opportunity = Assert.Single(workspace!.Opportunities);
        Assert.Equal(OpportunityStage.Lost, opportunity.Stage);
        Assert.Equal("Budget shifted to another quarter.", opportunity.LostReason);
        Assert.Equal("Contoso Advisory", opportunity.CompetitorName);
        Assert.True(opportunity.StageHistory.Count >= 3);
        Assert.Contains(opportunity.Parties, item => item.PartyId == partnerId && item.Role == OpportunityPartyRole.Partner);
    }

    [Fact]
    public async Task Home_page_surfaces_open_pipeline_preview()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var partyDirectoryService = harness.Context.Services.GetRequiredService<PartyDirectoryService>();
        var crmService = harness.Context.Services.GetRequiredService<CrmService>();

        var accountId = await CreatePartyAsync(
            partyDirectoryService,
            "Fabrikam Retainer",
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

        var saveResult = await crmService.SaveOpportunityAsync(new CrmOpportunityEditorModel
        {
            AccountPartyId = accountId,
            Title = "Renewal expansion",
            Stage = OpportunityStage.Proposal,
            OpportunitySource = OpportunitySource.Renewal,
            OwnerPartyId = ownerId,
            CurrencyCode = "USD",
            Amount = 45000m,
            ProbabilityPercent = 65,
            ExpectedCloseOn = new DateOnly(2026, 6, 20),
            Summary = "Renewal and extension of the advisory retainer.",
            LastChangedBy = "component-tests"
        });

        Assert.True(saveResult.IsSuccess);

        var cut = harness.Context.RenderComponent<CrmHrHomePage>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Open pipeline", cut.Markup);
            Assert.Contains("Renewal expansion", cut.Markup);
            Assert.Contains("Fabrikam Retainer", cut.Markup);
            Assert.Contains("Nina Owner", cut.Markup);
        });
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
            LastChangedBy = "component-tests",
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
