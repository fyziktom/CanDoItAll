using System.Text.Json;

namespace CanDoItAll.ComponentKit.Canvas;

public sealed class CanvasWorkbenchSurface
{
    public string SurfaceId { get; set; } = string.Empty;

    public string Mode { get; set; } = "authoring";

    public List<CanvasWorkbenchNode> Nodes { get; set; } = [];

    public List<CanvasWorkbenchLink> Links { get; set; } = [];

    public CanvasWorkbenchUiState UiState { get; set; } = new();

    public CanvasWorkbenchChrome Chrome { get; set; } = new();
}

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

    public List<CanvasWorkbenchAction> ContextActions { get; set; } = [];
}

public sealed class CanvasWorkbenchLink
{
    public string SourceId { get; set; } = string.Empty;

    public string TargetId { get; set; } = string.Empty;

    public string Kind { get; set; } = string.Empty;

    public bool IsUserAuthored { get; set; }
}

public sealed class CanvasWorkbenchUiState
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public const string CurrentVersion = "canvas-workbench.v1";

    public string Version { get; set; } = CurrentVersion;

    public List<string> SelectedNodeIds { get; set; } = [];

    public List<string> CollapsedNodeIds { get; set; } = [];

    public List<CanvasWorkbenchGroupFrame> GroupFrames { get; set; } = [];

    public Dictionary<string, CanvasWorkbenchPoint> ManualPositions { get; set; } = [];

    public double Zoom { get; set; } = 1;

    public double PanX { get; set; } = 90;

    public double PanY { get; set; } = 110;

    public double MenuActionScale { get; set; } = 1;

    public bool IsMaximized { get; set; }

    public string ActiveInspectorTab { get; set; } = string.Empty;

    public static CanvasWorkbenchUiState Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new CanvasWorkbenchUiState();
        }

        try
        {
            return JsonSerializer.Deserialize<CanvasWorkbenchUiState>(json, SerializerOptions) ?? new CanvasWorkbenchUiState();
        }
        catch
        {
            return new CanvasWorkbenchUiState();
        }
    }

    public string ToJson() => JsonSerializer.Serialize(this, SerializerOptions);
}

public sealed class CanvasWorkbenchChrome
{
    public string HintText { get; set; } = "Click to select, Alt-drag to marquee, drag the stage to pan, and use +/- to zoom.";

    public string EmptyStateKicker { get; set; } = "Canvas";

    public string EmptyStateTitle { get; set; } = "Choose a node to edit";

    public string EmptyStateDescription { get; set; } = "Selections open the mirrored editor surface in the inspector.";

    public string FocusActionLabel { get; set; } = "Focus first";

    public bool ShowFocusAction { get; set; } = true;

    public bool ShowQuickCreateRail { get; set; } = true;

    public string? ChildNoteActionId { get; set; }

    public string? SiblingNoteActionId { get; set; }

    public string InlineNotePlaceholder { get; set; } = "Write note";

    public List<CanvasWorkbenchAction> QuickCreateActions { get; set; } = [];

    public List<CanvasWorkbenchAction> GroupContextActions { get; set; } = [];
}

public sealed class CanvasWorkbenchAction
{
    public string ActionId { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string Icon { get; set; } = string.Empty;

    public string MenuLabel { get; set; } = string.Empty;

    public string MenuSize { get; set; } = "normal";

    public string SubmenuLayout { get; set; } = string.Empty;

    public string Tone { get; set; } = "neutral";

    public bool RequiresInput { get; set; }

    public string CreateMode { get; set; } = "command";

    public string ObjectSubtype { get; set; } = string.Empty;

    public string TitleLabel { get; set; } = "Title";

    public string TitlePlaceholder { get; set; } = string.Empty;

    public string SubtitleLabel { get; set; } = "Subtitle";

    public string SubtitlePlaceholder { get; set; } = string.Empty;

    public string NotesLabel { get; set; } = "Notes";

    public string NotesPlaceholder { get; set; } = string.Empty;

    public bool ShowDefaultTextFields { get; set; } = true;

    public string SubmitLabel { get; set; } = "Create";

    public bool RequiresFile { get; set; }

    public string AcceptedFileTypes { get; set; } = string.Empty;

    public string FilePrompt { get; set; } = "Drop a file here or choose one.";

    public bool SupportsDragDrop { get; set; } = true;

    public List<CanvasWorkbenchInputField> InputFields { get; set; } = [];

    public List<CanvasWorkbenchInputValue> DefaultInputValues { get; set; } = [];

    public List<CanvasWorkbenchAction> Children { get; set; } = [];
}

public sealed class CanvasWorkbenchChip
{
    public string Text { get; set; } = string.Empty;

    public string Tone { get; set; } = "neutral";
}

public sealed class CanvasWorkbenchPoint
{
    public double X { get; set; }

    public double Y { get; set; }
}

public sealed class CanvasWorkbenchGroupFrame
{
    public string Id { get; set; } = string.Empty;

    public string Label { get; set; } = "Group";

    public string Tone { get; set; } = "accent";

    public List<string> AnchorNodeIds { get; set; } = [];
}

public sealed class CanvasWorkbenchInputField
{
    public string Key { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public string Placeholder { get; set; } = string.Empty;

    public string InputMode { get; set; } = "text";

    public bool IsRequired { get; set; }
}

public sealed class CanvasWorkbenchInputValue
{
    public string Key { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;
}

public sealed class CanvasWorkbenchUploadedFile
{
    public string FileName { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public string Base64Data { get; set; } = string.Empty;
}

public sealed class CanvasWorkbenchStat
{
    public string Label { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;

    public string Tone { get; set; } = "neutral";
}

public sealed record CanvasWorkbenchSelectionChangedEventArgs(
    string? PrimaryNodeId,
    IReadOnlyList<string> SelectedNodeIds);

public sealed record CanvasWorkbenchContextActionRequest(
    string? NodeId,
    string ActionId,
    double X,
    double Y);

public sealed record CanvasWorkbenchCreateActionRequest(
    string ActionId,
    string? SourceNodeId,
    double X,
    double Y,
    string? ParentNodeId,
    string Title,
    string Subtitle,
    string Notes,
    string PlacementKind,
    string CreateMode,
    string ObjectSubtype,
    CanvasWorkbenchUploadedFile? UploadedFile,
    IReadOnlyList<CanvasWorkbenchInputValue>? InputValues = null);

public sealed record CanvasWorkbenchNodeEditRequest(
    string NodeId,
    string Title,
    string Notes);

public sealed record CanvasWorkbenchNodePositionChange(
    string NodeId,
    double X,
    double Y);

public sealed record CanvasWorkbenchNodesMovedEventArgs(
    IReadOnlyList<CanvasWorkbenchNodePositionChange> Positions);
