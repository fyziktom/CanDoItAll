using System.Text.Json;
using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright;

[Trait("Category", "Playwright")]
[Trait("Category", "HostPlatform")]
public sealed class ProcessesSandboxBrowserTests {
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Complete_independent_workspace_uses_real_renderers_and_assets(bool published, bool fast) {
        await using var host = new ProcessesSandboxHost();
        await host.StartAsync(published, fast);
        using var playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
        await using var context = await browser.NewContextAsync(new() { ViewportSize = new() { Width = 1920, Height = 1080 } });
        await context.Tracing.StartAsync(new() { Screenshots = true, Snapshots = true, Sources = true });
        var page = await context.NewPageAsync();
        var errors = new List<string>();
        var assets = new HashSet<string>();
        page.PageError += (_, error) => errors.Add(error);
        page.Response += (_, response) => {
            var path = new Uri(response.Url).AbsolutePath;
            if (response.Status >= 400) {
                errors.Add($"HTTP {response.Status}: {path}");
            }
            if (path.Contains(".css", StringComparison.Ordinal) || path.Contains(".js", StringComparison.Ordinal) || path.Contains(".woff", StringComparison.Ordinal)) {
                assets.Add(path);
            }
        };
        var directory = Path.Combine(PlaywrightTestHostPaths.RepositoryRoot, "output", "playwright", "processes-pc1",
            $"{(published ? "published" : "source")}-{(fast ? "fast" : "parity")}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try {
            await page.GotoAsync(host.BaseUrl);
            await Assertions.Expect(page.GetByTestId("processes-definition-editor-name")).ToBeVisibleAsync();
            Assert.True((await page.GetByTestId("process-scenario-toolbar").BoundingBoxAsync())!.Height < 110);
            await Assertions.Expect(page.Locator("html")).ToHaveAttributeAsync("data-asset-mode", fast ? "Fast" : "Parity");
            await page.GetByTestId("processes-definition-editor-name").FillAsync("Unsaved independent definition");
            foreach (var tab in new[] { "roles", "steps", "runs", "graphs", "analytics", "exchange", "manager-chat", "definition" }) {
                await Tab(tab);
            }
            await Assertions.Expect(page.GetByTestId("processes-definition-editor-name")).ToHaveValueAsync("Unsaved independent definition");
            await Shot("definition-draft");
            await Tab("roles");
            await page.GetByTestId("processes-role-solution-architect").ClickAsync();
            await page.GetByTestId("processes-role-executor-kind").SelectOptionAsync("Workflow");
            await page.GetByTestId("processes-role-workflow-id").FillAsync("invalid-workflow");
            await page.GetByTestId("processes-role-workflow-id").PressAsync("Tab");
            await page.GetByTestId("processes-role-details-dialog").GetByRole(AriaRole.Button, new() { Name = "Close", Exact = true }).Last.ClickAsync();
            await Tab("runs");
            await Tab("roles");
            await page.GetByTestId("processes-role-solution-architect").ClickAsync();
            await Assertions.Expect(page.GetByTestId("processes-role-workflow-id")).ToHaveValueAsync("invalid-workflow");
            await Assertions.Expect(page.GetByTestId("processes-role-save")).ToBeDisabledAsync();
            await Shot("role-invalid");
            await page.GetByTestId("processes-role-workflow-id").FillAsync("51000000-0000-0000-0000-000000000006");
            await page.GetByTestId("processes-role-workflow-id").PressAsync("Tab");
            await page.GetByTestId("processes-role-save").ClickAsync();
            await Assertions.Expect(page.GetByTestId("processes-role-editor-receipt")).ToContainTextAsync("saved");
            await page.GetByTestId("processes-role-details-dialog").GetByRole(AriaRole.Button, new() { Name = "Close", Exact = true }).Last.ClickAsync();
            await Tab("steps");
            await page.WaitForFunctionAsync("() => !!document.querySelector('.cw-canvas-host')?.__canvasWorkbenchState");
            await page.GetByTitle("Maximize canvas", new() { Exact = true }).ClickAsync();
            await Assertions.Expect(page.Locator(".cw-workbench-shell.is-maximized")).ToBeVisibleAsync();
            await page.GetByTestId("processes-canvas-toggle-toolbox").ClickAsync();
            var point = await ProcessCanvasBrowser.ReadNodePointAsync(page, "step:architecture-decision");
            await page.Mouse.ClickAsync(point.X, point.Y);
            await Assertions.Expect(page.GetByTestId("processes-canvas-selection")).ToContainTextAsync("Architecture decision");
            point = await ProcessCanvasBrowser.ReadNodePointAsync(page, "step:architecture-decision");
            await page.Mouse.MoveAsync(point.X, point.Y);
            await page.Mouse.DownAsync();
            await page.Mouse.MoveAsync(point.X + 40, point.Y + 30, new() { Steps = 8 });
            await page.Mouse.UpAsync();
            await page.GetByTestId("processes-canvas-recompose").FocusAsync();
            await page.GetByTestId("processes-canvas-recompose").PressAsync("Enter");
            await Assertions.Expect(page.GetByTestId("processes-canvas-command-receipt")).ToContainTextAsync("accepted");
            await Assertions.Expect(page.Locator(".cw-workbench-shell.is-maximized")).ToBeVisibleAsync();
            await Shot("canvas-maximized");
            await page.GetByTitle("Dock canvas", new() { Exact = true }).ClickAsync();
            await Assertions.Expect(page.Locator(".cw-workbench-shell.is-maximized")).ToHaveCountAsync(0);
            await page.GetByTestId("processes-step-target-lead-hours").FillAsync("-1");
            await Tab("runs");
            await Tab("steps");
            await Assertions.Expect(page.GetByTestId("processes-step-target-lead-hours")).ToHaveValueAsync("-1");
            await Tab("exchange");
            foreach (var preview in new[] { "markdown", "diagram", "json", "structure" }) {
                await page.GetByTestId("processes-template-library-preview-tab-" + preview).ClickAsync();
                await Assertions.Expect(page.GetByTestId("processes-template-library-preview-" + preview)).ToBeVisibleAsync();
                if (preview == "diagram") {
                    await Assertions.Expect(page.GetByTestId("processes-template-library-preview-diagram").Locator("svg")).ToBeVisibleAsync();
                }
                await Shot("template-" + preview);
            }
            foreach (var (category, kind) in new[] { ("processes", "process"), ("roles", "role"), ("artifacts", "artifact") }) {
                await page.GetByTestId("processes-template-library-category-" + category).ClickAsync();
                if (kind == "artifact") {
                    await page.GetByTestId("processes-template-library-artifact-target").SelectOptionAsync("architecture-decision");
                }
                await page.GetByTestId("processes-template-library-import-" + kind).ClickAsync();
                await Assertions.Expect(page.GetByTestId("processes-template-library-import-receipt")).ToContainTextAsync("Accepted");
            }
            await Tab("manager-chat");
            await page.GetByTestId("chat-prompt-input").FillAsync("Independent conversation draft");
            await page.GetByTestId("chat-send-button").ClickAsync();
            await Assertions.Expect(page.GetByTestId("processes-manager-chat-tab")).ToContainTextAsync("Independent conversation draft");
            await page.GetByLabel("Conversation scenario", new() { Exact = true }).SelectOptionAsync("Attachments");
            await Assertions.Expect(page.GetByTestId("processes-manager-chat-tab")).ToContainTextAsync("notes/research.md");
            await Shot("chat-attachments");
            await page.GetByLabel("Conversation scenario", new() { Exact = true }).SelectOptionAsync("Voice");
            await Assertions.Expect(page.GetByTestId("chat-voice-record-button")).ToHaveAttributeAsync("aria-label", "Stop voice recording");
            await page.GetByTestId("chat-voice-record-button").ClickAsync();
            await Assertions.Expect(page.GetByTestId("chat-voice-record-button")).ToHaveAttributeAsync("aria-label", "Start voice recording");
            await Shot("chat-voice-controls");
            await page.GetByLabel("Conversation scenario", new() { Exact = true }).SelectOptionAsync("Executing");
            await Assertions.Expect(page.GetByTestId("chat-send-button")).ToBeDisabledAsync();
            await page.GetByLabel("Conversation scenario", new() { Exact = true }).SelectOptionAsync("NoAgents");
            await Assertions.Expect(page.GetByTestId("chat-prompt-input")).ToHaveCountAsync(0);
            await page.GetByLabel("Conversation scenario", new() { Exact = true }).SelectOptionAsync("Adversarial");
            Assert.False(await page.EvaluateAsync<bool>("() => window.untrustedAgent === true"));
            await page.GetByRole(AriaRole.Button, new() { Name = "Files", Exact = true }).ClickAsync();
            var files = page.GetByTestId("process-run-files-dialog");
            await files.Locator(".ft-file-browser__item-main").Filter(new() { HasTextString = "evidence.md" }).PressAsync("Enter");
            await Assertions.Expect(files.GetByTestId("interaction-markdown-view")).ToContainTextAsync("Process evidence");
            await Assertions.Expect(files.GetByTestId("interaction-mode-edit")).ToBeDisabledAsync();
            await Shot("files-open");
            await files.GetByRole(AriaRole.Button, new() { Name = "Close", Exact = true }).Last.ClickAsync();
            await page.GetByRole(AriaRole.Button, new() { Name = "Live processes", Exact = true }).ClickAsync();
            foreach (var tab in new[] { "Activity", "Agents", "Graphs", "Tool history" }) {
                await page.GetByRole(AriaRole.Tab, new() { Name = tab, Exact = false }).ClickAsync();
                if (tab == "Graphs") {
                    await Assertions.Expect(page.GetByTestId("live-processes-graphs").Locator(".apexcharts-svg").First).ToBeVisibleAsync();
                }
                await Shot("live-" + tab.Replace(' ', '-'));
            }
            await page.GetByRole(AriaRole.Button, new() { Name = "Fail refresh", Exact = true }).ClickAsync();
            await Assertions.Expect(page.GetByTestId("live-processes-error")).ToContainTextAsync("last accepted projection");
            await page.GetByTestId("live-processes-refresh-button").ClickAsync();
            await Assertions.Expect(page.GetByTestId("live-processes-error")).ToHaveCountAsync(0);
            await page.GetByRole(AriaRole.Button, new() { Name = "Two workspaces", Exact = true }).ClickAsync();
            await Assertions.Expect(page.GetByTestId("processes-shell")).ToHaveCountAsync(2);
            await page.GetByTestId("processes-tab-definitions").Nth(0).ClickAsync();
            await page.GetByTestId("processes-tab-definitions").Nth(1).ClickAsync();
            var names = page.GetByTestId("processes-definition-editor-name");
            var secondName = await names.Nth(1).InputValueAsync();
            await names.Nth(0).FillAsync("Only the first opening");
            await Assertions.Expect(names.Nth(1)).ToHaveValueAsync(secondName);
            await Shot("two-openings");
            Assert.Contains(assets, path => path.Contains("material-symbols", StringComparison.Ordinal));
            Assert.Contains(assets, path => path.Contains(".woff", StringComparison.Ordinal));
            Assert.Contains(assets, path => path.Contains("CanDoItAll.Processes.UiSandbox", StringComparison.Ordinal) && path.Contains(".css", StringComparison.Ordinal));
            Assert.True(await page.EvaluateAsync<bool>("document.fonts.check('16px \"Material Symbols Rounded\"')"));
            Assert.Empty(errors);
            Assert.DoesNotContain("Unhandled exception", host.Logs, StringComparison.Ordinal);
            await File.WriteAllTextAsync(Path.Combine(directory, "served-assets.json"), JsonSerializer.Serialize(assets.Order()));
        } catch {
            await Shot("failure");
            await File.WriteAllTextAsync(Path.Combine(directory, "failure.txt"), await page.Locator("body").InnerTextAsync());
            throw;
        } finally {
            await context.Tracing.StopAsync(new() { Path = Path.Combine(directory, "trace.zip") });
        }
        Task Tab(string tab) => page.GetByTestId(tab == "definition" ? "processes-tab-definitions" : "processes-detail-tab-" + tab).ClickAsync();
        Task Shot(string name) => page.ScreenshotAsync(new() { Path = Path.Combine(directory, name + ".png") });
    }
}
