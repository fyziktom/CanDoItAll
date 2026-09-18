using System.ClientModel;
using System.ClientModel.Primitives;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Tooling;
using CanDoItAll.SharedProviders.Http;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OllamaSharp;
using OpenAI;
using OpenAI.Responses;

namespace CanDoItAll.Tests.Integration.Runtime;

#pragma warning disable OPENAI001, MAAI001
[Trait("Category", "FileSystemPortability")]
public sealed class MafToolAdmissionNativeLoopIntegrationTests {
    [Theory]
    [InlineData(WireProvider.Responses, false)]
    [InlineData(WireProvider.Responses, true)]
    [InlineData(WireProvider.ChatCompletions, false)]
    [InlineData(WireProvider.ChatCompletions, true)]
    [InlineData(WireProvider.Ollama, false)]
    [InlineData(WireProvider.Ollama, true)]
    public async Task Native_sdk_approval_and_completed_effects_replay_across_independent_stores_without_another_provider_call(
        WireProvider wireProvider, bool streaming) {
        await using var fixture = await AgentToolAdmissionJournalFixture.CreateAsync();
        var probe = new EffectProbe();
        var initialWire = new NativeWireHandler(wireProvider, complete: false);
        IReadOnlyList<PendingToolApprovalRecord> pending;
        var initialJournal = fixture.NewJournal();
        await using (var lease = await initialJournal.AcquireRunAsync(fixture.Session, default)) {
            using var bound = lease.Bind();
            using var client = CreateClient(wireProvider, initialWire);
            var runtime = CreateRuntime(client, initialJournal, fixture, probe);
            var runtimeSession = await runtime.Agent.CreateSessionAsync();
            var opened = await OpenAsync(fixture, initialJournal, lease, runtime, runtimeSession, wireProvider);
            using var active = opened.Context.Bind();
            var response = await RunAsync(runtime.Agent, opened.Session, opened.Input, streaming);
            var requests = response.Messages.SelectMany(message => message.Contents).OfType<ToolApprovalRequestContent>().ToArray();
            Assert.Equal(2, requests.Length);
            Assert.Empty(probe.Intents);
            var approvals = requests.Select(new MafApprovalContinuationDriver().MapPendingApproval).ToArray();
            var serialized = await new MafRuntimeSessionPersistenceDriver().TrySerializePersistableRuntimeSessionAsync(
                runtime.Agent, opened.Session, Provider(fixture, wireProvider), fixture.Provider.DefaultModel, Options(fixture),
                approvals, (_, _, _) => Task.CompletedTask, default);
            pending = await opened.Context.SaveApprovalsAsync(Assert.IsType<string>(serialized), approvals, default);
        }

        Assert.Single(initialWire.Requests);
        await fixture.ApproveAsync(pending);
        var continuationWire = new NativeWireHandler(wireProvider, complete: true);
        var restartedJournal = fixture.NewJournal(fixture.NewStore());
        await using (var lease = await restartedJournal.AcquireRunAsync(fixture.Session, default)) {
            using var bound = lease.Bind();
            using var client = CreateClient(wireProvider, continuationWire);
            var runtime = CreateRuntime(client, restartedJournal, fixture, probe);
            var opened = await OpenAsync(fixture, restartedJournal, lease, runtime, await runtime.Agent.CreateSessionAsync(), wireProvider);
            using var active = opened.Context.Bind();
            var response = await RunAsync(runtime.Agent, opened.Session, opened.Input, streaming);
            Assert.Contains("completed", response.Text, StringComparison.Ordinal);
        }

        Assert.Equal(2, probe.Intents.Count);
        Assert.Equal(2, probe.Intents.Distinct().Count());
        Assert.Equal(1, probe.MaximumActive);
        var request = Assert.Single(continuationWire.Requests);
        Assert.DoesNotContain("replacement prompt must never be sent", request, StringComparison.Ordinal);
        if (wireProvider == WireProvider.Responses) {
            Assert.Contains("opaque-reasoning", request, StringComparison.Ordinal);
            var unwrappedRequest = await UnwrappedResponsesApprovalRequestAsync(streaming);
            Assert.Equal(ReasoningItems(unwrappedRequest), ReasoningItems(request));
            var captured = (await fixture.NewStore().GetExecutionRunAsync(fixture.Session.ExecutionRunId))!.ToolAdmission!.Batches[0].Response;
            Assert.Contains("provider_extension", captured.PayloadJson, StringComparison.Ordinal);
        }

        var replayWire = new NativeWireHandler(wireProvider, complete: true, denyRequests: true);
        var replayJournal = fixture.NewJournal(fixture.NewStore());
        await using (var lease = await replayJournal.AcquireRunAsync(fixture.Session, default)) {
            using var bound = lease.Bind();
            using var client = CreateClient(wireProvider, replayWire);
            var runtime = CreateRuntime(client, replayJournal, fixture, probe);
            var opened = await OpenAsync(fixture, replayJournal, lease, runtime, await runtime.Agent.CreateSessionAsync(), wireProvider);
            using var active = opened.Context.Bind();
            var response = await RunAsync(runtime.Agent, opened.Session, opened.Input, streaming);
            Assert.Contains("completed", response.Text, StringComparison.Ordinal);
        }

        Assert.Empty(replayWire.Requests);
        Assert.Equal(2, probe.Intents.Count);
        var persisted = await fixture.NewStore().GetExecutionRunAsync(fixture.Session.ExecutionRunId);
        Assert.Equal(2, persisted!.ToolAdmission!.Segments.Length);
        Assert.Equal(2, persisted.ToolAdmission.Batches.Length);
        Assert.All(persisted.ToolAdmission.Batches[0].Proposals, proposal => Assert.Equal(AgentToolProposalState.Completed, proposal.State));
        Assert.Equal(pending.Select(item => item.ApprovalId), persisted.ToolAdmission.Segments[0].PendingApprovals.Select(item => item.ApprovalId));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Sdk_streaming_shows_text_before_response_completion_but_admits_tool_content_before_approval(bool proposeTool) {
        await using var fixture = await AgentToolAdmissionJournalFixture.CreateAsync();
        var journal = fixture.NewJournal();
        var effects = new EffectProbe();
        using var client = new GatedStreamingClient(proposeTool);
        await using var lease = await journal.AcquireRunAsync(fixture.Session, default);
        using var bound = lease.Bind();
        var runtime = CreateRuntime(client, journal, fixture, effects);
        var opened = await OpenAsync(fixture, journal, lease, runtime, await runtime.Agent.CreateSessionAsync(), WireProvider.Responses);
        using var active = opened.Context.Bind();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var iterator = runtime.Agent.RunStreamingAsync(opened.Input, opened.Session, cancellationToken: timeout.Token)
            .GetAsyncEnumerator(timeout.Token);
        var updates = new List<AgentResponseUpdate>();
        try {
            while (await iterator.MoveNextAsync()) {
                updates.Add(iterator.Current);
                if (iterator.Current.Text.Contains("Visible prefix", StringComparison.Ordinal)) {
                    break;
                }
            }

            Assert.Contains(updates, update => update.Text.Contains("Visible prefix", StringComparison.Ordinal));
            Assert.False(client.Release.Task.IsCompleted);
            Assert.False(client.Completed);
            Assert.Empty((await fixture.NewStore().GetExecutionRunAsync(fixture.Session.ExecutionRunId))!.ToolAdmission!.Batches);
            Assert.Empty(effects.Intents);
        } finally {
            client.Release.TrySetResult();
        }

        while (await iterator.MoveNextAsync()) {
            var update = iterator.Current;
            updates.Add(update);
            if (update.Contents.Any(content => content is FunctionCallContent or ToolApprovalRequestContent)) {
                var saved = (await fixture.NewStore().GetExecutionRunAsync(fixture.Session.ExecutionRunId))!.ToolAdmission!;
                Assert.Single(Assert.Single(saved.Batches).Proposals);
                Assert.True(client.Completed);
            }
        }

        Assert.True(client.Completed);
        Assert.Empty(effects.Intents);
        var response = updates.ToAgentResponse();
        Assert.Contains("Visible prefix", response.Text, StringComparison.Ordinal);
        Assert.Contains("Visible suffix", response.Text, StringComparison.Ordinal);
        Assert.Equal(proposeTool ? 1 : 0, response.Messages.SelectMany(message => message.Contents).OfType<ToolApprovalRequestContent>().Count());
        var batch = Assert.Single((await journal.ReadAsync(lease, default)).Batches);
        Assert.Equal(proposeTool ? 1 : 0, batch.Proposals.Length);
    }

    [Fact]
    public void Unsupported_opaque_provider_items_fail_before_an_envelope_can_be_admitted() {
        var response = new ChatResponse(new ChatMessage(ChatRole.Assistant, [new AIContent { RawRepresentation = new object() }]));
        var failure = Assert.Throws<AgentToolAdmissionException>(() => MafToolProtocolCodec.Encode(response));
        Assert.Equal("tool-admission.unsupported-protocol", failure.Code);
    }

    private static async Task<(MafToolRunContext Context, AgentSession Session, List<ChatMessage> Input)> OpenAsync(
        AgentToolAdmissionJournalFixture fixture, AgentToolAdmissionJournal journal, AgentToolRunLease lease,
        Runtime runtime, AgentSession session, WireProvider wireProvider) {
        var saved = await journal.ReadAsync(lease, default);
        var prompt = saved.Segments.Length == 0 ? "Create two reviewed definitions." : "replacement prompt must never be sent";
        return await MafToolRunContext.OpenAsync(journal, lease, runtime.Agent, session, fixture.Agent,
            Provider(fixture, wireProvider), fixture.Provider.DefaultModel, fixture.Detail.ChatSession!, Options(fixture), runtime.Capabilities,
            [new(ChatRole.User, prompt)], new MafRuntimeSessionPersistenceDriver(), isApprovalContinuation: false,
            (_, _, _) => Task.CompletedTask, default);
    }

    private static Runtime CreateRuntime(IChatClient client, AgentToolAdmissionJournal journal,
        AgentToolAdmissionJournalFixture fixture, EffectProbe probe) {
        var function = new ApprovalRequiredAIFunction(AIFunctionFactory.Create(
            async (string value, CancellationToken token) => {
                var payload = AgentToolAdmissionJournalFixture.Payload(value: value);
                var admitted = await journal.RequireInvocationAsync(fixture.Session, payload.ToolName, payload.Digest, token);
                probe.Active++;
                probe.MaximumActive = Math.Max(probe.MaximumActive, probe.Active);
                try {
                    await Task.Yield();
                    probe.Intents.Add(admitted.IntentId.Value);
                    AgentToolInvocationEffectScope.RecordCommitted("fixture-definition", admitted.IntentId.Value.ToString("N"));
                    return $"created:{admitted.IntentId.Value:N}";
                } finally {
                    probe.Active--;
                }
            }, "admission_fixture_create"));
        var capabilities = new RuntimeCapabilityState();
        capabilities.Tools.Add(function);
        capabilities.RuntimeToolMetadata.Add(new("fixture-provider", function.Name, AgentRuntimeToolOperationKind.Mutation, true) {
            PrepareAdmission = arguments => AgentToolAdmissionJournalFixture.Payload(value: arguments.GetProperty("value").GetString()!),
            AuthorizeAdmissionAsync = async (_, token) => {
                await journal.RequireSessionAsync(fixture.Session, token);
                return new EmptyScope();
            },
            AuthorizeResultDisclosureAsync = async (disclosure, token) => {
                await journal.RequireSessionAsync(fixture.Session, token);
                Assert.Contains(disclosure.IntentId.Value, probe.Intents);
                Assert.Equal($"created:{disclosure.IntentId.Value:N}", disclosure.Result.GetString());
                return null;
            }
        });
        var options = MafChatClientAgentOptionsFactory.Create(new ChatOptions { ModelId = fixture.Provider.DefaultModel, Tools = [function] });
        options.ChatHistoryProvider = new InMemoryChatHistoryProvider(new InMemoryChatHistoryProviderOptions {
            JsonSerializerOptions = MafToolProtocolCodec.SerializationOptions
        });
        options.RequirePerServiceCallChatHistoryPersistence = true;
        var agent = new ChatClientAgent(new MafToolAdmissionChatClient(client), options).AsBuilder()
            .Use(async (_, context, next, token) => {
                using var effects = AgentToolInvocationEffectScope.Begin();
                return await (MafToolRunContext.Current ?? throw new InvalidOperationException("No admitted SDK invocation."))
                    .InvokeAsync(context.CallContent, async cancellation => await next(context, cancellation), effects, token);
            }).Build();
        return new(agent, capabilities);
    }

    private static async Task<string> UnwrappedResponsesApprovalRequestAsync(bool streaming) {
        var wire = new NativeWireHandler(WireProvider.Responses, complete: false, completeAfterFirst: true);
        using var client = CreateClient(WireProvider.Responses, wire);
        var function = new ApprovalRequiredAIFunction(AIFunctionFactory.Create((string value) => $"created:{value}", "admission_fixture_create"));
        var options = MafChatClientAgentOptionsFactory.Create(new ChatOptions { ModelId = "fixture-model", Tools = [function] });
        options.ChatHistoryProvider = new InMemoryChatHistoryProvider();
        options.RequirePerServiceCallChatHistoryPersistence = true;
        var agent = new ChatClientAgent(client, options);
        var session = await agent.CreateSessionAsync();
        var proposed = await RunAsync(agent, session, [new(ChatRole.User, "Create two reviewed definitions.")], streaming);
        var approvals = proposed.Messages.SelectMany(message => message.Contents).OfType<ToolApprovalRequestContent>().ToArray();
        Assert.Equal(2, approvals.Length);
        var input = approvals.Select(approval => new ChatMessage(ChatRole.User, [approval.CreateResponse(true)])).ToList();
        var completed = await RunAsync(agent, session, input, streaming);
        Assert.Contains("completed", completed.Text, StringComparison.Ordinal);
        Assert.Equal(2, wire.Requests.Count);
        return wire.Requests[1];
    }

    private static string ReasoningItems(string request) {
        using var document = JsonDocument.Parse(request);
        var reasoning = document.RootElement.GetProperty("input").EnumerateArray()
            .Where(item => item.TryGetProperty("type", out var type) && type.GetString() == "reasoning")
            .Select(item => item.Clone()).ToArray();
        Assert.NotEmpty(reasoning);
        Assert.Contains("opaque-reasoning", JsonSerializer.Serialize(reasoning), StringComparison.Ordinal);
        return MafToolProtocolCodec.Canonicalize(JsonSerializer.SerializeToElement(reasoning));
    }

    private static AgentRuntimeExecutionOptions Options(AgentToolAdmissionJournalFixture fixture)
        => new(null, AgentFinalizerMode.Disabled, true, 0) {
            AdmittedToolSession = fixture.Session,
            RequireDurableToolProtocol = true,
            Governance = AgentTurnContextMetadata.TryReadExecutionGovernanceSnapshot(fixture.Detail.Run.MetadataJson),
            AuthorityPolicyFingerprint = "fixture-authority",
            ToolsetFingerprint = "fixture-toolset",
            ModelContextDigest = "fixture-context",
            CapabilityPolicyFingerprint = "fixture-capabilities",
            HistoryMode = AgentChatHistoryMode.FrameworkManaged,
            ContextIntent = AgentRuntimeContextIntent.Empty with { Purpose = AgentRuntimeContextPurpose.InteractiveChat }
        };

    private static ProviderProfile Provider(AgentToolAdmissionJournalFixture fixture, WireProvider provider)
        => fixture.Provider with {
            Kind = provider == WireProvider.Ollama ? ProviderKind.Ollama : ProviderKind.OpenAi,
            Transport = provider == WireProvider.Responses ? ProviderTransportKind.Responses : ProviderTransportKind.ChatCompletions
        };

    private static async Task<AgentResponse> RunAsync(AIAgent agent, AgentSession session, List<ChatMessage> input, bool streaming) {
        if (!streaming) {
            return await agent.RunAsync(input, session);
        }

        var updates = new List<AgentResponseUpdate>();
        await foreach (var update in agent.RunStreamingAsync(input, session)) {
            updates.Add(update);
        }

        return updates.ToAgentResponse();
    }

    private static IChatClient CreateClient(WireProvider provider, NativeWireHandler handler) {
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://provider.example.test") };
        if (provider == WireProvider.Ollama) {
            return new OllamaApiClient(http, "fixture-model", jsonSerializerContext: null);
        }

        var openAi = new OpenAIClient(new ApiKeyCredential("fixture-key"), new OpenAIClientOptions {
            Endpoint = new Uri("https://provider.example.test/v1"), Transport = new HttpClientPipelineTransport(http)
        });
        return provider == WireProvider.Responses
            ? openAi.GetResponsesClient().AsIChatClientWithStoredOutputDisabled("fixture-model", includeReasoningEncryptedContent: true)
            : openAi.GetChatClient("fixture-model").AsIChatClient();
    }

    public enum WireProvider { Responses, ChatCompletions, Ollama }
    private sealed record Runtime(AIAgent Agent, RuntimeCapabilityState Capabilities);
    private sealed class EffectProbe {
        internal List<Guid> Intents { get; } = [];
        internal int Active { get; set; }
        internal int MaximumActive { get; set; }
    }
    private sealed class EmptyScope : IAsyncDisposable {
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class GatedStreamingClient(bool proposeTool) : IChatClient {
        internal TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal bool Completed { get; private set; }

        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            CancellationToken cancellationToken = default) => throw new NotSupportedException("This proof requires the actual SDK streaming path.");

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages,
            ChatOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default) {
            yield return new ChatResponseUpdate(ChatRole.Assistant, "Visible prefix") { MessageId = "stream-message" };
            await Release.Task.WaitAsync(cancellationToken);
            if (proposeTool) {
                yield return new ChatResponseUpdate(ChatRole.Assistant, [new FunctionCallContent(
                    "stream-call", "admission_fixture_create", new Dictionary<string, object?> { ["value"] = "alpha" })]) {
                    MessageId = "stream-message"
                };
            }

            yield return new ChatResponseUpdate(ChatRole.Assistant, "Visible suffix") { MessageId = "stream-message" };
            Completed = true;
        }

        public object? GetService(Type serviceType, object? serviceKey = null)
            => serviceKey is null && serviceType.IsInstanceOfType(this) ? this : null;

        public void Dispose() {
        }
    }

    private sealed class NativeWireHandler(WireProvider provider, bool complete, bool denyRequests = false, bool completeAfterFirst = false) : HttpMessageHandler {
        internal List<string> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
            var json = await request.Content!.ReadAsStringAsync(cancellationToken);
            Requests.Add(json);
            if (completeAfterFirst && Requests.Count > 1) {
                complete = true;
            }

            Assert.False(denyRequests, "A completed saved provider response must replay locally.");
            using var parsed = JsonDocument.Parse(json);
            var streaming = parsed.RootElement.TryGetProperty("stream", out var stream) && stream.GetBoolean();
            var body = provider switch {
                WireProvider.Responses => Responses(streaming),
                WireProvider.ChatCompletions => Completions(streaming),
                WireProvider.Ollama => Ollama(),
                _ => throw new ArgumentOutOfRangeException(nameof(provider))
            };
            return new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8,
                streaming && provider != WireProvider.Ollama ? "text/event-stream" : "application/json") };
        }

        private string Responses(bool streaming) {
            object[] output = complete
                ? [new { type = "message", id = "message_done", role = "assistant", status = "completed",
                    content = new[] { new { type = "output_text", text = "completed", annotations = Array.Empty<object>() } } }]
                : [new { type = "reasoning", id = "reasoning_1", summary = Array.Empty<object>(), encrypted_content = "opaque-reasoning",
                        provider_extension = new { preserve = true } },
                    FunctionItem(0), FunctionItem(1)];
            var response = new { id = complete ? "response_done" : "response_proposed", @object = "response", created_at = 1785710401,
                status = "completed", model = "fixture-model", output, parallel_tool_calls = false, tools = Array.Empty<object>(),
                usage = new { input_tokens = 4, output_tokens = 2, total_tokens = 6 } };
            if (!streaming) {
                return JsonSerializer.Serialize(response);
            }

            var events = new List<string>();
            var sequence = 0;
            for (var index = 0; index < output.Length; index++) {
                events.Add(Event("response.output_item.added", new { type = "response.output_item.added", sequence_number = sequence++, output_index = index, item = output[index] }));
                if (complete) {
                    events.Add(Event("response.output_text.delta", new { type = "response.output_text.delta", sequence_number = sequence++, output_index = index,
                        item_id = "message_done", content_index = 0, delta = "completed" }));
                }

                events.Add(Event("response.output_item.done", new { type = "response.output_item.done", sequence_number = sequence++, output_index = index, item = output[index] }));
            }

            events.Add(Event("response.completed", new { type = "response.completed", sequence_number = sequence, response }));
            return string.Join(string.Empty, events);
        }

        private string Completions(bool streaming) {
            var calls = Enumerable.Range(0, 2).Select(index => new { index, id = $"call_{index}", type = "function",
                function = new { name = "admission_fixture_create", arguments = "{\"value\":\"alpha\"}" } }).ToArray();
            object message = complete ? new { role = "assistant", content = "completed" } : new { role = "assistant", tool_calls = calls };
            var finish = complete ? "stop" : "tool_calls";
            if (!streaming) {
                return JsonSerializer.Serialize(new { id = "completion_1", @object = "chat.completion", created = 1785710401,
                    model = "fixture-model", choices = new[] { new { index = 0, message, finish_reason = finish } },
                    usage = new { prompt_tokens = 4, completion_tokens = 2, total_tokens = 6 } });
            }

            return "data: " + JsonSerializer.Serialize(new { id = "completion_1", @object = "chat.completion.chunk", created = 1785710401,
                model = "fixture-model", choices = new[] { new { index = 0, delta = message, finish_reason = finish } } }) + "\n\ndata: [DONE]\n\n";
        }

        private string Ollama() {
            object message = complete
                ? new { role = "assistant", content = "completed" }
                : new { role = "assistant", content = string.Empty, tool_calls = Enumerable.Range(0, 2)
                    .Select(index => new { function = new { index, name = "admission_fixture_create", arguments = new { value = "alpha" } } }).ToArray() };
            return JsonSerializer.Serialize(new { model = "fixture-model", created_at = "2026-08-03T12:00:00Z", message,
                done = true, done_reason = complete ? "stop" : "tool_calls", prompt_eval_count = 4, eval_count = 2 }) + "\n";
        }

        private static object FunctionItem(int index) => new { type = "function_call", id = $"function_{index}", call_id = $"call_{index}",
            name = "admission_fixture_create", arguments = "{\"value\":\"alpha\"}", status = "completed" };
        private static string Event(string name, object value) => $"event: {name}\ndata: {JsonSerializer.Serialize(value)}\n\n";
    }
}
#pragma warning restore OPENAI001, MAAI001
