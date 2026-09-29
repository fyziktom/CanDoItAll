using System.Text.RegularExpressions;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Modules.Security;
using CanDoItAll.Modules.Workspace;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright.Smoke;

[CollectionDefinition(Name)]
public sealed class WorkspaceSettingsBrowserCollection : ICollectionFixture<PlaywrightAppFixture> {
    public const string Name = "Workspace Settings owned browser host";
}

[Collection(WorkspaceSettingsBrowserCollection.Name)]
[Trait("Category", "Playwright")]
[Trait("Category", "HostPlatform")]
public sealed class WorkspaceSettingsBrowserTests(PlaywrightAppFixture fixture) {
    private static readonly string Artifacts = Path.Combine(PlaywrightTestHostPaths.RepositoryRoot, "output", "playwright", "workspace-settings-core-ui");

    [Fact]
    public async Task Production_settings_use_real_owners_and_keep_deferred_hosts_reachable() {
        Directory.CreateDirectory(Artifacts);
        await using var provider = await TestApplicationBootstrap.BuildServiceProviderAsync(fixture.OwnedDatabaseProfile,
            "Workspace.Settings.Browser", TestSchemaBootstrapModules.Full,
            new Dictionary<string, string?> { [LocalRuntimeHostedWorkerPolicy.LaneKindConfigurationKey] = LocalRuntimeHostedWorkerPolicy.McpToolHostLaneKind });
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var workspace = services.GetRequiredService<WorkspaceService>();
        var secrets = services.GetRequiredService<SecretService>();
        var preferences = services.GetRequiredService<IFileApplicationPreferenceService>();
        var executable = Path.Combine(fixture.OwnedDatabaseProfile.WorkspaceRootPath, "never-execute-workspace-fixture.exe");
        await File.WriteAllTextAsync(executable, "Harmless fixture; never execute.");
        await using var context = await fixture.Browser.NewContextAsync(new() {
            ViewportSize = new() { Width = 1600, Height = 1000 }, Permissions = ["clipboard-read", "clipboard-write"]
        });
        var page = await context.NewPageAsync();
        var errors = Observe(page);
        var secretName = "Workspace browser secret " + Guid.NewGuid().ToString("N")[..8];
        var value = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(20));
        try {
            await page.GotoAsync(fixture.BaseUrl + "/settings");
            await PlaywrightAppFixture.CompleteDatabaseStartupAsync(page);
            await page.GetByTestId("defaults-name").FillAsync(" Browser saved workspace ");
            await page.GetByTestId("defaults-currency").FillAsync("eur");
            await page.GetByTestId("defaults-culture").FillAsync("de-DE");
            await page.GetByTestId("defaults-notes").FillAsync("Owned production proof");
            await page.GetByTestId("defaults-save").EvaluateAsync("element => element.click()");
            await Assertions.Expect(page.GetByTestId("settings-operation").First).ToContainTextAsync("Committed");
            var saved = await workspace.GetSettingsAsync();
            Assert.Equal("Browser saved workspace", saved.WorkspaceName);
            Assert.Equal("EUR", saved.CurrencyCode);
            Assert.Equal("Owned production proof", saved.Notes);
            await Shot(page, "production-defaults");

            await Tab(page, "Secrets");
            await page.GetByTestId("secret-name").FillAsync(secretName);
            await page.GetByTestId("settings-secret-value").FillAsync(value);
            await page.GetByTestId("secret-metadatajson").FillAsync("{\"synthetic\":true}");
            await page.GetByTestId("secret-save").EvaluateAsync("element => element.click()");
            await Assertions.Expect(page.GetByTestId("settings-operation").First).ToContainTextAsync("Committed");
            var metadata = Assert.Single(await secrets.ListForPickerAsync(), item => item.Name == secretName);
            Assert.True((await secrets.GetAsync(metadata.Id))!.SecretValue == value);
            await page.Locator(".cda-selection-list-item__button").Filter(new() { HasText = secretName }).ClickAsync();
            await Assertions.Expect(page.GetByTestId("settings-secret-value")).ToHaveAttributeAsync("type", "password");
            var copyValue = page.Locator(".cda-secret-field").GetByRole(AriaRole.Button, new() { Name = "Copy value", Exact = true });
            await copyValue.ClickAsync();
            await Assertions.Expect(copyValue).ToHaveAttributeAsync("data-copy-state", "copied");
            Assert.True(await page.EvaluateAsync<string>("navigator.clipboard.readText()") == value);
            await page.Locator(".cda-secret-field__reveal").ClickAsync();
            await Assertions.Expect(page.GetByTestId("settings-secret-value")).ToHaveAttributeAsync("type", "text");
            await Tab(page, "Files");
            await Tab(page, "Secrets");
            await Assertions.Expect(page.GetByTestId("settings-secret-value")).ToHaveAttributeAsync("type", "password");
            await Shot(page, "production-secret-masked");
            await page.GetByTestId("secret-delete").ClickAsync();
            await Assertions.Expect(page.GetByTestId("settings-operation")).ToHaveCountAsync(2);
            await Assertions.Expect(page.GetByTestId("settings-operation").First).ToContainTextAsync("Committed");
            Assert.Null(await secrets.GetAsync(metadata.Id));

            await Tab(page, "Files");
            await page.GetByTestId("file-application-extension").FillAsync(" .OWNED ");
            await page.GetByTestId("file-application-executable").FillAsync(executable);
            await page.GetByTestId("file-application-save").EvaluateAsync("element => element.click()");
            await Assertions.Expect(page.GetByTestId("settings-operation").First).ToContainTextAsync("Committed");
            var preference = Assert.Single(await preferences.ListAsync(), item => item.Extension.Value == ".owned");
            Assert.False(preference.RequiresRebind);
            Assert.Equal(executable, preference.ExecutablePath);
            await Assertions.Expect(page.GetByText("All files use the system default", new() { Exact = true })).ToHaveCountAsync(0);
            await Shot(page, "production-files");
            await page.GetByTestId("file-application-delete").ClickAsync();
            await Assertions.Expect(page.GetByTestId("settings-operation")).ToHaveCountAsync(2);
            await Assertions.Expect(page.GetByTestId("settings-operation").First).ToContainTextAsync("Committed");
            Assert.DoesNotContain(await preferences.ListAsync(), item => item.Extension.Value == ".owned");

            await Tab(page, "Provider history");
            await Assertions.Expect(page.GetByText("Policy not requested", new() { Exact = true })).ToBeVisibleAsync();
            await page.GetByTestId("history-policy-load").ClickAsync();
            await page.GetByTestId("history-policy-metadata-days").FillAsync("20");
            await page.GetByTestId("history-policy-apply").EvaluateAsync("element => element.click()");
            await Assertions.Expect(page.GetByTestId("history-policy-success")).ToContainTextAsync("Existing expiry dates are unchanged");
            await page.GetByTestId("history-policy-preview").ClickAsync();
            await Assertions.Expect(page.GetByTestId("history-policy-confirmation")).ToBeVisibleAsync();
            Assert.True(await page.GetByTestId("history-policy-confirmation").EvaluateAsync<bool>("e => { const r=e.getBoundingClientRect(); return r.top >= 0 && r.bottom <= innerHeight; }"));
            await Shot(page, "production-history-preview");
            await page.GetByTestId("history-policy-confirm").ClickAsync();
            await Assertions.Expect(page.GetByTestId("history-policy-success")).ToContainTextAsync("shorter retention applied");
            await page.ReloadAsync();
            await ShellReady(page);
            await page.GetByTestId("history-policy-load").ClickAsync();
            await Assertions.Expect(page.GetByTestId("history-policy-metadata-days")).ToHaveValueAsync("20");
            await Shot(page, "production-history");

            await Tab(page, "Data Sources");
            await Assertions.Expect(page.GetByText("Workspace defaults", new() { Exact = true })).ToHaveCountAsync(0);
            await Assertions.Expect(page.GetByTestId("database-data-sources-summary")).ToBeVisibleAsync();
            await Shot(page, "production-data-sources");
            await Tab(page, "Storage");
            await Assertions.Expect(page.GetByText("Storage catalog", new() { Exact = true }).First).ToBeVisibleAsync();
            await Shot(page, "production-storage");
            await Tab(page, "API Access");
            await Assertions.Expect(page.GetByText("Bearer tokens are not required.", new() { Exact = true })).ToBeVisibleAsync();
            await Shot(page, "production-api-access");
            await page.GotoAsync(fixture.BaseUrl + "/settings?tab=invalid");
            await ShellReady(page);
            await Assertions.Expect(page.GetByTestId("defaults-name")).ToHaveValueAsync("Browser saved workspace");
            await page.GotoAsync(fixture.BaseUrl + "/settings?tab=files");
            await ShellReady(page);
            await Assertions.Expect(page.GetByTestId("file-application-extension")).ToBeVisibleAsync();
            await page.GoBackAsync();
            await ShellReady(page);
            await Assertions.Expect(page.GetByTestId("defaults-name")).ToHaveValueAsync("Browser saved workspace");
            await page.GoForwardAsync();
            await ShellReady(page);
            await Assertions.Expect(page.GetByTestId("file-application-extension")).ToBeVisibleAsync();
            await Tab(page, "Providers");
            await Assertions.Expect(page).ToHaveURLAsync(new Regex("/agents\\?tab=providers$"));
            Assert.Empty(errors);
        } finally {
            File.Delete(executable);
        }
    }

    [Fact]
    public async Task Standalone_sandbox_preserves_focus_and_uses_real_secret_and_policy_controls() {
        Directory.CreateDirectory(Artifacts);
        await using var host = new WorkspaceSandboxHost();
        await host.ReadyAsync();
        await using var context = await fixture.Browser.NewContextAsync(new() { ViewportSize = new() { Width = 1600, Height = 1000 } });
        var page = await context.NewPageAsync();
        var errors = Observe(page);
        await page.GotoAsync(host.BaseUrl);
        var name = page.GetByTestId("defaults-name");
        await Assertions.Expect(name).ToHaveValueAsync("Scenario workspace");
        await page.GetByLabel("Operation", new() { Exact = true }).SelectOptionAsync("ProvidersRead");
        await page.GetByRole(AriaRole.Button, new() { Name = "Hold next operation", Exact = true }).ClickAsync();
        await page.GetByTestId("defaults-refresh").ClickAsync();
        await name.FillAsync("Unblurred sandbox input");
        await name.EvaluateAsync("element => element.setSelectionRange(6,6)");
        await page.GetByRole(AriaRole.Button, new() { Name = "Release operation", Exact = true }).EvaluateAsync("element => element.click()");
        await Assertions.Expect(page.GetByTestId("defaults-refresh")).ToBeEnabledAsync();
        await Assertions.Expect(name).ToBeFocusedAsync();
        Assert.Equal(6, await name.EvaluateAsync<int>("element => element.selectionStart"));
        await Shot(page, "sandbox-defaults");
        await Tab(page, "Secrets");
        await page.Locator(".cda-selection-list-item__button").ClickAsync();
        await page.Locator(".cda-secret-field__reveal").ClickAsync();
        await Assertions.Expect(page.GetByTestId("settings-secret-value")).ToHaveAttributeAsync("type", "text");
        await Assertions.Expect(page.GetByTestId("settings-secret-value")).ToHaveAttributeAsync("type", "password", new() { Timeout = 35000 });
        await Shot(page, "sandbox-secret-masked");
        await Tab(page, "Files");
        await Shot(page, "sandbox-files");
        await Tab(page, "Provider history");
        await Assertions.Expect(page.GetByTestId("scenario-reads")).ToContainTextAsync("Policy reads: 0");
        await page.GetByTestId("history-policy-load").ClickAsync();
        await page.GetByTestId("history-policy-preview").ClickAsync();
        await Assertions.Expect(page.GetByTestId("history-policy-confirmation")).ToBeVisibleAsync();
        await Shot(page, "sandbox-history-preview");
        await page.GetByRole(AriaRole.Button, new() { Name = "Cancel", Exact = true }).ClickAsync();
        await Assertions.Expect(page.GetByTestId("history-policy-confirmation")).ToHaveCountAsync(0);
        await page.GetByTestId("history-policy-batch").FillAsync("1e-");
        await Assertions.Expect(page.Locator(".validation-errors")).ToContainTextAsync("number");
        await page.GetByTestId("history-policy-apply").EvaluateAsync("element => element.click()");
        await Assertions.Expect(page.GetByTestId("scenario-effects")).ToContainTextAsync("Current store writes: 0");
        await Assertions.Expect(page.GetByTestId("history-policy-batch")).ToHaveValueAsync("1e-");
        Assert.Empty(errors);
    }

    private static Task Tab(IPage page, string name) => page.GetByRole(AriaRole.Button,
        new() { NameRegex = new Regex("^" + Regex.Escape(name)) }).ClickAsync();
    private static async Task ShellReady(IPage page) {
        await PlaywrightAppFixture.CompleteDatabaseStartupAsync(page);
        await page.WaitForFunctionAsync("() => typeof databaseSwitchStorageListener === 'function'");
    }
    private static Task Shot(IPage page, string name) => page.ScreenshotAsync(new() { Path = Path.Combine(Artifacts, name + ".png") });
    private static List<string> Observe(IPage page) {
        var errors = new List<string>();
        page.PageError += (_, _) => errors.Add("Page error");
        page.Response += (_, response) => {
            if (response.Status >= 400 && (response.Url.Contains("_content/", StringComparison.Ordinal) || response.Url.EndsWith(".css", StringComparison.Ordinal))) {
                errors.Add($"Asset HTTP {response.Status}");
            }
        };
        return errors;
    }
}
