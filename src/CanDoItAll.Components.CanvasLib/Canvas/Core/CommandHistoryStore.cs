namespace CanDoItAll.Components.CanvasLib;

public sealed class CommandHistoryStore<T>
{
    private readonly List<HistoryEntry> undoEntries = [];
    private readonly List<HistoryEntry> redoEntries = [];
    private readonly Func<T, string> fingerprint;

    public CommandHistoryStore(Func<T, string> fingerprint, int maxEntries = 40)
    {
        this.fingerprint = fingerprint ?? throw new ArgumentNullException(nameof(fingerprint));
        MaxEntries = Math.Max(1, maxEntries);
    }

    public int MaxEntries { get; }

    public bool CanUndo => undoEntries.Count > 0;

    public bool CanRedo => redoEntries.Count > 0;

    public void Clear()
    {
        undoEntries.Clear();
        redoEntries.Clear();
    }

    public void Remember(T snapshot)
    {
        var entry = CreateEntry(snapshot);
        if (undoEntries.Count > 0 &&
            string.Equals(undoEntries[^1].Fingerprint, entry.Fingerprint, StringComparison.Ordinal))
        {
            return;
        }

        if (undoEntries.Count >= MaxEntries)
        {
            undoEntries.RemoveAt(0);
        }

        undoEntries.Add(entry);
        redoEntries.Clear();
    }

    public bool TryUndo(T currentSnapshot, out T snapshot)
    {
        if (undoEntries.Count == 0)
        {
            snapshot = default!;
            return false;
        }

        redoEntries.Add(CreateEntry(currentSnapshot));
        snapshot = undoEntries[^1].Snapshot;
        undoEntries.RemoveAt(undoEntries.Count - 1);
        return true;
    }

    public bool TryRedo(T currentSnapshot, out T snapshot)
    {
        if (redoEntries.Count == 0)
        {
            snapshot = default!;
            return false;
        }

        undoEntries.Add(CreateEntry(currentSnapshot));
        snapshot = redoEntries[^1].Snapshot;
        redoEntries.RemoveAt(redoEntries.Count - 1);
        return true;
    }

    private HistoryEntry CreateEntry(T snapshot)
        => new(snapshot, fingerprint(snapshot));

    private sealed record HistoryEntry(T Snapshot, string Fingerprint);
}

public sealed class CommandHistoryStorePreviewSnapshot
{
    public string TestHookId { get; init; } = "command-history-store";

    public string Label { get; init; } = "Command history store";

    public string Title { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public string StatePill { get; init; } = string.Empty;

    public IReadOnlyList<string> Metrics { get; init; } = [];
}

public static class CommandHistoryStorePreviewFactory
{
    public static CommandHistoryStorePreviewSnapshot CreateForWorkbench(CanvasWorkbenchSurface surface)
    {
        ArgumentNullException.ThrowIfNull(surface);

        var store = new CommandHistoryStore<CanvasWorkbenchUiState>(state => state.ToJson(), maxEntries: 12);
        var baseline = surface.UiState;
        var edited = CanvasWorkbenchUiState.Parse(surface.UiState.ToJson());
        edited.SelectedNodeIds = [.. surface.UiState.SelectedNodeIds];
        edited.SelectedNodeIds.Add("history-preview");

        store.Remember(baseline);
        store.Remember(edited);
        store.TryUndo(edited, out var undone);

        return new CommandHistoryStorePreviewSnapshot
        {
            Title = "Undo and redo snapshots now flow through one bounded command store",
            Summary = "Prompt factory history, future graph undo, and selection state changes can share the same deduplicated fingerprinted store instead of each page inventing its own stack semantics.",
            StatePill = store.CanUndo || store.CanRedo ? "Armed" : "Idle",
            Metrics =
            [
                "12 entry cap",
                store.CanUndo ? "Undo available" : "Undo empty",
                store.CanRedo ? "Redo available" : "Redo empty",
                $"{undone.SelectedNodeIds.Count} ids restored"
            ]
        };
    }
}


