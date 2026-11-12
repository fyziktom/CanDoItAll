using CanDoItAll.FileTools.FileInteraction;
using CanDoItAll.Infrastructure.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Buffers;

namespace CanDoItAll.FileTools.Integration;

internal sealed class AuthorizedFileSaveTarget(
    IStorageFileAccessAuthorizationCoordinator coordinator,
    IFileAccessContextProvider contextProvider,
    IStorageDriverRegistry drivers,
    IFileCatalogChangeSink changeSink,
    IOptions<FileAccessHandleOptions> options,
    ILogger<AuthorizedFileSaveTarget> logger) : IFileSaveTarget
{
    private readonly FileAccessHandleOptions _options = ValidateOptions(options.Value);
    private FileReference? _boundFile;
    private Func<CancellationToken, ValueTask>? _authorizeWrite;

    public AuthorizedFileSaveTarget For(FileReference file, Func<CancellationToken, ValueTask>? authorizeWrite = null)
        => new(coordinator, contextProvider, drivers, changeSink, options, logger) { _boundFile = file, _authorizeWrite = authorizeWrite };

    public async ValueTask<FileSaveTargetResult> SaveAsync(
        FileSaveRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureBoundFile(request.File);
        if (_authorizeWrite is not null) {
            await _authorizeWrite(cancellationToken);
        }
        FileAccessContext context = await contextProvider.GetCurrentAsync(cancellationToken);
        AuthorizedStorageFile authorized = await coordinator.ResolveAsync(
            request.File,
            context,
            FileAccessOperation.Edit,
            cancellationToken);
        bool overwrite = request.ExpectedRevision is null;
        if (overwrite)
        {
            await coordinator.ResolveAsync(
                request.File,
                context,
                FileAccessOperation.Overwrite,
                cancellationToken);
        }

        IStorageDriver driver = drivers.Resolve(authorized.Storage.ProviderKind);
        if (driver is not IStorageRevisionedContentDriver revisioned)
        {
            throw new FileAccessDeniedException(
                FileAccessFailureCode.Unsupported,
                "Revision-aware saving is not supported by this storage provider.");
        }

        async ValueTask AuthorizeCommitAsync(CancellationToken token) {
            try {
                if (_authorizeWrite is not null) {
                    await _authorizeWrite(token);
                }
                var currentContext = await contextProvider.GetCurrentAsync(token);
                await coordinator.ResolveAsync(request.File, currentContext, FileAccessOperation.Edit, token);
                if (overwrite) {
                    await coordinator.ResolveAsync(request.File, currentContext, FileAccessOperation.Overwrite, token);
                }
            } catch (Exception failure) when (failure is FileAccessDeniedException or UnauthorizedAccessException) {
                logger.LogWarning("Authorized file write admission refused. Handle={HandleHash} Actor={ActorHash} FailureType={FailureType}.",
                    FileAccessLogIdentity.Hash(request.File.Value), FileAccessLogIdentity.Hash(context.ActorId.Value), failure.GetType().Name);
                throw;
            }
        }

        byte[] content = await ReadAllBoundedAsync(request.Content, _options.MaximumContentBytes, cancellationToken);
        await AuthorizeCommitAsync(cancellationToken);
        try
        {
            StorageRevisionedWriteResult result = await revisioned.ReplaceAsync(
                authorized.Storage,
                new StorageRevisionedWriteRequest(
                    authorized.Reference,
                    content,
                    request.ExpectedRevision is { } expected
                        ? new StorageContentRevision(expected.Value)
                        : null,
                    allowOverwrite: overwrite,
                    authorizeCommit: AuthorizeCommitAsync),
                cancellationToken);
            logger.LogInformation(
                "Authorized file content saved. Handle={HandleHash} Actor={ActorHash} Bytes={ByteCount} CorrelationId={CorrelationId}.",
                FileAccessLogIdentity.Hash(request.File.Value),
                FileAccessLogIdentity.Hash(context.ActorId.Value),
                content.LongLength,
                context.CorrelationId.Value);
            changeSink.PublishScopeChanged(authorized.Scope, authorized.Storage.Id);
            return new FileSaveTargetResult(new FileContentRevision(result.PersistedRevision.Value));
        }
        catch (StorageContentConflictException exception)
        {
            throw new FileSaveConflictException(
                request.File,
                exception.ExpectedRevision is { } expected
                    ? new FileContentRevision(expected.Value)
                    : null,
                exception.ActualRevision is { } actual
                    ? new FileContentRevision(actual.Value)
                    : null);
        }
    }

    private void EnsureBoundFile(FileReference file)
    {
        if (_boundFile is null || _boundFile.Value != file)
        {
            throw new FileAccessDeniedException(
                FileAccessFailureCode.InvalidHandle,
                "The save target is not bound to this file access handle.");
        }
    }

    private static async Task<byte[]> ReadAllBoundedAsync(
        IFileSaveContent content,
        long maximumBytes,
        CancellationToken cancellationToken)
    {
        if (content.Length > maximumBytes)
        {
            throw TooLarge();
        }

        await using Stream source = await content.OpenReadAsync(cancellationToken);
        using var destination = content.Length is > 0 and <= int.MaxValue
            ? new MemoryStream((int)content.Length.Value)
            : new MemoryStream();
        byte[] buffer = ArrayPool<byte>.Shared.Rent(80 * 1024);
        try
        {
            while (true)
            {
                int read = await source.ReadAsync(buffer, cancellationToken);
                if (read == 0)
                {
                    return destination.ToArray();
                }

                if (destination.Length + read > maximumBytes)
                {
                    throw TooLarge();
                }

                await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static FileAccessHandleOptions ValidateOptions(FileAccessHandleOptions options)
    {
        FileAccessHandleOptions.Validate(options);
        return options;
    }

    private static FileAccessDeniedException TooLarge()
        => new(FileAccessFailureCode.ContentTooLarge, "The file exceeds the configured interaction content limit.");
}
