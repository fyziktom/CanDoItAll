using CanDoItAll.Mcp.Core.Contracts;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Mcp.ProjectStructure;

public interface IProjectStructureCoordinator
{
    Task<IReadOnlyList<ProjectSummary>> ListProjectsAsync(CancellationToken cancellationToken = default);

    Task<ProjectSummary> CreateProjectAsync(ProjectStructureProjectSaveRequest request, int? estimatedMinutes, CancellationToken cancellationToken = default);

    Task<ProjectSummary> UpdateProjectAsync(Guid projectId, ProjectStructureProjectSaveRequest request, int? estimatedMinutes, CancellationToken cancellationToken = default);

    Task<ProjectHierarchySnapshot> GetHierarchyAsync(Guid projectId, CancellationToken cancellationToken = default);

    Task<OperationAck> ChangeSubprojectAsync(Guid parentProjectId, ProjectStructureSubprojectChangeRequest request, int? estimatedMinutes, CancellationToken cancellationToken = default);

    Task<ProjectStructureReadToolData> ReadAsync(Guid projectId, ProjectStructureReadRequest request, CancellationToken cancellationToken = default);

    Task<ProjectStructureChecklistResponse> GetChecklistAsync(Guid projectId, ProjectStructureChecklistRequest request, CancellationToken cancellationToken = default);

    Task<ProjectStructureDependencyResponse> GetDependenciesAsync(Guid projectId, ProjectStructureDependencyQueryRequest request, CancellationToken cancellationToken = default);

    Task<ProjectStructureNodeSummary> CreateNodeAsync(Guid projectId, ProjectStructureNodeCreateInput request, int? estimatedMinutes, CancellationToken cancellationToken = default);

    Task<ProjectStructureNodeSummary> UpdateNodeAsync(Guid projectId, string nodeId, ProjectStructureNodeEditInput request, int? estimatedMinutes, CancellationToken cancellationToken = default);

    Task<ProjectStructureSubtreeRecompositionResult> RecomposeNodeAsync(Guid projectId, ProjectStructureNodeRecomposeInput request, int? estimatedMinutes, CancellationToken cancellationToken = default);

    Task<ProjectStructureNodeSummary> ReparentNodeAsync(Guid projectId, ProjectStructureNodeReparentInput request, int? estimatedMinutes, CancellationToken cancellationToken = default);

    Task<ProjectStructureNodeSummary> CreateApprovalRequestAsync(Guid projectId, ProjectStructureApprovalRequestCreateInput request, CancellationToken cancellationToken = default);

    Task<ProjectStructureAssetDescriptor> GetAssetAsync(Guid projectId, string nodeId, CancellationToken cancellationToken = default);

    Task<ProjectStructureAssetDescriptor> CreateAssetRevisionAsync(Guid projectId, string nodeId, ProjectStructureAssetRevisionRequest request, int? estimatedMinutes, CancellationToken cancellationToken = default);

    Task<ProjectStructureImportResult> ImportAsync(ProjectStructureImportRequest request, int? estimatedMinutes, CancellationToken cancellationToken = default);

    Task<ProjectManagementGuidanceResponse> QueryKnowledgeAsync(ProjectManagementGuidanceQueryRequest request, CancellationToken cancellationToken = default);

    Task<ProjectStructureAnalyticsResponse> QueryAnalyticsAsync(ProjectStructureAnalyticsQueryRequest request, CancellationToken cancellationToken = default);

    Task<ProjectStructureLeaseSnapshot> AcquireLeaseAsync(ProjectStructureScopeInput scope, string reason, int durationMinutes, CancellationToken cancellationToken = default);

    Task<ProjectStructureLeaseSnapshot?> GetCurrentLeaseAsync(ProjectStructureScopeInput scope, CancellationToken cancellationToken = default);

    Task<ProjectStructureLeaseSnapshot?> ReleaseLeaseAsync(ProjectStructureScopeInput scope, string leaseToken, CancellationToken cancellationToken = default);
}

public sealed class ProjectStructureCoordinator(
    ProjectStructureHttpClient httpClient,
    RuntimeConfiguration runtimeConfiguration)
    : IProjectStructureCoordinator
{
    public Task<IReadOnlyList<ProjectSummary>> ListProjectsAsync(CancellationToken cancellationToken = default)
    {
        return httpClient.GetAsync<IReadOnlyList<ProjectSummary>>("/api/project-structure-mcp/projects", cancellationToken: cancellationToken);
    }

    public Task<ProjectSummary> CreateProjectAsync(ProjectStructureProjectSaveRequest request, int? estimatedMinutes, CancellationToken cancellationToken = default)
    {
        return httpClient.PostAsync<ProjectStructureProjectSaveRequest, ProjectSummary>(
            "/api/project-structure-mcp/projects",
            request,
            estimatedMinutes,
            cancellationToken);
    }

    public Task<ProjectSummary> UpdateProjectAsync(Guid projectId, ProjectStructureProjectSaveRequest request, int? estimatedMinutes, CancellationToken cancellationToken = default)
    {
        return httpClient.PutAsync<ProjectStructureProjectSaveRequest, ProjectSummary>(
            $"/api/project-structure-mcp/projects/{projectId}",
            request,
            estimatedMinutes,
            cancellationToken);
    }

    public Task<ProjectHierarchySnapshot> GetHierarchyAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        return httpClient.GetAsync<ProjectHierarchySnapshot>($"/api/project-structure-mcp/projects/{projectId}/hierarchy", cancellationToken: cancellationToken);
    }

    public Task<OperationAck> ChangeSubprojectAsync(Guid parentProjectId, ProjectStructureSubprojectChangeRequest request, int? estimatedMinutes, CancellationToken cancellationToken = default)
    {
        return httpClient.PostAsync<ProjectStructureSubprojectChangeRequest, OperationAck>(
            $"/api/project-structure-mcp/projects/{parentProjectId}/subprojects",
            request,
            estimatedMinutes,
            cancellationToken);
    }

    public async Task<ProjectStructureReadToolData> ReadAsync(Guid projectId, ProjectStructureReadRequest request, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsync<ProjectStructureReadRequest, ProjectStructureReadResponse>(
            $"/api/project-structure-mcp/projects/{projectId}/structure/read",
            request,
            cancellationToken: cancellationToken);

        return new ProjectStructureReadToolData(
            response.ProjectId,
            response.ProjectName,
            response.Nodes.Select(MapCompactNode).ToList(),
            response.Links,
            response.Warnings);
    }

    public Task<ProjectStructureChecklistResponse> GetChecklistAsync(Guid projectId, ProjectStructureChecklistRequest request, CancellationToken cancellationToken = default)
    {
        return httpClient.PostAsync<ProjectStructureChecklistRequest, ProjectStructureChecklistResponse>(
            $"/api/project-structure-mcp/projects/{projectId}/checklists/query",
            request,
            cancellationToken: cancellationToken);
    }

    public Task<ProjectStructureDependencyResponse> GetDependenciesAsync(Guid projectId, ProjectStructureDependencyQueryRequest request, CancellationToken cancellationToken = default)
    {
        return httpClient.PostAsync<ProjectStructureDependencyQueryRequest, ProjectStructureDependencyResponse>(
            $"/api/project-structure-mcp/projects/{projectId}/dependencies/query",
            request,
            cancellationToken: cancellationToken);
    }

    public Task<ProjectStructureNodeSummary> CreateNodeAsync(Guid projectId, ProjectStructureNodeCreateInput request, int? estimatedMinutes, CancellationToken cancellationToken = default)
    {
        return httpClient.PostAsync<ProjectStructureNodeCreateInput, ProjectStructureNodeSummary>(
            $"/api/project-structure-mcp/projects/{projectId}/nodes",
            request,
            estimatedMinutes,
            cancellationToken);
    }

    public Task<ProjectStructureNodeSummary> UpdateNodeAsync(Guid projectId, string nodeId, ProjectStructureNodeEditInput request, int? estimatedMinutes, CancellationToken cancellationToken = default)
    {
        return httpClient.PutAsync<ProjectStructureNodeEditInput, ProjectStructureNodeSummary>(
            $"/api/project-structure-mcp/projects/{projectId}/nodes/{Uri.EscapeDataString(nodeId)}",
            request,
            estimatedMinutes,
            cancellationToken);
    }

    public Task<ProjectStructureSubtreeRecompositionResult> RecomposeNodeAsync(Guid projectId, ProjectStructureNodeRecomposeInput request, int? estimatedMinutes, CancellationToken cancellationToken = default)
    {
        return httpClient.PostAsync<ProjectStructureNodeRecomposeInput, ProjectStructureSubtreeRecompositionResult>(
            $"/api/project-structure-mcp/projects/{projectId}/nodes/recompose",
            request,
            estimatedMinutes,
            cancellationToken);
    }

    public Task<ProjectStructureNodeSummary> ReparentNodeAsync(Guid projectId, ProjectStructureNodeReparentInput request, int? estimatedMinutes, CancellationToken cancellationToken = default)
    {
        return httpClient.PostAsync<ProjectStructureNodeReparentInput, ProjectStructureNodeSummary>(
            $"/api/project-structure-mcp/projects/{projectId}/nodes/reparent",
            request,
            estimatedMinutes,
            cancellationToken);
    }

    public Task<ProjectStructureNodeSummary> CreateApprovalRequestAsync(Guid projectId, ProjectStructureApprovalRequestCreateInput request, CancellationToken cancellationToken = default)
    {
        return httpClient.PostAsync<ProjectStructureApprovalRequestCreateInput, ProjectStructureNodeSummary>(
            $"/api/project-structure-mcp/projects/{projectId}/approvals/request",
            request,
            cancellationToken: cancellationToken);
    }

    public Task<ProjectStructureAssetDescriptor> GetAssetAsync(Guid projectId, string nodeId, CancellationToken cancellationToken = default)
    {
        return httpClient.GetAsync<ProjectStructureAssetDescriptor>(
            $"/api/project-structure-mcp/projects/{projectId}/assets/{Uri.EscapeDataString(nodeId)}",
            cancellationToken: cancellationToken);
    }

    public Task<ProjectStructureAssetDescriptor> CreateAssetRevisionAsync(Guid projectId, string nodeId, ProjectStructureAssetRevisionRequest request, int? estimatedMinutes, CancellationToken cancellationToken = default)
    {
        return httpClient.PostAsync<ProjectStructureAssetRevisionRequest, ProjectStructureAssetDescriptor>(
            $"/api/project-structure-mcp/projects/{projectId}/assets/{Uri.EscapeDataString(nodeId)}/revisions",
            request,
            estimatedMinutes,
            cancellationToken);
    }

    public Task<ProjectStructureImportResult> ImportAsync(ProjectStructureImportRequest request, int? estimatedMinutes, CancellationToken cancellationToken = default)
    {
        return httpClient.PostAsync<ProjectStructureImportRequest, ProjectStructureImportResult>(
            "/api/project-structure-mcp/imports",
            request,
            estimatedMinutes,
            cancellationToken);
    }

    public Task<ProjectManagementGuidanceResponse> QueryKnowledgeAsync(ProjectManagementGuidanceQueryRequest request, CancellationToken cancellationToken = default)
    {
        return httpClient.PostAsync<ProjectManagementGuidanceQueryRequest, ProjectManagementGuidanceResponse>(
            "/api/project-structure-mcp/knowledge/query",
            request,
            cancellationToken: cancellationToken);
    }

    public Task<ProjectStructureAnalyticsResponse> QueryAnalyticsAsync(ProjectStructureAnalyticsQueryRequest request, CancellationToken cancellationToken = default)
    {
        return httpClient.PostAsync<ProjectStructureAnalyticsQueryRequest, ProjectStructureAnalyticsResponse>(
            "/api/project-structure-mcp/analytics/query",
            request,
            cancellationToken: cancellationToken);
    }

    public Task<ProjectStructureLeaseSnapshot> AcquireLeaseAsync(ProjectStructureScopeInput scope, string reason, int durationMinutes, CancellationToken cancellationToken = default)
    {
        var resolvedScope = ResolveScope(scope);
        return httpClient.PostAsync<ProjectStructureLeaseAcquireRequest, ProjectStructureLeaseSnapshot>(
            "/api/project-structure-mcp/leases/acquire",
            new ProjectStructureLeaseAcquireRequest(resolvedScope.ScopeKind, resolvedScope.ScopeKey, reason, durationMinutes),
            cancellationToken: cancellationToken);
    }

    public Task<ProjectStructureLeaseSnapshot?> GetCurrentLeaseAsync(ProjectStructureScopeInput scope, CancellationToken cancellationToken = default)
    {
        var resolvedScope = ResolveScope(scope);
        var path = $"/api/project-structure-mcp/leases/current?scopeKind={resolvedScope.ScopeKind}&scopeKey={Uri.EscapeDataString(resolvedScope.ScopeKey)}";
        return httpClient.GetOptionalAsync<ProjectStructureLeaseSnapshot>(path, cancellationToken: cancellationToken);
    }

    public Task<ProjectStructureLeaseSnapshot?> ReleaseLeaseAsync(ProjectStructureScopeInput scope, string leaseToken, CancellationToken cancellationToken = default)
    {
        var resolvedScope = ResolveScope(scope);
        return httpClient.PostOptionalAsync<ProjectStructureLeaseReleaseRequest, ProjectStructureLeaseSnapshot>(
            "/api/project-structure-mcp/leases/release",
            new ProjectStructureLeaseReleaseRequest(resolvedScope.ScopeKind, resolvedScope.ScopeKey, leaseToken),
            cancellationToken: cancellationToken);
    }

    private ProjectStructureResolvedScope ResolveScope(ProjectStructureScopeInput scope)
    {
        return scope.ScopeKind switch
        {
            ProjectStructureLeaseScopeKind.Project when scope.ProjectId.HasValue => new ProjectStructureResolvedScope(
                ProjectStructureLeaseScopeKind.Project,
                scope.ProjectId.Value.ToString(),
                scope.ProjectId.Value.ToString()),
            ProjectStructureLeaseScopeKind.ProjectNode when !string.IsNullOrWhiteSpace(scope.NodeId) => new ProjectStructureResolvedScope(
                ProjectStructureLeaseScopeKind.ProjectNode,
                scope.NodeId!,
                scope.NodeId!),
            ProjectStructureLeaseScopeKind.RepoBranch => BuildRepoBranchScope(scope),
            _ => throw new ToolInvocationException("InvalidScope", $"Scope '{scope.ScopeKind}' requires its typed identifier.")
        };
    }

    private ProjectStructureResolvedScope BuildRepoBranchScope(ProjectStructureScopeInput scope)
    {
        var repositoryRoot = string.IsNullOrWhiteSpace(scope.RepositoryRoot)
            ? runtimeConfiguration.RepositoryRoot
            : Path.GetFullPath(scope.RepositoryRoot);
        var branchName = string.IsNullOrWhiteSpace(scope.BranchName)
            ? runtimeConfiguration.BranchName
            : scope.BranchName.Trim();
        if (string.IsNullOrWhiteSpace(repositoryRoot) || string.IsNullOrWhiteSpace(branchName))
        {
            throw new ToolInvocationException("InvalidScope", "Repo-branch scope requires a repository root and branch name.");
        }

        var scopeKey = $"repo-branch:{repositoryRoot}:{branchName}";
        return new ProjectStructureResolvedScope(ProjectStructureLeaseScopeKind.RepoBranch, scopeKey, scopeKey);
    }

    private static ProjectStructureCompactNode MapCompactNode(ProjectStructureNodeSummary node)
    {
        return new ProjectStructureCompactNode(
            node.Id,
            node.ParentId,
            node.ObjectType,
            node.ObjectSubtype,
            node.Title,
            node.Subtitle,
            node.Status,
            node.Route,
            node.EffectivePriority,
            node.ProgressMode,
            node.ProgressPercent,
            node.Notes,
            node.MetadataJson,
            node.MediaOriginalFileName,
            node.MediaRelativePath,
            node.MediaContentType,
            node.X,
            node.Y,
            node.DurationSeconds);
    }
}
