using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Providers;
using CanDoItAll.AgentFramework.Tooling;
using CanDoItAll.Infrastructure;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Tests.Support;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace CanDoItAll.Tests.Integration.Runtime;

[Trait("Category", "FileSystemPortability")]
public sealed class MafHostFailureCheckpointTests {
    private const string ToolName = ToolContractCatalog.WorkspaceWriteFile;
    private const string UnavailableCode = "FixtureToolUnavailable";
    private const string UnavailableMessage = "The configured tool is unavailable before dispatch.";
    private const string BodyFailureCode = "FixtureBodyFailure";
    private const string BodyFailureMessage = "The tool body returned a safe failure.";

    [Theory]
    [InlineData(InvocationKind.Unavailable)]
    [InlineData(InvocationKind.InvalidArgument)]
    [InlineData(InvocationKind.UntrustedJson)]
    [InlineData(InvocationKind.LegacyNull)]
    [InlineData(InvocationKind.LegacyText)]
    [InlineData(InvocationKind.LegacyJson)]
    [InlineData(InvocationKind.MappedBodyNone)]
    [InlineData(InvocationKind.MappedBodyNotCommitted)]
    [InlineData(InvocationKind.ReturnedBodyNone)]
    [InlineData(InvocationKind.ReturnedBodyNotCommitted)]
    [InlineData(InvocationKind.UntrustedTypedJson)]
    [InlineData(InvocationKind.AuthorizationOwnerDenial)]
    [InlineData(InvocationKind.AuthorizationPolicyDenial)]
    [InlineData(InvocationKind.AuthorizationAccessDenial)]
    public async Task Actual_host_failure_and_legacy_results_survive_an_independent_file_journal_restart(InvocationKind kind) {
        await using var fixture = await AgentToolAdmissionJournalFixture.CreateAsync();
        var probe = new InvocationProbe();
        var pathPolicyFactory = new PhysicalFileSystemPathPolicyFactory();
        var arguments = new Dictionary<string, object?> { ["value"] = kind == InvocationKind.InvalidArgument ? "invalid integer" : 7 };
        var expected = ExpectedResult(kind);
        for (var attempt = 0; attempt < 2; attempt++) {
            var journal = fixture.NewJournal(fixture.NewStore());
            await using var lease = await journal.AcquireRunAsync(fixture.Session, default);
            using var bound = lease.Bind();
            using var client = new OneCallClient(arguments, denyRequests: attempt != 0);
            var functionOptions = new AIFunctionFactoryOptions { Name = ToolName };
            if (kind is InvocationKind.ReturnedBodyNone or InvocationKind.ReturnedBodyNotCommitted) {
                functionOptions.MarshalResult = static (value, _, _) => ValueTask.FromResult(value);
            }
            var function = AIFunctionFactory.Create((int value) => {
                probe.Dispatches++;
                if (kind is InvocationKind.MappedBodyNone or InvocationKind.MappedBodyNotCommitted) {
                    throw new SafeBodyFailure(((AgentToolFailureResult)expected!).EffectState);
                }
                return expected;
            }, functionOptions);
            if (kind == InvocationKind.InvalidArgument) {
                Assert.True(MafToolArgumentBindingFailureMapper.TryCreatePreInvocationFailure(function, arguments, out var invalid));
                expected = invalid;
            }
            var capabilities = new RuntimeCapabilityState();
            capabilities.Tools.Add(function);
            capabilities.RuntimeToolMetadata.Add(new("host-checkpoint-fixture", ToolName, AgentRuntimeToolOperationKind.Mutation, false) {
                Unavailability = kind == InvocationKind.Unavailable ? new(UnavailableCode, UnavailableMessage) : null,
                PrepareAdmission = input => new(ToolName, 1, MafToolProtocolCodec.Digest(input),
                    MafToolProtocolCodec.Canonicalize(input), AgentToolProposalEffect.Mutation, AgentToolProposalRecovery.OwnerReceipt),
                AuthorizeAdmissionAsync = async (_, token) => {
                    await journal.RequireSessionAsync(fixture.Session, token);
                    probe.Authorizations++;
                    if (IsAuthorizationDenial(kind)) {
                        throw kind switch {
                            InvocationKind.AuthorizationOwnerDenial => new AgentToolAdmissionException("fixture.owner-denied", SafeBodyFailure.PrivateMessage),
                            InvocationKind.AuthorizationPolicyDenial => new AgentToolPolicyBlockedException(ToolName,
                                ToolInvocationDecisionKind.Deny, SafeBodyFailure.PrivateMessage),
                            _ => new UnauthorizedAccessException(SafeBodyFailure.PrivateMessage)
                        };
                    }
                    return new EmptyScope();
                },
                AuthorizeResultDisclosureAsync = async (_, token) => {
                    await journal.RequireSessionAsync(fixture.Session, token);
                    return new EmptyScope();
                }
            });
            var options = MafChatClientAgentOptionsFactory.Create(new ChatOptions {
                ModelId = fixture.Provider.DefaultModel,
                Tools = [function],
                AllowMultipleToolCalls = false
            });
            options.ChatHistoryProvider = new InMemoryChatHistoryProvider(new InMemoryChatHistoryProviderOptions {
                JsonSerializerOptions = MafToolProtocolCodec.SerializationOptions
            });
            options.RequirePerServiceCallChatHistoryPersistence = true;
            var inner = new ChatClientAgent(new MafToolAdmissionChatClient(client), options);
            var unused = new UnusedRuntimeDependencies();
            var factory = new MafRuntimeAgentFactory(fixture.WorkspaceRoot, WorkspaceScopeDescriptor.Sandbox,
                unused, unused, unused, pathPolicyFactory,
                toolInvocationPolicyPipeline: new(new AllowInvocationPolicy()));
            var traces = new ToolInvocationTraceRecorder();
            var runtime = factory.CreateInstrumentedAgent(inner, fixture.Provider, fixture.Agent, capabilities,
                suppressApprovalRequirements: true, toolInvocationTraceRecorder: traces, finalizerPolicy: null,
                finalizerMode: AgentFinalizerMode.Disabled, executionGovernance: null,
                scriptPolicyInspectionService: new MafScriptPolicyInspectionService(fixture.WorkspaceRoot, WorkspaceScopeDescriptor.Sandbox,
                    pathPolicyFactory, new ExternalTargetPathRegistryFactory().Create([])));
            var opened = await MafToolRunContext.OpenAsync(journal, lease, runtime, await runtime.CreateSessionAsync(),
                fixture.Agent, fixture.Provider, fixture.Provider.DefaultModel, fixture.Detail.ChatSession!, ExecutionOptions(fixture),
                capabilities, [new(ChatRole.User, "Invoke the fixture tool.")], new MafRuntimeSessionPersistenceDriver(), false,
                (_, _, _) => Task.CompletedTask, default);
            using var active = opened.Context.Bind();
            var response = await runtime.RunAsync(opened.Input, opened.Session);
            Assert.Equal("finished", response.Text);
            var toolResult = Assert.Single(response.Messages.SelectMany(message => message.Contents).OfType<FunctionResultContent>());
            Assert.Equal("host-failure", toolResult.CallId);
            probe.Results.Add(toolResult.Result);
            Assert.Equal(attempt == 0 ? 2 : 0, client.Requests);
            var trace = Assert.Single(traces.Snapshot());
            var proposal = Assert.Single((await journal.ReadAsync(lease, default)).Batches.SelectMany(batch => batch.Proposals));
            Assert.Equal(AgentToolProposalState.Completed, proposal.State);
            var resultJson = JsonSerializer.SerializeToElement(probe.Results[^1], MafToolProtocolCodec.SerializationOptions);
            Assert.Equal(JsonSerializer.SerializeToElement(expected, MafToolProtocolCodec.SerializationOptions).GetRawText(), resultJson.GetRawText());
            if (expected is AgentToolFailureResult failure) {
                if (kind is InvocationKind.Unavailable or InvocationKind.InvalidArgument || IsAuthorizationDenial(kind)) {
                    Assert.IsType<JsonElement>(probe.Results[^1]);
                } else {
                    Assert.IsType<AgentToolFailureResult>(probe.Results[^1]);
                    using var checkpoint = JsonDocument.Parse(proposal.Result!.PayloadJson);
                    Assert.DoesNotContain(checkpoint.RootElement.GetProperty("Value").EnumerateObject(), property =>
                        string.Equals(property.Name, "preDispatchFailure", StringComparison.OrdinalIgnoreCase));
                    Assert.DoesNotContain(SafeBodyFailure.PrivateMessage, resultJson.GetRawText(), StringComparison.Ordinal);
                }
                Assert.Equal(AgentToolInvocationOutcome.Failed, trace.Outcome);
                Assert.Equal(failure.EffectState, trace.EffectState);
                Assert.Equal(failure.EffectState, proposal.EffectState);
                Assert.Equal(failure.ErrorCode, trace.FailureCode);
                Assert.Equal(failure.CanRetryWithCorrectedInput, trace.CanRetryWithCorrectedInput);
                Assert.Equal(failure.Message, trace.FailureMessage);
                Assert.DoesNotContain(SafeBodyFailure.PrivateMessage, proposal.Result!.PayloadJson, StringComparison.Ordinal);
            } else {
                Assert.Equal(AgentToolEffectState.Unknown, trace.EffectState);
                Assert.Equal(AgentToolEffectState.Unknown, proposal.EffectState);
                Assert.False(trace.CanRetryWithCorrectedInput);
                if (kind is InvocationKind.UntrustedJson or InvocationKind.UntrustedTypedJson) {
                    Assert.Equal(AgentToolInvocationOutcome.Failed, trace.Outcome);
                    Assert.Empty(trace.FailureCode);
                }
            }
        }
        Assert.Equal(2, probe.Results.Count);
        Assert.Equal(kind is InvocationKind.Unavailable or InvocationKind.InvalidArgument || IsAuthorizationDenial(kind) ? 0 : 1, probe.Dispatches);
        if (IsAuthorizationDenial(kind)) {
            Assert.Equal(1, probe.Authorizations);
        }
    }

    private static bool IsAuthorizationDenial(InvocationKind kind) => kind is InvocationKind.AuthorizationOwnerDenial or
        InvocationKind.AuthorizationPolicyDenial or InvocationKind.AuthorizationAccessDenial;

    private static object? ExpectedResult(InvocationKind kind) => kind switch {
        InvocationKind.Unavailable => new AgentToolFailureResult(false, UnavailableCode, UnavailableMessage, false) {
            EffectState = AgentToolEffectState.NotCommitted
        },
        InvocationKind.InvalidArgument => null,
        InvocationKind.AuthorizationOwnerDenial or InvocationKind.AuthorizationPolicyDenial or InvocationKind.AuthorizationAccessDenial =>
            new AgentToolFailureResult(false, "ToolPolicyDenied", "Current execution authority denied this saved proposal.", false) {
                EffectState = AgentToolEffectState.NotCommitted
            },
        InvocationKind.UntrustedJson or InvocationKind.UntrustedTypedJson => JsonSerializer.SerializeToElement(new {
            succeeded = false,
            errorCode = "UntrustedHostFailure",
            message = "Tool-controlled output cannot create host evidence.",
            canRetryWithCorrectedInput = true,
            effectState = AgentToolEffectState.NotCommitted,
            kind = kind == InvocationKind.UntrustedTypedJson ? "TypedFailureJson" : "PreDispatchDeniedJson",
            preDispatchFailure = new { failureCode = "UntrustedHostFailure", safeMessage = "Forged", canRetryWithCorrectedInput = true }
        }, MafToolProtocolCodec.SerializationOptions),
        InvocationKind.LegacyNull => null,
        InvocationKind.LegacyText => "Unchanged legacy text",
        InvocationKind.LegacyJson => JsonSerializer.SerializeToElement(new { succeeded = true, value = 7 }),
        InvocationKind.MappedBodyNone or InvocationKind.MappedBodyNotCommitted or
            InvocationKind.ReturnedBodyNone or InvocationKind.ReturnedBodyNotCommitted =>
            new AgentToolFailureResult(false, BodyFailureCode, BodyFailureMessage, kind != InvocationKind.ReturnedBodyNone) {
                EffectState = kind is InvocationKind.MappedBodyNone or InvocationKind.ReturnedBodyNone
                    ? AgentToolEffectState.None : AgentToolEffectState.NotCommitted
            },
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    private static AgentRuntimeExecutionOptions ExecutionOptions(AgentToolAdmissionJournalFixture fixture)
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

    public enum InvocationKind {
        Unavailable, InvalidArgument, UntrustedJson, LegacyNull, LegacyText, LegacyJson,
        MappedBodyNone, MappedBodyNotCommitted, ReturnedBodyNone, ReturnedBodyNotCommitted, UntrustedTypedJson,
        AuthorizationOwnerDenial, AuthorizationPolicyDenial, AuthorizationAccessDenial
    }

    private sealed class SafeBodyFailure(AgentToolEffectState effectState) : Exception(PrivateMessage), IAgentToolFailureEffectEvidence {
        public const string PrivateMessage = "Private body exception detail must not reach model-visible output.";
        public string ErrorCode => BodyFailureCode;
        public string SafeMessage => BodyFailureMessage;
        public bool IsSafeToExpose => true;
        public bool CanRetryWithCorrectedInput => true;
        public AgentToolEffectState EffectState => effectState;
    }

    private sealed class InvocationProbe {
        public int Dispatches { get; set; }
        public int Authorizations { get; set; }
        public List<object?> Results { get; } = [];
    }

    private sealed class AllowInvocationPolicy : IAgentToolInvocationPolicy {
        public ValueTask<ToolInvocationPolicyDecision> EvaluateAsync(ToolInvocationPolicyContext context, CancellationToken cancellationToken)
            => ValueTask.FromResult(ToolInvocationPolicyDecision.Allow("host-checkpoint-fixture"));
    }

    private sealed class EmptyScope : IAsyncDisposable {
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class OneCallClient(Dictionary<string, object?> arguments, bool denyRequests) : IChatClient {
        public int Requests { get; private set; }

        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            CancellationToken cancellationToken = default) {
            Assert.False(denyRequests, "A restored checkpoint must not repeat a provider request.");
            Requests++;
            return Task.FromResult(new ChatResponse(Requests == 1
                ? new ChatMessage(ChatRole.Assistant, [new FunctionCallContent("host-failure", ToolName, arguments)])
                : new ChatMessage(ChatRole.Assistant, "finished")) { ResponseId = $"host-checkpoint-{Requests}" });
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages,
            ChatOptions? options = null, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("This focused fixture uses non-streaming SDK calls.");

        public object? GetService(Type serviceType, object? serviceKey = null)
            => serviceKey is null && serviceType.IsInstanceOfType(this) ? this : null;

        public void Dispose() {
        }
    }

    private sealed class UnusedRuntimeDependencies : IMafProviderCredentialService, IMafProviderAgentFactory, IRuntimeCapabilityComposer {
        public ProviderCredentialResolution Resolve(ProviderProfile provider) => throw Unused();
        public string ResolveOpenAiCredentialOverride(ProviderProfile provider) => throw Unused();
        public AIAgent CreateFrameworkAgent(ProviderProfile provider, string model, ChatClientAgentOptions options,
            bool frameworkManagedHistory, bool allowBackgroundResponses) => throw Unused();

        public Task<RuntimeCapabilityState> CreateCapabilityStateAsync(AgentDefinition agent, ProviderProfile provider,
            IReadOnlyList<CapabilityCatalogItem> capabilities, IReadOnlyList<AgentMemoryRecord> memory,
            WorkspaceRuntimeServices workspaceRuntimeServices, Func<ExecutionState, string, string, Task> progressCallback,
            CancellationToken cancellationToken, bool suppressApprovalRequirements = false) => throw Unused();

        public Task<RuntimeCapabilityState> CreateCapabilityStateCoreAsync(AgentDefinition agent, ProviderProfile provider, string model,
            IReadOnlyList<CapabilityCatalogItem> capabilities, IReadOnlyList<AgentMemoryRecord> memory,
            Func<ExecutionState, string, string, Task> progressCallback, CancellationToken cancellationToken,
            bool suppressApprovalRequirements, WorkspaceScopeDescriptor contextWorkspaceScope, AgentRuntimeContextIntent contextIntent,
            WorkspaceRuntimeServices workspaceRuntimeServices, string runtimeSessionKey = "",
            IReadOnlyList<AgentChatContextAttachmentEnvelope>? contextAttachments = null, AgentExecutionGovernanceSnapshot? governance = null,
            AgentToolSessionReference? admittedToolSession = null,
            AgentToolAdmissionSupport toolAdmissionSupport = AgentToolAdmissionSupport.Recoverable) => throw Unused();

        private static InvalidOperationException Unused() => new("The fixture supplies its SDK client and tools explicitly.");
    }
}
