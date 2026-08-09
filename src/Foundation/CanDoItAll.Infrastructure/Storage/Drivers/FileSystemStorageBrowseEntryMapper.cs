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
        foreach (var segment in FileSystemStorageKeyCodec.Decode(container.Key))
        {
            current = FileSystemStorageKeyCodec.AppendEncoded(current, segment.Encoded);
            path.Add(new StorageBrowsePathSegment(
                segment.Physical,
                new StorageBrowseContainer(current)));
        }

        return path;
    }

    private static string CombineKey(string container, string name)
        => FileSystemStorageKeyCodec.Append(container, name);

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

internal static class FileSystemStorageKeyCodec
{
    public static string Canonicalize(string key)
    {
        string canonical = string.Empty;
        foreach (var segment in Decode(key))
        {
            canonical = Append(canonical, segment.Physical);
        }

        return canonical;
    }

    public static string Append(string container, string physicalName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(physicalName);
        return AppendEncoded(container, Uri.EscapeDataString(physicalName));
    }

    public static string AppendEncoded(string container, string encodedSegment)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(encodedSegment);
        var canonicalContainer = string.Join(
            '/',
            container.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries));
        return canonicalContainer.Length == 0
            ? encodedSegment
            : $"{canonicalContainer}/{encodedSegment}";
    }

    public static IReadOnlyList<(string Encoded, string Physical)> Decode(string key)
    {
        if (string.IsNullOrWhiteSpace(key) || string.Equals(key.Trim(), ".", StringComparison.Ordinal))
        {
            return [];
        }

        var segments = key.Trim().Split(['/', '\\'], StringSplitOptions.None);
        if (segments.Any(string.IsNullOrEmpty))
        {
            throw new ArgumentException("Filesystem storage keys cannot contain empty segments.", nameof(key));
        }

        return segments
            .Select(segment => (Encoded: segment, Physical: DecodeSegment(segment)))
            .ToArray();
    }

    private static string DecodeSegment(string encoded)
    {
        for (var index = 0; index < encoded.Length; index++)
        {
            if (encoded[index] != '%')
            {
                continue;
            }

            if (index + 2 >= encoded.Length ||
                !Uri.IsHexDigit(encoded[index + 1]) ||
                !Uri.IsHexDigit(encoded[index + 2]))
            {
                throw new ArgumentException("Filesystem storage keys contain invalid percent encoding.", nameof(encoded));
            }

            index += 2;
        }

        var physical = Uri.UnescapeDataString(encoded);
        if (physical.Length == 0 ||
            physical is "." or ".." ||
            physical.Contains('\0') ||
            physical.Contains(Path.DirectorySeparatorChar) ||
            physical.Contains(Path.AltDirectorySeparatorChar))
        {
            throw new ArgumentException("Filesystem storage keys contain an invalid physical path segment.", nameof(encoded));
        }

        return physical;
    }
}
