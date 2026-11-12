using CanDoItAll.AppComponents.Navigation;
using Microsoft.AspNetCore.Components;

namespace CanDoItAll.Tests.Unit.Navigation;

internal sealed class ScriptedNavigationManager : NavigationManager {
    public const string Base = "http://localhost/";

    public ScriptedNavigationManager(string relativeUri, string? historyEntryState = null) {
        Initialize(Base, Base + relativeUri.TrimStart('/'));
        HistoryEntryState = historyEntryState;
    }

    public List<NavigationRequest> Requests { get; } = [];

    public bool DeliverImmediately { get; set; }

    protected override void NavigateToCore(string uri, NavigationOptions options) {
        Requests.Add(new(ToAbsoluteUri(uri).AbsoluteUri, options.ReplaceHistoryEntry, options.HistoryEntryState));
        if (DeliverImmediately) {
            Deliver(Requests.Count - 1);
        }
    }

    public void Deliver(int index) {
        var request = Requests[index];
        HistoryEntryState = request.HistoryEntryState;
        Uri = request.Uri;
        NotifyLocationChanged(false);
    }

    public void External(string relativeUri, string? historyEntryState = null) {
        HistoryEntryState = historyEntryState;
        Uri = Base + relativeUri.TrimStart('/');
        NotifyLocationChanged(false);
    }

    public string Relative => UrlLocation.PathAndQuery(Uri);
}

internal sealed record NavigationRequest(string Uri, bool Replace, string? HistoryEntryState) {
    public string Relative => UrlLocation.PathAndQuery(Uri);
}

internal sealed class ScriptedNavigationHistory(ScriptedNavigationManager navigation, string backTo) : IAppNavigationHistory {
    public int BackCalls { get; private set; }

    public int EntryStateReads { get; private set; }

    // What the browser still keeps for the current entry, for example after a reload.
    public string? EntryState { get; set; }

    // When set, the next answers wait for this source, as a browser round trip does.
    public TaskCompletionSource<string?>? PendingAnswer { get; set; }

    // When set, a Back stays in flight until ArriveBack, as the browser runs it after the call returns.
    public bool HoldBack { get; set; }

    public ValueTask BackAsync(CancellationToken cancellationToken = default) {
        BackCalls++;
        if (!HoldBack) {
            navigation.External(backTo);
        }
        return ValueTask.CompletedTask;
    }

    public void ArriveBack() => navigation.External(backTo);

    public ValueTask<string?> GetEntryStateAsync(CancellationToken cancellationToken = default) {
        EntryStateReads++;
        return PendingAnswer is { } answer ? new(answer.Task) : ValueTask.FromResult(EntryState);
    }
}
