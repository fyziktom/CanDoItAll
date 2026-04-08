using Bunit;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.CrmHr.Pages;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components;

public sealed class CrmHrDirectoryPageTests
{
    [Fact]
    public async Task Creates_and_updates_a_basic_party_record()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var cut = harness.Context.RenderComponent<CrmHrDirectoryPage>();

        cut.Find("[data-testid='crmhr-party-type']").Change(PartyType.Organization.ToString());
        cut.Find("[data-testid='crmhr-party-status']").Change(PartyLifecycleStatus.Active.ToString());
        cut.Find("[data-testid='crmhr-party-display-name']").Change("Northwind Delivery");
        cut.Find("[data-testid='crmhr-party-role']").Change(PartyRoleKind.Partner.ToString());
        cut.Find("[data-testid='crmhr-party-tags']").Change("partner, strategic");
        cut.Find("[data-testid='crmhr-party-email']").Change("hello@northwind.example");
        cut.Find("[data-testid='crmhr-party-phone']").Change("+49 555 0101");
        cut.Find("[data-testid='crmhr-party-summary']").Change("Primary implementation partner.");
        cut.Find("[data-testid='crmhr-party-save-button']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Party saved.", cut.Markup);
            Assert.Contains("Northwind Delivery", cut.Markup);
            Assert.Contains("hello@northwind.example", cut.Markup);
        });

        cut.Find("[data-testid='crmhr-party-display-name']").Change("Northwind Delivery Updated");
        cut.Find("[data-testid='crmhr-party-save-button']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Northwind Delivery Updated", cut.Markup);
        });

        var partyDirectoryService = harness.Context.Services.GetRequiredService<PartyDirectoryService>();
        var directoryItems = await partyDirectoryService.ListDirectoryAsync();
        var savedItem = Assert.Single(directoryItems);

        Assert.Equal("Northwind Delivery Updated", savedItem.DisplayName);
        Assert.Contains(PartyRoleKind.Partner, savedItem.Roles);
        Assert.Equal("hello@northwind.example", savedItem.PrimaryEmail);
        Assert.Equal("+49 555 0101", savedItem.PrimaryPhone);
    }
}
