using System.Globalization;
using Bunit;
using Bunit.TestDoubles;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.ProviderHistory;
using CanDoItAll.AgentFramework.UI.Catalog;
using CanDoItAll.AgentFramework.UI.History;
using CanDoItAll.AppComponents.Navigation;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.AgentFramework.Pages;
using CanDoItAll.Modules.AgentFramework.Pages.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.AgentFramework;

public sealed class AgentsUrlStateTests {
    private const string ParentMarker = "cda-nav-parent:";

    [Fact]
    public async Task Tab_clicks_push_one_entry_each_and_the_default_tab_has_no_query() {
        await using var fixture = await UrlFixture.CreateAsync();
        fixture.Navigation.NavigateTo("/agents?tab=agents");
        var page = fixture.RenderPage();
        page.WaitForDashboardLoaded();

        await page.InvokeAsync(() => FindTab(page, "Providers").ClickAsync());
        page.WaitForAssertion(() => Assert.StartsWith("/agents?tab=providers", fixture.Location, StringComparison.Ordinal));
        Assert.Equal(2, fixture.Navigation.History.Count);

        await page.InvokeAsync(() => FindTab(page, "Overview").ClickAsync());
        page.WaitForAssertion(() => Assert.Equal("/agents", fixture.Location));
        Assert.Equal(3, fixture.Navigation.History.Count);
        Assert.False(fixture.Navigation.History.First().Options.ReplaceHistoryEntry);
        Assert.Equal(0, fixture.Back.BackCount);
    }

    [Fact]
    public async Task Opening_an_agent_pushes_an_overlay_entry_sections_replace_it_and_closing_goes_back() {
        await using var fixture = await UrlFixture.CreateAsync();
        var agent = await fixture.FirstAgentAsync();
        fixture.Navigation.NavigateTo("/agents?tab=agents");
        var dialogHost = fixture.Harness.Context.Render<DialogHost>();
        var page = fixture.RenderPage();
        var catalog = page.WaitForComponent<AgentCatalogPanel>();
        page.WaitForElement("[data-testid='agents-catalog-card']");

        await page.InvokeAsync(() => catalog.Instance.Intent.InvokeAsync(new AgentCatalogIntent.OpenAgent(agent.Id)));
        var editor = dialogHost.WaitForComponent<AgentDetailsDialog>();
        var opened = fixture.Navigation.History.First();
        Assert.Equal(2, fixture.Navigation.History.Count);
        Assert.False(opened.Options.ReplaceHistoryEntry);
        Assert.Equal(ParentMarker + "/agents?tab=agents", opened.Options.HistoryEntryState);
        Assert.Equal($"/agents?tab=agents&agentId={agent.Id:D}", fixture.Location);
        Assert.Equal(agent.Id, editor.Instance.AgentId);

        editor.WaitForElement("[data-testid='agents-details-tabs']");
        await editor.InvokeAsync(() => editor.FindAll("[data-testid='agents-details-tabs'] button[role='tab']")
            .Single(tab => tab.TextContent.Contains("Runtime", StringComparison.Ordinal)).ClickAsync());
        page.WaitForAssertion(() => Assert.Equal($"/agents?tab=agents&agentId={agent.Id:D}&agentSection=runtime", fixture.Location));
        var section = fixture.Navigation.History.First();
        Assert.Equal(2, fixture.Navigation.History.Count);
        Assert.True(section.Options.ReplaceHistoryEntry);
        Assert.Equal(ParentMarker + "/agents?tab=agents", section.Options.HistoryEntryState);

        await dialogHost.InvokeAsync(() => fixture.Dialogs.CloseAsync());
        page.WaitForAssertion(() => Assert.Equal("/agents?tab=agents", fixture.Location));
        Assert.Equal(1, fixture.Back.BackCount);
        Assert.Empty(fixture.Dialogs.Dialogs);
    }

    [Fact]
    public async Task A_deep_linked_editor_opens_its_section_and_closing_without_a_parent_entry_replaces_the_location() {
        await using var fixture = await UrlFixture.CreateAsync();
        var agent = await fixture.FirstAgentAsync();
        fixture.Navigation.NavigateTo($"/agents?tab=agents&agentId={agent.Id:D}&agentSection=runtime");
        var dialogHost = fixture.Harness.Context.Render<DialogHost>();
        fixture.RenderPage();

        var editor = dialogHost.WaitForComponent<AgentDetailsDialog>();
        Assert.Equal(agent.Id, editor.Instance.AgentId);
        Assert.Equal(AgentEditorSection.Runtime, editor.Instance.Section);
        Assert.Single(fixture.Navigation.History);

        await dialogHost.InvokeAsync(() => fixture.Dialogs.CloseAsync());
        dialogHost.WaitForAssertion(() => Assert.Equal("/agents?tab=agents", fixture.Location));
        Assert.Single(fixture.Navigation.History);
        Assert.True(fixture.Navigation.History.First().Options.ReplaceHistoryEntry);
        Assert.Equal(0, fixture.Back.BackCount);
        Assert.Empty(fixture.Dialogs.Dialogs);
    }

    [Fact]
    public async Task Browser_back_from_an_open_editor_closes_it_without_writing_history() {
        await using var fixture = await UrlFixture.CreateAsync();
        var agent = await fixture.FirstAgentAsync();
        fixture.Navigation.NavigateTo("/agents?tab=agents");
        var dialogHost = fixture.Harness.Context.Render<DialogHost>();
        var page = fixture.RenderPage();
        page.WaitForElement("[data-testid='agents-catalog-card']");
        fixture.Navigation.NavigateTo($"/agents?tab=agents&agentId={agent.Id:D}", new NavigationOptions {
            HistoryEntryState = ParentMarker + "/agents?tab=agents"
        });
        dialogHost.WaitForComponent<AgentDetailsDialog>();

        fixture.Navigation.NavigateTo("/agents?tab=agents");
        dialogHost.WaitForAssertion(() => Assert.Empty(fixture.Dialogs.Dialogs));

        Assert.Equal("/agents?tab=agents", fixture.Location);
        Assert.Equal(3, fixture.Navigation.History.Count);
        Assert.Equal(0, fixture.Back.BackCount);
    }

    [Fact]
    public async Task A_card_selected_before_its_double_click_or_forward_still_opens_the_editor() {
        await using var fixture = await UrlFixture.CreateAsync();
        var agent = await fixture.FirstAgentAsync();
        fixture.Navigation.NavigateTo("/agents?tab=agents");
        var dialogHost = fixture.Harness.Context.Render<DialogHost>();
        var page = fixture.RenderPage();
        page.WaitForElement("[data-testid='agents-catalog-card']");
        AngleSharp.Dom.IElement Card() => page.FindAll("[data-testid='agents-catalog-card']")
            .Single(card => card.GetAttribute("aria-label") == $"Select {agent.Name}");

        // A browser delivers click before dblclick, so the card is selected before the open request arrives.
        await page.InvokeAsync(() => Card().ClickAsync());
        await page.InvokeAsync(() => Card().DoubleClickAsync());
        dialogHost.WaitForAssertion(() => Assert.Equal(agent.Id, dialogHost.FindComponent<AgentDetailsDialog>().Instance.AgentId));
        var opened = fixture.Navigation.Uri;
        Assert.Equal($"/agents?tab=agents&agentId={agent.Id:D}", fixture.Location);

        await dialogHost.InvokeAsync(() => fixture.Dialogs.CloseAsync());
        page.WaitForAssertion(() => Assert.Equal("/agents?tab=agents", fixture.Location));
        dialogHost.WaitForAssertion(() => Assert.Empty(fixture.Dialogs.Dialogs));

        await page.InvokeAsync(() => Card().ClickAsync());
        fixture.Navigation.NavigateTo(opened, new NavigationOptions { HistoryEntryState = ParentMarker + "/agents?tab=agents" });
        dialogHost.WaitForAssertion(() => Assert.Equal(agent.Id, dialogHost.FindComponent<AgentDetailsDialog>().Instance.AgentId));
        Assert.Single(fixture.Dialogs.Dialogs);
    }

    [Fact]
    public async Task Malformed_and_foreign_values_are_canonicalized_with_one_replace() {
        await using var fixture = await UrlFixture.CreateAsync();
        fixture.Navigation.NavigateTo("/agents?tab=AGENTS&agentSection=runtime&usageScope=agents&teamId=not-a-guid&utm=keep#top");
        var page = fixture.RenderPage();

        page.WaitForAssertion(() => Assert.Equal("/agents?tab=agents&utm=keep#top", fixture.Location));
        Assert.Single(fixture.Navigation.History);
        Assert.True(fixture.Navigation.History.First().Options.ReplaceHistoryEntry);
    }

    [Fact]
    public async Task A_provider_deep_link_restores_the_provider_and_section_and_section_changes_push() {
        await using var fixture = await UrlFixture.CreateAsync(services => services.AddSingleton<IProviderRequestHistory>(new ProviderHistoryUiFixture()));
        var catalog = await fixture.Harness.Context.Services.GetRequiredService<IProviderProfilesReads>().LoadCatalogAsync();
        var provider = Assert.IsType<ProviderProfile>(catalog.Providers.LastOrDefault());
        fixture.Navigation.NavigateTo($"/agents?tab=providers&providerId={provider.Id:D}&providerSection=history");
        var page = fixture.RenderPage();

        page.WaitForElement("[data-testid='history-search-form']");
        page.WaitForAssertion(() => Assert.Equal(provider.Id, page.FindComponent<AgentProviderProfilesPanel>().Instance.SelectedProvider?.Id));
        Assert.Single(fixture.Navigation.History);
        Assert.Equal($"/agents?tab=providers&providerId={provider.Id:D}&providerSection=history", fixture.Location);

        await page.InvokeAsync(() => page.FindAll("[data-testid='provider-editor-tabs'] button[role='tab']")
            .Single(tab => tab.TextContent.Contains("Connection", StringComparison.Ordinal)).ClickAsync());
        page.WaitForAssertion(() => Assert.Equal($"/agents?tab=providers&providerId={provider.Id:D}", fixture.Location));
        Assert.Equal(2, fixture.Navigation.History.Count);
        Assert.False(fixture.Navigation.History.First().Options.ReplaceHistoryEntry);
    }

    [Fact]
    public async Task A_new_provider_draft_leaves_the_provider_location_and_keeps_its_own_section() {
        await using var fixture = await UrlFixture.CreateAsync(services => services.AddSingleton<IProviderRequestHistory>(new ProviderHistoryUiFixture()));
        var catalog = await fixture.Harness.Context.Services.GetRequiredService<IProviderProfilesReads>().LoadCatalogAsync();
        var provider = Assert.IsType<ProviderProfile>(catalog.Providers.LastOrDefault());
        var opened = $"/agents?tab=providers&providerId={provider.Id:D}&providerSection=prices";
        fixture.Navigation.NavigateTo(opened);
        var page = fixture.RenderPage();
        var panel = page.FindComponent<AgentProviderProfilesPanel>();
        page.WaitForAssertion(() => Assert.Equal(provider.Id, panel.Instance.SelectedProvider?.Id));
        Assert.Equal(ProviderEditorSection.Prices, panel.Instance.State.Section);

        await page.InvokeAsync(() => page.Find("[data-testid='providers-new']").ClickAsync());
        page.WaitForAssertion(() => Assert.Equal("/agents?tab=providers", fixture.Location));
        Assert.Equal(2, fixture.Navigation.History.Count);
        Assert.False(fixture.Navigation.History.First().Options.ReplaceHistoryEntry);
        Assert.Null(panel.Instance.State.ProviderId);
        Assert.Equal(ProviderEditorSection.Connection, panel.Instance.State.Section);

        // The URL of a draft has no provider and so no section: a render of the page keeps the draft's own section.
        await page.InvokeAsync(() => page.Find("[data-testid='provider-editor-tab-runtime']").ClickAsync());
        page.Render(_ => { });
        Assert.Equal(ProviderEditorSection.Runtime, panel.Instance.State.Section);
        Assert.Null(panel.Instance.State.ProviderId);
        Assert.Equal("/agents?tab=providers", fixture.Location);
        Assert.Equal(2, fixture.Navigation.History.Count);

        // Back applies the section its location names.
        fixture.Navigation.NavigateTo(opened);
        page.WaitForAssertion(() => Assert.Equal(provider.Id, panel.Instance.SelectedProvider?.Id));
        Assert.Equal(ProviderEditorSection.Prices, panel.Instance.State.Section);
        Assert.Equal(opened, fixture.Location);
    }

    [Fact]
    public async Task A_tab_click_that_reaches_a_replaced_provider_editor_changes_neither_the_draft_nor_the_url() {
        await using var fixture = await UrlFixture.CreateAsync(services => services.AddSingleton<IProviderRequestHistory>(new ProviderHistoryUiFixture()));
        var catalog = await fixture.Harness.Context.Services.GetRequiredService<IProviderProfilesReads>().LoadCatalogAsync();
        var provider = Assert.IsType<ProviderProfile>(catalog.Providers.LastOrDefault());
        fixture.Navigation.NavigateTo($"/agents?tab=providers&providerId={provider.Id:D}");
        var page = fixture.RenderPage();
        var panel = page.FindComponent<AgentProviderProfilesPanel>();
        page.WaitForAssertion(() => Assert.Equal(provider.Id, panel.Instance.SelectedProvider?.Id));
        var replaced = panel.FindComponents<Tabs>().Single(tabs => tabs.Instance.AriaLabel == "Provider editor tabs").Instance;

        await page.InvokeAsync(() => page.Find("[data-testid='providers-new']").ClickAsync());
        page.WaitForAssertion(() => Assert.Equal("/agents?tab=providers", fixture.Location));

        // The server can still run a click on the tabs New replaced, as Blazor keeps their handlers until the browser
        // confirms the render that removed them.
        await page.InvokeAsync(() => replaced.SelectedIndexChanged.InvokeAsync(ProviderEditorSections.IndexOf(ProviderEditorSection.Runtime)));
        Assert.Null(panel.Instance.State.ProviderId);
        Assert.Equal(ProviderEditorSection.Connection, panel.Instance.State.Section);
        Assert.Equal("/agents?tab=providers", fixture.Location);
        Assert.Equal(2, fixture.Navigation.History.Count);
    }

    [Fact]
    public async Task Request_history_search_pushes_an_anchored_location_that_back_and_forward_restore() {
        var history = new ProviderHistoryUiFixture();
        await using var fixture = await UrlFixture.CreateAsync(services => services.AddSingleton<IProviderRequestHistory>(history));
        fixture.Navigation.NavigateTo("/agents?tab=request-history");
        var page = fixture.RenderPage();
        page.WaitForElement("[data-testid='history-search-form']");
        Assert.Empty(history.Queries);

        await page.InvokeAsync(() => page.Find("[data-testid='history-search-form']").SubmitAsync());
        page.WaitForAssertion(() => Assert.StartsWith("/agents?tab=request-history&range=24h&to=", fixture.Location, StringComparison.Ordinal));
        var searched = fixture.Navigation.Uri;
        var query = Assert.Single(history.Queries);
        Assert.Equal(2, fixture.Navigation.History.Count);
        Assert.False(fixture.Navigation.History.First().Options.ReplaceHistoryEntry);
        Assert.Contains(Uri.EscapeDataString(query.ToUtc.UtcDateTime.ToString(UrlFormats.Instant, CultureInfo.InvariantCulture)), searched, StringComparison.Ordinal);

        fixture.Navigation.NavigateTo("/agents?tab=request-history");
        page.WaitForAssertion(() => Assert.Equal(HistorySearchPhase.NotRequested,
            page.FindComponent<ProviderHistoryResultsSurface>().Instance.Presentation.Phase));
        fixture.Navigation.NavigateTo(searched);
        page.WaitForAssertion(() => Assert.Equal(2, history.Queries.Count));

        Assert.Equal(query.ToUtc, history.Queries[1].ToUtc);
        Assert.Equal(query.FromUtc, history.Queries[1].FromUtc);
        Assert.Equal(4, fixture.Navigation.History.Count);
    }

    [Fact]
    public async Task An_unanchored_history_link_is_anchored_once_with_replace_and_read_once() {
        var history = new ProviderHistoryUiFixture();
        await using var fixture = await UrlFixture.CreateAsync(services => services.AddSingleton<IProviderRequestHistory>(history));
        fixture.Navigation.NavigateTo("/agents?tab=request-history&range=7d&model=Vendor%2FExact");
        var page = fixture.RenderPage();

        page.WaitForAssertion(() => Assert.Single(history.Queries));
        var query = history.Queries[0];
        Assert.Equal("Vendor/Exact", query.Model!.Value.Value);
        Assert.Equal(TimeSpan.FromDays(7), query.ToUtc - query.FromUtc);
        Assert.StartsWith("/agents?tab=request-history&range=7d&to=", fixture.Location, StringComparison.Ordinal);
        Assert.EndsWith("&model=Vendor%2FExact", fixture.Location, StringComparison.Ordinal);
        Assert.Single(fixture.Navigation.History);
        Assert.True(fixture.Navigation.History.First().Options.ReplaceHistoryEntry);
    }

    private static AngleSharp.Dom.IElement FindTab(IRenderedComponent<AgentsHomePage> page, string label)
        => page.FindAll("[data-testid='agents-shell-tabs'] button")
            .Single(tab => tab.TextContent.Trim().StartsWith(label, StringComparison.Ordinal));

    private sealed class UrlFixture : IAsyncDisposable {
        private UrlFixture(ComponentTestHarness harness) {
            Harness = harness;
            Back = (TestNavigationHistory)harness.Context.Services.GetRequiredService<IAppNavigationHistory>();
            Navigation = (BunitNavigationManager)harness.Context.Services.GetRequiredService<NavigationManager>();
            Dialogs = harness.Context.Services.GetRequiredService<DialogService>();
        }

        public ComponentTestHarness Harness { get; }
        public TestNavigationHistory Back { get; }
        public BunitNavigationManager Navigation { get; }
        public DialogService Dialogs { get; }
        public string Location => "/" + Navigation.ToBaseRelativePath(Navigation.Uri);

        public static async Task<UrlFixture> CreateAsync(Action<IServiceCollection>? configure = null) {
            var harness = await ComponentTestHarness.CreateAsync(services => {
                configure?.Invoke(services);
                services.AddScoped<IAppNavigationHistory, TestNavigationHistory>();
            });
            return new(harness);
        }

        public IRenderedComponent<AgentsHomePage> RenderPage() => Harness.Context.Render<AgentsHomePage>();

        public async Task<AgentDefinition> FirstAgentAsync()
            => (await Harness.Context.Services.GetRequiredService<IAgentFrameworkWorkspaceService>().ListAgentsAsync(false)).First();

        public ValueTask DisposeAsync() => Harness.DisposeAsync();
    }

}
