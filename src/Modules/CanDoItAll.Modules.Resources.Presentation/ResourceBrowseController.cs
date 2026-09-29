using CanDoItAll.AppComponents.FileTools;
using CanDoItAll.FileTools.FileBrowser;
using CanDoItAll.FileTools.FileBrowser.Components;
using CanDoItAll.FileTools.FileInteraction;
using CanDoItAll.FileTools.Integration;
using CanDoItAll.Resources.UI;

namespace CanDoItAll.Modules.Resources;

public sealed class ResourceBrowseController(IResourceBrowseOwner owner, FileToolsHostActionRunner actions) : IResourceBrowseWorkspace, IAsyncDisposable {
    private readonly CancellationTokenSource lifetime = new();
    private CancellationTokenSource selectionLifetime = new();
    private readonly List<ResourcePromotionReceipt> promotions = [];
    private readonly List<ResourceFileActionReceipt> actionReceipts = [];
    private long catalogVersion;
    private long selectionVersion;
    private long previewVersion;
    private bool disposed;
    private ResourceFileSelection? previewSelection;
    private (Guid Id, ResourceFileSelection Selection)? promotedSelection;
    public event Action? Changed;
    public Func<Guid, Task>? Promoted { get; set; }
    public ResourceBrowseCatalog? Catalog { get; private set; }
    public ResourceBrowseLease? Workspace { get; private set; }
    public ResourcePreviewLease? Preview { get; private set; }
    public ResourcePromotionDraft? Promotion { get; private set; }
    public IReadOnlyList<ResourcePromotionReceipt> Promotions => promotions;
    public IReadOnlyList<ResourceFileActionReceipt> Actions => actionReceipts;
    public IFileBrowserHostActionCatalog? HostActions { get; private set; }
    public ResourceFileSourceKey? SelectedSource { get; private set; }
    public ResourceBrowsePosition? Position { get; private set; }
    public ResourceViewAccess Access { get; private set; } = ResourceViewAccess.Loading;
    public bool IsLoading { get; private set; }
    public bool IsOpening { get; private set; }
    public bool IsSaving => Promotion is { } draft && promotions.Any(r => r.Origin == draft.Origin && r.State is ResourceEffectState.Pending or ResourceEffectState.Unknown);
    public bool CanOpenPreviewExternally => Preview is not null && previewSelection is { } selection && Matches(selection) && SupportsLocal(selection.Item);
    public string? CatalogError { get; private set; }
    public string? SourceError { get; private set; }
    public string? CleanupError { get; private set; }

    public async Task RefreshAsync() {
        if (disposed) {
            return;
        }
        var version = ++catalogVersion;
        var selectedVersion = selectionVersion;
        IsLoading = true;
        CatalogError = null;
        Notify();
        try {
            var catalog = await owner.LoadAsync(lifetime.Token);
            if (!Current || version != catalogVersion) {
                return;
            }
            Catalog = catalog;
            if (selectedVersion == selectionVersion && SelectedSource is { } selected) {
                if (catalog.Sources.Any(s => s.Key == selected)) {
                    await OpenSourceAsync(selected);
                } else {
                    RetireSelection();
                    SourceError = "The selected source is no longer available. No other source was substituted.";
                    Access = ResourceViewAccess.Failed;
                    await ReleaseCurrentAsync();
                }
            } else if (SelectedSource is null) {
                Access = ResourceViewAccess.Ready;
            }
        } catch (Exception) {
            if (Current && version == catalogVersion) {
                CatalogError = "Sources could not be refreshed. Retained sources and sessions may be stale.";
                Access = ResourceViewAccess.Failed;
            }
        } finally {
            if (!disposed && version == catalogVersion) {
                IsLoading = false;
                if (!owner.IsCurrent) {
                    Access = ResourceViewAccess.Failed;
                    CatalogError = "The database profile changed. Open a new Resources workspace.";
                }
                Notify();
            }
        }
    }

    public Task SelectAsync(ResourceFileSourceKey key) => Workspace?.Source.Key == key && Access == ResourceViewAccess.Ready
        ? Task.CompletedTask : OpenSourceAsync(key);

    private async Task OpenSourceAsync(ResourceFileSourceKey key) {
        if (!Current) {
            return;
        }
        RetireSelection();
        var version = selectionVersion;
        var token = selectionLifetime.Token;
        SelectedSource = key;
        IsOpening = true;
        Access = ResourceViewAccess.Loading;
        SourceError = null;
        Notify();
        await ReleaseCurrentAsync();
        ResourceBrowseLease? acquired = null;
        try {
            if (!IsSelection(version)) {
                return;
            }
            acquired = await owner.OpenAsync(key, token);
            if (!IsSelection(version)) {
                return;
            }
            Workspace = acquired;
            acquired = null;
            HostActions = new DefaultFileToolsHostActionCatalog(ResolveActions, owner.IsLocalLaunchAvailable);
            var projectId = key.TryGetProjectId(out var id) ? id : (Guid?)null;
            Position = new(key, Workspace.Source.SourceClass, Workspace.Source.DisplayName, projectId,
                Catalog?.Projects.FirstOrDefault(p => p.Id == projectId)?.Name);
            Access = ResourceViewAccess.Ready;
        } catch (Exception) {
            if (IsSelection(version)) {
                SourceError = "The selected source could not be opened with current authority.";
                Access = ResourceViewAccess.Failed;
            }
        } finally {
            await ReleaseAsync(acquired);
            if (IsSelection(version)) {
                IsOpening = false;
                Notify();
            }
        }
    }

    public async Task ActivateAsync(FileBrowserItemInvokedEventArgs args, bool internallySupported) {
        if (!Current || Workspace is null || args.Item.IsContainer) {
            return;
        }
        if (args.Kind == FileBrowserInvocationKind.PointerDoubleClick && !internallySupported && SupportsLocal(args.Item)) {
            await ExecuteAsync(args.Item, FileToolsHostAction.OpenInPreferredApplication);
            return;
        }
        var selection = Capture(args.Item);
        if (promotions.Any(r => SameTarget(r.Command.Selection, selection) && r.State is ResourceEffectState.Pending or ResourceEffectState.Unknown)) {
            SourceError = "This object already has a pending or unacknowledged promotion. Review its original receipt.";
            Notify();
            return;
        }
        Promotion = new(selection, Catalog?.Projects ?? []);
        Notify();
    }

    public void ClosePromotion() {
        Promotion = null;
        Notify();
    }

    public async Task SavePromotionAsync() {
        if (!Current || Promotion is not { } draft || IsSaving) {
            return;
        }
        var project = draft.Projects.FirstOrDefault(p => p.Id == draft.ProjectId);
        if (project is null || string.IsNullOrWhiteSpace(draft.Name)) {
            draft.Error = "Choose an available project and enter a resource name.";
            Notify();
            return;
        }
        var command = new ResourcePromotionRequest(draft.Selection, project.Admission, draft.Name, draft.Sensitivity);
        if (promotions.Any(r => SameTarget(r.Command.Selection, command.Selection) && r.State is ResourceEffectState.Pending or ResourceEffectState.Unknown)) {
            return;
        }
        if (!MakeRoom(promotions, r => r.State is not (ResourceEffectState.Pending or ResourceEffectState.Unknown) && !r.IsReviewing)) {
            draft.Error = "Resolve existing pending promotions before submitting another object.";
            Notify();
            return;
        }
        var receipt = new ResourcePromotionReceipt(draft.Origin, command);
        var selection = selectionVersion;
        promotions.Add(receipt);
        draft.Error = null;
        Notify();
        try {
            receipt.Observation = await owner.PromoteAsync(command);
            receipt.State = receipt.Observation.Warning is null ? ResourceEffectState.Committed : ResourceEffectState.CommittedWarning;
            receipt.Message = receipt.Observation.Warning ?? (receipt.Observation.Created ? "Storage object saved." : "This object was already registered in the selected project.");
            var currentDialog = Current && ReferenceEquals(Promotion, draft);
            if (currentDialog) {
                Promotion = null;
            }
            if (Current && Promoted is { } callback) {
                await callback(receipt.Observation.ResourceId);
            }
            if (currentDialog && IsSelection(selection) && Promotion is null) {
                await OpenSourceAsync(command.Selection.Source.Key);
                if (IsSelection(selection + 1) && Workspace is { } current && current.Source.Scope == command.Selection.Source.Scope) {
                    promotedSelection = (receipt.Observation.ResourceId, new(current.Source, current.Revision, command.Selection.Item));
                }
            }
        } catch (ResourceActionRefusedException exception) {
            receipt.State = ResourceEffectState.Refused;
            receipt.Message = exception.Message;
        } catch (Exception) {
            receipt.State = receipt.Observation is null ? ResourceEffectState.Unknown : ResourceEffectState.CommittedWarning;
            receipt.Message = receipt.Observation is null
                ? "No final acknowledgement was received. Do not repeat promotion; review an exact stored identity."
                : "Storage object saved; follow-up notification failed. Its confirmed identity is retained.";
        } finally {
            if (Current && ReferenceEquals(Promotion, draft)) {
                draft.Error = receipt.Message;
            }
            Notify();
        }
    }

    public async Task ReviewPromotionAsync(ResourcePromotionReceipt receipt, Guid resourceId) {
        if (!Current || !promotions.Contains(receipt) || receipt.State != ResourceEffectState.Unknown || receipt.IsReviewing || resourceId == Guid.Empty) {
            return;
        }
        receipt.IsReviewing = true;
        Notify();
        ResourcePreviewLease? observed = null;
        try {
            observed = await owner.OpenResourceAsync(resourceId, lifetime.Token);
            if (Current && receipt.State == ResourceEffectState.Unknown && observed.ResourceId == resourceId) {
                receipt.Message = $"Resource {resourceId:D} can be reopened. This does not prove that the unacknowledged request created it; promotion remains blocked.";
            }
        } catch (Exception) {
            receipt.Message = "The exact identity could not be reopened. The original promotion remains unacknowledged.";
        } finally {
            await ReleaseAsync(observed);
            receipt.IsReviewing = false;
            Notify();
        }
    }

    public async Task OpenResourceAsync(Guid resourceId) {
        if (!Current) {
            return;
        }
        var version = ++previewVersion;
        var selected = selectionVersion;
        var old = Preview;
        Preview = null;
        previewSelection = null;
        SourceError = null;
        await ReleaseAsync(old);
        ResourcePreviewLease? acquired = null;
        try {
            if (!IsSelection(selected) || version != previewVersion) {
                return;
            }
            acquired = await owner.OpenResourceAsync(resourceId, selectionLifetime.Token);
            if (!IsSelection(selected) || version != previewVersion) {
                return;
            }
            Preview = acquired;
            acquired = null;
            var receipt = promotions.LastOrDefault(r => r.Observation?.ResourceId == resourceId);
            previewSelection = promotedSelection is { } promoted && promoted.Id == resourceId && Matches(promoted.Selection)
                ? promoted.Selection : receipt is not null && Matches(receipt.Command.Selection) ? receipt.Command.Selection : null;
        } catch (Exception) {
            if (IsSelection(selected) && version == previewVersion) {
                SourceError = "The stored object could not be reopened with current authority.";
            }
        } finally {
            await ReleaseAsync(acquired);
            Notify();
        }
    }

    public async Task ClosePreviewAsync() {
        previewVersion++;
        var old = Preview;
        Preview = null;
        previewSelection = null;
        Notify();
        await ReleaseAsync(old);
    }

    public Task OpenPreviewExternallyAsync(FileInteractionRequest request) => CanOpenPreviewExternally && Preview?.Request.File == request.File
        ? ExecuteAsync(previewSelection!.Item, FileToolsHostAction.OpenInPreferredApplication) : Task.CompletedTask;

    public Task ActionAsync(FileBrowserItemActionEventArgs args) {
        if (args.Origin != FileBrowserActionOrigin.Host) {
            return Task.CompletedTask;
        }
        return args.ActionId switch {
            FileBrowserActionIds.Open => ExecuteAsync(args.Item, FileToolsHostAction.OpenInPreferredApplication),
            FileToolsBrowseHostActionIds.OpenContainingFolder => ExecuteAsync(args.Item, FileToolsHostAction.OpenContainingFolder),
            FileBrowserActionIds.Download => ExecuteAsync(args.Item, FileToolsHostAction.Download),
            _ => Task.CompletedTask
        };
    }

    private async Task ExecuteAsync(FileBrowserItem item, FileToolsHostAction action) {
        if (!Current || Workspace is null || item.IsContainer) {
            return;
        }
        var selection = Capture(item);
        if (actionReceipts.Any(r => SameTarget(r.Selection, selection) && r.State == ResourceEffectState.Pending)) {
            return;
        }
        if (!MakeRoom(actionReceipts, r => r.State != ResourceEffectState.Pending)) {
            SourceError = "Wait for pending file actions to finish.";
            Notify();
            return;
        }
        var token = selectionLifetime.Token;
        var receipt = new ResourceFileActionReceipt(selection, action);
        actionReceipts.Add(receipt);
        Notify();
        try {
            var result = await actions.ExecuteAsync(action,
                (localAction, cancellation) => owner.LaunchAsync(selection, localAction, cancellation),
                cancellation => owner.AuthorizeDownloadAsync(selection, cancellation), token);
            receipt.State = result.IsSuccess ? ResourceEffectState.Committed : ResourceEffectState.Refused;
            receipt.Message = result.Message;
        } catch (ResourceActionRefusedException exception) {
            receipt.State = ResourceEffectState.Refused;
            receipt.Message = exception.Message;
        } catch (Exception) {
            receipt.State = ResourceEffectState.Unknown;
            receipt.Message = "The file action did not return a final acknowledgement. An already admitted external action cannot be undone by navigation.";
        }
        Notify();
    }

    private bool Current => !disposed && owner.IsCurrent;
    private bool IsSelection(long version) => Current && version == selectionVersion;
    private bool Matches(ResourceFileSelection selection) => Workspace is { } current && current.Source.Key == selection.Source.Key && current.Source.Scope == selection.Source.Scope && current.Revision == selection.Revision;
    private static bool SameTarget(ResourceFileSelection left, ResourceFileSelection right) => left.Source.Key == right.Source.Key && left.Source.Scope == right.Source.Scope && left.Item.Key == right.Item.Key;
    private ResourceFileSelection Capture(FileBrowserItem item) => new(Workspace!.Source, Workspace.Revision, item);
    private bool SupportsLocal(FileBrowserItem item) => owner.IsLocalLaunchAvailable && ResolveActions(item.Key.SourceId).SupportsLocalOpen;
    private FileToolsBrowseSourceActionAvailability ResolveActions(FileBrowserSourceId id) => Workspace is { } current && current.Browser.Snapshot.Sources.Any(s => s.Id == id) ? current.Actions : default;
    private void Notify() {
        if (!disposed) {
            Changed?.Invoke();
        }
    }
    private void RetireSelection() {
        selectionVersion++;
        previewVersion++;
        var previous = selectionLifetime;
        selectionLifetime = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
        previous.Cancel();
        previous.Dispose();
        Position = null;
        Promotion = null;
        promotedSelection = null;
    }
    private async Task ReleaseCurrentAsync() {
        var preview = Preview;
        var workspace = Workspace;
        Preview = null;
        Workspace = null;
        HostActions = null;
        previewSelection = null;
        await ReleaseAsync(preview);
        await ReleaseAsync(workspace);
    }
    private async ValueTask ReleaseAsync(IAsyncDisposable? lease) {
        if (lease is null) {
            return;
        }
        try {
            await lease.DisposeAsync();
        } catch (Exception) {
            CleanupError = "A retired file lease could not be released cleanly. Its cleanup was attempted once; current authority was not reused.";
        }
    }
    private static bool MakeRoom<T>(List<T> receipts, Func<T, bool> completed) where T : class {
        if (receipts.Count < 32) {
            return true;
        }
        var old = receipts.FirstOrDefault(completed);
        return old is not null && receipts.Remove(old);
    }
    public async ValueTask DisposeAsync() {
        if (disposed) {
            return;
        }
        disposed = true;
        lifetime.Cancel();
        selectionLifetime.Cancel();
        await ReleaseCurrentAsync();
        await actions.DisposeAsync();
        selectionLifetime.Dispose();
        lifetime.Dispose();
        Changed = null;
        Promoted = null;
    }
}
