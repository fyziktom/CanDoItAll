using CanDoItAll.Tests.Playwright.StorageCatalog;
using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright;

[Trait("Category", "Playwright")]
[Trait("Category", "HostPlatform")]
public sealed class WorkspaceApiDenialBrowserTests {
    [Fact]
    public async Task Both_current_list_denials_retire_management_and_allow_explicit_retry_and_navigation() {
        await using var host = new StorageCatalogBrowserHost(sandbox: false);
        await host.ReadyAsync(sandbox: false);
        using var playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
        await using var context = await browser.NewContextAsync(new() { ViewportSize = new() { Width = 1600, Height = 1000 } });
        var page = await context.NewPageAsync();
        var errors = new List<string>();
        page.PageError += (_, error) => errors.Add(error);
        page.Console += (_, message) => {
            if (message.Type == "error") {
                errors.Add(message.Text);
            }
        };
        await page.GotoAsync(host.BaseUrl + "/settings?tab=api-access");
        await PlaywrightAppFixture.CompleteDatabaseStartupAsync(page);
        foreach (var tokenList in new[] { false, true }) {
            await page.GetByTestId("api-token-create").ClickAsync();
            await page.GetByTestId("api-issued-token").WaitForAsync();
            if (tokenList) {
                await page.GetByTestId("api-tokens-open").ClickAsync();
                await page.GetByTestId("api-tokens-page").WaitForAsync();
                await host.CommandAsync(StorageCatalogProbeCommand.DenyApi);
                await page.GetByRole(AriaRole.Button, new() { Name = "Refresh tokens", Exact = true }).ClickAsync();
            } else {
                await host.CommandAsync(StorageCatalogProbeCommand.DenyApi);
                await page.GetByTestId("api-users-refresh").ClickAsync();
            }
            await Assertions.Expect(page.GetByTestId("api-token-access-denied")).ToBeVisibleAsync();
            await Assertions.Expect(page.GetByTestId("api-issued-token")).ToHaveCountAsync(0);
            await Assertions.Expect(page.GetByTestId("api-tokens-dialog")).ToHaveCountAsync(0);
            await Assertions.Expect(page.GetByTestId("api-user-create")).ToHaveCountAsync(0);
            await host.CommandAsync(StorageCatalogProbeCommand.AllowApi);
            await page.GetByTestId("api-access-retry").ClickAsync();
            await Assertions.Expect(page.GetByTestId("api-token-create")).ToBeVisibleAsync();
            await Assertions.Expect(page.GetByTestId("api-issued-token")).ToHaveCountAsync(0);
            await page.GetByRole(AriaRole.Button, new() { Name = "Workspace", Exact = true }).ClickAsync();
            await page.GetByTestId("defaults-name").WaitForAsync();
            await page.GetByRole(AriaRole.Button, new() { Name = "API Access", Exact = false }).ClickAsync();
            await page.GetByTestId("api-token-create").WaitForAsync();
            await Assertions.Expect(page.GetByTestId("api-issued-token")).ToHaveCountAsync(0);
        }
        var evidence = Path.Combine(PlaywrightTestHostPaths.RepositoryRoot, "output", "playwright", "workspace-storage-catalog-ui");
        Directory.CreateDirectory(evidence);
        await page.ScreenshotAsync(new() { Path = Path.Combine(evidence, "api-denial-retry.png"), FullPage = true });
        Assert.Empty(errors);
        Assert.DoesNotContain("Unhandled exception", host.Logs, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ObjectDisposedException", host.Logs, StringComparison.Ordinal);
    }
}
