namespace CanDoItAll.Components.CanvasLib;

public sealed class ViewportControllerOptions
{
    public double MinZoom { get; set; } = 0.15;

    public double MaxZoom { get; set; } = 1.75;

    public double DefaultZoom { get; set; } = 1;

    public double DefaultPanX { get; set; } = 90;

    public double DefaultPanY { get; set; } = 110;

    public double ClampMarginX { get; set; } = 160;

    public double ClampMarginY { get; set; } = 140;

    public double FitPadding { get; set; } = 120;
}

public sealed class ViewportPoint
{
    public double X { get; set; }

    public double Y { get; set; }
}

public sealed class ViewportFrame
{
    public double Width { get; set; }

    public double Height { get; set; }
}

public sealed class ViewportState
{
    public double Zoom { get; set; }

    public double PanX { get; set; }

    public double PanY { get; set; }
}

public sealed class ViewportSceneBounds
{
    public double MinX { get; set; }

    public double MaxX { get; set; }

    public double MinY { get; set; }

    public double MaxY { get; set; }

    public double Width => MaxX - MinX;

    public double Height => MaxY - MinY;

    public ViewportPoint Center => new()
    {
        X = MinX + (Width / 2d),
        Y = MinY + (Height / 2d)
    };

    public static ViewportSceneBounds? FromNodes(IReadOnlyList<CanvasWorkbenchNode>? nodes)
    {
        if (nodes is null || nodes.Count == 0)
        {
            return null;
        }

        var seeded = false;
        var bounds = new ViewportSceneBounds();
        foreach (var node in nodes)
        {
            var halfWidth = EstimateHalfWidth(node);
            var halfHeight = EstimateHalfHeight(node);

            var minX = node.X - halfWidth;
            var maxX = node.X + halfWidth;
            var minY = node.Y - halfHeight;
            var maxY = node.Y + halfHeight;

            if (!seeded)
            {
                bounds.MinX = minX;
                bounds.MaxX = maxX;
                bounds.MinY = minY;
                bounds.MaxY = maxY;
                seeded = true;
                continue;
            }

            bounds.MinX = Math.Min(bounds.MinX, minX);
            bounds.MaxX = Math.Max(bounds.MaxX, maxX);
            bounds.MinY = Math.Min(bounds.MinY, minY);
            bounds.MaxY = Math.Max(bounds.MaxY, maxY);
        }

        return seeded ? bounds : null;
    }

    private static double EstimateHalfWidth(CanvasWorkbenchNode node)
    {
        var longestText = Math.Max(node.Title.Length, node.Subtitle.Length);
        return Math.Clamp(112 + (longestText * 1.45), 112, 182);
    }

    private static double EstimateHalfHeight(CanvasWorkbenchNode node)
    {
        var subtitleWeight = string.IsNullOrWhiteSpace(node.Subtitle) ? 0 : 12;
        var chipWeight = (node.Chips.Count + node.FooterChips.Count) * 8;
        var annotationWeight = node.Annotations.Count * 6;
        return Math.Clamp(72 + subtitleWeight + chipWeight + annotationWeight, 72, 128);
    }
}

public sealed class ViewportController
{
    public ViewportController(ViewportControllerOptions? options = null)
    {
        Options = options ?? new ViewportControllerOptions();
    }

    public ViewportControllerOptions Options { get; }

    public ViewportState Normalize(CanvasWorkbenchUiState? uiState)
        => new()
        {
            Zoom = NormalizeZoom(uiState?.Zoom ?? Options.DefaultZoom),
            PanX = uiState?.PanX ?? Options.DefaultPanX,
            PanY = uiState?.PanY ?? Options.DefaultPanY
        };

    public ViewportState Normalize(ViewportState? state)
        => new()
        {
            Zoom = NormalizeZoom(state?.Zoom ?? Options.DefaultZoom),
            PanX = state?.PanX ?? Options.DefaultPanX,
            PanY = state?.PanY ?? Options.DefaultPanY
        };

    public double NormalizeZoom(double zoom) => Math.Clamp(zoom, Options.MinZoom, Options.MaxZoom);

    public ViewportState ClampToScene(ViewportState state, ViewportSceneBounds? bounds, ViewportFrame hostFrame)
    {
        var normalized = Normalize(state);
        if (bounds is null || hostFrame.Width <= 0 || hostFrame.Height <= 0)
        {
            return RoundState(normalized);
        }

        var marginX = Math.Max(Options.ClampMarginX, hostFrame.Width * 0.5d);
        var marginY = Math.Max(Options.ClampMarginY, hostFrame.Height * 0.5d);

        var minPanX = hostFrame.Width - marginX - (bounds.MaxX * normalized.Zoom);
        var maxPanX = marginX - (bounds.MinX * normalized.Zoom);
        var minPanY = hostFrame.Height - marginY - (bounds.MaxY * normalized.Zoom);
        var maxPanY = marginY - (bounds.MinY * normalized.Zoom);

        return RoundState(new ViewportState
        {
            Zoom = normalized.Zoom,
            PanX = Math.Clamp(normalized.PanX, Math.Min(minPanX, maxPanX), Math.Max(minPanX, maxPanX)),
            PanY = Math.Clamp(normalized.PanY, Math.Min(minPanY, maxPanY), Math.Max(minPanY, maxPanY))
        });
    }

    public ViewportState CreateFitViewTarget(ViewportSceneBounds? bounds, ViewportFrame hostFrame)
    {
        if (bounds is null || hostFrame.Width <= 0 || hostFrame.Height <= 0)
        {
            return Normalize((CanvasWorkbenchUiState?)null);
        }

        var width = Math.Max(bounds.Width, 320);
        var height = Math.Max(bounds.Height, 240);
        var zoom = NormalizeZoom(Math.Min(
            (hostFrame.Width - Options.FitPadding) / width,
            (hostFrame.Height - Options.FitPadding) / height));

        return ClampToScene(new ViewportState
        {
            Zoom = zoom,
            PanX = (hostFrame.Width / 2d) - ((bounds.MinX + (width / 2d)) * zoom),
            PanY = (hostFrame.Height / 2d) - ((bounds.MinY + (height / 2d)) * zoom)
        }, bounds, hostFrame);
    }

    public ViewportState CreateFocusTarget(
        ViewportPoint focusPoint,
        double zoom,
        ViewportSceneBounds? bounds,
        ViewportFrame hostFrame)
    {
        var normalizedZoom = NormalizeZoom(zoom);
        return ClampToScene(new ViewportState
        {
            Zoom = normalizedZoom,
            PanX = (hostFrame.Width / 2d) - (focusPoint.X * normalizedZoom),
            PanY = (hostFrame.Height / 2d) - (focusPoint.Y * normalizedZoom)
        }, bounds, hostFrame);
    }

    public ViewportState ZoomAroundPoint(
        ViewportState current,
        double zoomPercent,
        ViewportPoint anchor,
        ViewportSceneBounds? bounds,
        ViewportFrame hostFrame)
    {
        var normalizedCurrent = Normalize(current);
        var nextZoom = NormalizeZoom((zoomPercent <= 0 ? 100 : zoomPercent) / 100d);
        var worldPoint = ToScene(anchor, normalizedCurrent);

        return ClampToScene(new ViewportState
        {
            Zoom = nextZoom,
            PanX = anchor.X - (worldPoint.X * nextZoom),
            PanY = anchor.Y - (worldPoint.Y * nextZoom)
        }, bounds, hostFrame);
    }

    public ViewportPoint ToScene(ViewportPoint hostPoint, ViewportState state)
    {
        var normalized = Normalize(state);
        return new ViewportPoint
        {
            X = Round((hostPoint.X - normalized.PanX) / Math.Max(normalized.Zoom, 0.001d)),
            Y = Round((hostPoint.Y - normalized.PanY) / Math.Max(normalized.Zoom, 0.001d))
        };
    }

    public ViewportPoint ToHost(ViewportPoint scenePoint, ViewportState state)
    {
        var normalized = Normalize(state);
        return new ViewportPoint
        {
            X = Round((scenePoint.X * normalized.Zoom) + normalized.PanX),
            Y = Round((scenePoint.Y * normalized.Zoom) + normalized.PanY)
        };
    }

    private static ViewportState RoundState(ViewportState state)
        => new()
        {
            Zoom = Round(state.Zoom),
            PanX = Round(state.PanX),
            PanY = Round(state.PanY)
        };

    private static double Round(double value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}

public sealed class ViewportControllerPreviewSnapshot
{
    public string TestHookId { get; set; } = "viewport-controller-preview";

    public string Label { get; set; } = "Viewport controller";

    public string Title { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public string StatePill { get; set; } = "Live";

    public IReadOnlyList<string> Metrics { get; set; } = [];

    public IReadOnlyList<ViewportControllerPreviewCard> Cards { get; set; } = [];

    public string CurrentZoomLabel { get; set; } = "100%";

    public string CurrentPanLabel { get; set; } = "0, 0";

    public string FitZoomLabel { get; set; } = "100%";

    public string FocusNodeLabel { get; set; } = "Canvas root";

    public string SceneCenterLabel { get; set; } = "0, 0";

    public double FocusXPercent { get; set; } = 50;

    public double FocusYPercent { get; set; } = 50;

    public double ViewportLeftPercent { get; set; } = 18;

    public double ViewportTopPercent { get; set; } = 18;

    public double ViewportWidthPercent { get; set; } = 52;

    public double ViewportHeightPercent { get; set; } = 44;
}

public sealed class ViewportControllerPreviewCard
{
    public string Label { get; set; } = string.Empty;

    public string ValueLabel { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;
}

public static class ViewportControllerPreviewFactory
{
    public static ViewportControllerPreviewSnapshot CreateForWorkbench(CanvasWorkbenchSurface? surface)
    {
        var controller = new ViewportController();
        var hostFrame = new ViewportFrame
        {
            Width = 1120,
            Height = 680
        };

        var nodes = surface?.Nodes ?? [];
        var selection = SelectionModel.From(surface?.UiState?.SelectedNodeIds);
        var bounds = ViewportSceneBounds.FromNodes(nodes) ?? new ViewportSceneBounds
        {
            MinX = -260,
            MaxX = 260,
            MinY = -180,
            MaxY = 180
        };

        var current = controller.Normalize(surface?.UiState);
        var focusNode = nodes.FirstOrDefault(node => string.Equals(node.Id, selection.PrimaryNodeId, StringComparison.Ordinal))
            ?? nodes.FirstOrDefault();
        var focusPoint = focusNode is null
            ? bounds.Center
            : new ViewportPoint
            {
                X = focusNode.X,
                Y = focusNode.Y
            };

        var fitTarget = controller.CreateFitViewTarget(bounds, hostFrame);
        var sceneCenter = controller.ToScene(
            new ViewportPoint
            {
                X = hostFrame.Width / 2d,
                Y = hostFrame.Height / 2d
            },
            current);

        var visibleSceneWidth = hostFrame.Width / Math.Max(current.Zoom, 0.001d);
        var visibleSceneHeight = hostFrame.Height / Math.Max(current.Zoom, 0.001d);
        var viewportLeft = sceneCenter.X - (visibleSceneWidth / 2d);
        var viewportTop = sceneCenter.Y - (visibleSceneHeight / 2d);

        return new ViewportControllerPreviewSnapshot
        {
            Title = "Shared viewport now owns zoom, pan, fit, focus, and coordinate mapping",
            Summary = "Toolbar zoom, wheel anchors, fit-to-view, selection focus, and scene-to-host conversions now route through one reusable controller instead of staying embedded in the workbench runtime.",
            StatePill = "Live",
            Metrics =
            [
                $"{Math.Round(current.Zoom * 100d)}% current zoom",
                $"{nodes.Count} nodes",
                $"{selection.SelectedNodeIds.Count} selected",
                "Host <-> scene mapping"
            ],
            Cards =
            [
                new ViewportControllerPreviewCard
                {
                    Label = "Fit to view",
                    ValueLabel = $"{Math.Round(fitTarget.Zoom * 100d)}%",
                    Summary = $"Frames {nodes.Count} nodes with {Math.Round(controller.Options.FitPadding)}px padding before clamping the stage."
                },
                new ViewportControllerPreviewCard
                {
                    Label = "Focus node",
                    ValueLabel = focusNode?.Title ?? "Canvas root",
                    Summary = $"Centers the primary target at {Round(focusPoint.X)}, {Round(focusPoint.Y)} while keeping the current zoom stable."
                },
                new ViewportControllerPreviewCard
                {
                    Label = "Wheel zoom",
                    ValueLabel = $"{Math.Round(controller.Options.MinZoom * 100d)}%-{Math.Round(controller.Options.MaxZoom * 100d)}%",
                    Summary = "Zoom remains anchored to the pointer so the same scene position stays under the cursor."
                },
                new ViewportControllerPreviewCard
                {
                    Label = "Coordinate map",
                    ValueLabel = $"{Round(sceneCenter.X)}, {Round(sceneCenter.Y)}",
                    Summary = "Host-center coordinates resolve back into scene space for focus, minimap jumps, and hit testing."
                }
            ],
            CurrentZoomLabel = $"{Math.Round(current.Zoom * 100d)}%",
            CurrentPanLabel = $"{Round(current.PanX)}, {Round(current.PanY)}",
            FitZoomLabel = $"{Math.Round(fitTarget.Zoom * 100d)}%",
            FocusNodeLabel = focusNode?.Title ?? "Canvas root",
            SceneCenterLabel = $"{Round(sceneCenter.X)}, {Round(sceneCenter.Y)}",
            FocusXPercent = ToPercent(focusPoint.X, bounds.MinX, bounds.MaxX),
            FocusYPercent = ToPercent(focusPoint.Y, bounds.MinY, bounds.MaxY),
            ViewportLeftPercent = ToPercent(viewportLeft, bounds.MinX, bounds.MaxX),
            ViewportTopPercent = ToPercent(viewportTop, bounds.MinY, bounds.MaxY),
            ViewportWidthPercent = ToSizePercent(visibleSceneWidth, bounds.Width),
            ViewportHeightPercent = ToSizePercent(visibleSceneHeight, bounds.Height)
        };
    }

    private static string Round(double value) => Math.Round(value, 1, MidpointRounding.AwayFromZero).ToString("0.#");

    private static double ToPercent(double value, double min, double max)
    {
        var span = Math.Max(max - min, 1);
        return Math.Clamp(((value - min) / span) * 100d, 8d, 92d);
    }

    private static double ToSizePercent(double size, double span)
        => Math.Clamp((size / Math.Max(span, 1d)) * 100d, 18d, 88d);
}


