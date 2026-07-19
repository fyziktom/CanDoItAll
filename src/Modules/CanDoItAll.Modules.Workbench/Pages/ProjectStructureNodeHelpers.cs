using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Workbench.Pages;

internal static class ProjectStructureNodeHelpers
{
    public static IReadOnlyList<ProjectStructureCommandKind> ResolveInspectorCommands(ProjectStructureNode node)
    {
        var commands = new List<ProjectStructureCommandKind> { ProjectStructureCommandKind.Open };

        if (node.ObjectType == ProjectObjectType.PromptFlow)
        {
            commands.Add(ProjectStructureCommandKind.Wizard);
        }

        commands.Add(ProjectStructureCommandKind.Test);
        return commands;
    }

    public static string ResolveCommandLabel(ProjectStructureCommandKind command)
        => command switch
        {
            ProjectStructureCommandKind.Wizard => "Open prompt",
            ProjectStructureCommandKind.MarkUsed => "Mark used",
            _ => command.ToString()
        };

    public static bool IsCanvasStatusMutable(ProjectStructureNode node)
        => node.Id.StartsWith("custom:", StringComparison.Ordinal);

    public static string BuildSimpleNoteTitle(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "New note";
        }

        var normalized = text
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault();
        normalized = string.IsNullOrWhiteSpace(normalized)
            ? text.Trim()
            : normalized.Trim();
        return normalized.Length <= 64 ? normalized : $"{normalized[..61]}...";
    }

    public static string ResolveProgressLabel(ProjectStructureNode node)
        => node.ProgressPercent > 0 || string.Equals(node.ProgressMode, "progress", StringComparison.OrdinalIgnoreCase)
            ? $"{Math.Clamp(node.ProgressPercent, 0, 100)}%"
            : "Unset";

    public static string ResolvePriorityLabel(ProjectStructureNode node)
        => node.Priority > 0 ? $"P{node.Priority}" : "None";

    public static string ResolveMarkerLabel(ProjectStructureNode node)
        => node.Markers.Count switch
        {
            0 => string.IsNullOrWhiteSpace(node.MarkerLabel) ? "None" : node.MarkerLabel,
            1 => node.Markers[0].Label,
            <= 3 => string.Join(", ", node.Markers.Select(marker => marker.Label)),
            _ => $"{string.Join(", ", node.Markers.Take(3).Select(marker => marker.Label))} +{node.Markers.Count - 3}"
        };

    public static string ResolveAttachmentKindLabel(ProjectStructureNode node)
        => ResolveAttachmentPreviewKind(node) switch
        {
            AttachmentPreviewKind.Image => "Image",
            AttachmentPreviewKind.Video => "Video",
            AttachmentPreviewKind.Audio => "Audio",
            AttachmentPreviewKind.TextDocument => "Text",
            AttachmentPreviewKind.Document => "Document",
            _ => "File"
        };

    public static int ResolveOutlineWeight(ProjectObjectType objectType) => objectType switch
    {
        ProjectObjectType.ProjectRoot => 0,
        ProjectObjectType.Phase => 1,
        ProjectObjectType.ProjectBlock => 2,
        ProjectObjectType.PromptFlow or ProjectObjectType.PromptSession => 3,
        ProjectObjectType.PromptStep => 4,
        _ => 5
    };

    public static string? ResolveRootNodeId(IReadOnlyList<ProjectStructureNode> nodes)
        => nodes.FirstOrDefault(node => node.ObjectType == ProjectObjectType.ProjectRoot)?.Id
            ?? nodes.FirstOrDefault(node => string.IsNullOrWhiteSpace(node.ParentId))?.Id
            ?? nodes.FirstOrDefault()?.Id;

    public static bool HasManagedAttachment(ProjectStructureNode? node)
        => node is not null &&
            (!string.IsNullOrWhiteSpace(node.StorageObjectReferenceJson) ||
             !string.IsNullOrWhiteSpace(node.MediaRelativePath));

    public static bool CanRenderAttachmentPreview(ProjectStructureNode? node)
        => HasManagedAttachment(node);

    public static bool UsesDirectFileInteractionPreview(ProjectStructureNode? node)
        => HasManagedAttachment(node);

    public static AttachmentPreviewKind ResolveAttachmentPreviewKind(ProjectStructureNode? node)
    {
        if (HasInlineTextAssetPreview(node))
        {
            return AttachmentPreviewKind.TextDocument;
        }

        if (!HasManagedAttachment(node))
        {
            return AttachmentPreviewKind.None;
        }

        var contentType = ResolveAttachmentContentType(node!);
        if (node!.ObjectType == ProjectObjectType.ImageAsset ||
            contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            return AttachmentPreviewKind.Image;
        }

        if (node.ObjectType == ProjectObjectType.VideoAsset ||
            contentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase))
        {
            return AttachmentPreviewKind.Video;
        }

        if (contentType.StartsWith("audio/", StringComparison.OrdinalIgnoreCase))
        {
            return AttachmentPreviewKind.Audio;
        }

        if (IsTextAttachment(node, contentType))
        {
            return AttachmentPreviewKind.TextDocument;
        }

        if (IsDocumentAttachment(node, contentType))
        {
            return AttachmentPreviewKind.Document;
        }

        return AttachmentPreviewKind.None;
    }

    public static string ResolveAttachmentContentType(ProjectStructureNode node)
    {
        if (!string.IsNullOrWhiteSpace(node.MediaContentType) &&
            !string.Equals(node.MediaContentType, "application/octet-stream", StringComparison.OrdinalIgnoreCase))
        {
            return node.MediaContentType.Trim();
        }

        return ResolveAttachmentExtension(node) switch
        {
            ".mp3" => "audio/mpeg",
            ".m4a" => "audio/mp4",
            ".ogg" => "audio/ogg",
            ".pdf" => "application/pdf",
            ".json" => "application/json",
            ".xml" => "application/xml",
            ".txt" => "text/plain",
            ".md" => "text/markdown",
            ".csv" => "text/csv",
            ".log" => "text/plain",
            ".yaml" or ".yml" => "text/yaml",
            ".wav" => "audio/wav",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".xls" => "application/vnd.ms-excel",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
            _ => node.MediaContentType?.Trim() ?? string.Empty
        };
    }

    public static string ResolveAttachmentExtension(ProjectStructureNode node)
        => (Path.GetExtension(node.MediaOriginalFileName) switch
        {
            { Length: > 0 } extension => extension,
            _ => Path.GetExtension(node.Route)
        }).ToLowerInvariant();

    public static string ResolveAttachmentDisplayName(ProjectStructureNode node)
        => string.IsNullOrWhiteSpace(node.MediaOriginalFileName) ? node.Title : node.MediaOriginalFileName;

    public static string ResolveAttachmentLeadCopy(ProjectStructureNode node)
        => ResolveAttachmentPreviewKind(node) switch
        {
            AttachmentPreviewKind.Image => "The image opens through current file authority without an unsigned route.",
            AttachmentPreviewKind.Video => "No video renderer is registered; the governed interaction remains inert and explicit.",
            AttachmentPreviewKind.Audio => "No audio renderer is registered; the governed interaction remains inert and explicit.",
            AttachmentPreviewKind.TextDocument => "Text-based assets support bounded view/edit and awaited revision-aware save when storage is writable.",
            AttachmentPreviewKind.Document when ResolveAttachmentContentType(node).Equals("application/pdf", StringComparison.OrdinalIgnoreCase)
                => "PDF files use the governed browser-native renderer with its embedded-action limitation stated explicitly.",
            AttachmentPreviewKind.Document
                => "Only explicitly registered document renderers can load content; other formats remain inert.",
            _ => "This attachment opens through governed FileInteraction and remains inert when its type is unsupported."
        };

    private static bool HasInlineTextAssetPreview(ProjectStructureNode? node)
    {
        return node?.ObjectType == ProjectObjectType.File &&
               IsTextAssetSubtype(node.ObjectSubtype) &&
               !string.IsNullOrWhiteSpace(node.Notes);
    }

    private static bool IsTextAssetSubtype(string? subtype)
    {
        return subtype?.Trim().ToLowerInvariant() switch
        {
            "md" or "markdown" or "txt" or "text" or "json" or "xml" or "csv" or "log" or "yaml" or "yml" => true,
            _ => false
        };
    }

    private static bool IsTextAttachment(ProjectStructureNode node, string contentType)
    {
        if (contentType.Equals("application/json", StringComparison.OrdinalIgnoreCase) ||
            contentType.Equals("application/xml", StringComparison.OrdinalIgnoreCase) ||
            contentType.Equals("text/xml", StringComparison.OrdinalIgnoreCase) ||
            contentType.StartsWith("text/", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return ResolveAttachmentExtension(node) is ".txt" or ".md" or ".json" or ".xml" or ".csv" or ".log" or ".yaml" or ".yml";
    }

    private static bool IsDocumentAttachment(ProjectStructureNode node, string contentType)
    {
        if (contentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase) ||
            contentType.Equals("application/json", StringComparison.OrdinalIgnoreCase) ||
            contentType.Equals("application/xml", StringComparison.OrdinalIgnoreCase) ||
            contentType.Equals("text/xml", StringComparison.OrdinalIgnoreCase) ||
            contentType.StartsWith("text/", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return ResolveAttachmentExtension(node) is ".pdf" or ".txt" or ".md" or ".json" or ".xml" or ".csv" or ".log" or ".yaml" or ".yml";
    }

}
