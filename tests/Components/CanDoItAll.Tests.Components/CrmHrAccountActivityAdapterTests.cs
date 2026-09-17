using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.CrmHr.UI.Accounts;
using CanDoItAll.CrmHr.UI.Activity;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.CrmHr.Components;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.CrmHr;

// The module's compatibility adapters composed with the real rendering surfaces: the page-facing parameter shapes
// stay, the rendering comes from the library, and callbacks reach the page only for the record the adapter shows.
public sealed class CrmHrAccountActivityAdapterTests
{
    private static readonly Guid AccountId = Guid.Parse("83000000-0000-0000-0000-000000000001");
    private static readonly Guid OtherAccountId = Guid.Parse("83000000-0000-0000-0000-000000000002");

    [Fact]
    public async Task AccountSummaryPanel_maps_the_workspace_model_and_forwards_the_conversion_only_for_the_shown_account()
    {
        using var context = CreateContext();
        var opened = 0;
        var conversions = new List<Guid>();

        var cut = context.Render<AccountSummaryPanel>(parameters => parameters
            .Add(component => component.Account, Account(AccountId, CrmAccountRelationshipStage.Prospect))
            .Add(component => component.OpenDirectory, () => opened++)
            .Add(component => component.MarkActiveCustomer, id => conversions.Add(id)));

        var surface = cut.FindComponent<CrmHrAccountSummarySurface>();
        Assert.Equal(AccountId, surface.Instance.Account?.AccountPartyId);
        Assert.Equal("Prospect", cut.Find("[data-testid='crmhr-account-summary-stage']").TextContent.Trim());
        Assert.Equal("Active", cut.Find("[data-testid='crmhr-account-summary-lifecycle']").TextContent.Trim());
        Assert.Contains("accounts@aurora.example", cut.Markup, StringComparison.Ordinal);

        cut.Find("[data-testid='crmhr-account-convert-active-button']").Click();
        cut.Find("[data-testid='crmhr-account-summary-open-directory']").Click();
        Assert.Equal(new[] { AccountId }, conversions);
        Assert.Equal(1, opened);

        // The page moved on to another account; a conversion intent for the previous one is dropped by the adapter.
        cut.Render(parameters => parameters.Add(component => component.Account, Account(OtherAccountId, CrmAccountRelationshipStage.ActiveCustomer)));
        Assert.Empty(cut.FindAll("[data-testid='crmhr-account-convert-active-button']"));
        await cut.InvokeAsync(() => surface.Instance.Intent.InvokeAsync(new CrmHrAccountSummaryIntent.ConvertToActiveCustomer(AccountId)));
        Assert.Equal(new[] { AccountId }, conversions);

        await cut.InvokeAsync(() => surface.Instance.Intent.InvokeAsync(new CrmHrAccountSummaryIntent.ConvertToActiveCustomer(OtherAccountId)));
        Assert.Equal(new[] { AccountId, OtherAccountId }, conversions);
    }

    [Fact]
    public void AccountSummaryPanel_without_an_account_renders_the_empty_state_through_the_shared_surface()
    {
        using var context = CreateContext();
        var opened = 0;

        var cut = context.Render<AccountSummaryPanel>(parameters => parameters
            .Add(component => component.Account, null)
            .Add(component => component.OpenDirectory, () => opened++));

        Assert.Null(cut.FindComponent<CrmHrAccountSummarySurface>().Instance.Account);
        cut.Find("[data-testid='crmhr-account-summary-open-directory']").Click();
        Assert.Equal(1, opened);
    }

    [Fact]
    public void InteractionTimeline_maps_the_history_page_keeps_the_host_copy_and_test_ids_and_forwards_page_requests()
    {
        using var context = CreateContext();
        var requested = new List<int>();
        var page = new CrmActivityHistoryPage(
            [
                new CrmAccountActivityTimelineItemModel(Guid.NewGuid(), "Interaction", "Quarterly review", "Confirmed scope.", "Meeting / Bram Vos", new DateTimeOffset(2026, 3, 9, 14, 30, 0, TimeSpan.Zero), "info", IsOverdue: true),
                new CrmAccountActivityTimelineItemModel(Guid.NewGuid(), "Audit", "Party saved", "Updated", "crm-hr-ui", new DateTimeOffset(2026, 3, 8, 9, 0, 0, TimeSpan.Zero), "warning", IsOverdue: false)
            ],
            PageIndex: 0,
            PageSize: 10,
            TotalCount: 12,
            ActionCount: 4,
            OverdueActionCount: 1);

        var cut = context.Render<InteractionTimeline>(parameters => parameters
            .Add(component => component.Page, page)
            .Add(component => component.DataTestId, "crmhr-directory-activity")
            .Add(component => component.Eyebrow, "Party history")
            .Add(component => component.Title, "Recent interactions, changes, and follow-ups")
            .Add(component => component.PageRequested, index => requested.Add(index)));

        var surface = cut.FindComponent<CrmHrActivitySurface>();
        Assert.Equal(12, surface.Instance.Page.TotalCount);
        Assert.Equal("Party history", surface.Instance.Copy.Eyebrow);
        Assert.Equal(CrmHrActivityCopy.Default.EmptyTitle, surface.Instance.Copy.EmptyTitle);
        Assert.Equal(2, cut.FindAll("[data-testid='crmhr-directory-activity-item']").Count);
        Assert.Equal("1 overdue", cut.Find("[data-testid='crmhr-directory-activity-overdue-total']").TextContent.Trim());
        Assert.Contains("Party history", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("Page 1 of 2", cut.Find("[data-testid='crmhr-directory-activity-pager']").TextContent, StringComparison.Ordinal);

        cut.Find("[data-testid='crmhr-directory-activity-next']").Click();
        Assert.Equal(new[] { 1 }, requested);

        // The host owns loading: the same page reference with the loading flag shows the loading state.
        cut.Render(parameters => parameters.Add(component => component.IsLoading, true));
        Assert.NotNull(cut.Find("[data-testid='crmhr-directory-activity-loading']"));
        Assert.Empty(cut.FindAll("[data-testid='crmhr-directory-activity-item']"));
    }

    private static CrmAccountWorkspaceModel Account(Guid id, CrmAccountRelationshipStage stage)
        => new(
            id,
            "Aurora Logistics",
            "Regional logistics account.",
            PartyLifecycleStatus.Active,
            [PartyRoleKind.Customer],
            ["logistics"],
            "accounts@aurora.example",
            "+1 555 0100",
            new CrmAccountProfileEditorModel { AccountPartyId = id, RelationshipStage = stage },
            [],
            [],
            OpportunityCount: 2);

    private static BunitContext CreateContext()
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddCanDoItAllBaseLib();
        return context;
    }
}
