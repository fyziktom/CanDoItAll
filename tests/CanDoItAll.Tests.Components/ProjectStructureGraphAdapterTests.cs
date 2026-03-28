using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workbench.CanvasAdapters;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Tests.Components;

public sealed class ProjectStructureGraphAdapterTests
{
    [Fact]
    public void File_nodes_use_subtype_specific_palettes()
    {
        var adapter = new ProjectStructureGraphAdapter();
        var actionCatalog = new ProjectStructureActionCatalogAdapter();
        var surface = new ProjectStructureSurface(
            Guid.NewGuid(),
            "Palette validation",
            [
                CreateFileNode("file-pdf", "pdf", "#dc2626"),
                CreateFileNode("file-excel", "excel", "#16a34a"),
                CreateFileNode("file-docx", "docx", "#2563eb"),
                CreateFileNode("file-mermaid", "mermaid", "#7c3aed"),
                CreateFileNode("file-log", "log", "#475569")
            ],
            [],
            null);

        var canvasSurface = adapter.BuildSurface(
            surface,
            new CanvasWorkbenchUiState(),
            new CanvasWorkbenchChrome(),
            actionCatalog);

        Assert.Equal("rose", canvasSurface.Nodes.Single(node => node.Id == "file-pdf").PaletteKey);
        Assert.Equal("mint", canvasSurface.Nodes.Single(node => node.Id == "file-excel").PaletteKey);
        Assert.Equal("sky", canvasSurface.Nodes.Single(node => node.Id == "file-docx").PaletteKey);
        Assert.Equal("violet", canvasSurface.Nodes.Single(node => node.Id == "file-mermaid").PaletteKey);
        Assert.Equal("amber", canvasSurface.Nodes.Single(node => node.Id == "file-log").PaletteKey);
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

    private static ProjectStructureNode CreateFileNode(string id, string subtype, string accentColor)
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
            new ProjectObjectVisualProfile("rect", accentColor, subtype.ToUpperInvariant(), subtype),
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
