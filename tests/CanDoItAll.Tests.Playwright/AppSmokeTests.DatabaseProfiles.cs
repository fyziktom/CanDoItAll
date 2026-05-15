using System.IO;
using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright;

public sealed partial class AppSmokeTests
{
    [Fact]
    [Trait("Category", "Quarantined")]
    public async Task Startup_modal_shell_switcher_and_settings_data_sources_flow_render_cleanly()
    {
        await using var host = await DatabaseSwitchPlaywrightHost.CreateAsync();
        var initialProfile = await host.GetCurrentProfileAsync();
        var secondProfile = await host.CreateManagedSqliteProfileAsync();

        await using var browser = await host.Playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true
        });
        await using var context = await browser.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize
            {
                Width = 1600,
                Height = 1000
            }
        });

        var page = await context.NewPageAsync();
        var response = await page.GotoAsync($"{host.BaseUrl}/");
        Assert.NotNull(response);
        Assert.True(response!.Ok, $"Expected / to return 2xx, got {(int)response.Status}.");

        var startupDialog = page.GetByTestId("database-startup-modal");
        await startupDialog.WaitForAsync();
        await page.GetByTestId("database-dialog-current-selection").WaitForAsync();
        Assert.Contains(initialProfile.DisplayName, await startupDialog.TextContentAsync() ?? string.Empty, StringComparison.Ordinal);
        await SaveDatabaseEvidenceAsync(page, host.RepoRoot, "db-switch-startup-modal-desktop.png");

        await page.GetByTestId("database-startup-continue").ClickAsync();
        await startupDialog.WaitForAsync(new() { State = WaitForSelectorState.Detached });

        await page.GetByTestId("database-shell-action").ClickAsync();
        var switcherDialog = page.GetByTestId("database-switcher-dialog");
        await switcherDialog.WaitForAsync();
        await page.GetByTestId($"database-dialog-profile-row-{secondProfile.Id:N}").ClickAsync();
        await SaveDatabaseEvidenceAsync(page, host.RepoRoot, "db-switch-shell-switcher-desktop.png");

        await Task.WhenAll(
            page.WaitForURLAsync("**/projects", new() { Timeout = 20_000 }),
            page.GetByTestId("database-startup-switch").ClickAsync());
        await page.GetByTestId("database-switch-alert").WaitForAsync();

        await page.GetByTestId("database-shell-action").HoverAsync();
        var activeDatabaseText = await page.GetByTestId("database-shell-flyout-card").TextContentAsync();
        Assert.Contains(secondProfile.DisplayName, activeDatabaseText ?? string.Empty, StringComparison.Ordinal);

        response = await page.GotoAsync($"{host.BaseUrl}/settings?tab=data-sources");
        Assert.NotNull(response);
        Assert.True(response!.Ok, $"Expected /settings?tab=data-sources to return 2xx, got {(int)response.Status}.");
        await page.GetByTestId("database-data-sources-summary").WaitForAsync();
        await page.GetByTestId("database-profile-new-postgres").ClickAsync();
        await page.GetByTestId("database-profile-postgres-host").WaitForAsync();

        await page.GetByTestId("database-profile-name").FillAsync("Browser PostgreSQL");
        await page.GetByTestId("database-profile-workspace-root").FillAsync(Path.Combine(host.TestEnvironment.RootPath, "browser-postgres-workspace"));
        await page.GetByTestId("database-profile-postgres-host").FillAsync("db.internal");
        await page.GetByTestId("database-profile-postgres-port").FillAsync("5432");
        await page.GetByTestId("database-profile-postgres-database").FillAsync("candoitall_browser");
        await page.GetByTestId("database-profile-postgres-user").FillAsync("postgres");
        await page.GetByTestId("database-profile-postgres-password").FillAsync("browser-secret");
        await page.GetByTestId("database-profile-save").ClickAsync();

        await page.GetByTestId("database-profile-test-connection").WaitForAsync();
        await page.GetByTestId("database-profile-create-empty").WaitForAsync();
        Assert.Contains("Browser PostgreSQL", await page.TextContentAsync("body") ?? string.Empty, StringComparison.Ordinal);
        await SaveDatabaseEvidenceAsync(page, host.RepoRoot, "db-switch-settings-data-sources-desktop.png");
        Assert.False(await page.Locator("#blazor-error-ui").IsVisibleAsync());
    }

    [Fact]
    public async Task Settings_data_sources_locked_mode_is_visible_in_responsive_layout()
    {
        await using var context = await fixture.Browser.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize
            {
                Width = 1100,
                Height = 900
            }
        });

        var page = await context.NewPageAsync();
        var response = await page.GotoAsync($"{fixture.BaseUrl}/settings?tab=data-sources");
        Assert.NotNull(response);
        Assert.True(response!.Ok, $"Expected /settings?tab=data-sources to return 2xx, got {(int)response.Status}.");

        var startupDialog = page.GetByTestId("database-startup-modal");
        await startupDialog.WaitForAsync();
        Assert.Contains("Configured SQLite override", await startupDialog.TextContentAsync() ?? string.Empty, StringComparison.Ordinal);
        Assert.True(await page.GetByTestId("database-startup-create-managed").IsDisabledAsync());
        Assert.True(await page.GetByTestId("database-startup-switch").IsDisabledAsync());

        await page.GetByTestId("database-startup-continue").ClickAsync();
        await page.GetByTestId("database-data-sources-locked-message").WaitForAsync();
        Assert.True(await page.GetByTestId("database-profile-new-managed").IsDisabledAsync());
        Assert.True(await page.GetByTestId("database-profile-save").IsDisabledAsync());

        await SaveDatabaseEvidenceAsync(page, GetRepoRoot(), "db-switch-responsive-followup.png");
        Assert.False(await page.Locator("#blazor-error-ui").IsVisibleAsync());
    }

    [Fact]
    public async Task Clone_activate_and_cross_tab_switch_flow_render_cleanly()
    {
        await using var host = await DatabaseSwitchPlaywrightHost.CreateAsync();
        var sourceProfile = await host.GetCurrentProfileAsync();
        var sourceSeed = await host.SeedCurrentProfileAsync("Browser Alpha");

        await using var browser = await host.Playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true
        });
        await using var context = await browser.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize
            {
                Width = 1600,
                Height = 1000
            }
        });

        var settingsPage = await context.NewPageAsync();
        var response = await settingsPage.GotoAsync($"{host.BaseUrl}/settings?tab=data-sources");
        Assert.NotNull(response);
        Assert.True(response!.Ok, $"Expected /settings?tab=data-sources to return 2xx, got {(int)response.Status}.");
        await DismissStartupModalAsync(settingsPage);
        await settingsPage.GetByTestId("database-snapshot-source-summary").WaitForAsync();

        var projectsPage = await context.NewPageAsync();
        response = await projectsPage.GotoAsync($"{host.BaseUrl}/projects");
        Assert.NotNull(response);
        Assert.True(response!.Ok, $"Expected /projects to return 2xx, got {(int)response.Status}.");
        await DismissStartupModalAsync(projectsPage);
        await projectsPage.GetByTestId("projects-new-button").WaitForAsync();
        await WaitForBodyTextAsync(projectsPage, sourceSeed.ProjectName);

        await settingsPage.GetByTestId("database-clone-name").FillAsync("Browser Alpha Clone");
        await settingsPage.GetByTestId("database-clone-create").ClickAsync();
        await WaitForBodyTextAsync(settingsPage, "Browser Alpha Clone");
        await SaveDatabaseEvidenceAsync(settingsPage, host.RepoRoot, "db-switch-clone-flow-desktop.png");

        await Task.WhenAll(
            settingsPage.GetByTestId("database-switch-alert").WaitForAsync(),
            projectsPage.GetByTestId("database-switch-alert").WaitForAsync(),
            settingsPage.GetByTestId("database-profile-activate").ClickAsync());

        var cloneSeed = await host.SeedCurrentProfileAsync("Browser Clone");
        await projectsPage.ReloadAsync();
        await WaitForBodyTextAsync(projectsPage, cloneSeed.ProjectName);

        await host.SwitchAsync(sourceProfile.Id);

        await projectsPage.ReloadAsync();
        await WaitForBodyTextAsync(projectsPage, sourceSeed.ProjectName);
        Assert.DoesNotContain(cloneSeed.ProjectName, await projectsPage.TextContentAsync("body") ?? string.Empty, StringComparison.Ordinal);
        await SaveDatabaseEvidenceAsync(projectsPage, host.RepoRoot, "db-switch-cross-tab-desktop.png");

        Assert.False(await settingsPage.Locator("#blazor-error-ui").IsVisibleAsync());
        Assert.False(await projectsPage.Locator("#blazor-error-ui").IsVisibleAsync());
    }

    [Fact]
    public async Task Snapshot_local_and_ipfs_flow_render_cleanly()
    {
        await using var host = await DatabaseSwitchPlaywrightHost.CreateAsync(enableIpfs: true);
        Assert.NotNull(host.FakeIpfsServer);
        await host.SeedCurrentProfileAsync("Browser Snapshot");

        await using var browser = await host.Playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true
        });
        await using var context = await browser.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize
            {
                Width = 1600,
                Height = 1000
            }
        });

        var page = await context.NewPageAsync();
        var response = await page.GotoAsync($"{host.BaseUrl}/settings?tab=data-sources");
        Assert.NotNull(response);
        Assert.True(response!.Ok, $"Expected /settings?tab=data-sources to return 2xx, got {(int)response.Status}.");
        await DismissStartupModalAsync(page);
        await page.GetByTestId("database-snapshot-source-summary").WaitForAsync();

        await page.GetByTestId("database-snapshot-local-create").ClickAsync();
        await page.GetByTestId("database-snapshot-latest").WaitForAsync();
        var packagePath = await page.GetByTestId("database-snapshot-package-path").InputValueAsync();
        Assert.False(string.IsNullOrWhiteSpace(packagePath));

        await page.GetByTestId("database-snapshot-ipfs-create").ClickAsync();
        var cid = await WaitForNonEmptyInputAsync(page.GetByTestId("database-snapshot-cid"));
        Assert.False(string.IsNullOrWhiteSpace(cid));
        Assert.Contains(cid, host.FakeIpfsServer!.StoredCids);
        Assert.Contains(cid, host.FakeIpfsServer.PinnedCids);

        await page.GetByTestId("database-snapshot-profile-name").FillAsync("Browser IPFS Restore");
        await page.GetByTestId("database-snapshot-ipfs-restore").ClickAsync();
        await WaitForBodyTextAsync(page, "Browser IPFS Restore");
        await SaveDatabaseEvidenceAsync(page, host.RepoRoot, "db-switch-snapshot-ipfs-desktop.png");

        await page.SetViewportSizeAsync(1100, 900);
        await SaveDatabaseEvidenceAsync(page, host.RepoRoot, "db-switch-final-responsive.png");
        Assert.False(await page.Locator("#blazor-error-ui").IsVisibleAsync());
    }

    private static async Task SaveDatabaseEvidenceAsync(IPage page, string repoRoot, string fileName)
    {
        var evidenceRoot = Path.Combine(repoRoot, "evidence");
        Directory.CreateDirectory(evidenceRoot);
        await page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = Path.Combine(evidenceRoot, fileName),
            FullPage = true
        });
    }

    private static async Task DismissStartupModalAsync(IPage page)
    {
        var startupDialog = page.GetByTestId("database-startup-modal");
        await startupDialog.WaitForAsync();
        await page.GetByTestId("database-startup-continue").ClickAsync();
        await startupDialog.WaitForAsync(new() { State = WaitForSelectorState.Detached });
    }

    private static async Task DismissStartupModalIfPresentAsync(IPage page, float timeoutMs = 5_000)
    {
        var startupDialog = page.GetByTestId("database-startup-modal");
        if (!await WaitForLocatorAsync(startupDialog, timeoutMs))
        {
            return;
        }

        await page.GetByTestId("database-startup-continue").ClickAsync();
        await startupDialog.WaitForAsync(new() { State = WaitForSelectorState.Detached });
    }

    private static async Task WaitForBodyTextAsync(IPage page, string text, int timeoutMs = 10_000)
    {
        var timeoutAt = DateTimeOffset.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTimeOffset.UtcNow < timeoutAt)
        {
            if ((await page.TextContentAsync("body"))?.Contains(text, StringComparison.Ordinal) == true)
            {
                return;
            }

            await Task.Delay(200);
        }

        throw new TimeoutException($"Timed out waiting for '{text}' to appear in the page body.");
    }

    private static async Task<string> WaitForNonEmptyInputAsync(ILocator locator, int timeoutMs = 10_000)
    {
        var timeoutAt = DateTimeOffset.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTimeOffset.UtcNow < timeoutAt)
        {
            var value = await locator.InputValueAsync();
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }

            await Task.Delay(200);
        }

        throw new TimeoutException("Timed out waiting for a non-empty input value.");
    }
}
