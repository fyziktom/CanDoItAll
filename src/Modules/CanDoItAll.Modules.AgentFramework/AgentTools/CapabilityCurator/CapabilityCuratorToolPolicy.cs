using CanDoItAll.AgentFramework.Capabilities.Abstractions;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Core;
using static CanDoItAll.AgentFramework.Core.ToolCapabilityMetadataFactory;

namespace CanDoItAll.Modules.AgentFramework;

public static class CapabilityCuratorToolPolicy {
    private static readonly ToolCapabilityOperationEffects ExternalActionEffects = new(
        [ProcessOperationContractNames.ExternalActionControlled], canExecuteExternalAction: true);

    public const string CapabilityCuratorCatalogSearch = "capability_curator_catalog_search";
    public const string CapabilityCuratorEditorGet = "capability_curator_editor_get";
    public const string CapabilityCuratorAssignmentEditorGet = "capability_curator_assignment_editor_get";
    public const string CapabilityCuratorSave = "capability_curator_save";
    public const string CapabilityCuratorToolSetupTest = "capability_curator_tool_setup_test";
    public const string CapabilityCuratorMcpSetupTest = "capability_curator_mcp_setup_test";
    public const string CapabilityCuratorAssignmentUpdate = "capability_curator_assignment_update";
    public const string CapabilityCuratorVerify = "capability_curator_verify";

    public static IReadOnlyList<ToolCapabilityMetadata> Capabilities { get; } = Array.AsReadOnly<ToolCapabilityMetadata>([
        Read(CapabilityCuratorCatalogSearch, ToolCapabilitySideEffectKind.InternalDataRead) with { BusinessArgumentRetentionScheme = "capability-curator-approval-redacted-v1" },
        Read(CapabilityCuratorEditorGet, ToolCapabilitySideEffectKind.InternalDataRead),
        Read(CapabilityCuratorAssignmentEditorGet, ToolCapabilitySideEffectKind.InternalDataRead),
        Mutation(CapabilityCuratorSave, ToolCapabilitySideEffectKind.InternalStateMutation) with { BusinessArgumentRetentionScheme = "capability-curator-approval-redacted-v1" },
        Mutation( CapabilityCuratorToolSetupTest, ToolCapabilitySideEffectKind.ExternalAction, StaticRequirement(ProcessOperationContractNames.ExecuteExternalAction), effects: ExternalActionEffects) with { OperationClassifications = Classifications(CapabilityOperationClassification.ExternalAction, CapabilityOperationClassification.ScriptExecution), BusinessArgumentRetentionScheme = "capability-curator-approval-redacted-v1" },
        Mutation( CapabilityCuratorMcpSetupTest, ToolCapabilitySideEffectKind.ExternalAction, StaticRequirement(ProcessOperationContractNames.ExecuteExternalAction), effects: ExternalActionEffects) with { OperationClassifications = Classifications(CapabilityOperationClassification.ExternalAction, CapabilityOperationClassification.ScriptExecution), BusinessArgumentRetentionScheme = "capability-curator-approval-redacted-v1" },
        Mutation(CapabilityCuratorAssignmentUpdate, ToolCapabilitySideEffectKind.InternalStateMutation) with { BusinessArgumentRetentionScheme = "capability-curator-approval-redacted-v1" },
        Mutation( CapabilityCuratorVerify, ToolCapabilitySideEffectKind.ExternalAction, StaticRequirement(ProcessOperationContractNames.ExecuteExternalAction), effects: ExternalActionEffects) with { OperationClassifications = Classifications(CapabilityOperationClassification.ExternalAction, CapabilityOperationClassification.ScriptExecution), BusinessArgumentRetentionScheme = "capability-curator-approval-redacted-v1" }
    ]);
}
