using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright;

internal static class ProcessCanvasBrowser {
    internal static async Task<NodePoint> ReadNodePointAsync(IPage page, string nodeId) {
        await page.WaitForFunctionAsync("id => document.querySelector('.cw-canvas-host')?.__canvasWorkbenchState?.layoutPositions?.has(id)", nodeId);
        var point = await page.EvaluateAsync<NodePoint>("""
            id => {
                const host = document.querySelector('.cw-canvas-host');
                const state = host.__canvasWorkbenchState;
                const position = state.layoutPositions.get(id);
                const rect = host.getBoundingClientRect();
                return {
                    x: rect.left + (position.x + 80) * state.ui.zoom + state.ui.panX,
                    y: rect.top + (position.y + 48) * state.ui.zoom + state.ui.panY
                };
            }
            """, nodeId);
        Assert.InRange(point.X, 1, page.ViewportSize!.Width - 1);
        Assert.InRange(point.Y, 1, page.ViewportSize.Height - 1);
        return point;
    }

    internal sealed class NodePoint {
        public float X { get; set; }
        public float Y { get; set; }
    }
}
