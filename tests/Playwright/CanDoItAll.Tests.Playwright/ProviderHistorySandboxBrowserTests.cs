using System.Text.Json;
using CanDoItAll.AgentFramework.UiSandbox;
using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright;

[Trait("Category", "Playwright")]
[Trait("Category", "HostPlatform")]
public sealed class ProviderHistorySandboxBrowserTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Full_history_workspace_is_lazy_bounded_and_independent_with_real_assets(bool published) {
        await using var host = new IndependentProviderSandboxHost(ProviderSandboxKind.History);
        await host.StartAsync(published);
        using var playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
        await using var context = await browser.NewContextAsync(new() {
            ViewportSize = new() { Width = 1920, Height = 1080 }, DeviceScaleFactor = 1
        });
        var page = await context.NewPageAsync();
        var errors = new List<string>();
        var assets = new HashSet<string>(StringComparer.Ordinal);
        page.PageError += (_, error) => errors.Add(error);
        page.Console += (_, message) => {
            if (message.Type == "error") {
                errors.Add(message.Text);
            }
        };
        page.Response += (_, response) => {
            var path = new Uri(response.Url).AbsolutePath;
            if (response.Status >= 400) {
                errors.Add($"HTTP {response.Status}: {path}");
            }
            if (path.Contains(".css", StringComparison.Ordinal) || path.Contains(".woff", StringComparison.Ordinal) || path.Contains(".js", StringComparison.Ordinal)) {
                assets.Add(path);
            }
        };
        var directory = Path.Combine(PlaywrightTestHostPaths.RepositoryRoot, "output", "playwright", "provider-history-pp3",
            $"{DateTimeOffset.UtcNow:yyyyMMddTHHmmssfffZ}-{Guid.NewGuid():N}", published ? "published" : "source");
        Directory.CreateDirectory(directory);
        try {
            foreach (var option in HistoryScenarios.Options) {
                await NavigateAsync(option.Token);
                var workspaces = page.GetByTestId("sandbox-history-workspace");
                var workspace = workspaces.First;
                await Assertions.Expect(workspace.GetByTestId("sandbox-history-read-counts")).ToContainTextAsync("Search: 0 · Metadata: 0 · Content: 0");
                if (option.Value == HistoryScenario.Normal) {
                    await workspace.GetByTestId("history-more-filters").ClickAsync();
                    await workspace.GetByTestId("history-page-size").FillAsync("201");
                    await workspace.GetByTestId("history-more-filters").ClickAsync();
                    await workspace.GetByTestId("history-search").ClickAsync();
                    await Assertions.Expect(workspace.Locator(".validation-message").First).ToBeVisibleAsync();
                    await Assertions.Expect(workspace.GetByTestId("sandbox-history-read-counts")).ToContainTextAsync("Search: 0");
                    await workspace.GetByTestId("history-more-filters").ClickAsync();
                    await workspace.GetByTestId("history-page-size").FillAsync("10");
                    await workspace.GetByTestId("history-more-filters").ClickAsync();
                }
                await workspace.GetByTestId("history-search").ClickAsync();
                if (option.Value == HistoryScenario.DelayedSearch) {
                    await workspace.GetByTestId("history-cancel").ClickAsync();
                    await Assertions.Expect(workspace).ToContainTextAsync("Search canceled");
                    await Assertions.Expect(workspace.GetByTestId("sandbox-history-read-counts")).ToContainTextAsync("Pending: 0 · Canceled: 1");
                    await Assertions.Expect(workspace.GetByTestId("history-details")).ToHaveCountAsync(0);
                    continue;
                }
                if (option.Value is HistoryScenario.Denied or HistoryScenario.Failure) {
                    await Assertions.Expect(workspace.GetByTestId("history-error")).ToBeVisibleAsync();
                    continue;
                }
                await Assertions.Expect(workspace.GetByTestId("history-results")).ToBeVisibleAsync();
                if (option.Value == HistoryScenario.Empty) {
                    await Assertions.Expect(workspace).ToContainTextAsync("No matching requests");
                    continue;
                }
                if (option.Value == HistoryScenario.Partial) {
                    await Assertions.Expect(workspace).ToContainTextAsync("Coverage is incomplete");
                }
                if (option.Value == HistoryScenario.TwoWorkspaces) {
                    await Assertions.Expect(workspaces).ToHaveCountAsync(2);
                    await Assertions.Expect(workspaces.Nth(1).GetByTestId("sandbox-history-read-counts")).ToContainTextAsync("Search: 0 · Metadata: 0 · Content: 0");
                    await workspaces.Nth(1).GetByTestId("sandbox-history-scope").ClickAsync();
                    await Assertions.Expect(workspaces.Nth(1).GetByTestId("history-provider")).ToHaveCountAsync(0);
                    await workspaces.Nth(1).GetByTestId("history-search").ClickAsync();
                    await workspace.GetByTestId("sandbox-history-retire").ClickAsync();
                    await Assertions.Expect(workspaces.Nth(1).GetByTestId("history-details")).ToHaveCountAsync(12);
                    await ShotAsync(option.Token);
                    continue;
                }
                if (option.Value == HistoryScenario.Normal) {
                    await Assertions.Expect(workspace.GetByTestId("history-details")).ToHaveCountAsync(10);
                    await workspace.GetByTestId("history-model").FillAsync("Unsubmitted model");
                    await workspace.GetByTestId("history-next").ClickAsync();
                    await Assertions.Expect(workspace.GetByTestId("history-details")).ToHaveCountAsync(2);
                    await Assertions.Expect(workspace.GetByTestId("history-applied")).Not.ToContainTextAsync("Unsubmitted model");
                    await Assertions.Expect(workspace.GetByTestId("history-draft-warning")).ToBeVisibleAsync();
                    await workspace.GetByTestId("history-previous").ClickAsync();
                    await Assertions.Expect(workspace.GetByTestId("history-details")).ToHaveCountAsync(10);
                    await ShotAsync("normal");
                }
                await workspace.GetByTestId("history-details").First.ClickAsync();
                if (option.Value == HistoryScenario.DelayedMetadata) {
                    await page.GetByTestId("history-detail-close").ClickAsync();
                    await Assertions.Expect(workspace.GetByTestId("sandbox-history-read-counts")).ToContainTextAsync("Pending: 0 · Canceled: 1");
                    await Assertions.Expect(page.GetByTestId("history-detail-dialog")).ToHaveCountAsync(0);
                    continue;
                }
                if (option.Value == HistoryScenario.MetadataDenied) {
                    await Assertions.Expect(page.GetByTestId("history-detail-error")).ToContainTextAsync("Access denied");
                    continue;
                }
                var load = page.GetByTestId(option.Value == HistoryScenario.Canonical ? "history-owner-content" : "history-load-content").First;
                await Assertions.Expect(load).ToBeEnabledAsync();
                await Assertions.Expect(workspace.GetByTestId("sandbox-history-read-counts")).ToContainTextAsync("Metadata: 1 · Content: 0");
                await AssertDialogAsync("history-detail-dialog", "history-detail-close");
                await load.FocusAsync();
                await page.Keyboard.PressAsync("Enter");
                if (option.Value == HistoryScenario.DelayedContent) {
                    await page.GetByTestId("history-detail-close").ClickAsync();
                    await Assertions.Expect(workspace.GetByTestId("sandbox-history-read-counts")).ToContainTextAsync("Pending: 0 · Canceled: 1");
                    await Assertions.Expect(page.GetByTestId("history-content-dialog")).ToHaveCountAsync(0);
                    continue;
                }
                if (option.Value == HistoryScenario.ContentDenied) {
                    await Assertions.Expect(page.GetByTestId("history-detail-error")).ToContainTextAsync("Access denied");
                    await Assertions.Expect(page.GetByTestId("history-load-content")).ToHaveCountAsync(0);
                    continue;
                }
                await Assertions.Expect(page.GetByTestId("history-content-state")).ToBeVisibleAsync();
                await AssertDialogAsync("history-content-dialog", "history-content-close");
                if (option.Value == HistoryScenario.Large) {
                    var area = page.GetByTestId("history-content-text").Last;
                    Assert.True(await area.EvaluateAsync<bool>("element => element.scrollHeight > element.clientHeight"));
                    await area.EvaluateAsync("element => element.scrollTop = element.scrollHeight");
                }
                if (option.Value == HistoryScenario.Normal) {
                    await Assertions.Expect(page.GetByTestId("history-content-text").First).ToHaveValueAsync(HistorySandboxFixture.SyntheticInput);
                    await page.Keyboard.PressAsync("Tab");
                    Assert.True(await page.GetByTestId("history-content-dialog").EvaluateAsync<bool>("element => element.contains(document.activeElement)"));
                }
                await ShotAsync(option.Token + "-content");
                await page.Keyboard.PressAsync("Escape");
                await Assertions.Expect(page.GetByTestId("history-content-dialog")).ToHaveCountAsync(0);
                await Assertions.Expect(page.GetByTestId("history-detail-dialog")).ToHaveCountAsync(1);
                await page.Keyboard.PressAsync("Escape");
                await Assertions.Expect(page.GetByTestId("history-detail-dialog")).ToHaveCountAsync(0);
                await Assertions.Expect(workspace.GetByTestId("history-details").First).ToBeFocusedAsync();
                if (option.Value == HistoryScenario.Normal) {
                    foreach (var row in new[] { 1, 0 }) {
                        await workspace.GetByTestId("history-details").Nth(row).ClickAsync();
                        await page.GetByTestId("history-detail-dialog").GetByText("Entry / provider", new() { Exact = true }).WaitForAsync();
                        await page.GetByTestId("history-detail-close").ClickAsync();
                        await Assertions.Expect(page.GetByTestId("history-detail-dialog")).ToHaveCountAsync(0);
                    }
                    await Assertions.Expect(workspace.GetByTestId("sandbox-history-read-counts")).ToContainTextAsync("Metadata: 3 · Content: 1");
                }
            }
            Assert.Empty(errors);
            Assert.Contains(assets, path => path.Contains("CanDoItAll.Components.BaseLib", StringComparison.Ordinal) && path.Contains(".css", StringComparison.Ordinal));
            Assert.Contains(assets, path => path.Contains("CanDoItAll.AgentFramework.UI", StringComparison.Ordinal) && path.EndsWith(".css", StringComparison.Ordinal));
            Assert.Contains(assets, path => path.Contains(".woff", StringComparison.Ordinal));
            Assert.DoesNotContain("fail:", host.Logs, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("crit:", host.Logs, StringComparison.OrdinalIgnoreCase);
            await File.WriteAllTextAsync(Path.Combine(directory, "browser-evidence.json"), JsonSerializer.Serialize(new {
                published, Viewport = "1920x1080@1", Scenarios = HistoryScenarios.Options.Select(option => option.Token), assets, errors
            }));
        } catch {
            await ShotAsync("failure");
            throw;
        } finally {
            await File.WriteAllTextAsync(Path.Combine(directory, "server.log"), host.Logs);
        }

        async Task NavigateAsync(string scenario) {
            await page.GotoAsync($"{host.BaseUrl}/agents?specimen=history&scenario={scenario}");
            await Assertions.Expect(page.GetByTestId("sandbox-history-specimen")).ToHaveAttributeAsync("data-interactive", "true");
        }

        Task ShotAsync(string name) => page.ScreenshotAsync(new() { Path = Path.Combine(directory, name + ".png"), FullPage = false });

        async Task AssertDialogAsync(string id, string footerId) {
            var panel = page.GetByTestId(id).Locator("section").First;
            await Assertions.Expect(panel).ToBeInViewportAsync(new() { Ratio = 1 });
            var bounds = await panel.BoundingBoxAsync();
            Assert.NotNull(bounds);
            Assert.InRange(bounds.Width, 1000, 1920);
            Assert.True(bounds.Y >= 0 && bounds.Y + bounds.Height <= 1080);
            var footer = page.GetByTestId(footerId);
            await Assertions.Expect(footer).ToBeInViewportAsync(new() { Ratio = 1 });
            Assert.True(await footer.EvaluateAsync<bool>("element => { const r = element.getBoundingClientRect(); return element.contains(document.elementFromPoint(r.x + r.width / 2, r.y + r.height / 2)); }"));
        }
    }
}
