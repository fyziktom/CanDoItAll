using System.Text.Json;
using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright;

public sealed partial class AppSmokeTests
{
    [Fact]
    public async Task Project_structure_canvas_feedback_palette_and_catalog_are_validated_in_browser()
    {
        var repoRoot = GetRepoRoot();
        var artifactsDir = Path.Combine(repoRoot, "output", "playwright", "feedback-bundle-visuals");
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
        await CreateProjectAsync(page, "Playwright Canvas Feedback Visuals", "Validation");
        var projectRootId = await ReadNodeIdAsync(page, ".cw-node[data-node-id^='project:']");

        await EnsureStructureToolboxWindowExpandedAsync(page);
        await AssertToolboxSearchResultAsync(page, "computer", "Computer block");
        await AssertToolboxSearchResultAsync(page, "router", "Router block");
        await AssertToolboxSearchResultAsync(page, "wifi", "WiFi block");
        await CaptureLocatorAsync(
            page.GetByTestId("project-structure-toolbox-window"),
            Path.Combine(artifactsDir, "01-toolbox-common-network-blocks.png"));

        var pdfId = await InvokeStructureCreateActionAsync(
            page,
            "add-file-pdf",
            projectRootId,
            projectRootId,
            "Architecture proof PDF",
            "docs/architecture",
            "Validate the PDF palette mapping.",
            uploadedFile: BuildUploadedFile(
                "architecture-proof.pdf",
                "application/pdf",
                "%PDF-1.4\n1 0 obj\n<< /Type /Catalog >>\nendobj\ntrailer\n<< /Root 1 0 R >>\n%%EOF"));
        var excelId = await InvokeStructureCreateActionAsync(
            page,
            "add-file-excel",
            projectRootId,
            projectRootId,
            "Validation workbook",
            "reports",
            "Validate the Excel palette mapping.",
            uploadedFile: BuildUploadedFile(
                "validation-workbook.csv",
                "text/csv",
                "name,status\nexports,ready"));
        var deploymentId = await InvokeStructureCreateActionAsync(
            page,
            "add-block-deployment",
            projectRootId,
            projectRootId,
            "Blue rollout lane",
            "Docker topology",
            "Deployment blocks should stay blue-toned.");
        var computerId = await InvokeStructureCreateActionAsync(
            page,
            "add-block-computer",
            projectRootId,
            projectRootId,
            "Operator workstation",
            "Endpoint",
            "Computer blocks should keep a neutral machine tone.");
        var routerId = await InvokeStructureCreateActionAsync(
            page,
            "add-block-router",
            projectRootId,
            projectRootId,
            "Edge router",
            "Gateway",
            "Router blocks should use the shared network palette.");
        var wifiId = await InvokeStructureCreateActionAsync(
            page,
            "add-block-wifi",
            projectRootId,
            projectRootId,
            "Warehouse WiFi",
            "Coverage",
            "WiFi blocks should be available in the common catalog.");

        var pdfState = await ReadWorkbenchNodeStateAsync(page, pdfId);
        var excelState = await ReadWorkbenchNodeStateAsync(page, excelId);
        var deploymentState = await ReadWorkbenchNodeStateAsync(page, deploymentId);
        var computerState = await ReadWorkbenchNodeStateAsync(page, computerId);
        var routerState = await ReadWorkbenchNodeStateAsync(page, routerId);
        var wifiState = await ReadWorkbenchNodeStateAsync(page, wifiId);

        Assert.Equal("danger", pdfState.PaletteKey);
        Assert.Equal("success", excelState.PaletteKey);
        Assert.Equal("info", deploymentState.PaletteKey);
        Assert.Equal("neutral", computerState.PaletteKey);
        Assert.Equal("info", routerState.PaletteKey);
        Assert.Equal("info", wifiState.PaletteKey);

        var pdfAccent = await ReadNodeAccentColorAsync(page, pdfId);
        var excelAccent = await ReadNodeAccentColorAsync(page, excelId);
        var deploymentAccent = await ReadNodeAccentColorAsync(page, deploymentId);
        var computerAccent = await ReadNodeAccentColorAsync(page, computerId);
        var routerAccent = await ReadNodeAccentColorAsync(page, routerId);
        var wifiAccent = await ReadNodeAccentColorAsync(page, wifiId);

        Assert.NotEqual(pdfAccent, excelAccent);
        Assert.NotEqual(pdfAccent, deploymentAccent);
        Assert.NotEqual(pdfAccent, computerAccent);
        Assert.NotEqual(excelAccent, computerAccent);
        Assert.NotEqual(excelAccent, deploymentAccent);
        Assert.Equal(deploymentAccent, routerAccent);
        Assert.NotEqual(routerAccent, computerAccent);
        Assert.NotEqual(wifiAccent, computerAccent);

        await FocusCanvasNodeAsync(page, pdfId, 90);
        await CaptureCanvasSurfaceAsync(page, Path.Combine(artifactsDir, "02-pdf-palette-surface.png"));

        await FocusCanvasNodeAsync(page, excelId, 90);
        await CaptureCanvasSurfaceAsync(page, Path.Combine(artifactsDir, "03-excel-palette-surface.png"));

        await FocusCanvasNodeAsync(page, deploymentId, 90);
        await CaptureCanvasSurfaceAsync(page, Path.Combine(artifactsDir, "04-deployment-palette-surface.png"));

        await FocusCanvasNodeAsync(page, computerId, 90);
        await CaptureCanvasSurfaceAsync(page, Path.Combine(artifactsDir, "05-computer-palette-surface.png"));

        await FocusCanvasNodeAsync(page, routerId, 90);
        await CaptureCanvasSurfaceAsync(page, Path.Combine(artifactsDir, "06-router-palette-surface.png"));

        await FocusCanvasNodeAsync(page, wifiId, 90);
        await CaptureCanvasSurfaceAsync(page, Path.Combine(artifactsDir, "07-wifi-palette-surface.png"));
        Assert.False(await page.Locator("#blazor-error-ui").IsVisibleAsync());
    }

    [Fact]
    public async Task Project_structure_canvas_feedback_note_copy_and_mutation_flows_are_validated_in_browser()
    {
        var repoRoot = GetRepoRoot();
        var artifactsDir = Path.Combine(repoRoot, "output", "playwright", "feedback-bundle-mutations");
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
        await CreateProjectAsync(page, "Playwright Canvas Feedback Mutations", "Validation");
        await InstallCanvasClipboardStubAsync(page);
        var projectRootId = await ReadNodeIdAsync(page, ".cw-node[data-node-id^='project:']");

        var copyRootId = await InvokeStructureCreateActionAsync(
            page,
            "add-block-router",
            projectRootId,
            projectRootId,
            "Network hub",
            "Gateway",
            "Parent block for clipboard proofs.");
        var copyChildId = await InvokeStructureCreateActionAsync(
            page,
            "add-block-wifi",
            copyRootId,
            copyRootId,
            "Office WiFi",
            "Coverage",
            "Wireless child block.");
        var copyGrandchildId = await InvokeStructureCreateActionAsync(
            page,
            "add-work-task",
            copyChildId,
            copyChildId,
            "Survey AP layout",
            "Networking",
            "Capture the wireless layout proof.");

        await EnsureCanvasSelectionAsync(page, SelectorForNodeId(copyRootId));
        await EnsureFloatingWindowExpandedAsync(page, "project-structure-selection-window");
        var copyRootState = await ReadWorkbenchNodeStateAsync(page, copyRootId);
        Assert.Contains("copy-id", copyRootState.AnnotationActionIds);
        Assert.Contains("copy-subtree-ids", copyRootState.AnnotationActionIds);
        await CaptureLocatorAsync(
            page.GetByTestId("project-structure-selection-window"),
            Path.Combine(artifactsDir, "01-selection-copy-actions.png"));

        await ClickSelectionWindowActionAsync(page, "Copy id");
        await WaitForCanvasClipboardTextAsync(page, copyRootId);

        await ClickSelectionWindowActionAsync(page, "Copy tree ids");
        await WaitForCanvasClipboardTextAsync(
            page,
            $"{copyRootId}\n  {copyChildId}\n    {copyGrandchildId}");

        var mutableBlockId = await InvokeStructureCreateActionAsync(
            page,
            "add-block-computer",
            projectRootId,
            projectRootId,
            "Mutable delivery block",
            "Release lane",
            "This block will change type in the browser.");
        await EnsureCanvasSelectionAsync(page, SelectorForNodeId(mutableBlockId));
        await ClickSelectionWindowActionAsync(page, "Change block");

        var blockMutationDialog = page.GetByTestId("project-structure-block-mutation-dialog");
        await blockMutationDialog.WaitForAsync();
        await CaptureLocatorAsync(blockMutationDialog, Path.Combine(artifactsDir, "02-change-block-dialog.png"));
        var blockMutationSelect = blockMutationDialog.Locator("[data-testid='project-structure-block-mutation-select']");
        await blockMutationSelect.SelectOptionAsync("add-block-router");
        Assert.Equal("add-block-router", await blockMutationSelect.InputValueAsync());
        await blockMutationDialog
            .GetByTestId("project-structure-block-mutation-submit")
            .EvaluateAsync("button => button.click()");
        await page.WaitForFunctionAsync("() => !document.querySelector('[data-testid=\"project-structure-block-mutation-dialog\"]')");

        var changedBlock = await WaitForWorkbenchNodeStateAsync(
            page,
            mutableBlockId,
            node => string.Equals(node.PaletteKey, "info", StringComparison.Ordinal) &&
                string.Equals(node.Title, "Mutable delivery block", StringComparison.Ordinal),
            "block changed to router");
        await page.GetByTestId("project-structure-selection-window")
            .GetByText("Router block", new LocatorGetByTextOptions { Exact = true })
            .WaitForAsync();
        Assert.Equal("info", changedBlock.PaletteKey);

        await FocusCanvasRootAsync(page);
        var noteEditor = await OpenInlineNoteEditorAsync(page);
        await page.WaitForTimeoutAsync(120);
        await noteEditor.ClickAsync();
        await page.Keyboard.TypeAsync("Site survey");
        await page.Keyboard.PressAsync("Shift+Enter");
        await page.Keyboard.TypeAsync("Check AP placement");

        var editorValue = NormalizeLineEndings(await noteEditor.InputValueAsync());
        Assert.Equal("Site survey\nCheck AP placement", editorValue);
        await CaptureLocatorAsync(noteEditor, Path.Combine(artifactsDir, "03-multiline-note-editor.png"));

        await noteEditor.PressAsync("Enter");
        await page.WaitForFunctionAsync("() => !document.querySelector('.cw-note-editor__input')");

        var noteId = (await WaitForCanvasFocusStateAsync(
            page,
            state => !string.IsNullOrWhiteSpace(state.SelectedId) &&
                !state.SelectedId.StartsWith("project:", StringComparison.Ordinal),
            "selected multiline note")).SelectedId!;
        await WaitForWorkbenchNodeStateAsync(
            page,
            noteId,
            node => node.IsInlineTextNode &&
                string.Equals(NormalizeLineEndings(node.Title), "Site survey\nCheck AP placement", StringComparison.Ordinal),
            "multiline note stored");

        await ClickSelectionWindowActionAsync(page, "Convert to block");
        blockMutationDialog = page.GetByTestId("project-structure-block-mutation-dialog");
        await blockMutationDialog.WaitForAsync();
        blockMutationSelect = blockMutationDialog.Locator("[data-testid='project-structure-block-mutation-select']");
        await blockMutationSelect.SelectOptionAsync("add-block-deployment");
        Assert.Equal("add-block-deployment", await blockMutationSelect.InputValueAsync());
        await CaptureLocatorAsync(blockMutationDialog, Path.Combine(artifactsDir, "04-convert-note-dialog.png"));
        await blockMutationDialog
            .GetByTestId("project-structure-block-mutation-submit")
            .EvaluateAsync("button => button.click()");
        await page.WaitForFunctionAsync("() => !document.querySelector('[data-testid=\"project-structure-block-mutation-dialog\"]')");

        var convertedNote = await WaitForWorkbenchNodeStateAsync(
            page,
            noteId,
            node => !node.IsInlineTextNode &&
                string.Equals(node.PaletteKey, "info", StringComparison.Ordinal) &&
                string.Equals(node.Title, "Site survey", StringComparison.Ordinal),
            "note converted to deployment block");
        await page.GetByTestId("project-structure-selection-window")
            .GetByText("Deployment block", new LocatorGetByTextOptions { Exact = true })
            .WaitForAsync();
        Assert.Equal("info", convertedNote.PaletteKey);
        await page.GetByTestId("project-structure-selection-window")
            .GetByRole(AriaRole.Button, new() { Name = "Change block", Exact = true })
            .WaitForAsync();

        await CaptureCanvasSurfaceAsync(page, Path.Combine(artifactsDir, "05-mutation-results.png"));
        Assert.False(await page.Locator("#blazor-error-ui").IsVisibleAsync());
    }

    [Fact]
    public async Task Project_structure_canvas_feedback_clipboard_subtree_and_subproject_transfer_are_validated_in_browser()
    {
        var repoRoot = GetRepoRoot();
        var artifactsDir = Path.Combine(repoRoot, "output", "playwright", "feedback-bundle-transfer");
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
        await CreateProjectAsync(page, "Playwright Canvas Feedback Transfer", "Validation");
        await InstallCanvasClipboardStubAsync(page);
        var projectRootId = await ReadNodeIdAsync(page, ".cw-node[data-node-id^='project:']");

        var sourceBlockId = await InvokeStructureCreateActionAsync(
            page,
            "add-block-task-flow",
            projectRootId,
            projectRootId,
            "Network orchestration",
            "Delivery flow",
            "Parent subtree node for cut and paste.");
        var movedTaskId = await InvokeStructureCreateActionAsync(
            page,
            "add-work-task",
            sourceBlockId,
            sourceBlockId,
            "Inventory network dependencies",
            "Networking",
            "Task child that should move with the subtree.");
        var movedEvidenceId = await InvokeStructureCreateActionAsync(
            page,
            "add-test-evidence",
            movedTaskId,
            movedTaskId,
            "Store rack photo",
            "Validation",
            "Grandchild evidence node that should follow the subtree.");

        await FocusCanvasRootAsync(page);
        await SetCanvasZoomPercentAsync(page, 72);
        await page.WaitForTimeoutAsync(250);

        var beforeSource = await ReadWorkbenchNodeStateAsync(page, sourceBlockId);
        var beforeTask = await ReadWorkbenchNodeStateAsync(page, movedTaskId);
        var beforeEvidence = await ReadWorkbenchNodeStateAsync(page, movedEvidenceId);

        await EnsureCanvasSelectionAsync(page, SelectorForNodeId(sourceBlockId));
        await CaptureCanvasSurfaceAsync(page, Path.Combine(artifactsDir, "01-before-cut-paste.png"));

        await PressCanvasShortcutAsync(page, "Control+X");
        var clipboardPayloadJson = await WaitForCanvasClipboardTextContainingAsync(page, "\"operation\":\"cut\"");
        using (var clipboardPayload = JsonDocument.Parse(clipboardPayloadJson))
        {
            var selectedIds = clipboardPayload.RootElement
                .GetProperty("selectedNodeIds")
                .EnumerateArray()
                .Select(item => item.GetString())
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .ToArray();
            Assert.Equal(new[] { sourceBlockId }, selectedIds);
        }

        await RouteCanvasPasteAsync(page, 2000, 1200);
        var afterSource = await WaitForWorkbenchNodeStateAsync(
            page,
            sourceBlockId,
            node => Math.Abs(node.X - beforeSource.X) > 40 || Math.Abs(node.Y - beforeSource.Y) > 40,
            "cut subtree moved after paste",
            timeoutMs: 10_000);

        var afterTask = await ReadWorkbenchNodeStateAsync(page, movedTaskId);
        var afterEvidence = await ReadWorkbenchNodeStateAsync(page, movedEvidenceId);

        var deltaX = afterSource.X - beforeSource.X;
        var deltaY = afterSource.Y - beforeSource.Y;
        Assert.True(Math.Abs(deltaX) > 40 || Math.Abs(deltaY) > 40, "Expected cut and paste to move the subtree root to a new canvas location.");
        Assert.InRange(Math.Abs((afterTask.X - beforeTask.X) - deltaX), 0, 6);
        Assert.InRange(Math.Abs((afterTask.Y - beforeTask.Y) - deltaY), 0, 6);
        Assert.InRange(Math.Abs((afterEvidence.X - beforeEvidence.X) - deltaX), 0, 6);
        Assert.InRange(Math.Abs((afterEvidence.Y - beforeEvidence.Y) - deltaY), 0, 6);

        await CaptureCanvasSurfaceAsync(page, Path.Combine(artifactsDir, "02-after-cut-paste.png"));

        await EnsureCanvasSelectionAsync(page, SelectorForNodeId(sourceBlockId));
        await ClickSelectionWindowActionAsync(page, "To subproject");

        var subprojectTransferDialog = page.GetByTestId("project-structure-subproject-transfer-dialog");
        await subprojectTransferDialog.WaitForAsync();
        const string subprojectName = "Network extraction subproject";
        await subprojectTransferDialog
            .GetByTestId("project-structure-subproject-transfer-name")
            .FillAsync(subprojectName);
        await CaptureLocatorAsync(subprojectTransferDialog, Path.Combine(artifactsDir, "03-subproject-transfer-dialog.png"));
        await subprojectTransferDialog
            .GetByTestId("project-structure-subproject-transfer-submit")
            .EvaluateAsync("button => button.click()");

        await WaitForSceneSnapshotAsync(
            page,
            snapshot => snapshot.Nodes.Any(node => string.Equals(node.Title, subprojectName, StringComparison.Ordinal)),
            "subproject node appears after descendant transfer",
            timeoutMs: 10_000);

        await WaitForWorkbenchNodeMissingAsync(page, movedTaskId);
        await WaitForWorkbenchNodeMissingAsync(page, movedEvidenceId);

        var subprojectNodeId = await FindNodeIdByTitleAsync(page, subprojectName);
        Assert.StartsWith("project-child:", subprojectNodeId, StringComparison.Ordinal);
        var subprojectId = subprojectNodeId["project-child:".Length..];

        await page.GotoAsync(ToAbsoluteRoute($"/projects/{subprojectId}/structure"));
        await page.WaitForURLAsync("**/projects/*/structure");
        await page.WaitForSelectorAsync("text=Structure canvas");
        await WaitForSceneNodeTitleAsync(page, "Inventory network dependencies", timeoutMs: 10_000);

        await WaitForWorkbenchNodeStateAsync(
            page,
            movedTaskId,
            node => string.Equals(node.Title, "Inventory network dependencies", StringComparison.Ordinal),
            "moved task is present in the new subproject");
        await WaitForWorkbenchNodeStateAsync(
            page,
            movedEvidenceId,
            node => string.Equals(node.Title, "Store rack photo", StringComparison.Ordinal),
            "moved evidence is present in the new subproject");
        await WaitForWorkbenchNodeMissingAsync(page, sourceBlockId);

        await CaptureCanvasSurfaceAsync(page, Path.Combine(artifactsDir, "04-subproject-route.png"));
        Assert.False(await page.Locator("#blazor-error-ui").IsVisibleAsync());
    }

    private async Task AssertToolboxSearchResultAsync(IPage page, string query, string label)
    {
        await EnsureStructureToolboxWindowExpandedAsync(page);
        var search = page.Locator("[data-testid='project-structure-standard-blocks-toolbox'] .project-structure-toolbox__search");
        await search.WaitForAsync();
        await search.FillAsync(query);
        await page.GetByTestId("project-structure-toolbox-window")
            .GetByText(label, new LocatorGetByTextOptions { Exact = true })
            .WaitForAsync();
    }

    private async Task ClickSelectionWindowActionAsync(IPage page, string label)
    {
        await EnsureFloatingWindowExpandedAsync(page, "project-structure-selection-window");
        var button = page.GetByTestId("project-structure-selection-window")
            .GetByRole(AriaRole.Button, new() { Name = label, Exact = true });
        await button.WaitForAsync();
        await button.ClickAsync();
    }

    private async Task PressCanvasShortcutAsync(IPage page, string shortcut)
    {
        await page.EvaluateAsync(
            @"() => {
                const host = document.querySelector('.cw-canvas-host');
                if (host instanceof HTMLElement) {
                    host.focus();
                }
            }");
        await page.Keyboard.PressAsync(shortcut);
        await page.WaitForTimeoutAsync(280);
    }

    private static async Task FocusCanvasNodeAsync(IPage page, string nodeId, int zoomPercent = 100)
    {
        await page.EvaluateAsync(
            @"args => {
                const host = document.querySelector('.cw-canvas-host');
                const runtime = window.CanDoItAll?.canvasWorkbench;
                if (!host || !runtime?.focusNode || !runtime?.setZoomPercent) {
                    return;
                }

                runtime.focusNode(host, args.nodeId);
                runtime.setZoomPercent(host, args.zoomPercent);
            }",
            new
            {
                nodeId,
                zoomPercent
            });
        await page.WaitForTimeoutAsync(250);
    }

    private static async Task RouteCanvasPasteAsync(IPage page, double anchorX, double anchorY)
    {
        var routed = await page.EvaluateAsync<bool>(
            @"async requestedAnchor => {
                const host = document.querySelector('.cw-canvas-host');
                const state = host?.__canvasWorkbenchState;
                const readClipboardText = window.CanDoItAll?.canvasWorkbenchModule?.readClipboardText;
                if (!state?.dotNetRef?.invokeMethodAsync || typeof readClipboardText !== 'function') {
                    return false;
                }

                const payload = await readClipboardText();
                if (!payload) {
                    return false;
                }

                let surfaceId = state.surface?.surfaceId || '';
                try {
                    const parsedPayload = JSON.parse(payload);
                    if (parsedPayload && typeof parsedPayload.surfaceId === 'string' && parsedPayload.surfaceId.length > 0) {
                        surfaceId = parsedPayload.surfaceId;
                    }
                } catch {
                }

                const envelope = JSON.stringify({
                    payloadJson: payload,
                    anchorWorld: {
                        x: requestedAnchor.x,
                        y: requestedAnchor.y
                    },
                    surfaceId
                });
                await state.dotNetRef.invokeMethodAsync('OnClipboardAction', 'paste', envelope);
                return true;
            }",
            new
            {
                x = anchorX,
                y = anchorY
            });
        Assert.True(routed, "Expected the browser canvas bridge to route the clipboard paste request.");
        await page.WaitForTimeoutAsync(280);
    }

    private static async Task InstallCanvasClipboardStubAsync(IPage page)
    {
        await page.EvaluateAsync(
            @"() => {
                window.__feedbackBundleClipboard = '';
                window.__canvasClipboardWrite = async value => {
                    window.__feedbackBundleClipboard = value;
                };
                window.__canvasClipboardRead = async () => window.__feedbackBundleClipboard || '';

                const clipboard = {
                    writeText: async value => {
                        window.__feedbackBundleClipboard = value;
                    },
                    readText: async () => window.__feedbackBundleClipboard || ''
                };

                try {
                    Object.defineProperty(navigator, 'clipboard', {
                        configurable: true,
                        value: clipboard
                    });
                } catch {
                    navigator.clipboard = clipboard;
                }
            }");
    }

    private static async Task<string> ReadCanvasClipboardTextAsync(IPage page)
    {
        var value = await page.EvaluateAsync<string?>("() => window.__feedbackBundleClipboard || ''");
        return NormalizeLineEndings(value ?? string.Empty);
    }

    private static async Task<string> WaitForCanvasClipboardTextAsync(IPage page, string expected, int timeoutMs = 4_000)
    {
        var normalizedExpected = NormalizeLineEndings(expected);
        var attempts = Math.Max(1, timeoutMs / 120);
        string? lastValue = null;
        for (var attempt = 0; attempt < attempts; attempt++)
        {
            lastValue = await ReadCanvasClipboardTextAsync(page);
            if (string.Equals(lastValue, normalizedExpected, StringComparison.Ordinal))
            {
                return lastValue;
            }

            await page.WaitForTimeoutAsync(120);
        }

        throw new InvalidOperationException(
            $"Timed out waiting for clipboard text '{normalizedExpected}'. Last value was '{lastValue ?? string.Empty}'.");
    }

    private static async Task<string> WaitForCanvasClipboardTextContainingAsync(IPage page, string expectedFragment, int timeoutMs = 4_000)
    {
        var normalizedFragment = NormalizeLineEndings(expectedFragment);
        var attempts = Math.Max(1, timeoutMs / 120);
        string? lastValue = null;
        for (var attempt = 0; attempt < attempts; attempt++)
        {
            lastValue = await ReadCanvasClipboardTextAsync(page);
            if (lastValue.Contains(normalizedFragment, StringComparison.Ordinal))
            {
                return lastValue;
            }

            await page.WaitForTimeoutAsync(120);
        }

        throw new InvalidOperationException(
            $"Timed out waiting for clipboard text containing '{normalizedFragment}'. Last value was '{lastValue ?? string.Empty}'.");
    }

    private static async Task<string> ReadNodeAccentColorAsync(IPage page, string nodeId)
    {
        var accentColor = await page.EvaluateAsync<string?>(
            @"requestedNodeId => {
                const host = document.querySelector('.cw-canvas-host');
                const node = host?.__canvasWorkbenchState?.lookups?.byId?.get(requestedNodeId);
                const resolveAccentColor = window.CanDoItAll?.canvasWorkbenchModule?.resolveNodeAccentColor;
                if (!node || typeof resolveAccentColor !== 'function') {
                    return null;
                }

                return resolveAccentColor(node) || null;
            }",
            nodeId);
        Assert.False(string.IsNullOrWhiteSpace(accentColor), $"Expected an accent color for node '{nodeId}'.");
        return accentColor!;
    }

    private static async Task<CanvasNodeDomPaletteSnapshot> ReadNodeDomPaletteAsync(IPage page, string nodeId)
    {
        await page.Locator(SelectorForNodeId(nodeId)).First.WaitForAsync();
        var snapshot = await page.EvaluateAsync<CanvasNodeDomPaletteSnapshot?>(
            @"requestedNodeId => {
                const node = Array.from(document.querySelectorAll('.cw-node'))
                    .find(candidate => candidate instanceof HTMLElement && candidate.dataset.nodeId === requestedNodeId);
                if (!(node instanceof HTMLElement)) {
                    return null;
                }

                const styles = getComputedStyle(node);
                return {
                    paletteKey: node.dataset.palette || '',
                    surfaceBackground: styles.getPropertyValue('--cw-node-surface-bg') || '',
                    surfaceBorder: styles.getPropertyValue('--cw-node-surface-border') || ''
                };
            }",
            nodeId);
        Assert.NotNull(snapshot);
        return snapshot!;
    }

    private static async Task<CanvasWorkbenchNodeRuntimeSnapshot?> TryReadWorkbenchNodeStateAsync(IPage page, string nodeId)
        => await page.EvaluateAsync<CanvasWorkbenchNodeRuntimeSnapshot?>(
            @"requestedNodeId => {
                const host = document.querySelector('.cw-canvas-host');
                const node = host?.__canvasWorkbenchState?.lookups?.byId?.get(requestedNodeId);
                if (!node) {
                    return null;
                }

                return {
                    id: node.id || '',
                    title: node.title || '',
                    notes: node.notes || '',
                    objectSubtype: node.objectSubtype || '',
                    paletteKey: node.paletteKey || '',
                    route: node.route || '',
                    x: typeof node.x === 'number' ? node.x : 0,
                    y: typeof node.y === 'number' ? node.y : 0,
                    isInlineTextNode: !!node.isInlineTextNode,
                    annotationActionIds: Array.isArray(node.annotations) ? node.annotations.map(annotation => annotation.actionId || '') : []
                };
            }",
            nodeId);

    private static async Task<CanvasWorkbenchNodeRuntimeSnapshot> ReadWorkbenchNodeStateAsync(IPage page, string nodeId)
    {
        var snapshot = await TryReadWorkbenchNodeStateAsync(page, nodeId);
        Assert.NotNull(snapshot);
        return snapshot!;
    }

    private static async Task<CanvasWorkbenchNodeRuntimeSnapshot> WaitForWorkbenchNodeStateAsync(
        IPage page,
        string nodeId,
        Func<CanvasWorkbenchNodeRuntimeSnapshot, bool> predicate,
        string description,
        int timeoutMs = 6_000)
    {
        var attempts = Math.Max(1, timeoutMs / 120);
        CanvasWorkbenchNodeRuntimeSnapshot? lastSnapshot = null;
        for (var attempt = 0; attempt < attempts; attempt++)
        {
            lastSnapshot = await TryReadWorkbenchNodeStateAsync(page, nodeId);
            if (lastSnapshot is not null && predicate(lastSnapshot))
            {
                return lastSnapshot;
            }

            await page.WaitForTimeoutAsync(120);
        }

        throw new InvalidOperationException(
            $"Timed out waiting for workbench node state '{description}' on '{nodeId}'. " +
            $"Last snapshot title='{lastSnapshot?.Title ?? string.Empty}', subtype='{lastSnapshot?.ObjectSubtype ?? string.Empty}', " +
            $"palette='{lastSnapshot?.PaletteKey ?? string.Empty}', inline={lastSnapshot?.IsInlineTextNode}, " +
            $"x={lastSnapshot?.X}, y={lastSnapshot?.Y}.");
    }

    private static async Task WaitForWorkbenchNodeMissingAsync(IPage page, string nodeId, int timeoutMs = 6_000)
    {
        var attempts = Math.Max(1, timeoutMs / 120);
        for (var attempt = 0; attempt < attempts; attempt++)
        {
            if (await TryReadWorkbenchNodeStateAsync(page, nodeId) is null)
            {
                return;
            }

            await page.WaitForTimeoutAsync(120);
        }

        throw new InvalidOperationException($"Timed out waiting for workbench node '{nodeId}' to disappear from the active canvas state.");
    }

    private string ToAbsoluteRoute(string route)
        => route.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            route.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                ? route
                : $"{fixture.BaseUrl.TrimEnd('/')}/{route.TrimStart('/')}";

    private static string NormalizeLineEndings(string value)
        => value.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');

    private sealed class CanvasNodeDomPaletteSnapshot
    {
        public string PaletteKey { get; set; } = string.Empty;

        public string SurfaceBackground { get; set; } = string.Empty;

        public string SurfaceBorder { get; set; } = string.Empty;
    }

    private sealed class CanvasWorkbenchNodeRuntimeSnapshot
    {
        public string Id { get; set; } = string.Empty;

        public string Title { get; set; } = string.Empty;

        public string Notes { get; set; } = string.Empty;

        public string ObjectSubtype { get; set; } = string.Empty;

        public string PaletteKey { get; set; } = string.Empty;

        public string Route { get; set; } = string.Empty;

        public double X { get; set; }

        public double Y { get; set; }

        public bool IsInlineTextNode { get; set; }

        public string[] AnnotationActionIds { get; set; } = [];
    }
}
