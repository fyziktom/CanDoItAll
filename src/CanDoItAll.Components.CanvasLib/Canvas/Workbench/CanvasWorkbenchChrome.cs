namespace CanDoItAll.Components.CanvasLib;

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

    public bool CollapseOnDoubleClick { get; set; } = true;

    public List<CanvasWorkbenchAction> QuickCreateActions { get; set; } = [];

    public List<CanvasWorkbenchAction> GroupContextActions { get; set; } = [];

    public CanvasWorkbenchDiagnosticsOptions Diagnostics { get; set; } = new();

    public CanvasWorkbenchMinimapOptions Minimap { get; set; } = new();

    public CanvasWorkbenchClipboardOptions Clipboard { get; set; } = new();

    public CanvasWorkbenchTooltipPopoverOptions TooltipPopover { get; set; } = new();

    public CanvasWorkbenchMarqueeOptions MarqueeSelection { get; set; } = new();

    public CanvasWorkbenchSnapGuideOptions SnapGuides { get; set; } = new();

    public CanvasWorkbenchConnectorAnchorOptions ConnectorAnchors { get; set; } = new();

    public CanvasWorkbenchTransformHandleOptions TransformHandles { get; set; } = new();
}

public sealed class CanvasWorkbenchAction
{
    public string ActionId { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string Icon { get; set; } = string.Empty;

    public string MenuLabel { get; set; } = string.Empty;

    public string ShortcutKey { get; set; } = string.Empty;

    public string MenuSize { get; set; } = "normal";

    public string SubmenuLayout { get; set; } = string.Empty;

    public string Tone { get; set; } = "neutral";

    public string SetupRendererKey { get; set; } = string.Empty;

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

public sealed class CanvasWorkbenchInputField
{
    public string Key { get; set; } = string.Empty;

    public string SectionKey { get; set; } = string.Empty;

    public string SectionTitle { get; set; } = string.Empty;

    public string SectionDescription { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public string Placeholder { get; set; } = string.Empty;

    public string InputMode { get; set; } = "text";

    public bool IsRequired { get; set; }

    public List<CanvasWorkbenchInputOption> Options { get; set; } = [];
}

public sealed class CanvasWorkbenchInputValue
{
    public string Key { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;
}

public sealed class CanvasWorkbenchInputOption
{
    public string Value { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;
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

public sealed class CanvasWorkbenchDiagnosticsOptions
{
    public bool IsEnabled { get; set; }

    public bool ShowNodeBounds { get; set; } = true;

    public bool ShowConnectorAnchors { get; set; } = true;

    public bool ShowViewportStats { get; set; } = true;
}

public sealed class CanvasWorkbenchMinimapOptions
{
    public bool IsEnabled { get; set; } = true;

    public string Title { get; set; } = "Scene overview";
}

public sealed class CanvasWorkbenchClipboardOptions
{
    public bool IsEnabled { get; set; } = true;

    public bool AllowCopy { get; set; } = true;

    public bool AllowPaste { get; set; } = true;

    public bool AllowDuplicate { get; set; } = true;

    public string Format { get; set; } = "application/vnd.candoitall.canvas+json";
}

public sealed class CanvasWorkbenchTooltipPopoverOptions
{
    public bool IsEnabled { get; set; } = true;

    public bool FocusTriggers { get; set; } = true;

    public bool SupportsRichPreview { get; set; } = true;
}

public sealed class CanvasWorkbenchMarqueeOptions
{
    public bool IsEnabled { get; set; } = true;

    public string ModifierKey { get; set; } = "Alt";

    public string SelectionMode { get; set; } = "Intersect";
}

public sealed class CanvasWorkbenchSnapGuideOptions
{
    public bool IsEnabled { get; set; } = true;

    public double Tolerance { get; set; } = 18;

    public string ModifierPolicy { get; set; } = "ShiftBypassesSnap";
}

public sealed class CanvasWorkbenchConnectorAnchorOptions
{
    public bool IsEnabled { get; set; } = true;

    public bool ShowOnHover { get; set; } = true;

    public bool ShowOnSelection { get; set; } = true;

    public string PlacementMode { get; set; } = "Edges";
}

public sealed class CanvasWorkbenchTransformHandleOptions
{
    public bool IsEnabled { get; set; } = true;

    public bool ShowResizeHandles { get; set; } = true;

    public bool ShowRotateHandle { get; set; } = true;

    public string PlacementMode { get; set; } = "SelectionBounds";
}
