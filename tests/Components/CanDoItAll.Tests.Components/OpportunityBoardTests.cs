using Bunit;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.CrmHr.Pages;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components;

public sealed class OpportunityBoardTests
{
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
