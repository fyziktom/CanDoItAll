using CanDoItAll.AppComponents.FileTools;
using CanDoItAll.FileTools.FileBrowser;
using CanDoItAll.FileTools.FileBrowser.Components;
using CanDoItAll.FileTools.FileInteraction;
using CanDoItAll.Modules.Resources;

namespace CanDoItAll.Resources.UI;

public sealed class ResourcePromotionDraft(ResourceFileSelection selection, IReadOnlyList<ResourceProjectOption> projects, ResourceMetadataContext context) {
    public Guid Origin { get; } = Guid.NewGuid();
    public ResourceFileSelection Selection { get; } = selection;
    public ResourceMetadataContext Context { get; } = context;
    public Guid ResourceId { get; } = Guid.NewGuid();
    public IReadOnlyList<ResourceProjectOption> Projects { get; } = projects;
    public Guid? ProjectId { get; set; } = selection.Source.Key.TryGetProjectId(out var projectId) ? projectId : projects.FirstOrDefault()?.Id;
    public string Name { get; set; } = selection.Item.Name;
    public ResourceSensitivity Sensitivity { get; set; }
    public string? Error { get; set; }
}

public sealed class ResourcePromotionReceipt(Guid origin, ResourcePromotionRequest command) {
    public Guid Id { get; } = Guid.NewGuid();
    public Guid Origin { get; } = origin;
    public ResourcePromotionRequest Command { get; } = command;
    public ResourcePromotionObservation? Observation { get; set; }
    public ResourceMetadataSnapshot? Snapshot => Observation switch {
        ResourcePromotionObservation.Created created => created.Snapshot,
        ResourcePromotionObservation.Reused reused => reused.Snapshot,
        _ => null
    };
    public long? ScopeRevision => Observation switch {
        ResourcePromotionObservation.Created created => created.ScopeRevision,
        ResourcePromotionObservation.Reused reused => reused.ScopeRevision,
        _ => null
    };
    public ResourceEffectState State { get; set; } = ResourceEffectState.Pending;
    public string Message { get; set; } = "Waiting for the owner.";
    public bool IsReviewing { get; set; }
}

public sealed class ResourceFileActionReceipt(ResourceFileSelection selection, FileToolsHostAction action) {
    public Guid Id { get; } = Guid.NewGuid();
    public ResourceFileSelection Selection { get; } = selection;
    public FileToolsHostAction Action { get; } = action;
    public ResourceEffectState State { get; set; } = ResourceEffectState.Pending;
    public string Message { get; set; } = "Waiting for authorization.";
}

public interface IResourceBrowseWorkspace {
    event Action? Changed;
    ResourceBrowseCatalog? Catalog { get; }
    ResourceBrowseLease? Workspace { get; }
    ResourcePreviewLease? Preview { get; }
    ResourcePromotionDraft? Promotion { get; }
    IReadOnlyList<ResourcePromotionReceipt> Promotions { get; }
    IReadOnlyList<ResourceFileActionReceipt> Actions { get; }
    IFileBrowserHostActionCatalog? HostActions { get; }
    ResourceFileSourceKey? SelectedSource { get; }
    ResourceBrowsePosition? Position { get; }
    ResourceViewAccess Access { get; }
    bool IsLoading { get; }
    bool IsOpening { get; }
    bool IsSaving { get; }
    bool CanOpenPreviewExternally { get; }
    string? CatalogError { get; }
    string? SourceError { get; }
    string? CleanupError { get; }
    Task RefreshAsync();
    Task SelectAsync(ResourceFileSourceKey key);
    Task ActivateAsync(FileBrowserItemInvokedEventArgs args, bool internallySupported);
    Task ActionAsync(FileBrowserItemActionEventArgs args);
    Task OpenPreviewExternallyAsync(FileInteractionRequest request);
    Task SavePromotionAsync();
    void ClosePromotion();
    Task OpenResourceAsync(Guid resourceId);
    Task ClosePreviewAsync();
    Task ReviewPromotionAsync(ResourcePromotionReceipt receipt, Guid resourceId);
}
