using System.Text.Json;
using System.Text.Json.Nodes;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Tooling;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace CanDoItAll.Tests.Integration.Runtime;

[Trait("Category", "FileSystemPortability")]
public sealed class MafOwnerAcknowledgementCheckpointTests {
    private const string ToolName = ToolContractCatalog.WorkspaceWriteFile;
    private static readonly FunctionCallContent Call = new("owner-acknowledgement", ToolName,
        new Dictionary<string, object?> { ["path"] = "managed/original.txt" });
    private static readonly AgentToolSemanticDigest Request = MafToolProtocolCodec.Digest(new { Stage = "owner-acknowledgement" });

    public enum Corruption { UnknownJournalEffect, MissingOwner, MissingResultKind, LegacyKind, MixedFailure, NestedKind, EmptyOwner }

    [Theory]
    [InlineData(Corruption.UnknownJournalEffect)]
    [InlineData(Corruption.MissingOwner)]
    [InlineData(Corruption.MissingResultKind)]
    [InlineData(Corruption.LegacyKind)]
    [InlineData(Corruption.MixedFailure)]
    [InlineData(Corruption.NestedKind)]
    [InlineData(Corruption.EmptyOwner)]
    public async Task Saved_owner_evidence_requires_exact_committed_journal_pair_before_cached_return(Corruption corruption) {
        var checkpoint = await CaptureAcknowledgementAsync();
        var document = JsonNode.Parse(checkpoint.PayloadJson)!.AsObject();
        var value = document["Value"]!.AsObject();
        var kind = Property(value, "Kind");
        var owner = Property(value, "CommittedEffect");
        var originalKind = Property(value, "CommittedResultKind");
        switch (corruption) {
            case Corruption.MissingOwner:
                value.Remove(owner);
                break;
            case Corruption.MissingResultKind:
                value.Remove(originalKind);
                break;
            case Corruption.LegacyKind:
                value[kind] = value[originalKind]!.DeepClone();
                break;
            case Corruption.MixedFailure:
                var failureName = MafToolProtocolCodec.SerializationOptions.PropertyNamingPolicy?.ConvertName("PreDispatchFailure") ?? "PreDispatchFailure";
                value[failureName] = JsonSerializer.SerializeToNode(
                    new AgentToolPreDispatchFailure("ToolPolicyDenied", "Denied before dispatch."), MafToolProtocolCodec.SerializationOptions);
                break;
            case Corruption.NestedKind:
                value[originalKind] = value[kind]!.DeepClone();
                break;
            case Corruption.EmptyOwner:
                var effect = value[owner]!.AsObject();
                effect[Property(effect, "SourceId")] = "";
                break;
        }
        var corrupted = AgentToolProtocolEnvelope.Create(checkpoint.Format, checkpoint.Version, document.ToJsonString());
        await using var fixture = await AgentToolAdmissionJournalFixture.CreateAsync();
        var journal = fixture.NewJournal();
        await using (var lease = await journal.AcquireRunAsync(fixture.Session, default)) {
            using var bound = lease.Bind();
            var opened = await OpenAsync(fixture, journal, lease);
            using var active = opened.Context.Bind();
            await AdmitAsync(opened.Context);
            var batch = Assert.Single((await journal.ReadAsync(lease, default)).Batches);
            var proposal = Assert.Single(batch.Proposals);
            var claim = await journal.ClaimInvocationAsync(lease, batch.Id, Call.CallId, proposal.Payload, default);
            await journal.CompleteInvocationAsync(claim, corrupted,
                corruption == Corruption.UnknownJournalEffect ? AgentToolEffectState.Unknown : AgentToolEffectState.Committed, default);
        }

        var restarted = fixture.NewJournal(fixture.NewStore());
        await using var current = await restarted.AcquireRunAsync(fixture.Session, default);
        using var rebound = current.Bind();
        var restored = await OpenAsync(fixture, restarted, current);
        using var restoredActive = restored.Context.Bind();
        Assert.NotNull(await restored.Context.ReplayResponseAsync(Request, default));
        using var capture = AgentToolInvocationEffectScope.Begin();
        var dispatches = 0;
        var denied = await Assert.ThrowsAsync<AgentToolAdmissionException>(() => restored.Context.InvokeAsync(Call, _ => {
            dispatches++;
            return ValueTask.FromResult<object?>("must not run");
        }, capture, default).AsTask());
        Assert.Equal("tool-admission.runtime-denied", denied.Code);
        Assert.Equal(0, dispatches);
        Assert.Null(capture.CommittedEffect);
        var retained = Assert.Single(Assert.Single((await restarted.ReadAsync(current, default)).Batches).Proposals);
        Assert.Equal(AgentToolProposalState.Completed, retained.State);
        Assert.Equal(corrupted, retained.Result);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Lost_acknowledgement_or_failed_checkpoint_never_admits_a_second_dispatch(bool acknowledgementObserved) {
        await using var fixture = await AgentToolAdmissionJournalFixture.CreateAsync();
        var dispatches = 0;
        var first = fixture.NewJournal();
        await using (var lease = await first.AcquireRunAsync(fixture.Session, default)) {
            using var bound = lease.Bind();
            var opened = await OpenAsync(fixture, first, lease);
            using var active = opened.Context.Bind();
            await AdmitAsync(opened.Context);
            using var capture = AgentToolInvocationEffectScope.Begin();
            var failure = await Assert.ThrowsAnyAsync<Exception>(() => opened.Context.InvokeAsync(Call, _ => {
                dispatches++;
                if (!acknowledgementObserved) {
                    throw new IOException("The owner acknowledgement was lost after the effect.");
                }
                AgentToolInvocationEffectScope.RecordCommitted("fixture-owner", "original-target");
                return ValueTask.FromResult<object?>((Action)(() => { }));
            }, capture, default).AsTask());
            if (!acknowledgementObserved) {
                Assert.IsType<IOException>(failure);
            } else {
                Assert.IsType<NotSupportedException>(failure);
            }
        }
        var saved = (await fixture.NewStore().GetExecutionRunAsync(fixture.Session.ExecutionRunId))!.ToolAdmission!;
        var proposal = Assert.Single(Assert.Single(saved.Batches).Proposals);
        Assert.Equal(AgentToolProposalRecovery.ReconcileBeforeRetry, proposal.Payload.Recovery);
        Assert.Equal(AgentToolProposalState.ReconciliationRequired, proposal.State);
        Assert.Equal(AgentToolEffectState.Unknown, proposal.EffectState);
        Assert.Null(proposal.Result);
        var restarted = fixture.NewJournal(fixture.NewStore());
        await using var current = await restarted.AcquireRunAsync(fixture.Session, default);
        using var rebound = current.Bind();
        var denied = await Assert.ThrowsAsync<AgentToolAdmissionException>(() => OpenAsync(fixture, restarted, current));
        Assert.Equal("tool-admission.reconciliation-required", denied.Code);
        Assert.Equal(1, dispatches);
        Assert.Equal(proposal, Assert.Single(Assert.Single((await restarted.ReadAsync(current, default)).Batches).Proposals));
    }

    private static string Property(JsonObject value, string name)
        => value.Single(item => string.Equals(item.Key, name, StringComparison.OrdinalIgnoreCase)).Key;

    private static async Task<AgentToolProtocolEnvelope> CaptureAcknowledgementAsync() {
        await using var fixture = await AgentToolAdmissionJournalFixture.CreateAsync();
        var journal = fixture.NewJournal();
        await using var lease = await journal.AcquireRunAsync(fixture.Session, default);
        using var bound = lease.Bind();
        var opened = await OpenAsync(fixture, journal, lease);
        using var active = opened.Context.Bind();
        await AdmitAsync(opened.Context);
        using var capture = AgentToolInvocationEffectScope.Begin();
        var result = await opened.Context.InvokeAsync(Call, _ => {
            AgentToolInvocationEffectScope.RecordCommitted("fixture-owner", "original-target");
            return ValueTask.FromResult<object?>("original result");
        }, capture, default);
        Assert.Equal("original result", result);
        return Assert.Single(Assert.Single((await journal.ReadAsync(lease, default)).Batches).Proposals).Result!;
    }

    private static Task AdmitAsync(MafToolRunContext context) => context.AdmitResponseAsync(Request,
        MafToolProtocolCodec.Encode(new ChatResponse(new ChatMessage(ChatRole.Assistant, [Call]))), [Call], default);

    private static async Task<(MafToolRunContext Context, AgentSession Session, List<ChatMessage> Input)> OpenAsync(
        AgentToolAdmissionJournalFixture fixture, AgentToolAdmissionJournal journal, AgentToolRunLease lease) {
        var function = AIFunctionFactory.Create((string path) => path, ToolName);
        var capabilities = new RuntimeCapabilityState();
        capabilities.Tools.Add(function);
        capabilities.RuntimeToolMetadata.Add(new("owner-acknowledgement-fixture", ToolName, AgentRuntimeToolOperationKind.Mutation, false) {
            AuthorizeResultDisclosureAsync = async (_, token) => {
                await journal.RequireSessionAsync(fixture.Session, token);
                return null;
            }
        });
        var options = MafChatClientAgentOptionsFactory.Create(new ChatOptions { ModelId = fixture.Provider.DefaultModel, Tools = [function] });
        options.ChatHistoryProvider = new InMemoryChatHistoryProvider(new InMemoryChatHistoryProviderOptions {
            JsonSerializerOptions = MafToolProtocolCodec.SerializationOptions
        });
        options.RequirePerServiceCallChatHistoryPersistence = true;
        var runtime = new ChatClientAgent(new NoRequestClient(), options);
        var execution = new AgentRuntimeExecutionOptions(null, AgentFinalizerMode.Disabled, true, 0) {
            AdmittedToolSession = fixture.Session, RequireDurableToolProtocol = true,
            Governance = AgentTurnContextMetadata.TryReadExecutionGovernanceSnapshot(fixture.Detail.Run.MetadataJson),
            AuthorityPolicyFingerprint = "fixture-authority", ToolsetFingerprint = "fixture-toolset",
            ModelContextDigest = "fixture-context", CapabilityPolicyFingerprint = "fixture-capabilities",
            HistoryMode = AgentChatHistoryMode.FrameworkManaged,
            ContextIntent = AgentRuntimeContextIntent.Empty with { Purpose = AgentRuntimeContextPurpose.InteractiveChat }
        };
        return await MafToolRunContext.OpenAsync(journal, lease, runtime, await runtime.CreateSessionAsync(), fixture.Agent,
            fixture.Provider, fixture.Provider.DefaultModel, fixture.Detail.ChatSession!, execution, capabilities,
            [new(ChatRole.User, "Invoke the original tool.")], new MafRuntimeSessionPersistenceDriver(), false,
            (_, _, _) => Task.CompletedTask, default);
    }

    private sealed class NoRequestClient : IChatClient {
        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            CancellationToken cancellationToken = default) => throw new InvalidOperationException("No provider request is allowed.");
        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages,
            ChatOptions? options = null, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("No provider request is allowed.");
        public object? GetService(Type serviceType, object? serviceKey = null)
            => serviceKey is null && serviceType.IsInstanceOfType(this) ? this : null;
        public void Dispose() { }
    }
}
