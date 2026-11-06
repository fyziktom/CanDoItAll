using CanDoItAll.FileTools.FileInteraction;
using CanDoItAll.Infrastructure.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Buffers;
using System.Security.Cryptography;

namespace CanDoItAll.FileTools.Integration;

internal sealed class AuthorizedFileContentSource(
    IStorageFileAccessAuthorizationCoordinator coordinator,
    IFileAccessContextProvider contextProvider,
    IStorageDriverRegistry drivers,
    IOptions<FileAccessHandleOptions> options,
    ILogger<AuthorizedFileContentSource> logger) : IFileContentSource
{
    private readonly FileAccessHandleOptions _options = ValidateOptions(options.Value);
    private FileReference? _boundFile;
    private FileAccessOperation _operation = FileAccessOperation.View;
    private Func<CancellationToken, ValueTask>? _authorizeRead;

    public AuthorizedFileContentSource For(
        FileReference file,
        FileAccessOperation operation = FileAccessOperation.View,
        Func<CancellationToken, ValueTask>? authorizeRead = null)
        => new(coordinator, contextProvider, drivers, options, logger)
        {
            _boundFile = file,
            _operation = operation,
            _authorizeRead = authorizeRead
        };

    public async ValueTask<FileContentLease> OpenReadAsync(
        FileContentReadRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureBoundFile(request.File);
        if (_authorizeRead is not null) {
            await _authorizeRead(cancellationToken);
        }
        if (request.Offset > _options.MaximumContentBytes)
        {
            throw TooLarge();
        }

        FileAccessContext context = await contextProvider.GetCurrentAsync(cancellationToken);
        AuthorizedStorageFile authorized = await coordinator.ResolveAsync(
            request.File,
            context,
            _operation,
            cancellationToken);
        if (authorized.Reference.ContentLength > _options.MaximumContentBytes)
        {
            throw TooLarge();
        }

        IStorageDriver driver = drivers.Resolve(authorized.Storage.ProviderKind);
        var revisioned = driver as IStorageRevisionedContentDriver;
        StorageContentRevision? revision = revisioned is null ? null
            : await revisioned.GetRevisionAsync(authorized.Storage, authorized.Reference, cancellationToken);
        Stream stream = await driver.OpenReadAsync(authorized.Storage, authorized.Reference, cancellationToken);
        try
        {
            if (stream.CanSeek && stream.Length > _options.MaximumContentBytes) {
                throw TooLarge();
            }
            await PositionAsync(stream, request.Offset, cancellationToken);
            long maximumLength = Math.Min(
                request.Length ?? _options.MaximumContentBytes,
                _options.MaximumContentBytes);
            var bounded = new LengthLimitedReadStream(stream, maximumLength);
            Stream content = bounded;
            long? length = ResolveLength(authorized.Reference.ContentLength, request.Offset, maximumLength);
            if (revisioned is not null || _authorizeRead is not null) {
                var snapshot = new MemoryStream();
                try {
                    await bounded.CopyToAsync(snapshot, cancellationToken);
                    if (_authorizeRead is not null) {
                        await _authorizeRead(cancellationToken);
                    }
                    if (snapshot.Length == _options.MaximumContentBytes &&
                        await stream.ReadAsync(new byte[1], cancellationToken) > 0) {
                        throw TooLarge();
                    }
                    if (revisioned is not null && revision != await revisioned.GetRevisionAsync(authorized.Storage, authorized.Reference, cancellationToken)) {
                        throw new FileAccessDeniedException(FileAccessFailureCode.SourceUnavailable,
                            "The file changed while it was being read. Reopen it to load a coherent revision.");
                    }
                    var containsWholeFile = request.Offset == 0 && (request.Length is null || snapshot.Length < maximumLength ||
                        stream.CanSeek && snapshot.Length == stream.Length || snapshot.Length == _options.MaximumContentBytes);
                    if (containsWholeFile && revision is { IsContentHash: true } digest &&
                        snapshot.TryGetBuffer(out var contents) && StorageContentRevision.ForContent(contents.AsSpan()) != digest) {
                        throw new FileAccessDeniedException(FileAccessFailureCode.SourceUnavailable,
                            "The opened bytes no longer match this file revision. Reopen the file before editing.");
                    }
                    await bounded.DisposeAsync();
                    var currentContext = await contextProvider.GetCurrentAsync(cancellationToken);
                    await coordinator.ResolveAsync(request.File, currentContext, _operation, cancellationToken);
                    if (_authorizeRead is not null) {
                        await _authorizeRead(cancellationToken);
                    }
                    snapshot.Position = 0;
                    content = snapshot;
                    length = snapshot.Length;
                } catch {
                    await snapshot.DisposeAsync();
                    throw;
                }
            }
            logger.LogInformation(
                "Authorized file content opened. Handle={HandleHash} Actor={ActorHash} CorrelationId={CorrelationId}.",
                FileAccessLogIdentity.Hash(request.File.Value),
                FileAccessLogIdentity.Hash(context.ActorId.Value),
                context.CorrelationId.Value);
            return new FileContentLease(
                content,
                authorized.Reference.ContentType,
                length,
                revision is { } value ? new FileContentRevision(value.Value) : null);
        }
        catch
        {
            await stream.DisposeAsync();
            throw;
        }
    }

    private void EnsureBoundFile(FileReference file)
    {
        if (_boundFile is null || _boundFile.Value != file)
        {
            throw new FileAccessDeniedException(
                FileAccessFailureCode.InvalidHandle,
                "The content source is not bound to this file access handle.");
        }
    }

    private static async Task PositionAsync(Stream stream, long offset, CancellationToken cancellationToken)
    {
        if (stream.CanSeek)
        {
            if (offset > stream.Length)
            {
                throw new EndOfStreamException("The requested file offset is outside the content.");
            }

            stream.Position = offset;
            return;
        }

        byte[] buffer = ArrayPool<byte>.Shared.Rent(80 * 1024);
        try
        {
            long remaining = offset;
            while (remaining > 0)
            {
                int read = await stream.ReadAsync(
                    buffer.AsMemory(0, (int)Math.Min(buffer.Length, remaining)),
                    cancellationToken);
                if (read == 0)
                {
                    throw new EndOfStreamException("The requested file offset is outside the content.");
                }

                remaining -= read;
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static long? ResolveLength(long? contentLength, long offset, long maximumLength)
        => contentLength.HasValue
            ? Math.Min(Math.Max(0, contentLength.Value - offset), maximumLength)
            : null;

    private static FileAccessHandleOptions ValidateOptions(FileAccessHandleOptions options)
    {
        FileAccessHandleOptions.Validate(options);
        return options;
    }

    private static FileAccessDeniedException TooLarge()
        => new(FileAccessFailureCode.ContentTooLarge, "The file exceeds the configured interaction content limit.");
}

internal static class FileAccessLogIdentity
{
    public static string Hash(string value)
        => Convert.ToHexStringLower(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value)))[..12];
}
