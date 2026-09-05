using CanDoItAll.Components.BaseLib;
using CanDoItAll.Modules.AgentFramework.ProviderManagement;
using CanDoItAll.Modules.Security;
using CanDoItAll.SharedProviders.Abstractions;
using Microsoft.AspNetCore.Components;

namespace CanDoItAll.Modules.AgentFramework.Pages.Components;

public partial class SharedProviderSourcesDialog : IDisposable {
    [Inject]
    public ISharedProviderManagementService ManagementService { get; set; } = default!;

    [Inject]
    public NotificationService NotificationService { get; set; } = default!;

    [Parameter]
    public IReadOnlyList<SecretListItem> Secrets { get; set; } = [];

    [Parameter]
    public EventCallback<SharedProviderChange> ProvidersChanged { get; set; }

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
    private bool operationBusy;
    private bool mutationUnconfirmed;
    private bool isBusy => operationBusy || mutationUnconfirmed;
    private readonly CancellationTokenSource lifetime = new();
    private long generation;
    private long readGeneration;
    private bool disposed;
    private bool sourceDialogOpen;
    private bool catalogDialogOpen;

    protected override Task OnInitializedAsync() => LoadAsync();

    private async Task LoadAsync() {
        if (disposed) {
            return;
        }
        var read = ++readGeneration;
        isLoading = true;
        loadError = string.Empty;
        try {
            var result = await ManagementService.ListSourcesAsync(lifetime.Token);
            if (!disposed && read == readGeneration) {
                sources = result;
            }
        } catch (OperationCanceledException) when (lifetime.IsCancellationRequested) {
        } catch (Exception) {
            if (!disposed && read == readGeneration) {
                loadError = "Shared-provider connections could not be loaded.";
            }
        } finally {
            if (!disposed && read == readGeneration) {
                isLoading = false;
            }
        }
    }

    private bool IsCurrent(long operation) => !disposed && operation == generation;

    private async Task CloseOverlayAsync() {
        Dispose();
        await OnClose.InvokeAsync();
    }

    public void Dispose() {
        if (disposed) {
            return;
        }
        disposed = true;
        lifetime.Cancel();
        sourceDialogOpen = false;
        catalogDialogOpen = false;
    }

    private void OpenNewSourceDialog() {
        if (isBusy || disposed) {
            return;
        }
        sourceEditor = new SharedProviderSourceEditorModel {
            IsEnabled = true,
            ApiTokenSecretId = Secrets.Count == 1 ? Secrets[0].Id : Guid.Empty
        };
        sourceDialogError = string.Empty;
        sourceDialogOpen = true;
    }

    private void OpenEditSourceDialog(SharedProviderSourceSnapshot source) {
        if (isBusy || disposed) {
            return;
        }
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

        var request = new SharedProviderSourceEditorRequest(
            sourceEditor.Id, sourceEditor.ExpectedConcurrencyToken, sourceEditor.Name, baseUri,
            sourceEditor.ApiTokenSecretId, sourceEditor.IsEnabled, sourceEditor.AllowInsecurePrivateNetwork);
        await RunSourceMutationAsync(async token => {
            var result = await ManagementService.SaveSourceAsync(request, token);
            if (!disposed) {
                sourceEditor.Id = result.Id;
                sourceEditor.ExpectedConcurrencyToken = result.ConcurrencyToken;
                sourceDialogOpen = false;
            }
            return result.Change;
        }, "Source saved");
    }

    private Task ToggleSourceAsync(SharedProviderSourceSnapshot source) =>
        RunSourceMutationAsync(async token =>
            (await ManagementService.SetSourceEnabledAsync(source.Id, source.ConcurrencyToken, !source.IsEnabled, token)).Change,
            source.IsEnabled ? "Source disabled" : "Source enabled");

    private Task DeleteSourceAsync(SharedProviderSourceSnapshot source) =>
        RunSourceMutationAsync(async token =>
            (await ManagementService.DeleteSourceAsync(source.Id, source.ConcurrencyToken, token)).Change, "Source deleted");

    private async Task RunSourceMutationAsync(
        Func<CancellationToken, Task<SharedProviderChange?>> mutation, string successTitle) {
        if (disposed || isBusy) {
            return;
        }
        var operation = ++generation;
        operationBusy = true;
        SharedProviderChange? committed = null;
        try {
            var change = await mutation(lifetime.Token);
            committed = change;
            if (!IsCurrent(operation)) {
                return;
            }
            await PublishChangeAsync(change, operation);
            if (!IsCurrent(operation)) {
                return;
            }
            await LoadAsync();
            if (IsCurrent(operation)) {
                if (change?.Warning is { } warning) {
                    NotificationService.Warning(successTitle, warning);
                } else {
                    NotificationService.Success(successTitle, "The authoritative source state was saved.");
                }
            }
        } catch (OperationCanceledException) when (lifetime.IsCancellationRequested) {
        } catch (Exception exception) {
            if (IsCurrent(operation)) {
                if (committed is not null) {
                    loadError = "The shared-provider change is saved, but its workspace refresh did not complete.";
                } else {
                    await HandleOperationFailureAsync(exception, operation);
                }
            }
        } finally {
            if (IsCurrent(operation)) {
                operationBusy = false;
            }
        }
    }

    private async Task PublishChangeAsync(SharedProviderChange? change, long operation) {
        if (change is not null && IsCurrent(operation)) {
            await ProvidersChanged.InvokeAsync(change);
        }
    }

    private async Task HandleOperationFailureAsync(Exception exception, long operation) {
        if (exception is SharedProviderCommittedException committed) {
            loadError = committed.Change.Warning!;
            await PublishChangeAsync(committed.Change, operation);
            return;
        }
        var rejected = exception is SharedProviderConcurrencyException or SharedProviderSourceDeletionBlockedException
            or ArgumentException or KeyNotFoundException;
        mutationUnconfirmed = !rejected;
        var message = rejected ? "The source change was rejected. Reload current source state and correct the request."
            : "The source outcome is unconfirmed. Verify its state before repeating the operation.";
        loadError = message;
        sourceDialogError = message;
        NotificationService.Warning("Source change needs attention", message);
        if (!rejected) {
            await PublishChangeAsync(new(SharedProviderChangeKind.SourceAvailability, [],
                commitState: SharedProviderCommitState.Unconfirmed, unknownScope: true, warning: message), operation);
        }
    }

    private async Task TestSourceAsync(Guid sourceId) {
        var operation = generation + 1;
        var result = await RunSourceOperationAsync(
            token => ManagementService.TestSourceAsync(sourceId, token));
        if (!IsCurrent(operation)) {
            return;
        }
        if (result?.Outcome == SharedProviderSourceOperationOutcome.Succeeded) {
            NotificationService.Success(
                "Source connection passed",
                $"The catalog contains {result.Catalog!.Providers.Count} published provider(s).");
        }
    }

    private async Task DiscoverSourceAsync(SharedProviderSourceManagementSnapshot source) {
        var operation = generation + 1;
        var result = await RunSourceOperationAsync(
            token => ManagementService.TestSourceAsync(source.Source.Id, token));
        if (!IsCurrent(operation)) {
            return;
        }
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
        var operation = generation + 1;
        var selected = source.Imports
            .Where(import => import.SelectionState == SharedProviderSelectionState.Selected)
            .Select(import => import.RemotePublicationId)
            .ToHashSet();
        var result = await RunSourceOperationAsync(
            token => ManagementService.SynchronizeSourceAsync(source.Source.Id, selected, token));
        if (!IsCurrent(operation)) {
            return;
        }
        if (result?.IsSuccessful == true) {
            NotificationService.Success("Source synchronized", DescribeSourceOperation(result));
        }
    }

    private async Task ApplyCatalogSelectionAsync() {
        if (catalogSourceId == Guid.Empty) {
            return;
        }

        var operation = generation + 1;
        var result = await RunSourceOperationAsync(
            token => ManagementService.SynchronizeSourceAsync(
                catalogSourceId,
                selectedPublicationIds.ToHashSet(), token));
        if (!IsCurrent(operation)) {
            return;
        }
        if (result?.IsSuccessful != true) {
            return;
        }

        catalogDialogOpen = false;
        NotificationService.Success("Shared providers imported", DescribeSourceOperation(result));
    }

    private async Task<SharedProviderSourceOperationResult?> RunSourceOperationAsync(
        Func<CancellationToken, Task<SharedProviderSourceOperationResult>> run) {
        if (disposed || isBusy) {
            return null;
        }
        var operation = ++generation;
        operationBusy = true;
        SharedProviderChange? committed = null;
        try {
            var result = await run(lifetime.Token);
            committed = result.Change;
            if (!IsCurrent(operation)) {
                return null;
            }
            await PublishChangeAsync(result.Change, operation);
            if (!IsCurrent(operation)) {
                return null;
            }
            await LoadAsync();
            if (!IsCurrent(operation)) {
                return null;
            }
            if (result.Change?.Warning is { } warning) {
                NotificationService.Warning("Shared-provider change saved", warning);
            }
            if (!result.IsSuccessful) {
                NotificationService.Warning("Shared-provider source is unavailable",
                    result.Failure?.SanitizedMessage ?? FormatStatus(result.Outcome));
            }
            return result;
        } catch (OperationCanceledException) when (lifetime.IsCancellationRequested) {
            return null;
        } catch (Exception exception) {
            if (IsCurrent(operation)) {
                if (committed is not null) {
                    loadError = "The shared-provider change is saved, but its workspace refresh did not complete.";
                } else {
                    await HandleOperationFailureAsync(exception, operation);
                }
            }
            return null;
        } finally {
            if (IsCurrent(operation)) {
                operationBusy = false;
            }
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
