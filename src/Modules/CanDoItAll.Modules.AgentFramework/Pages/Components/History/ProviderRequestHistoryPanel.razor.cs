using CanDoItAll.AgentFramework.ProviderHistory;
using CanDoItAll.AgentFramework.UI.History;
using CanDoItAll.Infrastructure.Persistence;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

namespace CanDoItAll.Modules.AgentFramework.Pages.Components.History;

public partial class ProviderRequestHistoryPanel : IDisposable {
    [Parameter, EditorRequired] public HistoryProviderScope Scope { get; set; } = default!;
    [Inject] public IProviderRequestHistory History { get; set; } = default!;
    [Inject] public TimeProvider Clock { get; set; } = default!;
    [Inject] public IDatabaseSwitchNotificationService ProfileChanges { get; set; } = default!;
    [CascadingParameter] public Task<AuthenticationState>? AuthenticationState { get; set; }

    private ProviderHistoryWorkspace? workspace;
    private HistoryViewOrigin activation = HistoryViewOrigin.New();
    private HistoryProviderScope? previousScope;
    private Task<AuthenticationState>? previousAuthentication;
    private bool disposed;

    protected override void OnInitialized() => ProfileChanges.Changed += ProfileChanged;

    protected override void OnParametersSet() {
        ArgumentNullException.ThrowIfNull(Scope);
        if (previousScope != Scope || !ReferenceEquals(previousAuthentication, AuthenticationState)) {
            Retire();
            previousScope = Scope;
            previousAuthentication = AuthenticationState;
        }
    }

    private void Retire() {
        workspace?.Dispose();
        workspace = null;
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
