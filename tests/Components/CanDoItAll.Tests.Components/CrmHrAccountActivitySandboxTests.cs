using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.CrmHr.UI.Accounts;
using CanDoItAll.CrmHr.UI.Activity;
using CanDoItAll.CrmHr.UiSandbox.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.CrmHr;

public sealed class CrmHrAccountActivitySandboxTests : IDisposable
{
    private readonly BunitContext context = new();

    public CrmHrAccountActivitySandboxTests()
    {
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddLogging();
        context.Services.AddCanDoItAllBaseLib();
    }

    [Theory]
    [InlineData("populated", "crmhr-account-activity-item")]
    [InlineData("no-account", "crmhr-account-summary-empty")]
    [InlineData("active-customer", "crmhr-account-summary")]
    [InlineData("loading", "crmhr-workforce-history-loading")]
    [InlineData("empty", "crmhr-directory-activity-empty")]
    [InlineData("overdue", "crmhr-account-activity-overdue-total")]
    [InlineData("long-text", "crmhr-account-summary-text")]
    [InlineData("null-contacts", "crmhr-account-summary")]
    [InlineData("many-pages", "crmhr-workforce-history-next")]
    public void Query_restores_scenarios_with_the_real_surfaces(string scenario, string testId)
    {
        var cut = Render(scenario);

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find($"[data-testid='{testId}']")));
        Assert.Equal(scenario, cut.Find("[data-testid='crmhr-sandbox-frame']").GetAttribute("data-scenario"));
        Assert.NotNull(cut.FindComponent<CrmHrAccountSummarySurface>());
        Assert.Equal(3, cut.FindComponents<CrmHrActivitySurface>().Count);
    }

    [Fact]
    public void Active_customer_scenario_offers_no_conversion_and_the_no_account_scenario_shows_no_summary()
    {
        var active = Render("active-customer");
        active.WaitForAssertion(() => Assert.NotNull(active.Find("[data-testid='crmhr-account-summary']")));
        Assert.Empty(active.FindAll("[data-testid='crmhr-account-convert-active-button']"));
        Assert.Equal("ActiveCustomer", active.Find("[data-testid='crmhr-account-summary-stage']").TextContent.Trim());

        var none = Render("no-account");
        none.WaitForAssertion(() => Assert.NotNull(none.Find("[data-testid='crmhr-account-summary-empty']")));
        Assert.Empty(none.FindAll("[data-testid='crmhr-account-summary']"));
    }

    [Fact]
    public void The_three_timeline_hosts_page_independently_and_record_their_intents()
    {
        var cut = Render("many-pages");
        cut.WaitForAssertion(() => Assert.Equal("5", cut.Find("[data-testid='crmhr-directory-activity']").GetAttribute("data-total-pages")));

        cut.Find("[data-testid='crmhr-directory-activity-next']").Click();

        cut.WaitForAssertion(() => Assert.Equal("1", cut.Find("[data-testid='crmhr-directory-activity']").GetAttribute("data-page-index")));
        Assert.Equal("0", cut.Find("[data-testid='crmhr-account-activity']").GetAttribute("data-page-index"));
        Assert.Equal("0", cut.Find("[data-testid='crmhr-workforce-history']").GetAttribute("data-page-index"));
        Assert.Equal("Request page: Directory 2", cut.Find("[data-testid='crmhr-sandbox-intent']").TextContent);
        Assert.Contains("Page 2 of 5", cut.Find("[data-testid='crmhr-directory-activity-pager']").TextContent, StringComparison.Ordinal);
        Assert.Equal(10, cut.FindAll("[data-testid='crmhr-directory-activity-item']").Count);
        // Each host carries its own wording.
        Assert.Contains("Party history", cut.Find("[data-testid='crmhr-directory-activity']").TextContent, StringComparison.Ordinal);
        Assert.Contains("Workforce history", cut.Find("[data-testid='crmhr-workforce-history']").TextContent, StringComparison.Ordinal);
        Assert.Contains(CrmHrActivityCopy.Default.Title, cut.Find("[data-testid='crmhr-account-activity']").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void The_conversion_intent_updates_local_state_and_withdraws_the_offer()
    {
        var cut = Render("populated");
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='crmhr-account-convert-active-button']")));

        cut.Find("[data-testid='crmhr-account-convert-active-button']").Click();

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("[data-testid='crmhr-account-convert-active-button']")));
        Assert.StartsWith("Convert to active customer: account ", cut.Find("[data-testid='crmhr-sandbox-intent']").TextContent, StringComparison.Ordinal);
        Assert.Equal("ActiveCustomer", cut.Find("[data-testid='crmhr-account-summary-stage']").TextContent.Trim());
    }

    [Fact]
    public void Long_text_scenario_renders_markup_looking_values_as_text()
    {
        var cut = Render("long-text");

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='crmhr-account-summary-text']")));
        Assert.Empty(cut.FindAll("script"));
        Assert.Empty(cut.FindAll("[data-testid='crmhr-account-summary'] b, [data-testid='crmhr-account-activity-item'] b"));
        Assert.Contains("&lt;script&gt;", cut.Markup, StringComparison.Ordinal);
    }

    public void Dispose() => context.Dispose();

    private IRenderedComponent<Routes> Render(string scenario)
    {
        context.Services.GetRequiredService<NavigationManager>().NavigateTo($"/crm-hr/account-activity?scenario={scenario}&layout=matched");
        return context.Render<Routes>();
    }
}
