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
}
