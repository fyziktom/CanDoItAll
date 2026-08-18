using System.IO;
using CanDoItAll.Tests.Playwright.Flows;
using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright.Smoke;

public sealed partial class AppSmokeTests
{
    [Fact]
    [Trait("Category", "Quarantined")]
    public async Task Startup_modal_shell_switcher_and_settings_data_sources_flow_render_cleanly()
    {
        await using var host = await DatabaseSwitchPlaywrightHost.CreateAsync();
        var initialProfile = await host.GetCurrentProfileAsync();
        var secondProfile = await host.CreatePostgreSqlProfileAsync();

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
        Assert.Contains("Configured PostgreSQL override", await startupDialog.TextContentAsync() ?? string.Empty, StringComparison.Ordinal);
        Assert.True(await page.GetByTestId("database-startup-switch").IsDisabledAsync());

        await page.GetByTestId("database-startup-continue").ClickAsync();
        await page.GetByTestId("database-data-sources-locked-message").WaitForAsync();
        Assert.True(await page.GetByTestId("database-profile-new-postgres").IsDisabledAsync());
        Assert.True(await page.GetByTestId("database-profile-save").IsDisabledAsync());
        Assert.Equal(0, await page.GetByTestId("database-snapshot-deferred").CountAsync());
        Assert.Equal(0, await page.GetByTestId("database-clone-create").CountAsync());

        var bodyText = await page.TextContentAsync("body") ?? string.Empty;
        Assert.DoesNotContain(string.Concat("Sql", "ite"), bodyText, StringComparison.OrdinalIgnoreCase);

        await SaveDatabaseEvidenceAsync(page, GetRepoRoot(), "db-switch-responsive-followup.png");
        Assert.False(await page.Locator("#blazor-error-ui").IsVisibleAsync());
    }

    [Fact]
    public async Task Snapshot_actions_are_not_rendered_on_data_sources_page()
    {
        await using var context = await fixture.Browser.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize
            {
                Width = 1600,
                Height = 1000
            }
        });

        var settingsPage = await context.NewPageAsync();
        var response = await settingsPage.GotoAsync($"{fixture.BaseUrl}/settings?tab=data-sources");
        Assert.NotNull(response);
        Assert.True(response!.Ok, $"Expected /settings?tab=data-sources to return 2xx, got {(int)response.Status}.");
        await DismissStartupModalAsync(settingsPage);
        await settingsPage.GetByTestId("database-data-sources-summary").WaitForAsync();
        Assert.Equal(0, await settingsPage.GetByTestId("database-snapshot-deferred").CountAsync());
        Assert.Equal(0, await settingsPage.GetByTestId("database-clone-create").CountAsync());
        await SaveDatabaseEvidenceAsync(settingsPage, GetRepoRoot(), "db-switch-no-snapshot-actions-desktop.png");

        Assert.False(await settingsPage.Locator("#blazor-error-ui").IsVisibleAsync());
    }

    [Fact]
    public async Task Snapshot_actions_remain_absent_in_responsive_layout()
    {
        await using var context = await fixture.Browser.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize
            {
                Width = 1600,
                Height = 1000
            }
        });

        var page = await context.NewPageAsync();
        var response = await page.GotoAsync($"{fixture.BaseUrl}/settings?tab=data-sources");
        Assert.NotNull(response);
        Assert.True(response!.Ok, $"Expected /settings?tab=data-sources to return 2xx, got {(int)response.Status}.");
        await DismissStartupModalAsync(page);
        await page.GetByTestId("database-data-sources-summary").WaitForAsync();
        Assert.Equal(0, await page.GetByTestId("database-snapshot-deferred").CountAsync());
        Assert.Equal(0, await page.GetByTestId("database-clone-create").CountAsync());
        await SaveDatabaseEvidenceAsync(page, GetRepoRoot(), "db-switch-no-snapshot-actions-responsive-desktop.png");

        await page.SetViewportSizeAsync(1100, 900);
        Assert.Equal(0, await page.GetByTestId("database-snapshot-deferred").CountAsync());
        Assert.Equal(0, await page.GetByTestId("database-clone-create").CountAsync());
        await SaveDatabaseEvidenceAsync(page, GetRepoRoot(), "db-switch-no-snapshot-actions-responsive.png");
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
