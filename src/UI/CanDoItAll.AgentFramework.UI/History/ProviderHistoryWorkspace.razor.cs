using CanDoItAll.AgentFramework.ProviderHistory;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.AgentFramework.UI.History;

public partial class ProviderHistoryWorkspace : IDisposable {
    [Parameter, EditorRequired] public HistoryProviderScope Scope { get; set; } = default!;
    [Parameter, EditorRequired] public IProviderRequestHistory History { get; set; } = default!;
    [Parameter] public TimeProvider Clock { get; set; } = TimeProvider.System;
    [Inject] public ILogger<ProviderHistorySearchState> Logger { get; set; } = default!;

    private ProviderHistorySearchState state = default!;
    private ProviderHistoryFilterDraft draft = default!;
    private EditContext editContext = default!;
    private HistoryProviderScope? previousScope;
    private IProviderRequestHistory? previousHistory;
    private ProviderHistoryResultsSurface? results;
    private ProviderHistoryDetailsDialog? details;
    private DetailSelection? selectedEntry;
    private bool draftChanged;
    private bool disposed;

    protected override void OnParametersSet() {
        ArgumentNullException.ThrowIfNull(Scope);
        ArgumentNullException.ThrowIfNull(History);
        if (disposed || previousScope == Scope && ReferenceEquals(previousHistory, History)) {
            return;
        }
        ClearDetails();
        state?.Dispose();
        state = new(History, Logger);
        state.Changed += Render;
        if (editContext is not null) {
            editContext.OnFieldChanged -= DraftChanged;
        }
        draft = new(Clock.GetUtcNow());
        editContext = new(draft);
        editContext.OnFieldChanged += DraftChanged;
        draftChanged = false;
        previousScope = Scope;
        previousHistory = History;
    }

    private Task SearchAsync(HistoryViewOrigin origin, EditContext context) {
        if (!state.IsCurrent(origin) || !ReferenceEquals(editContext, context) || !context.Validate()) {
            return Task.CompletedTask;
        }
        var query = draft.ToQuery(Scope, Clock.GetUtcNow());
        ClearDetails();
        draftChanged = false;
        return state.SearchAsync(query);
    }

    private Task DispatchAsync(HistoryResultsIntent intent) {
        if (!state.IsCurrent(intent.Origin)) {
            return Task.CompletedTask;
        }
        switch (intent) {
            case HistoryResultsIntent.Previous:
                ClearDetails();
                return state.PreviousAsync();
            case HistoryResultsIntent.Next:
                ClearDetails();
                return state.NextAsync();
            case HistoryResultsIntent.Cancel:
                ClearDetails();
                state.Cancel();
                break;
            case HistoryResultsIntent.Clear:
                ClearDetails();
                state.Reset();
                draftChanged = false;
                break;
            case HistoryResultsIntent.Details selection when state.Page?.Entries.Any(entry => entry.Id == selection.EntryId) == true:
                ClearDetails();
                selectedEntry = new(selection.EntryId, HistoryViewOrigin.New());
                state.InvalidateIntents();
                break;
        }
        return Task.CompletedTask;
    }

    private void CloseDetails(DetailSelection selection) {
        if (!disposed && ReferenceEquals(selectedEntry, selection)) {
            ClearDetails();
            state.InvalidateIntents();
        }
    }

    private void ClearDetails() {
        details?.Dispose();
        details = null;
        selectedEntry = null;
    }

    private void DraftChanged(object? sender, FieldChangedEventArgs args) {
        MarkDraftChanged(sender as EditContext);
    }

    private void MarkDraftChanged(EditContext? context) {
        if (!disposed && ReferenceEquals(context, editContext)) {
            draftChanged = true;
        }
    }

    private void Render() => _ = InvokeAsync(() => {
        if (!disposed) {
            StateHasChanged();
        }
    });

    public void Dispose() {
        if (disposed) {
            return;
        }
        disposed = true;
        ClearDetails();
        results?.Dispose();
        results = null;
        state.Dispose();
        editContext.OnFieldChanged -= DraftChanged;
        editContext = null!;
        draft = null!;
        previousHistory = null;
        History = null!;
    }

    private sealed record DetailSelection(HistoryEntryId EntryId, HistoryViewOrigin Origin);
}
