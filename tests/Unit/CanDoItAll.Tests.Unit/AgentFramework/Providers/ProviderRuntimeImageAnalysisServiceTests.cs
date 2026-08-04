using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Providers;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Unit.AgentFramework.Providers;

public sealed class ProviderRuntimeImageAnalysisServiceTests
{
    [Fact]
    public async Task AnalyzeAsync_maps_gateway_request_and_preserves_token_usage()
    {
        var gateway = new FakeMafProviderRuntimeGateway
        {
            ImageChatResult = new ProviderTestChatResult("resolved-vision-model", "visible evidence", 123, 45)
        };
        var service = new ProviderRuntimeImageAnalysisService(gateway);
        var provider = CreateProvider(ProviderKind.OpenAi, "gpt-4o");
        var firstBytes = new byte[] { 1, 2, 3 };
        var secondBytes = new byte[] { 4, 5, 6 };
        const string configurationJson = """{"modelParameters":{"think":false}}""";

        var result = await service.AnalyzeAsync(new AgentImageAnalysisRequest(
            provider,
            "gpt-4o",
            "Describe visible differences.",
            [
                new AgentImageAnalysisSource("01-before.png", "image/png", firstBytes),
                new AgentImageAnalysisSource("02-after.jpg", "image/jpeg", secondBytes)
            ],
            configurationJson));

        Assert.Equal("resolved-vision-model", result.Model);
        Assert.Equal("visible evidence", result.Analysis);
        Assert.Equal(123, result.InputTokens);
        Assert.Equal(45, result.OutputTokens);
        Assert.Equal(1, gateway.ImageChatCallCount);
        Assert.Same(provider, gateway.ObservedProvider);
        Assert.Equal("gpt-4o", gateway.ObservedModel);
        Assert.Equal("Describe visible differences.", gateway.ObservedRequest?.Prompt);
        Assert.Equal(configurationJson, gateway.ObservedModelParameterConfigurationJson);
        Assert.Collection(
            gateway.ObservedAttachments,
            attachment =>
            {
                Assert.Equal("01-before.png", attachment.Name);
                Assert.Equal("image/png", attachment.ContentType);
                Assert.Same(firstBytes, attachment.Bytes);
            },
            attachment =>
            {
                Assert.Equal("02-after.jpg", attachment.Name);
                Assert.Equal("image/jpeg", attachment.ContentType);
                Assert.Same(secondBytes, attachment.Bytes);
            });
    }

    [Fact]
    public async Task AnalyzeAsync_rejects_provider_model_without_vision_without_calling_gateway()
    {
        var gateway = new FakeMafProviderRuntimeGateway();
        var service = new ProviderRuntimeImageAnalysisService(gateway);
        var provider = CreateProvider(ProviderKind.Ollama, "llama3.2");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.AnalyzeAsync(
            CreateRequest(provider, "llama3.2")));

        Assert.Contains("does not support vision", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, gateway.ImageChatCallCount);
    }

    [Fact]
    public async Task AnalyzeAsync_rejects_empty_sources_without_calling_gateway()
    {
        var gateway = new FakeMafProviderRuntimeGateway();
        var service = new ProviderRuntimeImageAnalysisService(gateway);
        var provider = CreateProvider(ProviderKind.OpenAi, "gpt-4o");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.AnalyzeAsync(
            new AgentImageAnalysisRequest(provider, "gpt-4o", "Analyze.", [])));

        Assert.Contains("at least one image source", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, gateway.ImageChatCallCount);
    }

    [Fact]
    public async Task AnalyzeAsync_rejects_disabled_provider_missing_model_and_missing_prompt()
    {
        var gateway = new FakeMafProviderRuntimeGateway();
        var service = new ProviderRuntimeImageAnalysisService(gateway);
        var provider = CreateProvider(ProviderKind.OpenAi, "gpt-4o");
        var source = new AgentImageAnalysisSource("frame.png", "image/png", [1]);
        var invalidRequests = new AgentImageAnalysisRequest[]
        {
            new(provider with { IsEnabled = false }, "gpt-4o", "Analyze.", [source]),
            new(provider, string.Empty, "Analyze.", [source]),
            new(provider, "gpt-4o", " ", [source])
        };

        foreach (var request in invalidRequests)
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.AnalyzeAsync(request));
        }

        Assert.Equal(0, gateway.ImageChatCallCount);
    }

    [Fact]
    public async Task AnalyzeAsync_rejects_invalid_sources_without_calling_gateway()
    {
        var gateway = new FakeMafProviderRuntimeGateway();
        var service = new ProviderRuntimeImageAnalysisService(gateway);
        var provider = CreateProvider(ProviderKind.OpenAi, "gpt-4o");
        var invalidSources = new AgentImageAnalysisSource[]
        {
            new(string.Empty, "image/png", [1]),
            new("document.txt", "text/plain", [1]),
            new("empty.png", "image/png", [])
        };

        foreach (var source in invalidSources)
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.AnalyzeAsync(
                new AgentImageAnalysisRequest(provider, "gpt-4o", "Analyze.", [source])));
        }

        Assert.Equal(0, gateway.ImageChatCallCount);
    }

    [Fact]
    public async Task AnalyzeAsync_propagates_cancellation_to_gateway()
    {
        var gateway = new FakeMafProviderRuntimeGateway
        {
            ImageChatHandler = async (_, _, _, _, _, cancellationToken) =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                return new ProviderTestChatResult("gpt-4o", "unreachable", 0, 0);
            }
        };
        var service = new ProviderRuntimeImageAnalysisService(gateway);
        using var cancellationSource = new CancellationTokenSource();

        var analysisTask = service.AnalyzeAsync(
            CreateRequest(CreateProvider(ProviderKind.OpenAi, "gpt-4o"), "gpt-4o"),
            cancellationSource.Token);
        await cancellationSource.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => analysisTask);
        Assert.Equal(cancellationSource.Token, gateway.ObservedCancellationToken);
    }

    [Fact]
    public async Task AnalyzeAsync_propagates_provider_failure_without_wrapping()
    {
        var expected = new InvalidOperationException("provider image chat failed");
        var gateway = new FakeMafProviderRuntimeGateway
        {
            ImageChatHandler = (_, _, _, _, _, _) => Task.FromException<ProviderTestChatResult>(expected)
        };
        var service = new ProviderRuntimeImageAnalysisService(gateway);

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() => service.AnalyzeAsync(
            CreateRequest(CreateProvider(ProviderKind.OpenAi, "gpt-4o"), "gpt-4o")));

        Assert.Same(expected, actual);
    }

    [Fact]
    public async Task Unavailable_service_fails_explicitly()
    {
        var service = new UnavailableAgentImageAnalysisService();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.AnalyzeAsync(
            CreateRequest(CreateProvider(ProviderKind.OpenAi, "gpt-4o"), "gpt-4o")));

        Assert.Contains("provider-runtime image analysis service", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AddMafProviderRuntimeServices_registers_provider_image_analysis_adapter()
    {
        var services = new ServiceCollection();

        services.AddMafProviderRuntimeServices();

        var registration = Assert.Single(services, descriptor =>
            descriptor.ServiceType == typeof(IAgentImageAnalysisService));
        Assert.Equal(typeof(ProviderRuntimeImageAnalysisService), registration.ImplementationType);
    }

    [Fact]
    public void MafRuntimeDependencyResolver_prefers_registered_image_analysis_service()
    {
        var gateway = new FakeMafProviderRuntimeGateway();
        var streamingGate = new FakeMafProviderStreamingDispatchGate();
        var imageAnalysisService = new FakeAgentImageAnalysisService();
        var services = new ServiceCollection();
        services.AddSingleton<IMafProviderRuntimeGateway>(gateway);
        services.AddSingleton<IMafProviderStreamingDispatchGate>(streamingGate);
        services.AddSingleton<IAgentImageAnalysisService>(imageAnalysisService);
        using var provider = services.BuildServiceProvider();

        var dependencies = new MafRuntimeDependencyResolver().ResolveProviderDependencies(provider);

        Assert.Same(gateway, dependencies.ProviderRuntimeGateway);
        Assert.Same(streamingGate, dependencies.ProviderStreamingDispatchGate);
        Assert.Same(imageAnalysisService, dependencies.ImageAnalysisService);
    }

    [Fact]
    public void MafRuntimeDependencyResolver_uses_explicit_gateway_adapter_for_standalone_runtime()
    {
        var gateway = new FakeMafProviderRuntimeGateway();
        var imageAnalysisService = new ProviderRuntimeImageAnalysisService(gateway);
        var services = new ServiceCollection();
        services.AddSingleton<IMafProviderRuntimeGateway>(gateway);
        services.AddSingleton<IMafProviderStreamingDispatchGate>(new FakeMafProviderStreamingDispatchGate());
        services.AddSingleton<IAgentImageAnalysisService>(imageAnalysisService);
        using var provider = services.BuildServiceProvider();

        var dependencies = new MafRuntimeDependencyResolver().ResolveProviderDependencies(provider);

        Assert.Same(imageAnalysisService, dependencies.ImageAnalysisService);
    }

    private static AgentImageAnalysisRequest CreateRequest(ProviderProfile provider, string model)
        => new(
            provider,
            model,
            "Analyze visible evidence.",
            [new AgentImageAnalysisSource("frame.png", "image/png", [1, 2, 3])]);

    private static ProviderProfile CreateProvider(ProviderKind kind, string defaultModel)
        => new(
            Guid.NewGuid(),
            $"{kind} vision provider",
            kind,
            "https://provider.example.test",
            "PROVIDER_API_KEY",
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
            SuggestedModels: [defaultModel],
            Purpose: ProviderProfilePurpose.Chat);

    private sealed class FakeMafProviderRuntimeGateway : IMafProviderRuntimeGateway
    {
        public Func<
            ProviderProfile,
            ProviderTestChatRequest,
            string,
            IReadOnlyList<ProviderChatAttachment>,
            string,
            CancellationToken,
            Task<ProviderTestChatResult>>? ImageChatHandler { get; init; }

        public ProviderTestChatResult ImageChatResult { get; init; } = new("vision-model", "analysis", 1, 2);

        public int ImageChatCallCount { get; private set; }

        public ProviderProfile? ObservedProvider { get; private set; }

        public ProviderTestChatRequest? ObservedRequest { get; private set; }

        public string? ObservedModel { get; private set; }

        public IReadOnlyList<ProviderChatAttachment> ObservedAttachments { get; private set; } = [];

        public string? ObservedModelParameterConfigurationJson { get; private set; }

        public CancellationToken ObservedCancellationToken { get; private set; }

        public Task<ProviderHealthResult> TestProviderAsync(
            ProviderProfile provider,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<ProviderTestChatResult> RunProviderTestChatAsync(
            ProviderProfile provider,
            ProviderTestChatRequest request,
            string model,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<ProviderTestChatResult> RunProviderImageChatAsync(
            ProviderProfile provider,
            ProviderTestChatRequest request,
            string model,
            IReadOnlyList<ProviderChatAttachment> attachments,
            string modelParameterConfigurationJson = "",
            CancellationToken cancellationToken = default)
        {
            ImageChatCallCount++;
            ObservedProvider = provider;
            ObservedRequest = request;
            ObservedModel = model;
            ObservedAttachments = attachments;
            ObservedModelParameterConfigurationJson = modelParameterConfigurationJson;
            ObservedCancellationToken = cancellationToken;
            return ImageChatHandler is null
                ? Task.FromResult(ImageChatResult)
                : ImageChatHandler(
                    provider,
                    request,
                    model,
                    attachments,
                    modelParameterConfigurationJson,
                    cancellationToken);
        }

        public Task<ProviderModelMaintenanceEditorResult> CreateOrUpdateProviderModelAsync(
            ProviderProfile provider,
            ProviderModelMaintenanceEditorRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class FakeMafProviderStreamingDispatchGate : IMafProviderStreamingDispatchGate
    {
        public ValueTask<IAsyncDisposable> EnterAsync(
            ProviderProfile provider,
            string model,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class FakeAgentImageAnalysisService : IAgentImageAnalysisService
    {
        public Task<AgentImageAnalysisResult> AnalyzeAsync(
            AgentImageAnalysisRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
