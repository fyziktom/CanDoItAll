namespace CanDoItAll.AgentFramework.Models;

public static class DeliveryManagerAgentIdentity
{
    public const string StableIdKey = "agents/delivery-manager";
    public const string TemplateKey = "delivery-manager";

    public static Guid AgentId { get; } = new("b0c2a317-a417-385f-9f59-0f4a2c5b7ff2");

    public static bool Matches(AgentDefinition? agent)
    {
        return agent is not null &&
               agent.Id == AgentId &&
               string.Equals(agent.TemplateKey, TemplateKey, StringComparison.Ordinal);
    }
}
