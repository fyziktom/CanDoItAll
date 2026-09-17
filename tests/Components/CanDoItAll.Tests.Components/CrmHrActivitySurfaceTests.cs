using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.CrmHr.UI.Activity;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.CrmHr;

// Controlled rendering of the activity timeline surface: no service, module runtime or history page model is involved.
public sealed class CrmHrActivitySurfaceTests
{
    private static readonly DateTimeOffset FirstAt = new(2026, 3, 9, 14, 30, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset SecondAt = new(2026, 3, 8, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Page_renders_totals_items_with_kind_tone_overdue_meta_and_timestamp_and_the_pager_requests_pages()
    {
        using var context = CreateContext();
        var intents = new List<CrmHrActivityIntent>();
        var page = new CrmHrActivityPage(
            [
                new CrmHrActivityEntry(Guid.NewGuid(), "Interaction", "Quarterly review", "Confirmed scope.", "Meeting / Bram Vos / Next action due 2026-03-20", FirstAt, CrmHrActivityTone.Info, IsOverdue: true),
                new CrmHrActivityEntry(Guid.NewGuid(), "Audit", "CRM account profile saved", null, "crm-hr-ui", SecondAt, CrmHrActivityTone.Neutral, IsOverdue: false)
            ],
            PageIndex: 1,
            PageSize: 10,
            TotalCount: 23,
            ActionCount: 5,
            OverdueActionCount: 2);

        var cut = context.Render<CrmHrActivitySurface>(parameters => parameters
            .Add(component => component.Page, page)
            .Add(component => component.DataTestId, "crmhr-account-activity")
            .Add(component => component.Intent, intent => intents.Add(intent)));

        var root = cut.Find("[data-testid='crmhr-account-activity']");
        Assert.Equal("1", root.GetAttribute("data-page-index"));
        Assert.Equal("3", root.GetAttribute("data-total-pages"));
        var totals = cut.Find("[data-testid='crmhr-account-activity-totals']").TextContent;
        Assert.Contains("23 activities", totals, StringComparison.Ordinal);
        Assert.Contains("5 next actions", totals, StringComparison.Ordinal);
        Assert.Equal("2 overdue", cut.Find("[data-testid='crmhr-account-activity-overdue-total']").TextContent.Trim());

        var items = cut.FindAll("[data-testid='crmhr-account-activity-item']");
        Assert.Equal(2, items.Count);
        Assert.Equal("Interaction", items[0].GetAttribute("data-kind"));
        Assert.Equal("true", items[0].GetAttribute("data-overdue"));
        Assert.Contains("Quarterly review", items[0].TextContent, StringComparison.Ordinal);
        Assert.Contains("Confirmed scope.", items[0].TextContent, StringComparison.Ordinal);
        Assert.Contains("Meeting / Bram Vos / Next action due 2026-03-20", items[0].TextContent, StringComparison.Ordinal);
        Assert.Contains(CrmHrActivityText.OverdueLabel, items[0].TextContent, StringComparison.Ordinal);
        Assert.Contains(CrmHrActivityText.FormatTimestamp(FirstAt), items[0].TextContent, StringComparison.Ordinal);
        Assert.Equal("Audit", items[1].GetAttribute("data-kind"));
        Assert.Equal("false", items[1].GetAttribute("data-overdue"));
        Assert.DoesNotContain(CrmHrActivityText.OverdueLabel, items[1].TextContent, StringComparison.Ordinal);
        var badges = cut.FindComponents<StatusBadge>();
        Assert.Equal("info", Assert.Single(badges, badge => badge.Instance.Text == "Interaction").Instance.Tone);
        Assert.Equal("neutral", Assert.Single(badges, badge => badge.Instance.Text == "Audit").Instance.Tone);

        Assert.Contains("Page 2 of 3", cut.Find("[data-testid='crmhr-account-activity-pager']").TextContent, StringComparison.Ordinal);
        Assert.False(cut.Find("[data-testid='crmhr-account-activity-previous']").HasAttribute("disabled"));
        Assert.False(cut.Find("[data-testid='crmhr-account-activity-next']").HasAttribute("disabled"));

        cut.Find("[data-testid='crmhr-account-activity-next']").Click();
        cut.Find("[data-testid='crmhr-account-activity-previous']").Click();

        Assert.Equal(new[] { 2, 0 }, intents.Select(intent => Assert.IsType<CrmHrActivityIntent.RequestPage>(intent).PageIndex));
    }

    [Fact]
    public void A_single_page_without_overdue_actions_hides_the_overdue_total_and_requests_nothing_beyond_its_bounds()
    {
        using var context = CreateContext();
        var intents = new List<CrmHrActivityIntent>();
        var page = new CrmHrActivityPage(
            [new CrmHrActivityEntry(Guid.NewGuid(), "Interaction", "Discovery call", "First conversation.", "Call / Bram Vos", FirstAt, CrmHrActivityTone.Success, IsOverdue: false)],
            PageIndex: 0,
            PageSize: 10,
            TotalCount: 1,
            ActionCount: 0,
            OverdueActionCount: 0);

        var cut = context.Render<CrmHrActivitySurface>(parameters => parameters
            .Add(component => component.Page, page)
            .Add(component => component.Intent, intent => intents.Add(intent)));

        Assert.Empty(cut.FindAll("[data-testid='crmhr-timeline-overdue-total']"));
        Assert.Contains("Page 1 of 1", cut.Find("[data-testid='crmhr-timeline-pager']").TextContent, StringComparison.Ordinal);
        Assert.True(cut.Find("[data-testid='crmhr-timeline-previous']").HasAttribute("disabled"));
        Assert.True(cut.Find("[data-testid='crmhr-timeline-next']").HasAttribute("disabled"));

        // Even a dispatched click on a bounded button never leaves the page range.
        cut.Find("[data-testid='crmhr-timeline-next']").Click();
        cut.Find("[data-testid='crmhr-timeline-previous']").Click();

        Assert.Empty(intents);
    }

    [Fact]
    public void Loading_shows_the_loading_state_instead_of_items_and_disables_paging()
    {
        using var context = CreateContext();
        var page = new CrmHrActivityPage(
            [new CrmHrActivityEntry(Guid.NewGuid(), "Interaction", "Stale row", null, "Call", FirstAt, CrmHrActivityTone.Neutral, IsOverdue: false)],
            PageIndex: 1,
            PageSize: 10,
            TotalCount: 30,
            ActionCount: 1,
            OverdueActionCount: 0);

        var cut = context.Render<CrmHrActivitySurface>(parameters => parameters
            .Add(component => component.Page, page)
            .Add(component => component.IsLoading, true));

        Assert.Equal("true", cut.Find("[data-testid='crmhr-timeline']").GetAttribute("data-loading"));
        Assert.NotNull(cut.Find("[data-testid='crmhr-timeline-loading']"));
        Assert.Empty(cut.FindAll("[data-testid='crmhr-timeline-item']"));
        Assert.True(cut.Find("[data-testid='crmhr-timeline-previous']").HasAttribute("disabled"));
        Assert.True(cut.Find("[data-testid='crmhr-timeline-next']").HasAttribute("disabled"));
        Assert.Contains("30 activities", cut.Find("[data-testid='crmhr-timeline-totals']").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void An_empty_page_shows_the_host_copy_under_the_host_test_identifier()
    {
        using var context = CreateContext();
        var copy = new CrmHrActivityCopy("Workforce history", "Recent staffing activity", "Staffing description.", "No workforce history", "No saved history for this record", "Save a workforce profile to build the trail.");

        var cut = context.Render<CrmHrActivitySurface>(parameters => parameters
            .Add(component => component.Page, CrmHrActivityPage.Empty())
            .Add(component => component.Copy, copy)
            .Add(component => component.DataTestId, "crmhr-workforce-history"));

        var root = cut.Find("[data-testid='crmhr-workforce-history']");
        Assert.Equal("0", root.GetAttribute("data-total-pages"));
        Assert.Contains("Workforce history", root.TextContent, StringComparison.Ordinal);
        Assert.Contains("Recent staffing activity", root.TextContent, StringComparison.Ordinal);
        var empty = cut.Find("[data-testid='crmhr-workforce-history-empty']");
        Assert.Contains("No saved history for this record", empty.TextContent, StringComparison.Ordinal);
        Assert.Contains("Save a workforce profile to build the trail.", empty.TextContent, StringComparison.Ordinal);
        Assert.Contains(CrmHrActivityText.NoPagesValue, cut.Find("[data-testid='crmhr-workforce-history-pager']").TextContent, StringComparison.Ordinal);
        Assert.Empty(cut.FindAll("[data-testid='crmhr-timeline-pager']"));
    }

    [Fact]
    public void Untrusted_text_is_rendered_as_text()
    {
        using var context = CreateContext();
        var page = new CrmHrActivityPage(
            [new CrmHrActivityEntry(Guid.NewGuid(), "Interaction", "<script>alert('activity')</script> Untrusted", "<b>Bold</b> description", "Meeting / <i>Participant</i>", FirstAt, CrmHrActivityTone.Info, IsOverdue: false)],
            0,
            10,
            1,
            0,
            0);

        var cut = context.Render<CrmHrActivitySurface>(parameters => parameters
            .Add(component => component.Page, page));

        Assert.Empty(cut.FindAll("script"));
        Assert.Empty(cut.FindAll("[data-testid='crmhr-timeline-item'] b, [data-testid='crmhr-timeline-item'] i"));
        Assert.Contains("&lt;script&gt;", cut.Markup, StringComparison.Ordinal);
    }

    private static BunitContext CreateContext()
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddCanDoItAllBaseLib();
        return context;
    }
}
