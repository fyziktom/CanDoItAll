using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Tooling;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace CanDoItAll.Tests.Integration.Runtime;

[Trait("Category", "FileSystemPortability")]
public sealed class MafPolicyDenialCheckpointTests {
    [Theory]
    [InlineData(ToolInvocationClassification.Read)]
    [InlineData(ToolInvocationClassification.Mutation)]
    public async Task Denial_checkpoint_preserves_legacy_text_and_typed_failure_after_independent_file_store_restart(
        ToolInvocationClassification classification) {
        const string denialText = "PolicyDenied: The original invocation was denied before dispatch.";
        await using var fixture = await AgentToolAdmissionJournalFixture.CreateAsync();
        var toolName = classification == ToolInvocationClassification.Read
            ? ToolContractCatalog.WorkspaceReadFile : ToolContractCatalog.WorkspaceWriteFile;
        var arguments = new Dictionary<string, object?> { ["path"] = "managed/evidence.txt" };
        var json = JsonSerializer.Serialize(arguments, MafToolProtocolCodec.SerializationOptions);
        var payload = new AgentToolPreparedPayload(toolName, 1, AgentToolProtocolEnvelope.ComputeDigest(json), json,
            classification == ToolInvocationClassification.Read ? AgentToolProposalEffect.Read : AgentToolProposalEffect.Mutation,
            classification == ToolInvocationClassification.Read ? AgentToolProposalRecovery.RevalidateAndRead : AgentToolProposalRecovery.OwnerReceipt);
        var call = new FunctionCallContent("denied-call", toolName, arguments);
        var request = MafToolProtocolCodec.Digest(new { Stage = "pre-dispatch-denial" });
        var dispatches = 0;
        var effects = 0;
        for (var attempt = 0; attempt < 2; attempt++) {
            var journal = fixture.NewJournal(fixture.NewStore());
            await using var lease = await journal.AcquireRunAsync(fixture.Session, default);
            using var bound = lease.Bind();
            using var client = new NoRequestClient();
            var function = AIFunctionFactory.Create((string path) => {
                effects++;
                return path;
            }, toolName);
            var capabilities = new RuntimeCapabilityState();
            capabilities.Tools.Add(function);
            capabilities.RuntimeToolMetadata.Add(new("fixture-policy", toolName,
                classification == ToolInvocationClassification.Read ? AgentRuntimeToolOperationKind.Read : AgentRuntimeToolOperationKind.Mutation,
                false) {
                PrepareAdmission = _ => payload,
                AuthorizeAdmissionAsync = async (_, token) => {
                    await journal.RequireSessionAsync(fixture.Session, token);
                    return new EmptyScope();
                },
                AuthorizeResultDisclosureAsync = async (_, token) => {
                    await journal.RequireSessionAsync(fixture.Session, token);
                    return new EmptyScope();
                }
            });
            var options = MafChatClientAgentOptionsFactory.Create(new ChatOptions {
                ModelId = fixture.Provider.DefaultModel,
                Tools = [function]
            });
            options.ChatHistoryProvider = new InMemoryChatHistoryProvider(new InMemoryChatHistoryProviderOptions {
                JsonSerializerOptions = MafToolProtocolCodec.SerializationOptions
            });
            options.RequirePerServiceCallChatHistoryPersistence = true;
            var runtime = new ChatClientAgent(client, options);
            var executionOptions = new AgentRuntimeExecutionOptions(null, AgentFinalizerMode.Disabled, true, 0) {
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
            var opened = await MafToolRunContext.OpenAsync(journal, lease, runtime, await runtime.CreateSessionAsync(),
                fixture.Agent, fixture.Provider, fixture.Provider.DefaultModel, fixture.Detail.ChatSession!, executionOptions,
                capabilities, [new(ChatRole.User, "Use the admitted tool.")], new MafRuntimeSessionPersistenceDriver(), false,
                (_, _, _) => Task.CompletedTask, default);
            using var active = opened.Context.Bind();
            if (attempt == 0) {
                await opened.Context.AdmitResponseAsync(request,
                    MafToolProtocolCodec.Encode(new ChatResponse(new ChatMessage(ChatRole.Assistant, [call]))), [call], default);
            } else {
                Assert.NotNull(await opened.Context.ReplayResponseAsync(request, default));
            }
            using var capture = AgentToolInvocationEffectScope.Begin();
            var result = await opened.Context.InvokeAsync(call, _ => {
                dispatches++;
                AgentToolInvocationEffectScope.RecordPreDispatchFailure(new("ToolPolicyDenied", denialText));
                return ValueTask.FromResult<object?>(denialText);
            }, capture, default);
            Assert.Equal(denialText, Assert.IsType<string>(result));
            var assessment = MafRuntimeToolInvocationResultClassifier.Assess(toolName, classification, result, capture.PreDispatchFailure);
            Assert.False(assessment.Succeeded);
            Assert.Equal(AgentToolInvocationOutcome.Failed, assessment.Outcome);
            Assert.Equal(AgentToolEffectState.NotCommitted, assessment.EffectState);
            Assert.Equal("ToolPolicyDenied", assessment.FailureCode);
            Assert.Equal(denialText, assessment.FailureMessage);
            var proposal = Assert.Single(Assert.Single((await journal.ReadAsync(lease, default)).Batches).Proposals);
            Assert.Equal(AgentToolProposalState.Completed, proposal.State);
            Assert.Equal(AgentToolEffectState.NotCommitted, proposal.EffectState);
            Assert.NotNull(proposal.Result);
        }
        Assert.Equal(1, dispatches);
        Assert.Equal(0, effects);
    }

    private sealed class EmptyScope : IAsyncDisposable {
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class NoRequestClient : IChatClient {
        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            CancellationToken cancellationToken = default) => throw new InvalidOperationException("No provider request is allowed.");
        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages,
            ChatOptions? options = null, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("No provider request is allowed.");
        public object? GetService(Type serviceType, object? serviceKey = null)
            => serviceKey is null && serviceType.IsInstanceOfType(this) ? this : null;
        public void Dispose() {
        }
    }
}
