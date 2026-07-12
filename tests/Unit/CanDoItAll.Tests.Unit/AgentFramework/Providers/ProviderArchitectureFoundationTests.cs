using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Providers;

namespace CanDoItAll.Tests.Unit.AgentFramework.Providers;

public sealed class ProviderArchitectureFoundationTests
{
    [Fact]
    public async Task Registry_ResolvesFakeSubdrivers_ForAllCapabilityLanes()
    {
        var provider = CreateProvider();
        var driver = new FakeProviderDriver(
            ProviderKind.OpenAi,
            Enum.GetValues<AgentProviderCapabilityKind>());
        var factory = new AgentProviderDriverRegistryBuilder()
            .AddDriver(driver)
            .Build();

        Assert.True(factory.Supports(ProviderKind.OpenAi, AgentProviderCapabilityKind.ChatCompletion));
        Assert.Same(driver, factory.Resolve<IProviderChatCompletionDriver>(ProviderKind.OpenAi));
        Assert.Same(driver, factory.Resolve<IProviderModelCatalogDriver>(ProviderKind.OpenAi));
        Assert.Same(driver, factory.Resolve<IProviderImageGenerationDriver>(ProviderKind.OpenAi));
        Assert.Same(driver, factory.Resolve<IProviderSpeechToTextDriver>(ProviderKind.OpenAi));
        Assert.Same(driver, factory.Resolve<IProviderTextToSpeechDriver>(ProviderKind.OpenAi));
        Assert.Same(driver, factory.Resolve<IProviderModelMaintenanceDriver>(ProviderKind.OpenAi));
        Assert.Same(driver, factory.Resolve<IProviderHealthDriver>(ProviderKind.OpenAi));

        var chatResult = await factory
            .Resolve<IProviderChatCompletionDriver>(ProviderKind.OpenAi)
            .CompleteChatAsync(new ProviderChatCompletionRequest(
                provider,
                "test-chat",
                "system",
                [new ProviderTestChatMessage(ChatMessageRole.User, "hello", DateTimeOffset.UnixEpoch)],
                "prompt"));
        Assert.Equal("fake response", chatResult.ResponseText);

        var imageResult = await factory
            .Resolve<IProviderImageGenerationDriver>(ProviderKind.OpenAi)
            .GenerateImageAsync(new ProviderImageGenerationRequest(
                provider,
                "test-image",
                "draw",
                "1024x1024",
                "standard",
                ProviderGeneratedImageFormat.Png,
                []));
        Assert.Single(imageResult.Images);

        var textToSpeechResult = await factory
            .Resolve<IProviderTextToSpeechDriver>(ProviderKind.OpenAi)
            .SynthesizeSpeechAsync(new ProviderTextToSpeechRequest(
                provider,
                "test-tts",
                "speak",
                "voice",
                "mp3",
                string.Empty));
        Assert.NotEmpty(textToSpeechResult.AudioBytes);

        var maintenanceResult = await factory
            .Resolve<IProviderModelMaintenanceDriver>(ProviderKind.OpenAi)
            .CreateOrUpdateModelAsync(new ProviderModelMaintenanceRequest(
                provider,
                "target-model",
                "base-model",
                "system",
                4096));
        Assert.Equal("target-model", maintenanceResult.Model);
    }

    [Fact]
    public void Registry_ThrowsPredictably_ForUnsupportedCapability()
    {
        var factory = new AgentProviderDriverRegistryBuilder()
            .AddDriver(new FakeProviderDriver(ProviderKind.Ollama, [AgentProviderCapabilityKind.ChatCompletion]))
            .Build();

        var exception = Assert.Throws<UnsupportedProviderCapabilityException>(
            () => factory.Resolve<IProviderImageGenerationDriver>(ProviderKind.Ollama));
        Assert.Equal(ProviderKind.Ollama, exception.ProviderKind);
        Assert.Equal(AgentProviderCapabilityKind.ImageGeneration, exception.Capability);
    }

    [Fact]
    public void Registry_ThrowsPredictably_ForDuplicateCapabilityRegistration()
    {
        var builder = new AgentProviderDriverRegistryBuilder()
            .AddDriver(new FakeProviderDriver(ProviderKind.AzureOpenAi, [AgentProviderCapabilityKind.ChatCompletion]))
            .AddDriver(new FakeProviderDriver(ProviderKind.AzureOpenAi, [AgentProviderCapabilityKind.ChatCompletion]));

        var exception = Assert.Throws<DuplicateProviderCapabilityRegistrationException>(() => builder.Build());
        Assert.Equal(ProviderKind.AzureOpenAi, exception.ProviderKind);
        Assert.Equal(AgentProviderCapabilityKind.ChatCompletion, exception.Capability);
    }

    [Fact]
    public void DispatchMetadata_IsQueriedByProviderCapabilityOperationAndModel()
    {
        var provider = CreateProvider(defaultModel: "batch-model");
        var factory = new AgentProviderDriverRegistryBuilder()
            .AddDriver(new FakeProviderDriver(ProviderKind.OpenAi, [AgentProviderCapabilityKind.ChatCompletion]))
            .Build();

        var limits = factory.GetDispatchLimits(new ProviderDispatchQuery(
            provider,
            AgentProviderCapabilityKind.ChatCompletion,
            AgentProviderOperationKind.CompleteChat,
            "batch-model"));

        Assert.True(limits.SupportsBatching);
        Assert.Equal(5, limits.MaxBatchSize);
        Assert.Equal(2, limits.MaxInFlightBatches);
        Assert.Equal(50, limits.MaxQueueDepth);

        var unbatchedLimits = factory.GetDispatchLimits(new ProviderDispatchQuery(
            provider,
            AgentProviderCapabilityKind.ChatCompletion,
            AgentProviderOperationKind.CompleteChat,
            "single-model"));

        Assert.False(unbatchedLimits.SupportsBatching);
        Assert.Equal(1, unbatchedLimits.MaxBatchSize);
    }

    [Fact]
    public void GenericProviderContracts_DoNotExposeProviderSpecificOperationNames()
    {
        var root = FindRepositoryRoot();
        var providerProjectRoot = Path.Combine(root, "src/MAF/Common/CanDoItAll.AgentFramework.Providers");
        var bannedTerms = new[] { "Ollama", "OpenAi", "AzureOpenAi", "ComfyUi" };
        var genericProviderSource = Directory
            .EnumerateFiles(providerProjectRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}Drivers{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .ToList();

        foreach (var file in genericProviderSource)
        {
            var text = File.ReadAllText(file);
            Assert.DoesNotContain(bannedTerms, term => text.Contains(term, StringComparison.Ordinal));
        }
    }

    [Fact]
    public void FoundationPhase_DoesNotReplaceExistingRuntimeConsumers()
    {
        var root = FindRepositoryRoot();
        var filesThatMustNotAdoptProviderProject = new[]
        {
            "src/Modules/CanDoItAll.Modules.AgentFramework/CanDoItAll.Modules.AgentFramework.csproj",
            "src/Modules/CanDoItAll.Modules.Workspace/CanDoItAll.Modules.Workspace.csproj"
        };

        foreach (var relativePath in filesThatMustNotAdoptProviderProject)
        {
            var text = File.ReadAllText(Path.Combine(root, relativePath));
            Assert.DoesNotContain("CanDoItAll.AgentFramework.Providers", text, StringComparison.Ordinal);
        }

        Assert.Contains(
            "CreateOrUpdateProviderModelAsync",
            File.ReadAllText(Path.Combine(root, "src/MAF/Common/CanDoItAll.AgentFramework.Core/Contracts/Contracts.cs")),
            StringComparison.Ordinal);
        Assert.Contains(
            "ApplyProviderModelMaintenanceResult",
            File.ReadAllText(Path.Combine(root, "src/MAF/Common/CanDoItAll.AgentFramework.Core/Providers/ProviderServices.cs")),
            StringComparison.Ordinal);
        Assert.Contains(
            "providerRuntimeGateway",
            File.ReadAllText(Path.Combine(root, "src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Providers/ProviderRuntimeDiagnostics.cs")),
            StringComparison.Ordinal);
        Assert.Contains(
            "ProviderRuntimeVoiceDriver",
            File.ReadAllText(Path.Combine(root, "src/MAF/Common/CanDoItAll.AgentFramework.Voice/AgentVoiceDriverFactory.cs")),
            StringComparison.Ordinal);
        var imageToolProviderSource = File.ReadAllText(Path.Combine(root, "src/Modules/CanDoItAll.Modules.AgentFramework/AgentTools/ImageGenerationAgentRuntimeToolProvider.cs"));
        Assert.Contains(
            "IAgentImageGenerationService",
            imageToolProviderSource,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "BuildOpenAiImagesEndpoint",
            imageToolProviderSource,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "GenerateOpenAiImageAsync",
            imageToolProviderSource,
            StringComparison.Ordinal);
    }

    private static ProviderProfile CreateProvider(
        ProviderKind kind = ProviderKind.OpenAi,
        string defaultModel = "test-model")
    {
        return new ProviderProfile(
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            "Fake provider",
            kind,
            "https://example.test",
            "FAKE_API_KEY",
            defaultModel,
            ProviderTransportKind.ChatCompletions,
            IsEnabled: true,
            SupportsStreaming: false,
            SupportsTools: false,
            PreferFrameworkManagedChatHistory: true,
            SupportsBackgroundResponses: false,
            ConfigurationJson: "{}",
            Notes: string.Empty,
            HealthStatus: "Not checked",
            LastCheckedAtUtc: null,
            SuggestedModels: [defaultModel]);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CanDoItAll.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find repository root.");
    }

    private sealed class FakeProviderDriver(
        ProviderKind providerKind,
        IEnumerable<AgentProviderCapabilityKind> capabilities) :
        IProviderHealthDriver,
        IProviderModelCatalogDriver,
        IProviderChatCompletionDriver,
        IProviderImageGenerationDriver,
        IProviderSpeechToTextDriver,
        IProviderTextToSpeechDriver,
        IProviderModelMaintenanceDriver
    {
        private static readonly ProviderDispatchLimits BatchLimits = ProviderDispatchLimits.Batched(
            maxBatchSize: 5,
            maxInFlightBatches: 2,
            maxQueueDepth: 50,
            maxQueueDelay: TimeSpan.FromMilliseconds(25),
            requestTimeout: TimeSpan.FromSeconds(30));

        private static readonly ProviderDispatchLimits SingleLimits = ProviderDispatchLimits.Unbatched(TimeSpan.FromSeconds(30));

        public ProviderKind ProviderKind { get; } = providerKind;

        public IReadOnlySet<AgentProviderCapabilityKind> Capabilities { get; } = capabilities.ToHashSet();

        public ProviderDispatchLimits GetDispatchLimits(ProviderDispatchQuery query)
        {
            return string.Equals(query.Model, "batch-model", StringComparison.OrdinalIgnoreCase)
                ? BatchLimits
                : SingleLimits;
        }

        public Task<ProviderHealthResult> TestHealthAsync(
            ProviderProfile provider,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ProviderHealthResult(true, "healthy", [provider.DefaultModel]));
        }

        public Task<IReadOnlyList<ProviderModelDescriptor>> ListModelsAsync(
            ProviderModelCatalogRequest request,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<ProviderModelDescriptor> models =
            [
                new ProviderModelDescriptor(
                    request.Provider.DefaultModel,
                    request.Provider.DefaultModel,
                    request.Capability,
                    SingleLimits)
            ];

            return Task.FromResult(models);
        }

        public Task<ProviderChatCompletionResult> CompleteChatAsync(
            ProviderChatCompletionRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ProviderChatCompletionResult(
                request.Model,
                "fake response",
                InputTokens: 1,
                OutputTokens: 2));
        }

        public Task<ProviderImageGenerationResult> GenerateImageAsync(
            ProviderImageGenerationRequest request,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<ProviderGeneratedImage> images =
            [
                new ProviderGeneratedImage("image/png", [1, 2, 3], "revised")
            ];

            return Task.FromResult(new ProviderImageGenerationResult(
                request.Model,
                request.Format,
                images));
        }

        public Task<ProviderSpeechToTextResult> TranscribeSpeechAsync(
            ProviderSpeechToTextRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ProviderSpeechToTextResult(request.Model, "transcript"));
        }

        public Task<ProviderTextToSpeechResult> SynthesizeSpeechAsync(
            ProviderTextToSpeechRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ProviderTextToSpeechResult(
                request.Model,
                request.VoiceId,
                request.ResponseFormat,
                "audio/mpeg",
                [1, 2, 3]));
        }

        public Task<ProviderModelMaintenanceResult> CreateOrUpdateModelAsync(
            ProviderModelMaintenanceRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ProviderModelMaintenanceResult(
                request.Model,
                request.BaseModel,
                request.SystemPrompt,
                request.ContextLength,
                "definition",
                "updated"));
        }
    }
}
