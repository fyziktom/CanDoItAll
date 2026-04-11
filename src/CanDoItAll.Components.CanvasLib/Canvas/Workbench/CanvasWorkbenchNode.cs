namespace CanDoItAll.Components.CanvasLib;

public sealed class CanvasWorkbenchNode
{
    public string Id { get; set; } = string.Empty;

    public string? ParentId { get; set; }

    public string Family { get; set; } = "item";

    public string Kind { get; set; } = "item";

    public string Icon { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Subtitle { get; set; } = string.Empty;

    public string LeadText { get; set; } = string.Empty;

    public CanvasWorkbenchCompactPath? CompactPath { get; set; }

    public string Status { get; set; } = string.Empty;

    public string BranchLabel { get; set; } = string.Empty;

    public string AccentColor { get; set; } = "#7c3aed";

    public string PaletteKey { get; set; } = "neutral";

    public string DurationLabel { get; set; } = string.Empty;

    public string StatusPill { get; set; } = string.Empty;

    public string ProgressMode { get; set; } = "na";

    public int ProgressPercent { get; set; }

    public string MarkerIcon { get; set; } = string.Empty;

    public string MarkerTone { get; set; } = string.Empty;

    public string MarkerLabel { get; set; } = string.Empty;

    public List<CanvasWorkbenchMarker> Markers { get; set; } = [];

    public int Priority { get; set; }

    public bool IsRequired { get; set; }

    public bool IsCollapsible { get; set; }

    public bool IsReadOnly { get; set; }

    public bool IsPreviewOnly { get; set; }

    public bool IsInlineTextNode { get; set; }

    public string InlineText { get; set; } = string.Empty;

    public string InlineTextPlaceholder { get; set; } = "Write note";

    public string MediaKind { get; set; } = string.Empty;

    public string MediaPreviewUrl { get; set; } = string.Empty;

    public string MediaPreviewAlt { get; set; } = string.Empty;

    public string MediaContentType { get; set; } = string.Empty;

    public string MediaFileName { get; set; } = string.Empty;

    public double X { get; set; }

    public double Y { get; set; }

    public List<CanvasWorkbenchChip> Chips { get; set; } = [];

    public List<CanvasWorkbenchChip> FooterChips { get; set; } = [];

    public List<CanvasWorkbenchAnnotation> Annotations { get; set; } = [];

    public List<CanvasWorkbenchAction> ContextActions { get; set; } = [];

    public List<CanvasWorkbenchPort> InputPorts { get; set; } = [];

    public List<CanvasWorkbenchPort> OutputPorts { get; set; } = [];
}

public sealed class CanvasWorkbenchCompactPath
{
    public string Label { get; set; } = string.Empty;

    public string DisplayText { get; set; } = string.Empty;

    public string FullPath { get; set; } = string.Empty;

    public string PromotedText { get; set; } = string.Empty;
}

public sealed class CanvasWorkbenchChip
{
    public string Text { get; set; } = string.Empty;

    public string Tone { get; set; } = "neutral";
}

public sealed class CanvasWorkbenchAnnotation
{
    public string Id { get; set; } = string.Empty;

    public string Kind { get; set; } = "info";

    public string Tone { get; set; } = "accent";

    public string Label { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string Icon { get; set; } = string.Empty;

    public string ActionId { get; set; } = string.Empty;
}

public sealed class CanvasWorkbenchMarker
{
    public string Icon { get; set; } = string.Empty;

    public string Tone { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;
}

public sealed class CanvasWorkbenchPort
{
    public string Id { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public string Side { get; set; } = string.Empty;

    public string Tone { get; set; } = "neutral";

    public string CategoryKey { get; set; } = string.Empty;

    public string AccentColor { get; set; } = string.Empty;

    public string Kind { get; set; } = string.Empty;

    public bool IsRequired { get; set; }
}
