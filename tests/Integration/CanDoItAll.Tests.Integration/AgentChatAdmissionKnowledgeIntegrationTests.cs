using System.Diagnostics;
using System.Reflection;
using System.Runtime.ExceptionServices;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.AgentFramework.Runtime.Abstractions;
using CanDoItAll.Infrastructure;
using CanDoItAll.Infrastructure.FileSystem;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.SharedKernel.Streaming;
using CanDoItAll.Tests.Integration.AgentFramework;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Integration.Runtime;

[Trait("Category", "HostPlatform")]
[Trait("Category", "FileSystemPortability")]
public sealed class AgentChatAdmissionKnowledgeIntegrationTests {
    [Fact]
    public Task Concrete_prepublication_refusal_leaves_original_prompt_unsent_and_never_binds_or_dispatches_run() => RunAsync(async scenario => {
        await scenario.HoldAgentAsync();
        using var wait = new PublicationWait();
        var pending = scenario.Start();
        await wait.Entered.Task.WaitAsync(scenario.Token);
        Assert.False(pending.IsCompleted);
        Assert.Equal(1, scenario.Proxy.Callbacks);
        var catalog = await scenario.Fixture.NewStore().LoadCatalogAsync(scenario.Token);
        var independentAgent = catalog.Agents.First(agent => agent.Id != scenario.Fixture.Agent.Id);
        var independentCreatedAt = DateTimeOffset.UtcNow;
        await scenario.Fixture.NewStore().CreateChatSessionAsync(new ChatSessionRecord(
            Guid.NewGuid(), independentAgent.Id, "Independent admission", independentCreatedAt, independentCreatedAt, []), scenario.Token);
        await scenario.ReleaseAsync();
        var failure = await Assert.ThrowsAsync<AgentChatAdmissionConflictException>(() => scenario.ObserveAsync());
        Assert.IsType<SandboxWorkspaceConcurrencyException>(failure.InnerException);
        Assert.Equal(scenario.Fixture.Agent.Id, failure.AgentId);
        Assert.Equal(scenario.Session.Id, failure.ChatSessionId);
        Assert.Equal(1, scenario.Proxy.Starts);
        Assert.Equal(1, scenario.Proxy.Callbacks);
        Assert.Null(scenario.Operation.ExecutionRunId);
        Assert.Equal(0, scenario.Runtime.Calls);
        Assert.Equal(scenario.BeforeSession, await File.ReadAllBytesAsync(scenario.Layout.SessionPath(scenario.Session.Id), scenario.Token));
        Assert.Equal(scenario.BeforeRuns, Directory.GetDirectories(scenario.Layout.ExecutionRunsRoot).Order(StringComparer.Ordinal).ToArray());
        Assert.False(File.Exists(Path.Combine(scenario.Layout.ExecutionStorageRoot, "pending-chat-run-start.json")));
        await scenario.RequireAgentAvailableAsync();
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task Durable_journal_boundary_failure_never_becomes_a_definite_unsent_result(bool aggregate) => RunAsync(async scenario => {
        var direct = new SandboxWorkspaceConcurrencyException(2, 3);
        Exception expected = aggregate ? new AggregateException(direct, new IOException("release failure")) : direct;
        var persistedJournalObserved = false;
        scenario.Proxy.Target = new FileSandboxWorkspaceStore(scenario.Fixture.WorkspaceRoot, scenario.Fixture.StorageScope,
            stage => {
                if (stage == ChatBackedRunCommitStage.JournalPersisted) {
                    persistedJournalObserved = true;
                    throw expected;
                }
            });
        scenario.Start();
        var failure = await Record.ExceptionAsync(() => scenario.ObserveAsync());
        Assert.Same(expected, failure);
        Assert.True(persistedJournalObserved);
        Assert.IsNotType<AgentChatAdmissionConflictException>(failure);
        Assert.Equal(1, scenario.Proxy.Starts);
        Assert.Equal(1, scenario.Proxy.Callbacks);
        Assert.Null(scenario.Operation.ExecutionRunId);
        Assert.Equal(0, scenario.Runtime.Calls);
        Assert.True(File.Exists(Path.Combine(scenario.Layout.ExecutionStorageRoot, "pending-chat-run-start.json")));
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task Unproven_custom_store_conflict_is_not_reclassified_by_the_actual_core_caller(bool publicationConflict) => RunAsync(async scenario => {
        Exception expected = publicationConflict
            ? new SandboxWorkspacePublicationConflictException(SandboxWorkspacePublicationConflictKind.OriginalBytesChanged)
            : new SandboxWorkspaceConcurrencyException(2, 3);
        scenario.Proxy.Failure = expected;
        scenario.Start();
        Assert.Same(expected, await Record.ExceptionAsync(() => scenario.ObserveAsync()));
        Assert.Equal(1, scenario.Proxy.Starts);
        Assert.Equal(0, scenario.Proxy.Callbacks);
        Assert.Null(scenario.Operation.ExecutionRunId);
        Assert.Equal(0, scenario.Runtime.Calls);
        Assert.Equal(scenario.BeforeSession, await File.ReadAllBytesAsync(scenario.Layout.SessionPath(scenario.Session.Id), scenario.Token));
    });

    [Fact]
    public async Task Existing_owner_default_preserves_its_unknown_failure_without_claiming_prepublication_knowledge() {
        var failure = new SandboxWorkspaceConcurrencyException(2, 3);
        var legacy = new LegacyOwner(failure);
        ISandboxWorkspaceChatRunStartStore port = legacy;
        var request = new ChatBackedRunStartRequest(Guid.NewGuid(), Guid.NewGuid(), new(1), null);
        Assert.Same(failure, await Record.ExceptionAsync(() => port.BeginChatBackedRunWithAdmissionKnowledgeAsync(
            request, _ => throw new InvalidOperationException("The legacy refusal does not prepare a new run."))));
        Assert.Equal(1, legacy.Calls);
    }

    private static async Task RunAsync(Func<Scenario, Task> test) {
        AgentToolAdmissionJournalFixture? fixture = null;
        Scenario? scenario = null;
        var failures = new List<Exception>();
        try {
            fixture = await AgentToolAdmissionJournalFixture.CreateAsync(includeRecoveryInput: true);
            scenario = new(fixture);
            await scenario.InitializeAsync();
            await test(scenario);
        } catch (Exception failure) {
            failures.Add(failure);
        } finally {
            if (scenario is not null) {
                await scenario.CleanupAsync(failures);
            }
            if (fixture is not null) {
                try {
                    await fixture.DisposeAsync();
                } catch (Exception failure) {
                    failures.Add(failure);
                }
            }
        }
        if (failures.Count == 1) {
            ExceptionDispatchInfo.Capture(failures[0]).Throw();
        }
        if (failures.Count > 1) {
            throw new AggregateException("Chat admission proof and terminal cleanup failed.", failures);
        }
    }

    private sealed class Scenario(AgentToolAdmissionJournalFixture fixture) {
        private readonly CancellationTokenSource cancellation = new(TimeSpan.FromSeconds(30));
        private readonly AgentExecutionPreparationCache cache = new(AgentExecutionPreparationCachePolicy.Default);
        private IAsyncDisposable? blocker;
        private Task<AgentChatRunResult>? pending;
        private bool observed;
        private AgentFrameworkWorkspaceService? service;
        public AgentToolAdmissionJournalFixture Fixture { get; } = fixture;
        public FileSandboxWorkspaceStorageLayout Layout { get; } = new(fixture.WorkspaceRoot, fixture.StorageScope);
        public NoDispatchRuntime Runtime { get; } = new();
        public StartupOwnerProxy Proxy { get; private set; } = default!;
        public IAgentExecutionActivityOperationLease Operation { get; private set; } = default!;
        public ChatSessionRecord Session { get; private set; } = default!;
        public byte[] BeforeSession { get; private set; } = [];
        public string[] BeforeRuns { get; private set; } = [];
        public CancellationToken Token => cancellation.Token;

        public async Task InitializeAsync() {
            var createdAt = DateTimeOffset.UtcNow;
            Session = await Fixture.Store.CreateChatSessionAsync(new ChatSessionRecord(
                Guid.NewGuid(), Fixture.Agent.Id, "Original selected thread", createdAt, createdAt, []), Token);
            BeforeSession = await File.ReadAllBytesAsync(Layout.SessionPath(Session.Id), Token);
            BeforeRuns = Directory.GetDirectories(Layout.ExecutionRunsRoot).Order(StringComparer.Ordinal).ToArray();
            var store = DispatchProxy.Create<AgentFrameworkExecutionRunTrackingIntegrationTests.IStartupBaselineWorkspaceStore, StartupOwnerProxy>();
            Proxy = (StartupOwnerProxy)(object)store;
            Proxy.Target = Fixture.Store;
            var coordinator = new AgentExecutionActivityCoordinator(new PartitionedSequencedStream<AgentExecutionActivityStreamId,
                AgentExecutionActivity>(PartitionedSequencedStreamPolicy.Default, TimeProvider.System), TimeProvider.System);
            var identity = new AgentExecutionActivityWorkspaceIdentity(Fixture.Profile.ProfileId, Fixture.StorageScope, Fixture.Profile.Generation);
            Operation = Assert.IsType<AgentExecutionActivityAdmitted>(coordinator.AdmitOperation(
                identity.CreateStreamId(AgentExecutionOperationId.New()), Fixture.Agent.Id, Session.Id, "Admission proof")).Operation;
            service = new(store, new ZipAgentPackageService(Fixture.WorkspaceRoot, Fixture.StorageScope), Runtime, Runtime, Runtime, Runtime,
                new CapabilityProofService(new PhysicalFileSystemPathPolicyFactory()), NullLogger<AgentFrameworkWorkspaceService>.Instance,
                coordinator, identity, cache, new FixedAgentExecutionProfileGenerationSource(Fixture.Profile.Generation), new NoProcessLeases(),
                new ExternalTargetPathRegistryFactory(), toolAdmissionJournal: Fixture.NewJournal());
        }

        public Task<AgentChatRunResult> Start() => pending = service!.SendMessageWithinOperationAsync(Operation,
            Fixture.Agent.Id, Session.Id, "Original immutable submitted prompt", new(Operation.StreamId.OperationId), Token);

        public async Task<AgentChatRunResult> ObserveAsync() {
            try {
                return await pending!;
            } finally {
                observed = true;
            }
        }

        public async Task HoldAgentAsync() => blocker = await AcquireAgentAsync();

        private async Task<IAsyncDisposable> AcquireAgentAsync() {
            var catalog = await Fixture.Store.LoadCatalogAsync(Token);
            var target = catalog with { Agents = catalog.Agents.Select(agent => agent.Id == Fixture.Agent.Id
                ? agent with { Name = agent.Name + " key selection" } : agent).ToArray() };
            var path = Assert.Single(FileSandboxWorkspaceWriteSet.Catalog(catalog, target).LockPaths(Layout));
            return await new DurableFileWriter(new PhysicalFileSystemPathPolicyFactory())
                .AcquireCoordinationAsync(Fixture.WorkspaceRoot, path, TimeSpan.FromSeconds(5), false, Token);
        }

        public async Task ReleaseAsync() {
            var owned = blocker;
            blocker = null;
            if (owned is not null) {
                await owned.DisposeAsync();
            }
        }

        public async Task RequireAgentAvailableAsync() {
            await using var acquired = await AcquireAgentAsync();
        }

        public async Task CleanupAsync(List<Exception> failures) {
            try {
                await cancellation.CancelAsync();
            } catch (Exception failure) {
                failures.Add(failure);
            }
            try {
                await ReleaseAsync();
            } catch (Exception failure) {
                failures.Add(failure);
            }
            if (pending is not null) {
                try {
                    await pending;
                } catch (Exception failure) {
                    if (!observed && failure is not OperationCanceledException) {
                        failures.Add(failure);
                    }
                }
            }
            foreach (var owned in new IDisposable?[] { Operation, service, cache, cancellation }) {
                try {
                    owned?.Dispose();
                } catch (Exception failure) {
                    failures.Add(failure);
                }
            }
        }
    }

    public class StartupOwnerProxy : DispatchProxy {
        public FileSandboxWorkspaceStore Target { get; set; } = default!;
        public Exception? Failure { get; set; }
        public int Starts { get; private set; }
        public int Callbacks { get; private set; }

        protected override object? Invoke(MethodInfo? method, object?[]? args) {
            ArgumentNullException.ThrowIfNull(method);
            ArgumentNullException.ThrowIfNull(args);
            if (method.Name == nameof(ISandboxWorkspaceChatRunStartStore.BeginChatBackedRunWithAdmissionKnowledgeAsync)) {
                Starts++;
                if (Failure is { } failure) {
                    return Task.FromException<ChatBackedRunStartResult>(failure);
                }
                var create = (Func<ChatBackedRunStartContext, ChatBackedRunStartMutation>)args[1]!;
                args[1] = (Func<ChatBackedRunStartContext, ChatBackedRunStartMutation>)(context => {
                    Callbacks++;
                    return create(context);
                });
            }
            try {
                return method.Invoke(Target, args);
            } catch (TargetInvocationException failure) when (failure.InnerException is not null) {
                ExceptionDispatchInfo.Capture(failure.InnerException).Throw();
                throw;
            }
        }
    }

    private sealed class LegacyOwner(Exception failure) : ISandboxWorkspaceChatRunStartStore {
        public int Calls { get; private set; }
        public Task<ChatBackedRunStartResult> BeginChatBackedRunAsync(ChatBackedRunStartRequest request,
            Func<ChatBackedRunStartContext, ChatBackedRunStartMutation> create, CancellationToken cancellationToken = default) {
            Calls++;
            return Task.FromException<ChatBackedRunStartResult>(failure);
        }
    }

    private sealed class PublicationWait : IDisposable {
        private readonly Activity trace = new Activity("chat-admission-knowledge").SetIdFormat(ActivityIdFormat.W3C).Start();
        private readonly ActivityListener listener;
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public PublicationWait() {
            listener = new() {
                ShouldListenTo = source => source.Name == AgentFrameworkTelemetry.SourceName,
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
                ActivityStarted = activity => {
                    if (activity.TraceId == trace.TraceId && activity.OperationName == "workspace.publication.keys.wait") {
                        Entered.TrySetResult();
                    }
                }
            };
            ActivitySource.AddActivityListener(listener);
        }

        public void Dispose() {
            listener.Dispose();
            trace.Dispose();
        }
    }

    private sealed class NoDispatchRuntime : IAgentExecutionRuntime, IAgentContinuationRuntime,
        IProviderDiagnosticsRuntime, IProviderModelAdministrationRuntime {
        public int Calls { get; private set; }
        private Exception Unexpected() {
            Calls++;
            return new InvalidOperationException("Rejected admission must not dispatch a provider or tool.");
        }
        public Task<AgentRuntimeResponse> ExecuteAsync(AgentRuntimeExecutionRequest request, CancellationToken cancellationToken = default) => throw Unexpected();
        public Task<AgentRuntimeResponse> ContinueAsync(AgentRuntimeContinuationRequest request, CancellationToken cancellationToken = default) => throw Unexpected();
        public Task<ProviderHealthResult> TestHealthAsync(ProviderProfile provider, CancellationToken cancellationToken = default) => throw Unexpected();
        public Task<ProviderTestChatResult> RunProbeAsync(ProviderProfile provider, ProviderTestChatRequest request, CancellationToken cancellationToken = default) => throw Unexpected();
        public Task<ProviderModelMaintenanceEditorResult> CreateOrUpdateModelAsync(ProviderProfile provider,
            ProviderModelMaintenanceEditorRequest request, CancellationToken cancellationToken = default) => throw Unexpected();
    }

    private sealed class NoProcessLeases : IWorkspaceExecutionRunProcessLeaseCleaner {
        public Task<WorkspaceExecutionRunProcessCleanupResult> CleanupAsync(Guid executionRunId)
            => Task.FromResult(WorkspaceExecutionRunProcessCleanupResult.Empty(executionRunId));
    }
}
