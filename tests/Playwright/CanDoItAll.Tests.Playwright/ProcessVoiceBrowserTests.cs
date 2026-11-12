using System.Text.Json;
using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright;

[Trait("Category", "Playwright")]
[Trait("Category", "HostPlatform")]
public sealed class ProcessVoiceBrowserTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Native_manager_voice_handles_denial_and_retires_late_media_for_same_agent_reopening(bool projectScoped) {
        await using var wire = await AgentResponseFixture.StartAsync("Voice lifecycle must not request a model.");
        await using var host = await ProcessNativeBrowserHost.StartAsync(wire.BaseUrl, allowVoice: true);
        using var playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
        await using var context = await browser.NewContextAsync(new() { ViewportSize = new() { Width = 1920, Height = 1080 } });
        await context.AddInitScriptAsync("""
            window.pc1Media = { mode: 'deny', requests: 0, stopped: 0, started: 0, release: null };
            Object.defineProperty(navigator.mediaDevices, 'getUserMedia', { value: async () => {
                const fixture = window.pc1Media;
                fixture.requests++;
                if (fixture.mode === 'deny') {
                    throw new DOMException('PC1 controlled permission denial', 'NotAllowedError');
                }
                if (fixture.mode === 'hold') {
                    await new Promise(resolve => fixture.release = resolve);
                }
                let stopped = false;
                return { getTracks: () => [{ stop: () => {
                    if (!stopped) {
                        stopped = true;
                        fixture.stopped++;
                    }
                } }] };
            } });
            window.MediaRecorder = class {
                state = 'inactive';
                start() {
                    this.state = 'recording';
                    window.pc1Media.started++;
                }
                stop() {
                    this.state = 'inactive';
                    this.onstop?.();
                }
            };
            """);
        await context.Tracing.StartAsync(new() { Screenshots = true, Snapshots = true, Sources = true });
        var page = await context.NewPageAsync();
        var errors = new List<string>();
        page.PageError += (_, error) => errors.Add(error);
        try {
            var route = projectScoped ? $"/projects/{host.ProjectId:D}/processes" : "/processes";
            await page.GotoAsync(host.BaseUrl + route);
            await PlaywrightAppFixture.CompleteDatabaseStartupAsync(page);
            await Assertions.Expect(page.GetByTestId("processes-page-scaffold")).ToHaveAttributeAsync("data-interactive", "true");
            await page.GetByTestId("processes-definition-" + ProcessNativeBrowserHost.CompleteDefinition).ClickAsync();
            await page.GetByTestId("processes-detail-tab-manager-chat").ClickAsync();
            var chat = page.GetByTestId("processes-manager-chat-tab");
            var agent = page.GetByTestId("processes-manager-chat-agent-select");
            var record = chat.GetByTestId("chat-voice-record-button");
            await SelectAgent();
            await record.ClickAsync();
            await Assertions.Expect(chat).ToContainTextAsync("PC1 controlled permission denial");
            await Assertions.Expect(record).ToBeEnabledAsync();
            await page.EvaluateAsync("() => window.pc1Media.mode = 'permit'");
            await record.ClickAsync();
            await Assertions.Expect(record).ToHaveAttributeAsync("aria-label", "Stop voice recording");
            await SelectAgent();
            await page.WaitForFunctionAsync("() => window.pc1Media.stopped === 1");
            await Assertions.Expect(record).ToHaveAttributeAsync("aria-label", "Start voice recording");
            await page.EvaluateAsync("() => window.pc1Media.mode = 'hold'");
            await record.ClickAsync();
            await Assertions.Expect(chat).ToContainTextAsync("Requesting microphone");
            await page.WaitForFunctionAsync("() => window.pc1Media.release !== null");
            await SelectAgent();
            await page.EvaluateAsync("() => window.pc1Media.release()");
            await page.WaitForFunctionAsync("() => window.pc1Media.stopped === 2");
            await Assertions.Expect(chat).Not.ToContainTextAsync("Record failed");
            await Assertions.Expect(record).ToBeEnabledAsync();
            await Assertions.Expect(record).ToHaveAttributeAsync("aria-label", "Start voice recording");
            await page.EvaluateAsync("() => window.pc1Media.mode = 'permit'");
            await record.ClickAsync();
            await Assertions.Expect(record).ToHaveAttributeAsync("aria-label", "Stop voice recording");
            await SelectAgent();
            await page.WaitForFunctionAsync("() => window.pc1Media.stopped === 3");
            var observed = await page.EvaluateAsync<MediaCounts>("() => window.pc1Media");
            Assert.Equal(new MediaCounts { Requests = 4, Stopped = 3, Started = 2 }, observed);
            Assert.Equal(0, wire.Requests);
            Assert.Empty(errors);
            await page.ScreenshotAsync(new() { Path = Path.Combine(host.Evidence, "voice-retired.png") });
            await File.WriteAllTextAsync(Path.Combine(host.Evidence, "voice-oracle.json"), JsonSerializer.Serialize(new {
                ProjectScoped = projectScoped, host.AgentId, observed, ModelRequests = wire.Requests,
                MediaBoundary = "Deterministic permitted/denied/held browser media; actual native voice owner and production JS; no microphone or speech service."
            }));
            async Task SelectAgent() {
                await agent.SelectOptionAsync(host.AgentId.ToString("D"));
                await Assertions.Expect(chat.Locator(".chat-panel-header").GetByAltText("PC1 process manager")).ToBeVisibleAsync();
                await Assertions.Expect(record).ToBeEnabledAsync();
            }
        } catch {
            await page.ScreenshotAsync(new() { Path = Path.Combine(host.Evidence, "voice-failure.png") });
            await File.WriteAllTextAsync(Path.Combine(host.Evidence, "voice-failure.txt"), await page.Locator("body").InnerTextAsync());
            throw;
        } finally {
            await context.Tracing.StopAsync(new() { Path = Path.Combine(host.Evidence, "voice-trace.zip") });
        }
    }
    private sealed record MediaCounts {
        public int Requests { get; init; }
        public int Stopped { get; init; }
        public int Started { get; init; }
    }
}
