using Bunit;
using CanDoItAll.AgentFramework.Llm.SimpleChats.UI;
using CanDoItAll.AgentFramework.UiSandbox;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Conversations.Components;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.AgentFramework;

public sealed class ConversationDialogSurfaceTests {
    [Fact]
    public async Task Start_picker_filters_actual_cards_and_emits_exact_definition_title_and_paging_intents() {
        using var context = Context();
        var state = new ConversationDialogPresentation(new(7, ConversationDialogKind.Start),
            Definitions: [DefinitionCatalogSandboxFixture.Card()], HasMoreDefinitions: true);
        var intents = new List<ConversationDialogIntent>();
        var cut = context.Render<LlmChatConversationDialogSurface>(p => p.Add(c => c.Presentation, state).Add(c => c.Intent, intents.Add));
        Assert.True(cut.Find("[data-testid='llm-chat-start-confirm']").HasAttribute("disabled"));
        cut.Find("[data-testid='llm-chat-start-definition-search']").Input("absent definition");
        Assert.Contains("No matching definitions", cut.Markup, StringComparison.Ordinal);
        cut.Find("[data-testid='llm-chat-start-definition-search']").Input("research");
        await cut.Find($"[data-testid='llm-chat-start-definition-{DefinitionCatalogSandboxFixture.DefinitionA:D}']").ClickAsync();
        cut.Find("[data-testid='llm-chat-start-title']").Change("Exact draft");
        await cut.Find("[data-testid='llm-chat-start-definitions-load-more']").ClickAsync();
        Assert.Equal(new[] { ConversationDialogAction.SelectDefinition, ConversationDialogAction.TitleChanged, ConversationDialogAction.LoadMoreDefinitions },
            intents.Select(intent => intent.Action));
        Assert.All(intents, intent => Assert.Equal(state.Origin, intent.Origin));
        Assert.Equal(DefinitionCatalogSandboxFixture.DefinitionA, intents[0].DefinitionId);
        Assert.Equal("Exact draft", intents[1].Text);

        cut.Render(p => p.Add(c => c.Presentation, state with { SelectedDefinitionId = DefinitionCatalogSandboxFixture.DefinitionA, Title = "Accepted title" }));
        await cut.Find("[data-testid='llm-chat-start-confirm']").ClickAsync();
        Assert.Equal(ConversationDialogAction.Confirm, intents[^1].Action);
    }

    [Theory]
    [InlineData(ConversationDialogKind.Start)]
    [InlineData(ConversationDialogKind.Rename)]
    public async Task Retired_input_selection_and_close_callbacks_cannot_retarget_a_reopened_dialog(ConversationDialogKind kind) {
        using var context = Context();
        var state = new ConversationDialogPresentation(new(1, kind, Guid.NewGuid()), "Original title",
            Definitions: [DefinitionCatalogSandboxFixture.Card()]);
        var original = new List<ConversationDialogIntent>();
        var current = new List<ConversationDialogIntent>();
        var cut = context.Render<LlmChatConversationDialogSurface>(p => p.Add(c => c.Presentation, state).Add(c => c.Intent, original.Add));
        var title = cut.FindComponents<TextBox>().Last().Instance.ValueChanged;
        var close = cut.FindComponent<Dialog>().Instance.OnClose;
        var selection = cut.FindComponents<ConversationParticipantCard>().FirstOrDefault()?.Instance.Selected;
        cut.Render(p => p.Add(c => c.Presentation, state with { Origin = state.Origin with { Generation = 3 }, Title = "Current title" })
            .Add(c => c.Intent, current.Add));
        await cut.InvokeAsync(() => title.InvokeAsync("Retired title"));
        await cut.InvokeAsync(() => close.InvokeAsync());
        if (selection is { } selected) {
            await cut.InvokeAsync(() => selected.InvokeAsync(DefinitionCatalogMapping.ToKey(DefinitionCatalogSandboxFixture.DefinitionA)));
        }
        Assert.Empty(original);
        Assert.Empty(current);
        Assert.Contains("Current title", cut.Markup, StringComparison.Ordinal);
        var activeClose = cut.FindComponent<Dialog>().Instance.OnClose;
        await context.DisposeRenderedComponentsAsync();
        await context.Renderer.Dispatcher.InvokeAsync(() => activeClose.InvokeAsync());
        Assert.Empty(current);
    }

    [Fact]
    public async Task Archive_requires_exact_confirmation_and_preserves_independent_opening_identity() {
        using var context = Context();
        var first = new ConversationDialogPresentation(new(1, ConversationDialogKind.Archive, Guid.NewGuid()), "Same title");
        var second = first with { Origin = new(2, ConversationDialogKind.Archive, Guid.NewGuid()) };
        var firstIntents = new List<ConversationDialogIntent>();
        var secondIntents = new List<ConversationDialogIntent>();
        var cut = context.Render<LlmChatConversationDialogSurface>(p => p.Add(c => c.Presentation, first).Add(c => c.Intent, firstIntents.Add));
        var neighbor = context.Render<LlmChatConversationDialogSurface>(p => p.Add(c => c.Presentation, second).Add(c => c.Intent, secondIntents.Add));
        var confirmation = cut.FindComponent<DangerActionDialog>();
        var button = confirmation.FindAll("button").Last(item => item.TextContent.Contains("Archive", StringComparison.Ordinal));
        Assert.True(button.HasAttribute("disabled"));
        confirmation.Find("input").Input(first.Title);
        await confirmation.FindAll("button").Last(item => item.TextContent.Contains("Archive", StringComparison.Ordinal)).ClickAsync();
        Assert.Equal(new(first.Origin, ConversationDialogAction.Confirm), Assert.Single(firstIntents));
        Assert.Empty(secondIntents);
        Assert.Equal("", neighbor.FindComponent<DangerActionDialog>().Find("input").GetAttribute("value"));
    }

    [Fact]
    public async Task Empty_and_busy_start_states_keep_confirm_disabled_and_refuse_mutation_callbacks() {
        using var context = Context();
        var state = new ConversationDialogPresentation(new(1, ConversationDialogKind.Start));
        var intents = new List<ConversationDialogIntent>();
        var cut = context.Render<LlmChatConversationDialogSurface>(p => p.Add(c => c.Presentation, state).Add(c => c.Intent, intents.Add));
        Assert.Contains("No active definitions", cut.Markup, StringComparison.Ordinal);
        Assert.True(cut.Find("[data-testid='llm-chat-start-confirm']").HasAttribute("disabled"));
        cut.Render(p => p.Add(c => c.Presentation, state with { Title = "Busy", IsMutating = true, IsLoading = true, HasMoreDefinitions = true }));
        Assert.True(cut.Find("[data-testid='llm-chat-start-title']").HasAttribute("disabled"));
        Assert.True(cut.Find("[data-testid='llm-chat-start-definitions-load-more']").HasAttribute("disabled"));
        await cut.InvokeAsync(() => cut.FindComponents<TextBox>().Last().Instance.ValueChanged.InvokeAsync("Do not mutate"));
        Assert.Empty(intents);
        await cut.InvokeAsync(() => cut.FindComponent<Dialog>().Instance.OnClose.InvokeAsync());
        Assert.Equal(ConversationDialogAction.Close, Assert.Single(intents).Action);
    }

    private static BunitContext Context() {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddLogging();
        context.Services.AddCanDoItAllBaseLib();
        return context;
    }
}
