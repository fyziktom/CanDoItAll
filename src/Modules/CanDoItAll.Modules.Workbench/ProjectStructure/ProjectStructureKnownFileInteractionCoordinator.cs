using CanDoItAll.FileTools.FileInteraction;
using CanDoItAll.FileTools.FileInteraction.Components;
using CanDoItAll.FileTools.Integration;
using CanDoItAll.Infrastructure.Storage;

namespace CanDoItAll.Modules.Workbench;

internal sealed class ProjectStructureKnownFileInteractionCoordinator(
    IProjectStructureNodeFileScopeProvider scopeResolver,
    IFileToolsKnownFileActivator knownFileActivator,
    IFileToolsKnownFileSessionFactory sessionFactory,
    IFileToolsKnownFileSessionReleaser sessionReleaser,
    IStorageCatalogService storageCatalog,
    IStorageDriverRegistry storageDrivers)
{
    public async ValueTask<ProjectStructureKnownFileInteraction> OpenAsync(
        Guid projectId,
        string nodeId,
        CancellationToken cancellationToken = default)
    {
        FileToolsKnownFileScope resolved = await scopeResolver.ResolveKnownFileAsync(
            projectId,
            nodeId,
            cancellationToken);
        StorageCatalogRecord? storage = await storageCatalog.GetAsync(
            resolved.Occurrence.StorageId,
            cancellationToken);
        if (storage is null ||
            !storage.IsEnabled ||
            !storageDrivers.TryResolve(storage.ProviderKind, out IStorageDriver driver))
        {
            throw new FileAccessDeniedException(
                FileAccessFailureCode.SourceUnavailable,
                "The authorized file source is unavailable.");
        }

        FileToolsKnownFileIntent intent = ProjectStructureFileInteractionPolicy.ResolveIntent(
            resolved.Occurrence.FileName,
            resolved.Occurrence.MediaType,
            storage,
            driver);
        FileToolsKnownFileActivation activation = await knownFileActivator.ActivateAsync(
            resolved.Scope,
            resolved.Occurrence,
            intent,
            cancellationToken);
        try
        {
            FileToolsKnownFileSession session = await sessionFactory.CreateAsync(
                activation.Request,
                cancellationToken);
            if (intent == FileToolsKnownFileIntent.Edit && session.SaveTarget is null)
            {
                throw new InvalidOperationException(
                    "The editable file interaction does not have an authorized save target.");
            }

            string? mediaType = ProjectStructureFileInteractionPolicy.NormalizeMediaType(
                activation.FileName,
                activation.MediaType);
            FileContentRevision? contentRevision = string.IsNullOrWhiteSpace(session.File.Revision)
                ? null
                : new FileContentRevision(session.File.Revision);
            var request = new FileInteractionRequest(
                session.File,
                activation.FileName,
                FileInteractionMode.View,
                mediaType,
                activation.Size,
                contentRevision);
            return new ProjectStructureKnownFileInteraction(request, session, sessionReleaser);
        }
        catch
        {
            await sessionReleaser.ReleaseAsync(activation.Request.File, CancellationToken.None);
            throw;
        }
    }

}

public sealed class ProjectStructureKnownFileInteraction : IAsyncDisposable
{
    private readonly IFileToolsKnownFileSessionReleaser releaser;
    private bool disposed;

    public ProjectStructureKnownFileInteraction(
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

    public bool CanEdit =>
        Session.Intent == FileToolsKnownFileIntent.Edit &&
        Session.SaveTarget is not null;

    public string HostNotice => ProjectStructureFileInteractionPolicy.ResolveHostNotice(Request, CanEdit);

    public FileInteractionRequest WithMode(FileInteractionMode mode)
    {
        if (mode == FileInteractionMode.Edit && !CanEdit)
        {
            throw new FileAccessDeniedException(
                FileAccessFailureCode.Unsupported,
                "Editing is not available for this file interaction.");
        }

        if (mode == FileInteractionMode.Diff)
        {
            throw new FileAccessDeniedException(
                FileAccessFailureCode.Unsupported,
                "Diff is not registered for this file interaction.");
        }

        return new FileInteractionRequest(
            Request.File,
            Request.FileName,
            mode,
            Request.MediaType,
            Request.Size,
            Request.ContentRevision);
    }

    public async Task SaveAsync(FileInteractionSaveRequestedEventArgs args)
    {
        ArgumentNullException.ThrowIfNull(args);
        if (!CanEdit || Session.SaveTarget is null || args.Request.File != Session.File)
        {
            throw new FileAccessDeniedException(
                FileAccessFailureCode.InvalidHandle,
                "The save request is not authorized for this interaction.");
        }

        FileSaveTargetResult result = await Session.SaveTarget.SaveAsync(args.Request);
        if (result.PersistedRevision is { } revision)
        {
            args.SetPersistedRevision(revision);
        }
    }

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
