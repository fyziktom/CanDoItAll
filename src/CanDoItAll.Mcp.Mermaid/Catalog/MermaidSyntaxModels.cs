namespace CanDoItAll.Mcp.Mermaid.Catalog;

public sealed record MermaidSyntaxIndex(
    string MermaidVersion,
    string SourceBasis,
    IReadOnlyList<string> GlobalRules,
    IReadOnlyList<MermaidDiagramTypeSummary> DiagramTypes);

public sealed record MermaidDiagramTypeSummary(
    string Key,
    string Title,
    string Status,
    string Summary,
    IReadOnlyList<string> Aliases,
    IReadOnlyList<string> StartsWith);

public sealed record MermaidDiagramSyntaxDocument(
    string Key,
    string Title,
    string Status,
    string Summary,
    IReadOnlyList<string> Aliases,
    IReadOnlyList<string> StartsWith,
    IReadOnlyList<string> MainRules,
    IReadOnlyList<string> AdvancedRules,
    IReadOnlyList<MermaidForbiddenSymbolRule> ForbiddenSymbols,
    IReadOnlyList<MermaidExampleDocument> Examples);

public sealed record MermaidForbiddenSymbolRule(
    string Scope,
    string Symbols,
    string Reason,
    string SaferForm);

public sealed record MermaidExampleDocument(
    string Title,
    string Description,
    string Source);

public sealed record MermaidSyntaxListData(
    string Query,
    IReadOnlyList<MermaidDiagramTypeSummary> DiagramTypes);

public sealed record MermaidForbiddenSymbolsData(
    string DiagramType,
    IReadOnlyList<MermaidForbiddenSymbolRule> Rules);

public sealed record MermaidExamplesData(
    string DiagramType,
    IReadOnlyList<MermaidExampleDocument> Examples);
