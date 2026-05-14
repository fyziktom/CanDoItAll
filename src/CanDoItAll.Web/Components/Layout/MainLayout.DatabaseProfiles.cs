using System.Text.Json;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.SharedKernel;
using Microsoft.JSInterop;

namespace CanDoItAll.Web.Components.Layout;

public partial class MainLayout
{
    private async Task LoadDatabaseProfileUiAsync(bool showStartupPrompt)
    {
        databaseProfiles = await DatabaseProfileWorkspaceService.ListProfilesAsync();
        databaseSelection = await DatabaseProfileWorkspaceService.GetCurrentSelectionAsync();
        selectedDatabaseProfileId ??= databaseSelection.ActiveProfileId;
        if (databaseProfiles.Count > 0 &&
            !databaseProfiles.Any(profile => profile.Id == selectedDatabaseProfileId))
        {
            selectedDatabaseProfileId = databaseProfiles[0].Id;
        }

        if (!showStartupPrompt)
        {
            return;
        }

        if (ShouldPromptForStartupDatabaseConfirmation(databaseSelection) &&
            !await JS.InvokeAsync<bool>("CanDoItAll.browserState.isDatabaseStartupPromptDismissed"))
        {
            databaseDialogStartupMode = true;
            databaseDialogOpen = true;
        }
    }

    private static bool ShouldPromptForStartupDatabaseConfirmation(DatabaseSelectionStateModel? selection)
    {
        return selection?.ResolutionSource is
            DatabaseProfileResolutionSource.ExplicitOverride or
            DatabaseProfileResolutionSource.PersistedCatalogFallback or
            DatabaseProfileResolutionSource.LegacyDiscovery or
            DatabaseProfileResolutionSource.AutoProvisionedManagedSqlite;
    }

    private async Task OpenDatabaseDialogAsync()
    {
        databaseProfileMessage = null;
        ResetCreatedDatabaseTransferPrompt();
        databaseDialogStartupMode = false;
        await LoadDatabaseProfileUiAsync(showStartupPrompt: false);
        databaseDialogOpen = true;
    }

    private Task SelectDatabaseProfileAsync(Guid profileId)
    {
        selectedDatabaseProfileId = profileId;
        databaseProfileMessage = null;
        return Task.CompletedTask;
    }

    private async Task ContinueWithCurrentDatabaseAsync()
    {
        await DismissStartupPromptIfNeededAsync();
        ResetCreatedDatabaseTransferPrompt();
        databaseDialogOpen = false;
        databaseDialogStartupMode = false;
    }

    private async Task CloseDatabaseDialogAsync()
    {
        await DismissStartupPromptIfNeededAsync();
        ResetCreatedDatabaseTransferPrompt();
        databaseDialogOpen = false;
        databaseDialogStartupMode = false;
    }

    private async Task OpenDatabaseSettingsAsync()
    {
        await DismissStartupPromptIfNeededAsync();
        ResetCreatedDatabaseTransferPrompt();
        databaseDialogOpen = false;
        databaseDialogStartupMode = false;
        Navigation.NavigateTo("/settings?tab=data-sources");
    }

    private async Task CreateManagedSqliteAsync()
    {
        if (!CanManageDatabases)
        {
            return;
        }

        databaseDialogBusy = true;
        databaseProfileMessage = null;
        ResetCreatedDatabaseTransferPrompt();

        var saveResult = await DatabaseProfileWorkspaceService.SaveProfileAsync(new DatabaseProfileEditorModel
        {
            DisplayName = "Managed SQLite workspace",
            ProviderKind = DatabaseProviderKind.Sqlite,
            SourceKind = DatabaseProfileSourceKind.ManagedSqlite
        });
        if (saveResult.IsFailure)
        {
            databaseDialogBusy = false;
            databaseProfileMessage = DescribeErrors(saveResult.Errors);
            return;
        }

        var createResult = await DatabaseProfileWorkspaceService.CreateEmptyAsync(saveResult.Value);
        databaseDialogBusy = false;
        if (createResult.IsFailure)
        {
            databaseProfileMessage = DescribeErrors(createResult.Errors);
            return;
        }

        pendingCreatedDatabaseProfileId = saveResult.Value;
        selectedDatabaseProfileId = saveResult.Value;
        await LoadDatabaseProfileUiAsync(showStartupPrompt: false);
        selectedDatabaseProfileId = saveResult.Value;
        pendingCreatedDatabaseName = databaseProfiles.FirstOrDefault(profile => profile.Id == saveResult.Value)?.DisplayName
            ?? "Managed SQLite workspace";
        databaseProfileMessage = "Managed SQLite database created. Choose baseline settings to transfer, or skip transfer.";
        await LoadCreatedDatabaseTransferPromptAsync(saveResult.Value);
    }

    private async Task SelectCreatedDatabaseTransferSourceAsync(Guid sourceProfileId)
    {
        createdDatabaseTransferSourceProfileId = sourceProfileId;
        await RefreshCreatedDatabaseTransferPreviewAsync(selectAvailableItems: true);
    }

    private Task ToggleCreatedDatabaseTransferItemAsync(string itemKey)
    {
        if (!createdDatabaseSelectedTransferItemKeys.Add(itemKey))
        {
            createdDatabaseSelectedTransferItemKeys.Remove(itemKey);
        }

        return Task.CompletedTask;
    }

    private async Task TransferCreatedDatabaseSettingsAsync()
    {
        if (!pendingCreatedDatabaseProfileId.HasValue || !createdDatabaseTransferSourceProfileId.HasValue)
        {
            databaseProfileMessage = "Select a source database before transferring baseline settings.";
            return;
        }

        createdDatabaseTransferBusy = true;
        databaseDialogBusy = true;
        databaseProfileMessage = null;
        var result = await DatabaseProfileWorkspaceService.TransferSettingsAsync(new DatabaseTransferRequest
        {
            SourceProfileId = createdDatabaseTransferSourceProfileId.Value,
            TargetProfileId = pendingCreatedDatabaseProfileId.Value,
            ItemKeys = createdDatabaseSelectedTransferItemKeys.ToList(),
            ReplaceExisting = true
        });
        createdDatabaseTransferBusy = false;
        databaseDialogBusy = false;

        if (!result.IsSuccess)
        {
            databaseProfileMessage = string.Join(" ", result.Items.Select(item => $"{item.Label}: {item.Message}"));
            return;
        }

        await ActivatePendingCreatedDatabaseAsync();
    }

    private Task SkipCreatedDatabaseTransferAsync()
    {
        return ActivatePendingCreatedDatabaseAsync();
    }

    private async Task ActivatePendingCreatedDatabaseAsync()
    {
        if (!pendingCreatedDatabaseProfileId.HasValue)
        {
            return;
        }

        var profileId = pendingCreatedDatabaseProfileId.Value;
        databaseDialogBusy = true;
        databaseProfileMessage = null;
        var result = await DatabaseProfileWorkspaceService.ActivateProfileAsync(profileId);
        databaseDialogBusy = false;
        if (result.IsFailure)
        {
            databaseProfileMessage = DescribeErrors(result.Errors);
            return;
        }

        ResetCreatedDatabaseTransferPrompt();
        selectedDatabaseProfileId = profileId;
        await DismissStartupPromptIfNeededAsync();
        databaseDialogOpen = false;
        databaseDialogStartupMode = false;
    }

    private async Task LoadCreatedDatabaseTransferPromptAsync(Guid targetProfileId)
    {
        createdDatabaseTransferBusy = true;
        createdDatabaseTransferSources = [];
        createdDatabaseTransferItems = [];
        createdDatabaseSelectedTransferItemKeys.Clear();
        createdDatabaseTransferSourceProfileId = null;

        try
        {
            createdDatabaseTransferSources = await DatabaseProfileWorkspaceService.ListTransferSourcesAsync(targetProfileId);
            createdDatabaseTransferSourceProfileId = ResolveCreatedDatabaseDefaultTransferSource(targetProfileId);
        }
        catch (Exception ex)
        {
            databaseProfileMessage = $"Managed SQLite database created, but transfer sources could not be loaded: {ex.Message}";
        }
        finally
        {
            createdDatabaseTransferBusy = false;
        }

        if (createdDatabaseTransferSourceProfileId.HasValue)
        {
            await RefreshCreatedDatabaseTransferPreviewAsync(selectAvailableItems: true);
        }
    }

    private Guid? ResolveCreatedDatabaseDefaultTransferSource(Guid targetProfileId)
    {
        if (databaseSelection is not null &&
            databaseSelection.ActiveProfileId != targetProfileId &&
            createdDatabaseTransferSources.Any(source => source.ProfileId == databaseSelection.ActiveProfileId))
        {
            return databaseSelection.ActiveProfileId;
        }

        return createdDatabaseTransferSources.FirstOrDefault()?.ProfileId;
    }

    private async Task RefreshCreatedDatabaseTransferPreviewAsync(bool selectAvailableItems)
    {
        if (!pendingCreatedDatabaseProfileId.HasValue || !createdDatabaseTransferSourceProfileId.HasValue)
        {
            createdDatabaseTransferItems = [];
            createdDatabaseSelectedTransferItemKeys.Clear();
            return;
        }

        createdDatabaseTransferBusy = true;
        try
        {
            createdDatabaseTransferItems = await DatabaseProfileWorkspaceService.PreviewTransferAsync(
                createdDatabaseTransferSourceProfileId.Value,
                pendingCreatedDatabaseProfileId.Value);

            if (selectAvailableItems)
            {
                createdDatabaseSelectedTransferItemKeys.Clear();
                foreach (var item in createdDatabaseTransferItems.Where(item => item.IsAvailable))
                {
                    createdDatabaseSelectedTransferItemKeys.Add(item.Descriptor.Key);
                }
            }
            else
            {
                createdDatabaseSelectedTransferItemKeys.IntersectWith(createdDatabaseTransferItems.Select(item => item.Descriptor.Key));
            }
        }
        catch (Exception ex)
        {
            createdDatabaseTransferItems = [];
            createdDatabaseSelectedTransferItemKeys.Clear();
            databaseProfileMessage = $"Could not preview baseline settings: {ex.Message}";
        }
        finally
        {
            createdDatabaseTransferBusy = false;
        }
    }

    private void ResetCreatedDatabaseTransferPrompt()
    {
        pendingCreatedDatabaseProfileId = null;
        pendingCreatedDatabaseName = string.Empty;
        createdDatabaseTransferSources = [];
        createdDatabaseTransferItems = [];
        createdDatabaseSelectedTransferItemKeys.Clear();
        createdDatabaseTransferSourceProfileId = null;
        createdDatabaseTransferBusy = false;
    }

    private async Task SwitchDatabaseProfileAsync()
    {
        if (!selectedDatabaseProfileId.HasValue || !CanSwitchSelectedProfile)
        {
            return;
        }

        databaseDialogBusy = true;
        databaseProfileMessage = null;

        var result = await DatabaseProfileWorkspaceService.ActivateProfileAsync(selectedDatabaseProfileId.Value);
        databaseDialogBusy = false;
        if (result.IsFailure)
        {
            databaseProfileMessage = DescribeErrors(result.Errors);
            return;
        }

        await DismissStartupPromptIfNeededAsync();
        databaseDialogOpen = false;
        databaseDialogStartupMode = false;
    }

    private async Task DismissStartupPromptIfNeededAsync()
    {
        if (!databaseDialogStartupMode)
        {
            return;
        }

        await JS.InvokeVoidAsync("CanDoItAll.browserState.dismissDatabaseStartupPrompt");
    }

    [JSInvokable]
    public Task HandleBrowserDatabaseSwitchAsync(string payload)
    {
        var browserMessage = DeserializeDatabaseSwitchMessage(payload);
        if (browserMessage is null || browserMessage.Generation <= lastObservedDatabaseSwitchGeneration)
        {
            return Task.CompletedTask;
        }

        return HandleDatabaseSwitchAsync(browserMessage, publishToBrowser: false);
    }

    private string ResolveDatabaseMessageClass()
    {
        var toneClass = databaseProfileMessage?.StartsWith("Database activation requested", StringComparison.OrdinalIgnoreCase) == true
            ? "border-sky-200 bg-sky-50 text-sky-950"
            : "border-rose-200 bg-rose-50 text-rose-950";
        return $"rounded-[1.35rem] border px-4 py-3 text-sm leading-6 {toneClass}";
    }

    private static string DescribeErrors(IReadOnlyList<Error> errors)
    {
        return string.Join(" ", errors.Select(error => error.Message));
    }

    private static string DescribeDatabaseProvider(DatabaseProviderKind providerKind)
    {
        return providerKind switch
        {
            DatabaseProviderKind.Sqlite => "SQLite",
            DatabaseProviderKind.PostgreSql => "PostgreSQL",
            DatabaseProviderKind.InMemory => "In-memory",
            _ => providerKind.ToString()
        };
    }

    private static string DescribeDatabaseSource(DatabaseProfileSourceKind sourceKind)
    {
        return sourceKind switch
        {
            DatabaseProfileSourceKind.ManagedSqlite => "Managed SQLite",
            DatabaseProfileSourceKind.ExternalSqliteFile => "External SQLite file",
            DatabaseProfileSourceKind.ImportedSqlite => "Imported SQLite file",
            DatabaseProfileSourceKind.PostgresConnection => "PostgreSQL connection",
            DatabaseProfileSourceKind.SnapshotCache => "Snapshot cache",
            DatabaseProfileSourceKind.IpfsSnapshot => "IPFS snapshot",
            DatabaseProfileSourceKind.InMemory => "In-memory",
            _ => sourceKind.ToString()
        };
    }

    private static string DescribeResolutionSource(DatabaseProfileResolutionSource resolutionSource)
    {
        return resolutionSource switch
        {
            DatabaseProfileResolutionSource.ExplicitOverride => "Explicit startup override",
            DatabaseProfileResolutionSource.PersistedActiveProfile => "Last active profile",
            DatabaseProfileResolutionSource.PersistedCatalogFallback => "Persisted catalog fallback",
            DatabaseProfileResolutionSource.LegacyDiscovery => "Legacy discovery",
            DatabaseProfileResolutionSource.AutoProvisionedManagedSqlite => "Auto-provisioned managed SQLite",
            _ => resolutionSource.ToString()
        };
    }

    private void HandleDatabaseSwitchChanged(object? sender, DatabaseProfileChangedNotification notification)
        => _ = InvokeAsync(() => HandleDatabaseSwitchAsync(
            new BrowserDatabaseSwitchMessage(
                notification.CurrentProfileId,
                notification.CurrentFingerprint,
                notification.Generation,
                Workbench.HasDirtyTabs),
            publishToBrowser: true));

    private async Task HandleDatabaseSwitchAsync(BrowserDatabaseSwitchMessage browserMessage, bool publishToBrowser)
    {
        if (browserMessage.Generation <= lastObservedDatabaseSwitchGeneration)
        {
            return;
        }

        lastObservedDatabaseSwitchGeneration = browserMessage.Generation;
        ResetCreatedDatabaseTransferPrompt();
        databaseDialogOpen = false;
        databaseDialogStartupMode = false;
        databaseProfileMessage = null;
        var payload = JsonSerializer.Serialize(browserMessage);
        if (publishToBrowser)
        {
            await JS.InvokeVoidAsync("CanDoItAll.browserState.publishDatabaseSwitch", payload);
        }

        await JS.InvokeVoidAsync("CanDoItAll.browserState.rememberDatabaseSwitchAlert", payload);
        Navigation.NavigateTo(ResolveDatabaseSwitchRecoveryRoute(), forceLoad: true);
    }

    private async Task RestoreDatabaseSwitchAlertAsync()
    {
        var payload = await JS.InvokeAsync<string?>("CanDoItAll.browserState.consumeDatabaseSwitchAlert");
        var browserMessage = DeserializeDatabaseSwitchMessage(payload);
        if (browserMessage is null)
        {
            return;
        }

        lastObservedDatabaseSwitchGeneration = Math.Max(lastObservedDatabaseSwitchGeneration, browserMessage.Generation);
        databaseSwitchAlert = browserMessage.HadDirtyTabs
            ? "The active database changed and the workbench reloaded a safe route. Unsaved tab state from the previous profile was discarded."
            : "The active database changed and the workbench reloaded a safe route for the current profile.";
    }

    private async Task LoadCollaborationShellStateAsync()
    {
        collaborationUnreadCount = (await CollaborationService.GetShellStateAsync()).UnreadCount;
    }

    private string ResolveDatabaseSwitchRecoveryRoute()
    {
        if (CurrentUri.AbsolutePath.EndsWith("/structure", StringComparison.OrdinalIgnoreCase) ||
            CurrentUri.AbsolutePath.EndsWith("/calendar", StringComparison.OrdinalIgnoreCase))
        {
            return "/projects";
        }

        return activeWorkspaceId switch
        {
            "quality" => "/validation",
            "automation" => "/automation",
            _ => "/projects"
        };
    }

    private static BrowserDatabaseSwitchMessage? DeserializeDatabaseSwitchMessage(string? payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return null;
        }

        return JsonSerializer.Deserialize<BrowserDatabaseSwitchMessage>(payload);
    }

    private sealed record BrowserDatabaseSwitchMessage(
        Guid CurrentProfileId,
        string CurrentFingerprint,
        long Generation,
        bool HadDirtyTabs);
}
