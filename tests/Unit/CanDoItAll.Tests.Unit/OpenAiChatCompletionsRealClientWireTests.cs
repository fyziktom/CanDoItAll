using System.ClientModel;
using System.ClientModel.Primitives;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Providers;
using CanDoItAll.SharedProviders.Abstractions;
using CanDoItAll.SharedProviders.Http;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Compaction;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OpenAI;
using OpenAI.Chat;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class OpenAiChatCompletionsRealClientWireTests
{
    private const string FunctionName = "record_value";
    private const string FunctionCallId = "call-001";

    [Fact]
    public async Task Maf_streaming_agent_request_is_accepted_by_shared_provider_policy()
    {
        var model = SharedProviderRoutingModelIdCodec.Create(
            new SharedProviderPublicationId(Guid.NewGuid()),
            "upstream-model").Value;
        var provider = CreateProvider(model) with
        {
            BaseUrl = "https://shared.example.test/api/shared-providers/openai/v1"
        };
        var handler = new SharedProviderPolicyCaptureHandler(model);
        using var httpClient = new HttpClient(handler);
        var factory = new MafProviderAgentFactory(
            new MafProviderCredentialService(
                new ProfileCredentialResolver(new Dictionary<Guid, string>
                {
                    [provider.Id] = "test-source-token"
                })),
            NoOpMafProviderStreamingDispatchGate.Instance,
            httpClientSelector: new FixedProviderHttpClientSelector(httpClient));
        var agent = CreateFrameworkAgent(factory, provider);

        try
        {
            var responseText = new StringBuilder();
            await foreach (var update in agent.RunStreamingAsync("Verify streaming compatibility."))
            {
                responseText.Append(update.Text);
            }

            Assert.Equal("accepted", responseText.ToString());
            var policyResult = Assert.IsType<SharedProviderRelayRequestPolicyResult.Accepted>(
                handler.PolicyResult);
            Assert.True(policyResult.Request.Stream);
            Assert.DoesNotContain("parallel_tool_calls", handler.RawPayload, StringComparison.Ordinal);
        }
        finally
        {
            await DisposeAgentAsync(agent);
        }
    }

    [Fact]
    public async Task Maf_factory_parallel_openai_profiles_send_profile_credentials_without_mutating_process_environment()
    {
        const string model = OpenAiModelIds.Gpt54Mini;
        var requestGate = new TwoParticipantAsyncGate();
        await using var endpointA = new CredentialCaptureChatServer(
            requestGate,
            model,
            "Profile A completed.");
        await using var endpointB = new CredentialCaptureChatServer(
            requestGate,
            model,
            "Profile B completed.");
        var secretRecordIdA = Guid.NewGuid();
        var secretRecordIdB = Guid.NewGuid();
        var providerA = CreateCredentialBoundProvider(
            "OpenAI profile A",
            endpointA.Endpoint,
            model,
            secretRecordIdA);
        var providerB = CreateCredentialBoundProvider(
            "OpenAI profile B",
            endpointB.Endpoint,
            model,
            secretRecordIdB);
        var credentialA = $"profile-a-{Guid.NewGuid():N}";
        var credentialB = $"profile-b-{Guid.NewGuid():N}";
        var resolver = new ProfileCredentialResolver(
            new Dictionary<Guid, string>
            {
                [providerA.Id] = credentialA,
                [providerB.Id] = credentialB
            });
        var factory = new MafProviderAgentFactory(
            new MafProviderCredentialService(resolver),
            NoOpMafProviderStreamingDispatchGate.Instance);
        var environmentNames = new[]
        {
            providerA.ApiKeyEnvironmentVariable,
            providerB.ApiKeyEnvironmentVariable,
            MafProviderRuntimeSettings.OpenAiApiKeyEnvironmentVariable
        };
        var environmentBefore = CaptureProcessEnvironment(environmentNames);
        Dictionary<string, string?>? environmentAfter = null;
        AIAgent? agentA = null;
        AIAgent? agentB = null;

        try
        {
            agentA = CreateFrameworkAgent(factory, providerA);
            agentB = CreateFrameworkAgent(factory, providerB);

            var responses = await Task.WhenAll(
                    RunTextAsync(agentA, "Respond for profile A."),
                    RunTextAsync(agentB, "Respond for profile B."))
                .WaitAsync(TimeSpan.FromSeconds(10));

            Assert.Equal(["Profile A completed.", "Profile B completed."], responses);
            Assert.Equal($"Bearer {credentialA}", endpointA.Authorization);
            Assert.Equal($"Bearer {credentialB}", endpointB.Authorization);
            Assert.DoesNotContain(credentialB, endpointA.RequestHeaders, StringComparison.Ordinal);
            Assert.DoesNotContain(credentialA, endpointB.RequestHeaders, StringComparison.Ordinal);
        }
        finally
        {
            environmentAfter = CaptureProcessEnvironment(environmentNames);
            RestoreProcessEnvironment(environmentBefore);
            await DisposeAgentAsync(agentB);
            await DisposeAgentAsync(agentA);
        }

        Assert.NotNull(environmentAfter);
        foreach (var (variableName, valueBefore) in environmentBefore)
        {
            Assert.True(
                environmentAfter.TryGetValue(variableName, out var valueAfter) &&
                string.Equals(valueBefore, valueAfter, StringComparison.Ordinal),
                $"Process environment variable '{variableName}' changed during parallel provider execution.");
        }
    }

    [Theory]
    [InlineData(OpenAiModelIds.Gpt56Terra, false)]
    [InlineData(OpenAiModelIds.Gpt56Luna, false)]
    [InlineData(OpenAiModelIds.Gpt54Mini, false)]
    [InlineData(OpenAiModelIds.Gpt56Terra, true)]
    [InlineData(OpenAiModelIds.Gpt56Luna, true)]
    [InlineData(OpenAiModelIds.Gpt54Mini, true)]
    public async Task Maf_agent_for_affected_model_sends_none_and_completes_function_tool_turn(
        string upstreamModel, bool shared) {
        var model = shared
            ? SharedProviderRoutingModelIdCodec.Create(
                new SharedProviderPublicationId(Guid.NewGuid()), upstreamModel).Value
            : upstreamModel;
        var handler = new ScriptedChatCompletionsHandler(model);
        using var httpClient = new HttpClient(handler);
        var nativeClient = new ChatClient(
            model,
            new ApiKeyCredential("unused-test-key"),
            new OpenAIClientOptions
            {
                Endpoint = new Uri("https://openai.test/v1"),
                Transport = new HttpClientPipelineTransport(httpClient)
            });
        var provider = CreateProvider(model);
        if (shared) {
            provider = provider with {
                CredentialBinding = new ProviderCredentialBinding(
                    Guid.NewGuid(), ProviderCredentialPurpose.SourceAccessToken,
                    ProviderCredentialConsumerKind.Source, Guid.NewGuid()),
                ModelCatalog = [new(model, upstreamModel)],
                ModelSelectionConstraint = new ProviderModelSelectionConstraint([model])
            };
        }
        var invocationCount = 0;
        string? invokedValue = null;
        var function = AIFunctionFactory.Create(
            (string value) =>
            {
                invocationCount++;
                invokedValue = value;
                return $"recorded:{value}";
            },
            FunctionName,
            "Records the supplied value.");
        AIAgent agent = nativeClient.AsAIAgent(
            options: new ChatClientAgentOptions
            {
                ChatOptions = new ChatOptions
                {
                    Reasoning = new ReasoningOptions
                    {
                        Effort = ReasoningEffort.Medium
                    },
                    Tools = [function]
                }
            },
            clientFactory: chatClient => new OpenAiChatCompletionsCompatibilityChatClient(
                chatClient,
                provider,
                model,
                logger: null));

        try
        {
            var session = await agent.CreateSessionAsync();
            var response = await agent.RunAsync("Record alpha with the available tool.", session);

            Assert.Equal("Recorded alpha.", response.Text);
            Assert.Equal(1, invocationCount);
            Assert.Equal("alpha", invokedValue);

            Assert.Collection(
                handler.RequestBodies,
                request =>
                {
                    AssertCompatibleRequest(request);
                    AssertInitialRequest(request);
                },
                request =>
                {
                    AssertCompatibleRequest(request);
                    AssertFollowUpRequest(request);
                });
        }
        finally
        {
            switch (agent)
            {
                case IAsyncDisposable asyncDisposable:
                    await asyncDisposable.DisposeAsync();
                    break;
                case IDisposable disposable:
                    disposable.Dispose();
                    break;
            }
        }

        void AssertCompatibleRequest(JsonDocument request)
        {
            var root = request.RootElement;
            Assert.Equal(model, root.GetProperty("model").GetString());
            Assert.Equal("none", root.GetProperty("reasoning_effort").GetString());
            if (shared) {
                Assert.True(new SharedProviderRelaySupportCatalog().TryGet(
                    SharedProviderConnectorPluginKeys.OpenAi, SharedProviderPurpose.Chat, out var descriptor));
                var normalized = new SharedProviderRelayRequestPolicy().Normalize(
                    SharedProviderRelayOperation.ChatCompletions,
                    Encoding.UTF8.GetBytes(root.GetRawText()), descriptor.Support);
                Assert.IsType<SharedProviderRelayRequestPolicyResult.Accepted>(normalized);
            }

            var tool = Assert.Single(root.GetProperty("tools").EnumerateArray());
            Assert.Equal("function", tool.GetProperty("type").GetString());
            Assert.Equal(
                FunctionName,
                tool.GetProperty("function").GetProperty("name").GetString());
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Shared_model_requires_unique_source_metadata_before_dispatch(bool duplicate) {
        var model = SharedProviderRoutingModelIdCodec.Create(
            new SharedProviderPublicationId(Guid.NewGuid()), OpenAiModelIds.Gpt54Mini).Value;
        var provider = CreateProvider(model) with {
            CredentialBinding = new ProviderCredentialBinding(
                Guid.NewGuid(), ProviderCredentialPurpose.SourceAccessToken,
                ProviderCredentialConsumerKind.Source, Guid.NewGuid()),
            ModelCatalog = duplicate ? [new(model, OpenAiModelIds.Gpt54Mini), new(model, OpenAiModelIds.Gpt56Luna)] : []
        };
        var handler = new ScriptedChatCompletionsHandler(model);
        using var httpClient = new HttpClient(handler);
        var nativeClient = new ChatClient(model, new ApiKeyCredential("unused-test-key"), new OpenAIClientOptions {
            Endpoint = new Uri("https://openai.test/v1"),
            Transport = new HttpClientPipelineTransport(httpClient)
        });
        using var client = new OpenAiChatCompletionsCompatibilityChatClient(
            nativeClient.AsIChatClient(), provider, model, logger: null);

        await Assert.ThrowsAsync<InvalidOperationException>(() => client.GetResponseAsync("Test metadata validation."));

        Assert.Empty(handler.RequestBodies);
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, true)]
    [InlineData(true, false, false)]
    [InlineData(true, true, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, true)]
    public async Task Approved_tool_after_session_restore_has_valid_wire_history(bool streaming, bool includeContext, bool includeCompaction) {
        const string model = OpenAiModelIds.Gpt54Mini;
        var handler = new ScriptedChatCompletionsHandler(model);
        using var httpClient = new HttpClient(handler);
        var provider = CreateProvider(model);
        var factory = new MafProviderAgentFactory(
            new MafProviderCredentialService(new ProfileCredentialResolver(
                new Dictionary<Guid, string> { [provider.Id] = "unused-test-key" })),
            NoOpMafProviderStreamingDispatchGate.Instance,
            httpClientSelector: new FixedProviderHttpClientSelector(httpClient));
        var invocationCount = 0;
        var function = AIFunctionFactory.Create((string value) => {
            invocationCount++;
            return $"recorded:{value}";
        }, FunctionName);
        var options = MafChatClientAgentOptionsFactory.Create(new ChatOptions {
            Tools = [new ApprovalRequiredAIFunction(function)]
        });
        options.ChatHistoryProvider = new InMemoryChatHistoryProvider(new InMemoryChatHistoryProviderOptions {
            StorageInputRequestMessageFilter = messages => messages.Where(message =>
                message.GetAgentRequestMessageSourceType() != AgentRequestMessageSourceType.AIContextProvider &&
                message.GetAgentRequestMessageSourceType() != AgentRequestMessageSourceType.ChatHistory).ToList()
        });
        options.RequirePerServiceCallChatHistoryPersistence = true;
#pragma warning disable MAAI001
        var providers = new List<AIContextProvider>();
        if (includeContext) {
            providers.Add(new StaticMessageContextProvider(new(ChatRole.User, "Scoped runtime context"), StaticMessageContextProvider.TransientAgentChatStateKey));
        }
        if (includeCompaction) {
            providers.Add(new CompactionProvider(new PipelineCompactionStrategy(
                new ToolResultCompactionStrategy(CompactionTriggers.MessagesExceed(40)),
                new SlidingWindowCompactionStrategy(CompactionTriggers.TurnsExceed(32)),
                new TruncationCompactionStrategy(CompactionTriggers.TokensExceed(64000)))));
        }
        options.AIContextProviders = providers;
#pragma warning restore MAAI001
        var agent = factory.CreateFrameworkAgent(provider, model, options, true, false);
        try {
            var session = await agent.CreateSessionAsync();
            var paused = await RunAsync([new(ChatRole.User, "Record alpha with the available tool.")], session);
            var approval = Assert.Single(paused.Messages.SelectMany(message => message.Contents)
                .OfType<ToolApprovalRequestContent>());
            Assert.Equal(0, invocationCount);
            var serialized = await agent.SerializeSessionAsync(session);
            var restored = await agent.DeserializeSessionAsync(serialized);

            var completed = await RunAsync([new(ChatRole.User, [approval.CreateResponse(true)])], restored);

            Assert.Equal("Recorded alpha.", completed.Text);
            Assert.Equal(1, invocationCount);
            Assert.Equal(2, handler.RequestBodies.Count);
            var messages = handler.RequestBodies[1].RootElement.GetProperty("messages").EnumerateArray().ToArray();
            var assistantIndex = Array.FindIndex(messages, message => message.TryGetProperty("tool_calls", out _));
            var toolIndex = Array.FindIndex(messages, message => message.GetProperty("role").GetString() == "tool");
            Assert.True(assistantIndex >= 0);
            Assert.Equal(assistantIndex + 1, toolIndex);
            Assert.Equal(FunctionCallId, messages[toolIndex].GetProperty("tool_call_id").GetString());
            Assert.All(handler.RequestBodies, request => Assert.Equal("none", request.RootElement.GetProperty("reasoning_effort").GetString()));
        } finally {
            await DisposeAgentAsync(agent);
        }

        async Task<AgentResponse> RunAsync(IEnumerable<Microsoft.Extensions.AI.ChatMessage> messages, AgentSession session) {
            var runOptions = new ChatClientAgentRunOptions(new ChatOptions { AllowMultipleToolCalls = false, Temperature = 0.4f });
            if (!streaming) {
                return await agent.RunAsync(messages, session, runOptions);
            }

            var updates = new List<AgentResponseUpdate>();
            await foreach (var update in agent.RunStreamingAsync(messages, session, runOptions)) {
                updates.Add(MafAgentResponseSnapshotter.SnapshotUpdate(update));
            }
            return updates.ToAgentResponse();
        }
    }

    private static void AssertInitialRequest(JsonDocument request)
    {
        var messages = request.RootElement.GetProperty("messages").EnumerateArray().ToArray();
        var userMessage = Assert.Single(messages);

        Assert.Equal("user", userMessage.GetProperty("role").GetString());
        Assert.Equal(
            "Record alpha with the available tool.",
            userMessage.GetProperty("content").GetString());
    }

    private static void AssertFollowUpRequest(JsonDocument request)
    {
        var messages = request.RootElement.GetProperty("messages").EnumerateArray().ToArray();
        Assert.Equal(3, messages.Length);

        var assistantMessage = messages[1];
        Assert.Equal("assistant", assistantMessage.GetProperty("role").GetString());
        var toolCall = Assert.Single(assistantMessage.GetProperty("tool_calls").EnumerateArray());
        Assert.Equal(FunctionCallId, toolCall.GetProperty("id").GetString());
        Assert.Equal(FunctionName, toolCall.GetProperty("function").GetProperty("name").GetString());
        using var arguments = JsonDocument.Parse(
            toolCall.GetProperty("function").GetProperty("arguments").GetString()!);
        Assert.Equal("alpha", arguments.RootElement.GetProperty("value").GetString());

        var toolMessage = messages[2];
        Assert.Equal("tool", toolMessage.GetProperty("role").GetString());
        Assert.Equal(FunctionCallId, toolMessage.GetProperty("tool_call_id").GetString());
        Assert.Equal(
            "recorded:alpha",
            JsonSerializer.Deserialize<string>(toolMessage.GetProperty("content").GetString()!));
    }

    private static ProviderProfile CreateProvider(string model)
    {
        return new ProviderProfile(
            Id: Guid.NewGuid(),
            Name: "OpenAI Chat Completions wire test",
            Kind: ProviderKind.OpenAi,
            BaseUrl: "https://openai.test/v1",
            ApiKeyEnvironmentVariable: "UNUSED_TEST_OPENAI_API_KEY",
            DefaultModel: model,
            Transport: ProviderTransportKind.ChatCompletions,
            IsEnabled: true,
            SupportsStreaming: true,
            SupportsTools: true,
            PreferFrameworkManagedChatHistory: true,
            SupportsBackgroundResponses: false,
            ConfigurationJson: "{}",
            Notes: string.Empty,
            HealthStatus: "ok",
            LastCheckedAtUtc: null,
            SuggestedModels: []);
    }

    private static ProviderProfile CreateCredentialBoundProvider(
        string name,
        string endpoint,
        string model,
        Guid secretRecordId)
    {
        return new ProviderProfile(
            Id: Guid.NewGuid(),
            Name: name,
            Kind: ProviderKind.OpenAi,
            BaseUrl: endpoint,
            ApiKeyEnvironmentVariable: $"secret:{secretRecordId:D}",
            DefaultModel: model,
            Transport: ProviderTransportKind.ChatCompletions,
            IsEnabled: true,
            SupportsStreaming: true,
            SupportsTools: true,
            PreferFrameworkManagedChatHistory: true,
            SupportsBackgroundResponses: false,
            ConfigurationJson: $$"""{"secretRecordId":"{{secretRecordId:D}}"}""",
            Notes: string.Empty,
            HealthStatus: "ok",
            LastCheckedAtUtc: null,
            SuggestedModels: [model]);
    }

    private static AIAgent CreateFrameworkAgent(
        MafProviderAgentFactory factory,
        ProviderProfile provider)
    {
        return factory.CreateFrameworkAgent(
            provider,
            provider.DefaultModel,
            MafChatClientAgentOptionsFactory.Create(new ChatOptions()),
            frameworkManagedHistory: true,
            allowBackgroundResponses: false);
    }

    private static async Task<string> RunTextAsync(
        AIAgent agent,
        string prompt)
    {
        var response = await agent.RunAsync(prompt);
        return response.Text;
    }

    private static async ValueTask DisposeAgentAsync(AIAgent? agent)
    {
        switch (agent)
        {
            case IAsyncDisposable asyncDisposable:
                await asyncDisposable.DisposeAsync();
                break;
            case IDisposable disposable:
                disposable.Dispose();
                break;
        }
    }

    private static Dictionary<string, string?> CaptureProcessEnvironment(
        IEnumerable<string> variableNames)
    {
        return variableNames
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                variableName => variableName,
                Environment.GetEnvironmentVariable,
                StringComparer.OrdinalIgnoreCase);
    }

    private static void RestoreProcessEnvironment(
        IReadOnlyDictionary<string, string?> snapshot)
    {
        foreach (var (variableName, value) in snapshot)
        {
            Environment.SetEnvironmentVariable(
                variableName,
                value,
                EnvironmentVariableTarget.Process);
        }
    }

    private sealed class ScriptedChatCompletionsHandler(string model) : HttpMessageHandler
    {
        private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
        private readonly List<JsonDocument> requestBodies = [];

        public IReadOnlyList<JsonDocument> RequestBodies => requestBodies;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("/v1/chat/completions", request.RequestUri!.AbsolutePath);

            var body = await request.Content!.ReadAsStringAsync(cancellationToken);
            requestBodies.Add(JsonDocument.Parse(body));

            var responseBody = requestBodies.Count switch
            {
                1 => CreateToolCallResponse(),
                2 => CreateTerminalResponse(),
                _ => throw new InvalidOperationException("The fake endpoint received an unexpected request.")
            };
            if (requestBodies[^1].RootElement.TryGetProperty("stream", out var stream) && stream.GetBoolean()) {
                var envelope = JsonNode.Parse(responseBody)!;
                envelope["object"] = "chat.completion.chunk";
                var choice = envelope["choices"]![0]!.AsObject();
                var delta = choice["message"]!.DeepClone();
                choice.Remove("message");
                choice["delta"] = delta;
                if (delta["tool_calls"] is JsonArray calls) {
                    for (var index = 0; index < calls.Count; index++) {
                        calls[index]!["index"] = index;
                    }
                }
                return new HttpResponseMessage(HttpStatusCode.OK) {
                    Content = new StringContent($"data: {envelope.ToJsonString()}\n\ndata: [DONE]\n\n", Encoding.UTF8, "text/event-stream")
                };
            }
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
            };
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                foreach (var requestBody in requestBodies)
                {
                    requestBody.Dispose();
                }
            }

            base.Dispose(disposing);
        }

        private string CreateToolCallResponse()
        {
            return JsonSerializer.Serialize(
                new
                {
                    id = "chatcmpl-tool",
                    @object = "chat.completion",
                    created = 1_785_710_400,
                    model,
                    choices = new[]
                    {
                        new
                        {
                            index = 0,
                            message = new
                            {
                                role = "assistant",
                                content = (string?)null,
                                tool_calls = new[]
                                {
                                    new
                                    {
                                        id = FunctionCallId,
                                        type = "function",
                                        function = new
                                        {
                                            name = FunctionName,
                                            arguments = "{\"value\":\"alpha\"}"
                                        }
                                    }
                                }
                            },
                            finish_reason = "tool_calls"
                        }
                    },
                    usage = new
                    {
                        prompt_tokens = 11,
                        completion_tokens = 4,
                        total_tokens = 15
                    }
                },
                SerializerOptions);
        }

        private string CreateTerminalResponse()
        {
            return JsonSerializer.Serialize(
                new
                {
                    id = "chatcmpl-terminal",
                    @object = "chat.completion",
                    created = 1_785_710_401,
                    model,
                    choices = new[]
                    {
                        new
                        {
                            index = 0,
                            message = new
                            {
                                role = "assistant",
                                content = "Recorded alpha."
                            },
                            finish_reason = "stop"
                        }
                    },
                    usage = new
                    {
                        prompt_tokens = 19,
                        completion_tokens = 3,
                        total_tokens = 22
                    }
                },
                SerializerOptions);
        }
    }

    private sealed class FixedProviderHttpClientSelector(HttpClient client)
        : IProviderHttpClientSelector
    {
        public bool TryGetClient(
            ProviderProfile provider,
            [NotNullWhen(true)]
            out HttpClient? selectedClient)
        {
            selectedClient = client;
            return true;
        }
    }

    private sealed class SharedProviderPolicyCaptureHandler(string model)
        : HttpMessageHandler
    {
        public SharedProviderRelayRequestPolicyResult? PolicyResult { get; private set; }

        public string RawPayload { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var payload = await request.Content!.ReadAsByteArrayAsync(cancellationToken);
            RawPayload = Encoding.UTF8.GetString(payload);
            PolicyResult = new SharedProviderRelayRequestPolicy().Normalize(
                SharedProviderRelayOperation.ChatCompletions,
                payload,
                new SharedProviderRelaySupportDescriptor(
                    new HashSet<SharedProviderRelayOperation>
                    {
                        SharedProviderRelayOperation.ChatCompletions
                    },
                    SharedProviderStreamingMode.ServerSentEvents,
                    supportsFunctionTools: true,
                    supportsParallelFunctionTools: false,
                    supportsStructuredOutput: true,
                    supportsVisionInput: true,
                    supportsBase64Images: false,
                    maximumRequestBytes: 4 * 1024 * 1024,
                    maximumOutputTokens: 4096,
                    maximumImageCount: 1));

            const string bodyTemplate = """
                data: {"id":"chatcmpl-shared-policy","object":"chat.completion.chunk","created":1785710400,"model":"__MODEL__","choices":[{"index":0,"delta":{"role":"assistant","content":"accepted"},"finish_reason":null}]}

                data: {"id":"chatcmpl-shared-policy","object":"chat.completion.chunk","created":1785710400,"model":"__MODEL__","choices":[{"index":0,"delta":{},"finish_reason":"stop"}],"usage":{"prompt_tokens":2,"completion_tokens":1,"total_tokens":3}}

                data: [DONE]

                """;
            var body = bodyTemplate.Replace("__MODEL__", model, StringComparison.Ordinal);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "text/event-stream")
            };
        }
    }

    private sealed class ProfileCredentialResolver(
        IReadOnlyDictionary<Guid, string> credentials) :
        IAgentProviderCredentialResolver
    {
        public ProviderCredentialResolution Resolve(ProviderProfile provider)
        {
            return credentials.TryGetValue(provider.Id, out var credential)
                ? new ProviderCredentialResolution(
                    credential,
                    "profile-bound test secret",
                    string.Empty)
                : new ProviderCredentialResolution(
                    string.Empty,
                    "profile-bound test secret",
                    "The profile has no bound test secret.");
        }
    }

    private sealed class TwoParticipantAsyncGate
    {
        private readonly TaskCompletionSource released =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int arrivals;

        public async Task SignalAndWaitAsync()
        {
            if (Interlocked.Increment(ref arrivals) == 2)
            {
                released.TrySetResult();
            }

            await released.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    private sealed class CredentialCaptureChatServer : IAsyncDisposable
    {
        private readonly TcpListener listener;
        private readonly TwoParticipantAsyncGate requestGate;
        private readonly string model;
        private readonly string responseText;
        private readonly Task serverTask;

        public CredentialCaptureChatServer(
            TwoParticipantAsyncGate requestGate,
            string model,
            string responseText)
        {
            this.requestGate = requestGate;
            this.model = model;
            this.responseText = responseText;
            listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            Endpoint = $"http://127.0.0.1:{port}/v1";
            serverTask = Task.Run(ServeOnceAsync);
        }

        public string Endpoint { get; }

        public string RequestHeaders { get; private set; } = string.Empty;

        public string Authorization => RequestHeaders
            .Split("\r\n", StringSplitOptions.RemoveEmptyEntries)
            .Single(line => line.StartsWith("Authorization:", StringComparison.OrdinalIgnoreCase))
            ["Authorization:".Length..]
            .Trim();

        public async ValueTask DisposeAsync()
        {
            listener.Stop();
            try
            {
                await serverTask.WaitAsync(TimeSpan.FromSeconds(2));
            }
            catch (Exception exception) when (
                exception is SocketException or ObjectDisposedException or TimeoutException)
            {
            }
        }

        private async Task ServeOnceAsync()
        {
            using var client = await listener.AcceptTcpClientAsync();
            await using var stream = client.GetStream();
            RequestHeaders = await ReadRequestHeadersAsync(stream);
            await requestGate.SignalAndWaitAsync();

            var body = JsonSerializer.Serialize(
                new
                {
                    id = $"chatcmpl-{Guid.NewGuid():N}",
                    @object = "chat.completion",
                    created = 1_785_710_402,
                    model,
                    choices = new[]
                    {
                        new
                        {
                            index = 0,
                            message = new
                            {
                                role = "assistant",
                                content = responseText
                            },
                            finish_reason = "stop"
                        }
                    },
                    usage = new
                    {
                        prompt_tokens = 4,
                        completion_tokens = 3,
                        total_tokens = 7
                    }
                });
            var bodyBytes = Encoding.UTF8.GetBytes(body);
            var headerBytes = Encoding.ASCII.GetBytes(
                "HTTP/1.1 200 OK\r\n" +
                "Content-Type: application/json\r\n" +
                $"Content-Length: {bodyBytes.Length}\r\n" +
                "Connection: close\r\n\r\n");
            await stream.WriteAsync(headerBytes);
            await stream.WriteAsync(bodyBytes);
        }

        private static async Task<string> ReadRequestHeadersAsync(
            NetworkStream stream)
        {
            var buffer = new byte[1024];
            var received = new List<byte>();
            while (received.Count < 8192)
            {
                var read = await stream.ReadAsync(buffer);
                if (read == 0)
                {
                    break;
                }

                received.AddRange(buffer.Take(read));
                var requestText = Encoding.ASCII.GetString(received.ToArray());
                if (requestText.Contains("\r\n\r\n", StringComparison.Ordinal))
                {
                    return requestText;
                }
            }

            return Encoding.ASCII.GetString(received.ToArray());
        }
    }
}
