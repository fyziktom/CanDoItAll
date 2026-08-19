using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright.Smoke;

public sealed partial class AppSmokeTests
{
    [Fact]
    public async Task Project_structure_web_preview_opens_on_physical_double_click_and_uses_the_available_dialog_space()
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
        await CreateProjectAsync(page, "Playwright Embedded Browser", "Validation");

        string runtimeUrl = $"{fixture.BaseUrl}/_dev/runtime";
        var longNotes = string.Join(
            Environment.NewLine,
            Enumerable.Repeat(
                "This long preview note must scroll without taking the browser viewport away.",
                80));
        await CreateWebLinkAsync(page, "Runtime browser preview", runtimeUrl, longNotes);
        await PhysicalDoubleClickCanvasNodeAsync(page, ".cw-node:has-text('Runtime browser preview')");

        var dialog = page.GetByTestId("project-structure-web-preview-dialog");
        await dialog.WaitForAsync();
        var browser = dialog.GetByTestId("project-structure-web-preview-frame");
        var frame = browser.Locator("iframe");
        await frame.WaitForAsync();
        Assert.Equal(runtimeUrl, await frame.GetAttributeAsync("src"));
        var frameHandle = await frame.ElementHandleAsync();
        Assert.NotNull(frameHandle);
        var contentFrame = await frameHandle.ContentFrameAsync();
        Assert.NotNull(contentFrame);
        await contentFrame.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
        Assert.StartsWith(runtimeUrl, contentFrame.Url, StringComparison.Ordinal);
        var runtimeIsReady = await contentFrame.EvaluateAsync<bool>(
            "() => JSON.parse(document.body.innerText).isReady === true");
        Assert.True(runtimeIsReady);
        var notes = dialog.GetByTestId("project-structure-web-preview-notes");
        var notesScroll = await notes.EvaluateAsync<bool>(
            "element => element.scrollHeight > element.clientHeight && getComputedStyle(element).overflowY === 'auto'");
        Assert.True(notesScroll);
        await AssertEmbeddedBrowserFillsBodyAsync(dialog, browser, frame);

        await dialog.GetByTestId("project-structure-dialog-size-toggle").ClickAsync();
        await Assertions.Expect(dialog).ToHaveAttributeAsync("data-maximized", "true");
        await AssertEmbeddedBrowserFillsBodyAsync(dialog, browser, frame);

        var runtimePopupTask = context.WaitForPageAsync();
        await dialog.GetByTestId("project-structure-web-preview-open-browser").ClickAsync();
        var runtimePopup = await runtimePopupTask;
        await runtimePopup.WaitForURLAsync($"{runtimeUrl}**");
        Assert.StartsWith(runtimeUrl, runtimePopup.Url, StringComparison.Ordinal);
        await runtimePopup.CloseAsync();
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Close", Exact = true }).ClickAsync();
        await Assertions.Expect(dialog).ToBeHiddenAsync();

        await CreateWebLinkAsync(page, "Google browser fallback", "https://google.com/");
        await PhysicalDoubleClickCanvasNodeAsync(page, ".cw-node:has-text('Google browser fallback')");

        await dialog.WaitForAsync();
        await Assertions.Expect(dialog.Locator("iframe")).ToHaveCountAsync(0);
        await Assertions.Expect(dialog).ToContainTextAsync("This site cannot be embedded");
        var externalLink = dialog.GetByTestId("project-structure-web-preview-open-browser");
        await Assertions.Expect(externalLink).ToHaveAttributeAsync("href", "https://google.com/");
        await Assertions.Expect(externalLink).ToHaveAttributeAsync("target", "_blank");
        await Assertions.Expect(externalLink).ToHaveAttributeAsync("rel", "noopener noreferrer");

        await context.RouteAsync("https://google.com/**", route => route.FulfillAsync(new RouteFulfillOptions
        {
            Status = 200,
            ContentType = "text/html",
            Body = "<title>External browser target</title>"
        }));
        var googlePopupTask = context.WaitForPageAsync();
        await externalLink.ClickAsync();
        var googlePopup = await googlePopupTask;
        await googlePopup.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
        Assert.StartsWith("https://google.com/", googlePopup.Url, StringComparison.OrdinalIgnoreCase);
        await googlePopup.CloseAsync();
    }

    private static async Task CreateWebLinkAsync(
        IPage page,
        string title,
        string url,
        string notes = "")
    {
        const string projectRootSelector = ".cw-node[data-node-id^='project:']";
        await OpenCanvasCreateComposerAsync(page, projectRootSelector, "Link", "add-link");
        await page.WaitForSelectorAsync("text=Address");
        await page.Locator(".cw-canvas-composer__input").Nth(0).FillAsync(title);
        await page.Locator(".cw-canvas-composer__input").Nth(1).FillAsync(url);
        await page.Locator(".cw-canvas-composer__textarea").FillAsync(notes);
        await page.Locator(".cw-canvas-composer__actions .cw-button[data-tone='accent']").ClickAsync();
        await WaitForSceneNodeTitleAsync(page, title, selectedOnly: true, timeoutMs: 15_000);
    }

    private static async Task PhysicalDoubleClickCanvasNodeAsync(IPage page, string selector)
    {
        var hotZoneCenter = await TryResolveCanvasHotZoneCenterAsync(page, selector, "node-body");
        Assert.NotNull(hotZoneCenter);
        await page.Mouse.DblClickAsync((float)hotZoneCenter.X, (float)hotZoneCenter.Y);
    }

    private static async Task AssertEmbeddedBrowserFillsBodyAsync(
        ILocator dialog,
        ILocator browser,
        ILocator frame)
    {
        var bodyBounds = await dialog.Locator(".project-structure-web-preview-dialog__body").BoundingBoxAsync();
        var browserBounds = await browser.BoundingBoxAsync();
        var frameBounds = await frame.BoundingBoxAsync();
        Assert.NotNull(bodyBounds);
        Assert.NotNull(browserBounds);
        Assert.NotNull(frameBounds);
        Assert.True(
            browserBounds.Height >= 240,
            $"Expected a useful embedded-browser height, but got {browserBounds.Height:0.##}px.");
        Assert.InRange(
            Math.Abs(bodyBounds.Y + bodyBounds.Height - (browserBounds.Y + browserBounds.Height)),
            0,
            6);
        Assert.InRange(Math.Abs(bodyBounds.Width - browserBounds.Width), 0, 6);
        Assert.InRange(Math.Abs(browserBounds.Height - frameBounds.Height), 0, 6);
        Assert.InRange(Math.Abs(browserBounds.Width - frameBounds.Width), 0, 6);
    }
}
