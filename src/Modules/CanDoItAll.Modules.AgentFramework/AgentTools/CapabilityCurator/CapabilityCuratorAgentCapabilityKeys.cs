using System.Collections.Frozen;
using CanDoItAll.AgentFramework.Core;

namespace CanDoItAll.Modules.AgentFramework;

public static class CapabilityCuratorAgentCapabilityKeys
{
    public const string CuratorSkill = "capability-curator-agent-inline-skill";
    public const string CatalogSearch = "capability-curator-catalog-search";
    public const string EditorGet = "capability-curator-editor-get";
    public const string Save = "capability-curator-save";
    public const string ToolSetupTest = "capability-curator-tool-setup-test";
    public const string McpSetupTest = "capability-curator-mcp-setup-test";
    public const string AssignmentEditorGet = "capability-curator-assignment-editor-get";
    public const string AssignmentUpdate = "capability-curator-assignment-update";
    public const string Verify = "capability-curator-verify";

    public static IReadOnlyDictionary<string, string> ToolNameToCapabilityKey { get; } =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [AgentToolInvocationPolicyMetadata.CapabilityCuratorCatalogSearch] = CatalogSearch,
            [AgentToolInvocationPolicyMetadata.CapabilityCuratorEditorGet] = EditorGet,
            [AgentToolInvocationPolicyMetadata.CapabilityCuratorSave] = Save,
            [AgentToolInvocationPolicyMetadata.CapabilityCuratorToolSetupTest] = ToolSetupTest,
            [AgentToolInvocationPolicyMetadata.CapabilityCuratorMcpSetupTest] = McpSetupTest,
            [AgentToolInvocationPolicyMetadata.CapabilityCuratorAssignmentEditorGet] = AssignmentEditorGet,
            [AgentToolInvocationPolicyMetadata.CapabilityCuratorAssignmentUpdate] = AssignmentUpdate,
            [AgentToolInvocationPolicyMetadata.CapabilityCuratorVerify] = Verify
        }.ToFrozenDictionary(StringComparer.Ordinal);

    public static IReadOnlySet<string> PrivilegedKeys { get; } = ToolNameToCapabilityKey.Values
        .Append(CuratorSkill)
        .ToFrozenSet(StringComparer.OrdinalIgnoreCase);
}
