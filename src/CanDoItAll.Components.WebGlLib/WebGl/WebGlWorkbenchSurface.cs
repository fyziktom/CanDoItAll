using System;

namespace CanDoItAll.Components.WebGlLib;

public static class WebGlWorkbenchProjectionModes
{
    public const string Orthographic = "orthographic";
    public const string Perspective = "perspective";
}

public static class WebGlWorkbenchCameraViewModes
{
    public const string Perspective = "perspective";
    public const string XY = "xy";
    public const string XZ = "xz";
    public const string YZ = "yz";

    public static string Normalize(string? value, string? projectionMode = null)
    {
        return value switch
        {
            XY => XY,
            XZ => XZ,
            YZ => YZ,
            Perspective => Perspective,
            _ => string.Equals(projectionMode, WebGlWorkbenchProjectionModes.Perspective, StringComparison.Ordinal)
                ? Perspective
                : XY
        };
    }

    public static string ResolveProjectionMode(string? viewMode)
    {
        return string.Equals(Normalize(viewMode), Perspective, StringComparison.Ordinal)
            ? WebGlWorkbenchProjectionModes.Perspective
            : WebGlWorkbenchProjectionModes.Orthographic;
    }
}

public static class WebGlWorkbenchViewPresets
{
    public const string Overview = "overview";
    public const string Roles = "roles";
    public const string Dependencies = "dependencies";
    public const string Branching = "branching";
    public const string Focus = "focus";
}

public static class WebGlWorkbenchLayoutModes
{
    public const string CenterLane = "center-lane";
    public const string AlternatingArc = "alternating-arc";
    public const string LayeredOrbit = "layered-orbit";
    public const string CriticalPathSpine = "critical-path-spine";
    public const string FanoutCorridor = "fanout-corridor";
    public const string RadialBurst = "radial-burst";

    public static string Normalize(string? value)
    {
        return value switch
        {
            AlternatingArc => AlternatingArc,
            LayeredOrbit => LayeredOrbit,
            CriticalPathSpine => CriticalPathSpine,
            FanoutCorridor => FanoutCorridor,
            RadialBurst => RadialBurst,
            _ => CenterLane
        };
    }
}

public static class WebGlWorkbenchConnectionActions
{
    public const string Connect = "connect";
    public const string Disconnect = "disconnect";
    public const string ReconnectTarget = "reconnect-target";
}

public static class WebGlWorkbenchAnchorRoles
{
    public const string Input = "input";
    public const string Output = "output";
}

public static class WebGlWorkbenchToolModes
{
    public const string Select = "select";
    public const string Delete = "delete";
    public const string Connect = "connect";
    public const string Reconnect = "reconnect";

    public static string Normalize(string? value)
    {
        return value switch
        {
            Delete => Delete,
            Connect => Connect,
            Reconnect => Reconnect,
            _ => Select
        };
    }
}

public static class WebGlWorkbenchNodeInfoModes
{
    public const string Detailed = "detailed";
    public const string Miniature = "miniature";
    public const string Hidden = "hidden";

    public static string Normalize(string? value)
    {
        return value switch
        {
            Miniature => Miniature,
            Hidden => Hidden,
            _ => Detailed
        };
    }
}

public sealed class WebGlWorkbenchSurface
{
    public string SurfaceId { get; set; } = string.Empty;

    public string SceneKey { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Subtitle { get; set; } = string.Empty;

    public List<WebGlWorkbenchNode> Nodes { get; set; } = [];

    public List<WebGlWorkbenchEdge> Edges { get; set; } = [];

    public WebGlWorkbenchUiState UiState { get; set; } = new();

    public WebGlWorkbenchChrome Chrome { get; set; } = new();
}

public sealed class WebGlWorkbenchNode
{
    public string Id { get; set; } = string.Empty;

    public string Kind { get; set; } = "node";

    public string Family { get; set; } = "item";

    public string Title { get; set; } = string.Empty;

    public string Subtitle { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string AccentColor { get; set; } = "#2563eb";

    public string FillColor { get; set; } = "#ffffff";

    public string BorderColor { get; set; } = "#cbd5e1";

    public double X { get; set; }

    public double Y { get; set; }

    public double Z { get; set; }

    public double Width { get; set; } = 220;

    public double Height { get; set; } = 128;

    public double Depth { get; set; } = 28;

    public bool IsReadOnly { get; set; }

    public bool IsSelected { get; set; }

    public List<string> Tags { get; set; } = [];

    public List<WebGlWorkbenchAnchor> Anchors { get; set; } = [];
}

public sealed class WebGlWorkbenchAnchor
{
    public string Id { get; set; } = string.Empty;

    public string NodeId { get; set; } = string.Empty;

    public string PortId { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public string Role { get; set; } = WebGlWorkbenchAnchorRoles.Input;

    public string Side { get; set; } = "left";

    public string CategoryKey { get; set; } = string.Empty;

    public string AccentColor { get; set; } = "#2563eb";

    public bool IsRequired { get; set; }

    public int Order { get; set; }

    public int TotalOnSide { get; set; } = 1;
}

public sealed class WebGlWorkbenchEdge
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

    public string Label { get; set; } = string.Empty;

    public string AccentColor { get; set; } = "#2563eb";

    public double DepthOffset { get; set; }

    public double Emphasis { get; set; } = 1;

    public double Opacity { get; set; } = 0.82d;

    public bool IsPrimaryPath { get; set; }

    public bool IsUserAuthored { get; set; }
}

public sealed class WebGlWorkbenchChrome
{
    public string HintText { get; set; } = string.Empty;

    public string EmptyStateTitle { get; set; } = "No process geometry";

    public string EmptyStateDescription { get; set; } = "Load one of the representative template packs to review the concept stage.";

    public List<WebGlWorkbenchChromeToolbarAction> ToolbarActions { get; set; } = [];
}

public sealed class WebGlWorkbenchChromeToolbarAction
{
    public string Id { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public string Glyph { get; set; } = string.Empty;

    public string VisualLabel { get; set; } = string.Empty;

    public string Tone { get; set; } = "neutral";

    public bool IconOnly { get; set; } = true;

    public double Width { get; set; } = 44;

    public bool IsVisible { get; set; } = true;

    public bool IsActive { get; set; }

    public bool IsToggled { get; set; }
}
