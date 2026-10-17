using CanDoItAll.FileTools.FileBrowser;
using CanDoItAll.FileTools.Integration;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.Projects;
using Microsoft.Extensions.Logging;
using System.Runtime.ExceptionServices;

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
    ProjectWriteAdmission ExpectedProjectAdmission,
    ResourceMetadataContext Context,
    Guid ResourceId,
    ResourceSensitivity Sensitivity = ResourceSensitivity.Normal);

internal sealed record ResourceStorageObjectPromotionResult(StorageObjectResourceWriteResult Outcome, FileCatalogRevision? Revision);

public sealed record ResourceStorageObjectPromotionUiResult(
    string SourceKey,
    Guid ResourceId,
    bool Created,
    long ScopeRevision);

internal sealed record StorageObjectResourceWriteRequest(
    Guid ProjectId,
    string Name,
    ResourceSensitivity Sensitivity,
    StorageObjectResourceConfig Config,
    ProjectWriteAdmission ExpectedProjectAdmission,
    ResourceMetadataContext Context,
    Guid ResourceId);

internal abstract record StorageObjectResourceWriteResult {
    private StorageObjectResourceWriteResult() {
    }

    internal sealed record Accepted(ResourceMetadataSnapshot Snapshot, bool Created,
        ResourceMetadataSecondaryDiagnostic? Diagnostic = null) : StorageObjectResourceWriteResult;
    internal sealed record Rejected(ResourceMetadataRejectionReason Reason) : StorageObjectResourceWriteResult;
    internal sealed record Unconfirmed : StorageObjectResourceWriteResult;
}

internal interface IStorageObjectResourceWriter {
    Task<StorageObjectResourceWriteResult> SaveAsync(StorageObjectResourceWriteRequest request, CancellationToken cancellationToken = default);
}

internal sealed class StorageObjectResourceWriter(ResourceMetadataWriter writer) : IStorageObjectResourceWriter {
    public Task<StorageObjectResourceWriteResult> SaveAsync(StorageObjectResourceWriteRequest request, CancellationToken cancellationToken = default)
        => writer.PromoteAsync(request, cancellationToken);
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

        StorageObjectResourceWriteResult? written = null;
        FileCatalogRevision? revision = null;
        Exception? failure = null;
        try {
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
            try
            {
                written = await writer.SaveAsync(
                    new StorageObjectResourceWriteRequest(
                        command.TargetProjectId,
                        command.ResourceName,
                        command.Sensitivity,
                        config,
                        command.ExpectedProjectAdmission,
                        command.Context,
                        command.ResourceId),
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

            if (written is StorageObjectResourceWriteResult.Accepted accepted) {
                try {
                    revision = accepted.Created
                        ? catalogChanges.PublishScopeChanged(source.Scope, authorized.Storage.Id)
                        : catalogRevisions.Get(source.Scope, authorized.Storage.Id);
                } catch (Exception exception) {
                    written = accepted with { Diagnostic = accepted.Diagnostic ?? ResourceMetadataSecondaryDiagnostic.CatalogPublicationPending };
                    failure = exception;
                }
                try {
                    logger.LogInformation(
                        "Storage object promotion completed. ResourceId={ResourceId} ProjectId={ProjectId} StorageId={StorageId} Created={Created} ScopeRevision={ScopeRevision}.",
                        accepted.Snapshot.Expected.ResourceId, command.TargetProjectId, authorized.Storage.Id, accepted.Created, revision?.Scope);
                } catch (Exception exception) {
                    var current = (StorageObjectResourceWriteResult.Accepted)written;
                    if (current.Diagnostic != ResourceMetadataSecondaryDiagnostic.CleanupPending) {
                        written = current with { Diagnostic = ResourceMetadataSecondaryDiagnostic.LoggingPending };
                    }
                    failure = failure is null ? exception : new AggregateException(failure, exception);
                }
            }
        } catch (Exception exception) {
            failure = exception;
        }
        try {
            await authorizationCoordinator.RevokeAsync(activation.Request.File, CancellationToken.None);
        } catch (Exception exception) {
            failure = failure is null ? exception : new AggregateException(failure, exception);
            if (written is StorageObjectResourceWriteResult.Accepted accepted) {
                written = accepted with { Diagnostic = ResourceMetadataSecondaryDiagnostic.CleanupPending };
            }
        }
        if (failure is not null) {
            try {
                logger.LogWarning("Resource promotion needs review. ResourceId={ResourceId} ProfileId={ProfileId} FailureType={FailureType}.",
                    command.ResourceId, command.Context.DatabaseProfileId, failure.GetType().Name);
            } catch (Exception) {
                if (written is StorageObjectResourceWriteResult.Accepted accepted &&
                    accepted.Diagnostic != ResourceMetadataSecondaryDiagnostic.CleanupPending) {
                    written = accepted with { Diagnostic = ResourceMetadataSecondaryDiagnostic.LoggingPending };
                }
            }
            if (written is not StorageObjectResourceWriteResult.Accepted and not StorageObjectResourceWriteResult.Unconfirmed) {
                ExceptionDispatchInfo.Throw(failure);
            }
        }
        return new(written ?? throw new InvalidOperationException("Resource promotion produced no outcome."), revision);
    }

    private static void ValidateCommand(ResourceStorageObjectPromotionCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.SourceKey.Value) ||
            command.TargetProjectId == Guid.Empty || command.ResourceId == Guid.Empty || command.Context is null ||
            command.ExpectedProjectAdmission is null || command.ExpectedProjectAdmission.ProjectId != command.TargetProjectId ||
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
