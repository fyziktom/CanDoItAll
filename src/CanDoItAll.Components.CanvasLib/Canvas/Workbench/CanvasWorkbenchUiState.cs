using System.Text.Json;
using CanDoItAll.Components.OverlayLib;

namespace CanDoItAll.Components.CanvasLib;

public sealed class CanvasWorkbenchUiState
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public const string CurrentVersion = "canvas-workbench.v1";

    public string Version { get; set; } = CurrentVersion;

    public List<string> SelectedNodeIds { get; set; } = [];

    public List<string> HighlightedNodeIds { get; set; } = [];

    public List<string> CollapsedNodeIds { get; set; } = [];

    public List<CanvasWorkbenchGroupFrame> GroupFrames { get; set; } = [];

    public Dictionary<string, CanvasWorkbenchPoint> ManualPositions { get; set; } = [];

    public Dictionary<string, CanvasWorkbenchWindowState> WindowStates { get; set; } = [];

    public double Zoom { get; set; } = 1;

    public double PanX { get; set; } = 90;

    public double PanY { get; set; } = 110;

    public double MenuActionScale { get; set; } = 1;

    public bool IsMaximized { get; set; }

    public string ActiveInspectorTab { get; set; } = string.Empty;

    public bool ShowDiagnostics { get; set; }

    public bool ShowMinimap { get; set; } = true;

    public static CanvasWorkbenchUiState Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new CanvasWorkbenchUiState();
        }

        try
        {
            var state = JsonSerializer.Deserialize<CanvasWorkbenchUiState>(json, SerializerOptions) ?? new CanvasWorkbenchUiState();
            state.SelectedNodeIds = SelectionModel.From(state.SelectedNodeIds).ToList();
            state.HighlightedNodeIds = NormalizeStringList(state.HighlightedNodeIds);
            state.CollapsedNodeIds = NormalizeStringList(state.CollapsedNodeIds);
            state.WindowStates = NormalizeWindowStates(state.WindowStates);
            return state;
        }
        catch
        {
            return new CanvasWorkbenchUiState();
        }
    }

    public string ToJson()
    {
        var normalized = new CanvasWorkbenchUiState
        {
            Version = Version,
            SelectedNodeIds = SelectionModel.From(SelectedNodeIds).ToList(),
            HighlightedNodeIds = NormalizeStringList(HighlightedNodeIds),
            CollapsedNodeIds = NormalizeStringList(CollapsedNodeIds),
            GroupFrames = GroupFrames,
            ManualPositions = ManualPositions,
            WindowStates = NormalizeWindowStates(WindowStates),
            Zoom = Zoom,
            PanX = PanX,
            PanY = PanY,
            MenuActionScale = MenuActionScale,
            IsMaximized = IsMaximized,
            ActiveInspectorTab = ActiveInspectorTab,
            ShowDiagnostics = ShowDiagnostics,
            ShowMinimap = ShowMinimap
        };

        return JsonSerializer.Serialize(normalized, SerializerOptions);
    }

    private static List<string> NormalizeStringList(IEnumerable<string>? values)
    {
        var normalized = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var value in values ?? [])
        {
            var candidate = value?.Trim();
            if (string.IsNullOrWhiteSpace(candidate) || !seen.Add(candidate))
            {
                continue;
            }

            normalized.Add(candidate);
        }

        return normalized;
    }

    private static Dictionary<string, CanvasWorkbenchWindowState> NormalizeWindowStates(
        IReadOnlyDictionary<string, CanvasWorkbenchWindowState>? values)
    {
        var normalized = new Dictionary<string, CanvasWorkbenchWindowState>(StringComparer.Ordinal);

        foreach (var (key, value) in values ?? new Dictionary<string, CanvasWorkbenchWindowState>())
        {
            var normalizedKey = key?.Trim();
            if (string.IsNullOrWhiteSpace(normalizedKey))
            {
                continue;
            }

            normalized[normalizedKey] = CanvasWorkbenchWindowState.Normalize(value);
        }

        return normalized;
    }
}

public sealed class CanvasWorkbenchWindowState
{
    public bool IsVisible { get; set; } = true;

    public bool IsMinimized { get; set; }

    public double? Left { get; set; }

    public double? Top { get; set; }

    public double? Width { get; set; }

    public double? Height { get; set; }

    public bool HasCustomGeometry
        => Left.HasValue || Top.HasValue || Width.HasValue || Height.HasValue;

    public CanvasWorkbenchWindowState Clone()
        => new()
        {
            IsVisible = IsVisible,
            IsMinimized = IsMinimized,
            Left = Left,
            Top = Top,
            Width = Width,
            Height = Height
        };

    public static CanvasWorkbenchWindowState Normalize(CanvasWorkbenchWindowState? value)
    {
        var normalized = value?.Clone() ?? new CanvasWorkbenchWindowState();
        normalized.Left = NormalizeDimension(normalized.Left);
        normalized.Top = NormalizeDimension(normalized.Top);
        normalized.Width = NormalizeDimension(normalized.Width);
        normalized.Height = NormalizeDimension(normalized.Height);
        return normalized;
    }

    public OverlayWindowState ToOverlayWindowState()
        => OverlayWindowState.Normalize(
            new OverlayWindowState
            {
                IsVisible = IsVisible,
                IsMinimized = IsMinimized,
                Left = Left,
                Top = Top,
                Width = Width,
                Height = Height
            });

    public static CanvasWorkbenchWindowState FromOverlayWindowState(OverlayWindowState? value)
    {
        var normalized = OverlayWindowState.Normalize(value);
        return new CanvasWorkbenchWindowState
        {
            IsVisible = normalized.IsVisible,
            IsMinimized = normalized.IsMinimized,
            Left = normalized.Left,
            Top = normalized.Top,
            Width = normalized.Width,
            Height = normalized.Height
        };
    }

    private static double? NormalizeDimension(double? value)
        => value.HasValue && value.Value > 0
            ? Math.Round(value.Value, 2, MidpointRounding.AwayFromZero)
            : null;
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
