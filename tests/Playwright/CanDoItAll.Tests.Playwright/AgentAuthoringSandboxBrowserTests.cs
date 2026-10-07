using System.Text;
using System.Text.Json;
using CanDoItAll.AgentFramework.CapabilityAuthoring.UiSandbox;
using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright;

[Trait("Category", "Playwright")]
[Trait("Category", "HostPlatform")]
public sealed class AgentAuthoringSandboxBrowserTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Capability_family_uses_real_controls_assets_upload_and_independent_drafts(bool published) {
        await using var host = new IndependentProviderSandboxHost(ProviderSandboxKind.CapabilityAuthoring);
        await host.StartAsync(published);
        using var playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
        await using var context = await browser.NewContextAsync(new() { ViewportSize = new() { Width = 1920, Height = 1080 }, DeviceScaleFactor = 1 });
        var page = await context.NewPageAsync();
        var proof = new BrowserProof(page, published, "capabilities");
        try {
            foreach (var scenario in Enum.GetValues<AuthoringScenario>()) {
                await page.GotoAsync(host.BaseUrl + "/authoring/" + scenario);
                await Assertions.Expect(page.GetByTestId("authoring-sandbox")).ToHaveAttributeAsync("data-interactive", "true");
                if (scenario == AuthoringScenario.HeldLoad) {
                    await page.GetByTestId("authoring-release").ClickAsync();
                }
                if (scenario == AuthoringScenario.LoadFailure) {
                    await Assertions.Expect(page.GetByTestId("agents-capability-details-load-failed")).ToBeVisibleAsync();
                    continue;
                }
                await Assertions.Expect(page.GetByTestId("capability-authoring-form")).ToBeVisibleAsync();
                if (scenario is AuthoringScenario.NewMcp or AuthoringScenario.NewSkill or AuthoringScenario.NewTool) {
                    await page.GetByTestId("agents-capability-setup-name").FillAsync("Browser " + scenario);
                    await page.GetByTestId("agents-capability-setup-next").ClickAsync();
                    if (scenario == AuthoringScenario.NewSkill) {
                        await page.GetByTestId("agents-capability-setup-skill-mode").SelectOptionAsync("Upload");
                        await page.GetByTestId("agents-capability-setup-skill-file").SetInputFilesAsync(new FilePayload {
                            Name = "SKILL.md", MimeType = "text/markdown", Buffer = Encoding.UTF8.GetBytes("\uFEFF---\nname: ca1-browser\ndescription: Unicode upload\n---\n# Instructions Ω\nRead the local fixture only.")
                        });
                        await Assertions.Expect(page.GetByTestId("agents-capability-setup-inline-instructions")).ToHaveValueAsync(new System.Text.RegularExpressions.Regex("Instructions Ω"));
                    } else if (scenario == AuthoringScenario.NewMcp) {
                        await page.GetByTestId("agents-capability-setup-mcp-transport").SelectOptionAsync("logical");
                        await page.GetByTestId("agents-capability-setup-mcp-server").FillAsync("browser-fixture");
                    } else {
                        await page.GetByTestId("agents-capability-setup-tool-kind").SelectOptionAsync("externalHttp");
                        await page.GetByTestId("agents-capability-setup-tool-http-endpoint").FillAsync("https://fixture.invalid/owned");
                    }
                    await page.GetByTestId("agents-capability-setup-next").ClickAsync();
                    await page.GetByTestId("agents-capability-setup-create").ClickAsync();
                    await Assertions.Expect(page.GetByTestId("authoring-fixture-state")).ToContainTextAsync("Definitions: 1; saves: 1; setups: 0");
                } else {
                    await page.GetByRole(AriaRole.Tab, new() { Name = "Configuration", Exact = true }).ClickAsync();
                    await page.GetByRole(AriaRole.Tab, new() { Name = "Raw", Exact = true }).ClickAsync();
                    if (scenario == AuthoringScenario.BuiltInTool) {
                        await page.GetByRole(AriaRole.Tab, new() { Name = "Identity", Exact = true }).ClickAsync();
                        await Assertions.Expect(page.GetByTestId("agents-capability-details-key")).ToBeDisabledAsync();
                    }
                }
                await proof.ShotAsync(scenario.ToString());
            }
            await page.GotoAsync(host.BaseUrl + "/authoring/ToolHttp");
            await Assertions.Expect(page.GetByTestId("authoring-sandbox")).ToHaveAttributeAsync("data-interactive", "true");
            await page.GetByRole(AriaRole.Tab, new() { Name = "Configuration", Exact = true }).ClickAsync();
            await page.GetByTestId("agents-capability-details-tool-http-timeout").FillAsync("invalid-raw");
            await page.GetByRole(AriaRole.Tab, new() { Name = "Raw", Exact = true }).ClickAsync();
            await page.GetByTestId("agents-capability-details-save").ClickAsync();
            await Assertions.Expect(page.Locator(".validation-errors")).ToContainTextAsync("Timeout must be a positive whole number");
            await Assertions.Expect(page.GetByTestId("authoring-fixture-state")).ToContainTextAsync("saves: 0; setups: 0");
            await page.GetByTestId("authoring-second-editor").ClickAsync();
            var second = page.GetByTestId("authoring-second-dialog");
            await second.GetByTestId("agents-capability-setup-name").FillAsync("Independent second draft");
            await proof.AssertDialogAsync(second);
            await page.Keyboard.PressAsync("Escape");
            await Assertions.Expect(second).ToHaveCountAsync(0);
            await page.GetByRole(AriaRole.Tab, new() { Name = "Configuration", Exact = true }).ClickAsync();
            await Assertions.Expect(page.GetByTestId("agents-capability-details-tool-http-timeout")).ToHaveValueAsync("invalid-raw");
            await proof.CompleteAsync(host);
        } catch (Exception exception) {
            await proof.CaptureFailureAsync(exception);
            throw;
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Team_family_preserves_membership_scroll_keyboard_nested_icons_and_two_editors(bool published) {
        await using var host = new IndependentProviderSandboxHost(ProviderSandboxKind.Teams);
        await host.StartAsync(published);
        using var playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
        await using var context = await browser.NewContextAsync(new() { ViewportSize = new() { Width = 1920, Height = 1080 }, DeviceScaleFactor = 1 });
        var page = await context.NewPageAsync();
        var proof = new BrowserProof(page, published, "teams");
        try {
            await page.GotoAsync(host.BaseUrl + "/teams/HeldLoad");
            await Assertions.Expect(page.GetByTestId("team-authoring-sandbox")).ToHaveAttributeAsync("data-interactive", "true");
            await page.GetByTestId("team-release").ClickAsync();
            await Assertions.Expect(page.GetByTestId("agents-team-name")).ToBeVisibleAsync();
            await page.GotoAsync(host.BaseUrl + "/teams/Large");
            await Assertions.Expect(page.GetByTestId("team-authoring-sandbox")).ToHaveAttributeAsync("data-interactive", "true");
            await page.GetByTestId("agents-team-name").FillAsync("First editor draft");
            await page.GetByTestId("team-open-members").ClickAsync();
            var members = page.GetByTestId("team-members-dialog");
            await proof.AssertDialogAsync(members);
            var grid = page.Locator(".agent-team-members-dialog__grid");
            Assert.True(await grid.EvaluateAsync<bool>("e => e.scrollHeight > e.clientHeight && getComputedStyle(e).overflowY === 'auto'"));
            await page.GetByTestId("agents-team-members-search").FillAsync("Fixture agent 90");
            await Assertions.Expect(page.GetByTestId("agents-team-member-card")).ToHaveCountAsync(1);
            await page.GetByTestId("agents-team-member-card").PressAsync("Enter");
            await page.GetByTestId("agents-team-members-search").FillAsync("");
            await Assertions.Expect(page.GetByTestId("agents-team-member-card")).ToHaveCountAsync(90);
            await proof.ShotAsync("large-members");
            await page.GetByTestId("agents-team-members-confirm").ClickAsync();
            await Assertions.Expect(members).ToHaveCountAsync(0);
            await page.GetByTestId("team-second-editor").ClickAsync();
            var second = page.GetByTestId("team-second-dialog");
            await second.GetByTestId("agents-team-name").FillAsync("Independent second editor");
            await second.GetByTestId("agents-team-choose-icon").ClickAsync();
            var icon = page.GetByTestId("team-icon-dialog");
            await proof.AssertDialogAsync(icon);
            await icon.GetByTestId("material-icon-picker-search").FillAsync("hub");
            await Assertions.Expect(icon.GetByTestId("material-icon-picker-option")).ToHaveCountAsync(1);
            await icon.GetByTestId("material-icon-picker-option").PressAsync("Enter");
            await icon.GetByTestId("material-icon-picker-confirm").ClickAsync();
            await Assertions.Expect(icon).ToHaveCountAsync(0);
            await Assertions.Expect(second.GetByTestId("agents-team-selected-icon")).ToContainTextAsync("hub");
            await page.Keyboard.PressAsync("Escape");
            await Assertions.Expect(second).ToHaveCountAsync(0);
            await Assertions.Expect(page.GetByTestId("agents-team-name")).ToHaveValueAsync("First editor draft");
            await page.GetByTestId("agents-team-name").PressAsync("Enter");
            await Assertions.Expect(page.GetByTestId("team-fixture-state")).ToContainTextAsync("metadata writes: 1; member writes: 1; members: 2");
            await page.GetByTestId("team-reopen").ClickAsync();
            await Assertions.Expect(page.GetByTestId("agents-team-name")).ToHaveValueAsync("First editor draft");
            await proof.ShotAsync("metadata-members-preserved");
            await page.GetByTestId("team-scenario").SelectOptionAsync("MissingReference");
            await page.GetByTestId("team-open-members").ClickAsync();
            await Assertions.Expect(page.GetByTestId("agents-team-unavailable-member")).ToBeVisibleAsync();
            await proof.ShotAsync("missing-member");
            await page.Keyboard.PressAsync("Escape");
            await proof.CompleteAsync(host);
        } catch (Exception exception) {
            await proof.CaptureFailureAsync(exception);
            throw;
        }
    }

    private sealed class BrowserProof {
        private readonly IPage page;
        private readonly string directory;
        private readonly List<string> errors = [];
        private readonly HashSet<string> assets = new(StringComparer.Ordinal);
        public BrowserProof(IPage page, bool published, string family) {
            this.page = page;
            directory = Path.Combine(PlaywrightTestHostPaths.RepositoryRoot, "artifacts", "agent-authoring-ca1", "browser",
                $"{DateTimeOffset.UtcNow:yyyyMMddTHHmmssfffZ}-{Guid.NewGuid():N}", family, published ? "published" : "source");
            Directory.CreateDirectory(directory);
            page.PageError += (_, error) => errors.Add(error);
            page.Response += (_, response) => {
                var path = new Uri(response.Url).AbsolutePath;
                if (response.Status >= 400) {
                    errors.Add($"HTTP {response.Status}: {path}");
                }
                if (path.Contains(".css", StringComparison.Ordinal) || path.Contains(".woff", StringComparison.Ordinal)) {
                    assets.Add(path);
                }
            };
        }
        public Task ShotAsync(string name) => page.ScreenshotAsync(new() { Path = Path.Combine(directory, name + ".png") });
        public async Task CaptureFailureAsync(Exception exception) {
            await File.WriteAllTextAsync(Path.Combine(directory, "failure.txt"), exception.ToString());
            try {
                await page.ScreenshotAsync(new() { Path = Path.Combine(directory, "failure.png"), Timeout = 5_000 });
            } catch (Exception screenshotException) {
                await File.WriteAllTextAsync(Path.Combine(directory, "screenshot-failure.txt"), screenshotException.ToString());
            }
        }
        public async Task AssertDialogAsync(ILocator dialog) {
            await Assertions.Expect(dialog).ToBeVisibleAsync();
            Assert.True(await dialog.EvaluateAsync<bool>("e => { const r=e.getBoundingClientRect(); return r.width > 400 && r.left >= 0 && r.right <= innerWidth && r.bottom <= innerHeight; }"));
            await page.Keyboard.PressAsync("Tab");
            Assert.True(await dialog.EvaluateAsync<bool>("e => e.contains(document.activeElement)"));
        }
        public async Task CompleteAsync(IndependentProviderSandboxHost host) {
            Assert.Equal(1, await page.EvaluateAsync<int>("devicePixelRatio"));
            Assert.Equal(1920, await page.EvaluateAsync<int>("innerWidth"));
            Assert.True(await page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= innerWidth"));
            Assert.Contains(assets, path => path.Contains("CanDoItAll.Components.BaseLib", StringComparison.Ordinal));
            Assert.Contains(assets, path => path.Contains(".woff", StringComparison.Ordinal));
            Assert.Empty(errors);
            Assert.DoesNotContain("fail:", host.Logs, StringComparison.OrdinalIgnoreCase);
            await File.WriteAllTextAsync(Path.Combine(directory, "evidence.json"), JsonSerializer.Serialize(new { Viewport = "1920x1080@1", assets, errors }));
        }
    }
}
