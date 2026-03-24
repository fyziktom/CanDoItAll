using System.IO;
using CanDoItAll.ComponentKit.Canvas;

namespace CanDoItAll.Modules.Factory.CanvasAdapters;

public static class PromptSessionAttachmentNode
{
    public static CanvasWorkbenchNode BuildNode(PromptSessionAttachmentSummary attachment, int index, string parentCanvasId)
    {
        var visualKind = ResolveVisualKind(attachment);
        var statusPill = ResolveStatusPill(attachment);
        return new CanvasWorkbenchNode
        {
            Id = BuildCanvasId(attachment.Id),
            ParentId = parentCanvasId,
            Family = "special",
            Kind = "input",
            Icon = ResolveIcon(attachment),
            Title = string.IsNullOrWhiteSpace(attachment.Title) ? $"Input {index + 1}" : attachment.Title,
            Subtitle = ResolveSubtitle(attachment),
            LeadText = ResolveLeadText(attachment),
            Status = visualKind,
            StatusPill = statusPill,
            AccentColor = ResolveAccent(attachment),
            PaletteKey = ResolvePalette(attachment),
            X = 860,
            Y = 780 + (index * 124),
            MediaKind = visualKind,
            MediaPreviewUrl = attachment.MediaRoute,
            MediaPreviewAlt = string.IsNullOrWhiteSpace(attachment.Title) ? attachment.Kind : attachment.Title,
            MediaContentType = attachment.MediaContentType,
            MediaFileName = attachment.MediaOriginalFileName,
            FooterChips =
            [
                new CanvasWorkbenchChip
                {
                    Text = string.IsNullOrWhiteSpace(attachment.MediaOriginalFileName)
                        ? statusPill
                        : attachment.MediaOriginalFileName,
                    Tone = "neutral"
                }
            ],
            ContextActions = PromptFactoryCatalogToolbox.BuildInputNodeActions(attachment.Id).ToList()
        };
    }

    public static string BuildCanvasId(string attachmentId) => $"selection:input:{attachmentId}";

    public static string ResolveSubtitle(PromptSessionAttachmentSummary attachment)
    {
        return ResolveVisualKind(attachment) switch
        {
            "link" => attachment.LinkUrl,
            "image" or "video" or "pdf" or "spreadsheet" or "document" or "text" or "archive" or "file" => string.IsNullOrWhiteSpace(attachment.MediaOriginalFileName)
                ? ResolveStatusPill(attachment)
                : attachment.MediaOriginalFileName,
            _ => string.IsNullOrWhiteSpace(attachment.Subtitle) ? attachment.Kind : attachment.Subtitle
        };
    }

    public static string ResolveLeadText(PromptSessionAttachmentSummary attachment)
    {
        if (!string.IsNullOrWhiteSpace(attachment.Notes))
        {
            return attachment.Notes;
        }

        if (!string.IsNullOrWhiteSpace(attachment.Subtitle))
        {
            return $"Use this input for: {attachment.Subtitle}";
        }

        return attachment.Kind;
    }

    public static string ResolveVisualKind(PromptSessionAttachmentSummary attachment)
    {
        if (string.Equals(attachment.Kind, "image", StringComparison.OrdinalIgnoreCase))
        {
            return "image";
        }

        if (string.Equals(attachment.Kind, "video", StringComparison.OrdinalIgnoreCase))
        {
            return "video";
        }

        if (string.Equals(attachment.Kind, "link", StringComparison.OrdinalIgnoreCase))
        {
            return "link";
        }

        if (string.Equals(attachment.Kind, "note", StringComparison.OrdinalIgnoreCase))
        {
            return "note";
        }

        var extension = Path.GetExtension(attachment.MediaOriginalFileName)?.Trim().ToLowerInvariant();
        return extension switch
        {
            ".pdf" => "pdf",
            ".xls" or ".xlsx" or ".csv" => "spreadsheet",
            ".doc" or ".docx" or ".ppt" or ".pptx" => "document",
            ".txt" or ".md" or ".json" or ".xml" or ".log" => "text",
            ".zip" or ".rar" or ".7z" => "archive",
            _ => "file"
        };
    }

    public static string ResolveIcon(PromptSessionAttachmentSummary attachment)
        => ResolveVisualKind(attachment) switch
        {
            "image" => "IMG",
            "video" => "VID",
            "link" => "URL",
            "note" => "NOTE",
            "pdf" => "PDF",
            "spreadsheet" => "XLS",
            "document" => "DOC",
            "text" => "TXT",
            "archive" => "ZIP",
            _ => "FILE"
        };

    public static string ResolveStatusPill(PromptSessionAttachmentSummary attachment)
        => ResolveVisualKind(attachment) switch
        {
            "image" => "Image",
            "video" => "Video",
            "link" => "Link",
            "note" => "Note",
            "pdf" => "PDF",
            "spreadsheet" => "Spreadsheet",
            "document" => "Document",
            "text" => "Text",
            "archive" => "Archive",
            _ => "File"
        };

    public static string ResolveAccent(PromptSessionAttachmentSummary attachment)
        => ResolveVisualKind(attachment) switch
        {
            "image" => "#2563eb",
            "video" => "#7c3aed",
            "link" => "#0284c7",
            "note" => "#4b5563",
            "pdf" => "#dc2626",
            "spreadsheet" => "#15803d",
            "document" => "#ea580c",
            "text" => "#475569",
            "archive" => "#7c2d12",
            _ => "#059669"
        };

    public static string ResolvePalette(PromptSessionAttachmentSummary attachment)
        => ResolveVisualKind(attachment) switch
        {
            "image" => "sky",
            "video" => "violet",
            "link" => "sky",
            "note" => "neutral",
            "pdf" => "danger",
            "spreadsheet" => "mint",
            "document" => "warn",
            "text" => "neutral",
            "archive" => "warn",
            _ => "mint"
        };
}
