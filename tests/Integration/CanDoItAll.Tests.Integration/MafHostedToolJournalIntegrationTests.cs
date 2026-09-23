using CanDoItAll.AgentFramework.Tooling;
using System.ClientModel;
using System.ClientModel.Primitives;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.SharedProviders.Http;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Responses;

namespace CanDoItAll.Tests.Integration.Runtime;

#pragma warning disable OPENAI001, MAAI001
[Trait("Category", "FileSystemPortability")]
public sealed class MafHostedToolJournalIntegrationTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Hosted_sdk_approval_keeps_exact_protocol_and_replays_the_committed_response_without_another_request(bool streaming) {
        await using var fixture = await AgentToolAdmissionJournalFixture.CreateAsync();
        var proposedWire = new HostedWire();
        var pending = await ProposeAsync(fixture, proposedWire, streaming);
        Assert.Single(proposedWire.Requests);
        Assert.Equal(0, proposedWire.Effects);
        await fixture.ApproveAsync(pending);

        var effectWire = new HostedWire();
        await ContinueAsync(fixture, effectWire, streaming);
        Assert.Single(effectWire.Requests);
        Assert.Equal(1, effectWire.Effects);
        var baseline = await BaselineApprovalRequestAsync(streaming);
        Assert.Equal(ApprovalProtocol(baseline), ApprovalProtocol(effectWire.Requests[0]));

        var replayWire = new HostedWire(denyAllRequests: true);
        await ContinueAsync(fixture, replayWire, streaming);
        Assert.Empty(replayWire.Requests);
        Assert.Equal(0, replayWire.Effects);
        var saved = (await fixture.NewStore().GetExecutionRunAsync(fixture.Session.ExecutionRunId))!.ToolAdmission!;
        Assert.Equal(2, saved.ProviderDispatches.Length);
        Assert.All(saved.ProviderDispatches, dispatch => Assert.Equal(AgentToolProviderDispatchState.ResponseAdmitted, dispatch.State));
        var proposal = Assert.Single(saved.Batches[0].Proposals);
        Assert.Equal(pending[0].ToolAdmission!.IntentId, proposal.IntentId);
        Assert.Equal(AgentToolProposalState.Completed, proposal.State);
        Assert.Equal(AgentToolEffectState.Unknown, proposal.EffectState);
        Assert.NotNull(proposal.ProviderCall);
        Assert.DoesNotContain("fixture-header-secret", JsonSerializer.Serialize(saved), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Hosted_effect_then_failed_response_is_not_retried_by_sdk_or_after_store_restart(bool streaming) {
        await using var fixture = await AgentToolAdmissionJournalFixture.CreateAsync();
        var pending = await ProposeAsync(fixture, new HostedWire(), streaming);
        await fixture.ApproveAsync(pending);
        var failureWire = new HostedWire(failAfterEffect: true);
        await Assert.ThrowsAnyAsync<Exception>(() => ContinueAsync(fixture, failureWire, streaming));
        Assert.Single(failureWire.Requests);
        Assert.Equal(1, failureWire.Effects);
        var persisted = (await fixture.NewStore().GetExecutionRunAsync(fixture.Session.ExecutionRunId))!.ToolAdmission!;
        Assert.Equal(AgentToolProviderDispatchState.Started, persisted.ProviderDispatches[^1].State);
        Assert.Equal(AgentToolProposalState.Executing, Assert.Single(persisted.Batches[0].Proposals).State);

        var replayWire = new HostedWire(denyAllRequests: true);
        var failure = await Assert.ThrowsAsync<AgentToolAdmissionException>(() => ContinueAsync(fixture, replayWire, streaming));
        Assert.Equal("tool-admission.reconciliation-required", failure.Code);
        Assert.Empty(replayWire.Requests);
        Assert.Equal(0, replayWire.Effects);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Unapproved_native_mode_also_records_uncertainty_before_the_first_remote_effect(bool streaming) {
        await using var fixture = await AgentToolAdmissionJournalFixture.CreateAsync();
        var wire = new HostedWire(failAfterEffect: true, requiresApproval: false);
        var journal = fixture.NewJournal();
        await using (var lease = await journal.AcquireRunAsync(fixture.Session, default)) {
            using var bound = lease.Bind();
            using var client = CreateClient(wire);
            var runtime = CreateRuntime(client, approvalRequired: false);
            var opened = await OpenAsync(fixture, journal, lease, runtime);
            using var active = opened.Context.Bind();
            await Assert.ThrowsAnyAsync<Exception>(() => RunAsync(runtime.Agent, opened.Session, opened.Input, streaming));
        }

        Assert.Single(wire.Requests);
        Assert.Equal(1, wire.Effects);
        var saved = (await fixture.NewStore().GetExecutionRunAsync(fixture.Session.ExecutionRunId))!.ToolAdmission!;
        Assert.Empty(saved.Batches);
        Assert.Equal(AgentToolProviderDispatchState.Started, Assert.Single(saved.ProviderDispatches).State);
        var restarted = fixture.NewJournal(fixture.NewStore());
        await using var restart = await restarted.AcquireRunAsync(fixture.Session, default);
        using var current = restart.Bind();
        var never = new HostedWire(denyAllRequests: true, requiresApproval: false);
        using var restartedClient = CreateClient(never);
        await Assert.ThrowsAsync<AgentToolAdmissionException>(() => OpenAsync(fixture, restarted, restart,
            CreateRuntime(restartedClient, approvalRequired: false)));
        Assert.Empty(never.Requests);
    }

    [Fact]
    public async Task Two_distinct_hosted_servers_can_open_one_admitted_runtime_despite_the_sdk_shared_tool_name() {
        await using var fixture = await AgentToolAdmissionJournalFixture.CreateAsync();
        var journal = fixture.NewJournal();
        await using var lease = await journal.AcquireRunAsync(fixture.Session, default);
        using var bound = lease.Bind();
        var wire = new HostedWire();
        using var client = CreateClient(wire);
        var runtime = CreateRuntime(client, secondServer: true);
        Assert.Equal(2, runtime.Capabilities.Tools.Count(tool => tool.Name == "mcp"));
        var opened = await OpenAsync(fixture, journal, lease, runtime);
        Assert.NotNull(opened.Context);
        Assert.Empty(wire.Requests);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Mixed_native_approval_and_local_function_drains_local_serial_work_then_publishes_approval(bool streaming) {
        await using var fixture = await AgentToolAdmissionJournalFixture.CreateAsync();
        var journal = fixture.NewJournal();
        var calls = 0;
        IReadOnlyList<PendingToolApprovalRecord> pending;
        var firstWire = new HostedWire(includeLocalFunction: true);
        await using (var lease = await journal.AcquireRunAsync(fixture.Session, default)) {
            using var bound = lease.Bind();
            using var client = CreateClient(firstWire);
            var runtime = CreateRuntime(client, localRead: () => calls++);
            var opened = await OpenAsync(fixture, journal, lease, runtime);
            using var active = opened.Context.Bind();
            var response = await RunAsync(runtime.Agent, opened.Session, opened.Input, streaming);
            Assert.Equal(1, calls);
            Assert.Single(firstWire.Requests);
            Assert.Equal(0, firstWire.Effects);
            var approval = Assert.Single(response.Messages.SelectMany(message => message.Contents).OfType<ToolApprovalRequestContent>());
            var records = new[] { new MafApprovalContinuationDriver().MapPendingApproval(approval) };
            var serialized = await new MafRuntimeSessionPersistenceDriver().TrySerializePersistableRuntimeSessionAsync(
                runtime.Agent, opened.Session, fixture.Provider, fixture.Provider.DefaultModel, Options(fixture, runtime),
                records, (_, _, _) => Task.CompletedTask, default);
            pending = await opened.Context.SaveApprovalsAsync(Assert.IsType<string>(serialized), records, default);
        }
        await fixture.ApproveAsync(pending);
        var restarted = fixture.NewJournal(fixture.NewStore());
        await using var restart = await restarted.AcquireRunAsync(fixture.Session, default);
        using var current = restart.Bind();
        var effectWire = new HostedWire(includeLocalFunction: true);
        using var effectClient = CreateClient(effectWire);
        var nextRuntime = CreateRuntime(effectClient, localRead: () => calls++);
        var next = await OpenAsync(fixture, restarted, restart, nextRuntime);
        using var nextScope = next.Context.Bind();
        Assert.Contains("completed", (await RunAsync(nextRuntime.Agent, next.Session, next.Input, streaming)).Text, StringComparison.Ordinal);
        Assert.Equal(1, calls);
        Assert.Equal(1, effectWire.Effects);
        Assert.Single(effectWire.Requests);
    }

    [Fact]
    public async Task Streaming_text_stays_visible_while_a_native_request_is_durably_pending() {
        await using var fixture = await AgentToolAdmissionJournalFixture.CreateAsync();
        var journal = fixture.NewJournal();
        await using var lease = await journal.AcquireRunAsync(fixture.Session, default);
        using var bound = lease.Bind();
        using var client = new PrefixClient();
        var runtime = CreateRuntime(client);
        var opened = await OpenAsync(fixture, journal, lease, runtime);
        using var active = opened.Context.Bind();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var updates = runtime.Agent.RunStreamingAsync(opened.Input, opened.Session, cancellationToken: timeout.Token)
            .GetAsyncEnumerator(timeout.Token);
        try {
            while (await updates.MoveNextAsync()) {
                if (updates.Current.Text.Contains("Visible prefix", StringComparison.Ordinal)) {
                    break;
                }
            }
            Assert.False(client.Finished);
            var saved = (await fixture.NewStore().GetExecutionRunAsync(fixture.Session.ExecutionRunId))!.ToolAdmission!;
            Assert.Empty(saved.Batches);
            Assert.Equal(AgentToolProviderDispatchState.Started, Assert.Single(saved.ProviderDispatches).State);
        } finally {
            client.Release.TrySetResult();
        }
        while (await updates.MoveNextAsync()) { }
        Assert.True(client.Finished);
        Assert.Equal(AgentToolProviderDispatchState.ResponseAdmitted,
            Assert.Single((await journal.ReadAsync(lease, default)).ProviderDispatches).State);
    }

    private static async Task<IReadOnlyList<PendingToolApprovalRecord>> ProposeAsync(
        AgentToolAdmissionJournalFixture fixture, HostedWire wire, bool streaming) {
        var journal = fixture.NewJournal();
        await using var lease = await journal.AcquireRunAsync(fixture.Session, default);
        using var bound = lease.Bind();
        using var client = CreateClient(wire);
        var runtime = CreateRuntime(client);
        var opened = await OpenAsync(fixture, journal, lease, runtime);
        using var active = opened.Context.Bind();
        var response = await RunAsync(runtime.Agent, opened.Session, opened.Input, streaming);
        var request = Assert.Single(response.Messages.SelectMany(message => message.Contents).OfType<ToolApprovalRequestContent>());
        var call = Assert.IsType<McpServerToolCallContent>(request.ToolCall);
        Assert.Equal("workspace", call.ServerName);
        Assert.Equal("write_file", call.Name);
        var pending = new[] { new MafApprovalContinuationDriver().MapPendingApproval(request) };
        var serialized = await new MafRuntimeSessionPersistenceDriver().TrySerializePersistableRuntimeSessionAsync(
            runtime.Agent, opened.Session, fixture.Provider, fixture.Provider.DefaultModel, Options(fixture, runtime),
            pending, (_, _, _) => Task.CompletedTask, default);
        return await opened.Context.SaveApprovalsAsync(Assert.IsType<string>(serialized), pending, default);
    }

    private static async Task ContinueAsync(AgentToolAdmissionJournalFixture fixture, HostedWire wire, bool streaming) {
        var journal = fixture.NewJournal(fixture.NewStore());
        await using var lease = await journal.AcquireRunAsync(fixture.Session, default);
        using var bound = lease.Bind();
        using var client = CreateClient(wire);
        var runtime = CreateRuntime(client);
        var opened = await OpenAsync(fixture, journal, lease, runtime);
        using var active = opened.Context.Bind();
        var response = await RunAsync(runtime.Agent, opened.Session, opened.Input, streaming);
        Assert.Contains("completed", response.Text, StringComparison.Ordinal);
    }

    private static async Task<(MafToolRunContext Context, AgentSession Session, List<ChatMessage> Input)> OpenAsync(
        AgentToolAdmissionJournalFixture fixture, AgentToolAdmissionJournal journal, AgentToolRunLease lease, Runtime runtime)
        => await MafToolRunContext.OpenAsync(journal, lease, runtime.Agent, await runtime.Agent.CreateSessionAsync(),
            fixture.Agent, fixture.Provider, fixture.Provider.DefaultModel, fixture.Detail.ChatSession!, Options(fixture, runtime),
            runtime.Capabilities, [new(ChatRole.User, "Write the reviewed file.")], new MafRuntimeSessionPersistenceDriver(), false,
            (_, _, _) => Task.CompletedTask, default);

    private static AgentRuntimeExecutionOptions Options(AgentToolAdmissionJournalFixture fixture, Runtime runtime)
        => new(null, AgentFinalizerMode.Disabled, true, 0) {
            AdmittedToolSession = fixture.Session, RequireDurableToolProtocol = true,
            Governance = AgentTurnContextMetadata.TryReadExecutionGovernanceSnapshot(fixture.Detail.Run.MetadataJson),
            AuthorityPolicyFingerprint = "fixture-authority", ModelContextDigest = "fixture-context",
            CapabilityPolicyFingerprint = "fixture-capabilities", HistoryMode = AgentChatHistoryMode.FrameworkManaged,
            ToolsetFingerprint = MafToolsetFingerprint.ComputeContractFingerprint(runtime.Capabilities.Tools),
            ContextIntent = AgentRuntimeContextIntent.Empty with { Purpose = AgentRuntimeContextPurpose.InteractiveChat }
        };

    private static Runtime CreateRuntime(IChatClient client, bool approvalRequired = true, bool wrapped = true,
        bool secondServer = false, Action? localRead = null) {
        var tool = new HostedMcpServerTool("workspace", "https://mcp.example.test") {
            ApprovalMode = approvalRequired ? HostedMcpServerToolApprovalMode.AlwaysRequire : HostedMcpServerToolApprovalMode.NeverRequire,
            AllowedTools = ["write_file"], Headers = new Dictionary<string, string> { ["Authorization"] = "fixture-header-secret" }
        };
        var capabilities = new RuntimeCapabilityState();
        capabilities.Tools.Add(tool);
        if (secondServer) {
            capabilities.Tools.Add(new HostedMcpServerTool("second", "https://second.example.test"));
        }
        if (localRead is not null) {
            capabilities.Tools.Add(AIFunctionFactory.Create(() => {
                localRead();
                return "local-read";
            }, "fixture_read"));
            capabilities.RuntimeToolMetadata.Add(new("hosted-journal-fixture", "fixture_read", AgentRuntimeToolOperationKind.Read, false) {
                AuthorizeResultDisclosureAsync = (_, _) => ValueTask.FromResult<IAsyncDisposable?>(null)
            });
        }
        var options = MafChatClientAgentOptionsFactory.Create(new ChatOptions {
            ModelId = "fixture-model", Tools = capabilities.Tools, AllowMultipleToolCalls = false
        });
        options.ChatHistoryProvider = new InMemoryChatHistoryProvider(new InMemoryChatHistoryProviderOptions {
            JsonSerializerOptions = MafToolProtocolCodec.SerializationOptions
        });
        options.RequirePerServiceCallChatHistoryPersistence = true;
        var agent = new ChatClientAgent(wrapped ? new MafToolAdmissionChatClient(client) : client, options).AsBuilder()
            .Use(async (_, invocation, next, token) => {
                if (!wrapped) {
                    return await next(invocation, token);
                }
                using var effects = AgentToolInvocationEffectScope.Begin();
                return await (MafToolRunContext.Current ?? throw new InvalidOperationException("The SDK invocation has no journal."))
                    .InvokeAsync(invocation.CallContent, async cancellation => await next(invocation, cancellation), effects, token);
            }).Build();
        return new(agent, capabilities);
    }

    private static IChatClient CreateClient(HostedWire wire) {
        var client = new OpenAIClient(new ApiKeyCredential("fixture-key"), new OpenAIClientOptions {
            Endpoint = new Uri("https://provider.example.test/v1"),
            Transport = new HttpClientPipelineTransport(new HttpClient(wire)), RetryPolicy = new MafNativeRequestRetryPolicy()
        });
        return client.GetResponsesClient().AsIChatClientWithStoredOutputDisabled("fixture-model", includeReasoningEncryptedContent: true);
    }

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

    private static async Task<string> BaselineApprovalRequestAsync(bool streaming) {
        var wire = new HostedWire();
        using var client = CreateClient(wire);
        var runtime = CreateRuntime(client, wrapped: false);
        var session = await runtime.Agent.CreateSessionAsync();
        var response = await RunAsync(runtime.Agent, session, [new(ChatRole.User, "Write the reviewed file.")], streaming);
        var request = Assert.Single(response.Messages.SelectMany(message => message.Contents).OfType<ToolApprovalRequestContent>());
        await RunAsync(runtime.Agent, session, [new(ChatRole.User, [request.CreateResponse(true)])], streaming);
        Assert.Equal(2, wire.Requests.Count);
        return wire.Requests[1];
    }

    private static string ApprovalProtocol(string request) {
        using var document = JsonDocument.Parse(request);
        var items = document.RootElement.GetProperty("input").EnumerateArray().Where(item =>
            item.TryGetProperty("type", out var type) && type.GetString() is "mcp_approval_request" or "mcp_approval_response")
            .Select(item => item.Clone()).ToArray();
        Assert.NotEmpty(items);
        return MafToolProtocolCodec.Canonicalize(JsonSerializer.SerializeToElement(items));
    }

    private sealed record Runtime(AIAgent Agent, RuntimeCapabilityState Capabilities);

    private sealed class HostedWire(bool failAfterEffect = false, bool denyAllRequests = false,
        bool requiresApproval = true, bool includeLocalFunction = false) : HttpMessageHandler {
        internal List<string> Requests { get; } = [];
        internal int Effects { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
            var json = await request.Content!.ReadAsStringAsync(cancellationToken);
            Requests.Add(json);
            Assert.False(denyAllRequests, "Saved provider responses must replay without a remote request.");
            using var document = JsonDocument.Parse(json);
            var approved = document.RootElement.GetProperty("input").EnumerateArray().Any(item =>
                item.TryGetProperty("type", out var type) && type.GetString() == "mcp_approval_response" && item.GetProperty("approve").GetBoolean());
            var completed = approved || !requiresApproval;
            if (completed) {
                Effects++;
                if (failAfterEffect) {
                    return new(HttpStatusCode.InternalServerError) {
                        Content = new StringContent("{\"error\":{\"message\":\"Remote response lost after effect\",\"type\":\"server_error\"}}", Encoding.UTF8, "application/json")
                    };
                }
            }
            object[] output = completed
                ? [new { type = "mcp_call", id = "mcp_call_1", name = "write_file", server_label = "workspace", arguments = "{\"path\":\"reviewed.txt\"}", output = "written", status = "completed" },
                    new { type = "message", id = "message_done", role = "assistant", status = "completed",
                        content = new[] { new { type = "output_text", text = "completed", annotations = Array.Empty<object>() } } }]
                : [new { type = "mcp_approval_request", id = "approval_1", name = "write_file", server_label = "workspace",
                    arguments = "{\"path\":\"reviewed.txt\"}", provider_extension = new { preserve = true } }];
            if (!completed && includeLocalFunction) {
                output = [.. output, new { type = "function_call", id = "function_read", call_id = "read_call",
                    name = "fixture_read", arguments = "{}", status = "completed" }];
            }
            var response = new { id = completed ? "response_done" : "response_proposed", @object = "response", created_at = 1785710401,
                status = "completed", model = "fixture-model", output, parallel_tool_calls = false, tools = Array.Empty<object>(),
                usage = new { input_tokens = 4, output_tokens = 2, total_tokens = 6 } };
            var streaming = document.RootElement.TryGetProperty("stream", out var stream) && stream.GetBoolean();
            if (!streaming) {
                return new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(response), Encoding.UTF8, "application/json") };
            }
            var events = new List<string>();
            var sequence = 0;
            for (var index = 0; index < output.Length; index++) {
                events.Add(Event("response.output_item.added", new { type = "response.output_item.added", sequence_number = sequence++, output_index = index, item = output[index] }));
                if (completed && index == output.Length - 1) {
                    events.Add(Event("response.output_text.delta", new { type = "response.output_text.delta", sequence_number = sequence++, output_index = index,
                        item_id = "message_done", content_index = 0, delta = "completed" }));
                }
                events.Add(Event("response.output_item.done", new { type = "response.output_item.done", sequence_number = sequence++, output_index = index, item = output[index] }));
            }
            events.Add(Event("response.completed", new { type = "response.completed", sequence_number = sequence, response }));
            return new(HttpStatusCode.OK) { Content = new StringContent(string.Join(string.Empty, events), Encoding.UTF8, "text/event-stream") };
        }

        private static string Event(string name, object value) => $"event: {name}\ndata: {JsonSerializer.Serialize(value)}\n\n";
    }

    private sealed class PrefixClient : IChatClient {
        internal TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal bool Finished { get; private set; }
        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages,
            ChatOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default) {
            yield return new(ChatRole.Assistant, "Visible prefix") { MessageId = "prefix" };
            await Release.Task.WaitAsync(cancellationToken);
            Finished = true;
            yield return new(ChatRole.Assistant, " completed") { MessageId = "prefix" };
        }
        public object? GetService(Type serviceType, object? serviceKey = null)
            => serviceKey is null && serviceType.IsInstanceOfType(this) ? this : null;
        public void Dispose() { }
    }
}
#pragma warning restore OPENAI001, MAAI001
