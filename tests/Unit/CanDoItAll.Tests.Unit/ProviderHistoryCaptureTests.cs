using CanDoItAll.AgentFramework.Llm.Abstractions;
using CanDoItAll.AgentFramework.Llm.ProviderRuntime;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.ProviderHistory;
using CanDoItAll.AgentFramework.Providers;

namespace CanDoItAll.Tests.Unit;

public sealed class ProviderHistoryCaptureTests {
    [Fact]
    public void Caller_context_is_not_accepted_from_execution_or_chat_command_json() {
        var caller = new HistoryCaller(HistoryAuthenticationKind.ManagedCredential, new(Guid.NewGuid()), Subject: "forged");
        var context = ExecutionInvocationContext.Empty with { HistoryCaller = caller };
        var json = System.Text.Json.JsonSerializer.Serialize(context);
        Assert.DoesNotContain("forged", json);
        var forged = json[..^1] + ",\"HistoryCaller\":{\"Kind\":1,\"Subject\":\"forged\"}}";
        Assert.Null(System.Text.Json.JsonSerializer.Deserialize<ExecutionInvocationContext>(forged)!.HistoryCaller);
        var command = new CanDoItAll.AgentFramework.Llm.SimpleChats.Application.SendLlmChatTurnCommand(
            new(Guid.NewGuid()), new(Guid.NewGuid()), 0, "hello") { HistoryCaller = caller };
        Assert.DoesNotContain("forged", System.Text.Json.JsonSerializer.Serialize(command));
    }

    [Fact]
    public async Task Durable_start_failure_sends_nothing() {
        var history = new RecordingProviderHistory { FailBegin = true };
        var driver = new ProviderHistoryTestDriver();
        var adapter = Adapter(driver, history);
        await Assert.ThrowsAsync<LlmInvocationException>(() => adapter.InvokeAsync(Request()));
        Assert.Equal(0, driver.Calls);
        Assert.Empty(history.Completions);
    }

    [Fact]
    public async Task Empty_retry_has_two_attempts_one_operation() {
        var history = new RecordingProviderHistory();
        var driver = new ProviderHistoryTestDriver { EmptyFirstResponse = true };
        driver.OnInvoke = () => Assert.Equal(driver.Calls, history.Starts.Count);
        var request = Request();
        var response = await Adapter(driver, history).InvokeAsync(request);
        Assert.Equal("answer", response.ResponseText);
        Assert.Equal(2, driver.Calls);
        Assert.Equal(2, history.Starts.Select(start => start.AttemptId).Distinct().Count());
        Assert.All(history.Starts, start => Assert.Equal(request.History.RequestId, start.RequestId));
        Assert.Equal(2, request.History.Attempts.Snapshot().Count);
        Assert.All(history.Completions, item => Assert.Equal(10, item.Completion.Usage.InputTokens));
    }

    [Fact]
    public async Task Terminal_write_failure_does_not_repeat_inference() {
        var history = new RecordingProviderHistory { FailCompletion = true };
        var driver = new ProviderHistoryTestDriver { EmptyFirstResponse = true };
        var exception = await Assert.ThrowsAsync<LlmInvocationException>(() => Adapter(driver, history).InvokeAsync(Request()));
        Assert.IsType<ProviderHistoryException>(exception.InnerException);
        Assert.Equal(1, driver.Calls);
        Assert.Single(history.Completions);
    }

    [Fact]
    public async Task Missing_usage_is_unavailable_not_zero() {
        var history = new RecordingProviderHistory();
        await Adapter(new() { NoUsage = true }, history).InvokeAsync(Request());
        var usage = Assert.Single(history.Completions).Completion.Usage;
        Assert.Equal(HistoryUsageState.Unavailable, usage.State);
        Assert.Null(usage.InputTokens);
        Assert.Null(usage.OutputTokens);
    }

    [Fact]
    public async Task Stream_terminal_usage_survives_disposal() {
        var history = new RecordingProviderHistory();
        var driver = Factory(new(), history).Resolve<IProviderStreamingChatCompletionDriver>(ProviderKind.OpenAi);
        await using (var stream = driver.StreamChatAsync(ChatRequest()).GetAsyncEnumerator()) {
            Assert.True(await stream.MoveNextAsync());
            Assert.IsType<ProviderChatTextDelta>(stream.Current);
            Assert.True(await stream.MoveNextAsync());
            Assert.IsType<ProviderChatCompleted>(stream.Current);
        }
        var completion = Assert.Single(history.Completions).Completion;
        Assert.Equal(HistoryOutcome.Succeeded, completion.Outcome);
        Assert.Equal(10, completion.Usage.InputTokens);
    }

    [Fact]
    public async Task Stream_failure_preserves_usage_before_terminal_marker() {
        var history = new RecordingProviderHistory();
        var driver = Factory(new() { FailAfterStreamUsage = true }, history).Resolve<IProviderStreamingChatCompletionDriver>(ProviderKind.OpenAi);
        await Assert.ThrowsAsync<IOException>(async () => {
            await foreach (var update in driver.StreamChatAsync(ChatRequest())) { }
        });
        var completion = Assert.Single(history.Completions).Completion;
        Assert.Equal(HistoryOutcome.Failed, completion.Outcome);
        Assert.Equal(10, completion.Usage.InputTokens);
    }

    [Fact]
    public async Task First_chunk_does_not_wait_for_stream_completion() {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var history = new RecordingProviderHistory();
        var driver = Factory(new() { ContinueStream = release }, history).Resolve<IProviderStreamingChatCompletionDriver>(ProviderKind.OpenAi);
        await using var stream = driver.StreamChatAsync(ChatRequest()).GetAsyncEnumerator();
        Assert.True(await stream.MoveNextAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(2)));
        Assert.Empty(history.Completions);
        release.SetResult();
    }

    [Theory]
    [InlineData(HistoryOperation.AnalyzeImage)]
    [InlineData(HistoryOperation.GenerateImage)]
    [InlineData(HistoryOperation.EditImage)]
    [InlineData(HistoryOperation.TranscribeSpeech)]
    [InlineData(HistoryOperation.SynthesizeSpeech)]
    [InlineData(HistoryOperation.ListModels)]
    [InlineData(HistoryOperation.TestHealth)]
    [InlineData(HistoryOperation.CreateOrUpdateModel)]
    public async Task Every_typed_capability_records_one_actual_call(HistoryOperation operation) {
        var history = new RecordingProviderHistory();
        var raw = new ProviderHistoryTestDriver();
        var factory = Factory(raw, history);
        var provider = ProviderHistoryTestDriver.Provider();
        switch (operation) {
            case HistoryOperation.AnalyzeImage:
                await factory.Resolve<IProviderChatCompletionDriver>(provider.Kind).CompleteChatAsync(ChatRequest() with {
                    Attachments = [new("image", "image/png", [1, 2])]
                });
                break;
            case HistoryOperation.GenerateImage or HistoryOperation.EditImage:
                await factory.Resolve<IProviderImageGenerationDriver>(provider.Kind).GenerateImageAsync(new(provider, provider.DefaultModel,
                    "image prompt", "", "", ProviderGeneratedImageFormat.Png,
                    operation == HistoryOperation.EditImage ? [new("source", "image/png", [1, 2])] : []));
                break;
            case HistoryOperation.TranscribeSpeech:
                await factory.Resolve<IProviderSpeechToTextDriver>(provider.Kind).TranscribeSpeechAsync(new(provider, provider.DefaultModel,
                    [new("audio", "audio/wav", [1, 2])], "", ""));
                break;
            case HistoryOperation.SynthesizeSpeech:
                await factory.Resolve<IProviderTextToSpeechDriver>(provider.Kind).SynthesizeSpeechAsync(new(provider, provider.DefaultModel, "speak", "", "wav", ""));
                break;
            case HistoryOperation.ListModels:
                await factory.Resolve<IProviderModelCatalogDriver>(provider.Kind).ListModelsAsync(new(provider, AgentProviderCapabilityKind.ChatCompletion));
                break;
            case HistoryOperation.TestHealth:
                await factory.Resolve<IProviderHealthDriver>(provider.Kind).TestHealthAsync(provider);
                break;
            case HistoryOperation.CreateOrUpdateModel:
                await factory.Resolve<IProviderModelMaintenanceDriver>(provider.Kind).CreateOrUpdateModelAsync(new(provider, provider.DefaultModel, "base", "system", 100));
                break;
        }
        Assert.Equal(1, raw.Calls);
        Assert.Equal(operation, Assert.Single(history.Starts).Operation);
        Assert.Single(history.Completions);
        if (operation is not HistoryOperation.AnalyzeImage) {
            Assert.Equal(HistoryPriceState.UnsupportedUnit, Assert.Single(history.Completions).Completion.Price.State);
        }
    }

    [Fact]
    public async Task Relay_owned_image_uses_canonical_audit_only() {
        var history = new RecordingProviderHistory();
        var raw = new ProviderHistoryTestDriver();
        var provider = ProviderHistoryTestDriver.Provider();
        await Factory(raw, history).Resolve<IProviderImageGenerationDriver>(provider.Kind).GenerateImageAsync(
            new(provider, provider.DefaultModel, "image", "", "", ProviderGeneratedImageFormat.Png, []) {
                History = HistoryInvocationContext.Create(HistoryWorkload.SharedRelay,
                    owner: new(HistorySourceKind.SharedRelay, new("relay"), new("request")))
            });
        Assert.Equal(1, raw.Calls);
        Assert.Empty(history.Starts);
    }


    [Fact]
    public async Task Actual_driver_capture_freezes_tariff_before_dispatch() {
        var prices = new List<ProviderModelTokenPrice> { new("history-model", 2m, 2m, 4m) };
        var provider = ProviderHistoryTestDriver.Provider() with { ModelPrices = prices, PricingSourceRevision = "revision-1" };
        var history = new RecordingProviderHistory();
        var raw = new ProviderHistoryTestDriver { OnInvoke = () => prices[0] = prices[0] with { InputPerMillionTokensUsd = 999m } };
        await Factory(raw, history).Resolve<IProviderChatCompletionDriver>(provider.Kind)
            .CompleteChatAsync(ChatRequest() with { Provider = provider });
        var price = Assert.Single(history.Completions).Completion.Price;
        Assert.Equal(HistoryPriceState.CalculatedAtExecution, price.State);
        Assert.Equal(0.00004m, price.Amount);
        Assert.Equal("revision-1", price.SourceRevision);
    }

    [Fact]
    public async Task Batch_callback_passes_typed_context_to_actual_capture_driver() {
        var provider = ProviderHistoryTestDriver.Provider();
        var history = new RecordingProviderHistory();
        var raw = new ProviderHistoryTestDriver();
        var factory = Factory(raw, history);
        var descriptors = new ProviderProfileRuntimeDescriptorStore();
        descriptors.Upsert(provider);
        await using var pool = new ProviderRuntimePool(descriptors, new ProviderRuntimeHandleFactory(factory));
        var job = new ProviderBatchJobRequest<string>(Guid.NewGuid(), [new(Guid.NewGuid(), 0, "", "current")],
            [new(provider)], AgentProviderCapabilityKind.ChatCompletion, AgentProviderOperationKind.CompleteChat,
            provider.DefaultModel, new(PersistenceMode: ProviderBatchPersistenceMode.Checkpointed));
        var result = await new ProviderBatchJobBalancer(pool, new InMemoryProviderBatchJobCheckpointStore()).ExecuteAsync<string, string>(job, async (context, token) => {
            var response = await factory.Resolve<IProviderChatCompletionDriver>(provider.Kind).CompleteChatAsync(
                new(provider, provider.DefaultModel, "", [], context.Input.Payload) { History = context.History }, token);
            return ProviderBatchDispatchOutcome<string>.FromValue(response.ResponseText);
        });
        Assert.True(result.Succeeded);
        Assert.Equal(1, raw.Calls);
        var start = Assert.Single(history.Starts);
        Assert.Equal(HistoryWorkload.Batch, start.Workload);
        Assert.Null(start.ContentOwner);
        Assert.Single(history.Completions);
    }

    private static ProviderBackedLlmInvocationAdapter Adapter(ProviderHistoryTestDriver driver, RecordingProviderHistory history) {
        var descriptors = new ProviderProfileRuntimeDescriptorStore();
        return new(descriptors, new ProviderRuntimePool(descriptors, new ProviderRuntimeHandleFactory(Factory(driver, history))));
    }

    private static HistoryProviderDriverFactory Factory(ProviderHistoryTestDriver driver, RecordingProviderHistory history) =>
        new(new AgentProviderDriverRegistryBuilder().AddDriver(driver).Build(), history, TimeProvider.System);

    private static LlmInvocationRequest Request() => new(ProviderHistoryTestDriver.Provider(), "history-model", [new(LlmMessageRole.User, "current")]);

    private static ProviderChatCompletionRequest ChatRequest() => new(ProviderHistoryTestDriver.Provider(), "history-model", "system", [], "current");
}
