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

        var dismissed = await JS.InvokeAsync<bool>("CanDoItAll.browserState.isDatabaseStartupPromptDismissed");
        if (!dismissed)
        {
            databaseDialogStartupMode = true;
            databaseDialogOpen = true;
        }
    }

    private async Task OpenDatabaseDialogAsync()
    {
        databaseProfileMessage = null;
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
        databaseDialogOpen = false;
        databaseDialogStartupMode = false;
    }

    private async Task CloseDatabaseDialogAsync()
    {
        await DismissStartupPromptIfNeededAsync();
        databaseDialogOpen = false;
        databaseDialogStartupMode = false;
    }

    private async Task OpenDatabaseSettingsAsync()
    {
        await DismissStartupPromptIfNeededAsync();
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

        var result = await DatabaseProfileWorkspaceService.CreateManagedSqliteAndActivateAsync();
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
