using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OllamaSharp.Models;

namespace CanDoItAll.Tests.Unit;

public sealed class MafProviderAgentFactoryEmptyCompletionCompositionTests
{
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
        using var services = new ServiceCollection()
            .AddSingleton<IAgentProviderCredentialResolver>(
                new FixedCredentialResolver())
            .BuildServiceProvider();
        var credentialService = new MafProviderCredentialService(services);
        var factory = new MafProviderAgentFactory(credentialService);
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
            allowBackgroundResponses: false,
            services);

        try
        {
            var decorator = agent.GetService<EmptyCompletionRetryChatClient>();

            Assert.NotNull(decorator);
            Assert.Same(
                decorator,
                decorator.GetService<EmptyCompletionRetryChatClient>());
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
        using var services = new ServiceCollection()
            .AddSingleton<IAgentProviderCredentialResolver>(
                new FixedCredentialResolver())
            .BuildServiceProvider();
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
            new MafProviderCredentialService(services));

        var agent = factory.CreateFrameworkAgent(
            provider,
            provider.DefaultModel,
            MafChatClientAgentOptionsFactory.Create(chatOptions),
            frameworkManagedHistory: false,
            allowBackgroundResponses: false,
            services);

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

    private sealed class FixedCredentialResolver :
        IAgentProviderCredentialResolver
    {
        public ProviderCredentialResolution Resolve(ProviderProfile provider)
        {
            return new ProviderCredentialResolution(
                "unit-test-api-key",
                "unit-test credential resolver",
                string.Empty,
                ShouldPromoteToProcessEnvironment: false);
        }
    }
}
