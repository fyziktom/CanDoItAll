using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Modules.AgentFramework;

public static class PromptGalleryAgentChatContextBuilder
{
    public const string SourceKind = "prompt-gallery";
    public const string SourceId = "gallery";
    public const string Route = "/prompt-gallery";
    public const string Module = "prompts";
    public const string Surface = "prompt-gallery";
    public const string View = "library";

    public static AgentChatContextSurface Build()
        => new(
            new AgentChatContextSource(
                new AgentChatContextSourceKind(SourceKind),
                new AgentChatContextSourceId(SourceId)),
            "Prompt Gallery",
            new AgentChatSurfacePosition(
                Module,
                Surface,
                View,
                Route),
            agentAccess:
            [
                new AgentChatContextAgentAccess(
                    PromptsCuratorAgentIdentity.AgentId,
                    AgentChatContextPermission.Read | AgentChatContextPermission.Mutate,
                    "Prompt Gallery")
            ],
            accessMode: AgentChatContextScopeAccessMode.Unrestricted);
}
