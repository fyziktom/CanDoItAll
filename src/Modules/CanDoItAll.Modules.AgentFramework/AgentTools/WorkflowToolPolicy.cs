using CanDoItAll.AgentFramework.Capabilities.Abstractions;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Core;
using static CanDoItAll.AgentFramework.Core.ToolCapabilityMetadataFactory;

namespace CanDoItAll.Modules.AgentFramework;

public static class WorkflowToolPolicy {
    private static readonly ToolCapabilityOperationEffects ExternalActionEffects = new(
        [ProcessOperationContractNames.ExternalActionControlled], canExecuteExternalAction: true);

    private static readonly ToolCapabilityOperationEffects ExternalReadEffects = new(
        [ProcessOperationContractNames.ExternalProductTargetReadOnly]);

    public const string WorkflowsDefinitionsList = "workflows_definitions_list";
    public const string WorkflowsRunStart = "workflows_run_start";
    public const string WorkflowsRunStatusGet = "workflows_run_status_get";
    public const string WorkflowsRunCancel = "workflows_run_cancel";
    public const string WorkflowsExternalResponseSubmit = "workflows_external_response_submit";

    public static IReadOnlyList<ToolCapabilityMetadata> Capabilities { get; } = Array.AsReadOnly<ToolCapabilityMetadata>([
        Read(WorkflowsDefinitionsList, ToolCapabilitySideEffectKind.None),
        Mutation( WorkflowsRunStart, ToolCapabilitySideEffectKind.RuntimeLaunch, StaticRequirement(ProcessOperationContractNames.LaunchRuntime), effects: ExternalReadEffects) with { OperationClassifications = Classifications(CapabilityOperationClassification.Validation, CapabilityOperationClassification.RuntimeLaunch, CapabilityOperationClassification.ResourceCleanup, CapabilityOperationClassification.ScriptExecution), BusinessArgumentRetentionScheme = "workflow-curator-approval-redacted-v1" },
        Read(WorkflowsRunStatusGet, ToolCapabilitySideEffectKind.None),
        Mutation( WorkflowsRunCancel, ToolCapabilitySideEffectKind.RuntimeLaunch, StaticRequirement(ProcessOperationContractNames.ExecuteExternalAction), effects: ExternalActionEffects) with { OperationClassifications = Classifications(CapabilityOperationClassification.ExternalAction, CapabilityOperationClassification.ScriptExecution) },
        Mutation( WorkflowsExternalResponseSubmit, ToolCapabilitySideEffectKind.RuntimeLaunch, StaticRequirement(ProcessOperationContractNames.ExecuteExternalAction), effects: ExternalActionEffects) with { OperationClassifications = Classifications(CapabilityOperationClassification.ExternalAction, CapabilityOperationClassification.ScriptExecution), BusinessArgumentRetentionScheme = "workflow-curator-approval-redacted-v1" }
    ]);
}
