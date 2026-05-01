using CanDoItAll.Mcp.Mermaid.Catalog;
using CanDoItAll.Mcp.Mermaid.Tools;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Mcp.Mermaid.Tests;

public sealed class MermaidToolsTests
{
    [Fact]
    public async Task Tools_Return_Syntax_Forbidden_Symbols_Examples_And_Search()
    {
        var tools = new MermaidTools(new MermaidSyntaxCatalogService(), NullLogger<MermaidTools>.Instance);

        var syntax = await tools.MermaidSyntaxGetAsync("architecture-beta");
        var forbidden = await tools.MermaidForbiddenSymbolsGetAsync("architecture-beta");
        var examples = await tools.MermaidExamplesGetAsync("flowchart");
        var search = await tools.MermaidSyntaxTypesListAsync("forbidden punctuation architecture");

        Assert.True(syntax.Ok);
        Assert.NotNull(syntax.Data);
        Assert.Equal("architecture-beta", syntax.Data!.Key);

        Assert.True(forbidden.Ok);
        Assert.Contains(forbidden.Data!.Rules, rule => rule.SaferForm.Contains("API: v2", StringComparison.OrdinalIgnoreCase));

        Assert.True(examples.Ok);
        Assert.Contains(examples.Data!.Examples, example => example.Source.Contains("flowchart", StringComparison.OrdinalIgnoreCase));

        Assert.True(search.Ok);
        Assert.Contains(search.Data!.DiagramTypes, diagram => string.Equals(diagram.Key, "architecture-beta", StringComparison.OrdinalIgnoreCase));
    }
}
