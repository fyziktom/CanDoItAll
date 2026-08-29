using System.ClientModel;
using System.ClientModel.Primitives;
using System.Net;
using System.Text;
using System.Text.Json;
using CanDoItAll.SharedProviders.Abstractions;
using CanDoItAll.SharedProviders.Http;
using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Responses;

namespace CanDoItAll.Tests.Unit;

#pragma warning disable OPENAI001, MAAI001
public sealed class OpenAiResponsesRealClientWireTests
{
    private const string FunctionCallId = "call-001";
    private const string FunctionName = "record_value";

    [Fact]
    public async Task Framework_managed_tool_turn_is_accepted_by_shared_provider_policy()
    {
        var model = SharedProviderRoutingModelIdCodec.Create(
            new SharedProviderPublicationId(Guid.NewGuid()),
            "upstream-model").Value;
        var handler = new ResponsesPolicyCaptureHandler(model);
        using var httpClient = new HttpClient(handler);
        var client = new OpenAIClient(
            new ApiKeyCredential("test-source-token"),
            new OpenAIClientOptions
            {
                Endpoint = new Uri("https://shared.example.test/api/shared-providers/openai/v1"),
                Transport = new HttpClientPipelineTransport(httpClient)
            });
        using var chatClient = client
            .GetResponsesClient()
            .AsIChatClientWithStoredOutputDisabled(
                model,
                includeReasoningEncryptedContent: false);
        var function = AIFunctionFactory.Create(
            (string value) => $"recorded:{value}",
            FunctionName);
        Microsoft.Extensions.AI.ChatMessage[] messages =
        [
            new(ChatRole.User, "Previous request."),
            new(ChatRole.Assistant, "Previous response."),
            new(ChatRole.User, "Record alpha with the available tool."),
            new(
                ChatRole.Assistant,
                [
                    new FunctionCallContent(
                        FunctionCallId,
                        FunctionName,
                        new Dictionary<string, object?>
                        {
                            ["value"] = "alpha"
                        })
                ]),
            new(
                ChatRole.Tool,
                [new FunctionResultContent(FunctionCallId, JsonSerializer.SerializeToElement(new { items = Array.Empty<object>(), pageIndex = 0, pageSize = 1, totalCount = 0, totalPages = 0 }))])
        ];

        var response = await chatClient.GetResponseAsync(
            messages,
            new ChatOptions
            {
                ModelId = model,
                Tools = [function]
            });

        Assert.Equal("accepted", response.Text);
        Assert.IsType<SharedProviderRelayRequestPolicyResult.Accepted>(handler.PolicyResult);
        using var payload = JsonDocument.Parse(handler.RawPayload);
        var input = payload.RootElement.GetProperty("input").EnumerateArray().ToArray();
        var assistantHistory = Assert.Single(
            input,
            item => item.TryGetProperty("role", out var role) && role.GetString() == "assistant");
        var assistantContent = assistantHistory.GetProperty("content")[0];
        Assert.Equal("output_text", assistantContent.GetProperty("type").GetString());
        Assert.Empty(assistantContent.GetProperty("annotations").EnumerateArray());
        Assert.Contains(input, item => item.GetProperty("type").GetString() == "function_call");
        Assert.Contains(input, item => item.GetProperty("type").GetString() == "function_call_output");
    }

    private sealed class ResponsesPolicyCaptureHandler(string model) : HttpMessageHandler
    {
        public SharedProviderRelayRequestPolicyResult? PolicyResult { get; private set; }

        public string RawPayload { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("/api/shared-providers/openai/v1/responses", request.RequestUri!.AbsolutePath);
            var payload = await request.Content!.ReadAsByteArrayAsync(cancellationToken);
            RawPayload = Encoding.UTF8.GetString(payload);
            Assert.True(new SharedProviderRelaySupportCatalog().TryGet(
                SharedProviderConnectorPluginKeys.OpenAi,
                SharedProviderPurpose.Chat,
                out var descriptor));
            PolicyResult = new SharedProviderRelayRequestPolicy().Normalize(
                SharedProviderRelayOperation.Responses,
                payload,
                descriptor.Support);

            var responseBody = JsonSerializer.Serialize(new
            {
                id = "resp_shared_policy",
                @object = "response",
                created_at = 1_785_710_401,
                status = "completed",
                model,
                output = new[]
                {
                    new
                    {
                        id = "msg_shared_policy",
                        type = "message",
                        status = "completed",
                        role = "assistant",
                        content = new[]
                        {
                            new
                            {
                                type = "output_text",
                                text = "accepted",
                                annotations = Array.Empty<object>()
                            }
                        }
                    }
                },
                parallel_tool_calls = false,
                tools = Array.Empty<object>(),
                usage = new
                {
                    input_tokens = 8,
                    output_tokens = 1,
                    total_tokens = 9
                }
            });
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
            };
        }
    }
}
#pragma warning restore OPENAI001, MAAI001
