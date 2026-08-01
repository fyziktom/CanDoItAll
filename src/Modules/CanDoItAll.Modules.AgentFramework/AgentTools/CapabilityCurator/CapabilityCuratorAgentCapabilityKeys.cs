using System.Collections.Frozen;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Modules.AgentFramework;

public static class CapabilityCuratorAgentCapabilityKeys
{
    public const string CuratorSkill = CapabilityCuratorAgentIdentity.CuratorSkillCapabilityKey;
    public const string CatalogSearch = CapabilityCuratorAgentIdentity.CatalogSearchCapabilityKey;
    public const string EditorGet = CapabilityCuratorAgentIdentity.EditorGetCapabilityKey;
    public const string Save = CapabilityCuratorAgentIdentity.SaveCapabilityKey;
    public const string ToolSetupTest = CapabilityCuratorAgentIdentity.ToolSetupTestCapabilityKey;
    public const string McpSetupTest = CapabilityCuratorAgentIdentity.McpSetupTestCapabilityKey;
    public const string AssignmentEditorGet = CapabilityCuratorAgentIdentity.AssignmentEditorGetCapabilityKey;
    public const string AssignmentUpdate = CapabilityCuratorAgentIdentity.AssignmentUpdateCapabilityKey;
    public const string Verify = CapabilityCuratorAgentIdentity.VerifyCapabilityKey;

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
