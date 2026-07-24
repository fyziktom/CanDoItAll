namespace CanDoItAll.AgentFramework.Core;

public static class ToolContractCatalog
{
    public const string WorkspaceListDirectory = "workspace_list_directory";
    public const string WorkspaceListFiles = "workspace_list_files";
    public const string WorkspaceSearch = "workspace_search";
    public const string WorkspaceReadFile = "workspace_read_file";
    public const string WorkspaceStatPath = "workspace_stat_path";
    public const string WorkspaceHashPath = "workspace_hash_path";
    public const string WorkspaceCreateDirectory = "workspace_create_directory";
    public const string WorkspaceWriteFile = "workspace_write_file";
    public const string WorkspaceAppendFile = "workspace_append_file";
    public const string WorkspaceCopyPath = "workspace_copy_path";
    public const string WorkspaceMovePath = "workspace_move_path";
    public const string WorkspaceDeletePath = "workspace_delete_path";
    public const string WorkspaceZipPath = "workspace_zip_path";
    public const string WorkspaceUnzipArchive = "workspace_unzip_archive";
    public const string WorkspaceDiffText = "workspace_diff_text";
    public const string WorkspaceDotNetNew = "workspace_dotnet_new";
    public const string WorkspaceDotNetRestore = "workspace_dotnet_restore";
    public const string WorkspaceDotNetBuild = "workspace_dotnet_build";
    public const string WorkspaceDotNetTest = "workspace_dotnet_test";
    public const string WorkspaceDotNetRun = "workspace_dotnet_run";
    public const string WorkspaceDotNetStop = "workspace_dotnet_stop";
    public const string WorkspacePowerShellRunScript = "workspace_pwsh_run_script";
    public const string WorkspacePythonRunFile = "workspace_python_run_file";
    public const string WorkspaceInspectImage = "workspace_inspect_image";
    public const string WorkspaceAnalyzeImage = "workspace_analyze_image";
    public const string WorkspaceAnalyzeImages = "workspace_analyze_images";
    public const string WorkspaceInspectSpreadsheet = "workspace_inspect_spreadsheet";
    public const string WorkspaceSpreadsheetSummary = "workspace_spreadsheet_summary";
    public const string WorkspaceReadSpreadsheetCell = "workspace_read_spreadsheet_cell";
    public const string WorkspaceReadSpreadsheetRange = "workspace_read_spreadsheet_range";
    public const string WorkspaceWriteSpreadsheet = "workspace_write_spreadsheet";
    public const string WorkspaceSpreadsheetFunctionCatalog = "workspace_spreadsheet_function_catalog";
    public const string WorkspaceConvertDocument = "workspace_convert_document";
    public const string WorkspaceCommandRun = "workspace_command_run";
    public const string WorkspaceExecutionBoundary = "workspace_execution_boundary";
    public const string WorkspaceGitDiff = "workspace_git_diff";
    public const string WorkspaceGitStatus = "workspace_git_status";
    public const string WorkspaceGitLog = "workspace_git_log";
    public const string WorkspaceGitShow = "workspace_git_show";
    public const string WorkspaceGitAdd = "workspace_git_add";
    public const string WorkspaceGitUnstage = "workspace_git_unstage";
    public const string WorkspaceGitCommit = "workspace_git_commit";
    public const string WorkspaceGitBranchCreate = "workspace_git_branch_create";
    public const string WorkspaceGitSwitch = "workspace_git_switch";
    public const string LocalMcpLaunch = "local_mcp_launch";

    public const string BrowserNavigate = "browser_navigate";
    public const string BrowserResize = "browser_resize";
    public const string BrowserConsoleMessages = "browser_console_messages";
    public const string BrowserEvaluate = "browser_evaluate";
    public const string BrowserNetworkRequests = "browser_network_requests";
    public const string BrowserSnapshot = "browser_snapshot";
    public const string BrowserTakeScreenshot = "browser_take_screenshot";
    public const string BrowserClick = "browser_click";
    public const string BrowserFillForm = "browser_fill_form";
    public const string BrowserSelectOption = "browser_select_option";
    public const string BrowserPressKey = "browser_press_key";
    public const string BrowserType = "browser_type";
    public const string BrowserDrag = "browser_drag";
    public const string BrowserWaitFor = "browser_wait_for";

    public const string PromptGalleryCatalogSearch = "prompt_gallery_catalog_search";
    public const string PromptGalleryItemEditorGet = "prompt_gallery_item_editor_get";
    public const string PromptGalleryDraftCreate = "prompt_gallery_draft_create";
    public const string PromptGalleryDraftUpdate = "prompt_gallery_draft_update";
    public const string PromptGalleryVersionCreate = "prompt_gallery_version_create";
    public const string WorkflowCuratorCatalogSearch = "workflow_curator_catalog_search";
    public const string WorkflowCuratorDefinitionEditorGet = "workflow_curator_definition_editor_get";
    public const string WorkflowCuratorAuthoringOptionsGet = "workflow_curator_authoring_options_get";
    public const string WorkflowCuratorDraftCreate = "workflow_curator_draft_create";
    public const string WorkflowCuratorDraftUpdate = "workflow_curator_draft_update";
    public const string WorkflowCuratorNodeUpdate = "workflow_curator_node_update";
    public const string WorkflowCuratorLifecycleChange = "workflow_curator_lifecycle_change";
    public const string CapabilityCuratorCatalogSearch = "capability_curator_catalog_search";
    public const string CapabilityCuratorEditorGet = "capability_curator_editor_get";
    public const string CapabilityCuratorAssignmentEditorGet = "capability_curator_assignment_editor_get";
    public const string CapabilityCuratorSave = "capability_curator_save";
    public const string CapabilityCuratorToolSetupTest = "capability_curator_tool_setup_test";
    public const string CapabilityCuratorMcpSetupTest = "capability_curator_mcp_setup_test";
    public const string CapabilityCuratorAssignmentUpdate = "capability_curator_assignment_update";
    public const string CapabilityCuratorVerify = "capability_curator_verify";
    public const string SchedulerWorkflowTargetsSearch = "scheduler_workflow_targets_search";
    public const string SchedulerWorkflowSchedulesSearch = "scheduler_workflow_schedules_search";
    public const string SchedulerWorkflowScheduleCreate = "scheduler_workflow_schedule_create";

    public static IReadOnlyList<string> WorkspaceToolNames { get; } =
    [
        WorkspaceListDirectory,
        WorkspaceListFiles,
        WorkspaceSearch,
        WorkspaceReadFile,
        WorkspaceStatPath,
        WorkspaceHashPath,
        WorkspaceCreateDirectory,
        WorkspaceWriteFile,
        WorkspaceAppendFile,
        WorkspaceCopyPath,
        WorkspaceMovePath,
        WorkspaceDeletePath,
        WorkspaceZipPath,
        WorkspaceUnzipArchive,
        WorkspaceDiffText,
        WorkspaceDotNetNew,
        WorkspaceDotNetRestore,
        WorkspaceDotNetBuild,
        WorkspaceDotNetTest,
        WorkspaceDotNetRun,
        WorkspaceDotNetStop,
        WorkspacePowerShellRunScript,
        WorkspacePythonRunFile,
        WorkspaceInspectImage,
        WorkspaceAnalyzeImage,
        WorkspaceAnalyzeImages,
        WorkspaceInspectSpreadsheet,
        WorkspaceSpreadsheetSummary,
        WorkspaceReadSpreadsheetCell,
        WorkspaceReadSpreadsheetRange,
        WorkspaceWriteSpreadsheet,
        WorkspaceSpreadsheetFunctionCatalog,
        WorkspaceConvertDocument,
        WorkspaceCommandRun,
        WorkspaceExecutionBoundary,
        WorkspaceGitDiff,
        WorkspaceGitStatus,
        WorkspaceGitLog,
        WorkspaceGitShow,
        WorkspaceGitAdd,
        WorkspaceGitUnstage,
        WorkspaceGitCommit,
        WorkspaceGitBranchCreate,
        WorkspaceGitSwitch,
        LocalMcpLaunch
    ];

    public static IReadOnlyList<string> BrowserToolNames { get; } =
    [
        BrowserNavigate,
        BrowserResize,
        BrowserConsoleMessages,
        BrowserEvaluate,
        BrowserNetworkRequests,
        BrowserSnapshot,
        BrowserTakeScreenshot,
        BrowserClick,
        BrowserFillForm,
        BrowserSelectOption,
        BrowserPressKey,
        BrowserType,
        BrowserDrag,
        BrowserWaitFor
    ];

    public static IReadOnlyList<string> BrowserEvidenceToolNames { get; } =
    [
        BrowserConsoleMessages,
        BrowserEvaluate,
        BrowserNetworkRequests,
        BrowserSnapshot,
        BrowserTakeScreenshot
    ];

    public static IReadOnlyList<string> FinalizerToolNames { get; } =
    [
        AgentFinalizerPolicies.SubmitProcessStepOutcomeToolName,
        AgentFinalizerPolicies.SubmitCodeReviewResultToolName,
        AgentFinalizerPolicies.SubmitArchitectureReviewResultToolName,
        AgentFinalizerPolicies.SubmitImplementationPlanToolName,
        AgentFinalizerPolicies.SubmitTestPlanToolName,
        AgentFinalizerPolicies.SubmitToolExecutionDecisionToolName,
        AgentFinalizerPolicies.SubmitProcessStatePatchToolName,
        AgentFinalizerPolicies.SubmitHumanEscalationRequestToolName
    ];

    public static IReadOnlyList<string> RepresentativeBrowserInteractionToolNames { get; } =
    [
        BrowserClick,
        BrowserFillForm,
        BrowserSelectOption,
        BrowserPressKey,
        BrowserType,
        BrowserDrag,
        BrowserEvaluate,
        BrowserWaitFor
    ];

    public static IReadOnlyList<string> DotNetValidationToolNames { get; } =
    [
        WorkspaceDotNetRestore,
        WorkspaceDotNetBuild,
        WorkspaceDotNetTest
    ];

    public static IReadOnlyList<string> KnownToolNames { get; } =
    [
        .. WorkspaceToolNames,
        .. BrowserToolNames,
        .. FinalizerToolNames,
        AgentToolInvocationPolicyMetadata.LoadSkill,
        AgentToolInvocationPolicyMetadata.ReadSkillResource,
        AgentToolInvocationPolicyMetadata.RunSkillScript,
        AgentToolInvocationPolicyMetadata.ProcessesDefinitionSave,
        AgentToolInvocationPolicyMetadata.ProcessesDefinitionRoleAdd,
        AgentToolInvocationPolicyMetadata.ProcessesDefinitionPublish,
        AgentToolInvocationPolicyMetadata.ProcessesDefinitionDelete,
        AgentToolInvocationPolicyMetadata.ProcessesDefinitionImport,
        AgentToolInvocationPolicyMetadata.ProcessesRunStart,
        AgentToolInvocationPolicyMetadata.ProcessesStepTransition,
        AgentToolInvocationPolicyMetadata.ProcessesAssignmentResolve,
        AgentToolInvocationPolicyMetadata.ProcessesArtifactRecord,
        AgentToolInvocationPolicyMetadata.ProcessesDefinitionsList,
        AgentToolInvocationPolicyMetadata.ProcessesDefinitionEditorGet,
        AgentToolInvocationPolicyMetadata.ProcessesDefinitionExport,
        AgentToolInvocationPolicyMetadata.ProcessesRunsList,
        AgentToolInvocationPolicyMetadata.ProcessesRunDetailGet,
        AgentToolInvocationPolicyMetadata.ProcessesAnalyticsGet,
        AgentToolInvocationPolicyMetadata.ProcessesPartyOptionsList,
        AgentToolInvocationPolicyMetadata.ProcessesExecutorOptionsList,
        AgentToolInvocationPolicyMetadata.ProcessesTemplatesList,
        AgentToolInvocationPolicyMetadata.ProcessesTemplateGet,
        AgentToolInvocationPolicyMetadata.ProcessesTemplateMermaidGet,
        AgentToolInvocationPolicyMetadata.ProcessesTemplateImport,
        AgentToolInvocationPolicyMetadata.ProcessesTemplateBaselineScenariosList,
        AgentToolInvocationPolicyMetadata.ProcessesTemplateLiveRunProfilesList,
        AgentToolInvocationPolicyMetadata.WorkflowsDefinitionsList,
        AgentToolInvocationPolicyMetadata.WorkflowsRunStart,
        AgentToolInvocationPolicyMetadata.WorkflowsRunStatusGet,
        AgentToolInvocationPolicyMetadata.WorkflowsRunCancel,
        AgentToolInvocationPolicyMetadata.WorkflowsExternalResponseSubmit,
        AgentToolInvocationPolicyMetadata.PromptGallerySearch,
        AgentToolInvocationPolicyMetadata.PromptGalleryItemGet,
        AgentToolInvocationPolicyMetadata.PromptGalleryCatalogSearch,
        AgentToolInvocationPolicyMetadata.PromptGalleryItemEditorGet,
        AgentToolInvocationPolicyMetadata.PromptGalleryDraftCreate,
        AgentToolInvocationPolicyMetadata.PromptGalleryDraftUpdate,
        AgentToolInvocationPolicyMetadata.PromptGalleryVersionCreate,
        AgentToolInvocationPolicyMetadata.WorkflowCuratorCatalogSearch,
        AgentToolInvocationPolicyMetadata.WorkflowCuratorDefinitionEditorGet,
        AgentToolInvocationPolicyMetadata.WorkflowCuratorAuthoringOptionsGet,
        AgentToolInvocationPolicyMetadata.WorkflowCuratorDraftCreate,
        AgentToolInvocationPolicyMetadata.WorkflowCuratorDraftUpdate,
        AgentToolInvocationPolicyMetadata.WorkflowCuratorNodeUpdate,
        AgentToolInvocationPolicyMetadata.WorkflowCuratorLifecycleChange,
        AgentToolInvocationPolicyMetadata.CapabilityCuratorCatalogSearch,
        AgentToolInvocationPolicyMetadata.CapabilityCuratorEditorGet,
        AgentToolInvocationPolicyMetadata.CapabilityCuratorAssignmentEditorGet,
        AgentToolInvocationPolicyMetadata.CapabilityCuratorSave,
        AgentToolInvocationPolicyMetadata.CapabilityCuratorToolSetupTest,
        AgentToolInvocationPolicyMetadata.CapabilityCuratorMcpSetupTest,
        AgentToolInvocationPolicyMetadata.CapabilityCuratorAssignmentUpdate,
        AgentToolInvocationPolicyMetadata.CapabilityCuratorVerify,
        AgentToolInvocationPolicyMetadata.SchedulerWorkflowTargetsSearch,
        AgentToolInvocationPolicyMetadata.SchedulerWorkflowSchedulesSearch,
        AgentToolInvocationPolicyMetadata.SchedulerWorkflowScheduleCreate,
        AgentToolInvocationPolicyMetadata.ImageGenerationCreate,
        AgentToolInvocationPolicyMetadata.HrAgentsSearch,
        AgentToolInvocationPolicyMetadata.HrAgentSettingsGet,
        AgentToolInvocationPolicyMetadata.HrAgentCreationOptionsGet,
        AgentToolInvocationPolicyMetadata.HrAgentCreate,
        AgentToolInvocationPolicyMetadata.HrAgentSettingsUpdate,
        AgentToolInvocationPolicyMetadata.HrAgentAvatarGenerate,
        AgentToolInvocationPolicyMetadata.HrAgentUsageGet,
        AgentToolInvocationPolicyMetadata.HrAgentProcessHistoryGet,
        AgentToolInvocationPolicyMetadata.HrAgentProcessManagerReviewRequest,
        AgentToolInvocationPolicyMetadata.HrCrmSearch,
        AgentToolInvocationPolicyMetadata.HrCrmItemSummaryGet,
        AgentToolInvocationPolicyMetadata.ProjectStructureProjectsList,
        AgentToolInvocationPolicyMetadata.ProjectStructureProjectCreate,
        AgentToolInvocationPolicyMetadata.ProjectStructureProjectUpdate,
        AgentToolInvocationPolicyMetadata.ProjectStructureHierarchyGet,
        AgentToolInvocationPolicyMetadata.ProjectStructureSubprojectCreate,
        AgentToolInvocationPolicyMetadata.ProjectStructureSubprojectLink,
        AgentToolInvocationPolicyMetadata.ProjectStructureNodesToNewSubproject,
        AgentToolInvocationPolicyMetadata.ProjectStructureRead,
        AgentToolInvocationPolicyMetadata.ProjectStructureNodeCatalog,
        AgentToolInvocationPolicyMetadata.ProjectStructureChecklist,
        AgentToolInvocationPolicyMetadata.ProjectStructureDependenciesQuery,
        AgentToolInvocationPolicyMetadata.ProjectPlanSummaryGet,
        AgentToolInvocationPolicyMetadata.ProjectTaskCreate,
        AgentToolInvocationPolicyMetadata.ProjectTaskUpdate,
        AgentToolInvocationPolicyMetadata.ProjectTaskResourceAttach,
        AgentToolInvocationPolicyMetadata.ProjectStructureDependencyLink,
        AgentToolInvocationPolicyMetadata.ProjectStructureDependencyUnlink,
        AgentToolInvocationPolicyMetadata.ProjectStructureNodeCreate,
        AgentToolInvocationPolicyMetadata.ProjectStructureNodeUpdate,
        AgentToolInvocationPolicyMetadata.ProjectStructureNodeTypeUpdate,
        AgentToolInvocationPolicyMetadata.ProjectStructureNodeMetadataUpdate,
        AgentToolInvocationPolicyMetadata.ProjectStructureNodesStatusUpdate,
        AgentToolInvocationPolicyMetadata.ProjectStructureNodeStatusUpdate,
        AgentToolInvocationPolicyMetadata.ProjectStructureNodesProgressUpdate,
        AgentToolInvocationPolicyMetadata.ProjectStructureNodeProgressUpdate,
        AgentToolInvocationPolicyMetadata.ProjectStructureNodesMarkerUpdate,
        AgentToolInvocationPolicyMetadata.ProjectStructureNodeMarkerUpdate,
        AgentToolInvocationPolicyMetadata.ProjectStructureNodesPriorityUpdate,
        AgentToolInvocationPolicyMetadata.ProjectStructureNodePriorityUpdate,
        AgentToolInvocationPolicyMetadata.ProjectStructureNodeMove,
        AgentToolInvocationPolicyMetadata.ProjectStructureNodeRecompose,
        AgentToolInvocationPolicyMetadata.ProjectStructureNodeReparent,
        AgentToolInvocationPolicyMetadata.ProjectStructureNodeDescendantsToProjectMove,
        AgentToolInvocationPolicyMetadata.ProjectStructureNodeCommandExecute,
        AgentToolInvocationPolicyMetadata.ProjectStructureNodeProcessDefinitionLink,
        AgentToolInvocationPolicyMetadata.ProjectStructureNodeProcessStart,
        AgentToolInvocationPolicyMetadata.ProjectStructureNodeWorkflowAddOptions,
        AgentToolInvocationPolicyMetadata.ProjectStructureNodeWorkflowDefinitionCreate,
        AgentToolInvocationPolicyMetadata.ProjectStructureNodeWorkflowStart,
        AgentToolInvocationPolicyMetadata.ProjectStructureNodeWorkflowStatusGet,
        AgentToolInvocationPolicyMetadata.ProjectStructureNodeDelete,
        AgentToolInvocationPolicyMetadata.ProjectStructureNodesDelete,
        AgentToolInvocationPolicyMetadata.ProjectStructureApprovalRequest,
        AgentToolInvocationPolicyMetadata.ProjectStructureAssetCreate,
        AgentToolInvocationPolicyMetadata.ProjectStructureAssetGet,
        AgentToolInvocationPolicyMetadata.ProjectStructureAssetContentGet,
        AgentToolInvocationPolicyMetadata.ProjectStructureAssetCreateRevision,
        AgentToolInvocationPolicyMetadata.ProjectStructureLinkCreate,
        AgentToolInvocationPolicyMetadata.ProjectStructureLinkUnlink,
        AgentToolInvocationPolicyMetadata.ProjectStructureImport,
        AgentToolInvocationPolicyMetadata.ProjectStructureKnowledgeQuery,
        AgentToolInvocationPolicyMetadata.ProjectStructureAnalyticsQuery,
        AgentToolInvocationPolicyMetadata.ProjectStructureProjectLeaseAcquire,
        AgentToolInvocationPolicyMetadata.ProjectStructureRepoBranchLeaseAcquire,
        AgentToolInvocationPolicyMetadata.ProjectStructureLeaseGet,
        AgentToolInvocationPolicyMetadata.ProjectStructureLeaseRenew,
        AgentToolInvocationPolicyMetadata.ProjectStructureLeaseRelease
    ];

    public static bool IsKnownToolName(string? toolName)
        => Contains(KnownToolNames, toolName);

    public static bool IsBrowserEvidenceToolName(string? toolName)
        => Contains(BrowserEvidenceToolNames, toolName);

    public static bool IsRepresentativeBrowserInteractionToolName(string? toolName)
        => Contains(RepresentativeBrowserInteractionToolNames, toolName);

    public static string NormalizeToolName(string? toolName)
        => string.IsNullOrWhiteSpace(toolName)
            ? string.Empty
            : toolName.Replace('-', '_').Trim().ToLowerInvariant();

    private static bool Contains(IReadOnlyList<string> values, string? value)
        => !string.IsNullOrWhiteSpace(value) &&
           values.Contains(NormalizeToolName(value), StringComparer.Ordinal);
}
