using Bunit;
using CanDoItAll.Modules.Plugins.Pages;
using CanDoItAll.Tests.Support;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.Shell;

[Trait("Category", "HostPlatform")]
public sealed class PluginsPageDraftRegressionTests {
    [Fact]
    public async Task Unblurred_input_survives_section_unmount_and_plugin_selection() {
        await using var harness = await ComponentTestHarness.CreateAsync();
        harness.Context.Services.GetRequiredService<NavigationManager>().NavigateTo("/plugins");
        var cut = harness.Context.Render<PluginsPage>();
        cut.WaitForElement("[data-testid='plugins-list-item-office365-mail']");
        await ClickAsync(cut, "plugins-list-item-office365-mail");
        await ClickAsync(cut, "plugins-tab-settings");
        const string selector = "[data-testid='plugin-setting-office365-mail-office365-clientId']";
        await cut.InvokeAsync(() => cut.Find(selector).InputAsync(new ChangeEventArgs { Value = "unfinished client id " }));
        await ClickAsync(cut, "plugins-tab-main");
        await ClickAsync(cut, "plugins-list-item-gmail-mail");
        await ClickAsync(cut, "plugins-list-item-office365-mail");
        await ClickAsync(cut, "plugins-tab-settings");
        Assert.Equal("unfinished client id ", cut.Find(selector).GetAttribute("value"));
    }

    [Fact]
    public async Task Refresh_preserves_dirty_connection_name() {
        await using var harness = await ComponentTestHarness.CreateAsync();
        harness.Context.Services.GetRequiredService<NavigationManager>().NavigateTo("/plugins");
        var cut = harness.Context.Render<PluginsPage>();
        cut.WaitForElement("[data-testid='plugins-list-item-office365-mail']");
        await ClickAsync(cut, "plugins-list-item-office365-mail");
        await ClickAsync(cut, "plugins-tab-settings");
        const string selector = "[data-testid='plugin-connection-name-office365-mail-office365']";
        await cut.InvokeAsync(() => cut.Find(selector).InputAsync(new ChangeEventArgs { Value = "Unsaved account" }));
        await cut.InvokeAsync(() => cut.Find("button[aria-label='Refresh']").ClickAsync(new MouseEventArgs()));
        Assert.Equal("Unsaved account", cut.Find(selector).GetAttribute("value"));
    }

    private static Task ClickAsync(IRenderedComponent<PluginsPage> cut, string id)
        => cut.InvokeAsync(() => cut.Find($"[data-testid='{id}']").ClickAsync(new MouseEventArgs()));
}
