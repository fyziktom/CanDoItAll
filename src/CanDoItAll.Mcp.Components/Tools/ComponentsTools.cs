using System.ComponentModel;
using CanDoItAll.Mcp.Components.Catalog;
using CanDoItAll.Mcp.Core.Contracts;
using CanDoItAll.Mcp.Core.Identity;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace CanDoItAll.Mcp.Components.Tools;

[McpServerToolType]
public sealed class ComponentsTools(ComponentCatalogService catalogService, ILogger<ComponentsTools> logger)
{
    [McpServerTool(Name = "components_search", ReadOnly = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Searches shared components, sandbox examples, and sandbox groups by component name, keyword, prop, or scenario.")]
    public Task<McpToolEnvelope<ComponentsSearchData>> ComponentsSearchAsync(
        string? query = null,
        string? library = null,
        string? group = null,
        int limit = 10,
        CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        return ExecuteAsync(
            "components_search",
            () => catalogService.Search(query, library, group, limit));
    }

    [McpServerTool(Name = "component_get", ReadOnly = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Returns a shared component summary, namespace, parameters, events, dependencies, and sandbox-linked metadata.")]
    public Task<McpToolEnvelope<ComponentDocument>> ComponentGetAsync(string component, CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        return ExecuteAsync("component_get", () => catalogService.GetComponent(component));
    }

    [McpServerTool(Name = "component_examples", ReadOnly = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Returns curated sandbox examples linked to a shared component.")]
    public Task<McpToolEnvelope<ComponentExamplesData>> ComponentExamplesAsync(string component, CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        return ExecuteAsync("component_examples", () => catalogService.GetExamples(component));
    }

    [McpServerTool(Name = "component_usage_examples", ReadOnly = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Returns real shared-component usage examples from sandbox and consumer Razor files such as CanDoItAll.Web.")]
    public Task<McpToolEnvelope<ComponentUsageExamplesData>> ComponentUsageExamplesAsync(
        string component,
        int limit = 10,
        CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        return ExecuteAsync("component_usage_examples", () => catalogService.GetUsageExamples(component, limit));
    }

    [McpServerTool(Name = "component_groups_list", ReadOnly = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Lists sandbox component groups with page routes, summaries, and proof notes.")]
    public Task<McpToolEnvelope<IReadOnlyList<ComponentGroupDocument>>> ComponentGroupsListAsync(CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        return ExecuteAsync("component_groups_list", catalogService.GetGroups);
    }

    [McpServerTool(Name = "component_css_tokens_get", ReadOnly = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Returns stylesheet locations and CSS/token notes for a shared component.")]
    public Task<McpToolEnvelope<ComponentCssTokensData>> ComponentCssTokensGetAsync(string component, CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        return ExecuteAsync("component_css_tokens_get", () => catalogService.GetCssTokens(component));
    }

    [McpServerTool(Name = "canvas_contract_get", ReadOnly = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Returns typed canvas contract models, event args, requests, and state definitions from CanvasLib.")]
    public Task<McpToolEnvelope<CanvasContractsData>> CanvasContractGetAsync(string? query = null, CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        return ExecuteAsync("canvas_contract_get", () => catalogService.GetCanvasContracts(query));
    }

    private Task<McpToolEnvelope<T>> ExecuteAsync<T>(string toolName, Func<T> callback)
    {
        var correlationId = CorrelationIdFactory.Create();

        try
        {
            var data = callback();
            return Task.FromResult(McpToolEnvelope<T>.Success(toolName, correlationId, data));
        }
        catch (ToolInvocationException ex)
        {
            logger.LogWarning(ex, "{ToolName} failed with a deterministic tool error {Code}.", toolName, ex.Code);
            return Task.FromResult(McpToolEnvelope<T>.Failure(toolName, correlationId, new ToolError(ex.Code, ex.Message, ex.Details), status: "failed", summary: ex.Message));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{ToolName} failed unexpectedly.", toolName);
            return Task.FromResult(McpToolEnvelope<T>.Failure(toolName, correlationId, new ToolError("InternalError", ex.Message), status: "failed", summary: "The tool failed unexpectedly."));
        }
    }
}
