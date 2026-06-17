namespace CanDoItAll.AgentFramework.Core;

public static class ToolContractCatalog
{
    public const string WorkspaceListFiles = "workspace_list_files";
    public const string WorkspaceSearch = "workspace_search";
    public const string WorkspaceReadFile = "workspace_read_file";
    public const string WorkspaceStatPath = "workspace_stat_path";
    public const string WorkspaceCreateDirectory = "workspace_create_directory";
    public const string WorkspaceWriteFile = "workspace_write_file";
    public const string WorkspaceAppendFile = "workspace_append_file";
    public const string WorkspaceCopyPath = "workspace_copy_path";
    public const string WorkspaceMovePath = "workspace_move_path";
    public const string WorkspaceDeletePath = "workspace_delete_path";
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
    public const string WorkspaceInspectSpreadsheet = "workspace_inspect_spreadsheet";
    public const string WorkspaceConvertDocument = "workspace_convert_document";
    public const string WorkspaceCommandRun = "workspace_command_run";
    public const string WorkspaceExecutionBoundary = "workspace_execution_boundary";
    public const string WorkspaceGitDiff = "workspace_git_diff";
    public const string WorkspaceGitStatus = "workspace_git_status";
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

    public static IReadOnlyList<string> WorkspaceToolNames { get; } =
    [
        WorkspaceListFiles,
        WorkspaceSearch,
        WorkspaceReadFile,
        WorkspaceStatPath,
        WorkspaceCreateDirectory,
        WorkspaceWriteFile,
        WorkspaceAppendFile,
        WorkspaceCopyPath,
        WorkspaceMovePath,
        WorkspaceDeletePath,
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
        WorkspaceInspectSpreadsheet,
        WorkspaceConvertDocument,
        WorkspaceCommandRun,
        WorkspaceExecutionBoundary,
        WorkspaceGitDiff,
        WorkspaceGitStatus,
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
        BrowserDrag
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
        BrowserEvaluate
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
        AgentToolInvocationPolicyMetadata.ImageGenerationCreate,
        AgentToolInvocationPolicyMetadata.ProjectStructureProjectsList,
        AgentToolInvocationPolicyMetadata.ProjectStructureProjectCreate,
        AgentToolInvocationPolicyMetadata.ProjectStructureProjectUpdate,
        AgentToolInvocationPolicyMetadata.ProjectStructureHierarchyGet,
        AgentToolInvocationPolicyMetadata.ProjectStructureSubprojectLink,
        AgentToolInvocationPolicyMetadata.ProjectStructureNodesToNewSubproject,
        AgentToolInvocationPolicyMetadata.ProjectStructureRead,
        AgentToolInvocationPolicyMetadata.ProjectStructureNodeCatalog,
        AgentToolInvocationPolicyMetadata.ProjectStructureChecklist,
        AgentToolInvocationPolicyMetadata.ProjectStructureDependenciesQuery,
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
