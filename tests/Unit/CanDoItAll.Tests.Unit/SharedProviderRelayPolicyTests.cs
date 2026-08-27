using System.Net;
using System.Text;
using System.Text.Json;
using CanDoItAll.Modules.AgentFramework.ProviderManagement;
using CanDoItAll.Modules.Workspace;
using CanDoItAll.SharedProviders.Abstractions;
using CanDoItAll.SharedProviders.Http;

namespace CanDoItAll.Tests.Unit;

public sealed class SharedProviderRelayPolicyTests
{
    private static readonly SharedProviderPublicationId PublicationId = new(
        Guid.Parse("2f17e4bd-8078-4ff2-a2e2-f910223027e6"));
    private static readonly SharedProviderRoutingModelId RoutingModelId =
        SharedProviderRoutingModelIdCodec.Create(PublicationId, "upstream-model");

    [Fact]
    public void ChatCompletionsSupportedSubset_NormalizesCanonicalRequest()
    {
        var request = Accept(
            SharedProviderRelayOperation.ChatCompletions,
            $$"""
            {"model":"{{RoutingModelId.Value}}","messages":[{"role":"user","content":"hello"}],"temperature":0.25,"stream":false}
            """);

        Assert.Equal(RoutingModelId, request.RoutingModelId);
        Assert.False(request.Stream);
        Assert.Contains("\"upstream-model\"", RewriteForUpstream(request));
        Assert.DoesNotContain(RoutingModelId.Value, RewriteForUpstream(request), StringComparison.Ordinal);

        var assistantToolCall = Accept(
            SharedProviderRelayOperation.ChatCompletions,
            $$$"""
            {"model":"{{{RoutingModelId.Value}}}","messages":[{"role":"user","content":"weather"},{"role":"assistant","tool_calls":[{"id":"call_1","type":"function","function":{"name":"weather","arguments":"{}"}}]},{"role":"tool","tool_call_id":"call_1","content":"sunny"}]}
            """);
        var assistantNullContentToolCall = Accept(
            SharedProviderRelayOperation.ChatCompletions,
            $$$"""
            {"model":"{{{RoutingModelId.Value}}}","messages":[{"role":"assistant","content":null,"tool_calls":[{"id":"call_1","type":"function","function":{"name":"weather","arguments":"{}"}}]}]}
            """);

        Assert.Contains("\"tool_calls\"", RewriteForUpstream(assistantToolCall));
        Assert.Contains("\"content\":null", RewriteForUpstream(assistantNullContentToolCall));
        AssertValidationFailure(
            SharedProviderRelayOperation.ChatCompletions,
            $$"""{"model":"{{RoutingModelId.Value}}","messages":[{"role":"user"}]}""");
        AssertValidationFailure(
            SharedProviderRelayOperation.ChatCompletions,
            $$"""{"model":"{{RoutingModelId.Value}}","messages":[{"role":"assistant"}]}""");
        AssertValidationFailure(
            SharedProviderRelayOperation.ChatCompletions,
            $$"""{"model":"{{RoutingModelId.Value}}","messages":[{"role":"user","content":null}]}""");
        AssertValidationFailure(
            SharedProviderRelayOperation.ChatCompletions,
            $$"""{"model":"{{RoutingModelId.Value}}","messages":[{"role":"tool","content":"sunny"}]}""");
    }

    [Theory]
    [InlineData("\"none\"")]
    [InlineData("\"minimal\"")]
    [InlineData("\"low\"")]
    [InlineData("\"medium\"")]
    [InlineData("\"high\"")]
    [InlineData("\"xhigh\"")]
    [InlineData("\"max\"")]
    [InlineData("null")]
    public void Chat_reasoning_effort_round_trips_documented_values(string value) {
        var request = Accept(SharedProviderRelayOperation.ChatCompletions,
            ChatJson($"\"reasoning_effort\":{value}"));

        using var upstream = JsonDocument.Parse(RewriteForUpstream(request));
        Assert.Equal(value, upstream.RootElement.GetProperty("reasoning_effort").GetRawText());
    }

    [Theory]
    [InlineData("[\"none\"]")]
    [InlineData("{}")]
    [InlineData("true")]
    [InlineData("1")]
    [InlineData("\"NONE\"")]
    [InlineData("\"bogus\"")]
    [InlineData("\"\"")]
    [InlineData("\" none \"")]
    [InlineData("\"none\",\"reasoning_effort\":\"high\"")]
    public void Chat_reasoning_effort_rejects_invalid_or_duplicate_values(string value) {
        AssertValidationFailure(SharedProviderRelayOperation.ChatCompletions,
            ChatJson($"\"reasoning_effort\":{value}"));
    }

    [Fact]
    public void Chat_reasoning_effort_is_not_accepted_on_other_operations() {
        AssertValidationFailure(SharedProviderRelayOperation.Responses,
            ResponsesJson("\"reasoning_effort\":\"none\""));
        AssertValidationFailure(SharedProviderRelayOperation.ImageGenerations,
            ImagesJson("\"reasoning_effort\":\"none\""), ImageSupport());
    }

    [Fact]
    public void ResponsesSupportedSubset_NormalizesCanonicalRequest()
    {
        var request = Accept(
            SharedProviderRelayOperation.Responses,
            $$"""
            {"model":"{{RoutingModelId.Value}}","input":"hello","instructions":"be concise","max_output_tokens":32}
            """);

        Assert.Equal(SharedProviderRelayOperation.Responses, request.Operation);
        Assert.Contains("\"input\":\"hello\"", Encoding.UTF8.GetString(request.CanonicalPayloadUtf8.Span));

        var messageRequest = Accept(
            SharedProviderRelayOperation.Responses,
            $$"""
            {"model":"{{RoutingModelId.Value}}","input":[{"type":"message","role":"user","content":[{"type":"input_text","text":"hello"}]}]}
            """);
        var functionOutputRequest = Accept(
            SharedProviderRelayOperation.Responses,
            $$"""
            {"model":"{{RoutingModelId.Value}}","input":[{"type":"function_call_output","call_id":"call_1","output":"sunny"}]}
            """);

        Assert.Contains("\"input_text\"", RewriteForUpstream(messageRequest));
        Assert.Contains("\"function_call_output\"", RewriteForUpstream(functionOutputRequest));
        foreach (string item in new[]
        {
            "{}",
            "{\"type\":\"message\"}",
            "{\"type\":\"message\",\"role\":\"user\"}",
            "{\"type\":\"function_call_output\"}",
            "{\"type\":\"function_call_output\",\"role\":\"user\",\"call_id\":\"call_1\",\"output\":\"sunny\"}"
        })
        {
            AssertValidationFailure(
                SharedProviderRelayOperation.Responses,
                $"{{\"model\":\"{RoutingModelId.Value}\",\"input\":[{item}]}}");
        }
    }

    [Fact]
    public void ImagesBase64SupportedSubset_NormalizesCanonicalRequest()
    {
        var request = Accept(
            SharedProviderRelayOperation.ImageGenerations,
            $$"""
            {"model":"{{RoutingModelId.Value}}","prompt":"a blue square","n":2,"size":"1024x1024","response_format":"b64_json","output_format":"png"}
            """,
            ImageSupport());

        Assert.False(request.Stream);
        Assert.Equal(2, request.RequestedImageCount);

        var defaultCountRequest = Accept(
            SharedProviderRelayOperation.ImageGenerations,
            $$"""
            {"model":"{{RoutingModelId.Value}}","prompt":"a blue square","response_format":"b64_json","output_format":"png"}
            """,
            ImageSupport());

        Assert.Equal(1, defaultCountRequest.RequestedImageCount);
    }

    [Fact]
    public void UnknownMembers_AreRejectedOnEverySurface()
    {
        foreach (var (operation, payload, support) in new[]
        {
            (SharedProviderRelayOperation.ChatCompletions, ChatJson("\"endpoint\":\"https://attacker.test/v1\""), ChatSupport()),
            (SharedProviderRelayOperation.Responses, ResponsesJson("\"mystery\":true"), ChatSupport()),
            (SharedProviderRelayOperation.ImageGenerations, ImagesJson("\"private_url\":\"file:///tmp/a.png\""), ImageSupport())
        })
        {
            AssertValidationFailure(operation, payload, support);
        }
    }

    [Fact]
    public void MalformedDuplicateAndExcessivelyDeepJson_AreRejected()
    {
        AssertValidationFailure(SharedProviderRelayOperation.ChatCompletions, "{");
        AssertValidationFailure(
            SharedProviderRelayOperation.ChatCompletions,
            $$"""{"model":"{{RoutingModelId.Value}}","model":"{{RoutingModelId.Value}}","messages":[]}""");
        AssertValidationFailure(
            SharedProviderRelayOperation.Responses,
            $"{{\"model\":\"{RoutingModelId.Value}\",\"input\":{new string('[', 40)}\"x\"{new string(']', 40)}}}");
    }

    [Fact]
    public void BodyMessagesToolsSchemasAndImages_AreBoundedBeforeDispatch()
    {
        AssertValidationFailure(
            SharedProviderRelayOperation.ChatCompletions,
            $"{{\"model\":\"{RoutingModelId.Value}\",\"messages\":[{{\"role\":\"user\",\"content\":\"{new string('x', 1_100_000)}\"}}]}}");
        AssertValidationFailure(
            SharedProviderRelayOperation.ChatCompletions,
            ChatJson($"\"tools\":[{string.Join(',', Enumerable.Repeat("{\"type\":\"function\",\"function\":{\"name\":\"f\",\"parameters\":{}}}", 129))}]"));
        AssertValidationFailure(
            SharedProviderRelayOperation.ImageGenerations,
            ImagesJson("\"n\":5"),
            ImageSupport());
    }

    [Fact]
    public void InvalidGenerationNumbersEnumsAndConflicts_AreRejected()
    {
        AssertValidationFailure(
            SharedProviderRelayOperation.ChatCompletions,
            ChatJson("\"temperature\":-0.1"));
        AssertValidationFailure(
            SharedProviderRelayOperation.ChatCompletions,
            ChatJson("\"max_tokens\":10,\"max_completion_tokens\":10"));
        AssertValidationFailure(
            SharedProviderRelayOperation.ImageGenerations,
            ImagesJson("\"size\":\"123x456\""),
            ImageSupport());
        foreach (var invalidCount in new[]
        {
            "\"2\"",
            "null",
            "1.5",
            "2147483648"
        })
        {
            AssertValidationFailure(
                SharedProviderRelayOperation.ImageGenerations,
                ImagesJson($"\"n\":{invalidCount}"),
                ImageSupport());
        }
    }

    [Fact]
    public void BuiltInAndHostedTools_AreRejected()
    {
        foreach (var toolType in new[]
        {
            "web_search",
            "file_search",
            "mcp",
            "computer_use",
            "code_interpreter"
        })
        {
            AssertValidationFailure(
                SharedProviderRelayOperation.Responses,
                ResponsesJson($"\"tools\":[{{\"type\":\"{toolType}\"}}]"));
        }
    }

    [Fact]
    public void FunctionToolsAndToolChoice_RoundTripCanonically()
    {
        const string chatTools = """
            "tools":[{"type":"function","function":{"name":"weather","description":"Get weather","parameters":{"type":"object","properties":{"city":{"type":"string"}},"required":["city"]},"strict":true}}],"tool_choice":{"type":"function","function":{"name":"weather"}}
            """;
        const string responsesTools = """
            "tools":[{"type":"function","name":"weather","description":"Get weather","parameters":{"type":"object","properties":{"city":{"type":"string"}},"required":["city"]},"strict":true}],"tool_choice":{"type":"function","name":"weather"}
            """;
        var chatRequest = Accept(
            SharedProviderRelayOperation.ChatCompletions,
            ChatJson(chatTools));
        var responsesRequest = Accept(
            SharedProviderRelayOperation.Responses,
            ResponsesJson(responsesTools));
        var minimalResponsesRequest = Accept(
            SharedProviderRelayOperation.Responses,
            ResponsesJson("\"tools\":[{\"type\":\"function\",\"name\":\"weather\"}],\"tool_choice\":{\"type\":\"function\",\"name\":\"weather\"}"));
        string chatUpstream = RewriteForUpstream(chatRequest);
        string responsesUpstream = RewriteForUpstream(responsesRequest);

        Assert.Contains("\"function\":{\"name\":\"weather\"", chatUpstream);
        Assert.Contains("\"tool_choice\"", chatUpstream);
        Assert.Contains("\"type\":\"function\",\"name\":\"weather\"", responsesUpstream);
        Assert.Contains("\"type\":\"function\",\"name\":\"weather\"", RewriteForUpstream(minimalResponsesRequest));
        Assert.DoesNotContain("\"function\":", responsesUpstream, StringComparison.Ordinal);
        AssertValidationFailure(
            SharedProviderRelayOperation.ChatCompletions,
            ChatJson(responsesTools));
        AssertValidationFailure(
            SharedProviderRelayOperation.Responses,
            ResponsesJson(chatTools));
        AssertValidationFailure(
            SharedProviderRelayOperation.ChatCompletions,
            ChatJson("\"tool_choice\":{\"type\":\"function\",\"function\":{\"name\":\"weather\"}}"));
        AssertValidationFailure(
            SharedProviderRelayOperation.Responses,
            ResponsesJson("\"tool_choice\":{\"type\":\"function\",\"name\":\"weather\"}"));
        AssertValidationFailure(
            SharedProviderRelayOperation.ChatCompletions,
            ChatJson("\"tools\":[{\"type\":\"function\",\"function\":{\"name\":\"weather\"}}],\"tool_choice\":{\"type\":\"function\",\"function\":{\"name\":\"forecast\"}}"));
        AssertValidationFailure(
            SharedProviderRelayOperation.Responses,
            ResponsesJson("\"tools\":[{\"type\":\"function\",\"name\":\"weather\",\"parameters\":{},\"strict\":true}],\"tool_choice\":{\"type\":\"function\",\"name\":\"forecast\"}"));
        foreach (string invalidResponsesTool in new[]
        {
            "\"tools\":[{\"type\":\"function\",\"name\":\"weather\",\"parameters\":null}]",
            "\"tools\":[{\"type\":\"function\",\"name\":\"weather\",\"strict\":\"yes\"}]",
            "\"tools\":[{\"type\":\"function\",\"name\":\"weather\",\"description\":null}]",
            "\"tools\":[{\"type\":\"function\",\"name\":null}]"
        })
        {
            AssertValidationFailure(
                SharedProviderRelayOperation.Responses,
                ResponsesJson(invalidResponsesTool));
        }
    }

    [Fact]
    public void ParallelTools_RequireAdvertisedCapability()
    {
        var withoutParallel = new SharedProviderRelaySupportDescriptor(
            ChatSupport().Operations,
            SharedProviderStreamingMode.ServerSentEvents,
            supportsFunctionTools: true,
            supportsParallelFunctionTools: false,
            supportsStructuredOutput: true,
            supportsVisionInput: false,
            supportsBase64Images: false,
            maximumRequestBytes: 2 * 1024 * 1024,
            maximumOutputTokens: 4096,
            maximumImageCount: 1);

        AssertValidationFailure(
            SharedProviderRelayOperation.ChatCompletions,
            ChatJson("\"parallel_tool_calls\":true"),
            withoutParallel);

        var sequential = Accept(
            SharedProviderRelayOperation.ChatCompletions,
            ChatJson("\"parallel_tool_calls\":false"),
            withoutParallel);

        Assert.DoesNotContain(SharedProviderCapability.ParallelFunctionTools, sequential.RequiredCapabilities);

        var accepted = Accept(
            SharedProviderRelayOperation.ChatCompletions,
            ChatJson("\"parallel_tool_calls\":true"),
            ChatSupport());

        Assert.Contains(SharedProviderCapability.ParallelFunctionTools, accepted.RequiredCapabilities);
    }

    [Fact]
    public void StructuredOutput_RequiresAdvertisedCapability()
    {
        var withoutStructured = Support(supportsStructuredOutput: false);
        const string chatSchema = """
            "response_format":{"type":"json_schema","json_schema":{"name":"answer","description":"An answer","schema":{"type":"object"},"strict":true}}
            """;
        const string responsesSchema = """
            "text":{"format":{"type":"json_schema","name":"answer","description":"An answer","schema":{"type":"object"},"strict":true}}
            """;
        string chatPayload = ChatJson(chatSchema);
        string responsesPayload = ResponsesJson(responsesSchema);

        AssertValidationFailure(
            SharedProviderRelayOperation.ChatCompletions,
            chatPayload,
            withoutStructured);
        AssertValidationFailure(
            SharedProviderRelayOperation.Responses,
            responsesPayload,
            withoutStructured);
        Assert.Contains("\"json_schema\":{", RewriteForUpstream(Accept(
            SharedProviderRelayOperation.ChatCompletions,
            chatPayload,
            ChatSupport())));
        Assert.Contains("\"format\":{\"type\":\"json_schema\",\"name\":", RewriteForUpstream(Accept(
            SharedProviderRelayOperation.Responses,
            responsesPayload,
            ChatSupport())));
        AssertValidationFailure(
            SharedProviderRelayOperation.ChatCompletions,
            ChatJson("\"response_format\":{\"type\":\"json_schema\",\"name\":\"answer\",\"schema\":{}}"));
        AssertValidationFailure(
            SharedProviderRelayOperation.Responses,
            ResponsesJson("\"text\":{\"format\":{\"type\":\"json_schema\",\"json_schema\":{\"name\":\"answer\",\"schema\":{}}}}"));
        foreach (string invalidChatSchema in new[]
        {
            "\"response_format\":{\"type\":\"json_schema\",\"json_schema\":{\"name\":\"answer\",\"description\":null,\"schema\":{}}}",
            "\"response_format\":{\"type\":\"json_schema\",\"json_schema\":{\"name\":\"answer\",\"schema\":{},\"strict\":\"yes\"}}"
        })
        {
            AssertValidationFailure(
                SharedProviderRelayOperation.ChatCompletions,
                ChatJson(invalidChatSchema));
        }

        foreach (string invalidResponsesSchema in new[]
        {
            "\"text\":{\"format\":{\"type\":\"json_schema\",\"name\":\"answer\",\"description\":null,\"schema\":{}}}",
            "\"text\":{\"format\":{\"type\":\"json_schema\",\"name\":\"answer\",\"schema\":{},\"strict\":\"yes\"}}"
        })
        {
            AssertValidationFailure(
                SharedProviderRelayOperation.Responses,
                ResponsesJson(invalidResponsesSchema));
        }
    }

    [Fact]
    public void VisionDataUri_RequiresCapabilityAndRejectsUnknownSiblingFields()
    {
        var visionSupport = Support(
            supportsStructuredOutput: true,
            supportsVisionInput: true);
        string chatPayload = $"{{\"model\":\"{RoutingModelId.Value}\",\"messages\":[{{\"role\":\"user\",\"content\":[{{\"type\":\"text\",\"text\":\"describe\"}},{{\"type\":\"image_url\",\"image_url\":{{\"url\":\"data:image/png;base64,iVBORw0KGgo=\",\"detail\":\"high\"}}}}]}}]}}";
        string responsesPayload = $"{{\"model\":\"{RoutingModelId.Value}\",\"input\":[{{\"role\":\"user\",\"content\":[{{\"type\":\"input_image\",\"image_url\":\"data:image/png;base64,iVBORw0KGgo=\"}}]}}]}}";
        string chatWithResponsesShape = $"{{\"model\":\"{RoutingModelId.Value}\",\"messages\":[{{\"role\":\"user\",\"content\":[{{\"type\":\"input_image\",\"image_url\":\"data:image/png;base64,iVBORw0KGgo=\"}}]}}]}}";
        string responsesWithChatShape = $"{{\"model\":\"{RoutingModelId.Value}\",\"input\":[{{\"role\":\"user\",\"content\":[{{\"type\":\"image_url\",\"image_url\":{{\"url\":\"data:image/png;base64,iVBORw0KGgo=\"}}}}]}}]}}";
        string chatWithResponsesText = $"{{\"model\":\"{RoutingModelId.Value}\",\"messages\":[{{\"role\":\"user\",\"content\":[{{\"type\":\"input_text\",\"text\":\"describe\"}}]}}]}}";
        string responsesWithChatText = $"{{\"model\":\"{RoutingModelId.Value}\",\"input\":[{{\"role\":\"user\",\"content\":[{{\"type\":\"text\",\"text\":\"describe\"}}]}}]}}";
        string chatWithUnknownSibling = chatPayload.Replace(
            "\"image_url\":{\"url\":",
            "\"file_id\":\"file-private\",\"image_url\":{\"url\":",
            StringComparison.Ordinal);
        string responsesWithUnknownSibling = responsesPayload.Replace(
            "\"image_url\":\"data:",
            "\"file_id\":\"file-private\",\"image_url\":\"data:",
            StringComparison.Ordinal);

        AssertValidationFailure(SharedProviderRelayOperation.ChatCompletions, chatPayload, ChatSupport());
        Assert.IsType<SharedProviderRelayRequestPolicyResult.Accepted>(Normalize(
            SharedProviderRelayOperation.ChatCompletions,
            chatPayload,
            visionSupport));
        Assert.IsType<SharedProviderRelayRequestPolicyResult.Accepted>(Normalize(
            SharedProviderRelayOperation.Responses,
            responsesPayload,
            visionSupport));
        AssertValidationFailure(
            SharedProviderRelayOperation.ChatCompletions,
            chatWithUnknownSibling,
            visionSupport);
        AssertValidationFailure(
            SharedProviderRelayOperation.Responses,
            responsesWithUnknownSibling,
            visionSupport);
        AssertValidationFailure(
            SharedProviderRelayOperation.ChatCompletions,
            chatWithResponsesShape,
            visionSupport);
        AssertValidationFailure(
            SharedProviderRelayOperation.Responses,
            responsesWithChatShape,
            visionSupport);
        AssertValidationFailure(
            SharedProviderRelayOperation.ChatCompletions,
            chatWithResponsesText,
            visionSupport);
        AssertValidationFailure(
            SharedProviderRelayOperation.Responses,
            responsesWithChatText,
            visionSupport);
        foreach (string invalidDetail in new[] { "null", "\"ultra\"", "{}" })
        {
            AssertValidationFailure(
                SharedProviderRelayOperation.ChatCompletions,
                chatPayload.Replace("\"high\"", invalidDetail, StringComparison.Ordinal),
                visionSupport);
        }

        foreach (string invalidDataUri in new[]
        {
            "data:image/png;base64,",
            "data:image/png;base64,%%%"
        })
        {
            AssertValidationFailure(
                SharedProviderRelayOperation.ChatCompletions,
                chatPayload.Replace(
                    "data:image/png;base64,iVBORw0KGgo=",
                    invalidDataUri,
                    StringComparison.Ordinal),
                visionSupport);
            AssertValidationFailure(
                SharedProviderRelayOperation.Responses,
                responsesPayload.Replace(
                    "data:image/png;base64,iVBORw0KGgo=",
                    invalidDataUri,
                    StringComparison.Ordinal),
                visionSupport);
        }

        foreach (string role in new[] { "system", "developer", "assistant", "tool" })
        {
            string toolCallId = role == "tool" ? ",\"tool_call_id\":\"call_1\"" : string.Empty;
            string payload = $"{{\"model\":\"{RoutingModelId.Value}\",\"messages\":[{{\"role\":\"{role}\",\"content\":[{{\"type\":\"image_url\",\"image_url\":{{\"url\":\"data:image/png;base64,iVBORw0KGgo=\"}}}}]{toolCallId}}}]}}";

            AssertValidationFailure(
                SharedProviderRelayOperation.ChatCompletions,
                payload,
                visionSupport);
        }
    }

    [Fact]
    public void StoreFalse_IsAcceptedWhilePersistenceAndUnsupportedServerFeaturesAreRejected()
    {
        var accepted = Assert.IsType<SharedProviderRelayRequestPolicyResult.Accepted>(Normalize(
            SharedProviderRelayOperation.Responses,
            ResponsesJson("\"store\":false"),
            ChatSupport()));
        using (var document = JsonDocument.Parse(accepted.Request.CanonicalPayloadUtf8))
        {
            Assert.Equal(JsonValueKind.False, document.RootElement.GetProperty("store").ValueKind);
        }

        var omittedStore = Assert.IsType<SharedProviderRelayRequestPolicyResult.Accepted>(Normalize(
            SharedProviderRelayOperation.Responses,
            ResponsesJson("\"temperature\":1"),
            ChatSupport()));
        using (var document = JsonDocument.Parse(omittedStore.Request.CanonicalPayloadUtf8))
        {
            Assert.Equal(JsonValueKind.False, document.RootElement.GetProperty("store").ValueKind);
        }

        foreach (string member in new[]
        {
            "\"store\":true",
            "\"store\":null",
            "\"store\":\"false\"",
            "\"background\":true",
            "\"conversation\":\"conv_private\"",
            "\"include\":[\"file_search_call.results\"]",
            "\"user\":\"caller-controlled\"",
            "\"modalities\":[\"audio\"]"
        })
        {
            AssertValidationFailure(
                SharedProviderRelayOperation.Responses,
                ResponsesJson(member));
        }
    }

    [Fact]
    public void ChatStreamOptions_AcceptsIncludeUsageAndRejectsMalformedOrUnknownOptions()
    {
        var accepted = Assert.IsType<SharedProviderRelayRequestPolicyResult.Accepted>(Normalize(
            SharedProviderRelayOperation.ChatCompletions,
            ChatJson("\"stream\":true,\"stream_options\":{\"include_usage\":true}"),
            ChatSupport()));
        using (var document = JsonDocument.Parse(accepted.Request.CanonicalPayloadUtf8))
        {
            Assert.True(document.RootElement
                .GetProperty("stream_options")
                .GetProperty("include_usage")
                .GetBoolean());
        }

        foreach (var member in new[]
        {
            "\"stream_options\":{\"include_usage\":true}",
            "\"stream\":true,\"stream_options\":null",
            "\"stream\":true,\"stream_options\":{}",
            "\"stream\":true,\"stream_options\":{\"include_usage\":\"true\"}",
            "\"stream\":true,\"stream_options\":{\"include_usage\":true,\"unknown\":true}"
        })
        {
            AssertValidationFailure(
                SharedProviderRelayOperation.ChatCompletions,
                ChatJson(member),
                ChatSupport());
        }
    }

    [Fact]
    public void MalformedRoutingModelId_FailsClosed()
    {
        var result = Normalize(
            SharedProviderRelayOperation.ChatCompletions,
            "{\"model\":\"../../private\",\"messages\":[{\"role\":\"user\",\"content\":\"hello\"}]}",
            ChatSupport());

        var rejected = Assert.IsType<SharedProviderRelayRequestPolicyResult.Rejected>(result);
        Assert.Equal(SharedProviderFailureCategory.NotFound, rejected.Failure.Category);
    }

    [Fact]
    public void DuplicateUpstreamNames_RemainSeparatedByPublication()
    {
        var otherPublication = new SharedProviderPublicationId(
            Guid.Parse("1b359d95-20e3-4451-b953-38cc7dc3cd55"));

        Assert.NotEqual(
            RoutingModelId,
            SharedProviderRoutingModelIdCodec.Create(otherPublication, "upstream-model"));
    }

    [Fact]
    public void RelayTarget_RechecksPurposeOperationAndCapabilities()
    {
        var target = CreateTarget(ChatSupport());

        Assert.True(target.Supports(SharedProviderRelayOperation.ChatCompletions));
        Assert.False(target.Supports(SharedProviderRelayOperation.ImageGenerations));
        Assert.Equal(SharedProviderPurpose.Chat, target.Purpose);
    }

    [Fact]
    public void RelayTarget_UsesOnlyStoredUriModelTimeoutAndCredential()
    {
        var credential = new SharedProviderRelayCredential("central-secret-value");
        var target = CreateTarget(ChatSupport(), credential);

        Assert.Equal(new Uri("https://central.example.test/proxy/v1"), target.BaseUri);
        Assert.Equal("upstream-model", target.UpstreamModelId);
        Assert.Equal(TimeSpan.FromSeconds(45), target.Timeout);
        Assert.Equal("[REDACTED]", credential.ToString());
        Assert.Equal(20, credential.UseValue(value => value.Length));
    }

    [Fact]
    public void Registry_ContainsExactlyFiveProductionRowsAndNoSyntheticAdapter()
    {
        var descriptors = new SharedProviderRelaySupportCatalog().List();

        Assert.Equal(5, descriptors.Count);
        Assert.All(descriptors, descriptor =>
            Assert.Equal(SharedProviderRelayAdapterClassification.Production, descriptor.Classification));
        Assert.DoesNotContain(descriptors, descriptor =>
            descriptor.ConnectorPluginKey.Contains("scenario", StringComparison.OrdinalIgnoreCase) ||
            descriptor.ConnectorPluginKey.Contains("mock", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Registry_OpenAiChatSupportsVisionInput()
    {
        var catalog = new SharedProviderRelaySupportCatalog();

        Assert.True(catalog.TryGet(
            "provider.openai",
            SharedProviderPurpose.Chat,
            out var descriptor));
        Assert.True(descriptor.Support.SupportsVisionInput);
    }

    [Fact]
    public void TargetNormalization_PreservesBasePathAndRejectsCallerUriOrHeaders()
    {
        var target = CreateTarget(ChatSupport());

        Assert.Equal(
            new Uri("https://central.example.test/proxy/v1/chat/completions"),
            SharedProviderRelayUriPolicy.Resolve(target, SharedProviderRelayOperation.ChatCompletions));
        AssertValidationFailure(
            SharedProviderRelayOperation.ChatCompletions,
            ChatJson("\"headers\":{\"Authorization\":\"attacker\"}"));
    }

    [Fact]
    public void UpstreamFailures_AreSanitizedAndRetryAfterIsBounded()
    {
        var failure = SharedProviderRelayFailureMapper.FromUpstream(
            HttpStatusCode.TooManyRequests,
            "999999999",
            "secret at http://10.0.0.4/private");

        Assert.Equal(SharedProviderFailureCategory.RateLimited, failure.Category);
        Assert.Equal(SharedProviderFailure.MaximumRetryAfter, failure.RetryAfter);
        Assert.DoesNotContain("secret", failure.SanitizedMessage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("10.0.0.4", failure.SanitizedMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void ResponseHeaderAllowlist_StripsUnsafeAndPrivateHeaders()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.OK);
        response.Headers.TryAddWithoutValidation("x-request-id", "upstream-request");
        response.Headers.TryAddWithoutValidation("retry-after", "3");
        response.Headers.TryAddWithoutValidation("set-cookie", "session=private");
        response.Headers.TryAddWithoutValidation("location", "http://10.0.0.4/private");
        response.Headers.TryAddWithoutValidation("server", "private-stack");

        var headers = SharedProviderRelayResponseHeaderPolicy.Project(response);

        Assert.Equal("upstream-request", headers.UpstreamRequestId);
        Assert.Equal(TimeSpan.FromSeconds(3), headers.RetryAfter);
        Assert.DoesNotContain("private", headers.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BufferedChatUsage_MapsCompletePartialAndUnavailableTruthfully()
    {
        Assert.Equal(
            SharedProviderRelayUsageCompleteness.Complete,
            SharedProviderRelayUsageExtractor.ExtractBuffered(
                SharedProviderRelayOperation.ChatCompletions,
                "{\"usage\":{\"prompt_tokens\":2,\"completion_tokens\":3}}"u8).Completeness);
        Assert.Equal(
            SharedProviderRelayUsageCompleteness.Partial,
            SharedProviderRelayUsageExtractor.ExtractBuffered(
                SharedProviderRelayOperation.ChatCompletions,
                "{\"usage\":{\"prompt_tokens\":2}}"u8).Completeness);
        var unavailable = SharedProviderRelayUsageExtractor.ExtractBuffered(
            SharedProviderRelayOperation.ChatCompletions,
            "{\"id\":\"chatcmpl-1\"}"u8);
        Assert.Equal(SharedProviderRelayUsageCompleteness.Unavailable, unavailable.Completeness);
        Assert.Null(unavailable.InputTokens);
        Assert.Null(unavailable.OutputTokens);
    }

    [Fact]
    public void ResponsesAndTerminalSseUsage_MapWithoutInventingZero()
    {
        var responseUsage = SharedProviderRelayUsageExtractor.ExtractBuffered(
            SharedProviderRelayOperation.Responses,
            "{\"usage\":{\"input_tokens\":4,\"output_tokens\":5}}"u8);
        var streamUsage = SharedProviderRelayUsageExtractor.ExtractServerSentEvents(
            SharedProviderRelayOperation.Responses,
            [new SharedProviderRelayStreamFrame(
                "response.completed",
                "{\"type\":\"response.completed\",\"response\":{\"usage\":{\"input_tokens\":7,\"output_tokens\":8}}}")]);

        Assert.Equal((4L, 5L), (responseUsage.InputTokens, responseUsage.OutputTokens));
        Assert.Equal((7L, 8L), (streamUsage.InputTokens, streamUsage.OutputTokens));
        var imageUsage = SharedProviderRelayUsageExtractor.ExtractBuffered(
            SharedProviderRelayOperation.ImageGenerations,
            "{\"data\":[{\"b64_json\":\"AA==\"},{\"b64_json\":\"AQ==\"}]}"u8);
        var unavailableImageUsage = SharedProviderRelayUsageExtractor.ExtractBuffered(
            SharedProviderRelayOperation.ImageGenerations,
            "{\"data\":[]}"u8);
        Assert.Equal(2, imageUsage.ImageCount);
        Assert.Equal(SharedProviderRelayUsageCompleteness.Complete, imageUsage.Completeness);
        Assert.Equal(
            SharedProviderRelayUsageCompleteness.Unavailable,
            unavailableImageUsage.Completeness);
        Assert.Throws<ArgumentOutOfRangeException>(() => new SharedProviderRelayUsage(
            inputTokens: null,
            outputTokens: null,
            imageCount: 0,
            SharedProviderRelayUsageCompleteness.Complete));
        Assert.Throws<ArgumentException>(() => new SharedProviderRelayUsage(
            inputTokens: null,
            outputTokens: null,
            imageCount: 1,
            SharedProviderRelayUsageCompleteness.Partial));
        Assert.Throws<ArgumentException>(() => new SharedProviderRelayUsage(
            inputTokens: 1,
            outputTokens: null,
            imageCount: 1,
            SharedProviderRelayUsageCompleteness.Complete));
        Assert.Throws<ArgumentException>(() => new SharedProviderRelayUsage(
            inputTokens: 1,
            outputTokens: null,
            imageCount: null,
            SharedProviderRelayUsageCompleteness.Complete));
        Assert.Throws<ArgumentException>(() => new SharedProviderRelayUsage(
            inputTokens: 1,
            outputTokens: 2,
            imageCount: null,
            SharedProviderRelayUsageCompleteness.Partial));
    }

    [Fact]
    public void InvocationAuditTransition_StoresMetadataOnlyAndRecoversInterruptedFinalizationIdempotently()
    {
        var record = SharedProviderInvocationTransitions.Create(
            "request-1",
            PublicationId,
            Guid.Parse("c28d00e8-e7df-4653-83f8-f13bb714a1e2"),
            "subject-1",
            new AccessContextReference("context-1"),
            "trace-1",
            "correlation-1",
            SharedProviderRelayOperation.ChatCompletions,
            RoutingModelId,
            "upstream-model",
            DateTimeOffset.Parse("2026-08-25T00:00:00Z"),
            DateTimeOffset.Parse("2026-09-24T00:00:00Z"));

        SharedProviderInvocationTransitions.Finalize(
            record,
            new SharedProviderInvocationCompletion(
                SharedProviderInvocationOutcome.Succeeded,
                DateTimeOffset.Parse("2026-08-25T00:00:01Z"),
                FailureCategory: null,
                InputTokenCount: 2,
                OutputTokenCount: 3,
                SharedProviderMetadataCompleteness.Complete,
                Price: null,
                SharedProviderMetadataCompleteness.Unavailable));

        Assert.Equal(SharedProviderInvocationOutcome.Succeeded, record.Outcome);
        Assert.Equal(SharedProviderMetadataCompleteness.Complete, record.UsageCompleteness);
        Assert.DoesNotContain("prompt", string.Join('|', record.RequestId, record.TraceId, record.CorrelationId));
        Assert.False(SharedProviderInvocationTransitions.RecoverInterruptedFinalization(
            record,
            DateTimeOffset.Parse("2026-08-25T00:20:00Z")));
        Assert.Equal(SharedProviderInvocationOutcome.Succeeded, record.Outcome);

        var interrupted = SharedProviderInvocationTransitions.Create(
            "request-interrupted",
            PublicationId,
            Guid.Parse("c28d00e8-e7df-4653-83f8-f13bb714a1e2"),
            "subject-1",
            new AccessContextReference("context-1"),
            "trace-1",
            "correlation-1",
            SharedProviderRelayOperation.ChatCompletions,
            RoutingModelId,
            "upstream-model",
            DateTimeOffset.Parse("2026-08-25T00:00:00Z"),
            DateTimeOffset.Parse("2026-09-24T00:00:00Z"));

        Assert.True(SharedProviderInvocationTransitions.RecoverInterruptedFinalization(
            interrupted,
            DateTimeOffset.Parse("2026-08-25T00:20:00Z")));
        Assert.Equal(SharedProviderInvocationOutcome.Failed, interrupted.Outcome);
        Assert.Equal(SharedProviderFailureCategory.Unavailable, interrupted.FailureCategory);
        Assert.Equal(SharedProviderMetadataCompleteness.Unavailable, interrupted.UsageCompleteness);
        Assert.Null(interrupted.InputTokenCount);
        Assert.Null(interrupted.OutputTokenCount);
        Assert.Null(interrupted.ImageCount);
        Assert.False(SharedProviderInvocationTransitions.RecoverInterruptedFinalization(
            interrupted,
            DateTimeOffset.Parse("2026-08-25T00:21:00Z")));

        var imageRecord = CreateInvocationRecord(
            "request-image",
            SharedProviderRelayOperation.ImageGenerations);
        var imageCompletion = new SharedProviderInvocationCompletion(
            SharedProviderInvocationOutcome.Succeeded,
            DateTimeOffset.Parse("2026-08-25T00:00:01Z"),
            FailureCategory: null,
            InputTokenCount: null,
            OutputTokenCount: null,
            SharedProviderMetadataCompleteness.Complete,
            Price: null,
            SharedProviderMetadataCompleteness.Unavailable)
        {
            ImageCount = 2
        };

        SharedProviderInvocationTransitions.Finalize(imageRecord, imageCompletion);
        SharedProviderInvocationTransitions.Finalize(imageRecord, imageCompletion);

        Assert.Equal(2, imageRecord.ImageCount);
        Assert.Throws<InvalidOperationException>(() => SharedProviderInvocationTransitions.Finalize(
            imageRecord,
            imageCompletion with { ImageCount = 3 }));
        Assert.Throws<ArgumentException>(() => SharedProviderInvocationTransitions.Finalize(
            CreateInvocationRecord("request-chat-image", SharedProviderRelayOperation.ChatCompletions),
            imageCompletion));
        Assert.Throws<ArgumentException>(() => SharedProviderInvocationTransitions.Finalize(
            CreateInvocationRecord("request-image-tokens", SharedProviderRelayOperation.ImageGenerations),
            new SharedProviderInvocationCompletion(
                SharedProviderInvocationOutcome.Succeeded,
                DateTimeOffset.Parse("2026-08-25T00:00:01Z"),
                FailureCategory: null,
                InputTokenCount: 2,
                OutputTokenCount: 3,
                SharedProviderMetadataCompleteness.Complete,
                Price: null,
                SharedProviderMetadataCompleteness.Unavailable)));

        var interruptedImage = CreateInvocationRecord(
            "request-interrupted-image",
            SharedProviderRelayOperation.ImageGenerations);
        Assert.True(SharedProviderInvocationTransitions.RecoverInterruptedFinalization(
            interruptedImage,
            DateTimeOffset.Parse("2026-08-25T00:20:00Z")));
        Assert.Null(interruptedImage.ImageCount);
    }

    private static SharedProviderInvocationRecord CreateInvocationRecord(
        string requestId,
        SharedProviderRelayOperation operation)
        => SharedProviderInvocationTransitions.Create(
            requestId,
            PublicationId,
            Guid.Parse("c28d00e8-e7df-4653-83f8-f13bb714a1e2"),
            "subject-1",
            new AccessContextReference("context-1"),
            "trace-1",
            "correlation-1",
            operation,
            RoutingModelId,
            "upstream-model",
            DateTimeOffset.Parse("2026-08-25T00:00:00Z"),
            DateTimeOffset.Parse("2026-09-24T00:00:00Z"));

    private static SharedProviderRelayNormalizedRequest Accept(
        SharedProviderRelayOperation operation,
        string payload,
        SharedProviderRelaySupportDescriptor? support = null)
    {
        var accepted = Assert.IsType<SharedProviderRelayRequestPolicyResult.Accepted>(
            Normalize(operation, payload, support ?? ChatSupport()));
        return accepted.Request;
    }

    private static SharedProviderRelayRequestPolicyResult Normalize(
        SharedProviderRelayOperation operation,
        string payload,
        SharedProviderRelaySupportDescriptor support)
        => new SharedProviderRelayRequestPolicy().Normalize(
            operation,
            Encoding.UTF8.GetBytes(payload),
            support);

    private static void AssertValidationFailure(
        SharedProviderRelayOperation operation,
        string payload,
        SharedProviderRelaySupportDescriptor? support = null)
    {
        var rejected = Assert.IsType<SharedProviderRelayRequestPolicyResult.Rejected>(
            Normalize(operation, payload, support ?? ChatSupport()));
        Assert.Contains(
            rejected.Failure.Category,
            new[] { SharedProviderFailureCategory.Validation, SharedProviderFailureCategory.NotFound });
    }

    private static string RewriteForUpstream(SharedProviderRelayNormalizedRequest request)
        => Encoding.UTF8.GetString(request.CreateUpstreamPayload("upstream-model").Span);

    private static SharedProviderRelayTarget CreateTarget(
        SharedProviderRelaySupportDescriptor support,
        SharedProviderRelayCredential? credential = null)
        => new(
            PublicationId,
            Guid.Parse("c28d00e8-e7df-4653-83f8-f13bb714a1e2"),
            "provider.openai",
            SharedProviderPurpose.Chat,
            new Uri("https://central.example.test/proxy/v1"),
            "upstream-model",
            RoutingModelId,
            TimeSpan.FromSeconds(45),
            "{}",
            credential,
            support);

    private static SharedProviderRelaySupportDescriptor ChatSupport()
        => Support(supportsStructuredOutput: true);

    private static SharedProviderRelaySupportDescriptor Support(
        bool supportsStructuredOutput,
        bool supportsVisionInput = false)
        => new(
            new HashSet<SharedProviderRelayOperation>
            {
                SharedProviderRelayOperation.ChatCompletions,
                SharedProviderRelayOperation.Responses
            },
            SharedProviderStreamingMode.ServerSentEvents,
            supportsFunctionTools: true,
            supportsParallelFunctionTools: true,
            supportsStructuredOutput,
            supportsVisionInput,
            supportsBase64Images: false,
            maximumRequestBytes: 4 * 1024 * 1024,
            maximumOutputTokens: 4096,
            maximumImageCount: 1);

    private static SharedProviderRelaySupportDescriptor ImageSupport()
        => new(
            new HashSet<SharedProviderRelayOperation>
            {
                SharedProviderRelayOperation.ImageGenerations
            },
            SharedProviderStreamingMode.None,
            supportsFunctionTools: false,
            supportsParallelFunctionTools: false,
            supportsStructuredOutput: false,
            supportsVisionInput: false,
            supportsBase64Images: true,
            maximumRequestBytes: 1024 * 1024,
            maximumOutputTokens: 1,
            maximumImageCount: 4);

    private static string ChatJson(string extra)
        => $$"""
        {"model":"{{RoutingModelId.Value}}","messages":[{"role":"user","content":"hello"}],{{extra}}}
        """;

    private static string ResponsesJson(string extra)
        => $$"""
        {"model":"{{RoutingModelId.Value}}","input":"hello",{{extra}}}
        """;

    private static string ImagesJson(string extra)
        => $$"""
        {"model":"{{RoutingModelId.Value}}","prompt":"a blue square",{{extra}}}
        """;
}
