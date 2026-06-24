using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Providers;

namespace CanDoItAll.Tests.Unit.AgentFramework.Providers;

public sealed class ProviderDispatchLaneGateTests
{
    [Fact]
    public async Task EnterAsync_serializes_same_provider_operation_and_model()
    {
        var provider = CreateProvider(Guid.Parse("a1089dac-a65a-4d87-9c8f-7174417111b0"));
        var gate = new ProviderDispatchLaneGate(
            new FakeProviderFactory(ProviderDispatchLimits.Unbatched(TimeSpan.FromSeconds(30))));
        var query = CreateQuery(provider, "shared-model");

        await using var firstLease = await gate.EnterAsync(query);
        var secondLeaseTask = gate.EnterAsync(query).AsTask();

        var secondEnteredBeforeRelease = await Task.WhenAny(secondLeaseTask, Task.Delay(100)) == secondLeaseTask;

        Assert.False(secondEnteredBeforeRelease);

        await firstLease.DisposeAsync();
        var secondLease = await secondLeaseTask.WaitAsync(TimeSpan.FromSeconds(2));
        await secondLease.DisposeAsync();
    }

    [Fact]
    public async Task EnterAsync_allows_different_models_to_use_independent_lanes()
    {
        var provider = CreateProvider(Guid.Parse("4b1f0632-c744-4cc2-bc3d-43245f3b5ee2"));
        var gate = new ProviderDispatchLaneGate(
            new FakeProviderFactory(ProviderDispatchLimits.Unbatched(TimeSpan.FromSeconds(30))));

        await using var firstLease = await gate.EnterAsync(CreateQuery(provider, "model-a"));
        var secondLease = await gate
            .EnterAsync(CreateQuery(provider, "model-b"))
            .AsTask()
            .WaitAsync(TimeSpan.FromSeconds(2));

        await secondLease.DisposeAsync();
    }

    [Fact]
    public async Task EnterAsync_allows_image_analysis_subdriver_during_chat_completion_for_same_model()
    {
        var provider = CreateProvider(Guid.Parse("42be0c93-4d2a-45c4-a18c-4f7e52901c7d"));
        var gate = new ProviderDispatchLaneGate(
            new FakeProviderFactory(ProviderDispatchLimits.Unbatched(TimeSpan.FromSeconds(30))));

        await using var chatLease = await gate.EnterAsync(CreateQuery(provider, "shared-model"));
        var imageLease = await gate
            .EnterAsync(CreateQuery(provider, "shared-model", AgentProviderOperationKind.AnalyzeImage))
            .AsTask()
            .WaitAsync(TimeSpan.FromSeconds(2));

        await imageLease.DisposeAsync();
    }

    [Fact]
    public async Task ProviderRuntimeHandle_dispatch_enforces_descriptor_timeout_and_clears_pending_request()
    {
        var provider = CreateProvider(Guid.Parse("48e78e35-0a5e-4d27-b593-a3ab925518bd"));
        var descriptor = ProviderRuntimeDescriptor.FromProfile(provider, timeoutSeconds: 5);
        await using var handle = new ProviderRuntimeHandle(
            descriptor,
            new FakeProviderFactory(ProviderDispatchLimits.Unbatched(TimeSpan.FromSeconds(30))),
            new ImmediateDispatchLaneGate());
        var request = new ProviderRuntimeDispatchRequest<string>(
            CreateQuery(provider, "shared-model"),
            "payload");

        var exception = await Assert.ThrowsAsync<TimeoutException>(() =>
            handle.DispatchAsync(
                request,
                async (_, cancellationToken) =>
                {
                    await Task.Delay(TimeSpan.FromMinutes(1), cancellationToken);
                    return "unreachable";
                }));

        Assert.Contains("exceeded the configured timeout", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, handle.InFlightRequestCount);
    }

    private static ProviderProfile CreateProvider(Guid providerId)
    {
        return new ProviderProfile(
            providerId,
            "Dispatch provider",
            ProviderKind.Ollama,
            "http://localhost:11434",
            string.Empty,
            "shared-model",
            ProviderTransportKind.ChatCompletions,
            IsEnabled: true,
            SupportsStreaming: true,
            SupportsTools: true,
            PreferFrameworkManagedChatHistory: true,
            SupportsBackgroundResponses: false,
            ConfigurationJson: "{}",
            Notes: string.Empty,
            HealthStatus: "Not checked",
            LastCheckedAtUtc: null,
            SuggestedModels: ["shared-model", "model-a", "model-b"]);
    }

    private static ProviderDispatchQuery CreateQuery(
        ProviderProfile provider,
        string model,
        AgentProviderOperationKind operation = AgentProviderOperationKind.CompleteChat)
    {
        return new ProviderDispatchQuery(
            provider,
            AgentProviderCapabilityKind.ChatCompletion,
            operation,
            model);
    }

    private sealed class FakeProviderFactory(ProviderDispatchLimits limits) : IAgentProviderFactory
    {
        public IReadOnlyList<ProviderCapabilityDescriptor> ListCapabilities(ProviderKind providerKind)
        {
            throw new NotSupportedException();
        }

        public bool Supports(ProviderKind providerKind, AgentProviderCapabilityKind capability)
        {
            throw new NotSupportedException();
        }

        public TDriver Resolve<TDriver>(ProviderKind providerKind)
            where TDriver : class, IAgentProviderDriver
        {
            throw new NotSupportedException();
        }

        public bool TryResolve<TDriver>(ProviderKind providerKind, out TDriver driver)
            where TDriver : class, IAgentProviderDriver
        {
            driver = null!;
            throw new NotSupportedException();
        }

        public ProviderDispatchLimits GetDispatchLimits(ProviderDispatchQuery query)
        {
            return limits;
        }
    }

    private sealed class ImmediateDispatchLaneGate : IProviderDispatchLaneGate
    {
        public ValueTask<IAsyncDisposable> EnterAsync(
            ProviderDispatchQuery query,
            string? subdriverKind = null,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult<IAsyncDisposable>(new Lease());
        }

        private sealed class Lease : IAsyncDisposable
        {
            public ValueTask DisposeAsync()
            {
                return ValueTask.CompletedTask;
            }
        }
    }
}
