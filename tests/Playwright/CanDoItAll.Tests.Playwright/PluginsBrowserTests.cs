using System.IO.Compression;
using System.Text.Json;
using CanDoItAll.Modules.Plugins;
using CanDoItAll.Plugins.Abstractions;
using CanDoItAll.Tests.Playwright.Plugins;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright.Smoke;

[Collection(PlaywrightCollection.Name)]
[Trait("Category", "Playwright")]
[Trait("Category", "HostPlatform")]
public sealed class PluginsBrowserTests(PlaywrightAppFixture fixture) {
    private const string ClientId = "ff43ae89-4a62-4184-bcbb-25f86a344290";
    private const string NameInput = "plugin-connection-name-office365-mail-office365";
    private const string ClientInput = "plugin-setting-office365-mail-office365-clientId";
    private const string SaveButton = "plugin-connection-save-office365-mail-office365";
    private const string Draft = "plugin-draft-office365-mail-office365";
    private static readonly string Artifacts = Path.Combine(PlaywrightTestHostPaths.RepositoryRoot, "output", "playwright", "plugins-ui");

    [Fact]
    public async Task Production_real_owners_preserve_drafts_receipts_effects_upload_and_observable_restart() {
        Directory.CreateDirectory(Artifacts);
        await using var host = new PluginsBrowserHost(sandbox: false);
        try {
            await host.ReadyAsync(sandbox: false);
            await using var provider = await TestApplicationBootstrap.BuildServiceProviderAsync(host.Profile!, "Plugins.Browser.Readback",
                TestSchemaBootstrapModules.Full, new Dictionary<string, string?> { ["PluginPackages:RootPath"] = host.PackageRoot });
            await using var scope = provider.CreateAsyncScope();
            var services = scope.ServiceProvider;
            var connections = services.GetRequiredService<PluginConnectionStore>();
            await using var context = await fixture.Browser.NewContextAsync(new() { ViewportSize = new() { Width = 1600, Height = 1000 } });
            await context.AddInitScriptAsync("window.pluginPopupCount = 0; window.open = (url, target, features) => { window.pluginPopupCount++; window.pluginPopupSafe = new URL(url).hostname === 'login.microsoftonline.com' && target === '_blank' && features === 'noopener,noreferrer'; return null; };");
            var page = await context.NewPageAsync();
            var errors = Observe(page);
            await page.GotoAsync(host.BaseUrl + "/plugins");
            await PlaywrightAppFixture.CompleteDatabaseStartupAsync(page);
            await page.GetByTestId("plugins-list-item-office365-mail").ClickAsync();
            foreach (var section in new[] { "main", "executors", "settings", "connections", "logs", "grants" }) {
                await TabAsync(page, section);
            }
            await TabAsync(page, "settings");
            await page.GetByTestId(ClientInput).FillAsync(ClientId);
            await page.GetByTestId(NameInput).FillAsync("Submitted browser account");
            await host.CommandAsync(PluginsProbeCommand.HoldReadback);
            await page.GetByTestId(SaveButton).ClickAsync();
            await host.ObserveAsync(PluginsProbeProtocol.ReadHeld);
            await Assertions.Expect(page.GetByTestId(Draft)).ToHaveAttributeAsync("data-status", "Pending");
            var id = Guid.Parse((await page.GetByTestId(Draft).GetAttributeAsync("data-connection-id"))!);
            await page.GetByTestId(NameInput).FillAsync("Newer unblurred browser input");
            await host.CommandAsync(PluginsProbeCommand.Release);
            await StateAsync(page, "Saved");
            await Assertions.Expect(page.GetByTestId(NameInput)).ToBeFocusedAsync();
            await Assertions.Expect(page.GetByTestId(NameInput)).ToHaveValueAsync("Newer unblurred browser input");
            Assert.Equal("Submitted browser account", (await connections.FindAsync(Office365PluginConstants.PluginId, new(id)))!.DisplayName);
            await ScreenshotAsync(page, "production-settings-held-readback");
            await host.CommandAsync(PluginsProbeCommand.FailReadback);
            await page.GetByTestId(SaveButton).ClickAsync();
            await StateAsync(page, "SavedWithWarning");
            await ScreenshotAsync(page, "production-warning");
            await page.GetByTestId("plugins-refresh").ClickAsync();
            await Assertions.Expect(page.GetByTestId("plugins-read-settings")).ToHaveCountAsync(0);
            var saved = Assert.Single(await connections.ListAsync(Office365PluginConstants.PluginId));
            Assert.Equal(id, saved.Id.Value);
            Assert.Equal("Newer unblurred browser input", saved.DisplayName);
            await page.GetByTestId(NameInput).FillAsync("Draft retained across grant");
            await TabAsync(page, "grants");
            var grant = page.GetByTestId("plugin-grant-row").Filter(new() { HasText = "OAuth2" });
            await grant.GetByRole(AriaRole.Button, new() { Name = "Grant", Exact = true }).ClickAsync();
            await Assertions.Expect(grant).ToContainTextAsync("Granted");
            Assert.Contains(await services.GetRequiredService<PluginGrantStore>().ListAsync(Office365PluginConstants.PluginId),
                item => item.Capability == PluginCapabilityKind.OAuth2 && item.State == PluginGrantState.Granted);
            await ScreenshotAsync(page, "production-grants");
            var install = page.GetByRole(AriaRole.Button, new() { Name = "Install and enable", Exact = true });
            if (await install.CountAsync() != 0) {
                await install.ClickAsync();
            }
            await page.GetByRole(AriaRole.Button, new() { Name = "Disable", Exact = true }).ClickAsync();
            await page.GetByRole(AriaRole.Button, new() { Name = "Enable", Exact = true }).ClickAsync();
            await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Disable", Exact = true })).ToBeEnabledAsync();
            Assert.True((await services.GetRequiredService<PluginInstallationStore>().FindAsync(Office365PluginConstants.PluginId))!.IsEnabled);
            await TabAsync(page, "settings");
            await Assertions.Expect(page.GetByTestId(NameInput)).ToHaveValueAsync("Draft retained across grant");
            await page.GetByTestId(SaveButton).ClickAsync();
            await StateAsync(page, "Saved");
            await TabAsync(page, "connections");
            await page.GetByTestId("plugin-oauth-login-office365-mail-office365").ClickAsync();
            await page.WaitForFunctionAsync("window.pluginPopupCount === 1");
            Assert.True(await page.EvaluateAsync<bool>("window.pluginPopupSafe"));
            await page.GetByTestId("plugins-refresh").ClickAsync();
            await TabAsync(page, "settings");
            await Assertions.Expect(page.GetByTestId(Draft)).ToHaveAttributeAsync("data-connection-id", id.ToString());
            Assert.Equal(1, await page.EvaluateAsync<int>("window.pluginPopupCount"));
            await using (var database = await services.GetRequiredService<IDbContextFactory<PluginsDbContext>>().CreateDbContextAsync()) {
                Assert.Equal(1, await database.Set<PluginOAuthSessionRecord>().CountAsync(item => item.ConnectionId == id));
            }
            await TabAsync(page, "logs");
            await page.GetByTestId("plugins-logs-all-filter").ClickAsync();
            await Assertions.Expect(page.GetByText("All plugins", new() { Exact = true }).Last).ToBeVisibleAsync();
            await page.GetByTestId("plugins-logs-selected-filter").ClickAsync();
            await page.GetByTestId("plugin-packages-open").ClickAsync();
            await page.GetByTestId("plugin-package-upload").SetInputFilesAsync(new FilePayload {
                Name = "controlled-package.zip", MimeType = "application/zip", Buffer = Archive()
            });
            await Assertions.Expect(page.GetByText("Uploaded package. Restart is required when shown below.", new() { Exact = true })).ToBeVisibleAsync();
            Assert.NotNull(await services.GetRequiredService<PluginInstallationStore>().FindAsync(new("browser.ui.fixture")));
            Assert.Empty(Directory.EnumerateFiles(Path.Combine(host.PackageRoot!, "state", "uploads")));
            await ScreenshotAsync(page, "production-package-dialog");
            await page.GetByRole(AriaRole.Button, new() { Name = "Close", Exact = true }).Last.ClickAsync();
            await page.GetByTestId("plugin-packages-open").ClickAsync();
            await Assertions.Expect(page.GetByTestId("plugin-package-upload")).ToBeEnabledAsync();
            await page.GetByRole(AriaRole.Button, new() { Name = "Close", Exact = true }).Last.ClickAsync();
            using (var client = new HttpClient()) {
                var icon = await client.GetAsync(host.BaseUrl + "/api/plugins/packages/browser.ui.package/icon");
                Assert.True(icon.IsSuccessStatusCode);
            }
            await ScreenshotAsync(page, "production-restart-required");
            Assert.False(await page.EvaluateAsync<bool>("document.documentElement.scrollWidth > innerWidth"));
            Assert.Empty(errors);
            await page.GetByTestId("plugin-runtime-restart").ClickAsync();
            await host.ObserveAsync(PluginsProbeProtocol.Stopping);
            Assert.True((await services.GetRequiredService<PluginRuntimeRestartService>().GetStatusAsync()).IsRestartRequested);
        } finally {
            await host.DisposeAsync();
            await File.WriteAllTextAsync(Path.Combine(Artifacts, "production-server.log"), host.Logs);
            Assert.DoesNotContain("ObjectDisposedException", host.Logs, StringComparison.Ordinal);
            Assert.DoesNotContain("Unhandled exception", host.Logs, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task Sandbox_real_controls_scenarios_busy_drafts_recovery_upload_and_assets_need_no_database() {
        Directory.CreateDirectory(Artifacts);
        await using var host = new PluginsBrowserHost(sandbox: true);
        try {
            await host.ReadyAsync(sandbox: true);
            await using var context = await fixture.Browser.NewContextAsync(new() { ViewportSize = new() { Width = 1600, Height = 1000 } });
            var page = await context.NewPageAsync();
            var errors = Observe(page);
            await page.GotoAsync(host.BaseUrl);
            await page.GetByTestId("plugins-list-item-office365-mail").First.ClickAsync();
            await page.WaitForFunctionAsync("[...document.images].some(i => i.src.includes('/icons/plugin.svg') && i.complete && i.naturalWidth > 0)");
            foreach (var section in new[] { "main", "executors", "settings", "connections", "logs", "grants" }) {
                await TabAsync(page, section);
            }
            await ScenarioAsync(page, "HeldGrant");
            await TabAsync(page, "grants");
            var row = page.GetByTestId("plugin-grant-row").Filter(new() { HasText = "OAuth2" });
            await row.GetByRole(AriaRole.Button, new() { Name = "Deny", Exact = true }).ClickAsync();
            await Assertions.Expect(row.GetByRole(AriaRole.Button).First).ToHaveAttributeAsync("aria-busy", "true");
            await ScreenshotAsync(page, "sandbox-busy-grants");
            await page.GetByTestId("plugins-release").ClickAsync();
            await Assertions.Expect(page.GetByTestId("plugins-store-counts")).ToHaveAttributeAsync("data-grants", "1");
            await ScenarioAsync(page, "HeldReadback");
            await TabAsync(page, "settings");
            await page.GetByTestId(ClientInput).FillAsync(ClientId);
            await page.GetByTestId(SaveButton).ClickAsync();
            await Assertions.Expect(page.GetByTestId("plugins-store-counts")).ToHaveAttributeAsync("data-saves", "1");
            await StateAsync(page, "Pending");
            var id = await page.GetByTestId(Draft).GetAttributeAsync("data-connection-id");
            Assert.True(Guid.TryParse(id, out _));
            await page.GetByTestId(NameInput).FillAsync("New sandbox text before blur");
            await page.GetByTestId("plugins-release").DispatchEventAsync("click");
            await StateAsync(page, "Saved");
            await Assertions.Expect(page.GetByTestId(NameInput)).ToBeFocusedAsync();
            await TabAsync(page, "connections");
            await TabAsync(page, "settings");
            await Assertions.Expect(page.GetByTestId(NameInput)).ToHaveValueAsync("New sandbox text before blur");
            await Assertions.Expect(page.GetByTestId(Draft)).ToHaveAttributeAsync("data-connection-id", id!);
            await ScreenshotAsync(page, "sandbox-settings");
            await ScenarioAsync(page, "UnknownSave");
            await TabAsync(page, "settings");
            await page.GetByTestId(ClientInput).FillAsync(ClientId);
            await page.GetByTestId(SaveButton).ClickAsync();
            await StateAsync(page, "Unknown");
            await page.GetByTestId("plugins-refresh").ClickAsync();
            await Assertions.Expect(page.GetByTestId(SaveButton)).ToBeDisabledAsync();
            await ScreenshotAsync(page, "sandbox-unknown-recovery");
            await page.GetByLabel("I reviewed stored connections.", new() { Exact = false }).CheckAsync();
            var savedId = await page.GetByTestId("plugin-connection-target").Locator("option").Last.GetAttributeAsync("value");
            await page.GetByTestId("plugin-connection-target").SelectOptionAsync(savedId!);
            await Assertions.Expect(page.GetByTestId(Draft)).ToHaveAttributeAsync("data-connection-id", savedId!);
            await Assertions.Expect(page.GetByTestId("plugins-store-counts")).ToHaveAttributeAsync("data-saves", "1");
            await ScenarioAsync(page, "HeldUpload");
            await page.GetByTestId("plugin-packages-open").ClickAsync();
            await page.GetByTestId("plugin-package-upload").SetInputFilesAsync(new FilePayload { Name = "harmless.zip", MimeType = "application/zip", Buffer = [1, 2, 3] });
            await Assertions.Expect(page.GetByTestId("plugin-package-upload")).ToBeDisabledAsync();
            await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Close", Exact = true }).Last).ToBeDisabledAsync();
            await page.GetByRole(AriaRole.Button, new() { Name = "Close", Exact = true }).First.ClickAsync();
            await page.Keyboard.PressAsync("Escape");
            await Assertions.Expect(page.GetByTestId("plugin-package-upload")).ToBeVisibleAsync();
            await ScreenshotAsync(page, "sandbox-upload-held");
            await page.GetByTestId("plugins-release").DispatchEventAsync("click");
            await Assertions.Expect(page.GetByTestId("plugins-store-counts")).ToHaveAttributeAsync("data-packages", "1");
            await page.GetByRole(AriaRole.Button, new() { Name = "Close", Exact = true }).Last.ClickAsync();
            await page.GetByTestId("plugin-packages-open").ClickAsync();
            await Assertions.Expect(page.GetByText("Installed", new() { Exact = true })).ToBeVisibleAsync();
            await page.GetByRole(AriaRole.Button, new() { Name = "Close", Exact = true }).Last.ClickAsync();
            await page.GetByTestId("plugin-runtime-restart").ClickAsync();
            await Assertions.Expect(page.GetByTestId("plugins-store-counts")).ToHaveAttributeAsync("data-restarts", "1");
            await ScreenshotAsync(page, "sandbox-restart-recorded");
            foreach (var (scenario, text) in new[] { ("Empty", "No plugins are registered"), ("Large", "100 catalog"),
                ("PartialReads", "Unavailable. Refresh to retry this read."), ("MissingReferences", "references"),
                ("OAuthConnected", "Connected"), ("OAuthReconnect", "ReconnectRequired"), ("OAuthError", "Authorization failed.") }) {
                await ScenarioAsync(page, scenario);
                await TabIfPresentAsync(page, scenario.StartsWith("OAuth", StringComparison.Ordinal) ? "connections" : "settings");
                await Assertions.Expect(page.Locator("body")).ToContainTextAsync(text);
            }
            await ScenarioAsync(page, "StaleReads");
            await page.GetByTestId("plugins-refresh").ClickAsync();
            await Assertions.Expect(page.GetByTestId("plugins-read-catalog")).ToContainTextAsync("stale");
            await ScenarioAsync(page, "Normal");
            await page.GetByTestId("plugins-list-item-sandbox-plugin-1").First.ClickAsync();
            await TabAsync(page, "settings");
            await page.GetByTestId("plugin-setting-sandbox-plugin-1-example-Number").FillAsync("-");
            await page.GetByTestId("plugin-setting-sandbox-plugin-1-example-Json").FillAsync("{");
            await TabAsync(page, "main");
            await TabAsync(page, "settings");
            await Assertions.Expect(page.GetByTestId("plugin-setting-sandbox-plugin-1-example-Number")).ToHaveValueAsync("-");
            await Assertions.Expect(page.GetByTestId("plugin-setting-sandbox-plugin-1-example-Json")).ToHaveValueAsync("{");
            await ScreenshotAsync(page, "sandbox-invalid-fields");
            Assert.False(await page.EvaluateAsync<bool>("document.documentElement.scrollWidth > innerWidth"));
            Assert.True(await page.EvaluateAsync<bool>("[...document.styleSheets].some(s => s.href?.includes('CanDoItAll.Components.BaseLib'))"));
            await page.WaitForFunctionAsync("[...document.images].some(i => i.src.includes('/api/plugins/packages/') && i.complete && i.naturalWidth > 0)");
            Assert.Empty(errors);
        } finally {
            await host.DisposeAsync();
            await File.WriteAllTextAsync(Path.Combine(Artifacts, "sandbox-server.log"), host.Logs);
            Assert.DoesNotContain("Unhandled exception", host.Logs, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static Task TabAsync(IPage page, string section) => page.GetByTestId("plugins-tab-" + section).ClickAsync();
    private static async Task TabIfPresentAsync(IPage page, string section) {
        if (await page.GetByTestId("plugins-tab-" + section).CountAsync() > 0) {
            await TabAsync(page, section);
        }
    }
    private static async Task ScenarioAsync(IPage page, string scenario) {
        await page.GetByTestId("plugins-scenario").SelectOptionAsync(scenario);
        await Assertions.Expect(page.GetByTestId("plugins-store-counts")).ToHaveAttributeAsync("data-scenario", scenario);
    }
    private static Task StateAsync(IPage page, string state) => Assertions.Expect(page.GetByTestId(Draft)).ToHaveAttributeAsync("data-status", state);
    private static Task ScreenshotAsync(IPage page, string name) => page.ScreenshotAsync(new() { Path = Path.Combine(Artifacts, name + ".png") });
    private static List<string> Observe(IPage page) {
        var errors = new List<string>();
        page.PageError += (_, error) => errors.Add(error);
        page.Console += (_, message) => {
            if (message.Type == "error") {
                errors.Add(message.Text);
            }
        };
        page.Response += (_, response) => {
            if (response.Status >= 400 && response.Request.ResourceType is "script" or "stylesheet" or "font") {
                errors.Add($"Asset {response.Status}: {response.Url}");
            }
        };
        return errors;
    }
    private static byte[] Archive() {
        var manifest = new PluginPackageManifest {
            Plugin = new(new("browser.ui.fixture"), "Browser UI fixture", "Harmless owned archive", "1.0.0", "Tests",
                PluginSourceKind.LocalPackage, PluginTrustLevel.LocalPackage, "1.0.0", PluginCapabilityKind.None, [],
                PluginSettingsDescriptor.Empty, [], new(new("browser.ui.package"), "1.0.0", "1.0.0", "", "")),
            IconPath = "icon.svg", RequiresRestart = true
        };
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true)) {
            using (var entry = zip.CreateEntry(PluginPackageManifestStore.ManifestFileName).Open()) {
                JsonSerializer.Serialize(entry, manifest, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            }
            using var icon = new StreamWriter(zip.CreateEntry("icon.svg").Open());
            icon.Write("<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 16 16\"><path d=\"M1 1h14v14H1z\"/></svg>");
        }
        return stream.ToArray();
    }
}
