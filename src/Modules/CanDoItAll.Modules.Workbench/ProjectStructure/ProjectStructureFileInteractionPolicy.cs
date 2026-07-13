using CanDoItAll.FileTools.FileInteraction;
using CanDoItAll.FileTools.Integration;
using CanDoItAll.Infrastructure.Storage;

namespace CanDoItAll.Modules.Workbench;

internal static class ProjectStructureFileInteractionPolicy
{
    public const string MermaidMediaType = "text/vnd.mermaid";
    public const int MaximumContentBytes = 16 * 1024 * 1024;

    private static readonly HashSet<string> EditableExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt", ".log", ".md", ".markdown", ".mmd", ".mermaid", ".json", ".xml",
        ".yaml", ".yml", ".csv", ".cs", ".razor", ".html", ".htm", ".css", ".js", ".ts"
    };

    public static FileToolsKnownFileIntent ResolveIntent(
        string fileName,
        string? mediaType,
        StorageCatalogRecord storage,
        IStorageDriver driver)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentNullException.ThrowIfNull(storage);
        ArgumentNullException.ThrowIfNull(driver);

        bool supportsRevisionedWrite =
            !storage.IsReadOnly &&
            storage.CapabilityMask.HasFlag(StorageCapability.Write) &&
            driver.SupportedCapabilities.HasFlag(StorageCapability.Write) &&
            driver is IStorageRevisionedContentDriver;
        return supportsRevisionedWrite && IsEditableText(fileName, mediaType)
            ? FileToolsKnownFileIntent.Edit
            : FileToolsKnownFileIntent.ReadOnly;
    }

    public static string? NormalizeMediaType(string fileName, string? mediaType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        string extension = Path.GetExtension(fileName);
        string? known = extension.ToLowerInvariant() switch
        {
            ".mmd" or ".mermaid" => MermaidMediaType,
            ".md" or ".markdown" => "text/markdown",
            ".svg" => "image/svg+xml",
            ".pdf" => "application/pdf",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".bmp" => "image/bmp",
            ".json" => "application/json",
            ".xml" => "application/xml",
            ".yaml" or ".yml" => "text/yaml",
            ".csv" => "text/csv",
            _ when EditableExtensions.Contains(extension) => "text/plain",
            _ => null
        };
        return known ?? (string.IsNullOrWhiteSpace(mediaType) ? null : mediaType.Trim());
    }

    public static string ResolveHostNotice(FileInteractionRequest request, bool canEdit)
    {
        ArgumentNullException.ThrowIfNull(request);
        return request.Extension switch
        {
            ".svg" => "SVG stays metadata-only and is never inserted as active markup.",
            ".pdf" => "PDF uses the browser-native viewer; embedded PDF actions are controlled by that viewer.",
            ".mmd" or ".mermaid" => "Mermaid renders in strict mode with HTML labels and source actions disabled.",
            _ when canEdit => "Text edits use bounded history and an awaited revision-aware save.",
            _ => "This file is read-only. Unsupported formats remain inert and expose metadata only."
        };
    }

    private static bool IsEditableText(string fileName, string? mediaType)
    {
        string extension = Path.GetExtension(fileName);
        if (EditableExtensions.Contains(extension))
        {
            return true;
        }

        string? normalized = string.IsNullOrWhiteSpace(mediaType) ? null : mediaType.Trim();
        return normalized?.StartsWith("text/", StringComparison.OrdinalIgnoreCase) == true ||
               normalized?.Equals("application/json", StringComparison.OrdinalIgnoreCase) == true ||
               normalized?.Equals("application/xml", StringComparison.OrdinalIgnoreCase) == true;
    }
}
