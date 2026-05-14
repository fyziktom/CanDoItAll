using System.Net;
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

    [Fact]
    public async Task Plugins_page_opens_oauth_login_in_new_tab()
    {
        const string clientId = "2f2a235f-7970-477b-93ba-656be29a8d03";

        await using var harness = await ComponentTestHarness.CreateAsync();
        var navigation = harness.Context.Services.GetRequiredService<NavigationManager>();
        var catalogService = harness.Context.Services.GetRequiredService<PluginCatalogService>();
        var settingsService = harness.Context.Services.GetRequiredService<PluginSettingsService>();

        var installResult = await catalogService.InstallAsync(
            Office365PluginConstants.PluginId,
            new PluginInstallRequest(Enable: true, Actor: "component-test"));
        Assert.True(installResult.IsSuccess);

        var grantResult = await settingsService.UpdateGrantAsync(
            Office365PluginConstants.PluginId,
            new PluginGrantUpdateRequest(PluginCapabilityKind.OAuth2, PluginGrantState.Granted),
            "component-test");
        Assert.True(grantResult.IsSuccess);

        var settingsState = new ConfigurationState(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [PluginOAuthConnectionSettingKeys.ClientId] = clientId
        });
        var saveResult = await settingsService.SaveConnectionAsync(
            Office365PluginConstants.PluginId,
            new PluginConnectionSaveRequest(
                Id: null,
                Office365PluginConstants.ConnectionKey,
                "Office365 account",
                settingsState.ToJson(),
                IsEnabled: true),
            "component-test");
        Assert.True(saveResult.IsSuccess);

        navigation.NavigateTo("/plugins");
        var cut = harness.Context.RenderComponent<PluginsPage>();

        cut.WaitForElement("[data-testid='plugins-list-item-office365-mail']");
        cut.Find("[data-testid='plugins-list-item-office365-mail']").Click();
        cut.Find("[data-testid='plugins-tab-connections']").Click();
        cut.WaitForElement("[data-testid='plugin-oauth-login-office365-mail-office365']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains(harness.Context.JSInterop.Invocations, invocation => invocation.Identifier == "open");
        });

        var openInvocation = harness.Context.JSInterop.Invocations.Single(invocation => invocation.Identifier == "open");
        var authorizationUrl = Assert.IsType<string>(openInvocation.Arguments[0]);
        var decodedAuthorizationUrl = WebUtility.UrlDecode(authorizationUrl);

        Assert.Equal("_blank", Assert.IsType<string>(openInvocation.Arguments[1]));
        Assert.Equal("noopener,noreferrer", Assert.IsType<string>(openInvocation.Arguments[2]));
        Assert.Contains("login.microsoftonline.com/common/oauth2/v2.0/authorize", authorizationUrl, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(clientId, decodedAuthorizationUrl, StringComparison.Ordinal);
        Assert.Contains(Office365PluginConstants.OpenIdScope, decodedAuthorizationUrl, StringComparison.Ordinal);
        Assert.Contains("prompt=consent", decodedAuthorizationUrl, StringComparison.Ordinal);
    }
}
