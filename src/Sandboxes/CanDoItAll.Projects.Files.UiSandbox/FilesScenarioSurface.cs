using CanDoItAll.FileTools.FileBrowser;
using CanDoItAll.FileTools.FileBrowser.Components;
using CanDoItAll.Projects.Files.UI;
using Microsoft.AspNetCore.Components;

namespace CanDoItAll.Projects.Files.UiSandbox;

public enum FilesScenario { Representative, Empty, Large, MissingSource }

public sealed class FilesScenarioSurface : IAsyncDisposable {
    private readonly object receiver;
    private readonly CanDoItAll.FileTools.FileInteraction.Components.FileInteractionComponentComposition composition = FilesFixtureComposition.Create();
    private readonly List<FilesFixtureProvider> providers;
    private readonly List<FilesFixtureGate> gates = [];
    private FilesFixtureContent? preview;
    private bool retired;
    private int revision;
    public IFileBrowserSession Browser { get; }
    public ProjectFilesViewState State { get; private set; } = new();
    public int SimulatedActions { get; private set; }
    public int PendingReads => gates.Count;

    public FilesScenarioSurface(object receiver, FilesScenario scenario, EventCallback closed = default) {
        this.receiver = receiver;
        providers = scenario == FilesScenario.Empty ? [] : [new("alpha", scenario == FilesScenario.Large ? 220 : 7), new("beta")];
        if (scenario == FilesScenario.MissingSource) {
            providers[0].Removed = true;
        }
        Browser = new FileBrowserSession(new FileBrowserSourceSet("fixture-initial", providers), options: new FileBrowserSessionOptions(pageSize: 50,
            defaultSort: new(FileBrowserSortField.ProviderNative, FileBrowserSortDirection.Ascending, FoldersFirst: false),
            retentionMode: FileBrowserStateRetentionMode.Disabled,
            searchBudget: new(maximumContainers: 32, maximumItems: 2_000, maximumDuration: TimeSpan.FromSeconds(5),
                maximumConcurrentRequests: 1, maximumMatches: 200, maximumRetainedBytes: 2L * 1024 * 1024)));
        State = new() {
            IsEmpty = providers.Count == 0,
            SourceSummary = $"{providers.Count} synthetic source(s) · actions record intent only",
            Closed = closed,
            Refresh = Callback(() => ReadAsync(() => Browser.RefreshAsync())),
            Retry = Callback(() => ReadAsync(() => Browser.RetryAsync())),
            Back = Callback(BackAsync),
            Browse = new(Browser, new FilesFixtureActions(),
                EventCallback.Factory.Create<FileBrowserSnapshot>(receiver, snapshot => {
                    if (!retired) {
                        State = State with { Snapshot = snapshot };
                    }
                }),
                EventCallback.Factory.Create<FileBrowserItemInvokedEventArgs>(receiver, PreviewAsync),
                EventCallback.Factory.Create<FileBrowserItemActionEventArgs>(receiver, args => RecordAction(args.Action.Label)))
        };
    }

    private EventCallback Callback(Func<Task> callback) => EventCallback.Factory.Create(receiver, callback);

    private async Task ReadAsync(Func<ValueTask> read) {
        int admitted = revision;
        try {
            await read();
        } catch (OperationCanceledException) when (retired || admitted != revision) {
        }
    }

    private async Task PreviewAsync(FileBrowserItemInvokedEventArgs args) {
        if (retired || args.Item.IsContainer) {
            return;
        }
        var provider = providers.SingleOrDefault(item => item.Descriptor.Id == args.Item.Key.SourceId);
        if (provider is null) {
            State = State with { ActivationError = "The synthetic source was removed." };
            return;
        }
        await BackAsync();
        preview = new(provider.Get(args.Item.Key));
        State = State with { Preview = new(preview.Request, preview, composition,
            EventCallback.Factory.Create<CanDoItAll.FileTools.FileInteraction.FileInteractionRequest>(receiver, _ => RecordAction("Open in preferred app"))) };
    }

    private void RecordAction(string name) {
        if (!retired) {
            SimulatedActions++;
            State = State with { ActionFeedback = $"Simulated: {name}. No native action or browser download was performed." };
        }
    }

    public async Task BackAsync() {
        var old = preview;
        preview = null;
        State = State with { Preview = null, ActivationError = null, ActionFeedback = null };
        if (old is not null) {
            await old.DisposeAsync();
        }
    }

    public void HoldNextRead() {
        if (providers.Count == 0) {
            return;
        }
        var gate = new FilesFixtureGate();
        gates.Add(gate);
        SelectedProvider().Gate = gate;
    }

    public void ReleaseReads() {
        foreach (var gate in gates) {
            gate.Release();
        }
        gates.Clear();
    }

    public async Task FailNextReadAsync() {
        if (providers.Count > 0) {
            SelectedProvider().FailNextRead = true;
            await Browser.RefreshAsync();
        }
    }

    private FilesFixtureProvider SelectedProvider() => providers.First(item => item.Descriptor.Id == Browser.Snapshot.CurrentSource?.Id);

    public async Task RemoveSourceAsync() {
        if (providers.Count == 0) {
            return;
        }
        await BackAsync();
        var removed = SelectedProvider();
        removed.Removed = true;
        providers.Remove(removed);
        await Browser.UpdateSourcesAsync(new($"fixture-{++revision}", providers));
        State = State with { IsEmpty = providers.Count == 0, SourceSummary = $"{providers.Count} synthetic source(s) · removed sources cannot open" };
    }

    public async ValueTask DisposeAsync() {
        if (retired) {
            return;
        }
        retired = true;
        ReleaseReads();
        await BackAsync();
        await Browser.DisposeAsync();
    }
}

public sealed class FilesFixtureActions : IFileBrowserHostActionCatalog {
    public ValueTask<IReadOnlyList<FileBrowserActionDescriptor>> GetActionsAsync(FileBrowserHostActionContext context, CancellationToken cancellationToken = default)
        => ValueTask.FromResult<IReadOnlyList<FileBrowserActionDescriptor>>(context.Item.IsContainer ? [] : [
            new(FileBrowserActionIds.Open, "Open in preferred app", "open_in_new"),
            new("fixture:folder", "Show in folder", "folder_open"),
            new(FileBrowserActionIds.Download, "Download", "download")
        ]);
}
