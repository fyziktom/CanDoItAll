using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright.Smoke;

public sealed partial class AppSmokeTests
{
    [Fact]
    public async Task Project_structure_concurrent_edit_preserves_the_newer_commit_and_unsaved_draft() {
        await using var firstContext = await fixture.Browser.NewContextAsync(new() {
            ViewportSize = new() { Width = 1920, Height = 1080 }, DeviceScaleFactor = 1
        });
        await using var secondContext = await fixture.Browser.NewContextAsync(new() {
            ViewportSize = new() { Width = 1920, Height = 1080 }, DeviceScaleFactor = 1
        });
        var first = await firstContext.NewPageAsync();
        var browserErrors = new List<string>();
        first.PageError += (_, error) => browserErrors.Add(error);
        var projectId = await CreateProjectAsync(first, "Concurrent structure edit", "Validation");
        const string original = "Shared editable link";
        const string accepted = "Accepted newer title";
        const string draft = "My unsaved title";
        var notes = string.Join('\n', Enumerable.Range(1, 36).Select(index => $"Draft line {index}: preserve this text after a conflicting save."));
        try {
            await CreateWebLinkAsync(first, original, "https://example.com/concurrent-edit", "Original notes");
        } catch (Exception failure) {
            var composerText = string.Join("; ", await first.Locator(".cw-canvas-composer").AllTextContentsAsync());
            throw new InvalidOperationException($"Creation failed. Browser: {string.Join("; ", browserErrors)}. Composer: {composerText}. Host: {fixture.GetLogSnapshot(35)}", failure);
        }
        await PhysicalRightClickCanvasNodeAsync(first, $".cw-node:has-text('{original}')");
        await (await AssertSingleVisibleEditActionAsync(first)).ClickAsync();
        var composer = first.Locator(".cw-canvas-composer.is-dialog");
        await composer.Locator(".cw-canvas-composer__input").Nth(0).FillAsync(draft);
        await composer.Locator(".cw-canvas-composer__textarea").FillAsync(notes);

        var second = await secondContext.NewPageAsync();
        await second.GotoAsync($"{fixture.BaseUrl}/projects/{projectId}/structure");
        await PlaywrightAppFixture.CompleteDatabaseStartupAsync(second);
        await second.GetByTestId("project-structure-canvas-loaded").WaitForAsync();
        await PhysicalRightClickCanvasNodeAsync(second, $".cw-node:has-text('{original}')");
        await (await AssertSingleVisibleEditActionAsync(second)).ClickAsync();
        var competingComposer = second.Locator(".cw-canvas-composer.is-dialog");
        await competingComposer.Locator(".cw-canvas-composer__input").Nth(0).FillAsync(accepted);
        await competingComposer.GetByRole(AriaRole.Button, new() { Name = "Save changes", Exact = true }).ClickAsync();
        await Assertions.Expect(competingComposer).ToBeHiddenAsync();
        await WaitForSceneNodeTitleAsync(second, accepted, selectedOnly: true, timeoutMs: 15_000);

        var evidence = Path.Combine(PlaywrightTestHostPaths.RepositoryRoot, "output", "playwright", "structure-concurrency");
        Directory.CreateDirectory(evidence);
        await first.ScreenshotAsync(new() { Path = Path.Combine(evidence, "open-draft.png") });
        await composer.GetByRole(AriaRole.Button, new() { Name = "Save changes", Exact = true }).ClickAsync();
        await Assertions.Expect(composer.GetByRole(AriaRole.Status)).ToContainTextAsync("The original node changed or its expected state is missing.");
        await Assertions.Expect(composer).ToBeVisibleAsync();
        await Assertions.Expect(composer.Locator(".cw-canvas-composer__input").Nth(0)).ToHaveValueAsync(draft);
        await Assertions.Expect(composer.Locator(".cw-canvas-composer__textarea")).ToHaveValueAsync(notes);
        await first.ScreenshotAsync(new() { Path = Path.Combine(evidence, "conflict-draft.png") });
        await second.ReloadAsync();
        await second.GetByTestId("project-structure-canvas-loaded").WaitForAsync();
        await WaitForSceneNodeTitleAsync(second, accepted, selectedOnly: false, timeoutMs: 15_000);
        await second.ScreenshotAsync(new() { Path = Path.Combine(evidence, "accepted-state.png") });

        await first.ReloadAsync();
        await first.GetByTestId("project-structure-canvas-loaded").WaitForAsync();
        await PhysicalRightClickCanvasNodeAsync(first, $".cw-node:has-text('{accepted}')");
        await (await AssertSingleVisibleEditActionAsync(first)).ClickAsync();
        await composer.Locator(".cw-canvas-composer__input").Nth(0).FillAsync("Submitted before more typing");
        await first.EvaluateAsync("""
            () => {
                const api = window.CanDoItAll.canvasWorkbench;
                const acknowledge = api.acknowledgeComposer;
                api.acknowledgeComposer = (host, result) => new Promise(resolve => {
                    window.releaseComposerAcknowledgement = () => {
                        api.acknowledgeComposer = acknowledge;
                        acknowledge(host, result);
                        resolve();
                    };
                });
            }
            """);
        await composer.GetByRole(AriaRole.Button, new() { Name = "Save changes", Exact = true }).ClickAsync();
        await first.WaitForFunctionAsync("() => typeof window.releaseComposerAcknowledgement === 'function'");
        await Assertions.Expect(composer.GetByRole(AriaRole.Status)).ToContainTextAsync("Saving");
        await composer.Locator(".cw-canvas-composer__textarea").FillAsync("New typing after the submitted write");
        await first.EvaluateAsync("() => window.releaseComposerAcknowledgement()");
        await Assertions.Expect(composer.GetByRole(AriaRole.Status)).ToContainTextAsync("Newer text remains here");
        await Assertions.Expect(composer.Locator(".cw-canvas-composer__textarea")).ToHaveValueAsync("New typing after the submitted write");
        await first.ScreenshotAsync(new() { Path = Path.Combine(evidence, "typing-after-submit.png") });
        await second.ReloadAsync();
        await second.GetByTestId("project-structure-canvas-loaded").WaitForAsync();
        await WaitForSceneNodeTitleAsync(second, "Submitted before more typing", selectedOnly: false, timeoutMs: 15_000);
    }

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
