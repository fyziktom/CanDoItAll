using CanDoItAll.AgentFramework.Llm.SimpleChats.Persistence;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;

using static CanDoItAll.Tests.Playwright.Smoke.AgentUiJourneySupport;
using static CanDoItAll.Tests.Playwright.Smoke.ProjectFilesUiJourney;

namespace CanDoItAll.Tests.Playwright.Smoke;

public sealed class SharedHostLifetimeBrowserTests {
    [Fact]
    [Trait("Category", "Playwright")]
    [Trait("Category", "HostPlatform")]
    public async Task Shared_host_retires_held_catalog_read_then_repeats_routes_overlays_history_and_independent_circuits() {
        await using var host = new DataSourcesBrowserHost();
        await host.StartAsync(enableApiManagement: true);
        Directory.CreateDirectory(host.ArtifactDirectory);
        using var playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
        await using var context = await browser.NewContextAsync(new() { ViewportSize = new() { Width = 1600, Height = 1000 } });
        var locked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var queryHeld = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<bool>? holding = null;
        try {
            const string seedContent = "Retained file preview across shared route history.";
            var asset = await SeedAsync(services => services.GetRequiredService<ProjectStructureAgentService>().CreateAssetAsync(host.ProjectA,
                new(ProjectObjectType.File, "Existing nonce asset", "", "", new("seed.txt", "text/plain", Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(seedContent))),
                    $"project:{host.ProjectA:D}"), new(FixtureActor, FixtureActor, Environment.MachineName, "", "", "shared-lifetime")));
            var page = await context.NewPageAsync();
            page.SetDefaultTimeout(45_000);
            var oracle = CrmHrBrowserOracle.Attach(page);
            await oracle.NavigateAsync(host.BaseUrl + "/settings");
            await page.GetByRole(AriaRole.Tablist, new() { Name = "Open workspace tabs", Exact = true }).WaitForAsync();
            await page.GetByTestId("defaults-name").WaitForAsync();
            await page.GetByRole(AriaRole.Button, new() { Name = "Files", Exact = true }).ClickAsync();
            await page.GetByTestId("file-application-extension").WaitForAsync();
            holding = SeedAsync(async services => {
                var database = services.GetRequiredService<SimpleChatsDbContext>();
                await using var transaction = await database.Database.BeginTransactionAsync();
                await database.Database.ExecuteSqlRawAsync("LOCK TABLE \"LlmChats_Definitions\" IN ACCESS EXCLUSIVE MODE");
                locked.TrySetResult();
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                while (!release.Task.IsCompleted) {
                    var waiting = await database.Database.SqlQueryRaw<int>(
                        "SELECT count(*)::integer AS \"Value\" FROM pg_locks WHERE relation = '\"LlmChats_Definitions\"'::regclass AND NOT granted AND mode = 'AccessShareLock'")
                        .SingleAsync(timeout.Token);
                    if (waiting > 0) {
                        queryHeld.TrySetResult();
                    }
                    await Task.Delay(100, timeout.Token);
                }
                await transaction.RollbackAsync();
                return true;
            });
            await locked.Task.WaitAsync(TimeSpan.FromSeconds(15));
            await NavigateAsync("/agents?tab=simple-chats&simpleChatView=definitions");
            await queryHeld.Task.WaitAsync(TimeSpan.FromSeconds(15));
            await NavigateAsync("/collaboration");
            await page.GetByTestId("collaboration-thread-subject").WaitForAsync();
            release.TrySetResult();
            Assert.True(await holding);

            for (var lap = 0; lap < 2; lap++) {
                await NavigateAsync("/settings");
                await page.GetByTestId("defaults-name").WaitForAsync();
                await page.GetByRole(AriaRole.Button, new() { Name = "API Access", Exact = false }).ClickAsync();
                await page.GetByTestId("api-scopes-open").ClickAsync();
                await page.GetByTestId("api-scopes-dialog").WaitForAsync();
                await page.Keyboard.PressAsync("Escape");
                await Assertions.Expect(page.GetByTestId("api-scopes-dialog")).ToHaveCountAsync(0);
                await page.GetByTestId("api-access-retry").ClickAsync();
                await page.GetByTestId("api-token-subject").WaitForAsync();
                await page.GetByRole(AriaRole.Button, new() { Name = "Storage", Exact = true }).ClickAsync();
                await Assertions.Expect(page.GetByText("Storage catalog", new() { Exact = true }).First).ToBeVisibleAsync();
                await page.GetByTestId("storage-settings-recovery").ClickAsync();
                await page.GetByTestId("storage-recovery-dialog").WaitForAsync();
                await page.GetByTestId("storage-recovery-close").ClickAsync();
                await Assertions.Expect(page.GetByTestId("storage-recovery-dialog")).ToHaveCountAsync(0);
                await page.GetByRole(AriaRole.Button, new() { Name = "Data Sources", Exact = true }).ClickAsync();
                await page.GetByTestId("database-data-sources-summary").WaitForAsync();
                await page.GetByTestId($"database-profile-row-{host.ProfileA:N}").ClickAsync();
                await page.GetByTestId("database-profile-transfer-settings").ClickAsync();
                await page.GetByTestId("database-transfer-dialog").WaitForAsync();
                await page.Keyboard.PressAsync("Escape");
                await Assertions.Expect(page.GetByTestId("database-transfer-dialog")).ToHaveCountAsync(0);
                await NavigateAsync("/agents");
                await page.GetByTestId("agents-overview-open-provider-usage").ClickAsync();
                await page.GetByTestId("provider-usage-dialog").WaitForAsync();
                await page.Keyboard.PressAsync("Escape");
                await Assertions.Expect(page.GetByTestId("provider-usage-dialog")).ToHaveCountAsync(0);
                await page.GetByTestId("agents-overview-open-provider-usage").ClickAsync();
                await page.GetByTestId("provider-usage-dialog").WaitForAsync();
                await page.ScreenshotAsync(new() { Path = Artifact($"shared-dialog-{lap}.png") });
                await NavigateAsync("/agents?tab=simple-chats&simpleChatView=definitions");
                await page.GetByTestId("llm-chat-definition-catalog").WaitForAsync();
                await NavigateAsync("/collaboration");
                await page.GetByTestId("collaboration-thread-subject").WaitForAsync();
                await NavigateAsync("/test-lab");
                await page.GetByTestId("testlab-title-input").WaitForAsync();
                await page.EvaluateAsync("() => history.back()");
                await page.GetByTestId("collaboration-thread-subject").WaitForAsync();
                await page.EvaluateAsync("() => history.forward()");
                await page.GetByTestId("testlab-title-input").WaitForAsync();
                await NavigateAsync($"/projects/{host.ProjectA:D}/structure");
                await page.GetByTestId("project-structure-canvas-loaded").WaitForAsync(new() { Timeout = 60_000, State = WaitForSelectorState.Attached });
                await page.WaitForFunctionAsync("() => Array.from(document.querySelectorAll('.cw-canvas-host')).some(host => !!host.__canvasWorkbenchState)");
                await SelectFileNodeAsync(page, asset.Id, "Existing nonce asset");
                await page.GetByTestId("project-structure-selection-window").GetByRole(AriaRole.Button, new() { Name = "Expand preview", Exact = true }).ClickAsync();
                var preview = page.GetByRole(AriaRole.Dialog, new() { Name = "seed.txt file interaction", Exact = true });
                await Assertions.Expect(preview).ToContainTextAsync(seedContent);
                await NavigateAsync("/settings");
                await page.GetByTestId("defaults-name").WaitForAsync();
            }

            var independent = await page.Context.NewPageAsync();
            var independentOracle = CrmHrBrowserOracle.Attach(independent);
            await independentOracle.NavigateAsync(host.BaseUrl + "/settings");
            await independent.GetByRole(AriaRole.Tablist, new() { Name = "Open workspace tabs", Exact = true }).WaitForAsync();
            await independent.GetByTestId("defaults-name").WaitForAsync();
            await oracle.AssertCleanAsync();
            await page.CloseAsync();
            await independent.GetByRole(AriaRole.Button, new() { Name = "Files", Exact = true }).ClickAsync();
            await independent.GetByTestId("file-application-extension").WaitForAsync();
            await independentOracle.NavigateAsync(host.BaseUrl + "/settings");
            await independent.GetByTestId("defaults-name").WaitForAsync();
            await independent.ScreenshotAsync(new() { Path = Artifact("shared-successor.png") });
            await independentOracle.AssertCleanAsync();
            await host.CaptureLogAsync();
            var log = host.LogSnapshot;
            Assert.DoesNotContain("ObjectDisposedException", log, StringComparison.Ordinal);
            Assert.DoesNotContain("Unhandled exception", log, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("failed to initialize", log, StringComparison.OrdinalIgnoreCase);

            Task NavigateAsync(string path) => page.EvaluateAsync("path => Blazor.navigateTo(path)", path);
        } finally {
            release.TrySetResult();
            if (holding is not null) {
                await holding;
            }
            await host.CaptureLogAsync();
        }

        string Artifact(string name) => Path.Combine(host.ArtifactDirectory, name);
        async Task<T> SeedAsync<T>(Func<IServiceProvider, Task<T>> operation) {
            await using var scope = host.Services.CreateAsyncScope();
            return await operation(scope.ServiceProvider);
        }
    }
}
