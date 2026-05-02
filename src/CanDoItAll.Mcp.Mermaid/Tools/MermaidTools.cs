using System.ComponentModel;
using CanDoItAll.Mcp.Core.Contracts;
using CanDoItAll.Mcp.Core.Identity;
using CanDoItAll.Mcp.Mermaid.Catalog;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace CanDoItAll.Mcp.Mermaid.Tools;

[McpServerToolType]
public sealed class MermaidTools(MermaidSyntaxCatalogService catalogService, ILogger<MermaidTools> logger)
{
    [McpServerTool(Name = "mermaid_syntax_index", ReadOnly = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Returns the Mermaid syntax catalog index, version basis, global authoring rules, and known diagram types.")]
    public Task<McpToolEnvelope<MermaidSyntaxIndex>> MermaidSyntaxIndexAsync(CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        return ExecuteAsync("mermaid_syntax_index", catalogService.GetIndex);
    }

    [McpServerTool(Name = "mermaid_syntax_types_list", ReadOnly = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Lists Mermaid diagram types known to the catalog, optionally filtered by keyword such as flowchart, forbidden symbol, or architecture-beta.")]
    public Task<McpToolEnvelope<MermaidSyntaxListData>> MermaidSyntaxTypesListAsync(string? query = null, CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        return ExecuteAsync("mermaid_syntax_types_list", () => catalogService.ListTypes(query));
    }

    [McpServerTool(Name = "mermaid_syntax_get", ReadOnly = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Returns syntax rules, advanced notes, examples, and forbidden-symbol guidance for one Mermaid diagram type.")]
    public Task<McpToolEnvelope<MermaidDiagramSyntaxDocument>> MermaidSyntaxGetAsync(string diagramType, CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        return ExecuteAsync("mermaid_syntax_get", () => catalogService.GetSyntax(diagramType));
    }

    [McpServerTool(Name = "mermaid_forbidden_symbols_get", ReadOnly = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Returns forbidden or dangerous symbols for one Mermaid diagram type, with safer alternatives.")]
    public Task<McpToolEnvelope<MermaidForbiddenSymbolsData>> MermaidForbiddenSymbolsGetAsync(string diagramType, CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        return ExecuteAsync("mermaid_forbidden_symbols_get", () => catalogService.GetForbiddenSymbols(diagramType));
    }

    [McpServerTool(Name = "mermaid_examples_get", ReadOnly = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Returns curated Mermaid source examples for one diagram type.")]
    public Task<McpToolEnvelope<MermaidExamplesData>> MermaidExamplesGetAsync(string diagramType, CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        return ExecuteAsync("mermaid_examples_get", () => catalogService.GetExamples(diagramType));
    }

    private Task<McpToolEnvelope<T>> ExecuteAsync<T>(string toolName, Func<T> callback)
    {
        var correlationId = CorrelationIdFactory.Create("mermaid");

        try
        {
            return Task.FromResult(McpToolEnvelope<T>.Success(toolName, correlationId, callback()));
        }
        catch (ToolInvocationException ex)
        {
            logger.LogWarning(ex, "{ToolName} failed with deterministic Mermaid catalog error {Code}.", toolName, ex.Code);
            return Task.FromResult(McpToolEnvelope<T>.Failure(toolName, correlationId, new ToolError(ex.Code, ex.Message, ex.Details), status: "failed", summary: ex.Message));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{ToolName} failed unexpectedly.", toolName);
            return Task.FromResult(McpToolEnvelope<T>.Failure(toolName, correlationId, new ToolError("InternalError", ex.Message), status: "failed", summary: "The Mermaid syntax tool failed unexpectedly."));
        }
    }
}
