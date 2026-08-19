using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright.Smoke;

public sealed partial class AppSmokeTests
{
    [Fact]
    public async Task Project_structure_context_menu_exposes_working_edit_for_every_node_catalog_branch()
    {
        await using var context = await fixture.Browser.NewContextAsync();
        var page = await context.NewPageAsync();
        var projectId = await CreateProjectAsync(page, "Playwright Universal Node Edit", "Validation");

        var projectRootSelector = $".cw-node[data-node-id='project:{projectId}']";
        await PhysicalRightClickCanvasNodeAsync(page, projectRootSelector);
        await AssertSingleVisibleEditActionAsync(page);
        await page.Keyboard.PressAsync("Escape");

        const string originalTitle = "Editable direct-open link";
        const string editedTitle = "Edited direct-open link";
        const string linkUrl = "https://example.com/editable";
        await CreateWebLinkAsync(page, originalTitle, linkUrl, "Edit must remain available beside the direct-open action.");
        await page.WaitForTimeoutAsync(500);

        await PhysicalRightClickCanvasNodeAsync(page, $".cw-node:has-text('{originalTitle}')");
        var editAction = await AssertSingleVisibleEditActionAsync(page);
        await editAction.ClickAsync();

        var composer = page.Locator(".cw-canvas-composer.is-dialog");
        await composer.WaitForAsync();
        var titleInput = composer.Locator(".cw-canvas-composer__input").Nth(0);
        Assert.Equal(originalTitle, await titleInput.InputValueAsync());
        Assert.Equal(linkUrl, await composer.Locator(".cw-canvas-composer__input").Nth(1).InputValueAsync());
        await Assertions.Expect(composer.GetByRole(AriaRole.Button, new() { Name = "Save changes", Exact = true })).ToBeVisibleAsync();

        await titleInput.FillAsync(editedTitle);
        await composer.GetByRole(AriaRole.Button, new() { Name = "Save changes", Exact = true }).ClickAsync();
        await Assertions.Expect(composer).ToBeHiddenAsync();
        await WaitForSceneNodeTitleAsync(page, editedTitle, selectedOnly: true, timeoutMs: 15_000);
    }

    private static async Task PhysicalRightClickCanvasNodeAsync(IPage page, string selector)
    {
        await page.Keyboard.PressAsync("Escape");
        var nodeId = await ResolveCanvasNodeIdAsync(page, selector);
        Assert.False(string.IsNullOrWhiteSpace(nodeId), $"Expected to resolve a canvas node id for '{selector}'.");
        var focused = await page.EvaluateAsync<bool>(
            @"nodeId => {
                const host = document.querySelector('.cw-canvas-host');
                const runtime = window.CanDoItAll?.canvasWorkbench;
                if (!host || !runtime?.focusNode) {
                    return false;
                }

                runtime.focusNode(host, nodeId);
                return true;
            }",
            nodeId);
        Assert.True(focused, $"Expected to focus the canvas node for '{selector}'.");
        await page.WaitForTimeoutAsync(500);
        var hotZoneCenter = await TryResolveCanvasHotZoneCenterAsync(page, selector, "node-body");
        Assert.NotNull(hotZoneCenter);
        await page.Mouse.MoveAsync((float)hotZoneCenter.X, (float)hotZoneCenter.Y);
        await page.Mouse.ClickAsync(
            (float)hotZoneCenter.X,
            (float)hotZoneCenter.Y,
            new MouseClickOptions
            {
                Button = MouseButton.Right
            });
        Assert.True(
            await WaitForContextMenuAsync(page, 3_000),
            $"Expected a physical right-click to open the context menu for '{selector}'.");
    }

    private static async Task<ILocator> AssertSingleVisibleEditActionAsync(IPage page)
    {
        var editAction = page.Locator(
            $".cw-context-menu__action[data-action-id='{CanDoItAll.Modules.Workbench.CanvasAdapters.ProjectStructureActionCatalogAdapter.EditActionId}']");
        await Assertions.Expect(editAction).ToHaveCountAsync(1);
        await Assertions.Expect(editAction).ToBeVisibleAsync();
        await Assertions.Expect(editAction).ToContainTextAsync("Edit");
        return editAction;
    }
}
