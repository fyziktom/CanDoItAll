using CanDoItAll.AgentFramework.Llm.SimpleChats.Components;
using CanDoItAll.Modules.Prompts.Components;
using Microsoft.AspNetCore.Components;

namespace CanDoItAll.Modules.AgentFramework;

public sealed class PromptGalleryLlmChatComposerActionContributor : ILlmChatComposerActionContributor
{
    public RenderFragment Render(LlmChatComposerActionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return builder =>
        {
            builder.OpenComponent<PromptGalleryChatComposerButton>(0);
            builder.AddAttribute(
                1,
                nameof(PromptGalleryChatComposerButton.Provider),
                context.ProviderModel.ProviderKind.ToString());
            builder.AddAttribute(
                2,
                nameof(PromptGalleryChatComposerButton.Model),
                context.ProviderModel.Model);
            builder.AddAttribute(
                3,
                nameof(PromptGalleryChatComposerButton.ContentSelected),
                context.ContentSelected);
            builder.CloseComponent();
        };
    }
}
