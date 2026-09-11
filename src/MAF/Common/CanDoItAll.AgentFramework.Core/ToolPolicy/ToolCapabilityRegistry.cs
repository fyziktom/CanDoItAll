using CanDoItAll.AgentFramework.Capabilities.Abstractions;
using static CanDoItAll.AgentFramework.Core.ToolCapabilityMetadataFactory;
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
    public IReadOnlyList<CapabilityOperationClassification> OperationClassifications { get; init; } = [];

    public string? BusinessArgumentRetentionScheme { get; init; }

    public bool ProtectRuntimeStateOnExport { get; init; }

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
    private static readonly ToolCapabilityOperationEffects DocumentConversionEffects = new(
        [ProcessOperationContractNames.ExternalProductTargetReadOnly, ProcessOperationContractNames.ManagedProcessArtifactsOnly], canWriteManagedArtifact: true);

    private static readonly ToolCapabilityOperationEffects ExternalActionEffects = new(
        [ProcessOperationContractNames.ExternalActionControlled], canExecuteExternalAction: true);

    private static readonly ToolCapabilityOperationEffects ExternalReadEffects = new(
        [ProcessOperationContractNames.ExternalProductTargetReadOnly]);

    private static readonly ToolCapabilityOperationEffects WorkspaceMutationEffects = new(
        [
            ProcessOperationContractNames.ExternalArtifactDestination,
            ProcessOperationContractNames.ExternalProductTargetMutable,
            ProcessOperationContractNames.ManagedOutputProduct,
            ProcessOperationContractNames.ManagedProcessArtifactsOnly
        ], canMutateProduct: true, canWriteManagedArtifact: true);

    private static readonly ToolCapabilityOperationEffects WorkspaceScriptEffects = new(
        [
            ProcessOperationContractNames.ExternalActionControlled,
            ProcessOperationContractNames.ExternalArtifactDestination,
            ProcessOperationContractNames.ExternalProductTargetMutable,
            ProcessOperationContractNames.ExternalProductTargetReadOnly,
            ProcessOperationContractNames.ManagedOutputProduct,
            ProcessOperationContractNames.ManagedProcessArtifactsOnly
        ], canMutateProduct: true, canExecuteExternalAction: true, canWriteManagedArtifact: true);

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
            Mutation(ToolContractCatalog.WorkspaceCreateDirectory, ToolCapabilitySideEffectKind.WorkspaceWrite, ToolCapabilityOperationRequirementKind.WorkspaceFileMutation, effects: WorkspaceMutationEffects),
            Mutation(ToolContractCatalog.WorkspaceWriteFile, ToolCapabilitySideEffectKind.WorkspaceWrite, ToolCapabilityOperationRequirementKind.WorkspaceFileMutation, effects: WorkspaceMutationEffects),
            Mutation(ToolContractCatalog.WorkspaceAppendFile, ToolCapabilitySideEffectKind.WorkspaceWrite, ToolCapabilityOperationRequirementKind.WorkspaceFileMutation, effects: WorkspaceMutationEffects),
            Mutation(ToolContractCatalog.WorkspaceCopyPath, ToolCapabilitySideEffectKind.WorkspaceWrite, ToolCapabilityOperationRequirementKind.WorkspaceFileMutation, effects: WorkspaceMutationEffects),
            Mutation(ToolContractCatalog.WorkspaceMovePath, ToolCapabilitySideEffectKind.WorkspaceWrite, ToolCapabilityOperationRequirementKind.WorkspaceFileMutation, effects: WorkspaceMutationEffects),
            Mutation(ToolContractCatalog.WorkspaceDeletePath, ToolCapabilitySideEffectKind.WorkspaceWrite, ToolCapabilityOperationRequirementKind.WorkspaceFileMutation, effects: WorkspaceMutationEffects),
            Mutation(ToolContractCatalog.WorkspaceZipPath, ToolCapabilitySideEffectKind.WorkspaceWrite, ToolCapabilityOperationRequirementKind.WorkspaceFileMutation, effects: WorkspaceMutationEffects),
            Mutation(ToolContractCatalog.WorkspaceUnzipArchive, ToolCapabilitySideEffectKind.WorkspaceWrite, ToolCapabilityOperationRequirementKind.WorkspaceFileMutation, effects: WorkspaceMutationEffects),
            Read(ToolContractCatalog.WorkspaceDiffText, ToolCapabilitySideEffectKind.WorkspaceRead),
            Mutation(ToolContractCatalog.WorkspaceDotNetNew, ToolCapabilitySideEffectKind.WorkspaceWrite, ToolCapabilityOperationRequirementKind.WorkspaceFileMutation, effects: WorkspaceMutationEffects),
            Validation(ToolContractCatalog.WorkspaceDotNetRestore, ToolCapabilitySideEffectKind.LocalProcessExecution, StaticRequirement(ProcessOperationContractNames.RunValidation), effects: ExternalReadEffects) with { OperationClassifications = Classifications(CapabilityOperationClassification.Validation, CapabilityOperationClassification.ScriptExecution) },
            Validation(ToolContractCatalog.WorkspaceDotNetBuild, ToolCapabilitySideEffectKind.LocalProcessExecution, StaticRequirement(ProcessOperationContractNames.RunValidation), effects: ExternalReadEffects) with { OperationClassifications = Classifications(CapabilityOperationClassification.Validation, CapabilityOperationClassification.ScriptExecution) },
            Validation(ToolContractCatalog.WorkspaceDotNetTest, ToolCapabilitySideEffectKind.LocalProcessExecution, StaticRequirement(ProcessOperationContractNames.RunValidation), effects: ExternalReadEffects) with { OperationClassifications = Classifications(CapabilityOperationClassification.Validation, CapabilityOperationClassification.ScriptExecution) },
            Validation(ToolContractCatalog.WorkspaceDotNetRun, ToolCapabilitySideEffectKind.RuntimeLaunch, ToolCapabilityOperationRequirementKind.DotNetRun, effects: ExternalReadEffects),
            Validation(ToolContractCatalog.WorkspaceDotNetStop, ToolCapabilitySideEffectKind.RuntimeLaunch, StaticRequirement(
                ProcessOperationContractNames.LaunchRuntime,
                ProcessOperationContractNames.CaptureRuntimeProof), effects: ExternalReadEffects) with { OperationClassifications = Classifications(CapabilityOperationClassification.Validation, CapabilityOperationClassification.RuntimeLaunch, CapabilityOperationClassification.ResourceCleanup, CapabilityOperationClassification.ScriptExecution, CapabilityOperationClassification.BrowserAccess) },
            Mutation(AgentToolInvocationPolicyMetadata.WorkspacePowerShellRunScript, ToolCapabilitySideEffectKind.LocalProcessExecution, ToolCapabilityOperationRequirementKind.WorkspaceScript, effects: WorkspaceScriptEffects),
            Mutation(AgentToolInvocationPolicyMetadata.WorkspacePythonRunFile, ToolCapabilitySideEffectKind.LocalProcessExecution, ToolCapabilityOperationRequirementKind.WorkspaceScript, effects: WorkspaceScriptEffects),
            Read(
                ToolContractCatalog.WorkspaceInspectImage,
                ToolCapabilitySideEffectKind.RuntimeProofCapture,
                StaticRequirement(
                    ProcessOperationContractNames.CaptureRuntimeProof,
                    ProcessOperationContractNames.ReadProjectStructure), effects: ExternalReadEffects) with { OperationClassifications = Classifications(CapabilityOperationClassification.Validation, CapabilityOperationClassification.BrowserAccess, CapabilityOperationClassification.ResourceCleanup, CapabilityOperationClassification.Read, CapabilityOperationClassification.ProjectStructure) },
            Read(
                ToolContractCatalog.WorkspaceAnalyzeImage,
                ToolCapabilitySideEffectKind.RuntimeProofCapture,
                StaticRequirement(
                    ProcessOperationContractNames.CaptureRuntimeProof,
                    ProcessOperationContractNames.ReadProjectStructure), effects: ExternalReadEffects) with { OperationClassifications = Classifications(CapabilityOperationClassification.Validation, CapabilityOperationClassification.BrowserAccess, CapabilityOperationClassification.ResourceCleanup, CapabilityOperationClassification.Read, CapabilityOperationClassification.ProjectStructure) },
            Read(
                ToolContractCatalog.WorkspaceAnalyzeImages,
                ToolCapabilitySideEffectKind.RuntimeProofCapture,
                StaticRequirement(
                    ProcessOperationContractNames.CaptureRuntimeProof,
                    ProcessOperationContractNames.ReadProjectStructure), effects: ExternalReadEffects) with { OperationClassifications = Classifications(CapabilityOperationClassification.Validation, CapabilityOperationClassification.BrowserAccess, CapabilityOperationClassification.ResourceCleanup, CapabilityOperationClassification.Read, CapabilityOperationClassification.ProjectStructure) },
            Read(ToolContractCatalog.WorkspaceInspectSpreadsheet, ToolCapabilitySideEffectKind.WorkspaceRead),
            Read(ToolContractCatalog.WorkspaceSpreadsheetSummary, ToolCapabilitySideEffectKind.WorkspaceRead),
            Read(ToolContractCatalog.WorkspaceReadSpreadsheetCell, ToolCapabilitySideEffectKind.WorkspaceRead),
            Read(ToolContractCatalog.WorkspaceReadSpreadsheetRange, ToolCapabilitySideEffectKind.WorkspaceRead),
            Mutation(ToolContractCatalog.WorkspaceWriteSpreadsheet, ToolCapabilitySideEffectKind.WorkspaceWrite, ToolCapabilityOperationRequirementKind.WorkspaceFileMutation, effects: WorkspaceMutationEffects),
            Read(ToolContractCatalog.WorkspaceSpreadsheetFunctionCatalog, ToolCapabilitySideEffectKind.WorkspaceRead),
            Validation(
                ToolContractCatalog.WorkspaceConvertDocument,
                ToolCapabilitySideEffectKind.DocumentConversion,
                StaticRequirement(
                    ProcessOperationContractNames.ReadProjectStructure,
                    ProcessOperationContractNames.WriteManagedProcessArtifacts), effects: DocumentConversionEffects) with { OperationClassifications = Classifications(CapabilityOperationClassification.Read, CapabilityOperationClassification.ProjectStructure, CapabilityOperationClassification.Write) },
            Mutation(
                ToolContractCatalog.WorkspaceCommandRun,
                ToolCapabilitySideEffectKind.LocalProcessExecution,
                StaticRequirement(ProcessOperationContractNames.ExecuteExternalAction), effects: ExternalActionEffects) with { OperationClassifications = Classifications(CapabilityOperationClassification.ExternalAction, CapabilityOperationClassification.ScriptExecution) },
            Read(ToolContractCatalog.WorkspaceExecutionBoundary, ToolCapabilitySideEffectKind.WorkspaceRead),
            Read(ToolContractCatalog.WorkspaceGitDiff, ToolCapabilitySideEffectKind.WorkspaceRead),
            Read(ToolContractCatalog.WorkspaceGitStatus, ToolCapabilitySideEffectKind.WorkspaceRead),
            Read(ToolContractCatalog.WorkspaceGitLog, ToolCapabilitySideEffectKind.WorkspaceRead),
            Read(ToolContractCatalog.WorkspaceGitShow, ToolCapabilitySideEffectKind.WorkspaceRead),
            Mutation(ToolContractCatalog.WorkspaceGitAdd, ToolCapabilitySideEffectKind.WorkspaceWrite, ToolCapabilityOperationRequirementKind.WorkspaceFileMutation, effects: WorkspaceMutationEffects),
            Mutation(ToolContractCatalog.WorkspaceGitUnstage, ToolCapabilitySideEffectKind.WorkspaceWrite, ToolCapabilityOperationRequirementKind.WorkspaceFileMutation, effects: WorkspaceMutationEffects),
            Mutation(ToolContractCatalog.WorkspaceGitCommit, ToolCapabilitySideEffectKind.WorkspaceWrite, ToolCapabilityOperationRequirementKind.WorkspaceFileMutation, effects: WorkspaceMutationEffects),
            Mutation(ToolContractCatalog.WorkspaceGitBranchCreate, ToolCapabilitySideEffectKind.WorkspaceWrite, ToolCapabilityOperationRequirementKind.WorkspaceFileMutation, effects: WorkspaceMutationEffects),
            Mutation(ToolContractCatalog.WorkspaceGitSwitch, ToolCapabilitySideEffectKind.WorkspaceWrite, ToolCapabilityOperationRequirementKind.WorkspaceFileMutation, effects: WorkspaceMutationEffects),
            Mutation(
                ToolContractCatalog.LocalMcpLaunch,
                ToolCapabilitySideEffectKind.LocalProcessExecution,
                StaticRequirement(ProcessOperationContractNames.ExecuteExternalAction), effects: ExternalActionEffects) with { OperationClassifications = Classifications(CapabilityOperationClassification.ExternalAction, CapabilityOperationClassification.ScriptExecution) },
            Read(ToolContractCatalog.ProviderHealth, ToolCapabilitySideEffectKind.InternalDataRead),
            Read(ToolContractCatalog.AgentPackageExport, ToolCapabilitySideEffectKind.WorkspaceRead),
            Validation(ToolContractCatalog.BrowserNavigate, ToolCapabilitySideEffectKind.RuntimeProofCapture, StaticRequirement(ProcessOperationContractNames.CaptureRuntimeProof), effects: ExternalReadEffects) with { OperationClassifications = Classifications(CapabilityOperationClassification.Validation, CapabilityOperationClassification.BrowserAccess, CapabilityOperationClassification.ResourceCleanup) },
            Validation(ToolContractCatalog.BrowserResize, ToolCapabilitySideEffectKind.RuntimeProofCapture, StaticRequirement(ProcessOperationContractNames.CaptureRuntimeProof), effects: ExternalReadEffects) with { OperationClassifications = Classifications(CapabilityOperationClassification.Validation, CapabilityOperationClassification.BrowserAccess, CapabilityOperationClassification.ResourceCleanup) },
            Validation(ToolContractCatalog.BrowserConsoleMessages, ToolCapabilitySideEffectKind.RuntimeProofCapture, StaticRequirement(ProcessOperationContractNames.CaptureRuntimeProof), effects: ExternalReadEffects) with { OperationClassifications = Classifications(CapabilityOperationClassification.Validation, CapabilityOperationClassification.BrowserAccess, CapabilityOperationClassification.ResourceCleanup) },
            Validation(ToolContractCatalog.BrowserEvaluate, ToolCapabilitySideEffectKind.RuntimeProofCapture, StaticRequirement(ProcessOperationContractNames.CaptureRuntimeProof), effects: ExternalReadEffects) with { OperationClassifications = Classifications(CapabilityOperationClassification.Validation, CapabilityOperationClassification.BrowserAccess, CapabilityOperationClassification.ResourceCleanup) },
            Validation(ToolContractCatalog.BrowserNetworkRequests, ToolCapabilitySideEffectKind.RuntimeProofCapture, StaticRequirement(ProcessOperationContractNames.CaptureRuntimeProof), effects: ExternalReadEffects) with { OperationClassifications = Classifications(CapabilityOperationClassification.Validation, CapabilityOperationClassification.BrowserAccess, CapabilityOperationClassification.ResourceCleanup) },
            Validation(ToolContractCatalog.BrowserSnapshot, ToolCapabilitySideEffectKind.RuntimeProofCapture, StaticRequirement(ProcessOperationContractNames.CaptureRuntimeProof), effects: ExternalReadEffects) with { OperationClassifications = Classifications(CapabilityOperationClassification.Validation, CapabilityOperationClassification.BrowserAccess, CapabilityOperationClassification.ResourceCleanup) },
            Validation(ToolContractCatalog.BrowserTakeScreenshot, ToolCapabilitySideEffectKind.RuntimeProofCapture, StaticRequirement(ProcessOperationContractNames.CaptureRuntimeProof), effects: ExternalReadEffects) with { OperationClassifications = Classifications(CapabilityOperationClassification.Validation, CapabilityOperationClassification.BrowserAccess, CapabilityOperationClassification.ResourceCleanup) },
            Validation(ToolContractCatalog.BrowserClick, ToolCapabilitySideEffectKind.RuntimeProofCapture, StaticRequirement(ProcessOperationContractNames.CaptureRuntimeProof), effects: ExternalReadEffects) with { OperationClassifications = Classifications(CapabilityOperationClassification.Validation, CapabilityOperationClassification.BrowserAccess, CapabilityOperationClassification.ResourceCleanup) },
            Validation(ToolContractCatalog.BrowserFillForm, ToolCapabilitySideEffectKind.RuntimeProofCapture, StaticRequirement(ProcessOperationContractNames.CaptureRuntimeProof), effects: ExternalReadEffects) with { OperationClassifications = Classifications(CapabilityOperationClassification.Validation, CapabilityOperationClassification.BrowserAccess, CapabilityOperationClassification.ResourceCleanup) },
            Validation(ToolContractCatalog.BrowserSelectOption, ToolCapabilitySideEffectKind.RuntimeProofCapture, StaticRequirement(ProcessOperationContractNames.CaptureRuntimeProof), effects: ExternalReadEffects) with { OperationClassifications = Classifications(CapabilityOperationClassification.Validation, CapabilityOperationClassification.BrowserAccess, CapabilityOperationClassification.ResourceCleanup) },
            Validation(ToolContractCatalog.BrowserPressKey, ToolCapabilitySideEffectKind.RuntimeProofCapture, StaticRequirement(ProcessOperationContractNames.CaptureRuntimeProof), effects: ExternalReadEffects) with { OperationClassifications = Classifications(CapabilityOperationClassification.Validation, CapabilityOperationClassification.BrowserAccess, CapabilityOperationClassification.ResourceCleanup) },
            Validation(ToolContractCatalog.BrowserType, ToolCapabilitySideEffectKind.RuntimeProofCapture, StaticRequirement(ProcessOperationContractNames.CaptureRuntimeProof), effects: ExternalReadEffects) with { OperationClassifications = Classifications(CapabilityOperationClassification.Validation, CapabilityOperationClassification.BrowserAccess, CapabilityOperationClassification.ResourceCleanup) },
            Validation(ToolContractCatalog.BrowserDrag, ToolCapabilitySideEffectKind.RuntimeProofCapture, StaticRequirement(ProcessOperationContractNames.CaptureRuntimeProof), effects: ExternalReadEffects) with { OperationClassifications = Classifications(CapabilityOperationClassification.Validation, CapabilityOperationClassification.BrowserAccess, CapabilityOperationClassification.ResourceCleanup) },
            Validation(ToolContractCatalog.BrowserWaitFor, ToolCapabilitySideEffectKind.RuntimeProofCapture, StaticRequirement(ProcessOperationContractNames.CaptureRuntimeProof), effects: ExternalReadEffects) with { OperationClassifications = Classifications(CapabilityOperationClassification.Validation, CapabilityOperationClassification.BrowserAccess, CapabilityOperationClassification.ResourceCleanup) },
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
            Mutation(AgentToolInvocationPolicyMetadata.RunSkillScript, ToolCapabilitySideEffectKind.LocalProcessExecution, StaticRequirement(ProcessOperationContractNames.ExecuteExternalAction), effects: ExternalActionEffects) with { OperationClassifications = Classifications(CapabilityOperationClassification.ExternalAction, CapabilityOperationClassification.ScriptExecution) },

        };

        return capabilities.Select(capability => capability with { BrowserProofRole = ResolveBrowserProofRole(capability.Name) }).ToDictionary(
            capability => ToolContractCatalog.NormalizeToolName(capability.Name),
            StringComparer.OrdinalIgnoreCase);
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
