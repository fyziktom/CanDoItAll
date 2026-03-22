using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace CanDoItAll.Modules.Factory.Pages;

public partial class PromptFactoryPage
{
    private ElementReference floatingInspectorPanel;
    private ElementReference floatingInspectorHandle;
    private bool isCanvasInspectorMinimized;

    private void ToggleCanvasInspector()
        => isCanvasInspectorMinimized = !isCanvasInspectorMinimized;

    private async Task DockCanvasInspectorAsync()
    {
        isCanvasInspectorMinimized = false;
        await JS.InvokeVoidAsync("CanDoItAll.promptFactory.resetFloatingInspector", floatingInspectorPanel);
    }

    private async Task SyncFloatingInspectorAsync()
    {
        if (!IsSupportLaneTab(SupportLaneTabCanvas) || canvasSurface is null)
        {
            return;
        }

        await JS.InvokeVoidAsync("CanDoItAll.promptFactory.mountFloatingInspector", floatingInspectorPanel, floatingInspectorHandle);
    }
}
