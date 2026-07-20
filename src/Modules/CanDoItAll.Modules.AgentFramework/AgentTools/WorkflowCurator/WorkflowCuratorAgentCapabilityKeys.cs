using System.Collections.Frozen;
using CanDoItAll.AgentFramework.Core;

namespace CanDoItAll.Modules.AgentFramework;

public static class WorkflowCuratorAgentCapabilityKeys
{
    public const string CuratorSkill = "workflow-curator-agent-inline-skill";
    public const string CatalogSearch = "workflow-curator-catalog-search";
    public const string DefinitionEditorGet = "workflow-curator-definition-editor-get";
    public const string AuthoringOptionsGet = "workflow-curator-authoring-options-get";
    public const string DraftCreate = "workflow-curator-draft-create";
    public const string DraftUpdate = "workflow-curator-draft-update";
    public const string NodeUpdate = "workflow-curator-node-update";
    public const string LifecycleChange = "workflow-curator-lifecycle-change";

    public static IReadOnlyDictionary<string, string> ToolNameToCapabilityKey { get; } =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [AgentToolInvocationPolicyMetadata.WorkflowCuratorCatalogSearch] = CatalogSearch,
            [AgentToolInvocationPolicyMetadata.WorkflowCuratorDefinitionEditorGet] = DefinitionEditorGet,
            [AgentToolInvocationPolicyMetadata.WorkflowCuratorAuthoringOptionsGet] = AuthoringOptionsGet,
            [AgentToolInvocationPolicyMetadata.WorkflowCuratorDraftCreate] = DraftCreate,
            [AgentToolInvocationPolicyMetadata.WorkflowCuratorDraftUpdate] = DraftUpdate,
            [AgentToolInvocationPolicyMetadata.WorkflowCuratorNodeUpdate] = NodeUpdate,
            [AgentToolInvocationPolicyMetadata.WorkflowCuratorLifecycleChange] = LifecycleChange
        }.ToFrozenDictionary(StringComparer.Ordinal);

    public static IReadOnlySet<string> PrivilegedKeys { get; } = ToolNameToCapabilityKey.Values
        .Append(CuratorSkill)
        .ToFrozenSet(StringComparer.OrdinalIgnoreCase);
}
