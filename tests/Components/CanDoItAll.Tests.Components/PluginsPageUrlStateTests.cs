using Bunit;
using Bunit.TestDoubles;
using CanDoItAll.Modules.Plugins;
using CanDoItAll.Modules.Plugins.Pages;
using CanDoItAll.Plugins.Abstractions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.Shell;

[Trait("Category", "HostPlatform")]
public sealed class PluginsPageUrlStateTests {
    private static string Office365 => Uri.EscapeDataString(Office365PluginConstants.PluginId.Value);

    [Fact]
    public async Task The_first_plugin_default_is_replaced_and_operator_selection_and_sections_push() {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var navigation = Navigation(harness);
        navigation.NavigateTo("http://localhost/plugins");
        var entries = navigation.History.Count;

        var cut = harness.Context.Render<PluginsPage>();
        cut.WaitForElement("[data-testid='plugins-list-item-office365-mail']");
        cut.WaitForAssertion(() => Assert.StartsWith("http://localhost/plugins?pluginId=", navigation.Uri, StringComparison.Ordinal));
        Assert.Equal(entries, navigation.History.Count);
        Assert.True(navigation.History.First().Options.ReplaceHistoryEntry);
        var defaulted = navigation.Uri;

        await ClickAsync(cut, "plugins-list-item-office365-mail");
        Assert.Equal($"http://localhost/plugins?pluginId={Office365}", navigation.Uri);
        var selected = navigation.History.Count;
        Assert.Equal(defaulted == navigation.Uri ? entries : entries + 1, selected);
        await ClickAsync(cut, "plugins-tab-connections");
        Assert.Equal($"http://localhost/plugins?pluginId={Office365}&section=connections", navigation.Uri);
        await ClickAsync(cut, "plugins-tab-logs");
        await ClickAsync(cut, "plugins-logs-all-filter");
        Assert.Equal($"http://localhost/plugins?pluginId={Office365}&section=logs&logs=all", navigation.Uri);
        Assert.Equal(selected + 3, navigation.History.Count);
        Assert.False(navigation.History.First().Options.ReplaceHistoryEntry);
    }

    [Fact]
    public async Task A_deep_link_selects_the_plugin_and_section_and_a_later_location_is_applied() {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var navigation = Navigation(harness);
        navigation.NavigateTo($"http://localhost/plugins?pluginId={Office365}&section=executors");

        var cut = harness.Context.Render<PluginsPage>();
        cut.WaitForElement("[data-testid='plugin-executor-office365-mail-office365-messages-by-category']");
        Assert.Equal($"http://localhost/plugins?pluginId={Office365}&section=executors", navigation.Uri);

        await cut.InvokeAsync(() => navigation.NavigateTo($"/plugins?pluginId={Office365}&section=settings"));
        cut.WaitForElement("[data-testid='plugin-setting-office365-mail-office365-clientId']");
    }

    [Fact]
    public async Task The_legacy_connections_tab_and_mixed_case_identity_are_canonicalized_once() {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var navigation = Navigation(harness);
        navigation.NavigateTo($"http://localhost/plugins?tab=connections&pluginId={Office365PluginConstants.PluginId.Value.ToUpperInvariant()}");

        var cut = harness.Context.Render<PluginsPage>();
        cut.WaitForElement("[data-testid='plugins-tab-connections']");
        Assert.Equal($"http://localhost/plugins?pluginId={Office365}&section=connections", navigation.Uri);
        Assert.True(navigation.History.First().Options.ReplaceHistoryEntry);
    }

    [Fact]
    public async Task An_OAuth_return_is_reported_once_and_its_one_shot_keys_are_removed_with_replace() {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var navigation = Navigation(harness);
        navigation.NavigateTo($"http://localhost/plugins?pluginId={Office365}&section=connections&oauth=failed&connectionId={Guid.NewGuid():D}&reason=access_denied");
        var entries = navigation.History.Count;

        var cut = harness.Context.Render<PluginsPage>();
        cut.WaitForAssertion(() => Assert.Contains("OAuth sign-in failed (access_denied)", cut.Markup, StringComparison.Ordinal));
        Assert.Equal($"http://localhost/plugins?pluginId={Office365}&section=connections", navigation.Uri);
        Assert.Equal(entries, navigation.History.Count);
    }

    [Fact]
    public async Task The_packages_dialog_is_a_route_driven_overlay() {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var navigation = Navigation(harness);
        navigation.NavigateTo($"http://localhost/plugins?pluginId={Office365}");
        var cut = harness.Context.Render<PluginsPage>();
        cut.WaitForElement("[data-testid='plugins-list-item-office365-mail']");
        var entries = navigation.History.Count;

        await ClickAsync(cut, "plugin-packages-open");
        cut.WaitForElement("[data-testid='plugin-package-upload']");
        Assert.Equal($"http://localhost/plugins?pluginId={Office365}&dialog=packages", navigation.Uri);
        Assert.Equal(entries + 1, navigation.History.Count);
        Assert.Equal($"cda-nav-parent:/plugins?pluginId={Office365}", navigation.History.First().Options.HistoryEntryState);

        await cut.InvokeAsync(() => navigation.NavigateTo($"/plugins?pluginId={Office365}"));
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("[data-testid='plugin-package-upload']")));

        await cut.InvokeAsync(() => navigation.NavigateTo($"/plugins?pluginId={Office365}&dialog=packages"));
        cut.WaitForElement("[data-testid='plugin-package-upload']");
    }

    private static BunitNavigationManager Navigation(ComponentTestHarness harness)
        => Assert.IsType<BunitNavigationManager>(harness.Context.Services.GetRequiredService<NavigationManager>());

    private static Task ClickAsync(IRenderedComponent<PluginsPage> cut, string id)
        => cut.InvokeAsync(() => cut.Find($"[data-testid='{id}']").ClickAsync(new MouseEventArgs()));
}
