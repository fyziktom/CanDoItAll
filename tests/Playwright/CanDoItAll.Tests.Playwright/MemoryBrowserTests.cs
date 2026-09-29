using CanDoItAll.Memory.Abstractions;
using CanDoItAll.Memory.Application;
using CanDoItAll.Memory.Protocol.Http;
using CanDoItAll.Modules.Memory.Services;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright.Smoke;

[Collection(MemoryBrowserCollection.Name)]
[Trait("Category", "Playwright")]
[Trait("Category", "HostPlatform")]
public sealed class MemoryBrowserTests(MemoryBrowserFixture memoryFixture) {
    private readonly PlaywrightAppFixture fixture = memoryFixture.App;
    private static readonly string Artifacts = Path.Combine(PlaywrightTestHostPaths.RepositoryRoot, "output", "playwright", "memory-ui");

    [Fact]
    public async Task Sandbox_seven_tabs_preserve_unblurred_input_and_render_owned_extensions_with_real_assets() {
        Directory.CreateDirectory(Artifacts);
        await using var host = new MemorySandboxHost();
        await host.ReadyAsync();
        await using var context = await fixture.Browser.NewContextAsync(new() { ViewportSize = new() { Width = 1600, Height = 1000 } });
        var page = await context.NewPageAsync();
        var errors = Observe(page);
        try {
            await page.GotoAsync(host.BaseUrl);
            await Assertions.Expect(page.GetByTestId("memory-ui-editor-display-name")).ToHaveValueAsync("Business memory");
            await page.GetByTestId("memory-hold-Read").ClickAsync();
            await page.GetByTestId("memory-ui-refresh").ClickAsync();
            var name = page.GetByTestId("memory-ui-editor-display-name");
            await name.FillAsync("Unblurred browser draft");
            await page.GetByTestId("memory-release").EvaluateAsync("e => e.click()");
            await Assertions.Expect(page.GetByTestId("memory-ui-refresh")).ToBeEnabledAsync();
            await Assertions.Expect(name).ToBeFocusedAsync();
            await Assertions.Expect(name).ToHaveValueAsync("Unblurred browser draft");
            await page.GetByTestId("memory-ui-editor-selection-tags").FillAsync("unfinished-tag");
            await page.GetByTestId("memory-ui-refresh").ClickAsync();
            await Assertions.Expect(page.GetByTestId("memory-ui-refresh")).ToBeEnabledAsync();
            await Assertions.Expect(page.GetByTestId("memory-ui-editor-selection-tags")).ToHaveValueAsync("unfinished-tag");
            foreach (var tab in new[] { "operations", "events", "feedback", "query", "ingestion", "provider-ui", "providers" }) {
                await page.GetByTestId("memory-ui-tab-" + tab).ClickAsync();
                await Shot(page, "sandbox-" + tab);
            }
            await Assertions.Expect(name).ToHaveValueAsync("Unblurred browser draft");
            await Assertions.Expect(page.GetByTestId("memory-ui-editor-selection-tags")).ToHaveValueAsync("unfinished-tag");
            await page.GetByTestId("memory-provider-provider-http").ClickAsync();
            await page.GetByTestId("memory-ui-editor-http-base-url").FillAsync("https://unblurred.example/");
            await page.GetByTestId("memory-ui-editor-http-timeout").FillAsync("-");
            await page.GetByTestId("memory-ui-refresh").ClickAsync();
            await Assertions.Expect(page.GetByTestId("memory-ui-editor-http-base-url")).ToHaveValueAsync("https://unblurred.example/");
            await Assertions.Expect(page.GetByTestId("memory-ui-editor-http-timeout")).ToHaveValueAsync("-");
            await page.GetByTestId("memory-scenario-Populated").ClickAsync();
            await page.GetByTestId("memory-ui-tab-query").ClickAsync();
            await page.GetByTestId("memory-ui-query-text").FillAsync("captured browser query");
            await page.GetByTestId("memory-hold-Query").ClickAsync();
            await page.GetByTestId("memory-ui-query-submit").ClickAsync();
            await Assertions.Expect(page.GetByTestId("memory-ui-query-submit")).ToBeDisabledAsync();
            await page.GetByTestId("memory-ui-tab-providers").ClickAsync();
            await page.GetByTestId("memory-provider-provider-b").ClickAsync();
            await Assertions.Expect(name).ToHaveValueAsync("Engineering memory");
            await page.GetByTestId("memory-release").ClickAsync();
            await Assertions.Expect(page.GetByTestId("memory-ui-submissions")).ToContainTextAsync("Observed");
            await Assertions.Expect(name).ToBeVisibleAsync();
            await page.GetByTestId("memory-ui-tab-query").ClickAsync();
            await Assertions.Expect(page.GetByText("Synthetic context for captured browser query", new() { Exact = true })).ToHaveCountAsync(0);
            await page.GetByTestId("memory-scenario-AcceptedQuery").ClickAsync();
            await page.GetByTestId("memory-ui-tab-query").ClickAsync();
            await page.GetByTestId("memory-ui-query-async").CheckAsync();
            await page.GetByTestId("memory-ui-query-submit").ClickAsync();
            await Assertions.Expect(page.GetByTestId("memory-ui-submissions")).ToContainTextAsync("Observed");
            await page.GetByTestId("memory-ui-tab-operations").ClickAsync();
            await page.GetByTestId("memory-hold-Status").ClickAsync();
            await page.GetByTestId("memory-ui-refresh-operation").ClickAsync();
            await Assertions.Expect(page.GetByTestId("memory-ui-refresh-operation")).ToBeDisabledAsync();
            await page.GetByTestId("memory-release").ClickAsync();
            await Assertions.Expect(page.GetByTestId("memory-ui-operations").GetByText("Synthetic status completed", new() { Exact = true }).First).ToBeVisibleAsync();
            await page.GetByTestId("memory-scenario-UiVariants").ClickAsync();
            await page.GetByTestId("memory-ui-tab-provider-ui").ClickAsync();
            await Assertions.Expect(page.GetByTestId("memory-ui-provider-rcl-host")).ToBeVisibleAsync();
            await Assertions.Expect(page.FrameLocator("[data-testid='memory-ui-provider-iframe']").GetByText("Owned local provider console")).ToBeVisibleAsync();
            await Assertions.Expect(page.GetByTestId("memory-ui-provider-external-link")).ToHaveAttributeAsync("rel", "noopener noreferrer");
            await Assertions.Expect(page.GetByTestId("memory-ui-provider-surface-fallback")).ToHaveCountAsync(4);
            await Shot(page, "sandbox-provider-variants");
            await page.GetByTestId("memory-ui-tab-providers").ClickAsync();
            await Assertions.Expect(page.Locator("iframe")).ToHaveCountAsync(0);
            await page.GetByTestId("memory-remove").ClickAsync();
            await Assertions.Expect(page.GetByTestId("memory-ui-error")).ToContainTextAsync("selected provider is missing");
            await page.GetByTestId("memory-provider-provider-b").ClickAsync();
            await Assertions.Expect(name).ToHaveValueAsync("Engineering memory");
            await page.GetByTestId("memory-scenario-InitialLoading").ClickAsync();
            await Assertions.Expect(page.GetByText("Loading memory providers", new() { Exact = true })).ToBeVisibleAsync();
            await page.GetByTestId("memory-release").ClickAsync();
            await Assertions.Expect(name).ToHaveValueAsync("Business memory");
            await page.GetByTestId("memory-hold-Save").ClickAsync();
            await name.FillAsync("Original store write");
            await page.GetByTestId("memory-ui-save-provider").ClickAsync();
            await Assertions.Expect(page.GetByTestId("memory-ui-submissions")).ToContainTextAsync("Pending");
            await page.GetByTestId("memory-scenario-Populated").ClickAsync();
            await Assertions.Expect(name).ToHaveValueAsync("Business memory");
            await page.GetByTestId("memory-release").ClickAsync();
            await Assertions.Expect(page.GetByTestId("memory-retired-stores")).ToContainTextAsync("Original store write");
            await page.GetByTestId("memory-scenario-UnknownQuery").ClickAsync();
            await page.GetByTestId("memory-ui-tab-query").ClickAsync();
            await page.GetByTestId("memory-ui-query-submit").ClickAsync();
            await Assertions.Expect(page.GetByTestId("memory-ui-submissions")).ToContainTextAsync("Unknown");
            await page.GetByTestId("memory-ui-submissions").Locator("summary").ClickAsync();
            await page.GetByTestId("memory-hold-Read").ClickAsync();
            await page.GetByTestId("memory-ui-review").ClickAsync();
            await Assertions.Expect(page.GetByTestId("memory-ui-review")).ToBeDisabledAsync();
            await Shot(page, "sandbox-unknown-review");
            await page.GetByTestId("memory-release").ClickAsync();
            await Assertions.Expect(page.GetByTestId("memory-ui-submissions")).ToContainTextAsync("outcome remains unresolved");
            await page.GetByTestId("memory-scenario-Partial").ClickAsync();
            await Assertions.Expect(page.GetByTestId("memory-ui-error")).ToContainTextAsync("stale");
            await page.GetByTestId("memory-scenario-Unavailable").ClickAsync();
            await Assertions.Expect(page.GetByText("Memory provider state is unavailable", new() { Exact = true })).ToBeVisibleAsync();
            await page.GetByTestId("memory-scenario-Empty").ClickAsync();
            await Assertions.Expect(page.GetByTestId("memory-ui-zero-provider")).ToBeVisibleAsync();
            await page.GetByTestId("memory-scenario-LargeCatalog").ClickAsync();
            await Assertions.Expect(page.Locator(".memory-ui-provider-option")).ToHaveCountAsync(103);
            var geometry = await page.EvaluateAsync<string>("""
                () => {
                    const option = document.querySelector('.memory-ui-provider-option');
                    const grid = document.querySelector('.memory-ui-layout');
                    const frame = document.querySelector('[data-testid="memory-ui-page"]');
                    return JSON.stringify({minHeight:getComputedStyle(option).minHeight, columns:getComputedStyle(grid).gridTemplateColumns, width:frame.getBoundingClientRect().width, overflow:document.documentElement.scrollWidth > innerWidth, fonts:document.fonts.status});
                }
                """);
            await File.WriteAllTextAsync(Path.Combine(Artifacts, "sandbox-geometry.json"), geometry);
            Assert.Contains("\"minHeight\":\"68px\"", geometry, StringComparison.Ordinal);
            Assert.Contains("\"overflow\":false", geometry, StringComparison.Ordinal);
            await Shot(page, "sandbox-large");
            Assert.Empty(errors);
        } finally {
            await File.WriteAllTextAsync(Path.Combine(Artifacts, "sandbox-host.log"), host.Logs);
        }
    }

    [Fact]
    public async Task Production_page_saves_queries_and_preserves_selection_while_real_HTTP_response_is_held() {
        Directory.CreateDirectory(Artifacts);
        await using var provider = await TestApplicationBootstrap.BuildServiceProviderAsync(fixture.OwnedDatabaseProfile, "Memory.Browser.Seed", TestSchemaBootstrapModules.Full,
            new Dictionary<string, string?> { [LocalRuntimeHostedWorkerPolicy.LaneKindConfigurationKey] = LocalRuntimeHostedWorkerPolicy.McpToolHostLaneKind });
        await using var scope = provider.CreateAsyncScope();
        var owner = scope.ServiceProvider.GetRequiredService<IMemoryProviderManagementUiService>();
        await using var remote = await HeldProvider.CreateAsync();
        await owner.SaveProviderAsync(new() { InstanceId = "provider.browser-http", DisplayName = "Owned held HTTP provider", DriverKind = MemoryProviderDriverKind.Http,
            ProviderKind = "memory.http", HealthState = MemoryProviderHealthState.Healthy, Http = new() { BaseUrl = remote.Url, TimeoutMilliseconds = 120000 } });
        await using var context = await fixture.Browser.NewContextAsync(new() { ViewportSize = new() { Width = 1600, Height = 1000 } });
        var page = await context.NewPageAsync();
        var errors = Observe(page);
        await page.GotoAsync(fixture.BaseUrl + "/memory");
        await PlaywrightAppFixture.CompleteDatabaseStartupAsync(page);
        await page.GetByTestId("memory-ui-add-demo-providers").ClickAsync();
        await Assertions.Expect(page.GetByTestId("memory-provider-provider-business-demo")).ToBeVisibleAsync();
        await page.GetByTestId("memory-provider-provider-business-demo").ClickAsync();
        await Assertions.Expect(page.GetByTestId("memory-ui-editor-display-name")).ToHaveValueAsync("Business demo memory");
        await page.GetByTestId("memory-ui-editor-display-name").FillAsync("Browser saved memory");
        await page.GetByTestId("memory-ui-save-provider").ClickAsync();
        await Assertions.Expect(page.GetByTestId("memory-ui-submission").Filter(new() { HasText = "Save: provider.business-demo" })).ToContainTextAsync("Observed");
        Assert.Equal("Browser saved memory", (await owner.GetSnapshotAsync(MemoryDemoProviderIds.Business)).SelectedProvider!.DisplayName);
        await page.GetByTestId("memory-ui-tab-query").ClickAsync();
        await page.GetByTestId("memory-ui-query-text").FillAsync("real harmless browser query");
        await page.GetByTestId("memory-ui-query-submit").ClickAsync();
        await Assertions.Expect(page.GetByText("Mock memory context for real harmless browser query", new() { Exact = true })).ToBeVisibleAsync();
        var query = (await owner.GetSnapshotAsync(MemoryDemoProviderIds.Business)).Operations.Single();
        Assert.Equal(MemoryLedgerStatus.Completed, query.Status);
        await Shot(page, "production-query");
        await page.GetByTestId("memory-ui-tab-ingestion").ClickAsync();
        await Assertions.Expect(page.GetByTestId("memory-ui-ingestion")).ToContainTextAsync("Ingestion unavailable");
        await Assertions.Expect(page.GetByTestId("memory-ui-ingestion-submit")).ToHaveCountAsync(0);
        await page.GetByTestId("memory-ui-tab-providers").ClickAsync();
        await page.GetByTestId("memory-provider-provider-browser-http").ClickAsync();
        await page.GetByTestId("memory-ui-tab-query").ClickAsync();
        await page.GetByTestId("memory-ui-query-text").FillAsync("original HTTP input");
        await page.GetByTestId("memory-ui-query-module").FillAsync("browser-module");
        await page.GetByTestId("memory-ui-query-record").FillAsync("browser-record");
        await page.GetByTestId("memory-ui-query-citation").FillAsync("browser-citation");
        await page.GetByTestId("memory-ui-query-submit").ClickAsync();
        await remote.Entered.Task.WaitAsync(TimeSpan.FromSeconds(20));
        await page.GetByTestId("memory-ui-query-text").FillAsync("successor raw input");
        await page.GetByTestId("memory-ui-tab-providers").ClickAsync();
        await page.GetByTestId("memory-provider-provider-business-demo").ClickAsync();
        remote.Release.TrySetResult();
        await Assertions.Expect(page.GetByTestId("memory-ui-submissions")).ToContainTextAsync("HTTP memory provider");
        await Assertions.Expect(page.GetByTestId("memory-ui-editor-display-name")).ToHaveValueAsync("Browser saved memory");
        await Assertions.Expect(page.GetByTestId("memory-ui-editor-display-name")).ToBeVisibleAsync();
        Assert.Contains("original HTTP input", remote.Body, StringComparison.Ordinal);
        Assert.Contains("browser-record", remote.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("successor raw input", remote.Body, StringComparison.Ordinal);
        Assert.Equal(1, remote.Requests);
        Assert.Equal(MemoryLedgerStatus.Completed, (await owner.GetSnapshotAsync("provider.browser-http")).Operations.Single().Status);
        foreach (var tab in new[] { "providers", "operations", "events", "feedback", "query", "ingestion", "provider-ui" }) {
            await page.GetByTestId("memory-ui-tab-" + tab).ClickAsync();
            await Shot(page, "production-" + tab);
        }
        Assert.Empty(errors);
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
    private static Task Shot(IPage page, string name) => page.ScreenshotAsync(new() { Path = Path.Combine(Artifacts, name + ".png"), FullPage = true });

    private sealed class HeldProvider(WebApplication app) : IAsyncDisposable {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public string Url => app.Urls.Single();
        public string Body { get; private set; } = string.Empty;
        public int Requests { get; private set; }
        public static async Task<HeldProvider> CreateAsync() {
            var builder = WebApplication.CreateSlimBuilder();
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            var app = builder.Build();
            var fixture = new HeldProvider(app);
            app.MapPost("/memory/query", async (HttpContext context) => {
                fixture.Requests++;
                using var reader = new StreamReader(context.Request.Body);
                fixture.Body = await reader.ReadToEndAsync(context.RequestAborted);
                fixture.Entered.TrySetResult();
                await fixture.Release.Task.WaitAsync(context.RequestAborted);
                return Results.Json(HttpMemoryProviderResponse.FromContextPack(new(MemoryContextPackId.New(), "Owned HTTP context", [], [], 1, null)));
            });
            await app.StartAsync();
            return fixture;
        }
        public async ValueTask DisposeAsync() {
            Release.TrySetResult();
            await app.StopAsync();
            await app.DisposeAsync();
        }
    }
}
