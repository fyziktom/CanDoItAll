using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright.Smoke;

[Collection(PlaywrightCollection.Name)]
public sealed class PromptGalleryBrowserTests
{
    private readonly PlaywrightAppFixture fixture;

    public PromptGalleryBrowserTests(PlaywrightAppFixture fixture)
    {
        this.fixture = fixture;
    }

    [Fact]
    public async Task Gallery_creates_saves_finalizes_and_reopens_an_item_through_the_real_host()
    {
        var repoRoot = PlaywrightTestHostPaths.RepositoryRoot;
        var artifactsDir = Path.Combine(repoRoot, "output", "playwright", "prompt-gallery");
        Directory.CreateDirectory(artifactsDir);
        await using var context = await fixture.Browser.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize
            {
                Width = 1600,
                Height = 1000
            }
        });
        var page = await context.NewPageAsync();
        var pageErrors = new List<string>();
        page.PageError += (_, message) => pageErrors.Add(message);
        var title = $"Browser gallery proof {Guid.NewGuid():N}";

        var response = await page.GotoAsync($"{fixture.BaseUrl}/prompt-gallery");
        Assert.NotNull(response);
        Assert.True(response!.Ok, $"Expected /prompt-gallery to return 2xx, got {(int)response.Status}.");
        await PlaywrightAppFixture.CompleteDatabaseStartupAsync(page);
        await page.GetByTestId("prompt-gallery-search-list").WaitForAsync();
        await page.GetByTestId("prompt-gallery-filter-rail").WaitForAsync();
        await page.ScreenshotAsync(new PageScreenshotOptions { Path = Path.Combine(artifactsDir, "01-gallery.png") });

        // A committed filter must survive the editor round trip instead of being reset by a list remount.
        await page.GetByTestId("prompt-gallery-status-filter").SelectOptionAsync("Draft");
        await page.GetByRole(AriaRole.Button, new() { Name = "New item", Exact = true }).ClickAsync();
        var editor = page.GetByTestId("prompt-gallery-item-editor");
        await editor.WaitForAsync();
        await page.GetByTestId("prompt-gallery-editor-save").ClickAsync();
        await page.GetByTestId("prompt-gallery-editor-validation").WaitForAsync();

        await page.GetByTestId("prompt-gallery-editor-title").FillAsync(title);
        await page.GetByTestId("prompt-gallery-editor-content").FillAsync("Summarize the supplied research in five bullets.");
        await page.GetByTestId("prompt-gallery-editor-save").ClickAsync();
        await page.GetByText("Prompt draft saved").First.WaitForAsync();
        await page.GetByTestId("prompt-gallery-editor-archive").WaitForAsync();
        await page.GetByText("Saved Gallery item").WaitForAsync();
        await page.ScreenshotAsync(new PageScreenshotOptions { Path = Path.Combine(artifactsDir, "02-draft-saved.png") });

        await page.GetByTestId("prompt-gallery-editor-finalize").ClickAsync();
        await page.GetByText("Final version created").First.WaitForAsync();
        await page.GetByTestId("prompt-gallery-editor-versions").WaitForAsync();
        await page.GetByText("Ready for reuse").First.WaitForAsync();
        await page.ScreenshotAsync(new PageScreenshotOptions { Path = Path.Combine(artifactsDir, "03-final-version.png") });

        await page.GetByTestId("prompt-gallery-editor-cancel").ClickAsync();
        await editor.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Detached });
        Assert.Equal("Draft", await page.GetByTestId("prompt-gallery-status-filter").InputValueAsync());
        await page.GetByTestId("prompt-gallery-status-filter").SelectOptionAsync("");
        await page.GetByTestId("prompt-gallery-search").FillAsync(title);
        var row = page.GetByTestId("prompt-gallery-grid").GetByText(title, new() { Exact = true });
        await row.WaitForAsync();
        await page.GetByTestId("prompt-gallery-grid").GetByText("v1", new() { Exact = true }).WaitForAsync();
        await page.ScreenshotAsync(new PageScreenshotOptions { Path = Path.Combine(artifactsDir, "04-list-refreshed.png") });

        await page.GetByTestId("prompt-gallery-edit").First.ClickAsync();
        await editor.WaitForAsync();
        Assert.Equal(title, await page.GetByTestId("prompt-gallery-editor-title").InputValueAsync());
        var promptId = await page.EvaluateAsync<string>("() => document.querySelector('[data-testid=\"prompt-gallery-editor-versions\"]') ? 'present' : 'missing'");
        Assert.Equal("present", promptId);
        await page.GetByTestId("prompt-gallery-editor-cancel").ClickAsync();
        await editor.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Detached });

        Assert.False(await page.Locator("#blazor-error-ui").IsVisibleAsync());
        Assert.Empty(pageErrors);
    }

    [Fact]
    public async Task Missing_promptId_request_shows_a_failed_editor_and_a_valid_request_reopens_it()
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
        var pageErrors = new List<string>();
        page.PageError += (_, message) => pageErrors.Add(message);

        var response = await page.GotoAsync($"{fixture.BaseUrl}/prompt-gallery?promptId={Guid.NewGuid():D}");
        Assert.NotNull(response);
        Assert.True(response!.Ok);
        await PlaywrightAppFixture.CompleteDatabaseStartupAsync(page);
        await page.GetByTestId("prompt-gallery-editor-retry").WaitForAsync();
        Assert.Equal(0, await page.GetByTestId("prompt-gallery-item-editor").CountAsync());
        await page.GetByTestId("prompt-gallery-editor-close").ClickAsync();
        await page.GetByTestId("prompt-gallery-editor-retry").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Detached });

        await page.GetByTestId("prompt-gallery-search-list").WaitForAsync();
        Assert.False(await page.Locator("#blazor-error-ui").IsVisibleAsync());
        Assert.Empty(pageErrors);
    }
}
