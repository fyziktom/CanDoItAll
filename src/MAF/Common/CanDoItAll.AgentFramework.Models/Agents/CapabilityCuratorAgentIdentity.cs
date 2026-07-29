using System.Collections.Frozen;

namespace CanDoItAll.AgentFramework.Models;

public static class CapabilityCuratorAgentIdentity
{
    public const string StableIdKey = "agents/capability-curator-agent";
    public const string TemplateKey = "capability-curator-agent";
    public const string DefaultDisplayName = "Capability Curator Agent";
    public const string DefaultAvatarImageUrl = AgentAvatarImageCatalog.BundledAvatarBasePath + "avatar-05.jpg";
    public const string CuratorSkillCapabilityKey = "capability-curator-agent-inline-skill";
    public const string CatalogSearchCapabilityKey = "capability-curator-catalog-search";
    public const string EditorGetCapabilityKey = "capability-curator-editor-get";
    public const string SaveCapabilityKey = "capability-curator-save";
    public const string ToolSetupTestCapabilityKey = "capability-curator-tool-setup-test";
    public const string McpSetupTestCapabilityKey = "capability-curator-mcp-setup-test";
    public const string AssignmentEditorGetCapabilityKey = "capability-curator-assignment-editor-get";
    public const string AssignmentUpdateCapabilityKey = "capability-curator-assignment-update";
    public const string VerifyCapabilityKey = "capability-curator-verify";

    public static Guid AgentId { get; } = new("8b7e3bc0-c7a7-b05a-af39-3446336ff2f7");

    public static IReadOnlySet<string> ToolCapabilityKeys { get; } = new[]
    {
        CatalogSearchCapabilityKey,
        EditorGetCapabilityKey,
        SaveCapabilityKey,
        ToolSetupTestCapabilityKey,
        McpSetupTestCapabilityKey,
        AssignmentEditorGetCapabilityKey,
        AssignmentUpdateCapabilityKey,
        VerifyCapabilityKey
    }.ToFrozenSet(StringComparer.Ordinal);

    public static bool Matches(AgentDefinition? agent)
    {
        return agent is not null &&
               agent.Id == AgentId &&
               string.Equals(agent.TemplateKey, TemplateKey, StringComparison.Ordinal);
    }
}
