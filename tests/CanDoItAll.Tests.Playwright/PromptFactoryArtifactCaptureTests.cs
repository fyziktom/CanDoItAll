using System.IO;
using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright;

public sealed partial class PromptLibraryVerificationTests
{
    [Fact]
    [Trait("Surface", "PromptFactory")]
    [Trait("Artifacts", "Deterministic")]
    public async Task Prompt_factory_artifacts_capture_toolbox_preview_and_single_add_flow()
    {
        var repoRoot = GetRepoRoot();
        var screenshotsRoot = Path.Combine(repoRoot, "artifacts", "screenshots");
        var i21Root = Path.Combine(screenshotsRoot, "i21");
        var i22Root = Path.Combine(screenshotsRoot, "i22");
        var i24Root = Path.Combine(screenshotsRoot, "i24");

        ResetDirectory(i21Root);
        ResetDirectory(i22Root);
        ResetDirectory(i24Root);

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
        await LoadPromptFactoryAsync(page);
        await ResetSessionAsync(page);

        var toolboxWindow = page.GetByTestId("prompt-factory-components-toolbox-window");
        await toolboxWindow.WaitForAsync();
        await page.WaitForSelectorAsync("text=112 items");

        await CaptureWorkspaceAsync(page, Path.Combine(i21Root, "01-primary-state.png"));

        var search = page.Locator("[data-testid='prompt-factory-components-toolbox'] .cw-context-toolbox__search");
        await search.FillAsync("validation");
        await page.WaitForFunctionAsync(
            @"() => {
                const items = Array.from(document.querySelectorAll('[data-testid^=""prompt-factory-component-""]'));
                return items.length > 0 &&
                    items.some(item => (item.textContent || '').toLowerCase().includes('validation'));
            }");
        await CaptureWorkspaceAsync(page, Path.Combine(i21Root, "02-secondary-state.png"));

        await page.EvaluateAsync(
            @"() => {
                const body = document.querySelector('[data-testid=""prompt-factory-components-toolbox""] .pf-components-toolbox__body');
                if (!(body instanceof HTMLElement)) {
                    return;
                }

                body.scrollTop = Math.max(420, body.scrollHeight * 0.58);
            }");
        await page.WaitForTimeoutAsync(180);
        await CaptureWorkspaceAsync(page, Path.Combine(i21Root, "03-interaction-result.png"));

        await search.FillAsync("architecture lead");
        await page.GetByTestId("prompt-factory-component-role-architecture-lead").WaitForAsync();
        await HoverComponentPreviewAsync(page, "role-architecture-lead");
        await AssertComponentPreviewPlacementAsync(page, "right");
        await CaptureWorkspaceAsync(page, Path.Combine(i22Root, "01-primary-state.png"));

        await DragFloatingWindowAsync(page, "prompt-factory-components-toolbox-window", 1600, 86);
        await HoverComponentPreviewAsync(page, "role-architecture-lead");
        await AssertComponentPreviewPlacementAsync(page, "left");
        await CaptureWorkspaceAsync(page, Path.Combine(i22Root, "02-secondary-state.png"));

        await page.Keyboard.PressAsync("Escape");
        await ResetSessionAsync(page);
        var resetWindowButton = page.GetByTestId("prompt-factory-components-toolbox-window")
            .GetByRole(AriaRole.Button, new() { Name = "Reset", Exact = true });
        if (await WaitForLocatorAsync(resetWindowButton, 1_000))
        {
            await resetWindowButton.ClickAsync();
            await page.WaitForTimeoutAsync(180);
        }

        await search.FillAsync("senior reviewer");
        await page.GetByTestId("prompt-factory-component-role-senior-reviewer").WaitForAsync();
        await CaptureWorkspaceAsync(page, Path.Combine(i24Root, "01-primary-state.png"));
        await InvokeCanvasCreateActionAsync(page, "component:add:role-senior-reviewer", []);
        await InvokeCanvasCreateActionAsync(page, "component:add:role-senior-reviewer", []);
        await WaitForNodeAsync(page, "selection:component:role-senior-reviewer");
        Assert.Equal(
            1,
            await page.Locator(".cw-node[data-node-id='selection:component:role-senior-reviewer']").CountAsync());

        var selectedComponentCount = await page.EvaluateAsync<int>(
            @"() => document.querySelectorAll('.cw-node[data-node-id^=""selection:component:""]').length");
        Assert.Equal(1, selectedComponentCount);
        await CaptureWorkspaceAsync(page, Path.Combine(i24Root, "02-secondary-state.png"));

        await FocusCanvasNodeAsync(page, "selection:component:role-senior-reviewer");
        await CaptureCanvasStageAsync(page, Path.Combine(i24Root, "03-interaction-result.png"));
    }
}
