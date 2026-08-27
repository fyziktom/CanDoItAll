using Bunit;
using CanDoItAll.Modules.AgentFramework.Pages.Components;
using CanDoItAll.Modules.AgentFramework.ProviderManagement;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.AgentFramework;

public sealed class ProviderAdministrationLayoutTests {
    [Fact]
    public async Task Toolbar_is_icon_only_and_connections_load_only_when_opened() {
        var service = new RecordingSharedProviderManagementService(
            SharedProviderPublicationPanelTests.CreateLocalState(Guid.NewGuid(), false, true));
        await using var harness = await ComponentTestHarness.CreateAsync(
            services => services.AddSingleton<ISharedProviderManagementService>(service));
        var cut = harness.Context.Render<AgentProviderProfilesPanel>();

        foreach (var id in new[] { "providers-new", "providers-refresh", "providers-connections" }) {
            var button = cut.WaitForElement($"[data-testid='{id}']");
            Assert.False(string.IsNullOrWhiteSpace(button.GetAttribute("aria-label")));
            Assert.Empty(button.QuerySelectorAll(".cda-button__text"));
        }
        Assert.Empty(cut.FindAll("[data-testid='shared-provider-connections-dialog']"));
        Assert.Equal(0, service.ListSourcesCallCount);

        cut.Find("[data-testid='providers-connections']").Click();
        cut.WaitForElement("[data-testid='shared-provider-connections-dialog']");
        Assert.Equal(1, service.ListSourcesCallCount);
        cut.Find("[data-testid='shared-provider-source-add']").Click();
        cut.WaitForElement("[data-testid='shared-provider-source-dialog']");
    }

    [Fact]
    public async Task Publication_settings_do_not_render_or_load_source_connections() {
        var service = new RecordingSharedProviderManagementService(
            SharedProviderPublicationPanelTests.CreateLocalState(Guid.NewGuid(), false, true));
        await using var harness = await ComponentTestHarness.CreateAsync(
            services => services.AddSingleton<ISharedProviderManagementService>(service));
        var cut = harness.Context.Render<SharedProviderManagementPanel>();

        Assert.Contains("No provider selected", cut.Markup);
        Assert.Empty(cut.FindAll("[data-testid='shared-provider-source-add']"));
        Assert.Equal(0, service.ListSourcesCallCount);
    }

    [Fact]
    public async Task Compact_filter_can_be_cleared() {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var cut = harness.Context.Render<AgentProviderProfilesPanel>();

        cut.WaitForElement("[data-testid='providers-search']").Input("no-such-provider");
        cut.WaitForAssertion(() => Assert.StartsWith("0 /", cut.Find("[data-testid='providers-filter-count']").TextContent));
        cut.Find("[data-testid='providers-search-reset']").Click();
        cut.WaitForAssertion(() => Assert.Equal(string.Empty, cut.Find("[data-testid='providers-search']").GetAttribute("value") ?? string.Empty));
    }
}
