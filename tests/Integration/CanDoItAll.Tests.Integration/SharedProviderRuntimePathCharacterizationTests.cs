using System.ClientModel;
using System.ClientModel.Primitives;
using System.Net;
using System.Text;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Providers;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.Workspace;
using OpenAI;
using OpenAI.Chat;
using OpenAI.Responses;

namespace CanDoItAll.Tests.Integration;

using AgentFrameworkProviderProfile = CanDoItAll.AgentFramework.Models.ProviderProfile;
using AgentFrameworkProviderKind = CanDoItAll.AgentFramework.Models.ProviderKind;
using WorkspaceProviderProfile = CanDoItAll.Modules.Workspace.ProviderProfile;

#pragma warning disable OPENAI001
public sealed class SharedProviderRuntimePathCharacterizationTests
{
    [Fact]
    public async Task Workspace_openai_profile_supports_sdk_chat_responses_and_streaming_at_custom_endpoint()
    {
        var mapper = CreateMapper();
        var endpoint = "https://relay.example.test/custom/v1";
        var provider = CreateWorkspaceProvider(
            OpenAiProviderAdapter.PluginKey,
            AgentFrameworkProviderKind.OpenAi,
            ProviderTransportKind.Responses,
            ProviderProfilePurpose.Chat,
            endpoint);

        var mapped = mapper.Map(provider);

        Assert.Equal(AgentFrameworkProviderKind.OpenAi, mapped.Kind);
        Assert.Equal(ProviderTransportKind.Responses, mapped.Transport);
        Assert.Equal(ProviderProfilePurpose.Chat, mapped.Purpose);
        Assert.Equal(endpoint, mapped.BaseUrl);

        var handler = new OpenAiSdkTransportHandler();
        using var httpClient = new HttpClient(handler);
        var client = new OpenAIClient(
            new ApiKeyCredential("test-key"),
            new OpenAIClientOptions
            {
                Endpoint = new Uri(mapped.BaseUrl),
                Transport = new HttpClientPipelineTransport(httpClient)
            });
        var chatClient = client.GetChatClient(mapped.DefaultModel);
        var responsesClient = client.GetResponsesClient();

        var chat = await chatClient.CompleteChatAsync([new UserChatMessage("chat normal")]);
        var streamedChatText = new StringBuilder();
        var sawChatCompletion = false;
        await foreach (var update in chatClient.CompleteChatStreamingAsync([new UserChatMessage("chat stream")]))
        {
            foreach (var part in update.ContentUpdate)
            {
                streamedChatText.Append(part.Text);
            }

            sawChatCompletion |= update.FinishReason == ChatFinishReason.Stop;
        }

        var responseOptions = CreateResponseOptions(mapped.DefaultModel, "response normal", streaming: false);
        var response = await responsesClient.CreateResponseAsync(responseOptions);
        var streamedResponseText = new StringBuilder();
        var responseUpdateTypes = new List<string>();
        var streamingResponseOptions = CreateResponseOptions(mapped.DefaultModel, "response stream", streaming: true);
        await foreach (var update in responsesClient.CreateResponseStreamingAsync(streamingResponseOptions))
        {
            responseUpdateTypes.Add(update.GetType().Name);
            if (update is StreamingResponseOutputTextDeltaUpdate delta)
            {
                streamedResponseText.Append(delta.Delta);
            }
        }

        Assert.Equal("chat normal", chat.Value.Content[0].Text);
        Assert.Equal("chat stream", streamedChatText.ToString());
        Assert.True(sawChatCompletion);
        Assert.Equal("response normal", response.Value.GetOutputText());
        Assert.Equal("response stream", streamedResponseText.ToString());
        Assert.Equal([nameof(StreamingResponseOutputTextDeltaUpdate)], responseUpdateTypes);
        Assert.Collection(
            handler.Requests,
            request => AssertSdkRequest(request, "/custom/v1/chat/completions", streaming: false),
            request => AssertSdkRequest(request, "/custom/v1/chat/completions", streaming: true),
            request => AssertSdkRequest(request, "/custom/v1/responses", streaming: false),
            request => AssertSdkRequest(request, "/custom/v1/responses", streaming: true));
    }

    [Fact]
    public void Workspace_azure_profile_roundtrips_through_openai_connector_metadata()
    {
        var mapper = CreateMapper();
        var endpoint = "https://contoso.openai.azure.com";
        var provider = CreateWorkspaceProvider(
            OpenAiProviderAdapter.PluginKey,
            AgentFrameworkProviderKind.AzureOpenAi,
            ProviderTransportKind.Responses,
            ProviderProfilePurpose.Chat,
            endpoint);

        var mapped = mapper.Map(provider);

        Assert.Equal(OpenAiProviderAdapter.PluginKey, provider.ConnectorPluginKey);
        Assert.Equal(AgentFrameworkProviderKind.AzureOpenAi, mapped.Kind);
        Assert.Equal(endpoint, mapped.BaseUrl);
    }

    [Fact]
    public void Workspace_comfyui_profile_maps_to_image_generation()
    {
        var mapper = CreateMapper();
        var provider = CreateWorkspaceProvider(
            ComfyUiProviderAdapter.PluginKey,
            AgentFrameworkProviderKind.ComfyUi,
            ProviderTransportKind.ChatCompletions,
            ProviderProfilePurpose.ImageGeneration,
            "http://127.0.0.1:8188");

        var mapped = mapper.Map(provider);

        Assert.Equal(AgentFrameworkProviderKind.ComfyUi, mapped.Kind);
        Assert.Equal(ProviderProfilePurpose.ImageGeneration, mapped.Purpose);
        Assert.Equal(ProviderTransportKind.ChatCompletions, mapped.Transport);
    }

    [Fact]
    public void Workspace_registry_exposes_the_six_production_connector_manifests()
    {
        var registry = new ProviderRegistry(
        [
            new OpenAiProviderAdapter(new FixedHttpClientFactory()),
            new ScenarioHarnessProviderAdapter(),
            new ProcessMockProviderAdapter(),
            new ComfyUiProviderAdapter(new FixedHttpClientFactory()),
            new OllamaProviderAdapter(new FixedHttpClientFactory()),
            new OllamaRemoteProviderAdapter(new FixedHttpClientFactory())
        ]);

        var pluginKeys = registry.ListManifests()
            .Select(manifest => manifest.PluginKey)
            .OrderBy(pluginKey => pluginKey, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
        [
            ComfyUiProviderAdapter.PluginKey,
            OllamaProviderAdapter.PluginKey,
            OllamaRemoteProviderAdapter.PluginKey,
            OpenAiProviderAdapter.PluginKey,
            ProcessMockProviderAdapter.PluginKey,
            ScenarioHarnessProviderAdapter.PluginKey
        ], pluginKeys);
        Assert.DoesNotContain(pluginKeys, pluginKey => pluginKey.Contains("azure", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Integrated_host_registration_replaces_the_legacy_workspace_gateway()
    {
        var root = FindRepositoryRoot();
        var composition = File.ReadAllText(Path.Combine(
            root,
            "src/App/CanDoItAll.Composition/RuntimeHostServiceCollectionExtensions.cs"));
        var workspaceRegistration = File.ReadAllText(Path.Combine(
            root,
            "src/Modules/CanDoItAll.Modules.Workspace/Services/WorkspaceModuleServiceCollectionExtensions.cs"));
        var agentFrameworkRegistration = File.ReadAllText(Path.Combine(
            root,
            "src/Modules/CanDoItAll.Modules.AgentFramework/Services/AgentFrameworkModuleServiceCollectionExtensions.cs"));

        var workspaceIndex = composition.IndexOf("services.AddWorkspaceModule()", StringComparison.Ordinal);
        var agentFrameworkIndex = composition.IndexOf("services.AddAgentFrameworkModule(configuration)", StringComparison.Ordinal);

        Assert.True(workspaceIndex >= 0);
        Assert.True(agentFrameworkIndex > workspaceIndex);
        Assert.Contains("TryAddScoped<IProviderRuntimeGateway>", workspaceRegistration, StringComparison.Ordinal);
        Assert.Contains(
            "AddScoped<IProviderRuntimeGateway, AgentFrameworkProviderRuntimeGateway>",
            agentFrameworkRegistration,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Openai_image_driver_preserves_a_custom_path_prefix()
    {
        var handler = new CapturingHandler();
        using var httpClient = new HttpClient(handler);
        var driver = new OpenAiProviderDriver(httpClient, new FixedCredentialResolver());
        var provider = new AgentFrameworkProviderProfile(
            Guid.NewGuid(),
            "Custom relay",
            AgentFrameworkProviderKind.OpenAi,
            "https://relay.example.test/custom/v1",
            "TEST_API_KEY",
            "gpt-image-test",
            ProviderTransportKind.Responses,
            IsEnabled: true,
            SupportsStreaming: true,
            SupportsTools: false,
            PreferFrameworkManagedChatHistory: false,
            SupportsBackgroundResponses: false,
            ConfigurationJson: "{}",
            Notes: string.Empty,
            HealthStatus: "Not checked",
            LastCheckedAtUtc: null,
            SuggestedModels: ["gpt-image-test"],
            Purpose: ProviderProfilePurpose.ImageGeneration);

        var result = await driver.GenerateImageAsync(new ProviderImageGenerationRequest(
            provider,
            provider.DefaultModel,
            "draw a cube",
            "1024x1024",
            "standard",
            ProviderGeneratedImageFormat.Png,
            []));

        Assert.Single(result.Images);
        Assert.Equal("/custom/v1/images/generations", handler.PathAndQuery);
        Assert.Equal("Bearer test-key", handler.Authorization);
    }

    private static WorkspaceAgentProviderProfileMapper CreateMapper()
    {
        var httpClientFactory = new FixedHttpClientFactory();
        var registry = new ProviderRegistry(
        [
            new OpenAiProviderAdapter(httpClientFactory),
            new ComfyUiProviderAdapter(httpClientFactory)
        ]);
        return new WorkspaceAgentProviderProfileMapper(registry, new ProviderProfileService());
    }

    private static CreateResponseOptions CreateResponseOptions(
        string model,
        string prompt,
        bool streaming)
    {
        var options = new CreateResponseOptions
        {
            Model = model,
            StreamingEnabled = streaming
        };
        options.InputItems.Add(ResponseItem.CreateUserMessageItem(prompt));
        return options;
    }

    private static void AssertSdkRequest(
        CapturedOpenAiRequest request,
        string expectedPath,
        bool streaming)
    {
        Assert.Equal(expectedPath, request.PathAndQuery);
        Assert.Equal("Bearer test-key", request.Authorization);
        using var body = JsonDocument.Parse(request.Body);
        var hasStreamingFlag = body.RootElement.TryGetProperty("stream", out var streamingElement);
        Assert.Equal(streaming, hasStreamingFlag && streamingElement.GetBoolean());
    }

    private static WorkspaceProviderProfile CreateWorkspaceProvider(
        string connectorPluginKey,
        AgentFrameworkProviderKind kind,
        ProviderTransportKind transport,
        ProviderProfilePurpose purpose,
        string baseUrl)
    {
        return new WorkspaceProviderProfile
        {
            Id = Guid.NewGuid(),
            Name = $"{kind} profile",
            ConnectorPluginKey = connectorPluginKey,
            ConfigSchemaVersion = "1.0",
            BaseUrl = baseUrl,
            DefaultModel = "model-test",
            IsEnabled = true,
            SupportsStreaming = true,
            SupportsToolCalling = false,
            ExtraSettingsJson = AgentFrameworkProviderMetadata.BuildExtraSettingsJson(
                "{}",
                connectorPluginKey,
                "1.0",
                secretRecordId: null,
                timeoutSeconds: 45,
                kind,
                transport,
                purpose,
                "model-test",
                thinkingEffortCapabilities: [])
        };
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CanDoItAll.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find repository root.");
    }

    private sealed class FixedHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
        {
            return new HttpClient(new CapturingHandler());
        }
    }

    private sealed class FixedCredentialResolver : IProviderDriverCredentialResolver
    {
        public ProviderDriverCredential Resolve(AgentFrameworkProviderProfile provider)
        {
            return ProviderDriverCredential.Resolved("test-key");
        }
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        public string PathAndQuery { get; private set; } = string.Empty;

        public string Authorization { get; private set; } = string.Empty;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            PathAndQuery = request.RequestUri?.PathAndQuery ?? string.Empty;
            Authorization = request.Headers.Authorization?.ToString() ?? string.Empty;
            var image = Convert.ToBase64String([1, 2, 3]);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    $$"""{"data":[{"b64_json":"{{image}}"}]}""",
                    Encoding.UTF8,
                    "application/json")
            });
        }
    }

    private sealed record CapturedOpenAiRequest(
        string PathAndQuery,
        string Authorization,
        string Body);

    private sealed class OpenAiSdkTransportHandler : HttpMessageHandler
    {
        public List<CapturedOpenAiRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var body = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);
            var pathAndQuery = request.RequestUri?.PathAndQuery ?? string.Empty;
            Requests.Add(new CapturedOpenAiRequest(
                pathAndQuery,
                request.Headers.Authorization?.ToString() ?? string.Empty,
                body));
            using var payload = JsonDocument.Parse(body);
            var streaming = payload.RootElement.TryGetProperty("stream", out var streamingElement) &&
                            streamingElement.GetBoolean();

            return (pathAndQuery, streaming) switch
            {
                ("/custom/v1/chat/completions", false) => JsonResponse(ChatCompletionJson("chat normal")),
                ("/custom/v1/chat/completions", true) => EventStreamResponse(ChatCompletionStream("chat stream")),
                ("/custom/v1/responses", false) => JsonResponse(ResponseJson("response normal")),
                ("/custom/v1/responses", true) => EventStreamResponse(ResponseStream("response stream")),
                _ => JsonResponse("""{"error":{"message":"unexpected endpoint"}}""", HttpStatusCode.NotFound)
            };
        }

        private static HttpResponseMessage JsonResponse(
            string body,
            HttpStatusCode statusCode = HttpStatusCode.OK)
        {
            return new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
        }

        private static HttpResponseMessage EventStreamResponse(string body)
        {
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "text/event-stream")
            };
        }

        private static string ChatCompletionJson(string text)
        {
            return $$"""
                {
                  "id": "chatcmpl-sb00",
                  "object": "chat.completion",
                  "created": 1787533200,
                  "model": "model-test",
                  "choices": [
                    {
                      "index": 0,
                      "message": {
                        "role": "assistant",
                        "content": "{{text}}"
                      },
                      "finish_reason": "stop"
                    }
                  ],
                  "usage": {
                    "prompt_tokens": 2,
                    "completion_tokens": 2,
                    "total_tokens": 4
                  }
                }
                """;
        }

        private static string ChatCompletionStream(string text)
        {
            return $$"""
                data: {"id":"chatcmpl-sb00-stream","object":"chat.completion.chunk","created":1787533200,"model":"model-test","choices":[{"index":0,"delta":{"role":"assistant","content":"{{text}}"},"finish_reason":null}]}

                data: {"id":"chatcmpl-sb00-stream","object":"chat.completion.chunk","created":1787533200,"model":"model-test","choices":[{"index":0,"delta":{},"finish_reason":"stop"}]}

                data: [DONE]

                """;
        }

        private static string ResponseJson(string text)
        {
            return $$"""
                {
                  "id": "resp_sb00",
                  "object": "response",
                  "created_at": 1787533200,
                  "status": "completed",
                  "model": "model-test",
                  "output": [
                    {
                      "id": "msg_sb00",
                      "type": "message",
                      "status": "completed",
                      "role": "assistant",
                      "content": [
                        {
                          "type": "output_text",
                          "text": "{{text}}",
                          "annotations": []
                        }
                      ]
                    }
                  ],
                  "parallel_tool_calls": false,
                  "tools": [],
                  "usage": {
                    "input_tokens": 2,
                    "output_tokens": 2,
                    "total_tokens": 4
                  }
                }
                """;
        }

        private static string ResponseStream(string text)
        {
            var completedResponse = ResponseJson(text).ReplaceLineEndings(string.Empty);
            return $$"""
                event: response.output_text.delta
                data: {"type":"response.output_text.delta","sequence_number":0,"item_id":"msg_sb00_stream","output_index":0,"content_index":0,"delta":"{{text}}"}

                event: response.completed
                data: {"type":"response.completed","sequence_number":1,"response":{{completedResponse}}}

                """;
        }
    }
}
#pragma warning restore OPENAI001
