using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.Workspace.StorageCatalog.Contracts;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Playwright.StorageCatalog;
using CanDoItAll.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright;

[Trait("Category", "Playwright")]
[Trait("Category", "HostPlatform")]
public sealed class StorageCatalogBrowserTests {
    private static readonly string Evidence = Path.Combine(PlaywrightTestHostPaths.RepositoryRoot, "output", "playwright", "workspace-storage-catalog-ui");

    [Fact]
    public async Task Production_wizard_persists_private_catalog_routes_health_and_passive_recovery() {
        await using var host = new StorageCatalogBrowserHost(false);
        await host.ReadyAsync(false);
        using var playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
        await using var context = await browser.NewContextAsync(new() { ViewportSize = new() { Width = 1600, Height = 1000 } });
        var page = await context.NewPageAsync();
        var errors = Observe(page);
        await page.GotoAsync(host.BaseUrl + "/settings?tab=storage");
        await PlaywrightAppFixture.CompleteDatabaseStartupAsync(page);
        await page.GetByTestId("storage-settings-name").FillAsync("Browser private catalog");
        await Shot(page, "production-storage-identity");
        await Next(page);
        var root = Path.Combine(host.Profile!.WorkspaceRootPath, "browser-catalog");
        Directory.CreateDirectory(root);
        var marker = Path.Combine(root, "unchanged.txt");
        await File.WriteAllTextAsync(marker, "Storage test must not modify existing content.");
        await page.GetByTestId("storage-settings-endpoint").FillAsync(root);
        await Shot(page, "production-storage-connectivity");
        await Next(page);
        await page.GetByTestId("storage-settings-purpose-evidence").CheckAsync();
        await page.GetByTestId("storage-settings-save").ClickAsync();
        await Assertions.Expect(page.GetByTestId("storage-operation-facts").First).ToContainTextAsync("Routing: Complete");
        await using var provider = await TestApplicationBootstrap.BuildServiceProviderAsync(host.Profile, "Storage.Catalog.Browser", TestSchemaBootstrapModules.Full,
            new Dictionary<string, string?> { [LocalRuntimeHostedWorkerPolicy.LaneKindConfigurationKey] = LocalRuntimeHostedWorkerPolicy.McpToolHostLaneKind });
        await using var scope = provider.CreateAsyncScope();
        var owner = scope.ServiceProvider.GetRequiredService<IStorageCatalogOwner>();
        var saved = Assert.Single(await owner.ReadCatalogAsync(default), row => row.Name == "Browser private catalog");
        Assert.Contains(CatalogPurpose.Evidence, (await owner.ReadEditorAsync(saved.Id, default))!.DefaultPurposes);
        await Assertions.Expect(page.GetByTestId("storage-settings-save")).ToBeEnabledAsync();
        await page.GetByTestId("storage-settings-test").ClickAsync();
        await Assertions.Expect(page.GetByTestId("storage-operation-facts").First).ToContainTextAsync("Driver: Completed");
        Assert.Equal(CatalogHealth.Unavailable, (await owner.ReadEditorAsync(saved.Id, default))!.Health.Status);
        Assert.Equal("Storage test must not modify existing content.", await File.ReadAllTextAsync(marker));
        await Assertions.Expect(page.GetByTestId("storage-settings-save")).ToBeEnabledAsync();
        await page.GetByTestId("storage-settings-purpose-evidence").UncheckAsync();
        await page.GetByTestId("storage-settings-save").ClickAsync();
        await Assertions.Expect(page.GetByTestId("storage-operation-facts")).ToHaveCountAsync(3);
        await Assertions.Expect(page.GetByTestId("storage-operation-facts").First).ToContainTextAsync("Routing: Complete");
        Assert.Empty((await owner.ReadEditorAsync(saved.Id, default))!.DefaultPurposes);
        await page.GetByRole(AriaRole.Button, new() { Name = "Previous step", Exact = true }).ClickAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Previous step", Exact = true }).ClickAsync();
        await page.GetByTestId("storage-settings-name").FillAsync("Unsaved reselected catalog");
        await page.GetByTestId("storage-settings-display-order").FillAsync("1e");
        await Next(page);
        await page.GetByTestId("storage-settings-save").ClickAsync();
        await Assertions.Expect(page.GetByText("Display order must be a complete integer.", new() { Exact = true })).ToBeVisibleAsync();
        await page.GetByTestId($"storage-catalog-row-{saved.Id:N}").ClickAsync();
        await Assertions.Expect(page.GetByTestId("storage-settings-endpoint")).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByText("Display order must be a complete integer.", new() { Exact = true })).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByTestId("storage-operation-facts")).ToHaveCountAsync(3);
        await page.GetByRole(AriaRole.Button, new() { Name = "Previous step", Exact = true }).ClickAsync();
        await Assertions.Expect(page.GetByTestId("storage-settings-name")).ToHaveValueAsync("Unsaved reselected catalog");
        await Assertions.Expect(page.GetByTestId("storage-settings-display-order")).ToHaveValueAsync("1e");
        Assert.Equal("Browser private catalog", (await owner.ReadEditorAsync(saved.Id, default))!.Name);
        await Shot(page, "production-storage-same-target");
        await page.GetByTestId("storage-settings-name").FillAsync(saved.Name);
        await page.GetByTestId("storage-settings-display-order").FillAsync(saved.DisplayOrder.ToString(System.Globalization.CultureInfo.InvariantCulture));
        await Next(page);
        await Next(page);
        await Shot(page, "production-storage-routing");
        await page.GetByTestId("storage-settings-recovery").ClickAsync();
        await page.GetByTestId("storage-recovery-dialog").WaitForAsync();
        await Shot(page, "production-storage-recovery");
        await page.GetByTestId("storage-recovery-dialog").GetByRole(AriaRole.Button, new() { Name = "Close", Exact = true }).Last.ClickAsync();
        await page.GetByTestId("storage-settings-delete").ClickAsync();
        await Assertions.Expect(page.GetByText("This target was deleted. Choose New explicitly to create another target.", new() { Exact = true })).ToBeVisibleAsync();
        Assert.Null(await owner.ReadEditorAsync(saved.Id, default));
        await page.GetByRole(AriaRole.Button, new() { Name = "Data Sources", Exact = true }).ClickAsync();
        await Assertions.Expect(page.GetByTestId("database-data-sources-summary")).ToBeVisibleAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Storage", Exact = true }).ClickAsync();
        await page.GetByTestId("storage-settings-new-ipfs").ClickAsync();
        await Assertions.Expect(page.GetByTestId("storage-settings-provider")).ToHaveValueAsync(CatalogProvider.Ipfs.ToString());
        await Next(page);
        await page.GetByTestId("storage-settings-ipfs-gateway").WaitForAsync();
        await Shot(page, "production-storage-ipfs");
        await page.GetByTestId("storage-settings-new-ftp").ClickAsync();
        await Assertions.Expect(page.GetByTestId("storage-settings-provider")).ToHaveValueAsync(CatalogProvider.Ftp.ToString());
        await Next(page);
        await page.GetByTestId("storage-settings-ftp-port").WaitForAsync();
        await Shot(page, "production-storage-ftp");
        Assert.Empty(errors);
        Assert.DoesNotContain("Unhandled exception", host.Logs, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Independent_source_and_published_sandbox_keep_real_wizard_focus_partial_outcomes_and_assets(bool published) {
        var publishedDirectory = published ? Path.Combine(PlaywrightTestHostPaths.RepositoryRoot, "artifacts", "workspace-storage-catalog-ui", "published") : null;
        await using var host = new StorageCatalogBrowserHost(true, publishedDirectory);
        await host.ReadyAsync(true);
        using var playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
        await using var context = await browser.NewContextAsync(new() { ViewportSize = new() { Width = 1600, Height = 1000 } });
        var page = await context.NewPageAsync();
        var errors = Observe(page);
        await page.GotoAsync(host.BaseUrl);
        var name = page.GetByTestId("storage-settings-name");
        await name.WaitForAsync();
        var wizard = await page.GetByTestId("storage-settings-wizard").BoundingBoxAsync();
        Assert.True(wizard is { Width: > 750 }, "The desktop wizard must use the available detail pane width.");
        await page.GetByTestId("storage-scenario-stage").SelectOptionAsync("SecretsRead");
        await page.GetByTestId("storage-stage-hold").ClickAsync();
        await page.GetByTestId("storage-refresh-secrets").ClickAsync();
        await name.FillAsync("Unblurred storage draft");
        await name.EvaluateAsync("element => element.setSelectionRange(8,8)");
        await page.GetByTestId("storage-stage-release").EvaluateAsync("element => element.click()");
        await Assertions.Expect(name).ToBeFocusedAsync();
        Assert.Equal(8, await name.EvaluateAsync<int>("element => element.selectionStart"));
        await Assertions.Expect(name).ToHaveValueAsync("Unblurred storage draft");
        await Assertions.Expect(page.GetByTestId("storage-scenario-counts")).ToContainTextAsync("driver tests: 0");
        await page.GetByTestId("storage-settings-display-order").FillAsync("-");
        await Next(page);
        await page.GetByTestId("storage-settings-endpoint").FillAsync("/scenario/visible");
        await Next(page);
        await page.GetByTestId("storage-settings-save").ClickAsync();
        await Assertions.Expect(page.GetByText("Display order must be a complete integer.", new() { Exact = true })).ToBeVisibleAsync();
        await Shot(page, published ? "published-storage-validation" : "sandbox-storage-validation");
        await page.GetByTestId("storage-scenario").SelectOptionAsync("PartialRouting");
        await page.GetByTestId("storage-scenario-reset").ClickAsync();
        await page.GetByTestId("storage-settings-name").FillAsync("Partial routing target");
        await Next(page);
        await page.GetByTestId("storage-settings-endpoint").FillAsync("/scenario/partial");
        await Next(page);
        await page.GetByTestId("storage-settings-purpose-projectasset").CheckAsync();
        await page.GetByTestId("storage-settings-save").ClickAsync();
        await Assertions.Expect(page.GetByTestId("storage-operation-facts")).ToContainTextAsync("Catalog: Committed. Routing: PossiblyPartial");
        await Assertions.Expect(page.GetByTestId("storage-settings-save")).ToBeDisabledAsync();
        await page.GetByTestId("storage-observe").ClickAsync();
        await Assertions.Expect(page.GetByTestId("storage-scenario-counts")).ToContainTextAsync("Catalog writes: 1;");
        await page.GetByTestId("storage-review-partial").ClickAsync();
        await Assertions.Expect(page.GetByTestId("storage-settings-save")).ToBeEnabledAsync();
        await Shot(page, published ? "published-storage-partial" : "sandbox-storage-partial");
        await page.GetByTestId("storage-settings-recovery").ClickAsync();
        await Assertions.Expect(page.GetByTestId("storage-scenario-controls")).ToContainTextAsync("Deferred host Recovery for exact catalog");
        Assert.True(await page.EvaluateAsync<bool>("document.fonts.check('16px \"Material Symbols Rounded\"')"));
        Assert.True(await page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= innerWidth + 1"));
        Assert.Empty(errors);
        Assert.DoesNotContain("Unhandled exception", host.Logs, StringComparison.OrdinalIgnoreCase);
    }

    private static Task Next(IPage page) => page.GetByRole(AriaRole.Button, new() { Name = "Next step", Exact = true }).ClickAsync();
    private static List<string> Observe(IPage page) {
        var errors = new List<string>();
        page.PageError += (_, error) => errors.Add(error);
        page.Console += (_, message) => {
            if (message.Type == "error") {
                errors.Add(message.Text);
            }
        };
        page.Response += (_, response) => {
            if (response.Status >= 400) {
                errors.Add($"HTTP {response.Status}: {new Uri(response.Url).AbsolutePath}");
            }
        };
        return errors;
    }
    private static async Task Shot(IPage page, string name) {
        Directory.CreateDirectory(Evidence);
        await page.ScreenshotAsync(new() { Path = Path.Combine(Evidence, name + ".png"), FullPage = true });
    }
}
