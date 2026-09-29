using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Workspace.ApiAccess;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

namespace CanDoItAll.Modules.Workspace.Pages;

public partial class SettingsPage {
    [SupplyParameterFromQuery(Name = "tab")] public string? RequestedTab { get; set; }
    [Inject] public IWorkspaceDefaultsOwner DefaultsOwner { get; set; } = default!;
    [Inject] public IWorkspaceSecretsOwner SecretsOwner { get; set; } = default!;
    [Inject] public IWorkspaceFilesOwner FilesOwner { get; set; } = default!;
    [Inject] public IDatabaseSwitchNotificationService ProfileChanges { get; set; } = default!;
    [Inject] public NavigationManager Navigation { get; set; } = default!;
    [CascadingParameter] public Task<AuthenticationState>? AuthenticationState { get; set; }
    private WorkspaceDefaultsController defaults = default!;
    private WorkspaceSecretsController secrets = default!;
    private WorkspaceFilesController files = default!;
    private WorkspaceSection section;
    private string apiStatusLabel = "Not loaded";
    private bool filesInitialized;
    private bool profileRetired;
    private bool disposed;
    private Task<AuthenticationState>? previousAuthentication;

    protected override async Task OnInitializedAsync() {
        defaults = new(DefaultsOwner);
        secrets = new(SecretsOwner);
        files = new(FilesOwner);
        previousAuthentication = AuthenticationState;
        ProfileChanges.Changed += ProfileChanged;
        ApplyRequestedTab();
        await Task.WhenAll(defaults.RefreshAsync(), secrets.RefreshAsync(), ActivateAsync());
    }

    protected override async Task OnParametersSetAsync() {
        if (!ReferenceEquals(previousAuthentication, AuthenticationState)) {
            RetireDatabaseState();
            previousAuthentication = AuthenticationState;
        }
        ApplyRequestedTab();
        await ActivateAsync();
    }

    private void ApplyRequestedTab() {
        section = RequestedTab switch {
            "data-sources" => WorkspaceSection.DataSources,
            "storage" => WorkspaceSection.Storage,
            "files" => WorkspaceSection.Files,
            "provider-history" => WorkspaceSection.ProviderHistory,
            "secrets" => WorkspaceSection.Secrets,
            "api-access" => WorkspaceSection.ApiAccess,
            "providers" => WorkspaceSection.Providers,
            _ => WorkspaceSection.Workspace
        };
        if (section == WorkspaceSection.Providers &&
            !string.Equals(Navigation.Uri, Navigation.ToAbsoluteUri("/agents?tab=providers").AbsoluteUri, StringComparison.Ordinal)) {
            Navigation.NavigateTo("/agents?tab=providers", replace: true);
        }
    }

    private async Task SelectSectionAsync(WorkspaceSection selected) {
        if (selected == WorkspaceSection.Providers) {
            Navigation.NavigateTo("/agents?tab=providers", replace: true);
            return;
        }
        section = selected;
        var token = selected switch {
            WorkspaceSection.Workspace => null,
            WorkspaceSection.DataSources => "data-sources",
            WorkspaceSection.Storage => "storage",
            WorkspaceSection.Files => "files",
            WorkspaceSection.ProviderHistory => "provider-history",
            WorkspaceSection.Secrets => "secrets",
            WorkspaceSection.ApiAccess => "api-access",
            _ => throw new ArgumentOutOfRangeException(nameof(selected))
        };
        Navigation.NavigateTo(token is null ? "/settings" : $"/settings?tab={token}", replace: true);
        await ActivateAsync();
    }

    private Task ActivateAsync() {
        if (section != WorkspaceSection.Files || filesInitialized) {
            return Task.CompletedTask;
        }
        filesInitialized = true;
        return files.RefreshAsync();
    }

    private void ApiStatusChanged(ApiAccessStatus? status) => apiStatusLabel = status is null ? "Unavailable" : status.AuthorizationEnabled ? "JWT" : "Open";

    private void ProfileChanged(object? sender, DatabaseProfileChangedNotification notification) {
        if (!disposed) {
            _ = InvokeAsync(() => {
                if (!disposed) {
                    RetireDatabaseState();
                    StateHasChanged();
                }
            });
        }
    }

    private void RetireDatabaseState() {
        defaults.Dispose();
        secrets.Dispose();
        profileRetired = true;
    }

    public void Dispose() {
        if (disposed) {
            return;
        }
        disposed = true;
        ProfileChanges.Changed -= ProfileChanged;
        defaults.Dispose();
        secrets.Dispose();
        files.Dispose();
    }
}
