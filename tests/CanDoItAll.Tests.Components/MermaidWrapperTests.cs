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

    [Theory]
    [InlineData("```mermaid\nflowchart TB\nA[Start] --> B[Done]\n```", "flowchart TB\nA[Start] --> B[Done]")]
    [InlineData("```mermaid flowchart TB\nA[Start] --> B[Done]\n```", "flowchart TB\nA[Start] --> B[Done]")]
    [InlineData("```mermaid sequenceDiagram\nparticipant A\nA->>A: done\n```", "sequenceDiagram\nparticipant A\nA->>A: done")]
    [InlineData("```mermaid sequenceDiagram participant A A->>A: done ```", "sequenceDiagram participant A A->>A: done")]
    [InlineData("flowchart LR\nA --> B", "flowchart LR\nA --> B")]
    public void Mermaid_Source_Normalizer_Strips_Render_Block_Fences(string source, string expected)
    {
        Assert.Equal(expected, MermaidSourceNormalizer.Normalize(source));
    }

    [Fact]
    public void Mermaid_Source_Normalizer_Extracts_Mermaid_Block_From_Markdown()
    {
        const string source = """
            Diagram follows:

            ```mermaid
            sequenceDiagram
            participant User
            User->>App: open
            ```
            """;

        Assert.Equal(
            "sequenceDiagram\nparticipant User\nUser->>App: open",
            MermaidSourceNormalizer.Normalize(source));
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

}
