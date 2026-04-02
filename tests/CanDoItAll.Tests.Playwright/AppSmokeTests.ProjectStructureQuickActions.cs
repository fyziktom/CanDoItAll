using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright;

public sealed partial class AppSmokeTests
{
    [Fact]
    public async Task Project_structure_double_click_opens_quick_actions_and_connector_collapse_button_toggles_children()
    {
        await using var context = await fixture.Browser.NewContextAsync(new BrowserNewContextOptions
        {
            IgnoreHTTPSErrors = true,
            ViewportSize = new ViewportSize
            {
                Width = 1800,
                Height = 1200
            }
        });

        var page = await context.NewPageAsync();
        await CreateProjectAsync(page, "Playwright Quick Actions Collapse", "Validation");
        var projectRootId = await ReadNodeIdAsync(page, ".cw-node[data-node-id^='project:']");
        Assert.False(string.IsNullOrWhiteSpace(projectRootId), "Expected the project root node id to resolve.");
        var projectRootSelector = SelectorForNodeId(projectRootId);

        const string childTitle = "Deployment child";
        var childNodeId = await InvokeStructureCreateActionAsync(
            page,
            "add-block-deployment",
            projectRootId!,
            projectRootId,
            childTitle,
            "Canvas child",
            "Validate quick actions and collapse control.");

        await WaitForWorkbenchNodeStateAsync(
            page,
            childNodeId,
            node => string.Equals(node.Title, childTitle, StringComparison.Ordinal),
            "child block appears beneath the project root");

        var rootCenter = await ResolveCanvasNodeCenterAsync(page, projectRootSelector);
        var collapseCenter = await ReadCanvasHotZoneCenterAsync(page, "node-collapse", nodeId: projectRootId);
        Assert.True(
            collapseCenter.X >= rootCenter.X + 24,
            $"Expected the collapse control to stay on the outgoing connector side of the node, but collapse x={collapseCenter.X} and node center x={rootCenter.X}.");
        Assert.InRange(collapseCenter.Y - rootCenter.Y, -36, 36);

        await OpenNodeQuickActionsAsync(page, projectRootSelector);
        var quickActionDialog = page.GetByTestId("project-structure-node-quick-actions");
        await quickActionDialog.WaitForAsync();
        await page.GetByTestId("project-structure-quick-action-edit").WaitForAsync();
        await page.WaitForFunctionAsync(
            @"expectedTitle => {
                const host = document.querySelector('.cw-canvas-host');
                const nodes = host?.__canvasWorkbenchState?.surface?.nodes || [];
                return nodes.some(node => node?.title === expectedTitle);
            }",
            childTitle);
        await quickActionDialog.GetByRole(AriaRole.Button, new() { Name = "Close", Exact = true }).ClickAsync();
        await page.WaitForFunctionAsync("() => !document.querySelector('[data-testid=\"project-structure-node-quick-actions\"]')");
        await page.WaitForTimeoutAsync(120);

        await ToggleCanvasNodeCollapseAsync(page, projectRootSelector);
        await WaitForSceneSnapshotAsync(
            page,
            snapshot => snapshot.Nodes.All(node => !string.Equals(node.Title, childTitle, StringComparison.Ordinal)),
            "child nodes are hidden from the connector button");

        await ToggleCanvasNodeCollapseAsync(page, projectRootSelector);
        await WaitForSceneSnapshotAsync(
            page,
            snapshot => snapshot.Nodes.Any(node => string.Equals(node.Title, childTitle, StringComparison.Ordinal)),
            "child nodes are restored from the connector button");
    }

    [Fact]
    public async Task Project_structure_note_nodes_open_quick_actions_on_double_click()
    {
        await using var context = await fixture.Browser.NewContextAsync(new BrowserNewContextOptions
        {
            IgnoreHTTPSErrors = true,
            ViewportSize = new ViewportSize
            {
                Width = 1800,
                Height = 1200
            }
        });

        var page = await context.NewPageAsync();
        await CreateProjectAsync(page, "Playwright Note Quick Actions", "Validation");

        var noteEditor = await OpenInlineNoteEditorAsync(page);
        await noteEditor.FillAsync("Quick action note");
        await noteEditor.PressAsync("Enter");
        await page.WaitForFunctionAsync("() => !document.querySelector('.cw-note-editor__input')");
        await page.WaitForSelectorAsync("text=Quick action note");

        await OpenNodeQuickActionsAsync(page, ".cw-node:has-text('Quick action note')");
        var quickActionDialog = page.GetByTestId("project-structure-node-quick-actions");
        await quickActionDialog.WaitForAsync();
        await page.GetByTestId("project-structure-quick-action-edit").WaitForAsync();

        await page.WaitForFunctionAsync(
            @"expectedTitle => {
                const host = document.querySelector('.cw-canvas-host');
                const nodes = host?.__canvasWorkbenchState?.surface?.nodes || [];
                return nodes.some(node => node?.title === expectedTitle);
            }",
            "Quick action note");

        await quickActionDialog.GetByRole(AriaRole.Button, new() { Name = "Close", Exact = true }).ClickAsync();
        await page.WaitForFunctionAsync("() => !document.querySelector('[data-testid=\"project-structure-node-quick-actions\"]')");
    }
}
