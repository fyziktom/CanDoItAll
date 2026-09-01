using CanDoItAll.Components.BaseLib;
using CanDoItAll.Modules.AgentFramework.ProviderManagement;
using CanDoItAll.Modules.Security;
using CanDoItAll.SharedProviders.Abstractions;
using Microsoft.AspNetCore.Components;

namespace CanDoItAll.Modules.AgentFramework.Pages.Components;

public partial class SharedProviderSourcesDialog {
    [Inject]
    public ISharedProviderManagementService ManagementService { get; set; } = default!;

    [Inject]
    public NotificationService NotificationService { get; set; } = default!;

    [Parameter]
    public IReadOnlyList<SecretListItem> Secrets { get; set; } = [];

    [Parameter]
    public EventCallback ProvidersChanged { get; set; }

    [Parameter]
    public EventCallback OnClose { get; set; }

    private IReadOnlyList<SharedProviderSourceManagementSnapshot> sources = [];
    private SharedProviderSourceEditorModel sourceEditor = new();
    private IReadOnlyList<SharedProviderCatalogPublication> catalogPublications = [];
    private readonly HashSet<SharedProviderPublicationId> selectedPublicationIds = [];
    private Guid catalogSourceId;
    private string catalogDialogSubtitle = string.Empty;
    private string sourceDialogError = string.Empty;
    private string loadError = string.Empty;
    private bool isLoading;
    private bool isBusy;
    private bool sourceDialogOpen;
    private bool catalogDialogOpen;

    protected override Task OnInitializedAsync() => LoadAsync();

    private async Task LoadAsync() {
        isLoading = true;
        loadError = string.Empty;
        try {
            sources = await ManagementService.ListSourcesAsync();
        } catch (Exception exception) {
            sources = [];
            loadError = exception.Message;
        } finally {
            isLoading = false;
        }
    }

    private void OpenNewSourceDialog() {
        sourceEditor = new SharedProviderSourceEditorModel {
            IsEnabled = true,
            ApiTokenSecretId = Secrets.Count == 1 ? Secrets[0].Id : Guid.Empty
        };
        sourceDialogError = string.Empty;
        sourceDialogOpen = true;
    }

    private void OpenEditSourceDialog(SharedProviderSourceSnapshot source) {
        sourceEditor = new SharedProviderSourceEditorModel {
            Id = source.Id,
            ExpectedConcurrencyToken = source.ConcurrencyToken,
            Name = source.Name,
            BaseUri = source.BaseUri.AbsoluteUri,
            ApiTokenSecretId = source.ApiTokenSecretId,
            IsEnabled = source.IsEnabled,
            AllowInsecurePrivateNetwork =
                source.NetworkPolicy == SharedProviderSourceNetworkPolicy.AllowPrivateNetwork
        };
        sourceDialogError = string.Empty;
        sourceDialogOpen = true;
    }

    private void CloseSourceDialog() {
        sourceDialogOpen = false;
        sourceDialogError = string.Empty;
    }

    private async Task SaveSourceAsync() {
        sourceDialogError = string.Empty;
        if (string.IsNullOrWhiteSpace(sourceEditor.Name)) {
            sourceDialogError = "Enter a source name.";
            return;
        }

        if (!Uri.TryCreate(sourceEditor.BaseUri.Trim(), UriKind.Absolute, out var baseUri)) {
            sourceDialogError = "Enter an absolute HTTP or HTTPS instance URL.";
            return;
        }

        if (sourceEditor.ApiTokenSecretId == Guid.Empty) {
            sourceDialogError = "Select a stored source credential.";
            return;
        }

        isBusy = true;
        try {
            await ManagementService.SaveSourceAsync(
                new SharedProviderSourceEditorRequest(
                    sourceEditor.Id,
                    sourceEditor.ExpectedConcurrencyToken,
                    sourceEditor.Name,
                    baseUri,
                    sourceEditor.ApiTokenSecretId,
                    sourceEditor.IsEnabled,
                    sourceEditor.AllowInsecurePrivateNetwork));
            sourceDialogOpen = false;
            await LoadAsync();
            NotificationService.Success("Source saved", "The shared-provider source configuration was saved.");
        } catch (Exception exception) {
            sourceDialogError = exception.Message;
        } finally {
            isBusy = false;
        }
    }

    private async Task ToggleSourceAsync(SharedProviderSourceSnapshot source) {
        await RunSourceMutationAsync(
            async () => {
                await ManagementService.SetSourceEnabledAsync(
                    source.Id,
                    source.ConcurrencyToken,
                    !source.IsEnabled);
            },
            source.IsEnabled ? "Source disabled" : "Source enabled");
    }

    private async Task DeleteSourceAsync(SharedProviderSourceSnapshot source) {
        await RunSourceMutationAsync(
            async () => {
                await ManagementService.DeleteSourceAsync(
                    source.Id,
                    source.ConcurrencyToken);
            },
            "Source deleted");
    }

    private async Task RunSourceMutationAsync(Func<Task> mutation, string successTitle) {
        isBusy = true;
        try {
            await mutation();
            await LoadAsync();
            NotificationService.Success(successTitle, "Shared-provider source state was updated.");
        } catch (Exception exception) {
            NotificationService.Error("Source change failed", exception.Message);
            await LoadAsync();
        } finally {
            isBusy = false;
        }
    }

    private async Task TestSourceAsync(Guid sourceId) {
        var result = await RunSourceOperationAsync(
            () => ManagementService.TestSourceAsync(sourceId));
        if (result?.Outcome == SharedProviderSourceOperationOutcome.Succeeded) {
            NotificationService.Success(
                "Source connection passed",
                $"The catalog contains {result.Catalog!.Providers.Count} published provider(s).");
        }
    }

    private async Task DiscoverSourceAsync(SharedProviderSourceManagementSnapshot source) {
        var result = await RunSourceOperationAsync(
            () => ManagementService.TestSourceAsync(source.Source.Id));
        if (result?.Outcome != SharedProviderSourceOperationOutcome.Succeeded ||
            result.Catalog is null) {
            return;
        }

        catalogSourceId = source.Source.Id;
        catalogDialogSubtitle = $"{source.Source.Name} · {result.Catalog.Providers.Count} published provider(s)";
        catalogPublications = result.Catalog.Providers;
        selectedPublicationIds.Clear();
        selectedPublicationIds.UnionWith(source.Imports
            .Where(import => import.SelectionState == SharedProviderSelectionState.Selected)
            .Select(import => import.RemotePublicationId));
        catalogDialogOpen = true;
    }

    private async Task SynchronizeExistingAsync(SharedProviderSourceManagementSnapshot source) {
        var selected = source.Imports
            .Where(import => import.SelectionState == SharedProviderSelectionState.Selected)
            .Select(import => import.RemotePublicationId)
            .ToHashSet();
        var result = await RunSourceOperationAsync(
            () => ManagementService.SynchronizeSourceAsync(source.Source.Id, selected));
        if (result?.IsSuccessful == true) {
            await ProvidersChanged.InvokeAsync();
            NotificationService.Success("Source synchronized", DescribeSourceOperation(result));
        }
    }

    private async Task ApplyCatalogSelectionAsync() {
        if (catalogSourceId == Guid.Empty) {
            return;
        }

        var result = await RunSourceOperationAsync(
            () => ManagementService.SynchronizeSourceAsync(
                catalogSourceId,
                selectedPublicationIds));
        if (result?.IsSuccessful != true) {
            return;
        }

        catalogDialogOpen = false;
        await ProvidersChanged.InvokeAsync();
        NotificationService.Success("Shared providers imported", DescribeSourceOperation(result));
    }

    private async Task<SharedProviderSourceOperationResult?> RunSourceOperationAsync(
        Func<Task<SharedProviderSourceOperationResult>> operation) {
        isBusy = true;
        try {
            var result = await operation();
            await LoadAsync();
            if (result.IsSuccessful) {
                return result;
            }

            NotificationService.Warning(
                "Shared-provider source is unavailable",
                result.Failure?.SanitizedMessage ?? FormatStatus(result.Outcome));
            return result;
        } catch (Exception exception) {
            NotificationService.Error("Shared-provider source failed", exception.Message);
            await LoadAsync();
            return null;
        } finally {
            isBusy = false;
        }
    }

    private void SetPublicationSelected(
        SharedProviderPublicationId publicationId,
        ChangeEventArgs args) {
        if (args.Value is true ||
            bool.TryParse(args.Value?.ToString(), out var isSelected) && isSelected) {
            selectedPublicationIds.Add(publicationId);
        } else {
            selectedPublicationIds.Remove(publicationId);
        }
    }

    private void CloseCatalogDialog() {
        catalogDialogOpen = false;
        catalogSourceId = Guid.Empty;
        catalogPublications = [];
        selectedPublicationIds.Clear();
    }

    private static string DescribeSourceOperation(SharedProviderSourceOperationResult result) {
        if (result.Outcome == SharedProviderSourceOperationOutcome.NotModified) {
            return "The remote catalog has not changed.";
        }

        return $"Updated {result.AffectedProviderProfileIds.Count} profile(s) and retired {result.RetiredProviderProfileIds.Count} profile(s).";
    }

    private static string ResolveSourceTone(SharedProviderSourceStatus status) => status switch {
        SharedProviderSourceStatus.Available => "success",
        SharedProviderSourceStatus.NeverSynchronized => "neutral",
        SharedProviderSourceStatus.SourceOffline => "warning",
        _ => "danger"
    };

    private static string ResolveHealthTone(SharedProviderHealthState state) => state switch {
        SharedProviderHealthState.Available => "success",
        SharedProviderHealthState.Degraded => "warning",
        _ => "danger"
    };

    private static string FormatStatus<T>(T value) where T : struct, Enum {
        var text = value.ToString();
        return string.Concat(text.Select((character, index) =>
            index > 0 && char.IsUpper(character)
                ? $" {char.ToLowerInvariant(character)}"
                : char.ToLowerInvariant(character).ToString()));
    }

    private sealed class SharedProviderSourceEditorModel {
        public Guid? Id { get; set; }

        public Guid? ExpectedConcurrencyToken { get; set; }

        public string Name { get; set; } = string.Empty;

        public string BaseUri { get; set; } = string.Empty;

        public Guid ApiTokenSecretId { get; set; }

        public bool IsEnabled { get; set; }

        public bool AllowInsecurePrivateNetwork { get; set; }
    }

}
