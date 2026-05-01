using System.ComponentModel;
using CanDoItAll.Mcp.Core.Contracts;
using CanDoItAll.Mcp.Core.Identity;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace CanDoItAll.Mcp.ProjectStructure;

[McpServerToolType]
public sealed class ProjectStructureTools(IProjectStructureCoordinator coordinator, ILogger<ProjectStructureTools> logger)
{
    [McpServerTool(Name = "project_structure_projects_list", ReadOnly = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Lists available CanDoItAll projects from the central project-structure API.")]
    public Task<McpToolEnvelope<IReadOnlyList<ProjectSummary>>> ProjectStructureProjectsListAsync(CancellationToken cancellationToken = default)
    {
        return ExecuteAsync("project_structure_projects_list", () => coordinator.ListProjectsAsync(cancellationToken));
    }

    [McpServerTool(Name = "project_structure_project_create", ReadOnly = false, Idempotent = false, OpenWorld = false, UseStructuredContent = true)]
    [Description("Creates a new project through the central project-structure API. Provide estimatedMinutes when approval thresholds are configured.")]
    public Task<McpToolEnvelope<ProjectSummary>> ProjectStructureProjectCreateAsync(ProjectStructureProjectSaveRequest request, int? estimatedMinutes = null, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync("project_structure_project_create", () => coordinator.CreateProjectAsync(request, estimatedMinutes, cancellationToken));
    }

    [McpServerTool(Name = "project_structure_project_update", ReadOnly = false, Idempotent = false, OpenWorld = false, UseStructuredContent = true)]
    [Description("Updates an existing CanDoItAll project through the central project-structure API.")]
    public Task<McpToolEnvelope<ProjectSummary>> ProjectStructureProjectUpdateAsync(Guid projectId, ProjectStructureProjectSaveRequest request, int? estimatedMinutes = null, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync("project_structure_project_update", () => coordinator.UpdateProjectAsync(projectId, request, estimatedMinutes, cancellationToken));
    }

    [McpServerTool(Name = "project_structure_hierarchy_get", ReadOnly = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Reads the project and subproject hierarchy for a specific project.")]
    public Task<McpToolEnvelope<ProjectHierarchySnapshot>> ProjectStructureHierarchyGetAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync("project_structure_hierarchy_get", () => coordinator.GetHierarchyAsync(projectId, cancellationToken));
    }

    [McpServerTool(Name = "project_structure_subproject_link", ReadOnly = false, Idempotent = false, OpenWorld = false, UseStructuredContent = true)]
    [Description("Adds or reconnects a subproject under a parent project.")]
    public Task<McpToolEnvelope<OperationAck>> ProjectStructureSubprojectLinkAsync(Guid parentProjectId, ProjectStructureSubprojectChangeRequest request, int? estimatedMinutes = null, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync("project_structure_subproject_link", () => coordinator.ChangeSubprojectAsync(parentProjectId, request, estimatedMinutes, cancellationToken));
    }

    [McpServerTool(Name = "project_structure_read", ReadOnly = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Reads a filtered project structure with compact node payloads by default. Nodes can include actionCapabilities describing runtime run actions (runtime:open/runtime:admin), local drive File Explorer actions (open-local), and IPFS new-tab actions (open-new-tab).")]
    public Task<McpToolEnvelope<ProjectStructureReadToolData>> ProjectStructureReadAsync(Guid projectId, ProjectStructureReadRequest? request = null, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync("project_structure_read", () => coordinator.ReadAsync(projectId, request ?? new ProjectStructureReadRequest(), cancellationToken));
    }

    [McpServerTool(Name = "project_structure_checklist", ReadOnly = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Returns unfinished project-structure items with prerequisite context and effective priority propagation.")]
    public Task<McpToolEnvelope<ProjectStructureChecklistResponse>> ProjectStructureChecklistAsync(Guid projectId, ProjectStructureChecklistRequest? request = null, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync("project_structure_checklist", () => coordinator.GetChecklistAsync(projectId, request ?? new ProjectStructureChecklistRequest(), cancellationToken));
    }

    [McpServerTool(Name = "project_structure_dependencies_query", ReadOnly = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Returns dependency readiness, prerequisite chains, dependents, and effective durations so agents can decide whether work can start or should wait.")]
    public Task<McpToolEnvelope<ProjectStructureDependencyResponse>> ProjectStructureDependenciesQueryAsync(Guid projectId, ProjectStructureDependencyQueryRequest? request = null, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync("project_structure_dependencies_query", () => coordinator.GetDependenciesAsync(projectId, request ?? new ProjectStructureDependencyQueryRequest(), cancellationToken));
    }

    [McpServerTool(Name = "project_structure_node_create", ReadOnly = false, Idempotent = false, OpenWorld = false, UseStructuredContent = true)]
    [Description("Creates a new project-structure node through the central API. Provide estimatedMinutes when approval thresholds are configured. For typed block variants, keep objectType as ProjectBlock and set objectSubtype to a lowercase key such as feature, architecture, implementation, testing, delivery, research, risk, deployment, operations, repos, or dockers.")]
    public Task<McpToolEnvelope<ProjectStructureNodeSummary>> ProjectStructureNodeCreateAsync(Guid projectId, ProjectStructureNodeCreateInput request, int? estimatedMinutes = null, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync("project_structure_node_create", () => coordinator.CreateNodeAsync(projectId, request, estimatedMinutes, cancellationToken));
    }

    [McpServerTool(Name = "project_structure_node_update", ReadOnly = false, Idempotent = false, OpenWorld = false, UseStructuredContent = true)]
    [Description("Updates the title, subtitle, notes, timing, optional metadata, and requested reclassification of an existing project-structure node. Typed blocks must use objectType ProjectBlock plus lowercase objectSubtype values like feature, architecture, implementation, testing, delivery, and deployment. Do not invent enum names like FeatureBlock.")]
    public Task<McpToolEnvelope<ProjectStructureNodeSummary>> ProjectStructureNodeUpdateAsync(Guid projectId, string nodeId, ProjectStructureNodeEditInput request, int? estimatedMinutes = null, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync("project_structure_node_update", () => coordinator.UpdateNodeAsync(projectId, nodeId, request, estimatedMinutes, cancellationToken));
    }

    [McpServerTool(Name = "project_structure_node_move", ReadOnly = false, Idempotent = false, OpenWorld = false, UseStructuredContent = true)]
    [Description("Moves an existing project-structure node to exact canvas coordinates so agents can resolve overlap or spacing issues that recomposition did not fix.")]
    public Task<McpToolEnvelope<OperationAck>> ProjectStructureNodeMoveAsync(Guid projectId, ProjectStructureNodeMoveInput request, int? estimatedMinutes = null, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync("project_structure_node_move", () => coordinator.MoveNodeAsync(projectId, request, estimatedMinutes, cancellationToken));
    }

    [McpServerTool(Name = "project_structure_node_recompose", ReadOnly = false, Idempotent = false, OpenWorld = false, UseStructuredContent = true)]
    [Description("Redistributes a selected branch after imports or manual edits so the resulting project mindmap opens in a readable layout.")]
    public Task<McpToolEnvelope<ProjectStructureSubtreeRecompositionResult>> ProjectStructureNodeRecomposeAsync(Guid projectId, ProjectStructureNodeRecomposeInput request, int? estimatedMinutes = null, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync("project_structure_node_recompose", () => coordinator.RecomposeNodeAsync(projectId, request, estimatedMinutes, cancellationToken));
    }

    [McpServerTool(Name = "project_structure_node_reparent", ReadOnly = false, Idempotent = false, OpenWorld = false, UseStructuredContent = true)]
    [Description("Reconnects an existing project-structure node under a new logical parent node or back to the project root.")]
    public Task<McpToolEnvelope<ProjectStructureNodeSummary>> ProjectStructureNodeReparentAsync(Guid projectId, ProjectStructureNodeReparentInput request, int? estimatedMinutes = null, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync("project_structure_node_reparent", () => coordinator.ReparentNodeAsync(projectId, request, estimatedMinutes, cancellationToken));
    }

    [McpServerTool(Name = "project_structure_approval_request", ReadOnly = false, Idempotent = false, OpenWorld = false, UseStructuredContent = true)]
    [Description("Records an approval-request node in the project structure so blocked work is written back into the graph instead of staying in chat.")]
    public Task<McpToolEnvelope<ProjectStructureNodeSummary>> ProjectStructureApprovalRequestAsync(Guid projectId, ProjectStructureApprovalRequestCreateInput request, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync("project_structure_approval_request", () => coordinator.CreateApprovalRequestAsync(projectId, request, cancellationToken));
    }

    [McpServerTool(Name = "project_structure_asset_get", ReadOnly = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Returns readonly metadata for an existing managed asset node.")]
    public Task<McpToolEnvelope<ProjectStructureAssetDescriptor>> ProjectStructureAssetGetAsync(Guid projectId, string nodeId, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync("project_structure_asset_get", () => coordinator.GetAssetAsync(projectId, nodeId, cancellationToken));
    }

    [McpServerTool(Name = "project_structure_asset_create_revision", ReadOnly = false, Idempotent = false, OpenWorld = false, UseStructuredContent = true)]
    [Description("Creates a new revision asset node under an existing asset node instead of overwriting the original asset.")]
    public Task<McpToolEnvelope<ProjectStructureAssetDescriptor>> ProjectStructureAssetCreateRevisionAsync(Guid projectId, string nodeId, ProjectStructureAssetRevisionRequest request, int? estimatedMinutes = null, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync("project_structure_asset_create_revision", () => coordinator.CreateAssetRevisionAsync(projectId, nodeId, request, estimatedMinutes, cancellationToken));
    }

    [McpServerTool(Name = "project_structure_import", ReadOnly = false, Idempotent = false, OpenWorld = false, UseStructuredContent = true)]
    [Description("Imports an externally described project structure such as Mermaid, DOCX outline, XMind, or JSON outline into the central project structure.")]
    public Task<McpToolEnvelope<ProjectStructureImportResult>> ProjectStructureImportAsync(ProjectStructureImportRequest request, int? estimatedMinutes = null, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync("project_structure_import", () => coordinator.ImportAsync(request, estimatedMinutes, cancellationToken));
    }

    [McpServerTool(Name = "project_structure_knowledge_query", ReadOnly = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Queries the central project-management knowledge guidance that supports planning, reporting, approval, estimation, and mission discussions.")]
    public Task<McpToolEnvelope<ProjectManagementGuidanceResponse>> ProjectStructureKnowledgeQueryAsync(ProjectManagementGuidanceQueryRequest? request = null, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync("project_structure_knowledge_query", () => coordinator.QueryKnowledgeAsync(request ?? new ProjectManagementGuidanceQueryRequest(), cancellationToken));
    }

    [McpServerTool(Name = "project_structure_analytics_query", ReadOnly = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Queries project-structure operation analytics so validation and post-implementation review can inspect what agents actually did.")]
    public Task<McpToolEnvelope<ProjectStructureAnalyticsResponse>> ProjectStructureAnalyticsQueryAsync(ProjectStructureAnalyticsQueryRequest? request = null, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync("project_structure_analytics_query", () => coordinator.QueryAnalyticsAsync(request ?? new ProjectStructureAnalyticsQueryRequest(), cancellationToken));
    }

    [McpServerTool(Name = "project_structure_project_lease_acquire", ReadOnly = false, Idempotent = false, OpenWorld = false, UseStructuredContent = true)]
    [Description("Acquires or renews a project-scoped central lease so concurrent agents do not mutate the same project at the same time.")]
    public Task<McpToolEnvelope<ProjectStructureLeaseSnapshot>> ProjectStructureProjectLeaseAcquireAsync(Guid projectId, string reason, int durationMinutes = 15, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync("project_structure_project_lease_acquire", () => coordinator.AcquireLeaseAsync(new ProjectStructureScopeInput(ProjectStructureLeaseScopeKind.Project, ProjectId: projectId), reason, durationMinutes, cancellationToken));
    }

    [McpServerTool(Name = "project_structure_repo_branch_lease_acquire", ReadOnly = false, Idempotent = false, OpenWorld = false, UseStructuredContent = true)]
    [Description("Acquires or renews a repo-branch lease using the configured or supplied repository root and branch so separate agents do not collide on the same branch.")]
    public Task<McpToolEnvelope<ProjectStructureLeaseSnapshot>> ProjectStructureRepoBranchLeaseAcquireAsync(string reason, string? repositoryRoot = null, string? branchName = null, int durationMinutes = 15, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync("project_structure_repo_branch_lease_acquire", () => coordinator.AcquireLeaseAsync(new ProjectStructureScopeInput(ProjectStructureLeaseScopeKind.RepoBranch, RepositoryRoot: repositoryRoot, BranchName: branchName), reason, durationMinutes, cancellationToken));
    }

    [McpServerTool(Name = "project_structure_lease_get", ReadOnly = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Gets the current active project, node, or repo-branch lease for the supplied scope.")]
    public Task<McpToolEnvelope<ProjectStructureLeaseSnapshot?>> ProjectStructureLeaseGetAsync(ProjectStructureScopeInput scope, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync("project_structure_lease_get", () => coordinator.GetCurrentLeaseAsync(scope, cancellationToken));
    }

    [McpServerTool(Name = "project_structure_lease_release", ReadOnly = false, Idempotent = false, OpenWorld = false, UseStructuredContent = true)]
    [Description("Releases an existing project, node, or repo-branch lease token.")]
    public Task<McpToolEnvelope<ProjectStructureLeaseSnapshot?>> ProjectStructureLeaseReleaseAsync(ProjectStructureScopeInput scope, string leaseToken, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync("project_structure_lease_release", () => coordinator.ReleaseLeaseAsync(scope, leaseToken, cancellationToken));
    }

    private async Task<McpToolEnvelope<T>> ExecuteAsync<T>(string toolName, Func<Task<T>> callback)
    {
        var correlationId = CorrelationIdFactory.Create("project-structure");

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
            "AgentTokenRequired" or "InvalidAgentToken" => "auth_failed",
            "AgentDisabled" => "agent_disabled",
            "ApprovalRequired" => "approval_required",
            "CapabilityDenied" => "capability_denied",
            "EstimateRequired" => "estimate_required",
            "InvalidEstimatedMinutes" => "validation_error",
            "LeaseConflict" => "lease_conflict",
            "LeaseMissing" => "lease_missing",
            "NodeNotFound" or "ProjectNotFound" => "not_found",
            "InvalidScope" => "validation_error",
            _ => "failed"
        };
    }
}
