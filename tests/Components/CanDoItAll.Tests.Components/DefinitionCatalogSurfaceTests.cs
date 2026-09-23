using System.Collections.Immutable;
using Bunit;
using CanDoItAll.AgentFramework.Llm.SimpleChats.UI;
using CanDoItAll.AgentFramework.UiSandbox;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Conversations.Components;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.LlmChats;

public sealed class DefinitionCatalogSurfaceTests {
    [Theory]
    [InlineData(DefinitionCatalogScenario.Denied)]
    [InlineData(DefinitionCatalogScenario.Loading)]
    [InlineData(DefinitionCatalogScenario.Empty)]
    [InlineData(DefinitionCatalogScenario.ReadOnly)]
    [InlineData(DefinitionCatalogScenario.Manageable)]
    [InlineData(DefinitionCatalogScenario.Filtered)]
    [InlineData(DefinitionCatalogScenario.Paged)]
    [InlineData(DefinitionCatalogScenario.Adversarial)]
    public void Surface_renders_without_effect_services_and_preserves_card_presentation(DefinitionCatalogScenario scenario) {
        using var context = CreateContext();
        var presentation = DefinitionCatalogSandboxFixture.Create(scenario);
        var cut = context.Render<LlmChatDefinitionCatalogSurface>(p => p.Add(x => x.Presentation, presentation)
            .Add(x => x.Limits, DefinitionCatalogSandboxFixture.Limits));
        Assert.Empty(cut.FindAll("script"));
        Assert.DoesNotContain("SystemPrompt", cut.Markup);
        Assert.DoesNotContain("SchemaJson", cut.Markup);
        Assert.Equal(presentation.CanManage, cut.FindAll("[data-testid='llm-chat-definition-create']").Count == 1);
        if (presentation.Cards.Length > 0 && presentation.CanRead) {
            var card = cut.FindComponent<ConversationParticipantCard>().Instance.Participant;
            Assert.Equal(presentation.Cards[0].Name, card.DisplayName);
            Assert.Equal("Revision 3", card.Subtitle);
            Assert.Equal("Active", Assert.Single(card.Badges).Text);
            Assert.Equal(presentation.Cards[0].Tags, card.Tags);
            Assert.Equal(presentation.Cards[0].DefinitionId.ToString("D"), card.AvatarSeed);
        }
        if (presentation.Failure is not null) {
            Assert.Contains(presentation.Error!, cut.Find("[data-testid='llm-chat-definition-error']").TextContent);
        }
        Assert.Same(presentation, cut.Instance.Presentation);
    }

    [Fact]
    public async Task Typed_actions_emit_once_and_local_filters_survive_same_controlled_snapshot() {
        using var context = CreateContext();
        var presentation = DefinitionCatalogSandboxFixture.Create(DefinitionCatalogScenario.Paged);
        var intents = new List<DefinitionCatalogIntent>();
        var cut = context.Render<LlmChatDefinitionCatalogSurface>(p => p.Add(x => x.Presentation, presentation)
            .Add(x => x.Limits, DefinitionCatalogSandboxFixture.Limits).Add(x => x.OnIntent, intents.Add));
        await cut.Find("[data-testid='llm-chat-definition-search']").InputAsync("draft search");
        cut.Render();
        Assert.Equal("draft search", cut.Find("[data-testid='llm-chat-definition-search']").GetAttribute("value"));
        await cut.InvokeAsync(() => cut.FindComponent<TagEditor>().Instance.ValueChanged.InvokeAsync(["research"]));
        await cut.Find("[data-testid='llm-chat-definition-status-filter']").ChangeAsync("2");
        await cut.Find("[data-testid='llm-chat-definition-filter-reset']").ClickAsync();
        await cut.Find("[data-testid='llm-chat-definition-load-more']").ClickAsync();
        await cut.Find("[data-testid='llm-chat-definition-create']").ClickAsync();
        await cut.Find($"[data-testid='llm-chat-definition-edit-{DefinitionCatalogSandboxFixture.DefinitionA:D}']").ClickAsync();
        Assert.Collection(intents,
            value => Assert.Equal(new DefinitionCatalogIntent.SearchChanged("draft search"), value),
            value => Assert.Equal(new[] { "research" }, Assert.IsType<DefinitionCatalogIntent.TagsChanged>(value).Value.ToArray()),
            value => Assert.Equal(new DefinitionCatalogIntent.StatusChanged(LlmChatDefinitionStatusFilter.Active), value),
            value => Assert.IsType<DefinitionCatalogIntent.ResetFilters>(value),
            value => Assert.IsType<DefinitionCatalogIntent.LoadMore>(value),
            value => Assert.IsType<DefinitionCatalogIntent.CreateDefinition>(value),
            value => Assert.Equal(new DefinitionCatalogIntent.EditDefinition(DefinitionCatalogSandboxFixture.DefinitionA), value));
        Assert.Equal("", cut.Find("[data-testid='llm-chat-definition-search']").GetAttribute("value"));
        Assert.Same(presentation, cut.Instance.Presentation);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Card_selection_emits_only_for_manageable_catalog(bool manageable) {
        using var context = CreateContext();
        var intents = new List<DefinitionCatalogIntent>();
        var cut = context.Render<LlmChatDefinitionCatalogSurface>(p =>
            p.Add(x => x.Presentation, DefinitionCatalogSandboxFixture.Create(DefinitionCatalogScenario.ReadOnly) with { CanManage = manageable })
                .Add(x => x.Limits, DefinitionCatalogSandboxFixture.Limits).Add(x => x.OnIntent, intents.Add));
        await cut.Find($"[data-testid='llm-chat-definition-select-{DefinitionCatalogSandboxFixture.DefinitionA:D}']").ClickAsync();
        Assert.Equal(manageable ? 1 : 0, intents.Count);
    }

    private static BunitContext CreateContext() {
        var context = new BunitContext();
        context.Services.AddLogging();
        context.Services.AddCanDoItAllBaseLib();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        return context;
    }
}
