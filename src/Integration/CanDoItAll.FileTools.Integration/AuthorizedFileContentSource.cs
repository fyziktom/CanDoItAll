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

    public AuthorizedFileContentSource For(
        FileReference file,
        FileAccessOperation operation = FileAccessOperation.View)
        => new(coordinator, contextProvider, drivers, options, logger)
        {
            _boundFile = file,
            _operation = operation
        };

    public async ValueTask<FileContentLease> OpenReadAsync(
        FileContentReadRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureBoundFile(request.File);
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
        Stream stream = await driver.OpenReadAsync(authorized.Storage, authorized.Reference, cancellationToken);
        try
        {
            await PositionAsync(stream, request.Offset, cancellationToken);
            long maximumLength = Math.Min(
                request.Length ?? _options.MaximumContentBytes,
                _options.MaximumContentBytes);
            var bounded = new LengthLimitedReadStream(stream, maximumLength);
            StorageContentRevision? revision = driver is IStorageRevisionedContentDriver revisioned
                ? await revisioned.GetRevisionAsync(authorized.Storage, authorized.Reference, cancellationToken)
                : null;
            logger.LogInformation(
                "Authorized file content opened. Handle={HandleHash} Actor={ActorHash} CorrelationId={CorrelationId}.",
                FileAccessLogIdentity.Hash(request.File.Value),
                FileAccessLogIdentity.Hash(context.ActorId.Value),
                context.CorrelationId.Value);
            return new FileContentLease(
                bounded,
                authorized.Reference.ContentType,
                ResolveLength(authorized.Reference.ContentLength, request.Offset, maximumLength),
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
