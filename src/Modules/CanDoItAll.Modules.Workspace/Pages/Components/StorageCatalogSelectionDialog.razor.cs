using CanDoItAll.AppComponents;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Infrastructure.Storage;
using Microsoft.AspNetCore.Components;

namespace CanDoItAll.Modules.Workspace.Pages.Components;

public partial class StorageCatalogSelectionDialog
{
    [Parameter]
    public IReadOnlyList<Guid> SelectedCatalogIds { get; set; } = [];

    [Parameter]
    public EventCallback<IReadOnlyList<StorageCatalogSummary>> CatalogsLoaded { get; set; }

    [Parameter]
    public string DataTestId { get; set; } = "storage-catalog-selection-dialog";

    [Inject]
    public IStorageCatalogSelectionSource CatalogSource { get; set; } = default!;

    [CascadingParameter]
    public DialogReference? DialogReference { get; set; }

    private readonly CancellationTokenSource lifetimeCancellation = new();
    private readonly HashSet<Guid> pendingSelectedIds = [];
    private IReadOnlyList<StorageCatalogSummary> catalogs = [];
    private bool selectionInitialized;
    private bool isLoading = true;
    private string? loadErrorMessage;

    private IReadOnlyList<ResourceCardPickerOption<Guid>> PickerOptions => BuildPickerOptions();

    protected override void OnParametersSet()
    {
        if (selectionInitialized)
        {
            return;
        }

        pendingSelectedIds.Clear();
        foreach (var catalogId in SelectedCatalogIds.Where(id => id != Guid.Empty))
        {
            pendingSelectedIds.Add(catalogId);
        }

        selectionInitialized = true;
    }

    protected override async Task OnInitializedAsync()
    {
        await LoadCatalogsAsync();
    }

    private async Task LoadCatalogsAsync()
    {
        if (lifetimeCancellation.IsCancellationRequested)
        {
            return;
        }

        isLoading = true;
        loadErrorMessage = null;
        try
        {
            catalogs = (await CatalogSource.ListAsync(lifetimeCancellation.Token))
                .Where(catalog => catalog.Id != Guid.Empty)
                .GroupBy(catalog => catalog.Id)
                .Select(group => group.First())
                .OrderBy(catalog => catalog.DisplayOrder)
                .ThenBy(catalog => catalog.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
            await CatalogsLoaded.InvokeAsync(catalogs);
        }
        catch (OperationCanceledException) when (lifetimeCancellation.IsCancellationRequested)
        {
            return;
        }
        catch (Exception exception)
        {
            catalogs = [];
            loadErrorMessage = exception.Message;
        }
        finally
        {
            isLoading = false;
        }
    }

    private Task ToggleCatalogAsync(Guid catalogId)
    {
        var catalog = catalogs.FirstOrDefault(item => item.Id == catalogId);
        if (catalog is { IsEnabled: false } && !pendingSelectedIds.Contains(catalogId))
        {
            return Task.CompletedTask;
        }

        if (!pendingSelectedIds.Add(catalogId))
        {
            pendingSelectedIds.Remove(catalogId);
        }

        return Task.CompletedTask;
    }

    private Task ConfirmAsync()
    {
        var selectedIds = pendingSelectedIds
            .OrderBy(id => id)
            .ToList();
        return DialogReference?.CloseAsync(new StorageCatalogSelectionDialogResult(selectedIds))
            ?? Task.CompletedTask;
    }

    private Task CancelAsync()
    {
        return DialogReference?.CloseAsync() ?? Task.CompletedTask;
    }

    private IReadOnlyList<ResourceCardPickerOption<Guid>> BuildPickerOptions()
    {
        var availableIds = catalogs.Select(catalog => catalog.Id).ToHashSet();
        var missingSelections = pendingSelectedIds
            .Where(id => !availableIds.Contains(id))
            .OrderBy(id => id)
            .Select(BuildMissingOption);
        var catalogOptions = catalogs
            .OrderByDescending(catalog => pendingSelectedIds.Contains(catalog.Id))
            .ThenBy(catalog => catalog.DisplayOrder)
            .ThenBy(catalog => catalog.Name, StringComparer.OrdinalIgnoreCase)
            .Select(BuildCatalogOption);

        return missingSelections.Concat(catalogOptions).ToList();
    }

    private ResourceCardPickerOption<Guid> BuildCatalogOption(StorageCatalogSummary catalog)
    {
        var isSelected = pendingSelectedIds.Contains(catalog.Id);
        var tags = new List<string>
        {
            catalog.IsEnabled ? "Enabled" : "Disabled",
            StoragePresentation.DescribeHealth(catalog.HealthStatus)
        };
        if (catalog.IsReadOnly)
        {
            tags.Add("Read only");
        }

        if (catalog.IsSystemDefault)
        {
            tags.Add("System default");
        }

        return new ResourceCardPickerOption<Guid>(
            catalog.Id,
            catalog.Name,
            StoragePresentation.DescribeProvider(catalog.ProviderKind))
        {
            Subtitle = StoragePresentation.DescribeConnectionMode(catalog.ConnectionMode),
            Description = catalog.EndpointOrRoot,
            Meta = catalog.Id.ToString("D"),
            Icon = "storage",
            Tags = tags,
            AdditionalSearchText = string.Join(
                ' ',
                StoragePresentation.DescribeHealth(catalog.HealthStatus),
                catalog.LastHealthMessage),
            IsSelected = isSelected,
            IsDisabled = !catalog.IsEnabled && !isSelected,
            DisabledReason = !catalog.IsEnabled && !isSelected
                ? "This storage catalog is disabled and cannot be newly selected."
                : string.Empty,
            TestId = $"{DataTestId}-option-{catalog.Id:N}"
        };
    }

    private ResourceCardPickerOption<Guid> BuildMissingOption(Guid catalogId)
    {
        return new ResourceCardPickerOption<Guid>(
            catalogId,
            "Missing storage catalog",
            "Unavailable reference")
        {
            Description = "This saved catalog ID is not present in the current workspace catalog. Select this card to remove it.",
            Meta = catalogId.ToString("D"),
            Icon = "link_off",
            Tags = ["Missing"],
            AdditionalSearchText = catalogId.ToString("D"),
            IsSelected = true,
            TestId = $"{DataTestId}-option-{catalogId:N}"
        };
    }

    public ValueTask DisposeAsync()
    {
        lifetimeCancellation.Cancel();
        lifetimeCancellation.Dispose();
        return ValueTask.CompletedTask;
    }
}
