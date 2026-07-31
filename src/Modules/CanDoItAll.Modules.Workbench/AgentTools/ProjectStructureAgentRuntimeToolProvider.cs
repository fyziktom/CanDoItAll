using System.Diagnostics;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Tooling;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench.ProjectStructure;
using CanDoItAll.Modules.Workspace;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.AI;

namespace CanDoItAll.Modules.Workbench;

public sealed class ProjectStructureAgentRuntimeToolProvider : IAgentRuntimeToolProvider
{
    private const int ProviderOrder = 900;
    private const int GovernedProcessDefaultStructureReadTake = 80;
    private const int GovernedProcessMaxExplicitLeaseMinutes = 5;
    private const string ProjectStructureSourceKind = "project-structure";
    private const string ProjectsSourceKind = "projects";
    private const string ProjectStructurePlannedStatus = "Planned";
    private const string ProjectStructurePublishedStatus = "Published";

    private static readonly IReadOnlyList<string> GovernedProcessDefaultStructureReadStatuses =
    [
        ProjectStatus.Active.ToString(),
        ProjectStatus.Draft.ToString(),
        ProjectStructurePlannedStatus,
        ProjectStructurePublishedStatus
    ];

    private readonly ProjectStructureToolBuilder toolBuilder;

    public ProjectStructureAgentRuntimeToolProvider(
        ProjectStructureAgentService agentService,
        ProjectStructureLeaseService leaseService,
        ProjectStructureAnalyticsService analyticsService,
        ProjectPlanAnalyticsQueryService planAnalyticsService,
        ProjectStructureAgentAuthorizationService authorizationService,
        ProjectStructureTaskCreationService taskCreationService,
        ProjectStructureTaskDetailsService taskDetailsService,
        ProjectStructureTaskResourceAttachmentService taskResourceAttachmentService,
        ProjectManagementKnowledgeService knowledgeService,
        IWorkspacePathResolutionService workspacePaths,
        IDatabaseRuntimeState databaseRuntimeState,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(agentService);
        ArgumentNullException.ThrowIfNull(leaseService);
        ArgumentNullException.ThrowIfNull(analyticsService);
        ArgumentNullException.ThrowIfNull(planAnalyticsService);
        ArgumentNullException.ThrowIfNull(authorizationService);
        ArgumentNullException.ThrowIfNull(taskCreationService);
        ArgumentNullException.ThrowIfNull(taskDetailsService);
        ArgumentNullException.ThrowIfNull(taskResourceAttachmentService);
        ArgumentNullException.ThrowIfNull(knowledgeService);
        ArgumentNullException.ThrowIfNull(workspacePaths);
        ArgumentNullException.ThrowIfNull(databaseRuntimeState);
        ArgumentNullException.ThrowIfNull(timeProvider);

        var workspaceRoot = workspacePaths.ResolveDirectoryPath(".", allowMissing: false).FullPath;
        var workspaceCommandExecutionService = new WorkspaceCommandExecutionService(
            workspaceRoot,
            new LocalWorkspaceProcessHost());
        var projectCreationCoordinator = new ProjectStructureAgentProjectCreationCoordinator(
            authorizationService);

        toolBuilder = new ProjectStructureToolBuilder(
            agentService,
            leaseService,
            analyticsService,
            planAnalyticsService,
            authorizationService,
            projectCreationCoordinator,
            taskCreationService,
            taskDetailsService,
            taskResourceAttachmentService,
            knowledgeService,
            workspaceCommandExecutionService,
            workspacePaths,
            workspaceRoot,
            databaseRuntimeState,
            timeProvider);
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

        if (!ShouldAttachForContext(context.ContextIntent))
        {
            return ValueTask.FromResult<IReadOnlyList<AITool>>([]);
        }

        return ValueTask.FromResult(toolBuilder.CreateTools(context));
    }

    internal static bool ShouldAttachForContext(AgentRuntimeContextIntent contextIntent)
    {
        ArgumentNullException.ThrowIfNull(contextIntent);

        if (string.Equals(contextIntent.SourceKind, ProjectStructureSourceKind, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(contextIntent.SourceKind, ProjectsSourceKind, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return contextIntent.IsGovernedProcessStep &&
               contextIntent.AllowedOperations.Any(IsProjectStructureProcessOperation);
    }

    private static bool IsProjectStructureProcessOperation(string operation)
    {
        return string.Equals(operation, ProcessOperationContractNames.ReadProjectStructure, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(operation, ProcessOperationContractNames.StartProjectNodeProcess, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(operation, ProcessOperationContractNames.ExecuteExternalAction, StringComparison.OrdinalIgnoreCase);
    }

    internal static bool IsProjectAllowedForContext(
        AgentRuntimeToolProviderPurpose purpose,
        AgentRuntimeContextIntent contextIntent,
        bool allowAllProjects,
        IReadOnlySet<Guid> allowedProjectIds,
        IReadOnlySet<Guid> sessionCreatedProjectIds,
        Guid projectId)
    {
        ArgumentNullException.ThrowIfNull(contextIntent);
        ArgumentNullException.ThrowIfNull(allowedProjectIds);
        ArgumentNullException.ThrowIfNull(sessionCreatedProjectIds);

        if (purpose == AgentRuntimeToolProviderPurpose.InteractiveChat &&
            string.Equals(contextIntent.SourceKind, ProjectStructureSourceKind, StringComparison.OrdinalIgnoreCase) &&
            (!Guid.TryParse(contextIntent.SourceId, out var activeProjectId) ||
             activeProjectId == Guid.Empty ||
             activeProjectId != projectId && !sessionCreatedProjectIds.Contains(projectId)))
        {
            return false;
        }

        return allowAllProjects || allowedProjectIds.Contains(projectId);
    }

    internal static void EnsureProjectAllowedForContext(
        AgentRuntimeToolProviderPurpose purpose,
        AgentRuntimeContextIntent contextIntent,
        bool allowAllProjects,
        IReadOnlySet<Guid> allowedProjectIds,
        IReadOnlySet<Guid> sessionCreatedProjectIds,
        Guid projectId)
    {
        ArgumentNullException.ThrowIfNull(contextIntent);
        ArgumentNullException.ThrowIfNull(allowedProjectIds);
        ArgumentNullException.ThrowIfNull(sessionCreatedProjectIds);

        if (purpose == AgentRuntimeToolProviderPurpose.InteractiveChat &&
            string.Equals(contextIntent.SourceKind, ProjectStructureSourceKind, StringComparison.OrdinalIgnoreCase))
        {
            if (!Guid.TryParse(contextIntent.SourceId, out var activeProjectId) ||
                activeProjectId == Guid.Empty)
            {
                throw new ProjectStructureAgentException(
                    403,
                    "ProjectStructureContextProjectInvalid",
                    "The project-structure chat does not identify a valid active project. Reopen the chat from the intended project.");
            }

            if (activeProjectId != projectId && !sessionCreatedProjectIds.Contains(projectId))
            {
                throw new ProjectStructureAgentException(
                    403,
                    "ProjectStructureContextProjectDenied",
                    $"Project '{projectId:D}' is outside the active project-structure chat project '{activeProjectId:D}'.");
            }
        }

        if (allowAllProjects || allowedProjectIds.Contains(projectId))
        {
            return;
        }

        throw new ProjectStructureAgentException(
            403,
            "ProjectStructureProjectDenied",
            $"Project '{projectId:D}' is outside the agent's allowed project-structure scope.");
    }

    private sealed class ProjectStructureToolBuilder(
        ProjectStructureAgentService agentService,
        ProjectStructureLeaseService leaseService,
        ProjectStructureAnalyticsService analyticsService,
        ProjectPlanAnalyticsQueryService planAnalyticsService,
        ProjectStructureAgentAuthorizationService authorizationService,
        ProjectStructureAgentProjectCreationCoordinator projectCreationCoordinator,
        ProjectStructureTaskCreationService taskCreationService,
        ProjectStructureTaskDetailsService taskDetailsService,
        ProjectStructureTaskResourceAttachmentService taskResourceAttachmentService,
        ProjectManagementKnowledgeService knowledgeService,
        IWorkspaceCommandExecutionService workspaceCommandExecutionService,
        IWorkspacePathResolutionService workspacePaths,
        string workspaceRoot,
        IDatabaseRuntimeState databaseRuntimeState,
        TimeProvider timeProvider)
    {
        private readonly ProjectStructureAgentService agentService = agentService;
        private readonly ProjectStructureLeaseService leaseService = leaseService;
        private readonly ProjectStructureAnalyticsService analyticsService = analyticsService;
        private readonly ProjectPlanAnalyticsQueryService planAnalyticsService = planAnalyticsService;
        private readonly ProjectStructureAgentAuthorizationService authorizationService = authorizationService;
        private readonly ProjectStructureAgentProjectCreationCoordinator projectCreationCoordinator = projectCreationCoordinator;
        private readonly ProjectStructureTaskCreationService taskCreationService = taskCreationService;
        private readonly ProjectStructureTaskDetailsService taskDetailsService = taskDetailsService;
        private readonly ProjectStructureTaskResourceAttachmentService taskResourceAttachmentService =
            taskResourceAttachmentService;
        private readonly ProjectManagementKnowledgeService knowledgeService = knowledgeService;
        private readonly IWorkspaceCommandExecutionService workspaceCommandExecutionService = workspaceCommandExecutionService;
        private readonly IWorkspacePathResolutionService workspacePaths = workspacePaths;
        private readonly string workspaceRoot = workspaceRoot;
        private readonly IDatabaseRuntimeState databaseRuntimeState = databaseRuntimeState;
        private readonly TimeProvider timeProvider = timeProvider;
        private string? currentBranchName;

        public IReadOnlyList<AITool> CreateTools(AgentRuntimeToolProviderContext context)
        {
            var agent = context.Agent;
            var accessSettings = AgentProjectStructureAccessMetadata.Read(agent.ConfigurationJson);
            var accessState = new ProjectStructureAccessState(
                accessSettings,
                ResolveScopedProcessAccess(context),
                context.ContextIntent,
                context.Purpose,
                ProjectStructureInvocationSnapshotReadContext.Capture(context));
            if (!accessState.CanRead &&
                !accessState.CanWrite &&
                !accessState.CanCreateProjects &&
                !accessState.CanCreateSubprojects)
            {
                return [];
            }

            List<AITool> tools =
            [
                AIFunctionFactory.Create(
                    (CancellationToken cancellationToken = default) => ProjectStructureProjectsListAsync(agent, accessState, cancellationToken),
                    "project_structure_projects_list",
                    "Lists the CanDoItAll projects that this internal agent is allowed to access."),
                AIFunctionFactory.Create(
                    (Guid projectId, ProjectStructureProjectSaveRequest request, int? estimatedMinutes = null, CancellationToken cancellationToken = default) => ProjectStructureProjectUpdateAsync(agent, accessState, projectId, request, estimatedMinutes, cancellationToken),
                    "project_structure_project_update",
                    "Updates an existing CanDoItAll project through the internal workspace project-structure service."),
                AIFunctionFactory.Create(
                    (Guid projectId, CancellationToken cancellationToken = default) => ProjectStructureHierarchyGetAsync(agent, accessState, projectId, cancellationToken),
                    "project_structure_hierarchy_get",
                    "Reads the project and subproject hierarchy for a specific project."),
                AIFunctionFactory.Create(
                    (Guid projectId, ProjectStructureReadRequest? request = null, CancellationToken cancellationToken = default) => ProjectStructureReadAsync(agent, accessState, projectId, request, cancellationToken),
                    "project_structure_read",
                    "Reads a filtered project structure. ContextDefault is an explicit context policy: interactive project-structure chat uses the captured InvocationSnapshot without storage fallback; governed-process and non-project contexts use CanonicalCurrent. Request InvocationSnapshot explicitly only in eligible interactive project context. Use CanonicalCurrent explicitly for notes, metadata, assets, layout, routes, action capabilities, storage references, file contents, or any snapshot coverage miss. Canonical node.actionCapabilities describe runtime run actions (runtime:open/runtime:admin), local folder actions (open-local), and IPFS new-tab actions (open-new-tab)."),
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
                    "Creates a new non-task project-structure node through the internal workspace service. Canonical WorkItem/task nodes are rejected here and must use project_task_create so lifecycle, assignment, and authoritative pricing are applied. For typed block variants, keep objectType as ProjectBlock and set objectSubtype to a lowercase key such as feature, architecture, implementation, testing, delivery, research, risk, deployment, operations, repos, or dockers. Delivery target blocks should set metadata.projectBlock.outputRoot or metadata.projectBlock.targetRoot to the destination folder. Runnable commands must not be ProjectBlock delivery nodes: use Script for shell/test/build commands, Environment for language runtimes such as dotnet-runtime or python, or Infrastructure for container/runtime commands, and include the matching runtime metadata. When adding Mermaid diagrams, always create a File asset node with objectType File, objectSubtype mermaid, and Mermaid source in notes. Other generated files should also use objectType File with an appropriate file subtype, not a ProjectBlock. Every created node needs parentNodeKey: use project:{projectId} for top-level nodes or an existing parent node id."),
                AIFunctionFactory.Create(
                    (Guid projectId, string nodeId, ProjectStructureNodeEditInput request, int? estimatedMinutes = null, CancellationToken cancellationToken = default) => ProjectStructureNodeUpdateAsync(agent, accessState, projectId, nodeId, request, estimatedMinutes, cancellationToken),
                    "project_structure_node_update",
                    "Updates an existing non-task project-structure node, including optional title, notes, timing, metadata, and requested type or subtype reclassification. Canonical WorkItem/task nodes and reclassification into or out of that type are rejected here; use project_task_update. Typed blocks must use objectType ProjectBlock plus lowercase objectSubtype values like feature, architecture, implementation, testing, delivery, and deployment. Delivery target blocks should keep metadata.projectBlock.outputRoot or metadata.projectBlock.targetRoot when they define the destination folder. Do not invent enum names like FeatureBlock. Runnable commands must be reclassified to Script, Environment, or Infrastructure with matching runtime metadata instead of remaining ProjectBlock delivery nodes. Mermaid diagrams must remain File asset nodes with objectSubtype mermaid and Mermaid source in notes; other generated files should remain File nodes with file subtypes."),
                AIFunctionFactory.Create(
                    (Guid projectId, string nodeId, ProjectStructureNodeTypeInput request, int? estimatedMinutes = null, CancellationToken cancellationToken = default) => ProjectStructureNodeTypeUpdateAsync(agent, accessState, projectId, nodeId, request, estimatedMinutes, cancellationToken),
                    "project_structure_node_type_update",
                    "Updates only the objectType/objectSubtype classification for an existing non-task project-structure node while preserving its title, notes, timing, metadata, and duration. Canonical WorkItem/task nodes and reclassification into or out of that type must use the typed task path."),
                AIFunctionFactory.Create(
                    (Guid projectId, string nodeId, ProjectStructureNodeMetadataInput request, int? estimatedMinutes = null, CancellationToken cancellationToken = default) => ProjectStructureNodeMetadataUpdateAsync(agent, accessState, projectId, nodeId, request, estimatedMinutes, cancellationToken),
                    "project_structure_node_metadata_update",
                    "Updates a non-task node's metadata JSON and optional notes/status without changing its type or layout. Canonical WorkItem/task metadata must use project_task_update."),
                AIFunctionFactory.Create(
                    (Guid projectId, ProjectStructureStatusBatchInput request, int? estimatedMinutes = null, CancellationToken cancellationToken = default) => ProjectStructureNodesStatusUpdateAsync(agent, accessState, projectId, request, estimatedMinutes, cancellationToken),
                    "project_structure_nodes_status_update",
                    "Updates the status for multiple project-structure nodes in one governed mutation."),
                AIFunctionFactory.Create(
                    (Guid projectId, string nodeId, ProjectStructureStatusInput request, int? estimatedMinutes = null, CancellationToken cancellationToken = default) => ProjectStructureNodeStatusUpdateAsync(agent, accessState, projectId, nodeId, request, estimatedMinutes, cancellationToken),
                    "project_structure_node_status_update",
                    "Updates the status for one project-structure node."),
                AIFunctionFactory.Create(
                    (Guid projectId, ProjectStructureProgressBatchInput request, int? estimatedMinutes = null, CancellationToken cancellationToken = default) => ProjectStructureNodesProgressUpdateAsync(agent, accessState, projectId, request, estimatedMinutes, cancellationToken),
                    "project_structure_nodes_progress_update",
                    "Updates progress mode and percent for multiple project-structure nodes in one governed mutation."),
                AIFunctionFactory.Create(
                    (Guid projectId, string nodeId, ProjectStructureProgressInput request, int? estimatedMinutes = null, CancellationToken cancellationToken = default) => ProjectStructureNodeProgressUpdateAsync(agent, accessState, projectId, nodeId, request, estimatedMinutes, cancellationToken),
                    "project_structure_node_progress_update",
                    "Updates progress mode and percent for one project-structure node."),
                AIFunctionFactory.Create(
                    (Guid projectId, ProjectStructureMarkerBatchInput request, int? estimatedMinutes = null, CancellationToken cancellationToken = default) => ProjectStructureNodesMarkerUpdateAsync(agent, accessState, projectId, request, estimatedMinutes, cancellationToken),
                    "project_structure_nodes_marker_update",
                    "Replaces marker icon, tone, and label for multiple project-structure nodes in one governed mutation."),
                AIFunctionFactory.Create(
                    (Guid projectId, string nodeId, ProjectStructureMarkerInput request, int? estimatedMinutes = null, CancellationToken cancellationToken = default) => ProjectStructureNodeMarkerUpdateAsync(agent, accessState, projectId, nodeId, request, estimatedMinutes, cancellationToken),
                    "project_structure_node_marker_update",
                    "Changes a single node marker using replace, add, toggle, remove, or clear semantics."),
                AIFunctionFactory.Create(
                    (Guid projectId, ProjectStructurePriorityBatchInput request, int? estimatedMinutes = null, CancellationToken cancellationToken = default) => ProjectStructureNodesPriorityUpdateAsync(agent, accessState, projectId, request, estimatedMinutes, cancellationToken),
                    "project_structure_nodes_priority_update",
                    "Updates priority for multiple project-structure nodes in one governed mutation."),
                AIFunctionFactory.Create(
                    (Guid projectId, string nodeId, ProjectStructurePriorityInput request, int? estimatedMinutes = null, CancellationToken cancellationToken = default) => ProjectStructureNodePriorityUpdateAsync(agent, accessState, projectId, nodeId, request, estimatedMinutes, cancellationToken),
                    "project_structure_node_priority_update",
                    "Updates priority for one project-structure node."),
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
                    (Guid projectId, string nodeId, ProjectStructureSubtreeTransferInput request, int? estimatedMinutes = null, CancellationToken cancellationToken = default) => ProjectStructureNodeDescendantsToProjectMoveAsync(agent, accessState, projectId, nodeId, request, estimatedMinutes, cancellationToken),
                    "project_structure_node_descendants_to_project_move",
                    "Moves all descendants of a source node into an existing target project. Use project_structure_nodes_to_new_subproject when the target project should be created first."),
                AIFunctionFactory.Create(
                    (Guid projectId, string nodeId, ProjectStructureNodeCommandInput request, int? estimatedMinutes = null, CancellationToken cancellationToken = default) => ProjectStructureNodeCommandExecuteAsync(agent, accessState, projectId, nodeId, request, estimatedMinutes, cancellationToken),
                    "project_structure_node_command_execute",
                    "Executes a supported project-structure node command such as Open, Wizard, or Test and returns the resulting artifact reference."),
                AIFunctionFactory.Create(
                    (Guid projectId, string nodeId, ProjectStructureProcessDefinitionLinkInput request, int? estimatedMinutes = null, CancellationToken cancellationToken = default) => ProjectStructureNodeProcessDefinitionLinkAsync(agent, accessState, projectId, nodeId, request, estimatedMinutes, cancellationToken),
                    "project_structure_node_process_definition_link",
                    "Links a non-canonical project-structure node to a process definition using a generic Uses link. Canonical WorkItem/task nodes are rejected; use project_task_resource_attach with the task's exact current execution snapshot so the process attachment and authoritative expected pricing are committed together."),
                AIFunctionFactory.Create(
                    (Guid projectId, string nodeId, ProjectStructureProcessNodeStartInput request, int? estimatedMinutes = null, CancellationToken cancellationToken = default) => ProjectStructureNodeProcessStartAsync(agent, accessState, projectId, nodeId, request, estimatedMinutes, cancellationToken),
                    "project_structure_node_process_start",
                    "Starts or prepares the process linked to a project-structure node. This can create launch plans and optionally execute a process run when requested."),
                AIFunctionFactory.Create(
                    (ProjectStructureProcessSubprocessLaunchInput request, int? estimatedMinutes = null, CancellationToken cancellationToken = default) => ProjectStructureProcessSubprocessLaunchAsync(agent, accessState, request, estimatedMinutes, cancellationToken),
                    "project_structure_process_subprocess_launch",
                    "Starts the child process mapped by the current governed process step. This tool is available only inside governed process automation with ExecuteExternalAction and inherits the parent process project scope. Leave liveRunProfileKey null unless the parent process explicitly provides a valid CanDoItAll process live-run profile key; never copy branch names, session ids, process definition keys, or template names into liveRunProfileKey."),
                AIFunctionFactory.Create(
                    (Guid projectId, string nodeId, ProjectStructureWorkflowAddOptionsInput request, CancellationToken cancellationToken = default) => ProjectStructureNodeWorkflowAddOptionsAsync(agent, accessState, projectId, nodeId, request, cancellationToken),
                    "project_structure_node_workflow_add_options",
                    "Returns workflow definitions and input-preview options that can be added under a project-structure node."),
                AIFunctionFactory.Create(
                    (Guid projectId, string nodeId, ProjectStructureWorkflowNodeCreateInput request, int? estimatedMinutes = null, CancellationToken cancellationToken = default) => ProjectStructureNodeWorkflowDefinitionCreateAsync(agent, accessState, projectId, nodeId, request, estimatedMinutes, cancellationToken),
                    "project_structure_node_workflow_definition_create",
                    "Creates a workflow-definition node under the supplied project-structure parent node."),
                AIFunctionFactory.Create(
                    (Guid projectId, string nodeId, ProjectStructureWorkflowNodeStartInput request, int? estimatedMinutes = null, CancellationToken cancellationToken = default) => ProjectStructureNodeWorkflowStartAsync(agent, accessState, projectId, nodeId, request, estimatedMinutes, cancellationToken),
                    "project_structure_node_workflow_start",
                    "Starts the workflow represented by a project-structure workflow node and returns the initial run status."),
                AIFunctionFactory.Create(
                    (Guid projectId, string nodeId, CancellationToken cancellationToken = default) => ProjectStructureNodeWorkflowStatusGetAsync(agent, accessState, projectId, nodeId, cancellationToken),
                    "project_structure_node_workflow_status_get",
                    "Reads the current workflow run status for a project-structure workflow node."),
                AIFunctionFactory.Create(
                    (Guid projectId, string nodeId, ProjectStructureNodeDeleteInput request, int? estimatedMinutes = null, CancellationToken cancellationToken = default) => ProjectStructureNodeDeleteAsync(agent, accessState, projectId, nodeId, request, estimatedMinutes, cancellationToken),
                    "project_structure_node_delete",
                    "Deletes a project-structure node and its editable descendants. For projected process-run branches, this hides the projection from the current project structure without deleting process history. This is destructive for editable nodes: read the branch first, acquire or pass a lease when coordinating with other agents, and use only when the requested cleanup is explicit."),
                AIFunctionFactory.Create(
                    (Guid projectId, ProjectStructureNodeDeleteBatchInput request, int? estimatedMinutes = null, CancellationToken cancellationToken = default) => ProjectStructureNodesDeleteAsync(agent, accessState, projectId, request, estimatedMinutes, cancellationToken),
                    "project_structure_nodes_delete",
                    "Deletes multiple project-structure nodes in one governed mutation. Provide all target node ids in request.nodeIds after reading the branch. Descendant duplicates are ignored when an ancestor is also selected. For projected process-run branches, this hides projections from the current project structure without deleting process history."),
                AIFunctionFactory.Create(
                    (Guid projectId, ProjectStructureApprovalRequestCreateInput request, CancellationToken cancellationToken = default) => ProjectStructureApprovalRequestAsync(agent, accessState, projectId, request, cancellationToken),
                    "project_structure_approval_request",
                    "Records an approval-request node in the project structure so blocked work is written back into the graph."),
                AIFunctionFactory.Create(
                    (Guid projectId, ProjectStructureAgentAssetCreateInput request, int? estimatedMinutes = null, CancellationToken cancellationToken = default) => ProjectStructureAssetCreateAsync(agent, accessState, projectId, request, estimatedMinutes, cancellationToken),
                    "project_structure_asset_create",
                    "Creates a managed File, ImageAsset, or VideoAsset node through the internal project-structure asset pipeline. Use title, subtitle, and notes for descriptive evidence; typed storage metadata is derived by the service and is not caller-controlled. Provide media base64 data, sourceWorkspacePath for a file inside the managed workspace, or sourceUrl for a public http/https file that should be downloaded and stored as a managed asset."),
                AIFunctionFactory.Create(
                    (Guid projectId, string nodeId, CancellationToken cancellationToken = default) => ProjectStructureAssetGetAsync(agent, accessState, projectId, nodeId, cancellationToken),
                    "project_structure_asset_get",
                    "Returns readonly metadata for an existing managed asset node."),
                AIFunctionFactory.Create(
                    (Guid projectId, string nodeId, CancellationToken cancellationToken = default) => ProjectStructureAssetContentGetAsync(agent, accessState, projectId, nodeId, cancellationToken),
                    "project_structure_asset_content_get",
                    "Returns readonly metadata and bounded base64 content for an existing managed asset node. Binary media and large assets omit Base64Data. For PDF or document assets, use the exact returned mediaRelativePath with workspace_convert_document. Use workspace_inspect_image or workspace_analyze_image only for image media."),
                AIFunctionFactory.Create(
                    (Guid projectId, string nodeId, ProjectStructureAgentAssetRevisionRequest request, int? estimatedMinutes = null, CancellationToken cancellationToken = default) => ProjectStructureAssetCreateRevisionAsync(agent, accessState, projectId, nodeId, request, estimatedMinutes, cancellationToken),
                    "project_structure_asset_create_revision",
                    "Creates a new revision asset node under an existing asset node instead of overwriting the original asset. Use title, subtitle, and notes for descriptive evidence; typed storage metadata is derived by the service and is not caller-controlled."),
                AIFunctionFactory.Create(
                    (Guid projectId, ProjectStructureLinkInput request, int? estimatedMinutes = null, CancellationToken cancellationToken = default) => ProjectStructureLinkCreateAsync(agent, accessState, projectId, request, estimatedMinutes, cancellationToken),
                    "project_structure_link_create",
                    "Creates a generic project-structure link with the supplied link kind. Use dependency-specific tools for DependsOn links unless another link kind is explicitly needed. Never use this tool to attach a workflow or process to a canonical WorkItem/task; use project_task_resource_attach."),
                AIFunctionFactory.Create(
                    (Guid projectId, ProjectStructureLinkInput request, int? estimatedMinutes = null, CancellationToken cancellationToken = default) => ProjectStructureLinkUnlinkAsync(agent, accessState, projectId, request, estimatedMinutes, cancellationToken),
                    "project_structure_link_unlink",
                    "Removes a generic project-structure link with the supplied link kind. Use dependency-specific tools for DependsOn links unless another link kind is explicitly needed."),
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
                    (ProjectStructureScopeInput scope, string leaseToken, int durationMinutes = 15, CancellationToken cancellationToken = default) => ProjectStructureLeaseRenewAsync(agent, accessState, scope, leaseToken, durationMinutes, cancellationToken),
                    "project_structure_lease_renew",
                    "Renews an owned project, node, or repo-branch lease token for continued coordinated mutation work."),
                AIFunctionFactory.Create(
                    (ProjectStructureScopeInput scope, string leaseToken, CancellationToken cancellationToken = default) => ProjectStructureLeaseReleaseAsync(agent, accessState, scope, leaseToken, cancellationToken),
                    "project_structure_lease_release",
                    "Releases an existing project, node, or repo-branch lease token.")
            ];

            if (!accessState.CanWrite)
            {
                tools.RemoveAll(tool => AgentToolInvocationPolicyMetadata.IsMutationTool(tool.Name));
            }
            else if (accessState.RequiresNonTaskWriteGuard)
            {
                tools.RemoveAll(tool => ProjectStructureNonTaskWritePolicy.RequiresFullStructureWrite(tool.Name));
            }

            if (accessState.CanCreateProjects)
            {
                tools.Add(AIFunctionFactory.Create(
                    (ProjectStructureProjectSaveRequest request, int? estimatedMinutes = null, CancellationToken cancellationToken = default) => ProjectStructureProjectCreateAsync(agent, accessState, request, estimatedMinutes, cancellationToken),
                    AgentToolInvocationPolicyMetadata.ProjectStructureProjectCreate,
                    "Creates a standalone CanDoItAll project. Use project_structure_subproject_create when the new project must be attached below a parent project."));
            }

            if (accessState.CanCreateSubprojects)
            {
                tools.Add(AIFunctionFactory.Create(
                    (Guid parentProjectId, ProjectStructureProjectSaveRequest request, int? estimatedMinutes = null, CancellationToken cancellationToken = default) => ProjectStructureSubprojectCreateAsync(agent, accessState, parentProjectId, request, estimatedMinutes, cancellationToken),
                    AgentToolInvocationPolicyMetadata.ProjectStructureSubprojectCreate,
                    "Creates a new project and atomically attaches it as a direct subproject of parentProjectId. Use the returned project id for subsequent project_structure_node_create calls that populate the new subproject."));
            }

            if (accessState.CanCreateSubprojects && accessState.CanWrite)
            {
                tools.Add(AIFunctionFactory.Create(
                    (Guid parentProjectId, ProjectStructureSubprojectChangeRequest request, int? estimatedMinutes = null, CancellationToken cancellationToken = default) => ProjectStructureSubprojectLinkAsync(agent, accessState, parentProjectId, request, estimatedMinutes, cancellationToken),
                    AgentToolInvocationPolicyMetadata.ProjectStructureSubprojectLink,
                    "Adds or reconnects an existing project as a subproject under a parent project."));
                tools.Add(AIFunctionFactory.Create(
                    (Guid projectId, ProjectStructureNodesToSubprojectInput request, int? estimatedMinutes = null, CancellationToken cancellationToken = default) => ProjectStructureNodesToNewSubprojectAsync(agent, accessState, projectId, request, estimatedMinutes, cancellationToken),
                    AgentToolInvocationPolicyMetadata.ProjectStructureNodesToNewSubproject,
                    "Creates a new subproject under the opened project and moves the supplied node ids, optionally with descendants, into that subproject in one operation. Use this for prompts like 'take selected nodes and move them to their own new subproject named XYZ'. If the contextual prompt lists selected node ids, pass those exact ids as nodeIds."));
            }

            if (accessState.CanRead &&
                ProjectPlanAgentAuthorizationPolicy.IsPlanSummaryAuthorized(agent, context.Capabilities))
            {
                tools.Add(AIFunctionFactory.Create(
                    (Guid projectId, ProjectPlanSummaryQuery? request = null, CancellationToken cancellationToken = default) => ProjectPlanSummaryGetAsync(agent, accessState, projectId, request, cancellationToken),
                    AgentToolInvocationPolicyMetadata.ProjectPlanSummaryGet,
                    "Returns a database-filtered project plan summary with task-state counts, expected cost by currency, schedule metrics, resource-group coverage, and bounded running/blocked/waiting task previews."));
            }

            if (accessState.CanWriteTasksUnscoped)
            {
                tools.Add(AIFunctionFactory.Create(
                    (Guid projectId, ProjectStructureTaskCreateRequest request, CancellationToken cancellationToken = default) => ProjectTaskCreateAsync(agent, accessState, projectId, request, cancellationToken),
                    AgentToolInvocationPolicyMetadata.ProjectTaskCreate,
                    "Creates a typed project task under the Main backlog, applies its delivery schedule and estimate, optionally assigns a person/agent or attaches a workflow/process, and inserts it into the Gantt row order."));
                tools.Add(AIFunctionFactory.Create(
                    (Guid projectId, ProjectStructureTaskDetailsUpdateRequest request, CancellationToken cancellationToken = default) => ProjectTaskUpdateAsync(agent, accessState, projectId, request, cancellationToken),
                    AgentToolInvocationPolicyMetadata.ProjectTaskUpdate,
                    "Updates a typed project task through the Gantt task-details mutation path. Read the current task first and provide exact current estimate, execution, and expected-cost-basis snapshots for optimistic concurrency. currentCostBasis is required even when its value is null. currentProgressPercent accepts -1 for untracked progress, while proposedProgressPercent must be 0-100. Direct assignees may be a person or agent."));
                tools.Add(AIFunctionFactory.Create(
                    (Guid projectId, string taskNodeId, ProjectStructureTaskResourceAttachRequest request, CancellationToken cancellationToken = default) => ProjectTaskResourceAttachAsync(agent, accessState, projectId, taskNodeId, request, cancellationToken),
                    AgentToolInvocationPolicyMetadata.ProjectTaskResourceAttach,
                    "Attaches an exact workflow version or process to a canonical task and commits authoritative expected pricing as one compensated operation. First call project_structure_read with the exact task id in nodeIds and includeMetadata true. Parse the returned metadataJson and copy workItem.executionState, workItem.actualStartedAtUtc, and workItem.actualEndedAtUtc exactly into currentExecution.state, currentExecution.actualStartedAtUtc, and currentExecution.actualEndedAtUtc. Do not infer defaults, reuse a stale snapshot, or use generic workflow, process-link, metadata, or Uses-link tools for canonical task resources."));
            }

            return tools;
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
                    var visibleProjects = projects
                        .Where(project => IsProjectAllowed(accessState, project.Id))
                        .ToList();
                    return visibleProjects
                        .OrderBy(project => project.Name, StringComparer.OrdinalIgnoreCase)
                        .ToList();
                },
                cancellationToken);
        }

        private Task<ProjectPlanSummary> ProjectPlanSummaryGetAsync(
            AgentDefinition agent,
            ProjectStructureAccessState accessState,
            Guid projectId,
            ProjectPlanSummaryQuery? request,
            CancellationToken cancellationToken)
        {
            return ExecuteAsync(
                agent,
                "plan.summary",
                projectId,
                null,
                null,
                null,
                request,
                async cancellationToken =>
                {
                    EnsureProjectReadAllowed(accessState, projectId);
                    await authorizationService.EnsurePlanSummaryAuthorizedAsync(
                        agent.Id,
                        projectId,
                        cancellationToken);
                    return await planAnalyticsService.GetSummaryAsync(projectId, request, cancellationToken);
                },
                cancellationToken);
        }

        private Task<ProjectStructureTaskCreateResult> ProjectTaskCreateAsync(
            AgentDefinition agent,
            ProjectStructureAccessState accessState,
            Guid projectId,
            ProjectStructureTaskCreateRequest request,
            CancellationToken cancellationToken)
        {
            return ExecuteAsync(
                agent,
                "tasks.create",
                projectId,
                null,
                ProjectStructureLeaseScopeKind.Project,
                projectId.ToString("D"),
                request,
                async cancellationToken =>
                {
                    EnsureProjectTaskWriteAllowed(accessState, projectId);
                    await authorizationService.EnsureTaskWriteAuthorizedAsync(
                        agent.Id,
                        projectId,
                        AgentToolInvocationPolicyMetadata.ProjectTaskCreate,
                        cancellationToken);
                    try
                    {
                        return await taskCreationService.CreateAsync(
                            projectId,
                            request,
                            BuildAgentContext(agent, accessState, projectId),
                            cancellationToken);
                    }
                    catch (ProjectStructureTaskCreationException exception)
                    {
                        throw ProjectStructureTaskAgentExceptionMapper.Map(exception);
                    }
                    catch (ProjectStructureGanttMutationException exception)
                    {
                        throw ProjectStructureTaskAgentExceptionMapper.Map(exception);
                    }
                },
                cancellationToken);
        }

        private Task<ProjectStructureGanttMutationResult> ProjectTaskUpdateAsync(
            AgentDefinition agent,
            ProjectStructureAccessState accessState,
            Guid projectId,
            ProjectStructureTaskDetailsUpdateRequest request,
            CancellationToken cancellationToken)
        {
            return ExecuteAsync(
                agent,
                "tasks.update",
                projectId,
                request.TaskId.Value,
                ProjectStructureLeaseScopeKind.Project,
                projectId.ToString("D"),
                request,
                async cancellationToken =>
                {
                    EnsureProjectTaskWriteAllowed(accessState, projectId);
                    await authorizationService.EnsureTaskWriteAuthorizedAsync(
                        agent.Id,
                        projectId,
                        AgentToolInvocationPolicyMetadata.ProjectTaskUpdate,
                        cancellationToken);
                    try
                    {
                        return await taskDetailsService.UpdateAsync(projectId, request, cancellationToken);
                    }
                    catch (ProjectStructureTaskDetailsException exception)
                    {
                        throw ProjectStructureTaskAgentExceptionMapper.Map(exception);
                    }
                    catch (ProjectStructureGanttMutationException exception)
                    {
                        throw ProjectStructureTaskAgentExceptionMapper.Map(exception);
                    }
                },
                cancellationToken);
        }

        private Task<ProjectStructureTaskResourceAttachResult> ProjectTaskResourceAttachAsync(
            AgentDefinition agent,
            ProjectStructureAccessState accessState,
            Guid projectId,
            string taskNodeId,
            ProjectStructureTaskResourceAttachRequest request,
            CancellationToken cancellationToken)
        {
            return ExecuteAsync(
                agent,
                "tasks.resource-attach",
                projectId,
                taskNodeId,
                ProjectStructureLeaseScopeKind.Project,
                projectId.ToString("D"),
                request,
                async cancellationToken =>
                {
                    EnsureProjectTaskWriteAllowed(accessState, projectId);
                    await authorizationService.EnsureTaskWriteAuthorizedAsync(
                        agent.Id,
                        projectId,
                        AgentToolInvocationPolicyMetadata.ProjectTaskResourceAttach,
                        cancellationToken);
                    return await taskResourceAttachmentService.AttachAsync(
                        projectId,
                        taskNodeId,
                        request,
                        BuildAgentContext(agent, accessState, projectId),
                        cancellationToken);
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
                    await authorizationService.EnsureProjectCreationAuthorizedAsync(agent.Id, cancellationToken);
                    EnsureProjectCreationAllowed(accessState);
                    ProjectStructureAgentCreationValidation.EnsureProjectRequest(request);
                    var context = BuildAgentContext(agent);
                    var created = await projectCreationCoordinator.CreateAsync(
                        agent,
                        (newProjectId, cancellationToken) => agentService.CreateProjectAsync(
                            newProjectId,
                            request,
                            context,
                            cancellationToken),
                        response => response.Id,
                        cancellationToken);
                    GrantSessionCreatedProjectAccess(accessState, created.Id);
                    return created;
                },
                cancellationToken,
                projectIdSelector: response => response.Id);
        }

        private Task<ProjectSummary> ProjectStructureSubprojectCreateAsync(
            AgentDefinition agent,
            ProjectStructureAccessState accessState,
            Guid parentProjectId,
            ProjectStructureProjectSaveRequest request,
            int? estimatedMinutes,
            CancellationToken cancellationToken)
        {
            return ExecuteAsync(
                agent,
                "projects.subproject-create",
                parentProjectId,
                null,
                ProjectStructureLeaseScopeKind.Project,
                parentProjectId.ToString("D"),
                request,
                async cancellationToken =>
                {
                    await authorizationService.EnsureSubprojectCreationAuthorizedAsync(
                        agent.Id,
                        parentProjectId,
                        cancellationToken);
                    EnsureSubprojectCreationAllowed(accessState);
                    EnsureProjectAllowed(accessState, parentProjectId);
                    ProjectStructureAgentCreationValidation.EnsureSubprojectRequest(parentProjectId, request);
                    var created = await projectCreationCoordinator.CreateAsync(
                        agent,
                        (newProjectId, cancellationToken) => agentService.CreateSubprojectAsync(
                            parentProjectId,
                            newProjectId,
                            request,
                            BuildAgentContext(agent, accessState, parentProjectId),
                            cancellationToken),
                        response => response.Id,
                        cancellationToken);
                    GrantSessionCreatedProjectAccess(accessState, created.Id);
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
                    return await agentService.SaveProjectAsync(projectId, request, BuildAgentContext(agent, accessState, projectId), cancellationToken);
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
                    await authorizationService.EnsureSubprojectLinkAuthorizedAsync(
                        agent.Id,
                        parentProjectId,
                        request.ChildProjectId,
                        request.CurrentParentProjectId,
                        cancellationToken);
                    EnsureSubprojectCreationAllowed(accessState);
                    EnsureProjectWriteAllowed(accessState, parentProjectId);
                    EnsureProjectWriteAllowed(accessState, request.ChildProjectId);
                    if (request.CurrentParentProjectId.HasValue)
                    {
                        EnsureProjectWriteAllowed(accessState, request.CurrentParentProjectId.Value);
                    }

                    await agentService.ChangeSubprojectAsync(parentProjectId, request, BuildAgentContext(agent, accessState, parentProjectId), cancellationToken);
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
                    var authorization = await authorizationService.EnsureNodesToNewSubprojectAuthorizedAsync(
                        agent.Id,
                        projectId,
                        cancellationToken);
                    EnsureSubprojectCreationAllowed(accessState);
                    EnsureProjectWriteAllowed(accessState, projectId);
                    ProjectStructureAgentCreationValidation.EnsureNodesToSubprojectRequest(projectId, request);
                    await EnsureTaskFreeTargetsAsync(
                        authorization.RequiresNonTaskWriteGuard,
                        projectId,
                        request.NodeIds,
                        request.IncludeDescendants,
                        cancellationToken);
                    var result = await projectCreationCoordinator.CreateAsync(
                        agent,
                        (targetProjectId, cancellationToken) => agentService.MoveNodesToNewSubprojectAsync(
                            projectId,
                            targetProjectId,
                            request,
                            BuildAgentContext(agent, accessState, projectId),
                            cancellationToken),
                        response => response.TargetProjectId,
                        cancellationToken);
                    GrantSessionCreatedProjectAccess(accessState, result.TargetProjectId);
                    return result;
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
                    var appliedDefaultScope = false;
                    var dispatch = await ProjectStructureInvocationSnapshotReadDispatcher.ReadAsync(
                        accessState.InvocationSnapshotReadContext,
                        new DatabaseProfileGeneration(
                            databaseRuntimeState.GetSnapshot().Generation),
                        timeProvider.GetUtcNow(),
                        projectId,
                        request ?? new ProjectStructureReadRequest(),
                        ReadCanonicalAsync,
                        cancellationToken);
                    var response = dispatch.Response;
                    var nodes = appliedDefaultScope
                        ? response.Nodes
                            .Where(ProjectStructureProcessContextNodeFilter.ShouldIncludeInProcessContext)
                            .ToList()
                        : response.Nodes;
                    var warnings = appliedDefaultScope
                        ? response.Warnings
                            .Append(
                                $"Governed process default applied: unfiltered project_structure_read returned only Active, Draft, Planned, and Published nodes with take={GovernedProcessDefaultStructureReadTake}. Pass an explicit request with statuses, take, nodeIds, subtreeRootIds, objectTypes, projectRoles, or maxPriority when broader graph context is required.")
                            .Append("Generated process evidence, proof, report, log, screenshot, and file-summary nodes are omitted from the default governed process read. Pass explicit nodeIds or subtreeRootIds only when named historical evidence is required by the current step.")
                            .ToList()
                        : response.Warnings;
                    return new ProjectStructureReadToolData(
                        response.ProjectId,
                        response.ProjectName,
                        nodes.Select(MapCompactNode).ToList(),
                        response.Links,
                        warnings,
                        dispatch.Source);

                    async Task<ProjectStructureReadResponse> ReadCanonicalAsync(
                        ProjectStructureReadRequest canonicalRequest,
                        CancellationToken canonicalCancellationToken)
                    {
                        var effectiveRequest = ResolveGovernedProcessReadRequest(
                            accessState,
                            canonicalRequest,
                            out appliedDefaultScope);
                        return await agentService.GetStructureAsync(
                            projectId,
                            effectiveRequest,
                            canonicalCancellationToken);
                    }
                },
                cancellationToken);
        }

        private static ProjectStructureReadRequest ResolveGovernedProcessReadRequest(
            ProjectStructureAccessState accessState,
            ProjectStructureReadRequest? request,
            out bool appliedDefaultScope)
        {
            var effectiveRequest = request ?? new ProjectStructureReadRequest();
            appliedDefaultScope = false;

            if (accessState.ScopedProcessAccess is null ||
                !IsUnscopedStructureRead(effectiveRequest))
            {
                return effectiveRequest;
            }

            appliedDefaultScope = true;
            return effectiveRequest with
            {
                Statuses = GovernedProcessDefaultStructureReadStatuses,
                Take = GovernedProcessDefaultStructureReadTake
            };
        }

        private static bool IsUnscopedStructureRead(ProjectStructureReadRequest request)
        {
            return IsNullOrEmpty(request.NodeIds) &&
                   IsNullOrEmpty(request.SubtreeRootIds) &&
                   IsNullOrEmpty(request.ObjectTypes) &&
                   IsNullOrEmpty(request.ProjectRoles) &&
                   IsNullOrEmpty(request.Statuses) &&
                   request.MaxPriority is null &&
                   request.Take is null;
        }

        private static bool IsNullOrEmpty<T>(IReadOnlyCollection<T>? values)
            => values is null || values.Count == 0;

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
                    await EnsureTaskFreeTargetsAsync(
                        accessState,
                        projectId,
                        [request.SourceNodeId, request.TargetNodeId],
                        includeDescendants: false,
                        cancellationToken);
                    return await agentService.LinkNodesAsync(
                        projectId,
                        request with { Kind = ProjectObjectLinkKind.DependsOn },
                        BuildAgentContext(agent, accessState, projectId),
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
                    await EnsureTaskFreeTargetsAsync(
                        accessState,
                        projectId,
                        [request.SourceNodeId, request.TargetNodeId],
                        includeDescendants: false,
                        cancellationToken);
                    return await agentService.UnlinkNodesAsync(
                        projectId,
                        request with { Kind = ProjectObjectLinkKind.DependsOn },
                        BuildAgentContext(agent, accessState, projectId),
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
                    EnsureAgentMetadataPayloadValid(request.MetadataJson);
                    var effectiveRequest = NormalizeGovernedProcessCreateParent(accessState, request);
                    ProjectStructureNonTaskWritePolicy.EnsureNodeCreateAllowed(
                        accessState.RequiresNonTaskWriteGuard,
                        effectiveRequest.ObjectType,
                        effectiveRequest.ObjectSubtype);
                    ProjectStructureCanonicalTaskMutationPolicy.EnsureGenericCreateAllowed(
                        effectiveRequest.ObjectType,
                        effectiveRequest.ObjectSubtype);
                    if (await TryReuseGovernedProcessNodeCreateAsync(
                            agent,
                            accessState,
                            projectId,
                            effectiveRequest,
                            cancellationToken) is { } existingNode)
                    {
                        return existingNode;
                    }

                    return await agentService.CreateNodeAsync(projectId, effectiveRequest, BuildAgentContext(agent, accessState, projectId), cancellationToken);
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
                    EnsureAgentMetadataPayloadValid(request.MetadataJson);
                    await EnsureNodeUpdateAllowedAsync(
                        accessState,
                        projectId,
                        nodeId,
                        request.ObjectType,
                        request.ObjectSubtype,
                        cancellationToken);
                    return await agentService.UpdateNodeAsync(projectId, nodeId, request, BuildAgentContext(agent, accessState, projectId), cancellationToken);
                },
                cancellationToken);
        }

        private Task<ProjectStructureNodeSummary> ProjectStructureNodeTypeUpdateAsync(
            AgentDefinition agent,
            ProjectStructureAccessState accessState,
            Guid projectId,
            string nodeId,
            ProjectStructureNodeTypeInput request,
            int? estimatedMinutes,
            CancellationToken cancellationToken)
        {
            return ExecuteAsync(
                agent,
                "structure.node-type",
                projectId,
                nodeId,
                ProjectStructureLeaseScopeKind.Project,
                projectId.ToString("D"),
                request,
                async cancellationToken =>
                {
                    EnsureProjectWriteAllowed(accessState, projectId);
                    await EnsureNodeUpdateAllowedAsync(
                        accessState,
                        projectId,
                        nodeId,
                        request.ObjectType,
                        request.ObjectSubtype,
                        cancellationToken);
                    return await agentService.UpdateNodeTypeAsync(projectId, nodeId, request, BuildAgentContext(agent, accessState, projectId), cancellationToken);
                },
                cancellationToken);
        }

        private Task<ProjectStructureNodeSummary> ProjectStructureNodeMetadataUpdateAsync(
            AgentDefinition agent,
            ProjectStructureAccessState accessState,
            Guid projectId,
            string nodeId,
            ProjectStructureNodeMetadataInput request,
            int? estimatedMinutes,
            CancellationToken cancellationToken)
        {
            return ExecuteAsync(
                agent,
                "structure.node-metadata",
                projectId,
                nodeId,
                ProjectStructureLeaseScopeKind.Project,
                projectId.ToString("D"),
                request,
                async cancellationToken =>
                {
                    EnsureProjectWriteAllowed(accessState, projectId);
                    EnsureAgentMetadataPayloadValid(request.MetadataJson);
                    await EnsureNodeUpdateAllowedAsync(
                        accessState,
                        projectId,
                        nodeId,
                        requestedObjectType: null,
                        requestedObjectSubtype: null,
                        cancellationToken);
                    return await agentService.UpdateNodeMetadataAsync(projectId, nodeId, request, BuildAgentContext(agent, accessState, projectId), cancellationToken);
                },
                cancellationToken);
        }

        private Task<OperationCount> ProjectStructureNodesStatusUpdateAsync(
            AgentDefinition agent,
            ProjectStructureAccessState accessState,
            Guid projectId,
            ProjectStructureStatusBatchInput request,
            int? estimatedMinutes,
            CancellationToken cancellationToken)
        {
            return ExecuteAsync(
                agent,
                "structure.node-statuses",
                projectId,
                null,
                ProjectStructureLeaseScopeKind.Project,
                projectId.ToString("D"),
                request,
                async cancellationToken =>
                {
                    EnsureProjectWriteAllowed(accessState, projectId);
                    await EnsureTaskFreeTargetsAsync(accessState, projectId, request.NodeIds, includeDescendants: false, cancellationToken);
                    var count = await agentService.UpdateNodeStatusesAsync(projectId, request, BuildAgentContext(agent, accessState, projectId), cancellationToken);
                    return new OperationCount(count);
                },
                cancellationToken);
        }

        private Task<OperationCount> ProjectStructureNodeStatusUpdateAsync(
            AgentDefinition agent,
            ProjectStructureAccessState accessState,
            Guid projectId,
            string nodeId,
            ProjectStructureStatusInput request,
            int? estimatedMinutes,
            CancellationToken cancellationToken)
        {
            return ExecuteAsync(
                agent,
                "structure.node-status",
                projectId,
                nodeId,
                ProjectStructureLeaseScopeKind.Project,
                projectId.ToString("D"),
                request,
                async cancellationToken =>
                {
                    EnsureProjectWriteAllowed(accessState, projectId);
                    await EnsureTaskFreeTargetsAsync(accessState, projectId, [nodeId], includeDescendants: false, cancellationToken);
                    var count = await agentService.UpdateNodeStatusesAsync(
                        projectId,
                        new ProjectStructureStatusBatchInput([nodeId], request.Status, request.LeaseToken),
                        BuildAgentContext(agent, accessState, projectId),
                        cancellationToken);
                    return new OperationCount(count);
                },
                cancellationToken);
        }

        private Task<OperationCount> ProjectStructureNodesProgressUpdateAsync(
            AgentDefinition agent,
            ProjectStructureAccessState accessState,
            Guid projectId,
            ProjectStructureProgressBatchInput request,
            int? estimatedMinutes,
            CancellationToken cancellationToken)
        {
            return ExecuteAsync(
                agent,
                "structure.node-progress",
                projectId,
                null,
                ProjectStructureLeaseScopeKind.Project,
                projectId.ToString("D"),
                request,
                async cancellationToken =>
                {
                    EnsureProjectWriteAllowed(accessState, projectId);
                    await EnsureTaskFreeTargetsAsync(accessState, projectId, request.NodeIds, includeDescendants: false, cancellationToken);
                    var count = await agentService.UpdateNodeProgressAsync(projectId, request, BuildAgentContext(agent, accessState, projectId), cancellationToken);
                    return new OperationCount(count);
                },
                cancellationToken);
        }

        private Task<OperationCount> ProjectStructureNodeProgressUpdateAsync(
            AgentDefinition agent,
            ProjectStructureAccessState accessState,
            Guid projectId,
            string nodeId,
            ProjectStructureProgressInput request,
            int? estimatedMinutes,
            CancellationToken cancellationToken)
        {
            return ExecuteAsync(
                agent,
                "structure.node-progress-single",
                projectId,
                nodeId,
                ProjectStructureLeaseScopeKind.Project,
                projectId.ToString("D"),
                request,
                async cancellationToken =>
                {
                    EnsureProjectWriteAllowed(accessState, projectId);
                    await EnsureTaskFreeTargetsAsync(accessState, projectId, [nodeId], includeDescendants: false, cancellationToken);
                    var count = await agentService.UpdateNodeProgressAsync(
                        projectId,
                        new ProjectStructureProgressBatchInput([nodeId], request.ProgressMode, request.ProgressPercent, request.LeaseToken),
                        BuildAgentContext(agent, accessState, projectId),
                        cancellationToken);
                    return new OperationCount(count);
                },
                cancellationToken);
        }

        private Task<OperationCount> ProjectStructureNodesMarkerUpdateAsync(
            AgentDefinition agent,
            ProjectStructureAccessState accessState,
            Guid projectId,
            ProjectStructureMarkerBatchInput request,
            int? estimatedMinutes,
            CancellationToken cancellationToken)
        {
            return ExecuteAsync(
                agent,
                "structure.node-markers",
                projectId,
                null,
                ProjectStructureLeaseScopeKind.Project,
                projectId.ToString("D"),
                request,
                async cancellationToken =>
                {
                    EnsureProjectWriteAllowed(accessState, projectId);
                    await EnsureTaskFreeTargetsAsync(accessState, projectId, request.NodeIds, includeDescendants: false, cancellationToken);
                    var count = await agentService.UpdateNodeMarkerAsync(projectId, request, BuildAgentContext(agent, accessState, projectId), cancellationToken);
                    return new OperationCount(count);
                },
                cancellationToken);
        }

        private Task<OperationCount> ProjectStructureNodeMarkerUpdateAsync(
            AgentDefinition agent,
            ProjectStructureAccessState accessState,
            Guid projectId,
            string nodeId,
            ProjectStructureMarkerInput request,
            int? estimatedMinutes,
            CancellationToken cancellationToken)
        {
            return ExecuteAsync(
                agent,
                "structure.node-marker",
                projectId,
                nodeId,
                ProjectStructureLeaseScopeKind.Project,
                projectId.ToString("D"),
                request,
                async cancellationToken =>
                {
                    EnsureProjectWriteAllowed(accessState, projectId);
                    await EnsureTaskFreeTargetsAsync(accessState, projectId, [nodeId], includeDescendants: false, cancellationToken);
                    var count = await agentService.ChangeNodeMarkerAsync(projectId, nodeId, request, BuildAgentContext(agent, accessState, projectId), cancellationToken);
                    return new OperationCount(count);
                },
                cancellationToken);
        }

        private Task<OperationCount> ProjectStructureNodesPriorityUpdateAsync(
            AgentDefinition agent,
            ProjectStructureAccessState accessState,
            Guid projectId,
            ProjectStructurePriorityBatchInput request,
            int? estimatedMinutes,
            CancellationToken cancellationToken)
        {
            return ExecuteAsync(
                agent,
                "structure.node-priorities",
                projectId,
                null,
                ProjectStructureLeaseScopeKind.Project,
                projectId.ToString("D"),
                request,
                async cancellationToken =>
                {
                    EnsureProjectWriteAllowed(accessState, projectId);
                    await EnsureTaskFreeTargetsAsync(accessState, projectId, request.NodeIds, includeDescendants: false, cancellationToken);
                    var count = await agentService.UpdateNodePriorityAsync(projectId, request, BuildAgentContext(agent, accessState, projectId), cancellationToken);
                    return new OperationCount(count);
                },
                cancellationToken);
        }

        private Task<OperationCount> ProjectStructureNodePriorityUpdateAsync(
            AgentDefinition agent,
            ProjectStructureAccessState accessState,
            Guid projectId,
            string nodeId,
            ProjectStructurePriorityInput request,
            int? estimatedMinutes,
            CancellationToken cancellationToken)
        {
            return ExecuteAsync(
                agent,
                "structure.node-priority",
                projectId,
                nodeId,
                ProjectStructureLeaseScopeKind.Project,
                projectId.ToString("D"),
                request,
                async cancellationToken =>
                {
                    EnsureProjectWriteAllowed(accessState, projectId);
                    await EnsureTaskFreeTargetsAsync(accessState, projectId, [nodeId], includeDescendants: false, cancellationToken);
                    var count = await agentService.UpdateNodePriorityAsync(
                        projectId,
                        new ProjectStructurePriorityBatchInput([nodeId], request.Priority, request.LeaseToken),
                        BuildAgentContext(agent, accessState, projectId),
                        cancellationToken);
                    return new OperationCount(count);
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
                    await EnsureTaskFreeTargetsAsync(accessState, projectId, [request.NodeId], includeDescendants: false, cancellationToken);
                    await agentService.MoveNodeAsync(projectId, request, BuildAgentContext(agent, accessState, projectId), cancellationToken);
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
                    await EnsureTaskFreeTargetsAsync(accessState, projectId, [request.RootNodeId], includeDescendants: true, cancellationToken);
                    return await agentService.RecomposeNodeAsync(projectId, request, BuildAgentContext(agent, accessState, projectId), cancellationToken);
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
                    await EnsureTaskFreeTargetsAsync(accessState, projectId, [request.NodeId], includeDescendants: false, cancellationToken);
                    return await agentService.ReparentNodeAsync(projectId, request, BuildAgentContext(agent, accessState, projectId), cancellationToken);
                },
                cancellationToken);
        }

        private Task<ProjectStructureSubprojectTransferResult> ProjectStructureNodeDescendantsToProjectMoveAsync(
            AgentDefinition agent,
            ProjectStructureAccessState accessState,
            Guid projectId,
            string nodeId,
            ProjectStructureSubtreeTransferInput request,
            int? estimatedMinutes,
            CancellationToken cancellationToken)
        {
            return ExecuteAsync(
                agent,
                "structure.node-transfer-descendants",
                projectId,
                nodeId,
                ProjectStructureLeaseScopeKind.Project,
                projectId.ToString("D"),
                request,
                async cancellationToken =>
                {
                    EnsureProjectWriteAllowed(accessState, projectId);
                    EnsureProjectWriteAllowed(accessState, request.TargetProjectId);
                    await EnsureTaskFreeTargetsAsync(accessState, projectId, [nodeId], includeDescendants: true, cancellationToken);
                    return await agentService.MoveDescendantsToProjectAsync(projectId, nodeId, request, BuildAgentContext(agent, accessState, projectId), cancellationToken);
                },
                cancellationToken);
        }

        private Task<ArtifactReference> ProjectStructureNodeCommandExecuteAsync(
            AgentDefinition agent,
            ProjectStructureAccessState accessState,
            Guid projectId,
            string nodeId,
            ProjectStructureNodeCommandInput request,
            int? estimatedMinutes,
            CancellationToken cancellationToken)
        {
            return ExecuteAsync(
                agent,
                "structure.node-command",
                projectId,
                nodeId,
                ProjectStructureLeaseScopeKind.Project,
                projectId.ToString("D"),
                request,
                async cancellationToken =>
                {
                    EnsureProjectWriteAllowed(accessState, projectId);
                    await EnsureTaskFreeTargetsAsync(accessState, projectId, [nodeId], includeDescendants: false, cancellationToken);
                    return await agentService.ExecuteNodeCommandAsync(projectId, nodeId, request, BuildAgentContext(agent, accessState, projectId), cancellationToken);
                },
                cancellationToken);
        }

        private Task<ProjectStructureLinkChangeResult> ProjectStructureNodeProcessDefinitionLinkAsync(
            AgentDefinition agent,
            ProjectStructureAccessState accessState,
            Guid projectId,
            string nodeId,
            ProjectStructureProcessDefinitionLinkInput request,
            int? estimatedMinutes,
            CancellationToken cancellationToken)
        {
            return ExecuteAsync(
                agent,
                "structure.node-link-process-definition",
                projectId,
                nodeId,
                ProjectStructureLeaseScopeKind.Project,
                projectId.ToString("D"),
                request,
                async cancellationToken =>
                {
                    EnsureProjectWriteAllowed(accessState, projectId);
                    await EnsureTaskFreeTargetsAsync(accessState, projectId, [nodeId], includeDescendants: false, cancellationToken);
                    return await agentService.LinkProcessDefinitionAsync(projectId, nodeId, request, BuildAgentContext(agent, accessState, projectId), cancellationToken);
                },
                cancellationToken);
        }

        private Task<ProjectStructureProcessNodeStartResult> ProjectStructureNodeProcessStartAsync(
            AgentDefinition agent,
            ProjectStructureAccessState accessState,
            Guid projectId,
            string nodeId,
            ProjectStructureProcessNodeStartInput request,
            int? estimatedMinutes,
            CancellationToken cancellationToken)
        {
            return ExecuteAsync(
                agent,
                "structure.node-start-process",
                projectId,
                nodeId,
                ProjectStructureLeaseScopeKind.Project,
                projectId.ToString("D"),
                request,
                async cancellationToken =>
                {
                    EnsureProjectWriteAllowed(accessState, projectId);
                    await EnsureTaskFreeTargetsAsync(accessState, projectId, [nodeId], includeDescendants: false, cancellationToken);
                    return await agentService.StartProcessNodeAsync(projectId, nodeId, request, BuildAgentContext(agent, accessState, projectId), cancellationToken);
                },
                cancellationToken);
        }

        private Task<ProjectStructureProcessSubprocessLaunchResult> ProjectStructureProcessSubprocessLaunchAsync(
            AgentDefinition agent,
            ProjectStructureAccessState accessState,
            ProjectStructureProcessSubprocessLaunchInput request,
            int? estimatedMinutes,
            CancellationToken cancellationToken)
        {
            var scopedProcessAccess = accessState.ScopedProcessAccess;
            return ExecuteAsync(
                agent,
                "structure.process-subprocess-launch",
                scopedProcessAccess?.ProjectId,
                request.ParentProjectNodeId,
                scopedProcessAccess is null ? null : ProjectStructureLeaseScopeKind.Project,
                scopedProcessAccess?.ProjectId.ToString("D"),
                request,
                async cancellationToken =>
                {
                    scopedProcessAccess = EnsureScopedProcessExternalActionAllowed(accessState);
                    return await agentService.StartProcessSubprocessAsync(
                        scopedProcessAccess.ProjectId,
                        scopedProcessAccess.ProcessRunId,
                        scopedProcessAccess.ProcessStepId,
                        request,
                        BuildAgentContext(agent, accessState, scopedProcessAccess.ProjectId),
                        cancellationToken);
                },
                cancellationToken);
        }

        private Task<ProjectStructureWorkflowAddOptionsResult> ProjectStructureNodeWorkflowAddOptionsAsync(
            AgentDefinition agent,
            ProjectStructureAccessState accessState,
            Guid projectId,
            string nodeId,
            ProjectStructureWorkflowAddOptionsInput request,
            CancellationToken cancellationToken)
        {
            return ExecuteAsync(
                agent,
                "structure.node-workflow-add-options",
                projectId,
                nodeId,
                null,
                null,
                request,
                async cancellationToken =>
                {
                    EnsureProjectReadAllowed(accessState, projectId);
                    return await agentService.GetWorkflowAddOptionsAsync(projectId, nodeId, request, cancellationToken);
                },
                cancellationToken);
        }

        private Task<ProjectStructureWorkflowNodeCreateResult> ProjectStructureNodeWorkflowDefinitionCreateAsync(
            AgentDefinition agent,
            ProjectStructureAccessState accessState,
            Guid projectId,
            string nodeId,
            ProjectStructureWorkflowNodeCreateInput request,
            int? estimatedMinutes,
            CancellationToken cancellationToken)
        {
            return ExecuteAsync(
                agent,
                "structure.node-create-workflow-definition",
                projectId,
                nodeId,
                ProjectStructureLeaseScopeKind.Project,
                projectId.ToString("D"),
                request,
                async cancellationToken =>
                {
                    EnsureProjectWriteAllowed(accessState, projectId);
                    return await agentService.CreateWorkflowNodeAsync(projectId, nodeId, request, BuildAgentContext(agent, accessState, projectId), cancellationToken);
                },
                cancellationToken);
        }

        private Task<ProjectStructureWorkflowNodeStartResult> ProjectStructureNodeWorkflowStartAsync(
            AgentDefinition agent,
            ProjectStructureAccessState accessState,
            Guid projectId,
            string nodeId,
            ProjectStructureWorkflowNodeStartInput request,
            int? estimatedMinutes,
            CancellationToken cancellationToken)
        {
            return ExecuteAsync(
                agent,
                "structure.node-start-workflow",
                projectId,
                nodeId,
                ProjectStructureLeaseScopeKind.Project,
                projectId.ToString("D"),
                request,
                async cancellationToken =>
                {
                    EnsureProjectWriteAllowed(accessState, projectId);
                    await EnsureTaskFreeTargetsAsync(accessState, projectId, [nodeId], includeDescendants: false, cancellationToken);
                    return await agentService.StartWorkflowNodeAsync(projectId, nodeId, request, BuildAgentContext(agent, accessState, projectId), cancellationToken);
                },
                cancellationToken);
        }

        private Task<ProjectStructureWorkflowRunStatus> ProjectStructureNodeWorkflowStatusGetAsync(
            AgentDefinition agent,
            ProjectStructureAccessState accessState,
            Guid projectId,
            string nodeId,
            CancellationToken cancellationToken)
        {
            return ExecuteAsync(
                agent,
                "structure.node-workflow-status",
                projectId,
                nodeId,
                null,
                null,
                null,
                async cancellationToken =>
                {
                    EnsureProjectReadAllowed(accessState, projectId);
                    return await agentService.GetWorkflowNodeStatusAsync(projectId, nodeId, cancellationToken);
                },
                cancellationToken);
        }

        private Task<OperationCount> ProjectStructureNodeDeleteAsync(
            AgentDefinition agent,
            ProjectStructureAccessState accessState,
            Guid projectId,
            string nodeId,
            ProjectStructureNodeDeleteInput request,
            int? estimatedMinutes,
            CancellationToken cancellationToken)
        {
            return ExecuteAsync(
                agent,
                "structure.node-delete",
                projectId,
                nodeId,
                ProjectStructureLeaseScopeKind.Project,
                projectId.ToString("D"),
                request,
                async cancellationToken =>
                {
                    EnsureProjectWriteAllowed(accessState, projectId);
                    await EnsureTaskFreeTargetsAsync(accessState, projectId, [nodeId], includeDescendants: true, cancellationToken);
                    var count = await agentService.DeleteNodeAsync(projectId, nodeId, request, BuildAgentContext(agent, accessState, projectId), cancellationToken);
                    return new OperationCount(count);
                },
                cancellationToken);
        }

        private Task<OperationCount> ProjectStructureNodesDeleteAsync(
            AgentDefinition agent,
            ProjectStructureAccessState accessState,
            Guid projectId,
            ProjectStructureNodeDeleteBatchInput request,
            int? estimatedMinutes,
            CancellationToken cancellationToken)
        {
            return ExecuteAsync(
                agent,
                "structure.nodes-delete",
                projectId,
                null,
                ProjectStructureLeaseScopeKind.Project,
                projectId.ToString("D"),
                request,
                async cancellationToken =>
                {
                    EnsureProjectWriteAllowed(accessState, projectId);
                    await EnsureTaskFreeTargetsAsync(accessState, projectId, request.NodeIds, includeDescendants: true, cancellationToken);
                    var count = await agentService.DeleteNodesAsync(projectId, request, BuildAgentContext(agent, accessState, projectId), cancellationToken);
                    return new OperationCount(count);
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
                    EnsureAgentMetadataPayloadValid(request.MetadataJson);
                    return await agentService.CreateApprovalRequestAsync(projectId, request, BuildAgentContext(agent, accessState, projectId), cancellationToken);
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

        private Task<ProjectStructureAssetContentDescriptor> ProjectStructureAssetContentGetAsync(
            AgentDefinition agent,
            ProjectStructureAccessState accessState,
            Guid projectId,
            string nodeId,
            CancellationToken cancellationToken)
        {
            return ExecuteAsync(
                agent,
                "assets.get-content",
                projectId,
                nodeId,
                null,
                null,
                null,
                async cancellationToken =>
                {
                    EnsureProjectReadAllowed(accessState, projectId);
                    var content = await agentService.GetAssetContentAsync(projectId, nodeId, cancellationToken);
                    return ProjectStructureAgentRuntimeAssetContentSanitizer.BoundForAgentRuntime(content);
                },
                cancellationToken);
        }

        private Task<ProjectStructureNodeSummary> ProjectStructureAssetCreateAsync(
            AgentDefinition agent,
            ProjectStructureAccessState accessState,
            Guid projectId,
            ProjectStructureAgentAssetCreateInput request,
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
                    var effectiveRequest = NormalizeGovernedProcessCreateParent(
                        accessState,
                        request.ToServiceRequest());
                    return await agentService.CreateAssetAsync(projectId, effectiveRequest, BuildAgentContext(agent, accessState, projectId), cancellationToken);
                },
                cancellationToken);
        }

        private Task<ProjectStructureAssetDescriptor> ProjectStructureAssetCreateRevisionAsync(
            AgentDefinition agent,
            ProjectStructureAccessState accessState,
            Guid projectId,
            string nodeId,
            ProjectStructureAgentAssetRevisionRequest request,
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
                    await EnsureTaskFreeTargetsAsync(accessState, projectId, [nodeId], includeDescendants: false, cancellationToken);
                    return await agentService.CreateAssetRevisionAsync(
                        projectId,
                        nodeId,
                        request.ToServiceRequest(),
                        BuildAgentContext(agent, accessState, projectId),
                        cancellationToken);
                },
                cancellationToken);
        }

        private Task<ProjectStructureLinkChangeResult> ProjectStructureLinkCreateAsync(
            AgentDefinition agent,
            ProjectStructureAccessState accessState,
            Guid projectId,
            ProjectStructureLinkInput request,
            int? estimatedMinutes,
            CancellationToken cancellationToken)
        {
            return ExecuteAsync(
                agent,
                "structure.link-create",
                projectId,
                request.SourceNodeId,
                ProjectStructureLeaseScopeKind.Project,
                projectId.ToString("D"),
                request,
                async cancellationToken =>
                {
                    EnsureProjectWriteAllowed(accessState, projectId);
                    await EnsureTaskFreeTargetsAsync(
                        accessState,
                        projectId,
                        [request.SourceNodeId, request.TargetNodeId],
                        includeDescendants: false,
                        cancellationToken);
                    return await agentService.LinkNodesAsync(projectId, request, BuildAgentContext(agent, accessState, projectId), cancellationToken);
                },
                cancellationToken);
        }

        private Task<ProjectStructureLinkChangeResult> ProjectStructureLinkUnlinkAsync(
            AgentDefinition agent,
            ProjectStructureAccessState accessState,
            Guid projectId,
            ProjectStructureLinkInput request,
            int? estimatedMinutes,
            CancellationToken cancellationToken)
        {
            return ExecuteAsync(
                agent,
                "structure.link-delete",
                projectId,
                request.SourceNodeId,
                ProjectStructureLeaseScopeKind.Project,
                projectId.ToString("D"),
                request,
                async cancellationToken =>
                {
                    EnsureProjectWriteAllowed(accessState, projectId);
                    await EnsureTaskFreeTargetsAsync(
                        accessState,
                        projectId,
                        [request.SourceNodeId, request.TargetNodeId],
                        includeDescendants: false,
                        cancellationToken);
                    return await agentService.UnlinkNodesAsync(projectId, request, BuildAgentContext(agent, accessState, projectId), cancellationToken);
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
                    ProjectStructureNonTaskWritePolicy.EnsureImportAllowed(
                        accessState.RequiresNonTaskWriteGuard,
                        request.LeafWorkItemSubtype);
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

                    return response with
                    {
                        Entries = response.Entries
                            .Where(entry => !entry.ProjectId.HasValue ||
                                IsProjectAllowed(accessState, entry.ProjectId.Value))
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
                    var resolvedDurationMinutes = ResolveExplicitLeaseDuration(accessState, durationMinutes);
                    return await leaseService.AcquireAsync(
                        new ProjectStructureLeaseAcquireRequest(
                            ProjectStructureLeaseScopeKind.Project,
                            projectId.ToString("D"),
                            reason,
                            resolvedDurationMinutes),
                        BuildAgentContext(agent, accessState, projectId),
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
                    var resolvedDurationMinutes = ResolveExplicitLeaseDuration(accessState, durationMinutes);
                    return await leaseService.AcquireAsync(
                        new ProjectStructureLeaseAcquireRequest(
                            ProjectStructureLeaseScopeKind.RepoBranch,
                            scopeKey,
                            reason,
                            resolvedDurationMinutes),
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

        private async Task<ProjectStructureLeaseSnapshot> ProjectStructureLeaseRenewAsync(
            AgentDefinition agent,
            ProjectStructureAccessState accessState,
            ProjectStructureScopeInput scope,
            string leaseToken,
            int durationMinutes,
            CancellationToken cancellationToken)
        {
            var resolvedScope = await ResolveScopeAsync(agent, accessState, scope, true, cancellationToken);
            return await ExecuteAsync(
                agent,
                "leases.renew",
                resolvedScope.ProjectId,
                null,
                resolvedScope.ScopeKind,
                resolvedScope.ScopeKey,
                new { scope, leaseToken, durationMinutes },
                async cancellationToken =>
                {
                    var context = BuildAgentContext(agent, accessState, resolvedScope.ProjectId, resolvedScope.BranchName, resolvedScope.RepositoryRoot);
                    var resolvedDurationMinutes = ResolveExplicitLeaseDuration(accessState, durationMinutes);
                    return await leaseService.RenewAsync(
                        new ProjectStructureLeaseRenewRequest(
                            resolvedScope.ScopeKind,
                            resolvedScope.ScopeKey,
                            leaseToken,
                            resolvedDurationMinutes),
                        context,
                        cancellationToken);
                },
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
                    var context = BuildAgentContext(agent, accessState, resolvedScope.ProjectId, resolvedScope.BranchName, resolvedScope.RepositoryRoot);
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
                        ProjectStructureAnalyticsService.SerializeResponseSummary(response)),
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
                when (SerializableMutationScope.IsConflict(exception))
            {
                const string errorCode =
                    "ProjectStructureConcurrentMutation";
                const string message =
                    "The project structure changed concurrently. Reload the authoritative project state and retry the mutation.";
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
                        errorCode,
                        message,
                        ProjectStructureAnalyticsService.SerializeSummary(
                            requestSummary),
                        ProjectStructureAnalyticsService.SerializeSummary(
                            new
                            {
                                FailureType = exception.GetType().Name
                            })),
                    cancellationToken);
                throw ProjectStructureAgentException.CreateAgentVisible(
                    409,
                    errorCode,
                    message,
                    canRetryWithCorrectedInput: true,
                    diagnosticDetails: new
                    {
                        FailureType = exception.GetType().Name
                    });
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

        private static void EnsureAgentMetadataPayloadValid(string? metadataJson)
        {
            if (!ProjectWorkbenchObjectModeling.HasMeaningfulMetadata(metadataJson))
            {
                return;
            }

            try
            {
                _ = ProjectObjectMetadataSerializer.Parse(metadataJson);
            }
            catch (ProjectObjectMetadataPayloadException exception)
            {
                const string errorCode = "InvalidProjectObjectMetadata";
                var jsonPath = string.IsNullOrWhiteSpace(exception.JsonPath)
                    ? "$"
                    : exception.JsonPath;
                var message =
                    $"request.metadataJson has an incompatible value at '{jsonPath}'. Omit metadataJson unless this tool explicitly documents a typed metadata envelope, then retry with the documented shape.";
                throw ProjectStructureAgentException.CreateAgentVisible(
                    400,
                    errorCode,
                    message,
                    canRetryWithCorrectedInput: true,
                    diagnosticDetails: new
                    {
                        exception.JsonPath,
                        FailureType = exception.GetType().Name
                    });
            }
        }

        private async Task<ProjectStructureNodeSummary?> TryReuseGovernedProcessNodeCreateAsync(
            AgentDefinition agent,
            ProjectStructureAccessState accessState,
            Guid projectId,
            ProjectStructureNodeCreateInput request,
            CancellationToken cancellationToken)
        {
            if (accessState.ScopedProcessAccess is null ||
                string.IsNullOrWhiteSpace(request.ParentNodeKey))
            {
                return null;
            }

            var normalizedTitle = NormalizeNodeCreateNaturalKeyText(request.Title);
            if (string.IsNullOrWhiteSpace(normalizedTitle))
            {
                return null;
            }

            var normalizedSubtype = ProjectStructureRequestedNodeKindParser.NormalizeSubtypeForType(
                request.ObjectType,
                request.ObjectSubtype) ?? string.Empty;
            var structure = await agentService.GetStructureAsync(
                projectId,
                new ProjectStructureReadRequest(
                    IncludeLinks: false,
                    IncludeLayout: true,
                    IncludeMetadata: true,
                    IncludeNotes: true,
                    IncludeAssets: true),
                cancellationToken);
            var existingNode = structure.Nodes
                .Where(node =>
                    string.Equals(node.ParentId, request.ParentNodeKey, StringComparison.Ordinal) &&
                    node.ObjectType == request.ObjectType &&
                    string.Equals(node.ObjectSubtype, normalizedSubtype, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(NormalizeNodeCreateNaturalKeyText(node.Title), normalizedTitle, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(node => node.Id, StringComparer.Ordinal)
                .FirstOrDefault();
            if (existingNode is null)
            {
                return null;
            }

            return await agentService.UpdateNodeAsync(
                projectId,
                existingNode.Id,
                new ProjectStructureNodeEditInput(
                    request.Title,
                    request.Subtitle,
                    request.Notes,
                    request.ObjectType,
                    normalizedSubtype,
                    request.StartUtc,
                    request.EndUtc,
                    request.MetadataJson,
                    request.LeaseToken,
                    request.DurationSeconds),
                BuildAgentContext(agent, accessState, projectId),
                cancellationToken);
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

        private ProjectStructureAgentContext BuildAgentContext(
            AgentDefinition agent,
            ProjectStructureAccessState accessState,
            Guid? projectId,
            string? branchName = null,
            string? repositoryRoot = null)
        {
            if (projectId.HasValue &&
                string.IsNullOrWhiteSpace(branchName) &&
                string.IsNullOrWhiteSpace(repositoryRoot) &&
                accessState.ScopedProcessAccess is { AgentContext: { } scopedAgentContext } scopedProcessAccess &&
                scopedProcessAccess.ProjectId == projectId.Value)
            {
                return scopedAgentContext;
            }

            return BuildAgentContext(agent, branchName, repositoryRoot);
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

            var candidateProjectIds = (accessState.AllowAllProjects
                ? (await agentService.ListProjectsAsync(cancellationToken))
                    .Select(project => project.Id)
                : accessState.AllowedProjectIds)
                .Where(projectId => IsProjectAllowed(accessState, projectId))
                .Distinct()
                .ToList();

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

        private static int ResolveExplicitLeaseDuration(ProjectStructureAccessState accessState, int requestedDurationMinutes)
        {
            return accessState.ScopedProcessAccess is null
                ? Math.Clamp(requestedDurationMinutes, 1, 120)
                : Math.Clamp(requestedDurationMinutes, 1, GovernedProcessMaxExplicitLeaseMinutes);
        }

        private async Task EnsureNodeUpdateAllowedAsync(
            ProjectStructureAccessState accessState,
            Guid projectId,
            string nodeId,
            ProjectObjectType? requestedObjectType,
            string? requestedObjectSubtype,
            CancellationToken cancellationToken)
        {
            var response = await agentService.GetStructureAsync(
                projectId,
                new ProjectStructureReadRequest(NodeIds: [nodeId]),
                cancellationToken);
            var node = response.Nodes.FirstOrDefault(item => string.Equals(item.Id, nodeId, StringComparison.Ordinal));
            if (node is null)
            {
                throw new ProjectStructureAgentException(
                    404,
                    "NodeNotFound",
                    $"Project-structure node '{nodeId}' was not found in project '{projectId:D}'.");
            }

            ProjectStructureCanonicalTaskMutationPolicy.EnsureGenericUpdateAllowed(
                node,
                requestedObjectType,
                requestedObjectSubtype);
            ProjectStructureNonTaskWritePolicy.EnsureNodeUpdateAllowed(
                accessState.RequiresNonTaskWriteGuard,
                node,
                requestedObjectType,
                requestedObjectSubtype);
        }

        private Task EnsureTaskFreeTargetsAsync(
            ProjectStructureAccessState accessState,
            Guid projectId,
            IEnumerable<string> nodeIds,
            bool includeDescendants,
            CancellationToken cancellationToken)
        {
            return EnsureTaskFreeTargetsAsync(
                accessState.RequiresNonTaskWriteGuard,
                projectId,
                nodeIds,
                includeDescendants,
                cancellationToken);
        }

        private async Task EnsureTaskFreeTargetsAsync(
            bool requiresNonTaskWriteGuard,
            Guid projectId,
            IEnumerable<string> nodeIds,
            bool includeDescendants,
            CancellationToken cancellationToken)
        {
            if (!requiresNonTaskWriteGuard)
            {
                return;
            }

            var normalizedNodeIds = nodeIds
                .Where(nodeId => !string.IsNullOrWhiteSpace(nodeId))
                .Select(nodeId => nodeId.Trim())
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            if (normalizedNodeIds.Length == 0)
            {
                return;
            }

            var response = await agentService.GetStructureAsync(
                projectId,
                includeDescendants
                    ? new ProjectStructureReadRequest(SubtreeRootIds: normalizedNodeIds)
                    : new ProjectStructureReadRequest(NodeIds: normalizedNodeIds),
                cancellationToken);
            ProjectStructureNonTaskWritePolicy.EnsureNodesAllowed(
                requiresNonTaskGuard: true,
                response.Nodes);
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
            if (accessState.CanWriteStructureUnscoped)
            {
                return;
            }

            throw new ProjectStructureAgentException(
                403,
                "ProjectStructureWriteDenied",
                "This agent is not allowed to write project structure. Enable write access in the agent settings.");
        }

        private static void EnsureProjectCreationAllowed(ProjectStructureAccessState accessState)
        {
            if (accessState.CanCreateProjects)
            {
                return;
            }

            throw new ProjectStructureAgentException(
                403,
                "ProjectCreationDenied",
                "This agent is not allowed to create standalone projects. Enable project creation in the agent settings.");
        }

        private static void EnsureSubprojectCreationAllowed(ProjectStructureAccessState accessState)
        {
            if (accessState.CanCreateSubprojects)
            {
                return;
            }

            throw new ProjectStructureAgentException(
                403,
                "SubprojectCreationDenied",
                "This agent is not allowed to create or attach subprojects. Enable subproject creation in the agent settings.");
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

        private static void EnsureProjectTaskWriteAllowed(ProjectStructureAccessState accessState, Guid projectId)
        {
            if (!accessState.CanWriteTasksUnscoped)
            {
                throw new ProjectStructureAgentException(
                    403,
                    "ProjectTaskWriteDenied",
                    "This agent is not allowed to create or update project tasks. Enable task write access in the agent settings.");
            }

            EnsureProjectAllowed(accessState, projectId);
        }

        private static ProjectStructureScopedProcessAccess EnsureScopedProcessExternalActionAllowed(ProjectStructureAccessState accessState)
        {
            if (accessState.ScopedProcessAccess is { CanWrite: true } scopedProcessAccess)
            {
                return scopedProcessAccess;
            }

            throw new ProjectStructureAgentException(
                403,
                "ProcessSubprocessLaunchDenied",
                $"Launching a child process from project structure requires governed process automation with {ProcessOperationContractNames.ExecuteExternalAction}.");
        }

        private static void EnsureProjectAllowed(ProjectStructureAccessState accessState, Guid projectId)
        {
            EnsureProjectAllowedForContext(
                accessState.Purpose,
                accessState.ContextIntent,
                accessState.AllowAllProjects,
                accessState.AllowedProjectIds,
                accessState.SessionCreatedProjectIds,
                projectId);
        }

        private static bool IsProjectAllowed(ProjectStructureAccessState accessState, Guid projectId)
        {
            return IsProjectAllowedForContext(
                accessState.Purpose,
                accessState.ContextIntent,
                accessState.AllowAllProjects,
                accessState.AllowedProjectIds,
                accessState.SessionCreatedProjectIds,
                projectId);
        }

        private static void GrantSessionCreatedProjectAccess(
            ProjectStructureAccessState accessState,
            Guid projectId)
        {
            accessState.AllowedProjectIds.Add(projectId);
            accessState.SessionCreatedProjectIds.Add(projectId);
        }

        private static ProjectStructureScopedProcessAccess? ResolveScopedProcessAccess(AgentRuntimeToolProviderContext context)
        {
            if (context.Purpose != AgentRuntimeToolProviderPurpose.GovernedProcessAutomation ||
                WorkspaceExecutionAuditContext.Current is not { } auditScope ||
                string.IsNullOrWhiteSpace(auditScope.ProcessRunId) ||
                string.IsNullOrWhiteSpace(auditScope.ProcessStepId) ||
                !TryResolveScopedProcessProjectId(context, auditScope, out var projectId))
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
                : new ProjectStructureScopedProcessAccess(
                    projectId,
                    auditScope.ProcessRunId.Trim(),
                    auditScope.ProcessStepId.Trim(),
                    canRead,
                    canWrite,
                    MapScopedProcessAgentContext(auditScope.ProjectStructureLaunchAgent),
                    auditScope.ProjectStructureProcessNodeContext);
        }

        private static ProjectStructureNodeCreateInput NormalizeGovernedProcessCreateParent(
            ProjectStructureAccessState accessState,
            ProjectStructureNodeCreateInput request)
        {
            var parentNodeKey = NormalizeGovernedProcessCreateParent(accessState, request.ParentNodeKey);
            return string.Equals(parentNodeKey, request.ParentNodeKey, StringComparison.Ordinal)
                ? request
                : request with { ParentNodeKey = parentNodeKey };
        }

        private static ProjectStructureAssetCreateInput NormalizeGovernedProcessCreateParent(
            ProjectStructureAccessState accessState,
            ProjectStructureAssetCreateInput request)
        {
            var parentNodeKey = NormalizeGovernedProcessCreateParent(accessState, request.ParentNodeKey);
            return string.Equals(parentNodeKey, request.ParentNodeKey, StringComparison.Ordinal)
                ? request
                : request with { ParentNodeKey = parentNodeKey };
        }

        private static string? NormalizeGovernedProcessCreateParent(
            ProjectStructureAccessState accessState,
            string? requestedParentNodeKey)
            => ProjectStructureProcessParentNodePolicy.NormalizeCreateParentNodeKey(
                accessState.ScopedProcessAccess?.ProcessNodeContext,
                requestedParentNodeKey);

        private static string NormalizeNodeCreateNaturalKeyText(string? value)
            => string.Join(
                " ",
                (value ?? string.Empty)
                    .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

        private static bool TryResolveScopedProcessProjectId(
            AgentRuntimeToolProviderContext context,
            WorkspaceExecutionAuditContext.WorkspaceExecutionAuditScopeState auditScope,
            out Guid projectId)
        {
            if (TryResolveProjectScopeId(context.Tags, out projectId))
            {
                return true;
            }

            if (auditScope.ContextWorkspaceScope is { Kind: WorkspaceScopeKind.Project } scope &&
                Guid.TryParse(scope.Key, out projectId) &&
                projectId != Guid.Empty)
            {
                return true;
            }

            projectId = Guid.Empty;
            return false;
        }

        private static ProjectStructureAgentContext? MapScopedProcessAgentContext(ProjectStructureAgentIdentityDescriptor? descriptor)
        {
            return descriptor?.HasLeaseOwnerIdentity == true
                ? new ProjectStructureAgentContext(
                    descriptor.AgentId.Trim(),
                    string.IsNullOrWhiteSpace(descriptor.AgentName) ? "Unnamed agent" : descriptor.AgentName.Trim(),
                    descriptor.MachineName.Trim(),
                    descriptor.RepositoryRoot.Trim(),
                    descriptor.BranchName.Trim(),
                    descriptor.SessionId.Trim())
                : null;
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
                ProjectStructureProcessNodeStartResult processNodeStartResult => processNodeStartResult.Warnings,
                ProjectStructureProcessSubprocessLaunchResult subprocessLaunchResult => subprocessLaunchResult.Warnings,
                ProjectStructureWorkflowNodeCreateResult workflowNodeCreateResult => workflowNodeCreateResult.Warnings,
                ProjectStructureWorkflowAddOptionsResult workflowAddOptionsResult => workflowAddOptionsResult.Warnings,
                ProjectStructureWorkflowNodeStartResult workflowNodeStartResult => workflowNodeStartResult.Warnings,
                ProjectPlanSummary planSummary => planSummary.Warnings,
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
            ProjectStructureScopedProcessAccess? scopedProcessAccess,
            AgentRuntimeContextIntent contextIntent,
            AgentRuntimeToolProviderPurpose purpose,
            ProjectStructureInvocationSnapshotReadContext invocationSnapshotReadContext)
        {
            ArgumentNullException.ThrowIfNull(contextIntent);
            ArgumentNullException.ThrowIfNull(invocationSnapshotReadContext);

            var normalized = AgentProjectStructureAccessMetadata.Normalize(settings);
            CanRead = normalized.CanRead || scopedProcessAccess?.CanRead == true;
            CanWrite = ProjectStructureNonTaskWritePolicy.CanUseStructureMutationTools(normalized) || scopedProcessAccess?.CanWrite == true;
            CanWriteUnscoped = normalized.CanWrite;
            CanWriteStructureUnscoped = normalized.CanWrite || normalized.CanWriteNonTaskStructure;
            CanWriteTasksUnscoped = ProjectStructureNonTaskWritePolicy.CanUseTaskMutationTools(normalized);
            CanCreateProjects = normalized.CanCreateProjects;
            CanCreateSubprojects = normalized.CanCreateSubprojects;
            RequiresNonTaskWriteGuard = normalized.CanWriteNonTaskStructure &&
                !normalized.CanWrite &&
                scopedProcessAccess?.CanWrite != true;
            AllowAllProjects = normalized.AllowAllProjects;
            AllowedProjectIds = normalized.AllowedProjectIds.ToHashSet();
            SessionCreatedProjectIds = [];
            ScopedProcessAccess = scopedProcessAccess;
            ContextIntent = contextIntent;
            Purpose = purpose;
            InvocationSnapshotReadContext = invocationSnapshotReadContext;
            if (scopedProcessAccess is not null)
            {
                AllowedProjectIds.Add(scopedProcessAccess.ProjectId);
            }
        }

        public bool CanRead { get; }

        public bool CanWrite { get; }

        public bool CanWriteUnscoped { get; }

        public bool CanWriteStructureUnscoped { get; }

        public bool CanWriteTasksUnscoped { get; }

        public bool CanCreateProjects { get; }

        public bool CanCreateSubprojects { get; }

        public bool RequiresNonTaskWriteGuard { get; }

        public bool AllowAllProjects { get; }

        public HashSet<Guid> AllowedProjectIds { get; }

        public HashSet<Guid> SessionCreatedProjectIds { get; }

        public ProjectStructureScopedProcessAccess? ScopedProcessAccess { get; }

        public AgentRuntimeContextIntent ContextIntent { get; }

        public AgentRuntimeToolProviderPurpose Purpose { get; }

        public ProjectStructureInvocationSnapshotReadContext InvocationSnapshotReadContext { get; }
    }

    private sealed record ProjectStructureScopedProcessAccess(
        Guid ProjectId,
        string ProcessRunId,
        string ProcessStepId,
        bool CanRead,
        bool CanWrite,
        ProjectStructureAgentContext? AgentContext,
        ProjectStructureProcessNodeContextDescriptor? ProcessNodeContext);

    private sealed record ProjectStructureResolvedScope(
        ProjectStructureLeaseScopeKind ScopeKind,
        string ScopeKey,
        Guid? ProjectId,
        string BranchName,
        string? RepositoryRoot);
}

internal static class ProjectStructureAgentRuntimeAssetContentSanitizer
{
    private const long MaxInlineAgentAssetContentBytes = 32 * 1024;

    public static ProjectStructureAssetContentDescriptor BoundForAgentRuntime(
        ProjectStructureAssetContentDescriptor content)
    {
        ArgumentNullException.ThrowIfNull(content);

        if (ShouldInlineAssetContent(content))
        {
            return content with
            {
                Base64DataOmitted = false,
                ContentSummary = $"Base64Data contains {content.ContentLength:N0} byte(s) from a small non-media asset."
            };
        }

        var mediaPath = string.IsNullOrWhiteSpace(content.Asset.MediaRelativePath)
            ? "the returned asset media path"
            : content.Asset.MediaRelativePath;
        var reason = IsBinaryMediaContentType(content.Asset.MediaContentType)
            ? $"Base64Data is omitted because '{content.Asset.MediaContentType}' is binary media."
            : $"Base64Data is omitted because the asset is {content.ContentLength:N0} byte(s), exceeding the {MaxInlineAgentAssetContentBytes:N0}-byte runtime inline limit.";
        var nextAction = ResolveOmittedContentNextAction(content.Asset.MediaContentType, mediaPath);

        return content with
        {
            Base64Data = string.Empty,
            Base64DataOmitted = true,
            ContentSummary = $"{reason} {nextAction}"
        };
    }

    private static bool ShouldInlineAssetContent(ProjectStructureAssetContentDescriptor content)
    {
        return content.ContentLength <= MaxInlineAgentAssetContentBytes &&
               !IsBinaryMediaContentType(content.Asset.MediaContentType);
    }

    private static bool IsBinaryMediaContentType(string contentType)
    {
        return IsImageContentType(contentType) ||
               contentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase) ||
               contentType.StartsWith("audio/", StringComparison.OrdinalIgnoreCase) ||
               contentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolveOmittedContentNextAction(string contentType, string mediaPath)
    {
        if (IsImageContentType(contentType))
        {
            return $"Use workspace_inspect_image or workspace_analyze_image with '{mediaPath}' when visual evidence is required.";
        }

        if (contentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase) ||
            contentType.Contains("wordprocessingml.document", StringComparison.OrdinalIgnoreCase) ||
            contentType.Equals("application/msword", StringComparison.OrdinalIgnoreCase) ||
            contentType.Equals("text/markdown", StringComparison.OrdinalIgnoreCase))
        {
            return $"Use workspace_convert_document with '{mediaPath}' and analyze the returned markdown preview or output path.";
        }

        if (IsSpreadsheetContentType(contentType))
        {
            return $"Use workspace_inspect_spreadsheet or workspace_spreadsheet_summary with '{mediaPath}', then use workspace_read_spreadsheet_range when tabular content is required.";
        }

        return $"Use a bounded workspace tool against '{mediaPath}' only when the step contract requires inspecting the asset bytes.";
    }

    private static bool IsImageContentType(string contentType)
        => contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);

    private static bool IsSpreadsheetContentType(string contentType)
    {
        return contentType.Contains("spreadsheetml.sheet", StringComparison.OrdinalIgnoreCase) ||
               contentType.Contains("ms-excel", StringComparison.OrdinalIgnoreCase) ||
               contentType.Equals("text/csv", StringComparison.OrdinalIgnoreCase) ||
               contentType.Equals("text/tab-separated-values", StringComparison.OrdinalIgnoreCase);
    }
}

public sealed record OperationAck(bool Ok);

public sealed record OperationCount(int Count);

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
    IReadOnlyList<string> Warnings,
    ProjectStructureReadSource Source = ProjectStructureReadSource.CanonicalCurrent);
