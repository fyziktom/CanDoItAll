using CanDoItAll.AgentFramework.ProviderHistory;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.AgentFramework.UI.History;

public partial class ProviderHistoryWorkspace : IDisposable {
    [Parameter, EditorRequired] public HistoryProviderScope Scope { get; set; } = default!;
    [Parameter, EditorRequired] public IProviderRequestHistory History { get; set; } = default!;
    [Parameter] public TimeProvider Clock { get; set; } = TimeProvider.System;
    [Parameter] public ProviderHistoryLocation? Location { get; set; }
    [Parameter] public EventCallback<ProviderHistoryLocationChange> LocationChanged { get; set; }
    [Inject] public ILogger<ProviderHistorySearchState> Logger { get; set; } = default!;

    private ProviderHistorySearchState state = default!;
    private ProviderHistoryFilterDraft draft = default!;
    private EditContext editContext = default!;
    private HistoryProviderScope? previousScope;
    private IProviderRequestHistory? previousHistory;
    private ProviderHistoryResultsSurface? results;
    private ProviderHistoryDetailsDialog? details;
    private DetailSelection? selectedEntry;
    private ProviderHistoryAppliedFilter? appliedFilter;
    private ProviderHistoryLocation? publishedLocation;
    private bool locationApplied;
    private bool draftChanged;
    private bool disposed;

    private bool IsControlled => LocationChanged.HasDelegate;

    protected override Task OnParametersSetAsync() {
        ArgumentNullException.ThrowIfNull(Scope);
        ArgumentNullException.ThrowIfNull(History);
        if (disposed) {
            return Task.CompletedTask;
        }
        if (previousScope != Scope || !ReferenceEquals(previousHistory, History)) {
            ClearDetails();
            state?.Dispose();
            state = new(History, Logger);
            state.Changed += Render;
            ReplaceDraft(new(Clock.GetUtcNow()));
            appliedFilter = null;
            publishedLocation = null;
            locationApplied = false;
            previousScope = Scope;
            previousHistory = History;
        }
        if (!IsControlled || locationApplied && Location == publishedLocation) {
            return Task.CompletedTask;
        }
        locationApplied = true;
        publishedLocation = Location;
        return ApplyLocationAsync(Location);
    }

    private ProviderHistoryLocation? CurrentLocation => appliedFilter is { } filter
        ? new(filter, state.CurrentCursor, state.PageNumber, selectedEntry?.EntryId)
        : null;

    private Task ApplyLocationAsync(ProviderHistoryLocation? location) {
        if (location is null) {
            ClearDetails();
            appliedFilter = null;
            draftChanged = false;
            if (state.HasRequested) {
                state.Reset();
            }
            return Task.CompletedTask;
        }
        if (appliedFilter != location.Filter) {
            ReplaceDraft(location.Filter.ToDraft());
        }
        appliedFilter = location.Filter;
        draftChanged = false;
        if (selectedEntry?.EntryId != location.EntryId) {
            ClearDetails();
            if (location.EntryId is { } entryId) {
                selectedEntry = new(entryId, HistoryViewOrigin.New());
            }
        }
        return state.ShowAsync(location.Filter.ToQuery(Scope), location.Cursor, location.Page);
    }

    private Task PublishAsync(ProviderHistoryLocationChangeKind kind, ProviderHistoryLocation? location) {
        if (!IsControlled || disposed) {
            return Task.CompletedTask;
        }
        publishedLocation = location;
        locationApplied = true;
        return LocationChanged.InvokeAsync(new(location, kind));
    }

    private async Task SearchAsync(HistoryViewOrigin origin, EditContext context) {
        if (!state.IsCurrent(origin) || !ReferenceEquals(editContext, context) || !context.Validate()) {
            return;
        }
        var filter = ProviderHistoryAppliedFilter.Capture(draft, Clock.GetUtcNow());
        ClearDetails();
        draftChanged = false;
        appliedFilter = filter;
        var search = state.SearchAsync(filter.ToQuery(Scope));
        await PublishAsync(ProviderHistoryLocationChangeKind.Search, CurrentLocation);
        await search;
    }

    private async Task DispatchAsync(HistoryResultsIntent intent) {
        if (!state.IsCurrent(intent.Origin)) {
            return;
        }
        switch (intent) {
            case HistoryResultsIntent.Previous when appliedFilter is { } filter && state.CanPrevious:
                ClearDetails();
                var previous = new ProviderHistoryLocation(filter, state.PreviousCursor, state.PageNumber - 1);
                var previousRead = state.PreviousAsync();
                await PublishAsync(ProviderHistoryLocationChangeKind.Page, previous);
                await previousRead;
                break;
            case HistoryResultsIntent.Next when appliedFilter is { } filter && state.CanNext:
                ClearDetails();
                var next = new ProviderHistoryLocation(filter, state.NextCursor, state.PageNumber + 1);
                var nextRead = state.NextAsync();
                await PublishAsync(ProviderHistoryLocationChangeKind.Page, next);
                await nextRead;
                break;
            case HistoryResultsIntent.Cancel:
                ClearDetails();
                state.Cancel();
                break;
            case HistoryResultsIntent.Clear:
                ClearDetails();
                state.Reset();
                appliedFilter = null;
                draftChanged = false;
                await PublishAsync(ProviderHistoryLocationChangeKind.Clear, null);
                break;
            case HistoryResultsIntent.Details selection when state.Page?.Entries.Any(entry => entry.Id == selection.EntryId) == true:
                ClearDetails();
                selectedEntry = new(selection.EntryId, HistoryViewOrigin.New());
                state.InvalidateIntents();
                await PublishAsync(ProviderHistoryLocationChangeKind.OpenEntry, CurrentLocation);
                break;
        }
    }

    private Task CloseDetails(DetailSelection selection) {
        if (disposed || !ReferenceEquals(selectedEntry, selection)) {
            return Task.CompletedTask;
        }
        ClearDetails();
        state.InvalidateIntents();
        return PublishAsync(ProviderHistoryLocationChangeKind.CloseEntry, CurrentLocation);
    }

    private void ClearDetails() {
        details?.Dispose();
        details = null;
        selectedEntry = null;
    }

    private void ReplaceDraft(ProviderHistoryFilterDraft next) {
        if (editContext is not null) {
            editContext.OnFieldChanged -= DraftChanged;
        }
        draft = next;
        editContext = new(draft);
        editContext.OnFieldChanged += DraftChanged;
        draftChanged = false;
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
