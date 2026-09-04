using System.Net;
using System.Text;
using System.Text.Json;
using CanDoItAll.Modules.AgentFramework.ProviderManagement;
using CanDoItAll.SharedProviders.Abstractions;
using CanDoItAll.SharedProviders.Http;

namespace CanDoItAll.Tests.Integration.AgentFramework;

public sealed class DirectSharedToolTransportParityIntegrationTests(
    SharedProviderOpenAiCompatibilityFixture fixture) :
    IClassFixture<SharedProviderOpenAiCompatibilityFixture>
{
    [Fact]
    public async Task Complete_nested_function_schema_matches_direct_normalization()
    {
        fixture.OpenHarness.Reset();
        var payload = ChatJson(
            Messages(new { role = "user", content = "Create an architecture asset." }),
            """
            "tools":[{"type":"function","function":{"name":"project_structure_asset_create","description":"Creates an asset.","parameters":{"type":"object","properties":{"projectId":{"type":"string","format":"uuid"},"request":{"type":"object","properties":{"objectType":{"type":"string","enum":["File","ImageAsset","VideoAsset"]},"title":{"type":"string"},"parentNodeKey":{"type":"string"},"sourceWorkspacePath":{"type":["string","null"]}},"required":["objectType","title","parentNodeKey"],"additionalProperties":false}},"required":["projectId","request"],"additionalProperties":false},"strict":true}}],
            "tool_choice":{"type":"function","function":{"name":"project_structure_asset_create"}}
            """);
        var direct = NormalizeDirect(payload);

        using var response = await PostAsync(payload);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var shared = Assert.Single(fixture.OpenHarness.Accepted);
        Assert.Equal(
            Encoding.UTF8.GetString(direct.CanonicalPayloadUtf8.Span),
            Encoding.UTF8.GetString(shared.CanonicalPayloadUtf8.Span));
        using var canonical = JsonDocument.Parse(shared.CanonicalPayloadUtf8);
        var requestSchema = canonical.RootElement
            .GetProperty("tools")[0]
            .GetProperty("function")
            .GetProperty("parameters")
            .GetProperty("properties")
            .GetProperty("request");
        Assert.Contains(
            requestSchema.GetProperty("required").EnumerateArray(),
            item => item.GetString() == "parentNodeKey");
    }

    [Fact]
    public async Task Streamed_tool_call_chunks_preserve_id_name_arguments_and_terminal_marker()
    {
        fixture.OpenHarness.Reset();
        var model = SharedProviderRelayTestData.ChatModelId.Value;
        var stream = new CompletedRelayStream(
        [
            new SharedProviderRelayStreamFrame(
                eventName: null,
                """{"id":"chatcmpl-tool","object":"chat.completion.chunk","model":"__MODEL__","choices":[{"index":0,"delta":{"role":"assistant","tool_calls":[{"index":0,"id":"call_asset","type":"function","function":{"name":"project_structure_asset_create","arguments":"{\"projectId\":\""}}]},"finish_reason":null}]}""".Replace("__MODEL__", model, StringComparison.Ordinal)),
            new SharedProviderRelayStreamFrame(
                eventName: null,
                """{"id":"chatcmpl-tool","object":"chat.completion.chunk","model":"__MODEL__","choices":[{"index":0,"delta":{"tool_calls":[{"index":0,"function":{"arguments":"fixture\"}"}}]},"finish_reason":null}]}""".Replace("__MODEL__", model, StringComparison.Ordinal)),
            new SharedProviderRelayStreamFrame(
                eventName: null,
                """{"id":"chatcmpl-tool","object":"chat.completion.chunk","model":"__MODEL__","choices":[{"index":0,"delta":{},"finish_reason":"tool_calls"}]}""".Replace("__MODEL__", model, StringComparison.Ordinal)),
            new SharedProviderRelayStreamFrame(eventName: null, "[DONE]")
        ],
            new SharedProviderRelayStreamCompletion(SharedProviderRelayUsage.Unavailable));
        fixture.OpenHarness.NextResult = new SharedProviderRelayDispatchResult.Streaming(stream);
        var payload = ChatJson(
            Messages(new { role = "user", content = "Create it." }),
            """
            "stream":true,
            "tools":[{"type":"function","function":{"name":"project_structure_asset_create","parameters":{"type":"object"}}}]
            """);

        using var response = await PostAsync(payload);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"id\":\"call_asset\"", body, StringComparison.Ordinal);
        Assert.Contains("\"name\":\"project_structure_asset_create\"", body, StringComparison.Ordinal);
        Assert.Contains("fixture\\\"}", body, StringComparison.Ordinal);
        Assert.Equal(1, Count(body, "data: [DONE]"));
    }

    [Fact]
    public async Task Sequential_tool_result_preserves_the_correlated_call_id()
    {
        fixture.OpenHarness.Reset();
        var payload = ChatJson(Messages(
            new { role = "user", content = "Create it." },
            new
            {
                role = "assistant",
                content = (string?)null,
                tool_calls = new[]
                {
                    new
                    {
                        id = "call_asset",
                        type = "function",
                        function = new { name = "project_structure_asset_create", arguments = "{}" }
                    }
                }
            },
            new { role = "tool", tool_call_id = "call_asset", content = "created" },
            new { role = "user", content = "Confirm the result." }));
        var direct = NormalizeDirect(payload);

        using var response = await PostAsync(payload);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var shared = Assert.Single(fixture.OpenHarness.Accepted);
        Assert.Equal(
            Encoding.UTF8.GetString(direct.CanonicalPayloadUtf8.Span),
            Encoding.UTF8.GetString(shared.CanonicalPayloadUtf8.Span));
        Assert.Contains("\"tool_call_id\":\"call_asset\"", Encoding.UTF8.GetString(shared.CanonicalPayloadUtf8.Span));
    }

    [Fact]
    public async Task Multiple_supported_tool_calls_require_and_preserve_each_result()
    {
        fixture.OpenHarness.Reset();
        var payload = ChatJson(Messages(
            new { role = "user", content = "Read both records." },
            new
            {
                role = "assistant",
                content = (string?)null,
                tool_calls = new[]
                {
                    new { id = "call_one", type = "function", function = new { name = "read_one", arguments = "{}" } },
                    new { id = "call_two", type = "function", function = new { name = "read_two", arguments = "{}" } }
                }
            },
            new { role = "tool", tool_call_id = "call_one", content = "one" },
            new { role = "tool", tool_call_id = "call_two", content = "two" }));

        using var response = await PostAsync(payload);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var canonical = Encoding.UTF8.GetString(Assert.Single(fixture.OpenHarness.Accepted).CanonicalPayloadUtf8.Span);
        Assert.Contains("\"tool_call_id\":\"call_one\"", canonical);
        Assert.Contains("\"tool_call_id\":\"call_two\"", canonical);
    }

    [Fact]
    public async Task Missing_or_unmatched_tool_result_is_rejected_before_dispatch()
    {
        fixture.OpenHarness.Reset();
        var missingId = ChatJson(Messages(new { role = "tool", content = "orphan" }));
        var unmatchedId = ChatJson(Messages(new { role = "tool", tool_call_id = "call_missing", content = "orphan" }));

        using var missingResponse = await PostAsync(missingId);
        using var unmatchedResponse = await PostAsync(unmatchedId);

        Assert.Equal(HttpStatusCode.BadRequest, missingResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, unmatchedResponse.StatusCode);
        Assert.Equal(0, fixture.OpenHarness.DispatchCount);
        Assert.Empty(fixture.OpenHarness.Accepted);
    }

    [Fact]
    public async Task Upstream_cancellation_and_capability_failures_remain_distinct_and_sanitized()
    {
        fixture.OpenHarness.Reset();
        fixture.OpenHarness.NextResult = Failed(
            SharedProviderFailureCategory.Unavailable,
            "provider_unavailable",
            "The upstream provider is unavailable.");
        using var upstream = await PostAsync(ChatJson(Messages(new { role = "user", content = "Hello" })));
        var upstreamBody = await upstream.Content.ReadAsStringAsync();

        fixture.OpenHarness.NextResult = Failed(
            SharedProviderFailureCategory.Cancelled,
            "provider_cancelled",
            "The provider request was cancelled.");
        using var cancelled = await PostAsync(ChatJson(Messages(new { role = "user", content = "Hello again" })));
        var cancelledBody = await cancelled.Content.ReadAsStringAsync();

        var unsupportedPayload = """
            {"model":"__MODEL__","messages":[{"role":"user","content":"JSON"}],"response_format":{"type":"json_object"}}
            """.Replace(
                "__MODEL__",
                SharedProviderRelayTestData.LimitedChatModelId.Value,
                StringComparison.Ordinal);
        using var capability = await PostAsync(unsupportedPayload);
        var capabilityBody = await capability.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.ServiceUnavailable, upstream.StatusCode);
        Assert.Contains("provider_unavailable", upstreamBody, StringComparison.Ordinal);
        Assert.DoesNotContain("private", upstreamBody, StringComparison.OrdinalIgnoreCase);
        Assert.False(cancelled.IsSuccessStatusCode);
        Assert.Contains("provider_cancelled", cancelledBody, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.BadRequest, capability.StatusCode);
        Assert.Contains("does not support", capabilityBody, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<HttpResponseMessage> PostAsync(string payload)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, SharedProviderRoutes.ChatCompletions)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        return await fixture.OpenHost.Client.SendAsync(request);
    }

    private static SharedProviderRelayNormalizedRequest NormalizeDirect(string payload)
    {
        var result = new SharedProviderRelayRequestPolicy().Normalize(
            SharedProviderRelayOperation.ChatCompletions,
            Encoding.UTF8.GetBytes(payload),
            ChatSupport);
        return Assert.IsType<SharedProviderRelayRequestPolicyResult.Accepted>(result).Request;
    }

    private static string ChatJson(string messages, string? additionalMembers = null)
    {
        var suffix = string.IsNullOrWhiteSpace(additionalMembers)
            ? string.Empty
            : "," + additionalMembers.Trim();
        return $$"""{"model":"{{SharedProviderRelayTestData.ChatModelId.Value}}","messages":{{messages}}{{suffix}}}""";
    }

    private static string Messages(params object[] messages)
    {
        return JsonSerializer.Serialize(messages);
    }

    private static SharedProviderRelayDispatchResult.Failed Failed(
        SharedProviderFailureCategory category,
        string code,
        string message)
    {
        return new SharedProviderRelayDispatchResult.Failed(new SharedProviderFailure(
            category,
            new SharedProviderFailureCode(code),
            message));
    }

    private static int Count(string value, string target)
    {
        var count = 0;
        var index = 0;
        while ((index = value.IndexOf(target, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += target.Length;
        }

        return count;
    }

    private static SharedProviderRelaySupportDescriptor ChatSupport { get; } = new(
        new HashSet<SharedProviderRelayOperation>
        {
            SharedProviderRelayOperation.ChatCompletions,
            SharedProviderRelayOperation.Responses
        },
        SharedProviderStreamingMode.ServerSentEvents,
        supportsFunctionTools: true,
        supportsParallelFunctionTools: true,
        supportsStructuredOutput: true,
        supportsVisionInput: false,
        supportsBase64Images: false,
        maximumRequestBytes: 4 * 1024 * 1024,
        maximumOutputTokens: 4096,
        maximumImageCount: 1);
}
