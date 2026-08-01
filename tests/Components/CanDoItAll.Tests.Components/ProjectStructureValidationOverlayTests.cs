using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workbench.CanvasAdapters;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Tests.Components;

public sealed class ProjectStructureValidationOverlayTests
{
    [Fact]
    public void Blocked_nodes_surface_health_annotations()
    {
        var node = CreateNode(
            "blocked-note",
            ProjectObjectType.Note,
            "Blocked",
            priority: 4);

        var annotations = ProjectStructureValidationOverlay.BuildNodeAnnotations(node);

        Assert.Contains(annotations, annotation => annotation.ActionId == "summary");
        Assert.Contains(annotations, annotation => annotation.Kind == "health");
        Assert.Contains(annotations, annotation => annotation.Kind == "priority");
    }

    [Fact]
    public void Summary_counts_blocked_and_priority_nodes()
    {
        var blocked = CreateNode("blocked", ProjectObjectType.Note, "Blocked", priority: 2);
        var review = CreateNode("review", ProjectObjectType.Decision, "Pending review", priority: 1);
        var priority = CreateNode("priority", ProjectObjectType.Decision, "Active", priority: 5);
        var surface = new ProjectStructureSurface(
            Guid.NewGuid(),
            "Project structure",
            [blocked, review, priority],
            [],
            null);

        var summary = ProjectStructureValidationOverlay.BuildSummary(surface, [blocked.Id, priority.Id]);

        Assert.True(summary.IsVisible);
        Assert.Equal(1, summary.BlockedCount);
        Assert.Equal(0, summary.ReviewCount);
        Assert.Equal(1, summary.PriorityCount);
        Assert.Equal(2, summary.SelectedIssueCount);
        Assert.Contains("Blocked", summary.SpotlightNodes[0], StringComparison.OrdinalIgnoreCase);
    }

    private static ProjectStructureNode CreateNode(string id, ProjectObjectType objectType, string status, int priority)
        => new(
            id,
            null,
            objectType,
            string.Empty,
            $"{status} node",
            string.Empty,
            status,
            string.Empty,
            string.Empty,
            string.Empty,
            null,
            string.Empty,
            string.Empty,
            string.Empty,
            0,
            0,
            new ProjectObjectVisualProfile("rect", "#2563eb", "ID", "Node"),
            [],
            string.Empty,
            0,
            string.Empty,
            string.Empty,
            string.Empty,
            [],
            priority);
}


