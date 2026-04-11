namespace CanDoItAll.Components.CanvasLib;

public sealed class ConnectorPathPrimitiveSegment
{
    public string Label { get; init; } = string.Empty;

    public string Tone { get; init; } = "neutral";

    public double StartX { get; init; }

    public double StartY { get; init; }

    public double EndX { get; init; }

    public double EndY { get; init; }
}

public sealed class ConnectorPathPrimitiveSnapshot
{
    public string TestHookId { get; init; } = "connector-path-primitive";

    public string Label { get; init; } = "Connector path primitive";

    public string Title { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public string StatePill { get; init; } = string.Empty;

    public IReadOnlyList<string> Metrics { get; init; } = [];

    public IReadOnlyList<ConnectorPathPrimitiveSegment> Segments { get; init; } = [];
}

public static class ConnectorPathPrimitiveFactory
{
    public static ConnectorPathPrimitiveSnapshot CreateForWorkbench(CanvasWorkbenchSurface surface)
    {
        ArgumentNullException.ThrowIfNull(surface);

        var lookup = surface.Nodes.ToDictionary(node => node.Id, StringComparer.Ordinal);
        var segments = surface.Links
            .Select(link => CreateSegment(link, lookup))
            .Where(segment => segment is not null)
            .Take(4)
            .Cast<ConnectorPathPrimitiveSegment>()
            .ToList();

        if (segments.Count == 0)
        {
            segments =
            [
                new ConnectorPathPrimitiveSegment { Label = "Contains", Tone = "accent", StartX = 28, StartY = 32, EndX = 152, EndY = 88 },
                new ConnectorPathPrimitiveSegment { Label = "Depends", Tone = "info", StartX = 152, StartY = 88, EndX = 260, EndY = 36 }
            ];
        }

        return new ConnectorPathPrimitiveSnapshot
        {
            Title = "Relationship paths now have a named connector primitive instead of being routed only inside the monolithic runtime",
            Summary = "Parent-child edges, branch links, and future annotated relationships can share one routing and decoration boundary across both graph surfaces.",
            StatePill = segments.Count > 0 ? "Routed" : "Idle",
            Metrics =
            [
                $"{surface.Links.Count} total links",
                $"{segments.Count} preview segments",
                $"{surface.Links.Count(link => link.IsUserAuthored)} user-authored links",
                $"{surface.Links.Select(link => link.Kind).Distinct(StringComparer.OrdinalIgnoreCase).Count()} link kinds"
            ],
            Segments = segments
        };
    }

    private static ConnectorPathPrimitiveSegment? CreateSegment(
        CanvasWorkbenchLink link,
        IReadOnlyDictionary<string, CanvasWorkbenchNode> nodes)
    {
        if (!nodes.TryGetValue(link.SourceId, out var source) || !nodes.TryGetValue(link.TargetId, out var target))
        {
            return null;
        }

        var sourceAnchor = CanvasWorkbenchPortGeometry.ResolveOutputAnchor(source, link.SourcePortId);
        var targetAnchor = CanvasWorkbenchPortGeometry.ResolveInputAnchor(target, link.TargetPortId);

        return new ConnectorPathPrimitiveSegment
        {
            Label = string.IsNullOrWhiteSpace(link.Kind) ? "Link" : link.Kind,
            Tone = link.Kind switch
            {
                "depends-on" => "warning",
                "branch" => "info",
                "contains" => "accent",
                _ => "neutral"
            },
            StartX = sourceAnchor?.X ?? source.X + 48,
            StartY = sourceAnchor?.Y ?? source.Y + 28,
            EndX = targetAnchor?.X ?? target.X + 48,
            EndY = targetAnchor?.Y ?? target.Y + 28
        };
    }
}


