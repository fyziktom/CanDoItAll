using Bunit;
using CanDoItAll.AgentFramework.Components;
using CanDoItAll.AgentFramework.Models;
using Microsoft.AspNetCore.Components;

namespace CanDoItAll.Tests.Components.AgentFramework;

public sealed class AgentThinkingEffortSettingsTests
{
    private const string SelectorTestId = "agents-catalog-thinking-effort";
    private const string SupportTestId = "agents-catalog-thinking-effort-support";

    [Fact]
    public void SharedThinkingEffort_UsesPublishedCapabilitiesForOpaqueModelId() {
        using var context = new BunitContext();
        const string route = "opaque-published-model";
        var capability = AgentThinkingEffortPolicy.ResolveDefinedCapability(
            ProviderKind.OpenAi, ProviderTransportKind.ChatCompletions, "gpt-5.6-sol") with { Model = route };
        var provider = CreateProvider(ProviderKind.OpenAi, route, capabilities: [capability]) with {
            CredentialBinding = new ProviderCredentialBinding(Guid.NewGuid(),
                ProviderCredentialPurpose.SourceAccessToken, ProviderCredentialConsumerKind.Source, Guid.NewGuid()),
            ModelCatalog = [new ProviderModelDisplayMetadata(route, "gpt-5.6-sol")]
        };
        var cut = context.Render<AgentThinkingEffortSettings>(parameters => parameters
            .Add(component => component.Provider, provider));

        Assert.False(cut.Find($"[data-testid='{SelectorTestId}']").HasAttribute("disabled"));
        Assert.Equal(["Provider default", "None (disable thinking)", "Low", "Medium", "High", "Extra high", "Max"],
            ReadOptionLabels(cut));
        Assert.Equal(AgentReasoningEffortLevel.High, AgentThinkingEffortPolicy.ResolveEffectiveEffort(
            provider, route, AgentThinkingEffortPolicy.WriteAgentOverride("{}", AgentReasoningEffortLevel.High)));
    }

    [Fact]
    public void Supported_model_lists_allowed_efforts_and_configured_provider_default()
    {
        using var context = new BunitContext();
        var provider = CreateProvider(
            ProviderKind.OpenAi,
            "gpt-5.4",
            AgentReasoningEffortLevel.Medium);

        var cut = context.Render<AgentThinkingEffortSettings>(parameters => parameters
            .Add(component => component.Provider, provider)
            .Add(component => component.Model, string.Empty)
            .Add(component => component.Value, null));

        Assert.Equal(
            [
                "Provider default (medium)",
                "None (disable thinking)",
                "Low",
                "Medium",
                "High",
                "Extra high"
            ],
            ReadOptionLabels(cut));
        Assert.False(cut.Find($"[data-testid='{SelectorTestId}']").HasAttribute("disabled"));
        Assert.Contains(
            "None explicitly disables thinking",
            cut.Find($"[data-testid='{SupportTestId}']").TextContent,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Supported_model_emits_override_and_provider_default_reset()
    {
        using var context = new BunitContext();
        var provider = CreateProvider(ProviderKind.OpenAi, "gpt-5.4");
        var changes = new List<AgentReasoningEffortLevel?>();
        var callback = EventCallback.Factory.Create<AgentReasoningEffortLevel?>(
            this,
            value => changes.Add(value));

        var cut = context.Render<AgentThinkingEffortSettings>(parameters => parameters
            .Add(component => component.Provider, provider)
            .Add(component => component.Value, null)
            .Add(component => component.ValueChanged, callback));

        cut.Find($"[data-testid='{SelectorTestId}']").Change("2");
        cut.Render(parameters => parameters
            .Add(component => component.Provider, provider)
            .Add(component => component.Value, AgentReasoningEffortLevel.Low)
            .Add(component => component.ValueChanged, callback));
        cut.Find($"[data-testid='{SelectorTestId}']").Change("0");

        Assert.Equal(
            [AgentReasoningEffortLevel.Low, null],
            changes);
    }

    [Fact]
    public void Original_gpt5_lists_minimal_and_does_not_offer_none()
    {
        using var context = new BunitContext();
        var provider = CreateProvider(ProviderKind.OpenAi, "gpt-5");

        var cut = context.Render<AgentThinkingEffortSettings>(parameters => parameters
            .Add(component => component.Provider, provider)
            .Add(component => component.Value, null));

        var optionLabels = ReadOptionLabels(cut);
        Assert.Equal(
            ["Provider default", "Minimal", "Low", "Medium", "High"],
            optionLabels);
        Assert.DoesNotContain("None (disable thinking)", optionLabels);
        Assert.DoesNotContain(
            "None explicitly disables thinking",
            cut.Find($"[data-testid='{SupportTestId}']").TextContent,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Invalid_provider_default_is_visible_and_can_be_replaced_by_a_valid_override()
    {
        using var context = new BunitContext();
        var provider = CreateProvider(
            ProviderKind.OpenAi,
            "gpt-5.4",
            AgentReasoningEffortLevel.Max);

        var cut = context.Render<AgentThinkingEffortSettings>(parameters => parameters
            .Add(component => component.Provider, provider)
            .Add(component => component.Value, null));

        Assert.False(cut.Find($"[data-testid='{SelectorTestId}']").HasAttribute("disabled"));
        Assert.Equal("Provider default (unavailable)", ReadOptionLabels(cut)[0]);
        var guidance = cut.Find($"[data-testid='{SupportTestId}']").TextContent;
        Assert.Contains("provider default cannot be applied", guidance, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Select a supported override", guidance, StringComparison.Ordinal);
        Assert.Contains("max", guidance, StringComparison.Ordinal);
    }

    [Fact]
    public void Unsupported_model_disables_inherited_selector_and_explains_the_limitation()
    {
        using var context = new BunitContext();
        var provider = CreateProvider(ProviderKind.OpenAi, "gpt-4.1");

        var cut = context.Render<AgentThinkingEffortSettings>(parameters => parameters
            .Add(component => component.Provider, provider)
            .Add(component => component.Value, null));

        Assert.True(cut.Find($"[data-testid='{SelectorTestId}']").HasAttribute("disabled"));
        Assert.Equal(["Provider default"], ReadOptionLabels(cut));
        var guidance = cut.Find($"[data-testid='{SupportTestId}']").TextContent;
        Assert.Contains("does not support configurable thinking effort", guidance, StringComparison.Ordinal);
        Assert.DoesNotContain("confirm this model's capabilities", guidance, StringComparison.Ordinal);
    }

    [Fact]
    public void Unknown_openai_model_disables_inherited_selector_and_requests_verified_definition()
    {
        using var context = new BunitContext();
        var provider = CreateProvider(ProviderKind.OpenAi, "custom-deployment-west");

        var cut = context.Render<AgentThinkingEffortSettings>(parameters => parameters
            .Add(component => component.Provider, provider)
            .Add(component => component.Value, null));

        Assert.True(cut.Find($"[data-testid='{SelectorTestId}']").HasAttribute("disabled"));
        var guidance = cut.Find($"[data-testid='{SupportTestId}']").TextContent;
        Assert.Contains("not defined", guidance, StringComparison.Ordinal);
        Assert.Contains("verified capability definition", guidance, StringComparison.Ordinal);
        Assert.DoesNotContain("does not support configurable thinking effort", guidance, StringComparison.Ordinal);
    }

    [Fact]
    public void Unknown_azure_deployment_requests_provider_scoped_definition()
    {
        using var context = new BunitContext();
        var provider = CreateProvider(
            ProviderKind.AzureOpenAi,
            "reasoning-deployment");

        var cut = context.Render<AgentThinkingEffortSettings>(parameters => parameters
            .Add(component => component.Provider, provider)
            .Add(component => component.Value, null));

        Assert.True(cut.Find($"[data-testid='{SelectorTestId}']").HasAttribute("disabled"));
        var guidance = cut.Find($"[data-testid='{SupportTestId}']").TextContent;
        Assert.Contains("provider-scoped", guidance, StringComparison.Ordinal);
        Assert.Contains("Define this deployment's allowed", guidance, StringComparison.Ordinal);
        Assert.DoesNotContain("Test the provider", guidance, StringComparison.Ordinal);
    }

    [Fact]
    public void Missing_provider_disables_selector_and_requests_provider_selection()
    {
        using var context = new BunitContext();

        var cut = context.Render<AgentThinkingEffortSettings>(parameters => parameters
            .Add(component => component.Provider, null)
            .Add(component => component.Model, null)
            .Add(component => component.Value, null));

        Assert.True(cut.Find($"[data-testid='{SelectorTestId}']").HasAttribute("disabled"));
        Assert.Contains(
            "Select a provider and model",
            cut.Find($"[data-testid='{SupportTestId}']").TextContent,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Existing_incompatible_override_is_preserved_until_user_resets_to_provider_default()
    {
        using var context = new BunitContext();
        var provider = CreateProvider(ProviderKind.OpenAi, "gpt-5.4");
        var changes = new List<AgentReasoningEffortLevel?>();
        var callback = EventCallback.Factory.Create<AgentReasoningEffortLevel?>(
            this,
            value => changes.Add(value));

        var cut = context.Render<AgentThinkingEffortSettings>(parameters => parameters
            .Add(component => component.Provider, provider)
            .Add(component => component.Model, "gpt-5.4")
            .Add(component => component.Value, AgentReasoningEffortLevel.Medium)
            .Add(component => component.ValueChanged, callback));

        cut.Render(parameters => parameters
            .Add(component => component.Provider, provider)
            .Add(component => component.Model, "gpt-4.1")
            .Add(component => component.Value, AgentReasoningEffortLevel.Medium)
            .Add(component => component.ValueChanged, callback));

        Assert.Empty(changes);
        Assert.False(cut.Find($"[data-testid='{SelectorTestId}']").HasAttribute("disabled"));
        Assert.Equal(
            ["Provider default", "Medium (currently configured; unavailable)"],
            ReadOptionLabels(cut));
        var guidance = cut.Find($"[data-testid='{SupportTestId}']").TextContent;
        Assert.Contains("cannot be applied", guidance, StringComparison.Ordinal);
        Assert.Contains("Select Provider default to remove this override", guidance, StringComparison.Ordinal);

        cut.Find($"[data-testid='{SelectorTestId}']").Change("0");

        Assert.Null(Assert.Single(changes));
    }

    [Fact]
    public void Incompatible_override_does_not_recommend_an_unavailable_provider_default()
    {
        using var context = new BunitContext();
        var provider = CreateProvider(
            ProviderKind.OpenAi,
            "gpt-5.6",
            AgentReasoningEffortLevel.Max);

        var cut = context.Render<AgentThinkingEffortSettings>(parameters => parameters
            .Add(component => component.Provider, provider)
            .Add(component => component.Model, "gpt-5.6")
            .Add(component => component.Value, AgentReasoningEffortLevel.Max));

        cut.Render(parameters => parameters
            .Add(component => component.Provider, provider)
            .Add(component => component.Model, "gpt-5.4")
            .Add(component => component.Value, AgentReasoningEffortLevel.Max));

        var optionLabels = ReadOptionLabels(cut);
        Assert.Equal("Provider default (unavailable)", optionLabels[0]);
        Assert.Equal("Max (currently configured; unavailable)", optionLabels[^1]);
        var guidance = cut.Find($"[data-testid='{SupportTestId}']").TextContent;
        Assert.Contains("provider default is also unavailable", guidance, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Select a supported override", guidance, StringComparison.Ordinal);
        Assert.DoesNotContain("Select Provider default to remove", guidance, StringComparison.Ordinal);
    }

    [Fact]
    public void Discovered_ollama_capability_lists_only_native_allowed_efforts()
    {
        using var context = new BunitContext();
        const string model = "custom-thinking:latest";
        var discoveredCapability = AgentThinkingEffortPolicy.CreateDiscoveredCapability(
            model,
            "custom",
            AgentThinkingEffortSupportStatus.Supported);
        var provider = CreateProvider(
            ProviderKind.Ollama,
            model,
            AgentReasoningEffortLevel.Medium,
            [discoveredCapability]);

        var cut = context.Render<AgentThinkingEffortSettings>(parameters => parameters
            .Add(component => component.Provider, provider)
            .Add(component => component.Model, string.Empty)
            .Add(component => component.Value, null));

        var optionLabels = ReadOptionLabels(cut);
        Assert.Equal(
            [
                "Provider default (enabled)",
                "None (disable thinking)",
                "Enabled"
            ],
            optionLabels);
        Assert.DoesNotContain("Extra high", optionLabels);
    }

    [Fact]
    public void Discovered_gptoss_capability_lists_levels_without_disable()
    {
        using var context = new BunitContext();
        const string model = "gptoss32k:latest";
        var discoveredCapability = AgentThinkingEffortPolicy.CreateDiscoveredCapability(
            model,
            "gptoss",
            AgentThinkingEffortSupportStatus.Supported);
        var provider = CreateProvider(
            ProviderKind.Ollama,
            model,
            AgentReasoningEffortLevel.Medium,
            [discoveredCapability]);

        var cut = context.Render<AgentThinkingEffortSettings>(parameters => parameters
            .Add(component => component.Provider, provider)
            .Add(component => component.Model, string.Empty)
            .Add(component => component.Value, null));

        Assert.Equal(
            ["Provider default (medium)", "Low", "Medium", "High"],
            ReadOptionLabels(cut));
        Assert.Contains(
            "cannot be disabled",
            cut.Find($"[data-testid='{SupportTestId}']").TextContent,
            StringComparison.Ordinal);
    }

    private static IReadOnlyList<string> ReadOptionLabels(
        IRenderedComponent<AgentThinkingEffortSettings> component)
    {
        return component.Find($"[data-testid='{SelectorTestId}']")
            .QuerySelectorAll("option")
            .Select(option => option.TextContent)
            .ToList();
    }

    private static ProviderProfile CreateProvider(
        ProviderKind kind,
        string defaultModel,
        AgentReasoningEffortLevel? providerDefault = null,
        IReadOnlyList<ProviderModelThinkingEffortCapability>? capabilities = null)
    {
        return new ProviderProfile(
            Guid.NewGuid(),
            kind == ProviderKind.Ollama ? "Local Ollama" : "OpenAI default",
            kind,
            kind == ProviderKind.Ollama
                ? "http://127.0.0.1:11434"
                : "https://api.openai.com/v1",
            kind == ProviderKind.Ollama ? string.Empty : "OPENAI_API_KEY",
            defaultModel,
            kind == ProviderKind.Ollama
                ? ProviderTransportKind.ChatCompletions
                : ProviderTransportKind.Responses,
            IsEnabled: true,
            SupportsStreaming: true,
            SupportsTools: true,
            PreferFrameworkManagedChatHistory: false,
            SupportsBackgroundResponses: true,
            ConfigurationJson: AgentThinkingEffortPolicy.WriteProviderDefault("{}", providerDefault),
            Notes: string.Empty,
            HealthStatus: "Not checked",
            LastCheckedAtUtc: null,
            SuggestedModels: [defaultModel])
        {
            Purpose = ProviderProfilePurpose.Chat,
            ModelThinkingEffortCapabilities = capabilities ?? []
        };
    }
}
