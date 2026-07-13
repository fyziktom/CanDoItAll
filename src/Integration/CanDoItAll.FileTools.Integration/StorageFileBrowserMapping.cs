using CanDoItAll.FileTools.FileBrowser;
using CanDoItAll.Infrastructure.Storage;

namespace CanDoItAll.FileTools.Integration;

internal static class StorageFileBrowserMapping
{
    public static FileBrowserItem MapItem(
        StorageFileBrowserKeyCodec keyCodec,
        FileBrowserItemKey parentKey,
        StorageBrowseContainer parent,
        StorageBrowseEntry entry,
        StorageBrowseCompleteness completeness)
    {
        FileBrowserItemKind kind = entry.Kind switch
        {
            StorageBrowseEntryKind.Container => FileBrowserItemKind.Container,
            StorageBrowseEntryKind.File => FileBrowserItemKind.File,
            StorageBrowseEntryKind.Link => FileBrowserItemKind.Link,
            _ => throw new FileBrowserProviderException(new FileBrowserError(
                FileBrowserErrorCode.CorruptProviderResponse,
                "The storage provider returned an unknown entry kind."))
        };
        FileBrowserItemKey key = kind == FileBrowserItemKind.Container
            ? keyCodec.EncodeContainer(entry.Id.Value)
            : keyCodec.EncodeItem(parent.Key, entry.Id.Value, revision: null);
        FileBrowserItemCapabilities capabilities = FileBrowserItemCapabilities.Select;
        if (kind == FileBrowserItemKind.Container &&
            entry.Capabilities.HasFlag(StorageBrowseEntryCapability.Browse))
        {
            capabilities |= FileBrowserItemCapabilities.Navigate;
        }
        else if (kind == FileBrowserItemKind.File &&
                 entry.Capabilities.HasFlag(StorageBrowseEntryCapability.Read))
        {
            capabilities |= FileBrowserItemCapabilities.Preview;
        }

        FileBrowserMetadataFields exact = FileBrowserMetadataFields.Name |
                                          FileBrowserMetadataFields.DisplayPath |
                                          FileBrowserMetadataFields.Kind |
                                          FileBrowserMetadataFields.ChildState;
        if (entry.Size.HasValue)
        {
            exact |= FileBrowserMetadataFields.Size;
        }

        if (entry.MediaType is not null)
        {
            exact |= FileBrowserMetadataFields.MediaType;
        }

        if (entry.CreatedAtUtc.HasValue || entry.ModifiedAtUtc.HasValue)
        {
            exact |= FileBrowserMetadataFields.Timestamps;
        }

        return new FileBrowserItem(
            key,
            parentKey,
            entry.Name,
            kind,
            MapCategory(entry),
            entry.DisplayPath,
            kind == FileBrowserItemKind.Container ? FileBrowserChildState.Unknown : FileBrowserChildState.Empty,
            size: entry.Size,
            mediaType: entry.MediaType,
            createdAt: entry.CreatedAtUtc,
            modifiedAt: entry.ModifiedAtUtc,
            metadataState: new FileBrowserMetadataState(
                exact,
                completeness: completeness == StorageBrowseCompleteness.Complete
                    ? FileBrowserCompleteness.Complete
                    : FileBrowserCompleteness.Partial),
            capabilities: capabilities);
    }

    public static StorageBrowseMetadataField MapMetadata(FileBrowserMetadataRequest request)
    {
        StorageBrowseMetadataField result = StorageBrowseMetadataField.None;
        if (request.Fields.HasFlag(FileBrowserMetadataFields.Size))
        {
            result |= StorageBrowseMetadataField.Size;
        }

        if (request.Fields.HasFlag(FileBrowserMetadataFields.MediaType))
        {
            result |= StorageBrowseMetadataField.MediaType;
        }

        if (request.Fields.HasFlag(FileBrowserMetadataFields.Timestamps))
        {
            result |= StorageBrowseMetadataField.CreatedAtUtc | StorageBrowseMetadataField.ModifiedAtUtc;
        }

        return result;
    }

    public static FileBrowserProviderException MapException(StorageBrowseException exception)
    {
        FileBrowserErrorCode code = exception.Error.Code switch
        {
            StorageBrowseErrorCode.AccessDenied => FileBrowserErrorCode.Forbidden,
            StorageBrowseErrorCode.UnsupportedOperation => FileBrowserErrorCode.Unsupported,
            StorageBrowseErrorCode.SourceChanged => FileBrowserErrorCode.StaleCursor,
            StorageBrowseErrorCode.InvalidCursor => FileBrowserErrorCode.StaleCursor,
            StorageBrowseErrorCode.ProviderUnavailable => FileBrowserErrorCode.Unavailable,
            StorageBrowseErrorCode.BudgetExceeded => FileBrowserErrorCode.RateLimited,
            StorageBrowseErrorCode.InvalidRequest => FileBrowserErrorCode.InvalidOperation,
            _ => FileBrowserErrorCode.ProviderFailure
        };
        return new FileBrowserProviderException(
            new FileBrowserError(
                code,
                "The storage provider could not complete the file browser operation.",
                exception.Error.IsRetryable));
    }

    private static FileBrowserItemCategory MapCategory(StorageBrowseEntry entry)
    {
        if (entry.Kind == StorageBrowseEntryKind.Container)
        {
            return FileBrowserItemCategory.Folder;
        }

        if (entry.Kind == StorageBrowseEntryKind.Link)
        {
            return FileBrowserItemCategory.Link;
        }

        if (entry.MediaType is not null)
        {
            if (entry.MediaType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                return FileBrowserItemCategory.Image;
            }

            if (entry.MediaType.StartsWith("video/", StringComparison.OrdinalIgnoreCase))
            {
                return FileBrowserItemCategory.Video;
            }

            if (entry.MediaType.StartsWith("audio/", StringComparison.OrdinalIgnoreCase))
            {
                return FileBrowserItemCategory.Audio;
            }

            if (entry.MediaType.StartsWith("text/", StringComparison.OrdinalIgnoreCase))
            {
                return FileBrowserItemCategory.Document;
            }
        }

        return Path.GetExtension(entry.Name).ToLowerInvariant() switch
        {
            ".zip" or ".tar" or ".gz" => FileBrowserItemCategory.Archive,
            ".json" or ".xml" or ".csv" => FileBrowserItemCategory.Data,
            ".cs" or ".razor" or ".js" or ".ts" => FileBrowserItemCategory.Code,
            _ => FileBrowserItemCategory.Other
        };
    }
}
