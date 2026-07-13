using System.Security.Cryptography;
using System.Text;

namespace CanDoItAll.Infrastructure.Storage;

internal sealed record RemoteStorageBrowseCursorState(
    StorageProviderKind ProviderKind,
    Guid StorageId,
    string ContainerFingerprint,
    int Offset,
    int PageSize,
    StorageBrowseMetadataField Metadata,
    string? SourceRevision);

internal sealed class RemoteStorageBrowseCursorCodec
{
    private const string InvalidCursorMessage = "The remote storage browse cursor is invalid.";
    private readonly StorageBrowseCursorProtector _protector = new();

    public StorageBrowseCursor Encode(RemoteStorageBrowseCursorState state)
        => _protector.Encode(state);

    public RemoteStorageBrowseCursorState Decode(StorageBrowseCursor cursor)
        => _protector.Decode<RemoteStorageBrowseCursorState>(cursor, InvalidCursorMessage);

    public static string Fingerprint(StorageBrowseContainer container)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(container.Key)));
}
