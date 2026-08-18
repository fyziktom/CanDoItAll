using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workbench.CanvasAdapters;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Tests.Components.ProjectStructure;

public sealed class ProjectStructureGraphAdapterTests
{
    [Fact]
    public void Node_visual_profiles_drive_canvas_palettes()
    {
        var adapter = new ProjectStructureGraphAdapter();
        var actionCatalog = new ProjectStructureActionCatalogAdapter();
        var surface = new ProjectStructureSurface(
            Guid.NewGuid(),
            "Palette validation",
            [
                CreateFileNode("file-pdf", "pdf", "#dc2626", ProjectObjectPaletteKeys.Danger),
                CreateFileNode("file-excel", "excel", "#16a34a", ProjectObjectPaletteKeys.Success),
                CreateFileNode("file-docx", "docx", "#2563eb", ProjectObjectPaletteKeys.Info),
                CreateFileNode("file-mermaid", "mermaid", "#7c3aed", ProjectObjectPaletteKeys.Secondary),
                CreateFileNode("file-log", "log", "#475569", ProjectObjectPaletteKeys.Neutral)
            ],
            [],
            null);

        var canvasSurface = adapter.BuildSurface(
            surface,
            new CanvasWorkbenchUiState(),
            new CanvasWorkbenchChrome(),
            actionCatalog);

        Assert.Equal(ProjectObjectPaletteKeys.Danger, canvasSurface.Nodes.Single(node => node.Id == "file-pdf").PaletteKey);
        Assert.Equal(ProjectObjectPaletteKeys.Success, canvasSurface.Nodes.Single(node => node.Id == "file-excel").PaletteKey);
        Assert.Equal(ProjectObjectPaletteKeys.Info, canvasSurface.Nodes.Single(node => node.Id == "file-docx").PaletteKey);
        Assert.Equal(ProjectObjectPaletteKeys.Secondary, canvasSurface.Nodes.Single(node => node.Id == "file-mermaid").PaletteKey);
        Assert.Equal(ProjectObjectPaletteKeys.Neutral, canvasSurface.Nodes.Single(node => node.Id == "file-log").PaletteKey);
    }

    [Fact]
    public void Shared_parent_project_nodes_render_as_read_only_ghosts()
    {
        var adapter = new ProjectStructureGraphAdapter();
        var actionCatalog = new ProjectStructureActionCatalogAdapter();
        var surface = new ProjectStructureSurface(
            Guid.NewGuid(),
            "Hierarchy validation",
            [
                CreateProjectNode(
                    "project-related-parent:11111111-1111-1111-1111-111111111111",
                    "Shared parent",
                    ProjectStructureProjectRole.AdditionalParentProject)
            ],
            [],
            null);

        var canvasSurface = adapter.BuildSurface(
            surface,
            new CanvasWorkbenchUiState(),
            new CanvasWorkbenchChrome(),
            actionCatalog);

        var sharedParentNode = Assert.Single(canvasSurface.Nodes);
        Assert.True(sharedParentNode.IsReadOnly);
        Assert.True(sharedParentNode.IsPreviewOnly);
        Assert.Equal("neutral", sharedParentNode.PaletteKey);
    }

    [Fact]
    public void Nodes_expose_id_info_and_tree_annotations()
    {
        var adapter = new ProjectStructureGraphAdapter();
        var actionCatalog = new ProjectStructureActionCatalogAdapter();
        var surface = new ProjectStructureSurface(
            Guid.NewGuid(),
            "Clipboard annotations",
            [
                CreateFileNode("custom:1234567890abcdef", "pdf", "#dc2626", ProjectObjectPaletteKeys.Danger)
            ],
            [],
            null);

        var canvasSurface = adapter.BuildSurface(
            surface,
            new CanvasWorkbenchUiState(),
            new CanvasWorkbenchChrome(),
            actionCatalog);

        var node = Assert.Single(canvasSurface.Nodes);
        Assert.Collection(
            node.Annotations.Take(3),
            annotation =>
            {
                Assert.Equal("copy-id", annotation.ActionId);
                Assert.Equal("ID", annotation.Label);
            },
            annotation =>
            {
                Assert.Equal("copy-info", annotation.ActionId);
                Assert.Equal("INF", annotation.Label);
            },
            annotation =>
            {
                Assert.Equal("copy-subtree-ids", annotation.ActionId);
                Assert.Equal("Tree", annotation.Label);
            });
    }

    [Fact]
    public void Managed_media_does_not_project_unsigned_canvas_preview_routes()
    {
        var adapter = new ProjectStructureGraphAdapter();
        var actionCatalog = new ProjectStructureActionCatalogAdapter();
        var image = CreateFileNode("image", "image", "#2563eb", ProjectObjectPaletteKeys.Info) with
        {
            ObjectType = ProjectObjectType.ImageAsset,
            Route = "/storage/objects/preview?ref=unsigned-display-reference",
            MediaRelativePath = "managed-files/project-media/image.png",
            MediaContentType = "image/png",
            MediaOriginalFileName = "image.png"
        };
        var surface = new ProjectStructureSurface(
            Guid.NewGuid(),
            "Safe media projection",
            [image],
            [],
            null);

        var canvasSurface = adapter.BuildSurface(
            surface,
            new CanvasWorkbenchUiState(),
            new CanvasWorkbenchChrome(),
            actionCatalog);

        var canvasNode = Assert.Single(canvasSurface.Nodes);
        Assert.Empty(canvasNode.MediaKind);
        Assert.Empty(canvasNode.MediaPreviewUrl);
        Assert.Equal("image.png", canvasNode.MediaFileName);
        Assert.Equal("image/png", canvasNode.MediaContentType);
    }

    [Fact]
    public void Parent_and_selection_indexes_preserve_canvas_mapping_behavior()
    {
        var adapter = new ProjectStructureGraphAdapter();
        var actionCatalog = new ProjectStructureActionCatalogAdapter();
        var parent = CreateFileNode("parent", "pdf", "#dc2626", ProjectObjectPaletteKeys.Danger);
        var child = CreateFileNode("child", "docx", "#2563eb", ProjectObjectPaletteKeys.Info) with
        {
            ParentId = parent.Id
        };
        var surface = new ProjectStructureSurface(
            Guid.NewGuid(),
            "Indexed canvas mapping",
            [parent, child],
            [],
            null);

        var canvasSurface = adapter.BuildSurface(
            surface,
            new CanvasWorkbenchUiState
            {
                SelectedNodeIds = [parent.Id, child.Id]
            },
            new CanvasWorkbenchChrome(),
            actionCatalog);

        var canvasParent = Assert.Single(canvasSurface.Nodes, node => node.Id == parent.Id);
        var canvasChild = Assert.Single(canvasSurface.Nodes, node => node.Id == child.Id);

        Assert.True(canvasParent.IsCollapsible);
        Assert.False(canvasChild.IsCollapsible);
        Assert.Equal(
            "Delete selected",
            canvasParent.ContextActions.Single(action => action.ActionId == "delete").Label);
        Assert.Equal(
            "Delete selected",
            canvasChild.ContextActions.Single(action => action.ActionId == "delete").Label);
    }

    private static ProjectStructureNode CreateFileNode(string id, string subtype, string accentColor, string paletteKey)
        => new(
            id,
            null,
            ProjectObjectType.File,
            subtype,
            subtype.ToUpperInvariant(),
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            null,
            string.Empty,
            string.Empty,
            string.Empty,
            0,
            0,
            new ProjectObjectVisualProfile("rect", accentColor, subtype.ToUpperInvariant(), subtype, paletteKey),
            [],
            string.Empty,
            0,
            string.Empty,
            string.Empty,
            string.Empty,
            [],
            0);

    private static ProjectStructureNode CreateProjectNode(
        string id,
        string title,
        ProjectStructureProjectRole projectRole)
        => new(
            id,
            null,
            ProjectObjectType.ProjectRoot,
            string.Empty,
            title,
            string.Empty,
            "Active",
            string.Empty,
            "/projects/11111111-1111-1111-1111-111111111111/structure",
            "project",
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            string.Empty,
            string.Empty,
            string.Empty,
            0,
            0,
            new ProjectObjectVisualProfile("hex", "#94a3b8", "PR", "Parent"),
            ["Shared parent"],
            string.Empty,
            0,
            string.Empty,
            string.Empty,
            string.Empty,
            [],
            0,
            ProjectRole: projectRole,
            RelatedProjectId: Guid.Parse("11111111-1111-1111-1111-111111111111"));
}
