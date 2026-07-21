using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public enum ToolCapabilitySideEffectKind
{
    None,
    WorkspaceRead,
    WorkspaceWrite,
    LocalProcessExecution,
    RuntimeLaunch,
    RuntimeProofCapture,
    ProcessMutation,
    ProjectStructureMutation,
    ExternalAction,
    MediaGeneration,
    DocumentConversion,
    ProviderNative,
    McpTool,
    InternalDataRead,
    InternalStateMutation
}

public enum ToolCapabilityOperationRequirementKind
{
    None,
    Static,
    WorkspaceFileMutation,
    WorkspaceScript,
    DotNetRun,
    ProcessArtifactWrite
}

public enum ToolCapabilityBrowserProofRole
{
    None,
    Navigation,
    Observation,
    Interaction,
    EvidenceCapture
}

public enum ToolCapabilityIdempotencyDescriptor
{
    Idempotent,
    RuntimeStateDependent,
    StateChanging,
    ExternalSideEffect
}

public sealed record ToolCapabilityProcessOperationRequirement(IReadOnlyList<string> AnyOf)
{
    public static ToolCapabilityProcessOperationRequirement Any(params string[] operations)
    {
        return new ToolCapabilityProcessOperationRequirement(operations
            .Where(operation => !string.IsNullOrWhiteSpace(operation))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray());
    }
}

public sealed record ToolCapabilityMetadata(
    string Name,
    ToolInvocationClassification Classification,
    bool RequiresApprovalByDefault,
    bool IsStateChanging,
    ToolCapabilitySideEffectKind SideEffectKind,
    ToolCapabilityOperationRequirementKind OperationRequirementKind,
    IReadOnlyList<ToolCapabilityProcessOperationRequirement> OperationRequirements,
    IReadOnlyList<string> TargetScopeRequirements,
    bool CanMutateProduct,
    bool CanExecuteExternalAction,
    bool CanReadExternalTarget,
    bool CanWriteManagedArtifact,
    ToolCapabilityBrowserProofRole BrowserProofRole,
    ToolCapabilityIdempotencyDescriptor IdempotencyDescriptor)
{
    public AgentToolPolicyMetadata ToPolicyMetadata()
    {
        return new AgentToolPolicyMetadata(
            Name,
            Classification,
            RequiresApprovalByDefault,
            IsStateChanging);
    }
}

public static class ToolCapabilityRegistry
{
    private static readonly IReadOnlyDictionary<string, ToolCapabilityMetadata> RegisteredCapabilities = BuildCapabilities();

    public static IReadOnlyCollection<ToolCapabilityMetadata> Capabilities => RegisteredCapabilities.Values.ToArray();

    public static IReadOnlyCollection<AgentToolPolicyMetadata> PolicyMetadata => RegisteredCapabilities.Values
        .Select(capability => capability.ToPolicyMetadata())
        .ToArray();

    public static bool TryResolve(string? toolName, out ToolCapabilityMetadata metadata)
    {
        var normalized = ToolContractCatalog.NormalizeToolName(toolName);
        if (!string.IsNullOrWhiteSpace(normalized) &&
            RegisteredCapabilities.TryGetValue(normalized, out var resolved))
        {
            metadata = resolved;
            return true;
        }

        metadata = null!;
        return false;
    }

    public static ToolInvocationClassification Classify(string? toolName)
    {
        if (TryResolve(toolName, out var metadata))
        {
            return metadata.Classification;
        }

        if (IsProviderNativeToolFamily(toolName))
        {
            return ToolInvocationClassification.HostedProviderNative;
        }

        if (IsHostedMcpToolFamily(toolName))
        {
            return ToolInvocationClassification.HostedMcp;
        }

        if (IsLocalMcpToolFamily(toolName))
        {
            return ToolInvocationClassification.LocalMcp;
        }

        return ToolInvocationClassification.Unknown;
    }

    public static bool RequiresApprovalByDefault(string? toolName)
    {
        return TryResolve(toolName, out var metadata) &&
               metadata.RequiresApprovalByDefault;
    }

    public static bool IsMutationTool(string? toolName)
    {
        return TryResolve(toolName, out var metadata) &&
               metadata.Classification == ToolInvocationClassification.Mutation;
    }

    public static bool IsValidationTool(string? toolName)
    {
        return TryResolve(toolName, out var metadata) &&
               metadata.Classification == ToolInvocationClassification.Validation;
    }

    private static IReadOnlyDictionary<string, ToolCapabilityMetadata> BuildCapabilities()
    {
        var capabilities = new List<ToolCapabilityMetadata>
        {
            Read(ToolContractCatalog.WorkspaceListDirectory, ToolCapabilitySideEffectKind.WorkspaceRead),
            Read(ToolContractCatalog.WorkspaceListFiles, ToolCapabilitySideEffectKind.WorkspaceRead),
            Read(ToolContractCatalog.WorkspaceSearch, ToolCapabilitySideEffectKind.WorkspaceRead),
            Read(ToolContractCatalog.WorkspaceReadFile, ToolCapabilitySideEffectKind.WorkspaceRead),
            Read(ToolContractCatalog.WorkspaceStatPath, ToolCapabilitySideEffectKind.WorkspaceRead),
            Read(ToolContractCatalog.WorkspaceHashPath, ToolCapabilitySideEffectKind.WorkspaceRead),
            Mutation(ToolContractCatalog.WorkspaceCreateDirectory, ToolCapabilitySideEffectKind.WorkspaceWrite, ToolCapabilityOperationRequirementKind.WorkspaceFileMutation),
            Mutation(ToolContractCatalog.WorkspaceWriteFile, ToolCapabilitySideEffectKind.WorkspaceWrite, ToolCapabilityOperationRequirementKind.WorkspaceFileMutation),
            Mutation(ToolContractCatalog.WorkspaceAppendFile, ToolCapabilitySideEffectKind.WorkspaceWrite, ToolCapabilityOperationRequirementKind.WorkspaceFileMutation),
            Mutation(ToolContractCatalog.WorkspaceCopyPath, ToolCapabilitySideEffectKind.WorkspaceWrite, ToolCapabilityOperationRequirementKind.WorkspaceFileMutation),
            Mutation(ToolContractCatalog.WorkspaceMovePath, ToolCapabilitySideEffectKind.WorkspaceWrite, ToolCapabilityOperationRequirementKind.WorkspaceFileMutation),
            Mutation(ToolContractCatalog.WorkspaceDeletePath, ToolCapabilitySideEffectKind.WorkspaceWrite, ToolCapabilityOperationRequirementKind.WorkspaceFileMutation),
            Mutation(ToolContractCatalog.WorkspaceZipPath, ToolCapabilitySideEffectKind.WorkspaceWrite, ToolCapabilityOperationRequirementKind.WorkspaceFileMutation),
            Mutation(ToolContractCatalog.WorkspaceUnzipArchive, ToolCapabilitySideEffectKind.WorkspaceWrite, ToolCapabilityOperationRequirementKind.WorkspaceFileMutation),
            Read(ToolContractCatalog.WorkspaceDiffText, ToolCapabilitySideEffectKind.WorkspaceRead),
            Mutation(ToolContractCatalog.WorkspaceDotNetNew, ToolCapabilitySideEffectKind.WorkspaceWrite, ToolCapabilityOperationRequirementKind.WorkspaceFileMutation),
            Validation(ToolContractCatalog.WorkspaceDotNetRestore, ToolCapabilitySideEffectKind.LocalProcessExecution, StaticRequirement(ProcessOperationContractNames.RunValidation)),
            Validation(ToolContractCatalog.WorkspaceDotNetBuild, ToolCapabilitySideEffectKind.LocalProcessExecution, StaticRequirement(ProcessOperationContractNames.RunValidation)),
            Validation(ToolContractCatalog.WorkspaceDotNetTest, ToolCapabilitySideEffectKind.LocalProcessExecution, StaticRequirement(ProcessOperationContractNames.RunValidation)),
            Validation(ToolContractCatalog.WorkspaceDotNetRun, ToolCapabilitySideEffectKind.RuntimeLaunch, ToolCapabilityOperationRequirementKind.DotNetRun),
            Validation(ToolContractCatalog.WorkspaceDotNetStop, ToolCapabilitySideEffectKind.RuntimeLaunch, StaticRequirement(
                ProcessOperationContractNames.LaunchRuntime,
                ProcessOperationContractNames.CaptureRuntimeProof)),
            Mutation(AgentToolInvocationPolicyMetadata.WorkspacePowerShellRunScript, ToolCapabilitySideEffectKind.LocalProcessExecution, ToolCapabilityOperationRequirementKind.WorkspaceScript),
            Mutation(AgentToolInvocationPolicyMetadata.WorkspacePythonRunFile, ToolCapabilitySideEffectKind.LocalProcessExecution, ToolCapabilityOperationRequirementKind.WorkspaceScript),
            Read(
                ToolContractCatalog.WorkspaceInspectImage,
                ToolCapabilitySideEffectKind.RuntimeProofCapture,
                StaticRequirement(
                    ProcessOperationContractNames.CaptureRuntimeProof,
                    ProcessOperationContractNames.ReadProjectStructure)),
            Read(
                ToolContractCatalog.WorkspaceAnalyzeImage,
                ToolCapabilitySideEffectKind.RuntimeProofCapture,
                StaticRequirement(
                    ProcessOperationContractNames.CaptureRuntimeProof,
                    ProcessOperationContractNames.ReadProjectStructure)),
            Read(
                ToolContractCatalog.WorkspaceAnalyzeImages,
                ToolCapabilitySideEffectKind.RuntimeProofCapture,
                StaticRequirement(
                    ProcessOperationContractNames.CaptureRuntimeProof,
                    ProcessOperationContractNames.ReadProjectStructure)),
            Read(ToolContractCatalog.WorkspaceInspectSpreadsheet, ToolCapabilitySideEffectKind.WorkspaceRead),
            Read(ToolContractCatalog.WorkspaceSpreadsheetSummary, ToolCapabilitySideEffectKind.WorkspaceRead),
            Read(ToolContractCatalog.WorkspaceReadSpreadsheetCell, ToolCapabilitySideEffectKind.WorkspaceRead),
            Read(ToolContractCatalog.WorkspaceReadSpreadsheetRange, ToolCapabilitySideEffectKind.WorkspaceRead),
            Mutation(ToolContractCatalog.WorkspaceWriteSpreadsheet, ToolCapabilitySideEffectKind.WorkspaceWrite, ToolCapabilityOperationRequirementKind.WorkspaceFileMutation),
            Read(ToolContractCatalog.WorkspaceSpreadsheetFunctionCatalog, ToolCapabilitySideEffectKind.WorkspaceRead),
            Validation(
                ToolContractCatalog.WorkspaceConvertDocument,
                ToolCapabilitySideEffectKind.DocumentConversion,
                StaticRequirement(
                    ProcessOperationContractNames.ReadProjectStructure,
                    ProcessOperationContractNames.WriteManagedProcessArtifacts)),
            Mutation(
                ToolContractCatalog.WorkspaceCommandRun,
                ToolCapabilitySideEffectKind.LocalProcessExecution,
                StaticRequirement(ProcessOperationContractNames.ExecuteExternalAction)),
            Read(ToolContractCatalog.WorkspaceExecutionBoundary, ToolCapabilitySideEffectKind.WorkspaceRead),
            Read(ToolContractCatalog.WorkspaceGitDiff, ToolCapabilitySideEffectKind.WorkspaceRead),
            Read(ToolContractCatalog.WorkspaceGitStatus, ToolCapabilitySideEffectKind.WorkspaceRead),
            Read(ToolContractCatalog.WorkspaceGitLog, ToolCapabilitySideEffectKind.WorkspaceRead),
            Read(ToolContractCatalog.WorkspaceGitShow, ToolCapabilitySideEffectKind.WorkspaceRead),
            Mutation(ToolContractCatalog.WorkspaceGitAdd, ToolCapabilitySideEffectKind.WorkspaceWrite, ToolCapabilityOperationRequirementKind.WorkspaceFileMutation),
            Mutation(ToolContractCatalog.WorkspaceGitUnstage, ToolCapabilitySideEffectKind.WorkspaceWrite, ToolCapabilityOperationRequirementKind.WorkspaceFileMutation),
            Mutation(ToolContractCatalog.WorkspaceGitCommit, ToolCapabilitySideEffectKind.WorkspaceWrite, ToolCapabilityOperationRequirementKind.WorkspaceFileMutation),
            Mutation(ToolContractCatalog.WorkspaceGitBranchCreate, ToolCapabilitySideEffectKind.WorkspaceWrite, ToolCapabilityOperationRequirementKind.WorkspaceFileMutation),
            Mutation(ToolContractCatalog.WorkspaceGitSwitch, ToolCapabilitySideEffectKind.WorkspaceWrite, ToolCapabilityOperationRequirementKind.WorkspaceFileMutation),
            Mutation(
                ToolContractCatalog.LocalMcpLaunch,
                ToolCapabilitySideEffectKind.LocalProcessExecution,
                StaticRequirement(ProcessOperationContractNames.ExecuteExternalAction)),
            Validation(ToolContractCatalog.BrowserNavigate, ToolCapabilitySideEffectKind.RuntimeProofCapture, StaticRequirement(ProcessOperationContractNames.CaptureRuntimeProof)),
            Validation(ToolContractCatalog.BrowserResize, ToolCapabilitySideEffectKind.RuntimeProofCapture, StaticRequirement(ProcessOperationContractNames.CaptureRuntimeProof)),
            Validation(ToolContractCatalog.BrowserConsoleMessages, ToolCapabilitySideEffectKind.RuntimeProofCapture, StaticRequirement(ProcessOperationContractNames.CaptureRuntimeProof)),
            Validation(ToolContractCatalog.BrowserEvaluate, ToolCapabilitySideEffectKind.RuntimeProofCapture, StaticRequirement(ProcessOperationContractNames.CaptureRuntimeProof)),
            Validation(ToolContractCatalog.BrowserNetworkRequests, ToolCapabilitySideEffectKind.RuntimeProofCapture, StaticRequirement(ProcessOperationContractNames.CaptureRuntimeProof)),
            Validation(ToolContractCatalog.BrowserSnapshot, ToolCapabilitySideEffectKind.RuntimeProofCapture, StaticRequirement(ProcessOperationContractNames.CaptureRuntimeProof)),
            Validation(ToolContractCatalog.BrowserTakeScreenshot, ToolCapabilitySideEffectKind.RuntimeProofCapture, StaticRequirement(ProcessOperationContractNames.CaptureRuntimeProof)),
            Validation(ToolContractCatalog.BrowserClick, ToolCapabilitySideEffectKind.RuntimeProofCapture, StaticRequirement(ProcessOperationContractNames.CaptureRuntimeProof)),
            Validation(ToolContractCatalog.BrowserFillForm, ToolCapabilitySideEffectKind.RuntimeProofCapture, StaticRequirement(ProcessOperationContractNames.CaptureRuntimeProof)),
            Validation(ToolContractCatalog.BrowserSelectOption, ToolCapabilitySideEffectKind.RuntimeProofCapture, StaticRequirement(ProcessOperationContractNames.CaptureRuntimeProof)),
            Validation(ToolContractCatalog.BrowserPressKey, ToolCapabilitySideEffectKind.RuntimeProofCapture, StaticRequirement(ProcessOperationContractNames.CaptureRuntimeProof)),
            Validation(ToolContractCatalog.BrowserType, ToolCapabilitySideEffectKind.RuntimeProofCapture, StaticRequirement(ProcessOperationContractNames.CaptureRuntimeProof)),
            Validation(ToolContractCatalog.BrowserDrag, ToolCapabilitySideEffectKind.RuntimeProofCapture, StaticRequirement(ProcessOperationContractNames.CaptureRuntimeProof)),
            Validation(ToolContractCatalog.BrowserWaitFor, ToolCapabilitySideEffectKind.RuntimeProofCapture, StaticRequirement(ProcessOperationContractNames.CaptureRuntimeProof)),
            Read(AgentFinalizerPolicies.SubmitProcessStepOutcomeToolName, ToolCapabilitySideEffectKind.None),
            Read(AgentFinalizerPolicies.SubmitCodeReviewResultToolName, ToolCapabilitySideEffectKind.None),
            Read(AgentFinalizerPolicies.SubmitArchitectureReviewResultToolName, ToolCapabilitySideEffectKind.None),
            Read(AgentFinalizerPolicies.SubmitImplementationPlanToolName, ToolCapabilitySideEffectKind.None),
            Read(AgentFinalizerPolicies.SubmitTestPlanToolName, ToolCapabilitySideEffectKind.None),
            Read(AgentFinalizerPolicies.SubmitToolExecutionDecisionToolName, ToolCapabilitySideEffectKind.None),
            Read(AgentFinalizerPolicies.SubmitProcessStatePatchToolName, ToolCapabilitySideEffectKind.None),
            Read(AgentFinalizerPolicies.SubmitHumanEscalationRequestToolName, ToolCapabilitySideEffectKind.None),
            Read(AgentToolInvocationPolicyMetadata.LoadSkill, ToolCapabilitySideEffectKind.WorkspaceRead),
            Read(AgentToolInvocationPolicyMetadata.ReadSkillResource, ToolCapabilitySideEffectKind.WorkspaceRead),
            Mutation(AgentToolInvocationPolicyMetadata.RunSkillScript, ToolCapabilitySideEffectKind.LocalProcessExecution, StaticRequirement(ProcessOperationContractNames.ExecuteExternalAction)),
            Mutation(AgentToolInvocationPolicyMetadata.ProcessesDefinitionSave, ToolCapabilitySideEffectKind.ProcessMutation, StaticRequirement(ProcessOperationContractNames.ExecuteExternalAction)),
            Mutation(AgentToolInvocationPolicyMetadata.ProcessesDefinitionRoleAdd, ToolCapabilitySideEffectKind.ProcessMutation, StaticRequirement(ProcessOperationContractNames.ExecuteExternalAction)),
            Mutation(AgentToolInvocationPolicyMetadata.ProcessesDefinitionPublish, ToolCapabilitySideEffectKind.ProcessMutation, StaticRequirement(ProcessOperationContractNames.ExecuteExternalAction)),
            Mutation(AgentToolInvocationPolicyMetadata.ProcessesDefinitionDelete, ToolCapabilitySideEffectKind.ProcessMutation, StaticRequirement(ProcessOperationContractNames.ExecuteExternalAction)),
            Mutation(AgentToolInvocationPolicyMetadata.ProcessesDefinitionImport, ToolCapabilitySideEffectKind.ProcessMutation, StaticRequirement(ProcessOperationContractNames.ExecuteExternalAction)),
            Mutation(AgentToolInvocationPolicyMetadata.ProcessesRunStart, ToolCapabilitySideEffectKind.ProcessMutation, StaticRequirement(ProcessOperationContractNames.ExecuteExternalAction)),
            Mutation(AgentToolInvocationPolicyMetadata.ProcessesStepTransition, ToolCapabilitySideEffectKind.ProcessMutation, StaticRequirement(
                ProcessOperationContractNames.EscalateOrDecide,
                ProcessOperationContractNames.RecoverArtifactsOnly,
                ProcessOperationContractNames.ExecuteExternalAction)),
            Mutation(AgentToolInvocationPolicyMetadata.ProcessesAssignmentResolve, ToolCapabilitySideEffectKind.ProcessMutation, StaticRequirement(ProcessOperationContractNames.ExecuteExternalAction)),
            Mutation(AgentToolInvocationPolicyMetadata.ProcessesArtifactRecord, ToolCapabilitySideEffectKind.ProcessMutation, ToolCapabilityOperationRequirementKind.ProcessArtifactWrite),
            Read(AgentToolInvocationPolicyMetadata.ProcessesDefinitionsList, ToolCapabilitySideEffectKind.WorkspaceRead),
            Read(AgentToolInvocationPolicyMetadata.ProcessesDefinitionEditorGet, ToolCapabilitySideEffectKind.WorkspaceRead),
            Read(AgentToolInvocationPolicyMetadata.ProcessesDefinitionExport, ToolCapabilitySideEffectKind.WorkspaceRead),
            Read(AgentToolInvocationPolicyMetadata.ProcessesRunsList, ToolCapabilitySideEffectKind.WorkspaceRead),
            Read(AgentToolInvocationPolicyMetadata.ProcessesRunDetailGet, ToolCapabilitySideEffectKind.WorkspaceRead),
            Read(AgentToolInvocationPolicyMetadata.ProcessesAnalyticsGet, ToolCapabilitySideEffectKind.WorkspaceRead),
            Read(AgentToolInvocationPolicyMetadata.ProcessesPartyOptionsList, ToolCapabilitySideEffectKind.WorkspaceRead),
            Read(AgentToolInvocationPolicyMetadata.ProcessesExecutorOptionsList, ToolCapabilitySideEffectKind.WorkspaceRead),
            Read(AgentToolInvocationPolicyMetadata.ProcessesTemplatesList, ToolCapabilitySideEffectKind.WorkspaceRead),
            Read(AgentToolInvocationPolicyMetadata.ProcessesTemplateGet, ToolCapabilitySideEffectKind.WorkspaceRead),
            Read(AgentToolInvocationPolicyMetadata.ProcessesTemplateMermaidGet, ToolCapabilitySideEffectKind.WorkspaceRead),
            Mutation(AgentToolInvocationPolicyMetadata.ProcessesTemplateImport, ToolCapabilitySideEffectKind.ProcessMutation, StaticRequirement(ProcessOperationContractNames.ExecuteExternalAction)),
            Read(AgentToolInvocationPolicyMetadata.ProcessesTemplateBaselineScenariosList, ToolCapabilitySideEffectKind.WorkspaceRead),
            Read(AgentToolInvocationPolicyMetadata.ProcessesTemplateLiveRunProfilesList, ToolCapabilitySideEffectKind.WorkspaceRead),
            Read(AgentToolInvocationPolicyMetadata.WorkflowsDefinitionsList, ToolCapabilitySideEffectKind.None),
            Mutation(
                AgentToolInvocationPolicyMetadata.WorkflowsRunStart,
                ToolCapabilitySideEffectKind.RuntimeLaunch,
                StaticRequirement(ProcessOperationContractNames.LaunchRuntime)),
            Read(AgentToolInvocationPolicyMetadata.WorkflowsRunStatusGet, ToolCapabilitySideEffectKind.None),
            Mutation(
                AgentToolInvocationPolicyMetadata.WorkflowsRunCancel,
                ToolCapabilitySideEffectKind.RuntimeLaunch,
                StaticRequirement(ProcessOperationContractNames.ExecuteExternalAction)),
            Mutation(
                AgentToolInvocationPolicyMetadata.WorkflowsExternalResponseSubmit,
                ToolCapabilitySideEffectKind.RuntimeLaunch,
                StaticRequirement(ProcessOperationContractNames.ExecuteExternalAction)),
            Read(AgentToolInvocationPolicyMetadata.PromptGallerySearch, ToolCapabilitySideEffectKind.InternalDataRead),
            Read(AgentToolInvocationPolicyMetadata.PromptGalleryItemGet, ToolCapabilitySideEffectKind.InternalDataRead),
            Read(AgentToolInvocationPolicyMetadata.PromptGalleryCatalogSearch, ToolCapabilitySideEffectKind.InternalDataRead),
            Read(AgentToolInvocationPolicyMetadata.PromptGalleryItemEditorGet, ToolCapabilitySideEffectKind.InternalDataRead),
            Mutation(AgentToolInvocationPolicyMetadata.PromptGalleryDraftCreate, ToolCapabilitySideEffectKind.InternalStateMutation),
            Mutation(AgentToolInvocationPolicyMetadata.PromptGalleryDraftUpdate, ToolCapabilitySideEffectKind.InternalStateMutation),
            Mutation(AgentToolInvocationPolicyMetadata.PromptGalleryVersionCreate, ToolCapabilitySideEffectKind.InternalStateMutation),
            Read(AgentToolInvocationPolicyMetadata.WorkflowCuratorCatalogSearch, ToolCapabilitySideEffectKind.InternalDataRead),
            Read(AgentToolInvocationPolicyMetadata.WorkflowCuratorDefinitionEditorGet, ToolCapabilitySideEffectKind.InternalDataRead),
            Read(AgentToolInvocationPolicyMetadata.WorkflowCuratorAuthoringOptionsGet, ToolCapabilitySideEffectKind.InternalDataRead),
            Mutation(AgentToolInvocationPolicyMetadata.WorkflowCuratorDraftCreate, ToolCapabilitySideEffectKind.InternalStateMutation),
            Mutation(AgentToolInvocationPolicyMetadata.WorkflowCuratorDraftUpdate, ToolCapabilitySideEffectKind.InternalStateMutation),
            Mutation(AgentToolInvocationPolicyMetadata.WorkflowCuratorNodeUpdate, ToolCapabilitySideEffectKind.InternalStateMutation),
            Mutation(AgentToolInvocationPolicyMetadata.WorkflowCuratorLifecycleChange, ToolCapabilitySideEffectKind.InternalStateMutation),
            Read(AgentToolInvocationPolicyMetadata.CapabilityCuratorCatalogSearch, ToolCapabilitySideEffectKind.InternalDataRead),
            Read(AgentToolInvocationPolicyMetadata.CapabilityCuratorEditorGet, ToolCapabilitySideEffectKind.InternalDataRead),
            Read(AgentToolInvocationPolicyMetadata.CapabilityCuratorAssignmentEditorGet, ToolCapabilitySideEffectKind.InternalDataRead),
            Mutation(AgentToolInvocationPolicyMetadata.CapabilityCuratorSave, ToolCapabilitySideEffectKind.InternalStateMutation),
            Mutation(
                AgentToolInvocationPolicyMetadata.CapabilityCuratorToolSetupTest,
                ToolCapabilitySideEffectKind.ExternalAction,
                StaticRequirement(ProcessOperationContractNames.ExecuteExternalAction)),
            Mutation(
                AgentToolInvocationPolicyMetadata.CapabilityCuratorMcpSetupTest,
                ToolCapabilitySideEffectKind.ExternalAction,
                StaticRequirement(ProcessOperationContractNames.ExecuteExternalAction)),
            Mutation(AgentToolInvocationPolicyMetadata.CapabilityCuratorAssignmentUpdate, ToolCapabilitySideEffectKind.InternalStateMutation),
            Mutation(
                AgentToolInvocationPolicyMetadata.CapabilityCuratorVerify,
                ToolCapabilitySideEffectKind.ExternalAction,
                StaticRequirement(ProcessOperationContractNames.ExecuteExternalAction)),
            Read(AgentToolInvocationPolicyMetadata.SchedulerWorkflowTargetsSearch, ToolCapabilitySideEffectKind.InternalDataRead),
            Read(AgentToolInvocationPolicyMetadata.SchedulerWorkflowSchedulesSearch, ToolCapabilitySideEffectKind.InternalDataRead),
            Mutation(AgentToolInvocationPolicyMetadata.SchedulerWorkflowScheduleCreate, ToolCapabilitySideEffectKind.InternalStateMutation),
            Mutation(AgentToolInvocationPolicyMetadata.ImageGenerationCreate, ToolCapabilitySideEffectKind.MediaGeneration, StaticRequirement(ProcessOperationContractNames.ExecuteExternalAction)),
            Read(AgentToolInvocationPolicyMetadata.HrAgentsSearch, ToolCapabilitySideEffectKind.InternalDataRead),
            Read(AgentToolInvocationPolicyMetadata.HrAgentSettingsGet, ToolCapabilitySideEffectKind.InternalDataRead),
            Read(AgentToolInvocationPolicyMetadata.HrAgentCreationOptionsGet, ToolCapabilitySideEffectKind.InternalDataRead),
            Mutation(AgentToolInvocationPolicyMetadata.HrAgentCreate, ToolCapabilitySideEffectKind.InternalStateMutation),
            Mutation(AgentToolInvocationPolicyMetadata.HrAgentSettingsUpdate, ToolCapabilitySideEffectKind.InternalStateMutation),
            Mutation(AgentToolInvocationPolicyMetadata.HrAgentAvatarGenerate, ToolCapabilitySideEffectKind.MediaGeneration, StaticRequirement(ProcessOperationContractNames.ExecuteExternalAction)),
            Read(AgentToolInvocationPolicyMetadata.HrAgentUsageGet, ToolCapabilitySideEffectKind.InternalDataRead),
            Read(AgentToolInvocationPolicyMetadata.HrAgentProcessHistoryGet, ToolCapabilitySideEffectKind.InternalDataRead),
            Mutation(AgentToolInvocationPolicyMetadata.HrAgentProcessManagerReviewRequest, ToolCapabilitySideEffectKind.ExternalAction, StaticRequirement(ProcessOperationContractNames.ExecuteExternalAction)),
            Read(AgentToolInvocationPolicyMetadata.HrCrmSearch, ToolCapabilitySideEffectKind.InternalDataRead),
            Read(AgentToolInvocationPolicyMetadata.HrCrmItemSummaryGet, ToolCapabilitySideEffectKind.InternalDataRead)
        };

        capabilities.AddRange(AgentToolInvocationPolicyMetadata.ProjectStructureReadTools.Select(toolName =>
            Read(toolName, ToolCapabilitySideEffectKind.WorkspaceRead)));
        capabilities.AddRange(AgentToolInvocationPolicyMetadata.ProjectStructureMutationTools
            .Where(toolName =>
                !string.Equals(toolName, AgentToolInvocationPolicyMetadata.ProjectStructureApprovalRequest, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(toolName, AgentToolInvocationPolicyMetadata.ProjectStructureNodeProcessStart, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(toolName, AgentToolInvocationPolicyMetadata.ProjectStructureProcessSubprocessLaunch, StringComparison.OrdinalIgnoreCase))
            .Select(toolName =>
                Mutation(toolName, ToolCapabilitySideEffectKind.ProjectStructureMutation, StaticRequirement(ProcessOperationContractNames.ExecuteExternalAction))));
        capabilities.Add(Mutation(
            AgentToolInvocationPolicyMetadata.ProjectStructureNodeProcessStart,
            ToolCapabilitySideEffectKind.ProjectStructureMutation,
            StaticRequirement(ProcessOperationContractNames.StartProjectNodeProcess)));
        capabilities.Add(Mutation(
            AgentToolInvocationPolicyMetadata.ProjectStructureProcessSubprocessLaunch,
            ToolCapabilitySideEffectKind.ProjectStructureMutation,
            StaticRequirement(ProcessOperationContractNames.ExecuteExternalAction)));
        capabilities.Add(Mutation(
            AgentToolInvocationPolicyMetadata.ProjectStructureApprovalRequest,
            ToolCapabilitySideEffectKind.ProjectStructureMutation,
            StaticRequirement(
                ProcessOperationContractNames.EscalateOrDecide,
                ProcessOperationContractNames.ExecuteExternalAction)));

        return capabilities.ToDictionary(
            capability => ToolContractCatalog.NormalizeToolName(capability.Name),
            StringComparer.OrdinalIgnoreCase);
    }

    private static ToolCapabilityMetadata Read(
        string name,
        ToolCapabilitySideEffectKind sideEffectKind,
        IReadOnlyList<ToolCapabilityProcessOperationRequirement>? requirements = null)
    {
        return Capability(
            name,
            ToolInvocationClassification.Read,
            requiresApprovalByDefault: false,
            isStateChanging: false,
            sideEffectKind,
            requirements);
    }

    private static ToolCapabilityMetadata Validation(
        string name,
        ToolCapabilitySideEffectKind sideEffectKind,
        IReadOnlyList<ToolCapabilityProcessOperationRequirement>? requirements = null)
    {
        return Capability(
            name,
            ToolInvocationClassification.Validation,
            requiresApprovalByDefault: false,
            isStateChanging: false,
            sideEffectKind,
            requirements);
    }

    private static ToolCapabilityMetadata Validation(
        string name,
        ToolCapabilitySideEffectKind sideEffectKind,
        ToolCapabilityOperationRequirementKind requirementKind)
    {
        return Capability(
            name,
            ToolInvocationClassification.Validation,
            requiresApprovalByDefault: false,
            isStateChanging: false,
            sideEffectKind,
            requirementKind,
            []);
    }

    private static ToolCapabilityMetadata Mutation(
        string name,
        ToolCapabilitySideEffectKind sideEffectKind,
        IReadOnlyList<ToolCapabilityProcessOperationRequirement>? requirements = null)
    {
        return Capability(
            name,
            ToolInvocationClassification.Mutation,
            requiresApprovalByDefault: true,
            isStateChanging: true,
            sideEffectKind,
            requirements);
    }

    private static ToolCapabilityMetadata Mutation(
        string name,
        ToolCapabilitySideEffectKind sideEffectKind,
        ToolCapabilityOperationRequirementKind requirementKind)
    {
        return Capability(
            name,
            ToolInvocationClassification.Mutation,
            requiresApprovalByDefault: true,
            isStateChanging: true,
            sideEffectKind,
            requirementKind,
            []);
    }

    private static ToolCapabilityMetadata Capability(
        string name,
        ToolInvocationClassification classification,
        bool requiresApprovalByDefault,
        bool isStateChanging,
        ToolCapabilitySideEffectKind sideEffectKind,
        IReadOnlyList<ToolCapabilityProcessOperationRequirement>? requirements)
    {
        var operationRequirements = requirements ?? [];
        return Capability(
            name,
            classification,
            requiresApprovalByDefault,
            isStateChanging,
            sideEffectKind,
            operationRequirements.Count == 0
                ? ToolCapabilityOperationRequirementKind.None
                : ToolCapabilityOperationRequirementKind.Static,
            operationRequirements);
    }

    private static ToolCapabilityMetadata Capability(
        string name,
        ToolInvocationClassification classification,
        bool requiresApprovalByDefault,
        bool isStateChanging,
        ToolCapabilitySideEffectKind sideEffectKind,
        ToolCapabilityOperationRequirementKind requirementKind,
        IReadOnlyList<ToolCapabilityProcessOperationRequirement> requirements)
    {
        return new ToolCapabilityMetadata(
            ToolContractCatalog.NormalizeToolName(name),
            classification,
            requiresApprovalByDefault,
            isStateChanging,
            sideEffectKind,
            requirementKind,
            requirements,
            ResolveTargetScopeRequirements(requirements, requirementKind),
            CanMutateProduct(requirements, requirementKind),
            CanExecuteExternalAction(requirements, requirementKind, sideEffectKind),
            CanReadExternalTarget(classification, sideEffectKind),
            CanWriteManagedArtifact(requirements, requirementKind),
            ResolveBrowserProofRole(name),
            ResolveIdempotencyDescriptor(classification, sideEffectKind));
    }

    private static IReadOnlyList<ToolCapabilityProcessOperationRequirement> StaticRequirement(params string[] operations)
    {
        return [ToolCapabilityProcessOperationRequirement.Any(operations)];
    }

    private static IReadOnlyList<string> ResolveTargetScopeRequirements(
        IReadOnlyList<ToolCapabilityProcessOperationRequirement> requirements,
        ToolCapabilityOperationRequirementKind requirementKind)
    {
        var scopes = new HashSet<string>(StringComparer.Ordinal);

        foreach (var scope in ResolveDynamicTargetScopes(requirementKind))
        {
            scopes.Add(scope);
        }

        foreach (var operation in requirements.SelectMany(requirement => requirement.AnyOf))
        {
            foreach (var scope in ResolveTargetScopesForOperation(operation))
            {
                scopes.Add(scope);
            }
        }

        return scopes
            .Where(ProcessOperationContractNames.IsTargetScopeName)
            .OrderBy(scope => scope, StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<string> ResolveDynamicTargetScopes(
        ToolCapabilityOperationRequirementKind requirementKind)
    {
        return requirementKind switch
        {
            ToolCapabilityOperationRequirementKind.WorkspaceFileMutation =>
            [
                ProcessOperationContractNames.ExternalArtifactDestination,
                ProcessOperationContractNames.ExternalProductTargetMutable,
                ProcessOperationContractNames.ManagedOutputProduct,
                ProcessOperationContractNames.ManagedProcessArtifactsOnly
            ],
            ToolCapabilityOperationRequirementKind.WorkspaceScript =>
            [
                ProcessOperationContractNames.ExternalActionControlled,
                ProcessOperationContractNames.ExternalArtifactDestination,
                ProcessOperationContractNames.ExternalProductTargetMutable,
                ProcessOperationContractNames.ExternalProductTargetReadOnly,
                ProcessOperationContractNames.ManagedOutputProduct,
                ProcessOperationContractNames.ManagedProcessArtifactsOnly
            ],
            ToolCapabilityOperationRequirementKind.DotNetRun =>
            [
                ProcessOperationContractNames.ExternalProductTargetReadOnly
            ],
            ToolCapabilityOperationRequirementKind.ProcessArtifactWrite =>
            [
                ProcessOperationContractNames.ExternalArtifactDestination,
                ProcessOperationContractNames.ManagedProcessArtifactsOnly
            ],
            _ => []
        };
    }

    private static IReadOnlyList<string> ResolveTargetScopesForOperation(string operation)
    {
        return operation switch
        {
            ProcessOperationContractNames.WriteManagedProcessArtifacts =>
            [
                ProcessOperationContractNames.ManagedProcessArtifactsOnly
            ],
            ProcessOperationContractNames.WriteExternalArtifactDestination =>
            [
                ProcessOperationContractNames.ExternalArtifactDestination
            ],
            ProcessOperationContractNames.MutateProductTarget =>
            [
                ProcessOperationContractNames.ExternalProductTargetMutable,
                ProcessOperationContractNames.ManagedOutputProduct
            ],
            ProcessOperationContractNames.RunValidation or
                ProcessOperationContractNames.LaunchRuntime or
                ProcessOperationContractNames.CaptureRuntimeProof or
                ProcessOperationContractNames.ReadProjectStructure =>
            [
                ProcessOperationContractNames.ExternalProductTargetReadOnly
            ],
            ProcessOperationContractNames.ExecuteExternalAction =>
            [
                ProcessOperationContractNames.ExternalActionControlled
            ],
            ProcessOperationContractNames.StartProjectNodeProcess =>
            [
                ProcessOperationContractNames.ExternalActionControlled
            ],
            _ => []
        };
    }

    private static bool CanMutateProduct(
        IReadOnlyList<ToolCapabilityProcessOperationRequirement> requirements,
        ToolCapabilityOperationRequirementKind requirementKind)
    {
        return requirementKind is ToolCapabilityOperationRequirementKind.WorkspaceFileMutation or ToolCapabilityOperationRequirementKind.WorkspaceScript ||
               HasOperationRequirement(requirements, ProcessOperationContractNames.MutateProductTarget);
    }

    private static bool CanExecuteExternalAction(
        IReadOnlyList<ToolCapabilityProcessOperationRequirement> requirements,
        ToolCapabilityOperationRequirementKind requirementKind,
        ToolCapabilitySideEffectKind sideEffectKind)
    {
        return requirementKind == ToolCapabilityOperationRequirementKind.WorkspaceScript ||
               sideEffectKind == ToolCapabilitySideEffectKind.ExternalAction ||
               HasOperationRequirement(requirements, ProcessOperationContractNames.ExecuteExternalAction) ||
               HasOperationRequirement(requirements, ProcessOperationContractNames.StartProjectNodeProcess);
    }

    private static bool CanReadExternalTarget(
        ToolInvocationClassification classification,
        ToolCapabilitySideEffectKind sideEffectKind)
    {
        return classification is ToolInvocationClassification.Read or ToolInvocationClassification.Validation ||
               sideEffectKind is ToolCapabilitySideEffectKind.WorkspaceRead or
                   ToolCapabilitySideEffectKind.WorkspaceWrite or
                   ToolCapabilitySideEffectKind.LocalProcessExecution or
                   ToolCapabilitySideEffectKind.RuntimeLaunch or
                   ToolCapabilitySideEffectKind.RuntimeProofCapture;
    }

    private static bool CanWriteManagedArtifact(
        IReadOnlyList<ToolCapabilityProcessOperationRequirement> requirements,
        ToolCapabilityOperationRequirementKind requirementKind)
    {
        return requirementKind is ToolCapabilityOperationRequirementKind.WorkspaceFileMutation or
                   ToolCapabilityOperationRequirementKind.WorkspaceScript or
                   ToolCapabilityOperationRequirementKind.ProcessArtifactWrite ||
               HasOperationRequirement(requirements, ProcessOperationContractNames.WriteManagedProcessArtifacts);
    }

    private static bool HasOperationRequirement(
        IReadOnlyList<ToolCapabilityProcessOperationRequirement> requirements,
        string operation)
    {
        return requirements.Any(requirement =>
            requirement.AnyOf.Contains(operation, StringComparer.OrdinalIgnoreCase));
    }

    private static ToolCapabilityBrowserProofRole ResolveBrowserProofRole(string name)
    {
        return ToolContractCatalog.NormalizeToolName(name) switch
        {
            ToolContractCatalog.BrowserNavigate or ToolContractCatalog.BrowserResize => ToolCapabilityBrowserProofRole.Navigation,
            ToolContractCatalog.BrowserClick or
                ToolContractCatalog.BrowserFillForm or
                ToolContractCatalog.BrowserSelectOption or
                ToolContractCatalog.BrowserPressKey or
                ToolContractCatalog.BrowserType or
                ToolContractCatalog.BrowserDrag => ToolCapabilityBrowserProofRole.Interaction,
            ToolContractCatalog.BrowserSnapshot or ToolContractCatalog.BrowserTakeScreenshot => ToolCapabilityBrowserProofRole.EvidenceCapture,
            ToolContractCatalog.BrowserConsoleMessages or
                ToolContractCatalog.BrowserEvaluate or
                ToolContractCatalog.BrowserNetworkRequests or
                ToolContractCatalog.BrowserWaitFor => ToolCapabilityBrowserProofRole.Observation,
            _ => ToolCapabilityBrowserProofRole.None
        };
    }

    private static ToolCapabilityIdempotencyDescriptor ResolveIdempotencyDescriptor(
        ToolInvocationClassification classification,
        ToolCapabilitySideEffectKind sideEffectKind)
    {
        if (sideEffectKind is ToolCapabilitySideEffectKind.LocalProcessExecution or ToolCapabilitySideEffectKind.ExternalAction)
        {
            return ToolCapabilityIdempotencyDescriptor.ExternalSideEffect;
        }

        return classification switch
        {
            ToolInvocationClassification.Mutation => ToolCapabilityIdempotencyDescriptor.StateChanging,
            ToolInvocationClassification.Validation => ToolCapabilityIdempotencyDescriptor.RuntimeStateDependent,
            _ => ToolCapabilityIdempotencyDescriptor.Idempotent
        };
    }

    private static bool IsProviderNativeToolFamily(string? toolName)
    {
        if (string.IsNullOrWhiteSpace(toolName))
        {
            return false;
        }

        var trimmed = toolName.Trim();
        return trimmed.StartsWith("provider_native_", StringComparison.OrdinalIgnoreCase) ||
               trimmed.StartsWith("provider-native-", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsHostedMcpToolFamily(string? toolName)
    {
        if (string.IsNullOrWhiteSpace(toolName))
        {
            return false;
        }

        var trimmed = toolName.Trim();
        return trimmed.StartsWith("hosted_mcp_", StringComparison.OrdinalIgnoreCase) ||
               trimmed.StartsWith("hosted-mcp-", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsLocalMcpToolFamily(string? toolName)
    {
        if (string.IsNullOrWhiteSpace(toolName))
        {
            return false;
        }

        return toolName.Trim().StartsWith("mcp_", StringComparison.OrdinalIgnoreCase);
    }
}
