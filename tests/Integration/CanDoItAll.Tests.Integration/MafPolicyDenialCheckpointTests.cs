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

    [Fact]
    public async Task Denial_checkpoint_replay_does_not_ask_the_owner_to_disclose_a_pre_dispatch_denial() {
        // The workspace result disclosure refuses a saved result without disclosure evidence. A policy denial recorded
        // before dispatch never produced owner data or evidence, so an approval replay must restore the denial without
        // consulting the owner; before the repair the replay failed closed with 'workspace.result-authority-unavailable'.
        const string denialText = "PolicyDenied: Current execution policy denied this saved proposal.";
        await using var fixture = await AgentToolAdmissionJournalFixture.CreateAsync();
        var toolName = ToolContractCatalog.WorkspaceSearch;
        var arguments = new Dictionary<string, object?> { ["query"] = "d1bbb641", ["maxResults"] = 20 };
        var json = JsonSerializer.Serialize(arguments, MafToolProtocolCodec.SerializationOptions);
        var payload = new AgentToolPreparedPayload(toolName, 1, AgentToolProtocolEnvelope.ComputeDigest(json), json,
            AgentToolProposalEffect.Read, AgentToolProposalRecovery.RevalidateAndRead);
        var call = new FunctionCallContent("denied-search", toolName, arguments);
        var request = MafToolProtocolCodec.Digest(new { Stage = "pre-dispatch-denial-replay" });
        var dispatches = 0;
        var disclosureChecks = 0;
        for (var attempt = 0; attempt < 2; attempt++) {
            var journal = fixture.NewJournal(fixture.NewStore());
            await using var lease = await journal.AcquireRunAsync(fixture.Session, default);
            using var bound = lease.Bind();
            using var client = new NoRequestClient();
            var function = AIFunctionFactory.Create((string query, int maxResults) => $"{query}:{maxResults}", toolName);
            var capabilities = new RuntimeCapabilityState();
            capabilities.Tools.Add(function);
            capabilities.RuntimeToolMetadata.Add(new("fixture-policy", toolName, AgentRuntimeToolOperationKind.Read, false) {
                PrepareAdmission = _ => payload,
                AuthorizeAdmissionAsync = async (_, token) => {
                    await journal.RequireSessionAsync(fixture.Session, token);
                    return new EmptyScope();
                },
                AuthorizeResultDisclosureAsync = (disclosure, _) => {
                    disclosureChecks++;
                    if (disclosure.Evidence is null) {
                        throw new AgentToolAdmissionException("workspace.result-authority-unavailable",
                            "The saved workspace result lacks supported original source and path evidence.");
                    }
                    return ValueTask.FromResult<IAsyncDisposable?>(new EmptyScope());
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
            Assert.Equal("ToolPolicyDenied", capture.PreDispatchFailure?.FailureCode);
            var proposal = Assert.Single(Assert.Single((await journal.ReadAsync(lease, default)).Batches).Proposals);
            Assert.Equal(AgentToolProposalState.Completed, proposal.State);
            Assert.Equal(AgentToolEffectState.NotCommitted, proposal.EffectState);
            Assert.Null(proposal.DisclosureEvidence);
        }
        Assert.Equal(1, dispatches);
        Assert.Equal(0, disclosureChecks);
    }

    [Fact]
    public async Task Tampered_denial_checkpoint_is_refused_without_consulting_the_owner_or_dispatching() {
        // A saved pre-dispatch denial whose journal outcome claims a committed effect is inconsistent evidence: the
        // replay must refuse it before any owner disclosure check or dispatch instead of restoring either outcome.
        const string denialText = "PolicyDenied: Current execution policy denied this saved proposal.";
        await using var fixture = await AgentToolAdmissionJournalFixture.CreateAsync();
        var toolName = ToolContractCatalog.WorkspaceSearch;
        var arguments = new Dictionary<string, object?> { ["query"] = "d1bbb641", ["maxResults"] = 20 };
        var json = JsonSerializer.Serialize(arguments, MafToolProtocolCodec.SerializationOptions);
        var payload = new AgentToolPreparedPayload(toolName, 1, AgentToolProtocolEnvelope.ComputeDigest(json), json,
            AgentToolProposalEffect.Read, AgentToolProposalRecovery.RevalidateAndRead);
        var call = new FunctionCallContent("denied-search", toolName, arguments);
        var request = MafToolProtocolCodec.Digest(new { Stage = "tampered-denial-replay" });
        var dispatches = 0;
        var disclosureChecks = 0;
        for (var attempt = 0; attempt < 2; attempt++) {
            if (attempt == 1) {
                await TamperDenialEffectStateAsync(fixture);
            }
            var journal = fixture.NewJournal(fixture.NewStore());
            await using var lease = await journal.AcquireRunAsync(fixture.Session, default);
            using var bound = lease.Bind();
            using var client = new NoRequestClient();
            var function = AIFunctionFactory.Create((string query, int maxResults) => $"{query}:{maxResults}", toolName);
            var capabilities = new RuntimeCapabilityState();
            capabilities.Tools.Add(function);
            capabilities.RuntimeToolMetadata.Add(new("fixture-policy", toolName, AgentRuntimeToolOperationKind.Read, false) {
                PrepareAdmission = _ => payload,
                AuthorizeAdmissionAsync = async (_, token) => {
                    await journal.RequireSessionAsync(fixture.Session, token);
                    return new EmptyScope();
                },
                AuthorizeResultDisclosureAsync = (_, _) => {
                    disclosureChecks++;
                    return ValueTask.FromResult<IAsyncDisposable?>(new EmptyScope());
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
            using var capture = AgentToolInvocationEffectScope.Begin();
            if (attempt == 0) {
                await opened.Context.AdmitResponseAsync(request,
                    MafToolProtocolCodec.Encode(new ChatResponse(new ChatMessage(ChatRole.Assistant, [call]))), [call], default);
                var result = await opened.Context.InvokeAsync(call, _ => {
                    dispatches++;
                    AgentToolInvocationEffectScope.RecordPreDispatchFailure(new("ToolPolicyDenied", denialText));
                    return ValueTask.FromResult<object?>(denialText);
                }, capture, default);
                Assert.Equal(denialText, Assert.IsType<string>(result));
                var saved = Assert.Single(Assert.Single((await journal.ReadAsync(lease, default)).Batches).Proposals);
                Assert.Equal(AgentToolEffectState.NotCommitted, saved.EffectState);
                Assert.NotNull(saved.Result);
                continue;
            }

            Assert.NotNull(await opened.Context.ReplayResponseAsync(request, default));
            var denied = await Assert.ThrowsAsync<AgentToolAdmissionException>(async () => await opened.Context.InvokeAsync(call, _ => {
                dispatches++;
                return ValueTask.FromResult<object?>(denialText);
            }, capture, default));
            Assert.Equal("tool-admission.runtime-denied", denied.Code);
            Assert.Null(capture.PreDispatchFailure);
            Assert.Null(capture.CommittedEffect);
        }
        Assert.Equal(1, dispatches);
        Assert.Equal(0, disclosureChecks);
    }

    private static async Task TamperDenialEffectStateAsync(AgentToolAdmissionJournalFixture fixture) {
        // The store itself refuses a forged journal through its API ("a stale run snapshot cannot change the current tool
        // admission journal"; "a saved tool intent, approved payload or terminal result is immutable"), so the forgery
        // has to bypass it and edit the persisted file: the saved denial proposal claims a committed effect.
        var store = fixture.NewStore();
        await ((ISandboxWorkspaceExecutionRunMutationStore)store).UpdateExecutionRunDetailAsync(fixture.Session.ExecutionRunId, detail => {
            var proposal = Assert.Single(Assert.Single(detail.Run.ToolAdmission!.Batches).Proposals);
            Assert.Equal(AgentToolEffectState.NotCommitted, proposal.EffectState);
            return detail;
        }, default);
        var runDirectoryName = fixture.Session.ExecutionRunId.ToString("N");
        var runFile = Assert.Single(Directory.EnumerateFiles(fixture.WorkspaceRoot, "run.json", SearchOption.AllDirectories)
            .Where(path => string.Equals(Path.GetFileName(Path.GetDirectoryName(path)), runDirectoryName, StringComparison.OrdinalIgnoreCase)));
        var document = JsonNode.Parse(await File.ReadAllTextAsync(runFile))!.AsObject();
        var proposalNode = document["toolAdmission"]!["batches"]![0]!["proposals"]![0]!.AsObject();
        Assert.Equal((int)AgentToolEffectState.NotCommitted, proposalNode["effectState"]!.GetValue<int>());
        proposalNode["effectState"] = (int)AgentToolEffectState.Committed;
        await File.WriteAllTextAsync(runFile, document.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
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
