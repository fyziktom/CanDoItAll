namespace CanDoItAll.Components.CanvasLib;

public sealed class CanvasWorkbenchPortAnchor
{
    public string PortId { get; init; } = string.Empty;

    public string Label { get; init; } = string.Empty;

    public string Side { get; init; } = string.Empty;

    public double X { get; init; }

    public double Y { get; init; }
}

public static class CanvasWorkbenchPortGeometry
{
    public static IReadOnlyList<CanvasWorkbenchPortAnchor> GetInputAnchors(CanvasWorkbenchNode node)
        => BuildAnchors(node, node.InputPorts, "left");

    public static IReadOnlyList<CanvasWorkbenchPortAnchor> GetOutputAnchors(CanvasWorkbenchNode node)
        => BuildAnchors(node, node.OutputPorts, "right");

    public static CanvasWorkbenchPortAnchor? ResolveInputAnchor(CanvasWorkbenchNode node, string? portId)
        => ResolveAnchor(node, node.InputPorts, portId, "left");

    public static CanvasWorkbenchPortAnchor? ResolveOutputAnchor(CanvasWorkbenchNode node, string? portId)
        => ResolveAnchor(node, node.OutputPorts, portId, "right");

    private static CanvasWorkbenchPortAnchor? ResolveAnchor(
        CanvasWorkbenchNode node,
        IReadOnlyList<CanvasWorkbenchPort> ports,
        string? portId,
        string defaultSide)
    {
        if (ports.Count == 0 || string.IsNullOrWhiteSpace(portId))
        {
            return null;
        }

        var index = ports
            .Select((port, candidateIndex) => new { port, candidateIndex })
            .FirstOrDefault(candidate => string.Equals(candidate.port.Id, portId, StringComparison.Ordinal))?.candidateIndex;
        if (index is null)
        {
            return null;
        }

        return BuildAnchor(node, ports[index.Value], index.Value, ports.Count, defaultSide);
    }

    private static IReadOnlyList<CanvasWorkbenchPortAnchor> BuildAnchors(
        CanvasWorkbenchNode node,
        IReadOnlyList<CanvasWorkbenchPort> ports,
        string defaultSide)
    {
        if (ports.Count == 0)
        {
            return [];
        }

        return ports
            .Select((port, index) => BuildAnchor(node, port, index, ports.Count, defaultSide))
            .ToList();
    }

    private static CanvasWorkbenchPortAnchor BuildAnchor(
        CanvasWorkbenchNode node,
        CanvasWorkbenchPort port,
        int index,
        int totalCount,
        string defaultSide)
    {
        var size = CanvasWorkbenchNodeMetrics.ResolveSize(node);
        var width = size.Width;
        var height = size.Height;
        var side = ResolveSide(port.Side, defaultSide);
        var horizontalInset = Math.Min(28d, width * 0.11d);
        var portTop = node.Y - (height / 2d) + Math.Min(86d, height * 0.34d);
        var portBottom = node.Y + (height / 2d) - Math.Min(24d, height * 0.12d);
        var y = totalCount <= 1
            ? node.Y
            : portTop + ((portBottom - portTop) * index / Math.Max(1, totalCount - 1));

        return new CanvasWorkbenchPortAnchor
        {
            PortId = port.Id,
            Label = string.IsNullOrWhiteSpace(port.Label) ? port.Id : port.Label,
            Side = side,
            X = side switch
            {
                "right" => node.X + (width / 2d) - horizontalInset,
                _ => node.X - (width / 2d) + horizontalInset
            },
            Y = y
        };
    }

    private static string ResolveSide(string? side, string defaultSide)
    {
        if (string.IsNullOrWhiteSpace(side))
        {
            return defaultSide;
        }

        return side.Trim().ToLowerInvariant() switch
        {
            "left" => "left",
            "right" => "right",
            _ => defaultSide
        };
    }
}
