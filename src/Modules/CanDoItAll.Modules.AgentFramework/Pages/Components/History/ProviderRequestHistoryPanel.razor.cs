using CanDoItAll.AgentFramework.ProviderHistory;
using CanDoItAll.AgentFramework.UI.History;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.AgentFramework.Pages.Navigation;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

namespace CanDoItAll.Modules.AgentFramework.Pages.Components.History;

public partial class ProviderRequestHistoryPanel : IDisposable {
    [Parameter, EditorRequired] public HistoryProviderScope Scope { get; set; } = default!;
    [Parameter] public ProviderHistoryLocation? Location { get; set; }
    [Parameter] public EventCallback<ProviderHistoryLocationChange> LocationChanged { get; set; }
    [Inject] public IProviderRequestHistory History { get; set; } = default!;
    [Inject] public TimeProvider Clock { get; set; } = default!;
    [Inject] public IDatabaseSwitchNotificationService ProfileChanges { get; set; } = default!;
    [CascadingParameter] public Task<AuthenticationState>? AuthenticationState { get; set; }

    private ProviderHistoryWorkspace? workspace;
    private HistoryViewOrigin activation = HistoryViewOrigin.New();
    private HistoryProviderScope? previousScope;
    private Task<AuthenticationState>? previousAuthentication;
    private ProviderHistoryLocation? published;
    private ProviderHistoryLocation? publishedOver;
    private bool hasPublished;
    private bool awaitingHost;
    private bool disposed;

    // A queued render of this panel can run before the host re-renders it with the location it was just told about,
    // so the published location stands until the host supplies a location other than the one it was published over.
    private ProviderHistoryLocation? WorkspaceLocation
        => awaitingHost || hasPublished && Reduce(published) == Reduce(Location) ? published : Location;

    private EventCallback<ProviderHistoryLocationChange> WorkspaceLocationChanged => LocationChanged.HasDelegate
        ? EventCallback.Factory.Create<ProviderHistoryLocationChange>(this, PublishAsync)
        : default;

    protected override void OnInitialized() => ProfileChanges.Changed += ProfileChanged;

    protected override void OnParametersSet() {
        ArgumentNullException.ThrowIfNull(Scope);
        if (awaitingHost && Location != publishedOver) {
            awaitingHost = false;
        }
        if (previousScope != Scope || !ReferenceEquals(previousAuthentication, AuthenticationState)) {
            Retire();
            previousScope = Scope;
            previousAuthentication = AuthenticationState;
        }
    }

    private Task PublishAsync(ProviderHistoryLocationChange change) {
        published = change.Location;
        publishedOver = Location;
        hasPublished = true;
        awaitingHost = true;
        return LocationChanged.InvokeAsync(change);
    }

    private static AgentsHistoryUrlState? Reduce(ProviderHistoryLocation? location) => AgentsHistoryUrlState.FromLocation(location);

    private void Retire() {
        workspace?.Dispose();
        workspace = null;
        published = null;
        publishedOver = null;
        hasPublished = false;
        awaitingHost = false;
        activation = HistoryViewOrigin.New();
    }

    private void ProfileChanged(object? sender, DatabaseProfileChangedNotification notification) {
        if (!disposed) {
            _ = InvokeAsync(() => {
                if (!disposed) {
                    Retire();
                    StateHasChanged();
                }
            });
        }
    }

    public void Dispose() {
        if (disposed) {
            return;
        }
        disposed = true;
        ProfileChanges.Changed -= ProfileChanged;
        Retire();
    }
}
