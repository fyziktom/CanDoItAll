using System.Text;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Providers;

namespace CanDoItAll.Tests.Unit.AgentFramework.Providers;

public sealed class ProviderRuntimeImageGenerationServiceTests
{
    [Fact]
    public async Task ImageGenerationService_DispatchesConcurrentRequestsThroughSharedRuntimeHandle()
    {
        var provider = CreateProvider(
            ProviderKind.OpenAi,
            ProviderProfilePurpose.ImageGeneration,
            "gpt-image-test");
        var driver = new ConcurrentImageProviderDriver(ProviderKind.OpenAi);
        var descriptorStore = new ProviderProfileRuntimeDescriptorStore();
        await using var runtimePool = new ProviderRuntimePool(
            descriptorStore,
            new ProviderRuntimeHandleFactory(new AgentProviderDriverRegistryBuilder()
                .AddDriver(driver)
                .Build()));
        var service = new ProviderRuntimeImageGenerationService(descriptorStore, runtimePool);

        var tasks = Enumerable.Range(0, 8)
            .Select(index => service.GenerateAsync(new AgentImageGenerationRequest(
                provider,
                "gpt-image-test",
                $"prompt-{index}",
                "1024x1024",
                "low",
                AgentGeneratedImageFormat.Png,
                [])))
            .ToArray();

        var results = await Task.WhenAll(tasks);

        Assert.Equal(
            Enumerable.Range(0, 8).Select(index => $"prompt-{index}").Order(),
            results.Select(result => Encoding.UTF8.GetString(Assert.Single(result.Images).Bytes)).Order());
        Assert.True(driver.MaxObservedInFlight > 1);
        var handle = await runtimePool.GetRequiredAsync(provider.Id);
        Assert.Equal(0, handle.InFlightRequestCount);
    }

    [Fact]
    public async Task ImageGenerationService_ThrowsUnsupportedCapability_WhenProviderKindHasNoImageDriver()
    {
        var provider = CreateProvider(
            ProviderKind.Ollama,
            ProviderProfilePurpose.ImageGeneration,
            "local-image");
        var descriptorStore = new ProviderProfileRuntimeDescriptorStore();
        await using var runtimePool = new ProviderRuntimePool(
            descriptorStore,
            new ProviderRuntimeHandleFactory(new AgentProviderDriverRegistryBuilder()
                .AddDriver(new ConcurrentImageProviderDriver(ProviderKind.OpenAi))
                .Build()));
        var service = new ProviderRuntimeImageGenerationService(descriptorStore, runtimePool);

        var exception = await Assert.ThrowsAsync<UnsupportedProviderCapabilityException>(() => service.GenerateAsync(new AgentImageGenerationRequest(
            provider,
            "local-image",
            "draw a cube",
            "1024x1024",
            "low",
            AgentGeneratedImageFormat.Png,
            [])));

        Assert.Equal(ProviderKind.Ollama, exception.ProviderKind);
        Assert.Equal(AgentProviderCapabilityKind.ImageGeneration, exception.Capability);
    }

    [Fact]
    public async Task ImageGenerationService_RejectsNonImageProviderPurpose()
    {
        var provider = CreateProvider(
            ProviderKind.OpenAi,
            ProviderProfilePurpose.Chat,
            "gpt-5-mini");
        var descriptorStore = new ProviderProfileRuntimeDescriptorStore();
        await using var runtimePool = new ProviderRuntimePool(
            descriptorStore,
            new ProviderRuntimeHandleFactory(new AgentProviderDriverRegistryBuilder()
                .AddDriver(new ConcurrentImageProviderDriver(ProviderKind.OpenAi))
                .Build()));
        var service = new ProviderRuntimeImageGenerationService(descriptorStore, runtimePool);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.GenerateAsync(new AgentImageGenerationRequest(
            provider,
            "gpt-5-mini",
            "draw a cube",
            "1024x1024",
            "low",
            AgentGeneratedImageFormat.Png,
            [])));

        Assert.Contains("image-generation provider profile", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static ProviderProfile CreateProvider(
        ProviderKind kind,
        ProviderProfilePurpose purpose,
        string defaultModel)
    {
        return new ProviderProfile(
            Guid.NewGuid(),
            $"{kind} {purpose} provider",
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
            Purpose: purpose);
    }

    private sealed class ConcurrentImageProviderDriver(ProviderKind providerKind) : IProviderImageGenerationDriver
    {
        private int inFlight;
        private int maxObservedInFlight;

        public ProviderKind ProviderKind { get; } = providerKind;

        public IReadOnlySet<AgentProviderCapabilityKind> Capabilities { get; } =
            new HashSet<AgentProviderCapabilityKind> { AgentProviderCapabilityKind.ImageGeneration };

        public int MaxObservedInFlight => Volatile.Read(ref maxObservedInFlight);

        public ProviderDispatchLimits GetDispatchLimits(ProviderDispatchQuery query)
        {
            return ProviderDispatchLimits.Unbatched(
                TimeSpan.FromSeconds(30),
                maxInFlightRequests: 8);
        }

        public async Task<ProviderImageGenerationResult> GenerateImageAsync(
            ProviderImageGenerationRequest request,
            CancellationToken cancellationToken = default)
        {
            var current = Interlocked.Increment(ref inFlight);
            ObserveMaxInFlight(current);
            try
            {
                await Task.Delay(50, cancellationToken).ConfigureAwait(false);
                return new ProviderImageGenerationResult(
                    request.Model,
                    request.Format,
                    [new ProviderGeneratedImage("image/png", Encoding.UTF8.GetBytes(request.Prompt), request.Prompt)]);
            }
            finally
            {
                Interlocked.Decrement(ref inFlight);
            }
        }

        private void ObserveMaxInFlight(int current)
        {
            while (true)
            {
                var observed = Volatile.Read(ref maxObservedInFlight);
                if (current <= observed ||
                    Interlocked.CompareExchange(ref maxObservedInFlight, current, observed) == observed)
                {
                    return;
                }
            }
        }
    }
}
