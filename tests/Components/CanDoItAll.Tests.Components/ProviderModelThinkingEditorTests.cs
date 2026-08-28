using Bunit;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Modules.AgentFramework.Pages.Components;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.AgentFramework;

public sealed class ProviderModelThinkingEditorTests {
    [Fact]
    public void Known_defaults_are_visible_and_manual_configuration_can_be_reset() {
        using var context = CreateContext();
        var model = Editor(ProviderKind.OpenAi, "gpt-5.6-sol");
        var cut = context.Render<ProviderModelThinkingEditor>(p => p.Add(c => c.Model, model));
        cut.WaitForAssertion(() => Assert.Contains("Built-in definition", cut.Markup));
        Assert.Contains("Extra high, Max", cut.Markup);
        Edit(cut);
        cut.Find("[data-testid='thinking-automatic']").Change(false);
        cut.Find("[data-testid='thinking-allow-Max']").Change(false);
        Select(cut, "thinking-model-default", "Low");
        cut.Find("[data-testid='thinking-apply']").Click();
        var configured = Assert.Single(ProviderModelThinkingConfiguration.Read(model.ConfigurationJson));
        Assert.DoesNotContain(AgentReasoningEffortLevel.Max, configured.AllowedEfforts);
        Assert.Equal(AgentReasoningEffortLevel.Low, configured.DefaultEffort);
        Assert.Contains("Administrator override", cut.Markup);
        Assert.Contains("timeoutSeconds", model.ConfigurationJson);
        Edit(cut);
        cut.Find("[data-testid='thinking-automatic']").Change(true);
        cut.Find("[data-testid='thinking-apply']").Click();
        Assert.Empty(ProviderModelThinkingConfiguration.Read(model.ConfigurationJson));
        Assert.Contains("Built-in definition", cut.Markup);
        Assert.Contains("Extra high, Max", cut.Markup);
    }

    [Fact]
    public void Custom_ollama_model_supports_explicit_boolean_controls() {
        using var context = CreateContext();
        var model = Editor(ProviderKind.Ollama, "custom-work:latest");
        var cut = context.Render<ProviderModelThinkingEditor>(p => p.Add(c => c.Model, model));
        cut.WaitForAssertion(() => Assert.Contains("Not defined", cut.Markup));
        Edit(cut);
        cut.Find("[data-testid='thinking-automatic']").Change(false);
        cut.Find("[data-testid='thinking-supported']").Change(true);
        Select(cut, "thinking-control-mode", "BooleanToggle");
        Select(cut, "thinking-model-default", "Enabled");
        cut.Find("[data-testid='thinking-apply']").Click();
        var configured = Assert.Single(ProviderModelThinkingConfiguration.Read(model.ConfigurationJson));
        Assert.Equal(AgentThinkingEffortControlMode.BooleanToggle, configured.ControlMode);
        Assert.Equal([AgentReasoningEffortLevel.None, AgentReasoningEffortLevel.Medium], configured.AllowedEfforts);
        Assert.Equal(AgentReasoningEffortLevel.Medium, configured.DefaultEffort);
    }

    [Fact]
    public void Empty_manual_efforts_are_rejected_and_cancel_keeps_original_configuration() {
        using var context = CreateContext();
        var model = Editor(ProviderKind.Ollama, "custom-work:latest");
        var original = model.ConfigurationJson;
        var cut = context.Render<ProviderModelThinkingEditor>(p => p.Add(c => c.Model, model));
        Edit(cut);
        cut.Find("[data-testid='thinking-automatic']").Change(false);
        cut.Find("[data-testid='thinking-supported']").Change(true);
        cut.Find("[data-testid='thinking-apply']").Click();
        Assert.Contains("no allowed efforts", cut.Find("[data-testid='thinking-edit-error']").TextContent);
        cut.FindAll("button").Single(button => button.TextContent.Trim() == "Cancel").Click();
        Assert.Equal(original, model.ConfigurationJson);
        Assert.Empty(cut.FindAll("[data-testid='provider-thinking-dialog']"));
    }

    [Fact]
    public void Explicit_unsupported_has_no_effort_or_default_controls() {
        using var context = CreateContext();
        var model = Editor(ProviderKind.OpenAi, "gpt-5.6-sol");
        var cut = context.Render<ProviderModelThinkingEditor>(p => p.Add(c => c.Model, model));
        Edit(cut);
        cut.Find("[data-testid='thinking-automatic']").Change(false);
        cut.Find("[data-testid='thinking-supported']").Change(false);
        Assert.Empty(cut.FindAll("[data-testid='thinking-model-default']"));
        cut.Find("[data-testid='thinking-apply']").Click();
        var configured = Assert.Single(ProviderModelThinkingConfiguration.Read(model.ConfigurationJson));
        Assert.Equal(AgentThinkingEffortSupportStatus.Unsupported, configured.Status);
        Assert.Empty(configured.AllowedEfforts);
    }

    private static BunitContext CreateContext() {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddCanDoItAllBaseLib();
        return context;
    }

    private static ProviderProfileEditorModel Editor(ProviderKind kind, string model) => new() {
        Name = "Provider", Kind = kind, DefaultModel = model, SuggestedModels = [model],
        ConfigurationJson = "{\"timeoutSeconds\":91}", Transport = kind == ProviderKind.Ollama
            ? ProviderTransportKind.ChatCompletions : ProviderTransportKind.Responses
    };

    private static void Edit(IRenderedComponent<ProviderModelThinkingEditor> cut) =>
        cut.WaitForElement("button[aria-label^='Edit thinking for ']").Click();

    private static void Select(IRenderedComponent<ProviderModelThinkingEditor> cut, string testId, string label) {
        var select = cut.Find($"[data-testid='{testId}']");
        var option = select.QuerySelectorAll("option").Single(item => item.TextContent.Trim() == label);
        select.Change(option.GetAttribute("value"));
    }
}
