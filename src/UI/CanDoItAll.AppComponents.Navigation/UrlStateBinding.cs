using Microsoft.AspNetCore.Components;

namespace CanDoItAll.AppComponents.Navigation;

public readonly record struct UrlStateChange<TState>(TState Previous, TState Current, bool IsInitial, bool IsChanged);

public sealed class UrlStateBinding<TState> {
    private const string ParentMarkerPrefix = "cda-nav-parent:";
    private readonly NavigationManager navigation;
    private readonly IAppNavigationHistory? history;
    private readonly IReadOnlyList<string>? routes;
    private readonly string createdAt;
    private readonly List<string> pending = [];
    private readonly Queue<HeldWrite> held = new();
    private string? pendingOrigin;
    private string? backTarget;
    private string? appliedUri;
    private bool writable = true;
    private bool entryRestoreStarted;
    private RestoredEntry? restoredEntry;

    public UrlStateBinding(NavigationManager navigation, IUrlStateCodec<TState> codec, IAppNavigationHistory? history = null) {
        ArgumentNullException.ThrowIfNull(navigation);
        ArgumentNullException.ThrowIfNull(codec);
        this.navigation = navigation;
        this.history = history;
        Codec = codec;
        State = codec.Read(navigation.Uri);
        createdAt = RelativePath(navigation.Uri);
    }

    // The page's own routes, plus the location it was created at: a location of another page is never applied,
    // canonicalized or written to.
    public UrlStateBinding(NavigationManager navigation, IUrlStateCodec<TState> codec, IAppNavigationHistory? history, Type page)
        : this(navigation, codec, history) {
        routes = UrlRoutes.Templates(page);
    }

    public IUrlStateCodec<TState> Codec { get; }

    public TState State { get; private set; }

    public long Version { get; private set; }

    private string EffectiveUri => pending.Count > 0 ? pending[^1] : navigation.Uri;

    // A state restored from the host stands in for HistoryEntryState only while the binding stays on that entry.
    private string? EntryState => navigation.HistoryEntryState ??
        (restoredEntry is { } entry && string.Equals(entry.Uri, navigation.Uri, StringComparison.Ordinal) &&
         string.Equals(entry.Uri, appliedUri, StringComparison.Ordinal)
            ? entry.State
            : null);

    // canonicalize also decides whether intents may write the location: pass RendererInfo.IsInteractive, so prerendering
    // neither redirects nor turns a default selection into a server redirect. While it is false, intents only update State.
    public UrlStateChange<TState> Apply(bool canonicalize = true) {
        var change = ApplyCore(canonicalize);
        if (canonicalize) {
            RestoreEntryStateOnce();
        }
        return change;
    }

    private UrlStateChange<TState> ApplyCore(bool canonicalize) {
        writable = canonicalize;
        var uri = navigation.Uri;
        var initial = appliedUri is null;
        if (!Owns(uri) || !initial && (IsPendingArrival(uri) || string.Equals(uri, appliedUri, StringComparison.Ordinal))) {
            return new(State, State, false, false);
        }
        pending.Clear();
        pendingOrigin = null;
        // Another location arrived before the binding's own Back: the writes held for that Back are superseded.
        backTarget = null;
        held.Clear();
        var previous = State;
        var current = Codec.Read(uri);
        var changed = initial || !Codec.SameState(previous, current);
        State = current;
        appliedUri = uri;
        if (changed) {
            Version++;
        }
        if (canonicalize) {
            var canonical = UrlLocation.Compose(uri, Codec, current);
            if (!string.Equals(canonical, uri, StringComparison.Ordinal)) {
                Go(canonical, UrlHistoryMode.Replace, EntryState);
            }
        }
        return new(previous, current, initial, changed);
    }

    public bool Navigate(TState next, UrlHistoryMode mode = UrlHistoryMode.Push)
        => NavigateCore(next, mode, mode == UrlHistoryMode.Replace ? EntryState : null);

    public bool OpenOverlay(TState next)
        => NavigateCore(next, UrlHistoryMode.Push, ParentMarkerPrefix + UrlLocation.PathAndQuery(EffectiveUri));

    public async Task CloseOverlayAsync(TState parent, CancellationToken cancellationToken = default) {
        var source = EffectiveUri;
        if (!Owns(source)) {
            return;
        }
        var normalized = Codec.Normalize(parent);
        if (!writable || history is null || pending.Count > 0) {
            NavigateCore(normalized, UrlHistoryMode.Replace, null);
            return;
        }
        // The close applies at once, like every intent; only the way the location follows may wait for the host.
        SetState(normalized);
        var target = UrlLocation.Compose(source, Codec, normalized);
        var back = await IsParentEntryAsync(history, target, cancellationToken);
        if (!string.Equals(EffectiveUri, source, StringComparison.Ordinal)) {
            return;
        }
        if (back) {
            pendingOrigin = navigation.Uri;
            pending.Add(target);
            backTarget = target;
            try {
                await history.BackAsync(cancellationToken);
            } catch {
                backTarget = null;
                ReleaseHeldWrites();
                throw;
            }
            return;
        }
        NavigateCore(normalized, UrlHistoryMode.Replace, null);
    }

    public string Href(TState state) => UrlLocation.PathAndQuery(UrlLocation.Compose(EffectiveUri, Codec, state));

    private bool NavigateCore(TState next, UrlHistoryMode mode, string? entryState) {
        var source = EffectiveUri;
        if (!Owns(source)) {
            return false;
        }
        var normalized = Codec.Normalize(next);
        var target = UrlLocation.Compose(source, Codec, normalized);
        SetState(normalized);
        if (!writable || string.Equals(target, source, StringComparison.Ordinal)) {
            return false;
        }
        Go(target, mode, entryState);
        return true;
    }

    private bool Owns(string uri) {
        if (routes is null) {
            return true;
        }
        var path = RelativePath(uri);
        return string.Equals(path, createdAt, StringComparison.OrdinalIgnoreCase) || UrlRoutes.Matches(routes, path);
    }

    private string RelativePath(string uri) => UrlLocation.Split(navigation.ToBaseRelativePath(uri)).Path.Trim('/');

    private void SetState(TState next) {
        if (!Codec.SameState(State, next)) {
            Version++;
        }
        State = next;
    }

    private void Go(string target, UrlHistoryMode mode, string? entryState) {
        if (pending.Count == 0) {
            pendingOrigin = navigation.Uri;
        }
        pending.Add(target);
        var write = new HeldWrite(target, mode, entryState);
        // A browser runs a Back relative to the entry it was requested on and drops an entry pushed before the Back
        // runs, so a write made while the binding's own Back is in flight goes out only once that Back has arrived.
        if (backTarget is not null) {
            held.Enqueue(write);
            return;
        }
        Write(write);
    }

    private void Write(HeldWrite write)
        => navigation.NavigateTo(write.Target, new NavigationOptions {
            ReplaceHistoryEntry = write.Mode == UrlHistoryMode.Replace,
            HistoryEntryState = write.EntryState
        });

    private void ReleaseHeldWrites() {
        while (held.TryDequeue(out var write)) {
            Write(write);
        }
    }

    private bool IsPendingArrival(string uri) {
        if (pending.Count == 0) {
            return false;
        }
        var index = pending.FindLastIndex(candidate => string.Equals(candidate, uri, StringComparison.Ordinal));
        if (backTarget is not null && string.Equals(uri, backTarget, StringComparison.Ordinal)) {
            backTarget = null;
            ReleaseHeldWrites();
        }
        if (index == pending.Count - 1) {
            pending.Clear();
            pendingOrigin = null;
            appliedUri = uri;
            return true;
        }
        if (index >= 0) {
            pending.RemoveRange(0, index + 1);
            pendingOrigin = uri;
            return true;
        }
        return string.Equals(uri, pendingOrigin, StringComparison.Ordinal);
    }

    // Blazor starts a circuit without the HistoryEntryState of the entry it starts on, so after a reload the marker of a
    // pushed overlay is known only to the host. Restoring it once keeps Replace from erasing it and lets Close go Back.
    private void RestoreEntryStateOnce() {
        var uri = navigation.Uri;
        if (entryRestoreStarted || history is null || !IsStillOn(uri)) {
            return;
        }
        entryRestoreStarted = true;
        _ = RestoreEntryStateAsync(uri);
    }

    private async Task RestoreEntryStateAsync(string uri) {
        var state = await history!.GetEntryStateAsync();
        if (state is not null && IsStillOn(uri)) {
            restoredEntry = new(uri, state);
        }
    }

    private async ValueTask<bool> IsParentEntryAsync(IAppNavigationHistory host, string parentUri, CancellationToken cancellationToken) {
        if (IsParentMarker(EntryState, parentUri)) {
            return true;
        }
        if (navigation.HistoryEntryState is not null) {
            return false;
        }
        var uri = navigation.Uri;
        var state = await host.GetEntryStateAsync(cancellationToken);
        return IsStillOn(uri) && IsParentMarker(state, parentUri);
    }

    private bool IsStillOn(string uri)
        => pending.Count == 0 && navigation.HistoryEntryState is null &&
           string.Equals(navigation.Uri, uri, StringComparison.Ordinal) && string.Equals(appliedUri, uri, StringComparison.Ordinal);

    private static bool IsParentMarker(string? entryState, string parentUri)
        => string.Equals(entryState, ParentMarkerPrefix + UrlLocation.PathAndQuery(parentUri), StringComparison.Ordinal);

    private sealed record RestoredEntry(string Uri, string State);

    private sealed record HeldWrite(string Target, UrlHistoryMode Mode, string? EntryState);
}
