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
    IReadOnlyList<ResourceProjectOption> Projects, string Fingerprint, ResourceMetadataContext Context);

public sealed record ResourceBrowsePosition(ResourceFileSourceKey SourceId, ResourceFileSourceClass SourceClass,
    string DisplayName, Guid? ProjectId, string? ProjectName);

public sealed record ResourceFileSelection(ResourceBrowseSource Source, string Revision, FileBrowserItem Item);

public sealed record ResourcePromotionRequest {
    public ResourcePromotionRequest(ResourceFileSelection selection, ProjectWriteAdmission project,
        string name, ResourceSensitivity sensitivity, ResourceMetadataContext context, Guid resourceId) {
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentOutOfRangeException.ThrowIfEqual(resourceId, Guid.Empty);
        ResourceMetadataLimits.RequireText(name, ResourceMetadataLimits.ScalarBytes, nameof(name));
        if (!Enum.IsDefined(sensitivity) || context.DatabaseProfileId != project.DatabaseProfileId) {
            throw new ArgumentException("The promotion target profile or sensitivity is invalid.");
        }
        Selection = selection;
        Project = project;
        Name = name;
        Sensitivity = sensitivity;
        Context = context;
        ResourceId = resourceId;
    }

    public ResourceFileSelection Selection { get; }
    public ProjectWriteAdmission Project { get; }
    public string Name { get; }
    public ResourceSensitivity Sensitivity { get; }
    public ResourceMetadataContext Context { get; }
    public Guid ResourceId { get; }
}

public abstract record ResourcePromotionObservation {
    private ResourcePromotionObservation() {
    }

    public sealed record Created : ResourcePromotionObservation {
        public Created(ResourceMetadataSnapshot snapshot, long? scopeRevision,
            ResourceMetadataSecondaryDiagnostic? diagnostic = null) {
            ArgumentNullException.ThrowIfNull(snapshot);
            ResourceMetadataLimits.RequireDiagnostic(diagnostic);
            Snapshot = snapshot;
            ScopeRevision = scopeRevision;
            Diagnostic = diagnostic;
        }

        public ResourceMetadataSnapshot Snapshot { get; }
        public long? ScopeRevision { get; }
        public ResourceMetadataSecondaryDiagnostic? Diagnostic { get; }
    }

    public sealed record Reused : ResourcePromotionObservation {
        public Reused(ResourceMetadataSnapshot snapshot, long? scopeRevision,
            ResourceMetadataSecondaryDiagnostic? diagnostic = null) {
            ArgumentNullException.ThrowIfNull(snapshot);
            ResourceMetadataLimits.RequireDiagnostic(diagnostic);
            Snapshot = snapshot;
            ScopeRevision = scopeRevision;
            Diagnostic = diagnostic;
        }

        public ResourceMetadataSnapshot Snapshot { get; }
        public long? ScopeRevision { get; }
        public ResourceMetadataSecondaryDiagnostic? Diagnostic { get; }
    }

    public sealed record Rejected : ResourcePromotionObservation {
        public Rejected(ResourceMetadataRejectionReason reason, string? message = null) {
            ResourceMetadataLimits.RequireRejection(reason);
            Reason = reason;
            Message = message;
        }

        public ResourceMetadataRejectionReason Reason { get; }
        public string? Message { get; }
    }

    public sealed record Unconfirmed : ResourcePromotionObservation {
        public Unconfirmed(ResourcePromotionRequest command) {
            ArgumentNullException.ThrowIfNull(command);
            Command = command;
        }

        public ResourcePromotionRequest Command { get; }
    }
}

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
