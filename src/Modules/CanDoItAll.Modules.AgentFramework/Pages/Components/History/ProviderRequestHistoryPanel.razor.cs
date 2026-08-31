using CanDoItAll.AgentFramework.ProviderHistory;
using CanDoItAll.Infrastructure.Persistence;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.AgentFramework.Pages.Components.History;

public partial class ProviderRequestHistoryPanel : IDisposable {
    [Parameter, EditorRequired] public HistoryProviderScope Scope { get; set; } = default!;
    [Inject] public IProviderRequestHistory History { get; set; } = default!;
    [Inject] public TimeProvider Clock { get; set; } = default!;
    [Inject] public ILogger<ProviderHistorySearchState> Logger { get; set; } = default!;
    [Inject] public IDatabaseSwitchNotificationService ProfileChanges { get; set; } = default!;
    [CascadingParameter] public Task<AuthenticationState>? AuthenticationState { get; set; }

    private ProviderHistorySearchState state = default!;
    private ProviderHistoryFilterDraft draft = default!;
    private EditContext editContext = default!;
    private HistoryProviderScope? previousScope;
    private Task<AuthenticationState>? previousAuthentication;
    private HistoryEntryId? selectedEntry;
    private bool draftChanged;
    private bool disposed;

    protected override void OnInitialized() {
        state = new(History, Logger);
        ResetDraft();
        ProfileChanges.Changed += ProfileChanged;
    }

    protected override void OnParametersSet() {
        ArgumentNullException.ThrowIfNull(Scope);
        if (previousScope != Scope || !ReferenceEquals(previousAuthentication, AuthenticationState)) {
            ClearResults();
            ResetDraft();
            previousScope = Scope;
            previousAuthentication = AuthenticationState;
        }
    }

    private void ResetDraft() {
        draft = new(Clock.GetUtcNow());
        editContext = new(draft);
        draftChanged = false;
    }

    private async Task SearchAsync() {
        if (!editContext.Validate()) {
            return;
        }
        selectedEntry = null;
        draftChanged = false;
        await state.SearchAsync(draft.ToQuery(Scope, Clock.GetUtcNow()));
    }

    private void DraftChanged() => draftChanged = true;

    private void ClearResults() {
        state.Reset();
        selectedEntry = null;
        draftChanged = false;
    }

    private void ProfileChanged(object? sender, DatabaseProfileChangedNotification notification) {
        if (!disposed) {
            _ = InvokeAsync(() => {
                if (disposed) {
                    return;
                }
                ClearResults();
                ResetDraft();
                StateHasChanged();
            });
        }
    }

    public void Dispose() {
        disposed = true;
        ProfileChanges.Changed -= ProfileChanged;
        state.Dispose();
    }
}
