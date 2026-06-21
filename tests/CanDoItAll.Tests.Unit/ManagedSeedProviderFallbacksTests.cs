using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Tests.Unit;

public sealed class ManagedSeedProviderFallbacksTests
{
    [Fact]
    public void Managed_seed_openai_agents_keep_openai_provider_when_the_openai_key_is_missing()
    {
        var agent = CreateManagedSeedAgent(model: "gpt-4.1");
        var provider = CreateOpenAiProvider();

        var effectiveProvider = ManagedSeedProviderFallbacks.Apply(agent, provider, openAiApiKeyOverride: string.Empty);
        var effectiveModel = ManagedSeedProviderFallbacks.ResolveModel(agent, provider, openAiApiKeyOverride: string.Empty);

        Assert.Equal(provider, effectiveProvider);
        Assert.Equal("gpt-4.1", effectiveModel);
    }

    [Fact]
    public void Managed_seed_openai_agents_keep_openai_provider_when_the_openai_key_is_present()
    {
        var agent = CreateManagedSeedAgent(model: "gpt-4.1");
        var provider = CreateOpenAiProvider();

        var effectiveProvider = ManagedSeedProviderFallbacks.Apply(agent, provider, openAiApiKeyOverride: "present");
        var effectiveModel = ManagedSeedProviderFallbacks.ResolveModel(agent, provider, openAiApiKeyOverride: "present");

        Assert.Equal(provider, effectiveProvider);
        Assert.Equal("gpt-4.1", effectiveModel);
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

    [Theory]
    [InlineData("delivery-qa-observer")]
    [InlineData("dotnet-application-developer")]
    [InlineData("blazor-application-developer")]
    [InlineData("javascript-application-developer")]
    [InlineData("business-strategist")]
    [InlineData("financial-strategist")]
    [InlineData("marketing-specialist")]
    public void Managed_seed_template_key_agents_fall_back_even_when_configuration_marker_is_missing(string templateKey)
    {
        var agent = CreateAgent("{}", "gpt-4.1", templateKey);
        var provider = CreateOpenAiProvider();

        var effectiveProvider = ManagedSeedProviderFallbacks.Apply(agent, provider, openAiApiKeyOverride: string.Empty);
        var effectiveModel = ManagedSeedProviderFallbacks.ResolveModel(agent, provider, openAiApiKeyOverride: string.Empty);

        Assert.Equal(provider, effectiveProvider);
        Assert.Equal("gpt-4.1", effectiveModel);
    }

    [Fact]
    public void Managed_seed_openai_provider_keeps_openai_even_when_agent_marker_is_missing()
    {
        var agent = CreateUnmanagedAgent(model: "gpt-4.1");
        var provider = CreateOpenAiProvider();

        var effectiveProvider = ManagedSeedProviderFallbacks.Apply(agent, provider, openAiApiKeyOverride: string.Empty);
        var effectiveModel = ManagedSeedProviderFallbacks.ResolveModel(agent, effectiveProvider, openAiApiKeyOverride: string.Empty);

        Assert.Equal(provider, effectiveProvider);
        Assert.Equal("gpt-4.1", effectiveModel);
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

        Assert.Equal(ManagedSeedProviderFallbacks.OpenAiDefaultProviderName, effectiveProvider.Name);
        Assert.Equal(ManagedSeedProviderFallbacks.OpenAiBaseUrl, effectiveProvider.BaseUrl);
        Assert.Equal(ProviderKind.OpenAi, effectiveProvider.Kind);
        Assert.Equal(ProviderTransportKind.Responses, effectiveProvider.Transport);
        Assert.Equal("gpt-4.1", effectiveModel);
    }

    [Fact]
    public void Remote_ollama_registry_provider_is_remapped_to_openai_and_uses_openai_model_for_ollama_model_assignments()
    {
        var agent = CreateManagedSeedAgent(model: ManagedSeedProviderFallbacks.FallbackModel);
        var registryProvider = CreateFallbackProvider();
        var catalogShadowProvider = CreateOpenAiProvider();

        var effectiveProvider = ManagedSeedProviderFallbacks.ResolvePreferredProvider(
            agent,
            registryProvider,
            catalogShadowProvider,
            openAiApiKeyOverride: "present");
        var effectiveModel = ManagedSeedProviderFallbacks.ResolveModel(agent, effectiveProvider, openAiApiKeyOverride: "present");

        Assert.Equal(ManagedSeedProviderFallbacks.OpenAiDefaultProviderName, effectiveProvider.Name);
        Assert.Equal(ProviderKind.OpenAi, effectiveProvider.Kind);
        Assert.Equal(ProviderTransportKind.Responses, effectiveProvider.Transport);
        Assert.Equal(ManagedSeedProviderFallbacks.OpenAiDefaultModel, effectiveModel);
    }

    [Fact]
    public void Provider_repair_override_preserves_remote_ollama_for_managed_seed_agent()
    {
        var originalAgent = CreateManagedSeedAgent(model: ManagedSeedProviderFallbacks.FallbackModel);
        var agent = originalAgent with
        {
            ConfigurationJson = ManagedSeedProviderFallbacks.EnableProviderRepairFallbackOverride(originalAgent.ConfigurationJson)
        };
        var registryProvider = CreateFallbackProvider();
        var catalogShadowProvider = CreateOpenAiProvider();

        var effectiveProvider = ManagedSeedProviderFallbacks.ResolvePreferredProvider(
            agent,
            registryProvider,
            catalogShadowProvider,
            openAiApiKeyOverride: "present");
        var effectiveModel = ManagedSeedProviderFallbacks.ResolveModel(agent, effectiveProvider, openAiApiKeyOverride: "present");

        Assert.Equal(registryProvider, effectiveProvider);
        Assert.Equal(ManagedSeedProviderFallbacks.FallbackModel, effectiveModel);
    }

    [Fact]
    public void Provider_repair_override_uses_fallback_model_when_managed_seed_agent_keeps_openai_model()
    {
        var originalAgent = CreateManagedSeedAgent(model: ManagedSeedProviderFallbacks.OpenAiDefaultModel);
        var agent = originalAgent with
        {
            ConfigurationJson = ManagedSeedProviderFallbacks.EnableProviderRepairFallbackOverride(originalAgent.ConfigurationJson)
        };
        var registryProvider = CreateFallbackProvider();

        var effectiveModel = ManagedSeedProviderFallbacks.ResolveModel(
            agent,
            registryProvider,
            openAiApiKeyOverride: "present");

        Assert.Equal(ManagedSeedProviderFallbacks.FallbackModel, effectiveModel);
    }

    [Fact]
    public void Catalog_shadow_openai_provider_is_used_when_registry_provider_is_missing()
    {
        var agent = CreateManagedSeedAgent(model: "gpt-4.1");
        var catalogShadowProvider = CreateOpenAiProvider();

        var effectiveProvider = ManagedSeedProviderFallbacks.ResolvePreferredProvider(
            agent,
            registryProvider: null,
            catalogShadowProvider,
            openAiApiKeyOverride: "present");
        var effectiveModel = ManagedSeedProviderFallbacks.ResolveModel(agent, effectiveProvider, openAiApiKeyOverride: "present");

        Assert.Equal(catalogShadowProvider, effectiveProvider);
        Assert.Equal("gpt-4.1", effectiveModel);
    }

    private static AgentDefinition CreateManagedSeedAgent(string model)
    {
        return CreateAgent("{\"managedSeedVersion\":\"2026-04-serious-delivery-v25\"}", model);
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
            DefaultModel: ManagedSeedProviderFallbacks.OpenAiDefaultModel,
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
            SuggestedModels: [ManagedSeedProviderFallbacks.OpenAiDefaultModel, "gpt-5.4", "gpt-5-mini", "gpt-4.1-mini", "gpt-4.1"]);
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
