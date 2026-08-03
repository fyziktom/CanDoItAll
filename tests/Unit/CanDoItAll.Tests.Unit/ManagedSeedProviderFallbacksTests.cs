using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Tests.Unit;

public sealed class ManagedSeedProviderFallbacksTests
{
    [Fact]
    public void Managed_seed_provider_default_is_written_once_in_canonical_model_parameters()
    {
        var configurationJson = ManagedSeedProviderFallbacks.EnsureDefaultReasoningConfigurationJson(
            """{"reasoningEffort":"low","keep":"value","modelParameters":{"think":false,"maxOutputTokens":512}}""",
            "service-managed");

        using var document = System.Text.Json.JsonDocument.Parse(configurationJson);
        var root = document.RootElement;
        var modelParameters = root.GetProperty("modelParameters");
        Assert.Equal("service-managed", root.GetProperty("history").GetString());
        Assert.Equal("value", root.GetProperty("keep").GetString());
        Assert.False(root.TryGetProperty("reasoningEffort", out _));
        Assert.False(root.TryGetProperty("think", out _));
        Assert.Equal("medium", modelParameters.GetProperty("reasoningEffort").GetString());
        Assert.Equal(512, modelParameters.GetProperty("maxOutputTokens").GetInt32());
        Assert.False(modelParameters.TryGetProperty("think", out _));
    }

    [Theory]
    [InlineData("{not-json")]
    [InlineData("[]")]
    public void Managed_seed_thinking_configuration_rejects_invalid_json_instead_of_replacing_it(
        string configurationJson)
    {
        Assert.Throws<InvalidOperationException>(() =>
            ManagedSeedProviderFallbacks.EnsureDefaultReasoningConfigurationJson(configurationJson));
        Assert.Throws<InvalidOperationException>(() =>
            ManagedSeedProviderFallbacks.EnsureFallbackRuntimeConfigurationJson(configurationJson, "test"));
    }

    [Fact]
    public void Managed_seed_openai_suggestions_include_all_gpt_5_6_models()
    {
        Assert.All(
            OpenAiModelIds.Gpt56Models,
            model => Assert.Contains(model, ManagedSeedProviderFallbacks.OpenAiSuggestedModels, StringComparer.OrdinalIgnoreCase));
    }

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

    [Fact]
    public void Top_level_managed_seed_ownership_is_detected()
    {
        var agent = CreateAgent(
            "{\"managedSeedVersion\":\"2026-08-test\"}",
            model: "gpt-4.1",
            templateKey: "customer-agent");

        Assert.True(ManagedSeedProviderFallbacks.IsManagedSeedAgent(agent));
    }

    [Theory]
    [InlineData("{\"notes\":\"managedSeedVersion\"}")]
    [InlineData("{\"metadata\":{\"managedSeedVersion\":\"2026-08-test\"}}")]
    public void Managed_seed_marker_outside_the_top_level_property_is_not_detected(
        string configurationJson)
    {
        var agent = CreateAgent(
            configurationJson,
            model: "gpt-4.1",
            templateKey: "customer-agent");

        Assert.False(ManagedSeedProviderFallbacks.IsManagedSeedAgent(agent));
    }

    [Fact]
    public void Removing_managed_seed_ownership_preserves_runtime_configuration()
    {
        const string configurationJson =
            "{\"managedSeedVersion\":\"2026-08-test\",\"managedSeedCustomizationVersion\":\"2026-08-test\",\"reasoningEffort\":\"high\",\"timeoutSeconds\":47,\"runtime\":{\"transport\":\"responses\",\"toolsEnabled\":true}}";

        var detached = AgentManagedSeedCustomizationMetadata.RemoveManagedSeedOwnership(
            configurationJson);

        using var document = System.Text.Json.JsonDocument.Parse(detached);
        var root = document.RootElement;
        Assert.False(root.TryGetProperty("managedSeedVersion", out _));
        Assert.False(root.TryGetProperty("managedSeedCustomizationVersion", out _));
        Assert.Equal("high", root.GetProperty("reasoningEffort").GetString());
        Assert.Equal(47, root.GetProperty("timeoutSeconds").GetInt32());
        var runtime = root.GetProperty("runtime");
        Assert.Equal("responses", runtime.GetProperty("transport").GetString());
        Assert.True(runtime.GetProperty("toolsEnabled").GetBoolean());
    }

    [Theory]
    [InlineData("{not-valid-json")]
    [InlineData("[\"managedSeedVersion\"]")]
    public void Removing_managed_seed_ownership_rejects_invalid_or_non_object_json(
        string configurationJson)
    {
        Assert.Throws<InvalidOperationException>(
            () => AgentManagedSeedCustomizationMetadata.RemoveManagedSeedOwnership(
                configurationJson));
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

        Assert.Equal(registryProvider, effectiveProvider);
        Assert.Equal(ManagedSeedProviderFallbacks.FallbackModel, effectiveModel);
    }

    [Fact]
    public void Remote_ollama_registry_provider_is_preserved_for_managed_seed_agent()
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

        Assert.Equal(registryProvider, effectiveProvider);
        Assert.Equal(ManagedSeedProviderFallbacks.FallbackModel, effectiveModel);
    }

    [Fact]
    public void Remote_ollama_registry_provider_uses_default_model_for_openai_model_assignments()
    {
        var agent = CreateManagedSeedAgent(model: ManagedSeedProviderFallbacks.OpenAiDefaultModel);
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
    public void Local_ollama_provider_uses_default_model_when_seed_agent_keeps_openai_model()
    {
        var agent = CreateManagedSeedAgent(model: ManagedSeedProviderFallbacks.OpenAiDefaultModel);
        var provider = CreateLocalOllamaProvider(defaultModel: "gemma4-12b-256k");

        var effectiveModel = ManagedSeedProviderFallbacks.ResolveModel(agent, provider, openAiApiKeyOverride: "present");

        Assert.Equal("gemma4-12b-256k", effectiveModel);
    }

    [Fact]
    public void Local_ollama_provider_preserves_supported_openai_named_model()
    {
        var agent = CreateManagedSeedAgent(model: ManagedSeedProviderFallbacks.OpenAiDefaultModel);
        var provider = CreateLocalOllamaProvider(defaultModel: "gemma4-12b-256k") with
        {
            SuggestedModels = [ManagedSeedProviderFallbacks.OpenAiDefaultModel, "gemma4-12b-256k"]
        };

        var effectiveModel = ManagedSeedProviderFallbacks.ResolveModel(agent, provider, openAiApiKeyOverride: "present");

        Assert.Equal(ManagedSeedProviderFallbacks.OpenAiDefaultModel, effectiveModel);
    }

    [Fact]
    public void Local_ollama_provider_preserves_explicit_custom_model()
    {
        var agent = CreateManagedSeedAgent(model: "gptoss20b64k");
        var provider = CreateLocalOllamaProvider(defaultModel: "gemma4-12b-256k");

        var effectiveModel = ManagedSeedProviderFallbacks.ResolveModel(agent, provider, openAiApiKeyOverride: "present");

        Assert.Equal("gptoss20b64k", effectiveModel);
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
    public void Remote_ollama_provider_is_recognized_as_managed_seed_fallback_for_catalog_repair()
    {
        var provider = CreateFallbackProvider() with
        {
            ConfigurationJson = "{\"history\":\"framework-managed\"}",
            Notes = "Targets the remote host validated during the latest Ollama repair and networking checks."
        };

        Assert.True(ManagedSeedProviderFallbacks.IsGeneratedManagedSeedFallbackProvider(provider));
    }

    [Fact]
    public void Fallback_runtime_configuration_sets_bounded_ollama_generation_parameters()
    {
        var configurationJson = ManagedSeedProviderFallbacks.CreateFallbackConfigurationJson("unit-test");

        var maxOutputTokens = AgentProviderModelParameterPolicy.ResolveMaxOutputTokens(
            ProviderKind.Ollama,
            configurationJson,
            string.Empty);
        var effort = AgentThinkingEffortPolicy.ReadConfiguredEffort(configurationJson, "provider");

        Assert.Equal(ManagedSeedProviderFallbacks.FallbackMaxOutputTokens, maxOutputTokens);
        Assert.Null(effort);

        using var document = System.Text.Json.JsonDocument.Parse(configurationJson);
        Assert.Equal("framework-managed", document.RootElement.GetProperty("history").GetString());
        Assert.Equal("unit-test", document.RootElement.GetProperty("fallback").GetString());
        Assert.Equal(ManagedSeedProviderFallbacks.FallbackTimeoutSeconds, document.RootElement.GetProperty("timeoutSeconds").GetInt32());
        Assert.False(document.RootElement.GetProperty("modelParameters").TryGetProperty("think", out _));
    }

    [Fact]
    public void Fallback_runtime_configuration_repairs_existing_unbounded_ollama_generation_parameters()
    {
        const string existingConfigurationJson = "{\"history\":\"custom\",\"fallback\":\"old\",\"timeoutSeconds\":1,\"modelParameters\":{\"numPredict\":100,\"think\":true}}";

        var configurationJson = ManagedSeedProviderFallbacks.EnsureFallbackRuntimeConfigurationJson(
            existingConfigurationJson,
            "repair");

        var maxOutputTokens = AgentProviderModelParameterPolicy.ResolveMaxOutputTokens(
            ProviderKind.Ollama,
            configurationJson,
            string.Empty);
        var effort = AgentThinkingEffortPolicy.ReadConfiguredEffort(configurationJson, "provider");

        Assert.Equal(ManagedSeedProviderFallbacks.FallbackMaxOutputTokens, maxOutputTokens);
        Assert.Null(effort);

        using var document = System.Text.Json.JsonDocument.Parse(configurationJson);
        Assert.Equal("framework-managed", document.RootElement.GetProperty("history").GetString());
        Assert.Equal("repair", document.RootElement.GetProperty("fallback").GetString());
        Assert.Equal(ManagedSeedProviderFallbacks.FallbackTimeoutSeconds, document.RootElement.GetProperty("timeoutSeconds").GetInt32());
        Assert.False(document.RootElement.GetProperty("modelParameters").TryGetProperty("think", out _));
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

    private static ProviderProfile CreateLocalOllamaProvider(string defaultModel)
    {
        return new ProviderProfile(
            Id: Guid.NewGuid(),
            Name: "Local Ollama",
            Kind: ProviderKind.Ollama,
            BaseUrl: "http://127.0.0.1:11434",
            ApiKeyEnvironmentVariable: string.Empty,
            DefaultModel: defaultModel,
            Transport: ProviderTransportKind.ChatCompletions,
            IsEnabled: true,
            SupportsStreaming: true,
            SupportsTools: true,
            PreferFrameworkManagedChatHistory: true,
            SupportsBackgroundResponses: false,
            ConfigurationJson: "{\"history\":\"framework-managed\"}",
            Notes: "Local test provider.",
            HealthStatus: "Healthy",
            LastCheckedAtUtc: null,
            SuggestedModels: [defaultModel]);
    }
}
