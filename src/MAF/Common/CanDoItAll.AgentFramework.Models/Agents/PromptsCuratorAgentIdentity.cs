namespace CanDoItAll.AgentFramework.Models;

public static class PromptsCuratorAgentIdentity
{
    public const string StableIdKey = "agents/prompts-curator-agent";
    public const string TemplateKey = "prompts-curator-agent";

    public static Guid AgentId { get; } = new("4cb32d12-253b-bd5c-9af6-506a836129a8");

    public static bool Matches(AgentDefinition? agent)
    {
        return agent is not null &&
               agent.Id == AgentId &&
               string.Equals(agent.TemplateKey, TemplateKey, StringComparison.Ordinal);
    }
}
