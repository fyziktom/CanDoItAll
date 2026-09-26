using System.Runtime.CompilerServices;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace CanDoItAll.Tests.Integration.Runtime;

public sealed partial class MafGenericToolAdmissionIntegrationTests {
    private const string AgentFacingPolicyReason =
        "Workspace tools must use grounded external-target aliases. Retry this structured workspace tool with 'external-target/v1/fixture/App' instead of 'C:\\fixture\\App'.";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Invocation_policy_denial_keeps_the_agent_facing_reason_in_the_durable_result(bool streaming) {
        await using var fixture = await AgentToolAdmissionJournalFixture.CreateAsync();
        var journal = fixture.NewJournal();
        await using var lease = await journal.AcquireRunAsync(fixture.Session, default);
        using var bound = lease.Bind();
        using var client = new PolicyReasonClient();
        var capabilities = new RuntimeCapabilityState();
        capabilities.Tools.Add(AIFunctionFactory.Create(
            (Func<string>)(() => throw new AgentToolPolicyBlockedException(
                "generic_fixture_read", ToolInvocationDecisionKind.Deny, AgentFacingPolicyReason)),
            "generic_fixture_read"));
        var options = MafChatClientAgentOptionsFactory.Create(new ChatOptions {
            ModelId = "fixture-model", Tools = capabilities.Tools, AllowMultipleToolCalls = false
        });
        options.ChatHistoryProvider = new InMemoryChatHistoryProvider(new InMemoryChatHistoryProviderOptions {
            JsonSerializerOptions = MafToolProtocolCodec.SerializationOptions
        });
        options.RequirePerServiceCallChatHistoryPersistence = true;
        var agent = new ChatClientAgent(new MafToolAdmissionChatClient(client), options).AsBuilder()
            .Use(async (_, invocation, next, token) => {
                using var effect = AgentToolInvocationEffectScope.Begin();
                return await MafToolRunContext.Current!.InvokeAsync(invocation.CallContent,
                    async cancellation => await next(invocation, cancellation), effect, token);
            }).Build();
        var runtime = new Runtime(agent, capabilities);
        var opened = await OpenAsync(fixture, journal, lease, runtime);
        using var active = opened.Context.Bind();

        Assert.Equal("finished", (await RunAsync(runtime.Agent, opened.Session, opened.Input, streaming)).Text);

        var proposal = Assert.Single((await journal.ReadAsync(lease, default)).Batches.SelectMany(batch => batch.Proposals));
        Assert.Equal(AgentToolProposalState.Completed, proposal.State);
        Assert.Equal(AgentToolEffectState.NotCommitted, proposal.EffectState);
        var checkpoint = Assert.IsType<AgentToolProtocolEnvelope>(proposal.Result);
        Assert.Contains("ToolPolicyDenied", checkpoint.PayloadJson, StringComparison.Ordinal);
        Assert.Contains("external-target/v1/fixture/App", checkpoint.PayloadJson, StringComparison.Ordinal);
        Assert.Contains("external-target/v1/fixture/App", client.ObservedToolResult, StringComparison.Ordinal);
    }

    private sealed class PolicyReasonClient : IChatClient {
        private int requests;

        internal string ObservedToolResult { get; private set; } = string.Empty;

        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            CancellationToken cancellationToken = default) {
            requests++;
            Assert.InRange(requests, 1, 2);
            if (requests == 1) {
                return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant,
                    [new FunctionCallContent("policy-read", "generic_fixture_read", new Dictionary<string, object?>())])) {
                    ResponseId = "policy-reason-1"
                });
            }

            ObservedToolResult = string.Join(" ", messages.SelectMany(message => message.Contents)
                .OfType<FunctionResultContent>()
                .Select(result => result.Result?.ToString() ?? string.Empty));
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "finished")) {
                ResponseId = "policy-reason-2"
            });
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages,
            ChatOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default) {
            var response = await GetResponseAsync(messages, options, cancellationToken);
            foreach (var update in response.ToChatResponseUpdates()) {
                yield return update;
            }
        }

        public object? GetService(Type serviceType, object? serviceKey = null)
            => serviceKey is null && serviceType.IsInstanceOfType(this) ? this : null;

        public void Dispose() {
        }
    }
}
