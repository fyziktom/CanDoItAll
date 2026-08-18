using CanDoItAll.AppComponents;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Infrastructure.Storage;
using Microsoft.AspNetCore.Components;

namespace CanDoItAll.Modules.Workspace.Pages.Components;

public partial class StorageCatalogSelectionField
{
    private readonly CancellationTokenSource lifetimeCancellation = new();

    [Parameter]
    public IReadOnlyList<Guid> Value { get; set; } = [];

    [Parameter]
    public EventCallback<IReadOnlyList<Guid>> ValueChanged { get; set; }

    [Parameter]
    public bool AllowAll { get; set; }

    [Parameter]
    public bool Disabled { get; set; }

    [Parameter]
    public string Label { get; set; } = "Allowed storage catalogs";

    [Parameter]
    public string Description { get; set; } =
        "Limit storage tools to explicit catalog references when Allow all is off.";

    [Parameter]
    public string DataTestId { get; set; } = "storage-catalog-selection";

    [Inject]
    public DialogService DialogService { get; set; } = default!;

    [Inject]
    public IStorageCatalogSelectionSource CatalogSource { get; set; } = default!;

    private IReadOnlyDictionary<Guid, StorageCatalogSummary> knownCatalogs =
        new Dictionary<Guid, StorageCatalogSummary>();
    private bool catalogsHaveLoaded;
    private bool isResolvingCatalogDetails;
    private bool isOpening;
    private string? lastCatalogDetailsRequestKey;
    private string? catalogDetailsErrorMessage;
    private string? openErrorMessage;

    private bool ChooserDisabled => Disabled || AllowAll || isOpening;

    private IReadOnlyList<SelectedReferenceItem<Guid>> SelectedReferences => NormalizeIds(Value)
        .Select(BuildSelectedReference)
        .ToList();

    protected override async Task OnParametersSetAsync()
    {
        var selectedIds = NormalizeIds(Value);
        if (selectedIds.Count == 0 || catalogsHaveLoaded)
        {
            return;
        }

        var requestKey = string.Join(',', selectedIds);
        if (string.Equals(lastCatalogDetailsRequestKey, requestKey, StringComparison.Ordinal))
        {
            return;
        }

        lastCatalogDetailsRequestKey = requestKey;
        await LoadCatalogDetailsAsync();
    }

    private async Task LoadCatalogDetailsAsync()
    {
        if (lifetimeCancellation.IsCancellationRequested)
        {
            return;
        }

        isResolvingCatalogDetails = true;
        catalogDetailsErrorMessage = null;
        try
        {
            await HandleCatalogsLoadedAsync(
                await CatalogSource.ListAsync(lifetimeCancellation.Token));
        }
        catch (OperationCanceledException) when (lifetimeCancellation.IsCancellationRequested)
        {
            return;
        }
        catch (Exception exception)
        {
            catalogDetailsErrorMessage =
                $"Saved catalog names could not be loaded. Open the chooser to retry. {exception.Message}";
        }
        finally
        {
            isResolvingCatalogDetails = false;
        }
    }

    private async Task OpenPickerAsync()
    {
        if (ChooserDisabled)
        {
            return;
        }

        isOpening = true;
        openErrorMessage = null;
        try
        {
            var result = await DialogService.OpenAsync<StorageCatalogSelectionDialog>(
                "Choose storage catalogs",
                new Dictionary<string, object?>
                {
                    [nameof(StorageCatalogSelectionDialog.SelectedCatalogIds)] = NormalizeIds(Value),
                    [nameof(StorageCatalogSelectionDialog.CatalogsLoaded)] =
                        EventCallback.Factory.Create<IReadOnlyList<StorageCatalogSummary>>(
                            this,
                            HandleCatalogsLoadedAsync),
                    [nameof(StorageCatalogSelectionDialog.DataTestId)] = $"{DataTestId}-dialog"
                },
                new DialogOptions
                {
                    Eyebrow = "Storage access",
                    Subtitle = "Search the current workspace catalog and stage the references this agent may use.",
                    Size = ModalSize.Wide,
                    DenseChrome = true,
                    AriaLabel = "Choose allowed storage catalogs",
                    TestId = $"{DataTestId}-dialog-shell"
                });

            if (result is StorageCatalogSelectionDialogResult selection)
            {
                var next = NormalizeIds(selection.SelectedCatalogIds);
                if (!next.SequenceEqual(NormalizeIds(Value)))
                {
                    await ValueChanged.InvokeAsync(next);
                }
            }
        }
        catch (Exception exception)
        {
            openErrorMessage = $"The storage catalog chooser could not be opened. {exception.Message}";
        }
        finally
        {
            isOpening = false;
        }
    }

    private Task HandleCatalogsLoadedAsync(IReadOnlyList<StorageCatalogSummary> catalogs)
    {
        knownCatalogs = catalogs
            .GroupBy(catalog => catalog.Id)
            .Select(group => group.First())
            .ToDictionary(catalog => catalog.Id);
        catalogsHaveLoaded = true;
        catalogDetailsErrorMessage = null;
        return Task.CompletedTask;
    }

    private Task RemoveCatalogAsync(Guid catalogId)
    {
        if (ChooserDisabled)
        {
            return Task.CompletedTask;
        }

        var next = NormalizeIds(Value)
            .Where(id => id != catalogId)
            .ToList();
        return ValueChanged.InvokeAsync(next);
    }

    private SelectedReferenceItem<Guid> BuildSelectedReference(Guid catalogId)
    {
        if (knownCatalogs.TryGetValue(catalogId, out var catalog))
        {
            var status = ResolveCatalogStatus(catalog);
            return new SelectedReferenceItem<Guid>(
                catalogId,
                catalog.Name,
                catalogId.ToString("D"))
            {
                DetailText = BuildCatalogDetail(catalog),
                StatusText = status.Text,
                StatusTone = status.Tone,
                TestId = $"{DataTestId}-selected-row-{catalogId:N}",
                CanRemove = true
            };
        }

        return new SelectedReferenceItem<Guid>(
            catalogId,
            catalogsHaveLoaded ? "Missing storage catalog" : "Storage catalog reference",
            catalogId.ToString("D"))
        {
            DetailText = catalogsHaveLoaded
                ? "This saved catalog ID is not present in the current workspace catalog."
                : isResolvingCatalogDetails
                    ? "Loading the catalog name and connection details."
                    : "Open the chooser to refresh catalog details.",
            StatusText = catalogsHaveLoaded
                ? "Missing"
                : isResolvingCatalogDetails
                    ? "Loading"
                    : "Not loaded",
            StatusTone = isResolvingCatalogDetails
                ? SelectedReferenceStatusTone.Info
                : SelectedReferenceStatusTone.Warning,
            TestId = $"{DataTestId}-selected-row-{catalogId:N}",
            CanRemove = true
        };
    }

    private static (string Text, SelectedReferenceStatusTone Tone) ResolveCatalogStatus(
        StorageCatalogSummary catalog)
    {
        if (!catalog.IsEnabled)
        {
            return ("Disabled", SelectedReferenceStatusTone.Warning);
        }

        return catalog.IsReadOnly
            ? ("Read only", SelectedReferenceStatusTone.Info)
            : ("Enabled", SelectedReferenceStatusTone.Success);
    }

    private static string BuildCatalogDetail(StorageCatalogSummary catalog)
    {
        var connection = StoragePresentation.DescribeConnectionMode(catalog.ConnectionMode);
        var provider = StoragePresentation.DescribeProvider(catalog.ProviderKind);
        return string.IsNullOrWhiteSpace(catalog.EndpointOrRoot)
            ? $"{provider} · {connection}"
            : $"{provider} · {connection} · {catalog.EndpointOrRoot}";
    }

    private static IReadOnlyList<Guid> NormalizeIds(IEnumerable<Guid> ids)
    {
        return ids
            .Where(id => id != Guid.Empty)
            .Distinct()
            .OrderBy(id => id)
            .ToList();
    }

    public ValueTask DisposeAsync()
    {
        lifetimeCancellation.Cancel();
        lifetimeCancellation.Dispose();
        return ValueTask.CompletedTask;
    }
}
