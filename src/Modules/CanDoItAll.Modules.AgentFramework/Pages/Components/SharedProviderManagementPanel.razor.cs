using CanDoItAll.Components.BaseLib;
using CanDoItAll.Modules.AgentFramework.ProviderManagement;
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
    public EventCallback ProvidersChanged { get; set; }

    private SharedProviderProfileSharingSnapshot? profileState;
    private Guid? loadedProviderProfileId;
    private string confirmationTitle = string.Empty;
    private string confirmationMessage = string.Empty;
    private string confirmationActionText = string.Empty;
    private ConfirmationAction confirmationAction;
    private bool isLoading;
    private bool isBusy;
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
            profileState = await LoadProfileSharingAsync();
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

    private enum ConfirmationAction
    {
        None,
        Unpublish,
        RetireImport
    }
}
