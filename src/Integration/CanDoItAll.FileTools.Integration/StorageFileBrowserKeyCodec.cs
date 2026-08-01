using System.Text.Json;
using CanDoItAll.FileTools.FileBrowser;

namespace CanDoItAll.FileTools.Integration;

internal enum StorageFileBrowserKeyKind
{
    Root,
    Container,
    Item
}

internal sealed record StorageFileBrowserKeyState(
    StorageFileBrowserKeyKind Kind,
    string Container,
    string? EntryId);

internal sealed class StorageFileBrowserKeyCodec
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly FileBrowserSourceId _sourceId;

    public StorageFileBrowserKeyCodec(FileBrowserSourceId sourceId)
    {
        _sourceId = sourceId;
        Root = Encode(new StorageFileBrowserKeyState(StorageFileBrowserKeyKind.Root, string.Empty, null));
    }

    public FileBrowserItemKey Root { get; }

    public FileBrowserItemKey EncodeContainer(string container)
        => Encode(new StorageFileBrowserKeyState(StorageFileBrowserKeyKind.Container, container, null));

    public FileBrowserItemKey EncodeItem(string container, string entryId, string? revision)
        => Encode(new StorageFileBrowserKeyState(StorageFileBrowserKeyKind.Item, container, entryId), revision);

    public StorageFileBrowserKeyState Decode(FileBrowserItemKey key)
    {
        if (key.SourceId != _sourceId)
        {
            throw InvalidKey();
        }

        try
        {
            string normalized = key.Value.Replace('-', '+').Replace('_', '/');
            normalized += new string('=', (4 - normalized.Length % 4) % 4);
            byte[] payload = Convert.FromBase64String(normalized);
            return JsonSerializer.Deserialize<StorageFileBrowserKeyState>(payload, SerializerOptions)
                ?? throw InvalidKey();
        }
        catch (Exception exception) when (exception is FormatException or JsonException)
        {
            throw InvalidKey();
        }
    }

    private FileBrowserItemKey Encode(StorageFileBrowserKeyState state, string? revision = null)
    {
        string token = Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(state, SerializerOptions))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
        return new FileBrowserItemKey(_sourceId, token, revision);
    }

    private static FileBrowserProviderException InvalidKey()
        => new(new FileBrowserError(
            FileBrowserErrorCode.InvalidLocation,
            "The file browser location is invalid."));
}
