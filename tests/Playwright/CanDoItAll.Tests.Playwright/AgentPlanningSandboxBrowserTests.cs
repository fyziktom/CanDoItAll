using System.Text.Json;
using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright;

[Trait("Category", "Playwright")]
[Trait("Category", "HostPlatform")]
public sealed class AgentPlanningSandboxBrowserTests {
    [Fact]
    public async Task Bounded_large_review_keeps_actions_visible_focus_contained_and_uncertain_publication_blocked() {
        await using var host = new RazorSandboxHost(RazorSandboxTarget.AgentChat);
        await host.StartAsync(published: false);
        using var playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
        await using var context = await browser.NewContextAsync(new() { ViewportSize = new() { Width = 1920, Height = 1080 }, DeviceScaleFactor = 1 });
        var page = await context.NewPageAsync();
        page.SetDefaultTimeout(30_000);
        var errors = new List<string>();
        page.PageError += (_, error) => errors.Add(error);
        var directory = Path.Combine(PlaywrightTestHostPaths.RepositoryRoot, "output", "playwright", "planning-review-sandbox", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var passed = false;
        var focus = new List<FocusObservation>();
        try {
            await page.GotoAsync(host.BaseUrl + "/planning-review");
            await page.GetByTestId("planning-sample-large").ClickAsync();
            var open = page.GetByTestId("agents-chat-planning-open");
            await open.ClickAsync();
            var dialog = page.GetByTestId("agents-chat-planning-dialog");
            await Assertions.Expect(dialog).ToBeVisibleAsync();
            var content = dialog.GetByTestId("agents-chat-planning-content");
            await Assertions.Expect(content).ToContainTextAsync("100. Review task 100");
            var body = dialog.Locator(".cda-dialog__body");
            Assert.True(await body.EvaluateAsync<bool>("node => node.scrollHeight > node.clientHeight * 2"));
            await Assertions.Expect(dialog.GetByTestId("agents-chat-planning-approve")).ToBeInViewportAsync(new() { Ratio = 1 });
            await Assertions.Expect(dialog.GetByTestId("agents-chat-planning-refresh")).ToBeInViewportAsync(new() { Ratio = 1 });
            Assert.False(await page.EvaluateAsync<bool>("document.documentElement.scrollWidth > document.documentElement.clientWidth"));
            await page.ScreenshotAsync(new() { Path = Path.Combine(directory, "large-review-top.png") });
            await body.EvaluateAsync("node => node.scrollTop = node.scrollHeight");
            await Assertions.Expect(dialog.GetByTestId("agents-chat-planning-approve")).ToBeInViewportAsync(new() { Ratio = 1 });
            await page.ScreenshotAsync(new() { Path = Path.Combine(directory, "large-review-bottom.png") });
            for (var index = 0; index < 9; index++) {
                await page.Keyboard.PressAsync("Tab");
                var observed = JsonSerializer.Deserialize<FocusObservation>(await dialog.EvaluateAsync<string>("""
                    node => JSON.stringify({ Inside: node.contains(document.activeElement), PageFocused: document.hasFocus(),
                        Body: document.activeElement === document.body, Tag: document.activeElement?.tagName,
                        TestId: document.activeElement?.getAttribute('data-testid') })
                    """))!;
                focus.Add(observed);
                Assert.True(observed.Inside || observed.Body && !observed.PageFocused, JsonSerializer.Serialize(observed));
            }
            await dialog.GetByRole(AriaRole.Button, new() { Name = "Close", Exact = true }).Last.FocusAsync();
            await page.Keyboard.PressAsync("Escape");
            await Assertions.Expect(dialog).ToHaveCountAsync(0);
            await Assertions.Expect(open).ToBeFocusedAsync();
            await page.GetByTestId("planning-sample-unknown").ClickAsync();
            await open.ClickAsync();
            await Assertions.Expect(dialog.GetByTestId("agents-chat-planning-unconfirmed")).ToBeVisibleAsync();
            await Assertions.Expect(dialog.GetByTestId("agents-chat-planning-approve")).ToHaveCountAsync(0);
            await page.ScreenshotAsync(new() { Path = Path.Combine(directory, "unknown-review.png") });
            await dialog.GetByTestId("agents-chat-planning-reload").ClickAsync();
            await Assertions.Expect(dialog).ToHaveCountAsync(0);
            await page.GetByTestId("planning-sample-changed").ClickAsync();
            await open.ClickAsync();
            await Assertions.Expect(dialog.GetByTestId("agents-chat-planning-approve")).ToHaveCountAsync(0);
            await Assertions.Expect(dialog.GetByTestId("agents-chat-planning-refresh")).ToBeEnabledAsync();
            await dialog.GetByTestId("agents-chat-planning-refresh").ClickAsync();
            await Assertions.Expect(page.GetByTestId("planning-sample-intent")).ToContainTextAsync("Captured Refresh for generation 1");
            Assert.Empty(errors);
            passed = true;
        } finally {
            await page.ScreenshotAsync(new() { Path = Path.Combine(directory, passed ? "final.png" : "failure.png"), Timeout = 5_000 });
            await File.WriteAllTextAsync(Path.Combine(directory, "evidence.json"), JsonSerializer.Serialize(new {
                Passed = passed, Viewport = "1920x1080@1", BackendFree = true, MaximumTasks = 100, Focus = focus, Errors = errors
            }));
        }
    }

    private sealed record FocusObservation(bool Inside, bool PageFocused, bool Body, string? Tag, string? TestId);
}
