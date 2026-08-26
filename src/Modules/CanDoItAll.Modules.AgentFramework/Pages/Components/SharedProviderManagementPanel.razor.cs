using CanDoItAll.Components.BaseLib;
using CanDoItAll.Modules.AgentFramework.ProviderManagement;
using CanDoItAll.Modules.Security;
using CanDoItAll.SharedProviders.Abstractions;
using Microsoft.AspNetCore.Components;

namespace CanDoItAll.Modules.AgentFramework.Pages.Components;

public partial class SharedProviderManagementPanel
{
    [Inject]
    public ISharedProviderManagementService ManagementService { get; set; } = default!;

    [Inject]
    public NotificationService NotificationService { get; set; } = default!;

    [Parameter]
    public Guid? ProviderProfileId { get; set; }

    [Parameter]
    public IReadOnlyList<SecretListItem> Secrets { get; set; } = [];

    [Parameter]
    public EventCallback ProvidersChanged { get; set; }

    private SharedProviderProfileSharingSnapshot? profileState;
    private IReadOnlyList<SharedProviderSourceManagementSnapshot> sources = [];
    private SharedProviderSourceEditorModel sourceEditor = new();
    private IReadOnlyList<SharedProviderCatalogPublication> catalogPublications = [];
    private readonly HashSet<SharedProviderPublicationId> selectedPublicationIds = [];
    private Guid? loadedProviderProfileId;
    private Guid catalogSourceId;
    private string catalogDialogSubtitle = string.Empty;
    private string sourceDialogError = string.Empty;
    private string confirmationTitle = string.Empty;
    private string confirmationMessage = string.Empty;
    private string confirmationActionText = string.Empty;
    private ConfirmationAction confirmationAction;
    private bool isLoading;
    private bool isBusy;
    private bool sourceDialogOpen;
    private bool catalogDialogOpen;
    private bool confirmationDialogOpen;

    protected override async Task OnParametersSetAsync()
    {
        if (loadedProviderProfileId == ProviderProfileId)
        {
            return;
        }

        loadedProviderProfileId = ProviderProfileId;
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        isLoading = true;
        try
        {
            var sourcesTask = ManagementService.ListSourcesAsync();
            var profileTask = LoadProfileSharingAsync();
            await Task.WhenAll(sourcesTask, profileTask);
            sources = await sourcesTask;
            profileState = await profileTask;
        }
        catch (Exception exception)
        {
            NotificationService.Error("Shared providers failed", exception.Message);
        }
        finally
        {
            isLoading = false;
        }
    }

    private Task<SharedProviderProfileSharingSnapshot?> LoadProfileSharingAsync()
        => ProviderProfileId.HasValue
            ? LoadProfileSharingCoreAsync(ProviderProfileId.Value)
            : Task.FromResult<SharedProviderProfileSharingSnapshot?>(null);

    private async Task<SharedProviderProfileSharingSnapshot?> LoadProfileSharingCoreAsync(
        Guid providerProfileId)
        => await ManagementService.GetProfileSharingAsync(providerProfileId);

    private Task PublishAsync()
        => ChangePublicationAsync(SharedProviderPublicationAction.Publish);

    private async Task ChangePublicationAsync(SharedProviderPublicationAction action)
    {
        if (profileState?.Publication is not { } publication)
        {
            return;
        }

        isBusy = true;
        try
        {
            profileState = await ManagementService.SetPublicationAsync(
                profileState.ProviderProfileId,
                action,
                publication.ConcurrencyToken);
            NotificationService.Success(
                action == SharedProviderPublicationAction.Publish
                    ? "Provider published"
                    : "Provider unpublished",
                action == SharedProviderPublicationAction.Publish
                    ? "The provider is now present in the shared-provider catalog."
                    : "New remote requests can no longer discover or invoke this provider.");
        }
        catch (Exception exception)
        {
            NotificationService.Error("Publication change failed", exception.Message);
            await LoadAsync();
        }
        finally
        {
            isBusy = false;
            confirmationDialogOpen = false;
        }
    }

    private void OpenUnpublishConfirmation()
    {
        confirmationAction = ConfirmationAction.Unpublish;
        confirmationTitle = "Unpublish this provider?";
        confirmationMessage = "The provider disappears from discovery and new remote invocations fail closed.";
        confirmationActionText = "Unpublish";
        confirmationDialogOpen = true;
    }

    private void OpenRetireConfirmation()
    {
        confirmationAction = ConfirmationAction.RetireImport;
        confirmationTitle = "Retire this imported provider?";
        confirmationMessage = "The local profile remains for audit and can be reactivated by selecting it during a later catalog import.";
        confirmationActionText = "Retire import";
        confirmationDialogOpen = true;
    }

    private async Task ConfirmDestructiveActionAsync()
    {
        if (confirmationAction == ConfirmationAction.Unpublish)
        {
            await ChangePublicationAsync(SharedProviderPublicationAction.Unpublish);
            return;
        }

        if (confirmationAction == ConfirmationAction.RetireImport)
        {
            await RetireImportedProfileAsync();
        }
    }

    private void CloseConfirmationDialog()
    {
        confirmationDialogOpen = false;
        confirmationAction = ConfirmationAction.None;
    }

    private async Task SaveImportedProfileAsync(SharedProviderImportedProfileEditModel editModel)
    {
        if (profileState?.Import is not { } import)
        {
            return;
        }

        isBusy = true;
        try
        {
            profileState = await ManagementService.UpdateImportedProfileAsync(
                new SharedProviderImportedProfileUpdateRequest(
                    import.ImportId,
                    import.ProviderProfileId,
                    editModel.LocalAlias,
                    editModel.IsEnabled,
                    import.ImportConcurrencyToken,
                    import.ProviderConcurrencyToken));
            await ProvidersChanged.InvokeAsync();
            NotificationService.Success(
                "Imported provider updated",
                "The local alias and enabled intent were saved. Remote-owned fields were not changed.");
        }
        catch (Exception exception)
        {
            NotificationService.Error("Imported provider update failed", exception.Message);
            await LoadAsync();
        }
        finally
        {
            isBusy = false;
        }
    }

    private async Task RetireImportedProfileAsync()
    {
        if (profileState?.Import is not { } import)
        {
            return;
        }

        isBusy = true;
        try
        {
            profileState = await ManagementService.RetireImportedProfileAsync(
                new SharedProviderImportedProfileRetireRequest(
                    import.ImportId,
                    import.ProviderProfileId,
                    import.ImportConcurrencyToken,
                    import.ProviderConcurrencyToken));
            await ProvidersChanged.InvokeAsync();
            NotificationService.Success(
                "Imported provider retired",
                "The provider is no longer selected for runtime use.");
        }
        catch (Exception exception)
        {
            NotificationService.Error("Imported provider retirement failed", exception.Message);
            await LoadAsync();
        }
        finally
        {
            isBusy = false;
            confirmationDialogOpen = false;
        }
    }

    private void OpenNewSourceDialog()
    {
        sourceEditor = new SharedProviderSourceEditorModel
        {
            IsEnabled = true,
            ApiTokenSecretId = Secrets.Count == 1 ? Secrets[0].Id : Guid.Empty
        };
        sourceDialogError = string.Empty;
        sourceDialogOpen = true;
    }

    private void OpenEditSourceDialog(SharedProviderSourceSnapshot source)
    {
        sourceEditor = new SharedProviderSourceEditorModel
        {
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

    private void CloseSourceDialog()
    {
        sourceDialogOpen = false;
        sourceDialogError = string.Empty;
    }

    private async Task SaveSourceAsync()
    {
        sourceDialogError = string.Empty;
        if (string.IsNullOrWhiteSpace(sourceEditor.Name))
        {
            sourceDialogError = "Enter a source name.";
            return;
        }

        if (!Uri.TryCreate(sourceEditor.BaseUri.Trim(), UriKind.Absolute, out var baseUri))
        {
            sourceDialogError = "Enter an absolute HTTP or HTTPS instance URL.";
            return;
        }

        if (sourceEditor.ApiTokenSecretId == Guid.Empty)
        {
            sourceDialogError = "Select a stored source credential.";
            return;
        }

        isBusy = true;
        try
        {
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
        }
        catch (Exception exception)
        {
            sourceDialogError = exception.Message;
        }
        finally
        {
            isBusy = false;
        }
    }

    private async Task ToggleSourceAsync(SharedProviderSourceSnapshot source)
    {
        await RunSourceMutationAsync(
            async () =>
            {
                await ManagementService.SetSourceEnabledAsync(
                    source.Id,
                    source.ConcurrencyToken,
                    !source.IsEnabled);
            },
            source.IsEnabled ? "Source disabled" : "Source enabled");
    }

    private async Task DeleteSourceAsync(SharedProviderSourceSnapshot source)
    {
        await RunSourceMutationAsync(
            async () =>
            {
                await ManagementService.DeleteSourceAsync(
                    source.Id,
                    source.ConcurrencyToken);
            },
            "Source deleted");
    }

    private async Task RunSourceMutationAsync(Func<Task> mutation, string successTitle)
    {
        isBusy = true;
        try
        {
            await mutation();
            await LoadAsync();
            NotificationService.Success(successTitle, "Shared-provider source state was updated.");
        }
        catch (Exception exception)
        {
            NotificationService.Error("Source change failed", exception.Message);
            await LoadAsync();
        }
        finally
        {
            isBusy = false;
        }
    }

    private async Task TestSourceAsync(Guid sourceId)
    {
        var result = await RunSourceOperationAsync(
            () => ManagementService.TestSourceAsync(sourceId));
        if (result?.Outcome == SharedProviderSourceOperationOutcome.Succeeded)
        {
            NotificationService.Success(
                "Source connection passed",
                $"The catalog contains {result.Catalog!.Providers.Count} published provider(s).");
        }
    }

    private async Task DiscoverSourceAsync(SharedProviderSourceManagementSnapshot source)
    {
        var result = await RunSourceOperationAsync(
            () => ManagementService.TestSourceAsync(source.Source.Id));
        if (result?.Outcome != SharedProviderSourceOperationOutcome.Succeeded ||
            result.Catalog is null)
        {
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

    private async Task SynchronizeExistingAsync(SharedProviderSourceManagementSnapshot source)
    {
        var selected = source.Imports
            .Where(import => import.SelectionState == SharedProviderSelectionState.Selected)
            .Select(import => import.RemotePublicationId)
            .ToHashSet();
        var result = await RunSourceOperationAsync(
            () => ManagementService.SynchronizeSourceAsync(source.Source.Id, selected));
        if (result is not null &&
            result.Outcome is SharedProviderSourceOperationOutcome.Succeeded or
                SharedProviderSourceOperationOutcome.NotModified)
        {
            await ProvidersChanged.InvokeAsync();
            NotificationService.Success("Source synchronized", DescribeSourceOperation(result));
        }
    }

    private async Task ApplyCatalogSelectionAsync()
    {
        if (catalogSourceId == Guid.Empty)
        {
            return;
        }

        var result = await RunSourceOperationAsync(
            () => ManagementService.SynchronizeSourceAsync(
                catalogSourceId,
                selectedPublicationIds));
        if (result?.Outcome != SharedProviderSourceOperationOutcome.Succeeded)
        {
            return;
        }

        catalogDialogOpen = false;
        await ProvidersChanged.InvokeAsync();
        NotificationService.Success("Shared providers imported", DescribeSourceOperation(result));
    }

    private async Task<SharedProviderSourceOperationResult?> RunSourceOperationAsync(
        Func<Task<SharedProviderSourceOperationResult>> operation)
    {
        isBusy = true;
        try
        {
            var result = await operation();
            await LoadAsync();
            if (result.Outcome is SharedProviderSourceOperationOutcome.Succeeded or
                SharedProviderSourceOperationOutcome.NotModified)
            {
                return result;
            }

            NotificationService.Warning(
                "Shared-provider source is unavailable",
                result.Failure?.SanitizedMessage ?? FormatStatus(result.Outcome));
            return result;
        }
        catch (Exception exception)
        {
            NotificationService.Error("Shared-provider source failed", exception.Message);
            await LoadAsync();
            return null;
        }
        finally
        {
            isBusy = false;
        }
    }

    private void SetPublicationSelected(
        SharedProviderPublicationId publicationId,
        ChangeEventArgs args)
    {
        if (args.Value is true ||
            bool.TryParse(args.Value?.ToString(), out var isSelected) && isSelected)
        {
            selectedPublicationIds.Add(publicationId);
        }
        else
        {
            selectedPublicationIds.Remove(publicationId);
        }
    }

    private void CloseCatalogDialog()
    {
        catalogDialogOpen = false;
        catalogSourceId = Guid.Empty;
        catalogPublications = [];
        selectedPublicationIds.Clear();
    }

    private static string DescribeSourceOperation(SharedProviderSourceOperationResult result)
    {
        if (result.Outcome == SharedProviderSourceOperationOutcome.NotModified)
        {
            return "The remote catalog has not changed.";
        }

        return $"Updated {result.AffectedProviderProfileIds.Count} profile(s) and retired {result.RetiredProviderProfileIds.Count} profile(s).";
    }

    private static string ResolveSourceTone(SharedProviderSourceStatus status) => status switch
    {
        SharedProviderSourceStatus.Available => "success",
        SharedProviderSourceStatus.NeverSynchronized => "neutral",
        SharedProviderSourceStatus.SourceOffline => "warning",
        _ => "danger"
    };

    private static string ResolveHealthTone(SharedProviderHealthState state) => state switch
    {
        SharedProviderHealthState.Available => "success",
        SharedProviderHealthState.Degraded => "warning",
        _ => "danger"
    };

    private static string FormatStatus<T>(T value) where T : struct, Enum
    {
        var text = value.ToString();
        return string.Concat(text.Select((character, index) =>
            index > 0 && char.IsUpper(character)
                ? $" {char.ToLowerInvariant(character)}"
                : char.ToLowerInvariant(character).ToString()));
    }

    private sealed class SharedProviderSourceEditorModel
    {
        public Guid? Id { get; set; }

        public Guid? ExpectedConcurrencyToken { get; set; }

        public string Name { get; set; } = string.Empty;

        public string BaseUri { get; set; } = string.Empty;

        public Guid ApiTokenSecretId { get; set; }

        public bool IsEnabled { get; set; }

        public bool AllowInsecurePrivateNetwork { get; set; }
    }

    private enum ConfirmationAction
    {
        None,
        Unpublish,
        RetireImport
    }
}
