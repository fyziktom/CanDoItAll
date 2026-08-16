using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Conversations.Components.Presentation;

namespace CanDoItAll.AgentFramework.Components;

public static class AgentActiveChatPresentationMapper
{
    public static ConversationActiveItemPresentation Map(ActiveAgentChat chat)
    {
        ArgumentNullException.ThrowIfNull(chat);

        return new(
            Key(chat.HandleId),
            chat.Agent.Name,
            [
                new(
                    chat.IsVisible ? "Open" : "Kept active",
                    chat.IsVisible ? PresentationTone.Success : PresentationTone.Default),
                new(ResolveRunStateLabel(chat.RunState), ResolveRunStateTone(chat.RunState))
            ],
            chat.IsVisible,
            chat.CanStop);
    }

    public static ConversationPresentationKey Key(AgentChatHandleId handleId)
        => new(handleId.Value.ToString("N"));

    public static AgentChatHandleId ResolveHandleId(ConversationPresentationKey key)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (!Guid.TryParseExact(key.Value, "N", out var value))
        {
            throw new ArgumentException(
                $"The conversation presentation key '{key.Value}' is not an Agent chat handle.",
                nameof(key));
        }

        return new(value);
    }

    private static string ResolveRunStateLabel(ActiveAgentChatRunState runState)
        => runState switch
        {
            ActiveAgentChatRunState.Running => "Running",
            ActiveAgentChatRunState.AwaitingApproval => "Awaiting approval",
            _ => "Ready"
        };

    private static PresentationTone ResolveRunStateTone(ActiveAgentChatRunState runState)
        => runState switch
        {
            ActiveAgentChatRunState.Running => PresentationTone.Info,
            ActiveAgentChatRunState.AwaitingApproval => PresentationTone.Warning,
            _ => PresentationTone.Success
        };
}
