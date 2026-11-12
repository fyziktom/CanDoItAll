using CanDoItAll.AppComponents.Navigation;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Workspace.ApiAccess;
using CanDoItAll.Modules.Workspace.ApiAccess.Contracts;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

namespace CanDoItAll.Modules.Workspace.Pages;

public partial class SettingsPage {
    private const string AgentProvidersRoute = "/agents?tab=providers";
    [SupplyParameterFromQuery(Name = SettingsUrlCodec.TabKey)] public string? TabQuery { get; set; }
    [SupplyParameterFromQuery(Name = SettingsUrlCodec.SecretKey)] public string? SecretQuery { get; set; }
    [SupplyParameterFromQuery(Name = SettingsUrlCodec.ProfileKey)] public string? ProfileQuery { get; set; }
    [SupplyParameterFromQuery(Name = SettingsUrlCodec.StorageKey)] public string? StorageQuery { get; set; }
    [SupplyParameterFromQuery(Name = SettingsUrlCodec.SearchKey)] public string? SearchQuery { get; set; }
    [SupplyParameterFromQuery(Name = SettingsUrlCodec.PageKey)] public string? PageQuery { get; set; }
    [Inject] public IWorkspaceDefaultsOwner DefaultsOwner { get; set; } = default!;
    [Inject] public IWorkspaceSecretsOwner SecretsOwner { get; set; } = default!;
    [Inject] public IWorkspaceFilesOwner FilesOwner { get; set; } = default!;
    [Inject] public IDatabaseSwitchNotificationService ProfileChanges { get; set; } = default!;
    [Inject] public NavigationManager Navigation { get; set; } = default!;
    [CascadingParameter] public Task<AuthenticationState>? AuthenticationState { get; set; }
    private UrlStateBinding<SettingsUrlState> url = default!;
    private WorkspaceDefaultsController defaults = default!;
    private WorkspaceSecretsController secrets = default!;
    private WorkspaceFilesController files = default!;
    private string apiStatusLabel = "Not loaded";
    private bool filesInitialized;
    private bool profileRetired;
    private bool redirected;
    private bool disposed;
    private Task<AuthenticationState>? previousAuthentication;

    private ApiPageQuery AccountQuery => new(url.State.Search ?? string.Empty, (url.State.Page - 1) * ApiPageQuery.PageSize);

    protected override void OnInitialized() {
        url = new(Navigation, SettingsUrlCodec.Instance, null, typeof(SettingsPage));
        defaults = new(DefaultsOwner);
        secrets = new(SecretsOwner);
        secrets.SelectionChanged += SecretSelectionChanged;
        files = new(FilesOwner);
        previousAuthentication = AuthenticationState;
        ProfileChanges.Changed += ProfileChanged;
    }

    protected override Task OnInitializedAsync() {
        var initial = url.Apply(canonicalize: RendererInfo.IsInteractive).Current;
        return initial.Tab == WorkspaceSection.Providers
            ? ApplyAsync(initial)
            : Task.WhenAll(defaults.RefreshAsync(), secrets.RefreshAsync(), ApplyAsync(initial));
    }

    protected override async Task OnParametersSetAsync() {
        if (!ReferenceEquals(previousAuthentication, AuthenticationState)) {
            RetireDatabaseState();
            previousAuthentication = AuthenticationState;
        }
        if (redirected) {
            return;
        }
        var change = url.Apply(canonicalize: RendererInfo.IsInteractive);
        if (change.IsChanged) {
            await ApplyAsync(change.Current);
        }
    }

    private Task ApplyAsync(SettingsUrlState current) {
        if (current.Tab == WorkspaceSection.Providers) {
            redirected = true;
            Navigation.NavigateTo(AgentProvidersRoute, replace: true);
            return Task.CompletedTask;
        }
        return Task.WhenAll(ActivateFilesAsync(current.Tab), ApplySecretAsync(current));
    }

    private Task SelectSectionAsync(WorkspaceSection selected) {
        if (selected == WorkspaceSection.Providers) {
            Navigation.NavigateTo(AgentProvidersRoute);
            return Task.CompletedTask;
        }
        var secretId = selected == WorkspaceSection.Secrets ? secrets.SelectedSecretId : null;
        url.Navigate(new SettingsUrlState(selected, SecretId: secretId), UrlHistoryMode.Push);
        return ApplyAsync(url.State);
    }

    private Task ApplySecretAsync(SettingsUrlState current) {
        if (current.Tab != WorkspaceSection.Secrets || !RendererInfo.IsInteractive || current.SecretId == secrets.SelectedSecretId) {
            return Task.CompletedTask;
        }
        if (current.SecretId is { } id) {
            return secrets.SelectAsync(id);
        }
        secrets.New();
        return Task.CompletedTask;
    }

    private void SecretSelectionChanged(WorkspaceSecretSelection selection) => _ = InvokeAsync(() => PublishRoute(WorkspaceSection.Secrets,
        url.State with { SecretId = selection.SecretId },
        selection.Cause == WorkspaceSelectionCause.Operator ? UrlHistoryMode.Push : UrlHistoryMode.Replace));

    private void ProfileSelectionChanged(SettingsRouteChange<Guid?> change)
        => PublishRoute(WorkspaceSection.DataSources, url.State with { ProfileId = change.Value }, change.Mode);

    private void StorageSelectionChanged(SettingsRouteChange<Guid?> change)
        => PublishRoute(WorkspaceSection.Storage, url.State with { StorageId = change.Value }, change.Mode);

    private void AccountQueryChanged(SettingsRouteChange<ApiPageQuery> change)
        => PublishRoute(WorkspaceSection.ApiAccess, url.State with { Search = change.Value.Search, Page = change.Value.Offset / ApiPageQuery.PageSize + 1 }, change.Mode);

    private void PublishRoute(WorkspaceSection owner, SettingsUrlState next, UrlHistoryMode mode) {
        if (disposed || redirected || url.State.Tab != owner) {
            return;
        }
        url.Navigate(next, mode);
    }

    private Task ActivateFilesAsync(WorkspaceSection tab) {
        if (tab != WorkspaceSection.Files || filesInitialized) {
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
        secrets.SelectionChanged -= SecretSelectionChanged;
        defaults.Dispose();
        secrets.Dispose();
        files.Dispose();
    }
}
