using Bunit;
using CanDoItAll.Components.Mermaid;

namespace CanDoItAll.Tests.Components;

public sealed class MermaidWrapperTests
{
    [Fact]
    public void Render_Error_Formats_Line_And_Column()
    {
        var error = new MermaidRenderError
        {
            Message = "Parse error",
            Line = 3,
            Column = 12,
            Excerpt = "3: A -->\n          ^",
            ExpectedTokens = ["ID", "TEXT"]
        };

        Assert.Equal("Line 3, column 12", error.LocationText);
        Assert.Contains("ID", error.ExpectedTokens);
    }

    [Fact]
    public void Diagram_Options_Default_To_Click_Friendly_Mermaid_Config()
    {
        var options = new MermaidDiagramOptions();

        Assert.Equal("default", options.Theme);
        Assert.Equal("loose", options.SecurityLevel);
        Assert.True(options.FlowchartUseMaxWidth);
        Assert.True(options.HtmlLabels);
        Assert.False(options.ArchitectureRandomize);
    }

    [Fact]
    public void Mermaid_Diagram_Renders_Control_Chrome()
    {
        using var context = new TestContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;

        var cut = context.RenderComponent<MermaidDiagram>(parameters => parameters
            .Add(component => component.Title, "Dependency graph")
            .Add(component => component.Description, "Interactive Mermaid sample")
            .Add(component => component.Source, "flowchart LR\nA[Start] --> B[Done]"));

        Assert.Contains("Dependency graph", cut.Markup);
        Assert.Contains("Interactive Mermaid sample", cut.Markup);
        Assert.Contains("Zoom in", cut.Markup);
        Assert.Contains("mermaid-diagram-viewport", cut.Markup);
    }

    [Fact]
    public void Mermaid_Diagram_Preserves_Custom_Root_Attributes()
    {
        using var context = new TestContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;

        var cut = context.RenderComponent<MermaidDiagram>(parameters => parameters
            .Add(component => component.Source, "flowchart LR\nA[Start] --> B[Done]")
            .AddUnmatched("data-testid", "custom-mermaid"));

        var root = cut.Find("[data-testid='custom-mermaid']");

        Assert.Contains("cda-mermaid", root.ClassName);
        Assert.Contains("mermaid-diagram-viewport", cut.Markup);
    }

    [Fact]
    public void Vendor_Asset_Metadata_And_Official_Esm_File_Exist()
    {
        var workspaceRoot = FindWorkspaceRoot();
        var packageRoot = Path.Combine(workspaceRoot, "src", "CanDoItAll.Components.Mermaid");

        Assert.True(File.Exists(Path.Combine(packageRoot, "MermaidVendor.md")));
        Assert.True(File.Exists(Path.Combine(packageRoot, "wwwroot", "js", "vendor", "mermaid.esm.min.mjs")));
        Assert.Contains(
            Directory.EnumerateFiles(Path.Combine(packageRoot, "wwwroot", "js", "vendor"), "*.mjs", SearchOption.AllDirectories),
            path => path.Contains(Path.Combine("chunks", "mermaid.esm.min"), StringComparison.OrdinalIgnoreCase));
    }

    private static string FindWorkspaceRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CanDoItAll.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate CanDoItAll.slnx.");
    }
}
