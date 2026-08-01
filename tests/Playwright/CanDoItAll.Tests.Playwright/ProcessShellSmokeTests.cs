using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright;

[Collection(PlaywrightCollection.Name)]
public sealed class ProcessShellSmokeTests
{
    private readonly PlaywrightAppFixture fixture;

    public ProcessShellSmokeTests(PlaywrightAppFixture fixture)
    {
        this.fixture = fixture;
    }

    [Fact]
    public async Task Process_canvas_stays_maximized_through_selection_and_recomposition()
    {
        await using var context = await fixture.Browser.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize
            {
                Width = 2048,
                Height = 1200
            }
        });
        var page = await context.NewPageAsync();
        var pageErrors = new List<string>();
        page.PageError += (_, message) => pageErrors.Add(message);

        var response = await page.GotoAsync($"{fixture.BaseUrl}/processes");
        Assert.NotNull(response);
        Assert.True(response!.Ok, $"Expected /processes to return 2xx, got {(int)response.Status}.");
        await DismissStartupModalIfPresentAsync(page, timeoutMs: 15_000);
        await page.GetByTestId("processes-shell").WaitForAsync();
        await page.GetByTestId("processes-definition-search").FillAsync("software-delivery");
        await page.GetByTestId("processes-definition-search-submit").ClickAsync();
        await page.GetByTestId("processes-definition-software-delivery").ClickAsync();
        await page.GetByTestId("processes-detail-tab-steps").ClickAsync();
        await page.GetByTestId("processes-definition-canvas").WaitForAsync();
        await page.Locator(".cw-canvas-host").WaitForAsync();

        var initial = await ReadProcessCanvasRuntimeAsync(page);
        await page.GetByTitle("Maximize canvas", new() { Exact = true }).ClickAsync();
        await page.Locator(".cw-workbench-shell.is-maximized").WaitForAsync();
        await WaitForTwoAnimationFramesAsync(page);

        var maximized = await ReadProcessCanvasRuntimeAsync(page);
        Assert.True(maximized.IsMaximized);
        Assert.True(maximized.StateIsMaximized);
        Assert.True(maximized.BodyLocked);
        Assert.InRange(maximized.RenderCount - initial.RenderCount, 1, 10);

        var intakeNode = page.GetByTestId("processes-canvas-node-step-feature-intake");
        await intakeNode.WaitForAsync(new() { State = WaitForSelectorState.Attached });
        await intakeNode.EvaluateAsync("element => element.click()");
        await ExpectTextContainsAsync(page.GetByTestId("processes-canvas-selection"), "Clarify .NET scope");
        await WaitForTwoAnimationFramesAsync(page);

        var selected = await ReadProcessCanvasRuntimeAsync(page);
        Assert.True(selected.IsMaximized);
        Assert.True(selected.StateIsMaximized);
        Assert.True(selected.BodyLocked);
        Assert.InRange(selected.RenderCount - maximized.RenderCount, 0, 6);

        await page.GetByTestId("processes-canvas-recompose").ClickAsync();
        await ExpectTextContainsAsync(page.GetByTestId("processes-canvas-command-receipt"), "recomposed");
        await page.WaitForTimeoutAsync(500);

        var recomposed = await ReadProcessCanvasRuntimeAsync(page);
        Assert.True(recomposed.IsMaximized);
        Assert.True(recomposed.StateIsMaximized);
        Assert.True(recomposed.BodyLocked);
        Assert.Equal(recomposed.IntakeY, recomposed.SuccessTerminalY);
        Assert.NotEqual(recomposed.IntakeY, recomposed.RepairY);
        Assert.False(await page.Locator("#blazor-error-ui").IsVisibleAsync());
        Assert.Empty(pageErrors);
    }

    [Fact]
    public async Task Process_shell_routes_render_global_and_project_scoped_workspaces()
    {
        var artifactDirectory = Path.Combine(
            PlaywrightTestHostPaths.RepositoryRoot,
            "output",
            "playwright",
            "process-shell-route-proof");
        Directory.CreateDirectory(artifactDirectory);

        await using var context = await fixture.Browser.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize
            {
                Width = 1440,
                Height = 900
            }
        });
        var page = await context.NewPageAsync();
        var consoleMessages = new List<string>();
        var failedRequests = new List<string>();
        var ignoredFailedRequests = new List<string>();
        var pageErrors = new List<string>();
        page.Console += (_, message) => consoleMessages.Add($"{message.Type}: {message.Text}");
        page.RequestFailed += (_, request) =>
        {
            var failure = $"{request.Method} {request.Url} {request.Failure}";
            if (request.Url.Contains("/_blazor/disconnect", StringComparison.OrdinalIgnoreCase))
            {
                ignoredFailedRequests.Add(failure);
                return;
            }

            failedRequests.Add(failure);
        };
        page.PageError += (_, message) => pageErrors.Add(message);

        var globalResponse = await page.GotoAsync($"{fixture.BaseUrl}/processes");
        Assert.NotNull(globalResponse);
        Assert.True(globalResponse!.Ok, $"Expected /processes to return 2xx, got {(int)globalResponse.Status}.");
        await DismissStartupModalIfPresentAsync(page);
        await page.GetByTestId("processes-shell").WaitForAsync();
        await page.GetByTestId("processes-command-strip").WaitForAsync();
        await page.GetByTestId("processes-tab-definitions").WaitForAsync();
        await page.GetByTestId("processes-definition-tree").WaitForAsync();
        await page.GetByTestId("processes-definition-search").FillAsync("architecture");
        await page.GetByTestId("processes-definition-search-submit").ClickAsync();
        await page.GetByTestId("processes-definition-architecture-decision-governance").WaitForAsync();
        await page.GetByTestId("processes-definition-architecture-decision-governance").ClickAsync();
        await page.GetByTestId("processes-definition-editor").WaitForAsync();
        await page.GetByTestId("processes-definition-editor-name").FillAsync("Architecture decision governance regression");
        await page.GetByTestId("processes-definition-editor-owner").FillAsync("Architecture board");
        await page.GetByTestId("processes-definition-editor-manager-override").FillAsync("Use the architecture board manager.");
        await page.GetByTestId("processes-definition-save").ClickAsync();
        await ExpectTextContainsAsync(page.GetByTestId("processes-definition-editor-receipt"), "Draft saved");
        await page.GetByTestId("processes-definition-publish").ClickAsync();
        await ExpectTextContainsAsync(page.GetByTestId("processes-definition-editor-receipt"), "published");
        await page.GetByTestId("processes-detail-tab-steps").ClickAsync();
        await page.GetByTestId("processes-detail-panel-steps").WaitForAsync();
        await page.GetByTestId("processes-definition-canvas").WaitForAsync();
        await page.Locator(".cw-workbench").WaitForAsync();
        await page.Locator(".cw-workbench__canvas--nodes").WaitForAsync();
        await page.GetByTestId("processes-canvas-node-step-decision-intake")
            .EvaluateAsync("element => element.click()");
        await ExpectTextContainsAsync(page.GetByTestId("processes-canvas-selection"), "Capture architecture decision demand");
        await page.GetByTestId("processes-canvas-toolbox-process-step-implementation").ClickAsync();
        await ExpectTextContainsAsync(page.GetByTestId("processes-canvas-command-receipt"), "accepted");
        await page.GetByTestId("processes-canvas-node-step-implementation")
            .WaitForAsync(new() { State = WaitForSelectorState.Attached });
        await page.GetByTestId("processes-canvas-node-step-implementation")
            .EvaluateAsync("element => element.click()");
        await ExpectTextContainsAsync(page.GetByTestId("processes-canvas-selection"), "Implementation");
        await page.GetByTestId("processes-canvas-recompose").ClickAsync();
        await ExpectTextContainsAsync(page.GetByTestId("processes-canvas-command-receipt"), "recomposed");
        await page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = Path.Combine(artifactDirectory, "processes-definition-canvas.png"),
            FullPage = true
        });
        await page.GetByTestId("processes-definition-step-editor").WaitForAsync();
        await ExpectTextContainsAsync(page.GetByTestId("processes-definition-step-editor"), "Capture architecture decision demand");
        await page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = Path.Combine(artifactDirectory, "processes-definition-step-editor.png"),
            FullPage = true
        });
        await page.GetByTestId("processes-detail-tab-exchange").ClickAsync();
        await page.GetByTestId("processes-detail-panel-exchange").WaitForAsync();
        await page.GetByTestId("processes-template-library").WaitForAsync();
        await page.GetByTestId("processes-template-library-search").FillAsync("AI-assisted");
        await page.GetByTestId("processes-template-library-search-submit").ClickAsync();
        await page.GetByTestId("processes-template-library-category-processes").ClickAsync();
        await page.GetByTestId("processes-template-library-item-process-ai-assisted-change-delivery").WaitForAsync();
        await page.GetByTestId("processes-template-library-item-process-ai-assisted-change-delivery").ClickAsync();
        await ExpectTextContainsAsync(page.GetByTestId("processes-template-library-preview"), "AI-assisted");
        await page.GetByTestId("processes-template-library-import-process").WaitForAsync();
        await page.GetByTestId("processes-template-library-preview-tab-markdown").ClickAsync();
        await page.GetByTestId("processes-template-library-markdown").WaitForAsync();
        await page.GetByTestId("processes-template-library-preview-tab-diagram").ClickAsync();
        await page.GetByTestId("processes-template-library-diagram").WaitForAsync();
        await page.GetByTestId("processes-template-library-preview-tab-json").ClickAsync();
        await page.GetByTestId("processes-template-library-json").WaitForAsync();
        await page.GetByTestId("processes-template-library-preview-tab-structure").ClickAsync();
        await page.GetByTestId("processes-template-library-structure").WaitForAsync();
        await page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = Path.Combine(artifactDirectory, "processes-template-library-preview.png"),
            FullPage = true
        });
        await page.GetByTestId("processes-detail-tab-roles").ClickAsync();
        await page.GetByTestId("processes-detail-panel-roles").WaitForAsync();
        await page.GetByTestId("processes-definition-role-editor").WaitForAsync();
        await page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = Path.Combine(artifactDirectory, "processes-definition-role-editor.png"),
            FullPage = true
        });
        await page.GetByTestId("processes-detail-tab-graphs").ClickAsync();
        await page.GetByTestId("processes-process-graphs-tab").WaitForAsync();
        await page.GetByTestId("processes-detail-tab-manager-chat").ClickAsync();
        await page.GetByTestId("processes-manager-chat-tab").WaitForAsync();
        await page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = Path.Combine(artifactDirectory, "processes-global-definition-catalog.png"),
            FullPage = true
        });
        Assert.False(await page.Locator("#blazor-error-ui").IsVisibleAsync());

        var liveResponse = await page.GotoAsync($"{fixture.BaseUrl}/processes/live");
        Assert.NotNull(liveResponse);
        Assert.True(liveResponse!.Ok, $"Expected /processes/live to return 2xx, got {(int)liveResponse.Status}.");
        await page.GetByTestId("live-processes-dashboard").WaitForAsync();
        await page.GetByTestId("live-processes-command-strip").WaitForAsync();
        await page.GetByTestId("live-processes-tabs").WaitForAsync();
        await page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = Path.Combine(artifactDirectory, "processes-live-dashboard.png"),
            FullPage = true
        });
        Assert.False(await page.Locator("#blazor-error-ui").IsVisibleAsync());

        var projectId = await CreateProjectAsync(page, "Playwright Process Shell", "Discovery");
        var runId = Guid.Parse("55555555-5555-5555-5555-555555555555");
        var projectResponse = await page.GotoAsync($"{fixture.BaseUrl}/projects/{projectId:D}/processes?runId={runId:D}");
        Assert.NotNull(projectResponse);
        Assert.True(projectResponse!.Ok, $"Expected project-scoped processes route to return 2xx, got {(int)projectResponse.Status}.");
        await page.GetByTestId("processes-shell").WaitForAsync();
        await page.GetByTestId("processes-detail-panel-runs").WaitForAsync();
        await page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = Path.Combine(artifactDirectory, "processes-project-shell.png"),
            FullPage = true
        });
        Assert.False(await page.Locator("#blazor-error-ui").IsVisibleAsync());
        await WriteBrowserValidationSummaryAsync(artifactDirectory, consoleMessages, failedRequests, ignoredFailedRequests, pageErrors);
        Assert.Empty(pageErrors);
        Assert.Empty(failedRequests);
    }

    private async Task<Guid> CreateProjectAsync(IPage page, string projectName, string phase)
    {
        await page.GotoAsync($"{fixture.BaseUrl}/projects");
        await DismissStartupModalIfPresentAsync(page, timeoutMs: 15_000);
        await page.GetByTestId("projects-new-button").WaitForAsync();
        await page.GetByTestId("projects-new-button").ClickAsync();

        try
        {
            await page.GetByTestId("project-name-input").WaitForAsync(new() { Timeout = 2_000 });
        }
        catch (TimeoutException)
        {
            await page.GetByTestId("projects-new-button").ClickAsync();
            await page.GetByTestId("project-name-input").WaitForAsync();
        }

        await page.GetByTestId("project-name-input").FillAsync(projectName);
        await page.GetByTestId("project-phase-input").FillAsync(phase);
        await Task.WhenAll(
            page.WaitForURLAsync("**/projects/*/structure"),
            page.GetByRole(AriaRole.Button, new() { Name = "Save and open structure" }).ClickAsync());

        var match = Regex.Match(page.Url, @"/projects/(?<projectId>[0-9a-fA-F-]+)/structure$", RegexOptions.IgnoreCase);
        Assert.True(match.Success, $"Could not parse project id from {page.Url}.");
        return Guid.Parse(match.Groups["projectId"].Value);
    }

    private static async Task DismissStartupModalIfPresentAsync(IPage page, float timeoutMs = 1_500)
    {
        var startupDialog = page.GetByTestId("database-startup-modal");
        if (!await WaitForLocatorAsync(startupDialog, timeoutMs))
        {
            return;
        }

        await page.GetByTestId("database-startup-continue").ClickAsync();
        await startupDialog.WaitForAsync(new() { State = WaitForSelectorState.Detached });
    }

    private static async Task<bool> WaitForLocatorAsync(ILocator locator, float timeoutMs)
    {
        try
        {
            await locator.WaitForAsync(new() { Timeout = timeoutMs });
            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
    }

    private static async Task ExpectTextContainsAsync(ILocator locator, string expectedText)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(10);
        string? renderedText = null;
        while (DateTimeOffset.UtcNow < deadline)
        {
            renderedText = await locator.TextContentAsync();
            if (renderedText?.Contains(expectedText, StringComparison.OrdinalIgnoreCase) == true)
            {
                return;
            }

            await Task.Delay(100);
        }

        Assert.Fail($"Expected locator text to contain '{expectedText}', but saw '{renderedText}'.");
    }

    private static Task WaitForTwoAnimationFramesAsync(IPage page)
        => page.EvaluateAsync(
            "() => new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve)))");

    private static Task<ProcessCanvasRuntimeSnapshot> ReadProcessCanvasRuntimeAsync(IPage page)
        => page.EvaluateAsync<ProcessCanvasRuntimeSnapshot>(
            @"() => {
                const shell = document.querySelector('.cw-workbench-shell');
                const host = document.querySelector('.cw-canvas-host');
                const state = host?.__canvasWorkbenchState;
                const nodes = Array.isArray(state?.surface?.nodes) ? state.surface.nodes : [];
                const y = nodeId => nodes.find(node => node.id === nodeId)?.y ?? Number.NaN;
                return {
                    isMaximized: shell?.classList.contains('is-maximized') === true,
                    stateIsMaximized: state?.ui?.isMaximized === true,
                    bodyLocked: document.body.classList.contains('cw-body-lock'),
                    renderCount: state?.metrics?.renderCount ?? 0,
                    intakeY: y('step:feature-intake'),
                    successTerminalY: y('step:post-release-learning'),
                    repairY: y('step:quality-repair')
                };
            }");

    private static Task WriteBrowserValidationSummaryAsync(
        string artifactDirectory,
        IReadOnlyCollection<string> consoleMessages,
        IReadOnlyCollection<string> failedRequests,
        IReadOnlyCollection<string> ignoredFailedRequests,
        IReadOnlyCollection<string> pageErrors)
    {
        var lines = new List<string>
        {
            "# Process Shell Browser Validation Summary",
            string.Empty,
            $"ConsoleMessages={consoleMessages.Count}",
            $"FailedRequests={failedRequests.Count}",
            $"IgnoredFailedRequests={ignoredFailedRequests.Count}",
            $"PageErrors={pageErrors.Count}",
            string.Empty,
            "## Console",
        };
        lines.AddRange(consoleMessages.Count == 0 ? ["None."] : consoleMessages);
        lines.Add(string.Empty);
        lines.Add("## Failed Requests");
        lines.AddRange(failedRequests.Count == 0 ? ["None."] : failedRequests);
        lines.Add(string.Empty);
        lines.Add("## Ignored Failed Requests");
        lines.AddRange(ignoredFailedRequests.Count == 0 ? ["None."] : ignoredFailedRequests);
        lines.Add(string.Empty);
        lines.Add("## Page Errors");
        lines.AddRange(pageErrors.Count == 0 ? ["None."] : pageErrors);

        return File.WriteAllLinesAsync(
            Path.Combine(artifactDirectory, "browser-validation-summary.txt"),
            lines);
    }

    private sealed class ProcessCanvasRuntimeSnapshot
    {
        public bool IsMaximized { get; set; }

        public bool StateIsMaximized { get; set; }

        public bool BodyLocked { get; set; }

        public int RenderCount { get; set; }

        public double IntakeY { get; set; }

        public double SuccessTerminalY { get; set; }

        public double RepairY { get; set; }
    }
}
