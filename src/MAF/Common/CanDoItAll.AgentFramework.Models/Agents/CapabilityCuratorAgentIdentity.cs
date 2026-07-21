namespace CanDoItAll.AgentFramework.Models;

public static class CapabilityCuratorAgentIdentity
{
    public const string StableIdKey = "agents/capability-curator-agent";
    public const string TemplateKey = "capability-curator-agent";
    public const string DefaultDisplayName = "Capability Curator Agent";
    public const string DefaultAvatarImageUrl = AgentAvatarImageCatalog.BundledAvatarBasePath + "avatar-05.jpg";

    public static Guid AgentId { get; } = new("8b7e3bc0-c7a7-b05a-af39-3446336ff2f7");

    public static bool Matches(AgentDefinition? agent)
    {
        return agent is not null &&
               agent.Id == AgentId &&
               string.Equals(agent.TemplateKey, TemplateKey, StringComparison.Ordinal);
    }
}
