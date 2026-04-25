using CanDoItAll.Components.WebGlLib;
using CanDoItAll.Space3D.Mouse.Driver.Protocol;
using CanDoItAll.Space3D.Mouse.Driver.Scene;

namespace CanDoItAll.Space3D.Mouse.Components.Scene;

public static class MouseLabSceneFactory
{
    private const double PointerRadius = 340d;
    private const double PlaneExtent = 320d;
    private const double AxisLength = 430d;
    private const double CursorAxisLength = 108d;
    private const double OriginSize = 52d;
    private const double PointerSize = 54d;
    private const double PlaneCubeSize = 42d;
    private const double AxisTipSize = 28d;
    private const double CursorAxisTipSize = 18d;

    public const string PointerNodeId = "mouse.pointer";
    public const string OriginNodeId = "mouse.origin";
    private const string AxisXNodeId = "axis.arrow.x";
    private const string AxisYNodeId = "axis.arrow.y";
    private const string AxisZNodeId = "axis.arrow.z";
    private const string CursorRightNodeId = "cursor.arrow.right";
    private const string CursorForwardNodeId = "cursor.arrow.forward";
    private const string CursorUpNodeId = "cursor.arrow.up";

    public static WebGlWorkbenchUiState CreateDefaultUiState()
        => new()
        {
            ActiveViewPreset = WebGlWorkbenchViewPresets.Overview,
            LayoutMode = WebGlWorkbenchLayoutModes.CenterLane,
            NodeInfoMode = WebGlWorkbenchNodeInfoModes.Miniature,
            ShowAnchors = false,
            ShowDiagnostics = false,
            ShowEdgeLabels = false,
            ShowGrid = true,
            TransparentGround = false,
            Camera = CreateYForwardCameraState()
        };

    public static WebGlWorkbenchCameraState CreateYForwardCameraState()
        => new()
        {
            ProjectionMode = WebGlWorkbenchProjectionModes.Perspective,
            ViewMode = WebGlWorkbenchCameraViewModes.Perspective,
            Distance = 1480d,
            Azimuth = Math.PI,
            Polar = 1.16d,
            TargetX = 0d,
            TargetY = 0d,
            TargetZ = 0d,
            Zoom = 1d
        };

    public static WebGlWorkbenchSurface CreateSurface(
        MouseSceneSnapshot? pose,
        WebGlWorkbenchUiState uiState,
        string connectionLabel)
    {
        var nodes = new List<WebGlWorkbenchNode>
        {
            CreateNode(OriginNodeId, "neutral", "ORIGIN", "Calibration origin", connectionLabel, "#f97316", "#fff3e8", SceneVector.Zero, OriginSize),
            CreateNode("plane.front-left", "plane", "PL1", "Main plane corner", "Negative X, positive Y", "#64748b", "#f8fafc",
                new SceneVector(-PlaneExtent, PlaneExtent, 0d), PlaneCubeSize),
            CreateNode("plane.front-right", "plane", "PL2", "Main plane corner", "Positive X, positive Y", "#64748b", "#f8fafc",
                new SceneVector(PlaneExtent, PlaneExtent, 0d), PlaneCubeSize),
            CreateNode("plane.back-left", "plane", "PL3", "Main plane corner", "Negative X, negative Y", "#64748b", "#f8fafc",
                new SceneVector(-PlaneExtent, -PlaneExtent, 0d), PlaneCubeSize),
            CreateNode("plane.back-right", "plane", "PL4", "Main plane corner", "Positive X, negative Y", "#64748b", "#f8fafc",
                new SceneVector(PlaneExtent, -PlaneExtent, 0d), PlaneCubeSize),
            CreateArrowNode(AxisXNodeId, "axis-arrow", "X", "Positive X axis", "Scene right", "#ef4444",
                new SceneVector(AxisLength, 0d, 0d), OriginNodeId, AxisTipSize),
            CreateArrowNode(AxisYNodeId, "axis-arrow", "Y", "Positive Y axis", "Scene forward", "#22c55e",
                new SceneVector(0d, AxisLength, 0d), OriginNodeId, AxisTipSize),
            CreateArrowNode(AxisZNodeId, "axis-arrow", "Z", "Positive Z axis", "Scene up", "#2563eb",
                new SceneVector(0d, 0d, AxisLength), OriginNodeId, AxisTipSize)
        };

        var pointerPosition = pose is not null && pose.Valid
            ? pose.PointerPosition * PointerRadius
            : SceneVector.Zero;

        nodes.Add(CreateNode(
            PointerNodeId,
            "pointer",
            "POINTER",
            pose is not null && pose.Valid ? "Neutral-relative angular cursor" : "Waiting for pose",
            pose is not null
                ? $"Yaw {pose.ForwardAzimuthDeg:+0.0;-0.0;+0.0} deg | Pitch {pose.ForwardElevationDeg:+0.0;-0.0;+0.0} deg | Roll {pose.RollDeg:+0.0;-0.0;+0.0} deg"
                : connectionLabel,
            "#dc2626",
            pose is not null && pose.Valid ? "#fff1f2" : "#f8fafc",
            pointerPosition,
            PointerSize));

        if (pose is not null && pose.Valid)
        {
            nodes.Add(CreateArrowNode(CursorRightNodeId, "cursor-arrow", "local X", "Module right axis", "Red local orientation axis", "#ef4444",
                pointerPosition + (pose.RightAxis.Normalized() * CursorAxisLength), PointerNodeId, CursorAxisTipSize));
            nodes.Add(CreateArrowNode(CursorForwardNodeId, "cursor-arrow", "local Y", "Module forward axis", "Green local orientation axis", "#22c55e",
                pointerPosition + (pose.ForwardAxis.Normalized() * CursorAxisLength), PointerNodeId, CursorAxisTipSize));
            nodes.Add(CreateArrowNode(CursorUpNodeId, "cursor-arrow", "local Z", "Module up axis", "Blue local orientation axis", "#2563eb",
                pointerPosition + (pose.UpAxis.Normalized() * CursorAxisLength), PointerNodeId, CursorAxisTipSize));
        }

        return new WebGlWorkbenchSurface
        {
            SurfaceId = "candoitall-space3d-mouse-lab",
            SceneKey = "candoitall-space3d-mouse-lab",
            Title = "CanDoItAll Space3D mouse lab",
            Subtitle = connectionLabel,
            Nodes = nodes,
            Edges = [],
            UiState = uiState,
            Chrome = new WebGlWorkbenchChrome
            {
                HintText = "Origin is the sphere center. Neutral starts at the front pole on +Y, and cursor position stays on the front hemisphere from yaw and pitch deltas. Red is +X/right, green is +Y/forward, blue is +Z/up. Small cursor arrows show the module local X/Y/Z rotation and roll.",
                EmptyStateTitle = "No scene geometry",
                EmptyStateDescription = "The lab should always render the plane corners, XYZ axes, and the live cursor."
            }
        };
    }

    private static WebGlWorkbenchNode CreateNode(
        string id,
        string kind,
        string title,
        string subtitle,
        string description,
        string accentColor,
        string fillColor,
        SceneVector position,
        double size,
        IEnumerable<string>? tags = null)
    {
        var webGlPosition = ToWebGlPosition(position);

        return new WebGlWorkbenchNode
        {
            Id = id,
            Kind = kind,
            Family = kind,
            Title = title,
            Subtitle = subtitle,
            Description = description,
            Status = kind,
            AccentColor = accentColor,
            FillColor = fillColor,
            BorderColor = accentColor,
            X = webGlPosition.X,
            Y = webGlPosition.Y,
            Z = webGlPosition.Z,
            Width = size,
            Height = size,
            Depth = size,
            IsReadOnly = true,
            Tags = tags?.ToList() ?? []
        };
    }

    private static WebGlWorkbenchNode CreateArrowNode(
        string id,
        string kind,
        string title,
        string subtitle,
        string description,
        string accentColor,
        SceneVector endPosition,
        string startNodeId,
        double size)
        => CreateNode(
            id,
            kind,
            title,
            subtitle,
            description,
            accentColor,
            "#ffffff",
            endPosition,
            size,
            [$"arrow-start:{startNodeId}"]);

    private static (double X, double Y, double Z) ToWebGlPosition(SceneVector position)
        => (position.X, -position.Z, position.Y);
}
