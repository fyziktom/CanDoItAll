namespace CanDoItAll.Infrastructure.Storage;

public static class StorageContentClassifier
{
    public static StorageContentKind Resolve(string? contentType, string? fileName)
    {
        var normalizedContentType = (contentType ?? string.Empty).Trim().ToLowerInvariant();
        var normalizedExtension = Path.GetExtension(fileName ?? string.Empty).Trim().ToLowerInvariant();

        if (normalizedContentType.StartsWith("image/", StringComparison.Ordinal))
        {
            return normalizedExtension is ".png" or ".jpg" or ".jpeg"
                ? StorageContentKind.Screenshot
                : StorageContentKind.Image;
        }

        if (normalizedContentType.StartsWith("video/", StringComparison.Ordinal))
        {
            return StorageContentKind.Video;
        }

        if (normalizedContentType.StartsWith("audio/", StringComparison.Ordinal))
        {
            return StorageContentKind.Audio;
        }

        if (normalizedContentType.Equals("application/pdf", StringComparison.Ordinal))
        {
            return StorageContentKind.Pdf;
        }

        if (normalizedContentType.Equals("application/json", StringComparison.Ordinal) ||
            normalizedExtension.Equals(".json", StringComparison.Ordinal))
        {
            return StorageContentKind.Json;
        }

        if (normalizedContentType.Equals("text/markdown", StringComparison.Ordinal) ||
            normalizedExtension.Equals(".md", StringComparison.Ordinal))
        {
            return StorageContentKind.Markdown;
        }

        if (normalizedExtension.Equals(".mmd", StringComparison.Ordinal))
        {
            return StorageContentKind.Mermaid;
        }

        if (normalizedExtension is ".log" or ".trace")
        {
            return StorageContentKind.Log;
        }

        if (normalizedContentType.StartsWith("text/", StringComparison.Ordinal))
        {
            return StorageContentKind.Text;
        }

        if (normalizedContentType.Equals("application/vnd.openxmlformats-officedocument.wordprocessingml.document", StringComparison.Ordinal) ||
            normalizedExtension.Equals(".docx", StringComparison.Ordinal))
        {
            return StorageContentKind.Docx;
        }

        if (normalizedContentType is "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" or "application/vnd.ms-excel" ||
            normalizedExtension is ".xlsx" or ".xls")
        {
            return StorageContentKind.Excel;
        }

        if (normalizedExtension is ".zip" or ".tar" or ".gz" or ".tgz" or ".7z")
        {
            return StorageContentKind.Archive;
        }

        return normalizedExtension switch
        {
            ".pdf" => StorageContentKind.Pdf,
            ".png" or ".jpg" or ".jpeg" or ".gif" or ".bmp" or ".webp" or ".svg" => StorageContentKind.Image,
            ".mp4" or ".mov" or ".webm" or ".avi" or ".mkv" => StorageContentKind.Video,
            ".mp3" or ".wav" or ".ogg" or ".m4a" or ".flac" => StorageContentKind.Audio,
            ".docx" => StorageContentKind.Docx,
            ".xlsx" or ".xls" => StorageContentKind.Excel,
            ".md" => StorageContentKind.Markdown,
            ".mmd" => StorageContentKind.Mermaid,
            ".json" => StorageContentKind.Json,
            ".txt" or ".csv" or ".yaml" or ".yml" or ".xml" => StorageContentKind.Text,
            _ => StorageContentKind.Unknown
        };
    }

    public static bool SupportsInlinePreview(StorageContentKind contentKind)
    {
        return contentKind is StorageContentKind.Text or
            StorageContentKind.Json or
            StorageContentKind.Markdown or
            StorageContentKind.Mermaid or
            StorageContentKind.Log or
            StorageContentKind.Pdf or
            StorageContentKind.Image or
            StorageContentKind.Screenshot or
            StorageContentKind.Audio or
            StorageContentKind.Video;
    }
}
