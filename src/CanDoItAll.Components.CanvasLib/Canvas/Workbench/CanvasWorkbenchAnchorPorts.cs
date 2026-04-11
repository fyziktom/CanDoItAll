namespace CanDoItAll.Components.CanvasLib;

public static class CanvasWorkbenchAnchorPorts
{
    public const string Left = "anchor:left";
    public const string Right = "anchor:right";
    public const string Top = "anchor:top";
    public const string Bottom = "anchor:bottom";

    public static bool IsInputPortId(string? portId)
    {
        return string.Equals(portId, Left, StringComparison.Ordinal) ||
               string.Equals(portId, Top, StringComparison.Ordinal);
    }

    public static bool IsOutputPortId(string? portId)
    {
        return string.Equals(portId, Right, StringComparison.Ordinal) ||
               string.Equals(portId, Bottom, StringComparison.Ordinal);
    }

    public static string ResolveSide(string? portId)
    {
        if (string.Equals(portId, Left, StringComparison.Ordinal))
        {
            return "left";
        }

        if (string.Equals(portId, Right, StringComparison.Ordinal))
        {
            return "right";
        }

        if (string.Equals(portId, Top, StringComparison.Ordinal))
        {
            return "top";
        }

        if (string.Equals(portId, Bottom, StringComparison.Ordinal))
        {
            return "bottom";
        }

        return string.Empty;
    }
}
