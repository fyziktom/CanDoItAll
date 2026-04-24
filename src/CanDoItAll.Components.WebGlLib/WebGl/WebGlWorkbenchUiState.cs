using System.Text.Json;

namespace CanDoItAll.Components.WebGlLib;

public sealed class WebGlWorkbenchUiState
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public const string CurrentVersion = "webgl-workbench.v1";

    public string Version { get; set; } = CurrentVersion;

    public List<string> SelectedNodeIds { get; set; } = [];

    public string ActiveViewPreset { get; set; } = WebGlWorkbenchViewPresets.Overview;

    public string LayoutMode { get; set; } = WebGlWorkbenchLayoutModes.CenterLane;

    public string ToolMode { get; set; } = WebGlWorkbenchToolModes.Select;

    public string NodeInfoMode { get; set; } = WebGlWorkbenchNodeInfoModes.Detailed;

    public double NodeSpacingFactor { get; set; } = 1;

    public bool DeterministicMode { get; set; } = true;

    public bool ShowDiagnostics { get; set; }

    public bool ShowGrid { get; set; } = true;

    public bool ShowAnchors { get; set; } = true;

    public bool ShowEdgeLabels { get; set; } = true;

    public bool TransparentGround { get; set; } = true;

    public bool IsStageMaximized { get; set; }

    public long CameraRevision { get; set; }

    public WebGlWorkbenchCameraState Camera { get; set; } = new();

    public static WebGlWorkbenchUiState Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new WebGlWorkbenchUiState();
        }

        try
        {
            var state = JsonSerializer.Deserialize<WebGlWorkbenchUiState>(json, SerializerOptions) ?? new WebGlWorkbenchUiState();
            state.SelectedNodeIds = NormalizeStringList(state.SelectedNodeIds);
            state.ActiveViewPreset = NormalizeActiveViewPreset(state.ActiveViewPreset);
            state.LayoutMode = WebGlWorkbenchLayoutModes.Normalize(state.LayoutMode);
            state.ToolMode = WebGlWorkbenchToolModes.Normalize(state.ToolMode);
            state.NodeInfoMode = WebGlWorkbenchNodeInfoModes.Normalize(state.NodeInfoMode);
            state.NodeSpacingFactor = NormalizeNodeSpacingFactor(state.NodeSpacingFactor);
            state.Camera = NormalizeCameraState(state.Camera);
            return state;
        }
        catch
        {
            return new WebGlWorkbenchUiState();
        }
    }

    public string ToJson()
    {
        var normalized = new WebGlWorkbenchUiState
        {
            Version = Version,
            SelectedNodeIds = NormalizeStringList(SelectedNodeIds),
            ActiveViewPreset = NormalizeActiveViewPreset(ActiveViewPreset),
            LayoutMode = WebGlWorkbenchLayoutModes.Normalize(LayoutMode),
            ToolMode = WebGlWorkbenchToolModes.Normalize(ToolMode),
            NodeInfoMode = WebGlWorkbenchNodeInfoModes.Normalize(NodeInfoMode),
            NodeSpacingFactor = NormalizeNodeSpacingFactor(NodeSpacingFactor),
            DeterministicMode = DeterministicMode,
            ShowDiagnostics = ShowDiagnostics,
            ShowGrid = ShowGrid,
            ShowAnchors = ShowAnchors,
            ShowEdgeLabels = ShowEdgeLabels,
            TransparentGround = TransparentGround,
            IsStageMaximized = IsStageMaximized,
            CameraRevision = CameraRevision,
            Camera = NormalizeCameraState(Camera)
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

    private static string NormalizeActiveViewPreset(string? value)
    {
        return value switch
        {
            WebGlWorkbenchViewPresets.Roles => WebGlWorkbenchViewPresets.Roles,
            WebGlWorkbenchViewPresets.Dependencies => WebGlWorkbenchViewPresets.Dependencies,
            WebGlWorkbenchViewPresets.Branching => WebGlWorkbenchViewPresets.Branching,
            WebGlWorkbenchViewPresets.Focus => WebGlWorkbenchViewPresets.Focus,
            _ => WebGlWorkbenchViewPresets.Overview
        };
    }

    private static double NormalizeNodeSpacingFactor(double value)
    {
        if (!double.IsFinite(value))
        {
            return 1;
        }

        return Math.Round(Math.Clamp(value, 0.75d, 1.85d), 2, MidpointRounding.AwayFromZero);
    }

    private static WebGlWorkbenchCameraState NormalizeCameraState(WebGlWorkbenchCameraState? state)
    {
        var normalized = state ?? new WebGlWorkbenchCameraState();
        normalized.ViewMode = WebGlWorkbenchCameraViewModes.Normalize(normalized.ViewMode, normalized.ProjectionMode);
        normalized.ProjectionMode = WebGlWorkbenchCameraViewModes.ResolveProjectionMode(normalized.ViewMode);
        return normalized;
    }
}

public sealed class WebGlWorkbenchCameraState
{
    public string ProjectionMode { get; set; } = WebGlWorkbenchProjectionModes.Orthographic;

    public string ViewMode { get; set; } = WebGlWorkbenchCameraViewModes.XY;

    public double Zoom { get; set; } = 1;

    public double TargetX { get; set; }

    public double TargetY { get; set; }

    public double TargetZ { get; set; }

    public double Distance { get; set; } = 1180;

    public double Azimuth { get; set; } = -0.72d;

    public double Polar { get; set; } = 1.08d;
}

public sealed class WebGlWorkbenchDiagnostics
{
    public int CreateCount { get; set; }

    public int UpdateCount { get; set; }

    public int RenderCount { get; set; }

    public int DragCommitCount { get; set; }

    public int ConnectionCommitCount { get; set; }

    public int ExportCount { get; set; }

    public int NodeCount { get; set; }

    public int EdgeCount { get; set; }

    public bool DeterministicMode { get; set; }

    public string ProjectionMode { get; set; } = WebGlWorkbenchProjectionModes.Orthographic;

    public string ViewMode { get; set; } = WebGlWorkbenchCameraViewModes.XY;
}

public sealed class WebGlAutomationSnapshot
{
    public string SurfaceId { get; set; } = string.Empty;

    public string SceneKey { get; set; } = string.Empty;

    public string ProjectionMode { get; set; } = WebGlWorkbenchProjectionModes.Orthographic;

    public string ViewMode { get; set; } = WebGlWorkbenchCameraViewModes.XY;

    public string ActiveViewPreset { get; set; } = WebGlWorkbenchViewPresets.Overview;

    public string LayoutMode { get; set; } = WebGlWorkbenchLayoutModes.CenterLane;

    public string ToolMode { get; set; } = WebGlWorkbenchToolModes.Select;

    public string NodeInfoMode { get; set; } = WebGlWorkbenchNodeInfoModes.Detailed;

    public double NodeSpacingFactor { get; set; } = 1;

    public bool DeterministicMode { get; set; } = true;

    public bool ShowGrid { get; set; } = true;

    public bool ShowAnchors { get; set; } = true;

    public bool ShowEdgeLabels { get; set; } = true;

    public bool TransparentGround { get; set; } = true;

    public bool IsStageMaximized { get; set; }

    public int ViewportWidth { get; set; }

    public int ViewportHeight { get; set; }

    public List<WebGlAutomationNodeSnapshot> Nodes { get; set; } = [];

    public List<WebGlAutomationEdgeSnapshot> Edges { get; set; } = [];

    public List<WebGlAutomationAnchorSnapshot> Anchors { get; set; } = [];
}

public sealed class WebGlAutomationNodeSnapshot
{
    public string Id { get; set; } = string.Empty;

    public string Kind { get; set; } = string.Empty;

    public string Family { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Subtitle { get; set; } = string.Empty;

    public double X { get; set; }

    public double Y { get; set; }

    public double Z { get; set; }

    public double Left { get; set; }

    public double Top { get; set; }

    public double Width { get; set; }

    public double Height { get; set; }

    public double SceneWidth { get; set; }

    public double SceneHeight { get; set; }

    public double SceneDepth { get; set; }

    public bool Selected { get; set; }
}

public sealed class WebGlAutomationEdgeSnapshot
{
    public string Id { get; set; } = string.Empty;

    public string SourceNodeId { get; set; } = string.Empty;

    public string SourceAnchorId { get; set; } = string.Empty;

    public string SourcePortId { get; set; } = string.Empty;

    public string TargetNodeId { get; set; } = string.Empty;

    public string TargetAnchorId { get; set; } = string.Empty;

    public string TargetPortId { get; set; } = string.Empty;

    public string Kind { get; set; } = string.Empty;

    public string CategoryKey { get; set; } = string.Empty;

    public bool IsPrimaryPath { get; set; }

    public double Emphasis { get; set; }

    public double Opacity { get; set; }

    public double X { get; set; }

    public double Y { get; set; }
}

public sealed class WebGlAutomationAnchorSnapshot
{
    public string Id { get; set; } = string.Empty;

    public string NodeId { get; set; } = string.Empty;

    public string PortId { get; set; } = string.Empty;

    public string Role { get; set; } = string.Empty;

    public string Side { get; set; } = string.Empty;

    public double X { get; set; }

    public double Y { get; set; }
}
