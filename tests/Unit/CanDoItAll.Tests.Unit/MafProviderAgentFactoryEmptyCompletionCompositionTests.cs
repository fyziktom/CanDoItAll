using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OllamaSharp.Models;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class MafProviderAgentFactoryEmptyCompletionCompositionTests
{
    [Fact]
    public void Missing_custom_credential_does_not_fall_back_to_canonical_openai_configuration()
    {
        var provider = CreateProvider(
            ProviderKind.OpenAi,
            ProviderTransportKind.Responses) with
        {
            ApiKeyEnvironmentVariable = $"MISSING_PROFILE_KEY_{Guid.NewGuid():N}"
        };
        const string unrelatedCanonicalKey = "canonical-key-owned-by-another-profile";
        using var services = new ServiceCollection()
            .AddSingleton<IConfiguration>(new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    [MafProviderRuntimeSettings.OpenAiApiKeyEnvironmentVariable] = unrelatedCanonicalKey
                })
                .Build())
            .BuildServiceProvider();
        var credentialService = new MafProviderCredentialService(
            services.GetService<IAgentProviderCredentialResolver>(),
            services.GetService<IConfiguration>());

        var resolution = credentialService.Resolve(provider);

        Assert.False(resolution.IsResolved);
        Assert.DoesNotContain(unrelatedCanonicalKey, resolution.FailureMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Explicit_secret_binding_does_not_fall_back_to_another_profiles_ambient_key()
    {
        var providerA = CreateProvider(
            ProviderKind.OpenAi,
            ProviderTransportKind.Responses) with
        {
            Id = Guid.NewGuid(),
            ApiKeyEnvironmentVariable = $"secret:{Guid.NewGuid():D}",
            ConfigurationJson = $"{{\"secretRecordId\":\"{Guid.NewGuid():D}\"}}"
        };
        var providerB = providerA with
        {
            Id = Guid.NewGuid(),
            Name = "Provider with missing bound secret",
            ApiKeyEnvironmentVariable = $"secret:{Guid.NewGuid():D}",
            ConfigurationJson = $"{{\"secretRecordId\":\"{Guid.NewGuid():D}\"}}"
        };
        const string providerAKey = "provider-a-key-must-not-cross-profile";
        using var services = new ServiceCollection()
            .AddSingleton<IAgentProviderCredentialResolver>(
                new TwoProfileCredentialResolver(providerA.Id, providerAKey))
            .AddSingleton<IConfiguration>(new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    [MafProviderRuntimeSettings.OpenAiApiKeyEnvironmentVariable] = providerAKey
                })
                .Build())
            .BuildServiceProvider();
        var credentialService = new MafProviderCredentialService(
            services.GetService<IAgentProviderCredentialResolver>(),
            services.GetService<IConfiguration>());

        var resolvedA = credentialService.Resolve(providerA);
        var unresolvedB = credentialService.Resolve(providerB);

        Assert.True(resolvedA.IsResolved);
        Assert.Equal(providerAKey, resolvedA.ApiKey);
        Assert.False(unresolvedB.IsResolved);
        Assert.DoesNotContain(providerAKey, unresolvedB.FailureMessage, StringComparison.Ordinal);

        var factory = CreateFactory(credentialService);
        var exception = Assert.Throws<MafProviderConfigurationException>(() =>
            factory.CreateFrameworkAgent(
                providerB,
                providerB.DefaultModel,
                MafChatClientAgentOptionsFactory.Create(new ChatOptions()),
                frameworkManagedHistory: false,
                allowBackgroundResponses: false));
        Assert.Equal(MafProviderConfigurationFailureReason.MissingCredential, exception.Reason);
        Assert.Equal(providerB.Id, exception.ProviderIdentity.ProviderProfileId);
    }

    [Fact]
    public void CreateFrameworkAgent_RejectsMissingCredentialWithTypedProviderIdentity()
    {
        var provider = CreateProvider(
            ProviderKind.OpenAi,
            ProviderTransportKind.Responses);
        var factory = CreateFactory(new UnresolvedCredentialService());

        var exception = Assert.Throws<MafProviderConfigurationException>(() =>
            factory.CreateFrameworkAgent(
                provider,
                provider.DefaultModel,
                MafChatClientAgentOptionsFactory.Create(new ChatOptions()),
                frameworkManagedHistory: false,
                allowBackgroundResponses: false));

        Assert.Equal(MafProviderConfigurationFailureReason.MissingCredential, exception.Reason);
        Assert.Equal(provider.Id, exception.ProviderIdentity.ProviderProfileId);
        Assert.Equal(provider.Name, exception.ProviderIdentity.ProviderName);
        Assert.Equal(provider.DefaultModel, exception.ProviderIdentity.Model);
    }

    [Theory]
    [InlineData("not-an-absolute-url")]
    [InlineData("ftp://localhost/models")]
    [InlineData("http://user:password@localhost/models")]
    [InlineData("http://localhost/models?api_key=secret")]
    [InlineData("http://localhost/models#credential")]
    public void CreateFrameworkAgent_RejectsInvalidEndpoint(string baseUrl)
    {
        var provider = CreateProvider(
            ProviderKind.Ollama,
            ProviderTransportKind.ChatCompletions) with
        {
            BaseUrl = baseUrl
        };
        var factory = CreateFactory(new FixedCredentialService());

        var exception = Assert.Throws<MafProviderConfigurationException>(() =>
            factory.CreateFrameworkAgent(
                provider,
                provider.DefaultModel,
                MafChatClientAgentOptionsFactory.Create(new ChatOptions()),
                frameworkManagedHistory: false,
                allowBackgroundResponses: false));

        Assert.Equal(MafProviderConfigurationFailureReason.InvalidEndpoint, exception.Reason);
    }

    [Fact]
    public void CreateFrameworkAgent_RejectsAzureEndpointWithEmbeddedCredential()
    {
        var provider = CreateProvider(
            ProviderKind.AzureOpenAi,
            ProviderTransportKind.Responses) with
        {
            BaseUrl = "https://user:password@example.openai.azure.com"
        };
        var factory = CreateFactory(new FixedCredentialService());

        var exception = Assert.Throws<MafProviderConfigurationException>(() =>
            factory.CreateFrameworkAgent(
                provider,
                provider.DefaultModel,
                MafChatClientAgentOptionsFactory.Create(new ChatOptions()),
                frameworkManagedHistory: false,
                allowBackgroundResponses: false));

        Assert.Equal(MafProviderConfigurationFailureReason.InvalidEndpoint, exception.Reason);
    }

    [Fact]
    public void CreateFrameworkAgent_RejectsBlankEffectiveModel()
    {
        var provider = CreateProvider(
            ProviderKind.Ollama,
            ProviderTransportKind.ChatCompletions);
        var factory = CreateFactory(new FixedCredentialService());

        var exception = Assert.Throws<MafProviderConfigurationException>(() =>
            factory.CreateFrameworkAgent(
                provider,
                " ",
                MafChatClientAgentOptionsFactory.Create(new ChatOptions()),
                frameworkManagedHistory: false,
                allowBackgroundResponses: false));

        Assert.Equal(MafProviderConfigurationFailureReason.InvalidModel, exception.Reason);
    }

    [Fact]
    public void CreateFrameworkAgent_RejectsUnsupportedProviderTransport()
    {
        var provider = CreateProvider(
            ProviderKind.Ollama,
            ProviderTransportKind.Responses);
        var factory = CreateFactory(new FixedCredentialService());

        var exception = Assert.Throws<MafProviderConfigurationException>(() =>
            factory.CreateFrameworkAgent(
                provider,
                provider.DefaultModel,
                MafChatClientAgentOptionsFactory.Create(new ChatOptions()),
                frameworkManagedHistory: false,
                allowBackgroundResponses: false));

        Assert.Equal(MafProviderConfigurationFailureReason.UnsupportedTransport, exception.Reason);
    }

    [Theory]
    [InlineData("not-json")]
    [InlineData("null")]
    [InlineData("{\"timeoutSeconds\":\"45\"}")]
    [InlineData("{\"timeoutSeconds\":4}")]
    [InlineData("{\"timeoutSeconds\":3601}")]
    public void CreateFrameworkAgent_RejectsInvalidRuntimeSettings(string configurationJson)
    {
        var provider = CreateProvider(
            ProviderKind.Ollama,
            ProviderTransportKind.ChatCompletions) with
        {
            ConfigurationJson = configurationJson
        };
        var factory = CreateFactory(new FixedCredentialService());

        var exception = Assert.Throws<MafProviderConfigurationException>(() =>
            factory.CreateFrameworkAgent(
                provider,
                provider.DefaultModel,
                MafChatClientAgentOptionsFactory.Create(new ChatOptions()),
                frameworkManagedHistory: false,
                allowBackgroundResponses: false));

        Assert.Equal(MafProviderConfigurationFailureReason.InvalidRuntimeSettings, exception.Reason);
    }

    [Theory]
    [InlineData(ProviderKind.OpenAi, ProviderTransportKind.ChatCompletions, false)]
    [InlineData(ProviderKind.OpenAi, ProviderTransportKind.Responses, false)]
    [InlineData(ProviderKind.OpenAi, ProviderTransportKind.Responses, true)]
    [InlineData(ProviderKind.AzureOpenAi, ProviderTransportKind.ChatCompletions, false)]
    [InlineData(ProviderKind.AzureOpenAi, ProviderTransportKind.Responses, false)]
    [InlineData(ProviderKind.AzureOpenAi, ProviderTransportKind.Responses, true)]
    [InlineData(ProviderKind.Ollama, ProviderTransportKind.ChatCompletions, false)]
    public async Task CreateFrameworkAgent_ComposesExactlyOneEmptyCompletionDecorator(
        ProviderKind providerKind,
        ProviderTransportKind transport,
        bool frameworkManagedHistory)
    {
        var credentialService = new MafProviderCredentialService(new FixedCredentialResolver());
        var factory = CreateFactory(credentialService);
        var provider = CreateProvider(providerKind, transport);
        var options = new ChatClientAgentOptions
        {
            Id = Guid.NewGuid().ToString("D"),
            Name = "Empty completion composition test",
            Description = "Verifies the provider transport decorator.",
            ChatOptions = new ChatOptions()
        };
        var agent = factory.CreateFrameworkAgent(
            provider,
            provider.DefaultModel,
            options,
            frameworkManagedHistory,
            allowBackgroundResponses: false);

        try
        {
            var decorator = agent.GetService<EmptyCompletionRetryChatClient>();

            Assert.NotNull(decorator);
            Assert.Same(
                decorator,
                decorator.GetService<EmptyCompletionRetryChatClient>());
            var transportBoundary = agent.GetService<MafProviderTransportBoundaryChatClient>();
            Assert.NotNull(transportBoundary);
            Assert.Same(
                transportBoundary,
                transportBoundary.GetService<MafProviderTransportBoundaryChatClient>());
            var compatibilityDecorator = agent.GetService<OpenAiChatCompletionsCompatibilityChatClient>();
            if (providerKind == ProviderKind.OpenAi &&
                transport == ProviderTransportKind.ChatCompletions)
            {
                Assert.NotNull(compatibilityDecorator);
                Assert.Same(
                    compatibilityDecorator,
                    compatibilityDecorator.GetService<OpenAiChatCompletionsCompatibilityChatClient>());
            }
            else
            {
                Assert.Null(compatibilityDecorator);
            }
        }
        finally
        {
            switch (agent)
            {
                case IAsyncDisposable asyncDisposable:
                    await asyncDisposable.DisposeAsync();
                    break;
                case IDisposable disposable:
                    disposable.Dispose();
                    break;
            }
        }
    }

    [Fact]
    public async Task CreateFrameworkAgent_OllamaAgentOverridePrecedesInvalidProviderThinkingDefault()
    {
        var provider = CreateProvider(
            ProviderKind.Ollama,
            ProviderTransportKind.ChatCompletions) with
        {
            DefaultModel = "qwen3.5:2b",
            ConfigurationJson = AgentThinkingEffortConfiguration.WriteProviderDefault(
                "{}",
                AgentReasoningEffortLevel.High),
            ModelThinkingEffortCapabilities =
            [
                AgentThinkingEffortPolicy.CreateDiscoveredCapability(
                    "qwen3.5:2b",
                    "qwen35",
                    AgentThinkingEffortSupportStatus.Supported)
            ]
        };
        var chatOptions = MafModelParametersBuilder.CreateModelCompatibleChatOptions(
            provider,
            provider.DefaultModel,
            requestedTemperature: null,
            forceOmitTemperature: false,
            AgentThinkingEffortConfiguration.WriteAgentOverride(
                "{}",
                AgentReasoningEffortLevel.Medium));
        var factory = new MafProviderAgentFactory(
            new MafProviderCredentialService(new FixedCredentialResolver()),
            NoOpMafProviderStreamingDispatchGate.Instance);

        var agent = factory.CreateFrameworkAgent(
            provider,
            provider.DefaultModel,
            MafChatClientAgentOptionsFactory.Create(chatOptions),
            frameworkManagedHistory: false,
            allowBackgroundResponses: false);

        try
        {
            Assert.True(Assert.IsType<bool>(
                chatOptions.AdditionalProperties![OllamaOption.Think.Name]));
            Assert.NotNull(agent.GetService<EmptyCompletionRetryChatClient>());
        }
        finally
        {
            switch (agent)
            {
                case IAsyncDisposable asyncDisposable:
                    await asyncDisposable.DisposeAsync();
                    break;
                case IDisposable disposable:
                    disposable.Dispose();
                    break;
            }
        }
    }

    private static ProviderProfile CreateProvider(
        ProviderKind providerKind,
        ProviderTransportKind transport)
    {
        var baseUrl = providerKind switch
        {
            ProviderKind.OpenAi => "https://api.openai.com/v1",
            ProviderKind.AzureOpenAi => "https://example.openai.azure.com",
            ProviderKind.Ollama => "http://127.0.0.1:11434",
            _ => throw new ArgumentOutOfRangeException(
                nameof(providerKind),
                providerKind,
                "Unsupported test provider kind.")
        };
        return new ProviderProfile(
            Id: Guid.NewGuid(),
            Name: $"Test {providerKind} {transport}",
            Kind: providerKind,
            BaseUrl: baseUrl,
            ApiKeyEnvironmentVariable: "TEST_PROVIDER_API_KEY",
            DefaultModel: "test-model",
            Transport: transport,
            IsEnabled: true,
            SupportsStreaming: true,
            SupportsTools: true,
            PreferFrameworkManagedChatHistory: false,
            SupportsBackgroundResponses: transport == ProviderTransportKind.Responses,
            ConfigurationJson: "{}",
            Notes: string.Empty,
            HealthStatus: "ok",
            LastCheckedAtUtc: null,
            SuggestedModels: []);
    }

    private static MafProviderAgentFactory CreateFactory(
        IMafProviderCredentialService credentialService)
        => new(
            credentialService,
            NoOpMafProviderStreamingDispatchGate.Instance);

    private sealed class FixedCredentialResolver :
        IAgentProviderCredentialResolver
    {
        public ProviderCredentialResolution Resolve(ProviderProfile provider)
        {
            return new ProviderCredentialResolution(
                "unit-test-api-key",
                "unit-test credential resolver",
                string.Empty);
        }
    }

    private sealed class FixedCredentialService : IMafProviderCredentialService
    {
        public ProviderCredentialResolution Resolve(ProviderProfile provider)
            => new(
                "unit-test-api-key",
                "unit-test credential service",
                string.Empty);

        public string ResolveOpenAiCredentialOverride(ProviderProfile provider)
            => "resolved";
    }

    private sealed class UnresolvedCredentialService : IMafProviderCredentialService
    {
        public ProviderCredentialResolution Resolve(ProviderProfile provider)
            => new(
                string.Empty,
                "unit-test credential service",
                "No credential is configured.");

        public string ResolveOpenAiCredentialOverride(ProviderProfile provider)
            => string.Empty;
    }

    private sealed class TwoProfileCredentialResolver(
        Guid resolvedProviderId,
        string resolvedApiKey) : IAgentProviderCredentialResolver
    {
        public ProviderCredentialResolution Resolve(ProviderProfile provider)
        {
            return provider.Id == resolvedProviderId
                ? new ProviderCredentialResolution(
                    resolvedApiKey,
                    "bound secret record",
                    string.Empty)
                : new ProviderCredentialResolution(
                    string.Empty,
                    "bound secret record",
                    "The bound secret record is unavailable.");
        }
    }
}
