using AngleSharp.Dom;
using Bunit;
using CanDoItAll.AgentFramework.Components;
using CanDoItAll.AgentFramework.Models;
using Microsoft.AspNetCore.Components;

namespace CanDoItAll.Tests.Components.AgentFramework;

public sealed class ProviderModelSelectorTests
{
    [Fact]
    public void SharedThinkingEffort_Model_suggestions_are_curated_but_legacy_selection_survives() {
        using var context = new BunitContext();
        var provider = CreateProvider("gpt-5.4-mini",
            ["gpt-3.5-turbo", "gpt-5.6-sol", "gpt-5.4-mini", "gpt-4.1", "gpt-5.4-2026-03-05"]);
        var cut = context.Render<ProviderModelSelector>(parameters => parameters
            .Add(component => component.Provider, provider));
        Assert.Equal(["Provider default (gpt-5.4-mini)", "gpt-4.1", "gpt-5.6-sol"],
            cut.FindAll("option").Select(option => option.TextContent));

        cut.Render(parameters => parameters.Add(component => component.Value, "gpt-3.5-turbo"));
        Assert.Contains(cut.FindAll("option"), option => option.TextContent == "gpt-3.5-turbo");
        Assert.Empty(cut.FindAll("input[type='text']"));
    }

    [Fact]
    public void SharedThinkingEffort_Shared_models_sort_by_real_name_numerically_and_keep_published_legacy_selection() {
        using var context = new BunitContext();
        var provider = CreateProvider("route-default", ["route-z", "route-ten", "route-two"]) with {
            CredentialBinding = new(Guid.NewGuid(), ProviderCredentialPurpose.SourceAccessToken,
                ProviderCredentialConsumerKind.Source, Guid.NewGuid()),
            ModelCatalog = [new("route-default", "default"), new("route-z", "zeta"),
                new("route-ten", "model10"), new("route-two", "model2"), new("route-old", "legacy")]
        };
        var cut = context.Render<ProviderModelSelector>(parameters => parameters
            .Add(component => component.Provider, provider)
            .Add(component => component.Value, "route-old"));
        Assert.Equal(["Provider default (default)", "legacy", "model2", "model10", "zeta"],
            cut.FindAll("option").Select(option => option.TextContent));
        Assert.DoesNotContain("Unavailable", cut.Markup);
    }
    [Fact]
    public void Shared_model_labels_emit_routing_ids_and_disable_custom_overrides() {
        using var context = new BunitContext();
        var provider = CreateProvider("route-alpha", ["route-alpha", "route-beta"]) with {
            CredentialBinding = new(Guid.NewGuid(), ProviderCredentialPurpose.SourceAccessToken,
                ProviderCredentialConsumerKind.Source, Guid.NewGuid()),
            ModelCatalog = [new("route-alpha", "model-alpha"), new("route-beta", "model-beta")]
        };
        string? selected = "route-alpha";
        var cut = context.Render<ProviderModelSelector>(parameters => parameters
            .Add(component => component.Provider, provider)
            .Add(component => component.Value, selected)
            .Add(component => component.ValueChanged, EventCallback.Factory.Create<string?>(this, value => selected = value))
            .Add(component => component.ChoiceTestId, "model-choice")
            .Add(component => component.OverrideTestId, "model-override"));

        Assert.Equal(["Provider default (model-alpha)", "model-beta"],
            cut.FindAll("option").Select(option => option.TextContent));
        Assert.Empty(cut.FindAll("[data-testid='model-override']"));
        cut.Find("[data-testid='model-choice']").Change("1");
        Assert.Equal("route-beta", selected);
        cut.Find("[data-testid='model-choice']").Change("0");
        Assert.Equal(string.Empty, selected);
    }

    [Fact]
    public void Removed_shared_model_is_explicitly_unavailable_and_does_not_show_its_routing_id() {
        using var context = new BunitContext();
        var provider = CreateProvider("route-alpha", ["route-alpha"]) with {
            CredentialBinding = new(Guid.NewGuid(), ProviderCredentialPurpose.SourceAccessToken,
                ProviderCredentialConsumerKind.Source, Guid.NewGuid()),
            ModelCatalog = [new("route-alpha", "model-alpha")]
        };
        var cut = context.Render<ProviderModelSelector>(parameters => parameters
            .Add(component => component.Provider, provider)
            .Add(component => component.Value, "removed-route"));

        Assert.Contains("Unavailable shared model", cut.Markup);
        Assert.DoesNotContain("removed-route", cut.Markup);
        cut.Render(parameters => parameters.Add(component => component.Disabled, true));
        Assert.Contains("Unavailable shared model", cut.Markup);
        Assert.DoesNotContain("removed-route", cut.Markup);
    }

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
