using System.Diagnostics;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Tooling;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workspace;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.AI;

namespace CanDoItAll.Modules.Workbench;

public sealed class ProjectStructureAgentRuntimeToolProvider : IAgentRuntimeToolProvider
{
    private const int ProviderOrder = 900;

    private readonly ProjectStructureToolBuilder toolBuilder;

    public ProjectStructureAgentRuntimeToolProvider(
        ProjectStructureAgentService agentService,
        ProjectStructureLeaseService leaseService,
        ProjectStructureAnalyticsService analyticsService,
        ProjectManagementKnowledgeService knowledgeService,
        IWorkspacePathResolutionService workspacePaths)
    {
        ArgumentNullException.ThrowIfNull(agentService);
        ArgumentNullException.ThrowIfNull(leaseService);
        ArgumentNullException.ThrowIfNull(analyticsService);
        ArgumentNullException.ThrowIfNull(knowledgeService);
        ArgumentNullException.ThrowIfNull(workspacePaths);

        var workspaceRoot = workspacePaths.ResolveDirectoryPath(".", allowMissing: false).FullPath;
        var workspaceCommandExecutionService = new WorkspaceCommandExecutionService(
            workspaceRoot,
            new LocalWorkspaceProcessHost());

        toolBuilder = new ProjectStructureToolBuilder(
            agentService,
            leaseService,
            analyticsService,
            knowledgeService,
            workspaceCommandExecutionService,
            workspacePaths,
            workspaceRoot);
    }

    public int Order => ProviderOrder;

    public AgentRuntimeToolProviderDescriptor Descriptor { get; } = new(
        "project-structure.runtime-tools",
        "Project structure runtime tools",
        "Provides project-structure read/write, lease, analytics, import, and guidance tools backed by Workbench services.",
        ["project-structure", "workbench", "projects"],
        [
            AgentRuntimeToolProviderPurpose.InteractiveChat,
            AgentRuntimeToolProviderPurpose.GovernedProcessAutomation,
            AgentRuntimeToolProviderPurpose.AutoApprovedNonInteractive
        ]);

    public ValueTask<IReadOnlyList<AITool>> CreateToolsAsync(
        AgentRuntimeToolProviderContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        return ValueTask.FromResult(toolBuilder.CreateTools(context));
    }

    private sealed class ProjectStructureToolBuilder(
        ProjectStructureAgentService agentService,
        ProjectStructureLeaseService leaseService,
        ProjectStructureAnalyticsService analyticsService,
        ProjectManagementKnowledgeService knowledgeService,
        IWorkspaceCommandExecutionService workspaceCommandExecutionService,
        IWorkspacePathResolutionService workspacePaths,
        string workspaceRoot)
    {
        private readonly ProjectStructureAgentService agentService = agentService;
        private readonly ProjectStructureLeaseService leaseService = leaseService;
        private readonly ProjectStructureAnalyticsService analyticsService = analyticsService;
        private readonly ProjectManagementKnowledgeService knowledgeService = knowledgeService;
        private readonly IWorkspaceCommandExecutionService workspaceCommandExecutionService = workspaceCommandExecutionService;
        private readonly IWorkspacePathResolutionService workspacePaths = workspacePaths;
        private readonly string workspaceRoot = workspaceRoot;
        private string? currentBranchName;

        public IReadOnlyList<AITool> CreateTools(AgentRuntimeToolProviderContext context)
        {
            var agent = context.Agent;
            var accessSettings = AgentProjectStructureAccessMetadata.Read(agent.ConfigurationJson);
            var accessState = new ProjectStructureAccessState(
                accessSettings,
                ResolveScopedProcessAccess(context));

            return
            [
                AIFunctionFactory.Create(
                    (CancellationToken cancellationToken = default) => ProjectStructureProjectsListAsync(agent, accessState, cancellationToken),
                    "project_structure_projects_list",
                    "Lists the CanDoItAll projects that this internal agent is allowed to access."),
                AIFunctionFactory.Create(
                    (ProjectStructureProjectSaveRequest request, int? estimatedMinutes = null, CancellationToken cancellationToken = default) => ProjectStructureProjectCreateAsync(agent, accessState, request, estimatedMinutes, cancellationToken),
                    "project_structure_project_create",
                    "Creates a new CanDoItAll project through the internal workspace project-structure service."),
                AIFunctionFactory.Create(
                    (Guid projectId, ProjectStructureProjectSaveRequest request, int? estimatedMinutes = null, CancellationToken cancellationToken = default) => ProjectStructureProjectUpdateAsync(agent, accessState, projectId, request, estimatedMinutes, cancellationToken),
                    "project_structure_project_update",
                    "Updates an existing CanDoItAll project through the internal workspace project-structure service."),
                AIFunctionFactory.Create(
                    (Guid projectId, CancellationToken cancellationToken = default) => ProjectStructureHierarchyGetAsync(agent, accessState, projectId, cancellationToken),
                    "project_structure_hierarchy_get",
                    "Reads the project and subproject hierarchy for a specific project."),
                AIFunctionFactory.Create(
                    (Guid parentProjectId, ProjectStructureSubprojectChangeRequest request, int? estimatedMinutes = null, CancellationToken cancellationToken = default) => ProjectStructureSubprojectLinkAsync(agent, accessState, parentProjectId, request, estimatedMinutes, cancellationToken),
                    "project_structure_subproject_link",
                    "Adds or reconnects a subproject under a parent project."),
                AIFunctionFactory.Create(
                    (Guid projectId, ProjectStructureNodesToSubprojectInput request, int? estimatedMinutes = null, CancellationToken cancellationToken = default) => ProjectStructureNodesToNewSubprojectAsync(agent, accessState, projectId, request, estimatedMinutes, cancellationToken),
                    "project_structure_nodes_to_new_subproject",
                    "Creates a new subproject under the opened project and moves the supplied node ids, optionally with descendants, into that subproject in one operation. Use this for prompts like 'take selected nodes and move them to their own new subproject named XYZ'. If the contextual prompt lists selected node ids, pass those exact ids as nodeIds."),
                AIFunctionFactory.Create(
                    (Guid projectId, ProjectStructureReadRequest? request = null, CancellationToken cancellationToken = default) => ProjectStructureReadAsync(agent, accessState, projectId, request, cancellationToken),
                    "project_structure_read",
                    "Reads a filtered project structure with compact node payloads by default. Inspect node.actionCapabilities for runtime run actions (runtime:open/runtime:admin), local File Explorer actions (open-local), and IPFS new-tab actions (open-new-tab)."),
                AIFunctionFactory.Create(
                    (CancellationToken cancellationToken = default) => ProjectStructureNodeCatalogAsync(agent, accessState, cancellationToken),
                    "project_structure_node_catalog",
                    "Returns the canonical project-structure node catalog shared with the UI, including all creatable node actions, objectType/objectSubtype pairs, aliases, required fields, and dependency-link guidance. Call this before creating or reclassifying unfamiliar nodes."),
                AIFunctionFactory.Create(
                    (Guid projectId, ProjectStructureChecklistRequest? request = null, CancellationToken cancellationToken = default) => ProjectStructureChecklistAsync(agent, accessState, projectId, request, cancellationToken),
                    "project_structure_checklist",
                    "Returns unfinished project-structure items with prerequisite context and effective priority propagation."),
                AIFunctionFactory.Create(
                    (Guid projectId, ProjectStructureDependencyQueryRequest? request = null, CancellationToken cancellationToken = default) => ProjectStructureDependenciesQueryAsync(agent, accessState, projectId, request, cancellationToken),
                    "project_structure_dependencies_query",
                    "Returns dependency readiness, prerequisite chains, dependents, and effective durations."),
                AIFunctionFactory.Create(
                    (Guid projectId, ProjectStructureLinkInput request, CancellationToken cancellationToken = default) => ProjectStructureDependencyLinkAsync(agent, accessState, projectId, request, cancellationToken),
                    "project_structure_dependency_link",
                    "Creates a DependsOn dependency link in the project structure. The source node depends on the target node, so use this when task ordering or Gantt scheduling requires a prerequisite."),
                AIFunctionFactory.Create(
                    (Guid projectId, ProjectStructureLinkInput request, CancellationToken cancellationToken = default) => ProjectStructureDependencyUnlinkAsync(agent, accessState, projectId, request, cancellationToken),
                    "project_structure_dependency_unlink",
                    "Removes a DependsOn dependency link from the project structure. The source node previously depended on the target node."),
                AIFunctionFactory.Create(
                    (Guid projectId, ProjectStructureNodeCreateInput request, int? estimatedMinutes = null, CancellationToken cancellationToken = default) => ProjectStructureNodeCreateAsync(agent, accessState, projectId, request, estimatedMinutes, cancellationToken),
                    "project_structure_node_create",
                    "Creates a new project-structure node through the internal workspace service. For work task nodes, use objectType WorkItem and objectSubtype task. For typed block variants, keep objectType as ProjectBlock and set objectSubtype to a lowercase key such as feature, architecture, implementation, testing, delivery, research, risk, deployment, operations, repos, or dockers. Runnable commands must not be ProjectBlock delivery nodes: use Script for shell/test/build commands, Environment for language runtimes such as dotnet-runtime or python, or Infrastructure for container/runtime commands, and include the matching runtime metadata. When adding Mermaid diagrams, always create a File asset node with objectType File, objectSubtype mermaid, and Mermaid source in notes. Other generated files should also use objectType File with an appropriate file subtype, not a ProjectBlock. Every created node needs parentNodeKey: use project:{projectId} for top-level nodes or an existing parent node id."),
                AIFunctionFactory.Create(
                    (Guid projectId, string nodeId, ProjectStructureNodeEditInput request, int? estimatedMinutes = null, CancellationToken cancellationToken = default) => ProjectStructureNodeUpdateAsync(agent, accessState, projectId, nodeId, request, estimatedMinutes, cancellationToken),
                    "project_structure_node_update",
                    "Updates an existing project-structure node, including optional title, notes, timing, metadata, and requested type or subtype reclassification. Typed blocks must use objectType ProjectBlock plus lowercase objectSubtype values like feature, architecture, implementation, testing, delivery, and deployment. Do not invent enum names like FeatureBlock. Runnable commands must be reclassified to Script, Environment, or Infrastructure with matching runtime metadata instead of remaining ProjectBlock delivery nodes. Mermaid diagrams must remain File asset nodes with objectSubtype mermaid and Mermaid source in notes; other generated files should remain File nodes with file subtypes."),
                AIFunctionFactory.Create(
                    (Guid projectId, ProjectStructureNodeMoveInput request, int? estimatedMinutes = null, CancellationToken cancellationToken = default) => ProjectStructureNodeMoveAsync(agent, accessState, projectId, request, estimatedMinutes, cancellationToken),
                    "project_structure_node_move",
                    "Moves an existing project-structure node to exact canvas coordinates. Use this when recomposition still leaves overlap, crowding, or unreadable spacing."),
                AIFunctionFactory.Create(
                    (Guid projectId, ProjectStructureNodeRecomposeInput request, int? estimatedMinutes = null, CancellationToken cancellationToken = default) => ProjectStructureNodeRecomposeAsync(agent, accessState, projectId, request, estimatedMinutes, cancellationToken),
                    "project_structure_node_recompose",
                    "Redistributes a selected branch after imports or manual edits so the project mindmap opens in a readable layout."),
                AIFunctionFactory.Create(
                    (Guid projectId, ProjectStructureNodeReparentInput request, int? estimatedMinutes = null, CancellationToken cancellationToken = default) => ProjectStructureNodeReparentAsync(agent, accessState, projectId, request, estimatedMinutes, cancellationToken),
                    "project_structure_node_reparent",
                    "Reconnects an existing project-structure node under a new logical parent node or back to the project root."),
                AIFunctionFactory.Create(
                    (Guid projectId, ProjectStructureApprovalRequestCreateInput request, CancellationToken cancellationToken = default) => ProjectStructureApprovalRequestAsync(agent, accessState, projectId, request, cancellationToken),
                    "project_structure_approval_request",
                    "Records an approval-request node in the project structure so blocked work is written back into the graph."),
                AIFunctionFactory.Create(
                    (Guid projectId, ProjectStructureAssetCreateInput request, int? estimatedMinutes = null, CancellationToken cancellationToken = default) => ProjectStructureAssetCreateAsync(agent, accessState, projectId, request, estimatedMinutes, cancellationToken),
                    "project_structure_asset_create",
                    "Creates a managed File, ImageAsset, or VideoAsset node through the internal project-structure asset pipeline. Use this for generated screenshots, downloaded PDFs, and binary media instead of writing loose files into project notes. Provide media base64 data, sourceWorkspacePath for a file inside the managed workspace, or sourceUrl for a public http/https file that should be downloaded and stored as a managed asset."),
                AIFunctionFactory.Create(
                    (Guid projectId, string nodeId, CancellationToken cancellationToken = default) => ProjectStructureAssetGetAsync(agent, accessState, projectId, nodeId, cancellationToken),
                    "project_structure_asset_get",
                    "Returns readonly metadata for an existing managed asset node."),
                AIFunctionFactory.Create(
                    (Guid projectId, string nodeId, ProjectStructureAssetRevisionRequest request, int? estimatedMinutes = null, CancellationToken cancellationToken = default) => ProjectStructureAssetCreateRevisionAsync(agent, accessState, projectId, nodeId, request, estimatedMinutes, cancellationToken),
                    "project_structure_asset_create_revision",
                    "Creates a new revision asset node under an existing asset node instead of overwriting the original asset."),
                AIFunctionFactory.Create(
                    (ProjectStructureImportRequest request, int? estimatedMinutes = null, CancellationToken cancellationToken = default) => ProjectStructureImportAsync(agent, accessState, request, estimatedMinutes, cancellationToken),
                    "project_structure_import",
                    "Imports Mermaid, DOCX outline, XMind, or JSON outline content into the central project structure."),
                AIFunctionFactory.Create(
                    (ProjectManagementGuidanceQueryRequest? request = null, CancellationToken cancellationToken = default) => ProjectStructureKnowledgeQueryAsync(agent, accessState, request, cancellationToken),
                    "project_structure_knowledge_query",
                    "Queries project-management guidance that supports planning, reporting, approval, estimation, and mission discussions."),
                AIFunctionFactory.Create(
                    (ProjectStructureAnalyticsQueryRequest? request = null, CancellationToken cancellationToken = default) => ProjectStructureAnalyticsQueryAsync(agent, accessState, request, cancellationToken),
                    "project_structure_analytics_query",
                    "Queries project-structure operation analytics so validation and post-implementation review can inspect what agents actually did."),
                AIFunctionFactory.Create(
                    (Guid projectId, string reason, int durationMinutes, CancellationToken cancellationToken = default) => ProjectStructureProjectLeaseAcquireAsync(agent, accessState, projectId, reason, durationMinutes, cancellationToken),
                    "project_structure_project_lease_acquire",
                    "Acquires or renews a project-scoped lease so concurrent agents do not mutate the same project at the same time."),
                AIFunctionFactory.Create(
                    (string reason, string? repositoryRoot = null, string? branchName = null, int durationMinutes = 60, CancellationToken cancellationToken = default) => ProjectStructureRepoBranchLeaseAcquireAsync(agent, accessState, reason, repositoryRoot, branchName, durationMinutes, cancellationToken),
                    "project_structure_repo_branch_lease_acquire",
                    "Acquires or renews a repo-branch lease so separate agents do not collide on the same branch."),
                AIFunctionFactory.Create(
                    (ProjectStructureScopeInput scope, CancellationToken cancellationToken = default) => ProjectStructureLeaseGetAsync(agent, accessState, scope, cancellationToken),
                    "project_structure_lease_get",
                    "Gets the current active project, node, or repo-branch lease for the supplied scope."),
                AIFunctionFactory.Create(
                    (ProjectStructureScopeInput scope, string leaseToken, CancellationToken cancellationToken = default) => ProjectStructureLeaseReleaseAsync(agent, accessState, scope, leaseToken, cancellationToken),
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
                    var visibleProjects = accessState.AllowAllProjects
                        ? projects
                        : projects.Where(project => accessState.AllowedProjectIds.Contains(project.Id)).ToList();
                    return visibleProjects
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

        private Task<ProjectStructureNodesToSubprojectResult> ProjectStructureNodesToNewSubprojectAsync(
            AgentDefinition agent,
            ProjectStructureAccessState accessState,
            Guid projectId,
            ProjectStructureNodesToSubprojectInput request,
            int? estimatedMinutes,
            CancellationToken cancellationToken)
        {
            return ExecuteAsync(
                agent,
                "structure.nodes-move-to-new-subproject",
                projectId,
                null,
                ProjectStructureLeaseScopeKind.Project,
                projectId.ToString("D"),
                request,
                async cancellationToken =>
                {
                    EnsureProjectWriteAllowed(accessState, projectId);
                    return await agentService.MoveNodesToNewSubprojectAsync(projectId, request, BuildAgentContext(agent), cancellationToken);
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

        private Task<ProjectStructureNodeCatalogResponse> ProjectStructureNodeCatalogAsync(
            AgentDefinition agent,
            ProjectStructureAccessState accessState,
            CancellationToken cancellationToken)
        {
            return ExecuteAsync(
                agent,
                "structure.node-catalog",
                null,
                null,
                null,
                null,
                null,
                async cancellationToken =>
                {
                    EnsureReadAllowed(accessState);
                    return await agentService.GetNodeCatalogAsync(cancellationToken);
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

        private Task<ProjectStructureLinkChangeResult> ProjectStructureDependencyLinkAsync(
            AgentDefinition agent,
            ProjectStructureAccessState accessState,
            Guid projectId,
            ProjectStructureLinkInput request,
            CancellationToken cancellationToken)
        {
            return ExecuteAsync(
                agent,
                "dependencies.link",
                projectId,
                request.SourceNodeId,
                ProjectStructureLeaseScopeKind.Project,
                projectId.ToString("D"),
                request,
                async cancellationToken =>
                {
                    EnsureProjectWriteAllowed(accessState, projectId);
                    return await agentService.LinkNodesAsync(
                        projectId,
                        request with { Kind = ProjectObjectLinkKind.DependsOn },
                        BuildAgentContext(agent),
                        cancellationToken);
                },
                cancellationToken);
        }

        private Task<ProjectStructureLinkChangeResult> ProjectStructureDependencyUnlinkAsync(
            AgentDefinition agent,
            ProjectStructureAccessState accessState,
            Guid projectId,
            ProjectStructureLinkInput request,
            CancellationToken cancellationToken)
        {
            return ExecuteAsync(
                agent,
                "dependencies.unlink",
                projectId,
                request.SourceNodeId,
                ProjectStructureLeaseScopeKind.Project,
                projectId.ToString("D"),
                request,
                async cancellationToken =>
                {
                    EnsureProjectWriteAllowed(accessState, projectId);
                    return await agentService.UnlinkNodesAsync(
                        projectId,
                        request with { Kind = ProjectObjectLinkKind.DependsOn },
                        BuildAgentContext(agent),
                        cancellationToken);
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

        private Task<OperationAck> ProjectStructureNodeMoveAsync(
            AgentDefinition agent,
            ProjectStructureAccessState accessState,
            Guid projectId,
            ProjectStructureNodeMoveInput request,
            int? estimatedMinutes,
            CancellationToken cancellationToken)
        {
            return ExecuteAsync(
                agent,
                "structure.node-move",
                projectId,
                request.NodeId,
                ProjectStructureLeaseScopeKind.Project,
                projectId.ToString("D"),
                request,
                async cancellationToken =>
                {
                    EnsureProjectWriteAllowed(accessState, projectId);
                    await agentService.MoveNodeAsync(projectId, request, BuildAgentContext(agent), cancellationToken);
                    return new OperationAck(true);
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

        private Task<ProjectStructureNodeSummary> ProjectStructureAssetCreateAsync(
            AgentDefinition agent,
            ProjectStructureAccessState accessState,
            Guid projectId,
            ProjectStructureAssetCreateInput request,
            int? estimatedMinutes,
            CancellationToken cancellationToken)
        {
            return ExecuteAsync(
                agent,
                "assets.create",
                projectId,
                null,
                ProjectStructureLeaseScopeKind.Project,
                projectId.ToString("D"),
                request,
                async cancellationToken =>
                {
                    EnsureProjectWriteAllowed(accessState, projectId);
                    return await agentService.CreateAssetAsync(projectId, request, BuildAgentContext(agent), cancellationToken);
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

                    if (!accessState.AllowAllProjects &&
                        accessState.AllowedProjectIds.Count == 0)
                    {
                        return response with
                        {
                            Entries = response.Entries
                                .Where(entry => !entry.ProjectId.HasValue)
                                .ToList()
                        };
                    }

                    return accessState.AllowAllProjects
                        ? response
                        : response with
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
                ? workspaceRoot
                : ResolveRepositoryRoot(repositoryRoot);
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
                string.IsNullOrWhiteSpace(repositoryRoot) ? workspaceRoot : repositoryRoot.Trim(),
                branchName?.Trim() ?? string.Empty,
                agent.Id.ToString("D"));
        }

        private string ResolveRepositoryRoot(string repositoryRoot)
        {
            return workspacePaths.ResolveDirectoryPath(repositoryRoot, allowMissing: true).FullPath;
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
                EnsureAnyWriteAllowed(accessState);
            }
            else
            {
                EnsureReadAllowed(accessState);
            }

            var candidateProjectIds = accessState.AllowAllProjects
                ? (await agentService.ListProjectsAsync(cancellationToken))
                    .Select(project => project.Id)
                    .ToList()
                : accessState.AllowedProjectIds.ToList();

            foreach (var projectId in candidateProjectIds)
            {
                try
                {
                    var structure = await agentService.GetStructureAsync(
                        projectId,
                        new ProjectStructureReadRequest(NodeIds: [nodeId], Take: 1),
                        cancellationToken);
                    if (structure.Nodes.Count > 0)
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
                ? workspaceRoot
                : ResolveRepositoryRoot(repositoryRoot);
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
            if (accessState.CanWriteUnscoped)
            {
                return;
            }

            throw new ProjectStructureAgentException(
                403,
                "ProjectStructureWriteDenied",
                "This agent is not allowed to write project structure. Enable write access in the agent settings.");
        }

        private static void EnsureAnyWriteAllowed(ProjectStructureAccessState accessState)
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
            EnsureAnyWriteAllowed(accessState);
            EnsureProjectAllowed(accessState, projectId);
        }

        private static void EnsureProjectAllowed(ProjectStructureAccessState accessState, Guid projectId)
        {
            if (accessState.AllowAllProjects ||
                accessState.AllowedProjectIds.Contains(projectId))
            {
                return;
            }

            throw new ProjectStructureAgentException(
                403,
                "ProjectStructureProjectDenied",
                $"Project '{projectId:D}' is outside the agent's allowed project-structure scope.");
        }

        private static ProjectStructureScopedProcessAccess? ResolveScopedProcessAccess(AgentRuntimeToolProviderContext context)
        {
            if (context.Purpose != AgentRuntimeToolProviderPurpose.GovernedProcessAutomation ||
                WorkspaceExecutionAuditContext.Current is not { } auditScope ||
                string.IsNullOrWhiteSpace(auditScope.ProcessRunId) ||
                string.IsNullOrWhiteSpace(auditScope.ProcessStepId) ||
                !TryResolveProjectScopeId(context.Tags, out var projectId))
            {
                return null;
            }

            var canRead = ContainsProcessOperation(
                auditScope.ProcessStepAllowedOperations,
                ProcessOperationContractNames.ReadProjectStructure);
            var canWrite = ContainsProcessOperation(
                auditScope.ProcessStepAllowedOperations,
                ProcessOperationContractNames.ExecuteExternalAction);
            return !canRead && !canWrite
                ? null
                : new ProjectStructureScopedProcessAccess(projectId, canRead, canWrite);
        }

        private static bool TryResolveProjectScopeId(
            IReadOnlyDictionary<string, string> tags,
            out Guid projectId)
        {
            projectId = Guid.Empty;
            return tags.TryGetValue("workspaceScopeKind", out var scopeKind) &&
                   string.Equals(scopeKind, WorkspaceScopeKind.Project.ToString(), StringComparison.OrdinalIgnoreCase) &&
                   tags.TryGetValue("workspaceScopeKey", out var scopeKey) &&
                   Guid.TryParse(scopeKey, out projectId);
        }

        private static bool ContainsProcessOperation(
            IReadOnlyList<string> operations,
            string operationName)
            => operations.Any(operation => string.Equals(operation, operationName, StringComparison.OrdinalIgnoreCase));

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
                ProjectStructureNodesToSubprojectResult nodesToSubprojectResult => nodesToSubprojectResult.Warnings,
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
                node.DurationSeconds,
                node.ActionCapabilities);
        }
    }

    private sealed class ProjectStructureAccessState
    {
        public ProjectStructureAccessState(
            AgentProjectStructureAccessSettings settings,
            ProjectStructureScopedProcessAccess? scopedProcessAccess)
        {
            var normalized = AgentProjectStructureAccessMetadata.Normalize(settings);
            CanRead = normalized.CanRead || scopedProcessAccess?.CanRead == true;
            CanWrite = normalized.CanWrite || scopedProcessAccess?.CanWrite == true;
            CanWriteUnscoped = normalized.CanWrite;
            AllowAllProjects = normalized.AllowAllProjects;
            AllowedProjectIds = normalized.AllowedProjectIds.ToHashSet();
            if (scopedProcessAccess is not null)
            {
                AllowedProjectIds.Add(scopedProcessAccess.ProjectId);
            }
        }

        public bool CanRead { get; }

        public bool CanWrite { get; }

        public bool CanWriteUnscoped { get; }

        public bool AllowAllProjects { get; }

        public HashSet<Guid> AllowedProjectIds { get; }
    }

    private sealed record ProjectStructureScopedProcessAccess(
        Guid ProjectId,
        bool CanRead,
        bool CanWrite);

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
    int? DurationSeconds = null,
    ProjectStructureNodeActionCapabilities? ActionCapabilities = null);

public sealed record ProjectStructureReadToolData(
    Guid ProjectId,
    string ProjectName,
    IReadOnlyList<ProjectStructureCompactNode> Nodes,
    IReadOnlyList<ProjectStructureLinkSummary> Links,
    IReadOnlyList<string> Warnings);
