using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Resources;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright.Smoke;

[CollectionDefinition(Name)]
public sealed class ResourcesBrowserCollection : ICollectionFixture<PlaywrightAppFixture> {
    public const string Name = "Resources owned browser host";
}

[Collection(ResourcesBrowserCollection.Name)]
[Trait("Category", "Playwright")]
[Trait("Category", "HostPlatform")]
public sealed class ResourcesBrowserTests(PlaywrightAppFixture fixture) {
    private static readonly string Artifacts = Path.Combine(PlaywrightTestHostPaths.RepositoryRoot, "output", "playwright", "resources-ui");

    [Fact]
    public async Task Production_registry_crud_browse_promotion_reopen_and_download_use_actual_owners() {
        Directory.CreateDirectory(Artifacts);
        await using var provider = await TestApplicationBootstrap.BuildServiceProviderAsync(fixture.OwnedDatabaseProfile, "Resources.Browser.Seed", TestSchemaBootstrapModules.Full,
            new Dictionary<string, string?> { [LocalRuntimeHostedWorkerPolicy.LaneKindConfigurationKey] = LocalRuntimeHostedWorkerPolicy.McpToolHostLaneKind });
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var project = await services.GetRequiredService<ProjectsService>().SaveAsync(new() { Name = "Owned Resources browser project" });
        Assert.True(project.IsSuccess);
        var owner = services.GetRequiredService<ResourcesService>();
        var storage = await services.GetRequiredService<IStorageCatalogService>().EnsureBootstrapFileSystemStorageAsync();
        var fileName = $"owned-resource-browser-{Guid.NewGuid():N}.txt";
        var path = Path.GetFullPath(Path.Combine(storage.EndpointOrRoot, fileName));
        Assert.StartsWith(Path.GetFullPath(fixture.OwnedDatabaseProfile.WorkspaceRootPath) + Path.DirectorySeparatorChar, path, StringComparison.OrdinalIgnoreCase);
        const string content = "Task-owned Resources browser content and download.";
        await File.WriteAllTextAsync(path, content);
        await using var context = await fixture.Browser.NewContextAsync(new() { ViewportSize = new() { Width = 1920, Height = 1080 } });
        var page = await context.NewPageAsync();
        var errors = Observe(page);
        try {
            await page.GotoAsync(fixture.BaseUrl + $"/resources?projectId={project.Value:D}");
            await PlaywrightAppFixture.CompleteDatabaseStartupAsync(page);
            await page.WaitForFunctionAsync("() => typeof databaseSwitchListeners !== 'undefined' && databaseSwitchListeners.size === 1");
            await page.GetByTestId("resource-plugin-select").SelectOptionAsync(ResourceConnectorPluginKeys.WebLink);
            await Assertions.Expect(page.GetByTestId($"resource-config-{ResourceConnectorFieldKeys.UrlTitleHint}")).ToBeVisibleAsync();
            await page.GetByTestId("resource-name-input").FillAsync("Browser created resource");
            await page.GetByTestId("resource-primary-input").FillAsync("https://example.test/browser-created");
            await page.GetByTestId("resource-save-button").EvaluateAsync("e => e.click()");
            await Assertions.Expect(page.GetByTestId("resource-mutation-receipt").First).ToContainTextAsync("Committed");
            var resource = Assert.Single(await owner.ListAsync(), r => r.ProjectId == project.Value);
            await page.GetByTestId("resource-name-input").FillAsync("Browser edited resource");
            await page.GetByTestId("resource-primary-input").FillAsync("https://example.test/browser-edited");
            await page.GetByTestId("resource-save-button").EvaluateAsync("e => e.click()");
            await Assertions.Expect(page.GetByTestId("resource-mutation-receipt")).ToHaveCountAsync(2);
            await Assertions.Expect(page.GetByTestId("resource-mutation-receipt").First).ToContainTextAsync("Committed");
            var edited = await owner.GetAsync(resource.Id);
            Assert.Equal("Browser edited resource", edited.Name);
            Assert.Equal("https://example.test/browser-edited", edited.LocationOrIdentifier);
            await Shot(page, "production-registry");
            await page.GetByTestId("resources-tab-browse").ClickAsync();
            await page.GetByTestId($"resources-source-storage:{storage.Id:N}").ClickAsync();
            var file = page.Locator(".ft-file-browser__item-main").Filter(new() { HasText = fileName });
            await file.DblClickAsync();
            await page.GetByTestId("resources-promotion-project").SelectOptionAsync(project.Value.ToString());
            await page.GetByTestId("resources-promotion-name").FillAsync("Browser governed file");
            Assert.True(await page.GetByTestId("resources-promotion-dialog").EvaluateAsync<bool>("e => { const r=e.getBoundingClientRect(); return r.top >= 0 && r.bottom <= innerHeight; }"));
            await Shot(page, "production-promotion");
            await page.GetByTestId("resources-promotion-save").EvaluateAsync("e => e.click()");
            await page.GetByTestId("resources-open-stored-object").ClickAsync();
            await Assertions.Expect(page.GetByTestId("interaction-text-view")).ToContainTextAsync(content);
            var promoted = Assert.Single(await owner.ListAsync(), r => r.ConnectorPluginKey == ResourceConnectorPluginKeys.StorageObject);
            Assert.Equal("Browser governed file", promoted.Name);
            await Shot(page, "production-preview");
            await page.GetByRole(AriaRole.Button, new() { Name = "Back to source", Exact = true }).ClickAsync();
            var row = page.Locator("tr[data-item-key]").Filter(new() { HasText = fileName });
            await row.Locator(".ft-file-browser__action-menu-button").ClickAsync();
            var download = await page.RunAndWaitForDownloadAsync(() => row.GetByRole(AriaRole.Button, new() { Name = "Download", Exact = true }).ClickAsync());
            await using (var stream = await download.CreateReadStreamAsync()) {
                using var reader = new StreamReader(stream!);
                Assert.Equal(content, await reader.ReadToEndAsync());
            }
            await download.DeleteAsync();
            await Shot(page, "production-browse");
            await page.GetByTestId("resources-tab-registry").ClickAsync();
            await Assertions.Expect(page.GetByTestId("resource-name-input")).ToHaveValueAsync("Browser edited resource");
            await page.GetByText("Browser governed file", new() { Exact = true }).ClickAsync();
            await Assertions.Expect(page.GetByTestId("resource-governed-storage-object")).ToBeVisibleAsync();
            await page.GetByRole(AriaRole.Button, new() { Name = "Delete", Exact = true }).ClickAsync();
            await Assertions.Expect(page.GetByTestId("resource-mutation-receipt").First).ToContainTextAsync("Resource deleted");
            Assert.Null((await owner.GetAsync(promoted.Id)).Id);
            await page.GetByText("Browser edited resource", new() { Exact = true }).ClickAsync();
            await page.GetByRole(AriaRole.Button, new() { Name = "Delete", Exact = true }).ClickAsync();
            await Assertions.Expect(page.GetByTestId("resource-name-input")).ToHaveValueAsync(string.Empty);
            Assert.Empty((await owner.ListAsync()).Where(r => r.ProjectId == project.Value));
            Assert.Empty(errors);
        } catch {
            await Shot(page, "production-failure");
            await File.WriteAllTextAsync(Path.Combine(Artifacts, "production-failure.html"), await page.ContentAsync());
            throw;
        } finally {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Standalone_sandbox_preserves_focus_raw_input_and_real_file_components_through_held_operations() {
        Directory.CreateDirectory(Artifacts);
        await using var host = new ResourcesSandboxHost();
        await host.ReadyAsync();
        await using var context = await fixture.Browser.NewContextAsync(new() { ViewportSize = new() { Width = 1600, Height = 1000 } });
        var page = await context.NewPageAsync();
        var errors = Observe(page);
        var assets = new List<string>();
        page.Response += (_, response) => {
            if (response.Url.Contains("_content/", StringComparison.Ordinal) && response.Status >= 400) {
                assets.Add($"{response.Status} {response.Url}");
            }
        };
        try {
            await page.GotoAsync(host.BaseUrl);
            var name = page.GetByTestId("resource-name-input");
            await Assertions.Expect(name).ToHaveValueAsync("Sample resource 1");
            await page.GetByTestId("resources-hold-lane").SelectOptionAsync("RegistryRead");
            await page.GetByTestId("resources-hold").ClickAsync();
            await page.GetByRole(AriaRole.Button, new() { Name = "Refresh", Exact = true }).ClickAsync();
            await Assertions.Expect(page.GetByText("Refreshing resource references; your editor is retained.", new() { Exact = true })).ToBeVisibleAsync();
            await name.FillAsync("Unblurred browser draft");
            await name.EvaluateAsync("e => e.setSelectionRange(6,6)");
            await page.GetByTestId("resources-release").EvaluateAsync("e => e.click()");
            await Assertions.Expect(page.GetByText("Refreshing resource references; your editor is retained.", new() { Exact = true })).ToHaveCountAsync(0);
            await Assertions.Expect(name).ToBeFocusedAsync();
            Assert.Equal(6, await name.EvaluateAsync<int>("e => e.selectionStart"));
            await Shot(page, "sandbox-registry");
            await page.GetByTestId("resources-tab-browse").ClickAsync();
            await page.GetByTestId("resources-source-group-filesystem").Locator(".cda-selection-list-item").ClickAsync();
            await page.Locator(".ft-file-browser__item-main").Filter(new() { HasText = "report-001.txt" }).DblClickAsync();
            await page.GetByTestId("resources-promotion-name").FillAsync("Captured sandbox promotion");
            await Shot(page, "sandbox-promotion");
            await page.GetByTestId("resources-promotion-save").ClickAsync();
            await page.GetByTestId("resources-open-stored-object").ClickAsync();
            await Assertions.Expect(page.GetByTestId("interaction-text-view")).ToContainTextAsync("Owned synthetic Resources content");
            await Assertions.Expect(page.GetByTestId("resources-scenario-state")).ToContainTextAsync("stored resources: 4; writes: 0; promotions: 1");
            await Shot(page, "sandbox-preview");
            await page.GetByTestId("resources-tab-registry").ClickAsync();
            await Assertions.Expect(name).ToHaveValueAsync("Unblurred browser draft");
            await page.GetByTestId("resources-hold-lane").SelectOptionAsync("PreviewRelease");
            await page.GetByTestId("resources-hold").ClickAsync();
            await page.GetByTestId("resources-scenario-select").SelectOptionAsync("InvalidFields");
            await page.GetByTestId("resources-reset-scenario").ClickAsync();
            await Assertions.Expect(page.GetByTestId("resource-primary-input")).ToHaveValueAsync("1e-");
            await page.GetByTestId("resource-config-fixtureJson").FillAsync("{ \"unfinished\":");
            await page.GetByTestId("resource-save-button").ClickAsync();
            await Assertions.Expect(page.GetByTestId("resource-registry-error")).ToContainTextAsync("Configuration field");
            await page.GetByTestId("resources-tab-browse").ClickAsync();
            await page.GetByTestId("resources-tab-registry").ClickAsync();
            await Assertions.Expect(page.GetByTestId("resource-config-fixtureJson")).ToHaveValueAsync("{ \"unfinished\":");
            await page.GetByTestId("resources-scenario-select").SelectOptionAsync("UnknownWrite");
            await page.GetByTestId("resources-reset-scenario").ClickAsync();
            await page.GetByTestId("resource-save-button").ClickAsync();
            await Assertions.Expect(page.GetByTestId("resource-mutation-receipt")).ToContainTextAsync("Unknown");
            await page.GetByRole(AriaRole.Button, new() { Name = "Review stored identity", Exact = true }).ClickAsync();
            await Assertions.Expect(page.GetByTestId("resource-mutation-receipt")).ToContainTextAsync("Reviewed");
            await page.GetByTestId("resources-scenario-select").SelectOptionAsync("Representative");
            await page.GetByTestId("resources-reset-scenario").ClickAsync();
            await page.GetByTestId("resources-hold-lane").SelectOptionAsync("Save");
            await page.GetByTestId("resources-hold").ClickAsync();
            await name.FillAsync("Retired scenario write");
            await page.GetByTestId("resource-save-button").ClickAsync();
            await Assertions.Expect(page.GetByTestId("resource-mutation-receipt")).ToContainTextAsync("Pending");
            await page.GetByTestId("resources-reset-scenario").ClickAsync();
            await page.GetByTestId("resources-release").ClickAsync();
            await Assertions.Expect(name).ToHaveValueAsync("Sample resource 1");
            await Assertions.Expect(page.GetByTestId("resources-retired-stores")).ToContainTextAsync("writes 1");
            Assert.False(await page.EvaluateAsync<bool>("() => document.documentElement.scrollWidth > innerWidth"));
            await page.EvaluateAsync("() => document.fonts.ready");
            Assert.Equal("loaded", await page.EvaluateAsync<string>("() => [...document.fonts].find(f => f.family.includes('Material'))?.status"));
            Assert.Empty(errors);
            Assert.Empty(assets);
        } finally {
            await File.WriteAllTextAsync(Path.Combine(Artifacts, "sandbox-host.log"), host.Logs);
        }
    }

    private static List<string> Observe(IPage page) {
        var errors = new List<string>();
        page.PageError += (_, error) => errors.Add(error);
        page.Console += (_, message) => {
            if (message.Type == "error") {
                errors.Add(message.Text);
            }
        };
        return errors;
    }
    private static Task Shot(IPage page, string name) => page.ScreenshotAsync(new() { Path = Path.Combine(Artifacts, name + ".png") });
}
