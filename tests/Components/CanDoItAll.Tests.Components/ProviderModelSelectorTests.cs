using AngleSharp.Dom;
using Bunit;
using CanDoItAll.AgentFramework.Components;
using CanDoItAll.AgentFramework.Models;
using Microsoft.AspNetCore.Components;

namespace CanDoItAll.Tests.Components.AgentFramework;

public sealed class ProviderModelSelectorTests
{
    [Fact]
    public void ProviderModelSelector_lists_provider_default_and_suggested_models()
    {
        using var context = new BunitContext();
        var provider = CreateProvider("gpt-5-mini", ["gpt-5-mini", "gpt-5.4"]);
        string? selectedModel = "gpt-5.4";

        var cut = context.Render<ProviderModelSelector>(parameters => parameters
            .Add(component => component.Provider, provider)
            .Add(component => component.Value, selectedModel)
            .Add(component => component.ValueChanged, EventCallback.Factory.Create<string?>(this, value => selectedModel = value))
            .Add(component => component.ChoiceTestId, "model-choice")
            .Add(component => component.OverrideTestId, "model-override")
            .Add(component => component.CustomModelTestId, "model-text"));

        var options = cut.Find("[data-testid='model-choice']")
            .QuerySelectorAll("option")
            .Select(option => option.TextContent)
            .ToList();

        Assert.Equal(["Provider default (gpt-5-mini)", "gpt-5.4"], options);

        cut.Find("[data-testid='model-choice']").Change("0");
        Assert.Equal(string.Empty, selectedModel);

        cut.Find("[data-testid='model-choice']").Change("1");
        Assert.Equal("gpt-5.4", selectedModel);
    }

    [Fact]
    public void ProviderModelSelector_keeps_custom_model_behind_override()
    {
        using var context = new BunitContext();
        var provider = CreateProvider("gpt-5-mini", ["gpt-5.4"]);
        string? selectedModel = string.Empty;

        var cut = context.Render<ProviderModelSelector>(parameters => parameters
            .Add(component => component.Provider, provider)
            .Add(component => component.Value, selectedModel)
            .Add(component => component.ValueChanged, EventCallback.Factory.Create<string?>(this, value => selectedModel = value))
            .Add(component => component.ChoiceTestId, "model-choice")
            .Add(component => component.OverrideTestId, "model-override")
            .Add(component => component.CustomModelTestId, "model-text"));

        Assert.Empty(cut.FindAll("[data-testid='model-text']"));

        cut.Find("[data-testid='model-override']").Change(true);
        cut.Find("[data-testid='model-text']").Input(" custom-model ");

        Assert.Equal("custom-model", selectedModel);
    }

    [Fact]
    public void ProviderModelSelector_unchecking_override_returns_to_provider_default()
    {
        using var context = new BunitContext();
        var provider = CreateProvider("gpt-5-mini", ["gpt-5.4"]);
        string? selectedModel = "custom-model";

        var cut = context.Render<ProviderModelSelector>(parameters => parameters
            .Add(component => component.Provider, provider)
            .Add(component => component.Value, selectedModel)
            .Add(component => component.ValueChanged, EventCallback.Factory.Create<string?>(this, value => selectedModel = value))
            .Add(component => component.ChoiceTestId, "model-choice")
            .Add(component => component.OverrideTestId, "model-override")
            .Add(component => component.CustomModelTestId, "model-text"));

        Assert.Equal("custom-model", cut.Find("[data-testid='model-text']").GetAttribute("value"));

        cut.Find("[data-testid='model-override']").Change(false);

        Assert.Equal(string.Empty, selectedModel);
        Assert.Empty(cut.FindAll("[data-testid='model-text']"));
    }

    [Fact]
    public void ProviderModelSelector_starts_in_override_for_unknown_existing_model()
    {
        using var context = new BunitContext();
        var provider = CreateProvider("gpt-5-mini", ["gpt-5.4"]);

        var cut = context.Render<ProviderModelSelector>(parameters => parameters
            .Add(component => component.Provider, provider)
            .Add(component => component.Value, "claude-3-5-sonnet")
            .Add(component => component.ChoiceTestId, "model-choice")
            .Add(component => component.OverrideTestId, "model-override")
            .Add(component => component.CustomModelTestId, "model-text"));

        Assert.Equal("claude-3-5-sonnet", cut.Find("[data-testid='model-text']").GetAttribute("value"));
    }

    [Fact]
    public void ProviderModelSelector_treats_non_empty_provider_default_value_as_explicit_override()
    {
        using var context = new BunitContext();
        var provider = CreateProvider("gpt-5-mini", ["gpt-5.4"]);

        var cut = context.Render<ProviderModelSelector>(parameters => parameters
            .Add(component => component.Provider, provider)
            .Add(component => component.Value, "gpt-5-mini")
            .Add(component => component.ChoiceTestId, "model-choice")
            .Add(component => component.OverrideTestId, "model-override")
            .Add(component => component.CustomModelTestId, "model-text"));

        Assert.Equal("gpt-5-mini", cut.Find("[data-testid='model-text']").GetAttribute("value"));
    }

    [Fact]
    public void ProviderModelSelector_supports_scalar_provider_metadata_for_workflow_surfaces()
    {
        using var context = new BunitContext();
        string? selectedModel = "llama3.1";

        var cut = context.Render<ProviderModelSelector>(parameters => parameters
            .Add(component => component.ProviderName, "Local Ollama")
            .Add(component => component.ProviderDefaultModel, "llama3.1")
            .Add(component => component.ModelOptions, ["llama3.1", "llama3.2"])
            .Add(component => component.Value, selectedModel)
            .Add(component => component.ValueChanged, EventCallback.Factory.Create<string?>(this, value => selectedModel = value))
            .Add(component => component.UseEmptyValueForProviderDefault, false)
            .Add(component => component.ChoiceTestId, "model-choice")
            .Add(component => component.OverrideTestId, "model-override")
            .Add(component => component.CustomModelTestId, "model-text"));

        var options = cut.Find("[data-testid='model-choice']")
            .QuerySelectorAll("option")
            .Select(option => option.TextContent)
            .ToList();

        Assert.Equal(["Provider default (llama3.1)", "llama3.2"], options);

        cut.Find("[data-testid='model-choice']").Change("0");
        Assert.Equal("llama3.1", selectedModel);
    }

    private static ProviderProfile CreateProvider(
        string defaultModel,
        IReadOnlyList<string> suggestedModels)
        => new(
            Guid.NewGuid(),
            "OpenAI default",
            ProviderKind.OpenAi,
            "https://api.openai.com/v1",
            "OPENAI_API_KEY",
            defaultModel,
            ProviderTransportKind.Responses,
            IsEnabled: true,
            SupportsStreaming: true,
            SupportsTools: true,
            PreferFrameworkManagedChatHistory: false,
            SupportsBackgroundResponses: true,
            ConfigurationJson: "{}",
            Notes: string.Empty,
            HealthStatus: "Not checked",
            LastCheckedAtUtc: null,
            SuggestedModels: suggestedModels);
}
