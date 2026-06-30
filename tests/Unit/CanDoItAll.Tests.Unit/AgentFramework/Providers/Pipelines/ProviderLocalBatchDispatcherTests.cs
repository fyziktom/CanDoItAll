using System.Collections.Concurrent;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.ProviderPipelines;

namespace CanDoItAll.Tests.Unit.AgentFramework.Providers.Pipelines;

public sealed class ProviderLocalBatchDispatcherTests
{
    [Fact]
    public async Task ProviderPipeline_BatchesCompatibleRequestsBySameKey()
    {
        await using var hub = new ProviderLocalBatchDispatcherHub<int, int>();
        var key = CreateKey("same-model", "chat");
        var policy = CreatePolicy(key, maxBatchSize: 3);
        var batches = new ConcurrentBag<IReadOnlyList<ProviderBatchExecutionItem<int>>>();

        var tasks = Enumerable.Range(1, 3)
            .Select(value => hub.DispatchAsync(
                new ProviderBatchEnvelope<int>(key, value, Guid.NewGuid()),
                policy,
                (items, cancellationToken) =>
                {
                    batches.Add(items);
                    return Task.FromResult<IReadOnlyList<ProviderBatchItemResult<int>>>(
                        items.Select(item => ProviderBatchItemResult<int>.Succeeded(item.CorrelationId, item.Payload * 10)).ToList());
                }))
            .ToArray();

        var results = await Task.WhenAll(tasks);

        Assert.Equal(new[] { 10, 20, 30 }, results.Order().ToArray());
        var batch = Assert.Single(batches);
        Assert.Equal(3, batch.Count);
        Assert.Single(batch.Select(item => item.DispatchKey).Distinct());
    }

    [Fact]
    public async Task ProviderPipeline_SeparatesDifferentModelsAndSubdrivers()
    {
        await using var hub = new ProviderLocalBatchDispatcherHub<int, int>();
        var chatModelA = CreateKey("model-a", "chat");
        var chatModelB = CreateKey("model-b", "chat");
        var imageModelA = CreateKey("model-a", "image");
        var batches = new ConcurrentBag<IReadOnlyList<ProviderBatchExecutionItem<int>>>();

        Task<int> DispatchAsync(ProviderDispatchKey key, int payload)
        {
            return hub.DispatchAsync(
                new ProviderBatchEnvelope<int>(key, payload, Guid.NewGuid()),
                CreatePolicy(key, maxBatchSize: 4, maxQueueDelay: TimeSpan.FromMilliseconds(30)),
                (items, cancellationToken) =>
                {
                    batches.Add(items);
                    return Task.FromResult<IReadOnlyList<ProviderBatchItemResult<int>>>(
                        items.Select(item => ProviderBatchItemResult<int>.Succeeded(item.CorrelationId, item.Payload)).ToList());
                });
        }

        var results = await Task.WhenAll(
            DispatchAsync(chatModelA, 1),
            DispatchAsync(chatModelB, 2),
            DispatchAsync(chatModelA, 3),
            DispatchAsync(imageModelA, 4));

        Assert.Equal(new[] { 1, 2, 3, 4 }, results.Order().ToArray());
        Assert.Equal(3, batches.Count);
        Assert.All(batches, batch => Assert.Single(batch.Select(item => item.DispatchKey).Distinct()));
        Assert.Contains(batches, batch => batch.All(item => item.DispatchKey.Model == "model-a" && item.DispatchKey.SubdriverKind == "chat") && batch.Count == 2);
        Assert.Contains(batches, batch => batch.All(item => item.DispatchKey.Model == "model-b" && item.DispatchKey.SubdriverKind == "chat") && batch.Count == 1);
        Assert.Contains(batches, batch => batch.All(item => item.DispatchKey.Model == "model-a" && item.DispatchKey.SubdriverKind == "image") && batch.Count == 1);
    }

    [Fact]
    public async Task ProviderPipeline_DisabledBatchingBypassesDispatcherCreation()
    {
        await using var hub = new ProviderLocalBatchDispatcherHub<int, int>();
        var key = CreateKey("single-model", "chat");
        var policy = new ProviderBatchPolicy(key, ProviderDispatchLimits.Unbatched(TimeSpan.FromSeconds(5)));
        var batchSizes = new ConcurrentBag<int>();

        var first = await hub.DispatchAsync(
            new ProviderBatchEnvelope<int>(key, 1, Guid.NewGuid()),
            policy,
            (items, cancellationToken) =>
            {
                batchSizes.Add(items.Count);
                return Task.FromResult<IReadOnlyList<ProviderBatchItemResult<int>>>(
                    [ProviderBatchItemResult<int>.Succeeded(items[0].CorrelationId, items[0].Payload)]);
            });
        var second = await hub.DispatchAsync(
            new ProviderBatchEnvelope<int>(key, 2, Guid.NewGuid()),
            policy,
            (items, cancellationToken) =>
            {
                batchSizes.Add(items.Count);
                return Task.FromResult<IReadOnlyList<ProviderBatchItemResult<int>>>(
                    [ProviderBatchItemResult<int>.Succeeded(items[0].CorrelationId, items[0].Payload)]);
            });

        Assert.Equal(1, first);
        Assert.Equal(2, second);
        Assert.Equal(0, hub.DispatcherCount);
        Assert.Equal(new[] { 1, 1 }, batchSizes.Order().ToArray());
    }

    [Fact]
    public async Task ProviderPipeline_BackpressureFailsFastWhenQueueDepthIsExceeded()
    {
        await using var hub = new ProviderLocalBatchDispatcherHub<int, int>();
        var key = CreateKey("capacity-model", "chat");
        var policy = CreatePolicy(key, maxBatchSize: 2, maxQueueDepth: 2, maxQueueDelay: TimeSpan.FromSeconds(2));
        var releaseExecution = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var first = hub.DispatchAsync(
            new ProviderBatchEnvelope<int>(key, 1, Guid.NewGuid()),
            policy,
            async (items, cancellationToken) =>
            {
                await releaseExecution.Task.WaitAsync(cancellationToken);
                return items.Select(item => ProviderBatchItemResult<int>.Succeeded(item.CorrelationId, item.Payload)).ToList();
            });
        var second = hub.DispatchAsync(
            new ProviderBatchEnvelope<int>(key, 2, Guid.NewGuid()),
            policy,
            async (items, cancellationToken) =>
            {
                await releaseExecution.Task.WaitAsync(cancellationToken);
                return items.Select(item => ProviderBatchItemResult<int>.Succeeded(item.CorrelationId, item.Payload)).ToList();
            });

        var rejected = await Assert.ThrowsAsync<ProviderBatchQueueCapacityExceededException>(() => hub.DispatchAsync(
            new ProviderBatchEnvelope<int>(key, 3, Guid.NewGuid()),
            policy,
            async (items, cancellationToken) =>
            {
                await releaseExecution.Task.WaitAsync(cancellationToken);
                return items.Select(item => ProviderBatchItemResult<int>.Succeeded(item.CorrelationId, item.Payload)).ToList();
            }));

        Assert.Equal(2, rejected.MaxQueueDepth);
        releaseExecution.SetResult();
        Assert.Equal(new[] { 1, 2 }, (await Task.WhenAll(first, second)).Order().ToArray());
    }

    [Fact]
    public async Task ProviderPipeline_CancellationTimeoutAndPartialFailureArePerRequest()
    {
        var partialKey = CreateKey("partial-model", "chat");
        await using var partialHub = new ProviderLocalBatchDispatcherHub<int, int>();
        var successId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var failureId = Guid.Parse("22222222-2222-2222-2222-222222222222");

        var successTask = partialHub.DispatchAsync(
            new ProviderBatchEnvelope<int>(partialKey, 10, successId),
            CreatePolicy(partialKey, maxBatchSize: 2),
            PartialBatchAsync);
        var failureTask = partialHub.DispatchAsync(
            new ProviderBatchEnvelope<int>(partialKey, 20, failureId),
            CreatePolicy(partialKey, maxBatchSize: 2),
            PartialBatchAsync);

        Assert.Equal(100, await successTask);
        await Assert.ThrowsAsync<InvalidOperationException>(() => failureTask);

        var cancellationKey = CreateKey("cancel-model", "chat");
        await using var cancellationHub = new ProviderLocalBatchDispatcherHub<int, int>();
        using var canceledRequest = new CancellationTokenSource();
        var canceledTask = cancellationHub.DispatchAsync(
            new ProviderBatchEnvelope<int>(cancellationKey, 1, Guid.NewGuid()),
            CreatePolicy(cancellationKey, maxBatchSize: 2, maxQueueDelay: TimeSpan.FromMilliseconds(40)),
            SuccessfulBatchAsync,
            canceledRequest.Token);
        await canceledRequest.CancelAsync();
        var unrelatedResult = await cancellationHub.DispatchAsync(
            new ProviderBatchEnvelope<int>(cancellationKey, 42, Guid.NewGuid()),
            CreatePolicy(cancellationKey, maxBatchSize: 2, maxQueueDelay: TimeSpan.FromMilliseconds(40)),
            SuccessfulBatchAsync);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => canceledTask);
        Assert.Equal(42, unrelatedResult);

        var timeoutKey = CreateKey("timeout-model", "chat");
        await using var timeoutHub = new ProviderLocalBatchDispatcherHub<int, int>();
        var timeoutTask = timeoutHub.DispatchAsync(
            new ProviderBatchEnvelope<int>(timeoutKey, 5, Guid.NewGuid()),
            CreatePolicy(
                timeoutKey,
                maxBatchSize: 2,
                maxQueueDelay: TimeSpan.FromMilliseconds(5),
                requestTimeout: TimeSpan.FromMilliseconds(40)),
            async (items, cancellationToken) =>
            {
                await Task.Delay(200, CancellationToken.None);
                return items.Select(item => ProviderBatchItemResult<int>.Succeeded(item.CorrelationId, item.Payload)).ToList();
            });

        await Assert.ThrowsAsync<TimeoutException>(() => timeoutTask);

        static Task<IReadOnlyList<ProviderBatchItemResult<int>>> PartialBatchAsync(
            IReadOnlyList<ProviderBatchExecutionItem<int>> items,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<ProviderBatchItemResult<int>>>(
                items.Select(item => item.Payload == 10
                    ? ProviderBatchItemResult<int>.Succeeded(item.CorrelationId, 100)
                    : ProviderBatchItemResult<int>.Failed(item.CorrelationId, new InvalidOperationException("partial failure"))).ToList());
        }

        static Task<IReadOnlyList<ProviderBatchItemResult<int>>> SuccessfulBatchAsync(
            IReadOnlyList<ProviderBatchExecutionItem<int>> items,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<ProviderBatchItemResult<int>>>(
                items.Select(item => ProviderBatchItemResult<int>.Succeeded(item.CorrelationId, item.Payload)).ToList());
        }
    }

    [Fact]
    public void ProviderPipelineProject_HasCleanDependencyDirection()
    {
        var root = FindRepositoryRoot();
        var projectFile = File.ReadAllText(Path.Combine(root, "src/MAF/Common/CanDoItAll.AgentFramework.ProviderPipelines/CanDoItAll.AgentFramework.ProviderPipelines.csproj"));

        Assert.Contains("CanDoItAll.AgentFramework.Models", projectFile, StringComparison.Ordinal);
        Assert.DoesNotContain("CanDoItAll.AgentFramework.Maf", projectFile, StringComparison.Ordinal);
        Assert.DoesNotContain("CanDoItAll.Modules.Workspace", projectFile, StringComparison.Ordinal);
        Assert.DoesNotContain("EntityFramework", projectFile, StringComparison.Ordinal);

        var source = string.Join(
            Environment.NewLine,
            Directory
                .EnumerateFiles(Path.Combine(root, "src", "MAF", "Common", "CanDoItAll.AgentFramework.ProviderPipelines"), "*.cs")
                .Select(File.ReadAllText));
        foreach (var forbidden in new[] { "Maf", "Modules.Workspace", "Blazor", "EntityFramework", "OpenAI", "OllamaSharp", "Comfy" })
        {
            Assert.DoesNotContain(forbidden, source, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void ProviderPipelinePhase_DoesNotAdoptExistingConsumers()
    {
        var root = FindRepositoryRoot();
        var filesThatMustNotAdoptProviderPipelines = new[]
        {
            "src/MAF/Common/CanDoItAll.AgentFramework.Maf/CanDoItAll.AgentFramework.Maf.csproj",
            "src/MAF/Common/CanDoItAll.AgentFramework.Voice/CanDoItAll.AgentFramework.Voice.csproj",
            "src/Modules/CanDoItAll.Modules.AgentFramework/CanDoItAll.Modules.AgentFramework.csproj",
            "src/Modules/CanDoItAll.Modules.Workspace/CanDoItAll.Modules.Workspace.csproj"
        };

        foreach (var relativePath in filesThatMustNotAdoptProviderPipelines)
        {
            var text = File.ReadAllText(Path.Combine(root, relativePath));
            Assert.DoesNotContain("CanDoItAll.AgentFramework.ProviderPipelines", text, StringComparison.Ordinal);
        }
    }

    private static ProviderBatchPolicy CreatePolicy(
        ProviderDispatchKey key,
        int maxBatchSize = 4,
        int? maxQueueDepth = null,
        TimeSpan? maxQueueDelay = null,
        TimeSpan? requestTimeout = null)
    {
        return new ProviderBatchPolicy(
            key,
            ProviderDispatchLimits.Batched(
                maxBatchSize,
                maxInFlightBatches: 1,
                maxQueueDepth ?? 16,
                maxQueueDelay ?? TimeSpan.FromMilliseconds(80),
                requestTimeout ?? TimeSpan.FromSeconds(5)));
    }

    private static ProviderDispatchKey CreateKey(
        string model,
        string subdriverKind)
    {
        return new ProviderDispatchKey(
            Guid.Parse("aaaaaaaa-1111-2222-3333-bbbbbbbbbbbb"),
            ProviderKind.OpenAi,
            AgentProviderCapabilityKind.ChatCompletion,
            AgentProviderOperationKind.CompleteChat,
            subdriverKind,
            model);
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
}
