using System.Data.Common;
using System.Text.Json;
using System.Text.Json.Nodes;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration.Processes;

public sealed partial class ProcessCatalogAuthorityPersistenceTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Storage_operator_reconciliation_preserves_pending_native_effect_and_expired_or_revoked_source(bool revokeSource) {
        var clock = new NativeClock();
        var driver = PendingStorageDriver();
        await using var app = await TestApplication.CreateAsync(StorageRecoveryHarness(clock, driver));
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var pending = await PreparePendingStorageAsync(services, clock);
        var journal = Assert.IsType<FileSandboxWorkspaceStore>(services.GetRequiredService<ISandboxWorkspaceExecutionRunStore>());
        var before = JsonSerializer.Serialize((await journal.GetExecutionRunDetailAsync(pending.Test.Run.Id))!.Run.ToolAdmission);
        if (revokeSource) {
            await pending.Test.Owner.RevokeAsync(Revocation.Write);
        } else {
            clock.Now = clock.Now.AddHours(2);
        }
        driver.ReadFailure = null;
        var recovery = services.GetRequiredService<StoragePlacementRecoveryService>();
        var request = new StoragePlacementRecoveryCommand(await recovery.GetCurrentContextAsync(), pending.Prepared.Plan.StorageIntentId);
        var result = await recovery.ReconcileAsync(request);
        Assert.Equal(StorageStablePlacementState.Completed, result.StorageState);
        Assert.True(result.StorageReceiptPresent);
        Assert.False(result.NativeReceiptPresent);
        Assert.Equal(StoragePlacementRecoveryAction.None, result.AvailableAction);
        Assert.Equal(StoragePlacementRecoveryOwner.ProcessAsset, result.Owner);
        Assert.Equal(1, driver.StableWrites);
        await AssertAssetRowsAsync(services, pending.Prepared, 0, 0, 0);
        Assert.Equal(before, JsonSerializer.Serialize((await journal.GetExecutionRunDetailAsync(pending.Test.Run.Id))!.Run.ToolAdmission));
        var failure = await Record.ExceptionAsync(() => services.GetRequiredService<ProjectWorkbenchService>()
            .CommitProcessAssetAsync(pending.Prepared, default));
        Assert.NotNull(failure);
        await AssertAssetRowsAsync(services, pending.Prepared, 0, 0, 0);
        Assert.Equal(1, driver.StableWrites);
    }

    [Fact]
    public async Task Storage_recovery_restart_preserves_safe_projection_and_original_profile_without_reupload() {
        await using var environment = CanDoItAllTestEnvironment.Create("storage-operator-recovery");
        var original = environment.CreatePostgreSqlProfile("original");
        var other = environment.CreatePostgreSqlProfile("other");
        var clock = new NativeClock();
        var driver = PendingStorageDriver();
        var configured = StorageRecoveryHarness(clock, driver);
        var options = new TestHarnessOptions { TestEnvironment = environment, ActiveProfile = original,
            ConfigureServices = configured.ConfigureServices };
        StoragePlacementRecoveryItem initial;
        await using (var first = await TestApplication.CreateAsync(options)) {
            await using var scope = first.Services.CreateAsyncScope();
            var pending = await PreparePendingStorageAsync(scope.ServiceProvider, clock);
            var recovery = scope.ServiceProvider.GetRequiredService<StoragePlacementRecoveryService>();
            initial = await recovery.GetAsync(new(await recovery.GetCurrentContextAsync(), pending.Prepared.Plan.StorageIntentId));
            AssertSafeStorageProjection(initial, first.RootPath);
        }
        await using var restarted = await TestApplication.CreateAsync(options);
        await using var originalScope = restarted.Services.CreateAsyncScope();
        var service = originalScope.ServiceProvider.GetRequiredService<StoragePlacementRecoveryService>();
        Assert.Equal(initial, await service.GetAsync(new(initial.Context, initial.Identity.IntentId)));
        var page = await service.ListPendingAsync(new(initial.Context, initial.Identity.OriginalProjectId, Take: 1));
        Assert.Equal(initial, Assert.Single(page.Items));
        Assert.Null(page.NextOffset);
        await using var isolated = await TestApplication.CreateAsync(new() { TestEnvironment = environment, ActiveProfile = other,
            ConfigureServices = configured.ConfigureServices });
        await using var isolatedScope = isolated.Services.CreateAsyncScope();
        var foreign = isolatedScope.ServiceProvider.GetRequiredService<StoragePlacementRecoveryService>();
        Assert.Equal(StoragePlacementRecoveryFailure.StaleContext,
            (await Assert.ThrowsAsync<StoragePlacementRecoveryException>(() => foreign.GetAsync(new(initial.Context, initial.Identity.IntentId)))).Failure);
        Assert.Equal(StoragePlacementRecoveryFailure.NotFound,
            (await Assert.ThrowsAsync<StoragePlacementRecoveryException>(() => foreign.GetAsync(new(
                new(isolated.Services.GetRequiredService<ICanonicalRuntimeDatabase>().Profile.Profile.Id, isolated.Services.GetRequiredService<ICanonicalRuntimeDatabase>().Generation), initial.Identity.IntentId)))).Failure);
        Assert.Equal(1, driver.StableWrites);
    }

    [Theory]
    [InlineData(StorageRecoveryAssociationChange.RetiredAndRecreated, StoragePlacementRecoveryBlock.OriginalProjectUnavailable)]
    [InlineData(StorageRecoveryAssociationChange.Imported, StoragePlacementRecoveryBlock.ImportedHistory)]
    [InlineData(StorageRecoveryAssociationChange.StorageProjectMismatch, StoragePlacementRecoveryBlock.InvalidOwnerAssociation)]
    [InlineData(StorageRecoveryAssociationChange.StorageRetargeted, StoragePlacementRecoveryBlock.OriginalTargetConflict)]
    public async Task Storage_recovery_uses_saved_owner_association_and_cannot_rebind_authority(StorageRecoveryAssociationChange change,
        StoragePlacementRecoveryBlock expected) {
        var clock = new NativeClock();
        var driver = PendingStorageDriver();
        await using var app = await TestApplication.CreateAsync(StorageRecoveryHarness(clock, driver));
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var pending = await PreparePendingStorageAsync(services, clock);
        switch (change) {
            case StorageRecoveryAssociationChange.RetiredAndRecreated:
                await ReplaceRecoveryProjectLifetimeAsync(services, pending.Prepared.Plan.ProjectAdmission);
                break;
            case StorageRecoveryAssociationChange.Imported:
                await using (var database = await services.GetRequiredService<IDbContextFactory<WorkbenchDbContext>>().CreateDbContextAsync()) {
                    var row = await database.Set<ProjectProcessAssetContributionRecord>().SingleAsync(item => item.IntentId == pending.Prepared.Plan.Producer.IntentId.Value);
                    row.ImportedHistory = new(Guid.NewGuid(), Guid.NewGuid());
                    await database.SaveChangesAsync();
                }
                break;
            case StorageRecoveryAssociationChange.StorageProjectMismatch:
            case StorageRecoveryAssociationChange.StorageRetargeted:
                await using (var database = await services.GetRequiredService<IDbContextFactory<StorageDbContext>>().CreateDbContextAsync()) {
                    var row = await database.Set<StoragePlacementIntentRecord>().SingleAsync(item => item.Id == pending.Prepared.Plan.StorageIntentId.Value);
                    if (change == StorageRecoveryAssociationChange.StorageProjectMismatch) {
                        row.ProjectId = Guid.NewGuid();
                    } else {
                        var catalog = await database.Set<StorageCatalogRecord>().SingleAsync(item => item.Id == row.StorageId);
                        catalog.EndpointOrRoot = Path.Combine(app.RootPath, "different-target");
                    }
                    await database.SaveChangesAsync();
                }
                break;
        }
        driver.ReadFailure = null;
        await using var restartedScope = app.Services.CreateAsyncScope();
        var recovery = restartedScope.ServiceProvider.GetRequiredService<StoragePlacementRecoveryService>();
        var command = new StoragePlacementRecoveryCommand(await recovery.GetCurrentContextAsync(), pending.Prepared.Plan.StorageIntentId);
        var status = await recovery.GetAsync(command);
        Assert.Equal(expected, status.Block);
        Assert.Equal(StoragePlacementRecoveryAction.None, status.AvailableAction);
        Assert.Equal(StoragePlacementRecoveryFailure.Blocked,
            (await Assert.ThrowsAsync<StoragePlacementRecoveryException>(() => recovery.ReconcileAsync(command))).Failure);
        Assert.Equal(1, driver.StableWrites);
        await AssertAssetRowsAsync(services, pending.Prepared, 0, 0, 0);
    }

    [Fact]
    public async Task Storage_recovery_final_owner_save_denies_project_retirement_after_provider_readback() {
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
        var pending = await PreparePendingStorageAsync(services, clock);
        driver.ReadFailure = null;
        waiter.Armed = true;
        var recovery = services.GetRequiredService<StoragePlacementRecoveryService>();
        var command = new StoragePlacementRecoveryCommand(await recovery.GetCurrentContextAsync(), pending.Prepared.Plan.StorageIntentId);
        var action = recovery.ReconcileAsync(command);
        await waiter.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        try {
            await ReplaceRecoveryProjectLifetimeAsync(services, pending.Prepared.Plan.ProjectAdmission);
        } finally {
            waiter.Release.TrySetResult();
        }
        Assert.Equal(StoragePlacementRecoveryFailure.Blocked,
            (await Assert.ThrowsAsync<StoragePlacementRecoveryException>(() => action)).Failure);
        Assert.Equal(StorageStablePlacementState.Uncertain, (await recovery.GetAsync(command)).StorageState);
        Assert.False((await recovery.GetAsync(command)).StorageReceiptPresent);
        Assert.Equal(1, driver.StableWrites);
        await AssertAssetRowsAsync(services, pending.Prepared, 0, 0, 0);
    }

    [Fact]
    public async Task Workflow_storage_association_without_saved_lifetime_stays_discoverable_and_blocked() {
        var clock = new NativeClock();
        var driver = PendingStorageDriver();
        await using var app = await TestApplication.CreateAsync(StorageRecoveryHarness(clock, driver));
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var pending = await PreparePendingStorageAsync(services, clock);
        await using (var database = await services.GetRequiredService<IDbContextFactory<WorkbenchDbContext>>().CreateDbContextAsync()) {
            database.Remove(await database.Set<ProjectProcessAssetContributionRecord>().SingleAsync(item => item.IntentId == pending.Prepared.Plan.Producer.IntentId.Value));
            database.Add(new ProjectWorkflowContributionRecord { RunId = Guid.NewGuid(), OccurrencePath = "root", Slot = 0,
                ProjectId = pending.Prepared.Plan.ProjectAdmission.ProjectId, NativeObjectId = Guid.NewGuid(),
                StoragePlacementIntentId = pending.Prepared.Plan.StorageIntentId.Value, PreparedAtUtc = DateTimeOffset.UtcNow });
            await database.SaveChangesAsync();
        }
        var recovery = services.GetRequiredService<StoragePlacementRecoveryService>();
        var command = new StoragePlacementRecoveryCommand(await recovery.GetCurrentContextAsync(), pending.Prepared.Plan.StorageIntentId);
        var item = Assert.Single((await recovery.ListPendingAsync(new(command.Context))).Items);
        Assert.Equal(StoragePlacementRecoveryOwner.WorkflowAsset, item.Owner);
        Assert.Equal(StoragePlacementRecoveryBlock.OriginalProjectLifetimeMissing, item.Block);
        Assert.Equal(StoragePlacementRecoveryFailure.Blocked,
            (await Assert.ThrowsAsync<StoragePlacementRecoveryException>(() => recovery.ReconcileAsync(command))).Failure);
        Assert.Equal(1, driver.StableWrites);
    }

    [Fact]
    public async Task Storage_pending_query_applies_original_project_filter_before_bounded_page_and_rejects_excessive_take() {
        var clock = new NativeClock();
        var driver = PendingStorageDriver();
        await using var app = await TestApplication.CreateAsync(StorageRecoveryHarness(clock, driver));
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var pending = await PreparePendingStorageAsync(services, clock);
        await using (var database = await services.GetRequiredService<IDbContextFactory<StorageDbContext>>().CreateDbContextAsync()) {
            var original = await database.Set<StoragePlacementIntentRecord>().AsNoTracking().SingleAsync(item => item.Id == pending.Prepared.Plan.StorageIntentId.Value);
            for (var index = 0; index < 2; index++) {
                var id = Guid.NewGuid();
                var plan = JsonNode.Parse(original.PlanJson)!;
                plan["reference"]!["placementIntentId"] = id;
                database.Add(new StoragePlacementIntentRecord { Id = id, StorageId = original.StorageId, ProjectId = Guid.NewGuid(),
                    RequestFingerprint = original.RequestFingerprint, PlanJson = plan.ToJsonString(), State = original.State,
                    CreatedAtUtc = original.CreatedAtUtc.AddMinutes(-index - 1), UpdatedAtUtc = original.UpdatedAtUtc });
            }
            await database.SaveChangesAsync();
        }
        var recovery = services.GetRequiredService<StoragePlacementRecoveryService>();
        var context = await recovery.GetCurrentContextAsync();
        var filtered = await recovery.ListPendingAsync(new(context, pending.Prepared.Plan.ProjectAdmission.ProjectId, Take: 1));
        Assert.Equal(pending.Prepared.Plan.StorageIntentId, Assert.Single(filtered.Items).Identity.IntentId);
        Assert.Null(filtered.NextOffset);
        var first = await recovery.ListPendingAsync(new(context, Take: 1));
        Assert.Equal(StoragePlacementRecoveryBlock.MissingOwnerAssociation, Assert.Single(first.Items).Block);
        Assert.Equal(1, first.NextOffset);
        Assert.Equal(StoragePlacementRecoveryFailure.InvalidRequest,
            (await Assert.ThrowsAsync<StoragePlacementRecoveryException>(() => recovery.ListPendingAsync(new(context, Take: 129)))).Failure);
    }

    private static AssetDriverProbe PendingStorageDriver() => new() {
        AfterWriteFailure = new IOException("Private storage credential/address must not reach the recovery DTO."),
        ReadFailure = new IOException("Private provider exception.")
    };

    private static TestHarnessOptions StorageRecoveryHarness(NativeClock clock, AssetDriverProbe driver) {
        var configured = AssetHarness(clock, driver);
        return new() { ConfigureServices = services => {
            configured.ConfigureServices!(services);
            StorageRecoveryTestServices.Add(services, true);
        } };
    }

    private static async Task<(AssetCase Test, ProjectProcessAssetSnapshot Prepared)> PreparePendingStorageAsync(IServiceProvider services,
        NativeClock clock) {
        var test = await CreateAssetCaseAsync(services, clock, false);
        var owner = services.GetRequiredService<ProjectWorkbenchService>();
        var prepared = await owner.PrepareProcessAssetAsync(test.Invocation, default);
        prepared = await owner.MaterializeProcessAssetAsync(prepared, AssetMedia(), default);
        await Assert.ThrowsAsync<ProjectProcessAssetReconciliationRequiredException>(() => owner.CommitProcessAssetAsync(prepared, default));
        return (test, prepared);
    }

    private static async Task ReplaceRecoveryProjectLifetimeAsync(IServiceProvider services, ProjectWriteAdmission original) {
        await using var database = await services.GetRequiredService<IDbContextFactory<ProjectsDbContext>>().CreateDbContextAsync();
        await using var transaction = await database.Database.BeginTransactionAsync();
        var project = await database.Set<Project>().SingleAsync(item => item.Id == original.ProjectId);
        database.Add(new ProjectRetirementRecord { ProjectId = project.Id, LifetimeId = project.LifetimeId, RetiredAtUtc = DateTimeOffset.UtcNow });
        database.Remove(project);
        await database.SaveChangesAsync();
        var recreated = new Project { Id = original.ProjectId, Name = project.Name, Slug = project.Slug,
            CreatedAtUtc = DateTimeOffset.UtcNow, UpdatedAtUtc = DateTimeOffset.UtcNow };
        Assert.NotEqual(original.LifetimeId, recreated.LifetimeId);
        database.Add(recreated);
        await database.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    private static void AssertSafeStorageProjection(StoragePlacementRecoveryItem item, string rootPath) {
        var json = JsonSerializer.Serialize(item);
        Assert.DoesNotContain(rootPath, json, StringComparison.Ordinal);
        foreach (var forbidden in new[] { "PlanJson", "ReceiptJson", "Location", "RelativePath", "EndpointOrRoot", "ConfigJson", "ObservationException", "Private storage", "Private provider", "Base64Data" }) {
            Assert.DoesNotContain(forbidden, json, StringComparison.OrdinalIgnoreCase);
        }
    }

    public enum StorageRecoveryAssociationChange { RetiredAndRecreated, Imported, StorageProjectMismatch, StorageRetargeted }

    private sealed class RecoveryFinalSaveWaiter : DbCommandInterceptor {
        internal bool Armed { get; set; }
        internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default) {
            if (Armed && command.CommandText.Contains("FOR SHARE", StringComparison.Ordinal) && command.CommandText.Contains("Storage_Catalog", StringComparison.Ordinal)) {
                Armed = false;
                Assert.NotNull(command.Transaction);
                Entered.TrySetResult();
                await Release.Task.WaitAsync(cancellationToken);
            }
            return result;
        }
    }
}
