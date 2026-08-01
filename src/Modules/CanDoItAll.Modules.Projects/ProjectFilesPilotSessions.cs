using CanDoItAll.FileTools.FileBrowser;
using CanDoItAll.FileTools.FileInteraction;
using CanDoItAll.FileTools.Integration;

namespace CanDoItAll.Modules.Projects;

public sealed class ProjectFilesPilotWorkspace : IAsyncDisposable
{
    private bool disposed;
    private IReadOnlyDictionary<FileBrowserSourceId, FileToolsBrowseSourceActionAvailability> sourceActions;

    public ProjectFilesPilotWorkspace(
        Guid projectId,
        string projectName,
        FileToolsSemanticScope scope,
        IFileBrowserSession browser,
        IReadOnlyDictionary<FileBrowserSourceId, FileToolsBrowseSourceActionAvailability> sourceActions)
    {
        if (projectId == Guid.Empty)
        {
            throw new ArgumentException("A project identifier is required.", nameof(projectId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(projectName);
        ProjectId = projectId;
        ProjectName = projectName.Trim();
        Scope = scope ?? throw new ArgumentNullException(nameof(scope));
        Browser = browser ?? throw new ArgumentNullException(nameof(browser));
        this.sourceActions = sourceActions ?? throw new ArgumentNullException(nameof(sourceActions));
    }

    public Guid ProjectId { get; }

    public string ProjectName { get; }

    public FileToolsSemanticScope Scope { get; }

    public IFileBrowserSession Browser { get; }

    public bool IsDisposed => disposed;

    internal FileToolsBrowseSourceActionAvailability GetActionAvailability(FileBrowserSourceId sourceId)
        => sourceActions.GetValueOrDefault(sourceId);

    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        sourceActions = new Dictionary<FileBrowserSourceId, FileToolsBrowseSourceActionAvailability>();
        await Browser.DisposeAsync();
    }
}

public sealed class ProjectFilesPilotInteraction : IAsyncDisposable
{
    private readonly IFileToolsKnownFileSessionReleaser releaser;
    private bool disposed;

    public ProjectFilesPilotInteraction(
        FileInteractionRequest request,
        FileToolsKnownFileSession session,
        IFileToolsKnownFileSessionReleaser releaser)
    {
        Request = request ?? throw new ArgumentNullException(nameof(request));
        Session = session ?? throw new ArgumentNullException(nameof(session));
        this.releaser = releaser ?? throw new ArgumentNullException(nameof(releaser));
    }

    public FileInteractionRequest Request { get; }

    public FileToolsKnownFileSession Session { get; }

    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        await releaser.ReleaseAsync(Session.File);
    }
}
