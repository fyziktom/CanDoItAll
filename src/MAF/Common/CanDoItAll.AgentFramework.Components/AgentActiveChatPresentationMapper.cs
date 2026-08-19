using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Conversations.Components.Presentation;

namespace CanDoItAll.AgentFramework.Components;

public static class AgentActiveChatPresentationMapper
{
    private static readonly ConversationPresentationKey OpenActionKey = new("open");
    private static readonly ConversationPresentationKey StopActionKey = new("stop");

    public static ConversationActiveItemPresentation Map(
        ActiveAgentChat chat,
        ConversationPresentationKey? key = null,
        bool? isFocused = null)
    {
        ArgumentNullException.ThrowIfNull(chat);
        var focused = isFocused ?? chat.IsVisible;

        return new(
            key ?? Key(chat.HandleId),
            chat.Agent.Name,
            [
                new(
                    focused ? "Open" : "Kept active",
                    focused ? PresentationTone.Success : PresentationTone.Default),
                new(ResolveRunStateLabel(chat.RunState), ResolveRunStateTone(chat.RunState))
            ],
            [
                new(
                    OpenActionKey,
                    "Open",
                    "open_in_new",
                    isDisabled: focused),
                new(
                    StopActionKey,
                    "Stop",
                    "stop_circle",
                    isDisabled: !chat.CanStop,
                    style: ConversationActionStyle.Danger)
            ]);
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

    public static AgentActiveChatPresentationAction ResolveAction(ConversationPresentationKey key)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (key == OpenActionKey)
        {
            return AgentActiveChatPresentationAction.Open;
        }

        if (key == StopActionKey)
        {
            return AgentActiveChatPresentationAction.Stop;
        }

        throw new ArgumentException(
            $"The conversation action key '{key.Value}' is not an Agent active-chat action.",
            nameof(key));
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

public enum AgentActiveChatPresentationAction
{
    Open,
    Stop
}
