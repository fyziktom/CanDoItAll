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
        currentDatabaseEditor = await DatabaseProfileWorkspaceService.GetCurrentEditorAsync();
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
            DatabaseProfileResolutionSource.AutoProvisionedPostgreSql;
    }

    private IReadOnlyList<DatabaseProfileSummary> RecentDatabaseProfiles => databaseProfiles
        .Where(profile => databaseSelection is null || profile.Id != databaseSelection.ActiveProfileId)
        .OrderByDescending(profile => profile.LastUsedUtc ?? profile.CreatedUtc)
        .ThenBy(profile => profile.DisplayName, StringComparer.OrdinalIgnoreCase)
        .Take(2)
        .ToList();

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

    private async Task SwitchDatabaseProfileFromFlyoutAsync(Guid profileId)
    {
        if (!CanManageDatabases || databaseDialogBusy)
        {
            return;
        }

        selectedDatabaseProfileId = profileId;
        await SwitchDatabaseProfileAsync();
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
            DatabaseProviderKind.Sqlite => "Unsupported legacy SQLite",
            DatabaseProviderKind.PostgreSql => "PostgreSQL",
            DatabaseProviderKind.InMemory => "In-memory",
            _ => providerKind.ToString()
        };
    }

    private static string DescribeDatabaseSource(DatabaseProfileSourceKind sourceKind)
    {
        return sourceKind switch
        {
            DatabaseProfileSourceKind.ManagedSqlite => "Unsupported legacy managed SQLite",
            DatabaseProfileSourceKind.ExternalSqliteFile => "Unsupported legacy SQLite file",
            DatabaseProfileSourceKind.ImportedSqlite => "Unsupported legacy imported SQLite",
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
            DatabaseProfileResolutionSource.AutoProvisionedPostgreSql => "Auto-provisioned PostgreSQL",
            _ => resolutionSource.ToString()
        };
    }

    private static string BuildSafeDatabaseDescriptor(DatabaseSelectionStateModel? selection)
    {
        if (selection is null)
        {
            return "Runtime selection is loading.";
        }

        if (string.IsNullOrWhiteSpace(selection.Descriptor))
        {
            return DescribeDatabaseProvider(selection.ProviderKind);
        }

        return selection.ProviderKind switch
        {
            DatabaseProviderKind.Sqlite => "Unsupported legacy SQLite profile",
            DatabaseProviderKind.PostgreSql => selection.Descriptor,
            DatabaseProviderKind.InMemory => selection.Descriptor,
            _ => DescribeDatabaseProvider(selection.ProviderKind)
        };
    }

    private static DatabaseFlyoutDetails BuildSafeDatabaseDetails(
        DatabaseSelectionStateModel? selection,
        DatabaseProfileEditorModel? editor)
    {
        if (selection is null)
        {
            return new DatabaseFlyoutDetails("Loading", "Loading", "Loading");
        }

        return selection.ProviderKind switch
        {
            DatabaseProviderKind.PostgreSql => new DatabaseFlyoutDetails(
                MaskHostName(editor?.PostgresHost),
                MaskIdentifier(editor?.PostgresDatabaseName),
                string.IsNullOrWhiteSpace(editor?.PostgresUsername) ? "Unavailable" : editor.PostgresUsername.Trim()),
            DatabaseProviderKind.Sqlite => new DatabaseFlyoutDetails(
                "Unsupported legacy provider",
                "Migration required",
                "Create PostgreSQL profile"),
            DatabaseProviderKind.InMemory => new DatabaseFlyoutDetails(
                "Process memory",
                BuildSafeDatabaseDescriptor(selection),
                "In-memory"),
            _ => new DatabaseFlyoutDetails(
                DescribeDatabaseProvider(selection.ProviderKind),
                BuildSafeDatabaseDescriptor(selection),
                DescribeDatabaseSource(selection.SourceKind))
        };
    }

    private static string BuildSafeDatabaseSummary(
        DatabaseSelectionStateModel? selection,
        DatabaseProfileEditorModel? editor)
    {
        if (selection is null)
        {
            return "Database profile: loading";
        }

        var details = BuildSafeDatabaseDetails(selection, editor);
        return string.Join(
            Environment.NewLine,
            [
                $"Profile: {selection.DisplayName}",
                $"Provider: {DescribeDatabaseProvider(selection.ProviderKind)}",
                $"Server: {details.Server}",
                $"Database: {details.Database}",
                $"User: {details.User}",
                $"Source: {DescribeDatabaseSource(selection.SourceKind)}",
                $"Resolution: {DescribeResolutionSource(selection.ResolutionSource)}",
                $"Runtime: {(selection.IsRuntimeLocked ? "Config locked" : "Switchable")}"
            ]);
    }

    private static string MaskHostName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "Unavailable";
        }

        var trimmed = value.Trim();
        var separatorIndex = trimmed.IndexOf('.');
        if (separatorIndex <= 0)
        {
            return MaskIdentifier(trimmed);
        }

        return $"{MaskIdentifier(trimmed[..separatorIndex])}{trimmed[separatorIndex..]}";
    }

    private static string MaskIdentifier(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "Unavailable";
        }

        var trimmed = value.Trim();
        if (trimmed.Length <= 4)
        {
            return trimmed;
        }

        var visiblePrefixLength = Math.Min(4, Math.Max(2, trimmed.Length / 3));
        var maskLength = Math.Min(10, Math.Max(3, trimmed.Length - visiblePrefixLength));
        return $"{trimmed[..visiblePrefixLength]}{new string('*', maskLength)}";
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
        var unreadCount = (await CollaborationService.GetShellStateAsync()).UnreadCount;
        if (collaborationUnreadCount == unreadCount)
        {
            return;
        }

        collaborationUnreadCount = unreadCount;
        await InvokeAsync(StateHasChanged);
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

    private sealed record DatabaseFlyoutDetails(
        string Server,
        string Database,
        string User);
}
