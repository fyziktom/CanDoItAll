using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright;

public sealed partial class AppSmokeTests
{
    [Fact]
    public async Task Process_management_canvas_bundle_flows_are_validated_in_browser()
    {
        var repoRoot = GetRepoRoot();
        var artifactsDir = Path.Combine(repoRoot, "output", "playwright", "process-management-bundle");
        ResetDirectory(artifactsDir);

        await using var context = await fixture.Browser.NewContextAsync(new BrowserNewContextOptions
        {
            IgnoreHTTPSErrors = true,
            ViewportSize = new ViewportSize
            {
                Width = 1900,
                Height = 1200
            }
        });

        var page = await context.NewPageAsync();
        var response = await page.GotoAsync($"{fixture.BaseUrl}/processes");
        Assert.NotNull(response);
        Assert.True(response!.Ok, $"Expected /processes to return 2xx, got {(int)response.Status}.");

        var startupDialog = page.GetByTestId("database-startup-modal");
        if (await WaitForLocatorAsync(startupDialog, 15_000))
        {
            await page.GetByTestId("database-startup-continue").ClickAsync();
            await startupDialog.WaitForAsync(new() { State = WaitForSelectorState.Detached });
        }
        await page.GetByTestId("processes-seed-baseline-button").WaitForAsync();
        await page.GetByTestId("processes-seed-baseline-button").ClickAsync();
        await WaitForBodyTextAsync(page, "Development seed baseline prepared.", 30_000);
        await WaitForBodyTextAsync(page, "Multi-team software delivery and release governance", 30_000);

        var stepsTab = page.GetByRole(AriaRole.Tab, new() { Name = "Steps", Exact = true });
        await stepsTab.WaitForAsync();
        await stepsTab.ClickAsync();
        await WaitForInitializedCanvasHostAsync(page);
        await WaitForCanvasRenderIdleAsync(page);

        var selectionToggle = page.GetByTestId("processes-canvas-toggle-selection");
        var toolboxToggle = page.GetByTestId("processes-canvas-toggle-toolbox");
        await selectionToggle.WaitForAsync();
        await toolboxToggle.WaitForAsync();
        Assert.True(await selectionToggle.IsVisibleAsync(), "Expected the definition selection toggle to be visible.");
        Assert.True(await toolboxToggle.IsVisibleAsync(), "Expected the definition toolbox toggle to be visible.");

        await page.Locator(".cw-stage-surface").First.ScreenshotAsync(new()
        {
            Path = Path.Combine(artifactsDir, "01-definition-canvas-toolbar.png")
        });

        await toolboxToggle.ClickAsync();
        var toolboxWindow = page.GetByTestId("processes-canvas-toolbox-window");
        await toolboxWindow.WaitForAsync();
        await toolboxWindow.GetByPlaceholder("Search templates, roles, steps, review, or release").FillAsync("qa");
        await page.GetByTestId("processes-toolbox-process-step.qa").WaitForAsync();
        await page.GetByTestId("processes-toolbox-process-step.qa").ClickAsync();

        var editorWindow = page.GetByTestId("processes-canvas-editor-window");
        await editorWindow.WaitForAsync();
        await page.GetByTestId("processes-canvas-template-select").WaitForAsync();
        await page.GetByTestId("processes-canvas-step-editor").WaitForAsync();
        await editorWindow.ScreenshotAsync(new()
        {
            Path = Path.Combine(artifactsDir, "02-step-editor-from-toolbox.png")
        });
        await editorWindow.GetByRole(AriaRole.Button, new() { Name = "Hide window", Exact = true }).ClickAsync();
        await editorWindow.WaitForAsync(new() { State = WaitForSelectorState.Hidden });

        var definitionNodes = await page.EvaluateAsync<ProcessCanvasNodeProof[]>(
            @"() => {
                const state = document.querySelector('.cw-canvas-host')?.__canvasWorkbenchState;
                const nodes = Array.isArray(state?.surface?.nodes) ? state.surface.nodes : [];
                return nodes
                    .filter(node => typeof node?.id === 'string' && node.id.startsWith('step:'))
                    .slice(0, 2)
                    .map(node => ({
                        id: node.id,
                        title: node.title || node.subtitle || node.id
                    }));
            }");
        Assert.NotNull(definitionNodes);
        Assert.True(definitionNodes!.Length >= 2, "Expected at least two definition nodes in the process canvas.");

        var firstStepSelector = $".cw-node[data-node-id='{definitionNodes[0].Id}']";
        var secondStepSelector = $".cw-node[data-node-id='{definitionNodes[1].Id}']";
        var selectionWindow = page.GetByTestId("processes-canvas-selection-window");

        await EnsureCanvasSelectionAsync(page, firstStepSelector);
        await selectionWindow.WaitForAsync();
        await page.WaitForFunctionAsync(
            @"title => {
                const panel = document.querySelector('[data-testid=""processes-canvas-selection-window""]');
                return !!panel && (panel.textContent || '').includes(title);
            }",
            definitionNodes[0].Title);
        await EnsureCanvasSelectionAsync(page, secondStepSelector);
        await page.WaitForFunctionAsync(
            @"title => {
                const panel = document.querySelector('[data-testid=""processes-canvas-selection-window""]');
                return !!panel && (panel.textContent || '').includes(title);
            }",
            definitionNodes[1].Title);
        await selectionWindow.ScreenshotAsync(new()
        {
            Path = Path.Combine(artifactsDir, "03-definition-selection-window.png")
        });

        var selectionWindowText = await selectionWindow.TextContentAsync();
        Assert.Contains("Edit step", selectionWindowText, StringComparison.Ordinal);
        Assert.Contains("Add dependent step", selectionWindowText, StringComparison.Ordinal);
        Assert.Contains("Add role binding", selectionWindowText, StringComparison.Ordinal);
        await selectionWindow.GetByRole(AriaRole.Button, new() { Name = "Add dependent step", Exact = true }).ClickAsync();
        await editorWindow.WaitForAsync();
        await page.GetByTestId("processes-canvas-step-editor").WaitForAsync();
        await editorWindow.GetByRole(AriaRole.Button, new() { Name = "Hide window", Exact = true }).ClickAsync();
        await editorWindow.WaitForAsync(new() { State = WaitForSelectorState.Hidden });

        var actionDialog = page.GetByTestId("processes-canvas-action-dialog");
        await OpenProcessCanvasActionDialogAsync(page, secondStepSelector);
        await actionDialog.ScreenshotAsync(new()
        {
            Path = Path.Combine(artifactsDir, "05-definition-double-click-actions.png")
        });
        await actionDialog.GetByRole(AriaRole.Button, new() { Name = "Close", Exact = true }).ClickAsync();
        await actionDialog.WaitForAsync(new() { State = WaitForSelectorState.Hidden });

        var runsTab = page.GetByRole(AriaRole.Tab, new() { Name = "Runs", Exact = true });
        await runsTab.WaitForAsync();
        await runsTab.ClickAsync();
        await WaitForInitializedCanvasHostAsync(page);
        await WaitForCanvasRenderIdleAsync(page);
        await page.GetByTestId("processes-runtime-toggle-selection").WaitForAsync();

        var runtimeNodes = await page.EvaluateAsync<ProcessCanvasNodeProof[]>(
            @"() => {
                const state = document.querySelector('.cw-canvas-host')?.__canvasWorkbenchState;
                const nodes = Array.isArray(state?.surface?.nodes) ? state.surface.nodes : [];
                return nodes
                    .filter(node => typeof node?.id === 'string' && node.id.startsWith('run-step:'))
                    .slice(0, 1)
                    .map(node => ({
                        id: node.id,
                        title: node.title || node.subtitle || node.id
                    }));
            }");
        Assert.NotNull(runtimeNodes);
        Assert.True(runtimeNodes!.Length > 0, "Expected a runtime node to be available in the selected run.");

        var runtimeStepSelector = $".cw-node[data-node-id='{runtimeNodes[0].Id}']";
        var runtimeSelectionWindow = page.GetByTestId("processes-runtime-selection-window");
        await ClickCanvasNodeAsync(page, runtimeStepSelector);
        await runtimeSelectionWindow.WaitForAsync();
        await page.WaitForFunctionAsync(
            @"title => {
                const panel = document.querySelector('[data-testid=""processes-runtime-selection-window""]');
                return !!panel && (panel.textContent || '').includes(title);
            }",
            runtimeNodes[0].Title);
        await runtimeSelectionWindow.GetByRole(AriaRole.Button, new() { Name = "More actions", Exact = true }).WaitForAsync();
        await runtimeSelectionWindow.ScreenshotAsync(new()
        {
            Path = Path.Combine(artifactsDir, "06-runtime-selection-window.png")
        });

        Assert.False(await page.Locator("#blazor-error-ui").IsVisibleAsync());
    }

    private sealed class ProcessCanvasNodeProof
    {
        public string Id { get; set; } = string.Empty;

        public string Title { get; set; } = string.Empty;
    }
}
