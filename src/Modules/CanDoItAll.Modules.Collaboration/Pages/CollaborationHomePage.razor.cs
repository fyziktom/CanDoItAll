using CanDoItAll.AppComponents.Navigation;
using CanDoItAll.Components.BaseLib;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.Collaboration.Pages;

public partial class CollaborationHomePage {
    [SupplyParameterFromQuery(Name = CollaborationUrlCodec.ViewKey)]
    public string? ViewQuery { get; set; }

    [SupplyParameterFromQuery(Name = CollaborationUrlCodec.UnreadKey)]
    public string? UnreadQuery { get; set; }

    [SupplyParameterFromQuery(Name = CollaborationUrlCodec.ThreadKey)]
    public string? ThreadIdQuery { get; set; }

    [Inject]
    private ICollaborationWorkspaceOwner Owner { get; set; } = default!;

    [Inject]
    private NavigationManager Navigation { get; set; } = default!;

    [Inject]
    private NotificationService Notifications { get; set; } = default!;

    [Inject]
    private ILogger<CollaborationWorkspaceSession> Logger { get; set; } = default!;

    private UrlStateBinding<CollaborationUrlState> url = default!;
    private CollaborationWorkspaceSession session = default!;
    private bool interactive;

    protected override void OnInitialized() {
        url = new(Navigation, CollaborationUrlCodec.Instance, null, typeof(CollaborationHomePage));
        session = new(Owner, Logger, StateHasChanged, (route, mode) => url.Navigate(route, mode), route => Navigation.NavigateTo(route), Notify);
    }

    protected override Task OnParametersSetAsync() {
        var change = url.Apply(canonicalize: RendererInfo.IsInteractive);
        return change.IsChanged ? session.ApplyRouteAsync(change.Current) : Task.CompletedTask;
    }

    protected override void OnAfterRender(bool firstRender) {
        if (firstRender) {
            interactive = true;
            StateHasChanged();
        }
    }

    private void Notify(string message, bool error) {
        if (error) {
            Notifications.Error("Collaboration update failed", message);
        } else {
            Notifications.Success("Collaboration updated", message);
        }
    }

    public void Dispose() => session.Dispose();
}
