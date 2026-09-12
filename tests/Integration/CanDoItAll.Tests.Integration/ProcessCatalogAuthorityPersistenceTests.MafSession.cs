using System.Runtime.CompilerServices;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.AgentFramework.Tooling;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Processes.Application;
using CanDoItAll.Tests.Support;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration.Processes;

public sealed partial class ProcessCatalogAuthorityPersistenceTests {
    private const string MafProcessFirstTool = "process_checkpoint_fixture_first";
    private const string MafProcessSecondTool = "process_checkpoint_fixture_second";
    private const string MafProcessOriginalInput = "Perform only the originally admitted Process work.";
    private const string MafProcessStateMarker = "original-process-sdk-state";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Governed_Process_journal_restores_exact_sdk_state_and_replays_serial_tools_without_persisting_final_conversation(bool streaming) {
        var clock = new NativeClock();
        await using var app = await TestApplication.CreateAsync(NativeHarness(clock));
        await using var scope = app.Services.CreateAsyncScope();
        var test = await MafProcessCase.CreateAsync(scope.ServiceProvider, clock);
        var effects = new MafProcessEffects();
        AgentToolInvocationSegment? originalSegment = null;
        AgentToolProposalRecord[]? originalProposals = null;

        for (var attempt = 0; attempt < 3; attempt++) {
            var journal = test.NewJournal(test.NewStore());
            await using var lease = await journal.AcquireRunAsync(test.Session, default);
            using var bound = lease.Bind();
            using var client = new MafProcessClient(attempt != 1, async () => {
                var durable = await journal.ReadAsync(lease, default);
                Assert.Equal(test.Session, durable.Session.Reference);
                Assert.NotNull(durable.Session.Reference.BackgroundSource);
                Assert.Equal(Guid.Empty, durable.Session.Reference.ChatSessionId);
                Assert.Equal(Guid.Empty, durable.Session.Reference.AuthorityId.Value);
                AssertMafProcessSegment(originalSegment!, Assert.Single(durable.Segments));
            });
            var runtime = test.CreateRuntime(client, effects);
            var sdkSession = await runtime.Agent.CreateSessionAsync();
            sdkSession.StateBag.SetValue(MafProcessStateMarker, attempt == 0 ? "original" : "replacement-must-not-win");
            var opened = await test.OpenAsync(journal, lease, runtime, sdkSession,
                attempt == 0 ? MafProcessOriginalInput : "Replacement input must not win.");
            using var active = opened.Context.Bind();

            Assert.Equal(MafProcessOriginalInput, Assert.Single(opened.Input).Text);
            var restored = (await runtime.Agent.SerializeSessionAsync(opened.Session, MafToolProtocolCodec.SerializationOptions)).GetRawText();
            Assert.Contains(MafProcessStateMarker, restored, StringComparison.Ordinal);
            Assert.Contains("original", restored, StringComparison.Ordinal);
            Assert.DoesNotContain("replacement-must-not-win", restored, StringComparison.Ordinal);
            originalSegment ??= Assert.Single((await journal.ReadAsync(lease, default)).Segments);
            AssertMafProcessSegment(originalSegment, opened.Context.Segment);

            if (attempt == 0) {
                Assert.Empty(effects.Completed);
                Assert.Equal(0, client.Requests);
                continue;
            }

            Assert.Equal("Process work completed.", (await RunMafProcessAsync(runtime, opened.Session, opened.Input, streaming)).Text);
            Assert.Equal([MafProcessFirstTool, MafProcessSecondTool], effects.Completed);
            Assert.Equal(attempt == 1 ? 2 : 0, client.Requests);
            Assert.Equal(1, effects.MaximumConcurrency);
            var saved = await journal.ReadAsync(lease, default);
            var proposals = saved.Batches.SelectMany(batch => batch.Proposals).ToArray();
            Assert.Equal(2, proposals.Length);
            Assert.All(proposals, proposal => Assert.Equal(AgentToolProposalState.Completed, proposal.State));
            Assert.Equal(2, proposals.Select(proposal => proposal.IntentId).Distinct().Count());
            Assert.False(saved.HasUnresolvedProviderDispatch);
            if (originalProposals is null) {
                originalProposals = proposals;
            } else {
                Assert.Equal(originalProposals, proposals);
            }

            Assert.Null(await new MafRuntimeSessionPersistenceDriver().TrySerializePersistableRuntimeSessionAsync(
                runtime.Agent, opened.Session, test.Provider, test.Provider.DefaultModel, test.Options(runtime), [],
                static (_, _, _) => Task.CompletedTask, default));
            var runtimeChat = test.Chat with { Compatibility = ChatSessionRuntimeCompatibilityRecord.Create(null,
                await new MafRuntimeSessionPersistenceDriver().TrySerializePersistableRuntimeSessionAsync(runtime.Agent,
                    opened.Session, test.Provider, test.Provider.DefaultModel, test.Options(runtime), [],
                    static (_, _, _) => Task.CompletedTask, default, MafRuntimeSessionCapturePurpose.ToolAdmissionCheckpoint), [], false) };
            var ordinary = await MafRuntimeSessionBuilder.RestoreOrCreateSessionAsync(runtime.Agent, test.Agent,
                test.Provider, test.Provider.DefaultModel, runtimeChat, test.Options(runtime), default);
            Assert.DoesNotContain(MafProcessStateMarker,
                (await runtime.Agent.SerializeSessionAsync(ordinary, MafToolProtocolCodec.SerializationOptions)).GetRawText(), StringComparison.Ordinal);
        }
        var persisted = (await test.NewStore().GetExecutionRunAsync(test.Execution.Id))!;
        Assert.Null(persisted.ChatSessionId);
        Assert.Null(persisted.SerializedSessionStateJson);
        Assert.Equal(test.Execution.ToolAdmission!.Session, persisted.ToolAdmission!.Session);
        Assert.Equal(test.Execution.ToolAdmission.BackgroundInput, persisted.ToolAdmission.BackgroundInput);
    }

    [Theory]
    [InlineData(Revocation.Tools)]
    [InlineData(Revocation.Read)]
    [InlineData(Revocation.ExactLifetime)]
    public async Task Governed_Process_checkpoint_restart_still_requires_the_original_source_current_read_authority(Revocation revocation) {
        var clock = new NativeClock();
        await using var app = await TestApplication.CreateAsync(NativeHarness(clock));
        await using var scope = app.Services.CreateAsyncScope();
        var test = await MafProcessCase.CreateAsync(scope.ServiceProvider, clock);
        var effects = new MafProcessEffects();
        var journal = test.NewJournal();
        AgentToolInvocationSegment original;
        using (var client = new MafProcessClient(denyRequests: true)) {
            var runtime = test.CreateRuntime(client, effects);
            await using var lease = await journal.AcquireRunAsync(test.Session, default);
            using var bound = lease.Bind();
            original = (await test.OpenAsync(journal, lease, runtime, await runtime.Agent.CreateSessionAsync())).Context.Segment;
        }
        await test.Fixture.RevokeAsync(revocation);
        using var never = new MafProcessClient(denyRequests: true);
        var deniedRuntime = test.CreateRuntime(never, effects);
        var restarted = test.NewJournal(test.NewStore());
        var denied = await Assert.ThrowsAsync<AgentToolAdmissionException>(async () => {
            await using var lease = await restarted.AcquireRunAsync(test.Session, default);
            using var bound = lease.Bind();
            await test.OpenAsync(restarted, lease, deniedRuntime, await deniedRuntime.Agent.CreateSessionAsync());
        });
        Assert.Contains("background", denied.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, never.Requests);
        Assert.Empty(effects.Completed);
        var saved = (await test.NewStore().GetExecutionRunAsync(test.Execution.Id))!.ToolAdmission!;
        AssertMafProcessSegment(original, Assert.Single(saved.Segments));
        Assert.Empty(saved.Batches);
        Assert.Equal(test.Execution.ToolAdmission!.Session, saved.Session);
    }

    [Fact]
    public async Task Governed_Process_checkpoint_rejects_changed_authority_fingerprint_before_any_provider_or_tool() {
        var clock = new NativeClock();
        await using var app = await TestApplication.CreateAsync(NativeHarness(clock));
        await using var scope = app.Services.CreateAsyncScope();
        var test = await MafProcessCase.CreateAsync(scope.ServiceProvider, clock);
        var effects = new MafProcessEffects();
        var journal = test.NewJournal();
        using var never = new MafProcessClient(denyRequests: true);
        var runtime = test.CreateRuntime(never, effects);
        AgentToolInvocationSegment original;
        await using (var lease = await journal.AcquireRunAsync(test.Session, default)) {
            using var bound = lease.Bind();
            original = (await test.OpenAsync(journal, lease, runtime, await runtime.Agent.CreateSessionAsync())).Context.Segment;
        }
        var restarted = test.NewJournal(test.NewStore());
        await using var current = await restarted.AcquireRunAsync(test.Session, default);
        using var currentBound = current.Bind();
        var denied = await Assert.ThrowsAsync<AgentToolAdmissionException>(() => test.OpenAsync(restarted, current, runtime,
            null, options: test.Options(runtime) with { AuthorityPolicyFingerprint = "changed-authority" }));
        Assert.Equal("tool-admission.runtime-denied", denied.Code);
        AssertMafProcessSegment(original, Assert.Single((await restarted.ReadAsync(current, default)).Segments));
        Assert.Equal(0, never.Requests);
        Assert.Empty(effects.Completed);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Governed_Process_checkpoint_preserves_lost_acknowledgement_uncertainty_across_sdk_restart(bool streaming) {
        var clock = new NativeClock();
        await using var app = await TestApplication.CreateAsync(NativeHarness(clock));
        await using var scope = app.Services.CreateAsyncScope();
        var test = await MafProcessCase.CreateAsync(scope.ServiceProvider, clock);
        var effects = new MafProcessEffects { LoseAcknowledgement = true };
        var journal = test.NewJournal();
        using var client = new MafProcessClient(denyRequests: false, singleTool: true);
        var runtime = test.CreateRuntime(client, effects);
        await using (var lease = await journal.AcquireRunAsync(test.Session, default)) {
            using var bound = lease.Bind();
            var opened = await test.OpenAsync(journal, lease, runtime, await runtime.Agent.CreateSessionAsync());
            using var active = opened.Context.Bind();
            var failure = await Assert.ThrowsAnyAsync<Exception>(() => RunMafProcessAsync(runtime, opened.Session, opened.Input, streaming));
            Assert.Contains("tool-admission.reconciliation-required", MafProcessFailureCodes(failure));
        }
        Assert.IsType<IOException>(effects.InjectedFailure);
        Assert.True(effects.BoundaryObservedInjectedFailure);
        Assert.Equal([MafProcessFirstTool], effects.Completed);
        Assert.Equal(1, client.Requests);
        var saved = (await test.NewStore().GetExecutionRunAsync(test.Execution.Id))!.ToolAdmission!;
        var original = Assert.Single(saved.Batches.SelectMany(batch => batch.Proposals));
        Assert.Equal(AgentToolProposalState.ReconciliationRequired, original.State);
        Assert.Equal(AgentToolProposalRecovery.ReconcileBeforeRetry, original.Payload.Recovery);
        Assert.Null(original.Result);
        var restarted = test.NewJournal(test.NewStore());
        await using var current = await restarted.AcquireRunAsync(test.Session, default);
        using var currentBound = current.Bind();
        using var never = new MafProcessClient(denyRequests: true, singleTool: true);
        var replay = test.CreateRuntime(never, effects);
        var denied = await Assert.ThrowsAsync<AgentToolAdmissionException>(() => test.OpenAsync(restarted, current, replay));
        Assert.Equal("tool-admission.reconciliation-required", denied.Code);
        Assert.Equal(0, never.Requests);
        Assert.Equal([MafProcessFirstTool], effects.Completed);
        Assert.Equal(original, Assert.Single((await restarted.ReadAsync(current, default)).Batches.SelectMany(batch => batch.Proposals)));
    }

    private static void AssertMafProcessSegment(AgentToolInvocationSegment expected, AgentToolInvocationSegment actual)
        => Assert.Equal(MafToolProtocolCodec.Encode(expected), MafToolProtocolCodec.Encode(actual));

    private static IEnumerable<string> MafProcessFailureCodes(Exception failure) {
        if (failure is AgentToolAdmissionException admission) {
            yield return admission.Code;
        }
        if (failure.InnerException is { } inner) {
            foreach (var code in MafProcessFailureCodes(inner)) {
                yield return code;
            }
        }
    }

    private static async Task<AgentResponse> RunMafProcessAsync(MafProcessRuntime runtime, AgentSession session,
        List<ChatMessage> input, bool streaming) {
        if (!streaming) {
            return await runtime.Agent.RunAsync(input, session);
        }
        var updates = new List<AgentResponseUpdate>();
        await foreach (var update in runtime.Agent.RunStreamingAsync(input, session)) {
            updates.Add(update);
        }
        return updates.ToAgentResponse();
    }

    private sealed record MafProcessRuntime(AIAgent Agent, RuntimeCapabilityState Capabilities);

    private sealed record MafProcessCase(IServiceProvider Services, NativeClock Clock, Fixture Fixture,
        ExecutionRunRecord Execution, FileSandboxWorkspaceStore Store, AgentDefinition Agent, ProviderProfile Provider) {
        public AgentToolSessionReference Session => Execution.ToolAdmission!.Session.Reference;
        public ChatSessionRecord Chat => ChatSessionRuntimeCompatibilityAdapter.CreateRuntimeSession(Execution, Agent.Id, null);

        public FileSandboxWorkspaceStore NewStore() {
            var profile = Services.GetRequiredService<ICanonicalRuntimeDatabase>().Profile.Profile;
            return new(profile.Storage.WorkspaceRoot, WorkspaceScopeDescriptor.Organization(profile.Id.ToString("N")));
        }

        public AgentToolAdmissionJournal NewJournal(FileSandboxWorkspaceStore? store = null)
            => new(store ?? Store, NativeProfile(Services), Clock, [NativeBackgroundPolicy(Services, Fixture, Clock)]);

        public AgentRuntimeExecutionOptions Options(MafProcessRuntime runtime) => new(null, AgentFinalizerMode.Disabled, true, 0) {
            AdmittedToolSession = Session, RequireDurableToolProtocol = true, ToolAdmissionSupport = AgentToolAdmissionSupport.Recoverable,
            AuthorityPolicyFingerprint = "original-process-authority", ModelContextDigest = "original-process-context",
            CapabilityPolicyFingerprint = "original-process-capabilities", HistoryMode = AgentChatHistoryMode.FrameworkManaged,
            ToolsetFingerprint = MafToolsetFingerprint.ComputeContractFingerprint(runtime.Capabilities.Tools),
            ContextIntent = AgentRuntimeContextIntent.Empty with {
                IsGovernedProcessStep = true, Purpose = AgentRuntimeContextPurpose.GovernedProcessAutomation,
                SourceKind = Execution.SourceKind, SourceId = Execution.SourceId,
                ProcessRunId = Execution.ProcessRunId!, ProcessStepId = Execution.ProcessStepId!,
                AllowedOperations = [ProcessOperationContractNames.ReadProjectStructure, ProcessOperationContractNames.ExecuteExternalAction]
            }
        };

        public async Task<(MafToolRunContext Context, AgentSession Session, List<ChatMessage> Input)> OpenAsync(
            AgentToolAdmissionJournal journal, AgentToolRunLease lease, MafProcessRuntime runtime, AgentSession? session = null,
            string input = MafProcessOriginalInput, AgentRuntimeExecutionOptions? options = null)
            => await MafToolRunContext.OpenAsync(journal, lease, runtime.Agent, session ?? await runtime.Agent.CreateSessionAsync(),
                Agent, Provider, Provider.DefaultModel, Chat, options ?? Options(runtime), runtime.Capabilities, [new(ChatRole.User, input)],
                new MafRuntimeSessionPersistenceDriver(), false, static (_, _, _) => Task.CompletedTask, default);

        public MafProcessRuntime CreateRuntime(IChatClient client, MafProcessEffects effects) {
            var capabilities = new RuntimeCapabilityState();
            foreach (var name in new[] { MafProcessFirstTool, MafProcessSecondTool }) {
                capabilities.Tools.Add(AIFunctionFactory.Create(() => effects.InvokeAsync(name), name));
                capabilities.RuntimeToolMetadata.Add(new("process-checkpoint-fixture", name, AgentRuntimeToolOperationKind.Mutation, false) {
                    AuthorizeResultDisclosureAsync = async (_, token) => await Fixture.Authority.AcquireResultReadAsync(Fixture.SavedAuthority, token)
                });
            }
            var options = MafChatClientAgentOptionsFactory.Create(new ChatOptions {
                ModelId = Provider.DefaultModel, Tools = capabilities.Tools, AllowMultipleToolCalls = false
            });
            options.ChatHistoryProvider = new InMemoryChatHistoryProvider(new InMemoryChatHistoryProviderOptions {
                JsonSerializerOptions = MafToolProtocolCodec.SerializationOptions
            });
            options.RequirePerServiceCallChatHistoryPersistence = true;
            var agent = new ChatClientAgent(new MafToolAdmissionChatClient(client), options).AsBuilder()
                .Use(async (_, invocation, next, token) => {
                    using var effect = AgentToolInvocationEffectScope.Begin();
                    return await MafToolRunContext.Current!.InvokeAsync(invocation.CallContent, async cancellation => {
                        try {
                            return await next(invocation, cancellation);
                        } catch (IOException failure) when (ReferenceEquals(failure, effects.InjectedFailure)) {
                            effects.BoundaryObservedInjectedFailure = true;
                            throw;
                        }
                    }, effect, token);
                }).Build();
            return new(agent, capabilities);
        }

        public static async Task<MafProcessCase> CreateAsync(IServiceProvider services, NativeClock clock) {
            var fixture = await Fixture.CreateAsync(services);
            var execution = await CreateClaimedExecutionAsync(services, fixture, clock, persistExecution: false);
            var store = Assert.IsType<FileSandboxWorkspaceStore>(services.GetRequiredService<ISandboxWorkspaceExecutionRunStore>());
            var catalog = await store.UpdateCatalogAsync(current => current with {
                Agents = current.Agents.Select(agent => agent.Id == execution.AgentId
                    ? agent with { ChatHistoryMode = AgentChatHistoryMode.FrameworkManaged } : agent).ToArray()
            });
            var journal = new AgentToolAdmissionJournal(store, NativeProfile(services), clock, [NativeBackgroundPolicy(services, fixture, clock)]);
            execution = execution with { ToolAdmission = await journal.CreateForNewBackgroundRunAsync(execution, MafProcessOriginalInput) };
            await store.SaveExecutionRunDetailAsync(new(execution, null, [], []));
            Assert.NotEqual(fixture.Agent.Id, execution.AgentId);
            return new(services, clock, fixture, execution, store, catalog.Agents.Single(agent => agent.Id == execution.AgentId), catalog.Providers.First());
        }
    }

    private sealed class MafProcessEffects {
        private int active;
        public List<string> Completed { get; } = [];
        public int MaximumConcurrency { get; private set; }
        public bool LoseAcknowledgement { get; init; }
        public IOException? InjectedFailure { get; private set; }
        public bool BoundaryObservedInjectedFailure { get; set; }

        public async Task<string> InvokeAsync(string name) {
            var entered = Interlocked.Increment(ref active);
            MaximumConcurrency = Math.Max(MaximumConcurrency, entered);
            try {
                Assert.Equal(1, entered);
                await Task.Yield();
                Completed.Add(name);
                if (LoseAcknowledgement) {
                    InjectedFailure = new IOException("Injected acknowledgement loss after the Process fixture effect.");
                    throw InjectedFailure;
                }
                return name + " completed";
            } finally {
                Interlocked.Decrement(ref active);
            }
        }
    }

    private sealed class MafProcessClient(bool denyRequests, Func<Task>? beforeRequest = null, bool singleTool = false) : IChatClient {
        public int Requests { get; private set; }
        public async Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            CancellationToken cancellationToken = default) {
            Requests++;
            Assert.False(denyRequests, "The saved journal must answer without another provider dispatch.");
            if (beforeRequest is not null) {
                await beforeRequest();
            }
            if (Requests > 1) {
                return new(new ChatMessage(ChatRole.Assistant, "Process work completed."));
            }
            List<AIContent> calls = [new FunctionCallContent("first-call", MafProcessFirstTool, new Dictionary<string, object?>())];
            if (!singleTool) {
                calls.Add(new FunctionCallContent("second-call", MafProcessSecondTool, new Dictionary<string, object?>()));
            }
            return new(new ChatMessage(ChatRole.Assistant, calls));
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
