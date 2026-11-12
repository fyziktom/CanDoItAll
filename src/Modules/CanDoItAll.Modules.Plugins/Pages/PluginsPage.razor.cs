using CanDoItAll.AppComponents.Navigation;
using CanDoItAll.Modules.Plugins.Presentation;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace CanDoItAll.Modules.Plugins.Pages;

public partial class PluginsPage : IPluginsWorkspaceRoute {
    [SupplyParameterFromQuery(Name = PluginsUrlCodec.PluginKey)] public string? PluginQuery { get; set; }
    [SupplyParameterFromQuery(Name = PluginsUrlCodec.SectionKey)] public string? SectionQuery { get; set; }
    [SupplyParameterFromQuery(Name = PluginsUrlCodec.LogsKey)] public string? LogsQuery { get; set; }
    [SupplyParameterFromQuery(Name = PluginsUrlCodec.DialogKey)] public string? DialogQuery { get; set; }
    [SupplyParameterFromQuery(Name = PluginsUrlCodec.OAuthKey)] public string? OAuthQuery { get; set; }
    [SupplyParameterFromQuery(Name = PluginsUrlCodec.ConnectionKey)] public string? ConnectionQuery { get; set; }
    [SupplyParameterFromQuery(Name = PluginsUrlCodec.ReasonKey)] public string? ReasonQuery { get; set; }
    [SupplyParameterFromQuery(Name = PluginsUrlCodec.LegacySectionKey)] public string? LegacySectionQuery { get; set; }
    [Inject] private PluginWorkspaceSession Session { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private IJSRuntime JsRuntime { get; set; } = default!;
    [Inject] private IAppNavigationHistory History { get; set; } = default!;
    private UrlStateBinding<PluginsUrlState> url = default!;
    private PluginsWorkspace? workspace;
    private PluginOAuthReturn? consumedOAuth;
    private bool disposed;

    protected override void OnInitialized() {
        url = new(Navigation, PluginsUrlCodec.Instance, History, typeof(PluginsPage));
        workspace = Session.CreateWorkspace(new Uri(Navigation.BaseUri),
            target => JsRuntime.InvokeVoidAsync("open", target, "_blank", "noopener,noreferrer").AsTask(), this);
        workspace.Changed += OnChanged;
    }

    protected override async Task OnParametersSetAsync() {
        if (workspace is null) {
            return;
        }
        var change = url.Apply(canonicalize: RendererInfo.IsInteractive);
        var oauth = RendererInfo.IsInteractive && (change.IsInitial || change.IsChanged) ? change.Current.OAuthReturn : null;
        if (change.IsInitial) {
            await workspace.OpenAsync(change.Current);
        } else if (change.IsChanged) {
            await workspace.ApplyRouteAsync(change.Current);
        }
        if (oauth is not null && oauth != consumedOAuth && !disposed) {
            consumedOAuth = oauth;
            workspace.ReportOAuthReturn(oauth);
        }
    }

    void IPluginsWorkspaceRoute.Navigate(PluginsUrlState state, UrlHistoryMode mode) {
        if (CanRoute) {
            url.Navigate(state, mode);
        }
    }

    void IPluginsWorkspaceRoute.OpenOverlay(PluginsUrlState state) {
        if (CanRoute) {
            url.OpenOverlay(state);
        }
    }

    void IPluginsWorkspaceRoute.CloseOverlay(PluginsUrlState parent) {
        if (CanRoute) {
            _ = CloseOverlayAsync(parent);
        }
    }

    private bool CanRoute => !disposed;

    private async Task CloseOverlayAsync(PluginsUrlState parent) {
        try {
            await url.CloseOverlayAsync(parent);
        } catch (Exception exception) {
            Session.ReportFailure("packages dialog close", exception);
        }
    }

    private void OnChanged() => _ = RenderAsync();

    private async Task RenderAsync() {
        try {
            await InvokeAsync(() => {
                if (!disposed) {
                    StateHasChanged();
                }
            });
        } catch (ObjectDisposedException) when (disposed) {
        } catch (Exception exception) {
            Session.ReportFailure("page rendering", exception);
        }
    }

    public void Dispose() {
        disposed = true;
        if (workspace is not null) {
            workspace.Changed -= OnChanged;
            workspace.Dispose();
        }
    }
}
