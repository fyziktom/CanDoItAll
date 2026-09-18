using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.AgentFramework.Tooling;
using CanDoItAll.Infrastructure;
using CanDoItAll.Infrastructure.FileSystem;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.SharedKernel.Streaming;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OllamaSharp.Models;

namespace CanDoItAll.Tests.Integration.Runtime;

[Trait("Category", "FileSystemPortability")]
public sealed class MafIntermediateCheckpointActivityIntegrationTests {
    private const string ToolName = "activity_checkpoint_read";
    private const string ProviderKey = "activity-checkpoint-fixture";
    private const string PrivateFailureMessage = "private-runtime-diagnostic-sentinel";

    [Fact]
    public async Task First_recoverable_chat_streams_after_its_restart_checkpoint_and_persists_only_after_streaming() {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        await using var fixture = await CreateJournalAsync();
        var client = new ScriptClient();
        using var execution = new Execution(fixture, scope.ServiceProvider, client);
        var operation = AgentExecutionOperationId.New();

        var result = await execution.SendAsync(operation);

        Assert.Equal(ExecutionState.Completed, result.State);
        Assert.Equal("completed", result.AssistantMessage.Content);
        Assert.True(client.StreamingRequests > 0);
        var saved = (await fixture.NewStore().GetExecutionRunDetailAsync(result.ExecutionRunId))!;
        var journal = saved.Run.ToolAdmission!;
        Assert.Equal(AgentToolAdmissionSupport.Recoverable, journal.Support);
        Assert.NotNull(Assert.Single(journal.Segments).RestartCheckpoint);
        Assert.Empty(journal.Batches.SelectMany(batch => batch.Proposals));
        Assert.Empty(saved.ToolReceipts);
        Assert.Equal(0, execution.Tools.Calls);
        AssertStreamingOrder(await execution.ReadActivitiesAsync(operation), AgentExecutionActivityPhase.Completed);
        Assert.Contains(saved.ExecutionLog, entry => entry.State == ExecutionState.Preparing && entry.Phase == "Session" &&
            entry.Message == "Serializing the Microsoft Agent Framework session.");
    }

    [Fact]
    public async Task Approval_continuation_after_file_restart_keeps_the_original_approval_and_can_stream_its_new_segment() {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        await using var fixture = await CreateJournalAsync();
        AgentChatRunResult pending;
        using (var initial = new Execution(fixture, scope.ServiceProvider, new ScriptClient(requestTool: true))) {
            var operation = AgentExecutionOperationId.New();
            pending = await initial.SendAsync(operation);
            Assert.Equal(ExecutionState.WaitingOnTool, pending.State);
            Assert.Equal(0, initial.Tools.Calls);
            AssertStreamingOrder(await initial.ReadActivitiesAsync(operation), AgentExecutionActivityPhase.AwaitingApproval);
        }
        var original = (await fixture.NewStore().GetExecutionRunDetailAsync(pending.ExecutionRunId))!;
        var approval = Assert.Single(original.Run.PendingApprovals);
        var originalSegment = Assert.Single(original.Run.ToolAdmission!.Segments);
        Assert.NotNull(originalSegment.ApprovalCheckpoint);
        var originalProposal = Assert.Single(original.Run.ToolAdmission.Batches.SelectMany(batch => batch.Proposals));
        Assert.Equal(AgentToolProposalState.Prepared, originalProposal.State);

        var client = new ScriptClient(requestTool: true);
        using var restarted = new Execution(fixture, scope.ServiceProvider, client);
        var continuationOperation = AgentExecutionOperationId.New();
        var result = await restarted.Service.ContinueExecutionRunAsync(pending.ExecutionRunId, continuationOperation,
            [new PendingToolApprovalDecision(approval.ApprovalId, true) { ToolAdmission = approval.ToolAdmission }]);

        Assert.Equal(pending.ExecutionRunId, result.ExecutionRunId);
        Assert.Equal(ExecutionState.Completed, result.State);
        Assert.Equal(1, restarted.Tools.Calls);
        Assert.True(client.StreamingRequests > 0);
        AssertStreamingOrder(await restarted.ReadActivitiesAsync(continuationOperation), AgentExecutionActivityPhase.Completed);
        var saved = (await fixture.NewStore().GetExecutionRunDetailAsync(result.ExecutionRunId))!;
        Assert.Equal(JsonSerializer.Serialize(originalSegment), JsonSerializer.Serialize(saved.Run.ToolAdmission!.Segments[0]));
        var continuation = Assert.Single(saved.Run.ToolAdmission.Segments, segment => segment.Id != originalSegment.Id);
        Assert.Equal(originalSegment.Id, continuation.ContinuesSegmentId);
        var completed = Assert.Single(saved.Run.ToolAdmission.Batches.SelectMany(batch => batch.Proposals));
        Assert.Equal(originalProposal.IntentId, completed.IntentId);
        Assert.Equal(originalProposal.Payload, completed.Payload);
        Assert.Equal(AgentToolProposalState.Completed, completed.State);
        Assert.Equal(ExecutionApprovalStatus.Approved, completed.ApprovalStatus);
        Assert.Equal(completed.Payload.Digest, completed.ApprovedDigest);
        Assert.Empty(saved.Run.PendingApprovals);
        Assert.Contains(saved.ToolReceipts, receipt => receipt.ToolName == ToolName && receipt.InvocationOutcome == AgentToolInvocationOutcome.Succeeded);
    }

    [Fact]
    public async Task Interrupted_checkpoint_recovers_through_fresh_core_and_maf_instances_without_secret_diagnostics() {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        await using var fixture = await CreateJournalAsync();
        var privatePath = Path.Combine(fixture.WorkspaceRoot, "private-diagnostic-file.txt");
        var logger = new DiagnosticLogger();
        using (var interrupted = new Execution(fixture, scope.ServiceProvider,
            new ScriptClient(failureMessage: $"{PrivateFailureMessage} {privatePath}"), logger)) {
            var operation = AgentExecutionOperationId.New();
            var failure = await Assert.ThrowsAnyAsync<Exception>(() => interrupted.SendAsync(operation));
            Assert.Contains(ExceptionChain(failure), item => item is CheckpointInterruptionException);
            var activities = await interrupted.ReadActivitiesAsync(operation);
            Assert.Equal(AgentExecutionActivityPhase.Failed, activities[^1].Phase);
        }
        var run = Assert.Single(await fixture.NewStore().ListExecutionRunsAsync(), item => item.Id != fixture.Session.ExecutionRunId);
        var original = (await fixture.NewStore().GetExecutionRunDetailAsync(run.Id))!;
        Assert.Equal(ExecutionState.Failed, original.Run.State);
        Assert.Equal(AgentToolAdmissionSupport.Recoverable, original.Run.ToolAdmission!.Support);
        var originalSegment = Assert.Single(original.Run.ToolAdmission.Segments);
        Assert.Empty(original.Run.ToolAdmission.Batches);
        Assert.Empty(original.ToolReceipts);
        Assert.Empty(original.Run.PendingApprovals);
        var diagnostics = logger.Entries.Where(entry => entry.Fields.ContainsKey("MethodStack")).ToArray();
        Assert.NotEmpty(diagnostics);
        Assert.Contains(diagnostics, entry => Equals(entry.Fields["FailureType"], typeof(CheckpointInterruptionException).FullName));
        Assert.Contains(diagnostics, entry => ((string)entry.Fields["MethodStack"]!).Contains(nameof(ScriptClient), StringComparison.Ordinal));
        foreach (var entry in diagnostics) {
            Assert.Null(entry.Exception);
            Assert.Equal(run.Id, entry.Fields["ExecutionRunId"]);
            Assert.Equal(fixture.Agent.Id, entry.Fields["AgentId"]);
            Assert.Equal(run.ChatSessionId, entry.Fields["ChatSessionId"]);
            Assert.DoesNotContain(PrivateFailureMessage, entry.Text, StringComparison.Ordinal);
            Assert.DoesNotContain(privatePath, entry.Text, StringComparison.Ordinal);
            Assert.DoesNotContain(fixture.WorkspaceRoot, entry.Text, StringComparison.Ordinal);
            Assert.DoesNotContain(".cs:line ", entry.Text, StringComparison.Ordinal);
        }

        var client = new ScriptClient();
        using var restarted = new Execution(fixture, scope.ServiceProvider, client);
        var recoveryOperation = AgentExecutionOperationId.New();
        var recovered = await restarted.Service.RecoverExecutionRunAsync(run.Id, recoveryOperation);

        Assert.Equal(run.Id, recovered.ExecutionRunId);
        Assert.Equal(ExecutionState.Completed, recovered.State);
        Assert.True(client.StreamingRequests > 0);
        AssertStreamingOrder(await restarted.ReadActivitiesAsync(recoveryOperation), AgentExecutionActivityPhase.Completed);
        var saved = (await fixture.NewStore().GetExecutionRunDetailAsync(run.Id))!;
        Assert.Equal(JsonSerializer.Serialize(originalSegment), JsonSerializer.Serialize(Assert.Single(saved.Run.ToolAdmission!.Segments)));
        Assert.Equal(original.Run.ToolAdmission.OriginalInput, saved.Run.ToolAdmission.OriginalInput);
        Assert.Equal(original.Run.ToolAdmission.Session, saved.Run.ToolAdmission.Session);
        Assert.Empty(saved.Run.ToolAdmission.Batches.SelectMany(batch => batch.Proposals));
        Assert.Empty(saved.ToolReceipts);
        Assert.Equal(0, restarted.Tools.Calls);
    }

    private static async Task<AgentToolAdmissionJournalFixture> CreateJournalAsync() {
        var fixture = await AgentToolAdmissionJournalFixture.CreateAsync(includeRecoveryInput: true, configureAgent: agent => agent with {
            Id = Guid.NewGuid(), TemplateKey = string.Empty, Workload = AgentWorkloadKind.General, Capabilities = [],
            Permissions = agent.Permissions with { CanUseTools = true }, ChatHistoryMode = AgentChatHistoryMode.FrameworkManaged,
            ConfigurationJson = AgentThinkingEffortConfiguration.WriteAgentOverride(agent.ConfigurationJson, AgentReasoningEffortLevel.Medium)
        });
        try {
            var provider = fixture.Provider with {
                ConfigurationJson = ProviderModelThinkingConfiguration.Write(fixture.Provider.ConfigurationJson, fixture.Provider.DefaultModel,
                    new(fixture.Provider.DefaultModel, AgentThinkingEffortSupportStatus.Supported,
                        AgentThinkingEffortControlMode.EffortLevels, [AgentReasoningEffortLevel.Medium], AgentReasoningEffortLevel.Medium))
            };
            await fixture.Store.UpdateCatalogAsync(catalog => catalog with {
                Providers = catalog.Providers.Select(item => item.Id == provider.Id ? provider : item).ToArray()
            });
            return fixture;
        } catch {
            await fixture.DisposeAsync();
            throw;
        }
    }

    private static void AssertStreamingOrder(IReadOnlyList<AgentExecutionActivity> activities, AgentExecutionActivityPhase terminal) {
        var phases = activities.Select(activity => activity.Phase).ToArray();
        var streaming = Array.IndexOf(phases, AgentExecutionActivityPhase.Streaming);
        Assert.True(streaming >= 0, "The actual Core activity must enter streaming.");
        Assert.DoesNotContain(phases.Take(streaming), phase => phase == AgentExecutionActivityPhase.PersistingResult);
        Assert.Contains(phases.Take(streaming), phase => phase == AgentExecutionActivityPhase.PreparingRuntime);
        Assert.True(Array.LastIndexOf(phases, AgentExecutionActivityPhase.PersistingResult) > streaming);
        Assert.Equal(terminal, phases[^1]);
    }

    private static IEnumerable<Exception> ExceptionChain(Exception failure) {
        for (Exception? current = failure; current is not null; current = current.InnerException) {
            yield return current;
        }
    }

    private sealed class Execution : IDisposable {
        private readonly AgentToolAdmissionJournalFixture fixture;
        private readonly AgentExecutionPreparationCache cache = new(AgentExecutionPreparationCachePolicy.Default);
        private readonly AgentExecutionActivityCoordinator coordinator = new(new PartitionedSequencedStream<AgentExecutionActivityStreamId,
            AgentExecutionActivity>(PartitionedSequencedStreamPolicy.Default, TimeProvider.System), TimeProvider.System);

        internal Execution(AgentToolAdmissionJournalFixture fixture, IServiceProvider services, ScriptClient client, DiagnosticLogger? logger = null) {
            this.fixture = fixture;
            var store = fixture.NewStore();
            var journal = fixture.NewJournal(store);
            Tools = new ReadToolProvider(journal);
            var policies = new AgentToolPolicyCatalog([ToolCapabilityMetadataFactory.Read(ToolName,
                ToolCapabilitySideEffectKind.InternalDataRead) with { RequiresApprovalByDefault = true }]);
            var dependencies = MafAgentRuntimeDependencies.FromServices(services);
            dependencies = dependencies with {
                ProviderAgentFactory = new ScriptAgentFactory(client), ToolAdmissionJournal = journal, ToolPolicies = policies,
                RuntimeToolProviderComposer = new RuntimeToolProviderComposer(new RuntimeToolProviderAccessFilter(policies), policies),
                CapabilityDependencies = dependencies.CapabilityDependencies with {
                    RuntimeToolProviders = [Tools], ContextContributors = [], ToolPolicies = policies
                }
            };
            var runtime = new MafAgentRuntime(fixture.WorkspaceRoot, fixture.StorageScope, dependencies);
            var identity = new AgentExecutionActivityWorkspaceIdentity(fixture.Profile.ProfileId, fixture.StorageScope, fixture.Profile.Generation);
            Service = new(store, new ZipAgentPackageService(fixture.WorkspaceRoot, fixture.StorageScope), runtime.ExecutionPort,
                runtime.ContinuationPort, runtime.DiagnosticsPort, runtime.ModelAdministrationPort,
                new CapabilityProofService(new PhysicalFileSystemPathPolicyFactory()), logger ?? new DiagnosticLogger(),
                coordinator, identity, cache, new FixedAgentExecutionProfileGenerationSource(fixture.Profile.Generation),
                new NoProcessLeases(), new ExternalTargetPathRegistryFactory(), toolAdmissionJournal: journal,
                executionAuthorityResolver: new CurrentAuthority(fixture), toolPolicies: policies);
        }

        internal AgentFrameworkWorkspaceService Service { get; }
        internal ReadToolProvider Tools { get; }

        internal Task<AgentChatRunResult> SendAsync(AgentExecutionOperationId operation)
            => Service.SendMessageAsync(fixture.Agent.Id, null, "Run the checkpoint fixture.", new(operation, WorkspaceToolsEnabled: false) {
                Context = ExecutionInvocationContext.Empty with {
                    SourceKind = "agents", SourceId = "agents", MetadataJson = fixture.Detail.Run.MetadataJson,
                    Policy = new(FinalizerMode: AgentFinalizerMode.Disabled)
                }
            });

        internal async Task<IReadOnlyList<AgentExecutionActivity>> ReadActivitiesAsync(AgentExecutionOperationId operation) {
            await using var reader = coordinator.OpenReader(new(fixture.Profile.ProfileId, fixture.StorageScope,
                fixture.Profile.Generation, operation), StreamSequence.Beginning);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            List<AgentExecutionActivity> activities = [];
            while (true) {
                var result = await reader.ReadAsync(timeout.Token);
                if (result is SequencedStreamEvents<AgentExecutionActivity> events) {
                    activities.AddRange(events.Items.Select(item => item.Event));
                } else {
                    Assert.IsType<SequencedStreamCompleted<AgentExecutionActivity>>(result);
                    return activities;
                }
            }
        }

        public void Dispose() {
            Service.Dispose();
            cache.Dispose();
        }
    }

    private sealed class ReadToolProvider(AgentToolAdmissionJournal journal) : IAgentRuntimeToolProvider {
        public int Order => 0;
        public AgentRuntimeToolProviderDescriptor Descriptor { get; } = new(ProviderKey, "Checkpoint read", "Reads a fixture literal after approval.");
        internal int Calls { get; private set; }

        public ValueTask<IReadOnlyList<AITool>> CreateToolsAsync(AgentRuntimeToolProviderContext context, CancellationToken cancellationToken) {
            Assert.Equal(AgentToolAdmissionSupport.Recoverable, context.ToolAdmissionSupport);
            Assert.NotNull(context.AdmittedToolSession);
            return ValueTask.FromResult<IReadOnlyList<AITool>>([AIFunctionFactory.Create(() => {
                Calls++;
                return "approved fixture read";
            }, ToolName)]);
        }

        public IReadOnlyList<AgentRuntimeToolMetadata> GetToolMetadata(AgentRuntimeToolProviderContext context)
            => [new(ProviderKey, ToolName, AgentRuntimeToolOperationKind.Read, true) {
                PrepareAdmission = input => new(ToolName, 1, MafToolProtocolCodec.Digest(input), MafToolProtocolCodec.Canonicalize(input),
                    AgentToolProposalEffect.Read, AgentToolProposalRecovery.RevalidateAndRead),
                AuthorizeAdmissionAsync = async (_, token) => {
                    await journal.RequireSessionAsync(context.AdmittedToolSession!, token);
                    return new EmptyScope();
                },
                AuthorizeResultDisclosureAsync = async (_, token) => {
                    await journal.RequireSessionAsync(context.AdmittedToolSession!, token);
                    return new EmptyScope();
                }
            }];
    }

    private sealed class EmptyScope : IAsyncDisposable {
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class CurrentAuthority(AgentToolAdmissionJournalFixture fixture) : IAgentExecutionAuthorityResolver {
        public ValueTask<AgentExecutionAuthorityRecord> ResolveAsync(AgentExecutionAuthorityResolutionRequest request,
            CancellationToken cancellationToken = default) {
            var original = AgentTurnContextMetadata.TryReadExecutionGovernanceSnapshot(fixture.Detail.Run.MetadataJson)!;
            var source = AgentTurnContextMetadata.TryReadTurnContextReference(fixture.Detail.Run.MetadataJson)!;
            Assert.Equal(fixture.Agent.Id, request.AgentId);
            Assert.Equal(fixture.Profile.Generation, request.ExpectedDatabaseProfileGeneration);
            Assert.Equal(original.WorkspaceScope, request.ObservedWorkspaceScope);
            Assert.Equal(source.SourceKind, request.SourceKind);
            Assert.Equal(source.SourceId, request.SourceId);
            return ValueTask.FromResult(new AgentExecutionAuthorityRecord(AgentExecutionAuthorityId.Create(), fixture.Agent.Id,
                fixture.Profile.ProfileId, fixture.Profile.Generation, original.WorkspaceScope, true, true,
                original.PolicyVersion, original.PolicyFingerprint, DateTimeOffset.UtcNow));
        }
    }

    private sealed class NoProcessLeases : IWorkspaceExecutionRunProcessLeaseCleaner {
        public Task<WorkspaceExecutionRunProcessCleanupResult> CleanupAsync(Guid executionRunId)
            => Task.FromResult(WorkspaceExecutionRunProcessCleanupResult.Empty(executionRunId));
    }

    private sealed class ScriptAgentFactory(ScriptClient client) : IMafProviderAgentFactory {
        public AIAgent CreateFrameworkAgent(ProviderProfile provider, string model, ChatClientAgentOptions options,
            bool frameworkManagedHistory, bool allowBackgroundResponses) {
            Assert.True(frameworkManagedHistory);
            Assert.NotNull(options.ChatHistoryProvider);
            var thinking = AgentThinkingEffortPolicy.ResolveCapability(provider, model);
            Assert.Equal(AgentThinkingEffortSupportStatus.Supported, thinking.Status);
            Assert.Equal(AgentThinkingEffortCapabilitySource.Configured, thinking.Source);
            Assert.Equal(AgentReasoningEffortLevel.Medium, Assert.Single(thinking.AllowedEfforts));
            Assert.Equal(AgentThinkingEffortPolicy.FormatEffort(AgentReasoningEffortLevel.Medium),
                options.ChatOptions!.AdditionalProperties![OllamaOption.Think.Name]);
            Assert.IsType<ApprovalRequiredAIFunction>(Assert.Single(options.ChatOptions!.Tools!, tool => tool.Name == ToolName));
            return new ChatClientAgent(new MafToolAdmissionChatClient(client), options);
        }
    }

    private sealed class ScriptClient(bool requestTool = false, string? failureMessage = null) : IChatClient {
        internal int StreamingRequests { get; private set; }

        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            CancellationToken cancellationToken = default) {
            if (failureMessage is not null) {
                throw new CheckpointInterruptionException(failureMessage);
            }
            if (requestTool && !messages.SelectMany(message => message.Contents).OfType<FunctionResultContent>().Any()) {
                return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant,
                    [new FunctionCallContent("checkpoint-read-call", ToolName, new Dictionary<string, object?>())])));
            }
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "completed")));
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages,
            ChatOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default) {
            StreamingRequests++;
            var response = await GetResponseAsync(messages, options, cancellationToken);
            foreach (var update in response.ToChatResponseUpdates()) {
                yield return update;
            }
        }

        public object? GetService(Type serviceType, object? serviceKey = null)
            => serviceKey is null && serviceType.IsInstanceOfType(this) ? this : null;
        public void Dispose() { }
    }

    private sealed class CheckpointInterruptionException(string message) : Exception(message) { }

    private sealed class DiagnosticLogger : ILogger<AgentFrameworkWorkspaceService> {
        internal ConcurrentQueue<DiagnosticEntry> Entries { get; } = new();
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) {
            var fields = state is IEnumerable<KeyValuePair<string, object?>> pairs
                ? pairs.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal)
                : new Dictionary<string, object?>(StringComparer.Ordinal);
            Entries.Enqueue(new(formatter(state, exception), fields, exception));
        }
    }

    private sealed record DiagnosticEntry(string Text, IReadOnlyDictionary<string, object?> Fields, Exception? Exception);
}
