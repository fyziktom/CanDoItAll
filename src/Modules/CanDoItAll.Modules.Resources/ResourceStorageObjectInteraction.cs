using CanDoItAll.FileTools.FileInteraction;
using CanDoItAll.FileTools.Integration;
using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Resources;

internal sealed class ResourceStorageObjectInteractionService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IResourceFileSourceCatalog sourceCatalog,
    IFileToolsKnownFileActivator knownFileActivator,
    IFileToolsKnownFileSessionFactory knownFileSessions,
    IFileToolsKnownFileSessionReleaser knownFileReleaser)
{
    public async ValueTask<ResourceStorageObjectInteraction> OpenAsync(
        Guid resourceId,
        CancellationToken cancellationToken = default)
    {
        if (resourceId == Guid.Empty)
        {
            throw new ArgumentException("A resource identifier is required.", nameof(resourceId));
        }

        await using AppDbContext dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        ProjectResource resource = await dbContext.Set<ProjectResource>()
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == resourceId, cancellationToken)
            ?? throw new InvalidOperationException("The storage-object resource no longer exists.");
        if (!string.Equals(
            resource.ConnectorPluginKey,
            StorageObjectResourceConnectorPlugin.PluginKey,
            StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The selected resource is not a governed storage object.");
        }

        StorageObjectResourceConfig config = StorageObjectResourceConnectorPlugin.Deserialize(resource.ConfigJson);
        if (!ResourceFileSourceKey.TryParse(config.SourceKey, out ResourceFileSourceKey sourceKey))
        {
            throw new InvalidOperationException("The storage-object resource source identity is invalid.");
        }

        ResourceFileSourceDescriptor source = await sourceCatalog.ResolveAsync(sourceKey, cancellationToken);
        if (source.StorageId is Guid sourceStorageId &&
            (sourceStorageId != config.StorageId || source.ProviderKind != config.ProviderKind))
        {
            throw new InvalidOperationException("The storage-object resource source changed. Refresh Resources before reopening it.");
        }

        var occurrence = new FileToolsKnownFileOccurrence(
            config.StorageId,
            StorageObjectResourceConnectorPlugin.ToOccurrenceKind(config.LocatorKind),
            config.Locator,
            config.DisplayName,
            config.ContentType,
            config.ContentLength);
        FileToolsKnownFileActivation activation = await knownFileActivator.ActivateAsync(
            source.Scope,
            occurrence,
            FileToolsKnownFileIntent.ReadOnly,
            cancellationToken);
        try
        {
            FileToolsKnownFileSession session = await knownFileSessions.CreateAsync(
                activation.Request,
                cancellationToken);
            var request = new FileInteractionRequest(
                session.File,
                activation.FileName,
                FileInteractionMode.View,
                activation.MediaType,
                activation.Size);
            return new ResourceStorageObjectInteraction(resource.Id, request, session, knownFileReleaser);
        }
        catch
        {
            await knownFileReleaser.ReleaseAsync(activation.Request.File, CancellationToken.None);
            throw;
        }
    }
}

internal sealed class ResourceStorageObjectInteraction(
    Guid resourceId,
    FileInteractionRequest request,
    FileToolsKnownFileSession session,
    IFileToolsKnownFileSessionReleaser releaser) : IAsyncDisposable
{
    private bool disposed;

    public Guid ResourceId { get; } = resourceId;

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
