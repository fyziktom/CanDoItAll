namespace CanDoItAll.Modules.Workbench.Pages;

public partial class ProjectStructurePage
{
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (string.IsNullOrWhiteSpace(pendingWorkbenchSelectionId) ||
            workbenchRef is null)
        {
            return;
        }

        var selectionId = pendingWorkbenchSelectionId;
        if (selectedNodeIds.Count != 1 ||
            !string.Equals(selectedNodeIds[0], selectionId, StringComparison.Ordinal))
        {
            pendingWorkbenchSelectionId = null;
            return;
        }

        pendingWorkbenchSelectionId = null;
        await workbenchRef.SelectNodesAsync([selectionId], selectionId);
    }
}
