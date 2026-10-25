using System.Collections.Concurrent;
using System.Text.Json;
using CanDoItAll.AgentFramework.UI.Chat;
using CanDoItAll.AgentFramework.UiSandbox;
using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright;

[Trait("Category", "Playwright")]
[Trait("Category", "HostPlatform")]
public sealed class AgentChatSendAffordanceBrowserTests {
    [Fact]
    public async Task Unconfirmed_outcome_blocks_Send_without_disabling_draft_editing() {
        await using var host = new IndependentProviderSandboxHost(ProviderSandboxKind.AgentChat);
        await host.StartAsync(published: false);
        using var playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
        await using var context = await browser.NewContextAsync(new() {
            ViewportSize = new() { Width = 1920, Height = 1080 },
            DeviceScaleFactor = 1
        });
        var page = await context.NewPageAsync();
        var console = new ConcurrentQueue<string>();
        var errors = new ConcurrentQueue<string>();
        var origin = new Uri(host.BaseUrl);
        page.PageError += (_, error) => errors.Enqueue(error);
        page.Console += (_, message) => {
            console.Enqueue($"{message.Type}: {message.Text}");
            if (message.Type == "error") {
                errors.Enqueue(message.Text);
            }
        };
        page.Response += (_, response) => {
            if (response.Status >= 400 && Uri.TryCreate(response.Url, UriKind.Absolute, out var address) &&
                address.GetLeftPart(UriPartial.Authority) == origin.GetLeftPart(UriPartial.Authority)) {
                errors.Enqueue($"HTTP {response.Status}: {address.AbsolutePath}");
            }
        };
        page.RequestFailed += (_, request) => {
            if (Uri.TryCreate(request.Url, UriKind.Absolute, out var address) &&
                address.GetLeftPart(UriPartial.Authority) == origin.GetLeftPart(UriPartial.Authority)) {
                errors.Enqueue($"Request failed: {address.AbsolutePath}");
            }
        };
        var directory = Path.Combine(PlaywrightTestHostPaths.RepositoryRoot, "output", "playwright", "agent-chat-send-affordance",
            $"{DateTimeOffset.UtcNow:yyyyMMddTHHmmssfffZ}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try {
            await page.GotoAsync($"{host.BaseUrl}/?specimen=agent-chat&scenario={nameof(AgentChatScenario.Unconfirmed)}");
            var specimen = page.GetByTestId("sandbox-agent-chat-specimen");
            var scenario = page.Locator("#agent-chat-scenario");
            var draft = page.GetByTestId("chat-prompt-input");
            var send = page.GetByTestId("chat-send-button");
            var intent = page.GetByTestId("sandbox-intent");
            var sends = page.GetByTestId("sandbox-send-count");
            await Assertions.Expect(scenario).ToHaveValueAsync(nameof(AgentChatScenario.Unconfirmed));
            await Assertions.Expect(specimen).ToHaveAttributeAsync("data-busy", "false");
            await Assertions.Expect(specimen).ToHaveAttributeAsync("data-send-blocked", "true");
            await Assertions.Expect(specimen).ToHaveAttributeAsync("data-approval-count", "0");
            await Assertions.Expect(sends).ToHaveTextAsync("0");
            await Assertions.Expect(send).ToBeDisabledAsync();
            await Assertions.Expect(draft).ToBeEditableAsync();

            const string retainedDraft = "Keep this editable sample while the earlier outcome needs review Ω.";
            await draft.FillAsync(retainedDraft);
            await Assertions.Expect(draft).ToHaveValueAsync(retainedDraft);
            await Assertions.Expect(intent).ToHaveTextAsync(nameof(ChatWorkspaceAction.DraftChanged));
            await Assertions.Expect(sends).ToHaveTextAsync("0");
            await send.ScrollIntoViewIfNeededAsync();
            var bounds = await send.BoundingBoxAsync();
            Assert.NotNull(bounds);
            Assert.True(await send.EvaluateAsync<bool>("element => { const bounds = element.getBoundingClientRect(); return element.contains(document.elementFromPoint(bounds.x + bounds.width / 2, bounds.y + bounds.height / 2)); }"));
            await page.Mouse.ClickAsync(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2);
            await Assertions.Expect(send).ToBeDisabledAsync();
            await Assertions.Expect(draft).ToHaveValueAsync(retainedDraft);
            await Assertions.Expect(intent).ToHaveTextAsync(nameof(ChatWorkspaceAction.DraftChanged));
            await Assertions.Expect(sends).ToHaveTextAsync("0");
            await CaptureAsync("blocked-editable-draft");

            await scenario.SelectOptionAsync(nameof(AgentChatScenario.Transcript));
            await Assertions.Expect(specimen).ToHaveAttributeAsync("data-scenario", nameof(AgentChatScenario.Transcript));
            await Assertions.Expect(specimen).ToHaveAttributeAsync("data-busy", "false");
            await Assertions.Expect(specimen).ToHaveAttributeAsync("data-send-blocked", "false");
            await Assertions.Expect(sends).ToHaveTextAsync("0");
            await Assertions.Expect(send).ToBeEnabledAsync();
            await draft.FillAsync("An ordinary idle sample may be sent.");
            await Assertions.Expect(intent).ToHaveTextAsync(nameof(ChatWorkspaceAction.DraftChanged));
            await send.ClickAsync();
            await Assertions.Expect(sends).ToHaveTextAsync("1");
            await Assertions.Expect(intent).ToHaveTextAsync(nameof(ChatWorkspaceAction.Send));
            await Assertions.Expect(draft).ToHaveValueAsync("");
            await CaptureAsync("idle-after-send");
            Assert.Empty(errors);
            Assert.DoesNotContain("fail:", host.Logs, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("crit:", host.Logs, StringComparison.OrdinalIgnoreCase);
        } catch {
            try {
                await page.ScreenshotAsync(new() { Path = Path.Combine(directory, "failure.png"), Timeout = 5_000 });
            } catch (Exception screenshotFailure) {
                console.Enqueue($"Failure screenshot unavailable: {screenshotFailure.GetType().Name}");
            }
            throw;
        } finally {
            await File.WriteAllTextAsync(Path.Combine(directory, "browser-evidence.json"), JsonSerializer.Serialize(new {
                Viewport = "1920x1080@1", Published = false, Console = console.ToArray(), Errors = errors.ToArray()
            }));
            await File.WriteAllTextAsync(Path.Combine(directory, "server.log"), host.Logs);
        }

        async Task CaptureAsync(string name) {
            Assert.False(await page.EvaluateAsync<bool>("document.documentElement.scrollWidth > document.documentElement.clientWidth"));
            await page.ScreenshotAsync(new() { Path = Path.Combine(directory, name + ".png"), FullPage = false });
        }
    }
}
