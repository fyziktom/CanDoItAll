using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright.Smoke;

public sealed partial class AppSmokeTests {
    [Fact]
    [Trait("Surface", "SharedCanvas")]
    public async Task Shared_canvas_keeps_a_physical_collapse_across_an_older_host_refresh() {
        await using var context = await fixture.Browser.NewContextAsync(new() {
            ViewportSize = new() { Width = 1900, Height = 1200 }
        });
        var page = await context.NewPageAsync();
        var projectId = await CreateProjectAsync(page, "Collapse acknowledgement", "Validation");
        await WaitForInitializedCanvasHostAsync(page);
        var rootId = $"project:{projectId}";
        await page.EvaluateAsync("""
            () => {
                const host = document.querySelector('.cw-canvas-host');
                const state = host.__canvasWorkbenchState;
                const workbench = window.CanDoItAll.canvasWorkbench;
                const probe = window.__collapseStateProbe = { refreshed: false, committed: false };
                const update = workbench.update;
                probe.apply = (surface, options) => update(host, surface, options);
                workbench.update = (target, surface, options) => {
                    if (target === host) {
                        probe.prior = { surface: structuredClone(surface), options: structuredClone(options) };
                    }
                    return update(target, surface, options);
                };
                const invoke = state.dotNetRef.invokeMethodAsync.bind(state.dotNetRef);
                state.dotNetRef.invokeMethodAsync = async (method, ...args) => {
                    const result = await invoke(method, ...args);
                    if (method === 'OnStateChanged' && probe.rootId &&
                        JSON.parse(args[0]).collapsedNodeIds.includes(probe.rootId)) {
                        probe.committed = true;
                    }
                    return result;
                };
            }
            """);
        await InvokeStructureCreateActionAsync(page, "add-note", rootId, rootId,
            "Collapse child", string.Empty, "Collapse child");
        await page.WaitForFunctionAsync("() => !!window.__collapseStateProbe.prior");

        var point = await page.EvaluateAsync<CanvasHotZoneCenter>("""
            rootId => {
                const host = document.querySelector('.cw-canvas-host');
                const state = host.__canvasWorkbenchState;
                const probe = window.__collapseStateProbe;
                probe.rootId = rootId;
                const prior = probe.prior;
                host.addEventListener('pointerdown', () => {
                    queueMicrotask(() => {
                        probe.collapsedBeforeRefresh = state.collapsedIds.has(rootId);
                        probe.apply(prior.surface, prior.options);
                        probe.refreshed = true;
                    });
                }, { once: true });
                const bounds = host.getBoundingClientRect();
                const center = window.CanDoItAll.canvasWorkbench.getHotZoneCenter(host, { zone: 'node-collapse', nodeId: rootId });
                return { x: bounds.left + center.x, y: bounds.top + center.y };
            }
            """, rootId);
        await page.Mouse.ClickAsync((float)point.X, (float)point.Y);
        await page.WaitForFunctionAsync("() => window.__collapseStateProbe.refreshed && window.__collapseStateProbe.committed");

        Assert.True(await page.EvaluateAsync<bool>("""
            () => {
                const probe = window.__collapseStateProbe;
                const state = document.querySelector('.cw-canvas-host').__canvasWorkbenchState;
                return probe.collapsedBeforeRefresh && state.collapsedIds.has(probe.rootId);
            }
            """));
        var scene = await ReadSceneSnapshotAsync(page);
        Assert.Equal(rootId, Assert.Single(scene.Nodes).Id);
    }
}
