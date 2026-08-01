using System.Security.Cryptography;
using System.Text;

namespace CanDoItAll.Infrastructure.Storage;

internal sealed record FileSystemStorageBrowseCursorState(
    Guid StorageId,
    string ContainerFingerprint,
    int Offset,
    int PageSize,
    StorageBrowseSortField SortField,
    StorageBrowseSortDirection SortDirection,
    StorageBrowseMetadataField Metadata,
    long DirectoryVersion);

internal sealed class FileSystemStorageBrowseCursorCodec
{
    private const string InvalidCursorMessage = "The filesystem browse cursor is invalid or no longer trusted.";
    private readonly StorageBrowseCursorProtector _protector = new();

    public StorageBrowseCursor Encode(FileSystemStorageBrowseCursorState state)
        => _protector.Encode(state);

    public FileSystemStorageBrowseCursorState Decode(StorageBrowseCursor cursor)
        => _protector.Decode<FileSystemStorageBrowseCursorState>(cursor, InvalidCursorMessage);

    public static string CreateContainerFingerprint(StorageBrowseContainer container)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(container.Key));
        return Convert.ToHexStringLower(hash);
    }
}
