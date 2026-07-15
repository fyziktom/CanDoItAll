using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright;

public sealed partial class AppSmokeTests
{
    [Fact]
    [Trait("Category", "Quarantined")]
    [Trait("Surface", "SharedCanvas")]
    public async Task Shared_canvas_diagnostics_counters_and_browser_gates_are_observable()
    {
        var repoRoot = GetRepoRoot();
        var artifactsDir = Path.Combine(repoRoot, "output", "playwright");
        Directory.CreateDirectory(artifactsDir);

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

        await CreateProjectAsync(page, "Playwright Diagnostics Gate", "Validation");
        await page.WaitForSelectorAsync("text=Structure canvas");
        await SetCanvasDiagnosticsVisibleAsync(page, isVisible: true);

        var structureBefore = await ReadCanvasDiagnosticsAsync(page);
        Assert.True(structureBefore.IsVisible);
        Assert.True(structureBefore.Metrics.RenderCount >= 1, $"Expected structure render count to be observable, got {structureBefore.Metrics.RenderCount}.");
        Assert.True(structureBefore.Metrics.NodeLayerRebuildCount >= 1, $"Expected structure node rebuild count to be observable, got {structureBefore.Metrics.NodeLayerRebuildCount}.");
        await CapturePrimaryWorkbenchShellAsync(page, Path.Combine(artifactsDir, "bundle-p0-07-project-structure-diagnostics.png"));

        var canvasHost = page.Locator(".cw-canvas-host");
        await canvasHost.HoverAsync();
        await page.Mouse.WheelAsync(0, -480);
        await page.WaitForTimeoutAsync(420);

        var structureAfter = await ReadCanvasDiagnosticsAsync(page);
        Assert.True(
            structureAfter.Metrics.RenderCount > structureBefore.Metrics.RenderCount,
            $"Expected wheel zoom to trigger additional renders. Before={structureBefore.Metrics.RenderCount}, after={structureAfter.Metrics.RenderCount}.");
        Assert.True(
            structureAfter.Metrics.ViewportCommitScheduleCount > structureBefore.Metrics.ViewportCommitScheduleCount,
            $"Expected wheel zoom to schedule a viewport commit. Before={structureBefore.Metrics.ViewportCommitScheduleCount}, after={structureAfter.Metrics.ViewportCommitScheduleCount}.");
        Assert.True(
            structureAfter.Metrics.ViewportCommitCount > structureBefore.Metrics.ViewportCommitCount,
            $"Expected wheel zoom to flush an idle viewport commit. Before={structureBefore.Metrics.ViewportCommitCount}, after={structureAfter.Metrics.ViewportCommitCount}.");
        Assert.True(
            structureAfter.Metrics.StatePublishCommitCount > structureBefore.Metrics.StatePublishCommitCount,
            $"Expected wheel zoom to publish updated canvas state. Before={structureBefore.Metrics.StatePublishCommitCount}, after={structureAfter.Metrics.StatePublishCommitCount}.");

        var promptFactoryResponse = await page.GotoAsync($"{fixture.BaseUrl}/prompt-factory");
        Assert.NotNull(promptFactoryResponse);
        Assert.True(promptFactoryResponse!.Ok, $"Expected /prompt-factory to return 2xx, got {(int)promptFactoryResponse.Status}.");
        await page.WaitForSelectorAsync("text=Prompt session workbench");
        await page.Locator(".cw-canvas-host").WaitForAsync();
        await SetCanvasDiagnosticsVisibleAsync(page, isVisible: true);

        var promptFactoryDiagnostics = await ReadCanvasDiagnosticsAsync(page);
        Assert.True(promptFactoryDiagnostics.IsVisible);
        Assert.True(promptFactoryDiagnostics.Metrics.RenderCount >= 1, $"Expected prompt-factory render count to be observable, got {promptFactoryDiagnostics.Metrics.RenderCount}.");
        Assert.True(promptFactoryDiagnostics.Metrics.NodeLayerRebuildCount >= 1, $"Expected prompt-factory node rebuild count to be observable, got {promptFactoryDiagnostics.Metrics.NodeLayerRebuildCount}.");
        Assert.True(promptFactoryDiagnostics.Metrics.StatePublishCommitCount >= 1, $"Expected prompt-factory state commit count to be observable, got {promptFactoryDiagnostics.Metrics.StatePublishCommitCount}.");
        await CapturePrimaryWorkbenchShellAsync(page, Path.Combine(artifactsDir, "bundle-p0-07-prompt-factory-diagnostics.png"));

        Assert.False(await page.Locator("#blazor-error-ui").IsVisibleAsync());
    }

    [Fact]
    [Trait("Surface", "SharedCanvas")]
    public async Task Shared_canvas_retained_renderer_keeps_node_and_link_layers_stable_during_drag_and_pan()
    {
        var repoRoot = GetRepoRoot();
        var artifactsDir = Path.Combine(repoRoot, "output", "playwright");
        Directory.CreateDirectory(artifactsDir);

        await using var context = await fixture.Browser.NewContextAsync(new BrowserNewContextOptions
        {
            IgnoreHTTPSErrors = true,
            ViewportSize = new ViewportSize
            {
                Width = 1600,
                Height = 1100
            }
        });
        var page = await context.NewPageAsync();

        var projectId = await CreateProjectAsync(page, "Playwright Retained Renderer", "Execution");
        var projectRootSelector = $".cw-node[data-node-id='project:{projectId}']";
        var projectRootId = await ReadNodeIdAsync(page, projectRootSelector);

        var noteId = await InvokeStructureCreateActionAsync(
            page,
            "add-note",
            projectRootId,
            projectRootId,
            "Retained renderer note",
            string.Empty,
            "Retained renderer note");
        await CommitCanvasNodePositionsAsync(
            page,
            [
                (noteId, 860, 340)
            ]);
        var noteSelector = SelectorForNodeId(noteId);
        await EnsureCanvasSelectionAsync(page, noteSelector);
        await SetCanvasDiagnosticsVisibleAsync(page, isVisible: true);

        var warmupDiagnostics = await ReadCanvasDiagnosticsAsync(page);
        Assert.True(warmupDiagnostics.TotalLinkCount >= 1, $"Expected the retained renderer smoke to include at least one projected link, got {warmupDiagnostics.TotalLinkCount}.");

        var beforeDragMetrics = await ReadCanvasDiagnosticsAsync(page);
        var beforeDragPosition = (await ReadNodePositionsAsync(page, [noteId])).Single();

        await DragCanvasNodeAsync(page, noteId, 180, 90);

        var afterDragMetrics = await ReadCanvasDiagnosticsAsync(page);
        var afterDragPosition = (await ReadNodePositionsAsync(page, [noteId])).Single();
        Assert.True(
            Math.Abs(afterDragPosition.Left - beforeDragPosition.Left) > 40 ||
            Math.Abs(afterDragPosition.Top - beforeDragPosition.Top) > 40,
            $"Expected retained drag proof node to move, but before=({beforeDragPosition.Left},{beforeDragPosition.Top}) after=({afterDragPosition.Left},{afterDragPosition.Top}).");
        Assert.True(
            afterDragMetrics.Metrics.RenderCount > beforeDragMetrics.Metrics.RenderCount,
            $"Expected drag to trigger additional renders. Before={beforeDragMetrics.Metrics.RenderCount}, after={afterDragMetrics.Metrics.RenderCount}.");
        Assert.Equal(
            beforeDragMetrics.Metrics.NodeLayerRebuildCount,
            afterDragMetrics.Metrics.NodeLayerRebuildCount);
        Assert.Equal(
            beforeDragMetrics.Metrics.LinkLayerRebuildCount,
            afterDragMetrics.Metrics.LinkLayerRebuildCount);
        Assert.Equal(
            beforeDragMetrics.Metrics.FrameLayerRebuildCount,
            afterDragMetrics.Metrics.FrameLayerRebuildCount);
        Assert.True(
            afterDragMetrics.Metrics.StatePublishCommitCount > beforeDragMetrics.Metrics.StatePublishCommitCount,
            $"Expected drag to publish updated canvas state. Before={beforeDragMetrics.Metrics.StatePublishCommitCount}, after={afterDragMetrics.Metrics.StatePublishCommitCount}.");
        await CapturePrimaryWorkbenchShellAsync(page, Path.Combine(artifactsDir, "bundle-p1-01-retained-drag.png"));

        var beforePanMetrics = await ReadCanvasDiagnosticsAsync(page);

        await PanCanvasAsync(page, 180, 120);

        var afterPanMetrics = await ReadCanvasDiagnosticsAsync(page);
        Assert.True(
            afterPanMetrics.Metrics.RenderCount > beforePanMetrics.Metrics.RenderCount,
            $"Expected pan to trigger additional renders. Before={beforePanMetrics.Metrics.RenderCount}, after={afterPanMetrics.Metrics.RenderCount}.");
        Assert.Equal(
            beforePanMetrics.Metrics.NodeLayerRebuildCount,
            afterPanMetrics.Metrics.NodeLayerRebuildCount);
        Assert.Equal(
            beforePanMetrics.Metrics.LinkLayerRebuildCount,
            afterPanMetrics.Metrics.LinkLayerRebuildCount);
        Assert.Equal(
            beforePanMetrics.Metrics.FrameLayerRebuildCount,
            afterPanMetrics.Metrics.FrameLayerRebuildCount);
        Assert.True(
            Math.Abs(afterPanMetrics.PanX - beforePanMetrics.PanX) > 10 ||
            Math.Abs(afterPanMetrics.PanY - beforePanMetrics.PanY) > 10,
            $"Expected pan proof to change viewport offsets. Before=({beforePanMetrics.PanX},{beforePanMetrics.PanY}), after=({afterPanMetrics.PanX},{afterPanMetrics.PanY}).");
        Assert.True(
            afterPanMetrics.Metrics.StatePublishCommitCount > beforePanMetrics.Metrics.StatePublishCommitCount,
            $"Expected pan to publish updated canvas state. Before={beforePanMetrics.Metrics.StatePublishCommitCount}, after={afterPanMetrics.Metrics.StatePublishCommitCount}.");
        await CapturePrimaryWorkbenchShellAsync(page, Path.Combine(artifactsDir, "bundle-p1-01-retained-pan.png"));

        Assert.False(await page.Locator("#blazor-error-ui").IsVisibleAsync());
    }

    [Fact]
    [Trait("Surface", "SharedCanvas")]
    public async Task Shared_canvas_recreate_disposes_previous_runtime_before_keyboard_note_create()
    {
        await using var context = await fixture.Browser.NewContextAsync(new BrowserNewContextOptions
        {
            IgnoreHTTPSErrors = true,
            ViewportSize = new ViewportSize
            {
                Width = 1600,
                Height = 1100
            }
        });
        var page = await context.NewPageAsync();

        await CreateProjectAsync(page, "Playwright Recreate Note Editor", "Validation");
        await page.Locator(".cw-canvas-host").WaitForAsync();

        var recreated = await page.EvaluateAsync<bool>(
            @"() => {
                const host = document.querySelector('.cw-canvas-host');
                const state = host?.__canvasWorkbenchState;
                const workbench = window.CanDoItAll?.canvasWorkbench;
                if (!host || !state || !workbench?.create || !state.dotNetRef || !state.surface) {
                    return false;
                }

                const selectedNodeIds = Array.isArray(state.ui?.selectedNodeIds)
                    ? [...state.ui.selectedNodeIds]
                    : [];
                workbench.create(
                    host,
                    state.dotNetRef,
                    state.surface,
                    state.selectionDispatchId || 0,
                    state.stateDispatchId || 0,
                    {
                        fitView: false,
                        selectedNodeIds,
                        primaryNodeId: selectedNodeIds[0] || null
                    });
                return true;
            }");
        Assert.True(recreated, "Expected the browser workbench runtime to be available for the recreate regression.");

        var canvasHost = page.Locator(".cw-canvas-host");
        await canvasHost.FocusAsync();
        await canvasHost.PressAsync("Tab");
        await page.Locator(".cw-note-editor__input").WaitForAsync();

        Assert.Equal(1, await page.Locator(".cw-note-editor__input").CountAsync());
        Assert.Equal(1, await page.Locator(".cw-canvas-composer.is-note").CountAsync());
        Assert.False(await page.Locator("#blazor-error-ui").IsVisibleAsync());
    }

    [Fact]
    [Trait("Surface", "SharedCanvas")]
    public async Task Shared_canvas_viewport_culling_reduces_rendered_nodes_without_losing_offscreen_selection()
    {
        var repoRoot = GetRepoRoot();
        var artifactsDir = Path.Combine(repoRoot, "output", "playwright");
        Directory.CreateDirectory(artifactsDir);

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

        var projectId = await CreateProjectAsync(page, "Playwright Viewport Culling", "Execution");
        var projectRootId = await ReadNodeIdAsync(page, ".cw-node[data-node-id^='project:']");
        var nodeIds = new List<string>();
        for (var index = 0; index < 18; index++)
        {
            var nodeId = await InvokeStructureCreateActionAsync(
                page,
                "add-note",
                projectRootId,
                projectRootId,
                $"Viewport note {index + 1}",
                $"Lane {(index % 6) + 1}",
                "Large graph viewport culling proof.");
            nodeIds.Add(nodeId);
        }

        await CommitCanvasNodePositionsAsync(
            page,
            nodeIds.Select((nodeId, index) => (
                NodeId: nodeId,
                X: 720 + ((index % 6) * 980),
                Y: 420 + ((index / 6) * 680))).ToArray());
        await CommitCanvasUiStateAsync(page, zoom: 0.55, panX: 140, panY: 110, selectedNodeIds: []);
        await SetCanvasMinimapVisibleAsync(page, isVisible: true);
        await SetCanvasDiagnosticsVisibleAsync(page, isVisible: true);
        await page.WaitForFunctionAsync(
            @"expectedCount => {
                const host = document.querySelector('.cw-canvas-host');
                const state = host?.__canvasWorkbenchState;
                return (state?.surface?.nodes?.length || 0) >= expectedCount;
            }",
            nodeIds.Count + 1,
            new() { Timeout = 60_000 });

        var cullingDiagnostics = await ReadCanvasDiagnosticsAsync(page);
        Assert.True(
            cullingDiagnostics.VisibleNodeCount <= cullingDiagnostics.TotalNodeCount - 6,
            $"Expected viewport culling to reduce rendered nodes materially. Visible={cullingDiagnostics.VisibleNodeCount}, total={cullingDiagnostics.TotalNodeCount}.");
        var cullingScene = await ReadSceneSnapshotAsync(page);
        var minimapNodeCount = cullingScene.Minimap?.NodeCount ?? 0;
        Assert.True(
            minimapNodeCount >= cullingDiagnostics.TotalNodeCount - 1,
            $"Expected minimap to retain the broader scene while the viewport is culled. Minimap={minimapNodeCount}, total={cullingDiagnostics.TotalNodeCount}.");

        var offscreenNodeId = await FindOffscreenNodeIdAsync(page, nodeIds);
        var offscreenBefore = (await ReadNodePositionsAsync(page, [offscreenNodeId])).Single();
        Assert.True(
            offscreenBefore.Left == -9999 ||
            offscreenBefore.Left < 0 ||
            offscreenBefore.Top < 0 ||
            offscreenBefore.Left > 1900 ||
            offscreenBefore.Top > 1200,
            $"Expected the off-screen proof node to start outside the viewport. Left={offscreenBefore.Left}, top={offscreenBefore.Top}.");
        await CapturePrimaryWorkbenchShellAsync(page, Path.Combine(artifactsDir, "bundle-p1-02-large-graph-culling.png"));

        var beforeSelection = await ReadCanvasDiagnosticsAsync(page);
        var focused = await page.EvaluateAsync<bool>(
            @"nodeId => {
                const workbench = window.CanDoItAll?.canvasWorkbench;
                const host = document.querySelector('.cw-canvas-host');
                if (!host || !workbench?.focusNode) {
                    return false;
                }

                workbench.focusNode(host, nodeId);
                return true;
            }",
            offscreenNodeId);
        Assert.True(focused, $"Expected the shared canvas host to expose focusNode for '{offscreenNodeId}'.");
        await WaitForSceneSnapshotAsync(
            page,
            snapshot =>
            {
                var node = snapshot.Nodes.FirstOrDefault(candidate => string.Equals(candidate.Id, offscreenNodeId, StringComparison.Ordinal));
                return node is not null &&
                    node.Width > 0 &&
                    node.Height > 0 &&
                    node.Left >= 32 &&
                    node.Top >= 32 &&
                    node.Right <= snapshot.ViewportWidth - 32 &&
                    node.Bottom <= snapshot.ViewportHeight - 32;
            },
            "offscreen selection restored into viewport",
            60_000);

        var afterSelection = await ReadCanvasDiagnosticsAsync(page);
        var offscreenAfter = (await ReadNodePositionsAsync(page, [offscreenNodeId])).Single();
        Assert.Equal(1, afterSelection.SelectedCount);
        Assert.True(
            Math.Abs(afterSelection.PanX - beforeSelection.PanX) > 150 ||
            Math.Abs(afterSelection.PanY - beforeSelection.PanY) > 150,
            $"Expected off-screen selection to recenter the viewport. Before=({beforeSelection.PanX},{beforeSelection.PanY}), after=({afterSelection.PanX},{afterSelection.PanY}).");
        Assert.True(
            offscreenAfter.Left >= 32 &&
            offscreenAfter.Top >= 32 &&
            offscreenAfter.Left <= 1700 &&
            offscreenAfter.Top <= 1000,
            $"Expected the selected off-screen node to be restored into view. Left={offscreenAfter.Left}, top={offscreenAfter.Top}.");
        Assert.True(
            afterSelection.VisibleNodeCount < afterSelection.TotalNodeCount,
            $"Expected culling to remain active after selection recentering. Visible={afterSelection.VisibleNodeCount}, total={afterSelection.TotalNodeCount}.");
        await CapturePrimaryWorkbenchShellAsync(page, Path.Combine(artifactsDir, "bundle-p1-02-offscreen-selection.png"));

        Assert.False(await page.Locator("#blazor-error-ui").IsVisibleAsync());
    }

    [Fact]
    [Trait("Surface", "SharedCanvas")]
    public async Task Project_structure_add_and_remove_keep_viewport_stable()
    {
        await using var context = await fixture.Browser.NewContextAsync(new BrowserNewContextOptions
        {
            IgnoreHTTPSErrors = true,
            ViewportSize = new ViewportSize
            {
                Width = 1600,
                Height = 1100
            }
        });
        var page = await context.NewPageAsync();

        var projectId = await CreateProjectAsync(page, "Playwright Stable Mutation Viewport", "Execution");
        var projectRootId = await ReadNodeIdAsync(page, $".cw-node[data-node-id='project:{projectId}']");
        var existingNoteId = await InvokeStructureCreateActionAsync(
            page,
            "add-note",
            projectRootId,
            projectRootId,
            "Existing viewport anchor",
            string.Empty,
            "Keep the viewport away from the root while the graph changes.");

        await CommitCanvasUiStateAsync(
            page,
            zoom: 0.68,
            panX: -360,
            panY: 240,
            selectedNodeIds: [existingNoteId]);
        var baseline = await ReadCanvasDiagnosticsAsync(page);

        await StartCanvasViewportSamplingAsync(page);
        var createdNoteId = await InvokeStructureCreateActionAsync(
            page,
            "add-note",
            existingNoteId,
            existingNoteId,
            "Stable viewport mutation",
            string.Empty,
            "Adding this node must not move the current canvas viewport.");
        await page.WaitForTimeoutAsync(500);
        var createSamples = await StopCanvasViewportSamplingAsync(page);

        AssertViewportStayedStable("create", baseline, createSamples);

        await StartCanvasViewportSamplingAsync(page);
        var deleteInvoked = await page.EvaluateAsync<bool>(
            @"async nodeId => {
                const host = document.querySelector('.cw-canvas-host');
                const state = host?.__canvasWorkbenchState;
                if (!state?.dotNetRef?.invokeMethodAsync) {
                    return false;
                }

                await state.dotNetRef.invokeMethodAsync('OnContextAction', nodeId, 'delete', 0, 0);
                return true;
            }",
            createdNoteId);
        Assert.True(deleteInvoked, "Expected the browser workbench host to expose the delete callback.");
        await page.WaitForFunctionAsync(
            @"nodeId => {
                const state = document.querySelector('.cw-canvas-host')?.__canvasWorkbenchState;
                return Array.isArray(state?.surface?.nodes)
                    && !state.surface.nodes.some(node => node.id === nodeId);
            }",
            createdNoteId);
        await page.WaitForTimeoutAsync(500);
        var deleteSamples = await StopCanvasViewportSamplingAsync(page);

        AssertViewportStayedStable("delete", baseline, deleteSamples);
        Assert.False(await page.Locator("#blazor-error-ui").IsVisibleAsync());
    }

    [Fact]
    [Trait("Surface", "SharedCanvas")]
    public async Task Project_structure_multi_selection_keeps_individual_node_selection_without_an_aggregate_transform_frame()
    {
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

        var projectId = await CreateProjectAsync(page, "Playwright Individual Multi-selection", "Validation");
        var projectRootId = await ReadNodeIdAsync(page, $".cw-node[data-node-id='project:{projectId}']");
        var firstNodeId = await InvokeStructureCreateActionAsync(
            page,
            "add-block-feature",
            projectRootId,
            projectRootId,
            "First selected node",
            "Selection proof",
            "Keep an individual selection outline around this node.");
        var secondNodeId = await InvokeStructureCreateActionAsync(
            page,
            "add-block-support",
            projectRootId,
            projectRootId,
            "Second selected node",
            "Selection proof",
            "Keep an individual selection outline around this node.");

        await CommitCanvasNodePositionsAsync(
            page,
            [
                (firstNodeId, 820, 430),
                (secondNodeId, 1220, 760)
            ]);
        await SelectCanvasNodesAsync(page, [firstNodeId, secondNodeId], firstNodeId);

        var selectedNodeIds = (await ReadSelectedNodePositionsAsync(page))
            .Select(position => position.Id)
            .Order(StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(
            new[] { firstNodeId, secondNodeId }.Order(StringComparer.Ordinal),
            selectedNodeIds);
        Assert.Equal(0, await page.Locator(".cw-transform-frame").CountAsync());

        var beforeDragPositions = await ReadNodePositionsAsync(page, [firstNodeId, secondNodeId]);
        await page.Keyboard.DownAsync("Control");
        try
        {
            await DragCanvasNodeAsync(page, firstNodeId, 170, 120, controlModifier: true);
        }
        finally
        {
            await page.Keyboard.UpAsync("Control");
        }

        var afterDragPositions = await ReadNodePositionsAsync(page, [firstNodeId, secondNodeId]);
        var firstBefore = beforeDragPositions.Single(position => position.Id == firstNodeId);
        var firstAfter = afterDragPositions.Single(position => position.Id == firstNodeId);
        var secondBefore = beforeDragPositions.Single(position => position.Id == secondNodeId);
        var secondAfter = afterDragPositions.Single(position => position.Id == secondNodeId);
        Assert.Equal(firstAfter.Left - firstBefore.Left, secondAfter.Left - secondBefore.Left);
        Assert.Equal(firstAfter.Top - firstBefore.Top, secondAfter.Top - secondBefore.Top);
        Assert.True(
            Math.Abs(firstAfter.Left - firstBefore.Left) > 40 || Math.Abs(firstAfter.Top - firstBefore.Top) > 40,
            "Expected the Ctrl group drag to move both selected nodes.");

        selectedNodeIds = (await ReadSelectedNodePositionsAsync(page))
            .Select(position => position.Id)
            .Order(StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(
            new[] { firstNodeId, secondNodeId }.Order(StringComparer.Ordinal),
            selectedNodeIds);
        Assert.Equal(0, await page.Locator(".cw-transform-frame").CountAsync());
        Assert.False(await page.Locator("#blazor-error-ui").IsVisibleAsync());
    }

    [Fact]
    [Trait("Surface", "SharedCanvas")]
    public async Task Shared_canvas_dirty_drag_loop_limits_patch_scope_and_preserves_guides_and_group_frame_updates()
    {
        var repoRoot = GetRepoRoot();
        var artifactsDir = Path.Combine(repoRoot, "output", "playwright");
        Directory.CreateDirectory(artifactsDir);

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

        var projectId = await CreateProjectAsync(page, "Playwright Dirty Drag Loop", "Execution");
        var projectRootId = await ReadNodeIdAsync(page, ".cw-node[data-node-id^='project:']");
        var leftGuideId = await InvokeStructureCreateActionAsync(
            page,
            "add-block-feature",
            projectRootId,
            projectRootId,
            "Guide anchor left",
            "Snap target",
            "Hold the left snap target steady during the drag proof.");
        var rightGuideId = await InvokeStructureCreateActionAsync(
            page,
            "add-block-support",
            projectRootId,
            projectRootId,
            "Guide anchor right",
            "Snap target",
            "Hold the right snap target steady during the drag proof.");
        var movedTaskId = await InvokeStructureCreateActionAsync(
            page,
            "add-work-task",
            projectRootId,
            projectRootId,
            "Dirty drag task",
            "Move set",
            "Move this node through the dirty drag loop.",
            [
                new CanvasInputValueSeed("dueUtc", "2026-04-20T12:00:00+00:00")
            ]);
        var movedEvidenceId = await InvokeStructureCreateActionAsync(
            page,
            "add-test-evidence",
            projectRootId,
            projectRootId,
            "Dirty drag evidence",
            "Move set",
            "Move this node with the task to keep the border in sync.");

        await CommitCanvasNodePositionsAsync(
            page,
            [
                (leftGuideId, 820, 420),
                (rightGuideId, 1160, 420),
                (movedTaskId, 820, 820),
                (movedEvidenceId, 1160, 890)
            ]);
        await FocusCanvasRootAsync(page);
        await SetCanvasZoomPercentAsync(page, 74);
        await page.WaitForTimeoutAsync(220);

        await SelectCanvasNodesAsync(page, [movedTaskId, movedEvidenceId], movedTaskId);
        await page.GetByTestId("project-structure-selection-window").WaitForAsync();
        await page.Locator(".cw-floating-window[data-testid='project-structure-selection-window'] input[placeholder='Name this border']").FillAsync("Guide batch");
        await page.GetByRole(AriaRole.Button, new() { Name = "Border", Exact = true }).ClickAsync();
        await WaitForSceneFrameLabelAsync(page, "Guide batch");

        await SelectCanvasNodesAsync(page, [movedTaskId, movedEvidenceId], movedTaskId);
        await page.WaitForSelectorAsync("text=2 nodes selected");
        await SetCanvasDiagnosticsVisibleAsync(page, isVisible: true);

        var beforeDragMetrics = await ReadCanvasDiagnosticsAsync(page);
        var beforeDragPositions = await ReadNodePositionsAsync(page, [movedTaskId, movedEvidenceId]);
        var beforeFrame = await ReadSceneFrameSnapshotByLabelAsync(page, "Guide batch");
        Assert.True(
            beforeDragMetrics.VisibleNodeCount >= 5,
            $"Expected the dirty drag proof to render a broader scene before dragging. Visible={beforeDragMetrics.VisibleNodeCount}.");

        await page.Keyboard.DownAsync("Control");
        await DragCanvasNodeAsync(page, movedTaskId, -31, -280, releasePointer: false, controlModifier: true);
        var snapGuideProbe = await WaitForCanvasSnapGuidesAsync(page, [leftGuideId, rightGuideId, movedTaskId, movedEvidenceId]);

        var duringDragMetrics = await ReadCanvasDiagnosticsAsync(page);
        var duringDragPositions = await ReadNodePositionsAsync(page, [movedTaskId, movedEvidenceId]);
        var guideCount = await page.Locator(".cw-snap-guide").CountAsync();
        var dragRenderDelta = duringDragMetrics.Metrics.RenderCount - beforeDragMetrics.Metrics.RenderCount;
        var dragPatchedNodeDelta = duringDragMetrics.Metrics.TotalDragPatchedNodeCount - beforeDragMetrics.Metrics.TotalDragPatchedNodeCount;
        var dragPatchedLinkDelta = duringDragMetrics.Metrics.TotalDragPatchedLinkCount - beforeDragMetrics.Metrics.TotalDragPatchedLinkCount;
        var dragPatchedFrameDelta = duringDragMetrics.Metrics.TotalDragPatchedFrameCount - beforeDragMetrics.Metrics.TotalDragPatchedFrameCount;
        Assert.Equal("drag", duringDragMetrics.Interaction);
        Assert.True(
            guideCount >= 1,
            $"Expected snap guides to stay visible during the held multi-select drag. {snapGuideProbe.ToDiagnosticString()}");
        Assert.True(
            snapGuideProbe.StateGuideCount >= 1,
            $"Expected the held multi-select drag to keep snap targets in state. {snapGuideProbe.ToDiagnosticString()}");
        Assert.True(
            dragRenderDelta > 0,
            $"Expected the held drag to trigger dirty drag renders. Before={beforeDragMetrics.Metrics.RenderCount}, during={duringDragMetrics.Metrics.RenderCount}.");
        Assert.True(
            dragPatchedNodeDelta > 0,
            "Expected the held drag to patch moved node chrome.");
        Assert.True(
            dragPatchedLinkDelta > 0,
            "Expected the held drag to patch retained links attached to the moved nodes.");
        Assert.True(
            dragPatchedFrameDelta > 0,
            "Expected the held drag to patch the group frame that owns the moved nodes.");
        Assert.True(
            dragPatchedNodeDelta <= (dragRenderDelta * 2) + 2,
            $"Expected dirty drag node patches to stay scoped to the two moved nodes. Render delta={dragRenderDelta}, node patch delta={dragPatchedNodeDelta}.");
        Assert.True(
            dragPatchedLinkDelta <= (dragRenderDelta * 3) + 3,
            $"Expected dirty drag link patches to stay near the affected links. Render delta={dragRenderDelta}, link patch delta={dragPatchedLinkDelta}.");
        Assert.True(
            dragPatchedFrameDelta <= dragRenderDelta + 1,
            $"Expected dirty drag frame patches to stay scoped to the active border. Render delta={dragRenderDelta}, frame patch delta={dragPatchedFrameDelta}.");
        Assert.All(
            duringDragPositions,
            position =>
            {
                var beforePosition = beforeDragPositions.Single(candidate => candidate.Id == position.Id);
                Assert.True(
                    Math.Abs(position.Top - beforePosition.Top) > 80 || Math.Abs(position.Left - beforePosition.Left) > 10,
                    $"Expected drag proof node '{position.Id}' to move while the pointer is held. Before=({beforePosition.Left},{beforePosition.Top}), during=({position.Left},{position.Top}).");
            });
        await CapturePrimaryWorkbenchShellAsync(page, Path.Combine(artifactsDir, "bundle-p1-03-guide-drag.png"));

        await ReleaseCanvasInteractionAsync(page);
        await page.Keyboard.UpAsync("Control");
        await WaitForSceneSnapshotAsync(
            page,
            snapshot =>
            {
                var frame = snapshot.Frames.FirstOrDefault(candidate => string.Equals(candidate.Label, "Guide batch", StringComparison.Ordinal));
                return frame is not null &&
                    (Math.Abs(frame.LabelLeft - beforeFrame.LabelLeft) > 40 ||
                    Math.Abs(frame.LabelTop - beforeFrame.LabelTop) > 80);
            },
            "group frame to settle after multi-select drag");

        var afterDragMetrics = await ReadCanvasDiagnosticsAsync(page);
        var afterFrame = await ReadSceneFrameSnapshotByLabelAsync(page, "Guide batch");
        Assert.True(
            afterDragMetrics.Metrics.StatePublishCommitCount > beforeDragMetrics.Metrics.StatePublishCommitCount,
            $"Expected the dirty drag release to publish updated state. Before={beforeDragMetrics.Metrics.StatePublishCommitCount}, after={afterDragMetrics.Metrics.StatePublishCommitCount}.");
        Assert.True(
            Math.Abs(afterFrame.LabelLeft - beforeFrame.LabelLeft) > 40 ||
            Math.Abs(afterFrame.LabelTop - beforeFrame.LabelTop) > 80,
            $"Expected the retained frame label to move with the multi-select drag. Before=({beforeFrame.LabelLeft},{beforeFrame.LabelTop}), after=({afterFrame.LabelLeft},{afterFrame.LabelTop}).");
        await page.WaitForSelectorAsync("text=2 nodes selected");

        Assert.False(await page.Locator("#blazor-error-ui").IsVisibleAsync());
    }

    private static async Task StartCanvasViewportSamplingAsync(IPage page)
    {
        var started = await page.EvaluateAsync<bool>(
            @"() => {
                const host = document.querySelector('.cw-canvas-host');
                if (!host?.__canvasWorkbenchState) {
                    return false;
                }

                const sampler = {
                    active: true,
                    samples: []
                };
                window.__canvasViewportSampler = sampler;
                const sample = () => {
                    const state = host.__canvasWorkbenchState;
                    if (state?.ui) {
                        sampler.samples.push({
                            zoomPercent: Math.round((state.ui.zoom || 1) * 100),
                            panX: Number(state.ui.panX || 0),
                            panY: Number(state.ui.panY || 0)
                        });
                    }

                    if (sampler.active) {
                        requestAnimationFrame(sample);
                    }
                };

                sample();
                return true;
            }");
        Assert.True(started, "Expected the initialized canvas host to support viewport sampling.");
    }

    private static async Task<IReadOnlyList<CanvasViewportSample>> StopCanvasViewportSamplingAsync(IPage page)
    {
        var samples = await page.EvaluateAsync<CanvasViewportSample[]>(
            @"() => {
                const sampler = window.__canvasViewportSampler;
                if (!sampler) {
                    return [];
                }

                sampler.active = false;
                delete window.__canvasViewportSampler;
                return sampler.samples;
            }");

        return samples;
    }

    private static void AssertViewportStayedStable(
        string operation,
        CanvasDiagnosticsSnapshot baseline,
        IReadOnlyList<CanvasViewportSample> samples)
    {
        Assert.True(samples.Count >= 2, $"Expected viewport samples while the {operation} mutation was applied.");

        var maxZoomDelta = samples.Max(sample => Math.Abs(sample.ZoomPercent - baseline.ZoomPercent));
        var maxPanXDelta = samples.Max(sample => Math.Abs(sample.PanX - baseline.PanX));
        var maxPanYDelta = samples.Max(sample => Math.Abs(sample.PanY - baseline.PanY));
        Assert.True(
            maxZoomDelta == 0 && maxPanXDelta <= 0.5 && maxPanYDelta <= 0.5,
            $"Expected {operation} to preserve the viewport. " +
            $"Baseline={baseline.ZoomPercent}%/{baseline.PanX:0.##},{baseline.PanY:0.##}; " +
            $"max deltas={maxZoomDelta}%/{maxPanXDelta:0.##},{maxPanYDelta:0.##}.");
    }

    private sealed class CanvasViewportSample
    {
        public int ZoomPercent { get; set; }

        public double PanX { get; set; }

        public double PanY { get; set; }
    }
}
