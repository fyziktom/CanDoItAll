using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Playwright.Smoke;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright;

[Trait("Category", "Playwright")]
[Trait("Category", "HostPlatform")]
public sealed class ProjectStructureVisualLinkBrowserTests {
    [Fact]
    public async Task Dragging_task_handles_persists_one_visual_link_alongside_an_independently_removable_prerequisite() {
        await using var host = await LiveUiHost.StartAsync();
        var (project, first, second) = await host.SeedAsync(async services => {
            var project = (await services.GetRequiredService<ProjectsService>().SaveAsync(new() { Name = "Garden links" })).Value;
            var owner = services.GetRequiredService<ProjectWorkbenchService>();
            var root = $"project:{project:D}";
            var first = await owner.CreateObjectAsync(project, new(ProjectObjectType.WorkItem, "Prepare the soil", "", "", root, 520, 215, ObjectSubtype: "task"));
            var second = await owner.CreateObjectAsync(project, new(ProjectObjectType.WorkItem, "Build the raised bed", "", "", root, 925, 400, ObjectSubtype: "task"));
            return (project, first.Id, second.Id);
        });
        var page = await host.NewPageAsync();
        await page.GotoAsync($"{host.BaseUrl}/projects/{project:D}/structure");
        await ProjectFilesUiJourney.ReadyFileCanvasAsync(page);
        await HideCanvasWindowsAsync(page);
        var firstBody = await ReadBodyAsync(page, first);
        await page.Mouse.ClickAsync(firstBody.X, firstBody.Y);
        await DragHandleAsync(page, first, "top", second);
        await WaitForLinksAsync(page, 1, 0);

        await page.ReloadAsync();
        await ProjectFilesUiJourney.ReadyFileCanvasAsync(page);
        await WaitForLinksAsync(page, 1, 0);
        await HideCanvasWindowsAsync(page);
        var secondBody = await ReadBodyAsync(page, second);
        await page.Mouse.ClickAsync(secondBody.X, secondBody.Y);
        await DragHandleAsync(page, second, "left", first);
        await page.Mouse.ClickAsync(secondBody.X, secondBody.Y);
        var dependency = page.GetByTestId("project-structure-toolbar-tool-dependency");
        await Assertions.Expect(dependency).ToBeEnabledAsync();
        await dependency.ClickAsync();
        await page.WaitForFunctionAsync("() => document.querySelector('.cw-canvas-host').__canvasWorkbenchState.surface.mode === 'dependency'");
        firstBody = await ReadBodyAsync(page, first);
        await page.Mouse.ClickAsync(firstBody.X, firstBody.Y);
        await WaitForLinksAsync(page, 1, 1);
        await page.ScreenshotAsync(new() { Path = host.Artifact("visual-link-and-prerequisite.png") });

        var before = await ReadAsync();
        var visual = Assert.Single(before.Links, link => link.Kind == ProjectObjectLinkKind.RelatesTo);
        var prerequisite = Assert.Single(before.Links, link => link.Kind == ProjectObjectLinkKind.DependsOn);
        Assert.Equal((second, first), (prerequisite.SourceId, prerequisite.TargetId));
        Assert.NotEqual(visual.RecordId, prerequisite.RecordId);

        await page.GetByTestId("project-structure-toolbar-tool-delete").ClickAsync();
        await page.WaitForFunctionAsync("() => document.querySelector('.cw-canvas-host').__canvasWorkbenchState.surface.mode === 'delete'");
        var linkPoint = await page.EvaluateAsync<ProcessCanvasBrowser.NodePoint>("""
            () => {
                const host = document.querySelector('.cw-canvas-host');
                const link = host.__canvasWorkbenchState.renderedLinks.find(link => link.kind === 'RelatesTo');
                const rect = host.getBoundingClientRect();
                return { x: rect.left + link.midPoint.x, y: rect.top + link.midPoint.y };
            }
            """);
        await page.Mouse.ClickAsync(linkPoint.X, linkPoint.Y);
        await WaitForLinksAsync(page, 0, 1);
        var after = await ReadAsync();
        Assert.DoesNotContain(after.Links, link => link.Kind == ProjectObjectLinkKind.RelatesTo);
        Assert.Contains(prerequisite, after.Links);

        Task<ProjectStructureSurface> ReadAsync() => host.SeedAsync(services =>
            services.GetRequiredService<ProjectWorkbenchService>().GetStructureAsync(project));
    }

    private static async Task HideCanvasWindowsAsync(IPage page) {
        var windows = page.Locator("[data-testid^='project-structure-'][data-testid$='-window']:visible");
        foreach (var id in await windows.EvaluateAllAsync<string[]>("items => items.map(item => item.dataset.testid)")) {
            var button = page.GetByTestId(id).GetByRole(AriaRole.Button, new() { Name = "Hide window", Exact = true });
            if (await button.IsVisibleAsync()) {
                await button.ClickAsync();
            }
        }
    }

    private static async Task<ProcessCanvasBrowser.NodePoint> ReadBodyAsync(IPage page, string id) {
        var host = page.Locator(".cw-canvas-host").First;
        await page.WaitForFunctionAsync("""
            id => !!window.CanDoItAll.canvasWorkbench.getHotZoneCenter(document.querySelector('.cw-canvas-host'), { zone: 'node-body', nodeId: id })
            """, id);
        return await host.EvaluateAsync<ProcessCanvasBrowser.NodePoint>("""
            (host,id) => {
                const point = window.CanDoItAll.canvasWorkbench.getHotZoneCenter(host, { zone: 'node-body', nodeId: id });
                const rect = host.getBoundingClientRect();
                return { x: rect.left + point.x, y: rect.top + point.y };
            }
            """, id);
    }

    private static async Task DragHandleAsync(IPage page, string source, string side, string targetId) {
        var handle = page.Locator($".cw-connector-anchor[data-node-id='{source}'][data-side='{side}']");
        await Assertions.Expect(handle).ToBeVisibleAsync();
        await handle.HoverAsync();
        await page.Mouse.DownAsync();
        var canvas = page.Locator(".cw-canvas-host").First;
        Assert.Equal(source, await canvas.EvaluateAsync<string?>("host => host.__canvasWorkbenchState.connectionDraft?.nodeId"));
        var target = await ReadBodyAsync(page, targetId);
        await page.Mouse.MoveAsync(target.X, target.Y, new() { Steps = 12 });
        var hit = await canvas.EvaluateAsync<string>("""
            host => {
                const state = host.__canvasWorkbenchState;
                const rect = host.getBoundingClientRect();
                const pointer = state.pointerHostPoint;
                const element = pointer && document.elementFromPoint(rect.left + pointer.x, rect.top + pointer.y);
                return state.connectionTarget?.nodeId ?? JSON.stringify({mode:state.surface.mode, visual:state.surface.chrome.allowVisualLinks,
                    draft:state.connectionDraft, pointer:state.pointerHostPoint, element:element?.className, window:element?.closest('[data-testid]')?.dataset.testid});
            }
            """);
        Assert.Equal(targetId, hit);
        await page.Mouse.UpAsync();
    }

    private static Task WaitForLinksAsync(IPage page, int visual, int prerequisites) => page.WaitForFunctionAsync("""
        expected => {
            const links = document.querySelector('.cw-canvas-host')?.__canvasWorkbenchState?.surface?.links;
            return links?.filter(link => link.kind === 'RelatesTo').length === expected.visual &&
                links.filter(link => link.kind === 'DependsOn').length === expected.prerequisites;
        }
        """, new { visual, prerequisites });
}
