using System.Data;
using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Tests.Support;
using CanDoItAll.Web.Api;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration.Processes;

public sealed partial class ProcessCatalogAuthorityPersistenceTests {
    [Fact]
    public async Task Workflow_storage_recovery_preserves_original_lifetime_across_restart_without_native_continuation() {
        await using var environment = CanDoItAllTestEnvironment.Create("workflow-storage-lifetime");
        var profile = environment.CreatePostgreSqlProfile("original");
        var clock = new NativeClock();
        var driver = PendingStorageDriver();
        var configured = StorageRecoveryHarness(clock, driver);
        var options = new TestHarnessOptions { TestEnvironment = environment, ActiveProfile = profile,
            ConfigureServices = configured.ConfigureServices };
        StoragePlacementRecoveryItem completed;
        string originalJournal;
        Guid executionRunId;
        await using (var first = await TestApplication.CreateAsync(options)) {
            await using var scope = first.Services.CreateAsyncScope();
            var services = scope.ServiceProvider;
            var prepared = await PrepareWorkflowRecoveryAsync(services, clock);
            executionRunId = prepared.Test.Run.Id;
            var store = services.GetRequiredService<ISandboxWorkspaceExecutionRunStore>();
            originalJournal = JsonSerializer.Serialize((await store.GetExecutionRunAsync(executionRunId))!.ToolAdmission);
            var recovery = services.GetRequiredService<StoragePlacementRecoveryService>();
            var request = new StoragePlacementRecoveryCommand(await recovery.GetCurrentContextAsync(), prepared.Prepared.Plan.StorageIntentId);
            var pending = await recovery.GetAsync(request);
            Assert.Equal(StoragePlacementRecoveryBlock.None, pending.Block);
            Assert.Equal(StoragePlacementRecoveryAction.Reconcile, pending.AvailableAction);
            AssertSafeStorageProjection(pending, first.RootPath);
            driver.ReadFailure = null;
            completed = await recovery.ReconcileAsync(request);
            Assert.Equal(StorageStablePlacementState.Completed, completed.StorageState);
            Assert.True(completed.StorageReceiptPresent);
            Assert.False(completed.NativeReceiptPresent);
            Assert.Equal(StoragePlacementRecoveryAction.None, completed.AvailableAction);
            Assert.Equal(originalJournal, JsonSerializer.Serialize((await store.GetExecutionRunAsync(executionRunId))!.ToolAdmission));
            await AssertWorkflowRecoveryRowsAsync(services, prepared.Prepared);
        }
        await using var restarted = await TestApplication.CreateAsync(options);
        await using var restartedScope = restarted.Services.CreateAsyncScope();
        var restartedServices = restartedScope.ServiceProvider;
        var owner = restartedServices.GetRequiredService<StoragePlacementRecoveryService>();
        var context = await owner.GetCurrentContextAsync();
        var item = Assert.Single((await owner.ListPendingContinuationsAsync(new(context))).Items);
        Assert.Equal(completed with { Context = context }, item.Storage);
        Assert.Equal(StoragePlacementContinuationPhase.NativeCommit, item.Phase);
        var continuation = await restartedServices.GetRequiredService<IStoragePlacementOwnerContinuation>()
            .ReconcileCancelledRunReceiptsAsync(new(context, completed.Identity.IntentId));
        Assert.Equal(StoragePlacementOwnerContinuationState.OriginalProposalUnavailable, continuation.State);
        Assert.Equal(StoragePlacementOwnerContinuationAction.None, continuation.AvailableAction);
        Assert.Equal(originalJournal, JsonSerializer.Serialize((await restartedServices.GetRequiredService<ISandboxWorkspaceExecutionRunStore>()
            .GetExecutionRunAsync(executionRunId))!.ToolAdmission));
        Assert.Equal(1, driver.StableWrites);
    }

    [Theory]
    [InlineData(WorkflowRecoveryChange.Legacy, StoragePlacementRecoveryBlock.OriginalProjectLifetimeMissing)]
    [InlineData(WorkflowRecoveryChange.PartialTuple, StoragePlacementRecoveryBlock.InvalidOwnerAssociation)]
    [InlineData(WorkflowRecoveryChange.ForeignProfile, StoragePlacementRecoveryBlock.OriginalProjectUnavailable)]
    [InlineData(WorkflowRecoveryChange.PlanLifetimeMismatch, StoragePlacementRecoveryBlock.InvalidOwnerAssociation)]
    [InlineData(WorkflowRecoveryChange.MalformedPlan, StoragePlacementRecoveryBlock.InvalidOwnerAssociation)]
    [InlineData(WorkflowRecoveryChange.Imported, StoragePlacementRecoveryBlock.ImportedHistory)]
    [InlineData(WorkflowRecoveryChange.RecreatedProject, StoragePlacementRecoveryBlock.OriginalProjectUnavailable)]
    public async Task Workflow_storage_recovery_cannot_replace_missing_or_changed_original_lineage(WorkflowRecoveryChange change,
        StoragePlacementRecoveryBlock expected) {
        var clock = new NativeClock();
        var driver = PendingStorageDriver();
        await using var app = await TestApplication.CreateAsync(StorageRecoveryHarness(clock, driver));
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var prepared = await PrepareWorkflowRecoveryAsync(services, clock);
        if (change == WorkflowRecoveryChange.RecreatedProject) {
            await ReplaceRecoveryProjectLifetimeAsync(services, prepared.Prepared.Plan.ProjectAdmission);
        } else {
            await using var database = await services.GetRequiredService<IDbContextFactory<WorkbenchDbContext>>().CreateDbContextAsync();
            var row = await database.Set<ProjectWorkflowContributionRecord>().SingleAsync();
            switch (change) {
                case WorkflowRecoveryChange.Legacy:
                    row.DatabaseProfileId = null;
                    row.ProjectLifetimeId = null;
                    row.PlanJson = "Legacy private payload is not a new admission.";
                    break;
                case WorkflowRecoveryChange.PartialTuple:
                    row.ProjectLifetimeId = null;
                    break;
                case WorkflowRecoveryChange.ForeignProfile:
                    row.DatabaseProfileId = Guid.NewGuid();
                    row.PlanJson = JsonSerializer.Serialize(prepared.Plan with {
                        ProjectLifetime = new(row.DatabaseProfileId.Value, row.ProjectId, row.ProjectLifetimeId!.Value)
                    }, WorkflowRecoveryJson);
                    break;
                case WorkflowRecoveryChange.PlanLifetimeMismatch:
                    row.PlanJson = JsonSerializer.Serialize(prepared.Plan with {
                        ProjectLifetime = new(row.DatabaseProfileId!.Value, row.ProjectId, Guid.NewGuid())
                    }, WorkflowRecoveryJson);
                    break;
                case WorkflowRecoveryChange.MalformedPlan:
                    row.PlanJson = "Private malformed payload";
                    break;
                case WorkflowRecoveryChange.Imported:
                    row.ImportedHistory = new(Guid.NewGuid(), Guid.NewGuid());
                    row.PlanJson = "Imported private payload must not be parsed as current authority.";
                    break;
            }
            await database.SaveChangesAsync();
        }
        driver.ReadFailure = null;
        await using var restartedScope = app.Services.CreateAsyncScope();
        var recovery = restartedScope.ServiceProvider.GetRequiredService<StoragePlacementRecoveryService>();
        var request = new StoragePlacementRecoveryCommand(await recovery.GetCurrentContextAsync(), prepared.Prepared.Plan.StorageIntentId);
        var item = await recovery.GetAsync(request);
        Assert.Equal(expected, item.Block);
        Assert.Equal(StoragePlacementRecoveryAction.None, item.AvailableAction);
        Assert.Equal(StoragePlacementRecoveryFailure.Blocked,
            (await Assert.ThrowsAsync<StoragePlacementRecoveryException>(() => recovery.ReconcileAsync(request))).Failure);
        AssertSafeStorageProjection(item, app.RootPath);
        Assert.Equal(1, driver.StableWrites);
        await AssertWorkflowRecoveryRowsAsync(services, prepared.Prepared);
    }

    [Fact]
    public async Task Workflow_storage_final_save_rechecks_original_project_after_provider_readback() {
        var clock = new NativeClock();
        var driver = PendingStorageDriver();
        var waiter = new RecoveryFinalSaveWaiter();
        var configured = StorageRecoveryHarness(clock, driver);
        await using var app = await TestApplication.CreateAsync(new() { ConfigureServices = services => {
            configured.ConfigureServices!(services);
            services.AddSingleton<IDbContextFactory<StorageDbContext>>(provider => new Factory<StorageDbContext>(
                NativeOptions<StorageDbContext>(provider, waiter), static options => new(options)));
        } });
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var prepared = await PrepareWorkflowRecoveryAsync(services, clock);
        driver.ReadFailure = null;
        waiter.Armed = true;
        var recovery = services.GetRequiredService<StoragePlacementRecoveryService>();
        var request = new StoragePlacementRecoveryCommand(await recovery.GetCurrentContextAsync(), prepared.Prepared.Plan.StorageIntentId);
        var action = recovery.ReconcileAsync(request);
        await waiter.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        try {
            await ReplaceRecoveryProjectLifetimeAsync(services, prepared.Prepared.Plan.ProjectAdmission);
        } finally {
            waiter.Release.TrySetResult();
        }
        Assert.Equal(StoragePlacementRecoveryFailure.Blocked,
            (await Assert.ThrowsAsync<StoragePlacementRecoveryException>(() => action)).Failure);
        var retained = await recovery.GetAsync(request);
        Assert.Equal(StorageStablePlacementState.Uncertain, retained.StorageState);
        Assert.False(retained.StorageReceiptPresent);
        Assert.Equal(StoragePlacementRecoveryBlock.OriginalProjectUnavailable, retained.Block);
        Assert.Equal(1, driver.StableWrites);
        await AssertWorkflowRecoveryRowsAsync(services, prepared.Prepared);
    }

    [Fact]
    public async Task Workflow_storage_final_owner_association_uses_the_exact_storage_transaction_and_row_lock() {
        var clock = new NativeClock();
        var driver = PendingStorageDriver();
        var probe = new WorkflowRecoveryLockProbe();
        var configured = StorageRecoveryHarness(clock, driver);
        await using var app = await TestApplication.CreateAsync(new() { ConfigureServices = services => {
            configured.ConfigureServices!(services);
            services.AddSingleton<IDbContextFactory<StorageDbContext>>(provider => new Factory<StorageDbContext>(
                NativeOptions<StorageDbContext>(provider, probe), static options => new(options)));
            services.AddScoped(provider => new ProjectStoragePlacementRecoveryQuery(
                provider.GetRequiredService<IDbContextFactory<WorkbenchDbContext>>(), NativeOptions<WorkbenchDbContext>(provider, probe),
                provider.GetRequiredService<CoordinatedDatabaseTransaction>()));
        } });
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var prepared = await PrepareWorkflowRecoveryAsync(services, clock);
        probe.Armed = true;
        driver.ReadFailure = null;
        var recovery = services.GetRequiredService<StoragePlacementRecoveryService>();
        var result = await recovery.ReconcileAsync(new(await recovery.GetCurrentContextAsync(), prepared.Prepared.Plan.StorageIntentId));
        Assert.Equal(StorageStablePlacementState.Completed, result.StorageState);
        Assert.True(probe.SawWorkflowLock);
        Assert.Equal(1, driver.StableWrites);
        await AssertWorkflowRecoveryRowsAsync(services, prepared.Prepared);
    }

    [Theory]
    [InlineData(WorkflowRecoveryCheckpoint.Pending, StoragePlacementContinuationPhase.WorkflowAcknowledgement)]
    [InlineData(WorkflowRecoveryCheckpoint.Completed, null)]
    [InlineData(WorkflowRecoveryCheckpoint.ForeignProfile, StoragePlacementContinuationPhase.OwnerEvidenceUnavailable)]
    [InlineData(WorkflowRecoveryCheckpoint.ForeignProject, StoragePlacementContinuationPhase.OwnerEvidenceUnavailable)]
    [InlineData(WorkflowRecoveryCheckpoint.ForeignLifetime, StoragePlacementContinuationPhase.OwnerEvidenceUnavailable)]
    [InlineData(WorkflowRecoveryCheckpoint.LegacyManifest, StoragePlacementContinuationPhase.OwnerEvidenceUnavailable)]
    public async Task Workflow_recovery_only_credits_a_manifest_with_the_exact_original_tuple(WorkflowRecoveryCheckpoint checkpoint,
        StoragePlacementContinuationPhase? expected) {
        var clock = new NativeClock();
        var driver = PendingStorageDriver();
        await using var app = await TestApplication.CreateAsync(StorageRecoveryHarness(clock, driver));
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var prepared = await PrepareWorkflowRecoveryAsync(services, clock);
        driver.ReadFailure = null;
        var recovery = services.GetRequiredService<StoragePlacementRecoveryService>();
        var context = await recovery.GetCurrentContextAsync();
        await recovery.ReconcileAsync(new(context, prepared.Prepared.Plan.StorageIntentId));
        await using (var database = await services.GetRequiredService<IDbContextFactory<WorkbenchDbContext>>().CreateDbContextAsync()) {
            var row = await database.Set<ProjectWorkflowContributionRecord>().SingleAsync();
            row.NativeObjectId = Guid.NewGuid();
            row.ReceiptJson = "Private native receipt is not returned by recovery.";
            await database.SaveChangesAsync();
        }
        var original = prepared.Plan.ProjectLifetime!;
        var output = new WorkflowStructureOutputRecord {
            RunId = prepared.Plan.Identity.Occurrence.RunId.Value, OccurrencePath = prepared.Plan.Identity.Occurrence.Path,
            Slot = prepared.Plan.Identity.Slot, StoragePlacementIntentId = prepared.Prepared.Plan.StorageIntentId.Value,
            DatabaseProfileId = original.DatabaseProfileId, ProjectId = original.ProjectId, ProjectLifetimeId = original.LifetimeId,
            PlanJson = JsonSerializer.Serialize(prepared.Plan, WorkflowRecoveryJson), IsComplete = checkpoint != WorkflowRecoveryCheckpoint.Pending,
            ReceiptJson = checkpoint == WorkflowRecoveryCheckpoint.Pending ? "" : "Private Workflow acknowledgement", NextInspectionAtUtc = clock.Now
        };
        switch (checkpoint) {
            case WorkflowRecoveryCheckpoint.ForeignProfile:
                output.DatabaseProfileId = Guid.NewGuid();
                break;
            case WorkflowRecoveryCheckpoint.ForeignProject:
                output.ProjectId = Guid.NewGuid();
                break;
            case WorkflowRecoveryCheckpoint.ForeignLifetime:
                output.ProjectLifetimeId = Guid.NewGuid();
                break;
            case WorkflowRecoveryCheckpoint.LegacyManifest:
                output.DatabaseProfileId = null;
                output.ProjectId = null;
                output.ProjectLifetimeId = null;
                break;
        }
        await using (var database = await services.GetRequiredService<IDbContextFactory<WorkflowDbContext>>().CreateDbContextAsync()) {
            database.Add(output);
            await database.SaveChangesAsync();
        }
        await using var restartedScope = app.Services.CreateAsyncScope();
        var page = await restartedScope.ServiceProvider.GetRequiredService<StoragePlacementRecoveryService>()
            .ListPendingContinuationsAsync(new(context));
        if (expected is { } phase) {
            var item = Assert.Single(page.Items);
            Assert.Equal(phase, item.Phase);
            Assert.Equal(StoragePlacementRecoveryBlock.None, item.Storage.Block);
            Assert.Equal(StoragePlacementRecoveryAction.None, item.Storage.AvailableAction);
            Assert.True(item.Storage.NativeReceiptPresent);
            AssertSafeStorageProjection(item.Storage, app.RootPath);
        } else {
            Assert.Empty(page.Items);
        }
        Assert.Equal(1, driver.StableWrites);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Workflow_storage_HTTP_preserves_current_Storage_and_project_authorization(bool canReconcile) {
        var clock = new NativeClock();
        var driver = PendingStorageDriver();
        await using var host = await CreateStorageRecoveryHttpHostAsync(clock, driver);
        await using var scope = host.App.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var prepared = await PrepareWorkflowRecoveryAsync(services, clock);
        await SetStorageRecoveryTokenAsync(host, canReconcile ? StorageRecoveryTestServices.ReconcileScopes : StorageRecoveryTestServices.ReadScopes);
        var canonical = host.App.Services.GetRequiredService<ICanonicalRuntimeDatabase>();
        var context = new StoragePlacementRecoveryContext(canonical.Profile.Profile.Id, canonical.Generation);
        using var read = await host.Client.GetAsync(StorageRecoveryUrl(context, prepared.Prepared.Plan.StorageIntentId));
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        var item = (await read.Content.ReadFromJsonAsync<StoragePlacementRecoveryItem>())!;
        Assert.Equal(StoragePlacementRecoveryOwner.WorkflowAsset, item.Owner);
        Assert.Equal(canReconcile ? StoragePlacementRecoveryAction.Reconcile : StoragePlacementRecoveryAction.None, item.AvailableAction);
        AssertSafeStorageProjection(item, host.RootPath);
        driver.ReadFailure = null;
        using var response = await host.Client.PostAsJsonAsync(StoragePlacementRecoveryEndpoints.Route + "/reconcile",
            new StoragePlacementRecoveryCommand(context, prepared.Prepared.Plan.StorageIntentId));
        Assert.Equal(canReconcile ? HttpStatusCode.OK : HttpStatusCode.Forbidden, response.StatusCode);
        Assert.DoesNotContain("Private", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.Equal(1, driver.StableWrites);
        await AssertWorkflowRecoveryRowsAsync(services, prepared.Prepared);
    }

    private static readonly JsonSerializerOptions WorkflowRecoveryJson = new(JsonSerializerDefaults.Web);

    private static async Task AssertWorkflowRecoveryRowsAsync(IServiceProvider services, ProjectProcessAssetSnapshot original) {
        await using var database = await services.GetRequiredService<IDbContextFactory<WorkbenchDbContext>>().CreateDbContextAsync();
        var row = await database.Set<ProjectWorkflowContributionRecord>().SingleAsync(item => item.StoragePlacementIntentId == original.Plan.StorageIntentId.Value);
        Assert.Equal(Guid.Empty, row.NativeObjectId);
        Assert.Empty(row.ReceiptJson);
        Assert.False(await database.Set<ProjectObjectRecord>().AnyAsync(item => item.Id == original.Plan.NativeObjectId));
        Assert.False(await database.Set<ProjectProcessAssetContributionRecord>().AnyAsync(item => item.IntentId == original.Plan.Producer.IntentId.Value));
    }

    private static async Task<(AssetCase Test, ProjectProcessAssetSnapshot Prepared, WorkflowStructureOutputPlan Plan)> PrepareWorkflowRecoveryAsync(
        IServiceProvider services, NativeClock clock) {
        var pending = await PreparePendingStorageAsync(services, clock);
        var original = pending.Prepared.Plan.ProjectAdmission;
        var plan = new WorkflowStructureOutputPlan(new(WorkflowExecutionOccurrence.Start(WorkflowRunId.New()), 0),
            WorkflowVersionId.New(), new("effect"), original.ProjectId, new($"project:{original.ProjectId:D}"), new string('a', 64),
            WorkflowStructureOutputKind.Asset, WorkflowStructureOutputRole.RequiredResult, new string('b', 64)) {
            ProjectLifetime = new(original.DatabaseProfileId, original.ProjectId, original.LifetimeId),
            SourceAuthorityFingerprint = new string('c', 64)
        };
        await using var database = await services.GetRequiredService<IDbContextFactory<WorkbenchDbContext>>().CreateDbContextAsync();
        database.Remove(await database.Set<ProjectProcessAssetContributionRecord>().SingleAsync(row => row.IntentId == pending.Prepared.Plan.Producer.IntentId.Value));
        database.Add(new ProjectWorkflowContributionRecord {
            RunId = plan.Identity.Occurrence.RunId.Value, OccurrencePath = plan.Identity.Occurrence.Path, Slot = plan.Identity.Slot,
            DatabaseProfileId = original.DatabaseProfileId, ProjectId = original.ProjectId, ProjectLifetimeId = original.LifetimeId,
            StoragePlacementIntentId = pending.Prepared.Plan.StorageIntentId.Value, PreparedAtUtc = clock.Now,
            PlanJson = JsonSerializer.Serialize(plan, WorkflowRecoveryJson), RequestJson = "Private native request is never returned by recovery."
        });
        await database.SaveChangesAsync();
        return (pending.Test, pending.Prepared, plan);
    }

    public enum WorkflowRecoveryChange { Legacy, PartialTuple, ForeignProfile, PlanLifetimeMismatch, MalformedPlan, Imported, RecreatedProject }
    public enum WorkflowRecoveryCheckpoint { Pending, Completed, ForeignProfile, ForeignProject, ForeignLifetime, LegacyManifest }

    private sealed class WorkflowRecoveryLockProbe : DbCommandInterceptor {
        internal bool Armed { get; set; }
        internal bool SawWorkflowLock { get; private set; }
        private DbConnection? storageConnection;
        private DbTransaction? storageTransaction;

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default) {
            if (!Armed || !command.CommandText.Contains("FOR SHARE", StringComparison.Ordinal)) {
                return ValueTask.FromResult(result);
            }
            if (command.CommandText.Contains("Storage_Catalog", StringComparison.Ordinal)) {
                storageConnection = command.Connection;
                storageTransaction = command.Transaction;
            }
            if (command.CommandText.Contains("Workbench_WorkflowContributionReceipts", StringComparison.Ordinal)) {
                Assert.NotNull(storageTransaction);
                Assert.Same(storageConnection, command.Connection);
                Assert.Same(storageTransaction, command.Transaction);
                Assert.Equal(IsolationLevel.Serializable, command.Transaction!.IsolationLevel);
                SawWorkflowLock = true;
            }
            return ValueTask.FromResult(result);
        }
    }
}
