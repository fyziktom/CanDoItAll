using CanDoItAll.FileTools.FileBrowser;
using CanDoItAll.FileTools.FileInteraction;
using CanDoItAll.FileTools.Integration;
using CanDoItAll.Modules.Projects;

namespace CanDoItAll.Modules.Resources;

public enum ResourceSourceHealth { Unknown, Healthy, Degraded, Unavailable }
public enum ResourceViewAccess { Loading, Ready, Failed }

public sealed record ResourceBrowseSource(ResourceFileSourceKey Key, ResourceFileSourceClass SourceClass,
    string DisplayName, string Detail, FileToolsSemanticScope Scope, Guid? StorageId, bool IsReadOnly, ResourceSourceHealth? Health);

public sealed record ResourceBrowseCatalog(IReadOnlyList<ResourceBrowseSource> Sources,
    IReadOnlyList<ResourceProjectOption> Projects, string Fingerprint);

public sealed record ResourceBrowsePosition(ResourceFileSourceKey SourceId, ResourceFileSourceClass SourceClass,
    string DisplayName, Guid? ProjectId, string? ProjectName);

public sealed record ResourceFileSelection(ResourceBrowseSource Source, string Revision, FileBrowserItem Item);

public sealed record ResourcePromotionRequest(ResourceFileSelection Selection, ProjectWriteAdmission Project,
    string Name, ResourceSensitivity Sensitivity);

public sealed record ResourcePromotionObservation(Guid ResourceId, bool Created, long? ScopeRevision, string? Warning = null);

public sealed class ResourceBrowseLease(ResourceBrowseSource source, FileBrowserSession browser,
    FileToolsBrowseSourceActionAvailability actions, string revision, Func<ValueTask> release) : IAsyncDisposable {
    private int disposed;
    public ResourceBrowseSource Source { get; } = source;
    public FileBrowserSession Browser { get; } = browser;
    public FileToolsBrowseSourceActionAvailability Actions { get; } = actions;
    public string Revision { get; } = revision;
    public bool IsDisposed => Volatile.Read(ref disposed) != 0;
    public ValueTask DisposeAsync() => Interlocked.Exchange(ref disposed, 1) == 0 ? release() : ValueTask.CompletedTask;
}

public sealed class ResourcePreviewLease(Guid resourceId, FileInteractionRequest request,
    IFileContentSource contentSource, Func<ValueTask> release) : IAsyncDisposable {
    private int disposed;
    public Guid ResourceId { get; } = resourceId;
    public FileInteractionRequest Request { get; } = request;
    public IFileContentSource ContentSource { get; } = contentSource;
    public bool IsDisposed => Volatile.Read(ref disposed) != 0;
    public ValueTask DisposeAsync() => Interlocked.Exchange(ref disposed, 1) == 0 ? release() : ValueTask.CompletedTask;
}

public interface IResourceBrowseOwner {
    bool IsCurrent { get; }
    bool IsLocalLaunchAvailable { get; }
    Task<ResourceBrowseCatalog> LoadAsync(CancellationToken cancellationToken = default);
    ValueTask<ResourceBrowseLease> OpenAsync(ResourceFileSourceKey key, CancellationToken cancellationToken = default);
    ValueTask<ResourcePreviewLease> OpenResourceAsync(Guid resourceId, CancellationToken cancellationToken = default);
    ValueTask<ResourcePromotionObservation> PromoteAsync(ResourcePromotionRequest command);
    ValueTask<FileToolsBrowseItemActionResult> LaunchAsync(ResourceFileSelection selection, FileToolsLocalFileAction action, CancellationToken cancellationToken = default);
    ValueTask<IFileToolsDownloadLease> AuthorizeDownloadAsync(ResourceFileSelection selection, CancellationToken cancellationToken = default);
}
