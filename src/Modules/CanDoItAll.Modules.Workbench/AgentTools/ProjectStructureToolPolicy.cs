using CanDoItAll.AgentFramework.Capabilities.Abstractions;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Core;
using static CanDoItAll.AgentFramework.Core.ToolCapabilityMetadataFactory;

namespace CanDoItAll.Modules.Workbench;

public static class ProjectStructureToolPolicy {
    private static readonly ToolCapabilityOperationEffects ExternalActionEffects = new(
        [ProcessOperationContractNames.ExternalActionControlled], canExecuteExternalAction: true);

    public const string ProjectStructureProjectsList = "project_structure_projects_list";
    public const string ProjectStructureProjectCreate = "project_structure_project_create";
    public const string ProjectStructureProjectUpdate = "project_structure_project_update";
    public const string ProjectStructureHierarchyGet = "project_structure_hierarchy_get";
    public const string ProjectStructureSubprojectCreate = "project_structure_subproject_create";
    public const string ProjectStructureSubprojectLink = "project_structure_subproject_link";
    public const string ProjectStructureNodesToNewSubproject = "project_structure_nodes_to_new_subproject";
    public const string ProjectStructureRead = "project_structure_read";
    public const string ProjectStructureNodeCatalog = "project_structure_node_catalog";
    public const string ProjectStructureChecklist = "project_structure_checklist";
    public const string ProjectStructureDependenciesQuery = "project_structure_dependencies_query";
    public const string ProjectPlanSummaryGet = "project_plan_summary_get";
    public const string ProjectTaskCreate = "project_task_create";
    public const string ProjectTaskUpdate = "project_task_update";
    public const string ProjectTaskResourceAttach = "project_task_resource_attach";
    public const string ProjectStructureDependencyLink = "project_structure_dependency_link";
    public const string ProjectStructureDependencyUnlink = "project_structure_dependency_unlink";
    public const string ProjectStructureNodeCreate = "project_structure_node_create";
    public const string ProjectStructureNodeUpdate = "project_structure_node_update";
    public const string ProjectStructureNodeTypeUpdate = "project_structure_node_type_update";
    public const string ProjectStructureNodeMetadataUpdate = "project_structure_node_metadata_update";
    public const string ProjectStructureNodesStatusUpdate = "project_structure_nodes_status_update";
    public const string ProjectStructureNodeStatusUpdate = "project_structure_node_status_update";
    public const string ProjectStructureNodesProgressUpdate = "project_structure_nodes_progress_update";
    public const string ProjectStructureNodeProgressUpdate = "project_structure_node_progress_update";
    public const string ProjectStructureNodesMarkerUpdate = "project_structure_nodes_marker_update";
    public const string ProjectStructureNodeMarkerUpdate = "project_structure_node_marker_update";
    public const string ProjectStructureNodesPriorityUpdate = "project_structure_nodes_priority_update";
    public const string ProjectStructureNodePriorityUpdate = "project_structure_node_priority_update";
    public const string ProjectStructureNodeMove = "project_structure_node_move";
    public const string ProjectStructureNodeRecompose = "project_structure_node_recompose";
    public const string ProjectStructureNodeReparent = "project_structure_node_reparent";
    public const string ProjectStructureNodesCopy = "project_structure_nodes_copy";
    public const string ProjectStructureNodeDescendantsToProjectMove = "project_structure_node_descendants_to_project_move";
    public const string ProjectStructureNodeCommandExecute = "project_structure_node_command_execute";
    public const string ProjectStructureNodeProcessDefinitionLink = "project_structure_node_process_definition_link";
    public const string ProjectStructureNodeProcessStart = "project_structure_node_process_start";
    public const string ProjectStructureProcessSubprocessLaunch = "project_structure_process_subprocess_launch";
    public const string ProjectStructureNodeWorkflowAddOptions = "project_structure_node_workflow_add_options";
    public const string ProjectStructureNodeWorkflowDefinitionCreate = "project_structure_node_workflow_definition_create";
    public const string ProjectStructureNodeWorkflowStart = "project_structure_node_workflow_start";
    public const string ProjectStructureNodeWorkflowStatusGet = "project_structure_node_workflow_status_get";
    public const string ProjectStructureNodeDelete = "project_structure_node_delete";
    public const string ProjectStructureNodesDelete = "project_structure_nodes_delete";
    public const string ProjectStructureApprovalRequest = "project_structure_approval_request";
    public const string ProjectStructureAssetCreate = "project_structure_asset_create";
    public const string ProjectStructureAssetGet = "project_structure_asset_get";
    public const string ProjectStructureAssetContentGet = "project_structure_asset_content_get";
    public const string ProjectStructureAssetTextGet = "project_structure_asset_text_get";
    public const string ProjectStructureAssetImageAnalyze = "project_structure_asset_image_analyze";
    public const string ProjectStructureAssetCreateRevision = "project_structure_asset_create_revision";
    public const string ProjectStructureLinkCreate = "project_structure_link_create";
    public const string ProjectStructureLinkUnlink = "project_structure_link_unlink";
    public const string ProjectStructureImport = "project_structure_import";
    public const string ProjectStructureKnowledgeQuery = "project_structure_knowledge_query";
    public const string ProjectStructureAnalyticsQuery = "project_structure_analytics_query";
    public const string ProjectStructureProjectLeaseAcquire = "project_structure_project_lease_acquire";
    public const string ProjectStructureRepoBranchLeaseAcquire = "project_structure_repo_branch_lease_acquire";
    public const string ProjectStructureLeaseGet = "project_structure_lease_get";
    public const string ProjectStructureLeaseRenew = "project_structure_lease_renew";
    public const string ProjectStructureLeaseRelease = "project_structure_lease_release";

    public static IReadOnlyList<ToolCapabilityMetadata> Capabilities { get; } = Array.AsReadOnly<ToolCapabilityMetadata>([
        Read(ProjectStructureProjectsList, ToolCapabilitySideEffectKind.WorkspaceRead),
        Mutation(ProjectStructureProjectCreate, ToolCapabilitySideEffectKind.ProjectStructureMutation, StaticRequirement(ProcessOperationContractNames.ExecuteExternalAction), effects: ExternalActionEffects) with { OperationClassifications = Classifications(CapabilityOperationClassification.ExternalAction, CapabilityOperationClassification.ScriptExecution) },
        Mutation(ProjectStructureProjectUpdate, ToolCapabilitySideEffectKind.ProjectStructureMutation, StaticRequirement(ProcessOperationContractNames.ExecuteExternalAction), effects: ExternalActionEffects) with { OperationClassifications = Classifications(CapabilityOperationClassification.ExternalAction, CapabilityOperationClassification.ScriptExecution) },
        Read(ProjectStructureHierarchyGet, ToolCapabilitySideEffectKind.WorkspaceRead),
        Mutation(ProjectStructureSubprojectCreate, ToolCapabilitySideEffectKind.ProjectStructureMutation, StaticRequirement(ProcessOperationContractNames.ExecuteExternalAction), effects: ExternalActionEffects) with { OperationClassifications = Classifications(CapabilityOperationClassification.ExternalAction, CapabilityOperationClassification.ScriptExecution) },
        Mutation(ProjectStructureSubprojectLink, ToolCapabilitySideEffectKind.ProjectStructureMutation, StaticRequirement(ProcessOperationContractNames.ExecuteExternalAction), effects: ExternalActionEffects) with { OperationClassifications = Classifications(CapabilityOperationClassification.ExternalAction, CapabilityOperationClassification.ScriptExecution) },
        Mutation(ProjectStructureNodesToNewSubproject, ToolCapabilitySideEffectKind.ProjectStructureMutation, StaticRequirement(ProcessOperationContractNames.ExecuteExternalAction), effects: ExternalActionEffects) with { OperationClassifications = Classifications(CapabilityOperationClassification.ExternalAction, CapabilityOperationClassification.ScriptExecution) },
        Read(ProjectStructureRead, ToolCapabilitySideEffectKind.WorkspaceRead),
        Read(ProjectStructureNodeCatalog, ToolCapabilitySideEffectKind.WorkspaceRead),
        Read(ProjectStructureChecklist, ToolCapabilitySideEffectKind.WorkspaceRead),
        Read(ProjectStructureDependenciesQuery, ToolCapabilitySideEffectKind.WorkspaceRead),
        Read(ProjectPlanSummaryGet, ToolCapabilitySideEffectKind.WorkspaceRead),
        Mutation(ProjectTaskCreate, ToolCapabilitySideEffectKind.ProjectStructureMutation, StaticRequirement(ProcessOperationContractNames.ExecuteExternalAction), effects: ExternalActionEffects) with { OperationClassifications = Classifications(CapabilityOperationClassification.ExternalAction, CapabilityOperationClassification.ScriptExecution) },
        Mutation(ProjectTaskUpdate, ToolCapabilitySideEffectKind.ProjectStructureMutation, StaticRequirement(ProcessOperationContractNames.ExecuteExternalAction), effects: ExternalActionEffects) with { OperationClassifications = Classifications(CapabilityOperationClassification.ExternalAction, CapabilityOperationClassification.ScriptExecution) },
        Mutation(ProjectTaskResourceAttach, ToolCapabilitySideEffectKind.ProjectStructureMutation, StaticRequirement(ProcessOperationContractNames.ExecuteExternalAction), effects: ExternalActionEffects) with { OperationClassifications = Classifications(CapabilityOperationClassification.ExternalAction, CapabilityOperationClassification.ScriptExecution) },
        Mutation(ProjectStructureDependencyLink, ToolCapabilitySideEffectKind.ProjectStructureMutation, StaticRequirement(ProcessOperationContractNames.ExecuteExternalAction), effects: ExternalActionEffects) with { OperationClassifications = Classifications(CapabilityOperationClassification.ExternalAction, CapabilityOperationClassification.ScriptExecution) },
        Mutation(ProjectStructureDependencyUnlink, ToolCapabilitySideEffectKind.ProjectStructureMutation, StaticRequirement(ProcessOperationContractNames.ExecuteExternalAction), effects: ExternalActionEffects) with { OperationClassifications = Classifications(CapabilityOperationClassification.ExternalAction, CapabilityOperationClassification.ScriptExecution) },
        Mutation(ProjectStructureNodeCreate, ToolCapabilitySideEffectKind.ProjectStructureMutation, StaticRequirement(ProcessOperationContractNames.ExecuteExternalAction), effects: ExternalActionEffects) with { OperationClassifications = Classifications(CapabilityOperationClassification.ExternalAction, CapabilityOperationClassification.ScriptExecution) },
        Mutation(ProjectStructureNodeUpdate, ToolCapabilitySideEffectKind.ProjectStructureMutation, StaticRequirement(ProcessOperationContractNames.ExecuteExternalAction), effects: ExternalActionEffects) with { OperationClassifications = Classifications(CapabilityOperationClassification.ExternalAction, CapabilityOperationClassification.ScriptExecution) },
        Mutation(ProjectStructureNodeTypeUpdate, ToolCapabilitySideEffectKind.ProjectStructureMutation, StaticRequirement(ProcessOperationContractNames.ExecuteExternalAction), effects: ExternalActionEffects) with { OperationClassifications = Classifications(CapabilityOperationClassification.ExternalAction, CapabilityOperationClassification.ScriptExecution) },
        Mutation(ProjectStructureNodeMetadataUpdate, ToolCapabilitySideEffectKind.ProjectStructureMutation, StaticRequirement(ProcessOperationContractNames.ExecuteExternalAction), effects: ExternalActionEffects) with { OperationClassifications = Classifications(CapabilityOperationClassification.ExternalAction, CapabilityOperationClassification.ScriptExecution) },
        Mutation(ProjectStructureNodesStatusUpdate, ToolCapabilitySideEffectKind.ProjectStructureMutation, StaticRequirement(ProcessOperationContractNames.ExecuteExternalAction), effects: ExternalActionEffects) with { OperationClassifications = Classifications(CapabilityOperationClassification.ExternalAction, CapabilityOperationClassification.ScriptExecution) },
        Mutation(ProjectStructureNodeStatusUpdate, ToolCapabilitySideEffectKind.ProjectStructureMutation, StaticRequirement(ProcessOperationContractNames.ExecuteExternalAction), effects: ExternalActionEffects) with { OperationClassifications = Classifications(CapabilityOperationClassification.ExternalAction, CapabilityOperationClassification.ScriptExecution) },
        Mutation(ProjectStructureNodesProgressUpdate, ToolCapabilitySideEffectKind.ProjectStructureMutation, StaticRequirement(ProcessOperationContractNames.ExecuteExternalAction), effects: ExternalActionEffects) with { OperationClassifications = Classifications(CapabilityOperationClassification.ExternalAction, CapabilityOperationClassification.ScriptExecution) },
        Mutation(ProjectStructureNodeProgressUpdate, ToolCapabilitySideEffectKind.ProjectStructureMutation, StaticRequirement(ProcessOperationContractNames.ExecuteExternalAction), effects: ExternalActionEffects) with { OperationClassifications = Classifications(CapabilityOperationClassification.ExternalAction, CapabilityOperationClassification.ScriptExecution) },
        Mutation(ProjectStructureNodesMarkerUpdate, ToolCapabilitySideEffectKind.ProjectStructureMutation, StaticRequirement(ProcessOperationContractNames.ExecuteExternalAction), effects: ExternalActionEffects) with { OperationClassifications = Classifications(CapabilityOperationClassification.ExternalAction, CapabilityOperationClassification.ScriptExecution) },
        Mutation(ProjectStructureNodeMarkerUpdate, ToolCapabilitySideEffectKind.ProjectStructureMutation, StaticRequirement(ProcessOperationContractNames.ExecuteExternalAction), effects: ExternalActionEffects) with { OperationClassifications = Classifications(CapabilityOperationClassification.ExternalAction, CapabilityOperationClassification.ScriptExecution) },
        Mutation(ProjectStructureNodesPriorityUpdate, ToolCapabilitySideEffectKind.ProjectStructureMutation, StaticRequirement(ProcessOperationContractNames.ExecuteExternalAction), effects: ExternalActionEffects) with { OperationClassifications = Classifications(CapabilityOperationClassification.ExternalAction, CapabilityOperationClassification.ScriptExecution) },
        Mutation(ProjectStructureNodePriorityUpdate, ToolCapabilitySideEffectKind.ProjectStructureMutation, StaticRequirement(ProcessOperationContractNames.ExecuteExternalAction), effects: ExternalActionEffects) with { OperationClassifications = Classifications(CapabilityOperationClassification.ExternalAction, CapabilityOperationClassification.ScriptExecution) },
        Mutation(ProjectStructureNodeMove, ToolCapabilitySideEffectKind.ProjectStructureMutation, StaticRequirement(ProcessOperationContractNames.ExecuteExternalAction), effects: ExternalActionEffects) with { OperationClassifications = Classifications(CapabilityOperationClassification.ExternalAction, CapabilityOperationClassification.ScriptExecution) },
        Mutation(ProjectStructureNodeRecompose, ToolCapabilitySideEffectKind.ProjectStructureMutation, StaticRequirement(ProcessOperationContractNames.ExecuteExternalAction), effects: ExternalActionEffects) with { OperationClassifications = Classifications(CapabilityOperationClassification.ExternalAction, CapabilityOperationClassification.ScriptExecution) },
        Mutation(ProjectStructureNodeReparent, ToolCapabilitySideEffectKind.ProjectStructureMutation, StaticRequirement(ProcessOperationContractNames.ExecuteExternalAction), effects: ExternalActionEffects) with { OperationClassifications = Classifications(CapabilityOperationClassification.ExternalAction, CapabilityOperationClassification.ScriptExecution) },
        Mutation(ProjectStructureNodesCopy, ToolCapabilitySideEffectKind.ProjectStructureMutation, StaticRequirement(ProcessOperationContractNames.ExecuteExternalAction), effects: ExternalActionEffects) with { OperationClassifications = Classifications(CapabilityOperationClassification.ExternalAction, CapabilityOperationClassification.ScriptExecution) },
        Mutation(ProjectStructureNodeDescendantsToProjectMove, ToolCapabilitySideEffectKind.ProjectStructureMutation, StaticRequirement(ProcessOperationContractNames.ExecuteExternalAction), effects: ExternalActionEffects) with { OperationClassifications = Classifications(CapabilityOperationClassification.ExternalAction, CapabilityOperationClassification.ScriptExecution) },
        Mutation(ProjectStructureNodeCommandExecute, ToolCapabilitySideEffectKind.ProjectStructureMutation, StaticRequirement(ProcessOperationContractNames.ExecuteExternalAction), effects: ExternalActionEffects) with { OperationClassifications = Classifications(CapabilityOperationClassification.ExternalAction, CapabilityOperationClassification.ScriptExecution) },
        Mutation(ProjectStructureNodeProcessDefinitionLink, ToolCapabilitySideEffectKind.ProjectStructureMutation, StaticRequirement(ProcessOperationContractNames.ExecuteExternalAction), effects: ExternalActionEffects) with { OperationClassifications = Classifications(CapabilityOperationClassification.ExternalAction, CapabilityOperationClassification.ScriptExecution) },
        Mutation(ProjectStructureNodeProcessStart, ToolCapabilitySideEffectKind.ProjectStructureMutation, StaticRequirement(ProcessOperationContractNames.StartProjectNodeProcess), effects: ExternalActionEffects) with { OperationClassifications = Classifications(CapabilityOperationClassification.ProjectStructure, CapabilityOperationClassification.ExternalAction, CapabilityOperationClassification.ScriptExecution) },
        Mutation(ProjectStructureProcessSubprocessLaunch, ToolCapabilitySideEffectKind.ProjectStructureMutation, StaticRequirement(ProcessOperationContractNames.ExecuteExternalAction), effects: ExternalActionEffects) with { OperationClassifications = Classifications(CapabilityOperationClassification.ExternalAction, CapabilityOperationClassification.ScriptExecution) },
        Read(ProjectStructureNodeWorkflowAddOptions, ToolCapabilitySideEffectKind.WorkspaceRead),
        Mutation(ProjectStructureNodeWorkflowDefinitionCreate, ToolCapabilitySideEffectKind.ProjectStructureMutation, StaticRequirement(ProcessOperationContractNames.ExecuteExternalAction), effects: ExternalActionEffects) with { OperationClassifications = Classifications(CapabilityOperationClassification.ExternalAction, CapabilityOperationClassification.ScriptExecution) },
        Mutation(ProjectStructureNodeWorkflowStart, ToolCapabilitySideEffectKind.ProjectStructureMutation, StaticRequirement(ProcessOperationContractNames.ExecuteExternalAction), effects: ExternalActionEffects) with { OperationClassifications = Classifications(CapabilityOperationClassification.ExternalAction, CapabilityOperationClassification.ScriptExecution) },
        Read(ProjectStructureNodeWorkflowStatusGet, ToolCapabilitySideEffectKind.WorkspaceRead),
        Mutation(ProjectStructureNodeDelete, ToolCapabilitySideEffectKind.ProjectStructureMutation, StaticRequirement(ProcessOperationContractNames.ExecuteExternalAction), effects: ExternalActionEffects) with { OperationClassifications = Classifications(CapabilityOperationClassification.ExternalAction, CapabilityOperationClassification.ScriptExecution) },
        Mutation(ProjectStructureNodesDelete, ToolCapabilitySideEffectKind.ProjectStructureMutation, StaticRequirement(ProcessOperationContractNames.ExecuteExternalAction), effects: ExternalActionEffects) with { OperationClassifications = Classifications(CapabilityOperationClassification.ExternalAction, CapabilityOperationClassification.ScriptExecution) },
        Mutation(ProjectStructureApprovalRequest, ToolCapabilitySideEffectKind.ProjectStructureMutation, StaticRequirement(ProcessOperationContractNames.EscalateOrDecide, ProcessOperationContractNames.ExecuteExternalAction), effects: ExternalActionEffects) with { OperationClassifications = Classifications(CapabilityOperationClassification.Read, CapabilityOperationClassification.ExternalAction, CapabilityOperationClassification.ScriptExecution) },
        Mutation(ProjectStructureAssetCreate, ToolCapabilitySideEffectKind.ProjectStructureMutation, StaticRequirement(ProcessOperationContractNames.ExecuteExternalAction), effects: ExternalActionEffects) with { OperationClassifications = Classifications(CapabilityOperationClassification.ExternalAction, CapabilityOperationClassification.ScriptExecution) },
        Read(ProjectStructureAssetGet, ToolCapabilitySideEffectKind.WorkspaceRead),
        Read(ProjectStructureAssetContentGet, ToolCapabilitySideEffectKind.WorkspaceRead),
        Read(ProjectStructureAssetTextGet, ToolCapabilitySideEffectKind.WorkspaceRead),
        Read(ProjectStructureAssetImageAnalyze, ToolCapabilitySideEffectKind.WorkspaceRead),
        Mutation(ProjectStructureAssetCreateRevision, ToolCapabilitySideEffectKind.ProjectStructureMutation, StaticRequirement(ProcessOperationContractNames.ExecuteExternalAction), effects: ExternalActionEffects) with { OperationClassifications = Classifications(CapabilityOperationClassification.ExternalAction, CapabilityOperationClassification.ScriptExecution) },
        Mutation(ProjectStructureLinkCreate, ToolCapabilitySideEffectKind.ProjectStructureMutation, StaticRequirement(ProcessOperationContractNames.ExecuteExternalAction), effects: ExternalActionEffects) with { OperationClassifications = Classifications(CapabilityOperationClassification.ExternalAction, CapabilityOperationClassification.ScriptExecution) },
        Mutation(ProjectStructureLinkUnlink, ToolCapabilitySideEffectKind.ProjectStructureMutation, StaticRequirement(ProcessOperationContractNames.ExecuteExternalAction), effects: ExternalActionEffects) with { OperationClassifications = Classifications(CapabilityOperationClassification.ExternalAction, CapabilityOperationClassification.ScriptExecution) },
        Mutation(ProjectStructureImport, ToolCapabilitySideEffectKind.ProjectStructureMutation, StaticRequirement(ProcessOperationContractNames.ExecuteExternalAction), effects: ExternalActionEffects) with { OperationClassifications = Classifications(CapabilityOperationClassification.ExternalAction, CapabilityOperationClassification.ScriptExecution) },
        Read(ProjectStructureKnowledgeQuery, ToolCapabilitySideEffectKind.WorkspaceRead),
        Read(ProjectStructureAnalyticsQuery, ToolCapabilitySideEffectKind.WorkspaceRead),
        Mutation(ProjectStructureProjectLeaseAcquire, ToolCapabilitySideEffectKind.ProjectStructureMutation, StaticRequirement(ProcessOperationContractNames.ExecuteExternalAction), effects: ExternalActionEffects) with { OperationClassifications = Classifications(CapabilityOperationClassification.ExternalAction, CapabilityOperationClassification.ScriptExecution) },
        Mutation(ProjectStructureRepoBranchLeaseAcquire, ToolCapabilitySideEffectKind.ProjectStructureMutation, StaticRequirement(ProcessOperationContractNames.ExecuteExternalAction), effects: ExternalActionEffects) with { OperationClassifications = Classifications(CapabilityOperationClassification.ExternalAction, CapabilityOperationClassification.ScriptExecution) },
        Read(ProjectStructureLeaseGet, ToolCapabilitySideEffectKind.WorkspaceRead),
        Mutation(ProjectStructureLeaseRenew, ToolCapabilitySideEffectKind.ProjectStructureMutation, StaticRequirement(ProcessOperationContractNames.ExecuteExternalAction), effects: ExternalActionEffects) with { OperationClassifications = Classifications(CapabilityOperationClassification.ExternalAction, CapabilityOperationClassification.ScriptExecution) },
        Mutation(ProjectStructureLeaseRelease, ToolCapabilitySideEffectKind.ProjectStructureMutation, StaticRequirement(ProcessOperationContractNames.ExecuteExternalAction), effects: ExternalActionEffects) with { OperationClassifications = Classifications(CapabilityOperationClassification.ExternalAction, CapabilityOperationClassification.ScriptExecution) }
    ]);

    public static IReadOnlyList<string> ReadTools { get; } = Array.AsReadOnly<string>([
        ProjectStructureProjectsList,
        ProjectStructureHierarchyGet,
        ProjectStructureRead,
        ProjectStructureNodeCatalog,
        ProjectStructureChecklist,
        ProjectStructureDependenciesQuery,
        ProjectPlanSummaryGet,
        ProjectStructureAssetGet,
        ProjectStructureAssetContentGet,
        ProjectStructureAssetTextGet,
        ProjectStructureAssetImageAnalyze,
        ProjectStructureNodeWorkflowAddOptions,
        ProjectStructureNodeWorkflowStatusGet,
        ProjectStructureKnowledgeQuery,
        ProjectStructureAnalyticsQuery,
        ProjectStructureLeaseGet
    ]);

    public static IReadOnlyList<string> MutationTools { get; } = Array.AsReadOnly<string>([
        ProjectStructureProjectCreate,
        ProjectStructureProjectUpdate,
        ProjectStructureSubprojectCreate,
        ProjectStructureSubprojectLink,
        ProjectStructureNodesToNewSubproject,
        ProjectTaskCreate,
        ProjectTaskUpdate,
        ProjectTaskResourceAttach,
        ProjectStructureDependencyLink,
        ProjectStructureDependencyUnlink,
        ProjectStructureNodeCreate,
        ProjectStructureNodeUpdate,
        ProjectStructureNodeTypeUpdate,
        ProjectStructureNodeMetadataUpdate,
        ProjectStructureNodesStatusUpdate,
        ProjectStructureNodeStatusUpdate,
        ProjectStructureNodesProgressUpdate,
        ProjectStructureNodeProgressUpdate,
        ProjectStructureNodesMarkerUpdate,
        ProjectStructureNodeMarkerUpdate,
        ProjectStructureNodesPriorityUpdate,
        ProjectStructureNodePriorityUpdate,
        ProjectStructureNodeMove,
        ProjectStructureNodeRecompose,
        ProjectStructureNodeReparent,
        ProjectStructureNodesCopy,
        ProjectStructureNodeDescendantsToProjectMove,
        ProjectStructureNodeCommandExecute,
        ProjectStructureNodeProcessDefinitionLink,
        ProjectStructureNodeProcessStart,
        ProjectStructureProcessSubprocessLaunch,
        ProjectStructureNodeWorkflowDefinitionCreate,
        ProjectStructureNodeWorkflowStart,
        ProjectStructureNodeDelete,
        ProjectStructureNodesDelete,
        ProjectStructureApprovalRequest,
        ProjectStructureAssetCreate,
        ProjectStructureAssetCreateRevision,
        ProjectStructureLinkCreate,
        ProjectStructureLinkUnlink,
        ProjectStructureImport,
        ProjectStructureProjectLeaseAcquire,
        ProjectStructureRepoBranchLeaseAcquire,
        ProjectStructureLeaseRenew,
        ProjectStructureLeaseRelease
    ]);

    public static bool IsMutation(string toolName) => MutationTools.Contains(toolName, StringComparer.OrdinalIgnoreCase);
}
