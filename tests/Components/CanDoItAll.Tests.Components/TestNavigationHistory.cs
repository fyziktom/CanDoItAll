using Bunit.TestDoubles;
using CanDoItAll.AppComponents.Navigation;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;

namespace CanDoItAll.Tests.Components;

// The session history of one browser tab for component tests. bUnit records navigations but has no Back, so this
// keeps the entries a browser keeps: a replaced location rewrites the current entry, any other location is pushed
// after it, and Back returns to the previous entry with the state that entry was saved with.
internal sealed class TestNavigationHistory : IAppNavigationHistory, IDisposable {
    private readonly BunitNavigationManager navigation;
    private readonly List<Entry> entries = [];
    private int? traversalTarget;

    public TestNavigationHistory(NavigationManager navigation) {
        this.navigation = navigation as BunitNavigationManager
            ?? throw new InvalidOperationException("The test history follows the bUnit navigation manager.");
        entries.Add(new(navigation.Uri, navigation.HistoryEntryState));
        navigation.LocationChanged += OnLocationChanged;
    }

    public int BackCount { get; private set; }

    public int Index { get; private set; }

    public IReadOnlyList<string> Locations => entries.Select(entry => entry.Uri).ToArray();

    public ValueTask BackAsync(CancellationToken cancellationToken = default) {
        BackCount++;
        if (Index == 0) {
            return ValueTask.CompletedTask;
        }

        var previous = entries[Index - 1];
        traversalTarget = Index - 1;
        navigation.NavigateTo(previous.Uri, new NavigationOptions {
            ReplaceHistoryEntry = true,
            HistoryEntryState = previous.State
        });
        return ValueTask.CompletedTask;
    }

    public ValueTask<string?> GetEntryStateAsync(CancellationToken cancellationToken = default)
        => ValueTask.FromResult(entries[Index].State);

    public void Dispose() => navigation.LocationChanged -= OnLocationChanged;

    private void OnLocationChanged(object? sender, LocationChangedEventArgs args) {
        if (traversalTarget is { } target) {
            traversalTarget = null;
            Index = target;
            return;
        }

        // bUnit records the navigation before it announces the new location; the latest record says how it was made.
        var latest = navigation.History.FirstOrDefault();
        if (latest is null ||
            !string.Equals(navigation.ToAbsoluteUri(latest.Uri).AbsoluteUri, new Uri(args.Location).AbsoluteUri, StringComparison.Ordinal)) {
            throw new InvalidOperationException($"bUnit recorded no navigation to '{args.Location}'.");
        }

        var entry = new Entry(args.Location, args.HistoryEntryState);
        if (latest.Options.ReplaceHistoryEntry) {
            entries[Index] = entry;
            return;
        }

        entries.RemoveRange(Index + 1, entries.Count - Index - 1);
        entries.Add(entry);
        Index++;
    }

    private sealed record Entry(string Uri, string? State);
}
