using Bunit;
using CanDoItAll.AppComponents;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.CrmHr.Pages;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components;

public sealed class CrmHrCatalogDialogTests
{
    [Fact]
    public async Task Directory_deep_link_opens_a_controlled_dialog_over_the_bounded_catalog()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var partyDirectoryService = harness.Context.Services.GetRequiredService<PartyDirectoryService>();
        var navigation = harness.Context.Services.GetRequiredService<NavigationManager>();
        var partyId = await CreatePartyAsync(
            partyDirectoryService,
            "Directory dialog proof",
            PartyRoleKind.Employee);

        navigation.NavigateTo($"/crm-hr/directory?partyId={partyId:D}");
        var cut = harness.Context.RenderComponent<CrmHrDirectoryPage>();

        cut.WaitForAssertion(() =>
        {
            Assert.NotNull(cut.Find("[data-testid='crmhr-directory-catalog']"));
            Assert.Contains(
                "paged-record-browser__results--bounded",
                cut.Find("[data-testid='crmhr-directory-results']").ClassList);
            Assert.NotNull(cut.Find("[data-testid='crmhr-directory-record-dialog']"));
            Assert.Equal(
                "Directory dialog proof",
                cut.Find("[data-testid='crmhr-party-display-name']").GetAttribute("value"));
        });

        cut.Find("[data-testid='crmhr-directory-record-close']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Empty(cut.FindAll("[data-testid='crmhr-directory-record-dialog']"));
            Assert.Empty(cut.FindAll("[data-testid='crmhr-party-display-name']"));
            Assert.EndsWith("/crm-hr/directory", navigation.Uri, StringComparison.Ordinal);
            Assert.NotNull(cut.Find("[data-testid='crmhr-directory-catalog']"));
        });
    }

    [Fact]
    public async Task Workforce_deep_link_opens_a_controlled_dialog_over_the_bounded_catalog()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var partyDirectoryService = harness.Context.Services.GetRequiredService<PartyDirectoryService>();
        var hrService = harness.Context.Services.GetRequiredService<HrService>();
        var navigation = harness.Context.Services.GetRequiredService<NavigationManager>();
        var partyId = await CreatePartyAsync(
            partyDirectoryService,
            "Workforce dialog proof",
            PartyRoleKind.Employee);
        var profileResult = await hrService.SaveWorkforceProfileAsync(new WorkforceProfileEditorModel
        {
            PartyId = partyId,
            WorkforceKind = WorkforceKind.Employee,
            Status = "Active",
            JobTitle = "Dialog tester",
            CapacityHoursPerWeek = 40m,
            LastChangedBy = "component-tests"
        });
        Assert.True(profileResult.IsSuccess);

        navigation.NavigateTo($"/crm-hr/workforce?partyId={partyId:D}");
        var cut = harness.Context.RenderComponent<CrmHrWorkforcePage>();

        cut.WaitForAssertion(() =>
        {
            Assert.NotNull(cut.Find("[data-testid='crmhr-workforce-catalog']"));
            Assert.Contains(
                "paged-record-browser__results--bounded",
                cut.Find("[data-testid='crmhr-workforce-results']").ClassList);
            Assert.NotNull(cut.Find("[data-testid='crmhr-workforce-record-dialog']"));
            Assert.Contains("Workforce dialog proof", cut.Markup);
        });

        cut.Find("[data-testid='crmhr-workforce-record-close']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Empty(cut.FindAll("[data-testid='crmhr-workforce-record-dialog']"));
            Assert.EndsWith("/crm-hr/workforce", navigation.Uri, StringComparison.Ordinal);
            Assert.NotNull(cut.Find("[data-testid='crmhr-workforce-catalog']"));
        });
    }

    private static async Task<Guid> CreatePartyAsync(
        PartyDirectoryService partyDirectoryService,
        string displayName,
        PartyRoleKind roleKind)
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
                    RoleKind = roleKind,
                    Title = roleKind.ToString(),
                    IsPrimary = true
                }
            ]
        });
        Assert.True(result.IsSuccess);
        return result.Value;
    }
}
