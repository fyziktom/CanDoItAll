using CanDoItAll.AgentFramework.ProviderHistory;
using CanDoItAll.Infrastructure.Persistence;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

namespace CanDoItAll.Modules.Workspace.Pages.Components;

public partial class ProviderHistoryPolicyPanel : IDisposable {
    [Inject] public IProviderHistoryPolicyService Policies { get; set; } = default!;
    [Inject] public IDatabaseSwitchNotificationService ProfileChanges { get; set; } = default!;
    [CascadingParameter] public Task<AuthenticationState>? AuthenticationState { get; set; }
    private WorkspaceHistoryController controller = default!;
    private Task<AuthenticationState>? previousAuthentication;
    private bool disposed;

    protected override void OnInitialized() {
        controller = new(Policies);
        ProfileChanges.Changed += ProfileChanged;
    }

    protected override void OnParametersSet() {
        if (!ReferenceEquals(previousAuthentication, AuthenticationState)) {
            controller.Retire();
            previousAuthentication = AuthenticationState;
        }
    }

    private void ProfileChanged(object? sender, DatabaseProfileChangedNotification notification) {
        if (!disposed) {
            _ = InvokeAsync(() => {
                if (!disposed) {
                    controller.Retire();
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
        controller.Dispose();
    }
}
