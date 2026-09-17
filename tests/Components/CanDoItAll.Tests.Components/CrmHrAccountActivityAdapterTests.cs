using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.CrmHr.UI.Accounts;
using CanDoItAll.CrmHr.UI.Activity;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.CrmHr.Components;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.CrmHr;

// The module's compatibility adapters composed with the real rendering surfaces and the real buttons: callbacks reach
// the page only for the record the adapter shows at the moment they run, whatever render created them.
public sealed class CrmHrAccountActivityAdapterTests
{
    private static readonly Guid AccountId = Guid.Parse("83000000-0000-0000-0000-000000000001");
    private static readonly Guid OtherAccountId = Guid.Parse("83000000-0000-0000-0000-000000000002");

    [Fact]
    public async Task A_conversion_action_rendered_for_one_account_never_converts_the_account_shown_later()
    {
        using var context = CreateContext();
        var opened = 0;
        var conversions = new List<Guid>();
        var first = Account(AccountId, CrmAccountRelationshipStage.Prospect);
        var second = Account(OtherAccountId, CrmAccountRelationshipStage.Prospect);

        var cut = context.Render<AccountSummaryPanel>(parameters => parameters
            .Add(component => component.Account, first)
            .Add(component => component.OpenDirectory, () => opened++)
            .Add(component => component.MarkActiveCustomer, id => conversions.Add(id)));
        Assert.Equal("Prospect", cut.Find("[data-testid='crmhr-account-summary-stage']").TextContent.Trim());
        var retainedConvert = FindButton(cut, "Convert to active customer").Instance.Click;
        var retainedDirectory = FindButton(cut, "Open directory record").Instance.Click;

        // Unchanged target: the retained callbacks are still the shown record's own actions.
        await cut.InvokeAsync(() => retainedConvert.InvokeAsync());
        await cut.InvokeAsync(() => retainedDirectory.InvokeAsync());
        Assert.Equal(new[] { AccountId }, conversions);
        Assert.Equal(1, opened);

        // A same-model rerender keeps the origin valid.
        cut.Render(parameters => parameters.Add(component => component.Account, first));
        await cut.InvokeAsync(() => retainedConvert.InvokeAsync());
        Assert.Equal(new[] { AccountId, AccountId }, conversions);

        // The page moved on to another account: the callbacks created for the first one are inert here, and only an
        // action created for the shown account reaches the page.
        cut.Render(parameters => parameters.Add(component => component.Account, second));
        Assert.Equal(OtherAccountId.ToString("D"), cut.Find("[data-testid='crmhr-account-summary']").GetAttribute("data-account-id"));
        await cut.InvokeAsync(() => retainedConvert.InvokeAsync());
        await cut.InvokeAsync(() => retainedDirectory.InvokeAsync());
        Assert.Equal(new[] { AccountId, AccountId }, conversions);
        Assert.Equal(1, opened);

        cut.Find("[data-testid='crmhr-account-convert-active-button']").Click();
        Assert.Equal(new[] { AccountId, AccountId, OtherAccountId }, conversions);
    }

    [Fact]
    public async Task Retained_actions_are_inert_after_the_account_went_away_and_after_the_same_account_was_loaded_again()
    {
        using var context = CreateContext();
        var opened = 0;
        var conversions = new List<Guid>();

        var cut = context.Render<AccountSummaryPanel>(parameters => parameters
            .Add(component => component.Account, Account(AccountId, CrmAccountRelationshipStage.Prospect))
            .Add(component => component.OpenDirectory, () => opened++)
            .Add(component => component.MarkActiveCustomer, id => conversions.Add(id)));
        var retainedConvert = FindButton(cut, "Convert to active customer").Instance.Click;
        var retainedDirectory = FindButton(cut, "Open directory record").Instance.Click;

        // No account shown: nothing throws, nothing is forwarded, and the empty state's own action still works.
        cut.Render(parameters => parameters.Add(component => component.Account, null));
        await cut.InvokeAsync(() => retainedConvert.InvokeAsync());
        await cut.InvokeAsync(() => retainedDirectory.InvokeAsync());
        Assert.Empty(conversions);
        Assert.Equal(0, opened);
        cut.Find("[data-testid='crmhr-account-summary-open-directory']").Click();
        Assert.Equal(1, opened);

        // The same account loaded again is a new selection lifetime with a new model instance: an action from the
        // earlier lifetime stays inert, a fresh action converts.
        cut.Render(parameters => parameters.Add(component => component.Account, Account(AccountId, CrmAccountRelationshipStage.Prospect)));
        await cut.InvokeAsync(() => retainedConvert.InvokeAsync());
        Assert.Empty(conversions);
        cut.Find("[data-testid='crmhr-account-convert-active-button']").Click();
        Assert.Equal(new[] { AccountId }, conversions);
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
    public async Task InteractionTimeline_keeps_the_host_copy_and_test_ids_and_forwards_a_page_request_only_while_it_is_admissible()
    {
        using var context = CreateContext();
        var requested = new List<int>();
        var firstPage = new CrmHrActivityPage(
            [
                new CrmHrActivityEntry(Guid.NewGuid(), "Interaction", "Quarterly review", "Confirmed scope.", "Meeting / Bram Vos", new DateTimeOffset(2026, 3, 9, 14, 30, 0, TimeSpan.Zero), CrmHrActivityTone.Info, IsOverdue: true),
                new CrmHrActivityEntry(Guid.NewGuid(), "Audit", "Party saved", "Updated", "crm-hr-ui", new DateTimeOffset(2026, 3, 8, 9, 0, 0, TimeSpan.Zero), CrmHrActivityTone.Warning, IsOverdue: false)
            ],
            PageIndex: 0,
            PageSize: 10,
            TotalCount: 12,
            ActionCount: 4,
            OverdueActionCount: 1);

        var cut = context.Render<InteractionTimeline>(parameters => parameters
            .Add(component => component.Presentation, CrmHrActivityPresentation.Ready(firstPage))
            .Add(component => component.DataTestId, "crmhr-directory-activity")
            .Add(component => component.Eyebrow, "Party history")
            .Add(component => component.Title, "Recent interactions, changes, and follow-ups")
            .Add(component => component.PageRequested, index => requested.Add(index)));

        var surface = cut.FindComponent<CrmHrActivitySurface>();
        Assert.Equal(12, surface.Instance.Presentation.Accepted?.TotalCount);
        Assert.Equal("Party history", surface.Instance.Copy.Eyebrow);
        Assert.Equal(CrmHrActivityCopy.Default.EmptyTitle, surface.Instance.Copy.EmptyTitle);
        Assert.Equal(2, cut.FindAll("[data-testid='crmhr-directory-activity-item']").Count);
        Assert.Equal("1 overdue", cut.Find("[data-testid='crmhr-directory-activity-overdue-total']").TextContent.Trim());
        Assert.Contains("Page 1 of 2", cut.Find("[data-testid='crmhr-directory-activity-pager']").TextContent, StringComparison.Ordinal);
        var retainedNext = Assert.Single(cut.FindComponents<Button>(), button => button.Instance.Text == "Next").Instance.Click;

        cut.Find("[data-testid='crmhr-directory-activity-next']").Click();
        Assert.Equal(new[] { 1 }, requested);

        // The host began the read: the same action, dispatched again, is dropped by the surface and the adapter.
        cut.Render(parameters => parameters.Add(component => component.Presentation, CrmHrActivityPresentation.Loading(firstPage)));
        Assert.NotNull(cut.Find("[data-testid='crmhr-directory-activity-loading']"));
        Assert.Contains("12 activities", cut.Find("[data-testid='crmhr-directory-activity-totals']").TextContent, StringComparison.Ordinal);
        await cut.InvokeAsync(() => retainedNext.InvokeAsync());
        Assert.Equal(new[] { 1 }, requested);

        // Nothing accepted for the record: no page can be requested at all.
        cut.Render(parameters => parameters.Add(component => component.Presentation, CrmHrActivityPresentation.NotAccepted));
        Assert.NotNull(cut.Find("[data-testid='crmhr-directory-activity-unavailable']"));
        await cut.InvokeAsync(() => retainedNext.InvokeAsync());
        Assert.Equal(new[] { 1 }, requested);
    }

    private static IRenderedComponent<Button> FindButton(IRenderedComponent<AccountSummaryPanel> cut, string text)
        => Assert.Single(cut.FindComponents<Button>(), button => button.Instance.Text == text);

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
