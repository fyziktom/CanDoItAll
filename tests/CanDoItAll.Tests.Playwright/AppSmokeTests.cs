using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright;

public sealed class AppSmokeTests(PlaywrightAppFixture fixture) : IClassFixture<PlaywrightAppFixture>
{
    [Fact]
    public async Task Dashboard_and_project_creation_flow_work()
    {
        await using var context = await fixture.Browser.NewContextAsync();
        var page = await context.NewPageAsync();

        var projectId = await CreateProjectAsync(page, "Playwright Project", "Discovery");

        await page.WaitForSelectorAsync("text=Structure canvas");
        Assert.Contains($"/projects/{projectId}/structure", page.Url, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Workbench_session_routes_are_persisted_after_reload()
    {
        await using var context = await fixture.Browser.NewContextAsync();
        var page = await context.NewPageAsync();

        await page.GotoAsync($"{fixture.BaseUrl}/projects");
        await page.GotoAsync($"{fixture.BaseUrl}/validation");
        await page.GotoAsync($"{fixture.BaseUrl}/test-lab");
        await page.WaitForSelectorAsync("text=Tests, evidence, and execution results");
        await page.WaitForFunctionAsync("() => localStorage.getItem('candoitall.workbench.session')?.includes('route:test-lab') === true");

        var storageBeforeReload = await page.EvaluateAsync<string?>("() => localStorage.getItem('candoitall.workbench.session')");
        Assert.NotNull(storageBeforeReload);
        Assert.Contains("\"version\":3", storageBeforeReload, StringComparison.Ordinal);
        Assert.Contains("route:test-lab", storageBeforeReload, StringComparison.Ordinal);

        await page.ReloadAsync();
        await page.WaitForSelectorAsync("text=Tests, evidence, and execution results");

        var storageAfterReload = await page.EvaluateAsync<string?>("() => localStorage.getItem('candoitall.workbench.session')");
        Assert.NotNull(storageAfterReload);
        Assert.Contains("\"version\":3", storageAfterReload, StringComparison.Ordinal);
        Assert.Contains("route:test-lab", storageAfterReload, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Direct_module_routes_and_workbench_surfaces_load_without_circuit_failure()
    {
        await using var context = await fixture.Browser.NewContextAsync();
        var page = await context.NewPageAsync();

        var projectsResponse = await page.GotoAsync($"{fixture.BaseUrl}/projects");
        Assert.NotNull(projectsResponse);
        Assert.True(projectsResponse!.Ok, $"Expected /projects to return 2xx, got {(int)projectsResponse.Status}.");
        await page.GetByTestId("projects-new-button").WaitForAsync();
        await page.WaitForSelectorAsync("text=Project workspace");
        Assert.False(await page.Locator("#blazor-error-ui").IsVisibleAsync());

        await CreateProjectAsync(page, "Playwright Workbench", "Discovery");

        await page.WaitForURLAsync("**/projects/*/structure");
        await page.WaitForSelectorAsync("text=Structure canvas");
        await page.Locator(".cw-workbench-shell").WaitForAsync();
        await page.GetByRole(AriaRole.Heading, new() { Name = "Local delivery workbench" }).WaitForAsync();
        await page.GetByLabel("Canvas zoom").WaitForAsync();
        await page.GetByLabel("Open quick create actions").ClickAsync();
        await page.Locator(".cw-context-menu__action[data-action-id='add-note']").WaitForAsync();
        await page.Locator(".cw-context-menu__action[data-action-id='group-assets']").HoverAsync(new() { Force = true });
        await page.Locator(".cw-context-menu__action[data-action-id='add-image-asset']").WaitForAsync();
        await page.Keyboard.PressAsync("Escape");
        await page.GetByRole(AriaRole.Button, new() { Name = "Toggle help", Exact = true }).ClickAsync();
        await page.GetByRole(AriaRole.Dialog, new() { Name = "Canvas shortcuts and gestures" }).WaitForAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Close help" }).ClickAsync();
        Assert.False(await page.Locator("#blazor-error-ui").IsVisibleAsync());

        var match = Regex.Match(page.Url, @"/projects/(?<projectId>[0-9a-fA-F-]+)/structure$", RegexOptions.IgnoreCase);
        Assert.True(match.Success, $"Could not parse project id from {page.Url}.");

        var calendarResponse = await page.GotoAsync($"{fixture.BaseUrl}/projects/{match.Groups["projectId"].Value}/calendar");
        Assert.NotNull(calendarResponse);
        Assert.True(calendarResponse!.Ok, $"Expected calendar route to return 2xx, got {(int)calendarResponse.Status}.");
        await page.WaitForSelectorAsync("text=Project calendar");
        Assert.False(await page.Locator("#blazor-error-ui").IsVisibleAsync());
    }

    [Fact]
    public async Task Prompt_factory_canvas_surface_loads_and_exposes_shared_chrome()
    {
        await using var context = await fixture.Browser.NewContextAsync();
        var page = await context.NewPageAsync();

        var projectId = await CreateProjectAsync(page, "Playwright Prompt Factory", "Review");

        var response = await page.GotoAsync($"{fixture.BaseUrl}/prompt-factory");
        Assert.NotNull(response);
        Assert.True(response!.Ok, $"Expected /prompt-factory to return 2xx, got {(int)response.Status}.");

        await page.WaitForSelectorAsync("text=Prompt session workbench");
        await page.WaitForTimeoutAsync(4000);
        await page.GetByTestId("prompt-factory-project").SelectOptionAsync(projectId.ToString());
        await page.WaitForFunctionAsync(
            "projectId => document.querySelector('[data-testid=\"prompt-factory-project\"]')?.value === projectId",
            projectId.ToString());
        await page.Locator(".cw-workbench-shell").WaitForAsync();
        await page.GetByRole(AriaRole.Heading, new() { Name = "Local delivery workbench" }).WaitForAsync();
        await page.GetByLabel("Canvas zoom").WaitForAsync();
        await page.WaitForTimeoutAsync(1000);
        await page.GetByRole(AriaRole.Button, new() { Name = "Toggle help", Exact = true }).ClickAsync();
        await page.GetByRole(AriaRole.Dialog, new() { Name = "Canvas shortcuts and gestures" }).WaitForAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Close help" }).ClickAsync();
        await page.Locator(".pf-inspector-step:has-text('Assembly')").ClickAsync();
        await page.WaitForFunctionAsync(
            @"() => Array.from(document.querySelectorAll('.pf-inspector-step'))
                .some(step => step.textContent?.includes('Assembly') === true &&
                              step.getAttribute('aria-selected') === 'true')");
        await page.GetByTestId("prompt-factory-build").First.WaitForAsync();
        await page.GetByTestId("prompt-factory-build").First.ClickAsync();
        await page.GetByTestId("prompt-factory-prompt-modal").WaitForAsync();
        await page.GetByTestId("prompt-factory-prompt-modal-text").WaitForAsync();
        Assert.False(await page.Locator("#blazor-error-ui").IsVisibleAsync());
    }

    [Fact]
    public async Task Structure_file_create_dialog_choose_file_button_opens_native_picker()
    {
        await using var context = await fixture.Browser.NewContextAsync();
        var page = await context.NewPageAsync();

        await CreateProjectAsync(page, "Playwright File Chooser", "Review");

        var canvasHost = page.Locator(".cw-canvas-host");
        await canvasHost.ScrollIntoViewIfNeededAsync();
        await ClickCanvasNodeAsync(page, ".cw-node[data-node-id^='project:']");
        await page.WaitForFunctionAsync("() => (document.activeElement?.className || '').includes('cw-canvas-host')");

        await OpenCanvasContextMenuAsync(page, ".cw-node[data-node-id^='project:']");
        await page.Locator(".cw-context-menu__action[data-action-id='group-assets']").HoverAsync(new() { Force = true });
        await page.Locator(".cw-context-menu__action[data-action-id='add-file']").WaitForAsync();
        await page.Locator(".cw-context-menu__action[data-action-id='add-file']").ClickAsync(new() { Force = true });
        await page.Locator(".cw-canvas-composer__upload-title").Filter(new() { HasText = "Drop a file here or choose one." }).WaitForAsync();
        await page.Locator(".cw-canvas-composer__file-trigger").WaitForAsync();

        var chooser = await page.RunAndWaitForFileChooserAsync(async () =>
            await page.GetByRole(AriaRole.Button, new() { Name = "Choose file", Exact = true }).ClickAsync());
        await chooser.SetFilesAsync(
        [
            new FilePayload
            {
                Name = "playwright-structure-file.txt",
                MimeType = "text/plain",
                Buffer = Encoding.UTF8.GetBytes("structure file chooser smoke test")
            }
        ]);

        await page.WaitForFunctionAsync("() => document.querySelector('.cw-canvas-composer__upload-summary')?.textContent?.includes('playwright-structure-file.txt') === true");
        await page.WaitForFunctionAsync(
            @"() => {
                const button = document.querySelector('.cw-canvas-composer__actions .cw-button[data-tone=""accent""]');
                return button instanceof HTMLButtonElement && button.disabled !== true;
            }");
        Assert.False(await page.Locator("#blazor-error-ui").IsVisibleAsync());
    }

    [Fact]
    public async Task Structure_canvas_supports_inline_note_creation_editing_and_context_create_dialogs()
    {
        await using var context = await fixture.Browser.NewContextAsync();
        var page = await context.NewPageAsync();
        var artifactsDir = @"C:\repositories\CanDoItAll\output\playwright";
        Directory.CreateDirectory(artifactsDir);

        await CreateProjectAsync(page, "Playwright Canvas Repair", "Discovery");

        var canvasHost = page.Locator(".cw-canvas-host");
        await canvasHost.ScrollIntoViewIfNeededAsync();
        await ClickCanvasNodeAsync(page, ".cw-node[data-node-id^='project:']");
        await page.WaitForFunctionAsync("() => (document.activeElement?.className || '').includes('cw-canvas-host')");
        var activeElementClass = await page.EvaluateAsync<string>("() => document.activeElement?.className || ''");
        Assert.Contains("cw-canvas-host", activeElementClass, StringComparison.Ordinal);

        var radialLabels = await OpenCanvasContextMenuAsync(page, ".cw-node[data-node-id^='project:']");
        Assert.Contains("Assets", radialLabels);
        Assert.Contains("Blocks", radialLabels);
        Assert.Contains("Prompts", radialLabels);
        Assert.Contains("Assurance", radialLabels);
        Assert.Contains("Open", radialLabels);
        Assert.Contains("Connect", radialLabels);
        Assert.Contains("Progress", radialLabels);
        Assert.Contains("Marker", radialLabels);
        Assert.Contains("Priority", radialLabels);
        var defaultOpenActionMetrics = await ReadContextMenuActionMetricsAsync(page, "open");
        await page.ScreenshotAsync(new() { Path = Path.Combine(artifactsDir, "structure-root-menu-metadata.png"), FullPage = true });
        await page.Keyboard.PressAsync("Escape");

        await page.GetByRole(AriaRole.Button, new() { Name = "Toggle settings", Exact = true }).ClickAsync();
        await page.GetByRole(AriaRole.Dialog, new() { Name = "Canvas settings" }).WaitForAsync();
        await page.EvaluateAsync(
            @"() => {
                const input = document.querySelector('input[aria-label=""Canvas menu item size""]');
                if (!input) {
                    return;
                }

                input.value = '120';
                input.dispatchEvent(new Event('input', { bubbles: true }));
            }");
        await page.WaitForTimeoutAsync(250);
        await page.GetByRole(AriaRole.Button, new() { Name = "Close settings" }).ClickAsync();

        radialLabels = await OpenCanvasContextMenuAsync(page, ".cw-node[data-node-id^='project:']");
        var scaledOpenActionMetrics = await ReadContextMenuActionMetricsAsync(page, "open");
        Assert.True(scaledOpenActionMetrics.Width >= defaultOpenActionMetrics.Width + 6, $"Expected the menu scale setting to enlarge root actions. Before={defaultOpenActionMetrics.Width}, after={scaledOpenActionMetrics.Width}.");
        await page.Locator(".cw-context-menu__action[data-action-id='group-assets']").HoverAsync(new() { Force = true });
        await page.WaitForSelectorAsync(".cw-context-menu__orbit.is-submenu");
        var submenuBackdrop = await page.EvaluateAsync<string?>(
            @"() => getComputedStyle(document.querySelector('.cw-context-menu__orbit.is-submenu')).backdropFilter");
        Assert.False(string.IsNullOrWhiteSpace(submenuBackdrop));
        Assert.DoesNotContain("none", submenuBackdrop, StringComparison.OrdinalIgnoreCase);

        var submenuMetrics = await page.EvaluateAsync<CanvasSubmenuMetrics>(
            @"() => {
                const action = document.querySelector('.cw-context-menu__action[data-action-id=""group-assets""]');
                const orbit = document.querySelector('.cw-context-menu__orbit.is-submenu');
                if (!action || !orbit) {
                    return { actionRight: 0, actionMidY: 0, orbitX: 0, orbitY: 0 };
                }

                const actionRect = action.getBoundingClientRect();
                const orbitRect = orbit.getBoundingClientRect();
                return {
                    actionRight: actionRect.right,
                    actionMidY: actionRect.top + (actionRect.height / 2),
                    orbitX: orbitRect.left + (orbitRect.width * 0.78),
                    orbitY: orbitRect.top + (orbitRect.height * 0.42)
                };
            }");
        await page.Mouse.MoveAsync((float)(submenuMetrics.ActionRight + 16), (float)submenuMetrics.ActionMidY);
        Assert.Equal(1, await page.Locator(".cw-context-menu__orbit.is-submenu").CountAsync());
        await page.Mouse.MoveAsync((float)submenuMetrics.OrbitX, (float)submenuMetrics.OrbitY);
        Assert.Equal(1, await page.Locator(".cw-context-menu__orbit.is-submenu").CountAsync());
        await page.Locator(".cw-context-menu__action[data-action-id='add-image-asset']").WaitForAsync();
        await page.Locator(".cw-context-menu__action[data-action-id='add-video-asset']").WaitForAsync();
        await page.Keyboard.PressAsync("Escape");

        await OpenCanvasContextMenuAsync(page, ".cw-node[data-node-id^='project:']");
        var rootMenuBounds = await ReadContextMenuBoundsAsync(page);
        Assert.True(rootMenuBounds.MinLeft >= 0, $"Expected the radial menu to stay inside the left viewport edge, but minLeft was {rootMenuBounds.MinLeft}.");
        Assert.True(rootMenuBounds.MaxRight <= rootMenuBounds.ViewportWidth, $"Expected the radial menu to stay inside the right viewport edge, but maxRight was {rootMenuBounds.MaxRight} of {rootMenuBounds.ViewportWidth}.");
        await page.Locator(".cw-context-menu__action[data-action-id='group-blocks']").HoverAsync(new() { Force = true });
        await page.Locator(".cw-context-menu__action[data-action-id='add-block-feature']").WaitForAsync();
        await page.Locator(".cw-context-menu__action[data-action-id='add-block-support']").WaitForAsync();
        await page.Keyboard.PressAsync("Escape");

        await page.Keyboard.PressAsync("Tab");
        var noteEditor = page.Locator(".cw-note-editor__input");
        await noteEditor.WaitForAsync();
        await noteEditor.FillAsync("Child note from keyboard");
        await noteEditor.PressAsync("Enter");
        await page.WaitForFunctionAsync("() => !document.querySelector('.cw-note-editor__input')");
        await page.WaitForSelectorAsync("text=Child note from keyboard");
        await ClickCanvasNodeAsync(page, ".cw-node:has-text('Child note from keyboard')");
        await page.WaitForFunctionAsync(
            @"() => {
                const surface = document.querySelector('.cw-node.is-inline-text.is-selected .cw-node__surface');
                return !!surface && getComputedStyle(surface).boxShadow.includes('0px 0px 0px 4px');
            }");

        await page.WaitForFunctionAsync("() => document.querySelectorAll('.cw-node').length > 1");
        await page.Locator(".cw-node[data-node-id^='project:'] .cw-node__collapse").ClickAsync();
        await page.WaitForFunctionAsync("() => !document.querySelector('.cw-node:not([data-node-id^=\"project:\"])')");
        await page.Locator(".cw-node[data-node-id^='project:'] .cw-node__collapse").ClickAsync();
        await page.WaitForFunctionAsync("() => !!document.querySelector('.cw-node:not([data-node-id^=\"project:\"])')");

        await page.Keyboard.PressAsync("Tab");
        await noteEditor.WaitForAsync();
        await noteEditor.FillAsync("Second child note");
        await noteEditor.PressAsync("Enter");
        await page.WaitForFunctionAsync("() => !document.querySelector('.cw-note-editor__input')");
        await page.WaitForSelectorAsync("text=Second child note");
        await AssertNoCanvasNodeOverlapsAsync(page, "after chained child-note creation");

        await ClickCanvasNodeAsync(page, ".cw-node.is-inline-text", clickCount: 2);
        await noteEditor.WaitForAsync();
        await noteEditor.FillAsync("Edited child note");
        await noteEditor.PressAsync("Enter");
        await page.WaitForFunctionAsync("() => !document.querySelector('.cw-note-editor__input')");
        await page.WaitForSelectorAsync("text=Edited child note");

        var nodeLabels = await OpenCanvasContextMenuAsync(page, ".cw-node:has-text('Edited child note')");
        Assert.Contains("Progress", nodeLabels);
        Assert.Contains("Marker", nodeLabels);
        Assert.Contains("Priority", nodeLabels);
        await page.Locator(".cw-context-menu__action[data-action-id='progress']").WaitForAsync();
        await page.EvaluateAsync(
            @"() => document.querySelector('.cw-context-menu__action[data-action-id=""progress""]')
                ?.dispatchEvent(new PointerEvent('pointerenter', { bubbles: true }))");
        await page.Locator(".cw-context-menu__action[data-action-id='progress:0']").WaitForAsync();
        await page.Locator(".cw-context-menu__action[data-action-id='progress:started']").WaitForAsync();
        await page.Locator(".cw-context-menu__action[data-action-id='progress:100']").WaitForAsync();
        var progressMetrics = await ReadContextMenuActionMetricsAsync(page, "progress:30");
        var progressBackground = await page.EvaluateAsync<string>(
            @"() => getComputedStyle(document.querySelector('.cw-context-menu__action[data-action-id=""progress:30""]')).backgroundImage");
        Assert.DoesNotContain("16, 185, 129", progressBackground, StringComparison.Ordinal);
        Assert.DoesNotContain("56, 189, 248", progressBackground, StringComparison.Ordinal);
        await page.ScreenshotAsync(new() { Path = Path.Combine(artifactsDir, "structure-progress-submenu-metadata.png"), FullPage = true });
        await page.Locator(".cw-context-menu__action[data-action-id='progress:30']").ClickAsync(new() { Force = true });
        await page.WaitForFunctionAsync("() => document.querySelector('.cw-node.is-selected .cw-node__progress')?.getAttribute('title')?.includes('30%') === true");

        var progressBadge = page.Locator(".cw-node.is-selected .cw-node__progress").First;
        var progressBadgeBounds = await progressBadge.BoundingBoxAsync();
        Assert.NotNull(progressBadgeBounds);
        await page.Mouse.ClickAsync(
            progressBadgeBounds!.X + (progressBadgeBounds.Width / 2),
            progressBadgeBounds.Y + (progressBadgeBounds.Height / 2),
            new() { ClickCount = 2 });
        await page.Locator(".cw-context-menu__action[data-action-id='progress:100']").WaitForAsync();
        await page.Keyboard.PressAsync("Escape");

        nodeLabels = await OpenCanvasContextMenuAsync(page, ".cw-node:has-text('Edited child note')");
        Assert.Contains("Marker", nodeLabels);
        await page.Locator(".cw-context-menu__action[data-action-id='marker']").WaitForAsync();
        await page.EvaluateAsync(
            @"() => document.querySelector('.cw-context-menu__action[data-action-id=""marker""]')
                ?.dispatchEvent(new PointerEvent('pointerenter', { bubbles: true }))");
        await page.Locator(".cw-context-menu__action[data-action-id='marker:question']").WaitForAsync();
        var markerMetrics = await ReadContextMenuActionMetricsAsync(page, "marker:question");
        Assert.True(markerMetrics.Width >= progressMetrics.Width - 2, $"Expected marker presets to stay comparable to progress preset size. Marker={markerMetrics.Width}, progress={progressMetrics.Width}.");
        await page.Locator(".cw-context-menu__action[data-action-id='marker:money']").ClickAsync(new() { Force = true });
        await page.WaitForFunctionAsync("() => document.querySelector('.cw-node.is-selected .cw-node__marker')?.textContent?.includes('$') === true");

        nodeLabels = await OpenCanvasContextMenuAsync(page, ".cw-node:has-text('Edited child note')");
        Assert.Contains("Priority", nodeLabels);
        await page.Locator(".cw-context-menu__action[data-action-id='priority']").WaitForAsync();
        await page.EvaluateAsync(
            @"() => document.querySelector('.cw-context-menu__action[data-action-id=""priority""]')
                ?.dispatchEvent(new PointerEvent('pointerenter', { bubbles: true }))");
        await page.Locator(".cw-context-menu__action[data-action-id='priority:1']").WaitForAsync();
        await page.Locator(".cw-context-menu__action[data-action-id='priority:6']").WaitForAsync();
        var priorityMetrics = await ReadContextMenuActionMetricsAsync(page, "priority:2");
        var priorityPresetLabelCount = await page.EvaluateAsync<int>(
            @"() => Array.from(document.querySelectorAll('.cw-context-menu__action.is-priority-preset .cw-context-menu__label'))
                .filter(label => (label.textContent || '').trim().length > 0)
                .length");
        Assert.True(progressMetrics.Width > priorityMetrics.Width, $"Expected progress presets to be larger than priority presets. Progress={progressMetrics.Width}, priority={priorityMetrics.Width}.");
        Assert.True(markerMetrics.Width > priorityMetrics.Width, $"Expected marker presets to be larger than priority presets. Marker={markerMetrics.Width}, priority={priorityMetrics.Width}.");
        Assert.Equal(0, priorityPresetLabelCount);
        await page.Locator(".cw-context-menu__action[data-action-id='priority:2']").ClickAsync(new() { Force = true });
        await page.WaitForFunctionAsync("() => document.querySelector('.cw-node.is-selected .cw-node__priority')?.textContent?.trim() === '2'");
        await page.ScreenshotAsync(new() { Path = Path.Combine(artifactsDir, "structure-note-badges-selected.png"), FullPage = true });

        await page.Keyboard.PressAsync("Enter");
        await noteEditor.WaitForAsync();
        await noteEditor.FillAsync("Sibling note from Enter");
        await noteEditor.PressAsync("Enter");
        await page.WaitForFunctionAsync("() => !document.querySelector('.cw-note-editor__input')");
        await page.WaitForSelectorAsync("text=Sibling note from Enter");
        await AssertNoCanvasNodeOverlapsAsync(page, "after sibling-note creation");

        await ClickCanvasNodeAsync(page, ".cw-node:has-text('Edited child note')");
        await page.Keyboard.DownAsync("Control");
        await page.Keyboard.DownAsync("Shift");
        await ClickCanvasNodeAsync(page, ".cw-node:has-text('Sibling note from Enter')");
        await page.Keyboard.UpAsync("Shift");
        await page.Keyboard.UpAsync("Control");
        await page.WaitForFunctionAsync("() => document.querySelectorAll('.cw-node.is-selected').length >= 2");

        var groupLabels = await OpenCanvasContextMenuAsync(page, ".cw-node:has-text('Sibling note from Enter')");
        Assert.Contains("Border", groupLabels);
        Assert.Contains("Progress", groupLabels);
        await page.Keyboard.PressAsync("Escape");

        var beforeDrag = await ReadSelectedNodePositionsAsync(page);
        Assert.True(beforeDrag.Length >= 2, "Expected a multi-selection before the drag assertion.");
        var draggedNode = page.Locator(".cw-node.is-selected:has-text('Sibling note from Enter')").First;
        var draggedBounds = await draggedNode.BoundingBoxAsync();
        Assert.NotNull(draggedBounds);
        await page.Keyboard.DownAsync("Control");
        await page.Mouse.MoveAsync(draggedBounds!.X + (draggedBounds.Width / 2), draggedBounds.Y + (draggedBounds.Height / 2));
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync(draggedBounds.X + (draggedBounds.Width / 2) + 84, draggedBounds.Y + (draggedBounds.Height / 2) + 56, new() { Steps = 10 });
        await page.Mouse.UpAsync();
        await page.Keyboard.UpAsync("Control");
        await page.WaitForFunctionAsync("() => document.querySelectorAll('.cw-node.is-selected').length >= 2");
        var afterDrag = await ReadNodePositionsAsync(page, beforeDrag.Select(node => node.Id).ToArray());
        foreach (var before in beforeDrag)
        {
            var after = afterDrag.Single(node => string.Equals(node.Id, before.Id, StringComparison.Ordinal));
            Assert.True(Math.Abs(after.Left - before.Left) >= 30, $"Expected node '{before.Id}' to move horizontally with the group drag.");
            Assert.True(Math.Abs(after.Top - before.Top) >= 20, $"Expected node '{before.Id}' to move vertically with the group drag.");
        }

        groupLabels = await OpenCanvasContextMenuAsync(page, ".cw-node:has-text('Sibling note from Enter')");
        Assert.Contains("Border", groupLabels);
        await page.Locator(".cw-context-menu__action[data-action-id='group-frame']").ClickAsync();
        await page.WaitForSelectorAsync(".cw-group-frame");
        Assert.True(await page.Locator(".cw-group-frame").CountAsync() > 0);
        var beforeFrameDrag = await ReadSelectedNodePositionsAsync(page);
        var frameLabel = page.Locator(".cw-group-frame__label").First;
        var frameLabelBounds = await frameLabel.BoundingBoxAsync();
        Assert.NotNull(frameLabelBounds);
        await page.Mouse.MoveAsync(frameLabelBounds!.X + (frameLabelBounds.Width / 2), frameLabelBounds.Y + (frameLabelBounds.Height / 2));
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync(frameLabelBounds.X + (frameLabelBounds.Width / 2) + 72, frameLabelBounds.Y + (frameLabelBounds.Height / 2) + 48, new() { Steps = 10 });
        await page.Mouse.UpAsync();
        var afterFrameDrag = await ReadNodePositionsAsync(page, beforeFrameDrag.Select(node => node.Id).ToArray());
        foreach (var before in beforeFrameDrag)
        {
            var after = afterFrameDrag.Single(node => string.Equals(node.Id, before.Id, StringComparison.Ordinal));
            Assert.True(Math.Abs(after.Left - before.Left) >= 24, $"Expected node '{before.Id}' to move horizontally when dragging the group border.");
            Assert.True(Math.Abs(after.Top - before.Top) >= 16, $"Expected node '{before.Id}' to move vertically when dragging the group border.");
        }

        groupLabels = await OpenCanvasContextMenuAsync(page, ".cw-node:has-text('Sibling note from Enter')");
        Assert.Contains("Progress", groupLabels);
        await page.Locator(".cw-context-menu__action[data-action-id='progress']").HoverAsync(new() { Force = true });
        await page.Locator(".cw-context-menu__action[data-action-id='progress:100']").ClickAsync(new() { Force = true });
        await page.WaitForFunctionAsync("() => document.querySelectorAll('.cw-node.is-selected .cw-node__progress.is-complete').length >= 2");

        await OpenCanvasCreateComposerAsync(page, ".cw-node[data-node-id^='project:']", "Link", "add-link");
        await page.WaitForSelectorAsync("text=Address");
        await page.Locator(".cw-canvas-composer__input").Nth(0).FillAsync("API reference");
        await page.Locator(".cw-canvas-composer__input").Nth(1).FillAsync("https://example.test/api");
        await page.Locator(".cw-canvas-composer__textarea").FillAsync("Reference for downstream build steps");
        await page.Locator(".cw-canvas-composer__actions").GetByRole(AriaRole.Button, new() { Name = "Link", Exact = true }).ClickAsync();
        await page.WaitForSelectorAsync("text=API reference");
        await page.WaitForFunctionAsync("() => Array.from(document.querySelectorAll('.cw-node.is-selected .cw-node__title')).some(node => node.textContent?.includes('API reference'))");

        await OpenCanvasCreateComposerAsync(page, ".cw-node[data-node-id^='project:']", "Image", "add-image-asset");
        await page.WaitForSelectorAsync("text=Drop an image here or choose one.");
        var pickerChooser = await page.RunAndWaitForFileChooserAsync(async () =>
            await page.Locator(".cw-canvas-composer__dropzone").ClickAsync());
        await pickerChooser.SetFilesAsync(
        [
            new FilePayload
            {
                Name = "playwright-picker-image.svg",
                MimeType = "image/svg+xml",
                Buffer = Encoding.UTF8.GetBytes("""
                    <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 120 90">
                      <rect width="120" height="90" rx="18" fill="#ec4899" />
                      <circle cx="60" cy="45" r="24" fill="#111827" />
                      <text x="60" y="52" text-anchor="middle" font-size="18" font-family="Arial" fill="#ffffff">QA</text>
                    </svg>
                    """)
            }
        ]);
        await page.WaitForFunctionAsync("() => document.querySelector('.cw-canvas-composer__upload-summary')?.textContent?.includes('playwright-picker-image.svg') === true");
        await page.Locator(".cw-canvas-composer__input").Nth(0).FillAsync("Picker uploaded image");
        await page.Locator(".cw-canvas-composer__input").Nth(1).FillAsync("Chooser flow media check");
        await page.Locator(".cw-canvas-composer__textarea").FillAsync("Created through the file chooser upload path");
        await page.Locator(".cw-canvas-composer__actions").GetByRole(AriaRole.Button, new() { Name = "Image", Exact = true }).ClickAsync();
        await page.WaitForFunctionAsync("() => document.querySelector('.cw-node.is-selected .cw-node__media-image') instanceof HTMLImageElement");
        await page.WaitForFunctionAsync("() => Array.from(document.querySelectorAll('.cw-node.is-selected .cw-node__title')).some(node => node.textContent?.includes('Picker uploaded image'))");
        await ClickCanvasNodeAsync(page, ".cw-node:has-text('Picker uploaded image')");
        await page.WaitForFunctionAsync("() => document.querySelector('.cw-inspector-shell .cw-media-preview')?.tagName === 'IMG'");
        await page.WaitForFunctionAsync("() => document.querySelector('.cw-inspector-shell')?.textContent?.includes('playwright-picker-image.svg') === true");

        await OpenCanvasCreateComposerAsync(page, ".cw-node[data-node-id^='project:']", "Image", "add-image-asset");
        await page.WaitForSelectorAsync("text=Drop an image here or choose one.");
        var imageUploadReady = await page.EvaluateAsync<bool>(
            @"async () => {
                const dropZone = document.querySelector('.cw-canvas-composer__dropzone');
                if (!dropZone) {
                    return false;
                }

                const base64 = 'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jmioAAAAASUVORK5CYII=';
                const binary = atob(base64);
                const bytes = Uint8Array.from(binary, c => c.charCodeAt(0));
                const file = new File([bytes], 'playwright-drop-image.png', { type: 'image/png' });
                const dataTransfer = new DataTransfer();
                dataTransfer.items.add(file);

                for (const eventName of ['dragenter', 'dragover']) {
                    dropZone.dispatchEvent(new DragEvent(eventName, { bubbles: true, cancelable: true, dataTransfer }));
                }

                dropZone.dispatchEvent(new DragEvent('drop', { bubbles: true, cancelable: true, dataTransfer }));
                await new Promise(resolve => setTimeout(resolve, 200));
                return document.querySelector('.cw-canvas-composer__upload-summary')?.textContent?.includes('playwright-drop-image.png') === true;
            }");
        Assert.True(imageUploadReady, "Expected drag/drop image upload to populate the create dialog.");

        await page.Locator(".cw-canvas-composer__input").Nth(0).FillAsync("Playwright dropped image");
        await page.Locator(".cw-canvas-composer__input").Nth(1).FillAsync("Regression media check");
        await page.Locator(".cw-canvas-composer__textarea").FillAsync("Created through the drag and drop upload path");
        await page.Locator(".cw-canvas-composer__actions").GetByRole(AriaRole.Button, new() { Name = "Image", Exact = true }).ClickAsync();
        await page.WaitForFunctionAsync("() => document.querySelector('.cw-media-preview')?.tagName === 'IMG'");
        await page.WaitForFunctionAsync("() => Array.from(document.querySelectorAll('.cw-node.is-selected .cw-node__title')).some(node => node.textContent?.includes('Playwright dropped image'))");
        await AssertNoCanvasNodeOverlapsAsync(page, "after mixed note/link/image creation");

        await ClickCanvasNodeAsync(page, ".cw-node.is-inline-text");
        await page.GetByRole(AriaRole.Button, new() { Name = "Focus root" }).ClickAsync();
        await page.WaitForFunctionAsync(
            @"() => {
                const host = document.querySelector('.cw-canvas-host');
                const selected = document.querySelector('.cw-node.is-selected');
                if (!host || !selected) {
                    return false;
                }

                const hostRect = host.getBoundingClientRect();
                const selectedRect = selected.getBoundingClientRect();
                const deltaX = Math.abs(Math.round((selectedRect.left + (selectedRect.width / 2)) - (hostRect.left + (hostRect.width / 2))));
                const deltaY = Math.abs(Math.round((selectedRect.top + (selectedRect.height / 2)) - (hostRect.top + (hostRect.height / 2))));
                return deltaX <= 2 && deltaY <= 2;
            }",
            null,
            new() { Timeout = 60_000 });
        var focusState = await ReadCanvasFocusStateAsync(page);
        Assert.Equal("Playwright Canvas Repair", await page.Locator(".cw-inspector-shell .cw-panel-card h3").First.TextContentAsync());
        Assert.InRange(Math.Abs(focusState.DeltaX), 0, 2);
        Assert.InRange(Math.Abs(focusState.DeltaY), 0, 2);

        if (await page.EvaluateAsync<bool>("() => document.querySelector('.cw-workbench-shell')?.classList.contains('is-maximized') === true"))
        {
            await page.GetByRole(AriaRole.Button, new() { Name = "Toggle maximize" }).ClickAsync();
            await page.WaitForFunctionAsync("() => document.querySelector('.cw-workbench-shell')?.classList.contains('is-maximized') !== true");
        }

        var docked = await ReadCanvasViewportStateAsync(page);
        Assert.False(docked.IsMaximized);
        Assert.False(docked.BodyLock);
        Assert.True(docked.HostWidth < docked.ViewportWidth, $"Expected docked host width to be smaller than viewport. Host={docked.HostWidth}, viewport={docked.ViewportWidth}.");

        await page.GetByRole(AriaRole.Button, new() { Name = "Toggle maximize" }).ClickAsync();
        await page.WaitForFunctionAsync("() => document.querySelector('.cw-workbench-shell')?.classList.contains('is-maximized') === true");
        var maximized = await ReadCanvasViewportStateAsync(page);
        Assert.True(maximized.IsMaximized);
        Assert.True(maximized.BodyLock);
        Assert.InRange(Math.Abs(maximized.HostLeft), 0, 1);
        Assert.InRange(Math.Abs(maximized.HostTop), 0, 1);
        Assert.InRange(Math.Abs(maximized.HostWidth - maximized.ViewportWidth), 0, 1);
        Assert.InRange(Math.Abs(maximized.HostHeight - maximized.ViewportHeight), 0, 1);
        await page.ScreenshotAsync(new() { Path = Path.Combine(artifactsDir, "structure-note-centered-pan.png"), FullPage = true });
    }

    private async Task<Guid> CreateProjectAsync(IPage page, string projectName, string phase)
    {
        await page.GotoAsync($"{fixture.BaseUrl}/projects");
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
        await page.Locator("input[name=\"editor.CurrentPhase\"]").FillAsync(phase);
        await Task.WhenAll(
            page.WaitForURLAsync("**/projects/*/structure"),
            page.GetByRole(AriaRole.Button, new() { Name = "Save and open structure" }).ClickAsync());

        var match = Regex.Match(page.Url, @"/projects/(?<projectId>[0-9a-fA-F-]+)/structure$", RegexOptions.IgnoreCase);
        Assert.True(match.Success, $"Could not parse project id from {page.Url}.");
        return Guid.Parse(match.Groups["projectId"].Value);
    }

    private static async Task ClickCanvasNodeAsync(IPage page, string selector, MouseButton button = MouseButton.Left, int clickCount = 1)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var node = page.Locator(selector).First;
            await node.WaitForAsync();

            try
            {
                await node.ScrollIntoViewIfNeededAsync();
                var bounds = await node.BoundingBoxAsync();
                if (bounds is null)
                {
                    await page.WaitForTimeoutAsync(120);
                    continue;
                }

                await page.Mouse.ClickAsync(
                    bounds.X + (bounds.Width / 2),
                    bounds.Y + (bounds.Height / 2),
                    new MouseClickOptions
                    {
                        Button = button,
                        ClickCount = clickCount
                    });
                return;
            }
            catch (PlaywrightException exception) when (exception.Message.Contains("attached", StringComparison.OrdinalIgnoreCase))
            {
                await page.WaitForTimeoutAsync(120);
            }
        }

        throw new InvalidOperationException($"Could not click canvas node matching selector '{selector}' after repeated rerenders.");
    }

    private static async Task OpenCanvasCreateComposerAsync(IPage page, string selector, string label, string? actionId = null)
    {
        var groupActionId = ResolveGroupedAction(actionId);
        var composerOpened = await page.EvaluateAsync<bool>(
            @"args => {
                const node = document.querySelector(args.selector);
                if (!node) {
                    return false;
                }

                const rect = node.getBoundingClientRect();
                const x = rect.left + (rect.width / 2);
                const y = rect.top + (rect.height / 2);
                node.dispatchEvent(new MouseEvent('contextmenu', {
                    bubbles: true,
                    cancelable: true,
                    button: 2,
                    buttons: 2,
                    clientX: x,
                    clientY: y
                }));

                const action = Array.from(document.querySelectorAll('.cw-context-menu__action'))
                    .find(button => button.dataset.actionId === args.actionId)
                    || Array.from(document.querySelectorAll('.cw-context-menu__action'))
                        .find(button => button.textContent?.includes(args.label));
                if (!action && args.groupActionId) {
                    const group = Array.from(document.querySelectorAll('.cw-context-menu__action'))
                        .find(button => button.dataset.actionId === args.groupActionId);
                    group?.dispatchEvent(new PointerEvent('pointerenter', { bubbles: true }));
                }

                const resolvedAction = Array.from(document.querySelectorAll('.cw-context-menu__action'))
                    .find(button => button.dataset.actionId === args.actionId)
                    || Array.from(document.querySelectorAll('.cw-context-menu__action'))
                        .find(button => button.textContent?.includes(args.label));
                resolvedAction?.click();
                return !!document.querySelector('.cw-canvas-composer__input');
            }",
            new { selector, label, actionId, groupActionId });

        Assert.True(composerOpened, $"Expected the radial menu action '{label}' to open the in-canvas composer.");
    }

    private static async Task<string[]> OpenCanvasContextMenuAsync(IPage page, string selector)
    {
        if (selector.Contains(":has-text(", StringComparison.Ordinal))
        {
            await ClickCanvasNodeAsync(page, selector, MouseButton.Right);
        }
        else
        {
            await page.EvaluateAsync(
                @"selector => {
                    const node = document.querySelector(selector);
                    if (!node) {
                        return;
                    }

                    const rect = node.getBoundingClientRect();
                    const x = rect.left + (rect.width / 2);
                    const y = rect.top + (rect.height / 2);
                    node.dispatchEvent(new MouseEvent('contextmenu', {
                        bubbles: true,
                        cancelable: true,
                        button: 2,
                        buttons: 2,
                        clientX: x,
                        clientY: y
                    }));
                }",
                selector);
        }

        await page.Locator(".cw-context-menu__action").First.WaitForAsync();
        return (await page.Locator(".cw-context-menu__label").AllTextContentsAsync())
            .Select(label => label.Trim())
            .Where(label => !string.IsNullOrWhiteSpace(label))
            .ToArray();
    }

    private static Task<CanvasFocusState> ReadCanvasFocusStateAsync(IPage page)
        => page.EvaluateAsync<CanvasFocusState>(
            @"() => {
                const host = document.querySelector('.cw-canvas-host');
                const selected = document.querySelector('.cw-node.is-selected');
                if (!host || !selected) {
                    return { selectedId: null, deltaX: 9999, deltaY: 9999 };
                }

                const hostRect = host.getBoundingClientRect();
                const selectedRect = selected.getBoundingClientRect();
                return {
                    selectedId: selected.getAttribute('data-node-id'),
                    deltaX: Math.round((selectedRect.left + (selectedRect.width / 2)) - (hostRect.left + (hostRect.width / 2))),
                    deltaY: Math.round((selectedRect.top + (selectedRect.height / 2)) - (hostRect.top + (hostRect.height / 2)))
                };
            }");

    private static Task<CanvasViewportState> ReadCanvasViewportStateAsync(IPage page)
        => page.EvaluateAsync<CanvasViewportState>(
            @"() => {
                const shell = document.querySelector('.cw-workbench-shell');
                const host = document.querySelector('.cw-canvas-host');
                return {
                    isMaximized: shell?.classList.contains('is-maximized') === true,
                    bodyLock: document.body.classList.contains('cw-body-lock'),
                    hostLeft: host?.getBoundingClientRect().left ?? 0,
                    hostTop: host?.getBoundingClientRect().top ?? 0,
                    hostWidth: host?.getBoundingClientRect().width ?? 0,
                    hostHeight: host?.getBoundingClientRect().height ?? 0,
                    viewportWidth: window.innerWidth,
                    viewportHeight: window.innerHeight
                };
            }");

    private static Task<CanvasNodePosition[]> ReadSelectedNodePositionsAsync(IPage page)
        => page.EvaluateAsync<CanvasNodePosition[]>(
            @"() => Array.from(document.querySelectorAll('.cw-node.is-selected')).map(node => {
                const rect = node.getBoundingClientRect();
                return {
                    id: node.getAttribute('data-node-id'),
                    left: Math.round(rect.left),
                    top: Math.round(rect.top)
                };
            })");

    private static Task<CanvasNodePosition[]> ReadNodePositionsAsync(IPage page, string[] nodeIds)
        => page.EvaluateAsync<CanvasNodePosition[]>(
            @"ids => ids.map(id => {
                const node = document.querySelector(`.cw-node[data-node-id=""${id}""]`);
                if (!node) {
                    return { id, left: -9999, top: -9999 };
                }

                const rect = node.getBoundingClientRect();
                return {
                    id,
                    left: Math.round(rect.left),
                    top: Math.round(rect.top)
                };
            })",
            nodeIds);

    private static Task<CanvasContextMenuBounds> ReadContextMenuBoundsAsync(IPage page)
        => page.EvaluateAsync<CanvasContextMenuBounds>(
            @"() => {
                const actions = Array.from(document.querySelectorAll('.cw-context-menu__action'));
                const viewportWidth = window.innerWidth;
                if (actions.length === 0) {
                    return { minLeft: 0, maxRight: 0, viewportWidth };
                }

                const bounds = actions.map(action => action.getBoundingClientRect());
                return {
                    minLeft: Math.round(Math.min(...bounds.map(rect => rect.left))),
                    maxRight: Math.round(Math.max(...bounds.map(rect => rect.right))),
                    viewportWidth
                };
            }");

    private static Task<CanvasMenuActionMetrics> ReadContextMenuActionMetricsAsync(IPage page, string actionId)
        => page.EvaluateAsync<CanvasMenuActionMetrics>(
            @"requestedActionId => {
                const action = document.querySelector(`.cw-context-menu__action[data-action-id=""${requestedActionId}""]`);
                if (!action) {
                    return { width: 0, height: 0 };
                }

                const rect = action.getBoundingClientRect();
                return {
                    width: Math.round(rect.width),
                    height: Math.round(rect.height)
                };
            }",
            actionId);

    private static async Task AssertNoCanvasNodeOverlapsAsync(IPage page, string phase)
    {
        await page.WaitForFunctionAsync(
            @"tolerance => {
                const nodes = Array.from(document.querySelectorAll('.cw-node'));
                for (let index = 0; index < nodes.length; index++) {
                    const first = nodes[index].getBoundingClientRect();
                    for (let compareIndex = index + 1; compareIndex < nodes.length; compareIndex++) {
                        const second = nodes[compareIndex].getBoundingClientRect();
                        const left = Math.max(first.left + tolerance, second.left + tolerance);
                        const right = Math.min(first.right - tolerance, second.right - tolerance);
                        const top = Math.max(first.top + tolerance, second.top + tolerance);
                        const bottom = Math.min(first.bottom - tolerance, second.bottom - tolerance);
                        if (right > left && bottom > top) {
                            return false;
                        }
                    }
                }

                return true;
            }",
            6,
            new() { Timeout = 10_000 });

        var overlaps = await page.EvaluateAsync<CanvasNodeOverlap[]>(
            @"tolerance => {
                const nodes = Array.from(document.querySelectorAll('.cw-node')).map(node => {
                    const rect = node.getBoundingClientRect();
                    const title = node.querySelector('.cw-node__title, .cw-note-node__text')?.textContent?.trim() || node.getAttribute('data-node-id') || 'node';
                    return {
                        id: node.getAttribute('data-node-id') || '',
                        title,
                        left: rect.left,
                        right: rect.right,
                        top: rect.top,
                        bottom: rect.bottom
                    };
                });

                const overlaps = [];
                for (let index = 0; index < nodes.length; index++) {
                    const first = nodes[index];
                    for (let compareIndex = index + 1; compareIndex < nodes.length; compareIndex++) {
                        const second = nodes[compareIndex];
                        const left = Math.max(first.left + tolerance, second.left + tolerance);
                        const right = Math.min(first.right - tolerance, second.right - tolerance);
                        const top = Math.max(first.top + tolerance, second.top + tolerance);
                        const bottom = Math.min(first.bottom - tolerance, second.bottom - tolerance);
                        if (right > left && bottom > top) {
                            overlaps.push({
                                firstTitle: first.title,
                                secondTitle: second.title
                            });
                        }
                    }
                }

                return overlaps;
            }",
            6);

        Assert.True(
            overlaps.Length == 0,
            $"{phase} still has overlapping nodes: {string.Join(", ", overlaps.Select(overlap => $"{overlap.FirstTitle} <-> {overlap.SecondTitle}"))}");
    }

    private sealed class CanvasFocusState
    {
        public string? SelectedId { get; set; }

        public int DeltaX { get; set; }

        public int DeltaY { get; set; }
    }

    private sealed class CanvasViewportState
    {
        public bool IsMaximized { get; set; }

        public bool BodyLock { get; set; }

        public double HostLeft { get; set; }

        public double HostTop { get; set; }

        public double HostWidth { get; set; }

        public double HostHeight { get; set; }

        public double ViewportWidth { get; set; }

        public double ViewportHeight { get; set; }
    }

    private sealed class CanvasNodePosition
    {
        public string Id { get; set; } = string.Empty;

        public int Left { get; set; }

        public int Top { get; set; }
    }

    private sealed class CanvasContextMenuBounds
    {
        public int MinLeft { get; set; }

        public int MaxRight { get; set; }

        public int ViewportWidth { get; set; }
    }

    private sealed class CanvasMenuActionMetrics
    {
        public int Width { get; set; }

        public int Height { get; set; }
    }

    private sealed class CanvasNodeOverlap
    {
        public string FirstTitle { get; set; } = string.Empty;

        public string SecondTitle { get; set; } = string.Empty;
    }

    private sealed class CanvasSubmenuMetrics
    {
        public double ActionRight { get; set; }

        public double ActionMidY { get; set; }

        public double OrbitX { get; set; }

        public double OrbitY { get; set; }
    }

    private static string? ResolveGroupedAction(string? actionId) => actionId switch
    {
        null or "" => null,
        var value when value.StartsWith("add-block-", StringComparison.Ordinal) => "group-blocks",
        "add-prompt-flow" or "add-prompt-session" or "add-prompt-step" => "group-prompts",
        "add-repository" or "add-file" or "add-image-asset" or "add-video-asset" or "add-link" or "add-connector" or "add-secret-reference" => "group-assets",
        "add-validation-run" or "add-test-plan" or "add-test-evidence" => "group-assurance",
        _ => null
    };
}
