using System.Text.Json;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Modules.Workspace;
using CanDoItAll.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;
using Npgsql;

namespace CanDoItAll.Tests.Playwright;

[Trait("Category", "Playwright")]
[Trait("Category", "HostPlatform")]
public sealed class ApplicationClosureBrowserTests {
    private static string Evidence(string name) {
        var root = Path.Combine(PlaywrightTestHostPaths.RepositoryRoot, "output", "playwright", "components-decoupling-closure");
        Directory.CreateDirectory(root);
        return Path.Combine(root, name);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Independent_application_controls_children_clipboard_and_geometry_survive_publication(bool published) {
        await using var host = new RazorSandboxHost(RazorSandboxTarget.Application);
        await host.StartAsync(published);
        using var playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
        await using var context = await browser.NewContextAsync(new() {
            ViewportSize = new() { Width = 1920, Height = 1080 }, Permissions = ["clipboard-read", "clipboard-write"]
        });
        var page = await context.NewPageAsync();
        var errors = new List<string>();
        var failedAssets = new List<string>();
        page.PageError += (_, error) => errors.Add(error);
        page.Response += (_, response) => {
            if (response.Status >= 400 && (response.Url.Contains(".css", StringComparison.Ordinal) || response.Url.Contains(".js", StringComparison.Ordinal) || response.Url.Contains(".woff", StringComparison.Ordinal))) {
                failedAssets.Add(response.Url);
            }
        };
        var mode = published ? "published" : "source";
        try {
            await page.GotoAsync(host.BaseUrl + "/dashboard");
            foreach (var state in new[] { "Loading", "Empty", "Populated", "PartialPricing", "Stale", "Failure" }) {
                await page.GetByRole(AriaRole.Button, new() { Name = state, Exact = true }).ClickAsync();
                switch (state) {
                    case "Loading":
                        await Assertions.Expect(page.GetByText("Loading operational snapshot", new() { Exact = true })).ToBeVisibleAsync();
                        break;
                    case "Empty":
                        await Assertions.Expect(page.GetByText("No projects yet", new() { Exact = true })).ToBeVisibleAsync();
                        break;
                    case "Stale":
                        await Assertions.Expect(page.GetByTestId("dashboard-stale-warning")).ToBeVisibleAsync();
                        await page.ScreenshotAsync(new() { Path = Evidence(mode + "-dashboard-stale.png") });
                        break;
                    case "Failure":
                        await page.GetByTestId("dashboard-retry").ClickAsync();
                        await Assertions.Expect(page.GetByTestId("dashboard-load-error")).ToHaveCountAsync(0);
                        break;
                    default:
                        await Assertions.Expect(page.GetByTestId("dashboard-project-11111111111111111111111111111111")).ToHaveAttributeAsync("href", "/projects?projectId=11111111-1111-1111-1111-111111111111");
                        break;
                }
            }
            await page.GetByTestId("dashboard-processes-tab").ClickAsync();
            await Assertions.Expect(page.GetByTestId("dashboard-process-44444444444444444444444444444444")).ToBeVisibleAsync();
            await page.GetByTestId("dashboard-workflows-tab").ClickAsync();
            await Assertions.Expect(page.GetByTestId("dashboard-workflow-33333333333333333333333333333333")).ToBeVisibleAsync();
            await page.GotoAsync(host.BaseUrl + "/runtime");
            await Assertions.Expect(page.GetByTestId("runtime-capability-terminal")).ToContainTextAsync("Platform Unsupported");
            await Assertions.Expect(page.GetByTestId("runtime-capability-vault")).ToContainTextAsync("Actual Host Unverified");
            await page.GetByTestId("runtime-capabilities-refresh").ClickAsync();
            await Assertions.Expect(page.GetByTestId("runtime-scenario-refreshes")).ToHaveTextAsync("Scenario refreshes: 1");
            foreach (var state in new[] { "Startup", "Active", "Selected", "Pending", "Locked", "Busy", "Error", "Empty" }) {
                await page.GotoAsync(host.BaseUrl + "/profiles");
                await page.GetByTestId("profile-scenario-" + state).ClickAsync();
                var dialog = page.GetByTestId(state == "Startup" ? "database-startup-modal" : "database-switcher-dialog");
                await Assertions.Expect(dialog).ToBeVisibleAsync();
                if (state is "Active" or "Pending" or "Locked" or "Busy" or "Empty") {
                    await Assertions.Expect(page.GetByTestId("database-startup-switch")).ToBeDisabledAsync();
                }
                if (state == "Selected") {
                    await page.GetByTestId("database-dialog-copy-active-target").ClickAsync();
                    await Assertions.Expect(page.GetByTestId("database-dialog-copy-active-target")).ToHaveAttributeAsync("data-copy-state", "copied");
                    var copied = await page.EvaluateAsync<string>("() => navigator.clipboard.readText()");
                    Assert.Equal((await page.GetByTestId("database-dialog-active-target").InnerTextAsync()).Trim(), copied);
                    await page.ScreenshotAsync(new() { Path = Evidence(mode + "-profiles-desktop.png") });
                    await page.GetByTestId("database-startup-switch").ClickAsync();
                    await Assertions.Expect(page.GetByTestId("database-dialog-message")).ToContainTextAsync("Scenario activation saved");
                }
                Assert.True(await dialog.EvaluateAsync<bool>("element => { const r = element.getBoundingClientRect(); return r.left >= 0 && r.right <= innerWidth && r.top >= 0 && r.bottom <= innerHeight; }"));
            }
            await page.GotoAsync(host.BaseUrl + "/profiles");
            await page.GetByRole(AriaRole.Button, new() { Name = "Workspace status", Exact = true }).ClickAsync();
            await Assertions.Expect(page.GetByTestId("layout-status-popover")).ToBeVisibleAsync();
            await page.Keyboard.PressAsync("Escape");
            await page.GetByTestId("database-shell-action").HoverAsync();
            await Assertions.Expect(page.GetByTestId("database-shell-flyout-card")).ToBeVisibleAsync();
            await page.GetByTestId("database-shell-action").ClickAsync();
            await Assertions.Expect(page.GetByTestId("database-switcher-dialog")).ToBeVisibleAsync();
            await page.SetViewportSizeAsync(1100, 760);
            await page.ScreenshotAsync(new() { Path = Evidence(mode + "-profiles-constrained.png") });
            Assert.True(await page.EvaluateAsync<bool>("() => document.documentElement.scrollWidth <= innerWidth"));
            await page.GetByTestId("database-switcher-dialog").GetByLabel("Close", new() { Exact = true }).FocusAsync();
            await page.Keyboard.PressAsync("Tab");
            Assert.True(await page.EvaluateAsync<bool>("() => !!document.activeElement?.closest('dialog')"));
            await page.Keyboard.PressAsync("Escape");
            await Assertions.Expect(page.GetByTestId("database-switcher-dialog")).ToHaveCountAsync(0);
            Assert.Empty(errors);
            Assert.Empty(failedAssets);
        } finally {
            await File.WriteAllTextAsync(Evidence(mode + "-browser.json"), JsonSerializer.Serialize(new { published, errors, failedAssets }));
            await page.ScreenshotAsync(new() { Path = Evidence(mode + "-last.png") });
        }
    }

    [Fact]
    public async Task Native_shell_activation_readback_and_owned_process_restart_preserve_runtime_authority() {
        await using var host = new DataSourcesBrowserHost();
        await host.StartAsync();
        var profiles = host.Services.GetRequiredService<IDatabaseProfileService>();
        var targetEditor = TestDatabaseProfileEditorFactory.CreatePostgreSqlEditor(host.B, "Owned workspace B");
        targetEditor.PostgresAdminDatabaseName = "postgres";
        var saved = await profiles.SaveAsync(targetEditor);
        Assert.True(saved.IsSuccess);
        var profileB = saved.Value;
        await using (var scope = host.Services.CreateAsyncScope()) {
            Assert.True((await scope.ServiceProvider.GetRequiredService<DatabaseProfileWorkspaceService>().ApplySchemaAsync(profileB)).IsSuccess);
        }
        using var playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
        await using var context = await browser.NewContextAsync(new() { ViewportSize = new() { Width = 1920, Height = 1080 }, Permissions = ["clipboard-read", "clipboard-write"] });
        var page = await context.NewPageAsync();
        await page.GotoAsync(host.BaseUrl + "/dashboard");
        await Assertions.Expect(page.GetByTestId($"dashboard-project-{host.ProjectA:N}")).ToBeVisibleAsync();
        await page.GetByTestId("database-shell-action").HoverAsync();
        await Assertions.Expect(page.GetByTestId("database-shell-flyout-card")).ToContainTextAsync("Owned workspace A");
        await page.GetByTestId("shell-runtime-capabilities-action").ClickAsync();
        await Assertions.Expect(page.GetByTestId("runtime-readiness-summary")).ToBeVisibleAsync();
        await page.GetByTestId("runtime-capabilities-refresh").ClickAsync();
        await page.GetByTestId("database-shell-action").ClickAsync();
        await page.GetByTestId($"database-dialog-profile-row-{profileB:N}").ClickAsync();
        await page.GetByTestId("database-startup-switch").ClickAsync();
        await Assertions.Expect(page.GetByTestId("database-dialog-message")).ToContainTextAsync("Restart");
        var before = await host.ReadRuntimeAsync();
        Assert.Equal(host.ProfileA, before.RuntimeProfileId);
        Assert.Equal(profileB, before.PendingRestartProfileId);
        Assert.Equal(profileB, (await profiles.GetCurrentSelectionAsync()).ActiveProfileId);
        var password = new NpgsqlConnectionStringBuilder(host.B.ConnectionString).Password!;
        Assert.False((await page.ContentAsync()).Contains(password, StringComparison.Ordinal));
        await page.GetByTestId("database-dialog-copy-active-target").ClickAsync();
        await Assertions.Expect(page.GetByTestId("database-dialog-copy-active-target")).ToHaveAttributeAsync("data-copy-state", "copied");
        var copied = await page.EvaluateAsync<string>("() => navigator.clipboard.readText()");
        Assert.Equal((await page.GetByTestId("database-dialog-active-target").InnerTextAsync()).Trim(), copied);
        Assert.False(copied.Contains(password, StringComparison.Ordinal));
        await page.ScreenshotAsync(new() { Path = Evidence("native-pending-restart.png") });
        await host.RestartAsync();
        var after = await host.ReadRuntimeAsync();
        Assert.Equal(profileB, after.RuntimeProfileId);
        Assert.Null(after.PendingRestartProfileId);
        await page.GotoAsync(host.BaseUrl + "/dashboard");
        await Assertions.Expect(page.GetByText("No projects yet", new() { Exact = true })).ToBeVisibleAsync();
        Assert.False(host.LogSnapshot.Contains(password, StringComparison.Ordinal));
        await File.WriteAllTextAsync(Evidence("native-profile-readback.json"), JsonSerializer.Serialize(new { before, after, activationClicked = true, processRestarted = true }));
    }
}
