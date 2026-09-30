using CanDoItAll.AppComponents;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Modules.Workspace.StorageSelection.Contracts;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Workspace.StorageSelection.UI;

public partial class StorageCatalogSelectionField {
    [Inject] private DialogService DialogService { get; set; } = default!;
    [Inject] private IStorageCatalogSelectionSource CatalogSource { get; set; } = default!;
    [Inject] private ILogger<StorageCatalogSelectionField> Logger { get; set; } = default!;
    [Parameter] public IReadOnlyList<Guid> Value { get; set; } = [];
    [Parameter] public EventCallback<IReadOnlyList<Guid>> ValueChanged { get; set; }
    [Parameter] public CancellationToken OwnerLifetime { get; set; }
    [Parameter] public bool AllowAll { get; set; }
    [Parameter] public bool Disabled { get; set; }
    [Parameter] public string Label { get; set; } = "Allowed storage catalogs";
    [Parameter] public string Description { get; set; } = "Choose the storage catalogs this agent may access.";
    [Parameter] public string DataTestId { get; set; } = "storage-catalog-selection";

    private SelectionIntent? intent;
    private CancellationTokenSource? detailsRequest;
    private CancellationTokenSource? dialogRequest;
    private Dictionary<Guid, StorageSelectionItem> knownCatalogs = [];
    private bool catalogsHaveLoaded;
    private bool detailsAttempted;
    private bool disposed;
    private string? catalogDetailsErrorMessage;
    private string? openErrorMessage;
    private bool isResolvingCatalogDetails => detailsRequest is not null;
    private bool isOpening => dialogRequest is not null;
    private bool ChooserDisabled => intent?.CanEdit != true || isOpening;
    private IReadOnlyList<SelectedReferenceItem<Guid>> SelectedReferences => [.. SelectionIntent.Normalize(Value).Select(BuildSelectedReference)];

    protected override void OnInitialized() => CatalogSource.ContextChanged += SourceChanged;
    protected override Task OnParametersSetAsync() => RefreshIntentAsync();

    private async Task RefreshIntentAsync() {
        if (disposed) {
            return;
        }
        if (intent is null || !intent.Matches(OwnerLifetime, Value, AllowAll, Disabled) || !intent.IsCurrent) {
            var clearMetadata = intent is null || intent.ParentLifetime != OwnerLifetime || intent.Context != CatalogSource.Context || !CatalogSource.IsCurrent;
            intent?.Dispose();
            CancelRequest(ref detailsRequest);
            CancelRequest(ref dialogRequest);
            intent = new(CatalogSource, OwnerLifetime, Value, AllowAll, Disabled);
            openErrorMessage = null;
            if (clearMetadata) {
                knownCatalogs.Clear();
                catalogsHaveLoaded = false;
                detailsAttempted = false;
                catalogDetailsErrorMessage = null;
            } else if (!catalogsHaveLoaded && catalogDetailsErrorMessage is null) {
                detailsAttempted = false;
            }
        }
        if (intent.IsCurrent && !detailsAttempted && !catalogsHaveLoaded && intent.Ids.Count > 0) {
            await LoadCatalogDetailsAsync();
        }
    }

    private void SourceChanged() {
        if (!disposed) {
            _ = InvokeAsync(async () => {
                await RefreshIntentAsync();
                if (!disposed) {
                    StateHasChanged();
                }
            });
        }
    }

    private bool IsCurrent(SelectionIntent owner) => !disposed && ReferenceEquals(intent, owner) && owner.IsCurrent;

    private async Task LoadCatalogDetailsAsync() {
        if (intent is not { IsCurrent: true } owner || detailsRequest is not null) {
            return;
        }
        var request = CancellationTokenSource.CreateLinkedTokenSource(owner.Token);
        detailsRequest = request;
        detailsAttempted = true;
        catalogDetailsErrorMessage = null;
        try {
            var rows = await CatalogSource.ListAsync(request.Token);
            if (IsCurrent(owner) && ReferenceEquals(detailsRequest, request)) {
                AcceptCatalogs(rows);
            }
        } catch (OperationCanceledException) when (request.IsCancellationRequested) {
        } catch (Exception exception) {
            if (IsCurrent(owner) && ReferenceEquals(detailsRequest, request)) {
                catalogDetailsErrorMessage = "Saved catalog names could not be loaded. Retry to refresh their details.";
                LogReadFailure(owner, exception);
            }
        } finally {
            if (ReferenceEquals(detailsRequest, request)) {
                detailsRequest = null;
            }
            request.Dispose();
        }
    }

    private async Task OpenPickerAsync() {
        if (ChooserDisabled || intent is not { } owner) {
            return;
        }
        var callback = ValueChanged;
        IReadOnlyList<StorageSelectionItem>? acquiredCatalogs = null;
        var request = CancellationTokenSource.CreateLinkedTokenSource(owner.Token);
        dialogRequest = request;
        openErrorMessage = null;
        try {
            var result = await DialogService.OpenAsync<StorageCatalogSelectionDialog>("Choose storage catalogs", new Dictionary<string, object?> {
                [nameof(StorageCatalogSelectionDialog.SelectedCatalogIds)] = owner.Ids,
                [nameof(StorageCatalogSelectionDialog.OwnerLifetime)] = request.Token,
                [nameof(StorageCatalogSelectionDialog.SourceContext)] = owner.Context,
                [nameof(StorageCatalogSelectionDialog.CatalogLoadFailed)] = EventCallback.Factory.Create(this, () => {
                    if (IsCurrent(owner) && ReferenceEquals(dialogRequest, request)) {
                        catalogDetailsErrorMessage = catalogsHaveLoaded
                            ? "Catalog refresh is unavailable. Previous catalog details and all selected IDs are retained."
                            : "Catalog details are unavailable. All selected IDs are retained. Retry to refresh their details.";
                    }
                }),
                [nameof(StorageCatalogSelectionDialog.CatalogsLoaded)] = EventCallback.Factory.Create<IReadOnlyList<StorageSelectionItem>>(this, rows => {
                    if (IsCurrent(owner) && ReferenceEquals(dialogRequest, request)) {
                        acquiredCatalogs = rows;
                        CancelRequest(ref detailsRequest);
                        AcceptCatalogs(rows);
                    }
                }),
                [nameof(StorageCatalogSelectionDialog.DataTestId)] = $"{DataTestId}-dialog"
            }, new() {
                Eyebrow = "Storage access", Subtitle = "Search the current workspace catalog and stage the references this agent may use.",
                Size = ModalSize.Wide, DenseChrome = true, AriaLabel = "Choose allowed storage catalogs", TestId = $"{DataTestId}-dialog-shell"
            }, request.Token);
            if (IsCurrent(owner) && ReferenceEquals(dialogRequest, request) && owner.CanEdit && result is StorageCatalogSelectionDialogResult selection) {
                var next = SelectionIntent.Normalize(selection.SelectedCatalogIds);
                if (acquiredCatalogs is null || next.Any(id => !owner.Ids.Contains(id) && !acquiredCatalogs.Any(row => row.Id == id && row.IsEnabled))) {
                    openErrorMessage = "The selection could not be verified against the current catalog. Open the chooser again.";
                    return;
                }
                if (!next.SequenceEqual(owner.Ids)) {
                    await callback.InvokeAsync(next);
                }
            }
        } catch (OperationCanceledException) when (request.IsCancellationRequested) {
        } catch (Exception exception) {
            if (IsCurrent(owner) && ReferenceEquals(dialogRequest, request)) {
                openErrorMessage = "The storage catalog chooser could not be opened. Please retry.";
                LogReadFailure(owner, exception);
            }
        } finally {
            if (ReferenceEquals(dialogRequest, request)) {
                dialogRequest = null;
            }
            request.Dispose();
        }
    }

    private void AcceptCatalogs(IReadOnlyList<StorageSelectionItem> rows) {
        knownCatalogs = SelectionIntent.NormalizeCatalogs(rows).ToDictionary(row => row.Id);
        catalogsHaveLoaded = true;
        detailsAttempted = true;
        catalogDetailsErrorMessage = null;
    }

    private Task RemoveCatalogAsync(Guid id) => ChooserDisabled || !Value.Contains(id)
        ? Task.CompletedTask
        : ValueChanged.InvokeAsync(SelectionIntent.Normalize(Value.Where(value => value != id)));

    private SelectedReferenceItem<Guid> BuildSelectedReference(Guid id) {
        if (knownCatalogs.TryGetValue(id, out var catalog)) {
            return new(id, catalog.Name, id.ToString("D")) {
                DetailText = $"{catalog.Provider} · {catalog.Connection} · {catalog.EndpointOrRoot}",
                StatusText = !catalog.IsEnabled ? "Disabled" : catalog.IsReadOnly ? "Read only" : "Enabled",
                StatusTone = !catalog.IsEnabled ? SelectedReferenceStatusTone.Warning : catalog.IsReadOnly ? SelectedReferenceStatusTone.Info : SelectedReferenceStatusTone.Success,
                TestId = $"{DataTestId}-selected-row-{id:N}", CanRemove = true
            };
        }
        return new(id, catalogsHaveLoaded ? "Missing storage catalog" : "Storage catalog reference", id.ToString("D")) {
            DetailText = catalogsHaveLoaded ? "This saved catalog ID is not present in the current workspace catalog."
                : isResolvingCatalogDetails ? "Loading the catalog name and connection details." : "Open the chooser to refresh catalog details.",
            StatusText = catalogsHaveLoaded ? "Missing" : isResolvingCatalogDetails ? "Loading" : "Not loaded",
            StatusTone = isResolvingCatalogDetails ? SelectedReferenceStatusTone.Info : SelectedReferenceStatusTone.Warning,
            TestId = $"{DataTestId}-selected-row-{id:N}", CanRemove = true
        };
    }

    private void LogReadFailure(SelectionIntent owner, Exception exception) => Logger.LogWarning(
        "Storage selection read failed for profile {ProfileId}, generation {Generation}; failure type {FailureType}.",
        owner.Context.ProfileId, owner.Context.Generation, exception.GetType().Name);

    private static void CancelRequest(ref CancellationTokenSource? slot) {
        var original = slot;
        slot = null;
        original?.Cancel();
    }

    public ValueTask DisposeAsync() {
        if (disposed) {
            return ValueTask.CompletedTask;
        }
        disposed = true;
        CatalogSource.ContextChanged -= SourceChanged;
        intent?.Dispose();
        CancelRequest(ref detailsRequest);
        CancelRequest(ref dialogRequest);
        return ValueTask.CompletedTask;
    }
}
