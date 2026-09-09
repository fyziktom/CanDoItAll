using Bunit;
using CanDoItAll.AgentFramework.Llm.SimpleChats.UI;
using CanDoItAll.AgentFramework.UiSandbox;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Conversations.Components;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.LlmChats;

public sealed class ConversationWorkspaceSurfaceTests {
    [Theory]
    [InlineData(ConversationWorkspaceScenario.Loading)]
    [InlineData(ConversationWorkspaceScenario.Denied)]
    [InlineData(ConversationWorkspaceScenario.Empty)]
    [InlineData(ConversationWorkspaceScenario.ListReady)]
    [InlineData(ConversationWorkspaceScenario.Selected)]
    [InlineData(ConversationWorkspaceScenario.TranscriptLoading)]
    [InlineData(ConversationWorkspaceScenario.LongTranscript)]
    [InlineData(ConversationWorkspaceScenario.Streaming)]
    [InlineData(ConversationWorkspaceScenario.Recovery)]
    [InlineData(ConversationWorkspaceScenario.Focused)]
    public void Every_scenario_renders_without_feature_services(ConversationWorkspaceScenario scenario) {
        using var context = Context();
        var presentation = ConversationWorkspaceSandboxFixture.Create(scenario);
        var cut = context.Render<LlmChatConversationSurface>(p => p.Add(x => x.Presentation, presentation));
        Assert.Empty(cut.FindAll("#conversation-injected"));
        if (presentation.Selected is not null && presentation.CanRead && !presentation.IsAuthorizing) {
            Assert.Equal(presentation.Selected.Title, cut.Find("[data-testid='llm-chat-selected-title']").TextContent);
        }
        if (scenario == ConversationWorkspaceScenario.Streaming) {
            Assert.Single(cut.FindAll("[data-testid='conversation-message'][data-state='pending']"));
            Assert.Single(cut.FindAll("[data-testid='conversation-message'][data-state='streaming']"));
        }
        if (scenario == ConversationWorkspaceScenario.Focused) {
            Assert.Empty(cut.FindAll("[data-testid='llm-chat-thread-search']"));
            Assert.Contains("llm-chat-conversation-workspace--focused", cut.Find("[data-testid='llm-chat-conversation-workspace']").ClassList);
        }
    }

    [Fact]
    public async Task Every_action_emits_typed_identity_and_generation_without_effects() {
        using var context = Context();
        var presentation = ConversationWorkspaceSandboxFixture.Create(ConversationWorkspaceScenario.Selected) with {
            Generation = 27, HasMoreConversations = true, HasMoreMessages = true, CanCancel = true, CanReconcile = true, CanAbandon = true, CanReloadSelected = true, OperationStatus = "Synthetic action contract"
        };
        var intents = new List<ConversationWorkspaceIntent>();
        var cut = context.Render<LlmChatConversationSurface>(p => p.Add(x => x.Presentation, presentation).Add(x => x.Intent, value => intents.Add(value)));
        var id = ConversationWorkspaceSandboxFixture.ConversationId;
        await cut.InvokeAsync(() => cut.FindComponent<ConversationThreadRail>().Instance.Selected.InvokeAsync(ConversationWorkspaceMapping.ToKey(id)));
        foreach (var testId in new[] { "llm-chat-thread-load-more", "llm-chat-transcript-load-more", "llm-chat-reload-selected", "llm-chat-new", $"llm-chat-rename-{id:D}", $"llm-chat-archive-{id:D}", "llm-chat-send", "llm-chat-operation-cancel", "llm-chat-operation-reconcile", "llm-chat-operation-abandon" }) {
            cut.Find($"[data-testid='{testId}']").Click();
        }
        await cut.InvokeAsync(() => cut.FindComponent<ConversationComposer>().Instance.DraftPromptChanged.InvokeAsync("Changed draft"));
        Assert.Equal(Enum.GetValues<ConversationWorkspaceAction>().Order(), intents.Select(x => x.Action).Order());
        Assert.All(intents, intent => Assert.Equal(27, intent.Generation));
        Assert.All(intents.Where(x => x.Action is ConversationWorkspaceAction.Select or ConversationWorkspaceAction.Rename or ConversationWorkspaceAction.Archive), intent => Assert.Equal(id, intent.ConversationId));
        Assert.Equal("Sample draft", presentation.DraftPrompt);
    }
    private static BunitContext Context() {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddLogging();
        context.Services.AddCanDoItAllBaseLib();
        return context;
    }
}
