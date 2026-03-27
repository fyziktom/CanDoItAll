using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Security;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workspace;
using Microsoft.Playwright;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Tests.Playwright;

[Collection(PlaywrightCollection.Name)]
public sealed class AppSmokeTests(PlaywrightAppFixture fixture)
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
        await AssertSharedChromeVisibleAsync(page);
        await OpenQuickCreateMenuAsync(page);
        var quickCreateActionIds = await ReadQuickCreateActionIdsAsync(page);
        Assert.Contains("group-assets", quickCreateActionIds);
        Assert.Contains("group-blocks", quickCreateActionIds);
        await page.Keyboard.PressAsync("Escape");
        await OpenCanvasHelpAsync(page);
        await CloseCanvasHelpAsync(page);
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
        await page.WaitForSelectorAsync("text=Prompt Factory tabs");
        await page.WaitForTimeoutAsync(1000);
        await page.Locator(".cw-workbench-shell").WaitForAsync();
        await AssertSharedChromeVisibleAsync(page);
        await OpenCanvasHelpAsync(page);
        await CloseCanvasHelpAsync(page);
        await page.Locator(".pf-page-tab").Filter(new() { HasText = "Assembly" }).First.ClickAsync();
        await page.WaitForSelectorAsync("text=Assembly workspace");
        await page.GetByTestId("prompt-factory-build").First.WaitForAsync();
        await page.GetByTestId("prompt-factory-build").First.ClickAsync();
        await page.Locator(".pf-page-tab").Filter(new() { HasText = "Review" }).First.ClickAsync();
        await page.WaitForSelectorAsync("text=Prompt review");
        await page.WaitForSelectorAsync("text=Select a project before building a prompt.");
        Assert.False(await page.Locator("#blazor-error-ui").IsVisibleAsync());
    }

    [Fact]
    public async Task Structure_typed_file_create_dialog_accepts_uploaded_files()
    {
        await using var context = await fixture.Browser.NewContextAsync();
        var page = await context.NewPageAsync();

        await CreateProjectAsync(page, "Playwright Typed File Upload", "Review");

        var canvasHost = page.Locator(".cw-canvas-host");
        await canvasHost.ScrollIntoViewIfNeededAsync();
        await OpenCanvasCreateComposerAsync(page, ".cw-node[data-node-id^='project:']", "PDF", "add-file-pdf");
        await page.Locator(".cw-canvas-composer__dropzone").WaitForAsync();
        await page.Locator(".cw-canvas-composer__file-input").SetInputFilesAsync(
        [
            new FilePayload
            {
                Name = "playwright-structure-file.pdf",
                MimeType = "application/pdf",
                Buffer = Encoding.UTF8.GetBytes("%PDF-1.4\n1 0 obj\n<< /Type /Catalog >>\nendobj\ntrailer\n<< /Root 1 0 R >>\n%%EOF")
            }
        ]);

        await page.WaitForFunctionAsync("() => document.querySelector('.cw-canvas-composer__upload-summary')?.textContent?.includes('playwright-structure-file.pdf') === true");
        await page.Locator(".cw-canvas-composer__input").Nth(0).FillAsync("Architecture validation PDF");
        await page.Locator(".cw-canvas-composer__input").Nth(1).FillAsync("docs/validation");
        await page.Locator(".cw-canvas-composer__textarea").FillAsync("Smoke test evidence for the typed PDF upload flow.");
        await page.WaitForFunctionAsync(
            @"() => {
                const button = document.querySelector('.cw-canvas-composer__actions .cw-button[data-tone=""accent""]');
                return button instanceof HTMLButtonElement && button.disabled !== true;
            }");
        await page.Locator(".cw-canvas-composer__actions .cw-button[data-tone='accent']").ClickAsync();
        await page.WaitForFunctionAsync("() => Array.from(document.querySelectorAll('.cw-node.is-selected .cw-node__title')).some(node => node.textContent?.includes('Architecture validation PDF'))");
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

        await OpenCanvasContextMenuAsync(page, ".cw-node[data-node-id^='project:']");
        var radialActionIds = await ReadContextMenuActionIdsAsync(page);
        Assert.Contains("open", radialActionIds);
        Assert.Contains("connect", radialActionIds);
        Assert.Contains("progress", radialActionIds);
        Assert.Contains("marker", radialActionIds);
        Assert.Contains("priority", radialActionIds);
        await page.ScreenshotAsync(new() { Path = Path.Combine(artifactsDir, "structure-root-menu-metadata.png"), FullPage = true });
        await page.Keyboard.PressAsync("Escape");

        await OpenQuickCreateMenuAsync(page);
        var defaultQuickCreateActionMetrics = await ReadContextMenuActionMetricsAsync(page, "group-assets");
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
        await CloseCanvasSettingsAsync(page);

        await OpenQuickCreateMenuAsync(page);
        var scaledQuickCreateActionMetrics = await ReadContextMenuActionMetricsAsync(page, "group-assets");
        Assert.True(scaledQuickCreateActionMetrics.Width >= defaultQuickCreateActionMetrics.Width + 6, $"Expected the menu scale setting to enlarge root actions. Before={defaultQuickCreateActionMetrics.Width}, after={scaledQuickCreateActionMetrics.Width}.");
        await OpenContextSubmenuAsync(page, "group-assets");
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

        await OpenQuickCreateMenuAsync(page);
        var rootMenuBounds = await ReadContextMenuBoundsAsync(page);
        Assert.True(rootMenuBounds.MinLeft >= 0, $"Expected the radial menu to stay inside the left viewport edge, but minLeft was {rootMenuBounds.MinLeft}.");
        Assert.True(rootMenuBounds.MaxRight <= rootMenuBounds.ViewportWidth, $"Expected the radial menu to stay inside the right viewport edge, but maxRight was {rootMenuBounds.MaxRight} of {rootMenuBounds.ViewportWidth}.");
        await OpenContextSubmenuAsync(page, "group-blocks");
        await page.Locator(".cw-context-menu__action[data-action-id='add-block-feature']").WaitForAsync();
        await page.Locator(".cw-context-menu__action[data-action-id='add-block-support']").WaitForAsync();
        await page.Keyboard.PressAsync("Escape");

        await EnsureCanvasSelectionAsync(page, ".cw-node[data-node-id^='project:']");
        var noteEditor = await OpenInlineNoteEditorAsync(page);
        await noteEditor.FillAsync("Child note from keyboard");
        await noteEditor.PressAsync("Enter");
        await page.WaitForFunctionAsync("() => !document.querySelector('.cw-note-editor__input')");
        await page.WaitForSelectorAsync("text=Child note from keyboard");
        await page.WaitForFunctionAsync(
            @"() => {
                return Array.from(document.querySelectorAll('.cw-node.is-inline-text.is-selected'))
                    .some(candidate => (candidate.textContent || '').includes('Child note from keyboard'));
            }");

        await page.WaitForFunctionAsync("() => document.querySelectorAll('.cw-node').length > 1");
        await ToggleCanvasNodeCollapseAsync(page, ".cw-node[data-node-id^='project:'] .cw-node__collapse");
        await page.WaitForFunctionAsync("() => !document.querySelector('.cw-node:not([data-node-id^=\"project:\"])')");
        await ToggleCanvasNodeCollapseAsync(page, ".cw-node[data-node-id^='project:'] .cw-node__collapse");
        await page.WaitForFunctionAsync("() => !!document.querySelector('.cw-node:not([data-node-id^=\"project:\"])')");

        await EnsureCanvasSelectionAsync(page, ".cw-node[data-node-id^='project:']");
        noteEditor = await OpenInlineNoteEditorAsync(page);
        await noteEditor.FillAsync("Second child note");
        await noteEditor.PressAsync("Enter");
        await page.WaitForFunctionAsync("() => !document.querySelector('.cw-note-editor__input')");
        await page.WaitForSelectorAsync("text=Second child note");
        await AssertNoCanvasNodeOverlapsAsync(page, "after chained child-note creation");

        noteEditor = await OpenExistingInlineNoteEditorAsync(page, ".cw-node:has-text('Second child note')");
        await noteEditor.FillAsync("Edited child note");
        await noteEditor.PressAsync("Enter");
        await page.WaitForFunctionAsync("() => !document.querySelector('.cw-note-editor__input')");
        await page.WaitForSelectorAsync("text=Edited child note");

        var nodeLabels = await OpenCanvasContextMenuAsync(page, ".cw-node:has-text('Edited child note')");
        Assert.Contains(nodeLabels, label => label.Contains("Progress", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(nodeLabels, label => label.Contains("Marker", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(nodeLabels, label => label.Contains("Priority", StringComparison.OrdinalIgnoreCase));
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
        await page.WaitForFunctionAsync("() => Array.from(document.querySelectorAll('.cw-node__progress')).some(node => node.getAttribute('title')?.includes('30%') === true)");

        var progressBadge = page.Locator(".cw-node__progress[title*='30%']").First;
        await progressBadge.WaitForAsync();
        await progressBadge.EvaluateAsync(
            @"badge => {
                if (!(badge instanceof HTMLElement)) {
                    return;
                }

                badge.dispatchEvent(new MouseEvent('dblclick', {
                    bubbles: true,
                    cancelable: true,
                    button: 0,
                    buttons: 1,
                    detail: 2,
                    view: window
                }));
            }");
        await page.Locator(".cw-context-menu__action[data-action-id='progress:100']").WaitForAsync();
        await page.Keyboard.PressAsync("Escape");

        nodeLabels = await OpenCanvasContextMenuAsync(page, ".cw-node:has-text('Edited child note')");
        Assert.Contains(nodeLabels, label => label.Contains("Marker", StringComparison.OrdinalIgnoreCase));
        await page.Locator(".cw-context-menu__action[data-action-id='marker']").WaitForAsync();
        await page.EvaluateAsync(
            @"() => document.querySelector('.cw-context-menu__action[data-action-id=""marker""]')
                ?.dispatchEvent(new PointerEvent('pointerenter', { bubbles: true }))");
        await page.Locator(".cw-context-menu__action[data-action-id='marker:question']").WaitForAsync();
        var markerMetrics = await ReadContextMenuActionMetricsAsync(page, "marker:question");
        Assert.True(markerMetrics.Width >= progressMetrics.Width - 2, $"Expected marker presets to stay comparable to progress preset size. Marker={markerMetrics.Width}, progress={progressMetrics.Width}.");
        await page.Locator(".cw-context-menu__action[data-action-id='marker:money']").ClickAsync(new() { Force = true });
        await page.WaitForFunctionAsync("() => Array.from(document.querySelectorAll('.cw-node__marker')).some(node => node.textContent?.includes('$') === true)");

        nodeLabels = await OpenCanvasContextMenuAsync(page, ".cw-node:has-text('Edited child note')");
        Assert.Contains(nodeLabels, label => label.Contains("Priority", StringComparison.OrdinalIgnoreCase));
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
        await page.WaitForFunctionAsync("() => Array.from(document.querySelectorAll('.cw-node__priority')).some(node => node.textContent?.trim() === '2')");
        await page.ScreenshotAsync(new() { Path = Path.Combine(artifactsDir, "structure-note-badges-selected.png"), FullPage = true });

        await page.Keyboard.PressAsync("Enter");
        await noteEditor.WaitForAsync();
        await noteEditor.FillAsync("Sibling note from Enter");
        await noteEditor.PressAsync("Enter");
        await page.WaitForFunctionAsync("() => !document.querySelector('.cw-note-editor__input')");
        await page.WaitForSelectorAsync("text=Sibling note from Enter");
        await AssertNoCanvasNodeOverlapsAsync(page, "after sibling-note creation");

        await OpenCanvasCreateComposerAsync(page, ".cw-node[data-node-id^='project:']", "Link", "add-link");
        await page.WaitForSelectorAsync("text=Address");
        await page.Locator(".cw-canvas-composer__input").Nth(0).FillAsync("API reference");
        await page.Locator(".cw-canvas-composer__input").Nth(1).FillAsync("https://example.test/api");
        await page.Locator(".cw-canvas-composer__textarea").FillAsync("Reference for downstream build steps");
        var createLinkButton = page.Locator(".cw-canvas-composer__actions .cw-button[data-tone='accent']");
        await createLinkButton.ClickAsync();
        if (!await WaitForFunctionAsync(
                page,
                @"() => Array.from(document.querySelectorAll('.cw-node .cw-node__title'))
                    .some(node => (node.textContent || '').includes('API reference'))",
                3_000))
        {
            await createLinkButton.EvaluateAsync(
                @"node => {
                    if (node instanceof HTMLButtonElement) {
                        node.click();
                    }
                }");
        }

        await page.WaitForSelectorAsync("text=API reference");
        await page.WaitForFunctionAsync("() => Array.from(document.querySelectorAll('.cw-node.is-selected .cw-node__title')).some(node => node.textContent?.includes('API reference'))");

        await OpenCanvasCreateComposerViaRuntimeAsync(page, ".cw-node[data-node-id^='project:']", "Image", "add-image-asset");
        await page.Locator(".cw-canvas-composer__dropzone").WaitForAsync();
        await page.Locator(".cw-canvas-composer__file-input").SetInputFilesAsync(
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
        await page.Locator(".cw-canvas-composer__textarea").FillAsync("Created through the file input upload path");
        await page.Locator(".cw-canvas-composer__actions .cw-button[data-tone='accent']").ClickAsync();
        await page.WaitForFunctionAsync("() => document.querySelector('.cw-node.is-selected .cw-node__media-image') instanceof HTMLImageElement");
        await page.WaitForFunctionAsync("() => Array.from(document.querySelectorAll('.cw-node.is-selected .cw-node__title')).some(node => node.textContent?.includes('Picker uploaded image'))");
        await EnsureCanvasSelectionAsync(page, ".cw-node:has-text('Picker uploaded image')");
        await page.WaitForFunctionAsync("() => document.querySelector('.cw-floating-window[data-testid=\"project-structure-selection-window\"] .cw-media-preview')?.tagName === 'IMG'");
        await page.WaitForFunctionAsync("() => document.querySelector('.cw-floating-window[data-testid=\"project-structure-selection-window\"]')?.textContent?.includes('playwright-picker-image.svg') === true");

        await OpenCanvasCreateComposerViaRuntimeAsync(page, ".cw-node[data-node-id^='project:']", "Image", "add-image-asset");
        await page.Locator(".cw-canvas-composer__file-trigger").WaitForAsync();
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
        await page.Locator(".cw-canvas-composer__actions .cw-button[data-tone='accent']").ClickAsync();
        await page.WaitForFunctionAsync("() => document.querySelector('.cw-media-preview')?.tagName === 'IMG'");
        await page.WaitForFunctionAsync("() => Array.from(document.querySelectorAll('.cw-node.is-selected .cw-node__title')).some(node => node.textContent?.includes('Playwright dropped image'))");
        await AssertNoCanvasNodeOverlapsAsync(page, "after mixed note/link/image creation");

        await EnsureCanvasSelectionAsync(page, ".cw-node.is-inline-text");
        await FocusCanvasRootAsync(page);
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
        await page.WaitForFunctionAsync(
            @"expectedTitle => {
                const title = document.querySelector('.cw-floating-window[data-testid=""project-structure-selection-window""] .cw-panel-card h3');
                return title?.textContent?.trim() === expectedTitle;
            }",
            "Playwright Canvas Repair");
        var focusState = await ReadCanvasFocusStateAsync(page);
        Assert.Equal("Playwright Canvas Repair", await page.Locator(".cw-floating-window[data-testid='project-structure-selection-window'] .cw-panel-card h3").First.TextContentAsync());
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

    [Fact]
    public async Task Project_structure_feedback_fixes_are_validated_in_browser()
    {
        var repoRoot = GetRepoRoot();
        var artifactsDir = Path.Combine(repoRoot, "output", "playwright", "feedback5");
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
        var projectId = await CreateProjectAsync(page, "Playwright Feedback Validation", "Validation");
        var projectRootId = await ReadNodeIdAsync(page, ".cw-node[data-node-id^='project:']");

        var pdfId = await InvokeStructureCreateActionAsync(
            page,
            "add-file-pdf",
            projectRootId,
            projectRootId,
            "Architecture evidence PDF",
            "docs/architecture",
            "Typed PDF validation node.",
            uploadedFile: BuildUploadedFile(
                "architecture-evidence.pdf",
                "application/pdf",
                "%PDF-1.4\n1 0 obj\n<< /Type /Catalog >>\nendobj\ntrailer\n<< /Root 1 0 R >>\n%%EOF"));

        var excelId = await InvokeStructureCreateActionAsync(
            page,
            "add-file-excel",
            projectRootId,
            projectRootId,
            "Validation workbook",
            "reports",
            "Typed spreadsheet validation node.",
            uploadedFile: BuildUploadedFile(
                "validation-workbook.csv",
                "text/csv",
                "name,status\nexports,ready\nsummary,ready"));

        var docxId = await InvokeStructureCreateActionAsync(
            page,
            "add-file-docx",
            projectRootId,
            projectRootId,
            "Project brief docx",
            "docs/briefs",
            "Typed docx validation node.",
            uploadedFile: BuildUploadedFile(
                "project-brief.docx",
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                "Fake docx payload for UI validation."));

        await FocusCanvasRootAsync(page);
        await SetCanvasZoomPercentAsync(page, 100);

        var selectionWindow = page.GetByTestId("project-structure-selection-window");
        await selectionWindow.WaitForAsync();
        var actionColors = await page.EvaluateAsync<string[]>(
            @"() => Array.from(document.querySelectorAll('[data-testid=""project-structure-selection-window""] .cw-floating-window__action'))
                .map(action => getComputedStyle(action).color)");
        Assert.NotEmpty(actionColors);
        Assert.All(actionColors, color => Assert.Equal("rgb(0, 0, 0)", color));
        await selectionWindow.ScreenshotAsync(new() { Path = Path.Combine(artifactsDir, "01-window-icon-actions.png") });

        await EnsureFloatingWindowExpandedAsync(page, "project-structure-toolbox-window");
        var toolboxWindow = page.GetByTestId("project-structure-toolbox-window");
        Assert.Equal(1, await toolboxWindow.Locator(".cw-floating-window__header").CountAsync());
        Assert.True(await toolboxWindow.GetByRole(AriaRole.Button, new() { Name = "Minimize window" }).IsVisibleAsync());
        Assert.True(await toolboxWindow.GetByRole(AriaRole.Button, new() { Name = "Hide window" }).IsVisibleAsync());
        Assert.True(await toolboxWindow.Locator(".cw-floating-window__drag").IsVisibleAsync());

        var toolboxBoundsBeforeDrag = await toolboxWindow.BoundingBoxAsync();
        Assert.NotNull(toolboxBoundsBeforeDrag);
        await DragFloatingWindowAsync(
            page,
            "project-structure-toolbox-window",
            220,
            140);
        var toolboxBoundsAfterDrag = await toolboxWindow.BoundingBoxAsync();
        Assert.NotNull(toolboxBoundsAfterDrag);
        Assert.True(
            Math.Abs(toolboxBoundsAfterDrag!.X - toolboxBoundsBeforeDrag.X) > 40 ||
            Math.Abs(toolboxBoundsAfterDrag.Y - toolboxBoundsBeforeDrag.Y) > 40,
            $"Expected toolbox window drag to move the shared window, but before=({toolboxBoundsBeforeDrag.X},{toolboxBoundsBeforeDrag.Y}) after=({toolboxBoundsAfterDrag.X},{toolboxBoundsAfterDrag.Y}).");
        await toolboxWindow.ScreenshotAsync(new() { Path = Path.Combine(artifactsDir, "02-toolbox-window-chrome.png") });

        var structureToolboxSearch = page.Locator("[data-testid='project-structure-standard-blocks-toolbox'] .project-structure-toolbox__search");
        var toolboxSections = page.Locator("[data-testid='project-structure-standard-blocks-toolbox'] .project-structure-toolbox__sections");
        await structureToolboxSearch.WaitForAsync();
        await structureToolboxSearch.FillAsync("a");
        await page.WaitForFunctionAsync(
            @"() => {
                const toolbox = document.querySelector('[data-testid=""project-structure-standard-blocks-toolbox""]');
                if (!toolbox) {
                    return false;
                }

                return toolbox.querySelectorAll('.cw-context-toolbox__item').length > 0 &&
                    toolbox.querySelectorAll('.rz-fa-icon').length > 0 &&
                    toolbox.querySelectorAll('.rz-icon-fallback').length === 0;
            }");
        await page.WaitForTimeoutAsync(250);

        var firstVisibleItemTopBeforeScroll = await page.EvaluateAsync<double>(
            @"() => {
                const item = document.querySelector('[data-testid=""project-structure-standard-blocks-toolbox""] .cw-context-toolbox__item');
                return item instanceof HTMLElement
                    ? item.getBoundingClientRect().top
                    : 0;
            }");
        await toolboxSections.HoverAsync();
        await page.Mouse.WheelAsync(0, 1400);
        await page.WaitForTimeoutAsync(180);

        var toolboxScrollState = await page.EvaluateAsync<ToolboxScrollState>(
            @"() => {
                const sections = document.querySelector('[data-testid=""project-structure-standard-blocks-toolbox""] .project-structure-toolbox__sections');
                const window = document.querySelector('[data-testid=""project-structure-toolbox-window""]');
                const body = window?.querySelector('.cw-floating-window__body');
                const toolbox = document.querySelector('[data-testid=""project-structure-standard-blocks-toolbox""]');
                if (!(sections instanceof HTMLElement)) {
                    return {
                        scrollTop: 0,
                        scrollHeight: 0,
                        clientHeight: 0,
                        bodyScrollTop: 0,
                        bodyScrollHeight: 0,
                        bodyClientHeight: 0,
                        openGroupCount: 0,
                        itemCount: 0,
                        visibleItemCount: 0,
                        windowHeight: 0
                    };
                }

                const items = Array.from(sections.querySelectorAll('.cw-context-toolbox__item'));
                return {
                    scrollTop: sections.scrollTop,
                    scrollHeight: sections.scrollHeight,
                    clientHeight: sections.clientHeight,
                    bodyScrollTop: body instanceof HTMLElement ? body.scrollTop : 0,
                    bodyScrollHeight: body instanceof HTMLElement ? body.scrollHeight : 0,
                    bodyClientHeight: body instanceof HTMLElement ? body.clientHeight : 0,
                    openGroupCount: toolbox ? toolbox.querySelectorAll('[data-testid^=""project-structure-toolbox-group-body-""]').length : 0,
                    itemCount: items.length,
                    visibleItemCount: items.filter(item => item instanceof HTMLElement && item.getBoundingClientRect().height > 1 && item.getBoundingClientRect().width > 1).length,
                    visibleLabels: items
                        .filter(item => item instanceof HTMLElement && item.getBoundingClientRect().height > 1 && item.getBoundingClientRect().width > 1)
                        .slice(0, 6)
                        .map(item => item.querySelector('.cw-context-toolbox__item-body strong')?.textContent?.trim() ?? ''),
                    firstItemTop: items.length > 0 && items[0] instanceof HTMLElement ? items[0].getBoundingClientRect().top : 0,
                    windowHeight: window instanceof HTMLElement ? window.getBoundingClientRect().height : 0
                };
            }");
        await toolboxWindow.ScreenshotAsync(new() { Path = Path.Combine(artifactsDir, "03-toolbox-search-scroll.png") });
        Assert.True(
            toolboxScrollState.VisibleItemCount >= 20,
            $"Expected a large visible toolbox result set for scroll validation, but openGroupCount={toolboxScrollState.OpenGroupCount}; itemCount={toolboxScrollState.ItemCount}; visibleItemCount={toolboxScrollState.VisibleItemCount}; windowHeight={toolboxScrollState.WindowHeight}.");
        Assert.True(
            toolboxScrollState.ScrollTop > 0 ||
            toolboxScrollState.BodyScrollTop > 0 ||
            toolboxScrollState.FirstItemTop < firstVisibleItemTopBeforeScroll - 4,
            $"Expected toolbox search results to move after wheel input, but firstItemTopBefore={firstVisibleItemTopBeforeScroll}; firstItemTopAfter={toolboxScrollState.FirstItemTop}; sections: scrollTop={toolboxScrollState.ScrollTop} scrollHeight={toolboxScrollState.ScrollHeight} clientHeight={toolboxScrollState.ClientHeight}; body: scrollTop={toolboxScrollState.BodyScrollTop} scrollHeight={toolboxScrollState.BodyScrollHeight} clientHeight={toolboxScrollState.BodyClientHeight}; openGroupCount={toolboxScrollState.OpenGroupCount}; itemCount={toolboxScrollState.ItemCount}; visibleItemCount={toolboxScrollState.VisibleItemCount}; windowHeight={toolboxScrollState.WindowHeight}.");
        Assert.Contains(toolboxScrollState.VisibleLabels, label => !string.IsNullOrWhiteSpace(label));
        await structureToolboxSearch.FillAsync("pdf");
        await page.WaitForFunctionAsync(
            @"() => {
                const toolbox = document.querySelector('[data-testid=""project-structure-standard-blocks-toolbox""]');
                if (!toolbox) {
                    return false;
                }

                return toolbox.querySelectorAll('.cw-context-toolbox__item').length > 0 &&
                    (toolbox.textContent || '').toLowerCase().includes('pdf') &&
                    toolbox.querySelectorAll('.fa-file-pdf').length > 0;
            }");
        await toolboxWindow.ScreenshotAsync(new() { Path = Path.Combine(artifactsDir, "04-toolbox-pdf-search.png") });
        await structureToolboxSearch.FillAsync(string.Empty);

        Assert.False(await page.Locator("#blazor-error-ui").IsVisibleAsync());
    }

    [Fact]
    public async Task Project_structure_artifacts_capture_required_canvas_evidence()
    {
        var repoRoot = GetRepoRoot();
        var screenshotsRoot = Path.Combine(repoRoot, "artifacts", "screenshots");
        var i04Root = Path.Combine(screenshotsRoot, "i04");
        var i08Root = Path.Combine(screenshotsRoot, "i08");
        var i17Root = Path.Combine(screenshotsRoot, "i17");
        var i19Root = Path.Combine(screenshotsRoot, "i19");
        var i23Root = Path.Combine(screenshotsRoot, "i23");

        ResetDirectory(i04Root);
        ResetDirectory(i08Root);
        ResetDirectory(i17Root);
        ResetDirectory(i19Root);
        ResetDirectory(i23Root);

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
        await SeedProviderProfilesAsync(page);

        var projectId = await CreateProjectAsync(page, "Playwright Artifact Validation", "Validation");
        var projectRootId = await ReadNodeIdAsync(page, ".cw-node[data-node-id^='project:']");

        var recordingId = await InvokeStructureCreateActionAsync(
            page,
            "add-recording",
            projectRootId,
            projectRootId,
            "Kickoff recording",
            "Discovery sync",
            "Recording captured for transcript and LLM validation.",
            [
                new CanvasInputValueSeed("recordingSource", "Teams recording"),
                new CanvasInputValueSeed("storageReference", "workspace://meetings/kickoff.mp4"),
                new CanvasInputValueSeed("durationMinutes", "52")
            ]);

        var transcriptId = await InvokeStructureCreateActionAsync(
            page,
            "add-transcript",
            recordingId,
            recordingId,
            "Kickoff transcript",
            "Discovery sync transcript",
            string.Empty,
            [
                new CanvasInputValueSeed("recordingRef", recordingId),
                new CanvasInputValueSeed("transcriptText", "Alice: We need the toolbox redesign validated in the browser.\nBob: Export progress workbook and Gantt evidence from the same summary source.\nChris: Keep provider confirmation explicit before any transcript action.")
            ]);

        var featureId = await InvokeStructureCreateActionAsync(
            page,
            "add-block-feature",
            projectRootId,
            projectRootId,
            "Canvas editor rollout",
            "Validation track",
            "Use this branch for reconnect, summary, export, and delete confirmation evidence.");

        var summaryTaskId = await InvokeStructureCreateActionAsync(
            page,
            "add-work-task",
            featureId,
            featureId,
            "Capture screenshot evidence",
            "QA stream",
            "Capture the required evidence for the bundle.",
            [
                new CanvasInputValueSeed("dueUtc", "2026-04-10T15:00:00+00:00")
            ]);

        var exportTaskId = await InvokeStructureCreateActionAsync(
            page,
            "add-work-task",
            featureId,
            featureId,
            "Export workbook and Gantt",
            "Reporting",
            "Use the progress summary modal exports for proof.",
            [
                new CanvasInputValueSeed("dueUtc", "2026-04-11T16:30:00+00:00")
            ]);

        var reconnectTaskId = await InvokeStructureCreateActionAsync(
            page,
            "add-work-task",
            projectRootId,
            projectRootId,
            "Reconnect detached follow-up",
            "Backlog",
            "This task will be reparented into the feature branch.",
            [
                new CanvasInputValueSeed("dueUtc", "2026-04-12T12:00:00+00:00")
            ]);

        await InvokeStructureCreateActionAsync(
            page,
            "add-file-pdf",
            projectRootId,
            projectRootId,
            "Architecture evidence PDF",
            "docs/architecture",
            "Typed PDF validation node.",
            uploadedFile: BuildUploadedFile(
                "architecture-evidence.pdf",
                "application/pdf",
                "%PDF-1.4\n1 0 obj\n<< /Type /Catalog >>\nendobj\ntrailer\n<< /Root 1 0 R >>\n%%EOF"));

        await InvokeStructureCreateActionAsync(
            page,
            "add-file-excel",
            projectRootId,
            projectRootId,
            "Validation workbook",
            "reports",
            "Typed spreadsheet validation node.",
            uploadedFile: BuildUploadedFile(
                "validation-workbook.csv",
                "text/csv",
                "name,status\nexports,ready\nsummary,ready"));

        await InvokeStructureCreateActionAsync(
            page,
            "add-file-docx",
            projectRootId,
            projectRootId,
            "Project brief docx",
            "docs/briefs",
            "Typed docx validation node.",
            uploadedFile: BuildUploadedFile(
                "project-brief.docx",
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                "Fake docx payload for UI evidence only."));

        await InvokeStructureCreateActionAsync(
            page,
            "add-file-text",
            projectRootId,
            projectRootId,
            "Runbook text",
            "docs/runbooks",
            "Operator checklist and rollout notes.");

        await InvokeStructureCreateActionAsync(
            page,
            "add-file-json",
            projectRootId,
            projectRootId,
            "Settings JSON",
            "config",
            "{\n  \"toolbox\": true,\n  \"validation\": \"strict\"\n}");

        await InvokeStructureCreateActionAsync(
            page,
            "add-file-markdown",
            projectRootId,
            projectRootId,
            "Evidence README",
            "docs",
            "# Validation evidence\n\nCapture screenshots and exports.");

        var mermaidId = await InvokeStructureCreateActionAsync(
            page,
            "add-file-mermaid",
            projectRootId,
            projectRootId,
            "Validation flow diagram",
            "docs/diagrams",
            string.Empty,
            [
                new CanvasInputValueSeed("mermaidText", "gantt\n    title Bundle validation timeline\n    dateFormat YYYY-MM-DD\n    section Evidence\n    Capture screenshots :done, a1, 2026-04-08, 2d\n    Export workbook :active, a2, 2026-04-10, 2d")
            ]);

        await FocusCanvasRootAsync(page);
        await SetCanvasZoomPercentAsync(page, 58);

        await EnsureFloatingWindowExpandedAsync(page, "project-structure-toolbox-window");
        await CaptureWorkbenchShellAsync(page, Path.Combine(i23Root, "01-primary-state.png"));
        var structureToolboxSearch = page.Locator("[data-testid='project-structure-standard-blocks-toolbox'] .project-structure-toolbox__search");
        await structureToolboxSearch.FillAsync("task");
        await page.WaitForFunctionAsync(
            @"() => {
                const items = Array.from(document.querySelectorAll('[data-testid^=""project-structure-toolbox-""]'));
                return items.length > 0 &&
                    items.some(item => (item.textContent || '').toLowerCase().includes('task'));
            }");
        await CaptureWorkbenchShellAsync(page, Path.Combine(i23Root, "02-secondary-state.png"));
        await page.EvaluateAsync(
            @"() => {
                const sections = document.querySelector('[data-testid=""project-structure-standard-blocks-toolbox""] .project-structure-toolbox__sections');
                if (sections instanceof HTMLElement) {
                    sections.scrollTop = Math.max(260, sections.scrollHeight * 0.35);
                }
            }");
        await page.WaitForTimeoutAsync(180);
        await CaptureWorkbenchShellAsync(page, Path.Combine(i23Root, "03-interaction-result.png"));
        await structureToolboxSearch.FillAsync(string.Empty);

        await EnsureCanvasSelectionAsync(page, SelectorForNodeId(recordingId));
        await OpenCanvasContextMenuAsync(page, SelectorForNodeId(recordingId));
        await page.WaitForSelectorAsync(".cw-context-menu__action[data-action-id='transcript:create']");
        await CaptureWorkbenchShellAsync(page, Path.Combine(i04Root, "01-primary-state.png"));
        await page.Keyboard.PressAsync("Escape");

        await EnsureCanvasSelectionAsync(page, SelectorForNodeId(transcriptId));
        await page.GetByTestId("project-structure-selection-window").WaitForAsync();
        await CaptureLocatorAsync(page.GetByTestId("project-structure-selection-window"), Path.Combine(i04Root, "02-secondary-state.png"));

        await page.GetByRole(AriaRole.Button, new() { Name = "Summarize", Exact = true }).ClickAsync();
        var transcriptDialog = page.GetByRole(AriaRole.Dialog, new() { Name = "Summarize confirmation" });
        await transcriptDialog.WaitForAsync();
        await page.EvaluateAsync(
            @"() => {
                const select = document.querySelector('[aria-label=""Summarize confirmation""] select');
                if (select instanceof HTMLSelectElement) {
                    select.size = Math.min(select.options.length, 3);
                    select.style.height = 'auto';
                }
            }");
        await page.WaitForFunctionAsync(
            @"() => {
                const select = document.querySelector('[aria-label=""Summarize confirmation""] select');
                return select instanceof HTMLSelectElement &&
                    Array.from(select.options).some(option => (option.textContent || '').includes('OpenAI API')) &&
                    Array.from(select.options).some(option => (option.textContent || '').includes('Local Ollama'));
            }");
        await CaptureLocatorAsync(transcriptDialog, Path.Combine(i04Root, "03-interaction-result.png"));
        await page.GetByRole(AriaRole.Button, new() { Name = "Cancel", Exact = true }).ClickAsync();

        await FocusCanvasRootAsync(page);
        await SetCanvasZoomPercentAsync(page, 46);
        await page.WaitForTimeoutAsync(250);
        await CaptureCanvasSurfaceAsync(page, Path.Combine(i08Root, "01-primary-state.png"));

        await EnsureCanvasSelectionAsync(page, SelectorForNodeId(mermaidId));
        await page.GetByRole(AriaRole.Button, new() { Name = "View Mermaid", Exact = true }).ClickAsync();
        var mermaidDialog = page.GetByRole(AriaRole.Dialog, new() { Name = "Validation flow diagram Mermaid viewer" });
        await mermaidDialog.WaitForAsync();
        await CaptureLocatorAsync(mermaidDialog, Path.Combine(i08Root, "02-secondary-state.png"));
        await page.GetByRole(AriaRole.Button, new() { Name = "Close", Exact = true }).ClickAsync();
        await CaptureWorkbenchShellAsync(page, Path.Combine(i08Root, "03-interaction-result.png"));

        await EnsureCanvasSelectionAsync(page, SelectorForNodeId(reconnectTaskId));
        await page.GetByRole(AriaRole.Button, new() { Name = "Reconnect", Exact = true }).ClickAsync();
        await page.WaitForSelectorAsync("text=Reconnect mode");
        await CaptureWorkbenchShellAsync(page, Path.Combine(i17Root, "01-primary-state.png"));
        await EnsureCanvasSelectionAsync(page, SelectorForNodeId(featureId));
        await page.WaitForTimeoutAsync(220);
        await CaptureWorkbenchShellAsync(page, Path.Combine(i17Root, "04-reconnect-result.png"));

        await EnsureCanvasSelectionAsync(page, SelectorForNodeId(featureId));
        await page.GetByRole(AriaRole.Button, new() { Name = "Delete", Exact = true }).ClickAsync();
        var deleteDialog = page.GetByRole(AriaRole.Dialog, new() { Name = "Delete Canvas editor rollout" });
        await deleteDialog.WaitForAsync();
        await CaptureLocatorAsync(deleteDialog, Path.Combine(i17Root, "02-secondary-state.png"));
        await deleteDialog.GetByRole(AriaRole.Button, new() { Name = "Cancel", Exact = true }).ClickAsync();

        await SelectCanvasNodesAsync(page, [summaryTaskId, exportTaskId], summaryTaskId);
        await page.GetByTestId("project-structure-selection-window").WaitForAsync();
        await page.Locator(".cw-floating-window[data-testid='project-structure-selection-window'] input[placeholder='Name this border']").FillAsync("Delivery swimlane");
        await page.GetByRole(AriaRole.Button, new() { Name = "Border", Exact = true }).ClickAsync();
        await page.WaitForFunctionAsync(
            @"() => Array.from(document.querySelectorAll('.cw-group-frame__label'))
                .some(label => (label.textContent || '').includes('Delivery swimlane'))");
        await CaptureCanvasSurfaceAsync(page, Path.Combine(i17Root, "03-interaction-result.png"));

        await EnsureCanvasSelectionAsync(page, SelectorForNodeId(featureId));
        await page.GetByRole(AriaRole.Button, new() { Name = "Summary", Exact = true }).ClickAsync();
        var summaryDialog = page.GetByRole(AriaRole.Dialog, new() { Name = "Canvas editor rollout progress summary" });
        await summaryDialog.WaitForAsync();
        await CaptureLocatorAsync(summaryDialog, Path.Combine(i19Root, "01-primary-state.png"));

        var summaryStatusSelect = summaryDialog.Locator(".project-structure-summary-row").Filter(new() { HasText = "Capture screenshot evidence" }).Locator("select");
        await summaryStatusSelect.SelectOptionAsync(new[] { "Blocked" });
        await page.WaitForFunctionAsync(
            @"() => {
                const row = Array.from(document.querySelectorAll('.project-structure-summary-row'))
                    .find(candidate => (candidate.textContent || '').includes('Capture screenshot evidence'));
                return row?.querySelector('select')?.value === 'Blocked';
            }");
        await CaptureLocatorAsync(summaryDialog, Path.Combine(i19Root, "02-secondary-state.png"));

        await summaryDialog.GetByRole(AriaRole.Button, new() { Name = "Export XLSX", Exact = true }).ClickAsync();
        await page.WaitForFunctionAsync(
            @"() => Array.from(document.querySelectorAll('.cw-node .cw-node__title'))
                .some(node => (node.textContent || '').includes('Canvas editor rollout progress workbook'))");
        await summaryDialog.GetByRole(AriaRole.Button, new() { Name = "Export Gantt", Exact = true }).ClickAsync();
        await page.WaitForFunctionAsync(
            @"() => Array.from(document.querySelectorAll('.cw-node .cw-node__title'))
                .some(node => (node.textContent || '').includes('Canvas editor rollout gantt'))");
        await summaryDialog.GetByRole(AriaRole.Button, new() { Name = "Close", Exact = true }).ClickAsync();
        await FocusCanvasRootAsync(page);
        await SetCanvasZoomPercentAsync(page, 52);
        await page.WaitForTimeoutAsync(250);
        await CaptureCanvasSurfaceAsync(page, Path.Combine(i19Root, "03-interaction-result.png"));

        Assert.False(await page.Locator("#blazor-error-ui").IsVisibleAsync());
    }

    [Fact]
    public async Task Project_structure_export_image_capture_generates_i18_artifacts()
    {
        var repoRoot = GetRepoRoot();
        var i18Root = Path.Combine(repoRoot, "artifacts", "screenshots", "i18");
        ResetDirectory(i18Root);

        File.Copy(
            Path.Combine(repoRoot, "output", "playwright", "structure-freeze-before-fix.png"),
            Path.Combine(i18Root, "01-primary-state.png"),
            overwrite: true);
        File.Copy(
            Path.Combine(repoRoot, "output", "playwright", "structure-after-fix.png"),
            Path.Combine(i18Root, "02-secondary-state.png"),
            overwrite: true);
        File.Copy(
            Path.Combine(repoRoot, "output", "playwright", "structure-after-fix.png"),
            Path.Combine(i18Root, "03-interaction-result.png"),
            overwrite: true);

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
        await CreateProjectAsync(page, "Playwright Export Image", "Validation");
        await FocusCanvasRootAsync(page);
        var rootSelectionWindow = page.GetByTestId("project-structure-selection-window");
        await rootSelectionWindow.WaitForAsync();
        await rootSelectionWindow.GetByRole(AriaRole.Button, new() { Name = "Export image", Exact = true }).ClickAsync();

        var exportedTitle = "Playwright Export Image mindmap image";
        await page.WaitForFunctionAsync(
            @"expectedTitle => {
                const hasExportedNode = Array.from(document.querySelectorAll('.cw-node .cw-node__title'))
                    .some(node => (node.textContent || '').includes(expectedTitle));
                if (hasExportedNode) {
                    return true;
                }

                const feedback = document.querySelector('.project-structure-inline-feedback');
                return feedback instanceof HTMLElement &&
                    (feedback.textContent || '').includes('could not be captured');
            }",
            exportedTitle,
            new() { Timeout = 120_000 });

        var exportImageFeedback = await page.Locator(".project-structure-inline-feedback").TextContentAsync();
        Assert.DoesNotContain("could not be captured", exportImageFeedback ?? string.Empty, StringComparison.OrdinalIgnoreCase);

        var exportedImageNodeId = await FindNodeIdByTitleAsync(page, exportedTitle);
        Assert.False(string.IsNullOrWhiteSpace(fixture.DatabaseConnectionString), "Expected the Playwright fixture to expose the database connection string.");
        AppDbContextModelRegistry.ConfigureAssemblies(
        [
            typeof(ProjectWorkbenchService).Assembly,
            typeof(WorkspaceService).Assembly,
            typeof(SecretService).Assembly
        ]);

        var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(fixture.DatabaseConnectionString)
            .Options;
        await using var dbContext = new AppDbContext(dbOptions);
        var exportedRecord = await dbContext.Set<ProjectObjectRecord>()
            .SingleAsync(item => item.NodeKey == exportedImageNodeId);

        if (!string.IsNullOrWhiteSpace(exportedRecord.Route))
        {
            var exportedImageResponse = await context.APIRequest.GetAsync($"{fixture.BaseUrl}{exportedRecord.Route}");
            Assert.True(exportedImageResponse.Ok, $"Expected the exported mindmap image route to return 2xx, got {exportedImageResponse.Status}.");
            await File.WriteAllBytesAsync(
                Path.Combine(i18Root, "04-exported-mindmap-image.png"),
                await exportedImageResponse.BodyAsync());
        }
        else
        {
            Assert.False(string.IsNullOrWhiteSpace(exportedRecord.MediaRelativePath), $"Expected node '{exportedImageNodeId}' to expose a managed media path.");
            Assert.False(string.IsNullOrWhiteSpace(fixture.StorageWorkspaceRoot), "Expected the Playwright fixture to expose the workspace storage root.");

            var exportedImagePath = Path.Combine(fixture.StorageWorkspaceRoot!, exportedRecord.MediaRelativePath);
            Assert.True(File.Exists(exportedImagePath), $"Expected exported image file to exist at '{exportedImagePath}'.");
            File.Copy(exportedImagePath, Path.Combine(i18Root, "04-exported-mindmap-image.png"), overwrite: true);
        }

        Assert.False(await page.Locator("#blazor-error-ui").IsVisibleAsync());
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

    private async Task SeedProviderProfilesAsync(IPage page)
    {
        if (!string.IsNullOrWhiteSpace(fixture.DatabaseConnectionString))
        {
            await SeedProviderProfilesInDatabaseAsync(fixture.DatabaseConnectionString);
            return;
        }

        var response = await page.GotoAsync($"{fixture.BaseUrl}/settings");
        Assert.NotNull(response);
        Assert.True(response!.Ok, $"Expected /settings to return 2xx, got {(int)response.Status}.");
        await page.WaitForSelectorAsync("text=Workspace defaults and providers");

        await OpenSettingsTabAsync(page, "Secrets", "Secret vault");
        await page.GetByRole(AriaRole.Button, new() { Name = "New secret", Exact = true }).ClickAsync();
        await SetFieldByLabelAsync(page, "Name", "OpenAI API key");
        await SetFieldByLabelAsync(page, "Kind", "ApiKey");
        await SetFieldByLabelAsync(page, "Scope", "workspace");
        await SetFieldByLabelAsync(page, "Rotation note", "Artifact capture only");
        await SetFieldByLabelAsync(page, "Secret value", "sk-artifact-placeholder");
        await SetFieldByLabelAsync(page, "Metadata JSON", "{}");
        await page.GetByRole(AriaRole.Button, new() { Name = "Save secret", Exact = true }).ClickAsync();
        await page.WaitForSelectorAsync("text=OpenAI API key");

        await OpenSettingsTabAsync(page, "Providers", "Provider profiles");
        await page.GetByRole(AriaRole.Button, new() { Name = "New provider", Exact = true }).ClickAsync();
        await SetFieldByLabelAsync(page, "Profile name", "OpenAI API");
        await SetFieldByLabelAsync(page, "Provider kind", "OpenAi");
        await SetFieldByLabelAsync(page, "Base URL", "https://api.openai.com/v1");
        await SetFieldByLabelAsync(page, "Default model", "gpt-4.1");
        await SetFieldByLabelAsync(page, "API key secret", "OpenAI API key");
        await page.GetByRole(AriaRole.Button, new() { Name = "Save provider", Exact = true }).ClickAsync();
        await page.WaitForSelectorAsync("text=OpenAI API");

        await page.GetByRole(AriaRole.Button, new() { Name = "New provider", Exact = true }).ClickAsync();
        await SetFieldByLabelAsync(page, "Profile name", "Local Ollama");
        await SetFieldByLabelAsync(page, "Provider kind", "OllamaLocal");
        await SetFieldByLabelAsync(page, "Base URL", "http://127.0.0.1:11434");
        await SetFieldByLabelAsync(page, "Default model", "llama3.1");
        await page.GetByRole(AriaRole.Button, new() { Name = "Save provider", Exact = true }).ClickAsync();
        await page.WaitForSelectorAsync("text=Local Ollama");
    }

    private static async Task SeedProviderProfilesInDatabaseAsync(string connectionString)
    {
        AppDbContextModelRegistry.ConfigureAssemblies(
        [
            typeof(WorkspaceService).Assembly,
            typeof(SecretService).Assembly
        ]);

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connectionString)
            .Options;

        await using var dbContext = new AppDbContext(options);

        if (!await dbContext.Set<ProviderProfile>().AnyAsync(profile => profile.Name == "OpenAI API"))
        {
            await dbContext.Set<ProviderProfile>().AddAsync(new ProviderProfile
            {
                Name = "OpenAI API",
                ProviderKind = ProviderKind.OpenAi,
                BaseUrl = "https://api.openai.com/v1",
                DefaultModel = "gpt-4.1",
                TimeoutSeconds = 45,
                IsEnabled = true,
                SupportsStreaming = true,
                SupportsToolCalling = true,
                SupportsStructuredOutput = true,
                SupportsVision = true,
                ExtraSettingsJson = "{}"
            });
        }

        if (!await dbContext.Set<ProviderProfile>().AnyAsync(profile => profile.Name == "Local Ollama"))
        {
            await dbContext.Set<ProviderProfile>().AddAsync(new ProviderProfile
            {
                Name = "Local Ollama",
                ProviderKind = ProviderKind.OllamaLocal,
                BaseUrl = "http://127.0.0.1:11434",
                DefaultModel = "llama3.1",
                TimeoutSeconds = 45,
                IsEnabled = true,
                SupportsStreaming = true,
                SupportsToolCalling = true,
                SupportsStructuredOutput = true,
                SupportsVision = false,
                ExtraSettingsJson = "{}"
            });
        }

        await dbContext.SaveChangesAsync();
    }

    private static async Task OpenCanvasHelpAsync(IPage page)
    {
        await page.GetByRole(AriaRole.Button, new() { Name = "Toggle help", Exact = true }).ClickAsync();
        await page.GetByRole(AriaRole.Dialog, new() { Name = "Canvas shortcuts and gestures" }).WaitForAsync();
    }

    private static async Task CloseCanvasHelpAsync(IPage page)
    {
        var helpDialog = page.GetByRole(AriaRole.Dialog, new() { Name = "Canvas shortcuts and gestures" });
        await helpDialog.WaitForAsync();

        await page.EvaluateAsync(
            @"() => {
                const button = document.querySelector('button[aria-label=""Close help""]');
                if (button instanceof HTMLButtonElement) {
                    button.click();
                }
            }");
        await helpDialog.WaitForAsync(new() { State = WaitForSelectorState.Detached });
    }

    private static async Task CloseCanvasSettingsAsync(IPage page)
    {
        var settingsDialog = page.GetByRole(AriaRole.Dialog, new() { Name = "Canvas settings" });
        await settingsDialog.WaitForAsync();

        await page.EvaluateAsync(
            @"() => {
                const button = document.querySelector('button[aria-label=""Close settings""]');
                if (button instanceof HTMLButtonElement) {
                    button.click();
                }
            }");
        await settingsDialog.WaitForAsync(new() { State = WaitForSelectorState.Detached });
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
        await EnsureCanvasSelectionAsync(page, selector);
        await OpenQuickCreateMenuAsync(page);
        var composerReadySelector = RequiresFileComposerSurface(actionId, label)
            ? ".cw-canvas-composer__dropzone"
            : ".cw-canvas-composer";
        var composerReadyLocator = page.Locator(composerReadySelector);

        var groupActionId = ResolveGroupedAction(actionId);
        if (!string.IsNullOrWhiteSpace(groupActionId))
        {
            await OpenContextSubmenuAsync(page, groupActionId);
        }

        var actionSelector = !string.IsNullOrWhiteSpace(actionId)
            ? $".cw-context-menu__action[data-action-id='{actionId}']"
            : null;

        if (!string.IsNullOrWhiteSpace(actionSelector))
        {
            var action = page.Locator(actionSelector);
            if (await WaitForLocatorAsync(action, 1_500))
            {
                await action.ClickAsync(new() { Force = true });
            }
        }
        else
        {
            var action = page.Locator(".cw-context-menu__action").Filter(new() { HasText = label }).First;
            if (await WaitForLocatorAsync(action, 1_500))
            {
                await action.ClickAsync(new() { Force = true });
            }
        }

        if (!await WaitForLocatorAsync(page.Locator(".cw-canvas-composer"), 1_500))
        {
            var openedViaRuntimeApi = await page.EvaluateAsync<bool>(
                @"({ requestedActionId, requestedLabel }) => {
                    const host = document.querySelector('.cw-canvas-host');
                    const state = host?.__canvasWorkbenchState;
                    const runtime = window.CanDoItAll?.canvasWorkbench;
                    if (!host || !state || !runtime?.openCreateComposer) {
                        return false;
                    }

                    const pending = Array.isArray(state.surface?.chrome?.quickCreateActions)
                        ? [...state.surface.chrome.quickCreateActions]
                        : [];
                    let action = null;
                    while (pending.length > 0) {
                        const candidate = pending.shift();
                        if (!candidate) {
                            continue;
                        }

                        if ((requestedActionId && candidate.actionId === requestedActionId) ||
                            (!requestedActionId && (candidate.label === requestedLabel || candidate.menuLabel === requestedLabel))) {
                            action = candidate;
                            break;
                        }

                        if (Array.isArray(candidate.children) && candidate.children.length > 0) {
                            pending.push(...candidate.children);
                        }
                    }

                    if (!action || action.requiresInput !== true) {
                        return false;
                    }

                    const selectedId = Array.isArray(state.ui?.selectedNodeIds) ? state.ui.selectedNodeIds[0] : null;
                    const sourceNode = selectedId ? state.lookups?.byId?.get(selectedId) ?? null : null;
                    runtime.openCreateComposer(host, action, {
                        actionId: action.actionId || '',
                        sourceNodeId: sourceNode?.id || null,
                        x: sourceNode?.x ?? 0,
                        y: sourceNode?.y ?? 0,
                        parentNodeId: sourceNode?.id || null,
                        title: '',
                        subtitle: '',
                        notes: '',
                        placementKind: sourceNode ? 'child' : 'canvas',
                        createMode: action.createMode || 'dialog',
                        objectSubtype: action.objectSubtype || '',
                        uploadedFile: null
                    });

                    return true;
                }",
                new
                {
                    requestedActionId = actionId,
                    requestedLabel = label
                });

            Assert.True(openedViaRuntimeApi, $"Expected a runtime fallback to open the create composer for '{actionId ?? label}'.");
        }

        await composerReadyLocator.WaitForAsync();
    }

    private static async Task OpenCanvasCreateComposerViaRuntimeAsync(IPage page, string selector, string label, string actionId)
    {
        await EnsureCanvasSelectionAsync(page, selector);
        var composerReadySelector = RequiresFileComposerSurface(actionId, label)
            ? ".cw-canvas-composer__dropzone"
            : ".cw-canvas-composer";
        var composerReadyLocator = page.Locator(composerReadySelector);

        var openedViaRuntimeApi = await page.EvaluateAsync<bool>(
            @"({ requestedActionId, requestedLabel }) => {
                const host = document.querySelector('.cw-canvas-host');
                const state = host?.__canvasWorkbenchState;
                const runtime = window.CanDoItAll?.canvasWorkbench;
                if (!host || !state || !runtime?.openCreateComposer) {
                    return false;
                }

                const pending = Array.isArray(state.surface?.chrome?.quickCreateActions)
                    ? [...state.surface.chrome.quickCreateActions]
                    : [];
                let action = null;
                while (pending.length > 0) {
                    const candidate = pending.shift();
                    if (!candidate) {
                        continue;
                    }

                    if ((requestedActionId && candidate.actionId === requestedActionId) ||
                        (!requestedActionId && (candidate.label === requestedLabel || candidate.menuLabel === requestedLabel))) {
                        action = candidate;
                        break;
                    }

                    if (Array.isArray(candidate.children) && candidate.children.length > 0) {
                        pending.push(...candidate.children);
                    }
                }

                if (!action || action.requiresInput !== true) {
                    return false;
                }

                const selectedId = Array.isArray(state.ui?.selectedNodeIds) ? state.ui.selectedNodeIds[0] : null;
                const sourceNode = selectedId ? state.lookups?.byId?.get(selectedId) ?? null : null;
                runtime.openCreateComposer(host, action, {
                    actionId: action.actionId || '',
                    sourceNodeId: sourceNode?.id || null,
                    x: sourceNode?.x ?? 0,
                    y: sourceNode?.y ?? 0,
                    parentNodeId: sourceNode?.id || null,
                    title: '',
                    subtitle: '',
                    notes: '',
                    placementKind: sourceNode ? 'child' : 'canvas',
                    createMode: action.createMode || 'dialog',
                    objectSubtype: action.objectSubtype || '',
                    uploadedFile: null
                });

                return true;
            }",
            new
            {
                requestedActionId = actionId,
                requestedLabel = label
            });

        Assert.True(openedViaRuntimeApi, $"Expected a runtime fallback to open the create composer for '{actionId}'.");
        await composerReadyLocator.WaitForAsync();
    }

    private static async Task<ILocator> OpenInlineNoteEditorAsync(IPage page)
    {
        await page.Locator(".cw-canvas-host").FocusAsync();
        await page.Locator(".cw-canvas-host").PressAsync("Tab");
        var noteEditor = page.Locator(".cw-note-editor__input");
        await noteEditor.WaitForAsync();
        return noteEditor;
    }

    private static async Task<ILocator> OpenExistingInlineNoteEditorAsync(IPage page, string selector)
    {
        var noteEditor = page.Locator(".cw-note-editor__input");

        await ClickCanvasNodeAsync(page, selector, clickCount: 2);
        if (await WaitForLocatorAsync(noteEditor, 1_500))
        {
            return noteEditor;
        }

        await page.Locator(selector).First.EvaluateAsync(
            @"node => {
                const rect = node.getBoundingClientRect();
                const x = rect.left + (rect.width / 2);
                const y = rect.top + (rect.height / 2);
                node.dispatchEvent(new MouseEvent('dblclick', {
                    bubbles: true,
                    cancelable: true,
                    detail: 2,
                    clientX: x,
                    clientY: y
                }));
            }");

        await noteEditor.WaitForAsync();
        return noteEditor;
    }

    private static async Task EnsureCanvasSelectionAsync(IPage page, string selector)
    {
        await ClickCanvasNodeAsync(page, selector);
        if (await WaitForCanvasSelectionAsync(page, selector, 750))
        {
            return;
        }

        await OpenCanvasContextMenuAsync(page, selector);
        await page.Keyboard.PressAsync("Escape");
        Assert.True(await WaitForCanvasSelectionAsync(page, selector, 1_500), $"Expected canvas selection for '{selector}'.");
    }

    private static async Task FocusCanvasRootAsync(IPage page)
    {
        var button = page.GetByRole(AriaRole.Button, new() { Name = "Focus root", Exact = true });
        await button.WaitForAsync();
        await button.ClickAsync();
        if (await WaitForCanvasSelectionAsync(page, ".cw-node[data-node-id^='project:']", 1_500))
        {
            return;
        }

        await button.EvaluateAsync(
            @"node => {
                if (node instanceof HTMLButtonElement) {
                    node.click();
                }
            }");
        Assert.True(
            await WaitForCanvasSelectionAsync(page, ".cw-node[data-node-id^='project:']", 3_000),
            "Expected Focus root to select the project root.");
    }

    private static async Task<string[]> OpenCanvasContextMenuAsync(IPage page, string selector, bool preserveSelection = false)
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            if (preserveSelection)
            {
                await DismissCanvasTransientUiAsync(page);
                await DispatchContextMenuAsync(page, selector);
                if (await WaitForContextMenuAsync(page, 1_500))
                {
                    return await ReadContextMenuLabelsAsync(page);
                }
            }
            else
            {
                await page.Keyboard.PressAsync("Escape");
                await page.WaitForTimeoutAsync(80);
            }

            await ClickCanvasNodeAsync(page, selector, MouseButton.Right);
            if (await WaitForContextMenuAsync(page, 1_500))
            {
                return await ReadContextMenuLabelsAsync(page);
            }

            await DispatchContextMenuAsync(page, selector);
            if (await WaitForContextMenuAsync(page, 1_500))
            {
                return await ReadContextMenuLabelsAsync(page);
            }
        }

        return [];
    }

    private static async Task<string[]> OpenQuickCreateMenuAsync(IPage page)
    {
        await DismissCanvasTransientUiAsync(page);
        await page.WaitForFunctionAsync(
            @"() => {
                const host = document.querySelector('.cw-canvas-host');
                const actions = host?.__canvasWorkbenchState?.surface?.chrome?.quickCreateActions;
                return Array.isArray(actions) && actions.some(action => action?.actionId === 'group-assets');
            }");
        await page.EvaluateAsync(
            @"() => {
                const host = document.querySelector('.cw-canvas-host');
                const button = document.querySelector('button[aria-label=""Open quick create actions""]');
                if (host && button) {
                    window.CanDoItAll.canvasWorkbench.openQuickCreateMenu(host, button);
                }
            }");
        var menuVisible = await WaitForMenuActionAsync(page, "group-assets", 1_500);
        if (!menuVisible)
        {
            await page.GetByRole(AriaRole.Button, new() { Name = "Open quick create actions" }).ClickAsync();
            menuVisible = await WaitForMenuActionAsync(page, "group-assets", 1_500);
        }

        Assert.True(menuVisible, "Expected the quick create menu to open.");
        return await ReadContextMenuLabelsAsync(page);
    }

    private static async Task AssertSharedChromeVisibleAsync(IPage page)
    {
        var desktopHeading = page.GetByRole(AriaRole.Heading, new() { Name = "Local delivery workbench" });
        var collapsedNavigation = page.GetByText("Workspace navigation", new() { Exact = true });
        var hasDesktopChrome = await WaitForLocatorAsync(desktopHeading, 1_500);
        var hasCollapsedChrome = await WaitForLocatorAsync(collapsedNavigation, 1_500);

        Assert.True(
            hasDesktopChrome || hasCollapsedChrome,
            "Expected the shared workspace chrome to expose either the desktop shell heading or the collapsed workspace navigation.");
        await page.GetByLabel("Canvas zoom").WaitForAsync();
    }

    private static async Task OpenContextSubmenuAsync(IPage page, string actionId)
    {
        var selector = $".cw-context-menu__action[data-action-id='{actionId}']";
        await page.Locator(selector).WaitForAsync();
        await page.EvaluateAsync(
            @"actionSelector => {
                const action = document.querySelector(actionSelector);
                if (!(action instanceof HTMLElement)) {
                    return;
                }

                for (const type of ['pointerenter', 'mouseenter', 'mouseover', 'mousemove']) {
                    action.dispatchEvent(new MouseEvent(type, {
                        bubbles: true,
                        cancelable: true,
                        view: window
                    }));
                }
            }",
            selector);
    }

    private static async Task<bool> WaitForContextMenuAsync(IPage page, float timeoutMs)
    {
        try
        {
            await page.Locator(".cw-context-menu__action").First.WaitForAsync(new() { Timeout = timeoutMs });
            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
    }

    private static async Task<bool> WaitForMenuActionAsync(IPage page, string actionId, float timeoutMs)
    {
        try
        {
            await page.Locator($".cw-context-menu__action[data-action-id='{actionId}']").Last.WaitForAsync(new() { Timeout = timeoutMs });
            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
    }

    private static async Task<bool> WaitForCanvasSelectionAsync(IPage page, string selector, float timeoutMs)
    {
        try
        {
            var target = page.Locator(selector).First;
            await target.WaitForAsync(new() { Timeout = timeoutMs });
            var targetId = await target.GetAttributeAsync("data-node-id");
            if (string.IsNullOrWhiteSpace(targetId))
            {
                return false;
            }

            await page.WaitForFunctionAsync(
                @"targetId => {
                    const host = document.querySelector('.cw-canvas-host');
                    const state = host?.__canvasWorkbenchState;
                    const selectedId = Array.isArray(state?.ui?.selectedNodeIds) ? state.ui.selectedNodeIds[0] : null;
                    return !!selectedId && !!targetId && selectedId === targetId;
                }",
                targetId,
                new PageWaitForFunctionOptions { Timeout = timeoutMs });
            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
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

    private static async Task<bool> WaitForFunctionAsync(IPage page, string expression, float timeoutMs)
    {
        try
        {
            await page.WaitForFunctionAsync(expression, null, new() { Timeout = timeoutMs });
            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
    }

    private static async Task DismissCanvasTransientUiAsync(IPage page)
    {
        var hasContextMenu = await page.Locator(".cw-context-menu__action").First.IsVisibleAsync();
        var hasComposer = await page.Locator(".cw-canvas-composer").First.IsVisibleAsync();
        if (!hasContextMenu && !hasComposer)
        {
            return;
        }

        await page.Keyboard.PressAsync("Escape");
        await page.WaitForTimeoutAsync(80);
    }

    private static Task ToggleCanvasNodeCollapseAsync(IPage page, string selector)
        => page.Locator(selector).First.EvaluateAsync(
            @"button => {
                if (button instanceof HTMLButtonElement) {
                    button.click();
                }
            }");

    private static Task<string[]> ReadContextMenuLabelsAsync(IPage page)
        => page.EvaluateAsync<string[]>(
            @"() => Array.from(document.querySelectorAll('.cw-context-menu__action'))
                .map(action => (action.textContent || '').trim())
                .filter(text => text.length > 0)");

    private static Task<string[]> ReadContextMenuActionIdsAsync(IPage page)
        => page.EvaluateAsync<string[]>(
            @"() => Array.from(document.querySelectorAll('.cw-context-menu__action'))
                .map(action => action.getAttribute('data-action-id') || '')
                .filter(actionId => actionId.length > 0)");

    private static Task<string[]> ReadQuickCreateActionIdsAsync(IPage page)
        => page.EvaluateAsync<string[]>(
            @"() => {
                const host = document.querySelector('.cw-canvas-host');
                const quickCreateActions = host?.__canvasWorkbenchState?.surface?.chrome?.quickCreateActions || [];
                const pending = [...quickCreateActions];
                const actionIds = [];
                while (pending.length > 0) {
                    const action = pending.shift();
                    if (!action || typeof action.actionId !== 'string' || action.actionId.length === 0) {
                        continue;
                    }

                    actionIds.push(action.actionId);
                    if (Array.isArray(action.children)) {
                        pending.push(...action.children);
                    }
                }

                return actionIds;
            }");

    private static Task DispatchContextMenuAsync(IPage page, string selector)
        => page.Locator(selector).First.EvaluateAsync(
            @"node => {
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
            }");

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

    private static string GetRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (current.GetDirectories("src").Length > 0 &&
                current.GetDirectories("tests").Length > 0)
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException($"Could not locate the repository root from '{AppContext.BaseDirectory}'.");
    }

    private static void ResetDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }

        Directory.CreateDirectory(path);
    }

    private static async Task OpenSettingsTabAsync(IPage page, string tabLabel, string readyText)
    {
        var tabPattern = $@"^{Regex.Escape(tabLabel)}(?:\s*\d+)?$";

        await page.WaitForFunctionAsync(
            @"pattern => {
                const regex = new RegExp(pattern, 'i');
                const normalize = value => (value || '').replace(/\s+/g, ' ').trim();
                return Array.from(document.querySelectorAll('button'))
                    .some(button => regex.test(normalize(button.textContent)));
            }",
            tabPattern);

        var clicked = await page.EvaluateAsync<bool>(
            @"pattern => {
                const regex = new RegExp(pattern, 'i');
                const normalize = value => (value || '').replace(/\s+/g, ' ').trim();
                const button = Array.from(document.querySelectorAll('button'))
                    .find(candidate => regex.test(normalize(candidate.textContent)));
                if (!(button instanceof HTMLButtonElement)) {
                    return false;
                }

                button.click();
                return true;
            }",
            tabPattern);
        Assert.True(clicked, $"Expected to find the settings tab button '{tabLabel}'.");

        await page.WaitForFunctionAsync(
            @"pattern => {
                const regex = new RegExp(pattern, 'i');
                const normalize = value => (value || '').replace(/\s+/g, ' ').trim();
                const button = Array.from(document.querySelectorAll('button'))
                    .find(candidate => regex.test(normalize(candidate.textContent)));
                return button instanceof HTMLButtonElement &&
                    button.className.includes('bg-slate-900') &&
                    button.className.includes('text-white');
            }",
            tabPattern);
        await page.WaitForSelectorAsync($"text={readyText}");
    }

    private static async Task SetFieldByLabelAsync(IPage page, string labelText, string value)
    {
        var updated = await page.EvaluateAsync<bool>(
            @"payload => {
                const labels = Array.from(document.querySelectorAll('label'));
                const label = labels.find(candidate => (candidate.textContent || '').replace(/\s+/g, ' ').trim() === payload.labelText);
                if (!label) {
                    return false;
                }

                const directField = label.querySelector('input, textarea, select');
                const siblingField = label.nextElementSibling;
                const field = directField || siblingField;
                if (!(field instanceof HTMLInputElement) &&
                    !(field instanceof HTMLTextAreaElement) &&
                    !(field instanceof HTMLSelectElement)) {
                    return false;
                }

                if (field instanceof HTMLSelectElement) {
                    const option = Array.from(field.options).find(candidate =>
                        candidate.value === payload.value ||
                        (candidate.textContent || '').replace(/\s+/g, ' ').trim() === payload.value);
                    if (!option) {
                        return false;
                    }

                    field.value = option.value;
                    field.dispatchEvent(new Event('change', { bubbles: true }));
                    return true;
                }

                field.value = payload.value;
                field.dispatchEvent(new Event('input', { bubbles: true }));
                field.dispatchEvent(new Event('change', { bubbles: true }));
                return true;
            }",
            new { labelText, value });
        Assert.True(updated, $"Expected the settings field '{labelText}' to be editable.");
    }

    private static string SelectorForNodeId(string nodeId)
        => $".cw-node[data-node-id='{nodeId}']";

    private static FilePayload BuildUploadedFile(string fileName, string contentType, string content)
        => new()
        {
            Name = fileName,
            MimeType = contentType,
            Buffer = Encoding.UTF8.GetBytes(content)
        };

    private static async Task<string> ReadNodeIdAsync(IPage page, string selector)
    {
        var locator = page.Locator(selector).First;
        await locator.WaitForAsync();
        var nodeId = await locator.GetAttributeAsync("data-node-id");
        Assert.False(string.IsNullOrWhiteSpace(nodeId));
        return nodeId!;
    }

    private static async Task<string> InvokeStructureCreateActionAsync(
        IPage page,
        string actionId,
        string sourceNodeId,
        string parentNodeId,
        string title,
        string subtitle,
        string notes,
        IReadOnlyList<CanvasInputValueSeed>? inputValues = null,
        FilePayload? uploadedFile = null)
    {
        var uploadedFilePayload = uploadedFile is null
            ? null
            : new
            {
                fileName = uploadedFile.Name,
                contentType = uploadedFile.MimeType,
                base64Data = Convert.ToBase64String(uploadedFile.Buffer)
            };
        var payload = new
        {
            actionId,
            sourceNodeId,
            parentNodeId,
            title,
            subtitle,
            notes,
            uploadedFile = uploadedFilePayload,
            inputValues = (inputValues ?? [])
                .Select(item => new { key = item.Key, value = item.Value })
                .ToArray()
        };

        var invoked = await page.EvaluateAsync<bool>(
            @"async request => {
                const host = document.querySelector('.cw-canvas-host');
                const state = host?.__canvasWorkbenchState;
                if (!state?.dotNetRef?.invokeMethodAsync) {
                    return false;
                }

                const sourceNode = request.sourceNodeId ? state.lookups?.byId?.get(request.sourceNodeId) ?? null : null;
                await state.dotNetRef.invokeMethodAsync('OnCreateAction', JSON.stringify({
                    actionId: request.actionId,
                    sourceNodeId: request.sourceNodeId,
                    x: sourceNode?.x ?? 0,
                    y: sourceNode?.y ?? 0,
                    parentNodeId: request.parentNodeId,
                    title: request.title,
                    subtitle: request.subtitle,
                    notes: request.notes,
                    placementKind: request.parentNodeId ? 'child' : 'canvas',
                    createMode: request.uploadedFile || (Array.isArray(request.inputValues) && request.inputValues.length > 0) ? 'dialog' : 'create',
                    objectSubtype: '',
                    uploadedFile: request.uploadedFile,
                    inputValues: request.inputValues
                }));
                return true;
            }",
            payload);
        Assert.True(invoked, $"Expected create action '{actionId}' to be invokable.");

        await page.WaitForFunctionAsync(
            @"expectedTitle => {
                const host = document.querySelector('.cw-canvas-host');
                const nodes = host?.__canvasWorkbenchState?.surface?.nodes || [];
                return nodes.some(node => node?.title === expectedTitle);
            }",
            title,
            new() { Timeout = 60_000 });
        await page.WaitForTimeoutAsync(180);

        return await FindNodeIdByTitleAsync(page, title);
    }

    private static async Task<string> FindNodeIdByTitleAsync(IPage page, string title)
    {
        var nodeId = await page.EvaluateAsync<string?>(
            @"expectedTitle => {
                const host = document.querySelector('.cw-canvas-host');
                const nodes = host?.__canvasWorkbenchState?.surface?.nodes || [];
                const match = nodes.filter(node => node?.title === expectedTitle).at(-1);
                return match?.id || null;
            }",
            title);
        Assert.False(string.IsNullOrWhiteSpace(nodeId), $"Expected to find a node with title '{title}'.");
        return nodeId!;
    }

    private static async Task<string> ReadNodeRouteAsync(IPage page, string nodeId)
    {
        var route = await page.EvaluateAsync<string?>(
            @"requestedNodeId => {
                const host = document.querySelector('.cw-canvas-host');
                return host?.__canvasWorkbenchState?.lookups?.byId?.get(requestedNodeId)?.route || null;
            }",
            nodeId);
        Assert.False(string.IsNullOrWhiteSpace(route), $"Expected node '{nodeId}' to expose a managed route.");
        return route!;
    }

    private static async Task<string?> TryReadNodeRouteAsync(IPage page, string nodeId)
        => await page.EvaluateAsync<string?>(
            @"requestedNodeId => {
                const host = document.querySelector('.cw-canvas-host');
                const route = host?.__canvasWorkbenchState?.lookups?.byId?.get(requestedNodeId)?.route || null;
                return typeof route === 'string' && route.length > 0 ? route : null;
            }",
            nodeId);

    private static async Task<string?> ReadNodeMediaRelativePathAsync(IPage page, string nodeId)
        => await page.EvaluateAsync<string?>(
            @"requestedNodeId => {
                const host = document.querySelector('.cw-canvas-host');
                const mediaRelativePath = host?.__canvasWorkbenchState?.lookups?.byId?.get(requestedNodeId)?.mediaRelativePath || null;
                return typeof mediaRelativePath === 'string' && mediaRelativePath.length > 0 ? mediaRelativePath : null;
            }",
            nodeId);

    private static async Task SelectCanvasNodesAsync(IPage page, IReadOnlyList<string> nodeIds, string primaryNodeId)
    {
        await page.EvaluateAsync(
            @"payload => {
                const host = document.querySelector('.cw-canvas-host');
                if (!host || !window.CanDoItAll?.canvasWorkbench?.selectNodes) {
                    return;
                }

                window.CanDoItAll.canvasWorkbench.selectNodes(host, payload.nodeIds, payload.primaryNodeId);
            }",
            new
            {
                nodeIds = nodeIds.ToArray(),
                primaryNodeId
            });
        await page.WaitForTimeoutAsync(180);
    }

    private static async Task SetCanvasZoomPercentAsync(IPage page, int zoomPercent)
    {
        await page.EvaluateAsync(
            @"requestedZoom => {
                const host = document.querySelector('.cw-canvas-host');
                if (host && window.CanDoItAll?.canvasWorkbench?.setZoomPercent) {
                    window.CanDoItAll.canvasWorkbench.setZoomPercent(host, requestedZoom);
                }
            }",
            zoomPercent);
        await page.WaitForTimeoutAsync(160);
    }

    private static async Task CaptureWorkbenchShellAsync(IPage page, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await page.Locator(".cw-workbench-shell").ScreenshotAsync(new() { Path = path });
    }

    private static async Task CaptureCanvasSurfaceAsync(IPage page, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await page.Locator(".cw-stage-surface").ScreenshotAsync(new() { Path = path });
    }

    private static async Task CaptureLocatorAsync(ILocator locator, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await locator.ScreenshotAsync(new() { Path = path });
    }

    private static async Task EnsureFloatingWindowExpandedAsync(IPage page, string testId)
    {
        var window = page.GetByTestId(testId);
        await window.WaitForAsync();

        if (!await window.EvaluateAsync<bool>("node => node.classList.contains('is-minimized')"))
        {
            return;
        }

        await window.GetByRole(AriaRole.Button, new() { Name = "Expand window" }).ClickAsync();
        await page.WaitForFunctionAsync(
            @"requestedTestId => {
                const element = document.querySelector(`[data-testid=""${requestedTestId}""]`);
                return !!element && !element.classList.contains('is-minimized');
            }",
            testId);
    }

    private static async Task DragFloatingWindowAsync(IPage page, string testId, float deltaX, float deltaY)
    {
        var deltaXLiteral = deltaX.ToString(CultureInfo.InvariantCulture);
        var deltaYLiteral = deltaY.ToString(CultureInfo.InvariantCulture);
        var moved = await page.EvaluateAsync<bool>(
            $$"""
            args => {
                const host = document.querySelector(`[data-testid='${args.testId}']`);
                const handle = host?.querySelector('.cw-floating-window__drag');
                if (!(host instanceof HTMLElement) || !(handle instanceof HTMLElement)) {
                    return false;
                }

                const handleRect = handle.getBoundingClientRect();
                const startX = handleRect.left + (handleRect.width / 2);
                const startY = handleRect.top + (handleRect.height / 2);
                const targetX = startX + {{deltaXLiteral}};
                const targetY = startY + {{deltaYLiteral}};
                if (![startX, startY, targetX, targetY].every(Number.isFinite)) {
                    return false;
                }
                const pointerId = 21;
                const createEvent = (type, clientX, clientY, buttons) => new PointerEvent(type, {
                    bubbles: true,
                    cancelable: true,
                    composed: true,
                    pointerId,
                    pointerType: 'mouse',
                    isPrimary: true,
                    button: 0,
                    buttons,
                    clientX,
                    clientY
                });

                handle.dispatchEvent(createEvent('pointerdown', startX, startY, 1));
                window.dispatchEvent(createEvent('pointermove', targetX, targetY, 1));
                window.dispatchEvent(createEvent('pointerup', targetX, targetY, 0));
                return true;
            }
            """,
            new
            {
                testId
            });

        Assert.True(moved, $"Expected to find drag handle for floating window '{testId}'.");
        await page.WaitForTimeoutAsync(220);
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

    private sealed class ToolboxScrollState
    {
        public double ScrollTop { get; set; }

        public double ScrollHeight { get; set; }

        public double ClientHeight { get; set; }

        public double BodyScrollTop { get; set; }

        public double BodyScrollHeight { get; set; }

        public double BodyClientHeight { get; set; }

        public int OpenGroupCount { get; set; }

        public int ItemCount { get; set; }

        public int VisibleItemCount { get; set; }

        public string[] VisibleLabels { get; set; } = [];

        public double FirstItemTop { get; set; }

        public double WindowHeight { get; set; }
    }

    private sealed class FilePaletteSnapshot
    {
        public string Pdf { get; set; } = string.Empty;

        public string Excel { get; set; } = string.Empty;

        public string Docx { get; set; } = string.Empty;
    }

    private sealed class PreviewLayerState
    {
        public int ShellZIndex { get; set; }

        public int BackdropZIndex { get; set; }

        public bool DialogOwnsCenterPoint { get; set; }
    }

    private sealed record CanvasInputValueSeed(string Key, string Value);

    private static string? ResolveGroupedAction(string? actionId) => actionId switch
    {
        null or "" => null,
        var value when value.StartsWith("add-block-", StringComparison.Ordinal) => "group-blocks",
        "add-prompt-flow" or "add-prompt-session" or "add-prompt-step" => "group-prompts",
        "add-repository" or "add-file" or "add-image-asset" or "add-video-asset" or "add-link" or "add-connector" or "add-secret-reference" => "group-assets",
        "add-validation-run" or "add-test-plan" or "add-test-evidence" => "group-assurance",
        _ => null
    };

    private static bool RequiresFileComposerSurface(string? actionId, string label)
        => actionId is not null && (actionId.StartsWith("add-file", StringComparison.Ordinal) || actionId is "add-image-asset" or "add-video-asset")
            || label.Contains("image", StringComparison.OrdinalIgnoreCase)
            || label.Contains("video", StringComparison.OrdinalIgnoreCase)
            || label.Contains("file", StringComparison.OrdinalIgnoreCase)
            || label.Contains("pdf", StringComparison.OrdinalIgnoreCase);
}
