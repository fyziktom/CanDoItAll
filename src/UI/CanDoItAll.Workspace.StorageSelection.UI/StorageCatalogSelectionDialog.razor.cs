using CanDoItAll.AppComponents;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Modules.Workspace.StorageSelection.Contracts;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Workspace.StorageSelection.UI;

public sealed record StorageCatalogSelectionDialogResult(IReadOnlyList<Guid> SelectedCatalogIds);

public partial class StorageCatalogSelectionDialog {
    [Inject] private IStorageCatalogSelectionSource CatalogSource { get; set; } = default!;
    [Inject] private ILogger<StorageCatalogSelectionDialog> Logger { get; set; } = default!;
    [CascadingParameter] private DialogReference? DialogReference { get; set; }
    [Parameter] public IReadOnlyList<Guid> SelectedCatalogIds { get; set; } = [];
    [Parameter] public EventCallback<IReadOnlyList<StorageSelectionItem>> CatalogsLoaded { get; set; }
    [Parameter] public EventCallback CatalogLoadFailed { get; set; }
    [Parameter] public CancellationToken OwnerLifetime { get; set; }
    [Parameter] public StorageCatalogSelectionContext? SourceContext { get; set; }
    [Parameter] public string DataTestId { get; set; } = "storage-catalog-dialog";

    private readonly HashSet<Guid> pendingSelectedIds = [];
    private IReadOnlyList<StorageSelectionItem> catalogs = [];
    private SelectionIntent? origin;
    private CancellationTokenSource? readRequest;
    private CancellationTokenRegistration parentRegistration;
    private bool acquired;
    private bool submitted;
    private bool disposed;
    private string? loadErrorMessage;
    private bool isLoading => readRequest is not null;
    private bool CanApply => !disposed && !submitted && origin?.CanEdit == true && acquired && !isLoading;
    private IReadOnlyList<ResourceCardPickerOption<Guid>> PickerOptions => BuildPickerOptions();

    protected override async Task OnInitializedAsync() {
        origin = new(CatalogSource, OwnerLifetime, SelectedCatalogIds, false, false);
        pendingSelectedIds.UnionWith(origin.Ids);
        CatalogSource.ContextChanged += SourceChanged;
        parentRegistration = OwnerLifetime.Register(SourceChanged);
        if (SourceContext is { } context && context != origin.Context) {
            await RetireAsync();
            return;
        }
        await LoadCatalogsAsync();
    }

    private void SourceChanged() {
        if (!disposed) {
            _ = InvokeAsync(RetireAsync);
        }
    }

    private async Task RetireAsync() {
        if (disposed) {
            return;
        }
        await DisposeAsync();
        if (DialogReference is not null) {
            await DialogReference.CloseAsync();
        }
    }

    private async Task LoadCatalogsAsync() {
        if (disposed || origin is not { IsCurrent: true } owner || readRequest is not null || submitted) {
            return;
        }
        var request = CancellationTokenSource.CreateLinkedTokenSource(owner.Token);
        readRequest = request;
        acquired = false;
        loadErrorMessage = null;
        try {
            var rows = await CatalogSource.ListAsync(request.Token);
            if (!CanPublish(owner, request)) {
                return;
            }
            catalogs = SelectionIntent.NormalizeCatalogs(rows);
            await CatalogsLoaded.InvokeAsync(catalogs);
            if (CanPublish(owner, request)) {
                acquired = true;
            }
        } catch (OperationCanceledException) when (request.IsCancellationRequested) {
        } catch (Exception exception) {
            if (CanPublish(owner, request)) {
                loadErrorMessage = "Catalog details are unavailable. Your staged selection is retained. Please retry.";
                Logger.LogWarning("Storage chooser read failed for profile {ProfileId}, generation {Generation}; failure type {FailureType}.",
                    owner.Context.ProfileId, owner.Context.Generation, exception.GetType().Name);
                await CatalogLoadFailed.InvokeAsync();
            }
        } finally {
            if (ReferenceEquals(readRequest, request)) {
                readRequest = null;
            }
            request.Dispose();
        }
    }

    private bool CanPublish(SelectionIntent owner, CancellationTokenSource request) =>
        !disposed && owner.IsCurrent && ReferenceEquals(origin, owner) && ReferenceEquals(readRequest, request);

    private Task ToggleCatalogAsync(Guid id) {
        if (!CanApply || id == Guid.Empty) {
            return Task.CompletedTask;
        }
        if (!pendingSelectedIds.Remove(id) && catalogs.Any(row => row.Id == id && row.IsEnabled)) {
            pendingSelectedIds.Add(id);
        }
        return Task.CompletedTask;
    }

    private Task ConfirmAsync() {
        if (!CanApply) {
            return Task.CompletedTask;
        }
        submitted = true;
        return DialogReference?.CloseAsync(new StorageCatalogSelectionDialogResult(SelectionIntent.Normalize(pendingSelectedIds))) ?? Task.CompletedTask;
    }

    private Task CancelAsync() => DialogReference?.CloseAsync() ?? Task.CompletedTask;

    private IReadOnlyList<ResourceCardPickerOption<Guid>> BuildPickerOptions() {
        var availableIds = catalogs.Select(catalog => catalog.Id).ToHashSet();
        var missing = pendingSelectedIds.Where(id => !availableIds.Contains(id)).Order().Select(BuildMissingOption);
        var available = catalogs.OrderByDescending(catalog => pendingSelectedIds.Contains(catalog.Id)).ThenBy(catalog => catalog.DisplayOrder)
            .ThenBy(catalog => catalog.Name, StringComparer.OrdinalIgnoreCase).Select(BuildCatalogOption);
        return [.. missing, .. available];
    }

    private ResourceCardPickerOption<Guid> BuildCatalogOption(StorageSelectionItem catalog) {
        var selected = pendingSelectedIds.Contains(catalog.Id);
        var tags = new List<string> { catalog.IsEnabled ? "Enabled" : "Disabled", catalog.Health };
        if (catalog.IsReadOnly) {
            tags.Add("Read only");
        }
        if (catalog.IsSystemDefault) {
            tags.Add("System default");
        }
        return new(catalog.Id, catalog.Name, catalog.Provider) {
            Subtitle = catalog.Connection, Description = catalog.EndpointOrRoot, Meta = catalog.Id.ToString("D"), Icon = "storage", Tags = tags,
            AdditionalSearchText = catalog.Health, IsSelected = selected, IsDisabled = !catalog.IsEnabled && !selected,
            DisabledReason = !catalog.IsEnabled && !selected ? "This storage catalog is disabled and cannot be newly selected." : string.Empty,
            TestId = $"{DataTestId}-option-{catalog.Id:N}"
        };
    }

    private ResourceCardPickerOption<Guid> BuildMissingOption(Guid id) => new(id, "Missing storage catalog", "Unavailable reference") {
        Description = "This saved catalog ID is not present in the current workspace catalog. Select this card to remove it.",
        Meta = id.ToString("D"), Icon = "link_off", Tags = ["Missing"], AdditionalSearchText = id.ToString("D"), IsSelected = true,
        TestId = $"{DataTestId}-option-{id:N}"
    };

    public ValueTask DisposeAsync() {
        if (disposed) {
            return ValueTask.CompletedTask;
        }
        disposed = true;
        CatalogSource.ContextChanged -= SourceChanged;
        parentRegistration.Dispose();
        origin?.Dispose();
        var request = readRequest;
        readRequest = null;
        request?.Cancel();
        return ValueTask.CompletedTask;
    }
}
