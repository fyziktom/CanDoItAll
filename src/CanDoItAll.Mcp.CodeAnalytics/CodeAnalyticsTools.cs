using System.ComponentModel;
using CanDoItAll.CodeAnalytics.Abstractions.Responses;
using CanDoItAll.Mcp.Core.Contracts;
using CanDoItAll.Mcp.Core.Identity;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace CanDoItAll.Mcp.CodeAnalytics;

[McpServerToolType]
public sealed class CodeAnalyticsTools(ICodeAnalyticsCoordinator coordinator, ILogger<CodeAnalyticsTools> logger)
{
    [McpServerTool(Name = "code_analytics_snapshot_build", ReadOnly = false, Idempotent = false, OpenWorld = false, UseStructuredContent = true)]
    [Description("Builds or refreshes an architectural snapshot for a C# solution or project using the sibling CanDoItAll.CodeAnalsis libraries. For large solutions, scope by project, namespace, or project path unless the task is architecture-wide.")]
    public Task<McpToolEnvelope<SnapshotBuildResponse>> CodeAnalyticsSnapshotBuildAsync(CodeAnalyticsBuildSnapshotInput? request = null, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync("code_analytics_snapshot_build", () => coordinator.BuildSnapshotAsync(request ?? new CodeAnalyticsBuildSnapshotInput(), cancellationToken));
    }

    [McpServerTool(Name = "code_analytics_recent_snapshots_list", ReadOnly = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Lists recently generated code-analysis snapshots from the configured output cache.")]
    public Task<McpToolEnvelope<IReadOnlyList<RecentSnapshotItem>>> CodeAnalyticsRecentSnapshotsListAsync(
        [Description("Maximum number of recent snapshots to return.")] int take = 10,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync("code_analytics_recent_snapshots_list", () => coordinator.ListRecentSnapshotsAsync(take, cancellationToken));
    }

    [McpServerTool(Name = "code_analytics_dashboard_get", ReadOnly = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Returns the dashboard summary for a snapshot, including top findings, diagnostics, and recent snapshot history.")]
    public Task<McpToolEnvelope<SnapshotDashboardResponse>> CodeAnalyticsDashboardGetAsync(CodeAnalyticsDashboardQueryInput request, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync("code_analytics_dashboard_get", () => coordinator.GetDashboardAsync(request, cancellationToken));
    }

    [McpServerTool(Name = "code_analytics_dependencies_get", ReadOnly = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Returns project, module, namespace, and type dependency facts plus detected cycles for a snapshot.")]
    public Task<McpToolEnvelope<DependencyViewResponse>> CodeAnalyticsDependenciesGetAsync(CodeAnalyticsSnapshotQueryInput request, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync("code_analytics_dependencies_get", () => coordinator.GetDependenciesAsync(request, cancellationToken));
    }

    [McpServerTool(Name = "code_analytics_findings_get", ReadOnly = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Returns findings, open questions, and architectural hotspots for a snapshot.")]
    public Task<McpToolEnvelope<FindingsViewResponse>> CodeAnalyticsFindingsGetAsync(CodeAnalyticsSnapshotQueryInput request, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync("code_analytics_findings_get", () => coordinator.GetFindingsAsync(request, cancellationToken));
    }

    [McpServerTool(Name = "code_analytics_solution_inventory_get", ReadOnly = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Returns the solution and project inventory for a snapshot, including direct project references and reverse references.")]
    public Task<McpToolEnvelope<SolutionInventoryResponse>> CodeAnalyticsSolutionInventoryGetAsync(CodeAnalyticsSolutionInventoryInput request, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync("code_analytics_solution_inventory_get", () => coordinator.GetSolutionInventoryAsync(request, cancellationToken));
    }

    [McpServerTool(Name = "code_analytics_project_inventory_get", ReadOnly = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Returns one project inventory entry, including its direct project references, reverse references, and optional document list.")]
    public Task<McpToolEnvelope<ProjectInventoryResponse>> CodeAnalyticsProjectInventoryGetAsync(CodeAnalyticsProjectInventoryInput request, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync("code_analytics_project_inventory_get", () => coordinator.GetProjectInventoryAsync(request, cancellationToken));
    }

    [McpServerTool(Name = "code_analytics_document_source_get", ReadOnly = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Returns the raw source text for a snapshot document identified by document id or path.")]
    public Task<McpToolEnvelope<DocumentSourceResponse>> CodeAnalyticsDocumentSourceGetAsync(CodeAnalyticsDocumentTargetInput request, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync("code_analytics_document_source_get", () => coordinator.GetDocumentSourceAsync(request, cancellationToken));
    }

    [McpServerTool(Name = "code_analytics_document_symbols_get", ReadOnly = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Returns the declared types and members for a snapshot document identified by document id or path.")]
    public Task<McpToolEnvelope<DocumentSymbolsResponse>> CodeAnalyticsDocumentSymbolsGetAsync(CodeAnalyticsDocumentTargetInput request, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync("code_analytics_document_symbols_get", () => coordinator.GetDocumentSymbolsAsync(request, cancellationToken));
    }

    [McpServerTool(Name = "code_analytics_services_get", ReadOnly = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Returns dependency-injection registrations discovered in a snapshot.")]
    public Task<McpToolEnvelope<ServiceViewResponse>> CodeAnalyticsServicesGetAsync(CodeAnalyticsSnapshotQueryInput request, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync("code_analytics_services_get", () => coordinator.GetServicesAsync(request, cancellationToken));
    }

    [McpServerTool(Name = "code_analytics_persistence_get", ReadOnly = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Returns EF Core persistence facts such as DbContexts, entities, and diagnostics for a snapshot.")]
    public Task<McpToolEnvelope<PersistenceViewResponse>> CodeAnalyticsPersistenceGetAsync(CodeAnalyticsSnapshotQueryInput request, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync("code_analytics_persistence_get", () => coordinator.GetPersistenceAsync(request, cancellationToken));
    }

    [McpServerTool(Name = "code_analytics_exports_get", ReadOnly = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Returns the rendered Markdown and Mermaid export artifacts available for a snapshot.")]
    public Task<McpToolEnvelope<ExportsViewResponse>> CodeAnalyticsExportsGetAsync(
        [Description("Snapshot identifier returned by a previous snapshot build.")] string snapshotId,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync("code_analytics_exports_get", () => coordinator.GetExportsAsync(snapshotId, cancellationToken));
    }

    [McpServerTool(Name = "code_analytics_types_search", ReadOnly = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Searches snapshot types and can optionally return matching members inside the selected types.")]
    public Task<McpToolEnvelope<TypesViewResponse>> CodeAnalyticsTypesSearchAsync(CodeAnalyticsTypeSearchInput request, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync("code_analytics_types_search", () => coordinator.GetTypesAsync(request, cancellationToken));
    }

    [McpServerTool(Name = "code_analytics_symbols_search", ReadOnly = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Searches type and member symbols inside a snapshot by contains, exact, or regex matching.")]
    public Task<McpToolEnvelope<SymbolSearchResponse>> CodeAnalyticsSymbolsSearchAsync(CodeAnalyticsSymbolSearchInput request, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync("code_analytics_symbols_search", () => coordinator.SearchSymbolsAsync(request, cancellationToken));
    }

    [McpServerTool(Name = "code_analytics_symbol_definition_get", ReadOnly = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Returns the declaration, XML summary, and source excerpt for a type or member in a snapshot.")]
    public Task<McpToolEnvelope<SymbolDefinitionResponse>> CodeAnalyticsSymbolDefinitionGetAsync(CodeAnalyticsSymbolTargetInput request, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync("code_analytics_symbol_definition_get", () => coordinator.GetSymbolDefinitionAsync(request, cancellationToken));
    }

    [McpServerTool(Name = "code_analytics_symbol_members_get", ReadOnly = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Returns the members declared by a type in a snapshot.")]
    public Task<McpToolEnvelope<SymbolMembersResponse>> CodeAnalyticsSymbolMembersGetAsync(CodeAnalyticsSymbolTargetInput request, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync("code_analytics_symbol_members_get", () => coordinator.GetSymbolMembersAsync(request, cancellationToken));
    }

    [McpServerTool(Name = "code_analytics_symbol_implementations_get", ReadOnly = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Returns implementations or derived types for an interface or base type in a snapshot.")]
    public Task<McpToolEnvelope<SymbolImplementationsResponse>> CodeAnalyticsSymbolImplementationsGetAsync(CodeAnalyticsSymbolTargetInput request, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync("code_analytics_symbol_implementations_get", () => coordinator.GetSymbolImplementationsAsync(request, cancellationToken));
    }

    [McpServerTool(Name = "code_analytics_symbol_references_get", ReadOnly = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Returns scored reference sites for a type or member in a snapshot.")]
    public Task<McpToolEnvelope<SymbolReferencesResponse>> CodeAnalyticsSymbolReferencesGetAsync(CodeAnalyticsSymbolReferencesInput request, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync("code_analytics_symbol_references_get", () => coordinator.GetSymbolReferencesAsync(request, cancellationToken));
    }

    [McpServerTool(Name = "code_analytics_focused_context_get", ReadOnly = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Builds a focused context pack around a type, member, service registration, or query text. Use FocusTags, RelationHints, Depth, Intent, and Precision to get high-signal usage context without loading broad callers.")]
    public Task<McpToolEnvelope<FocusedContextResponse>> CodeAnalyticsFocusedContextGetAsync(CodeAnalyticsFocusedContextInput request, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync("code_analytics_focused_context_get", () => coordinator.GetFocusedContextAsync(request, cancellationToken));
    }

    private async Task<McpToolEnvelope<T>> ExecuteAsync<T>(string toolName, Func<Task<T>> callback)
    {
        var correlationId = CorrelationIdFactory.Create("code-analytics");

        try
        {
            var data = await callback();
            return McpToolEnvelope<T>.Success(toolName, correlationId, data);
        }
        catch (ToolInvocationException ex)
        {
            logger.LogWarning(ex, "{ToolName} failed with a deterministic tool error {Code}.", toolName, ex.Code);
            return McpToolEnvelope<T>.Failure(
                toolName,
                correlationId,
                new ToolError(ex.Code, ex.Message, ex.Details),
                status: MapFailureStatus(ex.Code),
                summary: ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{ToolName} failed unexpectedly.", toolName);
            return McpToolEnvelope<T>.Failure(
                toolName,
                correlationId,
                new ToolError("InternalError", ex.Message),
                status: "failed",
                summary: "The tool failed unexpectedly.");
        }
    }

    private static string MapFailureStatus(string code)
    {
        return code switch
        {
            "DocumentNotFound" or "FocusedContextNotFound" or "ProjectNotFound" or "SnapshotNotFound" or "SolutionPathNotFound" or "SymbolNotFound" or "TypeNotFound" => "not_found",
            "ValidationError" => "validation_error",
            _ => "failed"
        };
    }
}
