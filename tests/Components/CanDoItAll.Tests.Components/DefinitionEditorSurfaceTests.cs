using Bunit;
using CanDoItAll.AgentFramework.Llm.SimpleChats.UI;
using CanDoItAll.AgentFramework.UiSandbox;
using CanDoItAll.Components.BaseLib;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.LlmChats;

public sealed class DefinitionEditorSurfaceTests {
    [Theory]
    [InlineData(DefinitionEditorScenario.Loading)]
    [InlineData(DefinitionEditorScenario.Denied)]
    [InlineData(DefinitionEditorScenario.New)]
    [InlineData(DefinitionEditorScenario.Existing)]
    [InlineData(DefinitionEditorScenario.ProviderPartial)]
    [InlineData(DefinitionEditorScenario.Validation)]
    [InlineData(DefinitionEditorScenario.Conflict)]
    [InlineData(DefinitionEditorScenario.Adversarial)]
    public void Surface_renders_all_scenarios_without_feature_services(DefinitionEditorScenario scenario) {
        using var context = CreateContext();
        var presentation = DefinitionEditorSandboxFixture.Create(scenario);
        var cut = context.Render<LlmChatDefinitionEditorSurface>(p => p.Add(x => x.Presentation, presentation));
        Assert.NotNull(cut.Find("[data-testid='llm-chat-definition-editor-dialog']"));
        Assert.Empty(cut.FindAll("#editor-injected"));
        if (presentation.Source is not null) {
            Assert.Equal(presentation.Source.Name, cut.Find("[data-testid='llm-chat-definition-name']").GetAttribute("value"));
            cut.Find("[data-testid='llm-chat-definition-tab-runtime']").Click();
            Assert.Equal(presentation.Source.SystemPrompt, cut.Find("[data-testid='llm-chat-definition-system-prompt']").GetAttribute("value"));
            if (scenario == DefinitionEditorScenario.ProviderPartial) {
                Assert.Contains(presentation.ProviderError, cut.Markup, StringComparison.Ordinal);
            }
        } else {
            Assert.Empty(cut.FindAll("[data-testid='llm-chat-definition-editor-save']"));
        }
    }

    [Fact]
    public void Local_draft_survives_provider_state_change_and_save_emits_one_immutable_snapshot() {
        using var context = CreateContext();
        var presentation = DefinitionEditorSandboxFixture.Create(DefinitionEditorScenario.Existing);
        var intents = new List<DefinitionEditorIntent>();
        var cut = context.Render<LlmChatDefinitionEditorSurface>(p => p.Add(x => x.Presentation, presentation).Add(x => x.Intent, value => intents.Add(value)));
        cut.Find("[data-testid='llm-chat-definition-name']").Change("Edited name");
        cut.Render(p => p.Add(x => x.Presentation, presentation with { ProvidersUnavailable = true }));
        cut.Find("[data-testid='llm-chat-definition-editor-save']").Click();
        var saved = Assert.Single(intents);
        Assert.Equal(DefinitionEditorAction.Save, saved.Action);
        Assert.Equal("Edited name", saved.Submission!.Name);
        cut.Find("[data-testid='llm-chat-definition-name']").Change("Later name");
        Assert.Equal("Edited name", saved.Submission.Name);
        Assert.NotEqual("Edited name", presentation.Source!.Name);
    }

    [Fact]
    public void Status_reload_and_cancel_emit_typed_actions_without_mutations() {
        using var context = CreateContext();
        var presentation = DefinitionEditorSandboxFixture.Create(DefinitionEditorScenario.Conflict);
        var intents = new List<DefinitionEditorIntent>();
        var cut = context.Render<LlmChatDefinitionEditorSurface>(p => p.Add(x => x.Presentation, presentation).Add(x => x.Intent, value => intents.Add(value)));
        cut.Find("[data-testid='llm-chat-definition-status-active']").Click();
        cut.Find("[data-testid='llm-chat-definition-editor-reload']").Click();
        cut.Find("[data-testid='llm-chat-definition-editor-cancel']").Click();
        Assert.Equal([DefinitionEditorAction.ChangeStatus, DefinitionEditorAction.Reload, DefinitionEditorAction.Cancel], intents.Select(intent => intent.Action));
        Assert.Equal(LlmChatDefinitionStatusFilter.Active, intents[0].Status);
        Assert.All(intents, intent => Assert.Equal(presentation.Generation, intent.Generation));
    }

    private static BunitContext CreateContext() {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddLogging();
        context.Services.AddCanDoItAllBaseLib();
        return context;
    }
}
