using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration.Processes;

public sealed partial class ProcessCatalogAuthorityPersistenceTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Completed_Storage_continuation_survives_host_restart_and_retains_native_or_Core_pending_state(bool nativeReceipt) {
        await using var environment = CanDoItAllTestEnvironment.Create("storage-continuation-restart");
        var original = environment.CreatePostgreSqlProfile("original");
        var other = environment.CreatePostgreSqlProfile("other");
        var clock = new NativeClock();
        var driver = nativeReceipt ? new AssetDriverProbe() : PendingStorageDriver();
        var configured = StorageRecoveryHarness(clock, driver);
        var options = new TestHarnessOptions { TestEnvironment = environment, ActiveProfile = original,
            ConfigureServices = configured.ConfigureServices };
        ProjectProcessAssetSnapshot prepared;
        StoragePlacementContinuationItem firstItem;
        string journalBefore;
        Guid executionRunId;
        await using (var first = await TestApplication.CreateAsync(options)) {
            await using var scope = first.Services.CreateAsyncScope();
            var services = scope.ServiceProvider;
            var recovery = services.GetRequiredService<StoragePlacementRecoveryService>();
            if (nativeReceipt) {
                var created = await CreateAcknowledgementCaseAsync(services, clock, ContinuationCheckpoint.Executing);
                prepared = created.Prepared;
                executionRunId = created.Test.Run.Id;
            } else {
                var pending = await PreparePendingStorageAsync(services, clock);
                prepared = pending.Prepared;
                executionRunId = pending.Test.Run.Id;
                driver.ReadFailure = null;
                await recovery.ReconcileAsync(new(await recovery.GetCurrentContextAsync(), prepared.Plan.StorageIntentId));
            }
            firstItem = Assert.Single((await recovery.ListPendingContinuationsAsync(new(await recovery.GetCurrentContextAsync()))).Items);
            Assert.Equal(nativeReceipt ? StoragePlacementContinuationPhase.CoreCheckpoint : StoragePlacementContinuationPhase.NativeCommit, firstItem.Phase);
            Assert.Equal(StorageStablePlacementState.Completed, firstItem.Storage.StorageState);
            Assert.True(firstItem.Storage.StorageReceiptPresent);
            Assert.Equal(nativeReceipt, firstItem.Storage.NativeReceiptPresent);
            Assert.Equal(StoragePlacementRecoveryAction.None, firstItem.Storage.AvailableAction);
            Assert.Empty((await recovery.ListPendingAsync(new(firstItem.Storage.Context))).Items);
            journalBefore = JsonSerializer.Serialize((await services.GetRequiredService<ISandboxWorkspaceExecutionRunStore>()
                .GetExecutionRunAsync(executionRunId))!.ToolAdmission);
        }
        clock.Now = clock.Now.AddHours(2);
        await using var restarted = await TestApplication.CreateAsync(options);
        await using var restartedScope = restarted.Services.CreateAsyncScope();
        var restartedServices = restartedScope.ServiceProvider;
        var service = restartedServices.GetRequiredService<StoragePlacementRecoveryService>();
        var context = await service.GetCurrentContextAsync();
        var item = Assert.Single((await service.ListPendingContinuationsAsync(new(context))).Items);
        Assert.Equal(firstItem with { Storage = firstItem.Storage with { Context = context } }, item);
        AssertSafeStorageProjection(item.Storage, restarted.RootPath);
        Assert.Equal(journalBefore, JsonSerializer.Serialize((await restartedServices.GetRequiredService<ISandboxWorkspaceExecutionRunStore>()
            .GetExecutionRunAsync(executionRunId))!.ToolAdmission));
        await AssertAssetRowsAsync(restartedServices, prepared, nativeReceipt ? 1 : 0, nativeReceipt ? 1 : 0, 0);
        if (!nativeReceipt) {
            Assert.NotNull(await Record.ExceptionAsync(() => restartedServices.GetRequiredService<ProjectWorkbenchService>()
                .CommitProcessAssetAsync(prepared, default)));
            Assert.Single((await service.ListPendingContinuationsAsync(new(context))).Items);
        }
        await using var isolated = await TestApplication.CreateAsync(new() { TestEnvironment = environment, ActiveProfile = other,
            ConfigureServices = configured.ConfigureServices });
        await using var isolatedScope = isolated.Services.CreateAsyncScope();
        var foreign = isolatedScope.ServiceProvider.GetRequiredService<StoragePlacementRecoveryService>();
        Assert.Equal(StoragePlacementRecoveryFailure.StaleContext, (await Assert.ThrowsAsync<StoragePlacementRecoveryException>(() =>
            foreign.ListPendingContinuationsAsync(new(context)))).Failure);
        Assert.Empty((await foreign.ListPendingContinuationsAsync(new(await foreign.GetCurrentContextAsync()))).Items);
        Assert.Equal(1, driver.StableWrites);
    }

    [Theory]
    [InlineData(ContinuationCheckpoint.Missing, StoragePlacementContinuationPhase.OwnerEvidenceUnavailable)]
    [InlineData(ContinuationCheckpoint.Executing, StoragePlacementContinuationPhase.CoreCheckpoint)]
    [InlineData(ContinuationCheckpoint.CompletedUnknown, StoragePlacementContinuationPhase.OwnerEvidenceUnavailable)]
    [InlineData(ContinuationCheckpoint.CompletedCommitted, null)]
    public async Task Native_receipt_is_removed_from_recovery_only_after_its_exact_Core_committed_checkpoint(
        ContinuationCheckpoint checkpoint, StoragePlacementContinuationPhase? expected) {
        var clock = new NativeClock();
        var driver = new AssetDriverProbe();
        await using var app = await TestApplication.CreateAsync(StorageRecoveryHarness(clock, driver));
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var created = await CreateAcknowledgementCaseAsync(services, clock, checkpoint);
        var store = services.GetRequiredService<ISandboxWorkspaceExecutionRunStore>();
        var before = JsonSerializer.Serialize((await store.GetExecutionRunAsync(created.Test.Run.Id))!.ToolAdmission);
        var recovery = services.GetRequiredService<StoragePlacementRecoveryService>();
        var page = await recovery.ListPendingContinuationsAsync(new(await recovery.GetCurrentContextAsync()));
        if (expected is { } phase) {
            var item = Assert.Single(page.Items);
            Assert.Equal(phase, item.Phase);
            Assert.True(item.Storage.NativeReceiptPresent);
            Assert.Equal(created.Prepared.Plan.StorageIntentId, item.Storage.Identity.IntentId);
            Assert.Equal(StoragePlacementRecoveryAction.None, item.Storage.AvailableAction);
        } else {
            Assert.Empty(page.Items);
        }
        Assert.Null(page.NextOffset);
        Assert.Equal(before, JsonSerializer.Serialize((await store.GetExecutionRunAsync(created.Test.Run.Id))!.ToolAdmission));
        Assert.Equal(1, driver.StableWrites);
        await AssertAssetRowsAsync(services, created.Prepared, 1, 1, 0);
    }

    [Fact]
    public async Task Continuation_scan_advances_empty_pages_without_loading_unassociated_completed_Storage_plans() {
        var clock = new NativeClock();
        var driver = PendingStorageDriver();
        await using var app = await TestApplication.CreateAsync(StorageRecoveryHarness(clock, driver));
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var pending = await PreparePendingStorageAsync(services, clock);
        var recovery = services.GetRequiredService<StoragePlacementRecoveryService>();
        var context = await recovery.GetCurrentContextAsync();
        driver.ReadFailure = null;
        await recovery.ReconcileAsync(new(context, pending.Prepared.Plan.StorageIntentId));
        await using (var database = await services.GetRequiredService<IDbContextFactory<WorkbenchDbContext>>().CreateDbContextAsync()) {
            database.Add(new ProjectProcessAssetContributionRecord { IntentId = Guid.NewGuid(), DatabaseProfileId = context.DatabaseProfileId,
                ProjectId = Guid.NewGuid(), ProjectLifetimeId = Guid.NewGuid(), SourceExecutionRunId = Guid.NewGuid(),
                NativeObjectId = Guid.NewGuid(), StorageIntentId = Guid.Parse("00000000-0000-0000-0000-000000000001"),
                PlanJson = "{}", PlanFingerprint = new string('a', 64), PreparedAtUtc = clock.Now });
            await database.SaveChangesAsync();
        }
        await using (var database = await services.GetRequiredService<IDbContextFactory<StorageDbContext>>().CreateDbContextAsync()) {
            var original = await database.Set<StoragePlacementIntentRecord>().SingleAsync(row => row.Id == pending.Prepared.Plan.StorageIntentId.Value);
            database.Add(new StoragePlacementIntentRecord { Id = Guid.Parse("00000000-0000-0000-0000-000000000002"),
                StorageId = original.StorageId, ProjectId = original.ProjectId, RequestFingerprint = original.RequestFingerprint,
                PlanJson = "Unassociated completed plan must never be parsed.", State = StorageStablePlacementState.Completed,
                CreatedAtUtc = original.CreatedAtUtc, UpdatedAtUtc = original.UpdatedAtUtc });
            await database.SaveChangesAsync();
        }
        var empty = await recovery.ListPendingContinuationsAsync(new(context, Take: 1));
        Assert.Empty(empty.Items);
        Assert.Equal(1, empty.NextOffset);
        var reached = await recovery.ListPendingContinuationsAsync(new(context, Take: 1, Offset: empty.NextOffset!.Value));
        Assert.Equal(pending.Prepared.Plan.StorageIntentId, Assert.Single(reached.Items).Storage.Identity.IntentId);
        Assert.Null(reached.NextOffset);
        var filtered = await recovery.ListPendingContinuationsAsync(new(context, pending.Prepared.Plan.ProjectAdmission.ProjectId, Take: 1));
        Assert.Equal(pending.Prepared.Plan.StorageIntentId, Assert.Single(filtered.Items).Storage.Identity.IntentId);
        Assert.Null(filtered.NextOffset);
        Assert.Equal(StoragePlacementRecoveryFailure.InvalidRequest, (await Assert.ThrowsAsync<StoragePlacementRecoveryException>(() =>
            recovery.ListPendingContinuationsAsync(new(context, Take: 129)))).Failure);
        Assert.Equal(1, driver.StableWrites);
    }

    [Theory]
    [InlineData(false, false, false, StoragePlacementContinuationPhase.NativeCommit)]
    [InlineData(true, false, false, StoragePlacementContinuationPhase.WorkflowAcknowledgement)]
    [InlineData(true, true, false, null)]
    [InlineData(true, true, true, StoragePlacementContinuationPhase.OwnerEvidenceUnavailable)]
    public async Task Workflow_continuation_observes_exact_manifest_acknowledgement_without_rebinding_legacy_lifetime(
        bool nativeReceipt, bool complete, bool wrongOccurrence, StoragePlacementContinuationPhase? expected) {
        var clock = new NativeClock();
        var driver = PendingStorageDriver();
        await using var app = await TestApplication.CreateAsync(StorageRecoveryHarness(clock, driver));
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var pending = await PreparePendingStorageAsync(services, clock);
        var recovery = services.GetRequiredService<StoragePlacementRecoveryService>();
        var context = await recovery.GetCurrentContextAsync();
        driver.ReadFailure = null;
        await recovery.ReconcileAsync(new(context, pending.Prepared.Plan.StorageIntentId));
        var runId = Guid.NewGuid();
        await using (var database = await services.GetRequiredService<IDbContextFactory<WorkbenchDbContext>>().CreateDbContextAsync()) {
            database.Remove(await database.Set<ProjectProcessAssetContributionRecord>().SingleAsync(row => row.IntentId == pending.Prepared.Plan.Producer.IntentId.Value));
            database.Add(new ProjectWorkflowContributionRecord { RunId = runId, OccurrencePath = "root", Slot = 0,
                ProjectId = pending.Prepared.Plan.ProjectAdmission.ProjectId, NativeObjectId = Guid.NewGuid(),
                StoragePlacementIntentId = pending.Prepared.Plan.StorageIntentId.Value, PreparedAtUtc = clock.Now,
                PlanJson = "{\"privatePlan\":\"not part of the observation contract\"}",
                ReceiptJson = nativeReceipt ? "{\"privateReceipt\":\"not part of the observation contract\"}" : "" });
            await database.SaveChangesAsync();
        }
        await using (var database = await services.GetRequiredService<IDbContextFactory<WorkflowDbContext>>().CreateDbContextAsync()) {
            database.Add(new WorkflowStructureOutputRecord { RunId = runId, OccurrencePath = wrongOccurrence ? "other" : "root", Slot = 0,
                StoragePlacementIntentId = pending.Prepared.Plan.StorageIntentId.Value, PlanJson = "{\"privatePlan\":true}",
                IsComplete = complete, ReceiptJson = complete ? "{\"privateReceipt\":true}" : "", NextInspectionAtUtc = clock.Now });
            await database.SaveChangesAsync();
        }
        await using var restartedScope = app.Services.CreateAsyncScope();
        var page = await restartedScope.ServiceProvider.GetRequiredService<StoragePlacementRecoveryService>()
            .ListPendingContinuationsAsync(new(context));
        if (expected is { } phase) {
            var item = Assert.Single(page.Items);
            Assert.Equal(phase, item.Phase);
            Assert.Equal(StoragePlacementRecoveryOwner.WorkflowAsset, item.Storage.Owner);
            Assert.Equal(StoragePlacementRecoveryBlock.OriginalProjectLifetimeMissing, item.Storage.Block);
            Assert.Equal(StoragePlacementRecoveryAction.None, item.Storage.AvailableAction);
            Assert.DoesNotContain("private", JsonSerializer.Serialize(page), StringComparison.OrdinalIgnoreCase);
        } else {
            Assert.Empty(page.Items);
        }
        Assert.Equal(1, driver.StableWrites);
    }

    [Theory]
    [InlineData(false, StoragePlacementRecoveryBlock.OriginalProjectUnavailable)]
    [InlineData(true, StoragePlacementRecoveryBlock.ImportedHistory)]
    public async Task Completed_Storage_continuation_retains_original_project_or_import_block(bool imported,
        StoragePlacementRecoveryBlock expected) {
        var clock = new NativeClock();
        var driver = PendingStorageDriver();
        await using var app = await TestApplication.CreateAsync(StorageRecoveryHarness(clock, driver));
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var pending = await PreparePendingStorageAsync(services, clock);
        var recovery = services.GetRequiredService<StoragePlacementRecoveryService>();
        var context = await recovery.GetCurrentContextAsync();
        driver.ReadFailure = null;
        await recovery.ReconcileAsync(new(context, pending.Prepared.Plan.StorageIntentId));
        if (imported) {
            await using var database = await services.GetRequiredService<IDbContextFactory<WorkbenchDbContext>>().CreateDbContextAsync();
            var row = await database.Set<ProjectProcessAssetContributionRecord>().SingleAsync(item => item.IntentId == pending.Prepared.Plan.Producer.IntentId.Value);
            row.ImportedHistory = new(Guid.NewGuid(), Guid.NewGuid());
            await database.SaveChangesAsync();
        } else {
            await ReplaceRecoveryProjectLifetimeAsync(services, pending.Prepared.Plan.ProjectAdmission);
        }
        var item = Assert.Single((await recovery.ListPendingContinuationsAsync(new(context))).Items);
        Assert.Equal(expected, item.Storage.Block);
        Assert.Equal(StoragePlacementRecoveryAction.None, item.Storage.AvailableAction);
        Assert.Equal(StoragePlacementRecoveryFailure.Blocked, (await Assert.ThrowsAsync<StoragePlacementRecoveryException>(() =>
            recovery.ReconcileAsync(new(context, item.Storage.Identity.IntentId)))).Failure);
        Assert.Equal(1, driver.StableWrites);
        await AssertAssetRowsAsync(services, pending.Prepared, 0, 0, 0);
    }

    private static async Task<(AssetCase Test, ProjectProcessAssetSnapshot Prepared)> CreateAcknowledgementCaseAsync(
        IServiceProvider services, NativeClock clock, ContinuationCheckpoint checkpoint) {
        var test = await CreateAssetCaseAsync(services, clock, false);
        var workbench = services.GetRequiredService<ProjectWorkbenchService>();
        if (checkpoint == ContinuationCheckpoint.Missing) {
            var missing = await workbench.PrepareProcessAssetAsync(test.Invocation, default);
            missing = await workbench.MaterializeProcessAssetAsync(missing, AssetMedia(), default);
            await workbench.CommitProcessAssetAsync(missing, default);
            return (test, missing);
        }
        await using var lease = await test.Journal.AcquireRunAsync(test.Run.ToolAdmission!.Session.Reference, default);
        using var bound = lease.Bind();
        var envelope = CanDoItAll.Tests.Integration.Runtime.AgentToolAdmissionJournalFixture.Envelope();
        await test.Journal.BeginSegmentAsync(lease, envelope, null, default);
        var batch = Assert.Single((await test.Journal.AdmitBatchAsync(lease, test.Payload.Digest, envelope,
            [new("asset", test.Payload, false)], default)).Batches);
        var claim = await test.Journal.ClaimInvocationAsync(lease, batch.Id, "asset", test.Payload, default);
        using var dispatch = claim.Bind();
        var admitted = new AgentToolAdmittedInvocation(test.Run.ToolAdmission.Session.Reference, batch.Id, claim.Proposal.IntentId,
            test.Payload, ExecutionApprovalStatus.Approved, test.Payload.Digest);
        test = test with { Invocation = new(admitted, test.Invocation.Execution, test.Invocation.ParentNodeKey) };
        var prepared = await workbench.PrepareProcessAssetAsync(test.Invocation, default);
        prepared = await workbench.MaterializeProcessAssetAsync(prepared, AssetMedia(), default);
        await workbench.CommitProcessAssetAsync(prepared, default);
        if (checkpoint is ContinuationCheckpoint.CompletedUnknown or ContinuationCheckpoint.CompletedCommitted) {
            await test.Journal.CompleteInvocationAsync(claim, envelope, checkpoint == ContinuationCheckpoint.CompletedCommitted
                ? AgentToolEffectState.Committed : AgentToolEffectState.Unknown, default);
        }
        return (test, prepared);
    }

    public enum ContinuationCheckpoint { Missing, Executing, CompletedUnknown, CompletedCommitted }
}
