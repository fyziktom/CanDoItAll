namespace CanDoItAll.Modules.Workspace.Pages.Components;

public sealed record StorageCatalogSelectionDialogResult(
    IReadOnlyList<Guid> SelectedCatalogIds);
