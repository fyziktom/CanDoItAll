using System.Text.Json;
using CanDoItAll.Application.UI.Profiles;
using CanDoItAll.Application.UI.Shell;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.SharedKernel;
using Microsoft.JSInterop;

namespace CanDoItAll.Web.Components.Layout;

public partial class MainLayout
{
    private async Task LoadDatabaseProfileUiAsync(bool showStartupPrompt)
    {
        EnsureLayoutCurrent();
        var generation = ++databaseProfileReadGeneration;
        var openingId = databaseDialogOpeningId;
        var profiles = await DatabaseProfileWorkspaceService.ListProfilesAsync(layoutLifetime.Token);
        EnsureLayoutCurrent();
        var selection = await DatabaseProfileWorkspaceService.GetCurrentSelectionAsync(layoutLifetime.Token);
        EnsureLayoutCurrent();
        var editor = await DatabaseProfileWorkspaceService.GetCurrentEditorAsync(layoutLifetime.Token);
        EnsureLayoutCurrent();
        if (generation != databaseProfileReadGeneration || openingId != databaseDialogOpeningId) {
            return;
        }
        databaseProfiles = profiles;
        databaseSelection = selection;
        currentDatabaseEditor = editor;
        selectedDatabaseProfileId ??= databaseSelection.RuntimeProfileId;
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
            !await JS.InvokeAsync<bool>("CanDoItAll.browserState.isDatabaseStartupPromptDismissed", layoutLifetime.Token))
        {
            EnsureLayoutCurrent();
            if (generation != databaseProfileReadGeneration || openingId != databaseDialogOpeningId) {
                return;
            }
            databaseDialogOpeningId = Guid.NewGuid();
            databaseDialogStartupMode = true;
            databaseDialogOpen = true;
        }
    }

    private static bool ShouldPromptForStartupDatabaseConfirmation(DatabaseSelectionStateModel? selection)
    {
        return selection?.ResolutionSource is
            DatabaseProfileResolutionSource.ExplicitOverride or
            DatabaseProfileResolutionSource.PersistedCatalogFallback or
            DatabaseProfileResolutionSource.AutoProvisionedPostgreSql;
    }

    private IReadOnlyList<DatabaseProfileSummary> RecentDatabaseProfiles => databaseProfiles
        .Where(profile => databaseSelection is null || profile.Id != databaseSelection.RuntimeProfileId)
        .OrderByDescending(profile => profile.LastUsedUtc ?? profile.CreatedUtc)
        .ThenBy(profile => profile.DisplayName, StringComparer.OrdinalIgnoreCase)
        .Take(2)
        .ToList();

    private bool IsDatabaseOpeningCurrent(Guid openingId)
        => IsLayoutCurrent && openingId == databaseDialogOpeningId;

    private Task OpenDatabaseDialogAsync(Guid openingId) => RunLayoutWorkAsync(nameof(OpenDatabaseDialogAsync), async () => {
        if (!IsDatabaseOpeningCurrent(openingId) || databaseDialogBusy) {
            return;
        }
        var requestedOpening = databaseDialogOpeningId = Guid.NewGuid();
        await LoadDatabaseProfileUiAsync(showStartupPrompt: false);
        if (!IsDatabaseOpeningCurrent(requestedOpening)) {
            return;
        }
        databaseProfileMessage = null;
        databaseActivationUncertain = false;
        databaseDialogStartupMode = false;
        databaseDialogOpen = true;
    });

    private Task SelectDatabaseProfileAsync(DatabaseProfileIntent intent) {
        if (IsDatabaseOpeningCurrent(intent.OpeningId) && databaseDialogOpen && !databaseDialogBusy &&
            databaseProfiles.Any(profile => profile.Id == intent.ProfileId)) {
            selectedDatabaseProfileId = intent.ProfileId;
            if (!databaseActivationUncertain) {
                databaseProfileMessage = null;
            }
        }
        return Task.CompletedTask;
    }

    private Task CloseDatabaseDialogAsync(Guid openingId)
        => LeaveDatabaseDialogAsync(openingId, openSettings: false);

    private Task OpenDatabaseSettingsAsync(Guid openingId)
        => LeaveDatabaseDialogAsync(openingId, openSettings: true);

    private Task LeaveDatabaseDialogAsync(Guid openingId, bool openSettings) => RunLayoutWorkAsync(nameof(LeaveDatabaseDialogAsync), async () => {
        if (!IsDatabaseOpeningCurrent(openingId) || !databaseDialogOpen) {
            return;
        }
        var closingOpening = databaseDialogOpeningId = Guid.NewGuid();
        await DismissStartupPromptIfNeededAsync();
        if (!IsDatabaseOpeningCurrent(closingOpening)) {
            return;
        }
        databaseDialogOpen = false;
        databaseDialogStartupMode = false;
        if (openSettings) {
            Navigation.NavigateTo("/settings?tab=data-sources");
        }
    });

    private Task SwitchDatabaseProfileAsync(DatabaseProfileIntent intent)
        => ActivateDatabaseProfileAsync(intent, fromFlyout: false);

    private Task SwitchDatabaseProfileFromFlyoutAsync(DatabaseProfileIntent intent) {
        if (!IsDatabaseOpeningCurrent(intent.OpeningId) || !CanManageDatabases || databaseDialogBusy ||
            !RecentDatabaseProfiles.Any(profile => profile.Id == intent.ProfileId)) {
            return Task.CompletedTask;
        }
        selectedDatabaseProfileId = intent.ProfileId;
        return ActivateDatabaseProfileAsync(intent, fromFlyout: true);
    }

    private Task ActivateDatabaseProfileAsync(DatabaseProfileIntent intent, bool fromFlyout)
        => RunLayoutWorkAsync(nameof(ActivateDatabaseProfileAsync), async () => {
            if (!IsDatabaseOpeningCurrent(intent.OpeningId) || databaseDialogBusy ||
                (!fromFlyout && !databaseDialogOpen) || selectedDatabaseProfileId != intent.ProfileId ||
                !CanSwitchSelectedProfile) {
                return;
            }
            var selectedProfile = databaseProfiles.SingleOrDefault(profile => profile.Id == intent.ProfileId);
            if (selectedProfile is null) {
                return;
            }
            databaseDialogBusy = true;
            databaseProfileMessage = null;
            try {
                var result = await DatabaseProfileWorkspaceService.ActivateProfileAsync(intent.ProfileId);
                if (!IsDatabaseOpeningCurrent(intent.OpeningId)) {
                    return;
                }
                if (result.IsFailure) {
                    databaseProfileMessage = new(DescribeErrors(result.Errors), DatabaseProfileMessageTone.Error);
                    return;
                }
                var activation = result.Value ?? throw new InvalidOperationException("Database profile activation returned no outcome.");
                if (activation.RequiresRestart) {
                    var pendingId = activation.PendingRestartProfileId ?? throw new InvalidOperationException("Restart activation returned no pending profile identity.");
                    if (databaseSelection is not null) {
                        databaseSelection.PendingRestartProfileId = pendingId;
                        databaseSelection.PendingRestartDisplayName = selectedProfile.DisplayName;
                        databaseSelection.PendingRestartDescriptor = selectedProfile.Descriptor;
                    }
                    databaseProfiles = databaseProfiles.Select(profile => profile with {
                        IsPendingRestartActivation = profile.Id == pendingId
                    }).ToArray();
                    databaseProfileMessage = new(string.IsNullOrWhiteSpace(activation.Message)
                        ? "Database profile activation was saved. Restart the process to make it the canonical runtime database."
                        : activation.Message, DatabaseProfileMessageTone.RestartRequired);
                    databaseDialogOpen = true;
                    databaseDialogStartupMode = false;
                    return;
                }
                await CloseDatabaseDialogAsync(intent.OpeningId);
            } catch (Exception failure) {
                CollaborationLogger.LogError("Database profile activation has an unconfirmed outcome. ProfileId={ProfileId}; OpeningId={OpeningId}; FailureType={FailureType}.",
                    intent.ProfileId, intent.OpeningId, failure.GetType().Name);
                if (IsDatabaseOpeningCurrent(intent.OpeningId)) {
                    databaseActivationUncertain = true;
                    databaseProfileMessage = new("The activation outcome could not be confirmed. Close and reopen Database profiles to read the saved selection before trying again.",
                        DatabaseProfileMessageTone.Error);
                }
            } finally {
                databaseDialogBusy = false;
            }
        });

    private async Task DismissStartupPromptIfNeededAsync()
    {
        EnsureLayoutCurrent();
        if (!databaseDialogStartupMode)
        {
            return;
        }

        await JS.InvokeVoidAsync("CanDoItAll.browserState.dismissDatabaseStartupPrompt", layoutLifetime.Token);
    }

    [JSInvokable]
    public Task HandleBrowserDatabaseSwitchAsync(string payload)
    {
        if (!IsLayoutCurrent) {
            return Task.CompletedTask;
        }
        var browserMessage = DeserializeDatabaseSwitchMessage(payload);
        if (browserMessage is null || browserMessage.Generation <= lastObservedDatabaseSwitchGeneration)
        {
            return Task.CompletedTask;
        }

        return RunLayoutWorkAsync(nameof(HandleBrowserDatabaseSwitchAsync), () => HandleDatabaseSwitchAsync(browserMessage, publishToBrowser: false));
    }

    private DatabaseProfileDialogPresentation DatabaseDialogPresentation => new(databaseDialogOpeningId,
        databaseDialogOpen, databaseDialogStartupMode,
        databaseSelection is { } selection ? new DatabaseSelectionDisplay(selection.DisplayName,
            DescribeDatabaseProvider(selection.ProviderKind), DescribeDatabaseSource(selection.SourceKind),
            DescribeResolutionSource(selection.ResolutionSource), BuildSafeDatabaseDescriptor(selection), selection.WorkspaceRoot,
            selection.IsRuntimeLocked, selection.PendingRestartProfileId, selection.PendingRestartDisplayName) : null,
        databaseProfiles.Select(ToProfileDisplay).ToArray(), selectedDatabaseProfileId, databaseDialogBusy,
        SelectedDialogProfileIsActive, SelectedDialogProfileIsPendingRestart, CanSwitchSelectedProfile, databaseProfileMessage);

    private ShellUtilitiesPresentation UtilitiesPresentation => new(databaseDialogOpeningId,
        databaseSelection?.DisplayName ?? "Loading runtime profile", BuildSafeDatabaseDetails(databaseSelection, currentDatabaseEditor),
        BuildSafeDatabaseSummary(databaseSelection, currentDatabaseEditor), RecentDatabaseProfiles.Select(ToProfileDisplay).ToArray(),
        databaseDialogBusy, CanManageDatabases);

    private static DatabaseProfileDisplay ToProfileDisplay(DatabaseProfileSummary profile) => new(profile.Id, profile.DisplayName,
        DescribeDatabaseProvider(profile.ProviderKind), DescribeDatabaseSource(profile.SourceKind), profile.Descriptor,
        profile.IsActive, profile.IsPendingRestartActivation, profile.IsRuntimeLocked);

    private static string DescribeErrors(IReadOnlyList<Error> errors)
    {
        return string.Join(" ", errors.Select(error => error.Message));
    }

    private static string DescribeDatabaseProvider(DatabaseProviderKind providerKind)
    {
        return providerKind switch
        {
            DatabaseProviderKind.PostgreSql => "PostgreSQL",
            DatabaseProviderKind.InMemory => "In-memory",
            _ => providerKind.ToString()
        };
    }

    private static string DescribeDatabaseSource(DatabaseProfileSourceKind sourceKind)
    {
        return sourceKind switch
        {
            DatabaseProfileSourceKind.PostgresConnection => "PostgreSQL connection",
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
                $"Runtime: {(selection.IsRuntimeLocked ? "Config locked" : "Restart required for profile changes")}"
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
        => _ = RunLayoutWorkAsync(nameof(HandleDatabaseSwitchChanged), () => InvokeAsync(() => HandleDatabaseSwitchAsync(
            new BrowserDatabaseSwitchMessage(
                notification.CurrentProfileId,
                notification.CurrentFingerprint,
                notification.Generation,
                Workbench.HasDirtyTabs),
            publishToBrowser: true)));

    private async Task HandleDatabaseSwitchAsync(BrowserDatabaseSwitchMessage browserMessage, bool publishToBrowser)
    {
        EnsureLayoutCurrent();
        if (browserMessage.Generation <= lastObservedDatabaseSwitchGeneration)
        {
            return;
        }

        lastObservedDatabaseSwitchGeneration = browserMessage.Generation;
        databaseDialogOpeningId = Guid.NewGuid();
        databaseDialogOpen = false;
        databaseDialogStartupMode = false;
        databaseProfileMessage = null;
        var payload = JsonSerializer.Serialize(browserMessage);
        if (publishToBrowser)
        {
            await JS.InvokeVoidAsync("CanDoItAll.browserState.publishDatabaseSwitch", layoutLifetime.Token, payload);
            EnsureLayoutCurrent();
        }

        await JS.InvokeVoidAsync("CanDoItAll.browserState.rememberDatabaseSwitchAlert", layoutLifetime.Token, payload);
        EnsureLayoutCurrent();
        Navigation.NavigateTo(ResolveDatabaseSwitchRecoveryRoute(), forceLoad: true);
    }

    private async Task RestoreDatabaseSwitchAlertAsync()
    {
        var payload = await JS.InvokeAsync<string?>("CanDoItAll.browserState.consumeDatabaseSwitchAlert", layoutLifetime.Token);
        EnsureLayoutCurrent();
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

    private async Task LoadCollaborationShellStateAsync() {
        var generation = ++collaborationReadGeneration;
        try {
            var unreadCount = (await CollaborationService.GetShellStateAsync()).UnreadCount;
            if (collaborationDisposed || generation != collaborationReadGeneration || collaborationUnreadCount == unreadCount) {
                return;
            }

            collaborationUnreadCount = unreadCount;
            await InvokeAsync(StateHasChanged);
        } catch (Exception exception) {
            CollaborationLogger.LogWarning(exception, "Collaboration shell badge refresh failed at generation {Generation}; retaining the accepted count.", generation);
        }
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
            "quality" => "/test-lab",
            "operations" => "/scheduler",
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
