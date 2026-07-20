namespace CanDoItAll.AgentFramework.Models;

public static class HrAgentIdentity
{
    public const string StableIdKey = "agents/hr-agent";
    public const string TemplateKey = "hr-agent";
    public const string DefaultDisplayName = "HR Agent";
    public const string DefaultAvatarImageUrl = AgentAvatarImageCatalog.BundledAvatarBasePath + "avatar-07.jpg";

    public static Guid AgentId { get; } = new("8efe3e66-484d-b757-a62d-ee0331266bf4");

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
