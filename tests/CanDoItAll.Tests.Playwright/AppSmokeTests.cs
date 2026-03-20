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
        await page.GetByLabel("Canvas zoom").WaitForAsync();
        await page.GetByLabel("Open quick create actions").ClickAsync();
        await page.Locator(".cw-create-rail").GetByRole(AriaRole.Button, new() { Name = "Note", Exact = true }).WaitForAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Help" }).ClickAsync();
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
        await page.GetByTestId("prompt-factory-project").SelectOptionAsync(projectId.ToString());
        await page.Locator(".cw-workbench-shell").WaitForAsync();
        await page.GetByLabel("Canvas zoom").WaitForAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Help" }).ClickAsync();
        await page.GetByRole(AriaRole.Dialog, new() { Name = "Canvas shortcuts and gestures" }).WaitForAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Close help" }).ClickAsync();
        await page.GetByTestId("prompt-factory-build").WaitForAsync();
        await page.GetByTestId("prompt-factory-build").ClickAsync();
        await page.Locator(".cw-panel-card:has-text('Generated prompt') textarea").WaitForAsync();
        Assert.False(await page.Locator("#blazor-error-ui").IsVisibleAsync());
    }

    [Fact]
    public async Task Structure_canvas_supports_inline_note_creation_editing_and_context_create_dialogs()
    {
        await using var context = await fixture.Browser.NewContextAsync();
        var page = await context.NewPageAsync();

        await CreateProjectAsync(page, "Playwright Canvas Repair", "Discovery");

        var canvasHost = page.Locator(".cw-canvas-host");
        await canvasHost.ScrollIntoViewIfNeededAsync();
        await ClickCanvasNodeAsync(page, ".cw-node[data-node-id^='project:']");
        await canvasHost.FocusAsync();

        var radialLabels = await OpenCanvasContextMenuAsync(page, ".cw-node[data-node-id^='project:']");
        Assert.Contains("Link", radialLabels);
        Assert.Contains("Secret", radialLabels);
        Assert.Contains("Session", radialLabels);
        Assert.True(radialLabels.Length >= 15, $"Expected the project-node radial menu to expose the broad create palette, got {radialLabels.Length} actions.");
        await page.Keyboard.PressAsync("Escape");

        await page.Keyboard.PressAsync("Tab");
        var noteEditor = page.Locator(".cw-note-editor__input");
        await noteEditor.WaitForAsync();
        await noteEditor.FillAsync("Child note from keyboard");
        await noteEditor.PressAsync("Enter");
        await page.WaitForSelectorAsync("text=Child note from keyboard");
        await page.WaitForFunctionAsync("() => !!document.querySelector('.cw-node.is-selected.is-inline-text')");

        await page.Keyboard.PressAsync("Tab");
        await noteEditor.WaitForAsync();
        await noteEditor.FillAsync("Second child note");
        await noteEditor.PressAsync("Enter");
        await page.WaitForSelectorAsync("text=Second child note");
        await page.WaitForFunctionAsync("() => Array.from(document.querySelectorAll('.cw-node.is-selected .cw-note-node__text')).some(node => node.textContent?.includes('Second child note'))");

        await ClickCanvasNodeAsync(page, ".cw-node.is-inline-text", clickCount: 2);
        await noteEditor.WaitForAsync();
        await noteEditor.FillAsync("Edited child note");
        await noteEditor.PressAsync("Enter");
        await page.WaitForSelectorAsync("text=Edited child note");
        await page.WaitForFunctionAsync("() => Array.from(document.querySelectorAll('.cw-node.is-selected .cw-note-node__text')).some(node => node.textContent?.includes('Edited child note'))");

        await page.Keyboard.PressAsync("Enter");
        await noteEditor.WaitForAsync();
        await noteEditor.FillAsync("Sibling note from Enter");
        await noteEditor.PressAsync("Enter");
        await page.WaitForSelectorAsync("text=Sibling note from Enter");
        await page.WaitForFunctionAsync("() => Array.from(document.querySelectorAll('.cw-node.is-selected .cw-note-node__text')).some(node => node.textContent?.includes('Sibling note from Enter'))");

        await OpenCanvasCreateComposerAsync(page, ".cw-node[data-node-id^='project:']", "Link", "add-link");
        await page.WaitForSelectorAsync("text=Address");
        await page.Locator(".cw-canvas-composer__input").Nth(0).FillAsync("API reference");
        await page.Locator(".cw-canvas-composer__input").Nth(1).FillAsync("https://example.test/api");
        await page.Locator(".cw-canvas-composer__textarea").FillAsync("Reference for downstream build steps");
        await page.Locator(".cw-canvas-composer__actions").GetByRole(AriaRole.Button, new() { Name = "Link", Exact = true }).ClickAsync();
        await page.WaitForSelectorAsync("text=API reference");
        await page.WaitForFunctionAsync("() => Array.from(document.querySelectorAll('.cw-node.is-selected .cw-node__title')).some(node => node.textContent?.includes('API reference'))");
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
        var node = page.Locator(selector).First;
        await node.WaitForAsync();
        await node.ScrollIntoViewIfNeededAsync();

        var bounds = await node.BoundingBoxAsync();
        Assert.NotNull(bounds);

        await page.Mouse.ClickAsync(
            bounds!.X + (bounds.Width / 2),
            bounds.Y + (bounds.Height / 2),
            new MouseClickOptions
            {
                Button = button,
                ClickCount = clickCount
            });
    }

    private static async Task OpenCanvasCreateComposerAsync(IPage page, string selector, string label, string? actionId = null)
    {
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
                action?.click();
                return !!document.querySelector('.cw-canvas-composer__input');
            }",
            new { selector, label, actionId });

        Assert.True(composerOpened, $"Expected the radial menu action '{label}' to open the in-canvas composer.");
    }

    private static async Task<string[]> OpenCanvasContextMenuAsync(IPage page, string selector)
    {
        return await page.EvaluateAsync<string[]>(
            @"selector => {
                const node = document.querySelector(selector);
                if (!node) {
                    return [];
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

                return Array.from(document.querySelectorAll('.cw-context-menu__label'))
                    .map(label => label.textContent?.trim() || '')
                    .filter(Boolean);
            }",
            selector);
    }
}
