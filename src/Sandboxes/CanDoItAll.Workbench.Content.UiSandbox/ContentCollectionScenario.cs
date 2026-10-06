using CanDoItAll.FileTools.FileBrowser;
using CanDoItAll.FileTools.FileBrowser.Components;
using CanDoItAll.FileTools.FileInteraction;
using CanDoItAll.FileTools.FileInteraction.Components;
using CanDoItAll.Workbench.Content.UI;
using Microsoft.AspNetCore.Components;

namespace CanDoItAll.Workbench.Content.UiSandbox;

public enum ContentCollectionScenarioKind { Representative, Empty, Large, MissingSource, Loading, OpenError }

public sealed class ContentCollectionScenario : IAsyncDisposable {
    private readonly List<ContentFixtureProvider> providers;
    private readonly FileInteractionComponentComposition composition;
    private readonly object receiver;
    private ContentFixtureContent? preview;
    private bool retired;
    private ContentFixtureGate? heldRead;
    public IFileBrowserSession Browser { get; }
    public ContentFileCollectionState State { get; private set; }
    public int SimulatedActions { get; private set; }

    public ContentCollectionScenario(object receiver, string name, ContentCollectionScenarioKind kind, FileInteractionComponentComposition composition) {
        this.receiver = receiver;
        this.composition = composition;
        providers = kind == ContentCollectionScenarioKind.Empty ? [] : [new("alpha", kind == ContentCollectionScenarioKind.Large ? 220 : 10), new("beta")];
        if (kind == ContentCollectionScenarioKind.MissingSource) {
            providers[0].Removed = true;
        }
        Browser = new FileBrowserSession(new FileBrowserSourceSet("scenario-1", providers), options: new FileBrowserSessionOptions(pageSize: 50,
            defaultSort: new(FileBrowserSortField.ProviderNative, FileBrowserSortDirection.Ascending, FoldersFirst: false),
            retentionMode: FileBrowserStateRetentionMode.Disabled,
            searchBudget: new(maximumContainers: 32, maximumItems: 2_000, maximumDuration: TimeSpan.FromSeconds(5),
                maximumConcurrentRequests: 1, maximumMatches: 200, maximumRetainedBytes: 2L * 1024 * 1024)));
        State = new() {
            OpeningId = Guid.NewGuid(), Title = name, Summary = "Synthetic collection · native actions are represented as intents",
            SourceStatus = $"{providers.Count} sources", IsProjectCollection = true, IncludeSubprojects = true,
            IsLoading = kind == ContentCollectionScenarioKind.Loading,
            OpenError = kind == ContentCollectionScenarioKind.OpenError ? "The synthetic scope is unavailable." : null
        };
        State = State with {
            Retry = Callback(() => { State = State with { IsLoading = false, OpenError = null }; }),
            Back = EventCallback.Factory.Create(receiver, BackAsync),
            IncludeSubprojectsChanged = EventCallback.Factory.Create<bool>(receiver, value => State = State with { IncludeSubprojects = value }),
            OpenRoot = Callback(() => RecordAction("Open root in Explorer")),
            Browse = new(Browser, new ContentFixtureActions(), default,
                EventCallback.Factory.Create<FileBrowserItemInvokedEventArgs>(receiver, PreviewAsync),
                EventCallback.Factory.Create<FileBrowserItemActionEventArgs>(receiver, args => RecordAction(args.Action.Label)))
        };
    }

    private EventCallback Callback(Action callback) => EventCallback.Factory.Create(receiver, callback);

    private async Task PreviewAsync(FileBrowserItemInvokedEventArgs args) {
        if (retired || args.Item.IsContainer) {
            return;
        }
        await BackAsync();
        preview = new(providers.Single(provider => provider.Descriptor.Id == args.Item.Key.SourceId).Get(args.Item.Key));
        State = State with { Preview = new(preview.Request(), preview, composition, 16 * 1024 * 1024,
            EventCallback.Factory.Create<FileInteractionRequest>(receiver, _ => RecordAction("Open in preferred app"))) };
    }

    private void RecordAction(string name) {
        if (!retired) {
            SimulatedActions++;
            State = State with { ActionFeedback = $"Simulated intent: {name}. Native launch and download are validated separately." };
        }
    }

    public async Task BackAsync() {
        var old = preview;
        preview = null;
        State = State with { Preview = null, ActionFeedback = null };
        if (old is not null) {
            await old.DisposeAsync();
        }
    }

    public void HoldNextRead() {
        if (providers.Count > 0) {
            heldRead = new();
            providers[0].Gate = heldRead;
        }
    }

    public void ReleaseRead() => heldRead?.Release();

    public async Task FailNextReadAsync() {
        if (providers.Count > 0) {
            providers[0].FailNextRead = true;
            await Browser.RefreshAsync();
        }
    }

    public async ValueTask DisposeAsync() {
        retired = true;
        ReleaseRead();
        await BackAsync();
        await Browser.DisposeAsync();
    }
}

public sealed class ContentFixtureActions : IFileBrowserHostActionCatalog {
    public ValueTask<IReadOnlyList<FileBrowserActionDescriptor>> GetActionsAsync(FileBrowserHostActionContext context, CancellationToken cancellationToken = default)
        => ValueTask.FromResult<IReadOnlyList<FileBrowserActionDescriptor>>(context.Item.IsContainer ? [] : [
            new(FileBrowserActionIds.Open, "Open in preferred app", "open_in_new"),
            new(FileBrowserActionIds.Download, "Download", "download")
        ]);
}
