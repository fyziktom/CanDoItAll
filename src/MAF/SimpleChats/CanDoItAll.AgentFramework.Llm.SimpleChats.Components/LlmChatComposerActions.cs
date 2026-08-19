using CanDoItAll.AgentFramework.Llm.Abstractions;
using Microsoft.AspNetCore.Components;

namespace CanDoItAll.AgentFramework.Llm.SimpleChats.Components;

public sealed record LlmChatComposerActionContext(
    LlmConversationProviderSnapshot ProviderModel,
    EventCallback<string> ContentSelected);

public interface ILlmChatComposerActionContributor
{
    RenderFragment Render(LlmChatComposerActionContext context);
}
