using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Tests.Unit;

public sealed class ManagedSeedProviderFallbacksTests
{
    [Fact]
    public void Managed_seed_openai_agents_fall_back_to_remote_ollama_when_the_openai_key_is_missing()
    {
        var agent = CreateManagedSeedAgent(model: "gpt-4.1");
        var provider = CreateOpenAiProvider();

        var effectiveProvider = ManagedSeedProviderFallbacks.Apply(agent, provider, openAiApiKeyOverride: string.Empty);
        var effectiveModel = ManagedSeedProviderFallbacks.ResolveModel(agent, provider, openAiApiKeyOverride: string.Empty);

        Assert.Equal(provider.Id, effectiveProvider.Id);
        Assert.Equal(ManagedSeedProviderFallbacks.FallbackProviderName, effectiveProvider.Name);
        Assert.Equal(ProviderKind.Ollama, effectiveProvider.Kind);
        Assert.Equal(ManagedSeedProviderFallbacks.FallbackBaseUrl, effectiveProvider.BaseUrl);
        Assert.Equal(ManagedSeedProviderFallbacks.FallbackModel, effectiveProvider.DefaultModel);
        Assert.Equal(ProviderTransportKind.ChatCompletions, effectiveProvider.Transport);
        Assert.Equal(ManagedSeedProviderFallbacks.FallbackModel, effectiveModel);
    }

    [Fact]
    public void Managed_seed_openai_agents_fall_back_to_remote_ollama_even_when_the_openai_key_is_present()
    {
        var agent = CreateManagedSeedAgent(model: "gpt-4.1");
        var provider = CreateOpenAiProvider();

        var effectiveProvider = ManagedSeedProviderFallbacks.Apply(agent, provider, openAiApiKeyOverride: "present");
        var effectiveModel = ManagedSeedProviderFallbacks.ResolveModel(agent, provider, openAiApiKeyOverride: "present");

        Assert.Equal(provider.Id, effectiveProvider.Id);
        Assert.Equal(ManagedSeedProviderFallbacks.FallbackProviderName, effectiveProvider.Name);
        Assert.Equal(ProviderKind.Ollama, effectiveProvider.Kind);
        Assert.Equal(ManagedSeedProviderFallbacks.FallbackBaseUrl, effectiveProvider.BaseUrl);
        Assert.Equal(ManagedSeedProviderFallbacks.FallbackModel, effectiveModel);
    }

    [Fact]
    public void Non_managed_seed_agents_keep_their_original_openai_provider()
    {
        var agent = CreateUnmanagedAgent(model: "gpt-4.1");
        var provider = CreateOpenAiProvider();

        var effectiveProvider = ManagedSeedProviderFallbacks.Apply(agent, provider, openAiApiKeyOverride: "present");
        var effectiveModel = ManagedSeedProviderFallbacks.ResolveModel(agent, provider, openAiApiKeyOverride: "present");

        Assert.Equal(provider, effectiveProvider);
        Assert.Equal("gpt-4.1", effectiveModel);
    }

    [Fact]
    public void Managed_seed_template_key_agents_fall_back_even_when_configuration_marker_is_missing()
    {
        var agent = CreateAgent("{}", "gpt-4.1", templateKey: "delivery-qa-observer");
        var provider = CreateOpenAiProvider();

        var effectiveProvider = ManagedSeedProviderFallbacks.Apply(agent, provider, openAiApiKeyOverride: string.Empty);
        var effectiveModel = ManagedSeedProviderFallbacks.ResolveModel(agent, provider, openAiApiKeyOverride: string.Empty);

        Assert.Equal(ManagedSeedProviderFallbacks.FallbackProviderName, effectiveProvider.Name);
        Assert.Equal(ProviderKind.Ollama, effectiveProvider.Kind);
        Assert.Equal(ManagedSeedProviderFallbacks.FallbackModel, effectiveModel);
    }

    [Fact]
    public void Managed_seed_openai_provider_falls_back_even_when_agent_marker_is_missing()
    {
        var agent = CreateUnmanagedAgent(model: "gpt-4.1");
        var provider = CreateOpenAiProvider();

        var effectiveProvider = ManagedSeedProviderFallbacks.Apply(agent, provider, openAiApiKeyOverride: string.Empty);
        var effectiveModel = ManagedSeedProviderFallbacks.ResolveModel(agent, effectiveProvider, openAiApiKeyOverride: string.Empty);

        Assert.Equal(ManagedSeedProviderFallbacks.FallbackProviderName, effectiveProvider.Name);
        Assert.Equal(ProviderKind.Ollama, effectiveProvider.Kind);
        Assert.Equal(ManagedSeedProviderFallbacks.FallbackModel, effectiveModel);
    }

    [Fact]
    public void Custom_openai_provider_keeps_original_when_key_is_missing()
    {
        var agent = CreateUnmanagedAgent(model: "gpt-4.1");
        var provider = CreateOpenAiProvider() with
        {
            Id = Guid.NewGuid(),
            Name = "Customer OpenAI",
            BaseUrl = "https://api.openai.com/v1"
        };

        var effectiveProvider = ManagedSeedProviderFallbacks.Apply(agent, provider, openAiApiKeyOverride: string.Empty);
        var effectiveModel = ManagedSeedProviderFallbacks.ResolveModel(agent, provider, openAiApiKeyOverride: string.Empty);

        Assert.Equal(provider, effectiveProvider);
        Assert.Equal("gpt-4.1", effectiveModel);
    }

    [Fact]
    public void Registry_provider_wins_over_catalog_shadow_provider_for_execution_resolution()
    {
        var agent = CreateManagedSeedAgent(model: "gpt-4.1");
        var registryProvider = CreateFallbackProvider();
        var catalogShadowProvider = CreateOpenAiProvider();

        var effectiveProvider = ManagedSeedProviderFallbacks.ResolvePreferredProvider(
            agent,
            registryProvider,
            catalogShadowProvider,
            openAiApiKeyOverride: "present");
        var effectiveModel = ManagedSeedProviderFallbacks.ResolveModel(agent, effectiveProvider, openAiApiKeyOverride: "present");

        Assert.Equal(ManagedSeedProviderFallbacks.FallbackProviderName, effectiveProvider.Name);
        Assert.Equal(ManagedSeedProviderFallbacks.FallbackBaseUrl, effectiveProvider.BaseUrl);
        Assert.Equal(ManagedSeedProviderFallbacks.FallbackModel, effectiveModel);
    }

    [Fact]
    public void Catalog_shadow_provider_is_fallback_adjusted_when_registry_provider_is_missing()
    {
        var agent = CreateManagedSeedAgent(model: "gpt-4.1");
        var catalogShadowProvider = CreateOpenAiProvider();

        var effectiveProvider = ManagedSeedProviderFallbacks.ResolvePreferredProvider(
            agent,
            registryProvider: null,
            catalogShadowProvider,
            openAiApiKeyOverride: "present");
        var effectiveModel = ManagedSeedProviderFallbacks.ResolveModel(agent, effectiveProvider, openAiApiKeyOverride: "present");

        Assert.Equal(ManagedSeedProviderFallbacks.FallbackProviderName, effectiveProvider.Name);
        Assert.Equal(ManagedSeedProviderFallbacks.FallbackBaseUrl, effectiveProvider.BaseUrl);
        Assert.Equal(ManagedSeedProviderFallbacks.FallbackModel, effectiveModel);
    }

    private static AgentDefinition CreateManagedSeedAgent(string model)
    {
        return CreateAgent("{\"managedSeedVersion\":\"2026-04-serious-delivery-v24\"}", model);
    }

    private static AgentDefinition CreateUnmanagedAgent(string model)
    {
        return CreateAgent("{}", model);
    }

    private static AgentDefinition CreateAgent(string configurationJson, string model)
    {
        return CreateAgent(configurationJson, model, templateKey: string.Empty);
    }

    private static AgentDefinition CreateAgent(string configurationJson, string model, string templateKey)
    {
        return new AgentDefinition(
            Id: Guid.NewGuid(),
            Name: "Delivery QA Observer",
            RoleTitle: "QA lead and browser-proof reviewer",
            Summary: "Managed seed QA agent.",
            Instructions: "Review the generated app.",
            Status: AgentLifecycleStatus.Active,
            ProviderProfileId: Guid.Parse("c1c103db-707e-3f52-8809-8d804fc171d1"),
            Model: model,
            Workload: AgentWorkloadKind.Qa,
            ChatHistoryMode: AgentChatHistoryMode.FrameworkManaged,
            Temperature: 0.1,
            RequirePerServiceCallChatHistoryPersistence: false,
            EnableBackgroundResponses: false,
            ConfigurationJson: configurationJson,
            IsTemplate: false,
            TemplateKey: templateKey,
            Permissions: AgentPermissionsPolicy.Default,
            Capabilities: [],
            Tags: ["browser"],
            CreatedAtUtc: DateTimeOffset.UtcNow,
            UpdatedAtUtc: DateTimeOffset.UtcNow);
    }

    private static ProviderProfile CreateOpenAiProvider()
    {
        return new ProviderProfile(
            Id: Guid.Parse("c1c103db-707e-3f52-8809-8d804fc171d1"),
            Name: "OpenAI default",
            Kind: ProviderKind.OpenAi,
            BaseUrl: "https://api.openai.com/v1",
            ApiKeyEnvironmentVariable: "OPENAI_API_KEY",
            DefaultModel: "gpt-4o-mini",
            Transport: ProviderTransportKind.Responses,
            IsEnabled: true,
            SupportsStreaming: true,
            SupportsTools: true,
            PreferFrameworkManagedChatHistory: false,
            SupportsBackgroundResponses: true,
            ConfigurationJson: "{\"history\":\"service-managed\"}",
            Notes: "Seeded OpenAI provider.",
            HealthStatus: "Not checked",
            LastCheckedAtUtc: null,
            SuggestedModels: ["gpt-4o-mini", "gpt-4.1"]);
    }

    private static ProviderProfile CreateFallbackProvider()
    {
        return new ProviderProfile(
            Id: Guid.Parse("c1c103db-707e-3f52-8809-8d804fc171d1"),
            Name: ManagedSeedProviderFallbacks.FallbackProviderName,
            Kind: ProviderKind.Ollama,
            BaseUrl: ManagedSeedProviderFallbacks.FallbackBaseUrl,
            ApiKeyEnvironmentVariable: string.Empty,
            DefaultModel: ManagedSeedProviderFallbacks.FallbackModel,
            Transport: ProviderTransportKind.ChatCompletions,
            IsEnabled: true,
            SupportsStreaming: true,
            SupportsTools: true,
            PreferFrameworkManagedChatHistory: true,
            SupportsBackgroundResponses: false,
            ConfigurationJson: "{\"history\":\"framework-managed\",\"fallback\":\"unit-test\"}",
            Notes: "Unit-test fallback provider.",
            HealthStatus: "Fallback active",
            LastCheckedAtUtc: null,
            SuggestedModels: [ManagedSeedProviderFallbacks.FallbackModel]);
    }
}
