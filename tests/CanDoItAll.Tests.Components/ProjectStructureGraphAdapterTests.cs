using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workbench.CanvasAdapters;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Tests.Components;

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
            0,
            ProjectRole: projectRole,
            RelatedProjectId: Guid.Parse("11111111-1111-1111-1111-111111111111"));
}
