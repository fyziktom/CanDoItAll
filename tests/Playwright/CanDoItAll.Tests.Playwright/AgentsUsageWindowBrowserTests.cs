using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.Infrastructure;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.FileSystem;
using CanDoItAll.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;
using Xunit.Abstractions;

namespace CanDoItAll.Tests.Playwright;

[Trait("Category", "Playwright")]
public sealed class AgentsUsageWindowBrowserTests(ITestOutputHelper output) : IAsyncLifetime {
    private readonly PlaywrightAppFixture fixture = new();
    public Task InitializeAsync() => fixture.InitializeAsync();
    public Task DisposeAsync() => fixture.DisposeAsync();

    [Fact]
    public async Task Real_overview_periods_pending_switch_dialogs_and_history_use_one_bounded_query() {
        Assert.NotNull(fixture.StorageWorkspaceRoot);
        await using var services = await TestApplicationBootstrap.BuildServiceProviderAsync(fixture.OwnedDatabaseProfile,
            "CanDoItAll.Tests.Playwright.UsageWindow", TestSchemaBootstrapModules.Full);
        await using var serviceScope = services.CreateAsyncScope();
        var provider = serviceScope.ServiceProvider;
        var profile = provider.GetRequiredService<IDatabaseProfileRuntimeAccessor>().ResolveCurrentProfile();
        var scope = WorkspaceScopeDescriptor.Organization(profile.Profile.Id.ToString("N"));
        var store = provider.GetRequiredService<ISandboxWorkspaceStore>();
        var agent = (await store.LoadCatalogAsync()).Agents.First();
        var now = DateTimeOffset.UtcNow;
        await store.SaveExecutionAsync(SandboxWorkspaceExecutionState.Empty with {
            ProviderUsageObservations = new[] { 2, 10, 100, 500 }.Select(days => new ProviderUsageObservation(Guid.NewGuid(), now.AddDays(-days),
                "Usage browser fixture", ProviderKind.OpenAi, "fixture-model", ProviderTransportKind.Responses,
                ProviderUsageSourcePhases.AgentRuntime, ProviderUsageObservationStatus.Observed, 60, 10, 40, 0, 100, 0) {
                AgentId = agent.Id, CalculatedCostUsd = 0.25m
            }).ToArray()
        });
        var maintenance = new FileProviderUsageIndexMaintenance(fixture.StorageWorkspaceRoot, scope);
        Assert.True((await maintenance.ProcessAsync()).Complete);
        var evidence = Path.Combine(PlaywrightTestHostPaths.RepositoryRoot, "output", "playwright", "agents-usage-window");
        Directory.CreateDirectory(evidence);
        await using var context = await fixture.Browser.NewContextAsync(new() { ViewportSize = new() { Width = 1440, Height = 1000 } });
        await context.Tracing.StartAsync(new() { Screenshots = true, Snapshots = true, Sources = true });
        var page = await context.NewPageAsync();
        page.SetDefaultTimeout(30_000);
        var errors = new List<string>();
        page.PageError += (_, error) => errors.Add(error);
        try {
            await page.GotoAsync(fixture.BaseUrl + "/agents");
            await PlaywrightAppFixture.CompleteDatabaseStartupAsync(page);
            await UsageAsync("1");
            await Assertions.Expect(page.GetByTestId("agents-overview-period-7d")).ToHaveAttributeAsync("aria-pressed", "true");
            await Assertions.Expect(page.GetByTestId("agents-overview-window")).ToContainTextAsync("Agents and Chats");
            await CaptureAsync("default-seven-days");
            await page.GetByTestId("agents-overview-period-14d").ClickAsync();
            await UsageAsync("2");
            await page.GetByTestId("agents-overview-period-1m").ClickAsync();
            await UsageAsync("2");
            await page.GetByTestId("agents-overview-period-1q").ClickAsync();
            await UsageAsync("2");
            await page.GetByTestId("agents-overview-period-1y").ClickAsync();
            await UsageAsync("3");
            await CaptureAsync("year");
            await page.GetByTestId("agents-overview-usage-scope").GetByRole(AriaRole.Button, new() { Name = "Chats", Exact = true }).ClickAsync();
            await UsageAsync("0");
            await Assertions.Expect(page.GetByTestId("agents-overview-window")).ToContainTextAsync("Chats · Last 1y");
            await page.GetByTestId("agents-overview-usage-scope").GetByRole(AriaRole.Button, new() { Name = "Agents", Exact = true }).ClickAsync();
            await UsageAsync("3");
            var accepted = await page.GetByTestId("agents-overview-window").InnerTextAsync();
            await page.GetByTestId("agents-overview-open-provider-usage").ClickAsync();
            await Assertions.Expect(page.GetByTestId("provider-usage-dialog")).ToContainTextAsync("Usage browser fixture");
            await CaptureAsync("provider-dialog");
            await page.GetByTestId("provider-usage-dialog").GetByRole(AriaRole.Button, new() { Name = "Close", Exact = true }).ClickAsync();
            Assert.Equal(accepted, await page.GetByTestId("agents-overview-window").InnerTextAsync());
            await page.GetByTestId("agents-overview-period-7d").ClickAsync();
            await UsageAsync("1");
            var writer = new DurableFileWriter(new PhysicalFileSystemPathPolicyFactory());
            await using (await writer.AcquireCoordinationAsync(fixture.StorageWorkspaceRoot, Path.Combine(scope.ResolveDataRoot(fixture.StorageWorkspaceRoot), "workspace.lock"),
                TimeSpan.FromSeconds(15), requirePrivateUnixMode: false, default)) {
                await page.GetByTestId("agents-overview-period-1y").ClickAsync();
                await Assertions.Expect(page.GetByTestId("agents-overview-period-1y")).ToHaveAttributeAsync("aria-pressed", "true");
                await CaptureAsync("year-pending");
                await page.GetByTestId("agents-overview-period-7d").ClickAsync();
                await page.GetByTestId("agents-overview-period-14d").ClickAsync();
                await Assertions.Expect(page.GetByTestId("agents-overview-period-14d")).ToHaveAttributeAsync("aria-pressed", "true");
            }
            await UsageAsync("2");
            await Assertions.Expect(page.GetByTestId("agents-overview-window")).ToContainTextAsync("Last 14d");
            await page.GetByTestId("agents-overview-usage-retry").ClickAsync();
            await UsageAsync("2");
            await page.GetByRole(AriaRole.Button, new() { Name = "Request history", Exact = true }).ClickAsync();
            await Assertions.Expect(page.GetByTestId("provider-request-history")).ToContainTextAsync("History not requested");
            await page.GetByRole(AriaRole.Button, new() { Name = "Overview", Exact = true }).ClickAsync();
            await UsageAsync("2");
            await Assertions.Expect(page.GetByTestId("agents-overview-period-14d")).ToHaveAttributeAsync("aria-pressed", "true");
            await page.GetByTestId("agents-shell-tabs").GetByRole(AriaRole.Button, new() { NameRegex = new("^Providers") }).ClickAsync();
            await Assertions.Expect(page.GetByTestId("agents-provider-profiles-panel")).ToBeVisibleAsync();
            await page.GetByRole(AriaRole.Button, new() { Name = "Overview", Exact = true }).ClickAsync();
            await UsageAsync("2");
            await Assertions.Expect(page.GetByTestId("agents-overview-period-14d")).ToHaveAttributeAsync("aria-pressed", "true");
            await page.SetViewportSizeAsync(420, 900);
            await page.GetByTestId("agents-overview-period-1y").ScrollIntoViewIfNeededAsync();
            await page.GetByTestId("agents-overview-period-1y").FocusAsync();
            await page.Keyboard.PressAsync("Enter");
            await UsageAsync("3");
            await page.GetByTestId("agents-overview-period-14d").ClickAsync();
            await UsageAsync("2");
            await page.GetByTestId("agents-overview-period-14d").ScrollIntoViewIfNeededAsync();
            await Assertions.Expect(page.GetByTestId("agents-overview-period-14d")).ToBeVisibleAsync();
            await CaptureAsync("mobile-controls");
            Assert.Empty(errors);
        } catch {
            await page.ScreenshotAsync(new() { Path = Path.Combine(evidence, "failure.png"), FullPage = true });
            output.WriteLine(fixture.GetLogSnapshot(60));
            throw;
        } finally {
            await context.Tracing.StopAsync(new() { Path = Path.Combine(evidence, "trace.zip") });
        }

        Task UsageAsync(string count) => Assertions.Expect(page.GetByTestId("agents-overview-metric-usage").Locator("strong")).ToHaveTextAsync(count);
        async Task CaptureAsync(string name) {
            await Assertions.Expect(page.Locator("#blazor-error-ui")).Not.ToBeVisibleAsync();
            await page.ScreenshotAsync(new() { Path = Path.Combine(evidence, name + ".png"), FullPage = true });
        }
    }
}
