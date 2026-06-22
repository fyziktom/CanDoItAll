using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Tooling;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.AgentFramework;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Unit;

public sealed class ImageGenerationAgentRuntimeToolProviderTests
{
    [Fact]
    public async Task CreateToolsAsync_returns_image_generation_tool_when_agent_is_allowed()
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        var toolProvider = new ImageGenerationAgentRuntimeToolProvider(
            new ThrowingProviderProfileRegistry(),
            new StaticWorkspacePathResolver(Path.GetTempPath()),
            new FakeAgentImageGenerationService(),
            services);
        var chatProvider = CreateProvider(ProviderProfilePurpose.Chat);
        var imageProvider = CreateProvider(ProviderProfilePurpose.ImageGeneration);
        var agent = CreateAgent(
            chatProvider.Id,
            AgentImageGenerationAccessMetadata.Write(
                "{}",
                new AgentImageGenerationAccessSettings
                {
                    CanGenerateImages = true,
                    PreferredProviderProfileId = imageProvider.Id,
                    DefaultModel = "gpt-image-1-mini"
                }));

        var tools = await toolProvider.CreateToolsAsync(
            CreateContext(agent, chatProvider),
            CancellationToken.None);

        var tool = Assert.Single(tools);
        Assert.Equal(950, toolProvider.Order);
        Assert.Equal("image-generation.runtime-tools", toolProvider.Descriptor?.ProviderKey);
        Assert.Equal(AgentToolInvocationPolicyMetadata.ImageGenerationCreate, tool.Name);
    }

    [Fact]
    public async Task CreateToolsAsync_returns_no_tools_when_agent_image_generation_is_disabled()
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        var toolProvider = new ImageGenerationAgentRuntimeToolProvider(
            new ThrowingProviderProfileRegistry(),
            new StaticWorkspacePathResolver(Path.GetTempPath()),
            new FakeAgentImageGenerationService(),
            services);
        var chatProvider = CreateProvider(ProviderProfilePurpose.Chat);
        var agent = CreateAgent(
            chatProvider.Id,
            AgentImageGenerationAccessMetadata.Write(
                "{}",
                new AgentImageGenerationAccessSettings()));

        var tools = await toolProvider.CreateToolsAsync(
            CreateContext(agent, chatProvider),
            CancellationToken.None);

        Assert.Empty(tools);
    }

    private static AgentRuntimeToolProviderContext CreateContext(
        AgentDefinition agent,
        ProviderProfile provider)
    {
        return new AgentRuntimeToolProviderContext(
            agent,
            provider,
            [],
            SuppressApprovalRequirements: false,
            AgentRuntimeToolProviderPurpose.InteractiveChat,
            RuntimeSessionKey: "unit-image-generation",
            AgentRuntimeContextIntent.Empty,
            Tags: new Dictionary<string, string>());
    }

    private static AgentDefinition CreateAgent(
        Guid providerProfileId,
        string configurationJson)
    {
        var now = DateTimeOffset.UtcNow;
        return new AgentDefinition(
            Guid.NewGuid(),
            "Image test agent",
            "Image tester",
            "Tests image generation tool provider.",
            "Test image generation.",
            AgentLifecycleStatus.Active,
            providerProfileId,
            "gpt-5-mini",
            AgentWorkloadKind.General,
            AgentChatHistoryMode.ProviderDefault,
            0.2,
            RequirePerServiceCallChatHistoryPersistence: false,
            EnableBackgroundResponses: false,
            configurationJson,
            IsTemplate: false,
            TemplateKey: string.Empty,
            AgentPermissionsPolicy.Default,
            [],
            [],
            now,
            now);
    }

    private static ProviderProfile CreateProvider(ProviderProfilePurpose purpose)
    {
        return new ProviderProfile(
            Guid.NewGuid(),
            purpose == ProviderProfilePurpose.ImageGeneration ? "OpenAI image" : "OpenAI chat",
            ProviderKind.OpenAi,
            "https://api.openai.com",
            "OPENAI_API_KEY",
            purpose == ProviderProfilePurpose.ImageGeneration ? "gpt-image-1-mini" : "gpt-5-mini",
            ProviderTransportKind.ChatCompletions,
            IsEnabled: true,
            SupportsStreaming: true,
            SupportsTools: true,
            PreferFrameworkManagedChatHistory: true,
            SupportsBackgroundResponses: false,
            ConfigurationJson: string.Empty,
            Notes: string.Empty,
            HealthStatus: string.Empty,
            LastCheckedAtUtc: null,
            SuggestedModels: [],
            purpose);
    }

    private sealed class ThrowingProviderProfileRegistry : IProviderProfileRegistry
    {
        public Task<IReadOnlyList<ProviderProfile>> ListProvidersAsync(CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<ProviderProfile?> GetProviderAsync(Guid providerId, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<ProviderProfileEditorModel> GetProviderEditorAsync(Guid? providerId = null, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<Guid> SaveProviderAsync(ProviderProfileEditorModel model, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task DeleteProviderAsync(Guid providerId, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<ProviderProfile> UpdateProviderAsync(
            Guid providerId,
            Func<ProviderProfile, ProviderProfile> update,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class StaticWorkspacePathResolver(string workspaceRoot) : IWorkspacePathResolver
    {
        public string ResolveWorkspaceRoot()
        {
            return workspaceRoot;
        }

        public string ResolveManagedFilesRoot()
        {
            return workspaceRoot;
        }

        public string ResolveExportsRoot()
        {
            return workspaceRoot;
        }

        public string ResolveEvidenceRoot()
        {
            return workspaceRoot;
        }

        public string ResolveManagerArtifactsRoot()
        {
            return workspaceRoot;
        }
    }

    private sealed class FakeAgentImageGenerationService : IAgentImageGenerationService
    {
        public List<AgentImageGenerationRequest> Requests { get; } = [];

        public Task<AgentImageGenerationResult> GenerateAsync(
            AgentImageGenerationRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Requests.Add(request);
            return Task.FromResult(new AgentImageGenerationResult(
                request.Model,
                request.Format,
                [new AgentGeneratedImage("image/png", [1, 2, 3], "revised")]));
        }
    }
}
