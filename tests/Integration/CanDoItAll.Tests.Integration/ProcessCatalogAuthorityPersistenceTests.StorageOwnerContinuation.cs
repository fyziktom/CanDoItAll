using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Tests.Support;
using CanDoItAll.Web.Api;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration.Processes;

public sealed partial class ProcessCatalogAuthorityPersistenceTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Storage_cancelled_run_receipts_survive_restart_without_native_or_provider_retry(bool nativeReceipt) {
        await using var environment = CanDoItAllTestEnvironment.Create("storage-cancelled-owner-receipts");
        var original = environment.CreatePostgreSqlProfile("original");
        var other = environment.CreatePostgreSqlProfile("other");
        var clock = new NativeClock();
        var driver = nativeReceipt ? new AssetDriverProbe() : PendingStorageDriver();
        var configured = StorageRecoveryHarness(clock, driver);
        var options = new TestHarnessOptions { TestEnvironment = environment, ActiveProfile = original,
            ConfigureServices = configured.ConfigureServices };
        ProjectProcessAssetSnapshot prepared;
        string segments;
        await using (var first = await TestApplication.CreateAsync(options)) {
            await using var scope = first.Services.CreateAsyncScope();
            var created = await CreateOwnerContinuationCaseAsync(scope.ServiceProvider, clock, driver, nativeReceipt);
            prepared = created.Prepared;
            await CancelOwnerContinuationRunAsync(scope.ServiceProvider, created.Test.Run.Id);
            var saved = (await scope.ServiceProvider.GetRequiredService<ISandboxWorkspaceExecutionRunStore>()
                .GetExecutionRunAsync(created.Test.Run.Id))!;
            segments = JsonSerializer.Serialize(saved.ToolAdmission!.Segments);
        }
        clock.Now = clock.Now.AddHours(2);
        await using var restarted = await TestApplication.CreateAsync(options);
        await using var restartedScope = restarted.Services.CreateAsyncScope();
        var services = restartedScope.ServiceProvider;
        var recovery = services.GetRequiredService<StoragePlacementRecoveryService>();
        var continuation = services.GetRequiredService<IStoragePlacementOwnerContinuation>();
        var command = new StoragePlacementRecoveryCommand(await recovery.GetCurrentContextAsync(), prepared.Plan.StorageIntentId);
        var before = await continuation.GetAsync(command);
        Assert.Equal(StoragePlacementOwnerContinuationState.Ready, before.State);
        Assert.Equal(StoragePlacementOwnerContinuationAction.ReconcileCancelledRunReceipts, before.AvailableAction);
        Assert.Equal(prepared.Plan.Producer.Session.ExecutionRunId, before.OriginalExecutionRunId);
        var result = await continuation.ReconcileCancelledRunReceiptsAsync(command);
        Assert.Equal(nativeReceipt ? StoragePlacementOwnerContinuationState.ReceiptRecorded
            : StoragePlacementOwnerContinuationState.NativeReceiptNotObserved, result.State);
        AssertSafeOwnerContinuation(result, restarted.RootPath);
        var store = services.GetRequiredService<ISandboxWorkspaceExecutionRunStore>();
        var run = (await store.GetExecutionRunAsync(prepared.Plan.Producer.Session.ExecutionRunId))!;
        Assert.Equal(RunOutcome.Cancelled, run.Outcome);
        Assert.Equal(segments, JsonSerializer.Serialize(run.ToolAdmission!.Segments));
        var proposals = run.ToolAdmission.Batches.SelectMany(batch => batch.Proposals).ToArray();
        var target = Assert.Single(proposals, item => item.IntentId == prepared.Plan.Producer.IntentId);
        Assert.Equal(AgentToolProposalState.Cancelled, target.State);
        Assert.Equal(nativeReceipt ? AgentToolEffectState.Committed : AgentToolEffectState.Unknown, target.EffectState);
        Assert.Equal(nativeReceipt ? AgentToolCancellationDisposition.ReceiptCommitted
            : AgentToolCancellationDisposition.CancelledUnreconciled, target.Cancellation!.Disposition);
        Assert.Equal(AgentToolCancellationDisposition.NotDispatched,
            Assert.Single(proposals, item => item.IntentId != target.IntentId).Cancellation!.Disposition);
        var replay = await continuation.ReconcileCancelledRunReceiptsAsync(command);
        Assert.Equal(result.State, replay.State);
        Assert.Equal(target, (await store.GetExecutionRunAsync(run.Id))!.ToolAdmission!.Batches
            .SelectMany(batch => batch.Proposals).Single(item => item.IntentId == target.IntentId));
        var pending = await recovery.ListPendingContinuationsAsync(new(command.Context));
        if (nativeReceipt) {
            Assert.Empty(pending.Items);
        } else {
            Assert.Equal(command.IntentId, Assert.Single(pending.Items).Storage.Identity.IntentId);
        }
        await AssertAssetRowsAsync(services, prepared, nativeReceipt ? 1 : 0, nativeReceipt ? 1 : 0, 0);
        Assert.Equal(1, driver.StableWrites);
        await using var isolated = await TestApplication.CreateAsync(new() { TestEnvironment = environment, ActiveProfile = other,
            ConfigureServices = configured.ConfigureServices });
        await using var isolatedScope = isolated.Services.CreateAsyncScope();
        var foreign = isolatedScope.ServiceProvider.GetRequiredService<IStoragePlacementOwnerContinuation>();
        Assert.Equal(StoragePlacementRecoveryFailure.StaleContext, (await Assert.ThrowsAsync<StoragePlacementRecoveryException>(() =>
            foreign.ReconcileCancelledRunReceiptsAsync(command))).Failure);
        Assert.Null(await isolatedScope.ServiceProvider.GetRequiredService<ISandboxWorkspaceExecutionRunStore>().GetExecutionRunAsync(run.Id));
    }

    [Theory]
    [InlineData(false, StoragePlacementOwnerContinuationState.OriginalRunNotCancelled)]
    [InlineData(true, StoragePlacementOwnerContinuationState.OriginalProposalUnavailable)]
    public async Task Storage_owner_continuation_does_not_resume_active_runs_or_relabel_undispatched_proposals(bool undispatched,
        StoragePlacementOwnerContinuationState expected) {
        var clock = new NativeClock();
        var driver = new AssetDriverProbe();
        await using var app = await TestApplication.CreateAsync(StorageRecoveryHarness(clock, driver));
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var created = await CreateOwnerContinuationCaseAsync(services, clock, driver, true, !undispatched);
        if (undispatched) {
            await CancelOwnerContinuationRunAsync(services, created.Test.Run.Id);
        }
        var store = services.GetRequiredService<ISandboxWorkspaceExecutionRunStore>();
        var before = JsonSerializer.Serialize((await store.GetExecutionRunAsync(created.Test.Run.Id))!.ToolAdmission);
        var command = new StoragePlacementRecoveryCommand(await services.GetRequiredService<StoragePlacementRecoveryService>()
            .GetCurrentContextAsync(), created.Prepared.Plan.StorageIntentId);
        var result = await services.GetRequiredService<IStoragePlacementOwnerContinuation>().ReconcileCancelledRunReceiptsAsync(command);
        Assert.Equal(expected, result.State);
        Assert.Equal(StoragePlacementOwnerContinuationAction.None, result.AvailableAction);
        Assert.Equal(before, JsonSerializer.Serialize((await store.GetExecutionRunAsync(created.Test.Run.Id))!.ToolAdmission));
        await AssertAssetRowsAsync(services, created.Prepared, 1, 1, 0);
        Assert.Equal(1, driver.StableWrites);
    }

    [Fact]
    public async Task Storage_cancelled_receipt_action_requires_the_original_admitted_batch_as_well_as_run_and_intent() {
        var clock = new NativeClock();
        var driver = new AssetDriverProbe();
        await using var app = await TestApplication.CreateAsync(StorageRecoveryHarness(clock, driver));
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var created = await CreateOwnerContinuationCaseAsync(services, clock, driver, true);
        await CancelOwnerContinuationRunAsync(services, created.Test.Run.Id);
        await using (var database = await services.GetRequiredService<IDbContextFactory<WorkbenchDbContext>>().CreateDbContextAsync()) {
            var row = await database.Set<ProjectProcessAssetContributionRecord>().SingleAsync(item => item.IntentId == created.Prepared.Plan.Producer.IntentId.Value);
            var changed = created.Prepared.Plan with { Producer = created.Prepared.Plan.Producer with { BatchId = new(Guid.NewGuid()) } };
            row.PlanJson = JsonSerializer.Serialize(changed, ProjectProcessAssetPersistence.Json);
            row.PlanFingerprint = ProjectProcessAssetPersistence.Hash(row.PlanJson);
            await database.SaveChangesAsync();
        }
        var store = services.GetRequiredService<ISandboxWorkspaceExecutionRunStore>();
        var before = JsonSerializer.Serialize((await store.GetExecutionRunAsync(created.Test.Run.Id))!.ToolAdmission);
        var command = new StoragePlacementRecoveryCommand(await services.GetRequiredService<StoragePlacementRecoveryService>()
            .GetCurrentContextAsync(), created.Prepared.Plan.StorageIntentId);
        var result = await services.GetRequiredService<IStoragePlacementOwnerContinuation>().ReconcileCancelledRunReceiptsAsync(command);
        Assert.Equal(StoragePlacementOwnerContinuationState.OriginalProposalUnavailable, result.State);
        Assert.Equal(StoragePlacementOwnerContinuationAction.None, result.AvailableAction);
        Assert.Equal(before, JsonSerializer.Serialize((await store.GetExecutionRunAsync(created.Test.Run.Id))!.ToolAdmission));
        Assert.Equal(1, driver.StableWrites);
    }

    [Fact]
    public async Task Storage_cached_cancelled_receipt_requires_current_read_without_destroying_its_saved_checkpoint() {
        var clock = new NativeClock();
        var driver = new AssetDriverProbe();
        await using var app = await TestApplication.CreateAsync(StorageRecoveryHarness(clock, driver));
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var created = await CreateOwnerContinuationCaseAsync(services, clock, driver, true);
        await CancelOwnerContinuationRunAsync(services, created.Test.Run.Id);
        var command = new StoragePlacementRecoveryCommand(await services.GetRequiredService<StoragePlacementRecoveryService>()
            .GetCurrentContextAsync(), created.Prepared.Plan.StorageIntentId);
        var continuation = services.GetRequiredService<IStoragePlacementOwnerContinuation>();
        Assert.Equal(StoragePlacementOwnerContinuationState.ReceiptRecorded, (await continuation.ReconcileCancelledRunReceiptsAsync(command)).State);
        var store = services.GetRequiredService<ISandboxWorkspaceExecutionRunStore>();
        var saved = JsonSerializer.Serialize((await store.GetExecutionRunAsync(created.Test.Run.Id))!.ToolAdmission);
        await created.Test.Owner.RevokeAsync(Revocation.Read);
        await using var refreshedScope = app.Services.CreateAsyncScope();
        var refreshed = refreshedScope.ServiceProvider.GetRequiredService<IStoragePlacementOwnerContinuation>();
        Assert.Equal(StoragePlacementOwnerContinuationState.CurrentReadDenied, (await refreshed.GetAsync(command)).State);
        Assert.Equal(StoragePlacementOwnerContinuationState.CurrentReadDenied, (await refreshed.ReconcileCancelledRunReceiptsAsync(command)).State);
        Assert.Equal(saved, JsonSerializer.Serialize((await store.GetExecutionRunAsync(created.Test.Run.Id))!.ToolAdmission));
        Assert.Equal(1, driver.StableWrites);
    }

    [Theory]
    [InlineData(Revocation.Write, StoragePlacementOwnerContinuationState.ReceiptRecorded)]
    [InlineData(Revocation.Read, StoragePlacementOwnerContinuationState.CurrentReadDenied)]
    public async Task Storage_cancelled_receipt_action_rechecks_original_read_authority_without_requiring_new_write_authority(
        Revocation revoke, StoragePlacementOwnerContinuationState expected) {
        var clock = new NativeClock();
        var driver = new AssetDriverProbe();
        await using var app = await TestApplication.CreateAsync(StorageRecoveryHarness(clock, driver));
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var created = await CreateOwnerContinuationCaseAsync(services, clock, driver, true);
        await CancelOwnerContinuationRunAsync(services, created.Test.Run.Id);
        var command = new StoragePlacementRecoveryCommand(await services.GetRequiredService<StoragePlacementRecoveryService>()
            .GetCurrentContextAsync(), created.Prepared.Plan.StorageIntentId);
        var continuation = services.GetRequiredService<IStoragePlacementOwnerContinuation>();
        Assert.Equal(StoragePlacementOwnerContinuationState.Ready, (await continuation.GetAsync(command)).State);
        await created.Test.Owner.RevokeAsync(revoke);
        var before = JsonSerializer.Serialize((await services.GetRequiredService<ISandboxWorkspaceExecutionRunStore>()
            .GetExecutionRunAsync(created.Test.Run.Id))!.ToolAdmission);
        var result = await continuation.ReconcileCancelledRunReceiptsAsync(command);
        Assert.Equal(expected, result.State);
        if (revoke == Revocation.Read) {
            Assert.Null(result.OriginalExecutionRunId);
            Assert.Equal(before, JsonSerializer.Serialize((await services.GetRequiredService<ISandboxWorkspaceExecutionRunStore>()
                .GetExecutionRunAsync(created.Test.Run.Id))!.ToolAdmission));
        }
        Assert.Equal(1, driver.StableWrites);
    }

    [Theory]
    [InlineData(false, StoragePlacementRecoveryBlock.OriginalProjectUnavailable)]
    [InlineData(true, StoragePlacementRecoveryBlock.ImportedHistory)]
    public async Task Storage_cancelled_receipt_action_rejects_recreated_project_and_imported_source(bool imported,
        StoragePlacementRecoveryBlock block) {
        var clock = new NativeClock();
        var driver = PendingStorageDriver();
        await using var app = await TestApplication.CreateAsync(StorageRecoveryHarness(clock, driver));
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var created = await CreateOwnerContinuationCaseAsync(services, clock, driver, false);
        await CancelOwnerContinuationRunAsync(services, created.Test.Run.Id);
        if (imported) {
            await using var database = await services.GetRequiredService<IDbContextFactory<WorkbenchDbContext>>().CreateDbContextAsync();
            var row = await database.Set<ProjectProcessAssetContributionRecord>().SingleAsync(item => item.IntentId == created.Prepared.Plan.Producer.IntentId.Value);
            row.ImportedHistory = new(Guid.NewGuid(), Guid.NewGuid());
            await database.SaveChangesAsync();
        } else {
            await ReplaceRecoveryProjectLifetimeAsync(services, created.Prepared.Plan.ProjectAdmission);
        }
        var store = services.GetRequiredService<ISandboxWorkspaceExecutionRunStore>();
        var before = JsonSerializer.Serialize((await store.GetExecutionRunAsync(created.Test.Run.Id))!.ToolAdmission);
        var command = new StoragePlacementRecoveryCommand(await services.GetRequiredService<StoragePlacementRecoveryService>()
            .GetCurrentContextAsync(), created.Prepared.Plan.StorageIntentId);
        var result = await services.GetRequiredService<IStoragePlacementOwnerContinuation>().ReconcileCancelledRunReceiptsAsync(command);
        Assert.Equal(StoragePlacementOwnerContinuationState.OriginalOwnerBlocked, result.State);
        Assert.Equal(block, result.Storage.Block);
        Assert.Equal(StoragePlacementOwnerContinuationAction.None, result.AvailableAction);
        Assert.Equal(before, JsonSerializer.Serialize((await store.GetExecutionRunAsync(created.Test.Run.Id))!.ToolAdmission));
        await AssertAssetRowsAsync(services, created.Prepared, 0, 0, 0);
        Assert.Equal(1, driver.StableWrites);
    }

    [Fact]
    public async Task Storage_Workflow_continuation_keeps_missing_original_lifetime_explicit() {
        var clock = new NativeClock();
        var driver = PendingStorageDriver();
        await using var app = await TestApplication.CreateAsync(StorageRecoveryHarness(clock, driver));
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var created = await CreateOwnerContinuationCaseAsync(services, clock, driver, false);
        await using (var database = await services.GetRequiredService<IDbContextFactory<WorkbenchDbContext>>().CreateDbContextAsync()) {
            database.Remove(await database.Set<ProjectProcessAssetContributionRecord>().SingleAsync(item => item.IntentId == created.Prepared.Plan.Producer.IntentId.Value));
            database.Add(new ProjectWorkflowContributionRecord { RunId = Guid.NewGuid(), OccurrencePath = "root", Slot = 0,
                ProjectId = created.Prepared.Plan.ProjectAdmission.ProjectId, NativeObjectId = Guid.NewGuid(),
                StoragePlacementIntentId = created.Prepared.Plan.StorageIntentId.Value, PreparedAtUtc = clock.Now,
                PlanJson = "Private historical plan is not a current lifetime admission." });
            await database.SaveChangesAsync();
        }
        var command = new StoragePlacementRecoveryCommand(await services.GetRequiredService<StoragePlacementRecoveryService>()
            .GetCurrentContextAsync(), created.Prepared.Plan.StorageIntentId);
        var result = await services.GetRequiredService<IStoragePlacementOwnerContinuation>().ReconcileCancelledRunReceiptsAsync(command);
        Assert.Equal(StoragePlacementOwnerContinuationState.OriginalOwnerBlocked, result.State);
        Assert.Equal(StoragePlacementRecoveryBlock.OriginalProjectLifetimeMissing, result.Storage.Block);
        Assert.Equal(StoragePlacementOwnerContinuationAction.None, result.AvailableAction);
        AssertSafeOwnerContinuation(result, app.RootPath);
        Assert.Equal(1, driver.StableWrites);
    }

    private static async Task<(AssetCase Test, ProjectProcessAssetSnapshot Prepared)> CreateOwnerContinuationCaseAsync(
        IServiceProvider services, NativeClock clock, AssetDriverProbe driver, bool nativeReceipt, bool dispatched = true) {
        var test = await CreateAssetCaseAsync(services, clock, false);
        await using var lease = await test.Journal.AcquireRunAsync(test.Run.ToolAdmission!.Session.Reference, default);
        using var bound = lease.Bind();
        var envelope = CanDoItAll.Tests.Integration.Runtime.AgentToolAdmissionJournalFixture.Envelope();
        await test.Journal.BeginSegmentAsync(lease, envelope, null, default);
        var batch = Assert.Single((await test.Journal.AdmitBatchAsync(lease, test.Payload.Digest, envelope,
            [new("asset", test.Payload, false), new("undispatched-sibling", test.Payload, false)], default)).Batches);
        var proposal = Assert.Single(batch.Proposals, item => item.CallId == "asset");
        if (dispatched) {
            await test.Journal.ClaimInvocationAsync(lease, batch.Id, "asset", test.Payload, default);
        }
        var admitted = new AgentToolAdmittedInvocation(test.Run.ToolAdmission.Session.Reference, batch.Id, proposal.IntentId,
            test.Payload, ExecutionApprovalStatus.Approved, test.Payload.Digest);
        test = test with { Invocation = new(admitted, test.Invocation.Execution, test.Invocation.ParentNodeKey) };
        var owner = services.GetRequiredService<ProjectWorkbenchService>();
        var prepared = await owner.PrepareProcessAssetAsync(test.Invocation, default);
        prepared = await owner.MaterializeProcessAssetAsync(prepared, AssetMedia(), default);
        if (nativeReceipt) {
            await owner.CommitProcessAssetAsync(prepared, default);
        } else {
            await Assert.ThrowsAsync<ProjectProcessAssetReconciliationRequiredException>(() => owner.CommitProcessAssetAsync(prepared, default));
            driver.ReadFailure = null;
            var completed = await services.GetRequiredService<StorageStablePlacementService>().ReconcileAsync(prepared.Plan.StorageIntentId);
            Assert.Equal(StorageStablePlacementState.Completed, completed.State);
        }
        return (test, prepared);
    }

    private static async Task CancelOwnerContinuationRunAsync(IServiceProvider services, Guid runId) {
        var store = (ISandboxWorkspaceExecutionRunMutationStore)services.GetRequiredService<ISandboxWorkspaceExecutionRunStore>();
        await store.UpdateExecutionRunDetailAsync(runId, detail => detail with { Run = detail.Run with {
            State = ExecutionState.Failed, Outcome = RunOutcome.Cancelled, CompletedAtUtc = DateTimeOffset.UtcNow,
            Revision = detail.Run.Revision + 1
        } });
    }

    private static void AssertSafeOwnerContinuation(StoragePlacementOwnerContinuationObservation value, string rootPath) {
        AssertSafeStorageProjection(value.Storage, rootPath);
        var json = JsonSerializer.Serialize(value);
        foreach (var forbidden in new[] { "OriginalSession", "OriginalProposal", "ArgumentsJson", "CommittedEffect", "Private", rootPath }) {
            Assert.DoesNotContain(forbidden, json, StringComparison.Ordinal);
        }
    }
}
