using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.Infrastructure;
using CanDoItAll.Infrastructure.FileSystem;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.AgentFramework.Pages.Components;
using IProviderRuntimeAdministrationService = CanDoItAll.Modules.AgentFramework.ProviderManagement.IProviderRuntimeAdministrationService;
using CanDoItAll.SharedKernel.Streaming;
using CanDoItAll.Tests.Support;
using CanDoItAll.Web.Api;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Tests.Integration.Runtime;

public enum ReceiptPerformanceWorkload { CoreSlice, FreshDetail, NativeSessionRefresh }
public enum ReceiptPerformanceShape { OneProposal, FourProposals, MaximumProposals, NearMaximumBytes }
internal enum ReceiptPerformanceTopology { Serial, IndependentWorkspaces, SharedWorkspace }
internal sealed record ReceiptPerformanceShapeEvidence(int Worker, Guid RunId, Guid ChatId, int Proposals,
    int JournalUtf8Bytes, int CompactRunUtf8Bytes, long RunRevision, long JournalRevision,
    string InitialJournalSha256, string InitialCompactRunSha256);
internal sealed record ReceiptPerformanceOperation(int Worker, int ResponseBytes, int Examined,
    AgentEffectReconciliationStop Stop, AgentEffectCheckpointPersistence Persistence, long RunRevision, long JournalRevision);

internal sealed class AgentReceiptStorePerformanceFixture : IAsyncDisposable {
    private static readonly DateTimeOffset InitialTime = new(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);
    private readonly List<Workspace> owners = [];
    private readonly List<Target> targets = [];
    private readonly ServiceProvider jsonServices;
    private readonly JsonSerializerOptions responseJson;

    internal string[] OwnedRoots => owners.Select(owner => owner.Environment.RootPath).ToArray();

    private AgentReceiptStorePerformanceFixture() {
        var services = new ServiceCollection();
        services.ConfigureAgentApiJson();
        jsonServices = services.BuildServiceProvider();
        responseJson = jsonServices.GetRequiredService<IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions>>().Value.SerializerOptions;
    }

    internal ReceiptPerformanceShapeEvidence[] Shapes => targets.Select((target, index) => new ReceiptPerformanceShapeEvidence(
        index, target.Run.Id, target.Run.ChatSessionId!.Value, target.Proposals, target.JournalBytes,
        target.RunBytes, target.Run.Revision, target.InitialJournalRevision, target.JournalHash, target.RunHash)).ToArray();

    internal static async Task<AgentReceiptStorePerformanceFixture> CreateAsync(string ownedRoot,
        ReceiptPerformanceShape shape, ReceiptPerformanceTopology topology, int concurrency) {
        var result = new AgentReceiptStorePerformanceFixture();
        try {
            await using var source = await AgentToolAdmissionJournalFixture.CreateAsync(ownedParentRoot: ownedRoot,
                configureAgent: agent => agent with { IsTemplate = false, Status = AgentLifecycleStatus.Active });
            for (var index = 0; index < concurrency; index++) {
                var profileWorker = topology == ReceiptPerformanceTopology.SharedWorkspace ? 0 : index;
                var profile = new AgentToolProfileBinding(StableId(SeedKind.Profile, shape, profileWorker),
                    "receipt-store-reference-profile-v1", new(1));
                var run = BuildRun(source, shape, index, profile);
                Workspace owner;
                if (topology != ReceiptPerformanceTopology.SharedWorkspace || index == 0) {
                    var environment = CanDoItAllTestEnvironment.CreateUnder(ownedRoot, "receipt-workspace");
                    try {
                        owner = new(environment, environment.CreateInMemoryProfile("primary").WorkspaceRootPath,
                            profile, source.Agent, source.Provider);
                    } catch (Exception original) {
                        try {
                            await environment.DisposeAsync();
                        } catch (Exception cleanup) {
                            throw new AggregateException(original, cleanup);
                        }
                        throw;
                    }
                    result.owners.Add(owner);
                    var initial = owner.NewStore();
                    await initial.UpdateCatalogAsync(catalog => catalog with {
                        Agents = catalog.Agents.Where(agent => agent.Id != source.Agent.Id).Append(source.Agent).ToArray(),
                        Providers = catalog.Providers.Where(provider => provider.Id != source.Provider.Id).Append(source.Provider).ToArray()
                    });
                } else {
                    owner = result.owners[0];
                }
                await owner.NewStore().SaveExecutionRunDetailAsync(new(run, NewChat(source, run), [], []));
                var journal = run.ToolAdmission!;
                var journalBytes = JsonSerializer.SerializeToUtf8Bytes(journal);
                var runBytes = JsonSerializer.SerializeToUtf8Bytes(run);
                result.targets.Add(new(owner, run with { ToolAdmission = null }, journal.Session.Reference,
                    journal.Batches.Sum(batch => batch.Proposals.Length), journalBytes.Length,
                    runBytes.Length, journal.Revision, Convert.ToHexString(SHA256.HashData(journalBytes)),
                    Convert.ToHexString(SHA256.HashData(runBytes))));
            }
            return result;
        } catch (Exception original) {
            try {
                await result.DisposeAsync();
            } catch (Exception cleanup) {
                throw new AggregateException(original, cleanup);
            }
            throw;
        }
    }

    private static ExecutionRunRecord BuildRun(AgentToolAdmissionJournalFixture fixture, ReceiptPerformanceShape shape,
        int worker, AgentToolProfileBinding profile) {
        var count = shape switch {
            ReceiptPerformanceShape.OneProposal => 1,
            ReceiptPerformanceShape.FourProposals => 4,
            ReceiptPerformanceShape.MaximumProposals => AgentToolJournalRecord.MaximumBatches * AgentToolJournalRecord.MaximumCallsPerBatch,
            ReceiptPerformanceShape.NearMaximumBytes => 192,
            _ => throw new ArgumentOutOfRangeException(nameof(shape))
        };
        var authority = new AgentExecutionAuthorityRecord(new(StableId(SeedKind.Authority, shape, worker)), fixture.Agent.Id,
            profile.ProfileId, profile.Generation, WorkspaceScopeDescriptor.Sandbox, true, true,
            "fixture-policy", "fixture-authority", InitialTime, schemaVersion: AgentExecutionAuthorityRecord.LegacySchemaVersion);
        var context = new AgentTurnContextReference(new(StableId(SeedKind.Context, shape, worker)),
            new(StableId(SeedKind.ContextEpoch, shape, worker)), new("agents"), new("agents"), "agents", "chat", 1,
            "fixture-context", InitialTime);
        var run = fixture.Detail.Run with {
            Id = StableId(SeedKind.Run, shape, worker), ChatSessionId = StableId(SeedKind.Chat, shape, worker), ToolAdmission = null,
            CreatedAtUtc = InitialTime, UpdatedAtUtc = InitialTime, StartedAtUtc = InitialTime,
            MetadataJson = AgentTurnContextMetadata.Apply("{}", context, authority)
        };
        run = run with { SourceId = run.ChatSessionId!.Value.ToString("N") };
        var original = new AgentToolAdmissionJournal(fixture.Store, profile)
            .CreateForNewRun(run, NewChat(fixture, run), AgentToolAdmissionSupport.Recoverable);
        var payload = AgentToolAdmissionJournalFixture.Payload();
        var origin = StableId(SeedKind.Origin, shape, worker);
        var batches = Enumerable.Range(0, (count + 63) / 64).Select(batch => new AgentToolBatchRecord(
            new(StableId(SeedKind.Batch, shape, worker, batch)), batch, payload.Digest, AgentToolAdmissionJournalFixture.Envelope(),
            Enumerable.Range(0, Math.Min(64, count - batch * 64)).Select(index => new AgentToolProposalRecord(
                new(StableId(SeedKind.Intent, shape, worker, batch * 64 + index)), index, $"call-{batch}-{index}", payload, false,
                AgentToolProposalState.Executing, ExecutionApprovalStatus.Approved, payload.Digest,
                DispatchClaimId: StableId(SeedKind.Dispatch, shape, worker, batch * 64 + index))).ToImmutableArray())).ToImmutableArray();
        var journal = original with {
            SchemaVersion = AgentToolJournalRecord.EffectJournalSchemaVersion,
            Support = AgentToolAdmissionSupport.EffectJournalOnly,
            ActiveDispatchLeaseId = origin,
            Segments = [new(StableId(SeedKind.Segment, shape, worker), 0, 0, null, PendingApprovals: [],
                CheckpointKind: AgentToolCheckpointKind.EffectsOnly, OriginDispatchLeaseId: origin)],
            Batches = batches
        };
        if (shape == ReceiptPerformanceShape.NearMaximumBytes) {
            var baseline = JsonSerializer.SerializeToUtf8Bytes(journal).Length;
            var padding = (AgentToolJournalRecord.MaximumUtf8Bytes - 64 * 1024 - baseline) / 3;
            journal = journal with {
                Batches = journal.Batches.Select(batch => batch with {
                    Response = AgentToolAdmissionJournalFixture.Envelope(JsonSerializer.Serialize(new { retained = new string('x', padding) }))
                }).ToImmutableArray()
            };
        }
        journal.Validate();
        if (shape == ReceiptPerformanceShape.NearMaximumBytes) {
            Assert.InRange(JsonSerializer.SerializeToUtf8Bytes(journal).Length, 23 * 1024 * 1024, AgentToolJournalRecord.MaximumUtf8Bytes);
        }
        return run with { ToolAdmission = journal };
    }

    private static ChatSessionRecord NewChat(AgentToolAdmissionJournalFixture source, ExecutionRunRecord run)
        => new(run.ChatSessionId!.Value, source.Agent.Id, run.Title, run.CreatedAtUtc, run.UpdatedAtUtc, [], LatestExecutionRunId: run.Id);

    internal async Task<ReceiptPerformanceOperation> ExecuteAsync(int worker, ReceiptPerformanceWorkload workload,
        AgentReceiptStorePerformanceProbe probe) {
        var target = targets[worker];
        Owner? owner = null;
        Exception? primary = null;
        try {
            owner = await probe.MeasureAsync(worker, ReceiptPerformancePhase.OwnerPreparation, async () => {
                var created = new Owner(target, probe);
                try {
                    await created.Store.LoadCatalogSnapshotAsync();
                    return created;
                } catch {
                    created.Dispose();
                    throw;
                }
            });
            var result = workload switch {
                ReceiptPerformanceWorkload.CoreSlice => await SliceAsync(worker, target, owner, probe),
                ReceiptPerformanceWorkload.FreshDetail => await DetailAsync(worker, target, owner, probe),
                ReceiptPerformanceWorkload.NativeSessionRefresh => await NativeAsync(worker, target, owner, probe),
                _ => throw new ArgumentOutOfRangeException(nameof(workload))
            };
            probe.ObserveOperation(result);
            Assert.Equal(0, owner.Runtime.Calls);
            return result;
        } catch (Exception exception) {
            primary = exception;
            throw;
        } finally {
            if (owner is not null) {
                try {
                    await probe.MeasureAsync(worker, ReceiptPerformancePhase.OwnerDisposal, () => {
                        owner.Dispose();
                        return Task.CompletedTask;
                    });
                } catch (Exception cleanup) when (primary is not null) {
                    throw new AggregateException(primary, cleanup);
                }
            }
        }
    }

    private async Task<ReceiptPerformanceOperation> SliceAsync(int worker, Target target, Owner owner, AgentReceiptStorePerformanceProbe probe) {
        var lease = await probe.MeasureAsync(worker, ReceiptPerformancePhase.Acquire,
            () => owner.Journal.AcquireInterruptedReconciliationAsync(target.Session, default).AsTask());
        AgentEffectContinuation result;
        Exception? primary = null;
        try {
            using var bound = lease.Bind();
            result = await probe.MeasureAsync(worker, ReceiptPerformancePhase.Reconcile, () => owner.Journal.ReconcileInterruptedAsync(
                lease, [new NotObservedReader(worker, probe)], (_, token) => {
                    token.ThrowIfCancellationRequested();
                    probe.CurrentRead();
                    return new(true);
                }, default));
            probe.ObserveOperation(new(worker, 0, result.ExaminedThisSlice, result.Stop, result.Persistence,
                result.RunRevision, result.JournalRevision));
        } catch (Exception exception) {
            primary = exception;
            throw;
        } finally {
            try {
                await probe.MeasureAsync(worker, ReceiptPerformancePhase.LeaseRelease, () => lease.DisposeAsync().AsTask());
            } catch (Exception cleanup) when (primary is not null) {
                throw new AggregateException(primary, cleanup);
            }
        }
        var bytes = await EncodeAsync(worker, result, probe);
        return new(worker, bytes, result.ExaminedThisSlice, result.Stop, result.Persistence, result.RunRevision, result.JournalRevision);
    }

    private async Task<ReceiptPerformanceOperation> DetailAsync(int worker, Target target, Owner owner, AgentReceiptStorePerformanceProbe probe) {
        var detail = await probe.MeasureAsync(worker, ReceiptPerformancePhase.DetailRead,
            () => owner.Service.GetExecutionRunDetailAsync(target.Run.Id));
        Assert.Equal(target.Run.Id, detail.Run.Id);
        var continuation = Assert.IsType<AgentEffectContinuation>(detail.EffectContinuation);
        probe.ObserveOperation(new(worker, 0, continuation.ExaminedThisSlice, continuation.Stop, continuation.Persistence,
            continuation.RunRevision, continuation.JournalRevision));
        var bytes = await probe.MeasureAsync(worker, ReceiptPerformancePhase.ResponseEncoding, () => Task.FromResult(
            JsonSerializer.SerializeToUtf8Bytes(AgentApiResponseMapper.ToExecutionRunDetail(detail), responseJson).Length));
        return new(worker, bytes, continuation.ExaminedThisSlice, continuation.Stop, continuation.Persistence,
            continuation.RunRevision, continuation.JournalRevision);
    }

    private async Task<ReceiptPerformanceOperation> NativeAsync(int worker, Target target, Owner owner, AgentReceiptStorePerformanceProbe probe) {
        using var session = new AgentChatSession(owner.Service, new MetadataProviders(target.Fixture.Provider),
            new FixedAgentExecutionProfileGenerationSource(target.Fixture.Profile.Generation), NullLogger.Instance);
        var loaded = await probe.MeasureAsync(worker, ReceiptPerformancePhase.NativeRefresh, () => session.LoadAsync(
            target.Fixture.Agent.Id, target.Run.ChatSessionId, focused: false, runId: target.Run.Id,
            expectedProfileGeneration: target.Fixture.Profile.Generation));
        Assert.True(loaded, "The real native session failed to accept its current workspace/detail read.");
        Assert.Equal(target.Run.Id, session.Workspace!.SelectedRun!.Id);
        var continuation = Assert.IsType<AgentEffectContinuation>(session.EffectContinuation);
        probe.ObserveOperation(new(worker, 0, continuation.ExaminedThisSlice, continuation.Stop, continuation.Persistence,
            continuation.RunRevision, continuation.JournalRevision));
        var bytes = await EncodeAsync(worker, continuation, probe);
        return new(worker, bytes, continuation.ExaminedThisSlice, continuation.Stop, continuation.Persistence,
            continuation.RunRevision, continuation.JournalRevision);
    }

    private Task<int> EncodeAsync<T>(int worker, T value, AgentReceiptStorePerformanceProbe probe)
        => probe.MeasureAsync(worker, ReceiptPerformancePhase.ResponseEncoding,
            () => Task.FromResult(JsonSerializer.SerializeToUtf8Bytes(value, responseJson).Length));

    public async ValueTask DisposeAsync() {
        var failures = new List<Exception>();
        foreach (var owner in owners) {
            try {
                await owner.DisposeAsync();
            } catch (Exception exception) {
                failures.Add(exception);
            }
        }
        jsonServices.Dispose();
        if (failures.Count != 0) {
            throw new AggregateException("One or more owned file fixtures could not be disposed.", failures);
        }
    }

    private sealed record Target(Workspace Fixture, ExecutionRunRecord Run, AgentToolSessionReference Session,
        int Proposals, int JournalBytes, int RunBytes, long InitialJournalRevision, string JournalHash, string RunHash);

    private enum SeedKind { Profile, Run, Chat, Authority, Context, ContextEpoch, Origin, Segment, Batch, Intent, Dispatch }

    private static Guid StableId(SeedKind kind, ReceiptPerformanceShape shape, int worker, int item = 0) {
        var value = FormattableString.Invariant($"receipt-store-reference-v1:{(int)kind}:{(int)shape}:{worker}:{item}");
        return new(SHA256.HashData(Encoding.UTF8.GetBytes(value)).AsSpan(0, 16));
    }

    private sealed record Workspace(CanDoItAllTestEnvironment Environment, string WorkspaceRoot,
        AgentToolProfileBinding Profile, AgentDefinition Agent, ProviderProfile Provider) : IAsyncDisposable {
        internal WorkspaceScopeDescriptor StorageScope => WorkspaceScopeDescriptor.Sandbox;
        internal FileSandboxWorkspaceStore NewStore() => new(WorkspaceRoot, StorageScope);
        public ValueTask DisposeAsync() => Environment.DisposeAsync();
    }

    private sealed class Owner : IDisposable {
        private readonly AgentExecutionPreparationCache cache = new(AgentExecutionPreparationCachePolicy.Default);
        private readonly AgentEffectRecoveryFixture.AuthorityResolver resolver;
        private readonly AgentReceiptStorePerformanceProbe probe;
        internal Owner(Target target, AgentReceiptStorePerformanceProbe probe) {
            this.probe = probe;
            resolver = new(target.Run);
            Store = new(target.Fixture.WorkspaceRoot, target.Fixture.StorageScope, null, probe.Commit,
                new FileSandboxWorkspaceJsonReadDiagnostics(probe.Read));
            Journal = new(Store, target.Fixture.Profile);
            Runtime = new();
            var coordinator = new AgentExecutionActivityCoordinator(new PartitionedSequencedStream<AgentExecutionActivityStreamId, AgentExecutionActivity>(
                PartitionedSequencedStreamPolicy.Default, TimeProvider.System), TimeProvider.System);
            var identity = new AgentExecutionActivityWorkspaceIdentity(target.Fixture.Profile.ProfileId,
                target.Fixture.StorageScope, target.Fixture.Profile.Generation);
            Service = new(Store, new ZipAgentPackageService(target.Fixture.WorkspaceRoot, target.Fixture.StorageScope),
                Runtime, Runtime, Runtime, Runtime, new CapabilityProofService(new PhysicalFileSystemPathPolicyFactory()),
                NullLogger<AgentFrameworkWorkspaceService>.Instance, coordinator, identity, cache,
                new FixedAgentExecutionProfileGenerationSource(target.Fixture.Profile.Generation), new AgentEffectRecoveryFixture.NoProcessLeases(),
                new ExternalTargetPathRegistryFactory(), toolAdmissionJournal: Journal,
                executionAuthorityResolver: resolver);
        }
        internal FileSandboxWorkspaceStore Store { get; }
        internal AgentToolAdmissionJournal Journal { get; }
        internal AgentFrameworkWorkspaceService Service { get; }
        internal AgentEffectRecoveryFixture.ForbiddenRuntime Runtime { get; }
        public void Dispose() {
            try {
                Service.Dispose();
            } finally {
                try {
                    cache.Dispose();
                } finally {
                    probe.CurrentRead(resolver.Calls);
                }
            }
        }
    }

    private sealed class NotObservedReader(int worker, AgentReceiptStorePerformanceProbe probe) : IAgentToolReceiptReconciliationProvider {
        public bool Supports(string toolName) => toolName == AgentToolAdmissionJournalFixture.Payload().ToolName;
        public async ValueTask<AgentToolReceiptObservation> ReconcileAsync(AgentToolReceiptReconciliationClaim claim,
            CancellationToken cancellationToken = default) {
            return await probe.MeasureAsync(worker, ReceiptPerformancePhase.OwnerLookup, async () => {
                probe.Lookup();
                await claim.RequireAsync(cancellationToken);
                return AgentToolReceiptObservation.NotObserved;
            });
        }
    }

    private sealed class MetadataProviders(ProviderProfile provider) : IProviderRuntimeAdministrationService {
        public Task<IReadOnlyList<ProviderProfile>> ListProvidersAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ProviderProfile>>([provider]);
        public Task<ProviderProfileEditorModel> GetProviderEditorAsync(Guid? providerId = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Guid> SaveProviderAsync(ProviderProfileEditorModel model, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task DeleteProviderAsync(Guid providerId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ProviderHealthResult> TestProviderAsync(Guid providerId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ProviderTestChatResult> RunProviderTestChatAsync(Guid providerId, ProviderTestChatRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ProviderModelMaintenanceEditorResult> CreateOrUpdateProviderModelAsync(Guid providerId,
            ProviderModelMaintenanceEditorRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
