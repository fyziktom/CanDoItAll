using CanDoItAll.Components.CanvasLib;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Workbench.CanvasAdapters;

public static class ProjectStructureValidationOverlay
{
    public static IReadOnlyList<CanvasWorkbenchAnnotation> BuildNodeAnnotations(ProjectStructureNode node)
    {
        var annotations = new List<CanvasWorkbenchAnnotation>();
        var status = node.Status?.Trim() ?? string.Empty;
        if (HasBlockingStatus(status))
        {
            annotations.Add(new CanvasWorkbenchAnnotation
            {
                Id = $"{node.Id}:validation",
                Kind = "validation",
                Tone = "danger",
                Label = "Blocked",
                Description = $"This {ProjectStructureCanvasCatalog.ResolveNodeLabel(node)} is marked '{status}'. Open validation tooling before continuing.",
                Icon = "QA",
                ActionId = "validate"
            });
        }
        else if (RequiresValidationReview(node, status))
        {
            annotations.Add(new CanvasWorkbenchAnnotation
            {
                Id = $"{node.Id}:validation-open",
                Kind = "validation",
                Tone = "warn",
                Label = "Review",
                Description = "This validation artifact still needs a pass or decision.",
                Icon = "QA",
                ActionId = "validate"
            });
        }

        if (node.Priority >= 4)
        {
            annotations.Add(new CanvasWorkbenchAnnotation
            {
                Id = $"{node.Id}:priority",
                Kind = "priority",
                Tone = "warn",
                Label = $"P{node.Priority}",
                Description = "High-priority work item. Keep it visible while reflowing the structure.",
                Icon = "!",
                ActionId = "open"
            });
        }

        return annotations;
    }

    public static ProjectStructureValidationOverlaySummary BuildSummary(ProjectStructureSurface surface, IReadOnlyList<string> selectedNodeIds)
    {
        var blockedCount = surface.Nodes.Count(node => HasBlockingStatus(node.Status?.Trim() ?? string.Empty));
        var reviewCount = surface.Nodes.Count(node => RequiresValidationReview(node, node.Status?.Trim() ?? string.Empty));
        var priorityCount = surface.Nodes.Count(node => node.Priority >= 4);
        var selectedIssueCount = surface.Nodes.Count(node =>
            selectedNodeIds.Contains(node.Id, StringComparer.Ordinal) &&
            (HasBlockingStatus(node.Status?.Trim() ?? string.Empty) || RequiresValidationReview(node, node.Status?.Trim() ?? string.Empty) || node.Priority >= 4));

        var spotlight = surface.Nodes
            .Where(node => HasBlockingStatus(node.Status?.Trim() ?? string.Empty) || RequiresValidationReview(node, node.Status?.Trim() ?? string.Empty))
            .Take(3)
            .Select(node => node.Title)
            .ToList();

        return new ProjectStructureValidationOverlaySummary(
            blockedCount > 0 || reviewCount > 0 || priorityCount > 0,
            blockedCount,
            reviewCount,
            priorityCount,
            selectedIssueCount,
            spotlight);
    }

    public static bool HasBlockingStatus(string status)
        => status.Contains("blocked", StringComparison.OrdinalIgnoreCase)
           || status.Contains("failed", StringComparison.OrdinalIgnoreCase)
           || status.Contains("error", StringComparison.OrdinalIgnoreCase);

    public static bool RequiresValidationReview(ProjectStructureNode node, string status)
        => node.ObjectType == ProjectObjectType.ValidationRun
           && !status.Contains("approved", StringComparison.OrdinalIgnoreCase)
           && !status.Contains("complete", StringComparison.OrdinalIgnoreCase);
}

public sealed record ProjectStructureValidationOverlaySummary(
    bool IsVisible,
    int BlockedCount,
    int ReviewCount,
    int PriorityCount,
    int SelectedIssueCount,
    IReadOnlyList<string> SpotlightNodes);


