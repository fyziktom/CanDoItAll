namespace CanDoItAll.Components.CanvasLib;

public sealed class AccessibilityMirrorItem
{
    public string Id { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string ActionLabel { get; set; } = string.Empty;

    public bool IsSelected { get; set; }

    public bool IsPrimary { get; set; }
}

public sealed class AccessibilityMirrorSnapshot
{
    public string Id { get; set; } = string.Empty;

    public string SurfaceKind { get; set; } = "canvas-workbench";

    public string RegionLabel { get; set; } = "Canvas accessibility mirror";

    public string SummaryLabel { get; set; } = string.Empty;

    public string LiveAnnouncement { get; set; } = string.Empty;

    public bool EnableDiagnostics { get; set; }

    public string TestHookId { get; set; } = "accessibility-mirror-layer";

    public List<AccessibilityMirrorItem> Items { get; set; } = [];
}

public static class AccessibilityMirrorLayerFactory
{
    public static AccessibilityMirrorSnapshot CreateForWorkbench(CanvasWorkbenchSurface surface, bool enableDiagnostics)
    {
        ArgumentNullException.ThrowIfNull(surface);

        var selection = SelectionModel
            .From(surface.UiState.SelectedNodeIds)
            .RemoveMissing(surface.Nodes.Select(node => node.Id));
        var nodeLookup = surface.Nodes.ToDictionary(node => node.Id, StringComparer.Ordinal);
        var orderedNodes = surface.Nodes
            .OrderByDescending(node => selection.SelectedNodeIds.Contains(node.Id, StringComparer.Ordinal))
            .ThenBy(node => node.Title, StringComparer.OrdinalIgnoreCase)
            .Take(12)
            .ToList();

        var primaryLabel = selection.PrimaryNodeId is not null && nodeLookup.TryGetValue(selection.PrimaryNodeId, out var primaryNode)
            ? DescribeNode(primaryNode)
            : "No primary selection";

        return new AccessibilityMirrorSnapshot
        {
            Id = $"{surface.SurfaceId}-accessibility-mirror",
            SurfaceKind = "canvas-workbench",
            RegionLabel = "Canvas accessibility mirror",
            SummaryLabel = $"{selection.SelectedNodeIds.Count} selected nodes across {surface.Nodes.Count} canvas nodes",
            LiveAnnouncement = selection.PrimaryNodeId is not null
                ? $"{primaryLabel} is the primary selection."
                : $"{surface.Nodes.Count} canvas nodes are available.",
            EnableDiagnostics = enableDiagnostics,
            TestHookId = "accessibility-mirror-layer",
            Items = orderedNodes.Select(node => new AccessibilityMirrorItem
            {
                Id = node.Id,
                Label = DescribeNode(node),
                Description = DescribeNodeDetails(node),
                ActionLabel = $"Open {DescribeNode(node)} in the inspector.",
                IsSelected = selection.SelectedNodeIds.Contains(node.Id, StringComparer.Ordinal),
                IsPrimary = string.Equals(selection.PrimaryNodeId, node.Id, StringComparison.Ordinal)
            }).ToList()
        };
    }

    public static AccessibilityMirrorSnapshot CreateForCalendar(CanvasCalendarSurface surface, bool enableDiagnostics = false)
    {
        ArgumentNullException.ThrowIfNull(surface);

        var orderedEvents = surface.Events
            .OrderByDescending(calendarEvent => string.Equals(calendarEvent.Id, surface.SelectedEventId, StringComparison.Ordinal))
            .ThenBy(calendarEvent => calendarEvent.StartUtc)
            .Take(12)
            .ToList();

        var selectedEvent = orderedEvents.FirstOrDefault(calendarEvent =>
            string.Equals(calendarEvent.Id, surface.SelectedEventId, StringComparison.Ordinal));

        return new AccessibilityMirrorSnapshot
        {
            Id = $"{surface.SurfaceId}-accessibility-mirror",
            SurfaceKind = "canvas-calendar",
            RegionLabel = "Calendar accessibility mirror",
            SummaryLabel = $"{orderedEvents.Count} calendar items mirrored for assistive navigation",
            LiveAnnouncement = selectedEvent is not null
                ? $"{selectedEvent.Title} is the selected calendar event."
                : $"{surface.Events.Count} calendar events are available.",
            EnableDiagnostics = enableDiagnostics,
            TestHookId = "calendar-accessibility-mirror-layer",
            Items = orderedEvents.Select(calendarEvent => new AccessibilityMirrorItem
            {
                Id = calendarEvent.Id,
                Label = string.IsNullOrWhiteSpace(calendarEvent.Title) ? "Untitled event" : calendarEvent.Title,
                Description = DescribeCalendarEvent(calendarEvent),
                ActionLabel = $"Review {calendarEvent.Title} event details.",
                IsSelected = string.Equals(calendarEvent.Id, surface.SelectedEventId, StringComparison.Ordinal),
                IsPrimary = string.Equals(calendarEvent.Id, surface.SelectedEventId, StringComparison.Ordinal)
            }).ToList()
        };
    }

    private static string DescribeNode(CanvasWorkbenchNode node)
    {
        if (!string.IsNullOrWhiteSpace(node.Title))
        {
            return node.Title;
        }

        if (!string.IsNullOrWhiteSpace(node.InlineText))
        {
            return node.InlineText;
        }

        return string.IsNullOrWhiteSpace(node.Kind)
            ? "Canvas node"
            : node.Kind;
    }

    private static string DescribeNodeDetails(CanvasWorkbenchNode node)
    {
        var details = new List<string>();

        if (!string.IsNullOrWhiteSpace(node.Subtitle))
        {
            details.Add(node.Subtitle);
        }

        if (!string.IsNullOrWhiteSpace(node.Status))
        {
            details.Add($"Status {node.Status}");
        }

        if (!string.IsNullOrWhiteSpace(node.BranchLabel))
        {
            details.Add($"Branch {node.BranchLabel}");
        }

        if (node.Annotations.Count > 0)
        {
            details.Add($"{node.Annotations.Count} annotations");
        }

        return string.Join(" • ", details);
    }

    private static string DescribeCalendarEvent(CanvasCalendarEvent calendarEvent)
    {
        var details = new List<string>();
        if (calendarEvent.StartUtc is not null)
        {
            details.Add(calendarEvent.StartUtc.Value.ToString("u"));
        }

        if (!string.IsNullOrWhiteSpace(calendarEvent.LocationLabel))
        {
            details.Add(calendarEvent.LocationLabel);
        }

        if (!string.IsNullOrWhiteSpace(calendarEvent.Status))
        {
            details.Add($"Status {calendarEvent.Status}");
        }

        return string.Join(" • ", details);
    }
}


