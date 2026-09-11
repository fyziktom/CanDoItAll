using System.Runtime.CompilerServices;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace CanDoItAll.Tests.Integration.Runtime;

[Trait("Category", "FileSystemPortability")]
public sealed partial class MafGenericToolAdmissionIntegrationTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Generic_mixed_function_batch_preserves_the_sdk_peer_approval_rule_and_serial_execution(bool streaming) {
        await using var fixture = await AgentToolAdmissionJournalFixture.CreateAsync();
        var invoked = new List<string>();
        var first = fixture.NewJournal();
        IReadOnlyList<PendingToolApprovalRecord> pending;
        await using (var lease = await first.AcquireRunAsync(fixture.Session, default)) {
            using var bound = lease.Bind();
            using var client = new ScriptClient(complete: false);
            var runtime = CreateRuntime(client, invoked);
            var opened = await OpenAsync(fixture, first, lease, runtime);
            using var active = opened.Context.Bind();
            var response = await RunAsync(runtime.Agent, opened.Session, opened.Input, streaming);
            Assert.Empty(invoked);
            var requests = response.Messages.SelectMany(message => message.Contents).OfType<ToolApprovalRequestContent>().ToArray();
            Assert.Equal(2, requests.Length);
            var records = requests.Select(new MafApprovalContinuationDriver().MapPendingApproval).ToArray();
            var serialized = await new MafRuntimeSessionPersistenceDriver().TrySerializePersistableRuntimeSessionAsync(
                runtime.Agent, opened.Session, fixture.Provider, fixture.Provider.DefaultModel, Options(fixture, runtime), records,
                (_, _, _) => Task.CompletedTask, default);
            pending = await opened.Context.SaveApprovalsAsync(Assert.IsType<string>(serialized), records, default);
            var saved = await first.ReadAsync(lease, default);
            Assert.All(saved.Batches[0].Proposals, proposal => {
                Assert.True(proposal.RequiresApproval);
                Assert.Equal(ExecutionApprovalStatus.Pending, proposal.ApprovalStatus);
            });
        }

        await fixture.ApproveAsync(pending);
        var restarted = fixture.NewJournal(fixture.NewStore());
        await using (var current = await restarted.AcquireRunAsync(fixture.Session, default)) {
            using var currentScope = current.Bind();
            using var complete = new ScriptClient(complete: true);
            var resumed = CreateRuntime(complete, invoked);
            var restored = await OpenAsync(fixture, restarted, current, resumed);
            using var runtimeScope = restored.Context.Bind();
            Assert.Contains("completed", (await RunAsync(resumed.Agent, restored.Session, restored.Input, streaming)).Text, StringComparison.Ordinal);
            Assert.Equal(["read", "write"], invoked);
            Assert.Equal(1, complete.Requests);
            Assert.All((await restarted.ReadAsync(current, default)).Batches[0].Proposals,
                proposal => Assert.Equal(AgentToolProposalState.Completed, proposal.State));
        }

        var replay = fixture.NewJournal(fixture.NewStore());
        await using var replayLease = await replay.AcquireRunAsync(fixture.Session, default);
        using var replayScope = replayLease.Bind();
        using var never = new ScriptClient(complete: true);
        var denied = await Assert.ThrowsAsync<AgentToolAdmissionException>(() =>
            OpenAsync(fixture, replay, replayLease, CreateRuntime(never, invoked)));
        Assert.Equal("tool-admission.disclosure-authorization-unavailable", denied.Code);
        Assert.Equal(0, never.Requests);
        Assert.Equal(["read", "write"], invoked);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Generic_effect_with_an_unsupported_result_stays_uncertain_and_never_automatically_redispatches(bool streaming) {
        await using var fixture = await AgentToolAdmissionJournalFixture.CreateAsync();
        var invoked = new List<string>();
        var initial = fixture.NewJournal();
        using var client = new ScriptClient(complete: false, unsupportedResult: true);
        await using (var lease = await initial.AcquireRunAsync(fixture.Session, default)) {
            using var bound = lease.Bind();
            var runtime = CreateRuntime(client, invoked, unsupportedResult: true);
            var opened = await OpenAsync(fixture, initial, lease, runtime);
            using var active = opened.Context.Bind();
            await Assert.ThrowsAnyAsync<Exception>(() => RunAsync(runtime.Agent, opened.Session, opened.Input, streaming));
        }
        Assert.Equal(["write"], invoked);
        Assert.Equal(1, client.Requests);
        var persisted = (await fixture.NewStore().GetExecutionRunAsync(fixture.Session.ExecutionRunId))!.ToolAdmission!;
        Assert.Equal(AgentToolProposalRecovery.ReconcileBeforeRetry, Assert.Single(persisted.Batches[0].Proposals).Payload.Recovery);
        Assert.Equal(AgentToolProposalState.ReconciliationRequired, persisted.Batches[0].Proposals[0].State);
        var restarted = fixture.NewJournal(fixture.NewStore());
        await using var current = await restarted.AcquireRunAsync(fixture.Session, default);
        using var currentScope = current.Bind();
        using var never = new ScriptClient(complete: true);
        var denied = await Assert.ThrowsAsync<AgentToolAdmissionException>(() => OpenAsync(fixture, restarted, current,
            CreateRuntime(never, invoked, unsupportedResult: true)));
        Assert.Equal("tool-admission.reconciliation-required", denied.Code);
        Assert.Equal(0, never.Requests);
        Assert.Equal(["write"], invoked);
    }

    private static Runtime CreateRuntime(IChatClient client, List<string> invoked, bool unsupportedResult = false) {
        var capabilities = new RuntimeCapabilityState();
        if (!unsupportedResult) {
            capabilities.Tools.Add(AIFunctionFactory.Create(() => {
                invoked.Add("read");
                return "summary";
            }, "generic_fixture_read"));
        }
        var write = AIFunctionFactory.Create(() => {
            invoked.Add("write");
            return unsupportedResult ? (object)(Action)(() => { }) : "written";
        }, "generic_fixture_write");
        capabilities.Tools.Add(unsupportedResult ? write : new ApprovalRequiredAIFunction(write));
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
        return new(agent, capabilities);
    }

    private static AgentRuntimeExecutionOptions Options(AgentToolAdmissionJournalFixture fixture, Runtime runtime)
        => new(null, AgentFinalizerMode.Disabled, true, 0) {
            AdmittedToolSession = fixture.Session, RequireDurableToolProtocol = true,
            Governance = AgentTurnContextMetadata.TryReadExecutionGovernanceSnapshot(fixture.Detail.Run.MetadataJson),
            AuthorityPolicyFingerprint = "fixture-authority", ModelContextDigest = "fixture-context",
            CapabilityPolicyFingerprint = "fixture-capabilities", HistoryMode = AgentChatHistoryMode.FrameworkManaged,
            ToolsetFingerprint = MafToolsetFingerprint.ComputeContractFingerprint(runtime.Capabilities.Tools),
            ContextIntent = AgentRuntimeContextIntent.Empty with { Purpose = AgentRuntimeContextPurpose.InteractiveChat }
        };

    private static async Task<(MafToolRunContext Context, AgentSession Session, List<ChatMessage> Input)> OpenAsync(
        AgentToolAdmissionJournalFixture fixture, AgentToolAdmissionJournal journal, AgentToolRunLease lease, Runtime runtime)
        => await MafToolRunContext.OpenAsync(journal, lease, runtime.Agent, await runtime.Agent.CreateSessionAsync(),
            fixture.Agent, fixture.Provider, fixture.Provider.DefaultModel, fixture.Detail.ChatSession!, Options(fixture, runtime),
            runtime.Capabilities, [new(ChatRole.User, "Perform the reviewed work.")], new MafRuntimeSessionPersistenceDriver(), false,
            (_, _, _) => Task.CompletedTask, default);

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

    private sealed record Runtime(AIAgent Agent, RuntimeCapabilityState Capabilities);

    private sealed class ScriptClient(bool complete, bool unsupportedResult = false) : IChatClient {
        internal int Requests { get; private set; }
        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            CancellationToken cancellationToken = default) {
            Requests++;
            if (complete) {
                return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "completed")));
            }
            List<AIContent> contents = unsupportedResult
                ? [new FunctionCallContent("write", "generic_fixture_write", new Dictionary<string, object?>())]
                : [new FunctionCallContent("read", "generic_fixture_read", new Dictionary<string, object?>()),
                    new FunctionCallContent("write", "generic_fixture_write", new Dictionary<string, object?>())];
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, contents)));
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
        public void Dispose() { }
    }
}
