using System.Diagnostics;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workspace;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.AI;

namespace CanDoItAll.AgentFramework.Maf;

public sealed partial class MafAgentRuntime
{
    private ProjectStructureToolBuilder? CreateProjectStructureToolBuilder(IWorkspaceCommandExecutionService workspaceCommandExecutionService)
    {
        var agentService = services.GetService(typeof(ProjectStructureAgentService)) as ProjectStructureAgentService;
        var leaseService = services.GetService(typeof(ProjectStructureLeaseService)) as ProjectStructureLeaseService;
        var analyticsService = services.GetService(typeof(ProjectStructureAnalyticsService)) as ProjectStructureAnalyticsService;
        var knowledgeService = services.GetService(typeof(ProjectManagementKnowledgeService)) as ProjectManagementKnowledgeService;

        if (agentService is null ||
            leaseService is null ||
            analyticsService is null ||
            knowledgeService is null)
        {
            return null;
        }

        return new ProjectStructureToolBuilder(
            this,
            agentService,
            leaseService,
            analyticsService,
            knowledgeService,
            workspaceCommandExecutionService);
    }

    private sealed class ProjectStructureToolBuilder(
        MafAgentRuntime owner,
        ProjectStructureAgentService agentService,
        ProjectStructureLeaseService leaseService,
        ProjectStructureAnalyticsService analyticsService,
        ProjectManagementKnowledgeService knowledgeService,
        IWorkspaceCommandExecutionService workspaceCommandExecutionService)
    {
        private readonly MafAgentRuntime owner = owner;
        private readonly ProjectStructureAgentService agentService = agentService;
        private readonly ProjectStructureLeaseService leaseService = leaseService;
        private readonly ProjectStructureAnalyticsService analyticsService = analyticsService;
        private readonly ProjectManagementKnowledgeService knowledgeService = knowledgeService;
        private readonly IWorkspaceCommandExecutionService workspaceCommandExecutionService = workspaceCommandExecutionService;
        private string? currentBranchName;

        public IReadOnlyList<AITool> CreateTools(AgentDefinition agent)
        {
            var accessSettings = AgentProjectStructureAccessMetadata.Read(agent.ConfigurationJson);
            var accessState = new ProjectStructureAccessState(accessSettings);

            return
            [
                AIFunctionFactory.Create(
                    (CancellationToken cancellationToken) => ProjectStructureProjectsListAsync(agent, accessState, cancellationToken),
                    "project_structure_projects_list",
                    "Lists the CanDoItAll projects that this internal agent is allowed to access."),
                AIFunctionFactory.Create(
                    (ProjectStructureProjectSaveRequest request, int? estimatedMinutes, CancellationToken cancellationToken) => ProjectStructureProjectCreateAsync(agent, accessState, request, estimatedMinutes, cancellationToken),
                    "project_structure_project_create",
                    "Creates a new CanDoItAll project through the internal workspace project-structure service."),
                AIFunctionFactory.Create(
                    (Guid projectId, ProjectStructureProjectSaveRequest request, int? estimatedMinutes, CancellationToken cancellationToken) => ProjectStructureProjectUpdateAsync(agent, accessState, projectId, request, estimatedMinutes, cancellationToken),
                    "project_structure_project_update",
                    "Updates an existing CanDoItAll project through the internal workspace project-structure service."),
                AIFunctionFactory.Create(
                    (Guid projectId, CancellationToken cancellationToken) => ProjectStructureHierarchyGetAsync(agent, accessState, projectId, cancellationToken),
                    "project_structure_hierarchy_get",
                    "Reads the project and subproject hierarchy for a specific project."),
                AIFunctionFactory.Create(
                    (Guid parentProjectId, ProjectStructureSubprojectChangeRequest request, int? estimatedMinutes, CancellationToken cancellationToken) => ProjectStructureSubprojectLinkAsync(agent, accessState, parentProjectId, request, estimatedMinutes, cancellationToken),
                    "project_structure_subproject_link",
                    "Adds or reconnects a subproject under a parent project."),
                AIFunctionFactory.Create(
                    (Guid projectId, ProjectStructureReadRequest? request, CancellationToken cancellationToken) => ProjectStructureReadAsync(agent, accessState, projectId, request, cancellationToken),
                    "project_structure_read",
                    "Reads a filtered project structure with compact node payloads by default."),
                AIFunctionFactory.Create(
                    (Guid projectId, ProjectStructureChecklistRequest? request, CancellationToken cancellationToken) => ProjectStructureChecklistAsync(agent, accessState, projectId, request, cancellationToken),
                    "project_structure_checklist",
                    "Returns unfinished project-structure items with prerequisite context and effective priority propagation."),
                AIFunctionFactory.Create(
                    (Guid projectId, ProjectStructureDependencyQueryRequest? request, CancellationToken cancellationToken) => ProjectStructureDependenciesQueryAsync(agent, accessState, projectId, request, cancellationToken),
                    "project_structure_dependencies_query",
                    "Returns dependency readiness, prerequisite chains, dependents, and effective durations."),
                AIFunctionFactory.Create(
                    (Guid projectId, ProjectStructureNodeCreateInput request, int? estimatedMinutes, CancellationToken cancellationToken) => ProjectStructureNodeCreateAsync(agent, accessState, projectId, request, estimatedMinutes, cancellationToken),
                    "project_structure_node_create",
                    "Creates a new project-structure node through the internal workspace service."),
                AIFunctionFactory.Create(
                    (Guid projectId, string nodeId, ProjectStructureNodeEditInput request, int? estimatedMinutes, CancellationToken cancellationToken) => ProjectStructureNodeUpdateAsync(agent, accessState, projectId, nodeId, request, estimatedMinutes, cancellationToken),
                    "project_structure_node_update",
                    "Updates the title, subtitle, notes, and optional metadata of an existing project-structure node."),
                AIFunctionFactory.Create(
                    (Guid projectId, ProjectStructureNodeRecomposeInput request, int? estimatedMinutes, CancellationToken cancellationToken) => ProjectStructureNodeRecomposeAsync(agent, accessState, projectId, request, estimatedMinutes, cancellationToken),
                    "project_structure_node_recompose",
                    "Redistributes a selected branch after imports or manual edits so the project mindmap opens in a readable layout."),
                AIFunctionFactory.Create(
                    (Guid projectId, ProjectStructureNodeReparentInput request, int? estimatedMinutes, CancellationToken cancellationToken) => ProjectStructureNodeReparentAsync(agent, accessState, projectId, request, estimatedMinutes, cancellationToken),
                    "project_structure_node_reparent",
                    "Reconnects an existing project-structure node under a new logical parent node or back to the project root."),
                AIFunctionFactory.Create(
                    (Guid projectId, ProjectStructureApprovalRequestCreateInput request, CancellationToken cancellationToken) => ProjectStructureApprovalRequestAsync(agent, accessState, projectId, request, cancellationToken),
                    "project_structure_approval_request",
                    "Records an approval-request node in the project structure so blocked work is written back into the graph."),
                AIFunctionFactory.Create(
                    (Guid projectId, string nodeId, CancellationToken cancellationToken) => ProjectStructureAssetGetAsync(agent, accessState, projectId, nodeId, cancellationToken),
                    "project_structure_asset_get",
                    "Returns readonly metadata for an existing managed asset node."),
                AIFunctionFactory.Create(
                    (Guid projectId, string nodeId, ProjectStructureAssetRevisionRequest request, int? estimatedMinutes, CancellationToken cancellationToken) => ProjectStructureAssetCreateRevisionAsync(agent, accessState, projectId, nodeId, request, estimatedMinutes, cancellationToken),
                    "project_structure_asset_create_revision",
                    "Creates a new revision asset node under an existing asset node instead of overwriting the original asset."),
                AIFunctionFactory.Create(
                    (ProjectStructureImportRequest request, int? estimatedMinutes, CancellationToken cancellationToken) => ProjectStructureImportAsync(agent, accessState, request, estimatedMinutes, cancellationToken),
                    "project_structure_import",
                    "Imports Mermaid, DOCX outline, XMind, or JSON outline content into the central project structure."),
                AIFunctionFactory.Create(
                    (ProjectManagementGuidanceQueryRequest? request, CancellationToken cancellationToken) => ProjectStructureKnowledgeQueryAsync(agent, accessState, request, cancellationToken),
                    "project_structure_knowledge_query",
                    "Queries project-management guidance that supports planning, reporting, approval, estimation, and mission discussions."),
                AIFunctionFactory.Create(
                    (ProjectStructureAnalyticsQueryRequest? request, CancellationToken cancellationToken) => ProjectStructureAnalyticsQueryAsync(agent, accessState, request, cancellationToken),
                    "project_structure_analytics_query",
                    "Queries project-structure operation analytics so validation and post-implementation review can inspect what agents actually did."),
                AIFunctionFactory.Create(
                    (Guid projectId, string reason, int durationMinutes, CancellationToken cancellationToken) => ProjectStructureProjectLeaseAcquireAsync(agent, accessState, projectId, reason, durationMinutes, cancellationToken),
                    "project_structure_project_lease_acquire",
                    "Acquires or renews a project-scoped lease so concurrent agents do not mutate the same project at the same time."),
                AIFunctionFactory.Create(
                    (string reason, string? repositoryRoot, string? branchName, int durationMinutes, CancellationToken cancellationToken) => ProjectStructureRepoBranchLeaseAcquireAsync(agent, accessState, reason, repositoryRoot, branchName, durationMinutes, cancellationToken),
                    "project_structure_repo_branch_lease_acquire",
                    "Acquires or renews a repo-branch lease so separate agents do not collide on the same branch."),
                AIFunctionFactory.Create(
                    (ProjectStructureScopeInput scope, CancellationToken cancellationToken) => ProjectStructureLeaseGetAsync(agent, accessState, scope, cancellationToken),
                    "project_structure_lease_get",
                    "Gets the current active project, node, or repo-branch lease for the supplied scope."),
                AIFunctionFactory.Create(
                    (ProjectStructureScopeInput scope, string leaseToken, CancellationToken cancellationToken) => ProjectStructureLeaseReleaseAsync(agent, accessState, scope, leaseToken, cancellationToken),
                    "project_structure_lease_release",
                    "Releases an existing project, node, or repo-branch lease token.")
            ];
        }

        private Task<IReadOnlyList<ProjectSummary>> ProjectStructureProjectsListAsync(
            AgentDefinition agent,
            ProjectStructureAccessState accessState,
            CancellationToken cancellationToken)
        {
            return ExecuteAsync<IReadOnlyList<ProjectSummary>>(
                agent,
                "projects.list",
                null,
                null,
                null,
                null,
                null,
                async cancellationToken =>
                {
                    EnsureReadAllowed(accessState);
                    var projects = await agentService.ListProjectsAsync(cancellationToken);
                    return projects
                        .Where(project => accessState.AllowedProjectIds.Contains(project.Id))
                        .OrderBy(project => project.Name, StringComparer.OrdinalIgnoreCase)
                        .ToList();
                },
                cancellationToken);
        }

        private Task<ProjectSummary> ProjectStructureProjectCreateAsync(
            AgentDefinition agent,
            ProjectStructureAccessState accessState,
            ProjectStructureProjectSaveRequest request,
            int? estimatedMinutes,
            CancellationToken cancellationToken)
        {
            return ExecuteAsync(
                agent,
                "projects.create",
                null,
                null,
                null,
                null,
                request,
                async cancellationToken =>
                {
                    EnsureWriteAllowed(accessState);
                    var context = BuildAgentContext(agent);
                    var created = await agentService.SaveProjectAsync(null, request, context, cancellationToken);
                    accessState.AllowedProjectIds.Add(created.Id);
                    return created;
                },
                cancellationToken,
                projectIdSelector: response => response.Id);
        }

        private Task<ProjectSummary> ProjectStructureProjectUpdateAsync(
            AgentDefinition agent,
            ProjectStructureAccessState accessState,
            Guid projectId,
            ProjectStructureProjectSaveRequest request,
            int? estimatedMinutes,
            CancellationToken cancellationToken)
        {
            return ExecuteAsync(
                agent,
                "projects.update",
                projectId,
                null,
                ProjectStructureLeaseScopeKind.Project,
                projectId.ToString("D"),
                request,
                async cancellationToken =>
                {
                    EnsureProjectWriteAllowed(accessState, projectId);
                    return await agentService.SaveProjectAsync(projectId, request, BuildAgentContext(agent), cancellationToken);
                },
                cancellationToken);
        }

        private Task<ProjectHierarchySnapshot> ProjectStructureHierarchyGetAsync(
            AgentDefinition agent,
            ProjectStructureAccessState accessState,
            Guid projectId,
            CancellationToken cancellationToken)
        {
            return ExecuteAsync(
                agent,
                "projects.hierarchy",
                projectId,
                null,
                null,
                null,
                null,
                async cancellationToken =>
                {
                    EnsureProjectReadAllowed(accessState, projectId);
                    return await agentService.GetHierarchyAsync(projectId, cancellationToken);
                },
                cancellationToken);
        }

        private Task<OperationAck> ProjectStructureSubprojectLinkAsync(
            AgentDefinition agent,
            ProjectStructureAccessState accessState,
            Guid parentProjectId,
            ProjectStructureSubprojectChangeRequest request,
            int? estimatedMinutes,
            CancellationToken cancellationToken)
        {
            return ExecuteAsync(
                agent,
                "projects.subproject-change",
                request.ChildProjectId,
                null,
                ProjectStructureLeaseScopeKind.Project,
                request.ChildProjectId.ToString("D"),
                request,
                async cancellationToken =>
                {
                    EnsureProjectWriteAllowed(accessState, parentProjectId);
                    EnsureProjectWriteAllowed(accessState, request.ChildProjectId);
                    if (request.CurrentParentProjectId.HasValue)
                    {
                        EnsureProjectWriteAllowed(accessState, request.CurrentParentProjectId.Value);
                    }

                    await agentService.ChangeSubprojectAsync(parentProjectId, request, BuildAgentContext(agent), cancellationToken);
                    return new OperationAck(true);
                },
                cancellationToken);
        }

        private Task<ProjectStructureReadToolData> ProjectStructureReadAsync(
            AgentDefinition agent,
            ProjectStructureAccessState accessState,
            Guid projectId,
            ProjectStructureReadRequest? request,
            CancellationToken cancellationToken)
        {
            return ExecuteAsync(
                agent,
                "structure.read",
                projectId,
                null,
                null,
                null,
                request,
                async cancellationToken =>
                {
                    EnsureProjectReadAllowed(accessState, projectId);
                    var response = await agentService.GetStructureAsync(projectId, request ?? new ProjectStructureReadRequest(), cancellationToken);
                    return new ProjectStructureReadToolData(
                        response.ProjectId,
                        response.ProjectName,
                        response.Nodes.Select(MapCompactNode).ToList(),
                        response.Links,
                        response.Warnings);
                },
                cancellationToken);
        }

        private Task<ProjectStructureChecklistResponse> ProjectStructureChecklistAsync(
            AgentDefinition agent,
            ProjectStructureAccessState accessState,
            Guid projectId,
            ProjectStructureChecklistRequest? request,
            CancellationToken cancellationToken)
        {
            return ExecuteAsync(
                agent,
                "checklists.query",
                projectId,
                null,
                null,
                null,
                request,
                async cancellationToken =>
                {
                    EnsureProjectReadAllowed(accessState, projectId);
                    return await agentService.GetChecklistAsync(projectId, request ?? new ProjectStructureChecklistRequest(), cancellationToken);
                },
                cancellationToken);
        }

        private Task<ProjectStructureDependencyResponse> ProjectStructureDependenciesQueryAsync(
            AgentDefinition agent,
            ProjectStructureAccessState accessState,
            Guid projectId,
            ProjectStructureDependencyQueryRequest? request,
            CancellationToken cancellationToken)
        {
            return ExecuteAsync(
                agent,
                "dependencies.query",
                projectId,
                null,
                null,
                null,
                request,
                async cancellationToken =>
                {
                    EnsureProjectReadAllowed(accessState, projectId);
                    return await agentService.GetDependenciesAsync(projectId, request ?? new ProjectStructureDependencyQueryRequest(), cancellationToken);
                },
                cancellationToken);
        }

        private Task<ProjectStructureNodeSummary> ProjectStructureNodeCreateAsync(
            AgentDefinition agent,
            ProjectStructureAccessState accessState,
            Guid projectId,
            ProjectStructureNodeCreateInput request,
            int? estimatedMinutes,
            CancellationToken cancellationToken)
        {
            return ExecuteAsync(
                agent,
                "structure.node-create",
                projectId,
                null,
                ProjectStructureLeaseScopeKind.Project,
                projectId.ToString("D"),
                request,
                async cancellationToken =>
                {
                    EnsureProjectWriteAllowed(accessState, projectId);
                    return await agentService.CreateNodeAsync(projectId, request, BuildAgentContext(agent), cancellationToken);
                },
                cancellationToken);
        }

        private Task<ProjectStructureNodeSummary> ProjectStructureNodeUpdateAsync(
            AgentDefinition agent,
            ProjectStructureAccessState accessState,
            Guid projectId,
            string nodeId,
            ProjectStructureNodeEditInput request,
            int? estimatedMinutes,
            CancellationToken cancellationToken)
        {
            return ExecuteAsync(
                agent,
                "structure.node-update",
                projectId,
                nodeId,
                ProjectStructureLeaseScopeKind.Project,
                projectId.ToString("D"),
                request,
                async cancellationToken =>
                {
                    EnsureProjectWriteAllowed(accessState, projectId);
                    return await agentService.UpdateNodeAsync(projectId, nodeId, request, BuildAgentContext(agent), cancellationToken);
                },
                cancellationToken);
        }

        private Task<ProjectStructureSubtreeRecompositionResult> ProjectStructureNodeRecomposeAsync(
            AgentDefinition agent,
            ProjectStructureAccessState accessState,
            Guid projectId,
            ProjectStructureNodeRecomposeInput request,
            int? estimatedMinutes,
            CancellationToken cancellationToken)
        {
            return ExecuteAsync(
                agent,
                "structure.node-recompose",
                projectId,
                request.RootNodeId,
                ProjectStructureLeaseScopeKind.Project,
                projectId.ToString("D"),
                request,
                async cancellationToken =>
                {
                    EnsureProjectWriteAllowed(accessState, projectId);
                    return await agentService.RecomposeNodeAsync(projectId, request, BuildAgentContext(agent), cancellationToken);
                },
                cancellationToken);
        }

        private Task<ProjectStructureNodeSummary> ProjectStructureNodeReparentAsync(
            AgentDefinition agent,
            ProjectStructureAccessState accessState,
            Guid projectId,
            ProjectStructureNodeReparentInput request,
            int? estimatedMinutes,
            CancellationToken cancellationToken)
        {
            return ExecuteAsync(
                agent,
                "structure.node-reparent",
                projectId,
                request.NodeId,
                ProjectStructureLeaseScopeKind.Project,
                projectId.ToString("D"),
                request,
                async cancellationToken =>
                {
                    EnsureProjectWriteAllowed(accessState, projectId);
                    return await agentService.ReparentNodeAsync(projectId, request, BuildAgentContext(agent), cancellationToken);
                },
                cancellationToken);
        }

        private Task<ProjectStructureNodeSummary> ProjectStructureApprovalRequestAsync(
            AgentDefinition agent,
            ProjectStructureAccessState accessState,
            Guid projectId,
            ProjectStructureApprovalRequestCreateInput request,
            CancellationToken cancellationToken)
        {
            return ExecuteAsync(
                agent,
                "approvals.request",
                projectId,
                null,
                ProjectStructureLeaseScopeKind.Project,
                projectId.ToString("D"),
                request,
                async cancellationToken =>
                {
                    EnsureProjectWriteAllowed(accessState, projectId);
                    return await agentService.CreateApprovalRequestAsync(projectId, request, BuildAgentContext(agent), cancellationToken);
                },
                cancellationToken);
        }

        private Task<ProjectStructureAssetDescriptor> ProjectStructureAssetGetAsync(
            AgentDefinition agent,
            ProjectStructureAccessState accessState,
            Guid projectId,
            string nodeId,
            CancellationToken cancellationToken)
        {
            return ExecuteAsync(
                agent,
                "assets.get",
                projectId,
                nodeId,
                null,
                null,
                null,
                async cancellationToken =>
                {
                    EnsureProjectReadAllowed(accessState, projectId);
                    return await agentService.GetAssetAsync(projectId, nodeId, cancellationToken);
                },
                cancellationToken);
        }

        private Task<ProjectStructureAssetDescriptor> ProjectStructureAssetCreateRevisionAsync(
            AgentDefinition agent,
            ProjectStructureAccessState accessState,
            Guid projectId,
            string nodeId,
            ProjectStructureAssetRevisionRequest request,
            int? estimatedMinutes,
            CancellationToken cancellationToken)
        {
            return ExecuteAsync(
                agent,
                "assets.create-revision",
                projectId,
                nodeId,
                ProjectStructureLeaseScopeKind.Project,
                projectId.ToString("D"),
                request,
                async cancellationToken =>
                {
                    EnsureProjectWriteAllowed(accessState, projectId);
                    return await agentService.CreateAssetRevisionAsync(projectId, nodeId, request, BuildAgentContext(agent), cancellationToken);
                },
                cancellationToken);
        }

        private Task<ProjectStructureImportResult> ProjectStructureImportAsync(
            AgentDefinition agent,
            ProjectStructureAccessState accessState,
            ProjectStructureImportRequest request,
            int? estimatedMinutes,
            CancellationToken cancellationToken)
        {
            return ExecuteAsync(
                agent,
                "imports.run",
                request.ProjectId,
                null,
                ProjectStructureLeaseScopeKind.Project,
                request.ProjectId.ToString("D"),
                request,
                async cancellationToken =>
                {
                    EnsureProjectWriteAllowed(accessState, request.ProjectId);
                    return await agentService.ImportAsync(request, BuildAgentContext(agent), cancellationToken);
                },
                cancellationToken);
        }

        private Task<ProjectManagementGuidanceResponse> ProjectStructureKnowledgeQueryAsync(
            AgentDefinition agent,
            ProjectStructureAccessState accessState,
            ProjectManagementGuidanceQueryRequest? request,
            CancellationToken cancellationToken)
        {
            return ExecuteAsync(
                agent,
                "knowledge.query",
                null,
                null,
                null,
                null,
                request,
                async cancellationToken =>
                {
                    EnsureReadAllowed(accessState);
                    var query = request ?? new ProjectManagementGuidanceQueryRequest();
                    var entries = await knowledgeService.QueryAsync(
                        new ProjectManagementKnowledgeQuery(
                            query.Categories?.Select(MapGuidanceCategory).ToList(),
                            query.Query,
                            query.Take),
                        cancellationToken);

                    return new ProjectManagementGuidanceResponse(
                        entries.Select(entry => new ProjectManagementGuidanceEntry(
                            entry.Id,
                            MapKnowledgeCategory(entry.Category),
                            entry.Title,
                            entry.Summary,
                            entry.Guidance,
                            entry.Tags,
                            entry.IsMissionAnchor))
                            .ToList());
                },
                cancellationToken);
        }

        private Task<ProjectStructureAnalyticsResponse> ProjectStructureAnalyticsQueryAsync(
            AgentDefinition agent,
            ProjectStructureAccessState accessState,
            ProjectStructureAnalyticsQueryRequest? request,
            CancellationToken cancellationToken)
        {
            return ExecuteAsync(
                agent,
                "analytics.query",
                request?.ProjectId,
                null,
                null,
                null,
                request,
                async cancellationToken =>
                {
                    EnsureReadAllowed(accessState);
                    var query = request ?? new ProjectStructureAnalyticsQueryRequest();
                    if (query.ProjectId.HasValue)
                    {
                        EnsureProjectReadAllowed(accessState, query.ProjectId.Value);
                    }

                    var response = await analyticsService.QueryAsync(query with
                    {
                        Take = Math.Clamp(query.Take, 1, 200)
                    }, cancellationToken);

                    if (accessState.AllowedProjectIds.Count == 0)
                    {
                        return response with
                        {
                            Entries = response.Entries
                                .Where(entry => !entry.ProjectId.HasValue)
                                .ToList()
                        };
                    }

                    return response with
                    {
                        Entries = response.Entries
                            .Where(entry => !entry.ProjectId.HasValue || accessState.AllowedProjectIds.Contains(entry.ProjectId.Value))
                            .ToList()
                    };
                },
                cancellationToken);
        }

        private Task<ProjectStructureLeaseSnapshot> ProjectStructureProjectLeaseAcquireAsync(
            AgentDefinition agent,
            ProjectStructureAccessState accessState,
            Guid projectId,
            string reason,
            int durationMinutes,
            CancellationToken cancellationToken)
        {
            return ExecuteAsync(
                agent,
                "leases.acquire",
                projectId,
                null,
                ProjectStructureLeaseScopeKind.Project,
                projectId.ToString("D"),
                new { projectId, reason, durationMinutes },
                async cancellationToken =>
                {
                    EnsureProjectWriteAllowed(accessState, projectId);
                    return await leaseService.AcquireAsync(
                        new ProjectStructureLeaseAcquireRequest(
                            ProjectStructureLeaseScopeKind.Project,
                            projectId.ToString("D"),
                            reason,
                            durationMinutes),
                        BuildAgentContext(agent),
                        cancellationToken);
                },
                cancellationToken);
        }

        private async Task<ProjectStructureLeaseSnapshot> ProjectStructureRepoBranchLeaseAcquireAsync(
            AgentDefinition agent,
            ProjectStructureAccessState accessState,
            string reason,
            string? repositoryRoot,
            string? branchName,
            int durationMinutes,
            CancellationToken cancellationToken)
        {
            EnsureWriteAllowed(accessState);
            var resolvedRepositoryRoot = string.IsNullOrWhiteSpace(repositoryRoot)
                ? owner.workspaceRoot
                : owner.ResolvePathFromWorkspace(repositoryRoot, false);
            var resolvedBranchName = string.IsNullOrWhiteSpace(branchName)
                ? await ResolveCurrentBranchNameAsync(cancellationToken)
                : branchName.Trim();
            if (string.IsNullOrWhiteSpace(resolvedBranchName))
            {
                throw new ProjectStructureAgentException(400, "InvalidScope", "A branch name is required to acquire a repo-branch lease.");
            }

            var scopeKey = BuildRepoBranchScopeKey(resolvedRepositoryRoot, resolvedBranchName);
            return await ExecuteAsync(
                agent,
                "leases.acquire",
                null,
                null,
                ProjectStructureLeaseScopeKind.RepoBranch,
                scopeKey,
                new { reason, resolvedRepositoryRoot, resolvedBranchName, durationMinutes },
                async cancellationToken =>
                {
                    var context = BuildAgentContext(agent, resolvedBranchName, resolvedRepositoryRoot);
                    return await leaseService.AcquireAsync(
                        new ProjectStructureLeaseAcquireRequest(
                            ProjectStructureLeaseScopeKind.RepoBranch,
                            scopeKey,
                            reason,
                            durationMinutes),
                        context,
                        cancellationToken);
                },
                cancellationToken);
        }

        private async Task<ProjectStructureLeaseSnapshot?> ProjectStructureLeaseGetAsync(
            AgentDefinition agent,
            ProjectStructureAccessState accessState,
            ProjectStructureScopeInput scope,
            CancellationToken cancellationToken)
        {
            EnsureReadAllowed(accessState);
            var resolvedScope = await ResolveScopeAsync(agent, accessState, scope, false, cancellationToken);
            return await ExecuteAsync(
                agent,
                "leases.current",
                resolvedScope.ProjectId,
                null,
                resolvedScope.ScopeKind,
                resolvedScope.ScopeKey,
                scope,
                async cancellationToken => await leaseService.GetActiveLeaseAsync(resolvedScope.ScopeKind, resolvedScope.ScopeKey, cancellationToken),
                cancellationToken);
        }

        private async Task<ProjectStructureLeaseSnapshot?> ProjectStructureLeaseReleaseAsync(
            AgentDefinition agent,
            ProjectStructureAccessState accessState,
            ProjectStructureScopeInput scope,
            string leaseToken,
            CancellationToken cancellationToken)
        {
            EnsureWriteAllowed(accessState);
            var resolvedScope = await ResolveScopeAsync(agent, accessState, scope, true, cancellationToken);
            return await ExecuteAsync(
                agent,
                "leases.release",
                resolvedScope.ProjectId,
                null,
                resolvedScope.ScopeKind,
                resolvedScope.ScopeKey,
                new { scope, leaseToken },
                async cancellationToken =>
                {
                    var context = BuildAgentContext(agent, resolvedScope.BranchName, resolvedScope.RepositoryRoot);
                    return await leaseService.ReleaseAsync(
                        new ProjectStructureLeaseReleaseRequest(
                            resolvedScope.ScopeKind,
                            resolvedScope.ScopeKey,
                            leaseToken),
                        context,
                        cancellationToken);
                },
                cancellationToken);
        }

        private async Task<T> ExecuteAsync<T>(
            AgentDefinition agent,
            string operationName,
            Guid? projectId,
            string? nodeId,
            ProjectStructureLeaseScopeKind? scopeKind,
            string? scopeKey,
            object? requestSummary,
            Func<CancellationToken, Task<T>> action,
            CancellationToken cancellationToken,
            Func<T, Guid?>? projectIdSelector = null)
        {
            var stopwatch = Stopwatch.StartNew();
            var context = BuildAgentContext(agent);

            try
            {
                var response = await action(cancellationToken);
                stopwatch.Stop();
                await analyticsService.RecordAsync(
                    new ProjectStructureAnalyticsWriteRequest(
                        operationName,
                        projectId ?? projectIdSelector?.Invoke(response),
                        nodeId,
                        scopeKind,
                        scopeKey,
                        context,
                        true,
                        stopwatch.ElapsedMilliseconds,
                        ExtractWarnings(response),
                        null,
                        null,
                        ProjectStructureAnalyticsService.SerializeSummary(requestSummary),
                        ProjectStructureAnalyticsService.SerializeSummary(response)),
                    cancellationToken);
                return response;
            }
            catch (ProjectStructureAgentException exception)
            {
                stopwatch.Stop();
                await analyticsService.RecordAsync(
                    new ProjectStructureAnalyticsWriteRequest(
                        operationName,
                        projectId,
                        nodeId,
                        scopeKind,
                        scopeKey,
                        context,
                        false,
                        stopwatch.ElapsedMilliseconds,
                        [],
                        exception.ErrorCode,
                        exception.Message,
                        ProjectStructureAnalyticsService.SerializeSummary(requestSummary),
                        ProjectStructureAnalyticsService.SerializeSummary(exception.Details)),
                    cancellationToken);
                throw;
            }
            catch (Exception exception)
            {
                stopwatch.Stop();
                await analyticsService.RecordAsync(
                    new ProjectStructureAnalyticsWriteRequest(
                        operationName,
                        projectId,
                        nodeId,
                        scopeKind,
                        scopeKey,
                        context,
                        false,
                        stopwatch.ElapsedMilliseconds,
                        [],
                        "UnhandledError",
                        exception.Message,
                        ProjectStructureAnalyticsService.SerializeSummary(requestSummary),
                        ProjectStructureAnalyticsService.SerializeSummary(new { exception.Message })),
                    cancellationToken);
                throw;
            }
        }

        private ProjectStructureAgentContext BuildAgentContext(
            AgentDefinition agent,
            string? branchName = null,
            string? repositoryRoot = null)
        {
            return new ProjectStructureAgentContext(
                agent.Id.ToString("D"),
                string.IsNullOrWhiteSpace(agent.Name) ? "Unnamed agent" : agent.Name.Trim(),
                Environment.MachineName,
                string.IsNullOrWhiteSpace(repositoryRoot) ? owner.workspaceRoot : repositoryRoot.Trim(),
                branchName?.Trim() ?? string.Empty,
                agent.Id.ToString("D"));
        }

        private async Task<ProjectStructureResolvedScope> ResolveScopeAsync(
            AgentDefinition agent,
            ProjectStructureAccessState accessState,
            ProjectStructureScopeInput scope,
            bool requireWrite,
            CancellationToken cancellationToken)
        {
            return scope.ScopeKind switch
            {
                ProjectStructureLeaseScopeKind.Project when scope.ProjectId.HasValue => ResolveProjectScope(accessState, scope.ProjectId.Value, requireWrite),
                ProjectStructureLeaseScopeKind.ProjectNode when !string.IsNullOrWhiteSpace(scope.NodeId) => await ResolveProjectNodeScopeAsync(accessState, scope.NodeId.Trim(), requireWrite, cancellationToken),
                ProjectStructureLeaseScopeKind.RepoBranch => await ResolveRepoBranchScopeAsync(agent, accessState, scope.RepositoryRoot, scope.BranchName, requireWrite, cancellationToken),
                _ => throw new ProjectStructureAgentException(400, "InvalidScope", $"Scope '{scope.ScopeKind}' requires its typed identifier.")
            };
        }

        private static ProjectStructureResolvedScope ResolveProjectScope(
            ProjectStructureAccessState accessState,
            Guid projectId,
            bool requireWrite)
        {
            if (requireWrite)
            {
                EnsureProjectWriteAllowed(accessState, projectId);
            }
            else
            {
                EnsureProjectReadAllowed(accessState, projectId);
            }

            return new ProjectStructureResolvedScope(
                ProjectStructureLeaseScopeKind.Project,
                projectId.ToString("D"),
                projectId,
                string.Empty,
                null);
        }

        private async Task<ProjectStructureResolvedScope> ResolveProjectNodeScopeAsync(
            ProjectStructureAccessState accessState,
            string nodeId,
            bool requireWrite,
            CancellationToken cancellationToken)
        {
            if (requireWrite)
            {
                EnsureWriteAllowed(accessState);
            }
            else
            {
                EnsureReadAllowed(accessState);
            }

            foreach (var projectId in accessState.AllowedProjectIds)
            {
                try
                {
                    var structure = await agentService.GetStructureAsync(
                        projectId,
                        new ProjectStructureReadRequest(NodeIds: [nodeId], Take: 1),
                        cancellationToken);
                    if (structure.Nodes.Count > 0)
                    {
                        return new ProjectStructureResolvedScope(
                            ProjectStructureLeaseScopeKind.ProjectNode,
                            nodeId,
                            projectId,
                            string.Empty,
                            null);
                    }
                }
                catch (ProjectStructureAgentException exception) when (string.Equals(exception.ErrorCode, "ProjectNotFound", StringComparison.Ordinal))
                {
                    continue;
                }
            }

            throw new ProjectStructureAgentException(
                403,
                "ProjectStructureProjectDenied",
                $"Node '{nodeId}' is outside the agent's allowed project-structure scope.");
        }

        private async Task<ProjectStructureResolvedScope> ResolveRepoBranchScopeAsync(
            AgentDefinition agent,
            ProjectStructureAccessState accessState,
            string? repositoryRoot,
            string? branchName,
            bool requireWrite,
            CancellationToken cancellationToken)
        {
            if (requireWrite)
            {
                EnsureWriteAllowed(accessState);
            }
            else
            {
                EnsureReadAllowed(accessState);
            }

            var resolvedRepositoryRoot = string.IsNullOrWhiteSpace(repositoryRoot)
                ? owner.workspaceRoot
                : owner.ResolvePathFromWorkspace(repositoryRoot, false);
            var resolvedBranchName = string.IsNullOrWhiteSpace(branchName)
                ? await ResolveCurrentBranchNameAsync(cancellationToken)
                : branchName.Trim();
            if (string.IsNullOrWhiteSpace(resolvedBranchName))
            {
                throw new ProjectStructureAgentException(400, "InvalidScope", "Repo-branch scope requires a repository root and branch name.");
            }

            return new ProjectStructureResolvedScope(
                ProjectStructureLeaseScopeKind.RepoBranch,
                BuildRepoBranchScopeKey(resolvedRepositoryRoot, resolvedBranchName),
                null,
                resolvedBranchName,
                resolvedRepositoryRoot);
        }

        private async Task<string> ResolveCurrentBranchNameAsync(CancellationToken cancellationToken)
        {
            if (!string.IsNullOrWhiteSpace(currentBranchName))
            {
                return currentBranchName;
            }

            var result = await workspaceCommandExecutionService.GitStatus(true, null, 30);
            if (!result.Succeeded || string.IsNullOrWhiteSpace(result.StdoutPreview))
            {
                return string.Empty;
            }

            var branchLine = result.StdoutPreview
                .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault(line => line.StartsWith("## ", StringComparison.Ordinal));
            if (string.IsNullOrWhiteSpace(branchLine))
            {
                return string.Empty;
            }

            var branchText = branchLine[3..].Trim();
            var splitIndex = branchText.IndexOf("...", StringComparison.Ordinal);
            if (splitIndex >= 0)
            {
                branchText = branchText[..splitIndex];
            }

            splitIndex = branchText.IndexOf(' ');
            if (splitIndex >= 0)
            {
                branchText = branchText[..splitIndex];
            }

            currentBranchName = branchText.Trim();
            return currentBranchName;
        }

        private static string BuildRepoBranchScopeKey(string repositoryRoot, string branchName)
        {
            return $"repo-branch:{repositoryRoot}:{branchName}";
        }

        private static void EnsureReadAllowed(ProjectStructureAccessState accessState)
        {
            if (accessState.CanRead)
            {
                return;
            }

            throw new ProjectStructureAgentException(
                403,
                "ProjectStructureReadDenied",
                "This agent is not allowed to read project structure. Enable read access in the agent settings.");
        }

        private static void EnsureWriteAllowed(ProjectStructureAccessState accessState)
        {
            if (accessState.CanWrite)
            {
                return;
            }

            throw new ProjectStructureAgentException(
                403,
                "ProjectStructureWriteDenied",
                "This agent is not allowed to write project structure. Enable write access in the agent settings.");
        }

        private static void EnsureProjectReadAllowed(ProjectStructureAccessState accessState, Guid projectId)
        {
            EnsureReadAllowed(accessState);
            EnsureProjectAllowed(accessState, projectId);
        }

        private static void EnsureProjectWriteAllowed(ProjectStructureAccessState accessState, Guid projectId)
        {
            EnsureWriteAllowed(accessState);
            EnsureProjectAllowed(accessState, projectId);
        }

        private static void EnsureProjectAllowed(ProjectStructureAccessState accessState, Guid projectId)
        {
            if (accessState.AllowedProjectIds.Contains(projectId))
            {
                return;
            }

            throw new ProjectStructureAgentException(
                403,
                "ProjectStructureProjectDenied",
                $"Project '{projectId:D}' is outside the agent's allowed project-structure scope.");
        }

        private static ProjectManagementKnowledgeCategory MapGuidanceCategory(ProjectManagementGuidanceCategory category)
        {
            return (ProjectManagementKnowledgeCategory)(int)category;
        }

        private static ProjectManagementGuidanceCategory MapKnowledgeCategory(ProjectManagementKnowledgeCategory category)
        {
            return (ProjectManagementGuidanceCategory)(int)category;
        }

        private static IReadOnlyList<string> ExtractWarnings<T>(T response)
        {
            return response switch
            {
                ProjectStructureReadToolData readResponse => readResponse.Warnings,
                ProjectStructureChecklistResponse checklistResponse => checklistResponse.Warnings,
                ProjectStructureDependencyResponse dependencyResponse => dependencyResponse.Warnings,
                ProjectStructureImportResult importResult => importResult.Warnings,
                _ => []
            };
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

    private sealed class ProjectStructureAccessState
    {
        public ProjectStructureAccessState(AgentProjectStructureAccessSettings settings)
        {
            var normalized = AgentProjectStructureAccessMetadata.Normalize(settings);
            CanRead = normalized.CanRead;
            CanWrite = normalized.CanWrite;
            AllowedProjectIds = normalized.AllowedProjectIds.ToHashSet();
        }

        public bool CanRead { get; }

        public bool CanWrite { get; }

        public HashSet<Guid> AllowedProjectIds { get; }
    }

    private sealed record ProjectStructureResolvedScope(
        ProjectStructureLeaseScopeKind ScopeKind,
        string ScopeKey,
        Guid? ProjectId,
        string BranchName,
        string? RepositoryRoot);
}

public sealed record OperationAck(bool Ok);

public sealed record ProjectStructureScopeInput(
    ProjectStructureLeaseScopeKind ScopeKind,
    Guid? ProjectId = null,
    string? NodeId = null,
    string? RepositoryRoot = null,
    string? BranchName = null);

public sealed record ProjectStructureCompactNode(
    string Id,
    string? ParentId,
    ProjectObjectType ObjectType,
    string ObjectSubtype,
    string Title,
    string Subtitle,
    string Status,
    string Route,
    int EffectivePriority,
    string ProgressMode,
    int ProgressPercent,
    string? Notes = null,
    string? MetadataJson = null,
    string? MediaOriginalFileName = null,
    string? MediaRelativePath = null,
    string? MediaContentType = null,
    double? X = null,
    double? Y = null,
    int? DurationSeconds = null);

public sealed record ProjectStructureReadToolData(
    Guid ProjectId,
    string ProjectName,
    IReadOnlyList<ProjectStructureCompactNode> Nodes,
    IReadOnlyList<ProjectStructureLinkSummary> Links,
    IReadOnlyList<string> Warnings);
