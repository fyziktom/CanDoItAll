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
    public async Task Project_structure_note_nodes_open_quick_actions_on_double_click_and_expose_side_aware_collapse_button()
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

        const string noteTitle = "Quick action note";
        const string childNodeTitle = "Quick action child";
        var noteEditor = await OpenInlineNoteEditorAsync(page);
        await noteEditor.FillAsync(noteTitle);
        await noteEditor.PressAsync("Enter");
        await page.WaitForFunctionAsync("() => !document.querySelector('.cw-note-editor__input')");
        await page.WaitForSelectorAsync($"text={noteTitle}");
        var noteNodeId = await ResolveCanvasNodeIdAsync(page, $".cw-node:has-text('{noteTitle}')");
        Assert.False(string.IsNullOrWhiteSpace(noteNodeId), "Expected the quick action note node id to resolve.");
        var noteSelector = SelectorForNodeId(noteNodeId!);

        await OpenNodeQuickActionsAsync(page, noteSelector);
        var quickActionDialog = page.GetByTestId("project-structure-node-quick-actions");
        await quickActionDialog.WaitForAsync();
        await page.GetByTestId("project-structure-quick-action-edit").WaitForAsync();

        await page.WaitForFunctionAsync(
            @"expectedTitle => {
                const host = document.querySelector('.cw-canvas-host');
                const nodes = host?.__canvasWorkbenchState?.surface?.nodes || [];
                return nodes.some(node => node?.title === expectedTitle);
            }",
            noteTitle);

        await quickActionDialog.GetByRole(AriaRole.Button, new() { Name = "Close", Exact = true }).ClickAsync();
        await page.WaitForFunctionAsync("() => !document.querySelector('[data-testid=\"project-structure-node-quick-actions\"]')");

        var childNodeId = await InvokeStructureCreateActionAsync(
            page,
            "add-block-feature",
            noteNodeId!,
            noteNodeId,
            childNodeTitle,
            "Note child",
            "Validate the note collapse control.");

        await WaitForWorkbenchNodeStateAsync(
            page,
            childNodeId,
            node => string.Equals(node.Title, childNodeTitle, StringComparison.Ordinal),
            "child block appears beneath the note node");

        var noteCenter = await ResolveCanvasNodeCenterAsync(page, noteSelector);
        var noteCollapseHotZone = await ReadCanvasHotZoneCenterAsync(page, "node-collapse", nodeId: noteNodeId);
        Assert.InRange(noteCollapseHotZone.Y - noteCenter.Y, -40d, 40d);

        var noteSnapshot = await ReadSceneNodeSnapshotAsync(page, noteNodeId!);
        var childSnapshot = await ReadSceneNodeSnapshotAsync(page, childNodeId);
        var desiredChildLeft = noteSnapshot.Left - Math.Max(childSnapshot.Width + 160d, 280d);
        var childDeltaX = (float)(desiredChildLeft - childSnapshot.Left);
        await DragCanvasNodeAsync(page, childNodeId, childDeltaX, -36f);
        await WaitForSceneSnapshotAsync(
            page,
            snapshot =>
            {
                var parent = Array.Find(snapshot.Nodes, node => string.Equals(node.Id, noteNodeId, StringComparison.Ordinal));
                var child = Array.Find(snapshot.Nodes, node => string.Equals(node.Id, childNodeId, StringComparison.Ordinal));
                return parent is not null &&
                    child is not null &&
                    child.Right <= parent.Left - 40d;
            },
            "child note moved to the left of the parent note");

        noteCenter = await ResolveCanvasNodeCenterAsync(page, noteSelector);
        noteCollapseHotZone = await ReadCanvasHotZoneCenterAsync(page, "node-collapse", nodeId: noteNodeId);
        Assert.True(
            noteCollapseHotZone.X <= noteCenter.X - 8d,
            $"Expected the note collapse hot zone to move to the left edge after the child moved left, but hot zone x={noteCollapseHotZone.X} and note center x={noteCenter.X}.");

        await ToggleCanvasNodeCollapseAsync(page, noteSelector);
        await WaitForSceneSnapshotAsync(
            page,
            snapshot => snapshot.Nodes.All(node => !string.Equals(node.Title, childNodeTitle, StringComparison.Ordinal)),
            "child node is hidden from the note connector button");

        await ToggleCanvasNodeCollapseAsync(page, noteSelector);
        await WaitForSceneSnapshotAsync(
            page,
            snapshot => snapshot.Nodes.Any(node => string.Equals(node.Title, childNodeTitle, StringComparison.Ordinal)),
            "child node is restored from the note connector button");
    }
}
