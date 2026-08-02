namespace CanDoItAll.AgentFramework.Models;

public static class WorkflowCuratorAgentIdentity
{
    public const string StableIdKey = "agents/workflows-curator-agent";
    public const string TemplateKey = "workflow-curator-agent";
    public const string DefaultDisplayName = "Workflow Curator Agent";
    public const string DefaultAvatarImageUrl = AgentAvatarImageCatalog.BundledAvatarBasePath + "avatar-04.jpg";
    public const string RuntimeAccessVersionPropertyName = "workflowRuntimeAccessVersion";
    public const string CurrentRuntimeAccessVersion = "2026-08-workflow-runtime-access-v1";

    public static Guid AgentId { get; } = new("248343b3-85d6-c35c-b121-c392722fb51d");

    public static bool Matches(AgentDefinition? agent)
    {
        return agent is not null &&
               agent.Id == AgentId &&
               string.Equals(agent.TemplateKey, TemplateKey, StringComparison.Ordinal);
    }
}
