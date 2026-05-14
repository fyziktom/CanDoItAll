using Bunit;
using CanDoItAll.Modules.Plugins;
using CanDoItAll.Modules.Plugins.Pages;
using CanDoItAll.Plugins.Abstractions;
using CanDoItAll.SharedKernel.Configuration;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components;

public sealed class PluginsPageTests
{
    [Fact]
    public async Task Plugins_page_lists_plugins_and_saves_connection_settings()
    {
        const string clientId = "2f2a235f-7970-477b-93ba-656be29a8d03";

        await using var harness = await ComponentTestHarness.CreateAsync();
        var navigation = harness.Context.Services.GetRequiredService<NavigationManager>();
        var settingsService = harness.Context.Services.GetRequiredService<PluginSettingsService>();

        navigation.NavigateTo("/plugins");
        var cut = harness.Context.RenderComponent<PluginsPage>();

        cut.WaitForElement("[data-testid='plugins-list-item-office365-mail']");
        cut.Find("[data-testid='plugins-list-item-office365-mail']").Click();
        cut.Find("[data-testid='plugins-tab-settings']").Click();
        cut.WaitForElement("[data-testid='plugin-setting-office365-mail-office365-clientId']");

        cut.Find("[data-testid='plugin-setting-office365-mail-office365-clientId']").Change(clientId);
        cut.Find("[data-testid='plugin-connection-save-office365-mail-office365']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Saved Office365 account settings", cut.Markup, StringComparison.Ordinal);
        });

        var settings = await settingsService.GetSettingsAsync(Office365PluginConstants.PluginId);
        Assert.NotNull(settings);
        var connection = Assert.Single(settings!.Connections, item => item.ConnectionKey == Office365PluginConstants.ConnectionKey);
        var state = ConfigurationState.FromJson(connection.SettingsJson);

        Assert.Equal(clientId, state.GetText(PluginOAuthConnectionSettingKeys.ClientId));
    }
}
