using System.Collections.Concurrent;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Providers;

namespace CanDoItAll.Tests.Unit.AgentFramework.Providers;

public sealed class ProviderBatchJobBalancerTests
{
    [Fact]
    public async Task ProviderBatchBalancer_PartitionsItemsAcrossEligibleProviderProfilesByDispatchLimits()
    {
        var fastProvider = CreateProvider("Alpha", Guid.Parse("11111111-1111-1111-1111-111111111111"));
        var slowProvider = CreateProvider("Beta", Guid.Parse("22222222-2222-2222-2222-222222222222"));
        await using var pool = CreateRuntimePool(
            [fastProvider, slowProvider],
            new FakeChatDriver(new Dictionary<Guid, ProviderDispatchLimits>
            {
                [fastProvider.Id] = ProviderDispatchLimits.Batched(
                    maxBatchSize: 4,
                    maxInFlightBatches: 1,
                    maxQueueDepth: 16,
                    maxQueueDelay: TimeSpan.FromMilliseconds(10),
                    requestTimeout: TimeSpan.FromSeconds(5)),
                [slowProvider.Id] = ProviderDispatchLimits.Unbatched(TimeSpan.FromSeconds(5))
            }));
        var balancer = new ProviderBatchJobBalancer(pool);

        var plan = await balancer.CreatePlanAsync(CreateRequest(
            inputs: CreateInputs(6),
            providers:
            [
                new ProviderBatchProviderSelection(fastProvider),
                new ProviderBatchProviderSelection(slowProvider)
            ],
            policy: new ProviderBatchExecutionPolicy(MaxTotalParallelism: 5, MaxPerProviderParallelism: 4)));

        Assert.Equal(2, plan.Lanes.Count);
        Assert.Equal(6, plan.Assignments.Count);
        var fastAssignments = plan.Assignments.Count(assignment => assignment.ProviderProfileId == fastProvider.Id);
        var slowAssignments = plan.Assignments.Count(assignment => assignment.ProviderProfileId == slowProvider.Id);
        Assert.True(fastAssignments > slowAssignments);
        Assert.True(slowAssignments > 0);
        Assert.Equal(4, plan.Lanes.Single(lane => lane.ProviderProfileId == fastProvider.Id).PlannedParallelism);
        Assert.Equal(1, plan.Lanes.Single(lane => lane.ProviderProfileId == slowProvider.Id).PlannedParallelism);
    }

    [Fact]
    public async Task ProviderBatchBalancer_ExecutesThroughRuntimePoolAndPreservesCorrelationUsageAndOrder()
    {
        var provider = CreateProvider("Runtime provider", Guid.Parse("33333333-3333-3333-3333-333333333333"));
        await using var pool = CreateRuntimePool(
            [provider],
            new FakeChatDriver(new Dictionary<Guid, ProviderDispatchLimits>
            {
                [provider.Id] = ProviderDispatchLimits.Unbatched(TimeSpan.FromSeconds(5))
            }));
        var balancer = new ProviderBatchJobBalancer(pool);
        var correlations = new ConcurrentBag<(Guid InputId, Guid RuntimeCorrelationId)>();

        var result = await balancer.ExecuteAsync<int, int>(
            CreateRequest(
                inputs: CreateInputs(4),
                providers: [new ProviderBatchProviderSelection(provider)],
                policy: new ProviderBatchExecutionPolicy(MaxTotalParallelism: 2, MaxPerProviderParallelism: 2)),
            (context, cancellationToken) =>
            {
                correlations.Add((context.Input.InputId, context.RuntimeContext.CorrelationId));
                var usage = CreateUsageObservation(
                    provider,
                    context.Assignment.Model,
                    context.RuntimeContext.CorrelationId);
                return Task.FromResult(ProviderBatchDispatchOutcome<int>.FromValue(
                    context.Input.Payload * 10,
                    usage,
                    $"artifact://{context.Input.InputId:N}"));
            });

        Assert.True(result.Succeeded);
        Assert.Equal([10, 20, 30, 40], result.Items.Select(item => item.Value).ToArray());
        Assert.All(result.Items, item =>
        {
            Assert.Equal(ProviderBatchItemStatus.Succeeded, item.Status);
            Assert.Equal(provider.Id, item.ProviderProfileId);
            Assert.NotNull(item.UsageObservation);
            Assert.StartsWith("artifact://", item.ResultReference, StringComparison.Ordinal);
        });
        Assert.All(correlations, pair => Assert.Equal(pair.InputId, pair.RuntimeCorrelationId));
    }

    [Fact]
    public async Task ProviderBatchBalancer_CheckpointedRecoverySkipsCompletedItemsAndRetriesFailures()
    {
        var provider = CreateProvider("Checkpoint provider", Guid.Parse("44444444-4444-4444-4444-444444444444"));
        var inputs = CreateInputs(3);
        var store = new InMemoryProviderBatchJobCheckpointStore();
        var jobId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        await store.UpsertItemCheckpointAsync(new ProviderBatchItemCheckpoint(
            jobId,
            inputs[0].InputId,
            inputs[0].Sequence,
            ProviderBatchItemStatus.Succeeded,
            provider.Id,
            provider.Kind,
            provider.Name,
            provider.DefaultModel,
            AttemptCount: 1,
            ResultReference: "artifact://existing",
            ErrorCode: string.Empty,
            ErrorMessage: string.Empty,
            DateTimeOffset.UtcNow));
        await using var pool = CreateRuntimePool(
            [provider],
            new FakeChatDriver(new Dictionary<Guid, ProviderDispatchLimits>
            {
                [provider.Id] = ProviderDispatchLimits.Unbatched(TimeSpan.FromSeconds(5))
            }));
        var balancer = new ProviderBatchJobBalancer(pool, store);
        var attemptCounts = new ConcurrentDictionary<Guid, int>();

        var result = await balancer.ExecuteAsync<int, int>(
            CreateRequest(
                jobId,
                inputs,
                [new ProviderBatchProviderSelection(provider)],
                new ProviderBatchExecutionPolicy(
                    MaxTotalParallelism: 1,
                    MaxPerProviderParallelism: 1,
                    MaxAttempts: 2,
                    PersistenceMode: ProviderBatchPersistenceMode.Checkpointed)),
            (context, cancellationToken) =>
            {
                var attempt = attemptCounts.AddOrUpdate(context.Input.InputId, 1, (_, count) => count + 1);
                if (context.Input.InputId == inputs[1].InputId && attempt == 1)
                {
                    throw new InvalidOperationException("transient item failure");
                }

                return Task.FromResult(ProviderBatchDispatchOutcome<int>.FromValue(context.Input.Payload * 100));
            });

        Assert.Equal(ProviderBatchItemStatus.Recovered, result.Items[0].Status);
        Assert.Equal("artifact://existing", result.Items[0].ResultReference);
        Assert.False(attemptCounts.ContainsKey(inputs[0].InputId));
        Assert.Equal(2, result.Items.Single(item => item.InputId == inputs[1].InputId).AttemptCount);
        Assert.Equal(ProviderBatchItemStatus.Succeeded, result.Items.Single(item => item.InputId == inputs[2].InputId).Status);
        var checkpoints = await store.GetItemCheckpointsAsync(jobId);
        Assert.Equal(3, checkpoints.Count(checkpoint => checkpoint.Status == ProviderBatchItemStatus.Succeeded));
    }

    [Fact]
    public async Task ProviderBatchBalancer_RejectsDisabledUnhealthyModelMismatchAndUnsupportedProviders()
    {
        var disabled = CreateProvider(
            "Disabled",
            Guid.Parse("55555555-5555-5555-5555-555555555555"),
            isEnabled: false);
        var wrongModel = CreateProvider(
            "Wrong model",
            Guid.Parse("66666666-6666-6666-6666-666666666666"),
            suggestedModels: ["other-model"]);
        var unhealthy = CreateProvider(
            "Unhealthy",
            Guid.Parse("77777777-7777-7777-7777-777777777777"),
            healthStatus: "Down");
        var unsupported = CreateProvider(
            "Image only",
            Guid.Parse("88888888-8888-8888-8888-888888888888"),
            kind: ProviderKind.ComfyUi);
        await using var pool = CreateRuntimePool(
            [unsupported],
            new FakeChatDriver(new Dictionary<Guid, ProviderDispatchLimits>()));
        var balancer = new ProviderBatchJobBalancer(pool);

        var exception = await Assert.ThrowsAsync<ProviderBatchPlanningException>(() => balancer.CreatePlanAsync(CreateRequest(
            inputs: CreateInputs(1),
            providers:
            [
                new ProviderBatchProviderSelection(disabled),
                new ProviderBatchProviderSelection(wrongModel),
                new ProviderBatchProviderSelection(unhealthy, RequireHealthy: true),
                new ProviderBatchProviderSelection(unsupported)
            ],
            policy: new ProviderBatchExecutionPolicy())));

        Assert.Contains(exception.Rejections, rejection => rejection.ReasonCode == ProviderBatchRejectionCodes.ProviderDisabled);
        Assert.Contains(exception.Rejections, rejection => rejection.ReasonCode == ProviderBatchRejectionCodes.ModelMismatch);
        Assert.Contains(exception.Rejections, rejection => rejection.ReasonCode == ProviderBatchRejectionCodes.ProviderUnhealthy);
        Assert.Contains(exception.Rejections, rejection => rejection.ReasonCode == ProviderBatchRejectionCodes.CapabilityUnsupported);
    }

    [Fact]
    public async Task ProviderBatchBalancer_FailFastCancelsPendingDispatchAndRecordsOutcomes()
    {
        var provider = CreateProvider("Fail fast provider", Guid.Parse("99999999-9999-9999-9999-999999999999"));
        await using var pool = CreateRuntimePool(
            [provider],
            new FakeChatDriver(new Dictionary<Guid, ProviderDispatchLimits>
            {
                [provider.Id] = ProviderDispatchLimits.Unbatched(TimeSpan.FromSeconds(5))
            }));
        var balancer = new ProviderBatchJobBalancer(pool);
        var dispatchCount = 0;

        var result = await balancer.ExecuteAsync<int, int>(
            CreateRequest(
                inputs: CreateInputs(3),
                providers: [new ProviderBatchProviderSelection(provider)],
                policy: new ProviderBatchExecutionPolicy(
                    MaxTotalParallelism: 1,
                    MaxPerProviderParallelism: 1,
                    FailurePolicy: ProviderBatchFailurePolicy.FailFast)),
            (context, cancellationToken) =>
            {
                Interlocked.Increment(ref dispatchCount);
                throw new InvalidOperationException("fatal provider failure");
            });

        Assert.False(result.Succeeded);
        Assert.Equal(1, dispatchCount);
        Assert.Single(result.Items, item => item.Status == ProviderBatchItemStatus.Failed);
        Assert.Equal(2, result.Items.Count(item => item.Status == ProviderBatchItemStatus.Cancelled));
    }

    private static ProviderBatchJobRequest<int> CreateRequest(
        IReadOnlyList<ProviderBatchInput<int>> inputs,
        IReadOnlyList<ProviderBatchProviderSelection> providers,
        ProviderBatchExecutionPolicy policy)
    {
        return CreateRequest(
            Guid.NewGuid(),
            inputs,
            providers,
            policy);
    }

    private static ProviderBatchJobRequest<int> CreateRequest(
        Guid jobId,
        IReadOnlyList<ProviderBatchInput<int>> inputs,
        IReadOnlyList<ProviderBatchProviderSelection> providers,
        ProviderBatchExecutionPolicy policy)
    {
        return new ProviderBatchJobRequest<int>(
            jobId,
            inputs,
            providers,
            AgentProviderCapabilityKind.ChatCompletion,
            AgentProviderOperationKind.CompleteChat,
            Model: "batch-model",
            Policy: policy);
    }

    private static IReadOnlyList<ProviderBatchInput<int>> CreateInputs(int count)
    {
        return Enumerable.Range(1, count)
            .Select(index => new ProviderBatchInput<int>(
                Guid.NewGuid(),
                index,
                $"source://page/{index}",
                index))
            .ToList();
    }

    private static ProviderProfile CreateProvider(
        string name,
        Guid id,
        ProviderKind kind = ProviderKind.Ollama,
        bool isEnabled = true,
        IReadOnlyList<string>? suggestedModels = null,
        string healthStatus = "Healthy")
    {
        return new ProviderProfile(
            id,
            name,
            kind,
            "http://localhost",
            string.Empty,
            "batch-model",
            ProviderTransportKind.ChatCompletions,
            isEnabled,
            SupportsStreaming: false,
            SupportsTools: false,
            PreferFrameworkManagedChatHistory: true,
            SupportsBackgroundResponses: false,
            ConfigurationJson: "{}",
            Notes: string.Empty,
            healthStatus,
            LastCheckedAtUtc: null,
            suggestedModels ?? ["batch-model"]);
    }

    private static ProviderRuntimePool CreateRuntimePool(
        IReadOnlyList<ProviderProfile> providers,
        IAgentProviderDriver driver)
    {
        var descriptors = providers.ToDictionary(
            provider => provider.Id,
            provider => ProviderRuntimeDescriptor.FromProfile(provider));
        var descriptorSource = new DictionaryDescriptorSource(descriptors);
        var factory = new AgentProviderDriverRegistryBuilder()
            .AddDriver(driver)
            .Build();

        return new ProviderRuntimePool(
            descriptorSource,
            new ProviderRuntimeHandleFactory(factory));
    }

    private static ProviderUsageObservation CreateUsageObservation(
        ProviderProfile provider,
        string model,
        Guid correlationId)
    {
        return new ProviderUsageObservation(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            provider.Name,
            provider.Kind,
            model,
            provider.Transport,
            ProviderUsageSourcePhases.AgentRuntime,
            ProviderUsageObservationStatus.Observed,
            InputTokens: 1,
            CachedInputTokens: 0,
            OutputTokens: 1,
            ReasoningTokens: 0,
            TotalTokens: 2,
            ToolCallCount: 0)
        {
            CorrelationId = correlationId.ToString("D")
        };
    }

    private sealed class DictionaryDescriptorSource(
        IReadOnlyDictionary<Guid, ProviderRuntimeDescriptor> descriptors) : IProviderRuntimeDescriptorSource
    {
        public Task<ProviderRuntimeDescriptor> GetRequiredAsync(
            Guid providerProfileId,
            CancellationToken cancellationToken = default)
        {
            if (descriptors.TryGetValue(providerProfileId, out var descriptor))
            {
                return Task.FromResult(descriptor);
            }

            throw new InvalidOperationException($"Missing descriptor for provider '{providerProfileId:D}'.");
        }
    }

    private sealed class FakeChatDriver(
        IReadOnlyDictionary<Guid, ProviderDispatchLimits> limitsByProviderId) : IProviderChatCompletionDriver
    {
        public ProviderKind ProviderKind => ProviderKind.Ollama;

        public IReadOnlySet<AgentProviderCapabilityKind> Capabilities { get; } =
            new HashSet<AgentProviderCapabilityKind> { AgentProviderCapabilityKind.ChatCompletion };

        public ProviderDispatchLimits GetDispatchLimits(ProviderDispatchQuery query)
        {
            return limitsByProviderId.TryGetValue(query.Provider.Id, out var limits)
                ? limits
                : ProviderDispatchLimits.Unbatched(TimeSpan.FromSeconds(5));
        }

        public Task<ProviderChatCompletionResult> CompleteChatAsync(
            ProviderChatCompletionRequest request,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Provider batch balancer tests dispatch through the runtime delegate.");
        }
    }
}
