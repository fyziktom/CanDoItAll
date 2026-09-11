using System.Data;
using System.Data.Common;
using System.Text.Json;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workspace.ApiAccess;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Tests.Integration.ProjectStructure;

public sealed partial class WorkflowStructureDeliveryPersistenceTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Storage_operator_continuation_uses_original_receipts_after_independent_restart(bool nativeCommitted) {
        await using var environment = CanDoItAllTestEnvironment.Create("workflow-storage-continuation");
        var profile = environment.CreatePostgreSqlProfile("original");
        var options = new TestHarnessOptions { TestEnvironment = environment, ActiveProfile = profile, ConfigureServices = ConfigureContinuation };
        WorkflowStructureOutputPlan plan;
        StorageStablePlacementReceipt storageReceipt;
        string preparedBytes;
        WorkflowLaunchOrigin originalOrigin;
        await using (var first = await TestApplication.CreateAsync(options)) {
            await using var fixture = await CreateFixtureAsync(first, prepareOutput: false);
            var asset = await PrepareContinuationAssetAsync(fixture, nativeCommitted);
            plan = asset.Plan;
            storageReceipt = asset.Storage;
            originalOrigin = (await fixture.Services.GetRequiredService<IWorkflowRunStore>().GetRunAsync(plan.Identity.Occurrence.RunId))!.Origin!;
            preparedBytes = await RetainedWorkflowCommandAsync(fixture.Services, plan);
            if (nativeCommitted) {
                var receipt = (await fixture.Owner.FindWorkflowContributionAsync(plan.Identity))!;
                await fixture.Owner.UpdateObjectAsync(fixture.ProjectId, receipt.Node.Id, "Human edit", "Human subtitle", "Human notes");
            }
        }
        await using var restarted = await TestApplication.CreateAsync(options);
        await using var scope = restarted.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var drivers = services.GetRequiredService<ContinuationDriverProbe>();
        drivers.Armed = true;
        var recovery = services.GetRequiredService<StoragePlacementRecoveryService>();
        var request = new StoragePlacementRecoveryCommand(await recovery.GetCurrentContextAsync(), storageReceipt.IntentId);
        var service = services.GetRequiredService<IStoragePlacementOwnerContinuation>();
        var observation = await service.GetAsync(request);
        Assert.Equal(StoragePlacementOwnerContinuationState.Ready, observation.State);
        Assert.Equal(nativeCommitted ? StoragePlacementOwnerContinuationAction.RecordWorkflowAssetReceipt
            : StoragePlacementOwnerContinuationAction.CompletePreparedWorkflowAsset, observation.AvailableAction);
        Assert.Equal(new StoragePlacementWorkflowContinuationIntent(plan.Identity.Occurrence.RunId.Value,
            plan.Identity.Occurrence.Path, plan.Identity.Slot, plan.Fingerprint), observation.WorkflowIntent);
        Assert.Null((await OutputStore(services).FindAsync(plan.Identity))!.Receipt);
        var owner = services.GetRequiredService<ProjectWorkbenchService>();
        Assert.Equal(nativeCommitted, await owner.FindWorkflowContributionAsync(plan.Identity) is not null);
        var result = await service.ReconcileWorkflowAssetAsync(Command(observation));
        Assert.Equal(StoragePlacementOwnerContinuationState.ReceiptRecorded, result.State);
        Assert.Equal(StoragePlacementOwnerContinuationAction.None, result.AvailableAction);
        var native = (await owner.FindWorkflowContributionAsync(plan.Identity))!;
        Assert.Equal(storageReceipt.IntentId.Value, native.Receipt!.AssetId);
        Assert.Equal(plan.ProjectLifetime, native.Receipt.ProjectLifetime);
        Assert.Equal(native.Receipt, (await OutputStore(services).FindAsync(plan.Identity))!.Receipt);
        Assert.Equal(storageReceipt, (await services.GetRequiredService<StorageStablePlacementService>().FindAsync(storageReceipt.IntentId))!.Receipt);
        Assert.Equal(preparedBytes, await RetainedWorkflowCommandAsync(services, plan));
        Assert.Equal(JsonSerializer.Serialize(originalOrigin), JsonSerializer.Serialize((await services.GetRequiredService<IWorkflowRunStore>()
            .GetRunAsync(plan.Identity.Occurrence.RunId))!.Origin));
        if (nativeCommitted) {
            Assert.Equal("Human edit", Assert.Single((await owner.GetStructureAsync(plan.ProjectId)).Nodes, item => item.Id == native.Node.Id).Title);
        }
        Assert.Equal(StoragePlacementRecoveryFailure.Blocked,
            (await Assert.ThrowsAsync<StoragePlacementRecoveryException>(() => service.ReconcileWorkflowAssetAsync(Command(observation)))).Failure);
        Assert.Equal(0, drivers.Attempts);
        await using var database = await services.GetRequiredService<IDbContextFactory<WorkbenchDbContext>>().CreateDbContextAsync();
        Assert.Single(await database.Set<ProjectObjectRecord>().Where(row => row.ProjectId == plan.ProjectId && row.NodeKey == native.Node.Id).ToArrayAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Storage_operator_continuation_rechecks_operator_and_original_source_after_native_flush(bool revokeOperator) {
        var source = new MutableApiPolicy();
        var transactionProbe = new ContinuationTransactionProbe();
        await using var app = await TestApplication.CreateAsync(new() { ConfigureServices = services => {
            ConfigureContinuation(services);
            services.AddSingleton<IOptionsMonitor<ApiAccessOptions>>(source);
            services.AddSingleton(provider => StorageOptions(provider, transactionProbe));
        } });
        await using var fixture = await CreateFixtureAsync(app, prepareOutput: false, sourceSurface: WorkflowStructureOperatorSurface.Api);
        var asset = await PrepareContinuationAssetAsync(fixture, false);
        var access = new ContinuationAccess(fixture.Services.GetRequiredService<IStoragePlacementRecoveryAccess>());
        var revoke = new ContinuationAfterFlush(() => {
            if (revokeOperator) {
                access.Denied = true;
            } else {
                source.CurrentValue.Enabled = false;
            }
        });
        var failingOwner = Owner(fixture.Services, Factory(fixture.Services, transactionProbe, revoke));
        var service = ContinuationService(fixture.Services, failingOwner, access);
        var context = await fixture.Services.GetRequiredService<StoragePlacementRecoveryService>().GetCurrentContextAsync();
        var current = await service.GetAsync(new(context, asset.Storage.IntentId));
        fixture.Services.GetRequiredService<ContinuationDriverProbe>().Armed = true;
        Assert.Equal(StoragePlacementRecoveryFailure.Denied,
            (await Assert.ThrowsAsync<StoragePlacementRecoveryException>(() => service.ReconcileAsync(Command(current)))).Failure);
        Assert.True(revoke.SawUncommittedNativeReceipt);
        Assert.True(transactionProbe.SawSameStorageAndNativeTransaction);
        Assert.Null(await fixture.Owner.FindWorkflowContributionAsync(asset.Plan.Identity));
        Assert.Null((await OutputStore(fixture.Services).FindAsync(asset.Plan.Identity))!.Receipt);
        Assert.Equal(asset.Storage, (await fixture.Services.GetRequiredService<StorageStablePlacementService>().FindAsync(asset.Storage.IntentId))!.Receipt);
        Assert.Equal(0, fixture.Services.GetRequiredService<ContinuationDriverProbe>().Attempts);
    }

    [Theory]
    [InlineData(ContinuationChange.Action)]
    [InlineData(ContinuationChange.Fingerprint)]
    [InlineData(ContinuationChange.Profile)]
    [InlineData(ContinuationChange.StorageDeleted)]
    [InlineData(ContinuationChange.StorageMissing)]
    [InlineData(ContinuationChange.ProjectRecreated)]
    [InlineData(ContinuationChange.Imported)]
    [InlineData(ContinuationChange.CancelledRun)]
    public async Task Storage_operator_continuation_denies_changed_intent_or_retained_owner_without_dispatch(ContinuationChange change) {
        await using var app = await TestApplication.CreateAsync(new() { ConfigureServices = ConfigureContinuation });
        await using var fixture = await CreateFixtureAsync(app, prepareOutput: false);
        var asset = await PrepareContinuationAssetAsync(fixture, false);
        var recovery = fixture.Services.GetRequiredService<StoragePlacementRecoveryService>();
        var service = fixture.Services.GetRequiredService<IStoragePlacementOwnerContinuation>();
        var request = new StoragePlacementRecoveryCommand(await recovery.GetCurrentContextAsync(), asset.Storage.IntentId);
        var current = await service.GetAsync(request);
        var command = Command(current);
        if (change == ContinuationChange.Action) {
            command = command with { Action = StoragePlacementOwnerContinuationAction.RecordWorkflowAssetReceipt };
        } else if (change == ContinuationChange.Fingerprint) {
            command = command with { ExpectedIntent = command.ExpectedIntent with { Fingerprint = new string('e', 64) } };
        } else if (change == ContinuationChange.Profile) {
            command = command with { Context = command.Context with { DatabaseProfileId = Guid.NewGuid() } };
        } else if (change is ContinuationChange.StorageDeleted or ContinuationChange.StorageMissing) {
            await using var storage = await fixture.Services.GetRequiredService<IDbContextFactory<StorageDbContext>>().CreateDbContextAsync();
            var row = await storage.Set<StoragePlacementIntentRecord>().SingleAsync(item => item.Id == asset.Storage.IntentId.Value);
            if (change == ContinuationChange.StorageDeleted) {
                row.DeletionRequested = true;
            } else {
                storage.Remove(row);
            }
            await storage.SaveChangesAsync();
        } else if (change == ContinuationChange.ProjectRecreated) {
            await RecreateProjectAsync(fixture);
        } else if (change == ContinuationChange.Imported) {
            await using var owner = await fixture.Factory.CreateDbContextAsync();
            var row = await owner.Set<ProjectWorkflowContributionRecord>().SingleAsync(item => item.RunId == asset.Plan.Identity.Occurrence.RunId.Value);
            row.ImportedHistory = new(Guid.NewGuid(), Guid.NewGuid());
            await owner.SaveChangesAsync();
        } else {
            var runs = fixture.Services.GetRequiredService<IWorkflowRunStore>();
            var run = (await runs.GetRunAsync(asset.Plan.Identity.Occurrence.RunId))!;
            await runs.SaveRunAsync(run with { State = WorkflowRunState.Cancelled });
            Assert.Equal(StoragePlacementOwnerContinuationState.WorkflowRunCancelled, (await service.GetAsync(request)).State);
        }
        var drivers = fixture.Services.GetRequiredService<ContinuationDriverProbe>();
        drivers.Armed = true;
        await Assert.ThrowsAsync<StoragePlacementRecoveryException>(() => service.ReconcileWorkflowAssetAsync(command));
        Assert.Null(await fixture.Owner.FindWorkflowContributionAsync(asset.Plan.Identity));
        Assert.Null((await OutputStore(fixture.Services).FindAsync(asset.Plan.Identity))!.Receipt);
        Assert.Equal(0, drivers.Attempts);
    }

    [Fact]
    public async Task Storage_operator_manifest_failure_retains_native_receipt_and_offers_only_acknowledgement() {
        await using var app = await TestApplication.CreateAsync(new() { ConfigureServices = ConfigureContinuation });
        await using var fixture = await CreateFixtureAsync(app, prepareOutput: false);
        var asset = await PrepareContinuationAssetAsync(fixture, false);
        var failedManifest = new ContinuationManifestFailure(OutputStore(fixture.Services));
        var service = ContinuationService(fixture.Services, fixture.Owner,
            fixture.Services.GetRequiredService<IStoragePlacementRecoveryAccess>(), failedManifest);
        var context = await fixture.Services.GetRequiredService<StoragePlacementRecoveryService>().GetCurrentContextAsync();
        var current = await service.GetAsync(new(context, asset.Storage.IntentId));
        fixture.Services.GetRequiredService<ContinuationDriverProbe>().Armed = true;
        var pending = await service.ReconcileAsync(Command(current));
        Assert.Equal(StoragePlacementOwnerContinuationState.WorkflowManifestPending, pending.State);
        Assert.Equal(StoragePlacementOwnerContinuationAction.RecordWorkflowAssetReceipt, pending.AvailableAction);
        var native = (await fixture.Owner.FindWorkflowContributionAsync(asset.Plan.Identity))!;
        Assert.Null((await OutputStore(fixture.Services).FindAsync(asset.Plan.Identity))!.Receipt);
        await using var independent = app.Services.CreateAsyncScope();
        var restarted = independent.ServiceProvider.GetRequiredService<IStoragePlacementOwnerContinuation>();
        var observed = await restarted.GetAsync(new(context, asset.Storage.IntentId));
        Assert.Equal(StoragePlacementOwnerContinuationAction.RecordWorkflowAssetReceipt, observed.AvailableAction);
        var complete = await restarted.ReconcileWorkflowAssetAsync(Command(observed));
        Assert.Equal(StoragePlacementOwnerContinuationState.ReceiptRecorded, complete.State);
        Assert.Equal(native.Receipt, (await OutputStore(fixture.Services).FindAsync(asset.Plan.Identity))!.Receipt);
        Assert.Equal(0, fixture.Services.GetRequiredService<ContinuationDriverProbe>().Attempts);
    }

    public enum ContinuationChange { Action, Fingerprint, Profile, StorageDeleted, StorageMissing, ProjectRecreated, Imported, CancelledRun }

    private static void ConfigureContinuation(IServiceCollection services) {
        RemoveAutomaticDelivery(services);
        StorageRecoveryTestServices.Add(services, true);
        services.AddSingleton<ContinuationDriverProbe>();
        services.AddSingleton<IStorageDriverRegistry>(provider => provider.GetRequiredService<ContinuationDriverProbe>());
    }

    private static async Task<(WorkflowStructureOutputPlan Plan, StorageStablePlacementReceipt Storage)> PrepareContinuationAssetAsync(
        Fixture fixture, bool nativeCommitted) {
        var request = fixture.Request with { ObjectType = ProjectObjectType.File, ObjectSubtype = "json",
            Media = new("proof.json", "application/json", Convert.ToBase64String("{\"original\":true}"u8.ToArray())) };
        var plan = fixture.Plan with { Kind = WorkflowStructureOutputKind.Asset };
        plan = plan with { Fingerprint = ProjectWorkflowContributionFingerprint.Create(plan, request) };
        request = BindRequest(request, plan, fixture.Request.WorkflowMutationAdmission!.Authority);
        var output = await OutputStore(fixture.Services).PrepareAsync(plan);
        var fault = new CommitFault(nativeCommitted);
        var failing = Owner(fixture.Services, Factory(fixture.Services, new ObserveReceiptCommands(fault), fault));
        Assert.Same(fault.Failure, await Assert.ThrowsAsync<ArgumentException>(() => failing.CreateWorkflowContributionAsync(plan, request,
            storagePlacementIntentId: output.StoragePlacementIntentId)));
        Assert.True(fault.SawReceiptInsert);
        var stored = await fixture.Services.GetRequiredService<StorageStablePlacementService>().FindAsync(new(output.StoragePlacementIntentId!.Value));
        Assert.Equal(StorageStablePlacementState.Completed, stored!.State);
        return (plan, Assert.IsType<StorageStablePlacementReceipt>(stored.Receipt));
    }

    private static StoragePlacementWorkflowContinuationCommand Command(StoragePlacementOwnerContinuationObservation observation)
        => new(observation.Storage.Context, observation.Storage.Identity.IntentId, observation.AvailableAction,
            observation.WorkflowIntent ?? throw new InvalidOperationException("The fixture has no original Workflow intent."));

    private static async Task<string> RetainedWorkflowCommandAsync(IServiceProvider services, WorkflowStructureOutputPlan plan) {
        await using var context = await services.GetRequiredService<IDbContextFactory<WorkbenchDbContext>>().CreateDbContextAsync();
        var row = await context.Set<ProjectWorkflowContributionRecord>().SingleAsync(item => item.RunId == plan.Identity.Occurrence.RunId.Value);
        return JsonSerializer.Serialize(new { row.PlanJson, row.RequestJson, row.StoragePlacementIntentId, row.PreparedAtUtc,
            row.DatabaseProfileId, row.ProjectId, row.ProjectLifetimeId });
    }

    private static ProjectWorkflowAssetContinuationService ContinuationService(IServiceProvider services, ProjectWorkbenchService owner,
        IStoragePlacementRecoveryAccess access, IWorkflowStructureOutputStore? outputs = null) {
        var gateway = Microsoft.Extensions.DependencyInjection.ActivatorUtilities.CreateInstance<WorkbenchProjectStructureRuntimeGateway>(services,
            owner, outputs ?? services.GetRequiredService<IWorkflowStructureOutputStore>());
        return new(services.GetRequiredService<StoragePlacementRecoveryService>(), access, gateway,
            services.GetRequiredService<IDatabaseRuntimeState>(), services.GetRequiredService<IDatabaseRuntimeWriteFence>());
    }

    private static DbContextOptions<StorageDbContext> StorageOptions(IServiceProvider services, params IInterceptor[] interceptors) {
        var options = new DbContextOptionsBuilder<StorageDbContext>();
        AppDbContextOptionsConfigurator.Configure(options, services.GetRequiredService<ICanonicalRuntimeDatabase>().Profile);
        options.AddInterceptors(interceptors);
        return options.Options;
    }

    private sealed class ContinuationDriverProbe(IEnumerable<IStorageDriver> drivers) : IStorageDriverRegistry {
        private readonly StorageDriverRegistry inner = new(drivers);
        internal bool Armed { get; set; }
        internal int Attempts { get; private set; }
        public IReadOnlyCollection<StorageProviderKind> RegisteredKinds => inner.RegisteredKinds;
        public bool TryResolve(StorageProviderKind providerKind, out IStorageDriver driver) {
            RequireNoDispatch();
            return inner.TryResolve(providerKind, out driver);
        }
        public IStorageDriver Resolve(StorageProviderKind providerKind) {
            RequireNoDispatch();
            return inner.Resolve(providerKind);
        }
        private void RequireNoDispatch() {
            if (Armed) {
                Attempts++;
                throw new InvalidOperationException("An operator continuation must not resolve a provider or retry external bytes.");
            }
        }
    }

    private sealed class ContinuationAfterFlush(Action revoke) : SaveChangesInterceptor {
        internal bool SawUncommittedNativeReceipt { get; private set; }
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result,
            CancellationToken cancellationToken = default) {
            if (!SawUncommittedNativeReceipt && eventData.Context is WorkbenchDbContext owner &&
                    owner.ChangeTracker.Entries<ProjectWorkflowContributionRecord>().Any(entry => entry.Entity.ReceiptJson != "")) {
                Assert.NotNull(owner.Database.CurrentTransaction);
                SawUncommittedNativeReceipt = true;
                revoke();
            }
            return ValueTask.FromResult(result);
        }
    }

    private sealed class ContinuationTransactionProbe : DbCommandInterceptor {
        private DbConnection? storageConnection;
        private DbTransaction? storageTransaction;
        internal bool SawSameStorageAndNativeTransaction { get; private set; }
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default) {
            if (command.CommandText.Contains("Storage_PlacementIntents", StringComparison.Ordinal) &&
                    command.CommandText.Contains("FOR SHARE", StringComparison.Ordinal) && command.Transaction is not null) {
                storageConnection = command.Connection;
                storageTransaction = command.Transaction;
                Assert.Equal(IsolationLevel.Serializable, storageTransaction.IsolationLevel);
            }
            if (command.CommandText.Contains("INSERT INTO \"Workbench_ProjectObjects\"", StringComparison.Ordinal)) {
                Assert.NotNull(storageTransaction);
                Assert.Same(storageConnection, command.Connection);
                Assert.Same(storageTransaction, command.Transaction);
                SawSameStorageAndNativeTransaction = true;
            }
            return ValueTask.FromResult(result);
        }
    }

    private sealed class ContinuationAccess(IStoragePlacementRecoveryAccess inner) : IStoragePlacementRecoveryAccess {
        internal bool Denied { get; set; }
        private void RequireCurrent() {
            if (Denied) {
                throw new StoragePlacementRecoveryException(StoragePlacementRecoveryFailure.Denied);
            }
        }
        public Task<StoragePlacementRecoveryAuthorization> AuthorizeAsync(StoragePlacementRecoveryOperation operation, CancellationToken cancellationToken) {
            RequireCurrent();
            return inner.AuthorizeAsync(operation, cancellationToken);
        }
        public Task EnsureCurrentAsync(StoragePlacementRecoveryAuthorization authorization, CancellationToken cancellationToken) {
            RequireCurrent();
            return inner.EnsureCurrentAsync(authorization, cancellationToken);
        }
        public Task RequireOriginalOwnerForMutationAsync(StoragePlacementRecoveryAuthorization authorization,
            StoragePlacementRecoveryIdentity identity, CancellationToken cancellationToken) {
            RequireCurrent();
            return inner.RequireOriginalOwnerForMutationAsync(authorization, identity, cancellationToken);
        }
        public Task<IReadOnlyDictionary<Guid, StoragePlacementRecoveryOwnerObservation>> InspectOwnersAsync(StoragePlacementRecoveryAuthorization authorization,
            IReadOnlyCollection<StoragePlacementRecoveryIdentity> identities, CancellationToken cancellationToken)
            => inner.InspectOwnersAsync(authorization, identities, cancellationToken);
        public Task<StoragePlacementContinuationScan> ListContinuationsAsync(StoragePlacementRecoveryAuthorization authorization,
            Guid? projectId, int take, int offset, CancellationToken cancellationToken)
            => inner.ListContinuationsAsync(authorization, projectId, take, offset, cancellationToken);
    }

    private sealed class ContinuationManifestFailure(IWorkflowStructureOutputStore inner) : IWorkflowStructureOutputStore {
        public Task<WorkflowProjectLifetime?> FindProjectLifetimeAsync(WorkflowRunId runId, Guid projectId, CancellationToken cancellationToken = default)
            => inner.FindProjectLifetimeAsync(runId, projectId, cancellationToken);
        public Task RequireForMutationAsync(WorkflowStructureOutputPlan plan, CancellationToken cancellationToken = default)
            => inner.RequireForMutationAsync(plan, cancellationToken);
        public Task<WorkflowStructureOutput?> FindAsync(WorkflowStructureOutputIdentity identity, CancellationToken cancellationToken = default)
            => inner.FindAsync(identity, cancellationToken);
        public Task<WorkflowStructureOutput> PrepareAsync(WorkflowStructureOutputPlan plan, CancellationToken cancellationToken = default)
            => inner.PrepareAsync(plan, cancellationToken);
        public Task CompleteAsync(WorkflowStructureOutputReceipt receipt, CancellationToken cancellationToken = default)
            => throw new IOException("Injected manifest acknowledgement failure after native commit.");
        public Task<IReadOnlyList<WorkflowStructureOutput>> ListAsync(WorkflowRunId runId, CancellationToken cancellationToken = default)
            => inner.ListAsync(runId, cancellationToken);
        public Task<IReadOnlyList<WorkflowStructureOutput>> ListPendingAsync(int take, CancellationToken cancellationToken = default)
            => inner.ListPendingAsync(take, cancellationToken);
        public Task DeferInspectionAsync(WorkflowStructureOutputIdentity identity, CancellationToken cancellationToken = default)
            => inner.DeferInspectionAsync(identity, cancellationToken);
        public Task<bool> TryBeginAssetDispatchAsync(WorkflowStructureOutputIdentity identity, CancellationToken cancellationToken = default)
            => inner.TryBeginAssetDispatchAsync(identity, cancellationToken);
    }
}
