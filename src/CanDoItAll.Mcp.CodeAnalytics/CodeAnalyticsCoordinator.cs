using CanDoItAll.CodeAnalytics.Abstractions;
using CanDoItAll.CodeAnalytics.Abstractions.Commands;
using CanDoItAll.CodeAnalytics.Abstractions.Queries;
using CanDoItAll.CodeAnalytics.Abstractions.Responses;
using CanDoItAll.Mcp.Core.Contracts;

namespace CanDoItAll.Mcp.CodeAnalytics;

public interface ICodeAnalyticsCoordinator
{
    Task<SnapshotBuildResponse> BuildSnapshotAsync(CodeAnalyticsBuildSnapshotInput request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RecentSnapshotItem>> ListRecentSnapshotsAsync(int take, CancellationToken cancellationToken = default);

    Task<SnapshotDashboardResponse> GetDashboardAsync(CodeAnalyticsDashboardQueryInput request, CancellationToken cancellationToken = default);

    Task<DependencyViewResponse> GetDependenciesAsync(CodeAnalyticsSnapshotQueryInput request, CancellationToken cancellationToken = default);

    Task<FindingsViewResponse> GetFindingsAsync(CodeAnalyticsSnapshotQueryInput request, CancellationToken cancellationToken = default);

    Task<SolutionInventoryResponse> GetSolutionInventoryAsync(CodeAnalyticsSolutionInventoryInput request, CancellationToken cancellationToken = default);

    Task<ProjectInventoryResponse> GetProjectInventoryAsync(CodeAnalyticsProjectInventoryInput request, CancellationToken cancellationToken = default);

    Task<DocumentSourceResponse> GetDocumentSourceAsync(CodeAnalyticsDocumentTargetInput request, CancellationToken cancellationToken = default);

    Task<DocumentSymbolsResponse> GetDocumentSymbolsAsync(CodeAnalyticsDocumentTargetInput request, CancellationToken cancellationToken = default);

    Task<ServiceViewResponse> GetServicesAsync(CodeAnalyticsSnapshotQueryInput request, CancellationToken cancellationToken = default);

    Task<PersistenceViewResponse> GetPersistenceAsync(CodeAnalyticsSnapshotQueryInput request, CancellationToken cancellationToken = default);

    Task<ExportsViewResponse> GetExportsAsync(string snapshotId, CancellationToken cancellationToken = default);

    Task<TypesViewResponse> GetTypesAsync(CodeAnalyticsTypeSearchInput request, CancellationToken cancellationToken = default);

    Task<SymbolSearchResponse> SearchSymbolsAsync(CodeAnalyticsSymbolSearchInput request, CancellationToken cancellationToken = default);

    Task<SymbolDefinitionResponse> GetSymbolDefinitionAsync(CodeAnalyticsSymbolTargetInput request, CancellationToken cancellationToken = default);

    Task<SymbolMembersResponse> GetSymbolMembersAsync(CodeAnalyticsSymbolTargetInput request, CancellationToken cancellationToken = default);

    Task<SymbolImplementationsResponse> GetSymbolImplementationsAsync(CodeAnalyticsSymbolTargetInput request, CancellationToken cancellationToken = default);

    Task<SymbolReferencesResponse> GetSymbolReferencesAsync(CodeAnalyticsSymbolReferencesInput request, CancellationToken cancellationToken = default);

    Task<FocusedContextResponse> GetFocusedContextAsync(CodeAnalyticsFocusedContextInput request, CancellationToken cancellationToken = default);
}

public sealed class CodeAnalyticsCoordinator(
    ICodeAnalyticsApplicationService applicationService,
    RuntimeConfiguration runtimeConfiguration)
    : ICodeAnalyticsCoordinator
{
    private const int MaxSymbolResults = 100;

    public Task<SnapshotBuildResponse> BuildSnapshotAsync(CodeAnalyticsBuildSnapshotInput request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var solutionPath = runtimeConfiguration.ResolveAnalysisPath(request.SolutionPath);
        if (!File.Exists(solutionPath))
        {
            throw new ToolInvocationException("SolutionPathNotFound", $"Solution or project path '{solutionPath}' was not found.");
        }

        var command = new BuildArchitectureSnapshotCommand(
            solutionPath,
            NormalizeList(request.ScopeProjectNames),
            NormalizeList(request.ScopeNamespacePrefixes),
            request.IncludeDi,
            request.IncludePersistence,
            request.IncludeRisks,
            request.IncludeXmlDocs,
            request.IncludeMermaidExports,
            request.ForceRefresh);

        return applicationService.BuildSnapshotAsync(command, progressReporter: null, cancellationToken);
    }

    public Task<IReadOnlyList<RecentSnapshotItem>> ListRecentSnapshotsAsync(int take, CancellationToken cancellationToken = default)
    {
        return applicationService.ListRecentSnapshotsAsync(NormalizeTake(take, 10, runtimeConfiguration.MaxRecentSnapshots), cancellationToken);
    }

    public async Task<SnapshotDashboardResponse> GetDashboardAsync(CodeAnalyticsDashboardQueryInput request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var snapshotId = RequireValue(request.SnapshotId, nameof(request.SnapshotId));
        var response = await applicationService.GetDashboardAsync(snapshotId, NormalizeTake(request.RecentTake, 10, runtimeConfiguration.MaxRecentSnapshots), cancellationToken);
        return response ?? throw SnapshotNotFound(snapshotId);
    }

    public async Task<DependencyViewResponse> GetDependenciesAsync(CodeAnalyticsSnapshotQueryInput request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var query = CreateSnapshotQuery(request);
        var response = await applicationService.GetDependenciesAsync(query, cancellationToken);
        return response ?? throw SnapshotNotFound(query.SnapshotId);
    }

    public async Task<FindingsViewResponse> GetFindingsAsync(CodeAnalyticsSnapshotQueryInput request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var query = CreateSnapshotQuery(request);
        var response = await applicationService.GetFindingsAsync(query, cancellationToken);
        return response ?? throw SnapshotNotFound(query.SnapshotId);
    }

    public async Task<SolutionInventoryResponse> GetSolutionInventoryAsync(CodeAnalyticsSolutionInventoryInput request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var query = new SolutionInventoryQuery(
            RequireValue(request.SnapshotId, nameof(request.SnapshotId)),
            request.IncludeDocuments);

        var response = await applicationService.GetSolutionInventoryAsync(query, cancellationToken);
        return response ?? throw SnapshotNotFound(query.SnapshotId);
    }

    public async Task<ProjectInventoryResponse> GetProjectInventoryAsync(CodeAnalyticsProjectInventoryInput request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.ProjectId)
            && string.IsNullOrWhiteSpace(request.ProjectName))
        {
            throw new ToolInvocationException("ValidationError", "Project inventory requires ProjectId or ProjectName.");
        }

        var query = new ProjectInventoryQuery(
            RequireValue(request.SnapshotId, nameof(request.SnapshotId)),
            NormalizeOptionalValue(request.ProjectId),
            NormalizeOptionalValue(request.ProjectName),
            request.IncludeDocuments);

        var response = await applicationService.GetProjectInventoryAsync(query, cancellationToken);
        return response ?? throw new ToolInvocationException("ProjectNotFound", $"The requested project was not found in snapshot '{query.SnapshotId}'.");
    }

    public async Task<DocumentSourceResponse> GetDocumentSourceAsync(CodeAnalyticsDocumentTargetInput request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var query = CreateDocumentQuery(request);
        var response = await applicationService.GetDocumentSourceAsync(query, cancellationToken);
        return response ?? throw new ToolInvocationException("DocumentNotFound", $"The requested document was not found in snapshot '{query.SnapshotId}'.");
    }

    public async Task<DocumentSymbolsResponse> GetDocumentSymbolsAsync(CodeAnalyticsDocumentTargetInput request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var query = CreateDocumentQuery(request);
        var response = await applicationService.GetDocumentSymbolsAsync(query, cancellationToken);
        return response ?? throw new ToolInvocationException("DocumentNotFound", $"The requested document was not found in snapshot '{query.SnapshotId}'.");
    }

    public async Task<ServiceViewResponse> GetServicesAsync(CodeAnalyticsSnapshotQueryInput request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var query = CreateSnapshotQuery(request);
        var response = await applicationService.GetServicesAsync(query, cancellationToken);
        return response ?? throw SnapshotNotFound(query.SnapshotId);
    }

    public async Task<PersistenceViewResponse> GetPersistenceAsync(CodeAnalyticsSnapshotQueryInput request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var query = CreateSnapshotQuery(request);
        var response = await applicationService.GetPersistenceAsync(query, cancellationToken);
        return response ?? throw SnapshotNotFound(query.SnapshotId);
    }

    public async Task<ExportsViewResponse> GetExportsAsync(string snapshotId, CancellationToken cancellationToken = default)
    {
        var normalizedSnapshotId = RequireValue(snapshotId, nameof(snapshotId));
        var response = await applicationService.GetExportsAsync(normalizedSnapshotId, cancellationToken);
        return response ?? throw SnapshotNotFound(normalizedSnapshotId);
    }

    public async Task<TypesViewResponse> GetTypesAsync(CodeAnalyticsTypeSearchInput request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.SearchText)
            && string.IsNullOrWhiteSpace(request.MemberSearchText)
            && string.IsNullOrWhiteSpace(request.ProjectName))
        {
            throw new ToolInvocationException("ValidationError", "Types search requires SearchText, MemberSearchText, or ProjectName.");
        }

        var query = new TypeSearchQuery(
            RequireValue(request.SnapshotId, nameof(request.SnapshotId)),
            NormalizeOptionalValue(request.SearchText),
            NormalizeOptionalValue(request.ProjectName),
            NormalizeOptionalValue(request.MemberSearchText),
            request.IncludeMembers,
            request.MethodsOnly);

        var response = await applicationService.GetTypesAsync(query, cancellationToken);
        return response ?? throw SnapshotNotFound(query.SnapshotId);
    }

    public async Task<SymbolSearchResponse> SearchSymbolsAsync(CodeAnalyticsSymbolSearchInput request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var searchText = RequireValue(request.SearchText, nameof(request.SearchText));
        if (!request.IncludeTypes && !request.IncludeMembers)
        {
            throw new ToolInvocationException("ValidationError", "Symbol search must include types, members, or both.");
        }

        var query = new SymbolSearchQuery(
            RequireValue(request.SnapshotId, nameof(request.SnapshotId)),
            searchText,
            NormalizeOptionalValue(request.ProjectName),
            request.SearchMode,
            request.IncludeTypes,
            request.IncludeMembers,
            NormalizeTake(request.Take, 40, MaxSymbolResults));

        var response = await applicationService.SearchSymbolsAsync(query, cancellationToken);
        return response ?? throw SnapshotNotFound(query.SnapshotId);
    }

    public async Task<SymbolDefinitionResponse> GetSymbolDefinitionAsync(CodeAnalyticsSymbolTargetInput request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var query = new SymbolDefinitionQuery(
            RequireValue(request.SnapshotId, nameof(request.SnapshotId)),
            RequireValue(request.TypeId, nameof(request.TypeId)),
            NormalizeOptionalValue(request.MemberId));

        var response = await applicationService.GetSymbolDefinitionAsync(query, cancellationToken);
        return response ?? throw new ToolInvocationException("SymbolNotFound", $"The requested symbol was not found in snapshot '{query.SnapshotId}'.");
    }

    public async Task<SymbolMembersResponse> GetSymbolMembersAsync(CodeAnalyticsSymbolTargetInput request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var query = new SymbolMembersQuery(
            RequireValue(request.SnapshotId, nameof(request.SnapshotId)),
            RequireValue(request.TypeId, nameof(request.TypeId)));

        var response = await applicationService.GetSymbolMembersAsync(query, cancellationToken);
        return response ?? throw new ToolInvocationException("TypeNotFound", $"Type '{query.TypeId}' was not found in snapshot '{query.SnapshotId}'.");
    }

    public async Task<SymbolImplementationsResponse> GetSymbolImplementationsAsync(CodeAnalyticsSymbolTargetInput request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var query = new SymbolImplementationsQuery(
            RequireValue(request.SnapshotId, nameof(request.SnapshotId)),
            RequireValue(request.TypeId, nameof(request.TypeId)));

        var response = await applicationService.GetSymbolImplementationsAsync(query, cancellationToken);
        return response ?? throw new ToolInvocationException("TypeNotFound", $"Type '{query.TypeId}' was not found in snapshot '{query.SnapshotId}'.");
    }

    public async Task<SymbolReferencesResponse> GetSymbolReferencesAsync(CodeAnalyticsSymbolReferencesInput request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var query = new SymbolReferencesQuery(
            RequireValue(request.SnapshotId, nameof(request.SnapshotId)),
            RequireValue(request.TypeId, nameof(request.TypeId)),
            NormalizeOptionalValue(request.MemberId),
            NormalizeTake(request.Take, 40, MaxSymbolResults));

        var response = await applicationService.GetSymbolReferencesAsync(query, cancellationToken);
        return response ?? throw new ToolInvocationException("SymbolNotFound", $"The requested symbol was not found in snapshot '{query.SnapshotId}'.");
    }

    public async Task<FocusedContextResponse> GetFocusedContextAsync(CodeAnalyticsFocusedContextInput request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.TypeId)
            && string.IsNullOrWhiteSpace(request.MemberId)
            && string.IsNullOrWhiteSpace(request.ServiceRegistrationId)
            && string.IsNullOrWhiteSpace(request.QueryText))
        {
            throw new ToolInvocationException("ValidationError", "Focused context requires a type id, member id, service registration id, or query text.");
        }

        var query = new FocusedContextQuery(
            RequireValue(request.SnapshotId, nameof(request.SnapshotId)),
            NormalizeOptionalValue(request.TypeId),
            NormalizeOptionalValue(request.MemberId),
            NormalizeOptionalValue(request.ServiceRegistrationId),
            Math.Clamp(request.Depth, 0, 5),
            NormalizeOptionalValue(request.QueryText),
            NormalizeList(request.FocusTags),
            request.Intent,
            request.Precision,
            NormalizeList(request.RelationHints));

        var response = await applicationService.GetFocusedContextAsync(query, cancellationToken);
        return response ?? throw new ToolInvocationException("FocusedContextNotFound", $"Focused context could not be resolved from snapshot '{query.SnapshotId}'.");
    }

    private static SnapshotQuery CreateSnapshotQuery(CodeAnalyticsSnapshotQueryInput request)
    {
        return new SnapshotQuery(
            RequireValue(request.SnapshotId, nameof(request.SnapshotId)),
            NormalizeOptionalValue(request.SearchText));
    }

    private static DocumentQuery CreateDocumentQuery(CodeAnalyticsDocumentTargetInput request)
    {
        if (string.IsNullOrWhiteSpace(request.DocumentId)
            && string.IsNullOrWhiteSpace(request.DocumentPath))
        {
            throw new ToolInvocationException("ValidationError", "Document queries require DocumentId or DocumentPath.");
        }

        return new DocumentQuery(
            RequireValue(request.SnapshotId, nameof(request.SnapshotId)),
            NormalizeOptionalValue(request.DocumentId),
            NormalizeOptionalValue(request.DocumentPath));
    }

    private static IReadOnlyList<string>? NormalizeList(IReadOnlyList<string>? values)
    {
        if (values is null || values.Count == 0)
        {
            return null;
        }

        var normalized = values
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return normalized.Length == 0 ? null : normalized;
    }

    private static string? NormalizeOptionalValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string RequireValue(string? value, string parameterName)
    {
        return string.IsNullOrWhiteSpace(value)
            ? throw new ToolInvocationException("ValidationError", $"Parameter '{parameterName}' is required.")
            : value.Trim();
    }

    private static int NormalizeTake(int take, int defaultValue, int maximum)
    {
        return Math.Clamp(take <= 0 ? defaultValue : take, 1, maximum);
    }

    private static ToolInvocationException SnapshotNotFound(string snapshotId)
    {
        return new ToolInvocationException("SnapshotNotFound", $"Snapshot '{snapshotId}' was not found.");
    }
}
