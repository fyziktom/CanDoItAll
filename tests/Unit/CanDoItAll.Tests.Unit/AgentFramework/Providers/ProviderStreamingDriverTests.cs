using System.Net;
using System.Text;
using System.Text.Json;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Providers;

namespace CanDoItAll.Tests.Unit.AgentFramework.Providers;

public sealed class ProviderStreamingDriverTests
{
    [Fact]
    public async Task OpenAi_chat_completions_preserves_utf8_across_fragmented_sse_and_reports_usage()
    {
        var handler = new StreamingHandler(
            """
            data: {"model":"gpt-stream","choices":[{"index":0,"delta":{"content":"Grü"},"finish_reason":null}]}

            data: {"model":"gpt-stream","choices":[{"index":0,"delta":{"content":"ße"},"finish_reason":"stop"}]}

            data: {"choices":[],"usage":{"prompt_tokens":9,"completion_tokens":2,"prompt_tokens_details":{"cached_tokens":3}}}

            data: [DONE]

            """,
            "text/event-stream");
        using var httpClient = new HttpClient(handler);
        var driver = new OpenAiProviderDriver(httpClient, new FixedCredentialResolver("openai-key"));
        var provider = CreateProvider(ProviderKind.OpenAi, "https://api.openai.test/v1", "gpt-stream");

        var updates = await CollectAsync(driver.StreamChatAsync(CreateRequest(provider)));

        Assert.Equal(["Grü", "ße"], updates.OfType<ProviderChatTextDelta>().Select(item => item.Text));
        var completed = Assert.Single(updates.OfType<ProviderChatCompleted>());
        Assert.Equal("stop", completed.FinishReason);
        Assert.Equal(9, completed.InputTokens);
        Assert.Equal(2, completed.OutputTokens);
        Assert.Equal(3, completed.CachedInputTokens);
        using var payload = JsonDocument.Parse(handler.RequestBody);
        Assert.True(payload.RootElement.GetProperty("stream").GetBoolean());
        Assert.True(payload.RootElement.GetProperty("stream_options").GetProperty("include_usage").GetBoolean());
    }

    [Fact]
    public async Task OpenAi_responses_emits_only_public_text_and_requires_completed_event()
    {
        var handler = new StreamingHandler(
            """
            event: response.reasoning_summary_text.delta
            data: {"type":"response.reasoning_summary_text.delta","delta":"hidden reasoning"}

            event: response.output_text.delta
            data: {"type":"response.output_text.delta","delta":"public answer"}

            event: response.completed
            data: {"type":"response.completed","response":{"model":"gpt-responses","status":"completed","usage":{"input_tokens":12,"output_tokens":4,"input_tokens_details":{"cached_tokens":5}}}}

            """,
            "text/event-stream");
        using var httpClient = new HttpClient(handler);
        var driver = new OpenAiProviderDriver(httpClient, new FixedCredentialResolver("openai-key"));
        var provider = CreateProvider(
            ProviderKind.OpenAi,
            "https://api.openai.test/v1",
            "gpt-responses",
            ProviderTransportKind.Responses);

        var updates = await CollectAsync(driver.StreamChatAsync(CreateRequest(provider)));

        Assert.Equal("public answer", Assert.Single(updates.OfType<ProviderChatTextDelta>()).Text);
        Assert.DoesNotContain("hidden reasoning", string.Join('|', updates.Select(item => item.ToString())), StringComparison.Ordinal);
        var completed = Assert.Single(updates.OfType<ProviderChatCompleted>());
        Assert.Equal(12, completed.InputTokens);
        Assert.Equal(4, completed.OutputTokens);
        Assert.Equal(5, completed.CachedInputTokens);
    }

    [Fact]
    public async Task Azure_openai_uses_the_same_incremental_contract_without_exposing_its_wire_protocol()
    {
        var handler = new StreamingHandler(
            """
            data: {"choices":[{"index":0,"delta":{"content":"azure "},"finish_reason":null}]}

            data: {"choices":[{"index":0,"delta":{"content":"answer"},"finish_reason":"stop"}],"usage":{"prompt_tokens":6,"completion_tokens":2}}

            data: [DONE]

            """,
            "text/event-stream");
        using var httpClient = new HttpClient(handler);
        var driver = new AzureOpenAiProviderDriver(httpClient, new FixedCredentialResolver("azure-key"));
        var provider = CreateProvider(
            ProviderKind.AzureOpenAi,
            "https://azure-openai.test",
            "deployment-stream");

        var updates = await CollectAsync(driver.StreamChatAsync(CreateRequest(provider)));

        Assert.Equal("azure answer", string.Concat(updates.OfType<ProviderChatTextDelta>().Select(item => item.Text)));
        Assert.Single(updates.OfType<ProviderChatCompleted>());
        Assert.Contains("/deployments/deployment-stream/chat/completions", handler.PathAndQuery, StringComparison.Ordinal);
        Assert.Equal("azure-key", handler.ApiKey);
    }

    [Fact]
    public async Task Ollama_ndjson_ignores_thinking_and_requires_the_done_frame()
    {
        var handler = new StreamingHandler(
            """
            {"model":"qwen-stream","message":{"role":"assistant","content":"hello ","thinking":"private chain"},"done":false}
            {"model":"qwen-stream","message":{"role":"assistant","content":"world"},"done":false}
            {"model":"qwen-stream","message":{"role":"assistant","content":""},"done":true,"done_reason":"stop","prompt_eval_count":10,"eval_count":2}
            """,
            "application/x-ndjson");
        using var httpClient = new HttpClient(handler);
        var driver = new OllamaProviderDriver(httpClient);
        var provider = CreateProvider(ProviderKind.Ollama, "http://ollama.test", "qwen-stream");

        var updates = await CollectAsync(driver.StreamChatAsync(CreateRequest(provider)));

        Assert.Equal("hello world", string.Concat(updates.OfType<ProviderChatTextDelta>().Select(item => item.Text)));
        Assert.DoesNotContain("private chain", string.Join('|', updates.Select(item => item.ToString())), StringComparison.Ordinal);
        var completed = Assert.Single(updates.OfType<ProviderChatCompleted>());
        Assert.Equal(10, completed.InputTokens);
        Assert.Equal(2, completed.OutputTokens);
        using var payload = JsonDocument.Parse(handler.RequestBody);
        Assert.True(payload.RootElement.GetProperty("stream").GetBoolean());
    }

    [Fact]
    public async Task Provider_stream_malformed_frame_failure_is_stable_and_redacted()
    {
        const string RawFrame = "{not-json-and-secret}";
        var handler = new StreamingHandler($"data: {RawFrame}\n\n", "text/event-stream");
        using var httpClient = new HttpClient(handler);
        var driver = new OpenAiProviderDriver(httpClient, new FixedCredentialResolver("openai-key"));
        var provider = CreateProvider(ProviderKind.OpenAi, "https://api.openai.test/v1", "gpt-stream");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => CollectAsync(driver.StreamChatAsync(CreateRequest(provider))));

        Assert.Equal("OpenAI chat completion stream contained malformed JSON.", exception.Message);
        Assert.DoesNotContain(RawFrame, exception.Message, StringComparison.Ordinal);
    }

    private static ProviderChatCompletionRequest CreateRequest(ProviderProfile provider)
        => new(provider, provider.DefaultModel, "system", [], "prompt");

    private static ProviderProfile CreateProvider(
        ProviderKind kind,
        string baseUrl,
        string model,
        ProviderTransportKind transport = ProviderTransportKind.ChatCompletions)
        => new(
            Guid.NewGuid(),
            $"{kind} streaming provider",
            kind,
            baseUrl,
            "TEST_API_KEY",
            model,
            transport,
            IsEnabled: true,
            SupportsStreaming: true,
            SupportsTools: false,
            PreferFrameworkManagedChatHistory: true,
            SupportsBackgroundResponses: false,
            ConfigurationJson: "{}",
            Notes: string.Empty,
            HealthStatus: "Not checked",
            LastCheckedAtUtc: null,
            SuggestedModels: [model]);

    private static async Task<List<T>> CollectAsync<T>(IAsyncEnumerable<T> source)
    {
        var values = new List<T>();
        await foreach (var value in source)
        {
            values.Add(value);
        }

        return values;
    }

    private sealed class FixedCredentialResolver(string apiKey) : IProviderDriverCredentialResolver
    {
        public ProviderDriverCredential Resolve(ProviderProfile provider)
            => ProviderDriverCredential.Resolved(apiKey);
    }

    private sealed class StreamingHandler(string body, string contentType) : HttpMessageHandler
    {
        public string RequestBody { get; private set; } = string.Empty;

        public string PathAndQuery { get; private set; } = string.Empty;

        public string ApiKey { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestBody = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);
            PathAndQuery = request.RequestUri?.PathAndQuery ?? string.Empty;
            ApiKey = request.Headers.TryGetValues("api-key", out var values)
                ? Assert.Single(values)
                : string.Empty;
            var bytes = Encoding.UTF8.GetBytes(body.Replace("\r\n", "\n", StringComparison.Ordinal));
            var content = new StreamContent(new FragmentedReadStream(bytes));
            content.Headers.ContentType = System.Net.Http.Headers.MediaTypeHeaderValue.Parse(contentType);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
        }
    }

    private sealed class FragmentedReadStream(byte[] bytes) : Stream
    {
        private int position;
        private int fragmentSize = 1;

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => bytes.Length;

        public override long Position
        {
            get => position;
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var read = ReadCore(buffer.AsSpan(offset, count));
            return read;
        }

        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(ReadCore(buffer.Span));
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin)
            => throw new NotSupportedException();

        public override void SetLength(long value)
            => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count)
            => throw new NotSupportedException();

        private int ReadCore(Span<byte> destination)
        {
            if (position >= bytes.Length)
            {
                return 0;
            }

            var count = Math.Min(Math.Min(destination.Length, fragmentSize), bytes.Length - position);
            bytes.AsSpan(position, count).CopyTo(destination);
            position += count;
            fragmentSize = fragmentSize == 3 ? 1 : fragmentSize + 1;
            return count;
        }
    }
}
