using Bunit;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using static CanDoItAll.Tests.Components.LlmChats.LlmChatConversationWorkspaceTests;

namespace CanDoItAll.Tests.Components.LlmChats;

public sealed class SimpleChatsWorkspaceLifetimeTests {
    [Fact]
    public async Task Supporting_definition_view_keeps_the_same_conversation_and_unsent_composer_without_rereading_it() {
        var original = CreateConversation();
        var conversations = new StubConversationGateway();
        conversations.ListPages.Enqueue(new([original], null));
        conversations.ListPages.Enqueue(new([original], null));
        conversations.TranscriptPages.Enqueue(CreateView(original, []));
        conversations.TranscriptPages.Enqueue(CreateView(original, []));
        using var context = CreateContext(conversations, new StubOperationGateway());
        context.Services.AddSimpleChatsComponents();
        context.Services.AddSingleton<ILlmChatDefinitionEditorAccess>(new LlmChatDefinitionUiTests.StubDefinitionEditorAccess(new StubAuthorization()));
        IRenderedComponent<SimpleChatsWorkspace>? cut = null;
        var changed = EventCallback.Factory.Create<SimpleChatWorkspaceRouteState>(this, state => cut!.Render(parameters => parameters
            .Add(component => component.View, state.View)
            .Add(component => component.ConversationId, state.ConversationId)
            .Add(component => component.DefinitionId, state.DefinitionId)));
        context.SetRendererInfo(new Microsoft.AspNetCore.Components.RendererInfo("Server", true));
        cut = context.Render<SimpleChatsWorkspace>(parameters => parameters
            .Add(component => component.View, SimpleChatWorkspaceView.Conversations)
            .Add(component => component.ConversationId, original.ConversationId)
            .Add(component => component.RouteStateChanged, changed));
        var conversationOwner = cut.FindComponent<LlmChatConversationWorkspace>().Instance;
        Assert.Empty(cut.FindComponents<LlmChatDefinitionCatalogPanel>());
        await cut.WaitForElement("[data-testid='llm-chat-prompt']").InputAsync("Unsent message retained across supporting views");

        await cut.Find("[data-testid='llm-chats-tab-definitions']").ClickAsync();
        cut.WaitForAssertion(() => Assert.Single(cut.FindComponents<LlmChatDefinitionCatalogPanel>()));
        await cut.Find("[data-testid='llm-chats-tab-conversations']").ClickAsync();

        Assert.Same(conversationOwner, cut.FindComponent<LlmChatConversationWorkspace>().Instance);
        Assert.Equal("Unsent message retained across supporting views", cut.Find("[data-testid='llm-chat-prompt']").GetAttribute("value"));
        Assert.Single(conversations.TranscriptQueries);
        Assert.Single(conversations.ListQueries);
    }
}
