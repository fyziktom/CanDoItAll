using CanDoItAll.AgentFramework.Llm.Abstractions;
using CanDoItAll.AgentFramework.Llm.ProviderRuntime;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Providers;

namespace CanDoItAll.Tests.Unit;

public sealed class ProviderBackedLlmInvocationAdapterTests
{
    [Fact]
    public async Task InvokeAsync_calls_the_driver_exactly_once_and_maps_the_text_response()
    {
        var driver = new RecordingChatCompletionDriver(
            (request, cancellationToken) => Task.FromResult(new ProviderChatCompletionResult(request.Model, "hello back", 10, 5)));
        var adapter = CreateAdapter(driver);
        var provider = CreateProvider();

        var result = await adapter.InvokeAsync(new LlmInvocationRequest(
            provider,
            provider.DefaultModel,
            [new LlmMessage(LlmMessageRole.System, "Be terse."), new LlmMessage(LlmMessageRole.User, "Hi")]));

        Assert.Equal(1, driver.CallCount);
        Assert.Equal("hello back", result.ResponseText);
        Assert.Equal(10, result.Usage.InputTokens);
        Assert.Equal(5, result.Usage.OutputTokens);
        Assert.Equal(0, result.Usage.CachedInputTokens);
    }

    [Fact]
    public async Task InvokeAsync_maps_system_user_assistant_order_for_multi_turn_conversations()
    {
        ProviderChatCompletionRequest? captured = null;
        var driver = new RecordingChatCompletionDriver((request, cancellationToken) =>
        {
            captured = request;
            return Task.FromResult(new ProviderChatCompletionResult(request.Model, "ok", 1, 1));
        });
        var adapter = CreateAdapter(driver);
        var provider = CreateProvider();

        await adapter.InvokeAsync(new LlmInvocationRequest(
            provider,
            provider.DefaultModel,
            [
                new LlmMessage(LlmMessageRole.System, "System A"),
                new LlmMessage(LlmMessageRole.System, "System B"),
                new LlmMessage(LlmMessageRole.User, "Q1"),
                new LlmMessage(LlmMessageRole.Assistant, "A1"),
                new LlmMessage(LlmMessageRole.User, "Q2")
            ]));

        Assert.NotNull(captured);
        Assert.Equal("System A\n\nSystem B", captured!.SystemPrompt);
        Assert.Collection(
            captured.Messages,
            message =>
            {
                Assert.Equal(ChatMessageRole.User, message.Role);
                Assert.Equal("Q1", message.Content);
            },
            message =>
            {
                Assert.Equal(ChatMessageRole.Assistant, message.Role);
                Assert.Equal("A1", message.Content);
            });
        Assert.Equal("Q2", captured.Prompt);
    }

    [Fact]
    public async Task InvokeAsync_forwards_temperature_and_response_format_to_the_driver_request()
    {
        ProviderChatCompletionRequest? captured = null;
        var driver = new RecordingChatCompletionDriver((request, cancellationToken) =>
        {
            captured = request;
            return Task.FromResult(new ProviderChatCompletionResult(request.Model, "ok", 1, 1));
        });
        var adapter = CreateAdapter(driver);
        var provider = CreateProvider();

        await adapter.InvokeAsync(new LlmInvocationRequest(
            provider,
            provider.DefaultModel,
            [new LlmMessage(LlmMessageRole.User, "Q")],
            responseFormat: new LlmResponseFormat(true, """{"type":"object"}""", "result", "A result."),
            settings: new LlmModelSettings(0.4, """{"maxOutputTokens":123}""")));

        Assert.NotNull(captured);
        Assert.Equal(0.4, captured!.Temperature);
        Assert.NotNull(captured.ResponseFormat);
        Assert.True(captured.ResponseFormat!.RequireJson);
        Assert.Equal("""{"type":"object"}""", captured.ResponseFormat.SchemaJson);
        Assert.Equal("result", captured.ResponseFormat.SchemaName);
        Assert.Equal("A result.", captured.ResponseFormat.SchemaDescription);
        Assert.Equal("""{"maxOutputTokens":123}""", captured.ModelParameterConfigurationJson);
    }

    [Fact]
    public async Task InvokeAsync_maps_usage_including_cached_tokens()
    {
        var driver = new RecordingChatCompletionDriver((request, cancellationToken) =>
            Task.FromResult(new ProviderChatCompletionResult(request.Model, "ok", 100, 40) { CachedInputTokens = 15 }));
        var adapter = CreateAdapter(driver);
        var provider = CreateProvider();

        var result = await adapter.InvokeAsync(new LlmInvocationRequest(
            provider,
            provider.DefaultModel,
            [new LlmMessage(LlmMessageRole.User, "Q")]));

        Assert.Equal(100, result.Usage.InputTokens);
        Assert.Equal(40, result.Usage.OutputTokens);
        Assert.Equal(15, result.Usage.CachedInputTokens);
    }

    [Fact]
    public async Task InvokeAsync_retries_once_then_fails_typed_on_persistently_blank_responses()
    {
        var attempts = 0;
        var driver = new RecordingChatCompletionDriver((request, cancellationToken) =>
        {
            attempts++;
            return Task.FromResult(new ProviderChatCompletionResult(request.Model, "   ", 1, 1));
        });
        var adapter = CreateAdapter(driver);
        var provider = CreateProvider();

        var exception = await Assert.ThrowsAsync<LlmInvocationException>(() => adapter.InvokeAsync(new LlmInvocationRequest(
            provider,
            provider.DefaultModel,
            [new LlmMessage(LlmMessageRole.User, "Q")])));

        Assert.Equal(LlmInvocationFailureKind.EmptyResponse, exception.Kind);
        Assert.Equal(ProviderBackedLlmInvocationAdapter.MaximumEmptyResponseAttempts, attempts);
    }

    [Fact]
    public async Task InvokeAsync_recovers_when_the_retry_returns_text()
    {
        var attempts = 0;
        var driver = new RecordingChatCompletionDriver((request, cancellationToken) =>
        {
            attempts++;
            return Task.FromResult(new ProviderChatCompletionResult(
                request.Model,
                attempts == 1 ? "   " : "recovered",
                1,
                1));
        });
        var adapter = CreateAdapter(driver);
        var provider = CreateProvider();

        var result = await adapter.InvokeAsync(new LlmInvocationRequest(
            provider,
            provider.DefaultModel,
            [new LlmMessage(LlmMessageRole.User, "Q")]));

        Assert.Equal("recovered", result.ResponseText);
        Assert.Equal(2, attempts);
    }

    [Fact]
    public async Task InvokeAsync_propagates_cancellation_when_the_driver_honors_the_token()
    {
        var driverStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var driver = new RecordingChatCompletionDriver(async (request, cancellationToken) =>
        {
            driverStarted.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return new ProviderChatCompletionResult(request.Model, "unused", 0, 0);
        });
        var adapter = CreateAdapter(driver);
        var provider = CreateProvider();
        using var cts = new CancellationTokenSource();

        var invokeTask = adapter.InvokeAsync(
            new LlmInvocationRequest(provider, provider.DefaultModel, [new LlmMessage(LlmMessageRole.User, "Q")]),
            cts.Token);
        await driverStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => invokeTask);
        Assert.Equal(1, driver.CallCount);
    }

    [Fact]
    public async Task InvokeAsync_wraps_provider_failures_in_sanitized_typed_exceptions()
    {
        var driver = new RecordingChatCompletionDriver((request, cancellationToken) =>
            throw new InvalidOperationException("simulated provider failure"));
        var adapter = CreateAdapter(driver);
        var provider = CreateProvider();

        var exception = await Assert.ThrowsAsync<LlmInvocationException>(() => adapter.InvokeAsync(new LlmInvocationRequest(
            provider,
            provider.DefaultModel,
            [new LlmMessage(LlmMessageRole.User, "Q")],
            correlationId: "caller-42")));

        Assert.Equal(LlmInvocationFailureKind.ProviderFailure, exception.Kind);
        // The user-facing message never carries raw provider exception text;
        // the original exception stays available for structured logging only.
        Assert.DoesNotContain("simulated provider failure", exception.Message, StringComparison.Ordinal);
        Assert.Contains("caller-42", exception.Message, StringComparison.Ordinal);
        Assert.Equal("simulated provider failure", exception.InnerException?.Message);
    }

    private static ProviderBackedLlmInvocationAdapter CreateAdapter(RecordingChatCompletionDriver driver)
    {
        var factory = new AgentProviderDriverRegistryBuilder().AddDriver(driver).Build();
        var store = new ProviderProfileRuntimeDescriptorStore();
        var pool = new ProviderRuntimePool(store, new ProviderRuntimeHandleFactory(factory));
        return new ProviderBackedLlmInvocationAdapter(store, pool);
    }

    private static ProviderProfile CreateProvider()
        => new(
            Guid.NewGuid(),
            "Lightweight test provider",
            ProviderKind.OpenAi,
            "https://example.invalid/v1",
            "LIGHTWEIGHT_TEST_API_KEY",
            "gpt-lightweight",
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
            SuggestedModels: ["gpt-lightweight"]);

    private sealed class RecordingChatCompletionDriver(
        Func<ProviderChatCompletionRequest, CancellationToken, Task<ProviderChatCompletionResult>> respond)
        : IProviderChatCompletionDriver
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
            return respond(request, cancellationToken);
        }
    }
}
