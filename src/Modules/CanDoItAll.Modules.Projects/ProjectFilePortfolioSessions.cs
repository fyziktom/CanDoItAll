using CanDoItAll.FileTools.FileBrowser;
using CanDoItAll.FileTools.Integration;

namespace CanDoItAll.Modules.Projects;

public readonly record struct ProjectFilePortfolioRevision
{
    public ProjectFilePortfolioRevision(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        string normalized = value.Trim();
        if (normalized.Length > 128)
        {
            throw new ArgumentOutOfRangeException(nameof(value));
        }

        Value = normalized;
    }

    public string Value { get; }

    public override string ToString() => Value ?? string.Empty;
}

internal sealed record ProjectFilePortfolioSourceSet(
    FileBrowserSourceSet Sources,
    IReadOnlyDictionary<FileBrowserSourceId, FileToolsSemanticScope> SourceScopes,
    ProjectFilePortfolioRevision Revision,
    int ProjectCount,
    FileBrowserSortDescriptor DefaultSort);

public sealed class ProjectFilePortfolioWorkspace : IAsyncDisposable
{
    private IReadOnlyDictionary<FileBrowserSourceId, FileToolsSemanticScope> sourceScopes;
    private bool disposed;

    internal ProjectFilePortfolioWorkspace(
        FileBrowserSession browser,
        ProjectFilePortfolioSourceSet sources)
    {
        Browser = browser ?? throw new ArgumentNullException(nameof(browser));
        ArgumentNullException.ThrowIfNull(sources);
        sourceScopes = sources.SourceScopes;
        Revision = sources.Revision;
        ProjectCount = sources.ProjectCount;
    }

    public IFileBrowserSession Browser { get; }

    public ProjectFilePortfolioRevision Revision { get; private set; }

    public int ProjectCount { get; private set; }

    public int SourceCount => sourceScopes.Count;

    public bool IsDisposed => disposed;

    internal bool TryGetScope(FileBrowserSourceId sourceId, out FileToolsSemanticScope? scope)
        => sourceScopes.TryGetValue(sourceId, out scope);

    internal async ValueTask ReplaceSourcesAsync(
        ProjectFilePortfolioSourceSet sources,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sources);
        if (disposed)
        {
            throw new ObjectDisposedException(nameof(ProjectFilePortfolioWorkspace));
        }

        await Browser.UpdateSourcesAsync(sources.Sources, cancellationToken);
        sourceScopes = sources.SourceScopes;
        Revision = sources.Revision;
        ProjectCount = sources.ProjectCount;
    }

    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        sourceScopes = new Dictionary<FileBrowserSourceId, FileToolsSemanticScope>();
        await Browser.DisposeAsync();
    }
}
