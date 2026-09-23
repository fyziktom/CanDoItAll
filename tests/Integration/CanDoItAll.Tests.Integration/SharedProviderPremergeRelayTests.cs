using System.ClientModel;
using System.ClientModel.Primitives;
using System.Net;
using System.Text;
using System.Text.Json;
using CanDoItAll.SharedProviders.Abstractions;
using OpenAI;
using OpenAI.Chat;
using OpenAI.Responses;

namespace CanDoItAll.Tests.Integration;

#pragma warning disable OPENAI001
public sealed class SharedProviderPremergeRelayTests(SharedProviderStreamingApiFixture fixture)
    : IClassFixture<SharedProviderStreamingApiFixture> {
    [Theory]
    [InlineData("\"completed\"", "null", true)]
    [InlineData("\"completed\"", "{\"message\":\"private upstream detail\"}", false)]
    [InlineData("\"failed\"", "null", false)]
    [InlineData("\"incomplete\"", "null", false)]
    [InlineData(null, "null", false)]
    [InlineData("42", "null", false)]
    public async Task BufferedResponses_RequireSuccessfulCompletion(string? status, string error, bool succeeds) {
        var statusProperty = status is null ? "" : $"\"status\":{status},";
        await using var dispatcher = DispatcherHarness.Create(_ => new(HttpStatusCode.OK) {
            Content = new StringContent(
                $$$"""{"id":"resp_fixture",{{{statusProperty}}}"error":{{{error}}},"model":"private-model","output":[],"usage":{"input_tokens":7,"output_tokens":11}}""",
                Encoding.UTF8, "application/json")
        });
        var result = await dispatcher.DispatchAsync(SharedProviderRelayOperation.Responses, stream: false);
        if (!succeeds) {
            Assert.Equal(SharedProviderFailureCategory.UpstreamFailure,
                Assert.IsType<SharedProviderRelayDispatchResult.Failed>(result).Failure.Category);
            return;
        }
        var buffered = Assert.IsType<SharedProviderRelayDispatchResult.Buffered>(result);
        using var json = JsonDocument.Parse(buffered.PayloadUtf8);
        Assert.Equal(dispatcher.ModelId.Value, json.RootElement.GetProperty("model").GetString());
        Assert.Equal(7, buffered.Usage.InputTokens);
        Assert.Equal(11, buffered.Usage.OutputTokens);
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests, false)]
    [InlineData(HttpStatusCode.TooManyRequests, true)]
    [InlineData(HttpStatusCode.GatewayTimeout, false)]
    [InlineData(HttpStatusCode.GatewayTimeout, true)]
    public async Task OversizedUpstreamError_PreservesStatusAndRetryAfter(HttpStatusCode status, bool chunked) {
        var bytes = Encoding.UTF8.GetBytes("private error " + new string('x', 4096));
        await using var dispatcher = DispatcherHarness.Create(_ => {
            var response = new HttpResponseMessage(status) {
                Content = chunked ? new StreamContent(new ChunkSequenceStream([bytes])) : new ByteArrayContent(bytes)
            };
            response.Headers.TryAddWithoutValidation("Retry-After", "7");
            return response;
        });
        var result = await dispatcher.DispatchAsync(SharedProviderRelayOperation.Responses, stream: false);
        var failure = Assert.IsType<SharedProviderRelayDispatchResult.Failed>(result).Failure;
        Assert.Equal(status == HttpStatusCode.TooManyRequests
            ? SharedProviderFailureCategory.RateLimited : SharedProviderFailureCategory.Timeout, failure.Category);
        Assert.Equal(status == HttpStatusCode.TooManyRequests ? TimeSpan.FromSeconds(7) : null, failure.RetryAfter);
        Assert.DoesNotContain("private", failure.SanitizedMessage);
    }

    [Theory]
    [InlineData(SharedProviderRelayOperation.Responses, StreamFailure.Failed)]
    [InlineData(SharedProviderRelayOperation.Responses, StreamFailure.Incomplete)]
    [InlineData(SharedProviderRelayOperation.Responses, StreamFailure.Timeout)]
    [InlineData(SharedProviderRelayOperation.Responses, StreamFailure.Malformed)]
    [InlineData(SharedProviderRelayOperation.Responses, StreamFailure.Disconnect)]
    [InlineData(SharedProviderRelayOperation.ChatCompletions, StreamFailure.Timeout)]
    [InlineData(SharedProviderRelayOperation.ChatCompletions, StreamFailure.Malformed)]
    [InlineData(SharedProviderRelayOperation.ChatCompletions, StreamFailure.Disconnect)]
    public async Task StreamingFailure_IsObservedByPinnedSdk(SharedProviderRelayOperation operation, StreamFailure failure) {
        var prefix = operation == SharedProviderRelayOperation.Responses
            ? "event: response.output_text.delta\ndata: {\"type\":\"response.output_text.delta\",\"sequence_number\":0,\"item_id\":\"msg_fixture\",\"output_index\":0,\"content_index\":0,\"delta\":\"first\"}\n\n"
            : "data: {\"id\":\"chatcmpl_fixture\",\"object\":\"chat.completion.chunk\",\"created\":1,\"model\":\"upstream-model\",\"choices\":[{\"index\":0,\"delta\":{\"content\":\"first\"},\"finish_reason\":null}]}\n\n";
        var consumedFirstChunk = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var tail = failure switch {
            StreamFailure.Failed or StreamFailure.Incomplete => TerminalFailure(failure),
            StreamFailure.Malformed => "data: {malformed}\n\n",
            _ => null
        };
        IReadOnlyList<ReadOnlyMemory<byte>> chunks = tail is null
            ? [Encoding.UTF8.GetBytes(prefix)] : [Encoding.UTF8.GetBytes(prefix), Encoding.UTF8.GetBytes(tail)];
        var upstream = new ChunkSequenceStream(chunks, failure switch {
            StreamFailure.Timeout => new TimeoutException("private timeout detail"),
            StreamFailure.Disconnect => new IOException("private transport detail"),
            _ => null
        }, consumedFirstChunk.Task);
        await using var dispatcher = DispatcherHarness.Create(_ => new(HttpStatusCode.OK) {
            Content = new StreamContent(upstream) {
                Headers = { ContentType = new("text/event-stream") }
            }
        });
        ISharedProviderRelayStream? relay = null;
        fixture.Relay.Configure(async (request, cancellationToken) => {
            var result = await dispatcher.DispatchAsync(request.Operation, cancellationToken);
            relay = Assert.IsType<SharedProviderRelayDispatchResult.Streaming>(result).Stream;
            return result;
        });
        var client = new OpenAIClient(new ApiKeyCredential("fixture-key"), new OpenAIClientOptions {
            Endpoint = new Uri(fixture.Host.Client.BaseAddress!, SharedProviderRoutes.OpenAiBase),
            Transport = new HttpClientPipelineTransport(fixture.Host.Client),
            RetryPolicy = new ClientRetryPolicy(0)
        });
        var text = new StringBuilder();
        var exception = await Record.ExceptionAsync(async () => {
            if (operation == SharedProviderRelayOperation.ChatCompletions) {
                await foreach (var update in client.GetChatClient(dispatcher.ModelId.Value)
                    .CompleteChatStreamingAsync([new UserChatMessage("hello")])) {
                    foreach (var part in update.ContentUpdate) {
                        text.Append(part.Text);
                    }
                    consumedFirstChunk.TrySetResult();
                }
            } else {
                var options = new CreateResponseOptions { Model = dispatcher.ModelId.Value, StoredOutputEnabled = false, StreamingEnabled = true };
                options.InputItems.Add(ResponseItem.CreateUserMessageItem("hello"));
                await foreach (var update in client.GetResponsesClient().CreateResponseStreamingAsync(options)) {
                    if (update is StreamingResponseOutputTextDeltaUpdate delta) {
                        text.Append(delta.Delta);
                        consumedFirstChunk.TrySetResult();
                    }
                }
            }
        });
        Assert.True(text.ToString() == "first", $"Partial text was absent. SDK failure: {exception}");
        Assert.NotNull(exception);
        Assert.DoesNotContain("private", exception.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(relay);
        Assert.NotNull((await relay.Completion).Failure);
    }

    private static string TerminalFailure(StreamFailure failure) {
        var status = failure == StreamFailure.Failed ? "failed" : "incomplete";
        return $"event: response.{status}\ndata: {{\"type\":\"response.{status}\",\"response\":{{\"status\":\"{status}\",\"error\":{{\"message\":\"private upstream detail\"}}}}}}\n\n";
    }

    public enum StreamFailure { Failed, Incomplete, Timeout, Malformed, Disconnect }
}
#pragma warning restore OPENAI001
