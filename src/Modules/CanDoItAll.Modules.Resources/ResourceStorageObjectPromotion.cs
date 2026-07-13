using CanDoItAll.FileTools.FileBrowser;
using CanDoItAll.FileTools.Integration;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.Resources;

internal enum ResourcePromotionFailureCode
{
    InvalidRequest,
    SourceChanged,
    SelectionChanged,
    Unauthorized,
    TargetUnavailable,
    PersistenceFailed
}

internal sealed class ResourcePromotionException(
    ResourcePromotionFailureCode code,
    string message,
    Exception? innerException = null) : Exception(message, innerException)
{
    public ResourcePromotionFailureCode Code { get; } = code;
}

internal sealed record ResourceStorageObjectPromotionCommand(
    ResourceFileSourceKey SourceKey,
    FileBrowserItemKey ItemKey,
    Guid TargetProjectId,
    string ResourceName,
    ResourceSensitivity Sensitivity = ResourceSensitivity.Normal);

internal sealed record ResourceStorageObjectPromotionResult(
    Guid ResourceId,
    bool Created,
    FileCatalogRevision Revision);

public sealed record ResourceStorageObjectPromotionUiResult(
    string SourceKey,
    Guid ResourceId,
    bool Created,
    long ScopeRevision);

internal sealed record StorageObjectResourceWriteRequest(
    Guid ProjectId,
    string Name,
    ResourceSensitivity Sensitivity,
    StorageObjectResourceConfig Config);

internal sealed record StorageObjectResourceWriteResult(Guid ResourceId, bool Created);

internal interface IStorageObjectResourceWriter
{
    Task<StorageObjectResourceWriteResult> SaveAsync(
        StorageObjectResourceWriteRequest request,
        CancellationToken cancellationToken = default);
}

internal sealed class StorageObjectResourceWriter(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IClock clock) : IStorageObjectResourceWriter
{
    public async Task<StorageObjectResourceWriteResult> SaveAsync(
        StorageObjectResourceWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ProjectId == Guid.Empty || string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ResourcePromotionException(
                ResourcePromotionFailureCode.InvalidRequest,
                "Select a target project and provide a resource name.");
        }

        string name = request.Name.Trim();
        if (name.Length > 200)
        {
            throw new ResourcePromotionException(
                ResourcePromotionFailureCode.InvalidRequest,
                "The resource name is longer than 200 characters.");
        }

        string configJson = StorageObjectResourceConnectorPlugin.Serialize(request.Config);
        await using AppDbContext dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        bool projectExists = await dbContext.Set<Project>()
            .AsNoTracking()
            .AnyAsync(project => project.Id == request.ProjectId, cancellationToken);
        if (!projectExists)
        {
            throw new ResourcePromotionException(
                ResourcePromotionFailureCode.TargetUnavailable,
                "The target project no longer exists.");
        }

        ProjectResource? existing = await dbContext.Set<ProjectResource>()
            .AsNoTracking()
            .SingleOrDefaultAsync(
                resource => resource.ProjectId == request.ProjectId &&
                            resource.ConnectorPluginKey == StorageObjectResourceConnectorPlugin.PluginKey &&
                            resource.ConfigJson == configJson,
                cancellationToken);
        if (existing is not null)
        {
            return new StorageObjectResourceWriteResult(existing.Id, false);
        }

        DateTimeOffset now = clock.GetUtcNow();
        var entity = new ProjectResource
        {
            ProjectId = request.ProjectId,
            ResourceKind = null,
            Name = name,
            ConnectorPluginKey = StorageObjectResourceConnectorPlugin.PluginKey,
            ConfigSchemaVersion = StorageObjectResourceConnectorPlugin.SchemaVersion,
            LocationOrIdentifier = StorageObjectResourceConnectorPlugin.BuildStableLocation(request.Config),
            ConfigJson = configJson,
            LinkedSecretIdsJson = "[]",
            ValidationStatus = ResourceValidationStatus.Valid,
            Sensitivity = request.Sensitivity,
            SupportsPreview = true,
            SupportsIndexing = false,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        await dbContext.Set<ProjectResource>().AddAsync(entity, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new StorageObjectResourceWriteResult(entity.Id, true);
    }
}

internal sealed class ResourceStorageObjectPromotionService(
    IResourceFileSourceCatalog sourceCatalog,
    IFileToolsBrowseItemActivator itemActivator,
    IFileAccessContextProvider accessContextProvider,
    IStorageFileAccessAuthorizationCoordinator authorizationCoordinator,
    IStorageObjectResourceWriter writer,
    IFileCatalogChangeSink catalogChanges,
    IFileCatalogRevisionReader catalogRevisions,
    ILogger<ResourceStorageObjectPromotionService> logger)
{
    public async ValueTask<ResourceStorageObjectPromotionResult> PromoteAsync(
        ResourceStorageObjectPromotionCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ValidateCommand(command);
        ResourceFileSourceDescriptor source;
        try
        {
            source = await sourceCatalog.ResolveAsync(command.SourceKey, cancellationToken);
        }
        catch (InvalidOperationException exception)
        {
            throw new ResourcePromotionException(
                ResourcePromotionFailureCode.SourceChanged,
                "The selected file source changed. Refresh Resources browse and try again.",
                exception);
        }

        FileToolsKnownFileActivation activation;
        try
        {
            activation = await itemActivator.ActivateAsync(
                source.Scope,
                command.ItemKey,
                FileToolsKnownFileIntent.ReadOnly,
                cancellationToken);
        }
        catch (FileBrowserProviderException exception)
        {
            throw new ResourcePromotionException(
                ResourcePromotionFailureCode.SelectionChanged,
                "The selected storage object changed. Refresh the file source and select it again.",
                exception);
        }
        catch (FileAccessDeniedException exception)
        {
            throw new ResourcePromotionException(
                ResourcePromotionFailureCode.Unauthorized,
                "The selected storage object is not authorized for the current actor.",
                exception);
        }

        try
        {
            FileAccessContext context = await accessContextProvider.GetCurrentAsync(cancellationToken);
            AuthorizedStorageFile authorized;
            try
            {
                authorized = await authorizationCoordinator.ResolveAsync(
                    activation.Request.File,
                    context,
                    FileAccessOperation.View,
                    cancellationToken);
            }
            catch (FileAccessDeniedException exception)
            {
                throw new ResourcePromotionException(
                    ResourcePromotionFailureCode.Unauthorized,
                    "The selected storage object is not authorized for the current actor.",
                    exception);
            }

            EnsureCurrentSelection(source, activation, authorized);
            var config = new StorageObjectResourceConfig(
                source.Key.Value,
                authorized.Storage.Id,
                authorized.Storage.ProviderKind,
                authorized.Reference.LocatorKind,
                authorized.Reference.Locator,
                activation.FileName,
                activation.MediaType ?? authorized.Reference.ContentType,
                activation.Size ?? authorized.Reference.ContentLength);
            StorageObjectResourceWriteResult written;
            try
            {
                written = await writer.SaveAsync(
                    new StorageObjectResourceWriteRequest(
                        command.TargetProjectId,
                        command.ResourceName,
                        command.Sensitivity,
                        config),
                    cancellationToken);
            }
            catch (ResourcePromotionException)
            {
                throw;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw new ResourcePromotionException(
                    ResourcePromotionFailureCode.PersistenceFailed,
                    "The storage object could not be saved as a resource.",
                    exception);
            }

            FileCatalogRevision revision = written.Created
                ? catalogChanges.PublishScopeChanged(source.Scope, authorized.Storage.Id)
                : catalogRevisions.Get(source.Scope, authorized.Storage.Id);
            logger.LogInformation(
                "Storage object promotion completed. ResourceId={ResourceId} ProjectId={ProjectId} SourceKey={SourceKey} StorageId={StorageId} ProviderKind={ProviderKind} Created={Created} ScopeRevision={ScopeRevision}.",
                written.ResourceId,
                command.TargetProjectId,
                source.Key.Value,
                authorized.Storage.Id,
                authorized.Storage.ProviderKind,
                written.Created,
                revision.Scope);
            return new ResourceStorageObjectPromotionResult(written.ResourceId, written.Created, revision);
        }
        finally
        {
            try
            {
                await authorizationCoordinator.RevokeAsync(activation.Request.File, CancellationToken.None);
            }
            catch (Exception exception)
            {
                logger.LogError(
                    "Storage-object promotion handle cleanup failed. SourceKey={SourceKey} FailureType={FailureType}.",
                    source.Key.Value,
                    exception.GetType().Name);
            }
        }
    }

    private static void ValidateCommand(ResourceStorageObjectPromotionCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.SourceKey.Value) ||
            command.TargetProjectId == Guid.Empty ||
            string.IsNullOrWhiteSpace(command.ResourceName) ||
            !Enum.IsDefined(command.Sensitivity))
        {
            throw new ResourcePromotionException(
                ResourcePromotionFailureCode.InvalidRequest,
                "Select a file source, storage object, target project, and resource name.");
        }
    }

    private static void EnsureCurrentSelection(
        ResourceFileSourceDescriptor source,
        FileToolsKnownFileActivation activation,
        AuthorizedStorageFile authorized)
    {
        bool sourceMatches = authorized.Scope.Kind == source.Scope.Kind &&
                             authorized.Scope.Id == source.Scope.Id &&
                             activation.Request.Scope.Kind == source.Scope.Kind &&
                             activation.Request.Scope.Id == source.Scope.Id;
        bool storageMatches = source.StorageId is not Guid sourceStorageId ||
                              sourceStorageId == authorized.Storage.Id;
        bool referenceMatches = authorized.Reference.StorageId == authorized.Storage.Id &&
                                authorized.Reference.ProviderKind == authorized.Storage.ProviderKind;
        if (!sourceMatches || !storageMatches || !referenceMatches)
        {
            throw new ResourcePromotionException(
                ResourcePromotionFailureCode.SelectionChanged,
                "The selected storage object no longer belongs to the current file source.");
        }

        _ = StorageObjectResourceConnectorPlugin.Serialize(new StorageObjectResourceConfig(
            source.Key.Value,
            authorized.Storage.Id,
            authorized.Storage.ProviderKind,
            authorized.Reference.LocatorKind,
            authorized.Reference.Locator,
            activation.FileName,
            activation.MediaType ?? authorized.Reference.ContentType,
            activation.Size ?? authorized.Reference.ContentLength));
    }
}
