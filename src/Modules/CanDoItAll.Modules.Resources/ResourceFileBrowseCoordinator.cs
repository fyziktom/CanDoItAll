using CanDoItAll.FileTools.FileBrowser;
using CanDoItAll.FileTools.Integration;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.Resources;

internal sealed class ResourceFileBrowseCoordinator(
    IResourceFileSourceCatalog sourceCatalog,
    IFileToolsBrowseSessionFactory browseSessions,
    ILogger<ResourceFileBrowseCoordinator> logger)
{
    private static readonly FileBrowserSearchBudget SearchBudget = new(
        maximumContainers: 32,
        maximumItems: 2_000,
        maximumDuration: TimeSpan.FromSeconds(5),
        maximumConcurrentRequests: 1,
        maximumMatches: 200,
        maximumRetainedBytes: 2L * 1024 * 1024);

    public async ValueTask<ResourceFileBrowseWorkspace> OpenAsync(
        ResourceFileSourceKey sourceKey,
        CancellationToken cancellationToken = default)
    {
        ResourceFileSourceDescriptor source = await sourceCatalog.ResolveAsync(sourceKey, cancellationToken);
        FileToolsBrowseSession session = await browseSessions.CreateAsync(source.Scope, cancellationToken);
        if (session.Providers.Count != 1)
        {
            throw new FileBrowserProviderException(new FileBrowserError(
                FileBrowserErrorCode.CorruptProviderResponse,
                "A Resources browse source must resolve to exactly one file provider."));
        }

        var sourceSet = new FileBrowserSourceSet(session.Revision.Value, session.Providers);
        var options = new FileBrowserSessionOptions(
            pageSize: 50,
            defaultSort: session.DefaultSort,
            retentionMode: FileBrowserStateRetentionMode.Disabled,
            searchBudget: SearchBudget);
        var browser = new FileBrowserSession(sourceSet, options: options);
        logger.LogInformation(
            "Resources file source opened. SourceClass={SourceClass} SourceKey={SourceKey} Revision={Revision} SessionRetention={SessionRetention}.",
            source.SourceClass,
            source.Key.Value,
            session.Revision.Value,
            FileBrowserStateRetentionMode.Disabled);
        return new ResourceFileBrowseWorkspace(source, browser, session.Revision.Value);
    }
}

internal sealed class ResourceFileBrowseWorkspace(
    ResourceFileSourceDescriptor source,
    FileBrowserSession browser,
    string revision) : IAsyncDisposable
{
    private bool disposed;

    public ResourceFileSourceDescriptor Source { get; } = source;

    public FileBrowserSession Browser { get; } = browser;

    public string Revision { get; } = revision;

    public bool IsDisposed => disposed;

    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        await Browser.DisposeAsync();
    }
}
