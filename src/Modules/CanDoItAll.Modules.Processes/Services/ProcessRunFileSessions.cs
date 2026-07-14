using CanDoItAll.FileTools.FileBrowser;
using CanDoItAll.FileTools.FileInteraction;
using CanDoItAll.FileTools.Integration;

namespace CanDoItAll.Modules.Processes;

internal sealed class ProcessRunFileWorkspace : IAsyncDisposable
{
    private IReadOnlyDictionary<FileBrowserSourceId, FileToolsSemanticScope> sourceScopes;
    private IReadOnlyDictionary<FileBrowserSourceId, FileToolsBrowseSourceActionAvailability> sourceActions;
    private bool disposed;

    public ProcessRunFileWorkspace(
        Guid runId,
        IFileBrowserSession browser,
        IReadOnlyDictionary<FileBrowserSourceId, FileToolsSemanticScope> sourceScopes,
        IReadOnlyDictionary<FileBrowserSourceId, FileToolsBrowseSourceActionAvailability> sourceActions,
        string revision)
    {
        if (runId == Guid.Empty)
        {
            throw new ArgumentException("A process run identifier is required.", nameof(runId));
        }

        RunId = runId;
        Browser = browser ?? throw new ArgumentNullException(nameof(browser));
        this.sourceScopes = sourceScopes ?? throw new ArgumentNullException(nameof(sourceScopes));
        this.sourceActions = sourceActions ?? throw new ArgumentNullException(nameof(sourceActions));
        if (!sourceScopes.Keys.ToHashSet().SetEquals(sourceActions.Keys))
        {
            throw new ArgumentException("Every process-run file source must declare host action availability.", nameof(sourceActions));
        }
        ArgumentException.ThrowIfNullOrWhiteSpace(revision);
        Revision = revision;
    }

    public Guid RunId { get; }

    public IFileBrowserSession Browser { get; }

    public string Revision { get; }

    public int SourceCount => sourceScopes.Count;

    public bool IsDisposed => disposed;

    public bool TryGetScope(FileBrowserSourceId sourceId, out FileToolsSemanticScope? scope)
        => sourceScopes.TryGetValue(sourceId, out scope);

    public FileToolsBrowseSourceActionAvailability GetActionAvailability(FileBrowserSourceId sourceId)
        => sourceActions.GetValueOrDefault(sourceId);

    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        sourceScopes = new Dictionary<FileBrowserSourceId, FileToolsSemanticScope>();
        sourceActions = new Dictionary<FileBrowserSourceId, FileToolsBrowseSourceActionAvailability>();
        await Browser.DisposeAsync();
    }
}

internal sealed class ProcessRunFileInteraction(
    FileInteractionRequest request,
    FileToolsKnownFileSession session,
    IFileToolsKnownFileSessionReleaser releaser) : IAsyncDisposable
{
    private bool disposed;

    public FileInteractionRequest Request { get; } = request ?? throw new ArgumentNullException(nameof(request));

    public FileToolsKnownFileSession Session { get; } = session ?? throw new ArgumentNullException(nameof(session));

    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        await releaser.ReleaseAsync(Session.File, CancellationToken.None);
    }
}
