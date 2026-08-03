using System.Net;
using System.Text;
using System.Text.Json;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Tests.Unit;

public sealed class OllamaChatResponseResilienceHandlerTests
{
    [Fact]
    public async Task SendAsync_retries_one_transient_chat_failure_with_identical_request_body()
    {
        var transport = new SequenceHttpMessageHandler(
            (_, _) => Task.FromResult(JsonResponse(
                HttpStatusCode.InternalServerError,
                """{"error":"error parsing tool call"}""")),
            (_, _) => Task.FromResult(JsonResponse(HttpStatusCode.OK, "{}")));
        using var client = CreateClient(transport);
        using var content = CreateChatContent();

        using var response = await client.PostAsync("/api/chat", content, CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, transport.RequestBodies.Count);
        Assert.Equal(transport.RequestBodies[0], transport.RequestBodies[1]);
    }

    [Fact]
    public async Task Protocol_normalization_precedes_retry_for_two_sequential_tool_results()
    {
        const string requestJson =
            """
            {
              "model": "gptoss20b64k",
              "messages": [
                {
                  "role": "assistant",
                  "tool_calls": [
                    { "id": "call-001", "function": { "name": "workspace_read_file", "arguments": {} } }
                  ]
                },
                { "role": "tool", "content": "{\"callId\":\"call-001\",\"result\":{\"message\":\"first\"}}" },
                {
                  "role": "assistant",
                  "tool_calls": [
                    { "id": "call-002", "function": { "name": "project_structure_read", "arguments": {} } }
                  ]
                },
                { "role": "tool", "content": "{\"callId\":\"call-002\",\"result\":{\"nodeCount\":22}}" }
              ],
              "stream": true
            }
            """;
        var transport = new SequenceHttpMessageHandler(
            (_, _) => Task.FromResult(JsonResponse(
                HttpStatusCode.InternalServerError,
                """{"error":"error parsing tool call"}""")),
            (_, _) => Task.FromResult(JsonResponse(HttpStatusCode.OK, "{}")));
        using var client = new HttpClient(
            new OllamaToolResultProtocolHandler(
                new OllamaChatResponseResilienceHandler(
                    transport,
                    CreateProvider(),
                    "gptoss20b64k",
                    logger: null)))
        {
            BaseAddress = new Uri("http://localhost:11434")
        };
        using var content = new StringContent(requestJson, Encoding.UTF8, "application/json");

        using var response = await client.PostAsync("/api/chat", content, CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, transport.RequestBodies.Count);
        Assert.Equal(transport.RequestBodies[0], transport.RequestBodies[1]);
        using var normalizedRequest = JsonDocument.Parse(transport.RequestBodies[0]);
        var messages = normalizedRequest.RootElement.GetProperty("messages");
        Assert.Equal("workspace_read_file", messages[1].GetProperty("tool_name").GetString());
        Assert.Equal("project_structure_read", messages[3].GetProperty("tool_name").GetString());
    }

    [Fact]
    public async Task SendAsync_surfaces_bounded_redacted_provider_detail_after_retry_exhaustion()
    {
        var secretDetail = "token=super-secret-value";
        var transport = new SequenceHttpMessageHandler(
            (_, _) => Task.FromResult(JsonResponse(
                HttpStatusCode.InternalServerError,
                """{"error":"error parsing tool call"}""")),
            (_, _) => Task.FromResult(JsonResponse(
                HttpStatusCode.InternalServerError,
                $$"""{"error":"{{secretDetail}}"}""")));
        using var client = CreateClient(transport);
        using var content = CreateChatContent();

        var exception = await Assert.ThrowsAsync<OllamaChatResponseException>(() =>
            client.PostAsync("/api/chat", content, CancellationToken.None));

        Assert.Equal(HttpStatusCode.InternalServerError, exception.StatusCode);
        Assert.True(exception.IsTransient);
        Assert.Equal(2, transport.RequestBodies.Count);
        Assert.Contains("[REDACTED]", exception.ProviderDetail, StringComparison.Ordinal);
        Assert.DoesNotContain("super-secret-value", exception.ProviderDetail, StringComparison.Ordinal);
        Assert.True(exception.ProviderDetail.Length <= 1_201);
    }

    [Fact]
    public async Task SendAsync_omits_oversized_json_error_body_instead_of_exposing_raw_fields()
    {
        var oversizedDetail = new string('x', 2_000);
        var transport = new SequenceHttpMessageHandler(
            (_, _) => Task.FromResult(JsonResponse(
                HttpStatusCode.InternalServerError,
                $$"""{"error":"{{oversizedDetail}}","prompt":"PRIVATE-BUSINESS-DATA"}""")),
            (_, _) => Task.FromResult(JsonResponse(
                HttpStatusCode.InternalServerError,
                $$"""{"error":"{{oversizedDetail}}","prompt":"PRIVATE-BUSINESS-DATA"}""")));
        using var client = CreateClient(transport);
        using var content = CreateChatContent();

        var exception = await Assert.ThrowsAsync<OllamaChatResponseException>(() =>
            client.PostAsync("/api/chat", content, CancellationToken.None));

        Assert.Equal(
            "The provider returned an oversized JSON error body; its contents were omitted.",
            exception.ProviderDetail);
        Assert.DoesNotContain("PRIVATE-BUSINESS-DATA", exception.ProviderDetail, StringComparison.Ordinal);
        Assert.Equal(2, transport.RequestBodies.Count);
    }

    [Fact]
    public async Task SendAsync_does_not_retry_non_transient_chat_response()
    {
        var transport = new SequenceHttpMessageHandler(
            (_, _) => Task.FromResult(JsonResponse(
                HttpStatusCode.BadRequest,
                """{"error":"model is not available"}""")));
        using var client = CreateClient(transport);
        using var content = CreateChatContent();

        var exception = await Assert.ThrowsAsync<OllamaChatResponseException>(() =>
            client.PostAsync("/api/chat", content, CancellationToken.None));

        Assert.Equal(HttpStatusCode.BadRequest, exception.StatusCode);
        Assert.False(exception.IsTransient);
        Assert.Equal("model is not available", exception.ProviderDetail);
        Assert.Single(transport.RequestBodies);
    }

    [Fact]
    public async Task SendAsync_does_not_apply_chat_policy_to_other_endpoints()
    {
        var transport = new SequenceHttpMessageHandler(
            (_, _) => Task.FromResult(JsonResponse(
                HttpStatusCode.InternalServerError,
                """{"error":"server failure"}""")));
        using var client = CreateClient(transport);
        using var content = CreateChatContent();

        using var response = await client.PostAsync("/api/embed", content, CancellationToken.None);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Single(transport.RequestBodies);
    }

    [Fact]
    public async Task SendAsync_does_not_retry_cancellation()
    {
        var transport = new SequenceHttpMessageHandler(
            (_, cancellationToken) => throw new OperationCanceledException(cancellationToken));
        using var client = CreateClient(transport);
        using var content = CreateChatContent();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.PostAsync("/api/chat", content, CancellationToken.None));

        Assert.Single(transport.RequestBodies);
    }

    [Fact]
    public async Task SendAsync_disposes_failed_response_when_error_body_read_is_cancelled()
    {
        var responseStream = new CancellingReadStream();
        var transport = new SequenceHttpMessageHandler(
            (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StreamContent(responseStream)
            }));
        using var client = CreateClient(transport);
        using var content = CreateChatContent();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.PostAsync("/api/chat", content, CancellationToken.None));

        Assert.True(responseStream.IsDisposed);
        Assert.Single(transport.RequestBodies);
    }

    private static HttpClient CreateClient(HttpMessageHandler transport)
    {
        return new HttpClient(
            new OllamaChatResponseResilienceHandler(
                transport,
                CreateProvider(),
                "gptoss20b64k",
                logger: null))
        {
            BaseAddress = new Uri("http://localhost:11434")
        };
    }

    private static StringContent CreateChatContent()
    {
        return new StringContent(
            """{"model":"gptoss20b64k","messages":[{"role":"user","content":"summarize"}],"stream":true}""",
            Encoding.UTF8,
            "application/json");
    }

    private static HttpResponseMessage JsonResponse(
        HttpStatusCode statusCode,
        string content)
    {
        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(content, Encoding.UTF8, "application/json")
        };
    }

    private static ProviderProfile CreateProvider()
    {
        return new ProviderProfile(
            Guid.NewGuid(),
            "Local Ollama",
            ProviderKind.Ollama,
            "http://localhost:11434",
            string.Empty,
            "gptoss20b64k",
            ProviderTransportKind.ChatCompletions,
            IsEnabled: true,
            SupportsStreaming: true,
            SupportsTools: true,
            PreferFrameworkManagedChatHistory: true,
            SupportsBackgroundResponses: false,
            ConfigurationJson: "{}",
            Notes: string.Empty,
            HealthStatus: "Healthy",
            LastCheckedAtUtc: null,
            SuggestedModels: ["gptoss20b64k"]);
    }

    private sealed class SequenceHttpMessageHandler(
        params Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>[] responses)
        : HttpMessageHandler
    {
        private int requestIndex;

        public List<string> RequestBodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestBodies.Add(request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken));
            if (requestIndex >= responses.Length)
            {
                throw new InvalidOperationException("No response was configured for this request.");
            }

            return await responses[requestIndex++](request, cancellationToken);
        }
    }

    private sealed class CancellingReadStream : Stream
    {
        public bool IsDisposed { get; private set; }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new OperationCanceledException();
        }

        public override Task<int> ReadAsync(
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken)
        {
            return Task.FromException<int>(new OperationCanceledException(cancellationToken));
        }

        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromException<int>(new OperationCanceledException(cancellationToken));
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        protected override void Dispose(bool disposing)
        {
            IsDisposed = true;
            base.Dispose(disposing);
        }
    }
}
