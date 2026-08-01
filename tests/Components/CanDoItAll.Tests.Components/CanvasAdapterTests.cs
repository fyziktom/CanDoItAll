using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workbench.CanvasAdapters;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Tests.Components;

public sealed class CanvasAdapterTests
{
    [Fact]
    public void Project_structure_graph_adapter_projects_canvas_nodes_and_links()
    {
        var rootNode = new ProjectStructureNode(
            "root",
            null,
            ProjectObjectType.ProjectRoot,
            string.Empty,
            "Project",
            "Root",
            "Ready",
            "Project root",
            "/projects/1/structure",
            "Project",
            null,
            string.Empty,
            string.Empty,
            string.Empty,
            0,
            0,
            new ProjectObjectVisualProfile("hex", "#0f172a", "PR", "Project"),
            ["Scheduled"],
            "complete",
            100,
            string.Empty,
            string.Empty,
            string.Empty,
            [],
            0);
        var noteNode = new ProjectStructureNode(
            "note-1",
            "root",
            ProjectObjectType.Note,
            string.Empty,
            "Architecture note",
            string.Empty,
            "Blocked",
            "Shared canvas adapter",
            "/projects/1/structure",
            "Note",
            null,
            string.Empty,
            string.Empty,
            string.Empty,
            120,
            160,
            new ProjectObjectVisualProfile("pill", "#059669", "NT", "Note"),
            [],
            "progress",
            25,
            string.Empty,
            string.Empty,
            string.Empty,
            [],
            0);

        var surface = new ProjectStructureSurface(
            Guid.NewGuid(),
            "Structure Project",
            [rootNode, noteNode],
            [new ProjectStructureLink("root", "note-1", ProjectObjectLinkKind.Contains, false)],
            null);
        var actionCatalog = new ProjectStructureActionCatalogAdapter();
        var adapter = new ProjectStructureGraphAdapter();
        var uiState = new CanvasWorkbenchUiState
        {
            SelectedNodeIds = ["note-1"]
        };

        var canvasSurface = adapter.BuildSurface(
            surface,
            uiState,
            new CanvasWorkbenchChrome
            {
                QuickCreateActions = actionCatalog.BuildQuickCreateActions(ProjectObjectType.Note).ToList(),
                GroupContextActions = actionCatalog.BuildGroupContextActions().ToList()
            },
            actionCatalog);

        var note = Assert.Single(canvasSurface.Nodes, node => node.Id == "note-1");
        Assert.True(note.IsInlineTextNode);
        Assert.Contains(note.Annotations, annotation => string.Equals(annotation.Kind, "health", StringComparison.Ordinal) && string.Equals(annotation.ActionId, "summary", StringComparison.Ordinal));
        Assert.Contains(note.ContextActions, action => string.Equals(action.ActionId, "open", StringComparison.Ordinal));
        Assert.True(canvasSurface.Chrome.Diagnostics.IsEnabled);
        Assert.True(canvasSurface.Chrome.Minimap.IsEnabled);
        Assert.True(canvasSurface.Chrome.Clipboard.IsEnabled);
        Assert.Single(canvasSurface.Links);
    }

    [Fact]
    public void Project_structure_placement_policy_places_sibling_below_source()
    {
        var sourceNode = new ProjectStructureNode(
            "source",
            "parent",
            ProjectObjectType.Note,
            string.Empty,
            "Source",
            string.Empty,
            "Draft",
            string.Empty,
            "/projects/1/structure",
            "Note",
            null,
            string.Empty,
            string.Empty,
            string.Empty,
            300,
            200,
            new ProjectObjectVisualProfile("pill", "#059669", "NT", "Note"),
            [],
            "progress",
            0,
            string.Empty,
            string.Empty,
            string.Empty,
            [],
            0);
        var siblingNode = sourceNode with { Id = "sibling" };
        var request = new CanvasWorkbenchCreateActionRequest(
            "add-note",
            "source",
            0,
            0,
            null,
            string.Empty,
            string.Empty,
            string.Empty,
            "sibling",
            "command",
            string.Empty,
            null);

        var placement = new ProjectStructurePlacementPolicy().ResolveCreatePlacement([sourceNode, siblingNode], sourceNode, null, request);

        Assert.Equal(300, placement.X);
        Assert.Equal(312, placement.Y);
    }

}


