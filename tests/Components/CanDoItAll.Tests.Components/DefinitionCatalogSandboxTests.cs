using Bunit;
using CanDoItAll.AgentFramework.Llm.SimpleChats.UI;
using CanDoItAll.AgentFramework.UiSandbox;
using CanDoItAll.AgentFramework.UiSandbox.Components;
using CanDoItAll.Components.BaseLib;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.LlmChats;

public sealed class DefinitionCatalogSandboxTests {
    [Fact]
    public async Task Specimen_uses_only_controlled_presentation_and_logs_actions_without_an_editor() {
        using var context = new BunitContext();
        context.Services.AddLogging();
        context.Services.AddCanDoItAllBaseLib();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        var cut = context.Render<DefinitionCatalogSpecimen>();
        Assert.Equal(8, cut.FindAll("#definition-catalog-scenario option").Count);
        await cut.Find("[data-testid='llm-chat-definition-create']").ClickAsync();
        Assert.Contains(nameof(DefinitionCatalogIntent.CreateDefinition), cut.Find("[data-testid='sandbox-intent']").TextContent);
        Assert.Empty(cut.FindAll("[data-testid='llm-chat-definition-editor']"));
        Assert.Equal(SandboxSpecimen.SimpleChatDefinitions, SandboxSpecimens.Parse("simple-chat-definitions"));
        var fixture = new DefinitionCatalogSandboxFixture();
        fixture.SetScenario(DefinitionCatalogScenario.Paged);
        fixture.Apply(new DefinitionCatalogIntent.LoadMore());
        Assert.Equal(2, fixture.Presentation.Cards.Length);
        Assert.False(fixture.Presentation.HasMore);
        fixture.Apply(new DefinitionCatalogIntent.SearchChanged("local only"));
        Assert.Equal("local only", fixture.Presentation.Filters.Search);
        fixture.Apply(new DefinitionCatalogIntent.ResetFilters());
        Assert.False(fixture.Presentation.Filters.IsActive);
    }
}
