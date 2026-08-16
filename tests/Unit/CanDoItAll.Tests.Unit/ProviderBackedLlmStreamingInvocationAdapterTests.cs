using System.Runtime.CompilerServices;
using CanDoItAll.AgentFramework.Llm.Abstractions;
using CanDoItAll.AgentFramework.Llm.ProviderRuntime;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Providers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class ProviderBackedLlmStreamingInvocationAdapterTests
{
    private const string PreparationLogTemplate =
        "LLM streaming runtime preparation failed. ProviderId={ProviderId} ProviderKind={ProviderKind} Model={Model} CorrelationId={CorrelationId}";
    private const string AttemptLogTemplate =
        "LLM streaming provider attempt failed. ProviderId={ProviderId} ProviderKind={ProviderKind} Model={Model} CorrelationId={CorrelationId} AttemptOrdinal={AttemptOrdinal} FailureKind={FailureKind} PartialOutputVisible={PartialOutputVisible}";
    private static readonly string[] SensitiveValues =
    [
        "sensitive-provider-body-1a8f",
        "sensitive-inner-exception-2b9e",
        "https://sensitive-endpoint-3c0d.invalid/v1",
        "SENSITIVE_CREDENTIAL_4D1C",
        "sensitive-local-path-5e2b",
        "sensitive-system-instruction-6f3a",
        "sensitive-user-prompt-7a49"
    ];

    [Fact]
    public async Task Preparation_failure_logs_only_allowlisted_context()
    {
        var logger = new CapturingLogger<ProviderBackedLlmStreamingInvocationAdapter>();
        var request = CreateSensitiveRequest();
        var adapter = CreatePreparationFailureAdapter(CreateSensitiveFailure(), logger);

        var exception = await Assert.ThrowsAsync<LlmInvocationException>(() => CollectAsync(adapter.StreamAsync(request)));

        Assert.Equal(LlmInvocationFailureKind.ProviderFailure, exception.Kind);
        Assert.Null(exception.InnerException);
        var entry = Assert.Single(logger.Entries);
        AssertPreparationLog(entry, request);
        AssertSensitiveValuesAbsent(exception.ToString(), Serialize(entry));
    }

    [Fact]
    public async Task Preparation_deadline_exposes_only_sanitized_public_exception()
    {
        var logger = new CapturingLogger<ProviderBackedLlmStreamingInvocationAdapter>();
        var runtimePool = new DeadlineRuntimePool();
        var adapter = new ProviderBackedLlmStreamingInvocationAdapter(
            new ProviderProfileRuntimeDescriptorStore(),
            runtimePool,
            TimeProvider.System,
            logger);
        var request = CreateSensitiveRequest(TimeSpan.FromMilliseconds(50));

        var exception = await Assert.ThrowsAsync<LlmInvocationException>(() => CollectAsync(adapter.StreamAsync(request)));

        Assert.Equal(LlmInvocationFailureKind.DeadlineExceeded, exception.Kind);
        Assert.Null(exception.InnerException);
        Assert.Equal(1, runtimePool.GetRequiredCallCount);
        Assert.Empty(logger.Entries);
        AssertSensitiveValuesAbsent(exception.ToString());
    }

    [Fact]
    public async Task Provider_attempt_failure_logs_only_allowlisted_context()
    {
        var logger = new CapturingLogger<ProviderBackedLlmStreamingInvocationAdapter>();
        var failure = CreateSensitiveFailure();
        var driver = new RecordingStreamingDriver((request, attempt, cancellationToken) =>
            FailWithExceptionAfterDelta(failure, cancellationToken));
        var adapter = CreateAdapter(driver, logger);
        var request = CreateRequest(correlationId: "safe-attempt-correlation");

        var updates = await CollectAsync(adapter.StreamAsync(request));

        var entry = Assert.Single(logger.Entries);
        AssertAttemptLog(entry, request, partialOutputVisible: true);
        Assert.Equal(1, driver.StreamCallCount);
        Assert.IsType<LlmStreamingFailed>(updates[^1]);
        AssertSensitiveValuesAbsent(Serialize(entry), string.Join('|', updates));
    }

    [Fact]
    public async Task Raw_provider_body_exception_endpoint_credential_path_and_prompts_never_enter_logs()
    {
        var request = CreateSensitiveRequest();
        var failure = CreateSensitiveFailure();
        var attemptLogger = new CapturingLogger<ProviderBackedLlmStreamingInvocationAdapter>();
        var driver = new RecordingStreamingDriver((providerRequest, attempt, cancellationToken) =>
            FailWithExceptionAfterDelta(failure, cancellationToken));
        var attemptAdapter = CreateAdapter(driver, attemptLogger);
        var updates = await CollectAsync(attemptAdapter.StreamAsync(request));
        var preparationLogger = new CapturingLogger<ProviderBackedLlmStreamingInvocationAdapter>();
        var preparationAdapter = CreatePreparationFailureAdapter(failure, preparationLogger);
        var publicException = await Assert.ThrowsAsync<LlmInvocationException>(
            () => CollectAsync(preparationAdapter.StreamAsync(request)));

        Assert.Null(publicException.InnerException);
        var attemptEntry = Assert.Single(attemptLogger.Entries);
        var preparationEntry = Assert.Single(preparationLogger.Entries);
        AssertAttemptLog(attemptEntry, request, partialOutputVisible: true);
        AssertPreparationLog(preparationEntry, request);
        AssertSensitiveValuesAbsent(
            Serialize(attemptEntry),
            Serialize(preparationEntry),
            publicException.ToString(),
            string.Join('|', updates));
    }

    [Fact]
    public async Task Redaction_preserves_retry_before_first_delta()
    {
        var logger = new CapturingLogger<ProviderBackedLlmStreamingInvocationAdapter>();
        var driver = new RecordingStreamingDriver((request, attempt, cancellationToken) =>
            attempt == 1
                ? FailBeforeDelta(cancellationToken)
                : CompleteIncrementally(request.Model, "recovered", cancellationToken));
        var adapter = CreateAdapter(driver, logger);
        var request = CreateRequest(correlationId: "safe-retry-correlation");

        var updates = await CollectAsync(adapter.StreamAsync(request));

        Assert.Equal(2, driver.StreamCallCount);
        AssertAttemptLog(Assert.Single(logger.Entries), request, partialOutputVisible: false);
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
    public async Task Redaction_preserves_cancellation_semantics()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var logger = new CapturingLogger<ProviderBackedLlmStreamingInvocationAdapter>();
        var driver = new RecordingStreamingDriver((request, attempt, cancellationToken) =>
            BlockUntilCancelled(cancellationToken, started));
        var adapter = CreateAdapter(driver, logger);
        using var cancellation = new CancellationTokenSource();
        var collect = CollectAsync(adapter.StreamAsync(CreateRequest(), cancellation.Token));
        await started.Task.WaitAsync(TimeSpan.FromSeconds(2));

        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => collect);
        Assert.Equal(1, driver.StreamCallCount);
        Assert.Empty(logger.Entries);
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

    private static async IAsyncEnumerable<ProviderChatStreamingUpdate> FailWithExceptionAfterDelta(
        Exception failure,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await Task.Yield();
        cancellationToken.ThrowIfCancellationRequested();
        yield return new ProviderChatTextDelta("safe-partial-output");
        throw failure;
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

    private static ProviderBackedLlmStreamingInvocationAdapter CreateAdapter(
        IAgentProviderDriver driver,
        ILogger<ProviderBackedLlmStreamingInvocationAdapter>? logger = null)
    {
        var factory = new AgentProviderDriverRegistryBuilder().AddDriver(driver).Build();
        var store = new ProviderProfileRuntimeDescriptorStore();
        var pool = new ProviderRuntimePool(store, new ProviderRuntimeHandleFactory(factory));
        return new ProviderBackedLlmStreamingInvocationAdapter(
            store,
            pool,
            TimeProvider.System,
            logger ?? NullLogger<ProviderBackedLlmStreamingInvocationAdapter>.Instance);
    }

    private static ProviderBackedLlmStreamingInvocationAdapter CreatePreparationFailureAdapter(
        Exception failure,
        ILogger<ProviderBackedLlmStreamingInvocationAdapter> logger)
        => new(
            new ProviderProfileRuntimeDescriptorStore(),
            new ThrowingRuntimePool(failure),
            TimeProvider.System,
            logger);

    private static LlmInvocationRequest CreateRequest(
        TimeSpan? timeout = null,
        string correlationId = "")
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
            timeout: timeout,
            correlationId: correlationId);
    }

    private static LlmInvocationRequest CreateSensitiveRequest(TimeSpan? timeout = null)
    {
        var provider = new ProviderProfile(
            Guid.NewGuid(),
            "Safe streaming provider",
            ProviderKind.OpenAi,
            SensitiveValues[2],
            SensitiveValues[3],
            "gpt-streaming",
            ProviderTransportKind.ChatCompletions,
            IsEnabled: true,
            SupportsStreaming: true,
            SupportsTools: false,
            PreferFrameworkManagedChatHistory: true,
            SupportsBackgroundResponses: false,
            ConfigurationJson: "{}",
            Notes: SensitiveValues[4],
            HealthStatus: "Not checked",
            LastCheckedAtUtc: null,
            SuggestedModels: ["gpt-streaming"]);
        return new LlmInvocationRequest(
            provider,
            provider.DefaultModel,
            [
                new LlmMessage(LlmMessageRole.System, SensitiveValues[5]),
                new LlmMessage(LlmMessageRole.User, SensitiveValues[6])
            ],
            timeout: timeout,
            correlationId: "safe-redaction-correlation");
    }

    private static Exception CreateSensitiveFailure()
        => new InvalidOperationException(
            string.Join('|', SensitiveValues),
            new ApplicationException(SensitiveValues[1]));

    private static async Task<List<T>> CollectAsync<T>(IAsyncEnumerable<T> source)
    {
        var values = new List<T>();
        await foreach (var value in source)
        {
            values.Add(value);
        }

        return values;
    }

    private static void AssertPreparationLog(
        CapturedLogEntry entry,
        LlmInvocationRequest request)
    {
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Equal(default, entry.EventId);
        Assert.Null(entry.Exception);
        Assert.Equal(PreparationLogTemplate, entry.Template);
        Assert.Equal(
            ["CorrelationId", "Model", "ProviderId", "ProviderKind"],
            entry.Properties.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(request.Provider.Id, entry.Properties["ProviderId"]);
        Assert.Equal(request.Provider.Kind, entry.Properties["ProviderKind"]);
        Assert.Equal(request.Model, entry.Properties["Model"]);
        Assert.Equal(request.CorrelationId, entry.Properties["CorrelationId"]);
    }

    private static void AssertAttemptLog(
        CapturedLogEntry entry,
        LlmInvocationRequest request,
        bool partialOutputVisible)
    {
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Equal(default, entry.EventId);
        Assert.Null(entry.Exception);
        Assert.Equal(AttemptLogTemplate, entry.Template);
        Assert.Equal(
            [
                "AttemptOrdinal",
                "CorrelationId",
                "FailureKind",
                "Model",
                "PartialOutputVisible",
                "ProviderId",
                "ProviderKind"
            ],
            entry.Properties.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(request.Provider.Id, entry.Properties["ProviderId"]);
        Assert.Equal(request.Provider.Kind, entry.Properties["ProviderKind"]);
        Assert.Equal(request.Model, entry.Properties["Model"]);
        Assert.Equal(request.CorrelationId, entry.Properties["CorrelationId"]);
        Assert.Equal(1, entry.Properties["AttemptOrdinal"]);
        Assert.Equal(LlmInvocationFailureKind.ProviderFailure, entry.Properties["FailureKind"]);
        Assert.Equal(partialOutputVisible, entry.Properties["PartialOutputVisible"]);
    }

    private static void AssertSensitiveValuesAbsent(params string[] outputs)
    {
        var combined = string.Join('|', outputs);
        foreach (var sensitiveValue in SensitiveValues)
        {
            Assert.DoesNotContain(sensitiveValue, combined, StringComparison.Ordinal);
        }
    }

    private static string Serialize(CapturedLogEntry entry)
        => string.Join('|',
            entry.Template,
            entry.RenderedMessage,
            string.Join('|', entry.Properties.Select(pair => $"{pair.Key}={pair.Value}")),
            entry.Exception?.ToString() ?? string.Empty);

    private sealed record CapturedLogEntry(
        LogLevel Level,
        EventId EventId,
        string Template,
        IReadOnlyDictionary<string, object?> Properties,
        Exception? Exception,
        string RenderedMessage);

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<CapturedLogEntry> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
            => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel)
            => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var values = state is IEnumerable<KeyValuePair<string, object?>> structured
                ? structured.ToArray()
                : [];
            var template = values
                .FirstOrDefault(pair => pair.Key == "{OriginalFormat}")
                .Value?
                .ToString() ?? string.Empty;
            var properties = values
                .Where(pair => pair.Key != "{OriginalFormat}")
                .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
            Entries.Add(new CapturedLogEntry(
                logLevel,
                eventId,
                template,
                properties,
                exception,
                formatter(state, exception)));
        }
    }

    private sealed class NullScope : IDisposable
    {
        public static NullScope Instance { get; } = new();

        public void Dispose()
        {
        }
    }

    private sealed class ThrowingRuntimePool(Exception failure) : IProviderRuntimePool
    {
        public ValueTask<IProviderRuntimeHandle> GetRequiredAsync(
            Guid providerProfileId,
            CancellationToken cancellationToken = default)
            => ValueTask.FromException<IProviderRuntimeHandle>(failure);

        public ValueTask InvalidateAsync(
            Guid providerProfileId,
            ProviderRuntimeInvalidationReason reason,
            CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;

        public ValueTask DisposeAsync()
            => ValueTask.CompletedTask;
    }

    private sealed class DeadlineRuntimePool : IProviderRuntimePool
    {
        public int GetRequiredCallCount { get; private set; }

        public async ValueTask<IProviderRuntimeHandle> GetRequiredAsync(
            Guid providerProfileId,
            CancellationToken cancellationToken = default)
        {
            GetRequiredCallCount++;
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw new OperationCanceledException(
                    string.Join('|', SensitiveValues),
                    new InvalidOperationException(SensitiveValues[1]),
                    cancellationToken);
            }

            throw new InvalidOperationException("The deadline runtime pool completed without cancellation.");
        }

        public ValueTask InvalidateAsync(
            Guid providerProfileId,
            ProviderRuntimeInvalidationReason reason,
            CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;

        public ValueTask DisposeAsync()
            => ValueTask.CompletedTask;
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
