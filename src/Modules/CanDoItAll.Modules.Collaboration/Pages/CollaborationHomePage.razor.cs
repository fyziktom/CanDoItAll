using CanDoItAll.Components.BaseLib;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.Collaboration.Pages;

public partial class CollaborationHomePage {
    [SupplyParameterFromQuery(Name = "threadId")]
    public Guid? ThreadIdQuery { get; set; }

    [Inject]
    private ICollaborationWorkspaceOwner Owner { get; set; } = default!;

    [Inject]
    private NavigationManager Navigation { get; set; } = default!;

    [Inject]
    private NotificationService Notifications { get; set; } = default!;

    [Inject]
    private ILogger<CollaborationWorkspaceSession> Logger { get; set; } = default!;

    private CollaborationWorkspaceSession session = default!;
    private bool interactive;

    protected override void OnInitialized() {
        session = new(Owner, Logger, StateHasChanged, ReplaceRoute, route => Navigation.NavigateTo(route), Notify);
    }

    protected override Task OnParametersSetAsync() => session.ApplyRouteAsync(ThreadIdQuery);

    protected override void OnAfterRender(bool firstRender) {
        if (firstRender) {
            interactive = true;
            StateHasChanged();
        }
    }

    private void ReplaceRoute(Guid? threadId) {
        var route = threadId.HasValue ? $"/collaboration?threadId={threadId.Value:D}" : "/collaboration";
        if (Navigation.ToAbsoluteUri(route).AbsoluteUri != Navigation.Uri) {
            Navigation.NavigateTo(route, replace: true);
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
