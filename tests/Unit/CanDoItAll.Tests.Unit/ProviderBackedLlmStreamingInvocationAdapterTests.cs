using System.Runtime.CompilerServices;
using CanDoItAll.AgentFramework.Llm.Abstractions;
using CanDoItAll.AgentFramework.Llm.ProviderRuntime;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Providers;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class ProviderBackedLlmStreamingInvocationAdapterTests
{
    [Fact]
    public async Task StreamAsync_retries_before_the_first_delta_and_exposes_monotonic_attempt_outcomes()
    {
        var driver = new RecordingStreamingDriver((request, attempt, cancellationToken) =>
            attempt == 1
                ? FailBeforeDelta(cancellationToken)
                : CompleteIncrementally(request.Model, "recovered", cancellationToken));
        var adapter = CreateAdapter(driver);

        var updates = await CollectAsync(adapter.StreamAsync(CreateRequest()));

        Assert.Equal(2, driver.StreamCallCount);
        Assert.Collection(
            updates,
            update => Assert.Equal(1, Assert.IsType<LlmStreamingAttemptStarted>(update).AttemptOrdinal),
            update =>
            {
                var failed = Assert.IsType<LlmStreamingFailed>(update);
                Assert.Equal(1, failed.AttemptOrdinal);
                Assert.True(failed.RetryScheduled);
                Assert.Equal(LlmInvocationFailureKind.ProviderFailure, failed.FailureKind);
            },
            update => Assert.Equal(2, Assert.IsType<LlmStreamingAttemptStarted>(update).AttemptOrdinal),
            update =>
            {
                var delta = Assert.IsType<LlmStreamingTextDelta>(update);
                Assert.Equal(2, delta.AttemptOrdinal);
                Assert.Equal("recovered", delta.Delta);
            },
            update =>
            {
                var completed = Assert.IsType<LlmStreamingCompleted>(update);
                Assert.Equal(2, completed.AttemptOrdinal);
                Assert.Equal(new LlmUsage(4, 2, 1), completed.Usage);
            });
    }

    [Fact]
    public async Task StreamAsync_never_retries_after_a_delta_is_visible()
    {
        var driver = new RecordingStreamingDriver((request, attempt, cancellationToken) =>
            FailAfterDelta(cancellationToken));
        var adapter = CreateAdapter(driver);

        var updates = await CollectAsync(adapter.StreamAsync(CreateRequest()));

        Assert.Equal(1, driver.StreamCallCount);
        Assert.Collection(
            updates,
            update => Assert.IsType<LlmStreamingAttemptStarted>(update),
            update => Assert.Equal("partial", Assert.IsType<LlmStreamingTextDelta>(update).Delta),
            update =>
            {
                var failed = Assert.IsType<LlmStreamingFailed>(update);
                Assert.False(failed.RetryScheduled);
                Assert.Equal(1, failed.AttemptOrdinal);
            });
    }

    [Fact]
    public async Task StreamAsync_uses_one_delta_completed_fallback_for_a_completed_only_driver()
    {
        var driver = new CompletedOnlyDriver();
        var adapter = CreateAdapter(driver);

        var updates = await CollectAsync(adapter.StreamAsync(CreateRequest()));

        Assert.Equal(1, driver.CallCount);
        Assert.Collection(
            updates,
            update => Assert.Equal(
                LlmStreamingDeliveryMode.CompletedFallback,
                Assert.IsType<LlmStreamingAttemptStarted>(update).DeliveryMode),
            update => Assert.Equal("fallback", Assert.IsType<LlmStreamingTextDelta>(update).Delta),
            update =>
            {
                var completed = Assert.IsType<LlmStreamingCompleted>(update);
                Assert.Equal(LlmStreamingDeliveryMode.CompletedFallback, completed.DeliveryMode);
                Assert.Equal(new LlmUsage(7, 3, 2), completed.Usage);
            });
    }

    [Fact]
    public async Task StreamAsync_returns_a_sanitized_terminal_failure_without_raw_provider_details()
    {
        const string Secret = "raw-provider-secret";
        var driver = new RecordingStreamingDriver((request, attempt, cancellationToken) =>
            FailWith(Secret, cancellationToken));
        var adapter = CreateAdapter(driver);

        var updates = await CollectAsync(adapter.StreamAsync(CreateRequest()));

        Assert.Equal(2, driver.StreamCallCount);
        var failures = updates.OfType<LlmStreamingFailed>().ToArray();
        Assert.Equal(2, failures.Length);
        Assert.True(failures[0].RetryScheduled);
        Assert.False(failures[1].RetryScheduled);
        Assert.DoesNotContain(Secret, string.Join('|', updates.Select(update => update.ToString())), StringComparison.Ordinal);
    }

    [Fact]
    public async Task StreamAsync_retries_an_empty_completion_once_and_preserves_usage()
    {
        var driver = new RecordingStreamingDriver((request, attempt, cancellationToken) =>
            CompleteWithoutText(request.Model, cancellationToken));
        var adapter = CreateAdapter(driver);

        var updates = await CollectAsync(adapter.StreamAsync(CreateRequest()));

        Assert.Equal(2, driver.StreamCallCount);
        var failures = updates.OfType<LlmStreamingFailed>().ToArray();
        Assert.Collection(
            failures,
            failure =>
            {
                Assert.Equal(LlmInvocationFailureKind.EmptyResponse, failure.FailureKind);
                Assert.True(failure.RetryScheduled);
                Assert.Equal(new LlmUsage(2, 1), failure.Usage);
            },
            failure =>
            {
                Assert.Equal(LlmInvocationFailureKind.EmptyResponse, failure.FailureKind);
                Assert.False(failure.RetryScheduled);
                Assert.Equal(new LlmUsage(4, 2), failure.Usage);
            });
    }

    [Fact]
    public async Task StreamAsync_reports_deadline_without_retrying()
    {
        var driver = new RecordingStreamingDriver((request, attempt, cancellationToken) =>
            BlockUntilCancelled(cancellationToken));
        var adapter = CreateAdapter(driver);

        var updates = await CollectAsync(adapter.StreamAsync(CreateRequest(TimeSpan.FromMilliseconds(50))));

        Assert.Equal(1, driver.StreamCallCount);
        var failed = Assert.IsType<LlmStreamingFailed>(updates.Last());
        Assert.Equal(LlmInvocationFailureKind.DeadlineExceeded, failed.FailureKind);
        Assert.False(failed.RetryScheduled);
    }

    [Fact]
    public async Task StreamAsync_keeps_caller_cancellation_cooperative()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var driver = new RecordingStreamingDriver((request, attempt, cancellationToken) =>
            BlockUntilCancelled(cancellationToken, started));
        var adapter = CreateAdapter(driver);
        using var cancellation = new CancellationTokenSource();
        var collect = CollectAsync(adapter.StreamAsync(CreateRequest(), cancellation.Token));
        await started.Task.WaitAsync(TimeSpan.FromSeconds(2));

        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => collect);
        Assert.Equal(1, driver.StreamCallCount);
    }

    private static async IAsyncEnumerable<ProviderChatStreamingUpdate> FailBeforeDelta(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await Task.Yield();
        cancellationToken.ThrowIfCancellationRequested();
        throw new InvalidOperationException("first attempt failed");
#pragma warning disable CS0162
        yield break;
#pragma warning restore CS0162
    }

    private static async IAsyncEnumerable<ProviderChatStreamingUpdate> FailAfterDelta(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await Task.Yield();
        cancellationToken.ThrowIfCancellationRequested();
        yield return new ProviderChatTextDelta("partial");
        throw new InvalidOperationException("must not trigger a retry");
    }

    private static async IAsyncEnumerable<ProviderChatStreamingUpdate> FailWith(
        string message,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await Task.Yield();
        cancellationToken.ThrowIfCancellationRequested();
        throw new InvalidOperationException(message);
#pragma warning disable CS0162
        yield break;
#pragma warning restore CS0162
    }

    private static async IAsyncEnumerable<ProviderChatStreamingUpdate> CompleteIncrementally(
        string model,
        string text,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await Task.Yield();
        cancellationToken.ThrowIfCancellationRequested();
        yield return new ProviderChatTextDelta(text);
        yield return new ProviderChatCompleted(model, 4, 2, "stop") { CachedInputTokens = 1 };
    }

    private static async IAsyncEnumerable<ProviderChatStreamingUpdate> CompleteWithoutText(
        string model,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await Task.Yield();
        cancellationToken.ThrowIfCancellationRequested();
        yield return new ProviderChatCompleted(model, 2, 1, "stop");
    }

    private static async IAsyncEnumerable<ProviderChatStreamingUpdate> BlockUntilCancelled(
        [EnumeratorCancellation] CancellationToken cancellationToken,
        TaskCompletionSource? started = null)
    {
        started?.TrySetResult();
        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        yield return new ProviderChatTextDelta("unreachable");
    }

    private static ProviderBackedLlmStreamingInvocationAdapter CreateAdapter(IAgentProviderDriver driver)
    {
        var factory = new AgentProviderDriverRegistryBuilder().AddDriver(driver).Build();
        var store = new ProviderProfileRuntimeDescriptorStore();
        var pool = new ProviderRuntimePool(store, new ProviderRuntimeHandleFactory(factory));
        return new ProviderBackedLlmStreamingInvocationAdapter(
            store,
            pool,
            TimeProvider.System,
            NullLogger<ProviderBackedLlmStreamingInvocationAdapter>.Instance);
    }

    private static LlmInvocationRequest CreateRequest(TimeSpan? timeout = null)
    {
        var provider = new ProviderProfile(
            Guid.NewGuid(),
            "Streaming test provider",
            ProviderKind.OpenAi,
            "https://example.invalid/v1",
            "STREAMING_TEST_API_KEY",
            "gpt-streaming",
            ProviderTransportKind.ChatCompletions,
            IsEnabled: true,
            SupportsStreaming: true,
            SupportsTools: false,
            PreferFrameworkManagedChatHistory: true,
            SupportsBackgroundResponses: false,
            ConfigurationJson: "{}",
            Notes: string.Empty,
            HealthStatus: "Not checked",
            LastCheckedAtUtc: null,
            SuggestedModels: ["gpt-streaming"]);
        return new LlmInvocationRequest(
            provider,
            provider.DefaultModel,
            [new LlmMessage(LlmMessageRole.User, "hello")],
            timeout: timeout);
    }

    private static async Task<List<T>> CollectAsync<T>(IAsyncEnumerable<T> source)
    {
        var values = new List<T>();
        await foreach (var value in source)
        {
            values.Add(value);
        }

        return values;
    }

    private sealed class RecordingStreamingDriver(
        Func<ProviderChatCompletionRequest, int, CancellationToken, IAsyncEnumerable<ProviderChatStreamingUpdate>> stream)
        : IProviderChatCompletionDriver, IProviderStreamingChatCompletionDriver
    {
        public int StreamCallCount { get; private set; }

        public ProviderKind ProviderKind => ProviderKind.OpenAi;

        public IReadOnlySet<AgentProviderCapabilityKind> Capabilities { get; } =
            new HashSet<AgentProviderCapabilityKind> { AgentProviderCapabilityKind.ChatCompletion };

        public ProviderDispatchLimits GetDispatchLimits(ProviderDispatchQuery query)
            => ProviderDispatchLimits.Unbatched(TimeSpan.FromSeconds(5));

        public ProviderChatStreamingMode ResolveStreamingMode(ProviderChatCompletionRequest request)
            => ProviderChatStreamingMode.Incremental;

        public IAsyncEnumerable<ProviderChatStreamingUpdate> StreamChatAsync(
            ProviderChatCompletionRequest request,
            CancellationToken cancellationToken = default)
        {
            StreamCallCount++;
            return stream(request, StreamCallCount, cancellationToken);
        }

        public Task<ProviderChatCompletionResult> CompleteChatAsync(
            ProviderChatCompletionRequest request,
            CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Incremental tests must not use completed fallback.");
    }

    private sealed class CompletedOnlyDriver : IProviderChatCompletionDriver
    {
        public int CallCount { get; private set; }

        public ProviderKind ProviderKind => ProviderKind.OpenAi;

        public IReadOnlySet<AgentProviderCapabilityKind> Capabilities { get; } =
            new HashSet<AgentProviderCapabilityKind> { AgentProviderCapabilityKind.ChatCompletion };

        public ProviderDispatchLimits GetDispatchLimits(ProviderDispatchQuery query)
            => ProviderDispatchLimits.Unbatched(TimeSpan.FromSeconds(5));

        public Task<ProviderChatCompletionResult> CompleteChatAsync(
            ProviderChatCompletionRequest request,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(new ProviderChatCompletionResult(request.Model, "fallback", 7, 3)
            {
                CachedInputTokens = 2
            });
        }
    }
}
