using CanDoItAll.Mcp.Core.Contracts;
using CanDoItAll.Mcp.Mermaid.Catalog;

namespace CanDoItAll.Mcp.Mermaid.Tests;

public sealed class MermaidSyntaxCatalogServiceTests
{
    [Fact]
    public void Index_Includes_Architecture_Beta_And_Global_Rules()
    {
        var service = new MermaidSyntaxCatalogService();

        var index = service.GetIndex();

        Assert.Equal("11.14.0", index.MermaidVersion);
        Assert.Contains(index.GlobalRules, rule => rule.Contains("punctuation", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(index.DiagramTypes, diagram => string.Equals(diagram.Key, "architecture-beta", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Architecture_Beta_Captures_Parser_Rules_And_Forbidden_Symbols()
    {
        var service = new MermaidSyntaxCatalogService();

        var syntax = service.GetSyntax("architecture");

        Assert.Equal("architecture-beta", syntax.Key);
        Assert.Contains(syntax.AdvancedRules, rule => rule.Contains("randomize", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(syntax.AdvancedRules, rule => rule.Contains(@"[\w]([-\w]*\w)?", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(syntax.ForbiddenSymbols, rule => rule.Scope.Contains("id", StringComparison.OrdinalIgnoreCase) && rule.Symbols.Contains(":"));
        Assert.Contains(syntax.ForbiddenSymbols, rule => rule.Scope.Contains("port", StringComparison.OrdinalIgnoreCase) && rule.Symbols.Contains("L"));
        Assert.Contains(syntax.Examples, example => example.Source.Contains("architecture-beta", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Flowchart_Guidance_Calls_Out_End_And_Leading_Arrow_Head_Pitfalls()
    {
        var service = new MermaidSyntaxCatalogService();

        var forbidden = service.GetForbiddenSymbols("flowchart");

        Assert.Contains(forbidden.Rules, rule => rule.Symbols.Contains("end", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(forbidden.Rules, rule => rule.Symbols.Contains("leading `o`", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Unknown_Diagram_Type_Fails_Deterministically()
    {
        var service = new MermaidSyntaxCatalogService();

        var ex = Assert.Throws<ToolInvocationException>(() => service.GetSyntax("unknown-graph"));

        Assert.Equal("DiagramTypeNotFound", ex.Code);
    }
}
