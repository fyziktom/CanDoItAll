using CanDoItAll.AgentFramework.Core;
using static CanDoItAll.AgentFramework.Core.ToolCapabilityMetadataFactory;

namespace CanDoItAll.Modules.AgentFramework;

public static class WorkflowCuratorToolPolicy {
    public const string WorkflowCuratorCatalogSearch = "workflow_curator_catalog_search";
    public const string WorkflowCuratorDefinitionEditorGet = "workflow_curator_definition_editor_get";
    public const string WorkflowCuratorAuthoringOptionsGet = "workflow_curator_authoring_options_get";
    public const string WorkflowCuratorDraftCreate = "workflow_curator_draft_create";
    public const string WorkflowCuratorDraftUpdate = "workflow_curator_draft_update";
    public const string WorkflowCuratorNodeUpdate = "workflow_curator_node_update";
    public const string WorkflowCuratorLifecycleChange = "workflow_curator_lifecycle_change";

    public static IReadOnlyList<ToolCapabilityMetadata> Capabilities { get; } = Array.AsReadOnly<ToolCapabilityMetadata>([
        Read(WorkflowCuratorCatalogSearch, ToolCapabilitySideEffectKind.InternalDataRead) with { BusinessArgumentRetentionScheme = "workflow-curator-approval-redacted-v1" },
        Read(WorkflowCuratorDefinitionEditorGet, ToolCapabilitySideEffectKind.InternalDataRead),
        Read(WorkflowCuratorAuthoringOptionsGet, ToolCapabilitySideEffectKind.InternalDataRead),
        Mutation(WorkflowCuratorDraftCreate, ToolCapabilitySideEffectKind.InternalStateMutation) with { BusinessArgumentRetentionScheme = "workflow-curator-approval-redacted-v1" },
        Mutation(WorkflowCuratorDraftUpdate, ToolCapabilitySideEffectKind.InternalStateMutation) with { BusinessArgumentRetentionScheme = "workflow-curator-approval-redacted-v1" },
        Mutation(WorkflowCuratorNodeUpdate, ToolCapabilitySideEffectKind.InternalStateMutation) with { BusinessArgumentRetentionScheme = "workflow-curator-approval-redacted-v1" },
        Mutation(WorkflowCuratorLifecycleChange, ToolCapabilitySideEffectKind.InternalStateMutation) with { BusinessArgumentRetentionScheme = "workflow-curator-approval-redacted-v1" }
    ]);
}
