using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.Infrastructure;
using CanDoItAll.Infrastructure.FileSystem;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.SharedKernel.Streaming;
using CanDoItAll.Tests.Support;
using Microsoft.Agents.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Integration.Processes;

[Trait("Category", "FileSystemPortability")]
public sealed class ProcessRegistrationCancellationRaceTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Actual_Core_and_MAF_entry_preserves_parent_cancellation_across_registration(bool registeredBeforeStop) {
        var registry = new RegistrationGate(registeredBeforeStop);
        await using var app = await TestApplication.CreateAsync(ProcessAdmissionCancellationRaceTests.Harness(services =>
            services.Replace(ServiceDescriptor.Singleton<IAgentExecutionCancellationRegistry>(registry))));
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var fixture = await ProcessExecutionAuthorityPersistenceTests.SeedAsync(services, admitBackground: true, persistExecution: false);
        var store = services.GetRequiredService<ISandboxWorkspaceStore>();
        await store.UpdateCatalogAsync(current => {
            var provider = current.Providers.First() with { Kind = ProviderKind.Ollama, Transport = ProviderTransportKind.ChatCompletions,
                DefaultModel = "fixture-model", ConfigurationJson = "{}" };
            var agent = current.Agents.Single(item => item.Id == fixture.Run.AgentId) with {
                ProviderProfileId = provider.Id, Model = provider.DefaultModel, Capabilities = []
            };
            return current with {
                Agents = current.Agents.Select(item => item.Id == agent.Id ? agent : item).ToArray(),
                Providers = current.Providers.Select(item => item.Id == provider.Id ? provider : item).ToArray()
            };
        });
        var journal = new AgentToolAdmissionJournal(store, fixture.Run.ToolAdmission!.Session.Profile,
            services.GetRequiredService<TimeProvider>(), services.GetServices<IAgentToolBackgroundSourcePolicy>());
        var wire = new RefusingProvider();
        var dependencies = MafAgentRuntimeDependencies.FromServices(services) with {
            ToolAdmissionJournal = journal, ProviderAgentFactory = wire, ProviderRuntimeGateway = wire
        };
        var root = services.GetRequiredService<IWorkspacePathResolver>().ResolveWorkspaceRoot();
        var storageScope = WorkspaceScopeDescriptor.Organization(journal.Profile.ProfileId.ToString("N"));
        var runtime = new MafAgentRuntime(root, storageScope, dependencies);
        using var cache = new AgentExecutionPreparationCache(AgentExecutionPreparationCachePolicy.Default);
        var activities = new AgentExecutionActivityCoordinator(new PartitionedSequencedStream<AgentExecutionActivityStreamId,
            AgentExecutionActivity>(PartitionedSequencedStreamPolicy.Default, TimeProvider.System), TimeProvider.System);
        using var service = new AgentFrameworkWorkspaceService(store, new ZipAgentPackageService(root, storageScope), runtime.ExecutionPort,
            runtime.ContinuationPort, runtime.DiagnosticsPort, runtime.ModelAdministrationPort,
            new CapabilityProofService(new PhysicalFileSystemPathPolicyFactory()), NullLogger<AgentFrameworkWorkspaceService>.Instance,
            activities, new(journal.Profile.ProfileId, storageScope, journal.Profile.Generation), cache,
            services.GetRequiredService<IAgentExecutionProfileGenerationSource>(), new NoProcessLeases(), new ExternalTargetPathRegistryFactory(),
            executionCancellationRegistry: registry, toolAdmissionJournal: journal);
        var source = ExecutionRunSourceKey.ForBackground(fixture.Run.SourceKind, fixture.Run.SourceId, fixture.Run.CorrelationId, fixture.Run.CausationId);
        var request = new ExecutionRunRequest(fixture.Run.AgentId, "Observe the original Process cancellation.", AgentExecutionOperationId.New(),
            Context: new(SourceKind: fixture.Run.SourceKind, SourceId: fixture.Run.SourceId, CorrelationId: fixture.Run.CorrelationId,
                CausationId: fixture.Run.CausationId, RequestedBy: fixture.Run.RequestedBy, RequestedByKind: fixture.Run.RequestedByKind,
                MetadataJson: fixture.Run.MetadataJson, ProcessRunId: fixture.Run.ProcessRunId, ProcessStepId: fixture.Run.ProcessStepId),
            AutoApprovePendingToolCalls: true);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var execution = Task.Run(() => service.ExecuteSameSourceRunAsync(source, request, deadline.Token));
        var observed = false;
        try {
            if (await Task.WhenAny(registry.Entered.Task, execution).WaitAsync(deadline.Token) == execution) {
                await execution;
                Assert.Fail("The actual Core producer never reached cancellation registration.");
            }
            var original = await registry.Entered.Task.WaitAsync(deadline.Token);
            Assert.NotEqual(fixture.Run.Id, original.Id);
            Assert.NotNull(original.ToolAdmission!.Session.Reference.BackgroundSource);
            var owner = services.GetRequiredService<IProcessRuntimeUnitOfWork>();
            var state = (await services.GetRequiredService<IProcessRuntimeStateStore>().LoadAsync(new(fixture.RunId), deadline.Token))!;
            await ProcessRootCancellationFixture.StopAsync(owner, state);
            var stopped = await services.GetRequiredService<IProcessExecutionStopper>()
                .StopAsync(new(fixture.RunId), new(fixture.RunId), new(original.Id), deadline.Token);
            Assert.Equal(registeredBeforeStop ? ProcessCancellationRecoveryPending.ActiveWork : ProcessCancellationRecoveryPending.None, stopped);
            var stoppedDetail = await new FileSandboxWorkspaceStore(root, storageScope).GetExecutionRunDetailAsync(original.Id, deadline.Token);
            if (!registeredBeforeStop) {
                await ProcessAdmissionCancellationRaceTests.CompleteRecoveryAsync(app.Services, deadline.Token);
            }
            registry.Release.TrySetResult();
            var failure = await Record.ExceptionAsync(async () => await execution);
            observed = true;
            var savedDetail = (await new FileSandboxWorkspaceStore(root, storageScope).GetExecutionRunDetailAsync(original.Id, deadline.Token))!;
            var saved = savedDetail.Run;
            Assert.Equal(0, wire.Calls);
            Assert.Equal(RunOutcome.Cancelled, saved.Outcome);
            Assert.Equal(original.ToolAdmission.Session, saved.ToolAdmission!.Session);
            Assert.Equal(original.ToolAdmission.BackgroundInput, saved.ToolAdmission.BackgroundInput);
            Assert.IsType<AgentExecutionCancelledException>(failure);
            if (!registeredBeforeStop) {
                Assert.Equal(JsonSerializer.Serialize(stoppedDetail), JsonSerializer.Serialize(savedDetail));
            }
            Assert.False(registry.RequestCancellation(saved.ToolAdmission.Session));
            if (registeredBeforeStop) {
                await ProcessAdmissionCancellationRaceTests.CompleteRecoveryAsync(app.Services, deadline.Token);
            }
        } finally {
            registry.Release.TrySetResult();
            await deadline.CancelAsync();
            if (!observed) {
                try {
                    await execution;
                } catch (OperationCanceledException) when (deadline.IsCancellationRequested) {
                }
            }
        }
    }

    private sealed class RegistrationGate(bool registeredBeforeStop) : IAgentExecutionCancellationRegistry {
        private readonly AgentExecutionCancellationRegistry inner = new(NullLogger<AgentExecutionCancellationRegistry>.Instance);
        internal TaskCompletionSource<ExecutionRunRecord> Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public IAgentExecutionCancellationRegistration Register(ExecutionRunRecord run, CancellationToken cancellationToken) {
            var registration = registeredBeforeStop ? inner.Register(run, cancellationToken) : null;
            try {
                Entered.SetResult(run);
                Release.Task.WaitAsync(cancellationToken).GetAwaiter().GetResult();
                return registration ?? inner.Register(run, cancellationToken);
            } catch {
                registration?.Dispose();
                throw;
            }
        }

        public bool RequestCancellation(Guid executionRunId) => inner.RequestCancellation(executionRunId);
        public bool RequestCancellation(AgentToolSessionAdmission original) => inner.RequestCancellation(original);
        public int RequestCancellationByProcessRunIds(IReadOnlyCollection<string> processRunIds, string requestedBy, string reason)
            => inner.RequestCancellationByProcessRunIds(processRunIds, requestedBy, reason);
    }

    private sealed class RefusingProvider : IMafProviderAgentFactory, IMafProviderRuntimeGateway {
        internal int Calls { get; private set; }
        public AIAgent CreateFrameworkAgent(ProviderProfile provider, string model, ChatClientAgentOptions options,
            bool frameworkManagedHistory, bool allowBackgroundResponses) => throw Refused();
        public Task<ProviderHealthResult> TestProviderAsync(ProviderProfile provider, CancellationToken cancellationToken = default) => throw Refused();
        public Task<ProviderTestChatResult> RunProviderTestChatAsync(ProviderProfile provider, ProviderTestChatRequest request,
            string model, CancellationToken cancellationToken = default) => throw Refused();
        public Task<ProviderTestChatResult> RunProviderImageChatAsync(ProviderProfile provider, ProviderTestChatRequest request,
            string model, IReadOnlyList<ProviderChatAttachment> attachments, string modelParameterConfigurationJson = "",
            CancellationToken cancellationToken = default) => throw Refused();
        public Task<ProviderModelMaintenanceEditorResult> CreateOrUpdateProviderModelAsync(ProviderProfile provider,
            ProviderModelMaintenanceEditorRequest request, CancellationToken cancellationToken = default) => throw Refused();

        private InvalidOperationException Refused() {
            Calls++;
            return new("The cancelled original Process must be refused before provider construction or invocation.");
        }
    }

    private sealed class NoProcessLeases : IWorkspaceExecutionRunProcessLeaseCleaner {
        public Task<WorkspaceExecutionRunProcessCleanupResult> CleanupAsync(Guid executionRunId)
            => Task.FromResult(WorkspaceExecutionRunProcessCleanupResult.Empty(executionRunId));
    }
}
