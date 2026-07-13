using System.Globalization;

namespace CanDoItAll.Infrastructure.Storage;

internal static class FileSystemStorageBrowseEntryMapper
{
    public static StorageBrowseEntry CreateEntry(
        StorageBrowseContainer container,
        FileSystemInfo info,
        StorageBrowseMetadataField metadata)
    {
        bool isContainer = info is DirectoryInfo;
        string key = CombineKey(container.Key, info.Name);
        long? size = metadata.HasFlag(StorageBrowseMetadataField.Size) && info is FileInfo file
            ? file.Length
            : null;
        DateTimeOffset? createdAtUtc = metadata.HasFlag(StorageBrowseMetadataField.CreatedAtUtc)
            ? info.CreationTimeUtc
            : null;
        DateTimeOffset? modifiedAtUtc = metadata.HasFlag(StorageBrowseMetadataField.ModifiedAtUtc)
            ? info.LastWriteTimeUtc
            : null;
        string? mediaType = metadata.HasFlag(StorageBrowseMetadataField.MediaType) && !isContainer
            ? ResolveMediaType(info.Extension)
            : null;
        StorageBrowseEntryCapability capabilities = isContainer
            ? StorageBrowseEntryCapability.Browse
            : StorageBrowseEntryCapability.Read;
        return new StorageBrowseEntry(
            new StorageBrowseEntryId(key),
            container,
            info.Name,
            key,
            isContainer ? StorageBrowseEntryKind.Container : StorageBrowseEntryKind.File,
            capabilities,
            size,
            createdAtUtc,
            modifiedAtUtc,
            mediaType);
    }

    public static StorageBrowsePage CreatePage(
        StorageCatalogRecord storage,
        StorageBrowseRequest request,
        IReadOnlyList<StorageBrowseEntry> entries,
        StorageBrowseCompleteness completeness,
        StorageBrowseOperationMetrics metrics,
        StorageBrowseCursor? nextCursor,
        long directoryVersion)
        => new(
            request.Container,
            BuildPath(storage, request.Container),
            entries,
            request.Sort,
            completeness,
            metrics,
            nextCursor,
            new StorageBrowseConsistencyToken(directoryVersion.ToString(CultureInfo.InvariantCulture)));

    public static IReadOnlyList<StorageBrowsePathSegment> BuildPath(
        StorageCatalogRecord storage,
        StorageBrowseContainer container)
    {
        var path = new List<StorageBrowsePathSegment>
        {
            new(string.IsNullOrWhiteSpace(storage.Name) ? "Files" : storage.Name, StorageBrowseContainer.Root)
        };
        if (container.IsRoot)
        {
            return path;
        }

        string current = string.Empty;
        foreach (string segment in container.Key.Split(
                     ['/', '\\'],
                     StringSplitOptions.RemoveEmptyEntries))
        {
            current = CombineKey(current, segment);
            path.Add(new StorageBrowsePathSegment(segment, new StorageBrowseContainer(current)));
        }

        return path;
    }

    private static string CombineKey(string container, string name)
        => string.IsNullOrWhiteSpace(container)
            ? name.Replace('\\', '/')
            : $"{container.TrimEnd('/', '\\').Replace('\\', '/')}/{name.Replace('\\', '/')}";

    private static string ResolveMediaType(string extension)
    {
        if (extension.Equals(".txt", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".log", StringComparison.OrdinalIgnoreCase))
        {
            return "text/plain";
        }

        if (extension.Equals(".md", StringComparison.OrdinalIgnoreCase))
        {
            return "text/markdown";
        }

        if (extension.Equals(".json", StringComparison.OrdinalIgnoreCase))
        {
            return "application/json";
        }

        if (extension.Equals(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            return "application/pdf";
        }

        if (extension.Equals(".png", StringComparison.OrdinalIgnoreCase))
        {
            return "image/png";
        }

        if (extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase))
        {
            return "image/jpeg";
        }

        if (extension.Equals(".gif", StringComparison.OrdinalIgnoreCase))
        {
            return "image/gif";
        }

        if (extension.Equals(".svg", StringComparison.OrdinalIgnoreCase))
        {
            return "image/svg+xml";
        }

        return "application/octet-stream";
    }
}
