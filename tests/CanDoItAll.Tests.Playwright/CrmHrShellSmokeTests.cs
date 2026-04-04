using CanDoItAll.Modules.CrmHr;
using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright;

[Collection(PlaywrightCollection.Name)]
public sealed class CrmHrShellSmokeTests
{
    private readonly PlaywrightAppFixture fixture;

    public CrmHrShellSmokeTests(PlaywrightAppFixture fixture)
    {
        this.fixture = fixture;
    }

    [Fact]
    public async Task Crm_hr_routes_load_and_directory_save_persists_after_reload()
    {
        var evidenceDirectory = @"C:\repositories\CanDoItAll\evidence\crm-hr\b02";
        Directory.CreateDirectory(evidenceDirectory);

        await using var context = await fixture.Browser.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize
            {
                Width = 1600,
                Height = 1000
            }
        });
        var page = await context.NewPageAsync();
        var partyName = $"Playwright Party {DateTimeOffset.UtcNow:yyyyMMddHHmmss}";

        await page.GotoAsync($"{fixture.BaseUrl}/crm-hr");
        await DismissStartupModalIfPresentAsync(page);
        await page.WaitForSelectorAsync("text=Unified relationship and workforce workspace");
        Assert.False(await page.Locator("#blazor-error-ui").IsVisibleAsync());
        await page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = Path.Combine(evidenceDirectory, "crm-hr-home-desktop.png"),
            FullPage = true
        });

        await page.GetByTestId("crmhr-home-open-directory").ClickAsync();
        await page.GetByTestId("crmhr-party-save-button").WaitForAsync();
        await page.GetByTestId("crmhr-party-display-name").FillAsync(partyName);
        await page.GetByTestId("crmhr-party-type").SelectOptionAsync(new[] { PartyType.Organization.ToString() });
        await page.GetByTestId("crmhr-party-status").SelectOptionAsync(new[] { PartyLifecycleStatus.Active.ToString() });
        await page.GetByTestId("crmhr-party-role").SelectOptionAsync(new[] { PartyRoleKind.Customer.ToString() });
        await page.GetByTestId("crmhr-party-email").FillAsync("playwright.crmhr@example.test");
        await page.GetByTestId("crmhr-party-phone").FillAsync("+1 206 555 0188");
        await page.GetByTestId("crmhr-party-tags").FillAsync("playwright, customer");
        await page.GetByTestId("crmhr-party-save-button").ClickAsync();
        await WaitForUrlContainsAsync(page, "/crm-hr/directory?partyId=");
        await page.WaitForSelectorAsync($"text={partyName}");
        Assert.Contains("/crm-hr/directory?partyId=", page.Url, StringComparison.OrdinalIgnoreCase);
        Assert.False(await page.Locator("#blazor-error-ui").IsVisibleAsync());
        await page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = Path.Combine(evidenceDirectory, "crm-hr-directory-desktop.png"),
            FullPage = true
        });

        var directoryUrl = page.Url;
        await page.ReloadAsync();
        await page.WaitForSelectorAsync($"text={partyName}");
        Assert.Equal(directoryUrl, page.Url);

        await page.SetViewportSizeAsync(1024, 900);
        await page.GetByTestId("crmhr-party-save-button").WaitForAsync();
        Assert.False(await page.Locator("#blazor-error-ui").IsVisibleAsync());
        await page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = Path.Combine(evidenceDirectory, "crm-hr-directory-tablet.png"),
            FullPage = true
        });

        await AssertRouteLoadsAsync(page, "/crm-hr/crm", "CRM workspace");
        await AssertRouteLoadsAsync(page, "/crm-hr/workforce", "Workforce workspace");
        await AssertRouteLoadsAsync(page, "/crm-hr/recruiting", "Recruiting workspace");
        await AssertRouteLoadsAsync(page, "/crm-hr/agents", "Agent workspace");
        await AssertRouteLoadsAsync(page, "/crm-hr/assignments", "Assignments workspace");
    }

    private async Task AssertRouteLoadsAsync(IPage page, string route, string markerText)
    {
        await page.GotoAsync($"{fixture.BaseUrl}{route}");
        await page.WaitForSelectorAsync($"text={markerText}");
        Assert.False(await page.Locator("#blazor-error-ui").IsVisibleAsync());
    }

    private static async Task DismissStartupModalIfPresentAsync(IPage page, float timeoutMs = 1_500)
    {
        var startupDialog = page.GetByTestId("database-startup-modal");
        try
        {
            await startupDialog.WaitForAsync(new LocatorWaitForOptions
            {
                Timeout = timeoutMs
            });
        }
        catch (TimeoutException)
        {
            return;
        }

        await page.GetByTestId("database-startup-continue").ClickAsync();
        await startupDialog.WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Detached
        });
    }

    private static async Task WaitForUrlContainsAsync(IPage page, string fragment, int timeoutMs = 10_000)
    {
        var timeoutAt = DateTimeOffset.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTimeOffset.UtcNow < timeoutAt)
        {
            if (page.Url.Contains(fragment, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            await Task.Delay(200);
        }

        throw new TimeoutException($"Timed out waiting for URL to contain '{fragment}'. Current URL: {page.Url}");
    }
}
