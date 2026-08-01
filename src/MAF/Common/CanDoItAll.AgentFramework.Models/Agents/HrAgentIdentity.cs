using System.Collections.Frozen;

namespace CanDoItAll.AgentFramework.Models;

public static class HrAgentIdentity
{
    public const string StableIdKey = "agents/hr-agent";
    public const string TemplateKey = "hr-agent";
    public const string DefaultDisplayName = "HR Agent";
    public const string DefaultAvatarImageUrl = AgentAvatarImageCatalog.BundledAvatarBasePath + "avatar-07.jpg";
    public const string CapabilityCurationSkillCapabilityKey = "hr-agent-capability-curation-inline-skill";
    public const string CapabilityCurationAccessVersionPropertyName = "hrCapabilityCurationAccessVersion";
    public const string CurrentCapabilityCurationAccessVersion = "2026-07-hr-capability-curation-v1";

    public static Guid AgentId { get; } = new("8efe3e66-484d-b757-a62d-ee0331266bf4");

    public static IReadOnlySet<string> CapabilityCurationCapabilityKeys { get; } = new[]
    {
        CapabilityCurationSkillCapabilityKey,
        CapabilityCuratorAgentIdentity.CatalogSearchCapabilityKey,
        CapabilityCuratorAgentIdentity.EditorGetCapabilityKey,
        CapabilityCuratorAgentIdentity.SaveCapabilityKey,
        CapabilityCuratorAgentIdentity.ToolSetupTestCapabilityKey,
        CapabilityCuratorAgentIdentity.McpSetupTestCapabilityKey,
        CapabilityCuratorAgentIdentity.VerifyCapabilityKey
    }.ToFrozenSet(StringComparer.Ordinal);

    public static bool Matches(AgentDefinition? agent)
    {
        return agent is not null &&
               agent.Id == AgentId &&
               string.Equals(agent.TemplateKey, TemplateKey, StringComparison.Ordinal);
    }
}

public static class HrAgentExecutionSourceKinds
{
    public const string ManagerReview = "hr-manager-review";
}
