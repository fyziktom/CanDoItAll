using Microsoft.Playwright;
using Npgsql;

namespace CanDoItAll.Tests.Playwright.Visual;

[Collection(PlaywrightCollection.Name)]
public sealed class DashboardOperationalSnapshotPlaywrightTests
{
    private readonly PlaywrightAppFixture fixture;

    public DashboardOperationalSnapshotPlaywrightTests(PlaywrightAppFixture fixture)
    {
        this.fixture = fixture;
    }

    [Fact]
    public async Task Empty_snapshot_and_failed_refresh_remain_honest_in_the_real_app()
    {
        await using var context = await fixture.Browser.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize
            {
                Width = 1440,
                Height = 900
            }
        });
        var page = await context.NewPageAsync();
        var consoleErrors = new List<string>();
        page.Console += (_, message) =>
        {
            if (string.Equals(message.Type, "error", StringComparison.OrdinalIgnoreCase))
            {
                consoleErrors.Add(message.Text);
            }
        };

        var response = await page.GotoAsync($"{fixture.BaseUrl}/dashboard");
        Assert.NotNull(response);
        Assert.True(response!.Ok, $"Expected /dashboard to return 2xx, got {(int)response.Status}.");

        await page.GetByTestId("database-startup-continue").ClickAsync();
        await page.GetByText("No projects yet", new PageGetByTextOptions { Exact = true }).WaitForAsync();
        await page.GetByText("No recent workflow runs", new PageGetByTextOptions { Exact = true }).WaitForAsync();

        await page.GetByTestId("dashboard-processes-tab").ClickAsync();
        await page.GetByText("No recent process runs", new PageGetByTextOptions { Exact = true }).WaitForAsync();
        await page.GetByTestId("dashboard-workflows-tab").ClickAsync();

        Assert.Equal(0, await page.Locator("[data-testid^='dashboard-project-']").CountAsync());
        Assert.Equal(0, await page.Locator("[data-testid^='dashboard-workflow-']").CountAsync());
        Assert.Equal(0, await page.Locator("[role='dialog']:visible").CountAsync());
        await CaptureAsync(page, "home-dashboard-1440x900-empty.png");

        await DropIsolatedDatabaseAsync();
        await page.GetByTestId("dashboard-refresh").ClickAsync();

        var staleWarning = page.GetByTestId("dashboard-stale-warning");
        await staleWarning.WaitForAsync(new LocatorWaitForOptions { Timeout = 30_000 });

        var staleText = await staleWarning.InnerTextAsync();
        Assert.Contains("Showing the last successful snapshot", staleText, StringComparison.Ordinal);
        Assert.Contains("Refresh failed", staleText, StringComparison.Ordinal);
        Assert.Equal(0, await page.Locator("[data-testid='dashboard-load-error']").CountAsync());
        Assert.True(await page.GetByText("No projects yet", new PageGetByTextOptions { Exact = true }).IsVisibleAsync());
        Assert.Empty(consoleErrors);

        await CaptureAsync(page, "home-dashboard-1440x900-refresh-error.png");
    }

    private static async Task CaptureAsync(IPage page, string fileName)
    {
        var evidenceDirectory = Path.Combine(
            PlaywrightTestHostPaths.RepositoryRoot,
            "codex",
            "bundles",
            "candoitall-main-dashboard-operational-snapshot",
            "evidence",
            "Dashboard acceptance");
        Directory.CreateDirectory(evidenceDirectory);
        await page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = Path.Combine(evidenceDirectory, fileName),
            FullPage = true
        });
    }

    private async Task DropIsolatedDatabaseAsync()
    {
        var connectionString = fixture.DatabaseConnectionString ?? throw new InvalidOperationException(
            "The dashboard Playwright fixture requires its isolated PostgreSQL profile.");
        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        var databaseName = string.IsNullOrWhiteSpace(builder.Database)
            ? throw new InvalidOperationException("The isolated PostgreSQL profile has no database name.")
            : builder.Database;
        builder.Database = "postgres";

        await using var connection = new NpgsqlConnection(builder.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"""drop database if exists "{EscapeIdentifier(databaseName)}" with (force);""";
        await command.ExecuteNonQueryAsync();
    }

    private static string EscapeIdentifier(string value)
        => value.Replace("\"", "\"\"", StringComparison.Ordinal);
}
