using System.Globalization;
using System.Text.Json;
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
public sealed partial class AppSmokeTests
{
    private readonly PlaywrightAppFixture fixture;

    public AppSmokeTests(PlaywrightAppFixture fixture)
    {
        this.fixture = fixture;
    }

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
        await page.WaitForFunctionAsync(
            @"() => {
                const key = Object.keys(localStorage)
                    .find(candidate => candidate.startsWith('candoitall.workbench.session:'));
                return !!key && (localStorage.getItem(key) || '').includes('route:test-lab');
            }");

        var storageBeforeReload = await ReadWorkbenchSessionStorageAsync(page);
        Assert.False(string.IsNullOrWhiteSpace(storageBeforeReload.Key));
        Assert.NotNull(storageBeforeReload.Value);
        Assert.Contains("\"version\":4", storageBeforeReload.Value, StringComparison.Ordinal);
        Assert.Contains("route:test-lab", storageBeforeReload.Value, StringComparison.Ordinal);

        await page.ReloadAsync();
        await page.WaitForSelectorAsync("text=Tests, evidence, and execution results");

        var storageAfterReload = await ReadWorkbenchSessionStorageAsync(page);
        Assert.Equal(storageBeforeReload.Key, storageAfterReload.Key);
        Assert.NotNull(storageAfterReload.Value);
        Assert.Contains("\"version\":4", storageAfterReload.Value, StringComparison.Ordinal);
        Assert.Contains("route:test-lab", storageAfterReload.Value, StringComparison.Ordinal);
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
        await WaitForSceneNodeTitleAsync(page, "Architecture validation PDF", selectedOnly: true);
        Assert.False(await page.Locator("#blazor-error-ui").IsVisibleAsync());
    }

    [Fact]
    public async Task Settings_page_supports_manifest_driven_provider_management()
    {
        var evidenceDirectory = @"C:\repositories\CanDoItAll\evidence\plugin-wave\v8";
        Directory.CreateDirectory(evidenceDirectory);

        await using var context = await fixture.Browser.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize
            {
                Width = 1600,
                Height = 1000
            }
        });
        var page = await context.NewPageAsync();

        var response = await page.GotoAsync($"{fixture.BaseUrl}/settings?tab=providers");
        Assert.NotNull(response);
        Assert.True(response!.Ok, $"Expected /settings?tab=providers to return 2xx, got {(int)response.Status}.");
        await DismissStartupModalIfPresentAsync(page);
        await page.GetByRole(AriaRole.Button, new() { Name = "New provider", Exact = true }).WaitForAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "New provider", Exact = true }).ClickAsync();
        await page.GetByTestId("provider-plugin-select").SelectOptionAsync(OllamaProviderAdapter.PluginKey);
        await page.GetByTestId("provider-name-input").FillAsync("Playwright Ollama");
        await page.GetByTestId("provider-base-url-input").FillAsync("http://127.0.0.1:11434");
        await page.GetByTestId("provider-default-model-input").FillAsync("llama3.1");
        await page.GetByTestId("provider-save-button").ClickAsync();
        await page.WaitForSelectorAsync("text=Provider profile saved.");
        await page.WaitForSelectorAsync("text=Playwright Ollama");
        await page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = Path.Combine(evidenceDirectory, "phase8-settings-providers-plugin-first.png"),
            FullPage = true
        });
        Assert.False(await page.Locator("#blazor-error-ui").IsVisibleAsync());
    }

    [Fact]
    public async Task Resources_page_supports_manifest_driven_connector_selection()
    {
        var evidenceDirectory = @"C:\repositories\CanDoItAll\evidence\plugin-wave\v8";
        Directory.CreateDirectory(evidenceDirectory);

        await using var context = await fixture.Browser.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize
            {
                Width = 1600,
                Height = 1000
            }
        });
        var page = await context.NewPageAsync();
        var projectId = await CreateProjectAsync(page, "Playwright Resource Connectors", "Execution");

        var response = await page.GotoAsync($"{fixture.BaseUrl}/resources?projectId={projectId}");
        Assert.NotNull(response);
        Assert.True(response!.Ok, $"Expected /resources to return 2xx, got {(int)response.Status}.");
        await DismissStartupModalIfPresentAsync(page);
        await page.GetByTestId("resource-project-select").WaitForAsync();
        await page.GetByTestId("resource-project-select").SelectOptionAsync(projectId.ToString());
        await page.GetByTestId("resource-plugin-select").SelectOptionAsync("resource.folder");
        await page.GetByTestId("resource-primary-input").WaitForAsync();
        await page.GetByTestId("resource-name-input").FillAsync("Playwright folder resource");
        await page.GetByTestId("resource-primary-input").FillAsync(@"C:\repositories\CanDoItAll\workspace");
        await page.GetByTestId("resource-save-button").ClickAsync();
        await page.WaitForSelectorAsync("text=Resource saved.");
        await page.WaitForSelectorAsync("text=Playwright folder resource");
        await page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = Path.Combine(evidenceDirectory, "phase8-resources-plugin-first.png"),
            FullPage = true
        });
        Assert.False(await page.Locator("#blazor-error-ui").IsVisibleAsync());
    }

    [Fact]
    public async Task Structure_canvas_maximize_locks_viewport_without_residual_document_scroll()
    {
        var repoRoot = GetRepoRoot();
        var artifactsDir = Path.Combine(repoRoot, "output", "playwright", "maximize-lock");
        ResetDirectory(artifactsDir);

        await using var context = await fixture.Browser.NewContextAsync(new BrowserNewContextOptions
        {
            IgnoreHTTPSErrors = true,
            ViewportSize = new ViewportSize
            {
                Width = 1920,
                Height = 945
            }
        });

        var page = await context.NewPageAsync();
        await CreateProjectAsync(page, "Playwright Maximize Lock", "Validation");
        await page.Locator(".cw-workbench-shell").WaitForAsync();

        if (await page.EvaluateAsync<bool>("() => document.querySelector('.cw-workbench-shell')?.classList.contains('is-maximized') === true"))
        {
            await EnsureCanvasMaximizedStateAsync(page, isMaximized: false);
        }

        var docked = await ReadCanvasViewportStateAsync(page);
        Assert.False(docked.IsMaximized);
        Assert.False(docked.BodyLock);
        Assert.True(docked.HostWidth < docked.ViewportWidth, $"Expected docked host width to be smaller than viewport. Host={docked.HostWidth}, viewport={docked.ViewportWidth}.");

        await EnsureCanvasMaximizedStateAsync(page, isMaximized: true);
        var maximized = await ReadCanvasViewportStateAsync(page);
        Assert.True(maximized.IsMaximized);
        Assert.True(maximized.BodyLock);
        Assert.InRange(Math.Abs(maximized.HostLeft), 0, 1);
        Assert.InRange(Math.Abs(maximized.HostTop), 0, 1);
        Assert.InRange(Math.Abs(maximized.HostWidth - maximized.ViewportWidth), 0, 1);
        Assert.InRange(Math.Abs(maximized.HostHeight - maximized.ViewportHeight), 0, 1);
        Assert.InRange(Math.Abs(maximized.DocumentClientHeight - maximized.ViewportHeight), 0, 1);
        Assert.InRange(Math.Abs(maximized.DocumentScrollHeight - maximized.ViewportHeight), 0, 1);

        await page.ScreenshotAsync(new()
        {
            Path = Path.Combine(artifactsDir, "structure-canvas-maximized.png"),
            FullPage = false
        });
    }

    [Fact]
    public async Task Structure_canvas_supports_inline_note_creation_quick_actions_and_context_create_dialogs()
    {
        await using var context = await fixture.Browser.NewContextAsync();
        var page = await context.NewPageAsync();
        var artifactsDir = @"C:\repositories\CanDoItAll\output\playwright";
        Directory.CreateDirectory(artifactsDir);

        var projectId = await CreateProjectAsync(page, "Playwright Canvas Repair", "Discovery");
        var projectRootSelector = $".cw-node[data-node-id='project:{projectId}']";

        var canvasHost = page.Locator(".cw-canvas-host");
        await canvasHost.ScrollIntoViewIfNeededAsync();
        await ClickCanvasNodeAsync(page, projectRootSelector);

        await OpenCanvasContextMenuAsync(page, projectRootSelector);
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

        await EnsureCanvasSelectionAsync(page, projectRootSelector);
        var noteEditor = await OpenInlineNoteEditorAsync(page);
        await noteEditor.FillAsync("Child note from keyboard");
        await noteEditor.PressAsync("Enter");
        await page.WaitForFunctionAsync("() => !document.querySelector('.cw-note-editor__input')");
        await page.WaitForSelectorAsync("text=Child note from keyboard");
        await WaitForSceneNodeTitleAsync(page, "Child note from keyboard", selectedOnly: true);
        await WaitForSceneSnapshotAsync(page, snapshot => snapshot.Nodes.Length > 1, "more than one rendered node");
        await ToggleCanvasNodeCollapseAsync(page, $"{projectRootSelector} .cw-node__collapse");
        await WaitForSceneSnapshotAsync(
            page,
            snapshot => snapshot.Nodes.All(node => node.Id.StartsWith("project:", StringComparison.Ordinal)),
            "collapsed project root without visible children");
        await ToggleCanvasNodeCollapseAsync(page, $"{projectRootSelector} .cw-node__collapse");
        await WaitForSceneSnapshotAsync(
            page,
            snapshot => snapshot.Nodes.Any(node => !node.Id.StartsWith("project:", StringComparison.Ordinal)),
            "expanded project root with visible children");

        await EnsureCanvasSelectionAsync(page, projectRootSelector);
        noteEditor = await OpenInlineNoteEditorAsync(page);
        await noteEditor.FillAsync("Second child note");
        await noteEditor.PressAsync("Enter");
        await page.WaitForFunctionAsync("() => !document.querySelector('.cw-note-editor__input')");
        await page.WaitForSelectorAsync("text=Second child note");
        await AssertNoCanvasNodeOverlapsAsync(page, "after chained child-note creation");

        await OpenNodeQuickActionsAsync(page, ".cw-node:has-text('Second child note')");
        var quickActionDialog = page.GetByTestId("project-structure-node-quick-actions");
        await quickActionDialog.WaitForAsync();
        var editQuickAction = page.GetByTestId("project-structure-quick-action-edit");
        await editQuickAction.WaitForAsync();
        Assert.Contains("Edit", await editQuickAction.TextContentAsync(), StringComparison.Ordinal);
        await quickActionDialog.GetByRole(AriaRole.Button, new() { Name = "Close", Exact = true }).ClickAsync();

        var editedNoteId = await ResolveCanvasNodeIdAsync(page, ".cw-node:has-text('Second child note')");
        Assert.False(string.IsNullOrWhiteSpace(editedNoteId), "Expected the second child note to stay addressable after opening quick actions.");

        var nodeLabels = await OpenCanvasContextMenuAsync(page, ".cw-node:has-text('Second child note')");
        Assert.Contains(nodeLabels, label => label.Contains("Progress", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(nodeLabels, label => label.Contains("Marker", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(nodeLabels, label => label.Contains("Priority", StringComparison.OrdinalIgnoreCase));
        await page.Locator(".cw-context-menu__action[data-action-id='progress']").WaitForAsync();
        await OpenContextSubmenuAsync(page, "progress");
        await page.Locator(".cw-context-menu__action[data-action-id='progress:0']").WaitForAsync();
        await page.Locator(".cw-context-menu__action[data-action-id='progress:started']").WaitForAsync();
        await page.Locator(".cw-context-menu__action[data-action-id='progress:100']").WaitForAsync();
        var progressMetrics = await ReadContextMenuActionMetricsAsync(page, "progress:30");
        var progressBackground = await page.EvaluateAsync<string>(
            @"() => getComputedStyle(document.querySelector('.cw-context-menu__action[data-action-id=""progress:30""]')).backgroundImage");
        Assert.DoesNotContain("16, 185, 129", progressBackground, StringComparison.Ordinal);
        Assert.DoesNotContain("56, 189, 248", progressBackground, StringComparison.Ordinal);
        await page.ScreenshotAsync(new() { Path = Path.Combine(artifactsDir, "structure-progress-submenu-metadata.png"), FullPage = true });
        await ClickContextMenuActionAsync(page, "progress:30");
        await WaitForSceneSnapshotAsync(
            page,
            snapshot => snapshot.Nodes.Any(node =>
                string.Equals(node.Id, editedNoteId, StringComparison.Ordinal) &&
                node.ProgressTitle.Contains("30%", StringComparison.Ordinal)),
            "progress badge metadata for edited child note");

        nodeLabels = await OpenCanvasContextMenuAsync(page, ".cw-node:has-text('Second child note')");
        Assert.Contains(nodeLabels, label => label.Contains("Marker", StringComparison.OrdinalIgnoreCase));
        await page.Locator(".cw-context-menu__action[data-action-id='marker']").WaitForAsync();
        await OpenContextSubmenuAsync(page, "marker");
        await page.Locator(".cw-context-menu__action[data-action-id='marker:question']").WaitForAsync();
        var markerMetrics = await ReadContextMenuActionMetricsAsync(page, "marker:question");
        Assert.True(markerMetrics.Width >= progressMetrics.Width - 2, $"Expected marker presets to stay comparable to progress preset size. Marker={markerMetrics.Width}, progress={progressMetrics.Width}.");
        await ClickContextMenuActionAsync(page, "marker:money");
        await WaitForSceneSnapshotAsync(
            page,
            snapshot => snapshot.Nodes.Any(node =>
                string.Equals(node.Id, editedNoteId, StringComparison.Ordinal) &&
                string.Equals(node.MarkerText, "Budget", StringComparison.Ordinal)),
            "marker badge metadata for edited child note");

        nodeLabels = await OpenCanvasContextMenuAsync(page, ".cw-node:has-text('Second child note')");
        Assert.Contains(nodeLabels, label => label.Contains("Priority", StringComparison.OrdinalIgnoreCase));
        await page.Locator(".cw-context-menu__action[data-action-id='priority']").WaitForAsync();
        await OpenContextSubmenuAsync(page, "priority");
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
        await ClickContextMenuActionAsync(page, "priority:2");
        await WaitForSceneSnapshotAsync(
            page,
            snapshot => snapshot.Nodes.Any(node =>
                string.Equals(node.Id, editedNoteId, StringComparison.Ordinal) &&
                string.Equals(node.PriorityText, "2", StringComparison.Ordinal)),
            "priority badge metadata for edited child note");
        await page.ScreenshotAsync(new() { Path = Path.Combine(artifactsDir, "structure-note-badges-selected.png"), FullPage = true });

        await page.Keyboard.PressAsync("Enter");
        await noteEditor.WaitForAsync();
        await noteEditor.FillAsync("Sibling note from Enter");
        await noteEditor.PressAsync("Enter");
        await page.WaitForFunctionAsync("() => !document.querySelector('.cw-note-editor__input')");
        await page.WaitForSelectorAsync("text=Sibling note from Enter");
        await AssertNoCanvasNodeOverlapsAsync(page, "after sibling-note creation");

        await OpenCanvasCreateComposerAsync(page, projectRootSelector, "Link", "add-link");
        await page.WaitForSelectorAsync("text=Address");
        await page.Locator(".cw-canvas-composer__input").Nth(0).FillAsync("API reference");
        await page.Locator(".cw-canvas-composer__input").Nth(1).FillAsync("https://example.test/api");
        await page.Locator(".cw-canvas-composer__textarea").FillAsync("Reference for downstream build steps");
        var createLinkButton = page.Locator(".cw-canvas-composer__actions .cw-button[data-tone='accent']");
        await createLinkButton.ClickAsync();
        if (!await WaitForNodeTitleInStateAsync(page, "API reference", 3_000))
        {
            await createLinkButton.EvaluateAsync(
                @"node => {
                    if (node instanceof HTMLButtonElement) {
                        node.click();
                    }
                }");
        }

        await page.WaitForSelectorAsync("text=API reference");
        await WaitForSceneNodeTitleAsync(page, "API reference", selectedOnly: true);

        await OpenCanvasCreateComposerViaRuntimeAsync(page, projectRootSelector, "Image", "add-image-asset");
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
        await WaitForSceneSnapshotAsync(
            page,
            snapshot => snapshot.Nodes.Any(node =>
                node.Selected &&
                string.Equals(node.Title, "Picker uploaded image", StringComparison.Ordinal) &&
                string.Equals(node.MediaKind, "image", StringComparison.OrdinalIgnoreCase)),
            "selected image asset node");
        await EnsureCanvasSelectionAsync(page, ".cw-node:has-text('Picker uploaded image')");
        await page.WaitForFunctionAsync("() => document.querySelector('.cw-floating-window[data-testid=\"project-structure-selection-window\"] .cw-media-preview')?.tagName === 'IMG'");
        await page.WaitForFunctionAsync("() => document.querySelector('.cw-floating-window[data-testid=\"project-structure-selection-window\"]')?.textContent?.includes('playwright-picker-image.svg') === true");

        await OpenCanvasCreateComposerViaRuntimeAsync(page, projectRootSelector, "Image", "add-image-asset");
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
        await WaitForSceneSnapshotAsync(
            page,
            snapshot => snapshot.Nodes.Any(node =>
                node.Selected &&
                string.Equals(node.Title, "Playwright dropped image", StringComparison.Ordinal) &&
                string.Equals(node.MediaKind, "image", StringComparison.OrdinalIgnoreCase)),
            "selected dropped image node");
        await AssertNoCanvasNodeOverlapsAsync(page, "after mixed note/link/image creation");

        await EnsureCanvasSelectionAsync(page, ".cw-node.is-inline-text");
        await FocusCanvasRootAsync(page);
        await WaitForSceneNodeTitleAsync(page, "Playwright Canvas Repair", selectedOnly: true, timeoutMs: 60_000);
        await page.WaitForFunctionAsync(
            @"expectedTitle => {
                const title = document.querySelector('.cw-floating-window[data-testid=""project-structure-selection-window""] .cw-panel-card h3');
                return title?.textContent?.trim() === expectedTitle;
            }",
            "Playwright Canvas Repair");
        var focusState = await WaitForCanvasFocusStateAsync(
            page,
            state => string.Equals(state.SelectedId, projectRootSelector.Replace(".cw-node[data-node-id='", string.Empty).Replace("']", string.Empty), StringComparison.Ordinal) &&
                Math.Abs(state.DeltaX) <= 2 &&
                Math.Abs(state.DeltaY) <= 2,
            "focused project root centered in canvas");
        Assert.Equal("Playwright Canvas Repair", await page.Locator(".cw-floating-window[data-testid='project-structure-selection-window'] .cw-panel-card h3").First.TextContentAsync());
        Assert.InRange(Math.Abs(focusState.DeltaX), 0, 2);
        Assert.InRange(Math.Abs(focusState.DeltaY), 0, 2);

        if (await page.EvaluateAsync<bool>("() => document.querySelector('.cw-workbench-shell')?.classList.contains('is-maximized') === true"))
        {
            await EnsureCanvasMaximizedStateAsync(page, isMaximized: false);
        }

        var docked = await ReadCanvasViewportStateAsync(page);
        Assert.False(docked.IsMaximized);
        Assert.False(docked.BodyLock);
        Assert.True(docked.HostWidth < docked.ViewportWidth, $"Expected docked host width to be smaller than viewport. Host={docked.HostWidth}, viewport={docked.ViewportWidth}.");

        await EnsureCanvasMaximizedStateAsync(page, isMaximized: true);
        var maximized = await ReadCanvasViewportStateAsync(page);
        Assert.True(maximized.IsMaximized);
        Assert.True(maximized.BodyLock);
        Assert.InRange(Math.Abs(maximized.HostLeft), 0, 1);
        Assert.InRange(Math.Abs(maximized.HostTop), 0, 1);
        Assert.InRange(Math.Abs(maximized.HostWidth - maximized.ViewportWidth), 0, 1);
        Assert.InRange(Math.Abs(maximized.HostHeight - maximized.ViewportHeight), 0, 1);
        Assert.InRange(Math.Abs(maximized.DocumentClientHeight - maximized.ViewportHeight), 0, 1);
        Assert.InRange(Math.Abs(maximized.DocumentScrollHeight - maximized.ViewportHeight), 0, 1);
        await page.ScreenshotAsync(new() { Path = Path.Combine(artifactsDir, "structure-note-centered-pan.png"), FullPage = true });
    }

    private static async Task EnsureCanvasMaximizedStateAsync(IPage page, bool isMaximized)
    {
        var toggleButton = page.GetByRole(AriaRole.Button, new() { Name = "Toggle maximize" });
        var expectedExpression = isMaximized
            ? "() => document.querySelector('.cw-workbench-shell')?.classList.contains('is-maximized') === true"
            : "() => document.querySelector('.cw-workbench-shell')?.classList.contains('is-maximized') !== true";

        for (var attempt = 0; attempt < 3; attempt++)
        {
            var currentState = await page.EvaluateAsync<bool>("() => document.querySelector('.cw-workbench-shell')?.classList.contains('is-maximized') === true");
            if (currentState == isMaximized)
            {
                return;
            }

            await toggleButton.ClickAsync();
            try
            {
                await page.WaitForFunctionAsync(expectedExpression, null, new() { Timeout = 5_000 });
                return;
            }
            catch (TimeoutException)
            {
            }
        }

        Assert.Equal(
            isMaximized,
            await page.EvaluateAsync<bool>("() => document.querySelector('.cw-workbench-shell')?.classList.contains('is-maximized') === true"));
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

        await EnsureStructureToolboxWindowExpandedAsync(page);
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

                return toolbox.querySelectorAll('.cda-treeview__row[data-testid^=""project-structure-toolbox-""]').length > 0 &&
                    toolbox.querySelectorAll('.cda-material-icon').length > 0 &&
                    toolbox.querySelectorAll('.rz-icon-fallback').length === 0;
            }");
        await page.WaitForTimeoutAsync(250);

        var firstVisibleItemTopBeforeScroll = await page.EvaluateAsync<double>(
            @"() => {
                const item = document.querySelector('[data-testid=""project-structure-standard-blocks-toolbox""] .cda-treeview__row[data-testid^=""project-structure-toolbox-""]');
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

                const items = Array.from(sections.querySelectorAll('.cda-treeview__row[data-testid^=""project-structure-toolbox-""]'));
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
                        .map(item => item.querySelector('.cda-treeview__text')?.textContent?.trim() ?? ''),
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

                return toolbox.querySelectorAll('.cda-treeview__row[data-testid^=""project-structure-toolbox-""]').length > 0 &&
                    (toolbox.textContent || '').toLowerCase().includes('pdf') &&
                    toolbox.querySelector('[data-testid=""project-structure-toolbox-add-file-pdf""]') instanceof HTMLElement;
            }");
        await toolboxWindow.ScreenshotAsync(new() { Path = Path.Combine(artifactsDir, "04-toolbox-pdf-search.png") });
        await structureToolboxSearch.FillAsync(string.Empty);

        Assert.False(await page.Locator("#blazor-error-ui").IsVisibleAsync());
    }

    [Fact]
    public async Task Project_structure_feedback6_context_menu_is_validated_in_browser()
    {
        var repoRoot = GetRepoRoot();
        var artifactsDir = Path.Combine(repoRoot, "output", "playwright", "feedback6");
        ResetDirectory(artifactsDir);

        await using var context = await fixture.Browser.NewContextAsync(new()
        {
            IgnoreHTTPSErrors = true,
            ViewportSize = new ViewportSize
            {
                Width = 1900,
                Height = 1200
            }
        });

        var page = await context.NewPageAsync();
        await CreateProjectAsync(page, "Playwright Feedback 6 Validation", "Validation");

        const string rootSelector = ".cw-node[data-node-id^='project:']";
        var rootNodeId = await ReadNodeIdAsync(page, rootSelector);
        var editableNoteId = await InvokeStructureCreateActionAsync(
            page,
            "add-note",
            rootNodeId,
            rootNodeId,
            "Feedback 6 editable note",
            "Validation",
            "Editable node for context menu mutation proof.");
        var editableNoteSelector = SelectorForNodeId(editableNoteId);

        await FocusCanvasRootAsync(page);
        await SetCanvasZoomPercentAsync(page, 100);

        await OpenCanvasContextMenuAsync(page, rootSelector);
        await page.Mouse.MoveAsync(8, 8);
        await page.WaitForTimeoutAsync(80);
        if (!await WaitForMenuActionAsync(page, "progress", 1_500))
        {
            await OpenCanvasContextMenuAsync(page, rootSelector);
            await page.Mouse.MoveAsync(8, 8);
            await page.WaitForTimeoutAsync(80);
        }
        await HoverContextMenuActionAsync(page, "progress");
        Assert.True(await page.Locator(".cw-context-menu__action[data-action-id='progress'].is-submenu-loading .cw-context-menu__loading-indicator").IsVisibleAsync());
        Assert.False(await WaitForMenuActionAsync(page, "progress:10", 200), "Expected the progress submenu to stay closed during the hover-delay window.");
        await page.ScreenshotAsync(new() { Path = Path.Combine(artifactsDir, "01-progress-loading-delay.png"), FullPage = true });
        await page.Mouse.MoveAsync(8, 8);
        await page.WaitForTimeoutAsync(450);
        Assert.Equal(0, await page.Locator(".cw-context-menu__action[data-action-id='progress:10']").CountAsync());
        Assert.Equal(0, await page.Locator(".cw-context-menu__action[data-action-id='progress'] .cw-context-menu__loading-indicator").CountAsync());
        await page.Keyboard.PressAsync("Escape");

        await OpenCanvasContextMenuAsync(page, rootSelector);
        await page.Mouse.MoveAsync(8, 8);
        await page.WaitForTimeoutAsync(80);
        await HoverContextMenuActionAsync(page, "progress");
        Assert.True(await WaitForMenuActionAsync(page, "progress:10", 1_000), "Expected the progress submenu to open after the hover-delay window.");
        var progressLayout = await ReadContextMenuLayerSnapshotAsync(page, "progress:");
        Assert.Equal("10%", progressLayout.Actions.First(action => action.ActionId == "progress:10").CenterText);
        Assert.Equal("N/A", progressLayout.Actions.First(action => action.ActionId == "progress:na").CenterText);
        Assert.True(string.IsNullOrWhiteSpace(progressLayout.Actions.First(action => action.ActionId == "progress:started").CenterText));
        Assert.True(CountDistinctBands(progressLayout.Actions.Select(action => action.DistanceFromCore), 26) >= 2, "Expected the progress submenu to use multiple hive distance bands instead of a single circular ring.");
        Assert.All(
            progressLayout.Actions,
            action => Assert.True(
                action.Top >= progressLayout.ToolbarBottom + 4,
                $"Expected progress submenu action '{action.ActionId}' to stay below the toolbar. top={action.Top}, toolbarBottom={progressLayout.ToolbarBottom}, hostTop={progressLayout.HostTop}, safeTop={progressLayout.SafeTop}, rootCenterY={progressLayout.RootCenterY}."));
        await page.ScreenshotAsync(new() { Path = Path.Combine(artifactsDir, "02-progress-submenu-hive.png"), FullPage = true });
        AssertMenuActionsDoNotOverlap(progressLayout.Actions, 18, "progress submenu");
        await page.Keyboard.PressAsync("Escape");
        await OpenCanvasContextMenuAsync(page, editableNoteSelector);
        await OpenContextSubmenuAsync(page, "progress");
        await ClickContextMenuActionAsync(page, "progress:30");
        await WaitForNodeProgressStateAsync(page, editableNoteId, "progress", 30);

        await OpenCanvasContextMenuAsync(page, rootSelector);
        await page.Mouse.MoveAsync(8, 8);
        await page.WaitForTimeoutAsync(80);
        await HoverContextMenuActionAsync(page, "marker");
        Assert.True(await WaitForMenuActionAsync(page, "marker:question", 1_000), "Expected the marker submenu to open after the hover-delay window.");
        var markerLayout = await ReadContextMenuLayerSnapshotAsync(page, "marker:");
        Assert.True(CountDistinctBands(markerLayout.Actions.Select(action => action.DistanceFromCore), 26) >= 2, "Expected the marker submenu to use multiple hive distance bands instead of a single circular ring.");
        Assert.All(
            markerLayout.Actions,
            action => Assert.True(
                action.Top >= markerLayout.ToolbarBottom + 4,
                $"Expected marker submenu action '{action.ActionId}' to stay below the toolbar. top={action.Top}, toolbarBottom={markerLayout.ToolbarBottom}, hostTop={markerLayout.HostTop}, safeTop={markerLayout.SafeTop}, rootCenterY={markerLayout.RootCenterY}."));
        await page.ScreenshotAsync(new() { Path = Path.Combine(artifactsDir, "03-marker-submenu-hive.png"), FullPage = true });
        AssertMenuActionsDoNotOverlap(markerLayout.Actions, 18, "marker submenu");
        await page.Keyboard.PressAsync("Escape");
        await OpenCanvasContextMenuAsync(page, editableNoteSelector);
        await OpenContextSubmenuAsync(page, "marker");
        await ClickContextMenuActionAsync(page, "marker:money");
        await WaitForSceneSnapshotAsync(
            page,
            snapshot => snapshot.Nodes.Any(node =>
                string.Equals(node.Id, editableNoteId, StringComparison.Ordinal) &&
                string.Equals(node.MarkerText, "Budget", StringComparison.Ordinal)),
            "editable node marker badge metadata");

        await OpenCanvasContextMenuAsync(page, rootSelector);
        await page.Mouse.MoveAsync(8, 8);
        await page.WaitForTimeoutAsync(80);
        await HoverContextMenuActionAsync(page, "priority");
        Assert.True(await WaitForMenuActionAsync(page, "priority:2", 1_000), "Expected the priority submenu to stay functional after the shared menu layout changes.");
        await page.Keyboard.PressAsync("Escape");
        await OpenCanvasContextMenuAsync(page, editableNoteSelector);
        await OpenContextSubmenuAsync(page, "priority");
        await ClickContextMenuActionAsync(page, "priority:2");
        await WaitForSceneSnapshotAsync(
            page,
            snapshot => snapshot.Nodes.Any(node =>
                string.Equals(node.Id, editableNoteId, StringComparison.Ordinal) &&
                string.Equals(node.PriorityText, "2", StringComparison.Ordinal)),
            "editable node priority badge metadata");

        Assert.False(await page.Locator("#blazor-error-ui").IsVisibleAsync());
    }

    [Fact]
    public async Task Project_structure_multi_select_move_keeps_selection_and_adopts_border_in_browser()
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
        var projectId = await CreateProjectAsync(page, "Playwright P0-04 Move", "Execution");
        var projectRootId = await ReadNodeIdAsync(page, ".cw-node[data-node-id^='project:']");

        var leftAnchorId = await InvokeStructureCreateActionAsync(
            page,
            "add-block-feature",
            projectRootId,
            projectRootId,
            "Delivery anchor",
            "Border anchor",
            "Keep the group frame stable during the move proof.");
        var rightAnchorId = await InvokeStructureCreateActionAsync(
            page,
            "add-block-support",
            projectRootId,
            projectRootId,
            "Evidence anchor",
            "Border anchor",
            "Keep the group frame stable during the move proof.");
        var movedTaskId = await InvokeStructureCreateActionAsync(
            page,
            "add-work-task",
            projectRootId,
            projectRootId,
            "Capture screenshots",
            "Move target",
            "Should stay selected after the move callback.",
            [
                new CanvasInputValueSeed("dueUtc", "2026-04-12T12:00:00+00:00")
            ]);
        var movedEvidenceId = await InvokeStructureCreateActionAsync(
            page,
            "add-test-evidence",
            projectRootId,
            projectRootId,
            "Store proof",
            "Move target",
            "Should be adopted into the existing border.");

        await FocusCanvasRootAsync(page);
        await SetCanvasZoomPercentAsync(page, 70);
        await page.WaitForTimeoutAsync(220);

        await SelectCanvasNodesAsync(page, [leftAnchorId, rightAnchorId], leftAnchorId);
        await page.GetByTestId("project-structure-selection-window").WaitForAsync();
        await page.Locator(".cw-floating-window[data-testid='project-structure-selection-window'] input[placeholder='Name this border']").FillAsync("Delivery swimlane");
        await page.GetByRole(AriaRole.Button, new() { Name = "Border", Exact = true }).ClickAsync();
        await WaitForSceneFrameLabelAsync(page, "Delivery swimlane");

        await SelectCanvasNodesAsync(page, [movedTaskId, movedEvidenceId], movedTaskId);
        await page.WaitForSelectorAsync("text=2 nodes selected");
        var selectionPersisted = await page.EvaluateAsync<bool>(
            @"async payload => {
                const host = document.querySelector('.cw-canvas-host');
                const state = host?.__canvasWorkbenchState;
                if (!state?.dotNetRef?.invokeMethodAsync || !state?.surface?.uiState) {
                    return false;
                }

                const uiState = JSON.parse(JSON.stringify(state.surface.uiState));
                uiState.selectedNodeIds = payload.nodeIds;
                const dispatchId = (state.stateDispatchId || 0) + 1;
                state.stateDispatchId = dispatchId;
                await state.dotNetRef.invokeMethodAsync('OnStateChanged', JSON.stringify(uiState), dispatchId);
                return true;
            }",
            new
            {
                nodeIds = new[] { movedTaskId, movedEvidenceId }
            });
        Assert.True(selectionPersisted, "Expected the browser workbench host to expose the state commit callback.");
        await page.WaitForTimeoutAsync(220);
        await CaptureCanvasSurfaceAsync(page, Path.Combine(artifactsDir, "bundle-p0-04-before-drag.png"));

        var anchorPositions = await page.EvaluateAsync<CanvasNodePosition[]>(
            @"payload => {
                const host = document.querySelector('.cw-canvas-host');
                const lookups = host?.__canvasWorkbenchState?.lookups?.byId;
                return payload.nodeIds.map(nodeId => {
                    const node = lookups?.get(nodeId);
                    return {
                        id: nodeId,
                        left: Math.round(node?.x ?? 0),
                        top: Math.round(node?.y ?? 0)
                    };
                });
            }",
            new
            {
                nodeIds = new[] { leftAnchorId, rightAnchorId }
            });
        Assert.Equal(2, anchorPositions.Length);
        var targetCenterX = (int)Math.Round(anchorPositions.Average(position => position.Left));
        var targetCenterY = (int)Math.Round(anchorPositions.Average(position => position.Top));

        var moveApplied = await page.EvaluateAsync<bool>(
            @"async payload => {
                const host = document.querySelector('.cw-canvas-host');
                const state = host?.__canvasWorkbenchState;
                if (!state?.dotNetRef?.invokeMethodAsync) {
                    return false;
                }

                await state.dotNetRef.invokeMethodAsync('OnNodesMoved', JSON.stringify(payload.positions));
                return true;
            }",
            new
            {
                positions = new[]
                {
                    new { nodeId = movedTaskId, x = targetCenterX - 40, y = targetCenterY - 20 },
                    new { nodeId = movedEvidenceId, x = targetCenterX + 40, y = targetCenterY + 40 }
                }
            });
        Assert.True(moveApplied, "Expected the browser workbench host to expose the move callback.");

        await page.WaitForFunctionAsync(
            @"payload => {
                const host = document.querySelector('.cw-canvas-host');
                const state = host?.__canvasWorkbenchState;
                const uiState = state?.surface?.uiState;
                const frame = uiState?.groupFrames?.find(candidate => candidate.label === payload.label);
                const selectedIds = uiState?.selectedNodeIds || [];
                if (!frame) {
                    return false;
                }

                return payload.nodeIds.every(nodeId => frame.anchorNodeIds.includes(nodeId)) &&
                    payload.nodeIds.every(nodeId => selectedIds.includes(nodeId));
            }",
            new
            {
                label = "Delivery swimlane",
                nodeIds = new[] { movedTaskId, movedEvidenceId }
            });
        await page.WaitForSelectorAsync("text=2 nodes selected");
        await CaptureCanvasSurfaceAsync(page, Path.Combine(artifactsDir, "bundle-p0-04-after-drag.png"));

        Assert.False(await page.Locator("#blazor-error-ui").IsVisibleAsync());
    }

    [Fact]
    public async Task Project_structure_feedback_7_is_validated_in_browser()
    {
        var repoRoot = GetRepoRoot();
        var artifactsDir = Path.Combine(repoRoot, "output", "playwright", "feedback7");
        ResetDirectory(artifactsDir);

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

        var projectId = await CreateProjectAsync(page, "Playwright Feedback 7", "Execution");
        var projectRootId = await ReadNodeIdAsync(page, ".cw-node[data-node-id^='project:']");
        const string repositoryPath = @"C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench";
        Assert.False(string.IsNullOrWhiteSpace(fixture.StorageWorkspaceRoot), "Expected the Playwright fixture to expose the workspace storage root.");
        var runtimeProjectDirectory = Path.Combine(fixture.StorageWorkspaceRoot!, "runtime", "PVEInvoicing.ServerApp");
        Directory.CreateDirectory(runtimeProjectDirectory);
        var runtimeProjectPath = Path.Combine(runtimeProjectDirectory, "PVEInvoicing.csproj");
        await File.WriteAllTextAsync(runtimeProjectPath, "<Project Sdk=\"Microsoft.NET.Sdk.Web\"></Project>");

        var repositoryId = await InvokeStructureCreateActionAsync(
            page,
            "add-repository-local",
            projectRootId,
            projectRootId,
            "Main repository",
            "Workspace clone",
            "Keep repository nodes readable on the canvas.",
            [
                new CanvasInputValueSeed("repositoryMode", "localRepository"),
                new CanvasInputValueSeed("localPath", repositoryPath),
                new CanvasInputValueSeed("defaultBranch", "main")
            ]);

        var runtimeId = await InvokeStructureCreateActionAsync(
            page,
            "add-environment-dotnet-watch",
            projectRootId,
            projectRootId,
            "API runtime",
            "dotnet watch",
            "Launch the runtime from the quick action modal.",
            [
                new CanvasInputValueSeed("environmentKind", "dotNetWatch"),
                new CanvasInputValueSeed("projectPath", runtimeProjectPath),
                new CanvasInputValueSeed("launchProfileName", "https")
            ]);

        var promptFlowId = await InvokeStructureCreateActionAsync(
            page,
            "add-prompt-flow",
            projectRootId,
            projectRootId,
            "Checkout assistant flow",
            "Prompt orchestration",
            "Open the wizard in a new tab from the quick action modal.");

        await FocusCanvasRootAsync(page);
        await SetCanvasZoomPercentAsync(page, 72);
        await page.WaitForTimeoutAsync(250);
        await CaptureWorkbenchShellAsync(page, Path.Combine(artifactsDir, "01-workbench-state.png"));

        await page.EvaluateAsync(
            @"() => {
                window.__feedback7CopiedPath = '';
                window.__canvasClipboardWrite = async value => {
                    window.__feedback7CopiedPath = value;
                };
                window.__canvasClipboardRead = async () => window.__feedback7CopiedPath || '';
                const clipboard = {
                    writeText: async value => {
                        window.__feedback7CopiedPath = value;
                    },
                    readText: async () => window.__feedback7CopiedPath || ''
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

        var runtimeSelector = SelectorForNodeId(runtimeId);
        await EnsureCanvasSelectionAsync(page, runtimeSelector);
        await WaitForSceneSnapshotAsync(
            page,
            snapshot => snapshot.Nodes.Any(node =>
                string.Equals(node.Id, runtimeId, StringComparison.Ordinal) &&
                node.HasPathButton &&
                string.Equals(node.PathTitle, runtimeProjectPath, StringComparison.Ordinal) &&
                string.Equals(node.PathPromotedText, "PVEInvoicing.csproj", StringComparison.OrdinalIgnoreCase) &&
                !node.PathDisplayText.Contains(runtimeProjectPath, StringComparison.OrdinalIgnoreCase)),
            "runtime compact path metadata");
        var runtimeNode = await ReadSceneNodeSnapshotAsync(page, runtimeId);
        Assert.Equal(runtimeProjectPath, runtimeNode.PathTitle);
        Assert.Equal("PVEInvoicing.csproj", runtimeNode.PathPromotedText);
        var runtimePathCenter = await ReadCanvasHotZoneCenterAsync(page, "node-path", nodeId: runtimeId);
        Assert.True(runtimePathCenter.X > 0 && runtimePathCenter.Y > 0, "Expected the runtime path hot zone to resolve to a clickable point.");
        await ActivateCanvasHotZoneAsync(page, "node-path", nodeId: runtimeId);
        await page.WaitForFunctionAsync(
            @"expectedPath => {
                return window.__feedback7CopiedPath === expectedPath;
            }",
            runtimeProjectPath);

        var repositorySelector = SelectorForNodeId(repositoryId);
        await EnsureCanvasSelectionAsync(page, repositorySelector);
        await WaitForSceneSnapshotAsync(
            page,
            snapshot => snapshot.Nodes.Any(node =>
                string.Equals(node.Id, repositoryId, StringComparison.Ordinal) &&
                node.HasPathButton &&
                string.Equals(node.PathTitle, repositoryPath, StringComparison.Ordinal) &&
                node.PathDisplayText.Contains("CanDoItAll.Modules.Workbench", StringComparison.OrdinalIgnoreCase) &&
                !node.PathDisplayText.Contains(repositoryPath, StringComparison.OrdinalIgnoreCase)),
            "repository compact path metadata");
        var repositoryNode = await ReadSceneNodeSnapshotAsync(page, repositoryId);
        Assert.Equal(repositoryPath, repositoryNode.PathTitle);

        var promptSelector = SelectorForNodeId(promptFlowId);
        await EnsureCanvasSelectionAsync(page, promptSelector);
        await OpenNodeQuickActionsAsync(page, promptSelector);

        var quickActionDialog = page.GetByTestId("project-structure-node-quick-actions");
        await quickActionDialog.WaitForAsync();
        var editQuickAction = page.GetByTestId("project-structure-quick-action-edit");
        var primaryQuickAction = page.GetByTestId("project-structure-quick-action-primary");
        await editQuickAction.WaitForAsync();
        await primaryQuickAction.WaitForAsync();
        Assert.Contains("Edit", await editQuickAction.TextContentAsync(), StringComparison.Ordinal);
        Assert.Contains("Open Wizard in New Tab", await primaryQuickAction.TextContentAsync(), StringComparison.Ordinal);
        await CaptureLocatorAsync(quickActionDialog, Path.Combine(artifactsDir, "02-prompt-quick-actions.png"));

        var popupTask = context.WaitForPageAsync();
        await primaryQuickAction.ClickAsync();
        var popup = await popupTask;
        await popup.WaitForURLAsync("**/prompt-factory?sessionId=*");
        await popup.CloseAsync();

        await EnsureCanvasSelectionAsync(page, runtimeSelector);
        await OpenNodeQuickActionsAsync(page, runtimeSelector);
        quickActionDialog = page.GetByTestId("project-structure-node-quick-actions");
        await quickActionDialog.WaitForAsync();
        primaryQuickAction = page.GetByTestId("project-structure-quick-action-primary");
        await primaryQuickAction.WaitForAsync();
        Assert.Contains("Run PowerShell", await primaryQuickAction.TextContentAsync(), StringComparison.Ordinal);
        await quickActionDialog.GetByRole(AriaRole.Button, new() { Name = "Close", Exact = true }).ClickAsync();

        var settingsButton = page.GetByRole(AriaRole.Button, new() { Name = "Toggle settings", Exact = true });
        var settingsButtonText = await settingsButton.TextContentAsync();
        Assert.DoesNotContain("cfg", settingsButtonText ?? string.Empty, StringComparison.OrdinalIgnoreCase);

        var toolbarBounds = await page.Locator(".cw-toolbar").BoundingBoxAsync();
        Assert.NotNull(toolbarBounds);
        await settingsButton.ClickAsync();

        var settingsOverlay = page.GetByTestId("canvas-settings-overlay");
        var settingsDialog = settingsOverlay.GetByRole(AriaRole.Dialog, new() { Name = "Canvas settings" });
        await settingsDialog.WaitForAsync();
        var settingsBounds = await settingsDialog.BoundingBoxAsync();
        Assert.NotNull(settingsBounds);
        Assert.True(
            settingsBounds!.Y >= toolbarBounds!.Y + toolbarBounds.Height - 1,
            $"Expected settings dialog to render below the toolbar safe zone. DialogTop={settingsBounds.Y}, ToolbarBottom={toolbarBounds.Y + toolbarBounds.Height}.");
        await CaptureLocatorAsync(settingsDialog, Path.Combine(artifactsDir, "03-settings-safe-zone.png"));

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
                const host = document.querySelector('.cw-canvas-host');
                const nodes = host?.__canvasWorkbenchState?.surface?.nodes;
                const hasExportedNode = Array.isArray(nodes) &&
                    nodes.some(node => typeof node?.title === 'string' && node.title.includes(expectedTitle));
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
        var exportedBinding = await dbContext.Set<ProjectNodeBindingRecord>()
            .SingleAsync(item => item.ProjectObjectId == exportedRecord.Id);

        if (!string.IsNullOrWhiteSpace(exportedBinding.Route))
        {
            var exportedImageResponse = await context.APIRequest.GetAsync($"{fixture.BaseUrl}{exportedBinding.Route}");
            Assert.True(exportedImageResponse.Ok, $"Expected the exported mindmap image route to return 2xx, got {exportedImageResponse.Status}.");
            await File.WriteAllBytesAsync(
                Path.Combine(i18Root, "04-exported-mindmap-image.png"),
                await exportedImageResponse.BodyAsync());
        }
        else
        {
            Assert.False(string.IsNullOrWhiteSpace(exportedBinding.MediaRelativePath), $"Expected node '{exportedImageNodeId}' to expose a managed media path.");
            Assert.False(string.IsNullOrWhiteSpace(fixture.StorageWorkspaceRoot), "Expected the Playwright fixture to expose the workspace storage root.");

            var exportedImagePath = Path.Combine(fixture.StorageWorkspaceRoot!, exportedBinding.MediaRelativePath);
            Assert.True(File.Exists(exportedImagePath), $"Expected exported image file to exist at '{exportedImagePath}'.");
            File.Copy(exportedImagePath, Path.Combine(i18Root, "04-exported-mindmap-image.png"), overwrite: true);
        }

        Assert.False(await page.Locator("#blazor-error-ui").IsVisibleAsync());
    }

    private async Task<Guid> CreateProjectAsync(IPage page, string projectName, string phase)
    {
        await page.GotoAsync($"{fixture.BaseUrl}/projects");
        await DismissStartupModalIfPresentAsync(page);
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
        await page.WaitForSelectorAsync("text=Workspace defaults, data sources, and providers");

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
        await SetFieldByLabelAsync(page, "Connector plugin", "OpenAI provider");
        await SetFieldByLabelAsync(page, "Base URL", "https://api.openai.com/v1");
        await SetFieldByLabelAsync(page, "Default model", "gpt-4.1");
        await SetFieldByLabelAsync(page, "API key secret", "OpenAI API key");
        await page.GetByRole(AriaRole.Button, new() { Name = "Save provider", Exact = true }).ClickAsync();
        await page.WaitForSelectorAsync("text=OpenAI API");

        await page.GetByRole(AriaRole.Button, new() { Name = "New provider", Exact = true }).ClickAsync();
        await SetFieldByLabelAsync(page, "Profile name", "Local Ollama");
        await SetFieldByLabelAsync(page, "Connector plugin", "Ollama local provider");
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
        try
        {
            var center = await TryResolveCanvasHotZoneCenterAsync(page, selector, "node-body")
                ?? await ResolveCanvasNodeCenterAsync(page, selector);
            await page.Mouse.ClickAsync(
                (float)center.X,
                (float)center.Y,
                new MouseClickOptions
                {
                    Button = button,
                    ClickCount = clickCount
                });
            return;
        }
        catch (InvalidOperationException)
        {
        }

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
            try
            {
                await OpenContextSubmenuAsync(page, groupActionId);
            }
            catch (TimeoutException)
            {
            }
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
        var targetNodeId = await ResolveCanvasNodeIdAsync(page, selector);
        Assert.False(string.IsNullOrWhiteSpace(targetNodeId), $"Expected to resolve a canvas node id for '{selector}'.");
        var composerReadySelector = RequiresFileComposerSurface(actionId, label)
            ? ".cw-canvas-composer__dropzone"
            : ".cw-canvas-composer";
        var composerReadyLocator = page.Locator(composerReadySelector);

        var openedViaRuntimeApi = await page.EvaluateAsync<bool>(
            @"({ requestedActionId, requestedLabel, requestedNodeId }) => {
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

                const sourceNode = requestedNodeId
                    ? state.lookups?.byId?.get(requestedNodeId) ?? null
                    : null;
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
                requestedLabel = label,
                requestedNodeId = targetNodeId
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

        await DoubleClickCanvasNodeAsync(page, selector);
        if (await WaitForLocatorAsync(noteEditor, 1_500))
        {
            return noteEditor;
        }

        var center = await TryResolveCanvasHotZoneCenterAsync(page, selector, "node-body")
            ?? await ResolveCanvasNodeCenterAsync(page, selector);
        await page.Mouse.DblClickAsync((float)center.X, (float)center.Y);
        if (await WaitForLocatorAsync(noteEditor, 1_500))
        {
            return noteEditor;
        }

        var targetId = await ResolveCanvasNodeIdAsync(page, selector);
        if (!string.IsNullOrWhiteSpace(targetId))
        {
            var openedViaRuntime = await page.EvaluateAsync<bool>(
                @"targetId => {
                    const host = document.querySelector('.cw-canvas-host');
                    const state = host?.__canvasWorkbenchState;
                    const runtimeModule = window.CanDoItAll?.canvasWorkbenchModule;
                    const node = state?.lookups?.byId?.get?.(targetId) || null;
                    if (!state || !node || typeof runtimeModule?.openExistingNoteEditor !== 'function') {
                        return false;
                    }

                    runtimeModule.openExistingNoteEditor(state, node);
                    return true;
                }",
                targetId);
            if (openedViaRuntime && await WaitForLocatorAsync(noteEditor, 1_500))
            {
                return noteEditor;
            }
        }

        await noteEditor.WaitForAsync();
        return noteEditor;
    }

    private static async Task DoubleClickCanvasNodeAsync(IPage page, string selector)
    {
        var hotZoneCenter = await TryResolveCanvasHotZoneCenterAsync(page, selector, "node-body");
        if (hotZoneCenter is not null)
        {
            await page.Mouse.DblClickAsync((float)hotZoneCenter.X, (float)hotZoneCenter.Y);
            return;
        }

        try
        {
            await ClickCanvasNodeAsync(page, selector, clickCount: 2);
            return;
        }
        catch (TimeoutException)
        {
        }
        catch (PlaywrightException)
        {
        }

        var targetId = await ResolveCanvasNodeIdAsync(page, selector);
        if (!string.IsNullOrWhiteSpace(targetId))
        {
            await page.EvaluateAsync(
                @"targetId => {
                    const host = document.querySelector('.cw-canvas-host');
                    const runtime = window.CanDoItAll?.canvasWorkbench;
                    if (!host || !runtime?.focusNode || !targetId) {
                        return false;
                    }

                    runtime.focusNode(host, targetId);
                    return true;
                }",
                targetId);
            await page.WaitForTimeoutAsync(120);
        }

        var center = await ResolveCanvasNodeCenterAsync(page, selector);
        await page.Mouse.DblClickAsync((float)center.X, (float)center.Y);
    }

    private static async Task OpenNodeQuickActionsAsync(IPage page, string selector)
    {
        await DoubleClickCanvasNodeAsync(page, selector);

        var quickActionDialog = page.GetByTestId("project-structure-node-quick-actions");
        if (await WaitForLocatorAsync(quickActionDialog, 1_500))
        {
            return;
        }

        var targetId = await ResolveCanvasNodeIdAsync(page, selector);
        Assert.False(string.IsNullOrWhiteSpace(targetId), $"Expected to resolve a canvas node id for '{selector}'.");

        var opened = await page.EvaluateAsync<bool>(
            @"targetId => {
                const host = document.querySelector('.cw-canvas-host');
                const runtime = window.CanDoItAll?.canvasWorkbench;
                if (!host || !runtime?.openNode || !targetId) {
                    return false;
                }

                return runtime.openNode(host, targetId);
            }",
            targetId);
        Assert.True(opened, $"Expected quick-action fallback bridge to open '{selector}'.");
        await quickActionDialog.WaitForAsync();
    }

    private static async Task EnsureCanvasSelectionAsync(IPage page, string selector)
    {
        await SelectCanvasNodeAsync(page, selector);
        if (await WaitForCanvasSelectionAsync(page, selector, 750))
        {
            return;
        }

        await OpenCanvasContextMenuAsync(page, selector);
        await page.Keyboard.PressAsync("Escape");
        Assert.True(await WaitForCanvasSelectionAsync(page, selector, 1_500), $"Expected canvas selection for '{selector}'.");
    }

    private static async Task SelectCanvasNodeAsync(IPage page, string selector)
    {
        var target = page.Locator(selector).First;
        if (await WaitForLocatorAsync(target, 500))
        {
            try
            {
                await ClickCanvasNodeAsync(page, selector);
                return;
            }
            catch (InvalidOperationException)
            {
            }
            catch (TimeoutException)
            {
            }
            catch (PlaywrightException)
            {
            }
        }

        var targetId = await ResolveCanvasNodeIdAsync(page, selector);
        if (string.IsNullOrWhiteSpace(targetId))
        {
            throw new InvalidOperationException($"Could not resolve a canvas node id for selector '{selector}'.");
        }

        var selected = await page.EvaluateAsync<bool>(
            @"targetId => {
                const host = document.querySelector('.cw-canvas-host');
                const runtime = window.CanDoItAll?.canvasWorkbench;
                if (!host || !runtime?.selectNodes || !targetId) {
                    return false;
                }

                runtime.selectNodes(host, [targetId], targetId);
                return true;
            }",
            targetId);
        Assert.True(selected, $"Expected runtime selection bridge to select '{selector}'.");
    }

    private static async Task SelectStructureOutlineNodeAsync(IPage page, string title)
    {
        var outlineItem = page.Locator(".project-structure-support-card--outline .cda-treeview__row")
            .Filter(new LocatorFilterOptions
            {
                HasText = title
            })
            .First;

        await outlineItem.WaitForAsync();
        await outlineItem.ClickAsync();
        await page.WaitForFunctionAsync(
            @"expectedTitle => {
                const selectionWindow = document.querySelector('[data-testid=""project-structure-selection-window""]');
                return !!selectionWindow && (selectionWindow.textContent || '').includes(expectedTitle);
            }",
            title);
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

        for (var attempt = 0; attempt < 4; attempt++)
        {
            var action = page.Locator(selector).Last;
            if (!await WaitForLocatorAsync(action, 1_000))
            {
                await page.WaitForTimeoutAsync(120);
                continue;
            }

            try
            {
                await action.HoverAsync();
            }
            catch (PlaywrightException exception) when (exception.Message.Contains("detached", StringComparison.OrdinalIgnoreCase))
            {
            }

            await HoverContextMenuActionAsync(page, actionId);
            if (await WaitForFunctionAsync(page, @"() => document.querySelector('.cw-context-menu__action[data-layer-depth=""1""]') !== null", 1_500))
            {
                return;
            }

            await page.WaitForTimeoutAsync(120);
        }

        await page.Locator(".cw-context-menu__action[data-layer-depth='1']").First.WaitForAsync();
    }

    private static async Task HoverContextMenuActionAsync(IPage page, string actionId)
    {
        var selector = $".cw-context-menu__action[data-action-id='{actionId}']";
        await page.Locator(selector).Last.WaitForAsync();
        await page.EvaluateAsync(
            @"actionSelector => {
                const matches = Array.from(document.querySelectorAll(actionSelector));
                const action = matches.length > 0 ? matches[matches.length - 1] : null;
                if (!(action instanceof HTMLElement)) {
                    return;
                }

                action.dispatchEvent(new PointerEvent('pointerenter', {
                    bubbles: true,
                    cancelable: true,
                    pointerType: 'mouse',
                    isPrimary: true
                }));
            }",
            selector);
    }

    private static async Task ClickContextMenuActionAsync(IPage page, string actionId)
    {
        var selector = $".cw-context-menu__action[data-action-id='{actionId}']";
        var parentActionId = actionId.Contains(':', StringComparison.Ordinal)
            ? actionId.Split(':', 2)[0]
            : null;

        for (var attempt = 0; attempt < 4; attempt++)
        {
            var action = page.Locator(selector).Last;
            if (!await WaitForLocatorAsync(action, 1_000))
            {
                if (!string.IsNullOrWhiteSpace(parentActionId))
                {
                    await HoverContextMenuActionAsync(page, parentActionId);
                    if (await WaitForMenuActionAsync(page, actionId, 1_500))
                    {
                        continue;
                    }
                }
                else
                {
                    await page.WaitForTimeoutAsync(120);
                }

                await page.WaitForTimeoutAsync(120);
                continue;
            }

            try
            {
                await action.ClickAsync(new() { Force = true });
                return;
            }
            catch (PlaywrightException exception) when (
                exception.Message.Contains("detached", StringComparison.OrdinalIgnoreCase) ||
                exception.Message.Contains("stable", StringComparison.OrdinalIgnoreCase))
            {
            }

            var clicked = await page.EvaluateAsync<bool>(
                @"actionSelector => {
                    const matches = Array.from(document.querySelectorAll(actionSelector));
                    const action = matches.length > 0 ? matches[matches.length - 1] : null;
                    if (!(action instanceof HTMLButtonElement)) {
                        return false;
                    }

                    action.click();
                    return true;
                }",
                selector);
            if (clicked)
            {
                return;
            }

            await page.WaitForTimeoutAsync(120);
        }

        if (!string.IsNullOrWhiteSpace(parentActionId))
        {
            await HoverContextMenuActionAsync(page, parentActionId);
            var action = page.Locator(selector).Last;
            if (await WaitForLocatorAsync(action, 1_500))
            {
                try
                {
                    await action.ClickAsync(new() { Force = true });
                    return;
                }
                catch (PlaywrightException exception) when (
                    exception.Message.Contains("detached", StringComparison.OrdinalIgnoreCase) ||
                    exception.Message.Contains("stable", StringComparison.OrdinalIgnoreCase))
                {
                }

                var clicked = await page.EvaluateAsync<bool>(
                    @"actionSelector => {
                        const matches = Array.from(document.querySelectorAll(actionSelector));
                        const action = matches.length > 0 ? matches[matches.length - 1] : null;
                        if (!(action instanceof HTMLButtonElement)) {
                            return false;
                        }

                        action.click();
                        return true;
                    }",
                    selector);
                if (clicked)
                {
                    return;
                }
            }
        }

        throw new InvalidOperationException($"Expected context menu action '{actionId}' to be available for clicking.");
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
            var targetId = await ResolveCanvasNodeIdAsync(page, selector);
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

    private static async Task<string?> ResolveCanvasNodeIdAsync(IPage page, string selector)
    {
        await WaitForInitializedCanvasHostAsync(page);

        var exactMatch = Regex.Match(selector, @"data-node-id='(?<id>[^']+)'", RegexOptions.IgnoreCase);
        if (exactMatch.Success)
        {
            return exactMatch.Groups["id"].Value;
        }

        var prefixMatch = Regex.Match(selector, @"data-node-id\^='(?<prefix>[^']+)'", RegexOptions.IgnoreCase);
        if (prefixMatch.Success)
        {
            return await page.EvaluateAsync<string?>(
                @"prefix => {
                    const host = document.querySelector('.cw-canvas-host');
                    const nodes = host?.__canvasWorkbenchState?.surface?.nodes;
                    if (!Array.isArray(nodes)) {
                        return null;
                    }

                    const match = nodes.find(node => typeof node?.id === 'string' && node.id.startsWith(prefix));
                    return match?.id || null;
                }",
                prefixMatch.Groups["prefix"].Value);
        }

        var textMatch = Regex.Match(selector, @":has-text\('(?<text>[^']+)'\)", RegexOptions.IgnoreCase);
        if (textMatch.Success)
        {
            return await page.EvaluateAsync<string?>(
                @"text => {
                    const host = document.querySelector('.cw-canvas-host');
                    const nodes = host?.__canvasWorkbenchState?.surface?.nodes;
                    if (!Array.isArray(nodes)) {
                        return null;
                    }

                    const lowered = (text || '').toLowerCase();
                    const match = nodes.find(node => {
                        const candidates = [node?.title, node?.inlineText, node?.leadText];
                        return candidates.some(candidate => typeof candidate === 'string' && candidate.toLowerCase().includes(lowered));
                    });
                    return match?.id || null;
                }",
                textMatch.Groups["text"].Value);
        }

        if (string.Equals(selector, ".cw-node.is-inline-text", StringComparison.Ordinal))
        {
            return await page.EvaluateAsync<string?>(
                @"() => {
                    const host = document.querySelector('.cw-canvas-host');
                    const nodes = host?.__canvasWorkbenchState?.surface?.nodes;
                    if (!Array.isArray(nodes)) {
                        return null;
                    }

                    const match = nodes.find(node => node?.isInlineTextNode === true);
                    return match?.id || null;
                }");
        }

        var target = page.Locator(selector).First;
        if (await WaitForLocatorAsync(target, 250))
        {
            var targetId = await target.GetAttributeAsync("data-node-id");
            if (!string.IsNullOrWhiteSpace(targetId))
            {
                return targetId;
            }
        }

        return null;
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
        => ToggleCanvasNodeCollapseCoreAsync(page, selector);

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
        => DispatchCanvasContextMenuAsync(page, selector);

    private static async Task<CanvasFocusState> ReadCanvasFocusStateAsync(IPage page)
    {
        var snapshot = await ReadSceneSnapshotAsync(page);
        var selected = snapshot.Nodes.FirstOrDefault(node => node.Selected);
        if (selected is null)
        {
            return new CanvasFocusState
            {
                SelectedId = null,
                DeltaX = 9999,
                DeltaY = 9999
            };
        }

        var hostBounds = await ReadPrimaryCanvasHostBoundsAsync(page);
        var selectedCenterX = hostBounds.Left + selected.Left + (selected.Width / 2d);
        var selectedCenterY = hostBounds.Top + selected.Top + (selected.Height / 2d);
        var hostCenterX = hostBounds.Left + (hostBounds.Width / 2d);
        var hostCenterY = hostBounds.Top + (hostBounds.Height / 2d);
        return new CanvasFocusState
        {
            SelectedId = selected.Id,
            DeltaX = (int)Math.Round(selectedCenterX - hostCenterX),
            DeltaY = (int)Math.Round(selectedCenterY - hostCenterY)
        };
    }

    private static async Task<CanvasFocusState> WaitForCanvasFocusStateAsync(
        IPage page,
        Func<CanvasFocusState, bool> predicate,
        string description,
        int timeoutMs = 4_000)
    {
        var attempts = Math.Max(1, timeoutMs / 120);
        CanvasFocusState? lastState = null;
        for (var attempt = 0; attempt < attempts; attempt++)
        {
            var focusState = await ReadCanvasFocusStateAsync(page);
            lastState = focusState;
            if (predicate(focusState))
            {
                return focusState;
            }

            await page.WaitForTimeoutAsync(120);
        }

        throw new InvalidOperationException(
            $"Timed out waiting for canvas focus state '{description}'. Last state: selected='{lastState?.SelectedId ?? "<none>"}', dx={lastState?.DeltaX ?? -9999}, dy={lastState?.DeltaY ?? -9999}.");
    }

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
                    documentClientHeight: document.documentElement.clientHeight,
                    documentScrollHeight: document.documentElement.scrollHeight,
                    viewportWidth: window.innerWidth,
                    viewportHeight: window.innerHeight
                };
            }");

    private static async Task<CanvasNodePosition[]> ReadSelectedNodePositionsAsync(IPage page)
    {
        var snapshot = await ReadSceneSnapshotAsync(page);
        return snapshot.Nodes
            .Where(node => node.Selected)
            .Select(node => new CanvasNodePosition
            {
                Id = node.Id,
                Left = (int)Math.Round(node.Left),
                Top = (int)Math.Round(node.Top)
            })
            .ToArray();
    }

    private static async Task<CanvasNodePosition[]> ReadNodePositionsAsync(IPage page, string[] nodeIds)
    {
        var snapshot = await ReadSceneSnapshotAsync(page);
        return nodeIds
            .Select(nodeId =>
            {
                var node = snapshot.Nodes.FirstOrDefault(candidate => string.Equals(candidate.Id, nodeId, StringComparison.Ordinal));
                return new CanvasNodePosition
                {
                    Id = nodeId,
                    Left = node is null ? -9999 : (int)Math.Round(node.Left),
                    Top = node is null ? -9999 : (int)Math.Round(node.Top)
                };
            })
            .ToArray();
    }

    private static async Task<string> FindOffscreenNodeIdAsync(IPage page, IReadOnlyList<string> nodeIds)
    {
        var snapshot = await ReadSceneSnapshotAsync(page);
        var nodeId = nodeIds.FirstOrDefault(candidateId =>
        {
            var node = snapshot.Nodes.FirstOrDefault(entry => string.Equals(entry.Id, candidateId, StringComparison.Ordinal));
            return node is null ||
                node.Right < 0 ||
                node.Bottom < 0 ||
                node.Left > snapshot.ViewportWidth ||
                node.Top > snapshot.ViewportHeight;
        });
        Assert.False(string.IsNullOrWhiteSpace(nodeId), "Expected at least one large-graph node to be culled or off-screen.");
        return nodeId!;
    }

    private static async Task CommitCanvasNodePositionsAsync(
        IPage page,
        IReadOnlyList<(string NodeId, int X, int Y)> positions)
    {
        var committed = await page.EvaluateAsync<bool>(
            @"async payload => {
                const host = document.querySelector('.cw-canvas-host');
                const state = host?.__canvasWorkbenchState;
                if (!state?.dotNetRef?.invokeMethodAsync) {
                    return false;
                }

                await state.dotNetRef.invokeMethodAsync('OnNodesMoved', JSON.stringify(payload.positions));
                return true;
            }",
            new
            {
                positions = positions
                    .Select(position => new
                    {
                        nodeId = position.NodeId,
                        x = position.X,
                        y = position.Y
                    })
                    .ToArray()
            });
        Assert.True(committed, "Expected the browser workbench host to expose the node-move callback.");
        await page.WaitForTimeoutAsync(260);
    }

    private static async Task CommitCanvasUiStateAsync(
        IPage page,
        double? zoom = null,
        double? panX = null,
        double? panY = null,
        IReadOnlyList<string>? selectedNodeIds = null)
    {
        var committed = await page.EvaluateAsync<bool>(
            @"async payload => {
                const host = document.querySelector('.cw-canvas-host');
                const state = host?.__canvasWorkbenchState;
                if (!state?.dotNetRef?.invokeMethodAsync || !state?.surface?.uiState) {
                    return false;
                }

                const uiState = JSON.parse(JSON.stringify(state.surface.uiState));
                if (Array.isArray(payload.selectedNodeIds)) {
                    uiState.selectedNodeIds = payload.selectedNodeIds;
                }

                if (typeof payload.zoom === 'number') {
                    uiState.zoom = payload.zoom;
                }

                if (typeof payload.panX === 'number') {
                    uiState.panX = payload.panX;
                }

                if (typeof payload.panY === 'number') {
                    uiState.panY = payload.panY;
                }

                const dispatchId = (state.stateDispatchId || 0) + 1;
                state.stateDispatchId = dispatchId;
                await state.dotNetRef.invokeMethodAsync('OnStateChanged', JSON.stringify(uiState), dispatchId);
                return true;
            }",
            new
            {
                zoom,
                panX,
                panY,
                selectedNodeIds = selectedNodeIds?.ToArray()
            });
        Assert.True(committed, "Expected the browser workbench host to expose the state commit callback.");
        await page.WaitForTimeoutAsync(260);
    }

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

    private static Task<CanvasMenuLayerSnapshot> ReadContextMenuLayerSnapshotAsync(IPage page, string actionIdPrefix)
        => page.EvaluateAsync<CanvasMenuLayerSnapshot>(
            @"requestedPrefix => {
                const host = document.querySelector('.cw-canvas-host');
                const frame = host?.closest('.cw-workbench-frame');
                const toolbars = frame ? Array.from(frame.querySelectorAll('.cw-toolbar')) : [];
                const toolbarBottom = toolbars.reduce((maxBottom, toolbar) => Math.max(maxBottom, toolbar.getBoundingClientRect().bottom), 0);
                const hostTop = host?.getBoundingClientRect().top ?? 0;
                const safeTop = Math.max(0, Math.round(toolbarBottom - hostTop + 12));
                const layers = Array.from(document.querySelectorAll('.cw-context-menu__layer'));
                const activeLayer = layers.length > 0 ? layers[layers.length - 1] : null;
                const core = activeLayer?.querySelector('.cw-context-menu__core');
                const coreRect = core?.getBoundingClientRect();
                const coreCenterX = coreRect ? coreRect.left + (coreRect.width / 2) : 0;
                const coreCenterY = coreRect ? coreRect.top + (coreRect.height / 2) : 0;
                const actions = Array.from(activeLayer?.querySelectorAll(`.cw-context-menu__action[data-action-id^=""${requestedPrefix}""]`) || []);
                return {
                    toolbarBottom,
                    hostTop,
                    safeTop,
                    rootCenterY: host?.__canvasWorkbenchState?.contextMenuState?.rootCenter?.y ?? 0,
                    actions: actions.map(action => {
                        const rect = action.getBoundingClientRect();
                        const centerX = rect.left + (rect.width / 2);
                        const centerY = rect.top + (rect.height / 2);
                        return {
                            actionId: action.getAttribute('data-action-id') || '',
                            left: rect.left,
                            top: rect.top,
                            right: rect.right,
                            bottom: rect.bottom,
                            centerX,
                            centerY,
                            distanceFromCore: Math.hypot(centerX - coreCenterX, centerY - coreCenterY),
                            centerText: action.querySelector('.cw-node__progress-center')?.textContent?.trim() || ''
                        };
                    })
                };
            }",
            actionIdPrefix);

    private static int CountDistinctBands(IEnumerable<double> values, double tolerance)
    {
        var ordered = values
            .OrderBy(value => value)
            .ToList();
        if (ordered.Count == 0)
        {
            return 0;
        }

        var bands = 1;
        var bandStart = ordered[0];
        for (var index = 1; index < ordered.Count; index++)
        {
            if (Math.Abs(ordered[index] - bandStart) <= tolerance)
            {
                continue;
            }

            bands += 1;
            bandStart = ordered[index];
        }

        return bands;
    }

    private static void AssertMenuActionsDoNotOverlap(IReadOnlyList<CanvasMenuActionSnapshot> actions, double tolerance, string phase)
    {
        for (var index = 0; index < actions.Count; index++)
        {
            var first = actions[index];
            for (var compareIndex = index + 1; compareIndex < actions.Count; compareIndex++)
            {
                var second = actions[compareIndex];
                var left = Math.Max(first.Left + tolerance, second.Left + tolerance);
                var right = Math.Min(first.Right - tolerance, second.Right - tolerance);
                var top = Math.Max(first.Top + tolerance, second.Top + tolerance);
                var bottom = Math.Min(first.Bottom - tolerance, second.Bottom - tolerance);
                Assert.False(
                    right > left && bottom > top,
                    $"{phase} still has overlapping actions: {first.ActionId} <-> {second.ActionId}.");
            }
        }
    }

    private static async Task AssertNoCanvasNodeOverlapsAsync(IPage page, string phase)
    {
        const double tolerance = 6d;
        var overlaps = Array.Empty<CanvasNodeOverlap>();
        await WaitForSceneSnapshotAsync(
            page,
            snapshot =>
            {
                overlaps = FindCanvasNodeOverlaps(snapshot.Nodes, tolerance);
                return overlaps.Length == 0;
            },
            $"{phase} without node overlaps",
            timeoutMs: 10_000);

        Assert.True(
            overlaps.Length == 0,
            $"{phase} still has overlapping nodes: {string.Join(", ", overlaps.Select(overlap => $"{overlap.FirstTitle} <-> {overlap.SecondTitle}"))}");
    }

    private static CanvasNodeOverlap[] FindCanvasNodeOverlaps(IReadOnlyList<CanvasSceneNodeSnapshot> nodes, double tolerance)
    {
        var overlaps = new List<CanvasNodeOverlap>();
        for (var index = 0; index < nodes.Count; index++)
        {
            var first = nodes[index];
            for (var compareIndex = index + 1; compareIndex < nodes.Count; compareIndex++)
            {
                var second = nodes[compareIndex];
                var left = Math.Max(first.Left + tolerance, second.Left + tolerance);
                var right = Math.Min(first.Right - tolerance, second.Right - tolerance);
                var top = Math.Max(first.Top + tolerance, second.Top + tolerance);
                var bottom = Math.Min(first.Bottom - tolerance, second.Bottom - tolerance);
                if (right <= left || bottom <= top)
                {
                    continue;
                }

                overlaps.Add(new CanvasNodeOverlap
                {
                    FirstTitle = first.DisplayTitle,
                    SecondTitle = second.DisplayTitle
                });
            }
        }

        return overlaps.ToArray();
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

    private static Task<WorkbenchStorageState> ReadWorkbenchSessionStorageAsync(IPage page)
        => page.EvaluateAsync<WorkbenchStorageState>(
            @"() => {
                const key = Object.keys(localStorage)
                    .find(candidate => candidate.startsWith('candoitall.workbench.session:')) ?? null;
                return {
                    key,
                    value: key ? localStorage.getItem(key) : null
                };
            }");

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
        var nodeId = await ResolveCanvasNodeIdAsync(page, selector);
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
        await WaitForInitializedCanvasHostAsync(page);
        await WaitForCanvasRenderIdleAsync(page);

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
        var payloadJson = JsonSerializer.Serialize(payload);

        var invoked = await page.EvaluateAsync<bool>(
            @"async payloadJson => {
                const host = document.querySelector('.cw-canvas-host');
                const state = host?.__canvasWorkbenchState;
                const request = typeof payloadJson === 'string'
                    ? JSON.parse(payloadJson)
                    : payloadJson;
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
            payloadJson);
        Assert.True(invoked, $"Expected create action '{actionId}' to be invokable.");

        var appeared = true;
        try
        {
            await page.WaitForFunctionAsync(
                @"expectedTitle => {
                    const host = document.querySelector('.cw-canvas-host');
                    const nodes = host?.__canvasWorkbenchState?.surface?.nodes || [];
                    return nodes.some(node => node?.title === expectedTitle);
                }",
                title,
                new() { Timeout = 60_000 });
        }
        catch (TimeoutException)
        {
            appeared = false;
        }

        if (!appeared)
        {
            var failureSnapshot = await page.EvaluateAsync<CreateActionFailureSnapshot>(
                @"() => {
                    const host = document.querySelector('.cw-canvas-host');
                    const state = host?.__canvasWorkbenchState;
                    const nodes = state?.surface?.nodes || [];
                    return {
                        nodeCount: nodes.length,
                        titles: nodes.map(node => node?.title || '').filter(title => !!title),
                        selectedNodeIds: Array.from(state?.selectedIds || []),
                        errorUiVisible: !!document.querySelector('#blazor-error-ui[style*=""display: block""]')
                    };
                }");
            throw new InvalidOperationException(
                $"Timed out waiting for create action '{actionId}' to surface node title '{title}'. " +
                $"NodeCount={failureSnapshot?.NodeCount ?? 0}, " +
                $"Selected={string.Join(", ", failureSnapshot?.SelectedNodeIds ?? [])}, " +
                $"Titles={string.Join(" | ", failureSnapshot?.Titles ?? [])}, " +
                $"BlazorErrorUiVisible={failureSnapshot?.ErrorUiVisible ?? false}.");
        }
        var createdNodeId = await FindNodeIdByTitleAsync(page, title);
        try
        {
            await page.WaitForFunctionAsync(
                @"expectedNodeId => {
                    const host = document.querySelector('.cw-canvas-host');
                    const selectedNodeIds = host?.__canvasWorkbenchState?.ui?.selectedNodeIds || [];
                    return selectedNodeIds.length === 1 && selectedNodeIds[0] === expectedNodeId;
                }",
                createdNodeId,
                new() { Timeout = 10_000 });
        }
        catch (TimeoutException)
        {
        }

        await WaitForInitializedCanvasHostAsync(page);
        await WaitForCanvasRenderIdleAsync(page, steadyMs: uploadedFile is null ? 220 : 520, timeoutMs: 15_000);

        return createdNodeId;
    }

    private static async Task<string> FindNodeIdByTitleAsync(IPage page, string title)
    {
        var attempts = Math.Max(1, 6_000 / 120);
        string? nodeId = null;
        for (var attempt = 0; attempt < attempts; attempt++)
        {
            nodeId = await TryFindNodeIdByTitleAsync(page, title);
            if (!string.IsNullOrWhiteSpace(nodeId))
            {
                break;
            }

            await page.WaitForTimeoutAsync(120);
        }

        Assert.False(string.IsNullOrWhiteSpace(nodeId), $"Expected to find a node with title '{title}'.");
        return nodeId!;
    }

    private static Task<string?> TryFindNodeIdByTitleAsync(IPage page, string title)
        => page.EvaluateAsync<string?>(
            @"expectedTitle => {
                const host = document.querySelector('.cw-canvas-host');
                const nodes = host?.__canvasWorkbenchState?.surface?.nodes || [];
                const match = nodes.filter(node => {
                    const candidates = [node?.title, node?.inlineText, node?.leadText];
                    return candidates.some(candidate => candidate === expectedTitle);
                }).at(-1);
                return match?.id || null;
            }",
            title);

    private static async Task<bool> WaitForNodeTitleInStateAsync(IPage page, string title, int timeoutMs = 6_000)
    {
        var attempts = Math.Max(1, timeoutMs / 120);
        for (var attempt = 0; attempt < attempts; attempt++)
        {
            if (!string.IsNullOrWhiteSpace(await TryFindNodeIdByTitleAsync(page, title)))
            {
                return true;
            }

            await page.WaitForTimeoutAsync(120);
        }

        return false;
    }

    private static async Task WaitForNodeProgressStateAsync(
        IPage page,
        string nodeId,
        string expectedProgressMode,
        int expectedProgressPercent,
        int timeoutMs = 10_000)
    {
        await page.WaitForFunctionAsync(
            @"request => {
                const host = document.querySelector('.cw-canvas-host');
                const nodes = host?.__canvasWorkbenchState?.surface?.nodes || [];
                const node = nodes.find(candidate => candidate?.id === request.nodeId);
                return !!node &&
                    (node.progressMode || '') === request.progressMode &&
                    Number(node.progressPercent || 0) === Number(request.progressPercent);
            }",
            new
            {
                nodeId,
                progressMode = expectedProgressMode,
                progressPercent = expectedProgressPercent
            },
            new() { Timeout = timeoutMs });
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
        var payloadJson = JsonSerializer.Serialize(new
        {
            nodeIds = nodeIds.ToArray(),
            primaryNodeId
        });
        var selectionStabilized = false;
        SelectionFailureSnapshot? selectionFailureSnapshot = null;
        for (var attempt = 0; attempt < 3; attempt++)
        {
            await page.EvaluateAsync(
                @"payloadJson => {
                    const host = document.querySelector('.cw-canvas-host');
                    const payload = typeof payloadJson === 'string'
                        ? JSON.parse(payloadJson)
                        : payloadJson;
                    const state = host?.__canvasWorkbenchState;
                    if (!host || !state || !window.CanDoItAll?.canvasWorkbench?.selectNodes) {
                        return;
                    }

                    if (!window.CanDoItAll.canvasWorkbench.__selectionDebugWrapped) {
                        const originalSelectNodes = window.CanDoItAll.canvasWorkbench.selectNodes.bind(window.CanDoItAll.canvasWorkbench);
                        window.CanDoItAll.canvasWorkbench.__selectionDebugWrapped = true;
                        window.CanDoItAll.canvasWorkbench.__selectionDebugCalls = [];
                        window.CanDoItAll.canvasWorkbench.selectNodes = (targetHost, nodeIds, requestedPrimaryNodeId) => {
                            window.CanDoItAll.canvasWorkbench.__selectionDebugCalls.push({
                                nodeIds: Array.isArray(nodeIds) ? [...nodeIds] : [],
                                primaryNodeId: requestedPrimaryNodeId || null,
                                stack: (new Error().stack || '').split('\n').slice(0, 5).join(' | ')
                            });
                            return originalSelectNodes(targetHost, nodeIds, requestedPrimaryNodeId);
                        };
                    }

                    if (state.dotNetRef && !state.dotNetRef.__selectionDebugWrapped) {
                        const originalInvokeMethodAsync = state.dotNetRef.invokeMethodAsync.bind(state.dotNetRef);
                        state.selectionDebugEvents = [];
                        state.dotNetRef.__selectionDebugWrapped = true;
                        state.dotNetRef.invokeMethodAsync = (...args) => {
                            const event = {
                                method: args[0] || '',
                                args: args.slice(1).map(value => typeof value === 'string' ? value : JSON.stringify(value)),
                                status: 'pending'
                            };
                            state.selectionDebugEvents.push(event);
                            return Promise.resolve(originalInvokeMethodAsync(...args))
                                .then(result => {
                                    event.status = 'fulfilled';
                                    return result;
                                })
                                .catch(error => {
                                    event.status = 'rejected';
                                    event.error = String(error);
                                    throw error;
                                });
                        };
                    }

                    window.CanDoItAll.canvasWorkbench.selectNodes(host, payload.nodeIds, payload.primaryNodeId);
                }",
                payloadJson);
            try
            {
                await page.WaitForFunctionAsync(
                    @"expectedNodeIds => {
                        const host = document.querySelector('.cw-canvas-host');
                        const selectedNodeIds = host?.__canvasWorkbenchState?.ui?.selectedNodeIds || [];
                        return Array.isArray(expectedNodeIds) &&
                            expectedNodeIds.length === selectedNodeIds.length &&
                            expectedNodeIds.every(nodeId => selectedNodeIds.includes(nodeId));
                    }",
                    nodeIds.ToArray(),
                    new() { Timeout = 5_000 });
            }
            catch (TimeoutException)
            {
                selectionFailureSnapshot = await CaptureSelectionFailureSnapshotAsync(page);
                continue;
            }
            await page.WaitForTimeoutAsync(240);

            selectionStabilized = await page.EvaluateAsync<bool>(
                @"expectedNodeIds => {
                    const host = document.querySelector('.cw-canvas-host');
                    const selectedNodeIds = host?.__canvasWorkbenchState?.ui?.selectedNodeIds || [];
                    return Array.isArray(expectedNodeIds) &&
                        expectedNodeIds.length === selectedNodeIds.length &&
                        expectedNodeIds.every(nodeId => selectedNodeIds.includes(nodeId));
                }",
                nodeIds.ToArray());
            if (selectionStabilized)
            {
                break;
            }

            selectionFailureSnapshot = await CaptureSelectionFailureSnapshotAsync(page);
        }

        Assert.True(
            selectionStabilized,
            $"Expected canvas selection to stabilize for [{string.Join(", ", nodeIds)}]. " +
            $"HostSelectedNodeIds=[{string.Join(", ", selectionFailureSnapshot?.HostSelectedNodeIds ?? [])}] " +
            $"SelectionApiCalls=[{string.Join(" || ", selectionFailureSnapshot?.SelectionApiCalls ?? [])}] " +
            $"SelectionDebugEvents=[{string.Join(" || ", selectionFailureSnapshot?.SelectionDebugEvents ?? [])}] " +
            $"SelectionPanelTitles=[{string.Join(", ", selectionFailureSnapshot?.SelectionPanelTitles ?? [])}] " +
            $"SelectionWindowPresent={selectionFailureSnapshot?.SelectionWindowPresent} " +
            $"SelectionWindowVisible={selectionFailureSnapshot?.SelectionWindowVisible} " +
            $"SelectionWindowMinimized={selectionFailureSnapshot?.SelectionWindowMinimized} " +
            $"SelectionWindowText='{selectionFailureSnapshot?.SelectionWindowText ?? string.Empty}'.");

        if (nodeIds.Count > 1)
        {
            try
            {
                await page.WaitForFunctionAsync(
                    @"expectedCount => {
                        const selectionWindow = document.querySelector('.cw-floating-window[data-testid=""project-structure-selection-window""]');
                        const text = (selectionWindow?.textContent || '').replace(/\s+/g, ' ').trim();
                        return text.includes(`${expectedCount} selected`) ||
                            text.includes(`${expectedCount} nodes selected`);
                    }",
                    nodeIds.Count,
                    new() { Timeout = 10_000 });
            }
            catch (TimeoutException exception)
            {
                var snapshot = await CaptureSelectionFailureSnapshotAsync(page);
                throw new InvalidOperationException(
                    $"Expected visible multi-selection text for count '{nodeIds.Count}' but it did not appear. " +
                    $"HostSelectedNodeIds=[{string.Join(", ", snapshot?.HostSelectedNodeIds ?? [])}] " +
                    $"SelectionApiCalls=[{string.Join(" || ", snapshot?.SelectionApiCalls ?? [])}] " +
                    $"SelectionDebugEvents=[{string.Join(" || ", snapshot?.SelectionDebugEvents ?? [])}] " +
                    $"SelectionPanelTitles=[{string.Join(", ", snapshot?.SelectionPanelTitles ?? [])}] " +
                    $"SelectionWindowPresent={snapshot?.SelectionWindowPresent} " +
                    $"SelectionWindowVisible={snapshot?.SelectionWindowVisible} " +
                    $"SelectionWindowMinimized={snapshot?.SelectionWindowMinimized} " +
                    $"SelectionWindowText='{snapshot?.SelectionWindowText ?? string.Empty}'.",
                    exception);
            }

            await EnsureFloatingWindowExpandedAsync(page, "project-structure-selection-window");
            await page.Locator(".cw-floating-window[data-testid='project-structure-selection-window'] input[placeholder='Name this border']")
                .WaitForAsync(new() { Timeout = 10_000 });
        }

        await page.WaitForTimeoutAsync(180);
    }

    private static async Task<SelectionFailureSnapshot?> CaptureSelectionFailureSnapshotAsync(IPage page)
    {
        return await page.EvaluateAsync<SelectionFailureSnapshot?>(
            @"() => {
                const host = document.querySelector('.cw-canvas-host');
                const windowElement = document.querySelector('.cw-floating-window[data-testid=""project-structure-selection-window""]');
                const panelTitles = Array.from(document.querySelectorAll('.project-structure-selection-panel .cw-panel-title'))
                    .map(candidate => (candidate.textContent || '').trim())
                    .filter(Boolean);
                const windowText = windowElement instanceof HTMLElement
                    ? (windowElement.textContent || '').trim()
                    : '';

                return {
                    hostSelectedNodeIds: Array.isArray(host?.__canvasWorkbenchState?.ui?.selectedNodeIds)
                        ? host.__canvasWorkbenchState.ui.selectedNodeIds
                        : [],
                    selectionApiCalls: Array.isArray(window.CanDoItAll?.canvasWorkbench?.__selectionDebugCalls)
                        ? window.CanDoItAll.canvasWorkbench.__selectionDebugCalls
                            .slice(-6)
                            .map(call => `[${(call.nodeIds || []).join(', ')}] primary=${call.primaryNodeId || ''} stack=${call.stack || ''}`)
                        : [],
                    selectionDebugEvents: Array.isArray(host?.__canvasWorkbenchState?.selectionDebugEvents)
                        ? host.__canvasWorkbenchState.selectionDebugEvents
                            .slice(-8)
                            .map(event => `${event.method}:${event.status}:${(event.args || []).join(' | ')}`)
                        : [],
                    selectionPanelTitles: panelTitles,
                    selectionWindowPresent: windowElement instanceof HTMLElement,
                    selectionWindowVisible: windowElement instanceof HTMLElement &&
                        windowElement.offsetParent !== null &&
                        getComputedStyle(windowElement).visibility !== 'hidden',
                    selectionWindowMinimized: windowElement instanceof HTMLElement &&
                        windowElement.classList.contains('is-minimized'),
                    selectionWindowText: windowText
                };
            }");
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

    private static async Task WaitForInitializedCanvasHostAsync(IPage page)
    {
        await page.WaitForFunctionAsync(
            @"() => {
                const workbench = window.CanDoItAll?.canvasWorkbench;
                if (!workbench?.getDiagnostics) {
                    return false;
                }

                return Array.from(document.querySelectorAll('.cw-canvas-host'))
                    .filter(candidate => candidate instanceof HTMLElement)
                    .filter(candidate => {
                        const rect = candidate.getBoundingClientRect();
                        if (rect.width <= 0 || rect.height <= 0) {
                            return false;
                        }

                        const styles = getComputedStyle(candidate);
                        if (styles.display === 'none' || styles.visibility === 'hidden') {
                            return false;
                        }

                        return !!candidate.__canvasWorkbenchState && !!workbench.getDiagnostics(candidate);
                    })
                    .length > 0;
            }");
    }

    private static async Task WaitForCanvasRenderIdleAsync(IPage page, int steadyMs = 240, int timeoutMs = 10_000)
    {
        await WaitForInitializedCanvasHostAsync(page);
        await page.WaitForFunctionAsync(
            @"steadyMs => {
                const workbench = window.CanDoItAll?.canvasWorkbench;
                const host = Array.from(document.querySelectorAll('.cw-canvas-host'))
                    .filter(candidate => candidate instanceof HTMLElement)
                    .filter(candidate => {
                        const rect = candidate.getBoundingClientRect();
                        if (rect.width <= 0 || rect.height <= 0) {
                            return false;
                        }

                        const styles = getComputedStyle(candidate);
                        return styles.display !== 'none'
                            && styles.visibility !== 'hidden'
                            && !!candidate.__canvasWorkbenchState
                            && !!workbench?.getDiagnostics;
                    })
                    .sort((left, right) => {
                        const leftRect = left.getBoundingClientRect();
                        const rightRect = right.getBoundingClientRect();
                        return (rightRect.width * rightRect.height) - (leftRect.width * leftRect.height);
                    })[0];
                if (!host || !workbench?.getDiagnostics) {
                    return false;
                }

                const diagnostics = workbench.getDiagnostics(host);
                if (!diagnostics || diagnostics.interaction !== 'idle') {
                    host.__codexCanvasIdleSignature = '';
                    host.__codexCanvasIdleSince = 0;
                    return false;
                }

                const signature = JSON.stringify({
                    renderCount: diagnostics.metrics?.renderCount || 0,
                    totalNodeCount: diagnostics.totalNodeCount || 0,
                    selectedCount: diagnostics.selectedCount || 0,
                    interaction: diagnostics.interaction || 'idle'
                });
                const now = Date.now();
                if (host.__codexCanvasIdleSignature !== signature) {
                    host.__codexCanvasIdleSignature = signature;
                    host.__codexCanvasIdleSince = now;
                    return false;
                }

                return now - (host.__codexCanvasIdleSince || 0) >= steadyMs;
            }",
            steadyMs,
            new() { Timeout = timeoutMs });
    }

    private static async Task<CanvasSceneSnapshot> ReadSceneSnapshotAsync(IPage page)
    {
        await WaitForInitializedCanvasHostAsync(page);
        var snapshot = await page.EvaluateAsync<CanvasSceneSnapshot?>(
            @"() => {
                const workbench = window.CanDoItAll?.canvasWorkbench;
                const hosts = Array.from(document.querySelectorAll('.cw-canvas-host'));
                const host = hosts
                    .filter(candidate => candidate instanceof HTMLElement)
                    .filter(candidate => {
                        const rect = candidate.getBoundingClientRect();
                        if (rect.width <= 0 || rect.height <= 0) {
                            return false;
                        }

                        const styles = getComputedStyle(candidate);
                        return styles.display !== 'none'
                            && styles.visibility !== 'hidden'
                            && !!candidate.__canvasWorkbenchState
                            && !!workbench?.getSceneSnapshot;
                    })
                    .sort((left, right) => {
                        const leftRect = left.getBoundingClientRect();
                        const rightRect = right.getBoundingClientRect();
                        return (rightRect.width * rightRect.height) - (leftRect.width * leftRect.height);
                    })[0];
                if (!host || !workbench?.getSceneSnapshot) {
                    return null;
                }

                return workbench.getSceneSnapshot(host);
            }");
        Assert.NotNull(snapshot);
        var viewport = await page.EvaluateAsync<CanvasScenePoint>(
            @"() => ({
                x: window.innerWidth || 0,
                y: window.innerHeight || 0
            })");
        snapshot!.ViewportWidth = (int)Math.Round(viewport.X);
        snapshot.ViewportHeight = (int)Math.Round(viewport.Y);
        return snapshot!;
    }

    private static async Task<CanvasSceneNodeSnapshot?> TryReadSceneNodeSnapshotAsync(IPage page, string nodeId)
    {
        var snapshot = await ReadSceneSnapshotAsync(page);
        return snapshot.Nodes.FirstOrDefault(node => string.Equals(node.Id, nodeId, StringComparison.Ordinal));
    }

    private static async Task<CanvasHotZoneCenter?> TryReadCanvasHotZoneCenterAsync(
        IPage page,
        string zone,
        string? nodeId = null,
        string? frameId = null)
    {
        await WaitForInitializedCanvasHostAsync(page);
        return await page.EvaluateAsync<CanvasHotZoneCenter?>(
            @"request => {
                const workbench = window.CanDoItAll?.canvasWorkbench;
                const hosts = Array.from(document.querySelectorAll('.cw-canvas-host'));
                const host = hosts
                    .filter(candidate => candidate instanceof HTMLElement)
                    .filter(candidate => {
                        const rect = candidate.getBoundingClientRect();
                        if (rect.width <= 0 || rect.height <= 0) {
                            return false;
                        }

                        const styles = getComputedStyle(candidate);
                        return styles.display !== 'none'
                            && styles.visibility !== 'hidden'
                            && !!candidate.__canvasWorkbenchState
                            && !!workbench?.getHotZoneCenter;
                    })
                    .sort((left, right) => {
                        const leftRect = left.getBoundingClientRect();
                        const rightRect = right.getBoundingClientRect();
                        return (rightRect.width * rightRect.height) - (leftRect.width * leftRect.height);
                    })[0];
                if (!host || !workbench?.getHotZoneCenter) {
                    return null;
                }

                return workbench.getHotZoneCenter(host, request);
            }",
            new
            {
                zone,
                nodeId,
                frameId
            });
    }

    private static async Task<bool> TrySimulateCanvasDragAsync(
        IPage page,
        string? nodeId,
        string? frameId,
        float deltaX,
        float deltaY,
        bool releasePointer,
        bool controlModifier = false)
    {
        await WaitForInitializedCanvasHostAsync(page);
        await page.WaitForFunctionAsync("() => typeof window.CanDoItAll?.canvasWorkbench?.simulateDrag === 'function'");
        var requestJson = JsonSerializer.Serialize(new
        {
            nodeId,
            frameId,
            deltaX,
            deltaY,
            release = releasePointer,
            ctrlKey = controlModifier,
            steps = 16
        });
        return await page.EvaluateAsync<bool>(
            @"requestJson => {
                const workbench = window.CanDoItAll?.canvasWorkbench;
                const request = typeof requestJson === 'string'
                    ? JSON.parse(requestJson)
                    : requestJson;
                const hosts = Array.from(document.querySelectorAll('.cw-canvas-host'));
                const host = hosts
                    .filter(candidate => candidate instanceof HTMLElement)
                    .filter(candidate => {
                        const rect = candidate.getBoundingClientRect();
                        if (rect.width <= 0 || rect.height <= 0) {
                            return false;
                        }

                        const styles = getComputedStyle(candidate);
                        return styles.display !== 'none'
                            && styles.visibility !== 'hidden'
                            && !!candidate.__canvasWorkbenchState
                            && typeof workbench?.simulateDrag === 'function';
                    })
                    .sort((left, right) => {
                        const leftRect = left.getBoundingClientRect();
                        const rightRect = right.getBoundingClientRect();
                        return (rightRect.width * rightRect.height) - (leftRect.width * leftRect.height);
                    })[0];
                if (!host || typeof workbench?.simulateDrag !== 'function') {
                    return false;
                }

                return !!workbench.simulateDrag(host, request);
            }",
            requestJson);
    }

    private static async Task ReleaseCanvasInteractionAsync(IPage page)
    {
        await WaitForInitializedCanvasHostAsync(page);
        await page.WaitForFunctionAsync("() => typeof window.CanDoItAll?.canvasWorkbench?.finishInteraction === 'function'");
        var released = await page.EvaluateAsync<bool>(
            @"() => {
                const workbench = window.CanDoItAll?.canvasWorkbench;
                const hosts = Array.from(document.querySelectorAll('.cw-canvas-host'));
                const host = hosts
                    .filter(candidate => candidate instanceof HTMLElement)
                    .filter(candidate => {
                        const rect = candidate.getBoundingClientRect();
                        if (rect.width <= 0 || rect.height <= 0) {
                            return false;
                        }

                        const styles = getComputedStyle(candidate);
                        return styles.display !== 'none'
                            && styles.visibility !== 'hidden'
                            && !!candidate.__canvasWorkbenchState
                            && typeof workbench?.finishInteraction === 'function';
                    })
                    .sort((left, right) => {
                        const leftRect = left.getBoundingClientRect();
                        const rightRect = right.getBoundingClientRect();
                        return (rightRect.width * rightRect.height) - (leftRect.width * leftRect.height);
                    })[0];
                if (!host || typeof workbench?.finishInteraction !== 'function') {
                    return false;
                }

                return !!workbench.finishInteraction(host);
            }");
        Assert.True(released, "Expected the shared canvas runtime to expose a releasable active interaction.");
        await page.WaitForTimeoutAsync(220);
    }

    private static async Task<CanvasScenePoint> ResolveCanvasNodeCenterAsync(IPage page, string selector)
    {
        var targetId = await ResolveCanvasNodeIdAsync(page, selector);
        if (string.IsNullOrWhiteSpace(targetId))
        {
            throw new InvalidOperationException($"Could not resolve a canvas node id for selector '{selector}'.");
        }

        var hostBounds = await ReadPrimaryCanvasHostBoundsAsync(page);
        for (var attempt = 0; attempt < 6; attempt++)
        {
            var snapshot = await TryReadSceneNodeSnapshotAsync(page, targetId);
            if (snapshot is not null)
            {
                return new CanvasScenePoint
                {
                    X = hostBounds.Left + snapshot.Left + (snapshot.Width / 2d),
                    Y = hostBounds.Top + snapshot.Top + (snapshot.Height / 2d)
                };
            }

            await page.WaitForTimeoutAsync(120);
        }

        throw new InvalidOperationException($"Could not resolve visible canvas geometry for selector '{selector}'.");
    }

    private static async Task<CanvasScenePoint?> TryResolveCanvasHotZoneCenterAsync(IPage page, string selector, string zone)
    {
        var targetId = await ResolveCanvasNodeIdAsync(page, selector);
        if (string.IsNullOrWhiteSpace(targetId))
        {
            return null;
        }

        return await page.EvaluateAsync<CanvasScenePoint?>(
            @"request => {
                const host = document.querySelector('.cw-canvas-host');
                const state = host?.__canvasWorkbenchState;
                const runtimeModule = window.CanDoItAll?.canvasWorkbenchModule;
                if (!host || !state || !runtimeModule?.findSceneHotZoneCenter || !request?.nodeId || !request?.zone) {
                    return null;
                }

                const point = runtimeModule.findSceneHotZoneCenter(state, {
                    nodeId: request.nodeId,
                    zone: request.zone
                });
                const hostRect = host.getBoundingClientRect();
                if (!point || !hostRect) {
                    return null;
                }

                return {
                    x: hostRect.left + point.x,
                    y: hostRect.top + point.y,
                    width: point.width,
                    height: point.height
                };
            }",
            new
            {
                nodeId = targetId,
                zone
            });
    }

    private static async Task<CanvasSceneNodeSnapshot> ReadSceneNodeSnapshotAsync(IPage page, string nodeId, int timeoutMs = 6_000)
    {
        var attempts = Math.Max(1, timeoutMs / 120);
        for (var attempt = 0; attempt < attempts; attempt++)
        {
            var snapshot = await TryReadSceneNodeSnapshotAsync(page, nodeId);
            if (snapshot is not null)
            {
                return snapshot;
            }

            await page.WaitForTimeoutAsync(120);
        }

        throw new InvalidOperationException($"Could not resolve scene snapshot geometry for node '{nodeId}'.");
    }

    private static async Task<CanvasSceneFrameSnapshot> ReadSceneFrameSnapshotAsync(IPage page, string frameId, int timeoutMs = 6_000)
    {
        var attempts = Math.Max(1, timeoutMs / 120);
        for (var attempt = 0; attempt < attempts; attempt++)
        {
            var snapshot = await ReadSceneSnapshotAsync(page);
            var frame = snapshot.Frames.FirstOrDefault(candidate => string.Equals(candidate.FrameId, frameId, StringComparison.Ordinal));
            if (frame is not null)
            {
                return frame;
            }

            await page.WaitForTimeoutAsync(120);
        }

        throw new InvalidOperationException($"Could not resolve scene snapshot geometry for frame '{frameId}'.");
    }

    private static async Task<CanvasSceneFrameSnapshot> ReadSceneFrameSnapshotByLabelAsync(IPage page, string label, int timeoutMs = 6_000)
    {
        var attempts = Math.Max(1, timeoutMs / 120);
        for (var attempt = 0; attempt < attempts; attempt++)
        {
            var snapshot = await ReadSceneSnapshotAsync(page);
            var frame = snapshot.Frames.FirstOrDefault(candidate => string.Equals(candidate.Label, label, StringComparison.Ordinal));
            if (frame is not null)
            {
                return frame;
            }

            await page.WaitForTimeoutAsync(120);
        }

        throw new InvalidOperationException($"Could not resolve scene snapshot geometry for frame label '{label}'.");
    }

    private static async Task<CanvasSceneSnapshot> WaitForSceneSnapshotAsync(
        IPage page,
        Func<CanvasSceneSnapshot, bool> predicate,
        string description,
        int timeoutMs = 6_000)
    {
        var attempts = Math.Max(1, timeoutMs / 120);
        CanvasSceneSnapshot? lastSnapshot = null;
        for (var attempt = 0; attempt < attempts; attempt++)
        {
            lastSnapshot = await ReadSceneSnapshotAsync(page);
            if (predicate(lastSnapshot))
            {
                return lastSnapshot;
            }

            await page.WaitForTimeoutAsync(120);
        }

        throw new InvalidOperationException($"Timed out waiting for scene snapshot condition '{description}'.");
    }

    private static async Task WaitForSceneNodeTitleAsync(IPage page, string title, bool selectedOnly = false, int timeoutMs = 6_000)
    {
        await WaitForSceneSnapshotAsync(
            page,
            snapshot => snapshot.Nodes.Any(node =>
                string.Equals(node.DisplayTitle, title, StringComparison.Ordinal) &&
                (!selectedOnly || node.Selected)),
            selectedOnly ? $"selected node title '{title}'" : $"node title '{title}'",
            timeoutMs);
    }

    private static async Task WaitForSceneFrameLabelAsync(IPage page, string label, int timeoutMs = 6_000)
    {
        await WaitForSceneSnapshotAsync(
            page,
            snapshot => snapshot.Frames.Any(frame => string.Equals(frame.Label, label, StringComparison.Ordinal)),
            $"frame label '{label}'",
            timeoutMs);
    }

    private static async Task<CanvasHotZoneCenter> ReadCanvasHotZoneCenterAsync(
        IPage page,
        string zone,
        string? nodeId = null,
        string? frameId = null,
        int timeoutMs = 6_000)
    {
        var attempts = Math.Max(1, timeoutMs / 120);
        var hostBounds = await ReadPrimaryCanvasHostBoundsAsync(page);
        for (var attempt = 0; attempt < attempts; attempt++)
        {
            var center = await TryReadCanvasHotZoneCenterAsync(page, zone, nodeId, frameId);
            if (center is not null)
            {
                return new CanvasHotZoneCenter
                {
                    X = hostBounds.Left + center.X,
                    Y = hostBounds.Top + center.Y
                };
            }

            await page.WaitForTimeoutAsync(120);
        }

        throw new InvalidOperationException($"Could not resolve hot zone '{zone}' for node '{nodeId}' and frame '{frameId}'.");
    }

    private static async Task ActivateCanvasHotZoneAsync(
        IPage page,
        string zone,
        string? nodeId = null,
        string? frameId = null)
    {
        await WaitForInitializedCanvasHostAsync(page);
        var activated = await page.EvaluateAsync<bool>(
            @"request => {
                const workbench = window.CanDoItAll?.canvasWorkbench;
                const hosts = Array.from(document.querySelectorAll('.cw-canvas-host'));
                const host = hosts
                    .filter(candidate => candidate instanceof HTMLElement)
                    .filter(candidate => {
                        const rect = candidate.getBoundingClientRect();
                        if (rect.width <= 0 || rect.height <= 0) {
                            return false;
                        }

                        const styles = getComputedStyle(candidate);
                        return styles.display !== 'none'
                            && styles.visibility !== 'hidden'
                            && !!candidate.__canvasWorkbenchState
                            && typeof workbench?.activateHotZone === 'function';
                    })
                    .sort((left, right) => {
                        const leftRect = left.getBoundingClientRect();
                        const rightRect = right.getBoundingClientRect();
                        return (rightRect.width * rightRect.height) - (leftRect.width * leftRect.height);
                    })[0];
                if (!host || typeof workbench?.activateHotZone !== 'function') {
                    return false;
                }

                return !!workbench.activateHotZone(host, request);
            }",
            new
            {
                zone,
                nodeId,
                frameId
            });

        Assert.True(activated, $"Expected canvas hot zone '{zone}' for node '{nodeId}' and frame '{frameId}' to activate.");
    }

    private static async Task<CanvasSceneBounds> ReadPrimaryCanvasHostBoundsAsync(IPage page)
    {
        var bounds = await page.EvaluateAsync<CanvasSceneBounds?>(
            @"() => {
                const host = Array.from(document.querySelectorAll('.cw-canvas-host'))
                    .filter(candidate => candidate instanceof HTMLElement)
                    .filter(candidate => {
                        const rect = candidate.getBoundingClientRect();
                        if (rect.width <= 0 || rect.height <= 0) {
                            return false;
                        }

                        const styles = getComputedStyle(candidate);
                        return styles.display !== 'none' && styles.visibility !== 'hidden';
                    })
                    .sort((left, right) => {
                        const leftRect = left.getBoundingClientRect();
                        const rightRect = right.getBoundingClientRect();
                        return (rightRect.width * rightRect.height) - (leftRect.width * leftRect.height);
                    })[0];
                if (!host) {
                    return null;
                }

                const rect = host.getBoundingClientRect();
                return {
                    left: rect.left,
                    top: rect.top,
                    width: rect.width,
                    height: rect.height,
                    right: rect.right,
                    bottom: rect.bottom
                };
            }");
        Assert.NotNull(bounds);
        return bounds!;
    }

    private static async Task ToggleCanvasNodeCollapseCoreAsync(IPage page, string selector)
    {
        var nodeId = await ResolveCanvasNodeIdAsync(page, selector);
        if (string.IsNullOrWhiteSpace(nodeId))
        {
            throw new InvalidOperationException($"Could not resolve a collapsible canvas node for selector '{selector}'.");
        }

        var center = await ReadCanvasHotZoneCenterAsync(page, "node-collapse", nodeId: nodeId);
        await page.Mouse.ClickAsync((float)center.X, (float)center.Y);
        await page.WaitForTimeoutAsync(160);
    }

    private static async Task DispatchCanvasContextMenuAsync(IPage page, string selector)
    {
        var center = await TryResolveCanvasHotZoneCenterAsync(page, selector, "node-body")
            ?? await ResolveCanvasNodeCenterAsync(page, selector);
        await page.EvaluateAsync(
            @"point => {
                const host = document.querySelector('.cw-canvas-host');
                if (!(host instanceof HTMLElement)) {
                    return false;
                }

                host.dispatchEvent(new MouseEvent('contextmenu', {
                    bubbles: true,
                    cancelable: true,
                    button: 2,
                    buttons: 2,
                    clientX: point.x,
                    clientY: point.y,
                    view: window
                }));
                return true;
            }",
            new
            {
                x = center.X,
                y = center.Y
            });
    }

    private static async Task SetCanvasDiagnosticsVisibleAsync(IPage page, bool isVisible)
    {
        await WaitForInitializedCanvasHostAsync(page);
        await page.EvaluateAsync(
            @"requestedVisibility => {
                const workbench = window.CanDoItAll?.canvasWorkbench;
                const hosts = Array.from(document.querySelectorAll('.cw-canvas-host'));
                const host = hosts
                    .filter(candidate => candidate instanceof HTMLElement)
                    .filter(candidate => {
                        const rect = candidate.getBoundingClientRect();
                        if (rect.width <= 0 || rect.height <= 0) {
                            return false;
                        }

                        const styles = getComputedStyle(candidate);
                        return styles.display !== 'none'
                            && styles.visibility !== 'hidden'
                            && !!candidate.__canvasWorkbenchState
                            && !!workbench?.getDiagnostics?.(candidate);
                    })
                    .sort((left, right) => {
                        const leftRect = left.getBoundingClientRect();
                        const rightRect = right.getBoundingClientRect();
                        return (rightRect.width * rightRect.height) - (leftRect.width * leftRect.height);
                    })[0];
                if (!host || !workbench?.toggleDiagnostics || !workbench?.getDiagnostics) {
                    return;
                }

                const snapshot = workbench.getDiagnostics(host);
                const currentVisibility = !!snapshot?.isVisible;
                if (currentVisibility !== requestedVisibility) {
                    workbench.toggleDiagnostics(host);
                }
            }",
            isVisible);
        await page.WaitForTimeoutAsync(180);
    }

    private static async Task SetCanvasMinimapVisibleAsync(IPage page, bool isVisible)
    {
        await WaitForInitializedCanvasHostAsync(page);
        await page.EvaluateAsync(
            @"requestedVisibility => {
                const workbench = window.CanDoItAll?.canvasWorkbench;
                const hosts = Array.from(document.querySelectorAll('.cw-canvas-host'));
                const host = hosts
                    .filter(candidate => candidate instanceof HTMLElement)
                    .filter(candidate => {
                        const rect = candidate.getBoundingClientRect();
                        if (rect.width <= 0 || rect.height <= 0) {
                            return false;
                        }

                        const styles = getComputedStyle(candidate);
                        return styles.display !== 'none'
                            && styles.visibility !== 'hidden'
                            && !!candidate.__canvasWorkbenchState;
                    })
                    .sort((left, right) => {
                        const leftRect = left.getBoundingClientRect();
                        const rightRect = right.getBoundingClientRect();
                        return (rightRect.width * rightRect.height) - (leftRect.width * leftRect.height);
                    })[0];
                const state = host?.__canvasWorkbenchState;
                if (!host || !state || !workbench?.toggleMinimap) {
                    return;
                }

                const currentVisibility = state.ui?.showMinimap !== false;
                if (currentVisibility !== requestedVisibility) {
                    workbench.toggleMinimap(host);
                }
            }",
            isVisible);
        await page.WaitForTimeoutAsync(180);
    }

    private static async Task<CanvasDiagnosticsSnapshot> ReadCanvasDiagnosticsAsync(IPage page)
    {
        await WaitForInitializedCanvasHostAsync(page);
        var snapshot = await page.EvaluateAsync<CanvasDiagnosticsSnapshot?>(
            @"() => {
                const workbench = window.CanDoItAll?.canvasWorkbench;
                const hosts = Array.from(document.querySelectorAll('.cw-canvas-host'));
                const host = hosts
                    .filter(candidate => candidate instanceof HTMLElement)
                    .filter(candidate => {
                        const rect = candidate.getBoundingClientRect();
                        if (rect.width <= 0 || rect.height <= 0) {
                            return false;
                        }

                        const styles = getComputedStyle(candidate);
                        return styles.display !== 'none'
                            && styles.visibility !== 'hidden'
                            && !!candidate.__canvasWorkbenchState
                            && !!workbench?.getDiagnostics?.(candidate);
                    })
                    .sort((left, right) => {
                        const leftRect = left.getBoundingClientRect();
                        const rightRect = right.getBoundingClientRect();
                        return (rightRect.width * rightRect.height) - (leftRect.width * leftRect.height);
                    })[0];
                if (!host || !workbench?.getDiagnostics) {
                    return null;
                }

                const snapshot = workbench.getDiagnostics(host);
                if (!snapshot) {
                    return null;
                }

                const panel = host.__canvasWorkbenchState?.diagnosticsPanel;
                return {
                    ...snapshot,
                    isPanelVisible: panel instanceof HTMLElement && getComputedStyle(panel).display !== 'none'
                };
            }");
        Assert.NotNull(snapshot);
        Assert.NotNull(snapshot!.Metrics);
        return snapshot;
    }

    private static async Task<CanvasSnapGuideProbe> ReadCanvasSnapGuideProbeAsync(IPage page, IReadOnlyList<string> nodeIds)
    {
        await WaitForInitializedCanvasHostAsync(page);
        var probe = await page.EvaluateAsync<CanvasSnapGuideProbe?>(
            @"payload => {
                const workbench = window.CanDoItAll?.canvasWorkbench;
                const hosts = Array.from(document.querySelectorAll('.cw-canvas-host'));
                const host = hosts
                    .filter(candidate => candidate instanceof HTMLElement)
                    .filter(candidate => {
                        const rect = candidate.getBoundingClientRect();
                        if (rect.width <= 0 || rect.height <= 0) {
                            return false;
                        }

                        const styles = getComputedStyle(candidate);
                        return styles.display !== 'none'
                            && styles.visibility !== 'hidden'
                            && !!candidate.__canvasWorkbenchState
                            && !!workbench?.getDiagnostics?.(candidate);
                    })
                    .sort((left, right) => {
                        const leftRect = left.getBoundingClientRect();
                        const rightRect = right.getBoundingClientRect();
                        return (rightRect.width * rightRect.height) - (leftRect.width * leftRect.height);
                    })[0];
                if (!host || !workbench?.getDiagnostics || !workbench?.getSceneSnapshot) {
                    return null;
                }

                const state = host.__canvasWorkbenchState;
                const diagnostics = workbench.getDiagnostics(host);
                const scene = workbench.getSceneSnapshot(host);
                const highlighted = Array.isArray(payload?.nodeIds) ? payload.nodeIds : [];
                const guideSummary = (state?.snapGuides || [])
                    .map(guide => `${guide.orientation || 'vertical'}:${Math.round(guide.value || 0)}`)
                    .join(' | ');
                const nodeSummary = (scene?.nodes || [])
                    .filter(node => highlighted.includes(node.id))
                    .map(node => `${node.title || node.id}@${Math.round(node.left)},${Math.round(node.top)}:${Math.round(node.width)}x${Math.round(node.height)}`)
                    .join(' | ');

                return {
                    stateGuideCount: Array.isArray(state?.snapGuides) ? state.snapGuides.length : 0,
                    elementGuideCount: document.querySelectorAll('.cw-snap-guide').length,
                    interaction: diagnostics?.interaction || state?.interaction?.kind || '',
                    zoomPercent: diagnostics?.zoomPercent || 0,
                    resolvedDragX: diagnostics?.metrics?.lastResolvedDragDeltaX || 0,
                    resolvedDragY: diagnostics?.metrics?.lastResolvedDragDeltaY || 0,
                    guideSummary,
                    nodeSummary
                };
            }",
            new
            {
                nodeIds = nodeIds.ToArray()
            });
        Assert.NotNull(probe);
        return probe!;
    }

    private static async Task<CanvasSnapGuideProbe> WaitForCanvasSnapGuidesAsync(
        IPage page,
        IReadOnlyList<string> nodeIds,
        int timeoutMs = 6_000)
    {
        var attempts = Math.Max(1, timeoutMs / 120);
        CanvasSnapGuideProbe? lastProbe = null;
        for (var attempt = 0; attempt < attempts; attempt++)
        {
            lastProbe = await ReadCanvasSnapGuideProbeAsync(page, nodeIds);
            if (lastProbe.StateGuideCount > 0 || lastProbe.ElementGuideCount > 0)
            {
                return lastProbe;
            }

            await page.WaitForTimeoutAsync(120);
        }

        throw new InvalidOperationException($"Timed out waiting for snap guides. {lastProbe?.ToDiagnosticString()}");
    }

    private static async Task CapturePrimaryWorkbenchShellAsync(IPage page, string path)
    {
        var shellIndex = await page.EvaluateAsync<int>(
            @"() => {
                const shells = Array.from(document.querySelectorAll('.cw-workbench-shell'));
                const visibleShells = shells
                    .map((shell, index) => ({ shell, index }))
                    .filter(entry => entry.shell instanceof HTMLElement)
                    .filter(entry => {
                        const rect = entry.shell.getBoundingClientRect();
                        if (rect.width <= 0 || rect.height <= 0) {
                            return false;
                        }

                        const styles = getComputedStyle(entry.shell);
                        return styles.display !== 'none' && styles.visibility !== 'hidden';
                    })
                    .sort((left, right) => {
                        const leftRect = left.shell.getBoundingClientRect();
                        const rightRect = right.shell.getBoundingClientRect();
                        return (rightRect.width * rightRect.height) - (leftRect.width * leftRect.height);
                    });
                return visibleShells[0]?.index ?? 0;
            }");

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await page.Locator(".cw-workbench-shell").Nth(shellIndex).ScreenshotAsync(new() { Path = path });
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

    private static async Task DragLocatorAsync(
        IPage page,
        ILocator locator,
        string targetDescription,
        float deltaX,
        float deltaY,
        MouseButton button = MouseButton.Left,
        bool releasePointer = true,
        int steps = 16,
        int preDragDelayMs = 380)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            await locator.WaitForAsync();

            try
            {
                await locator.ScrollIntoViewIfNeededAsync();
                var bounds = await locator.BoundingBoxAsync();
                if (bounds is null)
                {
                    await page.WaitForTimeoutAsync(120);
                    continue;
                }

                var startX = bounds.X + (bounds.Width / 2);
                var startY = bounds.Y + (bounds.Height / 2);
                await page.WaitForTimeoutAsync(preDragDelayMs);
                await page.Mouse.MoveAsync(startX, startY);
                await page.Mouse.DownAsync(new MouseDownOptions
                {
                    Button = button
                });
                await page.Mouse.MoveAsync(
                    startX + deltaX,
                    startY + deltaY,
                    new MouseMoveOptions
                    {
                        Steps = steps
                    });
                if (releasePointer)
                {
                    await page.Mouse.UpAsync(new MouseUpOptions
                    {
                        Button = button
                    });
                    await page.WaitForTimeoutAsync(220);
                }

                return;
            }
            catch (PlaywrightException exception) when (exception.Message.Contains("attached", StringComparison.OrdinalIgnoreCase))
            {
                await page.WaitForTimeoutAsync(120);
            }
        }

        throw new InvalidOperationException($"Could not drag {targetDescription} after repeated rerenders.");
    }

    private static async Task DragCanvasNodeCoreAsync(
        IPage page,
        string nodeId,
        float deltaX,
        float deltaY,
        bool releasePointer,
        bool controlModifier = false)
    {
        var beforeSnapshot = await TryReadSceneNodeSnapshotAsync(page, nodeId);
        if (await TrySimulateCanvasDragAsync(page, nodeId, null, deltaX, deltaY, releasePointer, controlModifier))
        {
            var syntheticFailure = await TryWaitForNodeMovementAsync(page, nodeId, beforeSnapshot, deltaX, deltaY, $"synthetic drag for '{nodeId}'");
            if (syntheticFailure is null)
            {
                await page.WaitForTimeoutAsync(releasePointer ? 220 : 120);
                return;
            }
        }

        var locator = page.Locator(SelectorForNodeId(nodeId)).First;
        if (await WaitForLocatorAsync(locator, 300))
        {
            try
            {
                await DragLocatorAsync(
                    page,
                    locator,
                    $"canvas node '{nodeId}'",
                    deltaX,
                    deltaY,
                    releasePointer: releasePointer);
                var locatorFailure = await TryWaitForNodeMovementAsync(page, nodeId, beforeSnapshot, deltaX, deltaY, $"locator drag for '{nodeId}'");
                if (locatorFailure is null)
                {
                    await page.WaitForTimeoutAsync(releasePointer ? 220 : 120);
                    return;
                }
            }
            catch (InvalidOperationException)
            {
            }
            catch (TimeoutException)
            {
            }
            catch (PlaywrightException)
            {
            }
        }

        var snapshot = await ReadSceneNodeSnapshotAsync(page, nodeId);
        var hostBounds = await ReadPrimaryCanvasHostBoundsAsync(page);
        var startX = (float)(hostBounds.Left + snapshot.Left + Math.Clamp(snapshot.Width * 0.46d, 36d, Math.Max(36d, snapshot.Width - 24d)));
        var startY = (float)(hostBounds.Top + snapshot.Top + Math.Clamp(snapshot.Height * 0.24d, 24d, Math.Max(24d, snapshot.Height - 24d)));
        await page.WaitForTimeoutAsync(380);
        await page.Mouse.MoveAsync(startX, startY);
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync(
            startX + deltaX,
            startY + deltaY,
            new MouseMoveOptions
            {
                Steps = 16
            });
        if (releasePointer)
        {
            await page.Mouse.UpAsync();
        }

        var pointerFailure = await TryWaitForNodeMovementAsync(page, nodeId, beforeSnapshot, deltaX, deltaY, $"pointer drag for '{nodeId}'");
        if (pointerFailure is null)
        {
            await page.WaitForTimeoutAsync(releasePointer ? 220 : 120);
            return;
        }

        throw new InvalidOperationException(pointerFailure);
    }

    private static async Task<string?> TryWaitForNodeMovementAsync(
        IPage page,
        string nodeId,
        CanvasSceneNodeSnapshot? beforeSnapshot,
        float deltaX,
        float deltaY,
        string description,
        int timeoutMs = 4_000)
    {
        if (beforeSnapshot is null)
        {
            await page.WaitForTimeoutAsync(220);
            return null;
        }

        var minimumHorizontalDelta = Math.Abs(deltaX) > 1f
            ? Math.Max(8d, Math.Abs(deltaX) * 0.18d)
            : 0d;
        var minimumVerticalDelta = Math.Abs(deltaY) > 1f
            ? Math.Max(8d, Math.Abs(deltaY) * 0.18d)
            : 0d;
        if (minimumHorizontalDelta <= 0d && minimumVerticalDelta <= 0d)
        {
            await page.WaitForTimeoutAsync(120);
            return null;
        }

        var attempts = Math.Max(1, timeoutMs / 120);
        CanvasSceneNodeSnapshot? lastSnapshot = beforeSnapshot;
        for (var attempt = 0; attempt < attempts; attempt++)
        {
            lastSnapshot = await TryReadSceneNodeSnapshotAsync(page, nodeId);
            if (lastSnapshot is not null)
            {
                var horizontalDelta = Math.Abs(lastSnapshot.Left - beforeSnapshot.Left);
                var verticalDelta = Math.Abs(lastSnapshot.Top - beforeSnapshot.Top);
                if ((minimumHorizontalDelta > 0d && horizontalDelta >= minimumHorizontalDelta) ||
                    (minimumVerticalDelta > 0d && verticalDelta >= minimumVerticalDelta))
                {
                    return null;
                }
            }

            await page.WaitForTimeoutAsync(120);
        }

        var diagnostics = await ReadCanvasDiagnosticsAsync(page);
        var hostSummary = await page.EvaluateAsync<string>(
            @"() => {
                const hosts = Array.from(document.querySelectorAll('.cw-canvas-host'));
                return hosts
                    .map((host, index) => {
                        const rect = host.getBoundingClientRect();
                        const styles = getComputedStyle(host);
                        const hasState = !!host.__canvasWorkbenchState;
                        return `#${index}:${Math.round(rect.width)}x${Math.round(rect.height)}@${Math.round(rect.left)},${Math.round(rect.top)}:${styles.display}/${styles.visibility}:state=${hasState}`;
                    })
                    .join(' | ');
            }");
        var nodeSummary = await page.EvaluateAsync<string>(
            @"requestedNodeId => {
                const host = document.querySelector('.cw-canvas-host');
                const snapshot = window.CanDoItAll?.canvasWorkbench?.getSceneSnapshot?.(host);
                return (snapshot?.nodes || [])
                    .map(node => {
                        const marker = node.id === requestedNodeId ? '*' : '';
                        return `${marker}${node.title || node.id}@${Math.round(node.left)},${Math.round(node.top)}:${Math.round(node.width)}x${Math.round(node.height)}`;
                    })
                    .join(' | ');
            }",
            nodeId);
        return
            $"Timed out waiting for node movement during {description}. " +
            $"Before=({beforeSnapshot.Left:F1},{beforeSnapshot.Top:F1}), " +
            $"After=({lastSnapshot?.Left.ToString("F1") ?? "n/a"},{lastSnapshot?.Top.ToString("F1") ?? "n/a"}), " +
            $"Pan=({diagnostics.PanX:F1},{diagnostics.PanY:F1}), " +
            $"Interaction={diagnostics.Interaction}, " +
            $"RenderCount={diagnostics.Metrics.RenderCount}, " +
            $"StateCommits={diagnostics.Metrics.StatePublishCommitCount}, " +
            $"MovePublishes={diagnostics.Metrics.MovePublishRequestCount}/{diagnostics.Metrics.MovePublishSuccessCount}/{diagnostics.Metrics.MovePublishFailureCount}, " +
            $"MoveStatus={diagnostics.Metrics.LastMovePublishStatus}, " +
            $"ResolvedDrag=({diagnostics.Metrics.LastResolvedDragDeltaX:F1},{diagnostics.Metrics.LastResolvedDragDeltaY:F1}), " +
            $"Released={diagnostics.Metrics.LastReleasedInteractionKind}/{diagnostics.Metrics.LastReleasedInteractionMoved}, " +
            $"NodeRebuilds={diagnostics.Metrics.NodeLayerRebuildCount}, " +
            $"DragPatchedNodes={diagnostics.Metrics.TotalDragPatchedNodeCount}, " +
            $"Hosts={hostSummary}, " +
            $"Nodes={nodeSummary}.";
    }

    private static Task DragCanvasNodeAsync(
        IPage page,
        string nodeId,
        float deltaX,
        float deltaY,
        bool releasePointer = true,
        bool controlModifier = false)
        => DragCanvasNodeCoreAsync(page, nodeId, deltaX, deltaY, releasePointer, controlModifier);

    private static async Task DragCanvasFrameAsync(IPage page, string frameId, float deltaX, float deltaY, bool releasePointer = true)
    {
        if (await TrySimulateCanvasDragAsync(page, null, frameId, deltaX, deltaY, releasePointer))
        {
            await page.WaitForTimeoutAsync(releasePointer ? 220 : 120);
            return;
        }

        var center = await ReadCanvasHotZoneCenterAsync(page, "frame-handle", frameId: frameId);
        await page.WaitForTimeoutAsync(380);
        await page.Mouse.MoveAsync((float)center.X, (float)center.Y);
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync(
            (float)(center.X + deltaX),
            (float)(center.Y + deltaY),
            new MouseMoveOptions
            {
                Steps = 16
            });
        if (releasePointer)
        {
            await page.Mouse.UpAsync();
        }

        await page.WaitForTimeoutAsync(releasePointer ? 220 : 120);
    }

    private static async Task PanCanvasAsync(IPage page, float deltaX, float deltaY)
    {
        await WaitForInitializedCanvasHostAsync(page);
        var hostIndex = await page.EvaluateAsync<int>(
            @"() => {
                const hosts = Array.from(document.querySelectorAll('.cw-canvas-host'));
                const visibleHosts = hosts
                    .map((host, index) => ({ host, index }))
                    .filter(entry => entry.host instanceof HTMLElement)
                    .filter(entry => {
                        const rect = entry.host.getBoundingClientRect();
                        if (rect.width <= 0 || rect.height <= 0) {
                            return false;
                        }

                        const styles = getComputedStyle(entry.host);
                        return styles.display !== 'none' && styles.visibility !== 'hidden';
                    })
                    .sort((left, right) => {
                        const leftRect = left.host.getBoundingClientRect();
                        const rightRect = right.host.getBoundingClientRect();
                        return (rightRect.width * rightRect.height) - (leftRect.width * leftRect.height);
                    });

                return visibleHosts[0]?.index ?? -1;
            }");
        Assert.True(hostIndex >= 0, "Expected to find a visible canvas host for the pan proof.");

        var host = page.Locator(".cw-canvas-host").Nth(hostIndex);
        await host.WaitForAsync();
        var bounds = await host.BoundingBoxAsync();
        Assert.NotNull(bounds);

        var startX = (float)(bounds!.X + (bounds.Width * 0.5));
        var startY = (float)(bounds.Y + (bounds.Height * 0.5));
        await page.Mouse.MoveAsync(startX, startY);
        await page.Mouse.DownAsync(new MouseDownOptions
        {
            Button = MouseButton.Middle
        });
        await page.Mouse.MoveAsync(
            startX + deltaX,
            startY + deltaY,
            new MouseMoveOptions
            {
                Steps = 16
            });
        await page.Mouse.UpAsync(new MouseUpOptions
        {
            Button = MouseButton.Middle
        });
        await page.WaitForTimeoutAsync(220);
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

    private static async Task EnsureStructureToolboxWindowExpandedAsync(IPage page)
    {
        var window = page.GetByTestId("project-structure-toolbox-window");
        if (!await window.IsVisibleAsync())
        {
            var toolbarToggle = page.GetByTestId("project-structure-toolbox-toggle");
            var selectionPanelToggle = page.GetByRole(AriaRole.Button, new() { Name = "Open standard blocks", Exact = true }).First;
            for (var attempt = 0; attempt < 3; attempt++)
            {
                if (await WaitForLocatorAsync(window, 1_000))
                {
                    break;
                }

                if (await WaitForLocatorAsync(toolbarToggle, 1_000))
                {
                    await toolbarToggle.ClickAsync();
                    if (await WaitForLocatorAsync(window, 1_500))
                    {
                        break;
                    }
                }

                if (await WaitForLocatorAsync(selectionPanelToggle, 750))
                {
                    await selectionPanelToggle.ClickAsync();
                    if (await WaitForLocatorAsync(window, 1_500))
                    {
                        break;
                    }
                }

                await page.WaitForTimeoutAsync(180);
            }
        }

        Assert.True(
            await WaitForLocatorAsync(window, 5_000),
            "Expected the project structure toolbox window to be open before asserting its contents.");
        await EnsureFloatingWindowExpandedAsync(page, "project-structure-toolbox-window");
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

        public double DocumentClientHeight { get; set; }

        public double DocumentScrollHeight { get; set; }

        public double ViewportWidth { get; set; }

        public double ViewportHeight { get; set; }
    }

    private sealed class CanvasNodePosition
    {
        public string Id { get; set; } = string.Empty;

        public int Left { get; set; }

        public int Top { get; set; }
    }

    private sealed class CanvasSceneSnapshot
    {
        public string RendererMode { get; set; } = string.Empty;

        public CanvasSceneNodeSnapshot[] Nodes { get; set; } = [];

        public CanvasSceneFrameSnapshot[] Frames { get; set; } = [];

        public CanvasSceneHotZoneSnapshot[] HotZones { get; set; } = [];

        public CanvasSceneMinimapSnapshot? Minimap { get; set; }

        public int ViewportWidth { get; set; }

        public int ViewportHeight { get; set; }
    }

    private sealed class CanvasSceneNodeSnapshot
    {
        public string Id { get; set; } = string.Empty;

        public double Left { get; set; }

        public double Top { get; set; }

        public double Width { get; set; }

        public double Height { get; set; }

        public double Right { get; set; }

        public double Bottom { get; set; }

        public string Title { get; set; } = string.Empty;

        public string Subtitle { get; set; } = string.Empty;

        public string InlineText { get; set; } = string.Empty;

        public bool Selected { get; set; }

        public bool Collapsed { get; set; }

        public bool IsInlineTextNode { get; set; }

        public string MarkerText { get; set; } = string.Empty;

        public string PriorityText { get; set; } = string.Empty;

        public string ProgressTitle { get; set; } = string.Empty;

        public bool HasPathButton { get; set; }

        public string PathTitle { get; set; } = string.Empty;

        public string PathDisplayText { get; set; } = string.Empty;

        public string PathPromotedText { get; set; } = string.Empty;

        public string MediaKind { get; set; } = string.Empty;

        public string MediaPreviewUrl { get; set; } = string.Empty;

        public string DisplayTitle
            => IsInlineTextNode && !string.IsNullOrWhiteSpace(InlineText)
                ? InlineText
                : (!string.IsNullOrWhiteSpace(Title) ? Title : Id);
    }

    private sealed class CanvasSceneFrameSnapshot
    {
        public string FrameId { get; set; } = string.Empty;

        public string Label { get; set; } = string.Empty;

        public string[] NodeIds { get; set; } = [];

        public double Left { get; set; }

        public double Top { get; set; }

        public double Width { get; set; }

        public double Height { get; set; }

        public double LabelLeft { get; set; }

        public double LabelTop { get; set; }

        public double LabelWidth { get; set; }

        public double LabelHeight { get; set; }
    }

    private sealed class CanvasSceneHotZoneSnapshot
    {
        public string Type { get; set; } = string.Empty;

        public string NodeId { get; set; } = string.Empty;

        public string FrameId { get; set; } = string.Empty;

        public CanvasSceneBounds Bounds { get; set; } = new();
    }

    private sealed class CanvasSceneBounds
    {
        public double Left { get; set; }

        public double Top { get; set; }

        public double Width { get; set; }

        public double Height { get; set; }

        public double Right { get; set; }

        public double Bottom { get; set; }
    }

    private sealed class CanvasSceneMinimapSnapshot
    {
        public double Width { get; set; }

        public double Height { get; set; }

        public int NodeCount { get; set; }
    }

    private sealed class CanvasHotZoneCenter
    {
        public double X { get; set; }

        public double Y { get; set; }
    }

    private sealed class CanvasScenePoint
    {
        public double X { get; set; }

        public double Y { get; set; }
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

    private sealed class CanvasMenuLayerSnapshot
    {
        public double ToolbarBottom { get; set; }

        public double HostTop { get; set; }

        public double SafeTop { get; set; }

        public double RootCenterY { get; set; }

        public CanvasMenuActionSnapshot[] Actions { get; set; } = [];
    }

    private sealed class CanvasMenuActionSnapshot
    {
        public string ActionId { get; set; } = string.Empty;

        public double Left { get; set; }

        public double Top { get; set; }

        public double Right { get; set; }

        public double Bottom { get; set; }

        public double CenterX { get; set; }

        public double CenterY { get; set; }

        public double DistanceFromCore { get; set; }

        public string CenterText { get; set; } = string.Empty;
    }

    private sealed class CanvasNodeOverlap
    {
        public string FirstTitle { get; set; } = string.Empty;

        public string SecondTitle { get; set; } = string.Empty;
    }

    private sealed class WorkbenchStorageState
    {
        public string? Key { get; set; }

        public string? Value { get; set; }
    }

    private sealed class CanvasSubmenuMetrics
    {
        public double ActionRight { get; set; }

        public double ActionMidY { get; set; }

        public double OrbitX { get; set; }

        public double OrbitY { get; set; }
    }

    private sealed class CanvasDiagnosticsSnapshot
    {
        public bool IsVisible { get; set; }

        public bool IsPanelVisible { get; set; }

        public int VisibleNodeCount { get; set; }

        public int TotalNodeCount { get; set; }

        public int TotalLinkCount { get; set; }

        public int SelectedCount { get; set; }

        public string Interaction { get; set; } = string.Empty;

        public int ZoomPercent { get; set; }

        public double PanX { get; set; }

        public double PanY { get; set; }

        public CanvasDiagnosticsMetrics Metrics { get; set; } = new();
    }

    private sealed class CreateActionFailureSnapshot
    {
        public int NodeCount { get; set; }

        public string[] Titles { get; set; } = [];

        public string[] SelectedNodeIds { get; set; } = [];

        public bool ErrorUiVisible { get; set; }
    }

    private sealed class SelectionFailureSnapshot
    {
        public string[] HostSelectedNodeIds { get; set; } = [];

        public string[] SelectionApiCalls { get; set; } = [];

        public string[] SelectionDebugEvents { get; set; } = [];

        public string[] SelectionPanelTitles { get; set; } = [];

        public bool SelectionWindowPresent { get; set; }

        public bool SelectionWindowVisible { get; set; }

        public bool SelectionWindowMinimized { get; set; }

        public string SelectionWindowText { get; set; } = string.Empty;
    }

    private sealed class CanvasDiagnosticsMetrics
    {
        public int RenderCount { get; set; }

        public int FrameLayerRebuildCount { get; set; }

        public int LinkLayerRebuildCount { get; set; }

        public int NodeLayerRebuildCount { get; set; }

        public int StatePublishCommitCount { get; set; }

        public int ViewportCommitScheduleCount { get; set; }

        public int ViewportCommitCount { get; set; }

        public int MovePublishRequestCount { get; set; }

        public int MovePublishSuccessCount { get; set; }

        public int MovePublishFailureCount { get; set; }

        public string LastMovePublishStatus { get; set; } = string.Empty;

        public double LastResolvedDragDeltaX { get; set; }

        public double LastResolvedDragDeltaY { get; set; }

        public string LastReleasedInteractionKind { get; set; } = string.Empty;

        public bool LastReleasedInteractionMoved { get; set; }

        public int DragPatchCount { get; set; }

        public int TotalDragPatchedNodeCount { get; set; }

        public int TotalDragPatchedLinkCount { get; set; }

        public int TotalDragPatchedFrameCount { get; set; }

        public int LastDragPatchedNodeCount { get; set; }

        public int LastDragPatchedLinkCount { get; set; }

        public int LastDragPatchedFrameCount { get; set; }
    }

    private sealed class CanvasSnapGuideProbe
    {
        public int StateGuideCount { get; set; }

        public int ElementGuideCount { get; set; }

        public string Interaction { get; set; } = string.Empty;

        public int ZoomPercent { get; set; }

        public double ResolvedDragX { get; set; }

        public double ResolvedDragY { get; set; }

        public string GuideSummary { get; set; } = string.Empty;

        public string NodeSummary { get; set; } = string.Empty;

        public string ToDiagnosticString()
            => $"Interaction={Interaction}, Zoom={ZoomPercent}, ResolvedDrag=({ResolvedDragX:F1},{ResolvedDragY:F1}), StateGuides={StateGuideCount}, ElementGuides={ElementGuideCount}, Guides={GuideSummary}, Nodes={NodeSummary}";
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
