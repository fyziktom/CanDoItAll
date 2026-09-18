using CanDoItAll.AgentFramework.Llm.Abstractions;
using Microsoft.AspNetCore.Components;

namespace CanDoItAll.AgentFramework.Llm.SimpleChats.Components;

// TargetKey identifies the conversation the content is inserted into so a contributor can drop an interaction
// that was started for a different selection, independent of provider/model equality.
public sealed record LlmChatComposerActionContext(
    LlmConversationProviderSnapshot ProviderModel,
    EventCallback<string> ContentSelected,
    object? TargetKey = null);

public interface ILlmChatComposerActionContributor
{
    RenderFragment Render(LlmChatComposerActionContext context);
}
