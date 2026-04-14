namespace CanDoItAll.Components.CanvasLib;

public readonly record struct CanvasWorkbenchNodeSize(double Width, double Height);

public sealed class CanvasLayoutNodeBox
{
    public string NodeId { get; init; } = string.Empty;

    public double X { get; set; }

    public double Y { get; set; }

    public double Width { get; init; }

    public double Height { get; init; }

    public bool IsPinned { get; init; }

    public double Left => X - (Width / 2d);

    public double Right => X + (Width / 2d);

    public double Top => Y - (Height / 2d);

    public double Bottom => Y + (Height / 2d);

    public CanvasLayoutNodeBox Clone()
    {
        return new CanvasLayoutNodeBox
        {
            NodeId = NodeId,
            X = X,
            Y = Y,
            Width = Width,
            Height = Height,
            IsPinned = IsPinned
        };
    }

    public static CanvasLayoutNodeBox FromNode(CanvasWorkbenchNode node, bool isPinned = false)
    {
        ArgumentNullException.ThrowIfNull(node);

        var size = CanvasWorkbenchNodeMetrics.ResolveSize(node);
        return new CanvasLayoutNodeBox
        {
            NodeId = node.Id,
            X = node.X,
            Y = node.Y,
            Width = size.Width,
            Height = size.Height,
            IsPinned = isPinned
        };
    }
}

public enum CanvasLayoutAxisPreference
{
    Auto = 0,
    Horizontal = 1,
    Vertical = 2
}

public sealed class CanvasLayoutCollisionOptions
{
    public double MinimumGapX { get; set; } = 48d;

    public double MinimumGapY { get; set; } = 40d;

    public double PreferredAxisBias { get; set; } = 1.4d;

    public CanvasLayoutAxisPreference AxisPreference { get; set; } = CanvasLayoutAxisPreference.Auto;

    public int MaxIterations { get; set; } = 24;

    public double SeparationEpsilon { get; set; } = 1d;
}

public sealed class CanvasLayoutExpansionOptions
{
    public double HorizontalFactor { get; set; } = 1.16d;

    public double VerticalFactor { get; set; } = 1.12d;

    public double MinimumOffset { get; set; } = 18d;
}

public static class CanvasWorkbenchNodeMetrics
{
    public static CanvasWorkbenchNodeSize ResolveSize(CanvasWorkbenchNode node)
    {
        ArgumentNullException.ThrowIfNull(node);

        var baseSize = node.Family?.ToLowerInvariant() switch
        {
            "root" => new CanvasWorkbenchNodeSize(288d, 210d),
            "group" => new CanvasWorkbenchNodeSize(272d, 196d),
            "special" => new CanvasWorkbenchNodeSize(248d, 178d),
            _ => new CanvasWorkbenchNodeSize(256d, 190d)
        };
        var portRows = Math.Max(node.InputPorts.Count, node.OutputPorts.Count);
        if (portRows <= 0)
        {
            return baseSize;
        }

        return new CanvasWorkbenchNodeSize(
            Math.Max(baseSize.Width, 336d),
            Math.Max(baseSize.Height, 188d + Math.Max(0, portRows - 1) * 28d));
    }
}

public static class CanvasLayoutSpacingExpander
{
    public static IReadOnlyList<CanvasLayoutNodeBox> Expand(
        IReadOnlyList<CanvasLayoutNodeBox> nodes,
        CanvasLayoutExpansionOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(nodes);

        if (nodes.Count == 0)
        {
            return [];
        }

        options ??= new CanvasLayoutExpansionOptions();

        var centerX = nodes.Average(node => node.X);
        var centerY = nodes.Average(node => node.Y);
        var expanded = new List<CanvasLayoutNodeBox>(nodes.Count);

        for (var index = 0; index < nodes.Count; index++)
        {
            var node = nodes[index];
            var clone = node.Clone();
            if (!clone.IsPinned)
            {
                var offsetX = clone.X - centerX;
                var offsetY = clone.Y - centerY;
                if (Math.Abs(offsetX) < 0.5d && Math.Abs(offsetY) < 0.5d)
                {
                    offsetX = (index % 2 == 0 ? -1d : 1d) * options.MinimumOffset;
                    offsetY = (index % 3 == 0 ? -1d : 1d) * (options.MinimumOffset * 0.6d);
                }

                clone.X = centerX + (offsetX * options.HorizontalFactor);
                clone.Y = centerY + (offsetY * options.VerticalFactor);
            }

            expanded.Add(clone);
        }

        return expanded;
    }
}

public static class CanvasLayoutCollisionResolver
{
    public static IReadOnlyList<CanvasLayoutNodeBox> Resolve(
        IReadOnlyList<CanvasLayoutNodeBox> nodes,
        CanvasLayoutCollisionOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(nodes);

        if (nodes.Count == 0)
        {
            return [];
        }

        options ??= new CanvasLayoutCollisionOptions();
        var working = nodes
            .Select(node => node.Clone())
            .ToList();

        for (var iteration = 0; iteration < options.MaxIterations; iteration++)
        {
            var movedAny = false;
            for (var leftIndex = 0; leftIndex < working.Count - 1; leftIndex++)
            {
                var left = working[leftIndex];
                for (var rightIndex = leftIndex + 1; rightIndex < working.Count; rightIndex++)
                {
                    var right = working[rightIndex];
                    if (left.IsPinned && right.IsPinned)
                    {
                        continue;
                    }

                    var deltaX = right.X - left.X;
                    var deltaY = right.Y - left.Y;
                    var overlapX = ((left.Width + right.Width) / 2d) + options.MinimumGapX - Math.Abs(deltaX);
                    var overlapY = ((left.Height + right.Height) / 2d) + options.MinimumGapY - Math.Abs(deltaY);
                    if (overlapX <= 0d || overlapY <= 0d)
                    {
                        continue;
                    }

                    var axis = ResolveAxis(overlapX, overlapY, options);
                    if (axis == CanvasLayoutAxisPreference.Horizontal)
                    {
                        var direction = ResolveDirection(deltaX, left.NodeId, right.NodeId);
                        ApplyHorizontalShift(left, right, direction * (overlapX + options.SeparationEpsilon));
                    }
                    else
                    {
                        var direction = ResolveDirection(deltaY, left.NodeId, right.NodeId);
                        ApplyVerticalShift(left, right, direction * (overlapY + options.SeparationEpsilon));
                    }

                    movedAny = true;
                }
            }

            if (!movedAny)
            {
                break;
            }
        }

        return working;
    }

    private static CanvasLayoutAxisPreference ResolveAxis(
        double overlapX,
        double overlapY,
        CanvasLayoutCollisionOptions options)
    {
        return options.AxisPreference switch
        {
            CanvasLayoutAxisPreference.Horizontal => overlapX <= overlapY * options.PreferredAxisBias
                ? CanvasLayoutAxisPreference.Horizontal
                : CanvasLayoutAxisPreference.Vertical,
            CanvasLayoutAxisPreference.Vertical => overlapY <= overlapX * options.PreferredAxisBias
                ? CanvasLayoutAxisPreference.Vertical
                : CanvasLayoutAxisPreference.Horizontal,
            _ => overlapX <= overlapY
                ? CanvasLayoutAxisPreference.Horizontal
                : CanvasLayoutAxisPreference.Vertical
        };
    }

    private static double ResolveDirection(double delta, string leftNodeId, string rightNodeId)
    {
        if (Math.Abs(delta) > 0.01d)
        {
            return Math.Sign(delta);
        }

        return string.Compare(leftNodeId, rightNodeId, StringComparison.Ordinal) <= 0
            ? 1d
            : -1d;
    }

    private static void ApplyHorizontalShift(CanvasLayoutNodeBox left, CanvasLayoutNodeBox right, double shift)
    {
        if (left.IsPinned)
        {
            right.X += shift;
            return;
        }

        if (right.IsPinned)
        {
            left.X -= shift;
            return;
        }

        var halfShift = shift / 2d;
        left.X -= halfShift;
        right.X += halfShift;
    }

    private static void ApplyVerticalShift(CanvasLayoutNodeBox left, CanvasLayoutNodeBox right, double shift)
    {
        if (left.IsPinned)
        {
            right.Y += shift;
            return;
        }

        if (right.IsPinned)
        {
            left.Y -= shift;
            return;
        }

        var halfShift = shift / 2d;
        left.Y -= halfShift;
        right.Y += halfShift;
    }
}
